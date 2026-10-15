using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Reason = StreamAssistant2.TwitchEventSub.SessionExitReason;

namespace StreamAssistant2.Tests {
	public class EventSubReceiveTimeoutTests {
		// A loopback WebSocket: the test drives Server, the helper reads from Client
		sealed class Loopback : IDisposable {
			readonly HttpListener _listener;
			public readonly ClientWebSocket Client;
			public readonly WebSocket Server;

			Loopback(HttpListener listener, ClientWebSocket client, WebSocket server) {
				_listener = listener;
				Client = client;
				Server = server;
			}

			public static async Task<Loopback> OpenAsync() {
				var probe = new TcpListener(IPAddress.Loopback, 0);
				probe.Start();
				int port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();

				var listener = new HttpListener();
				listener.Prefixes.Add($"http://localhost:{port}/");
				listener.Start();

				var accept = Task.Run(async () => {
					var ctx = await listener.GetContextAsync();
					return (await ctx.AcceptWebSocketAsync(null)).WebSocket;
				});
				var client = new ClientWebSocket();
				await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), CancellationToken.None);
				return new Loopback(listener, client, await accept);
			}

			public Task SendFrameAsync(string text, bool end) {
				return Server.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, end, CancellationToken.None);
			}

			public void Dispose() {
				Client.Dispose();
				Server.Dispose();
				_listener.Close();
			}
		}

		// A WebSocket over raw TCP, so the server can drop the connection without a Close frame
		sealed class RawLoopback : IDisposable {
			readonly TcpListener _listener;
			readonly TcpClient _server;
			public readonly ClientWebSocket Client;

			RawLoopback(TcpListener listener, TcpClient server, ClientWebSocket client) {
				_listener = listener;
				_server = server;
				Client = client;
			}

			public static async Task<RawLoopback> OpenAsync() {
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				int port = ((IPEndPoint)listener.LocalEndpoint).Port;

				var accept = Task.Run(async () => {
					var tcp = await listener.AcceptTcpClientAsync();
					var stream = tcp.GetStream();
					string key = ReadKey(await ReadHeadersAsync(stream));
					string hash = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
					await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {hash}\r\n\r\n"));
					return tcp;
				});
				var client = new ClientWebSocket();
				await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);
				return new RawLoopback(listener, await accept, client);
			}

			static async Task<string> ReadHeadersAsync(NetworkStream stream) {
				var sb = new StringBuilder();
				var one = new byte[1];
				while (sb.Length < 4 || sb.ToString(sb.Length - 4, 4) != "\r\n\r\n") {
					if (await stream.ReadAsync(one) == 0) {
						throw new IOException("Handshake cut short");
					}
					sb.Append((char)one[0]);
				}
				return sb.ToString();
			}

			static string ReadKey(string headers) {
				const string name = "Sec-WebSocket-Key:";
				string line = headers.Split("\r\n").First(l => l.StartsWith(name, StringComparison.OrdinalIgnoreCase));
				return line.Substring(name.Length).Trim();
			}

			// TCP reset
			public void Reset() {
				_server.Client.LingerState = new LingerOption(true, 0);
				_server.Close();
			}

			// TCP close, no Close frame
			public void Drop() {
				_server.Close();
			}

			public void Dispose() {
				Client.Dispose();
				_server.Dispose();
				_listener.Stop();
			}
		}

		static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

		[Fact]
		public async Task Silence_ReturnsKeepAliveTimeout() {
			using var lb = await Loopback.OpenAsync();
			var (json, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), Short, CancellationToken.None);
			Assert.Equal(Reason.KeepAliveTimeout, reason);
			Assert.Equal("", json);
		}

		[Fact]
		public async Task AlreadyPastTimeout_ReturnsWithoutWaiting() {
			using var lb = await Loopback.OpenAsync();
			var keepAlive = Stopwatch.StartNew();
			await Task.Delay(Short);
			var watch = Stopwatch.StartNew();
			var (_, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, keepAlive, Short, CancellationToken.None);
			Assert.Equal(Reason.KeepAliveTimeout, reason);
			Assert.True(watch.ElapsedMilliseconds < 100);
		}

		[Fact]
		public async Task WholeMessage_ReturnsIt() {
			using var lb = await Loopback.OpenAsync();
			await lb.SendFrameAsync("{\"a\":1}", true);
			var (json, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			Assert.Equal(Reason.None, reason);
			Assert.Equal("{\"a\":1}", json);
		}

		[Fact]
		public async Task TwoFramesWithinTimeout_ReturnsJoinedMessage() {
			using var lb = await Loopback.OpenAsync();
			var receive = TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			await lb.SendFrameAsync("{\"a\":", false);
			await Task.Delay(100);
			await lb.SendFrameAsync("1}", true);
			var (json, reason) = await receive;
			Assert.Equal(Reason.None, reason);
			Assert.Equal("{\"a\":1}", json);
		}

		[Fact]
		public async Task StallMidMessage_ReturnsKeepAliveTimeout() {
			using var lb = await Loopback.OpenAsync();
			await lb.SendFrameAsync("{\"a\":", false);
			var (json, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), Short, CancellationToken.None);
			Assert.Equal(Reason.KeepAliveTimeout, reason);
			Assert.Equal("", json);
		}

		[Fact]
		public async Task ServerCloses_ReturnsSocketClosed() {
			using var lb = await Loopback.OpenAsync();
			await lb.Server.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
			var (_, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			Assert.Equal(Reason.SocketClosed, reason);
		}

		[Fact]
		public async Task SocketNotOpen_ReturnsSocketDied() {
			using var lb = await Loopback.OpenAsync();
			lb.Client.Abort();
			var (_, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			Assert.Equal(Reason.SocketDied, reason);
		}

		[Fact]
		public async Task ServerResets_ReturnsConnectionLost() {
			using var lb = await RawLoopback.OpenAsync();
			lb.Reset();
			var (json, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			Assert.Equal(Reason.ConnectionLost, reason);
			Assert.Equal("", json);
		}

		[Fact]
		public async Task ServerDropsWithoutClose_ReturnsConnectionLost() {
			using var lb = await RawLoopback.OpenAsync();
			lb.Drop();
			var (json, reason) = await TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(5), CancellationToken.None);
			Assert.Equal(Reason.ConnectionLost, reason);
			Assert.Equal("", json);
		}

		[Fact]
		public async Task OuterCancel_Throws() {
			using var lb = await Loopback.OpenAsync();
			using var cts = new CancellationTokenSource(Short);
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(10), cts.Token));
		}
	}
}
