namespace StreamAssistant2 {
	internal static class GameColor {
		const string SCHEME_SET = "gameschemes";

		static readonly object _lock = new();
		static string? _game;
		static string? _signScheme;

		/// <summary>
		/// Recolours the layout with the game's scheme: for a game with sign schemes, the one for LiveSplit's previous-split
		/// sign now, else its default one; a game without a scheme leaves the colour.
		/// </summary>
		internal static void Set(string gameName) {
			string? input = SchemeInput(gameName, s => ColorSchemeRegistry.TryGetScheme(s, out _));
			string? scheme = input == null ? null : ChooseScheme(Category(gameName), SplitWatcher.ReadSign());
			lock (_lock) {
				_game = gameName;
				_signScheme = scheme;
			}
			if (input == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No colour scheme for {gameName}, colour left as is");
				return;
			}
			LayoutColoring.TryChangeToSingle(scheme == null ? input : $"{input} {scheme}");
		}

		/// <summary>
		/// On LiveSplit's previous-split sign after a change of the split index: for a game with sign schemes, fades the
		/// layout to the scheme for it when that isn't the one it last went to, without a chat line.
		/// </summary>
		internal static void OnSign(string? sign) {
			string? game;
			lock (_lock) {
				game = _game;
			}
			if (game == null || ChooseScheme(Category(game), sign) is not string scheme) {
				return;
			}
			lock (_lock) {
				if (_game != game || _signScheme == scheme) {
					return;
				}
				_signScheme = scheme;
			}
			if (!ColorSchemeRegistry.TryGetScheme($"{SCHEME_SET} {game} {scheme}", out var data)) {
				return;
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"Split sign {sign?.Trim()}: layout colour {game} {scheme}");
			LayoutColoring.QueueLayoutColorChange(new LayoutColoring.ColorRequest(data!.Scheme.Inner, data.Scheme.Outer, data.Scheme.Text));
		}

		/// <summary>
		/// The colour request for the game's scheme, or null if the scheme set has no such game.
		/// </summary>
		internal static string? SchemeInput(string gameName, Func<string, bool> hasScheme) {
			string input = $"{SCHEME_SET} {gameName}";
			return hasScheme(input) ? input : null;
		}

		/// <summary>
		/// The scheme for a previous-split sign in a category with sign schemes: the one for the sign's trend, else the
		/// category's default; null for a category without sign schemes.
		/// </summary>
		internal static string? ChooseScheme(ColorSchemeRegistry.Category? category, string? sign) {
			if (category == null || category.SignSchemes.Count == 0) {
				return null;
			}
			return SplitColors.Trend(sign) is string trend && category.SignSchemes.TryGetValue(trend, out string? scheme) ? scheme : category.Default;
		}

		/// <summary>
		/// The game's category in the game scheme set, or null.
		/// </summary>
		static ColorSchemeRegistry.Category? Category(string gameName) {
			return ColorSchemeRegistry.TryGetSet(SCHEME_SET, out var set) && ColorSchemeRegistry.TryGetCategoryInSet(set!, gameName, out var category) ? category : null;
		}
	}
}
