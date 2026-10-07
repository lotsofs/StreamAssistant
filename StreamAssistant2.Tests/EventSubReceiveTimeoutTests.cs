using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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
		public async Task OuterCancel_Throws() {
			using var lb = await Loopback.OpenAsync();
			using var cts = new CancellationTokenSource(Short);
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchEventSub.ReceiveFullMessage(lb.Client, Stopwatch.StartNew(), TimeSpan.FromSeconds(10), cts.Token));
		}
	}
}
