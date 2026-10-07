using System.Net;
using System.Net.Sockets;
using Xunit;

namespace StreamAssistant2.Tests {
	public class IrcConnectTimeoutTests {
		// Non-routable: the SYN goes nowhere, so the connect hangs rather than being refused
		const string BlackHole = "10.255.255.1";

		[Fact]
		public async Task Hang_ThrowsTimeout() {
			using var client = new TcpClient();
			await Assert.ThrowsAsync<TimeoutException>(() => TwitchIRCManager.ConnectOrTimeoutAsync(client, BlackHole, 6667, TimeSpan.FromMilliseconds(200), CancellationToken.None));
		}

		[Fact]
		public async Task OuterCancel_ThrowsCancelledNotTimeout() {
			using var client = new TcpClient();
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchIRCManager.ConnectOrTimeoutAsync(client, BlackHole, 6667, TimeSpan.FromSeconds(10), cts.Token));
		}

		[Fact]
		public async Task ListeningServer_Connects() {
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try {
				using var client = new TcpClient();
				var accept = listener.AcceptTcpClientAsync();
				await TwitchIRCManager.ConnectOrTimeoutAsync(client, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, TimeSpan.FromSeconds(5), CancellationToken.None);
				Assert.True(client.Connected);
				using var server = await accept;
			}
			finally {
				listener.Stop();
			}
		}
	}
}
