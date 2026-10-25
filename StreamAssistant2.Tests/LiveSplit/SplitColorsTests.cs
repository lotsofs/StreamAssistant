using Newtonsoft.Json.Linq;
using Xunit;

namespace StreamAssistant2.Tests {
	public class SplitColorsTests {
		const string N = "None", P = "LabelPositive", M = "LabelNegative", G = "LabelGold";

		[Theory]
		[InlineData("NotApplicable", N, N, N)]
		[InlineData("Ahead", P, N, N)]
		[InlineData("Behind", M, N, N)]
		[InlineData("GainedAhead", P, P, N)]
		[InlineData("GainedBehind", M, P, N)]
		[InlineData("LostAhead", P, M, N)]
		[InlineData("LostBehind", M, M, N)]
		[InlineData("GoldAhead", P, G, G)]
		[InlineData("GoldBehind", M, G, G)]
		[InlineData("FinishedNotApplicable", N, N, N)]
		[InlineData("FinishedAhead", P, N, N)]
		[InlineData("FinishedBehind", M, N, N)]
		[InlineData("FinishedGainedAhead", P, P, N)]
		[InlineData("FinishedGainedBehind", M, P, N)]
		[InlineData("FinishedLostAhead", P, M, N)]
		[InlineData("FinishedLostBehind", M, M, N)]
		[InlineData("FinishedGoldAhead", P, G, G)]
		[InlineData("FinishedGoldBehind", M, G, G)]
		[InlineData("goldahead\r\n", P, G, G)]
		[InlineData("  Behind ", M, N, N)]
		[InlineData(" finishedlostahead\r\n", P, M, N)]
		[InlineData("Finished", N, N, N)]
		[InlineData("PBGold", N, N, N)]
		[InlineData("Sideways", N, N, N)]
		[InlineData("", N, N, N)]
		[InlineData(null, N, N, N)]
		public void SignKeys_RunSegmentAndGold(string? sign, string run, string segment, string gold) {
			Assert.Equal((run, segment, gold), SplitColors.SignKeys(sign));
		}

		[Theory]
		[InlineData("GoldAhead", "Gold")]
		[InlineData("GoldBehind", "Gold")]
		[InlineData("FinishedGoldAhead", "Gold")]
		[InlineData("FinishedGoldBehind", "Gold")]
		[InlineData("Ahead", "Ahead")]
		[InlineData("GainedAhead", "Ahead")]
		[InlineData("LostAhead", "Ahead")]
		[InlineData("FinishedAhead", "Ahead")]
		[InlineData("FinishedGainedAhead", "Ahead")]
		[InlineData("FinishedLostAhead", "Ahead")]
		[InlineData("Behind", "Behind")]
		[InlineData("GainedBehind", "Behind")]
		[InlineData("LostBehind", "Behind")]
		[InlineData("FinishedBehind", "Behind")]
		[InlineData("FinishedGainedBehind", "Behind")]
		[InlineData("FinishedLostBehind", "Behind")]
		[InlineData(" lostbehind\r\n", "Behind")]
		[InlineData("NotApplicable", null)]
		[InlineData("FinishedNotApplicable", null)]
		[InlineData("PB", null)]
		[InlineData("", null)]
		[InlineData(null, null)]
		public void Trend_GoldFirst_ThenTheRunsSide(string? sign, string? trend) {
			Assert.Equal(trend, SplitColors.Trend(sign));
		}

		static readonly ColorSchemeRegistry.Category Swat4 = JObject.Parse("""
			{
				"Default": "Menu",
				"SignSchemes": { "Ahead": "BlueTeam", "Behind": "RedTeam", "Gold": "Element" },
				"ColorSchemes": { "Menu": {}, "BlueTeam": {}, "RedTeam": {}, "Element": {} }
			}
			""").ToObject<ColorSchemeRegistry.Category>()!;

