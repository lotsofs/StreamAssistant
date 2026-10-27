using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace StreamAssistant2.Tests {
	/// <summary>
	/// Loads the real Stream-Resources colour data, where it exists, and checks it loads cleanly.
	/// A skipped or colliding entry would also have been logged, so this is the canary for bad data.
	/// </summary>
	[Collection(ColorDataCollection.Name)]
	public class RealDataSmokeTest {
		[RealDataFact]
		public void RealDataLoadsCompletely() {
			// [RealDataFact] only runs this when both directories are known and exist.
			string colors = ColorData.RealColors!;
			string colorSchemes = ColorData.RealColorSchemes!;
			ColorData.Load(colors, colorSchemes);
			try {
				foreach (string file in Directory.EnumerateFiles(colors, "*.json")) {
					string source = Path.GetFileNameWithoutExtension(file);
					var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file))!;
					Assert.Equal(raw.Count, ColorTableRegistry.Tables[source].Entries.Count);
				}

				foreach (string file in Directory.EnumerateFiles(colorSchemes, "*.json")) {
					string setName = Path.GetFileNameWithoutExtension(file);
					var categories = (JObject)JObject.Parse(File.ReadAllText(file))["Categories"]!;
					foreach (var (categoryName, category) in categories) {
						int raw = (category!["ColorSchemes"] as JObject)?.Count ?? 0;
						Assert.Equal(raw, ColorSchemeRegistry.Sets[setName].Categories[categoryName].ColorSchemes.Count);
					}
				}

				Regex lowercaseHex = new("^#[0-9a-f]{6}$");
				Assert.All(ColorTableRegistry.Tables.Values.SelectMany(t => t.Entries.Values), c => Assert.Matches(lowercaseHex, c.Hex));

				Assert.True(SingleColorParser.TryParse("navy blue", out _));
				Assert.True(SingleColorParser.TryParse("cafe au lait", out _));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}

		[RealDataFact]
		public void RealTwitchTable_IsTheBotsNamedColours() {
			ColorData.Load(ColorData.RealColors!, ColorData.RealColorSchemes!);
			try {
				Dictionary<string, NamedColor> twitch = ColorTableRegistry.Tables["twitch"].Entries;
				Assert.Equal(NameColor.Named.Length, twitch.Count);
				foreach ((string name, string hex) in NameColor.Named) {
					Assert.Equal(hex, twitch[name].Hex);
				}
				Assert.True(SingleColorParser.TryParse("twitch golden_rod", out _));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}

		[RealDataFact]
		public void RealGameScheme_ResolvesBySetAndCategory() {
			ColorData.Load(ColorData.RealColors!, ColorData.RealColorSchemes!);
			try {
				Assert.NotNull(GameColor.SchemeInput("KTANE", s => ColorSchemeRegistry.TryGetScheme(s, out _)));
				Assert.True(SingleColorParser.TryParse("gameschemes KTANE", out _));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}

		[RealDataFact]
		public void RealGameSchemes_Swat4HasASchemeForEverySignTrend() {
			ColorData.Load(ColorData.RealColors!, ColorData.RealColorSchemes!);
			try {
				Assert.True(ColorSchemeRegistry.TryGetSet("gameschemes", out var set));
				Assert.True(ColorSchemeRegistry.TryGetCategoryInSet(set!, "SWAT4", out var swat4));
				Assert.All(ColorSchemeRegistry.SignTrends, trend => Assert.True(swat4!.SignSchemes.ContainsKey(trend), $"SWAT4 has no sign scheme for {trend}"));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}

		[RealDataFact]
		public void RealGameSchemes_NoneHasEverySplitColour() {
			ColorData.Load(ColorData.RealColors!, ColorData.RealColorSchemes!);
			try {
				var resolved = SplitColors.For("KTANE");
				Assert.Equal("none", resolved!.Value.From);
				string[] keys = ["None", "ListGainedAhead", "ListLostAhead", "ListGainedBehind", "ListLostBehind", "ListGold", "LabelPositive", "LabelNegative", "LabelGold"];
				Assert.All(keys, key => Assert.True(resolved.Value.Colors.ContainsKey(key), $"none has no {key}"));
			}
			finally {
				ColorData.LoadFixtures();
			}
		}
	}
}
