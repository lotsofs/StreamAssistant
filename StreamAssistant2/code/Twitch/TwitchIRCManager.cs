using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchIRCManager {
		const string HOST = "irc.chat.twitch.tv";
		const int PORT = 6667;

		// No line for this long: reconnect
		internal static readonly TimeSpan SilenceTimeout = TimeSpan.FromMinutes(7);
		internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

		static TcpClient? _client;
		static StreamReader? _reader;
		static StreamWriter? _writer;
		static CancellationTokenSource? _cts;
		static Task? _listenTask;

		static string _oauth = "";
		static string _username = "lotsofs";
		static string _channel = "#lotsofs";

		static DateTime _lastPingTime = DateTime.MinValue;

		public static TimeSpan TimeSinceLastPing { get {
				return DateTime.UtcNow - _lastPingTime;
			}
		}

		internal static bool HasReceivedLine => _lastPingTime != DateTime.MinValue;

		internal static event Action<string>? OnMessage;

		internal static void Connect() {
			_oauth = Config.Data.TwitchAuth.AccessToken;
			_cts = new CancellationTokenSource();
			_ = Task.Run(() => StartConnectionLoop(_cts.Token));
		}

		static async Task StartConnectionLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				try {
					await ConnectOnce(token);

					_listenTask = ListenLoop(token);
					await _listenTask;
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error 1");
					ConsoleLogger.LogToFile(ex);
					await Task.Delay(3000, token);
				}
			}
		}

		static async Task ConnectOnce(CancellationToken token) {
			_client?.Close();
			_client = null;
			_reader = null;
			_writer = null;

			_client = new TcpClient();
			await ConnectOrTimeoutAsync(_client, HOST, PORT, ConnectTimeout, token);

			var stream = _client.GetStream();
			_reader = new StreamReader(stream);
			_writer = new StreamWriter(stream) { AutoFlush = true };

			await _writer.WriteLineAsync($"PASS oauth:{_oauth}");
			await _writer.WriteLineAsync($"NICK {_username}");
			await _writer.WriteLineAsync($"CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
			await _writer.WriteLineAsync($"JOIN {_channel}");

			SendMessage("🟣 Connected");
		}

		static async Task ListenLoop(CancellationToken token) {
			try {
				StreamReader reader = _reader ?? throw new InvalidOperationException("ListenLoop started without a connection");
				StreamWriter writer = _writer ?? throw new InvalidOperationException("ListenLoop started without a connection");
				while (!token.IsCancellationRequested) {
					var message = await ReadLineOrTimeoutAsync(reader, SilenceTimeout, token);
					if (message == null) {
						throw new Exception("Received null IRC message");
					}

					_lastPingTime = DateTime.UtcNow;

					if (message.StartsWith("PING")) {
						await writer.WriteLineAsync("PONG :tmi.twitch.tv");
						continue;
					}

					OnMessage?.Invoke(message);
				}
			}
			catch (Exception ex) {
				// TODO: handle
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TIRC2: Connection Lost");
				ConsoleLogger.LogToFile(ex);
				throw;
			}
		}

		// Throws TimeoutException on silence, OperationCanceledException only when token is cancelled
		internal static async Task<string?> ReadLineOrTimeoutAsync(StreamReader reader, TimeSpan timeout, CancellationToken token) {
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
			linked.CancelAfter(timeout);
			try {
				return await reader.ReadLineAsync(linked.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) {
				throw new TimeoutException($"No IRC data for {timeout}");
			}
		}

		// Throws TimeoutException if the connect hangs, OperationCanceledException only when token is cancelled
		internal static async Task ConnectOrTimeoutAsync(TcpClient client, string host, int port, TimeSpan timeout, CancellationToken token) {
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
			linked.CancelAfter(timeout);
			try {
				await client.ConnectAsync(host, port, linked.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) {
				throw new TimeoutException($"IRC connect took over {timeout}");
			}
		}

		internal static void Disconnect() {
			_cts?.Cancel();
			_client?.Close();
			_client = null;
			_reader = null;
			_writer = null;
		}

		internal static void SendMessage(string message) {
			FireForget.Run("TIRC3", "chat send", () => SendMessageAsync(message));
		}

		static async Task SendMessageAsync(string message) {
			if (_writer == null) {
				return;
				// notify no connection
			}
			await _writer.WriteLineAsync($"PRIVMSG {_channel} :{message}");
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ChatOutgoing, $"> {message}");
		}
	}
}
