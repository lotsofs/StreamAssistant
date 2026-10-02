// using Newtonsoft.Json;
using System.Text.RegularExpressions;

using System.Drawing;
using Newtonsoft.Json;

namespace StreamAssistant2 {
	public static class ColorTables {
		public class Table {
			Dictionary<string, NamedColor> _entries = new(new ColorNameComparer());
			public Dictionary<string, NamedColor> Entries { 
				get => _entries; 
				set => _entries = new Dictionary<string, NamedColor>(value, new ColorNameComparer());
			}
		}
		
		public sealed record NamedColor(string Source, string OriginalName, string Hex);
		
		public static readonly Dictionary<string, Table> Tables = new (new ColorNameComparer());

		public static void LoadTables() {
			Tables.Clear();
			foreach (string file in Directory.EnumerateFiles(Config.Data.Directories.Colors, "*.json")) {
				string source = Path.GetFileNameWithoutExtension(file);
				var json = File.ReadAllText(file);
				var dict = JsonConvert.DeserializeObject<Dictionary<string,string>>(json);
				if (dict == null) {
					continue;
				}
				Tables[source] = new Table();
				foreach (var (name, hex) in dict) {
					Tables[source].Entries[name] = new NamedColor(source, name, hex);
				}
			}
		}

		public static bool TryGetTableColor(string input, out NamedColor? namedColor) {
			var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			namedColor = null;
			if (parts.Length >= 2) {
				string tableName = parts[0];
				string colorName = string.Join(' ', parts.Skip(1));
				if (TryGetTable(tableName, out var table) && TryGetColorFromTable(table!, colorName, out namedColor)) {
					return true;
				}
			}
			foreach (var key in Tables.Keys) {
				var table = Tables[key];
				string colorName = string.Join(' ', parts);
				if (TryGetColorFromTable(table, colorName, out namedColor)) {
					return true;
				}
			}
			return false;
		}

		public static bool TryGetTable(string tableName, out Table? table) {
			table = null;
			return Tables.TryGetValue(tableName, out table);
		}

		public static bool TryGetColorFromTable(Table table, string colorName, out NamedColor? namedColor) {
			namedColor = null;
			return table.Entries.TryGetValue(colorName, out namedColor);
		}

		public static NamedColor GetRandomColor() {
			var colors = Tables["encycolorpedia"].Entries;
			int r = Random.Shared.Next(0, colors.Count);
			var item = colors.ElementAt(r);
			return item.Value;
		}
	}
}
