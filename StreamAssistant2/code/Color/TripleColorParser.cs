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
			// Eg. "navy blue, red" → tokens [navy, blue, red], gaps [0, 1] (the space, then the comma).
			Tokenize(input, out List<string> tokenList, out List<int> gaps);
			if (tokenList.Count > MAX_TOKENS) {
				tokenList = tokenList.GetRange(0, MAX_TOKENS);
				gaps = gaps.GetRange(0, MAX_TOKENS - 1);
			}
			// An array, because only string.Join's array overload can join a sub-range without copying.
			string[] tokens = tokenList.ToArray();

			// totalWeightBeforeToken[k] adds up every separator before token k, so [0] is 0.
			// The weight between token a and token b is then [b] - [a].
			int[] totalWeightBeforeToken = new int[gaps.Count + 1];
			for (int k = 0; k < gaps.Count; k++) {
				totalWeightBeforeToken[k + 1] = totalWeightBeforeToken[k] + gaps[k];
			}

			// A "length" is a candidate span size: how many tokens, starting at i, to try as one colour.
			List<int> lengths = new();
			int i = 0;
			while (i < tokens.Length && found.Count < 3) {
				// Every size that fits from here: 1, 2, … up to the tokens left.
				lengths.Clear();
				for (int length = 1; length <= tokens.Length - i; length++) {
					lengths.Add(length);
				}

				// Reorder into try-order: most tightly bound first (lowest mean separator weight),
				// and on a tie the longer span first, so a multi-word name beats its own first word.
				// "navy blue, red" at i = 0: means 0, 0, 0.5 for lengths 1, 2, 3 → order [2, 1, 3].
				int start = i;
				lengths.Sort((a, b) => {
					int byWeight = MeanWeight(totalWeightBeforeToken, start, a).CompareTo(MeanWeight(totalWeightBeforeToken, start, b));
					return byWeight == 0 ? b.CompareTo(a) : byWeight;
				});

				// Take the first span in that order that resolves to a colour.
				int consumed = 0;
				foreach (int length in lengths) {
					// Space-joined: what the table and scheme lookups need to see, and what the
					// widened numeric regexes accept.
					if (SingleColorParser.TryParse(string.Join(' ', tokens, i, length), out ColorEntry? entry)) {
						found.Add(entry!);
						consumed = length;
						break;
					}
				}

				// Continue after the span that matched; if nothing starting here matched, skip this token.
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
		/// The span's gaps are gaps[start .. start+length-2], summed via totalWeightBeforeToken.
		/// </summary>
		static double MeanWeight(int[] totalWeightBeforeToken, int start, int length) {
			if (length <= 1) {
				return 0.0;
			}
			int sum = totalWeightBeforeToken[start + length - 1] - totalWeightBeforeToken[start];
			return (double)sum / (length - 1);
		}
	}
}
