using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static StreamAssistant2.Coloring;
using static StreamAssistant2.Obs;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace StreamAssistant2 {
	internal static class Coloring {
		class ColorNameComparer : IEqualityComparer<string> {
			public ColorNameComparer() { }

			public bool Equals(string? x, string? y) {
				if (x == null && y == null) return true;
				if (x == null || y == null) return false;

				x = Regex.Replace(x, "[^a-zA-Z0-9]", "").ToLower();
				y = Regex.Replace(y, "[^a-zA-Z0-9]", "").ToLower();
				return x == y;
			}

			public int GetHashCode(string obj) {
				obj = Regex.Replace(obj, "[^a-zA-Z0-9]", "").ToLower();
				return obj.GetHashCode();
			}
		}

		private class ColorItem {
			public Color Color;
			public string Source;
			public int Index;

			public ColorItem(Color color, string source, int index) {
				Color = color;
				Source = source;
				Index = index;
			}
		}

		private class ColorScheme {
			public string Inner;
			public string Outer;
			public string Text;
		}

		private class ColorSchemeCategory {
			public string Default;
			public Dictionary<string, ColorScheme> ColorSchemes;
		}

		private class ColorSchemeTable {
            public Dictionary<string, ColorSchemeCategory> Categories;
        }
		
		const string COLOR_FILES_DIRECTORY = @"D:\Repositories\Stream-Resources\Input\Streamerbot\";
		static readonly char[] COMMAE_SPACE_AND_GROUPERS = { ' ', ',', ';', '(', ')', '<', '>', '{', '}', '\'', '"' };
		static readonly char[] COMMAE = { ',', ';' };
		static readonly char[] COMMAE_AND_SPACE = { ',', ';', ' ' };

		static string[] SimpleColorFiles = { "basiccolors", "htmlcolors", "encycolorpedia", "crayola", };
		static string[] ColorSchemeFiles = { "gameschemes", "countryschemes", };

		// ---------------file  , categories      -------------------------------
		static Dictionary<string, ColorSchemeTable> _colorSchemeFiles = new Dictionary<string, ColorSchemeTable>(new ColorNameComparer());
		// ---------------file  ,------------name  , hex   ----------------------
		static Dictionary<string, Dictionary<string, string>> _colorDatabases = new Dictionary<string, Dictionary<string, string>>(new ColorNameComparer());

		static DateTime _nextColorChangeOpportunity = DateTime.MinValue;

		public static void ReadFiles() {
			LoadColorSchemes();
			LoadColorTables();
		}

		private static void LoadColorSchemes() {
			_colorSchemeFiles.Clear();
			foreach (string file in ColorSchemeFiles) {
				string path = Path.Combine(COLOR_FILES_DIRECTORY, "ColorSchemes", file + ".json");
				string json = File.ReadAllText(path).ToLower();
				ColorSchemeTable scheme = JsonConvert.DeserializeObject<ColorSchemeTable>(json) ?? new ColorSchemeTable();
				Dictionary<string, ColorSchemeCategory> newDict = new Dictionary<string, ColorSchemeCategory>(scheme.Categories, new ColorNameComparer());
				scheme.Categories = newDict;
				foreach (var category in scheme.Categories) {
					Dictionary<string, ColorScheme> newDictSub = new Dictionary<string, ColorScheme>(category.Value.ColorSchemes, new ColorNameComparer());
					category.Value.ColorSchemes = newDictSub;
				}
				_colorSchemeFiles.Add(file, scheme);
			}
		}

		private static void LoadColorTables() {
			_colorDatabases.Clear();
			foreach (string file in SimpleColorFiles) {
				string path = Path.Combine(COLOR_FILES_DIRECTORY, "Colors", file + ".json");
				string json = File.ReadAllText(path).ToLower();
				Dictionary<string, string> deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
				Dictionary<string, string> items = new Dictionary<string, string>(deserialized, new ColorNameComparer());
				_colorDatabases.Add(file, items);
			}
		}

		public static void RandomColor() {
			int t = _colorDatabases["encycolorpedia"].Count;
			System.Random random = new System.Random();
			int r = random.Next(0, t);
			var item = _colorDatabases["encycolorpedia"].ElementAt(r);
			List<ColorItem> l = CheckSimpleColorFile("encycolorpedia", item.Key);
			Color col1 = l[0].Color;
			Color col2 = DarkenColor(col1);
			Color col3 = LightenColor(col1);

			TimeSpan wait = _nextColorChangeOpportunity - DateTime.Now;
			int waitInMs = (int)wait.TotalMilliseconds;
			if (waitInMs < 0) {
				waitInMs = 0;
			}
			_nextColorChangeOpportunity = DateTime.Now + TimeSpan.FromMilliseconds(15000 + waitInMs);

			string logSources = string.Format("Changing colors to {0:x6} {1:x6} {2:x6} ( {3} )",
				$"#{col1.R:X2}{col1.G:X2}{col1.B:X2}",
				$"#{col2.R:X2}{col2.G:X2}{col2.B:X2}",
				$"#{col3.R:X2}{col3.G:X2}{col3.B:X2}",
				l[0].Source
			);

			MsgQueue.TimedEnqueue(waitInMs, MsgTypes.ChatMsg, logSources, false);

			Obs.ChangeLayoutColor(col1, col2, col3, waitInMs);
		}

		public static void ChangeColor(string to, bool reportToChat = true, bool single = false) {
			List<ColorItem> colorItems = ReadColor(to);
			Color col1;
			Color col2;
			Color col3;
			if (colorItems.Count == 0) {
				MsgQueue.Enqueue(MsgTypes.ChatMsg, "No valid color provided", false);
				return;
			}
			col1 = colorItems[0].Color;
			if ((colorItems.Count >= 3 && (colorItems[2].Source == "THEME" || !single))) {
				col3 = colorItems[2].Color;
			}
			else {
				col3 = LightenColor(col1);
			}

			if ((colorItems.Count >= 2 && (colorItems[1].Source == "THEME" || !single))) {
				col2 = colorItems[1].Color;
			}
			else {
				col2 = DarkenColor(col1);
			}

			TimeSpan wait = _nextColorChangeOpportunity - DateTime.Now;
			int waitInMs = (int)wait.TotalMilliseconds;
			if (waitInMs < 0) {
				waitInMs = 0;
			}
			_nextColorChangeOpportunity = DateTime.Now + TimeSpan.FromMilliseconds(15000 + waitInMs);

			if (reportToChat) {
				string logSources = string.Format("Changing colors to {0:x6} {1:x6} {2:x6} ( ", 
					$"#{col1.R:X2}{col1.G:X2}{col1.B:X2}",
					$"#{col2.R:X2}{col2.G:X2}{col2.B:X2}",
					$"#{col3.R:X2}{col3.G:X2}{col3.B:X2}"
				);
				
				for (int i = 0; i < colorItems.Count; i++) {
					int tossIndex = (!single || (colorItems.Count >= 3 && colorItems[2].Source == "THEME")) ? 3 : 1;
					if (i == tossIndex) {
						logSources += " ) ( Tossing: ";
					}
					var item = colorItems[i];
					if (!string.IsNullOrEmpty(item.Source) && item.Source != "THEME") {
						logSources += item.Source.Replace(" ", "") + " "; 
					}
				}
				logSources += ")";
				MsgQueue.TimedEnqueue(waitInMs, MsgTypes.ChatMsg, logSources, false);
			}

			Obs.ChangeLayoutColor(col1, col2, col3, waitInMs);
		}

		static List<ColorItem> ReadColor(string query) {
			List<ColorItem> colorItems = new List<ColorItem>();
			colorItems.AddRange(CheckColorSchemes(query));
			if (colorItems.Count > 0) { 
				// Color Schemes always come with 3 colors
				return colorItems; 
			}
			colorItems.AddRange(CheckHex(query));
			colorItems.AddRange(CheckRgb(query));
			colorItems.AddRange(CheckColorNames(query));
			colorItems.AddRange(CheckSystem(query));

			colorItems.RemoveAll(x => x.Color == Color.Empty);
			colorItems = colorItems.DistinctBy(x => x.Index).ToList();
			colorItems = colorItems.OrderBy(x => x.Index).ToList();
			return colorItems;
		}

		static List<ColorItem> CheckSystem(string query) {
			// System colors
			List<ColorItem> colors = new List<ColorItem>();
			string[] split = query.Split(COMMAE, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int i = 0; i < split.Length; i++) {
				try {
					Color c = ColorTranslator.FromHtml(split[i]);
					if (c != Color.Empty) {
						colors.Add(new ColorItem(c, "system", i));
						continue;
					}

					if (int.TryParse(split[i], out int ole)) {
						c = ColorTranslator.FromOle(ole);
					}
					if (c != Color.Empty) {
						colors.Add(new ColorItem(c, "ole", i));
						continue;
					}
				}
				catch (Exception e) {
					// Not a system color
					continue;
				}
			}
			return colors;
		}

		static List<ColorItem> CheckHex(string query) {
			// Find any hex codes and process them.
			List<ColorItem> colors = new List<ColorItem>();
			string[] split = query.Split(COMMAE_SPACE_AND_GROUPERS, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int i = 0; i < split.Length; i++) {
				if (split[i][0] != '#') {
					continue;
				}
				Color c = Color.Empty;
				try {
					c = ColorTranslator.FromHtml(split[i]);
				}
				catch (FormatException e) {
					continue;
				}
				if (c != Color.Empty) {
					colors.Add(new ColorItem(c, "hex", i));
				}
			}
			return colors;
		}

		static List<ColorItem> CheckRgb(string query) {
			// Find any hex codes and process them.
			List<ColorItem> colors = new List<ColorItem>();
			string[] split = query.Split(COMMAE_SPACE_AND_GROUPERS, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int i = 0; i < split.Length; i++) {
				byte r = (byte)i;
				byte g = (byte)(i + 1);
				byte b = (byte)(i + 2);
				bool isPrefixed = split[i].Substring(0, Math.Min(3, split[i].Length)).ToLower() == "rgb";
				if (isPrefixed) {
					r++;
					g++;
					b++;
				}
				if (b >= split.Length) {
					// Not enough splits to contain a color (r g b)
					break;
				}
				bool parsed = 
					Byte.TryParse(split[r], out r)
					&& Byte.TryParse(split[g], out g)
					&& Byte.TryParse(split[b], out b)
				;
				if (parsed) {
					Color col = Color.FromArgb(r, g, b);
					colors.Add(new ColorItem(col, "rgb", i));
					colors.Add(new ColorItem(Color.Empty, "rgb", i + 1));
					colors.Add(new ColorItem(Color.Empty, "rgb", i + 2));
					if (isPrefixed) colors.Add(new ColorItem(Color.Empty, "rgb", i + 3));
					i += isPrefixed ? 3 : 2; // Move i to B index in split[] instead of current R or Prefix index
				}
			}
			return colors;
		}

		static List<ColorItem> CheckColorNames(string query) {
			// Find any color names from databases.
			List<ColorItem> colors = new List<ColorItem>();

			// Someone submits: "Red, Green, Orange Red"
			// We want to look for: Red, Green, Orange Red, Red Green, Green Orange Red, Red Green Orange Red
			string[] split = query.Split(COMMAE, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int c = 1; c <= split.Length; c++) {
				for (int s = 0; s <= split.Length - c; s++) {
					string file;
					string name;

					string subQuery = string.Join(' ', split, s, c);
					int openSquare = subQuery.IndexOf('[');
					int closeSquare = subQuery.IndexOf("]");
					List<ColorItem> readFile = new List<ColorItem>();

					if (openSquare > 0 && closeSquare > openSquare + 1) {
						// Format: x[x]
						file = subQuery.Substring(0, openSquare);
						name = subQuery.Substring(openSquare + 1, closeSquare - openSquare - 1);
						readFile.AddRange(CheckSimpleColorFile(file, name, s, s+c));
					}
					else if (openSquare == -1) {
						// Format: x
						readFile.AddRange(CheckAllSimpleColorFiles(subQuery, s, s+c));
					}
					if (readFile.Count > 0) {
						colors.AddRange(readFile);
						s += c - 1; // Found a color with this phrase. Set s to e so we continue reading after it.
					}
				}
			}

			// Someone submits: Red Green Orange (and somehow nothing was found already)
			// Check: Red, Red Green, Red Green Orange, Green, Green Orange, Orange
			split = query.Split(COMMAE_AND_SPACE, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (int s = 0; s < split.Length; s++) {
				for (int e = split.Length - 1; e >= s; e--) {
					string file;
					string name;
					
					string subQuery = string.Join(' ', split, s, e - s + 1);	
					int openSquare = subQuery.IndexOf('[');
					int closeSquare = subQuery.IndexOf("]");
					List<ColorItem> readFile = new List<ColorItem>();

					if (openSquare > 0 && closeSquare > openSquare + 1) {
						// Format: x[x]
						file = subQuery.Substring(0, openSquare);
						name = subQuery.Substring(openSquare + 1, closeSquare - openSquare - 1);
						readFile.AddRange(CheckSimpleColorFile(file, name, s, e));
					}
					else if (openSquare == -1) {
						// Format: x
						readFile.AddRange(CheckAllSimpleColorFiles(subQuery, s, e));
					}
					if (readFile.Count > 0) {
						colors.AddRange(readFile);
						s = e; // Found a color with this phrase. Set s to e so we continue reading after it.
						break;
					}
				}
			}
			return colors;
		}

		static List<ColorItem> CheckAllSimpleColorFiles(string name, int startIndex = 0, int endIndex = 0) {
			List<ColorItem> list = new List<ColorItem>();
			foreach (var fil in _colorDatabases) {
				list = CheckSimpleColorFile(fil.Key, name, startIndex, endIndex);
				if (list.Count > 0) { break; }
			}
			return list;
		}

		static List<ColorItem> CheckSimpleColorFile(string file, string name, int startIndex = 0, int endIndex = 0) {
			List<ColorItem> list = new List<ColorItem>();
			if (!_colorDatabases.ContainsKey(file)) {
				return list;
			}
			Dictionary<string, string> colors = _colorDatabases[file];

			if (!colors.ContainsKey(name)) {
				return list;
			}
			string source = string.Format("{0}[{1}]", file.ToLower(), name.ToLower());
			list.Add(new ColorItem(ColorTranslator.FromHtml(colors[name]), source, startIndex));
			// Pad the list with empty items to help with ambiguous entries later on
			// Eg "Summer 1969" is a hypothetical color name that takes up two indices,
			// but later on in the color reading the "1969" might get interpreted as an Ole color.
			// By padding with empty, we mark the index of "1969" as used and it can get filtered.
			for (int i = startIndex + 1; i < endIndex; i++) {
				list.Add(new ColorItem(Color.Empty, "", startIndex));
			}
			return list;
		}

		static List<ColorItem> CheckColorSchemes(string query) {
			string file;
			string category;
			string name;
			string subQuery;

			int openSquare = query.IndexOf('[');
			int closeSquare = query.IndexOf("]");
			int colon = query.IndexOf(":");

			if (openSquare > 0 && closeSquare > openSquare + 1) {
				// Formats: x[x] or x[x:x]
				file = query.Substring(0, openSquare);
				subQuery = query.Substring(openSquare + 1, closeSquare - openSquare - 1);
				colon = subQuery.IndexOf(":");
				if (colon == -1) {
					category = subQuery;
					name = "";
				}
				else {
					category = subQuery.Substring(0, colon);
					name = subQuery.Substring(colon + 1);
				}
				return CheckColorSchemeFile(file, category, name);
			}
			else if (openSquare == -1) {
				// Formats: x or x:x
				if (colon == -1) {
					category = query;
					name = "";
				}
				else {
					category = query.Substring(0, colon);
					name = query.Substring(colon + 1);
				}
				return CheckAllColorSchemeFiles(category, name);
			}
			return new List<ColorItem>();
		}

		static List<ColorItem> CheckAllColorSchemeFiles(string category, string name = "") {
			List<ColorItem> list = new List<ColorItem>();
			foreach (var fil in _colorSchemeFiles) {
				list = CheckColorSchemeFile(fil.Key, category, name);
				if (list.Count > 0) { break; }
			}
			return list;
		}

		static List<ColorItem> CheckColorSchemeFile(string file, string category, string name = "") {
			List<ColorItem> list = new List<ColorItem>();

			if (!_colorSchemeFiles.ContainsKey(file)) {
				// Specified file does not exist
				return list;
			}
			ColorSchemeTable table = _colorSchemeFiles[file];

			if (!table.Categories.ContainsKey(category)) {
				// Specified file does not contain color category
				return list;
			}
			ColorSchemeCategory cat = table.Categories[category];

			if (name == "" || !cat.ColorSchemes.ContainsKey(name)) {
				name = cat.Default;
			}
			ColorScheme scheme = cat.ColorSchemes[name];
			string source = string.Format("{0}[{1}:{2}]", file.ToLower(), category.ToLower(), name.ToLower());
			list.Add(new ColorItem(ColorTranslator.FromHtml(scheme.Inner), source, 0));
			list.Add(new ColorItem(ColorTranslator.FromHtml(scheme.Outer), "THEME", 1));
			list.Add(new ColorItem(ColorTranslator.FromHtml(scheme.Text), "THEME", 2));
			return list;
		}

		private static Color DarkenColor(Color color) {
			return Color.FromArgb(color.R / 2, color.G / 2, color.B / 2);
		}

		private static Color LightenColor(Color color) {
			return Color.FromArgb(
				color.R + (255 - color.R) / 2,
				color.G + (255 - color.G) / 2,
				color.B + (255 - color.B) / 2
			);
		}


		// Index, Color, Source

		// Valid single color entries:
		// hex
		// #ffffff					

		// hex with alpha
		// #ffffffaa

		// rgb
		// could be prefixed with rgb
		// could be surrounded with parens, braces, brackets
		// could be comma or semicolon separated, with or without spaces
		// could have alpha
		// or none of that
		// rgb(255,255,255)			
		// rgb 255; 255; 255; 170
		// 255 255 255

		// color names
		// Red
		// Red cola
		// Red kite (bird species) (Benitobi)

		// color names with specific category
		// encycolorpedia:Red
		// encycolorpedia: Red
		// encycolorpedia :Red
		// encycolorpedia : Red

		// windows colors
		// Background

		// Valid colorscheme entries
		// SWAT4
		// SWAT4:Menu
		// SWAT4 :Menu
		// SWAT4: Menu
		// SWAT4 : Menu
	}
}
