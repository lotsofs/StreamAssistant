using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Xunit;

namespace StreamAssistant2.Tests {
	// Runs the real StreamElementsSocket.StartConnectionLoop against a fake Astro gateway on loopback.
	// Events go to a capture, never to the real handlers, so no anthem, TTS or chat line.
	public class StreamElementsLoopTests : IAsyncLifetime {
		sealed record Connection(WebSocket Socket, string Query);

		sealed class FakeAstro : IDisposable {
			readonly HttpListener _listener = new();
			readonly Channel<Connection> _connections = Channel.CreateUnbounded<Connection>();
			public readonly int Port;

			public FakeAstro() {
				var probe = new TcpListener(IPAddress.Loopback, 0);
				probe.Start();
				Port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();
				_listener.Prefixes.Add($"http://localhost:{Port}/");
				_listener.Start();
				_ = Task.Run(ServeAsync);
			}

			public string Url => $"ws://localhost:{Port}/";

			async Task ServeAsync() {
				while (_listener.IsListening) {
					HttpListenerContext ctx;
					try {
						ctx = await _listener.GetContextAsync();
					}
					catch {
						return;
					}
					if (!ctx.Request.IsWebSocketRequest) {
						ctx.Response.StatusCode = 400;
						ctx.Response.Close();
						continue;
					}
					string query = ctx.Request.Url?.Query ?? "";
					var ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;
					_connections.Writer.TryWrite(new Connection(ws, query));
				}
			}

			public async Task<Connection> NextConnectionAsync(TimeSpan timeout) {
				using var cts = new CancellationTokenSource(timeout);
				return await _connections.Reader.ReadAsync(cts.Token);
			}

			public bool HasAnotherConnection() {
				return _connections.Reader.TryPeek(out _);
			}

			public void Dispose() {
				_listener.Close();
			}
		}

		const string JWT = "fake-jwt-0123456789";
		const string ROOM = "room-1";
		const string RECONNECT_TOKEN = "tok-123";
		static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

		static Task SendAsync(WebSocket ws, string text) {
			return ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		// One whole frame the bot sent, parsed
		static async Task<JsonElement> ReceiveJsonAsync(WebSocket ws) {
			using var cts = new CancellationTokenSource(Wait);
			var buffer = new byte[8192];
			var sb = new StringBuilder();
			WebSocketReceiveResult result;
			do {
				result = await ws.ReceiveAsync(buffer, cts.Token);
				sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
			}
			while (!result.EndOfMessage);
			return JsonDocument.Parse(sb.ToString()).RootElement.Clone();
		}

		// Shapes from docs.streamelements.com/websockets
		static string Welcome() => """{"id":"01","ts":"2026-10-10T12:00:00Z","type":"welcome","data":{"message":"You are in a maze of dank memes, all alike.","client_id":"c1"}}""";
		static string Ack(string nonce) => $$$"""{"id":"02","type":"response","nonce":"{{{nonce}}}","data":{"message":"successfully subscribed to topic","topic":"channel.tips","room":"{{{ROOM}}}"}}""";
		static string Refusal(string nonce, string error, string message = "nope") => $$$"""{"id":"03","type":"response","nonce":"{{{nonce}}}","error":"{{{error}}}","data":{"message":"{{{message}}}"}}""";
		// What Astro really answers to a subscribe for a topic already subscribed (seen live 2026-10-10)
		static string AlreadySubscribed(string nonce) => Refusal(nonce, "err_bad_request", "already subscribed to topic");
		static string Tip(string id) => $$$"""{"id":"04","type":"message","topic":"channel.tips","room":"{{{ROOM}}}","data":{"_id":"{{{id}}}"}}""";
		static string Reconnect() => $$$"""{"type":"reconnect","data":{"message":"The server is shutting down.","reconnect_token":"{{{RECONNECT_TOKEN}}}"}}""";

		static readonly StreamElementsSocket.Timings _times = new(TimeSpan.FromHours(1), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));

		FakeAstro _astro = null!;
		readonly Channel<(string Topic, string Id)> _events = Channel.CreateUnbounded<(string, string)>();
		readonly List<string> _log = new();
		CancellationTokenSource _cts = null!;
		Task? _loop;

