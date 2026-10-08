using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	public class EventSubReconnectTests {
		// A loopback WebSocket server; the server side is scripted per test once a client connects
		sealed class Server : IDisposable {
			readonly HttpListener _listener;
			public readonly Uri Url;
			public readonly Task<WebSocket> Accepted;

			Server(HttpListener listener, Uri url, Task<WebSocket> accepted) {
				_listener = listener;
				Url = url;
				Accepted = accepted;
			}

			public static Server Start() {
				var probe = new TcpListener(IPAddress.Loopback, 0);
				probe.Start();
				int port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();

				var listener = new HttpListener();
				listener.Prefixes.Add($"http://localhost:{port}/");
				listener.Start();
				var accepted = Task.Run(async () => {
					var ctx = await listener.GetContextAsync();
					return (await ctx.AcceptWebSocketAsync(null)).WebSocket;
				});
				return new Server(listener, new Uri($"ws://localhost:{port}/"), accepted);
			}

			public void Dispose() {
				if (Accepted.IsCompletedSuccessfully) {
					Accepted.Result.Dispose();
				}
				_listener.Close();
			}
		}

		static Task SendAsync(WebSocket ws, string text) {
			return ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		static string Message(string type, string payload = "{}") {
			return $"{{\"metadata\":{{\"message_type\":\"{type}\"}},\"payload\":{payload}}}";
		}

		static readonly string Welcome = Message("session_welcome", "{\"session\":{\"id\":\"abc123\"}}");
		static readonly TimeSpan Long = TimeSpan.FromSeconds(5);
		static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

		[Fact]
		public async Task Welcome_ReturnsOpenSocketAndId() {
			using var server = Server.Start();
			var connect = TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, Long, CancellationToken.None);
			await SendAsync(await server.Accepted, Welcome);
			var (socket, id) = await connect;
			using (socket) {
				Assert.NotNull(socket);
				Assert.Equal(WebSocketState.Open, socket!.State);
				Assert.Equal("abc123", id);
			}
		}

		[Fact]
		public async Task Close4007_ReturnsNull() {
			using var server = Server.Start();
			var connect = TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, Long, CancellationToken.None);
			await (await server.Accepted).CloseOutputAsync((WebSocketCloseStatus)4007, "invalid reconnect attempt", CancellationToken.None);
			var (socket, id) = await connect;
			Assert.Null(socket);
			Assert.Equal("", id);
		}

		[Fact]
		public async Task NoWelcome_ReturnsNullAfterTimeout() {
			using var server = Server.Start();
			var (socket, _) = await TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, Short, CancellationToken.None);
			Assert.Null(socket);
		}

		[Fact]
		public async Task OtherMessageFirst_ReturnsNull() {
			using var server = Server.Start();
			var connect = TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, Long, CancellationToken.None);
			await SendAsync(await server.Accepted, Message("session_keepalive"));
			var (socket, _) = await connect;
			Assert.Null(socket);
		}

		[Fact]
		public async Task WelcomeWithoutId_ReturnsNull() {
			using var server = Server.Start();
			var connect = TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, Long, CancellationToken.None);
			await SendAsync(await server.Accepted, Message("session_welcome", "{\"session\":{}}"));
			var (socket, _) = await connect;
			Assert.Null(socket);
		}

		[Fact]
		public async Task ConnectHangs_ReturnsNull() {
			var (socket, _) = await TwitchEventSub.ConnectToReconnectUrlAsync(new Uri("ws://10.255.255.1/ws"), TimeSpan.FromMilliseconds(200), Long, CancellationToken.None);
			Assert.Null(socket);
		}

		[Fact]
		public async Task OuterCancel_Throws() {
			using var server = Server.Start();
			using var cts = new CancellationTokenSource(Short);
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchEventSub.ConnectToReconnectUrlAsync(server.Url, Long, TimeSpan.FromSeconds(10), cts.Token));
		}

		[Fact]
		public async Task Drain_HandlesNotificationsUntilClose() {
			using var server = Server.Start();
			using var client = new ClientWebSocket();
			await client.ConnectAsync(server.Url, CancellationToken.None);
			var ws = await server.Accepted;
			await SendAsync(ws, Message("notification", "{\"subscription\":{\"type\":\"a\"}}"));
			await SendAsync(ws, Message("session_keepalive"));
			await SendAsync(ws, Message("notification", "{\"subscription\":{\"type\":\"b\"}}"));
			await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);

			List<string> seen = new();
			int count = await TwitchEventSub.DrainOldSocketAsync(client, Long, root => seen.Add(root.ReadString("payload.subscription.type")), CancellationToken.None);
			Assert.Equal(2, count);
			Assert.Equal(new[] { "a", "b" }, seen);
		}

		[Fact]
		public async Task Drain_Silence_ReturnsZeroAfterLimit() {
			using var server = Server.Start();
			using var client = new ClientWebSocket();
			await client.ConnectAsync(server.Url, CancellationToken.None);
			await server.Accepted;
			Assert.Equal(0, await TwitchEventSub.DrainOldSocketAsync(client, Short, _ => { }, CancellationToken.None));
		}
	}
}
