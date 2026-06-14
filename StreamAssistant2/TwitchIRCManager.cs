using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchIRCManager {
		const string HOST = "irc.chat.twitch.tv";
		const int PORT = 6667;

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

		internal static event Action<string>? OnMessage;

		internal static void Connect() {
			_oauth = Config.Data.TwitchAuth.AccessToken;
			_cts = new CancellationTokenSource();
			_ = Task.Run(() => StartConnectionLoop(_cts.Token));
		}

		static async Task StartConnectionLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				try {
					await ConnectOnce();

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

		static async Task ConnectOnce() {
			_client?.Close();
			_client = null;
			_reader = null;
			_writer = null;

			_client = new TcpClient();
			await _client.ConnectAsync(HOST, PORT);

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
				while (!token.IsCancellationRequested) {
					var message = await _reader.ReadLineAsync();
					if (message == null) {
						throw new Exception("Received null IRC message");
					}

					_lastPingTime = DateTime.UtcNow;

					if (message.StartsWith("PING")) {
						await _writer.WriteLineAsync("PONG :tmi.twitch.tv");
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

		internal static void Disconnect() {
			_cts?.Cancel();
			_client?.Close();
			_client = null;
			_reader = null;
			_writer = null;
		}

		internal static void SendMessage(string message) {
			_ = SendMessageAsync(message);
		}

		static async Task SendMessageAsync(string message) {
			if (_writer == null) {
				return;
				// notify no connection
			}
			try {
				string msg = $"PRIVMSG {_channel} :{message}";
				await _writer.WriteLineAsync(msg);
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ChatOutgoing, $"> {message}");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error 3");
				ConsoleLogger.LogToFile(ex);
				return;
				// notify no connection
			}
		}
	}
}