		string _savedUrl = "";
		StreamElementsSocket.Timings _savedTimes = null!;
		Action<string, JsonElement> _savedHandler = null!;
		StreamElementsConfig _savedConfig = null!;

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		List<string> Log { get { lock (_log) return _log.ToList(); } }

		public Task InitializeAsync() {
			_astro = new FakeAstro();
			_savedUrl = StreamElementsSocket.AstroUrl;
			_savedTimes = StreamElementsSocket.Times;
			_savedHandler = StreamElementsSocket.NotificationHandler;
			_savedConfig = Config.Data.StreamElements;

			StreamElementsSocket.AstroUrl = _astro.Url;
			StreamElementsSocket.Times = _times;
			// Read now: the JsonDocument behind data is gone once the handler returns.
			StreamElementsSocket.NotificationHandler = (topic, data) => _events.Writer.TryWrite((topic, data.ReadString("_id")));
			Config.Data.StreamElements = new StreamElementsConfig { ChannelId = ROOM, Jwt = JWT };
			ConsoleLogger.LineLogged += OnLine;

			_cts = new CancellationTokenSource();
			return Task.CompletedTask;
		}

		public async Task DisposeAsync() {
			_cts.Cancel();
			if (_loop != null) {
				try {
					await _loop;
				}
				catch (OperationCanceledException) {
				}
			}
			ConsoleLogger.LineLogged -= OnLine;
			StreamElementsSocket.AstroUrl = _savedUrl;
			StreamElementsSocket.Times = _savedTimes;
			StreamElementsSocket.NotificationHandler = _savedHandler;
			Config.Data.StreamElements = _savedConfig;
			_astro.Dispose();
			_cts.Dispose();
		}

		void Start() {
			_loop = StreamElementsSocket.StartConnectionLoop(_cts.Token);
		}

		async Task<(string Topic, string Id)> NextEventAsync() {
			using var cts = new CancellationTokenSource(Wait);
			return await _events.Reader.ReadAsync(cts.Token);
		}

		static async Task WaitUntilAsync(Func<bool> condition, string what) {
			var deadline = DateTime.UtcNow + Wait;
			while (!condition()) {
				Assert.True(DateTime.UtcNow < deadline, $"timed out waiting for {what}");
				await Task.Delay(20);
			}
		}

		// Next connection, welcomed; returns it with the bot's first frame
		async Task<(Connection Connection, JsonElement Frame)> StartSessionAsync() {
			Connection connection = await _astro.NextConnectionAsync(Wait);
			await SendAsync(connection.Socket, Welcome());
			return (connection, await ReceiveJsonAsync(connection.Socket));
		}

		// Next connection, welcomed and its subscribe acknowledged
		async Task<Connection> ConnectedSessionAsync() {
			var (connection, frame) = await StartSessionAsync();
			await SendAsync(connection.Socket, Ack(frame.ReadString("nonce")));
			await WaitUntilAsync(() => StreamElementsSocket.IsConnected, "IsConnected");
			return connection;
		}

		[Fact]
		public async Task Welcome_SubscribesToTipsWithRoomAndToken() {
			Start();
			var (connection, frame) = await StartSessionAsync();
			Assert.Equal("", connection.Query);
			Assert.Equal("subscribe", frame.ReadString("type"));
			Assert.Equal("subscribe", frame.ReadString("nonce"));
			Assert.Equal("channel.tips", frame.ReadString("data.topic"));
			Assert.Equal(ROOM, frame.ReadString("data.room"));
			Assert.Equal(JWT, frame.ReadString("data.token"));
			Assert.Equal("jwt", frame.ReadString("data.token_type"));
		}

		[Fact]
		public async Task Acknowledged_ConnectsAndDeliversTips() {
			Start();
			Connection connection = await ConnectedSessionAsync();
			Assert.Contains(Log, l => l.Contains("StreamElements subscribed to channel.tips"));

			await SendAsync(connection.Socket, Tip("t1"));
			Assert.Equal(("channel.tips", "t1"), await NextEventAsync());
		}

