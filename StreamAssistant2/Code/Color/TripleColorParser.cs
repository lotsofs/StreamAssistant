using static StreamAssistant2.Coloring;

namespace StreamAssistant2 {
	/// <summary>
	/// Partitions a chat string into up to three colours (inner/outer/text). See CLAUDE.md for
	/// the separator-weight rule; it is not guessable from the code alone.
	/// </summary>
	public static class TripleColorParser {
		const int MAX_TOKENS = 32;	// Limit user input length

		public static bool TryParse(string input, out ColorEntry? colorEntry) {
			colorEntry = null;

			// EMPTY
			if (string.IsNullOrWhiteSpace(input)) {
				return false;
			}

			List<ColorEntry> found = [];
			Collect(input.Trim(), found);

			if (found.Count == 0) {
				return false;
			}

			// Hex1/Hex2/Hex3 are inner/outer/text. Pad a missing slot from the first entry's
			// own channel for that slot, not from its base colour: that keeps a scheme's
			// designed Outer/Text intact, and gives a plain colour its Darken/Lighten pair.
			ColorEntry first = found[0];
			string inner = first.Hex1;
			string outer = found.Count > 1 ? found[1].Hex1 : first.Hex2;
			string text = found.Count > 2 ? found[2].Hex1 : first.Hex3;
			string source = string.Join("|", found.Select(e => e.Source));
			string name = string.Join("|", found.Select(e => e.Name));
			colorEntry = new ColorEntry(source, name, inner, outer, text);
			return true;
		}

		/// <summary>
		/// Check if char c is a separator character
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		static bool IsSeparator(char c) {
			return c == ' ' || c == '\t' || c == ',' || c == ';';
		}

		/// <summary>
		/// How strongly a separator pushes two tokens apart. Space costs nothing, so multi-word
		/// names bind tightest; a semicolon separates harder than a comma.
		/// </summary>
		static int SeparatorWeight(char c) => c switch {
			',' => 1,
			';' => 2,
			_ => 0,
		};

		/// <summary>
		/// Walks the request left to right. At each position the candidate spans are ordered by
		/// mean separator weight ascending, ties broken by the longest span, and the first one
		/// that resolves is taken. So "navy blue" is one colour while "navy,blue" is two, and
		/// "crayola,navy,blue" is one again because nothing more tightly bound matches.
		/// </summary>
		static void Collect(string input, List<ColorEntry> found) {
			Tokenize(input, out List<string> tokens, out List<int> gaps);
			if (tokens.Count > MAX_TOKENS) {
				tokens = tokens.GetRange(0, MAX_TOKENS);
				gaps = gaps.GetRange(0, MAX_TOKENS - 1);
			}

			List<int> lengths = new();
			int i = 0;
			while (i < tokens.Count && found.Count < 3) {
				lengths.Clear();
				for (int length = 1; length <= tokens.Count - i; length++) {
					lengths.Add(length);
				}
				int start = i;
				lengths.Sort((a, b) => {
					int byWeight = MeanWeight(gaps, start, a).CompareTo(MeanWeight(gaps, start, b));
					return byWeight != 0 ? byWeight : b.CompareTo(a);
				});

				int consumed = 0;
				foreach (int length in lengths) {
					// Space-joined: what the table and scheme lookups need to see, and what the
					// widened numeric regexes accept.
					if (SingleColorParser.TryParse(string.Join(' ', tokens.GetRange(i, length)), out ColorEntry? entry)) {
						found.Add(entry!);
						consumed = length;
						break;
					}
				}
				i += consumed > 0 ? consumed : 1;
			}
		}

		/// <summary>
		/// Splits into maximal separator-free tokens. gaps[k] is the strength of the separator
		/// run between tokens[k] and tokens[k+1], taking the strongest separator in the run, so
		/// gaps.Count == tokens.Count - 1.
		/// </summary>
		static void Tokenize(string input, out List<string> tokens, out List<int> gaps) {
			tokens = new();
			gaps = new();
			int i = 0;
			while (i < input.Length) {
				int weight = 0;
				while (i < input.Length && IsSeparator(input[i])) {
					weight = Math.Max(weight, SeparatorWeight(input[i]));
					i++;
				}
				if (i >= input.Length) {
					break;
				}
				if (tokens.Count > 0) {
					gaps.Add(weight);
				}
				int start = i;
				while (i < input.Length && !IsSeparator(input[i])) {
					i++;
				}
				tokens.Add(input[start..i]);
			}
		}

		/// <summary>
		/// Mean strength of the separators inside a span. A single token contains none, so 0 —
		/// the tightest binding there is. Averaging rather than summing is what lets a longer
		/// name beat its own shorter prefix ("Zed,Zed,Zed,Zed" over "Zed,Zed,Zed") while still
		/// losing to a more tightly bound alternative ("crayola navy blue" over "crayola,navy").
		/// </summary>
		static double MeanWeight(List<int> gaps, int start, int length) {
			if (length <= 1) {
				return 0.0;
			}
			int sum = 0;
			for (int k = start; k < start + length - 1; k++) {
				sum += gaps[k];
			}
			return (double)sum / (length - 1);
		}
	}
}
