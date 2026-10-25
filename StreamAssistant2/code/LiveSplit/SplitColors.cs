namespace StreamAssistant2 {
	internal static class SplitColors {
		const string SCHEME_SET = "gameschemes";
		const string FALLBACK = "none";
		const string PROPERTY = "color_multiply";
		internal const string HIGHLIGHT_FILTER = "Color Correction 2";
		internal const string PREVIOUS_FILTER = "Color Correction";
		internal const string PREVIOUS_RUN = "Livesplit: Previous Run";
		internal const string PREVIOUS_SEGMENT = "Livesplit: Previous Segment";
		internal const string PREVIOUS_GOLD = "Livesplit: Previous Gold";
		// The left-panel groups that hold the highlights and the previous split's differences
		internal const string HIGHLIGHT_GROUP = "Splits List Highlights";
		internal const string PREVIOUS_GROUP = "Splits Previous Values";
		const string NONE = "None";
		const string POSITIVE = "LabelPositive";
		const string NEGATIVE = "LabelNegative";
		const string GOLD = "LabelGold";
		// What LiveSplit puts in front of a sign at the finish
		const string FINISHED = "finished";

		// The split list highlight sources and the split colour each takes
		internal static readonly (string Source, string Key)[] Highlights = [
			("Livesplit: SL Highlights Gold", "ListGold"),
			("Livesplit: SL Highlights AheadGained", "ListGainedAhead"),
			("Livesplit: SL Highlights AheadLost", "ListLostAhead"),
			("Livesplit: SL Highlights BehindGained", "ListGainedBehind"),
			("Livesplit: SL Highlights BehindLost", "ListLostBehind"),
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
		/// trimmed, a finish's like the same sign during the run); None for each with an unknown or blank sign.
		/// </summary>
		internal static (string Run, string Segment, string Gold) SignKeys(string? sign) {
			return InRun(sign) switch {
				"ahead" => (POSITIVE, NONE, NONE),
				"behind" => (NEGATIVE, NONE, NONE),
				"gainedahead" => (POSITIVE, POSITIVE, NONE),
				"gainedbehind" => (NEGATIVE, POSITIVE, NONE),
				"lostahead" => (POSITIVE, NEGATIVE, NONE),
				"lostbehind" => (NEGATIVE, NEGATIVE, NONE),
				"goldahead" => (POSITIVE, GOLD, GOLD),
				"goldbehind" => (NEGATIVE, GOLD, GOLD),
				_ => (NONE, NONE, NONE),
			};
		}

		/// <summary>
		/// The trend of LiveSplit's sign (any case, trimmed, a finish's like the same sign during the run): Gold for a gold,
		/// else Ahead or Behind by the run; null when the sign says neither.
		/// </summary>
		internal static string? Trend(string? sign) {
			return InRun(sign) switch {
				"goldahead" or "goldbehind" => "Gold",
				"ahead" or "gainedahead" or "lostahead" => "Ahead",
				"behind" or "gainedbehind" or "lostbehind" => "Behind",
				_ => null,
			};
		}

		/// <summary>
		/// The sign trimmed, in lower case and without a finish's leading "finished"; null for none.
		/// </summary>
		static string? InRun(string? sign) {
			string? trimmed = sign?.Trim().ToLowerInvariant();
			return trimmed != null && trimmed.StartsWith(FINISHED, StringComparison.Ordinal) ? trimmed[FINISHED.Length..] : trimmed;
		}

		/// <summary>
		/// The split list highlights' filter colours; a highlight whose key has no colour is left out.
		/// </summary>
		internal static List<FilterColor> HighlightFilters(IReadOnlyDictionary<string, string> colors) {
			return Highlights.Select(h => ToFilter(h.Source, HIGHLIGHT_FILTER, h.Key, colors)).OfType<FilterColor>().ToList();
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