		[Theory]
		[InlineData("Ahead", "BlueTeam")]
		[InlineData("LostBehind", "RedTeam")]
		[InlineData("GoldAhead", "Element")]
		[InlineData("FinishedGoldBehind", "Element")]
		[InlineData("FinishedLostAhead", "BlueTeam")]
		[InlineData("NotApplicable", "Menu")]
		[InlineData("FinishedNotApplicable", "Menu")]
		[InlineData(null, "Menu")]
		public void ChooseScheme_TheTrendsScheme_ElseTheDefault(string? sign, string scheme) {
			Assert.Equal(scheme, GameColor.ChooseScheme(Swat4, sign));
		}

		[Fact]
		public void ChooseScheme_ACategoryWithoutSignSchemes_IsNull() {
			Assert.Null(GameColor.ChooseScheme(JObject.Parse("""{ "Default": "Menu" }""").ToObject<ColorSchemeRegistry.Category>(), "Ahead"));
			Assert.Null(GameColor.ChooseScheme(null, "Ahead"));
		}

		static ColorSchemeRegistry.ThemeSet Set(string categories) {
			return JObject.Parse($$"""{ "Categories": { {{categories}} } }""").ToObject<ColorSchemeRegistry.ThemeSet>()!;
		}

		const string NoneColors = """ "none": { "SplitColors": { "None": "#ffffff", "ListGold": "#eeeeee", "LabelGold": "#dddddd" } } """;

		[Fact]
		public void Resolve_TheGamesColours_OverNones() {
			var resolved = SplitColors.Resolve(Set($$"""{{NoneColors}}, "SWAT4": { "SplitColors": { "ListGold": "#ffc800" } }"""), "swat4");
			Assert.Equal("swat4", resolved!.Value.From);
			Assert.Equal("#ffc800", resolved.Value.Colors["ListGold"]);
			Assert.Equal("#ffffff", resolved.Value.Colors["None"]);
		}

		[Theory]
		[InlineData("KTANE")]
		[InlineData("Unknown")]
		public void Resolve_AGameWithoutSplitColours_GetsNones(string game) {
			var resolved = SplitColors.Resolve(Set($$"""{{NoneColors}}, "KTANE": { "ColorSchemes": {} }"""), game);
			Assert.Equal("none", resolved!.Value.From);
			Assert.Equal(3, resolved.Value.Colors.Count);
		}

		[Fact]
		public void Resolve_NothingAnywhere_IsNull() {
			Assert.Null(SplitColors.Resolve(Set(""" "KTANE": { "ColorSchemes": {} } """), "KTANE"));
			Assert.Null(SplitColors.Resolve(null, "KTANE"));
		}

		static readonly Dictionary<string, string> Colors = new() {
			["None"] = "#ffffff", ["LabelPositive"] = "#0000ff", ["LabelGold"] = "#ffc800",
			["ListGold"] = "#ffc800", ["ListGainedAhead"] = "#000086", ["ListLostBehind"] = "#af0000",
		};

		[Fact]
		public void HighlightFilters_EachHighlightWithAColour_OnColorCorrection2() {
			Assert.Equal([
				new("Livesplit: SL Highlights Gold", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#ffc800")),
				new("Livesplit: SL Highlights AheadGained", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#000086")),
				new SplitColors.FilterColor("Livesplit: SL Highlights BehindLost", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#af0000")),
			], SplitColors.HighlightFilters(Colors));
		}

		[Fact]
		public void PreviousFilters_ColourRunSegmentAndGoldBySign_OnColorCorrection() {
			Assert.Equal([
				new("Livesplit: Previous Run", "Color Correction", "color_multiply", ColorUtil.ToOBS("#0000ff")),
				new("Livesplit: Previous Segment", "Color Correction", "color_multiply", ColorUtil.ToOBS("#ffc800")),
				new SplitColors.FilterColor("Livesplit: Previous Gold", "Color Correction", "color_multiply", ColorUtil.ToOBS("#ffc800")),
			], SplitColors.PreviousFilters(Colors, "GoldAhead"));
		}

		[Fact]
		public void PreviousFilters_AKeyWithoutAColour_IsLeftOut() {
			Assert.Equal(["Livesplit: Previous Segment", "Livesplit: Previous Gold"], SplitColors.PreviousFilters(Colors, "Behind").Select(f => f.Source));
		}
	}
}
