namespace StreamAssistant2 {
	internal static class GameBorder {
		const string INNER = "Border: Colorable Inner";
		const string OUTER = "Border: Colorable Outer";
		const string TWIN = " (Transitionary)";
		const string DEFAULT_ASPECT = "16x9";
		const string INNER_FILE = "Borders_Inner";
		const string OUTER_FILE = "Borders_Outer";

		/// <summary>
		/// Points the inner and outer borders, and their fade twins, at the images for the game's aspect, else the plain
		/// ones; leaves them when neither pair is there. An OBS failure is logged, not thrown.
		/// </summary>
		internal static void Set(Games.Game game) {
			string directory = Config.Data.Directories.Layout;
			if (string.IsNullOrEmpty(directory)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Layout not set in paths.json, borders left as is");
				return;
			}
			if (Files(directory, game.Aspect, File.Exists) is not var (inner, outer)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No border images for {game.Name}, borders left as is");
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
		/// The inner and outer border images for the aspect (16x9 without one): Borders_Inner_&lt;aspect&gt;.png and
		/// Borders_Outer_&lt;aspect&gt;.png when both exist, else Borders_Inner.png and Borders_Outer.png; null when the
		/// directory isn't set or neither pair exists.
		/// </summary>
		internal static (string Inner, string Outer)? Files(string directory, string? aspect, Func<string, bool> exists) {
			if (string.IsNullOrEmpty(directory)) {
				return null;
			}
			string suffix = "_" + (aspect ?? DEFAULT_ASPECT);
			foreach (string variant in new[] { suffix, "" }) {
				string inner = Path.Combine(directory, INNER_FILE + variant + ".png");
				string outer = Path.Combine(directory, OUTER_FILE + variant + ".png");
				if (exists(inner) && exists(outer)) {
					return (inner, outer);
				}
			}
			return null;
		}
	}
}
