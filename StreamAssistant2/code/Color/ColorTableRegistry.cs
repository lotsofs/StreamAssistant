using Newtonsoft.Json;

namespace StreamAssistant2 {
	/// <summary>
	/// Loads every colour table JSON file and resolves colour names across them.
	/// </summary>
	public static class ColorTableRegistry {
		/// <summary>
		/// The tables and every entry of them, for the random pick, from one load.
		/// </summary>
		sealed record Loaded(Dictionary<string, ColorTable> Tables, NamedColor[] RandomPool);

		static Loaded _loaded = new(new(ColorNameComparer.Instance), []);

		public static Dictionary<string, ColorTable> Tables => _loaded.Tables;

		/// <summary>
		/// Loads every table file into new tables, swapped in at once; a file that can't be loaded logs and keeps its
		/// table from before, and a folder that can't be listed keeps them all.
		/// </summary>
		public static void LoadTables() {
			string[]? files = Coloring.ListFiles(Config.Data.Directories.Colors);
			if (files == null) {
				return;
			}
			Dictionary<string, ColorTable> old = Tables;
			Dictionary<string, ColorTable> tables = new(ColorNameComparer.Instance);
			foreach (string file in files) {
				string source = Path.GetFileNameWithoutExtension(file);
				try {
					if (LoadTable(file, source) is ColorTable table) {
						tables[source] = table;
					}
				}
				catch (Exception ex) {
					Coloring.LoadFailed(file, ex);
					if (old.TryGetValue(source, out ColorTable? kept)) {
						tables[source] = kept;
					}
				}
			}
			_loaded = new(tables, tables.Values.SelectMany(t => t.Entries.Values).ToArray());
		}

		/// <summary>
		/// One table file, logging the entries it skips; null for a file that holds null.
		/// </summary>
		static ColorTable? LoadTable(string file, string source) {
			var dict = JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(file));
			if (dict == null) {
				return null;
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
			return new ColorTable(entries);
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
			string fullName = string.Join(' ', parts);
			foreach (var table in Tables.Values) {
				if (TryGetColorFromTable(table, fullName, loose, out namedColor)) {
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

		/// <summary>
		/// A random table colour, or a random #rrggbb when no tables are loaded.
		/// </summary>
		public static NamedColor GetRandomColor() {
			NamedColor[] pool = _loaded.RandomPool;
			if (pool.Length == 0) {
				string hex = ColorUtil.ToHex(Random.Shared.Next(256), Random.Shared.Next(256), Random.Shared.Next(256));
				return new NamedColor("random rgb", hex, hex);
			}
			return pool[Random.Shared.Next(pool.Length)];
		}
	}
}
