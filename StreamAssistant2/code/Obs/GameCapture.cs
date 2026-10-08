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
		/// Points and shows a capture slot per game executable; the rest are pointed at none and hidden.
		/// Logs each shown slot, and the hidden ones together on one line.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.ObsSocket.IsConnected) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, capture sources not set");
				return;
			}
			List<(string Kind, int Slot)> hidden = new();
			SetKind(GAME_CAPTURE, game.GameCapture, hidden);
			SetKind(WINDOW_CAPTURE, game.WindowCapture, hidden);
			SetKind(AUDIO_CAPTURE, game.AudioCapture, hidden);
			if (hidden.Count > 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChangesUnimportant, HiddenLine(hidden));
			}
		}

		/// <summary>
		/// The one log line for every slot set to none and hidden, slot numbers grouped by kind.
		/// </summary>
		internal static string HiddenLine(IReadOnlyList<(string Kind, int Slot)> hidden) {
			var groups = hidden.GroupBy(h => h.Kind).Select(g => $"{g.Key} {string.Join(", ", g.Select(h => h.Slot))}");
			return $"→ none, hidden: {string.Join("; ", groups)}";
		}

		/// <summary>
		/// Sets one kind's slots, adding the hidden ones to the list; a source that can't be set is logged and the rest still go.
		/// </summary>
		static void SetKind(string prefix, IReadOnlyList<string> executables, List<(string Kind, int Slot)> hidden) {
			for (int i = SLOTS; i < executables.Count; i++) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix.Trim()} slot for {executables[i]}, only {SLOTS}");
			}
			var slots = Assignments(prefix, executables);
			for (int i = 0; i < slots.Count; i++) {
				(string source, string executable) = slots[i];
				try {
					bool used = IsUsed(executable);
					Obs.SetInputSetting(source, "window", WindowValue(executable));
					Obs.SetSourceEnabled(SCENE, source, used);
					if (used) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"{source} → {executable}, shown");
					}
					else {
						hidden.Add((prefix.Trim(), i));
					}
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