		[Fact]
		public async Task Probes_ResubscribeOnTheInterval() {
			StreamElementsSocket.Times = _times with { Probe = TimeSpan.FromMilliseconds(200) };
			Start();
			var (connection, first) = await StartSessionAsync();
			Assert.Equal("subscribe", first.ReadString("nonce"));
			Assert.Equal("probe-1", (await ReceiveJsonAsync(connection.Socket)).ReadString("nonce"));
			JsonElement second = await ReceiveJsonAsync(connection.Socket);
			Assert.Equal("probe-2", second.ReadString("nonce"));
			Assert.Equal("channel.tips", second.ReadString("data.topic"));
			Assert.Equal(JWT, second.ReadString("data.token"));
		}

		// A refused probe isn't a refused subscribe: the session must carry on.
		[Fact]
		public async Task ProbeError_KeepsTheSession() {
			StreamElementsSocket.Times = _times with { Probe = TimeSpan.FromMilliseconds(200) };
			Start();
			Connection connection = await ConnectedSessionAsync();
			JsonElement probe = await ReceiveJsonAsync(connection.Socket);
			await SendAsync(connection.Socket, Refusal(probe.ReadString("nonce"), "err_bad_request"));

			await SendAsync(connection.Socket, Tip("t2"));
			Assert.Equal(("channel.tips", "t2"), await NextEventAsync());
			Assert.True(StreamElementsSocket.IsConnected);
			Assert.False(_astro.HasAnotherConnection());
			Assert.DoesNotContain(Log, l => l.Contains("Error SES"));
		}

		// Astro answers every probe this way. It's the normal reply, so it must keep the session and say nothing.
		[Fact]
		public async Task AlreadySubscribed_OnProbe_KeepsTheSession() {
			StreamElementsSocket.Times = _times with { Probe = TimeSpan.FromMilliseconds(200) };
			Start();
			Connection connection = await ConnectedSessionAsync();
			JsonElement probe = await ReceiveJsonAsync(connection.Socket);
			await SendAsync(connection.Socket, AlreadySubscribed(probe.ReadString("nonce")));

			await SendAsync(connection.Socket, Tip("t4"));
			Assert.Equal(("channel.tips", "t4"), await NextEventAsync());
			Assert.True(StreamElementsSocket.IsConnected);
			Assert.False(_astro.HasAnotherConnection());
			Assert.Single(Log, l => l.Contains("StreamElements subscribed to channel.tips"));
			Assert.DoesNotContain(Log, l => l.Contains("Error SES"));
		}

		// After a resumed reconnect Astro has restored the subscription, so the session's first subscribe gets
		// "already subscribed". Treated as a failure, it dropped the resumed session (Error SES3, a fresh one).
		[Fact]
		public async Task AlreadySubscribed_OnFirstSubscribe_Connects() {
			Start();
			var (connection, frame) = await StartSessionAsync();
			Assert.Equal("subscribe", frame.ReadString("nonce"));
			await SendAsync(connection.Socket, AlreadySubscribed("subscribe"));

			await WaitUntilAsync(() => StreamElementsSocket.IsConnected, "IsConnected");
			await SendAsync(connection.Socket, Tip("t5"));
			Assert.Equal(("channel.tips", "t5"), await NextEventAsync());
			Assert.False(_astro.HasAnotherConnection());
			Assert.Contains(Log, l => l.Contains("StreamElements subscribed to channel.tips"));
			Assert.DoesNotContain(Log, l => l.Contains("Error SES"));
		}

		[Fact]
		public async Task Reconnect_ResumesWithTheTokenAtOnce() {
			Start();
			Connection connection = await ConnectedSessionAsync();
			await SendAsync(connection.Socket, Reconnect());

			Connection next = await _astro.NextConnectionAsync(Wait);
			Assert.Contains($"reconnect_token={RECONNECT_TOKEN}", next.Query);
			await SendAsync(next.Socket, Welcome());
			Assert.Equal("subscribe", (await ReceiveJsonAsync(next.Socket)).ReadString("nonce"));
			Assert.Contains(Log, l => l.Contains("StreamElements session ended: ReconnectRequested"));
		}

		// The token is single-use: a session after the resumed one connects plainly.
		[Fact]
		public async Task Reconnect_TokenIsUsedOnce() {
			Start();
			Connection connection = await ConnectedSessionAsync();
			await SendAsync(connection.Socket, Reconnect());
			Connection resumed = await _astro.NextConnectionAsync(Wait);
			await resumed.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);

