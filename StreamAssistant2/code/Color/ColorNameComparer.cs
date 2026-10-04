using System.Text;

namespace StreamAssistant2 {
	/// <summary>
	/// Compares colour names by their letters and digits only, case-insensitively, so
	/// "Green-Blue", "green blue" and "GREENBLUE" are the same key. Accents are significant.
	/// Colour names in tables fall back to <see cref="ColorTable.TryGetLoose"/> once the
	/// strict lookup has missed everywhere.
	/// </summary>
	public class ColorNameComparer : IEqualityComparer<string> {
		public static readonly ColorNameComparer Instance = new();

		/// <summary>
		/// Letters and digits only, lowercased, with accents folded to their base letter (é → e,
		/// Ō → o). Letters Unicode doesn't decompose into one base letter plus marks (ŧ, ı, ð, æ, ß)
		/// are kept as they are; ColorTable.TryGetLoose treats those as wildcards.
		/// </summary>
		public static string Fold(string name) {
			StringBuilder folded = new(name.Length);
			foreach (char c in name) {
				if (!char.IsLetterOrDigit(c)) {
					continue;
				}
				char lower = char.ToLowerInvariant(c);
				if (char.IsAscii(lower)) {
					folded.Append(lower);
					continue;
				}
				char baseLetter = '\0';
				int letters = 0;
				foreach (char part in lower.ToString().Normalize(NormalizationForm.FormD)) {
					if (char.IsLetterOrDigit(part)) {
						baseLetter = part;
						letters++;
					}
				}
				folded.Append(letters == 1 ? baseLetter : lower);
			}
			return folded.ToString();
		}

		public bool Equals(string? x, string? y) {
			if (ReferenceEquals(x, y)) return true;
			if (x == null || y == null) return false;

			// Walk both strings in step, skipping everything that isn't a letter or digit.
			int i = 0;
			int j = 0;
			while (true) {
				while (i < x.Length && !char.IsLetterOrDigit(x[i])) i++;
				while (j < y.Length && !char.IsLetterOrDigit(y[j])) j++;
				if (i == x.Length || j == y.Length) {
					return i == x.Length && j == y.Length;
				}
				if (char.ToLowerInvariant(x[i]) != char.ToLowerInvariant(y[j])) {
					return false;
				}
				i++;
				j++;
			}
		}

		public int GetHashCode(string obj) {
			if (obj is null) return 0;
			HashCode hash = new();
			foreach (char c in obj) {
				if (char.IsLetterOrDigit(c)) {
					hash.Add(char.ToLowerInvariant(c));
				}
			}
			return hash.ToHashCode();
		}
	}
}
