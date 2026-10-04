namespace StreamAssistant2 {
	/// <summary>
	/// Compares colour names by their letters and digits only, case-insensitively, so
	/// "Green-Blue", "green blue" and "GREENBLUE" are the same key. Stateless; use Instance.
	/// </summary>
	public class ColorNameComparer : IEqualityComparer<string> {
		public static readonly ColorNameComparer Instance = new();

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