			Connection plain = await _astro.NextConnectionAsync(Wait);
			Assert.Equal("", plain.Query);
		}

		[Fact]
		public async Task Unauthorized_StopsForGood() {
			Start();
			var (connection, frame) = await StartSessionAsync();
			await SendAsync(connection.Socket, Refusal(frame.ReadString("nonce"), "err_unauthorized"));

			await _loop!.WaitAsync(Wait);
			Assert.False(_astro.HasAnotherConnection());
			Assert.False(StreamElementsSocket.IsConnected);
			Assert.Contains(Log, l => l.Contains("Error SES3: StreamElements refused the token: nope"));
			Assert.Contains(Log, l => l.Contains("StreamElements stopped"));
		}

		[Fact]
		public async Task SubscribeError_RetriesWithAFreshSession() {
			Start();
			var (connection, frame) = await StartSessionAsync();
			await SendAsync(connection.Socket, Refusal(frame.ReadString("nonce"), "err_bad_request"));

			Connection next = await _astro.NextConnectionAsync(Wait);
			Assert.Equal("", next.Query);
			Assert.Contains(Log, l => l.Contains("Error SES3: StreamElements subscribe failed: err_bad_request nope"));
			Assert.Contains(Log, l => l.Contains("StreamElements session ended: SubscribeFailed"));
		}

		[Fact]
		public async Task Silence_EndsTheSessionAndReconnects() {
			StreamElementsSocket.Times = _times with { Silence = TimeSpan.FromMilliseconds(500) };
			Start();
			await StartSessionAsync();

			await _astro.NextConnectionAsync(Wait);
			Assert.Contains(Log, l => l.Contains("StreamElements session ended: SilenceTimeout"));
		}

		[Fact]
		public async Task ServerClose_Reconnects() {
			Start();
			Connection connection = await ConnectedSessionAsync();
			await connection.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

			await _astro.NextConnectionAsync(Wait);
			Assert.Contains(Log, l => l.Contains("StreamElements session ended: SocketClosed"));
			Assert.False(StreamElementsSocket.IsConnected);
		}

		// Shutdown sends a close frame without waiting for the reply; this fake never replies, and a waiting
		// close used to time out here with Error SES2 whenever the socket was still open at cancel.
		[Fact]
		public async Task Cancel_EndsWithoutAnError() {
			Start();
			await ConnectedSessionAsync();
			_cts.Cancel();

			await _loop!.WaitAsync(Wait);
			Assert.Contains(Log, l => l.Contains("StreamElements session ended: CancelRequested"));
			Assert.DoesNotContain(Log, l => l.Contains("Error SES"));
		}

		// The JWT and the reconnect token are credentials: no window line may carry either.
		[Fact]
		public async Task TokensAreNeverLogged() {
			Start();
			Connection connection = await ConnectedSessionAsync();
			await SendAsync(connection.Socket, Tip("t3"));
			await NextEventAsync();
			await SendAsync(connection.Socket, Reconnect());
			Connection next = await _astro.NextConnectionAsync(Wait);
			await SendAsync(next.Socket, Welcome());
			await ReceiveJsonAsync(next.Socket);

			Assert.NotEmpty(Log);
			Assert.DoesNotContain(Log, l => l.Contains(JWT) || l.Contains(RECONNECT_TOKEN));
		}
	}

	public class StreamElementsRetryTests {
		[Theory]
		[InlineData(0, 3)]
		[InlineData(1, 3)]
		[InlineData(2, 6)]
		[InlineData(3, 12)]
		[InlineData(4, 24)]
		[InlineData(5, 30)]
		[InlineData(1000, 30)]
		public void RetryDelay_DoublesUpToTheCap(int failures, int seconds) {
			Assert.Equal(TimeSpan.FromSeconds(seconds), StreamElementsSocket.RetryDelay(failures, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30)));
		}

		[Fact]
		public void Defaults_SilenceIsTheHealthDeadline() {
			Assert.Equal(StreamElementsSocket.SilenceTimeout, StreamElementsSocket.Times.Silence);
			Assert.Equal(TimeSpan.FromSeconds(30), StreamElementsSocket.Times.Probe);
		}
	}
}
