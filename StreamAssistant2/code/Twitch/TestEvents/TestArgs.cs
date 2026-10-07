namespace StreamAssistant2 {
	/// <summary>
	/// Parses a test script's arguments. Required parameters come first, in order. The optional ones
	/// follow either as keywords ("late missing 2 msg hi there") or positionally ("true 2 hi there");
	/// positional is chosen when the first optional word is a boolean or a number.
	/// </summary>
	internal static class TestArgs {
		internal abstract record Param(string Name);
		internal record IntParam(string Name, int Default, int Min, int Max) : Param(Name);
		internal record BoolParam(string Name) : Param(Name);
		/// <summary>Takes the rest of the line, so it must be the last parameter.</summary>
		internal record TextParam(string Name) : Param(Name);

		internal class Values {
			readonly Dictionary<string, object> _values = new();

			internal int Int(string name) => (int)_values[name];
			internal bool Bool(string name) => (bool)_values[name];
			internal string Text(string name) => (string)_values[name];
			internal void Set(string name, object value) => _values[name] = value;

			public override string ToString() => string.Join(", ", _values.Select(kv => $"{kv.Key} {kv.Value}"));
		}

		internal static Values? Parse(string argument, IReadOnlyList<Param> required, IReadOnlyList<Param> optional) {
			string[] words = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			Values values = new();
			foreach (Param p in optional) {
				values.Set(p.Name, Default(p));
			}

			int i = 0;
			foreach (Param p in required) {
				if (!TakeValue(p, words, ref i, values) || (p is TextParam && values.Text(p.Name).Length == 0)) {
					return null;
				}
			}
			if (i == words.Length) {
				return values;
			}

			if (bool.TryParse(words[i], out _) || int.TryParse(words[i], out _)) {
				foreach (Param p in optional) {
					if (i == words.Length) {
						break;
					}
					if (!TakeValue(p, words, ref i, values)) {
						return null;
					}
				}
				return i == words.Length ? values : null;
			}

			while (i < words.Length) {
				Param? p = optional.FirstOrDefault(o => o.Name.Equals(words[i], StringComparison.OrdinalIgnoreCase));
				if (p == null) {
					return null;
				}
				i++;
				if (p is BoolParam) {
					values.Set(p.Name, true);
				}
				else if (!TakeValue(p, words, ref i, values)) {
					return null;
				}
			}
			return values;
		}

		static bool TakeValue(Param p, string[] words, ref int i, Values values) {
			if (i >= words.Length) {
				return false;
			}
			switch (p) {
				case IntParam ip:
					if (!int.TryParse(words[i], out int n) || n < ip.Min || n > ip.Max) {
						return false;
					}
					values.Set(p.Name, n);
					i++;
					return true;
				case BoolParam:
					if (!bool.TryParse(words[i], out bool b)) {
						return false;
					}
					values.Set(p.Name, b);
					i++;
					return true;
				case TextParam:
					values.Set(p.Name, string.Join(' ', words[i..]));
					i = words.Length;
					return true;
				default:
					return false;
			}
		}

		static object Default(Param p) {
			return p switch {
				IntParam ip => ip.Default,
				BoolParam => false,
				_ => "",
			};
		}

		internal static string Usage(string name, IReadOnlyList<Param> required, IReadOnlyList<Param> optional) {
			IEnumerable<string> req = required.Select(p => $"<{p.Name}>");
			IEnumerable<string> keywords = optional.Select(p => p switch {
				BoolParam => $"[{p.Name}]",
				TextParam => $"[{p.Name} <text…>]",
				_ => $"[{p.Name} <n>]",
			});
			IEnumerable<string> positional = optional.Select(p => $"<{p.Name}>");
			string line = string.Join(' ', req.Prepend($"!test {name}").Concat(keywords));
			if (optional.Count > 0) {
				line += " | " + string.Join(' ', req.Prepend($"!test {name}").Concat(positional));
			}
			return line;
		}
	}
}
