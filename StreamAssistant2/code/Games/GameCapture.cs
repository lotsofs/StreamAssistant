using Capture = StreamAssistant2.Games.Capture;
using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2 {
	internal static class GameCapture {
		internal const string NONE = "none";
		const double MIN_DB = -100;
		const double MAX_DB = 26;

		/// <summary>
		/// A capture kind's short name by its sources' name before the slot number: "Game capture" for the game capture
		/// prefix ("Game: Game Capture"), likewise for window and audio; any other name as it is.
		/// </summary>
		internal static string KindName(string kind) {
			GameSettings settings = Settings.Data.Game;
			return kind == settings.GameCapturePrefix ? "Game capture"
				: kind == settings.WindowCapturePrefix ? "Window capture"
				: kind == settings.AudioCapturePrefix ? "Audio capture"
				: kind;
		}

		/// <summary>
		/// Points, shows and sets the volume of a capture slot per game executable; the rest are pointed at none and hidden.
		/// Shown and hidden in every games scene. Logs each shown slot, and the hidden ones together on one line.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.IsReady) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, capture sources not set");
				return;
			}
			List<string> scenes = GameScene.GamesScenes();
			if (scenes.Count == 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "No games scenes in Basic Stream Layout, capture slots not shown or hidden");
			}
			List<(string Kind, int Slot)> hidden = new();
			double? defaultVolume = Games.DefaultVolume;
			GameSettings settings = Settings.Data.Game;
			SetKind(settings.GameCapturePrefix, game.GameCapture, settings.CaptureSlots, defaultVolume, scenes, hidden);
			SetKind(settings.WindowCapturePrefix, game.WindowCapture, settings.CaptureSlots, defaultVolume, scenes, hidden);
			SetKind(settings.AudioCapturePrefix, game.AudioCapture, settings.CaptureSlots, defaultVolume, scenes, hidden);
			if (hidden.Count > 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChangesUnimportant, HiddenLine(hidden));
			}
		}

		/// <summary>
		/// The one log line for every slot set to none and hidden, slot numbers grouped by kind.
		/// </summary>
		internal static string HiddenLine(IReadOnlyList<(string Kind, int Slot)> hidden) {
			return $"→ none, hidden: {SlotGroups(hidden)}";
		}

		/// <summary>
		/// Slot numbers grouped by kind, in the order the kinds first appear: "Game: Game Capture 1, 2; Game: Window Capture 0".
		/// </summary>
		internal static string SlotGroups(IReadOnlyList<(string Kind, int Slot)> slots) {
			var groups = slots.GroupBy(h => h.Kind).Select(g => $"{g.Key} {string.Join(", ", g.Select(h => h.Slot))}");
			return string.Join("; ", groups);
		}

		/// <summary>
		/// Sets one kind's slots, showing or hiding each in the scenes, adding the hidden ones to the list; a source that can't
		/// be set is logged and the rest still go.
		/// </summary>
		static void SetKind(string prefix, IReadOnlyList<Capture> captures, int slotCount, double? defaultVolume, IReadOnlyList<string> scenes, List<(string Kind, int Slot)> hidden) {
			for (int i = slotCount; i < captures.Count; i++) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix} slot for {captures[i].Executable}, only {slotCount}");
			}
			var slots = Assignments(prefix, captures, slotCount);
			for (int i = 0; i < slots.Count; i++) {
				(string source, Capture capture) = slots[i];
				try {
					bool used = IsUsed(capture.Executable);
					Obs.SetInputSetting(source, "window", WindowValue(capture.Executable));
					foreach (string scene in scenes) {
						Obs.SetSourceEnabled(scene, source, used);
					}
					if (!used) {
						hidden.Add((prefix, i));
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
		/// Each slot's source name, the prefix and its number from 0, with its capture or none.
		/// </summary>
		internal static List<(string Source, Capture Capture)> Assignments(string prefix, IReadOnlyList<Capture> captures, int slotCount) {
			List<(string, Capture)> slots = new();
			for (int i = 0; i < slotCount; i++) {
				slots.Add(($"{prefix} {i}", i < captures.Count ? captures[i] : new Capture(NONE)));
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
