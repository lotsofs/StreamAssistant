using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2 {
	internal static class GameCapture {
		internal const int SLOTS = 3;
		internal const string NONE = "none";
		const string GAME_CAPTURE = "Game: Game Capture ";
		const string WINDOW_CAPTURE = "Game: Window Capture ";
		const string AUDIO_CAPTURE = "Audio 5: App Capture ";
		const string SCENE = "!Scene: Games 1920x1080";

		/// <summary>
		/// Points and shows a capture slot per game executable; the rest are pointed at none and hidden. Logs each one.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.ObsSocket.IsConnected) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, capture sources not set");
				return;
			}
			SetKind(GAME_CAPTURE, game.GameCapture);
			SetKind(WINDOW_CAPTURE, game.WindowCapture);
			SetKind(AUDIO_CAPTURE, game.AudioCapture);
		}

		/// <summary>
		/// Sets one kind's slots; a source that can't be set is logged and the rest still go.
		/// </summary>
		static void SetKind(string prefix, IReadOnlyList<string> executables) {
			for (int i = SLOTS; i < executables.Count; i++) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix.Trim()} slot for {executables[i]}, only {SLOTS}");
			}
			foreach ((string source, string executable) in Assignments(prefix, executables)) {
				try {
					bool used = IsUsed(executable);
					Obs.SetInputSetting(source, "window", WindowValue(executable));
					Obs.SetSourceEnabled(SCENE, source, used);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"{source} → {executable}, {(used ? "shown" : "hidden")}");
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {source}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Each slot's source name, numbered from 0, with its executable or none.
		/// </summary>
		internal static List<(string Source, string Executable)> Assignments(string prefix, IReadOnlyList<string> executables) {
			List<(string, string)> slots = new();
			for (int i = 0; i < SLOTS; i++) {
				slots.Add(($"{prefix}{i}", i < executables.Count ? executables[i] : NONE));
			}
			return slots;
		}

		/// <summary>
		/// Whether a slot's executable is a real one rather than none.
		/// </summary>
		internal static bool IsUsed(string executable) => !executable.Equals(NONE, StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// OBS's title:class:executable window value, with obviously fake title and class.
		/// </summary>
		internal static string WindowValue(string executable) => $"PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:{executable}";
	}
}
