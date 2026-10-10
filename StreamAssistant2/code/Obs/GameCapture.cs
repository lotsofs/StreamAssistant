using Capture = StreamAssistant2.Games.Capture;
using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2 {
	internal static class GameCapture {
		internal const int SLOTS = 3;
		internal const string NONE = "none";
		const double MIN_DB = -100;
		const double MAX_DB = 26;
		const string GAME_CAPTURE = "Game: Game Capture ";
		const string WINDOW_CAPTURE = "Game: Window Capture ";
		const string AUDIO_CAPTURE = "Audio 5: App Capture ";
		const string SCENE = "!Scene: Games 1920x1080";

		/// <summary>
		/// Points, shows and sets the volume of a capture slot per game executable; the rest are pointed at none and hidden.
		/// Logs each shown slot, and the hidden ones together on one line.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.IsReady) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, capture sources not set");
				return;
			}
			List<(string Kind, int Slot)> hidden = new();
			double? defaultVolume = Games.DefaultVolume;
			SetKind(GAME_CAPTURE, game.GameCapture, defaultVolume, hidden);
			SetKind(WINDOW_CAPTURE, game.WindowCapture, defaultVolume, hidden);
			SetKind(AUDIO_CAPTURE, game.AudioCapture, defaultVolume, hidden);
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
		static void SetKind(string prefix, IReadOnlyList<Capture> captures, double? defaultVolume, List<(string Kind, int Slot)> hidden) {
			for (int i = SLOTS; i < captures.Count; i++) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix.Trim()} slot for {captures[i].Executable}, only {SLOTS}");
			}
			var slots = Assignments(prefix, captures);
			for (int i = 0; i < slots.Count; i++) {
				(string source, Capture capture) = slots[i];
				try {
					bool used = IsUsed(capture.Executable);
					Obs.SetInputSetting(source, "window", WindowValue(capture.Executable));
					Obs.SetSourceEnabled(SCENE, source, used);
					if (!used) {
						hidden.Add((prefix.Trim(), i));
						continue;
					}
					double? volume = VolumeFor(capture, defaultVolume);
					if (volume is double db) {
						Obs.SetInputVolumeDb(source, db);
					}
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, ShownLine(source, capture.Executable, volume));
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {source}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Each slot's source name, numbered from 0, with its capture or none.
		/// </summary>
		internal static List<(string Source, Capture Capture)> Assignments(string prefix, IReadOnlyList<Capture> captures) {
			List<(string, Capture)> slots = new();
			for (int i = 0; i < SLOTS; i++) {
				slots.Add(($"{prefix}{i}", i < captures.Count ? captures[i] : new Capture(NONE)));
			}
			return slots;
		}

		/// <summary>
		/// The slot's own dB, else the default, clamped to what OBS takes; null if neither is set.
		/// </summary>
		internal static double? VolumeFor(Capture capture, double? defaultVolume) {
			double? db = capture.Volume ?? defaultVolume;
			return db is double d ? Math.Clamp(d, MIN_DB, MAX_DB) : null;
		}

		/// <summary>
		/// The log line for a shown slot, with its volume when one was set.
		/// </summary>
		internal static string ShownLine(string source, string executable, double? volume) =>
			volume is double db ? $"{source} → {executable}, shown, {db} dB" : $"{source} → {executable}, shown";

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
