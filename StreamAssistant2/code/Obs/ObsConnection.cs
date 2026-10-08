using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace StreamAssistant2 {
	internal static class ObsConnection {
		public static OBSWebsocket ObsSocket = new OBSWebsocket();
		
		static CancellationTokenSource? _cts;

		public static void Connect() {
			if (_cts != null) {
				return;
			}
			_cts = new CancellationTokenSource();
			ObsSocket.Disconnected += OnDisconnected;
			ObsSocket.Connected += OnConnected;
			ObsSocket.StreamStateChanged += OnStreamStateChanged;
			_ = Loop(_cts.Token);
		}

		private static async Task Loop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				if (ObsSocket.IsConnected) {
					await Task.Delay(1000, token);
					continue;
				}
				try {
					ObsSocket.ConnectAsync("ws://127.0.0.1:4455", Config.Data.Obs.SocketPassword);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "OBSException58");
					ConsoleLogger.LogToFile(ex);
				}
				await Task.Delay(3000, token);
			}
		}

		public static void Disconnect() {
			_cts?.Cancel();
			if (ObsSocket.IsConnected) {
				ObsSocket.Disconnect();
			}
		}

		static void OnConnected(object? sender, EventArgs e) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Connected to OBS");
		}

		static void OnDisconnected(object? sender, ObsDisconnectionInfo e) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Lost connection to OBS: {e.DisconnectReason}");
		}

		/// <summary>
		/// Logs OBS starting or stopping its stream; on start, resets the chatter list and sets up the game.
		/// </summary>
		internal static void OnStreamStateChanged(object? sender, StreamStateChangedEventArgs e) {
			try {
				switch (e.OutputState.State) {
					case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "OBS started streaming");
						ChatterList.Reset();
						FireForget.Run("GMS_live", "go-live game setup", Games.OnStreamStartedAsync);
						break;
					case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "OBS stopped streaming");
						break;
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error OSS1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Runs the action now if OBS is connected, else on its next connect. Can run twice if OBS connects
		/// during the call, so the action guards itself.
		/// </summary>
		internal static void WhenConnected(Action action) {
			EventHandler? handler = null;
			handler = (_, _) => {
				ObsSocket.Connected -= handler;
				action();
			};
			ObsSocket.Connected += handler;
			if (ObsSocket.IsConnected) {
				ObsSocket.Connected -= handler;
				action();
			}
		}

		public static bool IsConnected() {
			if (!ObsSocket.IsConnected) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Trying to do an OBS action but not connected to OBS");
				return false;
			}
			return true;
		}
	}
}
