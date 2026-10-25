using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace StreamAssistant2.Tests {
	[Collection(ColorDataCollection.Name)]
	public class RegistryTests {
		static readonly Regex LowercaseHex = new("^#[0-9a-f]{6}$");

		[Fact]
		public void Tables_LoadEveryFixtureEntry() {
			Assert.Equal(4, ColorTableRegistry.Tables["basiccolors"].Entries.Count);
			Assert.Equal(2, ColorTableRegistry.Tables["crayola"].Entries.Count);
			Assert.Equal(3, ColorTableRegistry.Tables["encycolorpedia"].Entries.Count);
			Assert.Equal(9, ColorTableRegistry.Tables["loosetest"].Entries.Count);
		}

		[Fact]
		public void Tables_StoreHexLowercase() {
			Assert.Equal("#ff0000", ColorTableRegistry.Tables["basiccolors"].Entries["Red"].Hex);
			Assert.All(ColorTableRegistry.Tables.Values.SelectMany(t => t.Entries.Values), c => Assert.Matches(LowercaseHex, c.Hex));
		}

		[Fact]
		public void TablePrefixForm() {
			Assert.True(ColorTableRegistry.TryGetTableColor("crayola navy blue", false, out NamedColor? color));
			Assert.Equal("crayola", color!.Source);
		}

		[Fact]
		public void TablePrefixForm_ColourNameMayMatchLoosely() {
			Assert.False(ColorTableRegistry.TryGetTableColor("encycolorpedia cafe au lait", false, out _));
			Assert.True(ColorTableRegistry.TryGetTableColor("encycolorpedia cafe au lait", true, out NamedColor? color));
			Assert.Equal("Café au lait", color!.OriginalName);
		}

		[Fact]
		public void TableNamesAreAlwaysStrict() {
			Assert.False(ColorTableRegistry.TryGetTableColor("crayolá navy blue", true, out _));
		}

		[Fact]
		public void Schemes_SetAndCategoryFallsBackToDefault() {
			Assert.True(ColorSchemeRegistry.TryGetScheme("countryschemes albania", out var data));
			Assert.Equal(("countryschemes", "default"), (data!.SetName, data.SchemeName));
			Assert.Equal("#ff0000", data.Scheme.Outer);   // #FF0000 in the file, lowercased at load
		}

		[Fact]
		public void Schemes_CategoryAndScheme() {
			Assert.True(ColorSchemeRegistry.TryGetScheme("gtavc weapon", out var data));
			Assert.Equal("#112233", data!.Scheme.Inner);
		}

		[Fact]
		public void Schemes_CategoryAloneUsesItsDefault() {
			Assert.True(ColorSchemeRegistry.TryGetScheme("gtavc", out var data));
			Assert.Equal("#aabbcc", data!.Scheme.Inner);   // Hud; #AABBCC in the file
		}

		[Fact]
		public void Schemes_DuplicateNamesKeepTheFirstEntry() {
			JObject root = JObject.Parse("""
				{
					"Categories": {
						"Green-Blue": { "ColorSchemes": { "A": { "Inner": "#111111", "Outer": "#111111", "Text": "#111111" } } },
						"Green Blue": { "ColorSchemes": { "B": { "Inner": "#222222", "Outer": "#222222", "Text": "#222222" } } },
						"Other": {
							"ColorSchemes": {
								"Navy-Blue": { "Inner": "#333333", "Outer": "#333333", "Text": "#333333" },
								"navy blue": { "Inner": "#444444", "Outer": "#444444", "Text": "#444444" }
							}
						}
					}
				}
				""");

			var dropped = ColorSchemeRegistry.DropDuplicateNames(root);
			Assert.Equal([("", "Green Blue", "Green-Blue"), (" Other", "navy blue", "Navy-Blue")], dropped);

			// The bug this guards against: the first spelling kept, but with the last entry's colours.
			var set = root.ToObject<ColorSchemeRegistry.ThemeSet>()!;
			Assert.Equal("#111111", set.Categories["green blue"].ColorSchemes["A"].Inner);
			Assert.False(set.Categories["green blue"].ColorSchemes.ContainsKey("B"));
			Assert.Equal("#333333", set.Categories["Other"].ColorSchemes["navy blue"].Inner);
		}

		[Fact]
		public void Schemes_SplitColoursAreLowercased_AndABadOneDroppedAndLogged() {
			var set = JObject.Parse("""
				{ "Categories": { "SWAT4": { "SplitColors": { "None": "FFFBFF", "ListGold": "#FFC800", "LabelGold": "gold" } } } }
				""").ToObject<ColorSchemeRegistry.ThemeSet>()!;
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			try {
				ColorSchemeRegistry.NormalizeHex("gameschemes", set);
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			var colors = set.Categories["swat4"].SplitColors;
			Assert.Equal("#fffbff", colors["None"]);
			Assert.Equal("#ffc800", colors["listgold"]);
			Assert.False(colors.ContainsKey("LabelGold"));
			lock (log) {
				Assert.Contains(log, l => l.Contains("Colour scheme gameschemes SWAT4: skipped split colour \"LabelGold\", \"gold\" is not a hex colour"));
			}
		}

		[Fact]
		public void Schemes_SignSchemesWithAnUnknownTrendOrScheme_AreDroppedAndLogged() {
			var set = JObject.Parse("""
				{ "Categories": { "SWAT4": {
					"ColorSchemes": { "BlueTeam": { "Inner": "#000086", "Outer": "#000062", "Text": "#fffbff" } },
					"SignSchemes": { "Ahead": "BlueTeam", "Behind": "Nope", "Sideways": "BlueTeam" }
				} } }
				""").ToObject<ColorSchemeRegistry.ThemeSet>()!;
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			try {
				ColorSchemeRegistry.CheckSignSchemes("gameschemes", set);
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			Assert.Equal(["Ahead"], set.Categories["SWAT4"].SignSchemes.Keys.ToList());
			lock (log) {
				Assert.Contains(log, l => l.Contains("Colour scheme gameschemes SWAT4: skipped sign scheme \"Behind\", no scheme \"Nope\""));
				Assert.Contains(log, l => l.Contains("Colour scheme gameschemes SWAT4: skipped sign scheme \"Sideways\", not one of Ahead, Behind, Gold"));
			}
		}

		[Fact]
		public void Schemes_NoDuplicatesDropsNothing() {
			JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ColorData.Fixture("ColorSchemes"), "gameschemes.json")));
			Assert.Empty(ColorSchemeRegistry.DropDuplicateNames(root));
		}

		[Fact]
		public void Schemes_EmptyInputIsNotAnError() {
			Assert.False(ColorSchemeRegistry.TryGetCategorySchemeInAnySet([], out _));
		}

		[Fact]
		public void GetRandomColor_PicksFromEveryTable() {
			// Each pick is a real entry of its table, and over enough picks every table shows up
			// (18 fixture colours, 500 picks: missing crayola's 2 has odds around 1e-26).
			HashSet<string> sources = [];
			for (int i = 0; i < 500; i++) {
				NamedColor color = ColorTableRegistry.GetRandomColor();
				Assert.Same(color, ColorTableRegistry.Tables[color.Source].Entries[color.OriginalName]);
				sources.Add(color.Source);
			}
			Assert.Equal(ColorTableRegistry.Tables.Keys.ToHashSet(), sources);
		}

		[Fact]
		public void GetRandomColor_IsARandomHexWithNoTablesAtAll() {
			DirectoryInfo empty = Directory.CreateTempSubdirectory("colors-empty-");
			ColorData.Load(empty.FullName, ColorData.Fixture("ColorSchemes"));
			try {
				NamedColor color = ColorTableRegistry.GetRandomColor();
				Assert.Equal("random rgb", color.Source);
				Assert.Matches(LowercaseHex, color.Hex);
				Assert.Equal("random rgb", Coloring.GetRandomColor().Source);
			}
			finally {
				ColorData.LoadFixtures();
				empty.Delete();
			}
		}

		[Fact]
		public void Reload_ABrokenFileKeepsWhatItHeld_AndTheOthersReload() {
			DirectoryInfo dir = CopyFixtures();
			string colors = Path.Combine(dir.FullName, "Colors");
			string schemes = Path.Combine(dir.FullName, "ColorSchemes");
			try {
				ColorData.Load(colors, schemes);
				File.WriteAllText(Path.Combine(colors, "crayola.json"), """{ "Navy Blue": """);
				File.WriteAllText(Path.Combine(schemes, "gameschemes.json"), """{ "Categories": { , """);
				File.WriteAllText(Path.Combine(colors, "basiccolors.json"), """{ "Red": "#FF0000", "Teal": "#008080" }""");
				File.WriteAllText(Path.Combine(schemes, "countryschemes.json"), """{ "Categories": { "Chad": { "ColorSchemes": { "TD": { "Inner": "#002664", "Outer": "#FECB00", "Text": "#C60C30" } }, "Default": "TD" } } }""");
				File.Delete(Path.Combine(colors, "loosetest.json"));

				List<string> log = Logged(Coloring.Load);

				Assert.Equal(2, ColorTableRegistry.Tables["crayola"].Entries.Count);
				Assert.True(ColorSchemeRegistry.TryGetScheme("gameschemes gtavc weapon", out var weapon));
				Assert.Equal("#112233", weapon!.Scheme.Inner);
				Assert.Equal(["Red", "Teal"], ColorTableRegistry.Tables["basiccolors"].Entries.Keys.Order().ToList());
				Assert.True(ColorSchemeRegistry.TryGetScheme("chad", out _));
				Assert.False(ColorSchemeRegistry.TryGetScheme("albania", out _));
				Assert.False(ColorTableRegistry.Tables.ContainsKey("loosetest"));
				Assert.Contains(log, l => l.Contains($"Error CLR1: can't load {Path.Combine(colors, "crayola.json")}: "));
				Assert.Contains(log, l => l.Contains($"Error CLR1: can't load {Path.Combine(schemes, "gameschemes.json")}: "));
				Assert.Contains(log, l => l.Contains("Loaded 3 colour tables and 2 scheme sets"));
				// The random pick follows the reload too.
				for (int i = 0; i < 200; i++) {
					Assert.NotEqual("loosetest", ColorTableRegistry.GetRandomColor().Source);
				}
			}
			finally {
				ColorData.LoadFixtures();
				dir.Delete(true);
			}
		}

		[Fact]
		public void Load_ABrokenNewFileIsLeftOut_AndTheRestLoad() {
			DirectoryInfo dir = CopyFixtures();
			string colors = Path.Combine(dir.FullName, "Colors");
			try {
				File.WriteAllText(Path.Combine(colors, "broken.json"), "{ nope");
				List<string> log = Logged(() => ColorData.Load(colors, Path.Combine(dir.FullName, "ColorSchemes")));
				Assert.False(ColorTableRegistry.Tables.ContainsKey("broken"));
				Assert.Equal(4, ColorTableRegistry.Tables["basiccolors"].Entries.Count);
				Assert.Contains(log, l => l.Contains($"Error CLR1: can't load {Path.Combine(colors, "broken.json")}: "));
			}
			finally {
				ColorData.LoadFixtures();
				dir.Delete(true);
			}
		}

		[Fact]
		public void Load_AFolderThatCantBeListedKeepsEverything() {
			string missing = Path.Combine(Path.GetTempPath(), "colors-missing-" + Guid.NewGuid().ToString("N"));
			try {
				List<string> log = Logged(() => ColorData.Load(missing, missing));
				Assert.Equal(2, ColorTableRegistry.Tables["crayola"].Entries.Count);
				Assert.True(ColorSchemeRegistry.TryGetScheme("gtavc", out _));
				Assert.Equal(2, log.Count(l => l.Contains($"Error CLR1: can't load {missing}: ")));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}

		// The fixture folders, copied under a new temporary folder.
		static DirectoryInfo CopyFixtures() {
			DirectoryInfo dir = Directory.CreateTempSubdirectory("colors-reload-");
			foreach (string folder in new[] { "Colors", "ColorSchemes" }) {
				string to = Directory.CreateDirectory(Path.Combine(dir.FullName, folder)).FullName;
				foreach (string file in Directory.GetFiles(ColorData.Fixture(folder))) {
					File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
				}
			}
			return dir;
		}

		// The lines logged while the action runs.
		static List<string> Logged(Action action) {
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			try {
				action();
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			lock (log) return log.ToList();
		}
	}
}
