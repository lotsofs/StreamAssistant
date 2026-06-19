namespace StreamAssistant2 {
	public class ColorNameComparer : IEqualityComparer<string> {
		public bool Equals(string? x, string? y) {
			if (ReferenceEquals(x, y)) return true;
			if (x == null && y == null) return true;
			if (x == null || y == null) return false;

			return Normalize(x) == Normalize(y);
		}

		public int GetHashCode(string obj) {
			if (obj is null) return 0;
			return Normalize(obj).GetHashCode();
		}

		static string Normalize(string input) {
			var buffer = new char[input.Length];
			int pos = 0;
			foreach (char c in input) {
				if (char.IsLetterOrDigit(c)) {
					buffer[pos++] = char.ToLowerInvariant(c);
				}
			}
			return new string(buffer, 0, pos);
		}
	}
}
