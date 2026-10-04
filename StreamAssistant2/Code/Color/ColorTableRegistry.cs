// using Newtonsoft.Json;
using System.Text.RegularExpressions;

using System.Drawing;
using Newtonsoft.Json;

namespace StreamAssistant2 {
	/// <summary>
	/// Loads every colour table JSON file and resolves colour names across them.
	/// </summary>
	public static class ColorTableRegistry {
		public static readonly Dictionary<string, ColorTable> Tables = new (ColorNameComparer.Instance);

		public static void LoadTables() {
			Tables.Clear();
			foreach (string file in Directory.EnumerateFiles(Config.Data.Directories.Colors, "*.json")) {
				string source = Path.GetFileNameWithoutExtension(file);
				var json = File.ReadAllText(file);
				var dict = JsonConvert.DeserializeObject<Dictionary<string,string>>(json);
				if (dict == null) {
					continue;
				}
				Dictionary<string, NamedColor> entries = new(ColorNameComparer.Instance);
				foreach (var (name, value) in dict) {
					if (!ColorUtil.TryNormalizeHex(value, out string hex)) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Colour table {source}: skipped \"{name}\", \"{value}\" is not a hex colour");
						continue;
					}
					if (!entries.TryAdd(name, new NamedColor(source, name, hex))) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Colour table {source}: skipped \"{name}\", same name as \"{entries[name].OriginalName}\"");
					}
				}
				Tables[source] = new ColorTable(entries);
			}
		}

		/// <param name="loose">Match colour names through ColorTable.TryGetLoose instead of strictly. Table names are always strict.</param>
		public static bool TryGetTableColor(string input, bool loose, out NamedColor? namedColor) {
			var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			namedColor = null;
			if (parts.Length >= 2) {
				string tableName = parts[0];
				string colorName = string.Join(' ', parts.Skip(1));
				if (TryGetTable(tableName, out var table) && TryGetColorFromTable(table!, colorName, loose, out namedColor)) {
					return true;
				}
			}
			foreach (var key in Tables.Keys) {
				var table = Tables[key];
				string colorName = string.Join(' ', parts);
				if (TryGetColorFromTable(table, colorName, loose, out namedColor)) {
					return true;
				}
			}
			return false;
		}

		public static bool TryGetTable(string tableName, out ColorTable? table) {
			return Tables.TryGetValue(tableName, out table);
		}

		public static bool TryGetColorFromTable(ColorTable table, string colorName, bool loose, out NamedColor? namedColor) {
			return loose ? table.TryGetLoose(colorName, out namedColor) : table.Entries.TryGetValue(colorName, out namedColor);
		}

		public static NamedColor GetRandomColor() {
			var colors = Tables["encycolorpedia"].Entries;
			int r = Random.Shared.Next(0, colors.Count);
			var item = colors.ElementAt(r);
			return item.Value;
		}
	}
}
