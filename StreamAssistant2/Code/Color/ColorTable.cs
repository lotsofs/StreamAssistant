using System.Text.RegularExpressions;

namespace StreamAssistant2 {
	public sealed record NamedColor(string Source, string OriginalName, string Hex);

	/// <summary>
	/// One colour table: the name → colour entries of a single JSON file, plus the loose fallback
	/// for names with accents or other non-ASCII letters.
	/// </summary>
	public class ColorTable {
		public Dictionary<string, NamedColor> Entries { get; }

		// The loose fallback, built only from entries whose name contains non-ASCII.
		readonly Dictionary<string, NamedColor> _looseEntries = [];
		readonly List<(Regex Pattern, NamedColor Color)> _wildcards = [];

		/// <param name="entries">The finished entries, keyed with ColorNameComparer.Instance.</param>
		public ColorTable(Dictionary<string, NamedColor> entries) {
			Entries = entries;
			foreach (var (name, color) in entries) {
				if (name.All(char.IsAscii)) {
					continue;
				}
				string folded = ColorNameComparer.Fold(name);
				if (folded.All(char.IsAscii)) {
					// Plain accents: Café is stored under "cafe".
					_looseEntries.TryAdd(folded, color);
				}
				else if (folded.Any(char.IsAscii)) {
					// Letters that don't fold (ŧ ı ð ß æ …) match 1 or 2 input letters: Straße → ^stra.{1,2}e$.
					// A name with no ASCII letter at all gets nothing, so it can't match everything.
					string pattern = "^" + string.Concat(folded.Select(c => char.IsAscii(c) ? Regex.Escape(c.ToString()) : ".{1,2}")) + "$";
					_wildcards.Add((new Regex(pattern, RegexOptions.CultureInvariant), color));
				}
			}
		}

		/// <summary>
		/// The fallback once the strict lookup has missed in every table: accented input finds a
		/// plain name (café → Cafe), plain input finds an accented one (cafe → Café), and
		/// wildcard letters match last (strasse → Straße).
		/// </summary>
		public bool TryGetLoose(string name, out NamedColor? color) {
			string folded = ColorNameComparer.Fold(name);
			if (Entries.TryGetValue(folded, out color) || _looseEntries.TryGetValue(folded, out color)) {
				return true;
			}
			foreach (var (pattern, candidate) in _wildcards) {
				if (pattern.IsMatch(folded)) {
					color = candidate;
					return true;
				}
			}
			color = null;
			return false;
		}
	}
}
