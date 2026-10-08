using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Xunit;

namespace StreamAssistant2.Tests {
	public class EventSubConnectTimeoutTests {
		// Non-routable: the SYN goes nowhere, so the connect hangs rather than being refused
		static readonly Uri BlackHole = new("ws://10.255.255.1/ws");

		[Fact]
		public async Task Hang_ThrowsTimeout() {
			using var socket = new ClientWebSocket();
			await Assert.ThrowsAsync<TimeoutException>(() => TwitchEventSub.ConnectOrTimeoutAsync(socket, BlackHole, TimeSpan.FromMilliseconds(200), CancellationToken.None));
		}

		[Fact]
		public async Task OuterCancel_ThrowsCancelledNotTimeout() {
			using var socket = new ClientWebSocket();
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchEventSub.ConnectOrTimeoutAsync(socket, BlackHole, TimeSpan.FromSeconds(10), cts.Token));
		}

		[Fact]
		public async Task ListeningServer_Connects() {
			var probe = new TcpListener(IPAddress.Loopback, 0);
			probe.Start();
			int port = ((IPEndPoint)probe.LocalEndpoint).Port;
			probe.Stop();

			var listener = new HttpListener();
			listener.Prefixes.Add($"http://localhost:{port}/");
			listener.Start();
			try {
				var accept = Task.Run(async () => {
					var ctx = await listener.GetContextAsync();
					return (await ctx.AcceptWebSocketAsync(null)).WebSocket;
				});
				using var socket = new ClientWebSocket();
				await TwitchEventSub.ConnectOrTimeoutAsync(socket, new Uri($"ws://localhost:{port}/"), TimeSpan.FromSeconds(5), CancellationToken.None);
				Assert.Equal(WebSocketState.Open, socket.State);
				using var server = await accept;
			}
			finally {
				listener.Close();
			}
		}
	}
}
