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
	}
}
