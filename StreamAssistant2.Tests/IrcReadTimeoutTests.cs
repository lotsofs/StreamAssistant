using System.Net;
using System.Net.Sockets;
using Xunit;

namespace StreamAssistant2.Tests {
	public class IrcReadTimeoutTests {
		// A loopback connection: the test writes on server, the helper reads from client
		sealed class Loopback : IDisposable {
			readonly TcpListener _listener;
			public readonly TcpClient Client;
			public readonly TcpClient Server;
			public readonly StreamReader Reader;

			Loopback(TcpListener listener, TcpClient client, TcpClient server) {
				_listener = listener;
				Client = client;
				Server = server;
				Reader = new StreamReader(client.GetStream());
			}

			public static async Task<Loopback> OpenAsync() {
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				var client = new TcpClient();
				var accept = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
				return new Loopback(listener, client, await accept);
			}

			public async Task SendAsync(string text) {
				var writer = new StreamWriter(Server.GetStream()) { AutoFlush = true };
				await writer.WriteAsync(text);
			}

			public void Dispose() {
				Reader.Dispose();
				Client.Dispose();
				Server.Dispose();
				_listener.Stop();
			}
		}

		static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

		[Fact]
		public async Task Silence_ThrowsTimeout() {
			using var lb = await Loopback.OpenAsync();
			await Assert.ThrowsAsync<TimeoutException>(() => TwitchIRCManager.ReadLineOrTimeoutAsync(lb.Reader, Short, CancellationToken.None));
		}

		[Fact]
		public async Task OuterCancel_ThrowsCancelledNotTimeout() {
			using var lb = await Loopback.OpenAsync();
			using var cts = new CancellationTokenSource(Short);
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TwitchIRCManager.ReadLineOrTimeoutAsync(lb.Reader, TimeSpan.FromSeconds(10), cts.Token));
			Assert.True(cts.IsCancellationRequested);
		}

		[Fact]
		public async Task DataAvailable_ReturnsLine() {
			using var lb = await Loopback.OpenAsync();
			await lb.SendAsync("PING :tmi.twitch.tv\r\n");
			Assert.Equal("PING :tmi.twitch.tv", await TwitchIRCManager.ReadLineOrTimeoutAsync(lb.Reader, TimeSpan.FromSeconds(5), CancellationToken.None));
		}

		[Fact]
		public async Task LineArrivingBeforeTimeout_ReturnsLine() {
			using var lb = await Loopback.OpenAsync();
			var read = TwitchIRCManager.ReadLineOrTimeoutAsync(lb.Reader, TimeSpan.FromSeconds(5), CancellationToken.None);
			await Task.Delay(100);
			await lb.SendAsync("hello\r\n");
			Assert.Equal("hello", await read);
		}

		[Fact]
		public async Task ServerCloses_ReturnsNull() {
			using var lb = await Loopback.OpenAsync();
			lb.Server.Close();
			Assert.Null(await TwitchIRCManager.ReadLineOrTimeoutAsync(lb.Reader, TimeSpan.FromSeconds(5), CancellationToken.None));
		}
	}
}
