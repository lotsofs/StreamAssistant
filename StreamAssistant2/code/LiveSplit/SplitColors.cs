namespace StreamAssistant2 {
	internal static class SplitColors {
		const string SCHEME_SET = "gameschemes";
		const string FALLBACK = "none";
		const string PROPERTY = "color_multiply";
		const string HIGHLIGHT_FILTER = "Color Correction 2";
		const string PREVIOUS_FILTER = "Color Correction";
		const string PREVIOUS_RUN = "Livesplit: Previous Run";
		const string PREVIOUS_SEGMENT = "Livesplit: Previous Segment";
		const string PREVIOUS_GOLD = "Livesplit: Previous Gold";
		const string NONE = "None";
		const string POSITIVE = "LabelPositive";
		const string NEGATIVE = "LabelNegative";
		const string GOLD = "LabelGold";

		static readonly (string Source, string Key)[] _highlights = [
			("Livesplit: Splits List Highlights Gold", "ListGold"),
			("Livesplit: Splits List Highlights AheadGained", "ListGainedAhead"),
			("Livesplit: Splits List Highlights AheadLost", "ListLostAhead"),
			("Livesplit: Splits List Highlights BehindGained", "ListGainedBehind"),
			("Livesplit: Splits List Highlights BehindLost", "ListLostBehind"),
		];

		/// <summary>
		/// One filter colour to set: the source, its filter, the filter's setting and the OBS colour.
		/// </summary>
		internal sealed record FilterColor(string Source, string Filter, string Property, long Color);

		/// <summary>
		/// The game's split colours from gameschemes over none's, and where they came from; null when neither has any.
		/// </summary>
		internal static (string From, Dictionary<string, string> Colors)? For(string gameName) {
			return Resolve(ColorSchemeRegistry.TryGetSet(SCHEME_SET, out var set) ? set : null, gameName);
		}

		/// <summary>
		/// The game's split colours over the fallback category's, and where they came from: the game when it has any,
		/// else the fallback; null when neither has any.
		/// </summary>
		internal static (string From, Dictionary<string, string> Colors)? Resolve(ColorSchemeRegistry.ThemeSet? set, string gameName) {
			if (set == null) {
				return null;
			}
			Dictionary<string, string> colors = new(StringComparer.OrdinalIgnoreCase);
			Copy(set, FALLBACK, colors);
			bool own = Copy(set, gameName, colors);
			return colors.Count == 0 ? null : (own ? gameName : FALLBACK, colors);
		}

		/// <summary>
		/// Copies a category's split colours into the map; false when it has none.
		/// </summary>
		static bool Copy(ColorSchemeRegistry.ThemeSet set, string category, Dictionary<string, string> into) {
			if (!ColorSchemeRegistry.TryGetCategoryInSet(set, category, out var found) || found!.SplitColors.Count == 0) {
				return false;
			}
			foreach (var (key, value) in found.SplitColors) {
				into[key] = value;
			}
			return true;
		}

		/// <summary>
		/// The split colour keys for the previous split's run, segment and gold differences, by LiveSplit's sign (any case,
		/// trimmed); None for each with an unknown or blank sign.
		/// </summary>
		internal static (string Run, string Segment, string Gold) SignKeys(string? sign) {
			return sign?.Trim().ToLowerInvariant() switch {
				"ahead" => (POSITIVE, NONE, NONE),
				"gained" => (NONE, POSITIVE, NONE),
				"gainedahead" => (POSITIVE, POSITIVE, NONE),
				"lostahead" => (POSITIVE, NEGATIVE, NONE),
				"behind" => (NEGATIVE, NONE, NONE),
				"lost" => (NONE, NEGATIVE, NONE),
				"gainedbehind" => (NEGATIVE, POSITIVE, NONE),
				"lostbehind" => (NEGATIVE, NEGATIVE, NONE),
				"nopb" => (NEGATIVE, NONE, NONE),
				"nopbgained" => (NEGATIVE, POSITIVE, NONE),
				"nopblost" => (NEGATIVE, NEGATIVE, NONE),
				"gold" => (NONE, GOLD, GOLD),
				"goldahead" => (POSITIVE, GOLD, GOLD),
				"goldbehind" => (NEGATIVE, GOLD, GOLD),
				"pb" => (POSITIVE, NONE, NONE),
				"pbgained" => (POSITIVE, POSITIVE, NONE),
				"pblost" => (POSITIVE, NONE, NONE),
				"pbgold" => (POSITIVE, GOLD, GOLD),
				"nopbgold" => (NEGATIVE, GOLD, GOLD),
				_ => (NONE, NONE, NONE),
			};
		}

		/// <summary>
		/// The trend of LiveSplit's sign (any case, trimmed): Gold for any gold, else Ahead or Behind by the run (a PB
		/// counts as ahead, no PB as behind); null when the sign says neither.
		/// </summary>
		internal static string? Trend(string? sign) {
			return sign?.Trim().ToLowerInvariant() switch {
				"gold" or "goldahead" or "goldbehind" or "pbgold" or "nopbgold" => "Gold",
				"ahead" or "gainedahead" or "lostahead" or "pb" or "pbgained" or "pblost" => "Ahead",
				"behind" or "gainedbehind" or "lostbehind" or "nopb" or "nopbgained" or "nopblost" => "Behind",
				_ => null,
			};
		}

		/// <summary>
		/// The split list highlights' filter colours; a highlight whose key has no colour is left out.
		/// </summary>
		internal static List<FilterColor> HighlightFilters(IReadOnlyDictionary<string, string> colors) {
			return _highlights.Select(h => ToFilter(h.Source, HIGHLIGHT_FILTER, h.Key, colors)).OfType<FilterColor>().ToList();
		}

		/// <summary>
		/// The previous split's run, segment and gold filter colours for the sign; one whose key has no colour is left out.
		/// </summary>
		internal static List<FilterColor> PreviousFilters(IReadOnlyDictionary<string, string> colors, string? sign) {
			var (run, segment, gold) = SignKeys(sign);
			return new[] {
				ToFilter(PREVIOUS_RUN, PREVIOUS_FILTER, run, colors),
				ToFilter(PREVIOUS_SEGMENT, PREVIOUS_FILTER, segment, colors),
				ToFilter(PREVIOUS_GOLD, PREVIOUS_FILTER, gold, colors),
			}.OfType<FilterColor>().ToList();
		}

		/// <summary>
		/// The filter colour for a source from the key's colour; null when the key has none.
		/// </summary>
		static FilterColor? ToFilter(string source, string filter, string key, IReadOnlyDictionary<string, string> colors) {
			return colors.TryGetValue(key, out string? hex) ? new FilterColor(source, filter, PROPERTY, ColorUtil.ToOBS(hex)) : null;
		}
	}
}
