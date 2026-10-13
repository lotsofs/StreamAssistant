namespace StreamAssistant2 {
	internal static class GameBorder {
		const string INNER = "Border: Colorable Inner";
		const string OUTER = "Border: Colorable Outer";
		const string TWIN = " (Transitionary)";
		const string WIDE = "16x9";
		const string INNER_FILE = "Borders_Inner";
		const string OUTER_FILE = "Borders_Outer";

		/// <summary>
		/// Points the inner and outer borders, and their fade twins, at the 16x9 images, or the plain ones for a game with
		/// another aspect; leaves them when those images aren't there. An OBS failure is logged, not thrown.
		/// </summary>
		internal static void Set(Games.Game game) {
			string directory = Config.Data.Directories.Layout;
			if (string.IsNullOrEmpty(directory)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Layout not set in paths.json, borders left as is");
				return;
			}
			if (Files(directory, game.Aspect, File.Exists) is not var (inner, outer)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {(IsWide(game.Aspect) ? WIDE : "plain")} border images for {game.Name}, borders left as is");
				return;
			}
			try {
				Obs.SetImageSource(INNER, inner);
				Obs.SetImageSource(OUTER, outer);
				Obs.SetImageSource(INNER + TWIN, inner);
				Obs.SetImageSource(OUTER + TWIN, outer);
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"Borders → {Path.GetFileName(inner)}, {Path.GetFileName(outer)}");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set the borders: {ex.Message}");
			}
		}

		/// <summary>
		/// Whether an aspect gets the 16x9 borders: none given, or 16x9 itself (any case).
		/// </summary>
		internal static bool IsWide(string? aspect) {
			return aspect == null || aspect.Equals(WIDE, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// The inner and outer border images for the aspect: Borders_Inner_16x9.png and Borders_Outer_16x9.png when it's
		/// wide, else Borders_Inner.png and Borders_Outer.png; null when the directory isn't set or either is missing.
		/// </summary>
		internal static (string Inner, string Outer)? Files(string directory, string? aspect, Func<string, bool> exists) {
			if (string.IsNullOrEmpty(directory)) {
				return null;
			}
			string suffix = IsWide(aspect) ? "_" + WIDE : "";
			string inner = Path.Combine(directory, INNER_FILE + suffix + ".png");
			string outer = Path.Combine(directory, OUTER_FILE + suffix + ".png");
			return exists(inner) && exists(outer) ? (inner, outer) : null;
		}
	}
}
