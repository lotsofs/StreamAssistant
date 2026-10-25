namespace StreamAssistant2 {
	internal static class GameBackground {
		const string TEMPLATE = "Template";

		/// <summary>
		/// Shows the game's background image, else Template.png; with neither, leaves the current one. An OBS failure is logged, not thrown.
		/// </summary>
		internal static void Set(string gameName) {
			string directory = Settings.Data.Directories.Backgrounds;
			string? background = PathFor(directory, gameName, File.Exists);
			if (background == null) {
				background = PathFor(directory, TEMPLATE, File.Exists);
				if (background == null) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No background for {gameName} and no {TEMPLATE}.png, left as is");
					return;
				}
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No background for {gameName}, using {TEMPLATE}.png");
			}
			string source = Settings.Data.Game.BackgroundSource;
			try {
				Obs.SetImageSource(source, background);
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"{source} → {Path.GetFileName(background)}");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {source}: {ex.Message}");
			}
		}

		/// <summary>
		/// The game's background image path, or null if the directory isn't set or the file doesn't exist.
		/// </summary>
		internal static string? PathFor(string directory, string gameName, Func<string, bool> exists) {
			if (string.IsNullOrEmpty(directory)) {
				return null;
			}
			string path = Path.Combine(directory, gameName + ".png");
			return exists(path) ? path : null;
		}
	}
}
