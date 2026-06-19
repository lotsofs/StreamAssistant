// using Newtonsoft.Json;
using System.Text.RegularExpressions;

using System.Drawing;
using Newtonsoft.Json;

namespace StreamAssistant2 {
	public static class ColorSchemes {
		public class ThemeSet {
			public Dictionary<string, Category> Categories { get; set; } = [];
		}

		public class Category {
			public string Default { get; set; } = "";
			public Dictionary<string, Scheme> ColorSchemes { get; set; } = [];
			public Dictionary<string, string>? SplitColors { get; set; }
		}

		public class Scheme {
			public string Outer { get; set; } = "";
			public string Inner { get; set; } = "";
			public string Text { get; set; } = "";
		}

		public static readonly Dictionary<string, ThemeSet> Sets = new(new ColorNameComparer());
		
		public sealed record ColorSchemeData (string SetName, string CategoryName, string SchemeName, Scheme Scheme);

		public static void LoadSets() {
			Sets.Clear();
			foreach (string file in Directory.EnumerateFiles(Config.Data.Directories.ColorSchemes, "*.json")) {
				string name = Path.GetFileNameWithoutExtension(file);
				var json = File.ReadAllText(file);
				var ts = JsonConvert.DeserializeObject<ThemeSet>(json);
				if (ts != null) {
					Sets[name] = ts;
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
			string categoryName = parts[0];
			string schemeName = string.Join(' ', parts.Skip(1));
			foreach (var (setName, set) in Sets) {
				if (!TryGetCategorySchemeInSet(set, categoryName, schemeName, out var scheme)) continue;
				cr = new ColorSchemeData(setName, categoryName, schemeName, scheme);
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

			cr = new ColorSchemeData(setName, categoryName, schemeName, scheme);
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
		
		public static ThemeSet? FindThemeSet(string name) {
			return Sets.TryGetValue(name, out ThemeSet? value) ? value : null;
		}

		public static Category? FindCategory(ThemeSet set, string name) {
			return set.Categories.TryGetValue(name, out Category? value) ? value : null;
		}
	}
}
