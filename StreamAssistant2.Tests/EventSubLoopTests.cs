using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Xunit;

namespace StreamAssistant2.Tests {
	// Runs the real TwitchEventSub.StartConnectionLoop against a fake Twitch on loopback: WebSocket sessions
	// and the subscribe POSTs. Notifications go to a capture, never to the real handlers.
	public class EventSubLoopTests : IAsyncLifetime {
		sealed class FakeTwitch : IDisposable {
			readonly HttpListener _listener = new();
			readonly Channel<WebSocket> _sockets = Channel.CreateUnbounded<WebSocket>();
			readonly List<string> _posts = new();
			public readonly int Port;
			public int SubscribeStatus = 202;

			public FakeTwitch() {
				var probe = new TcpListener(IPAddress.Loopback, 0);
				probe.Start();
				Port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();
				_listener.Prefixes.Add($"http://localhost:{Port}/");
				_listener.Start();
				_ = Task.Run(ServeAsync);
			}

			public string WsUrl => $"ws://localhost:{Port}/ws";
			public string SubscriptionsUrl => $"http://localhost:{Port}/subscriptions";

			public List<string> Posts { get { lock (_posts) return _posts.ToList(); } }

			async Task ServeAsync() {
				while (_listener.IsListening) {
					HttpListenerContext ctx;
					try {
						ctx = await _listener.GetContextAsync();
					}
					catch {
						return;
					}
					if (ctx.Request.IsWebSocketRequest) {
						var ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;
						_sockets.Writer.TryWrite(ws);
						continue;
					}
					using (var reader = new StreamReader(ctx.Request.InputStream)) {
						string body = await reader.ReadToEndAsync();
						lock (_posts) _posts.Add(body);
					}
					ctx.Response.StatusCode = SubscribeStatus;
					byte[] reply = Encoding.UTF8.GetBytes("{}");
					await ctx.Response.OutputStream.WriteAsync(reply);
					ctx.Response.Close();
				}
			}

			public async Task<WebSocket> NextSocketAsync(TimeSpan timeout) {
				using var cts = new CancellationTokenSource(timeout);
				return await _sockets.Reader.ReadAsync(cts.Token);
			}

			public bool HasAnotherSocket() {
				return _sockets.Reader.TryPeek(out _);
			}

			public void Dispose() {
				_listener.Close();
			}
		}

