namespace StreamAssistant2 {
	internal static class GameBackground {
		const string SOURCE = "Image: Background";

		/// <summary>
		/// Shows the game's background image; a missing image leaves the current one.
		/// </summary>
		internal static void Set(string gameName) {
			string? background = PathFor(Config.Data.Directories.Backgrounds, gameName, File.Exists);
			if (background == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No background for {gameName}, left as is");
				return;
			}
			Obs.SetImageSource(SOURCE, background);
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
