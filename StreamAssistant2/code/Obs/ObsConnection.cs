using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace StreamAssistant2 {
	internal static class ObsConnection {
		public static OBSWebsocket ObsSocket = new OBSWebsocket();
		
		static CancellationTokenSource? _cts;
		static volatile bool _ready;
		static string? _lastFailure;

		/// <summary>
		/// True once OBS has accepted the connection (its Connected event), false again when it drops.
		/// The socket alone counts as connected earlier, during the handshake, when requests still fail.
		/// </summary>
		internal static bool IsReady => _ready;

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
			_ready = true;
			_lastFailure = null;
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Connected to OBS");
			LeftPanel.OnObsConnected();
			GameSidebar.OnObsConnected();
			MoneyGoal.OnObsConnected();
			ChannelPoints.OnObsConnected();
		}

		/// <summary>
		/// Logs a drop of the connection, or a failed connect whose reason isn't the last one logged.
		/// </summary>
		static void OnDisconnected(object? sender, ObsDisconnectionInfo e) {
			bool wasReady = _ready;
			_ready = false;
			(string? line, _lastFailure) = DisconnectLine(wasReady, e.DisconnectReason ?? "", _lastFailure);
			if (line != null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, line);
			}
		}

		/// <summary>
		/// The line for a disconnect, or null, and the failure reason to remember: a drop of a connection OBS had accepted
		/// is logged and leaves an unreachable OBS unlogged; a failed connect is logged when its reason isn't the last one.
		/// </summary>
		internal static (string? Line, string? Last) DisconnectLine(bool wasReady, string reason, string? last) {
			if (wasReady) {
				return ($"Lost connection to OBS: {reason}", "");
			}
			if (reason == last) {
				return (null, last);
			}
			return (reason.Length == 0 ? "Can't reach OBS, connecting when it's up" : $"Can't connect to OBS: {reason}", reason);
		}

		/// <summary>
		/// Logs OBS starting or stopping its stream; on start, resets the chatter list, sets up the game and polls the uptime.
		/// </summary>
		internal static void OnStreamStateChanged(object? sender, StreamStateChangedEventArgs e) {
			try {
				switch (e.OutputState.State) {
					case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "OBS started streaming");
						ChatterList.Reset();
						FireForget.Run("GMS_live", "go-live game setup", Games.OnStreamStartedAsync);
						TwitchUptime.PollNow();
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
		/// Runs the action now if OBS is ready, else on its next connect; returns whether it ran now. Can run
		/// twice if OBS connects during the call, so the action guards itself.
		/// </summary>
		internal static bool WhenConnected(Action action) {
			EventHandler? handler = null;
			handler = (_, _) => {
				ObsSocket.Connected -= handler;
				action();
			};
			ObsSocket.Connected += handler;
			if (!IsReady) {
				return false;
			}
			ObsSocket.Connected -= handler;
			action();
			return true;
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
