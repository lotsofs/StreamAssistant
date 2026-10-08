namespace StreamAssistant2 {
	internal static class GameColor {
		const string SCHEME_SET = "gameschemes";

		/// <summary>
		/// Recolours the layout with the game's scheme; a game without one leaves the colour.
		/// </summary>
		internal static void Set(string gameName) {
			string? input = SchemeInput(gameName, s => ColorSchemeRegistry.TryGetScheme(s, out _));
			if (input == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No colour scheme for {gameName}, colour left as is");
				return;
			}
			LayoutColoring.TryChangeToSingle(input);
		}

		/// <summary>
		/// The colour request for the game's scheme, or null if the scheme set has no such game.
		/// </summary>
		internal static string? SchemeInput(string gameName, Func<string, bool> hasScheme) {
			string input = $"{SCHEME_SET} {gameName}";
			return hasScheme(input) ? input : null;
		}
	}
}