		static Task SendAsync(WebSocket ws, string text) {
			return ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		static string Message(string type, string payload) {
			return $"{{\"metadata\":{{\"message_type\":\"{type}\"}},\"payload\":{payload}}}";
		}

		static string Welcome(string id) => Message("session_welcome", $"{{\"session\":{{\"id\":\"{id}\"}}}}");
		static string Notification(string type) => Message("notification", $"{{\"subscription\":{{\"type\":\"{type}\"}},\"event\":{{}}}}");
		static string Reconnect(string url) => Message("session_reconnect", $"{{\"session\":{{\"reconnect_url\":\"{url}\"}}}}");

		static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
		static int SubscriptionCount => TwitchEventSubSubscription.Subscriptions.Count;

		FakeTwitch _twitch = null!;
		readonly Channel<string> _events = Channel.CreateUnbounded<string>();
		readonly List<string> _log = new();
		CancellationTokenSource _cts = null!;
		Task _loop = null!;

		string _savedEventSubUrl = "";
		string _savedSubscriptionsUrl = "";
		TimeSpan _savedRetryDelay;
		Action<string, JsonElement> _savedHandler = null!;

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		List<string> Log { get { lock (_log) return _log.ToList(); } }

		public Task InitializeAsync() {
			_twitch = new FakeTwitch();
			_savedEventSubUrl = TwitchEventSub.EventSubUrl;
			_savedSubscriptionsUrl = TwitchEventSub.SubscriptionsUrl;
			_savedRetryDelay = TwitchEventSub.RetryDelay;
			_savedHandler = TwitchEventSub.NotificationHandler;

			TwitchEventSub.EventSubUrl = _twitch.WsUrl;
			TwitchEventSub.SubscriptionsUrl = _twitch.SubscriptionsUrl;
			TwitchEventSub.RetryDelay = TimeSpan.FromMilliseconds(100);
			TwitchEventSub.NotificationHandler = (type, _) => _events.Writer.TryWrite(type);
			ConsoleLogger.LineLogged += OnLine;

			_cts = new CancellationTokenSource();
			_loop = TwitchEventSub.StartConnectionLoop(_cts.Token);
			return Task.CompletedTask;
		}

		public async Task DisposeAsync() {
			_cts.Cancel();
			try {
				await _loop;
			}
			catch (OperationCanceledException) {
			}
			ConsoleLogger.LineLogged -= OnLine;
			TwitchEventSub.EventSubUrl = _savedEventSubUrl;
			TwitchEventSub.SubscriptionsUrl = _savedSubscriptionsUrl;
			TwitchEventSub.RetryDelay = _savedRetryDelay;
			TwitchEventSub.NotificationHandler = _savedHandler;
			_twitch.Dispose();
			_cts.Dispose();
		}

		async Task<string> NextEventAsync() {
			using var cts = new CancellationTokenSource(Wait);
			return await _events.Reader.ReadAsync(cts.Token);
		}

		async Task WaitForPostsAsync(int count) {
			var deadline = DateTime.UtcNow + Wait;
			while (_twitch.Posts.Count < count) {
				Assert.True(DateTime.UtcNow < deadline, $"expected {count} subscribe POSTs, got {_twitch.Posts.Count}");
				await Task.Delay(20);
			}
		}

		async Task<WebSocket> StartSessionAsync(string id) {
			var ws = await _twitch.NextSocketAsync(Wait);
			await SendAsync(ws, Welcome(id));
			await WaitForPostsAsync(SubscriptionCount);
			return ws;
		}

		[Fact]
		public async Task FreshSession_SubscribesEverythingAndDeliversEvents() {
			await StartSessionAsync("s1");
			Assert.Equal(SubscriptionCount, _twitch.Posts.Count);
			Assert.All(_twitch.Posts, p => Assert.Contains("\"session_id\":\"s1\"", p));
		}

		[Fact]
		public async Task FreshSession_DeliversNotifications() {
			var ws = await StartSessionAsync("s1");
			await SendAsync(ws, Notification("channel.cheer"));
			Assert.Equal("channel.cheer", await NextEventAsync());
		}

		[Fact]
		public async Task Reconnect_SwitchesSocketsWithoutResubscribing() {
			var old = await StartSessionAsync("s1");
			await SendAsync(old, Reconnect(_twitch.WsUrl + "?reconnect=1"));
			await SendAsync(old, Notification("during.switch"));

			var fresh = await _twitch.NextSocketAsync(Wait);
			await SendAsync(fresh, Welcome("s2"));
			Assert.Equal("during.switch", await NextEventAsync());
			await old.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);

			await SendAsync(fresh, Notification("after.switch"));
			Assert.Equal("after.switch", await NextEventAsync());

			Assert.Equal(SubscriptionCount, _twitch.Posts.Count);
			Assert.False(_twitch.HasAnotherSocket());
			Assert.Contains(Log, l => l.Contains("EventSub reconnected. Session ID: s2"));
			Assert.Contains(Log, l => l.Contains("EventSub old connection closed (1 events"));
			Assert.DoesNotContain(Log, l => l.Contains("EventSub session ended"));
		}

		[Fact]
		public async Task ReconnectRefused_FallsBackToFreshSession() {
			var old = await StartSessionAsync("s1");
			await SendAsync(old, Reconnect(_twitch.WsUrl + "?reconnect=1"));

			var refused = await _twitch.NextSocketAsync(Wait);
			await refused.CloseOutputAsync((WebSocketCloseStatus)4007, "invalid reconnect attempt", CancellationToken.None);

			var fallback = await _twitch.NextSocketAsync(Wait);
			await SendAsync(fallback, Welcome("s3"));
			await WaitForPostsAsync(2 * SubscriptionCount);

			Assert.Contains(Log, l => l.Contains("EventSub reconnect failed, starting a fresh session"));
			Assert.Contains(Log, l => l.Contains("EventSub session ended: ReconnectRequested"));
			Assert.All(_twitch.Posts.Skip(SubscriptionCount), p => Assert.Contains("\"session_id\":\"s3\"", p));
		}

		[Fact]
		public async Task Subscribe409_IsNotAFailure() {
			_twitch.SubscribeStatus = 409;
			var ws = await StartSessionAsync("s1");
			await SendAsync(ws, Notification("still.alive"));
			Assert.Equal("still.alive", await NextEventAsync());

			Assert.Contains(Log, l => l.Contains("already exists"));
			Assert.DoesNotContain(Log, l => l.Contains("Error TES1"));
			Assert.False(_twitch.HasAnotherSocket());
		}

		[Fact]
		public async Task Subscribe500_EndsTheSession() {
			_twitch.SubscribeStatus = 500;
			var ws = await _twitch.NextSocketAsync(Wait);
			await SendAsync(ws, Welcome("s1"));

			await _twitch.NextSocketAsync(Wait);
			Assert.Contains(Log, l => l.Contains("Error TES1"));
		}
	}
}
