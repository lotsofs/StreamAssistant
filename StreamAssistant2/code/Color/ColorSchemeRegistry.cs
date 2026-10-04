using Newtonsoft.Json.Linq;

namespace StreamAssistant2 {
	public static class ColorSchemeRegistry {
		public class ThemeSet {
			public Dictionary<string, Category> Categories { get; } = new(ColorNameComparer.Instance);
		}

		public class Category {
			public Dictionary<string, Scheme> ColorSchemes { get; } = new(ColorNameComparer.Instance);
			public Dictionary<string, string> SplitColors { get; } = new(ColorNameComparer.Instance);
			public string Default { get; set; } = "";
		}

		public class Scheme {
			public string Outer { get; set; } = "";
			public string Inner { get; set; } = "";
			public string Text { get; set; } = "";
		}

		public static readonly Dictionary<string, ThemeSet> Sets = new(ColorNameComparer.Instance);

		public sealed record ColorSchemeData (string SetName, string CategoryName, string SchemeName, Scheme Scheme);

		public static void LoadSets() {
			Sets.Clear();
			foreach (string file in Directory.EnumerateFiles(Config.Data.Directories.ColorSchemes, "*.json")) {
				string name = Path.GetFileNameWithoutExtension(file);
				JObject root = JObject.Parse(File.ReadAllText(file));
				foreach (var (where, dropped, kept) in DropDuplicateNames(root)) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Colour scheme {name}{where}: skipped \"{dropped}\", same name as \"{kept}\"");
				}
				var ts = root.ToObject<ThemeSet>();
				if (ts != null) {
					NormalizeHex(name, ts);
					Sets[name] = ts;
				}
			}
		}

		/// <summary>
		/// Removes category and scheme names that ColorNameComparer.Instance treats as equal to an
		/// earlier one ("Green-Blue" then "Green Blue"), keeping the first. Returns what was dropped
		/// so the caller can log it.
		/// </summary>
		internal static List<(string Where, string Dropped, string Kept)> DropDuplicateNames(JObject root) {
			List<(string, string, string)> dropped = [];
			if (root["Categories"] is not JObject categories) {
				return dropped;
			}
			DropDuplicates(categories, "", dropped);
			foreach (JProperty category in categories.Properties()) {
				if (category.Value["ColorSchemes"] is JObject schemes) {
					DropDuplicates(schemes, $" {category.Name}", dropped);
				}
			}
			return dropped;
		}

		static void DropDuplicates(JObject names, string where, List<(string, string, string)> dropped) {
			Dictionary<string, string> firstSpelling = new(ColorNameComparer.Instance);
			foreach (JProperty property in names.Properties().ToList()) {
				if (firstSpelling.TryGetValue(property.Name, out string? kept)) {
					dropped.Add((where, property.Name, kept));
					property.Remove();
				}
				else {
					firstSpelling[property.Name] = property.Name;
				}
			}
		}

		/// <summary>
		/// Lowercases every scheme's Inner/Outer/Text to #rrggbb, and drops (with a log line) any
		/// scheme where one of them isn't a hex colour.
		/// </summary>
		static void NormalizeHex(string setName, ThemeSet set) {
			foreach (var (categoryName, category) in set.Categories) {
				List<string> invalid = [];
				foreach (var (schemeName, scheme) in category.ColorSchemes) {
					if (ColorUtil.TryNormalizeHex(scheme.Inner, out string inner)
						&& ColorUtil.TryNormalizeHex(scheme.Outer, out string outer)
						&& ColorUtil.TryNormalizeHex(scheme.Text, out string text)) {
						scheme.Inner = inner;
						scheme.Outer = outer;
						scheme.Text = text;
					}
					else {
						invalid.Add(schemeName);
					}
				}
				foreach (string schemeName in invalid) {
					var scheme = category.ColorSchemes[schemeName];
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Colour scheme {setName} {categoryName}: skipped \"{schemeName}\", not all of \"{scheme.Inner}\" \"{scheme.Outer}\" \"{scheme.Text}\" are hex colours");
					category.ColorSchemes.Remove(schemeName);
				}
			}
		}

		public static bool TryGetScheme(string input, out ColorSchemeData? data) {
			var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			data = null;
			if (TryGetSetCategoryScheme(parts, out data)) return true;
			if (TryGetCategorySchemeInAnySet(parts, out data)) return true;
			return false;
		}


		/// <summary>
		/// (any set [category] [scheme]) or (any set [category] default)
		/// </summary>
		/// <param name="parts"></param>
		/// <returns></returns>
		public static bool TryGetCategorySchemeInAnySet(string[] parts, out ColorSchemeData? cr) {
			cr = null;
			if (parts.Length < 1) return false;

			string categoryName = parts[0];
			string schemeName = string.Join(' ', parts.Skip(1));
			foreach (var (setName, set) in Sets) {
				if (!TryGetCategorySchemeInSet(set, categoryName, schemeName, out var scheme)) continue;
				if (string.IsNullOrWhiteSpace(schemeName)) {
					schemeName = "default";
				}
				cr = new ColorSchemeData(setName, categoryName, schemeName, scheme!);
				return true;
			}
			return false;
		}

		/// <summary>
		/// ([set] [category] [scheme]) or ([set] [category] default)
		/// </summary>
		/// <param name="parts"></param>
		/// <returns></returns>
		public static bool TryGetSetCategoryScheme(string[] parts, out ColorSchemeData? cr) {
			cr = null;
			if (parts.Length < 2) return false;
			
			string setName = parts[0];
			string categoryName = parts[1];
			string schemeName = string.Join(' ', parts.Skip(2));

			if (!TryGetSet(setName, out ThemeSet? set)) return false;
			if (!TryGetCategorySchemeInSet(set!, categoryName, schemeName, out var scheme)) return false;

			if (string.IsNullOrWhiteSpace(schemeName)) {
				schemeName = "default";
			}
			cr = new ColorSchemeData(setName, categoryName, schemeName, scheme!);
			return true;
		}

		public static bool TryGetCategorySchemeInSet(ThemeSet set, string categoryName, string schemeName, out Scheme? scheme) {
			scheme = null;
			if (!TryGetCategoryInSet(set!, categoryName, out var category)) return false;

			if (string.IsNullOrEmpty(schemeName)) {
				schemeName = category!.Default;
			}

			return TryGetSchemeInCategory(category!, schemeName, out scheme);
		}

		public static bool TryGetSchemeInCategory(Category category, string schemeName, out Scheme? scheme) {
			return category.ColorSchemes.TryGetValue(schemeName, out scheme);
		}

		public static bool TryGetCategoryInSet(ThemeSet set, string categoryName, out Category? category) {
			return set.Categories.TryGetValue(categoryName, out category);
		}

		public static bool TryGetSet(string setName, out ThemeSet? set) {
			return Sets.TryGetValue(setName, out set);
		}
	}
}
