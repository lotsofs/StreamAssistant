using Newtonsoft.Json.Linq;
using Xunit;

namespace StreamAssistant2.Tests {
	public class SplitColorsTests {
		const string N = "None", P = "LabelPositive", M = "LabelNegative", G = "LabelGold";

		[Theory]
		[InlineData("Undetermined", N, N, N)]
		[InlineData("NotApplicable", N, N, N)]
		[InlineData("Ahead", P, N, N)]
		[InlineData("Behind", M, N, N)]
		[InlineData("Gained", N, P, N)]
		[InlineData("Lost", N, M, N)]
		[InlineData("Gold", N, G, G)]
		[InlineData("GainedAhead", P, P, N)]
		[InlineData("GainedBehind", M, P, N)]
		[InlineData("LostAhead", P, M, N)]
		[InlineData("LostBehind", M, M, N)]
		[InlineData("GoldAhead", P, G, G)]
		[InlineData("GoldBehind", M, G, G)]
		[InlineData("PB", P, N, N)]
		[InlineData("NoPB", M, N, N)]
		[InlineData("PBGained", P, P, N)]
		[InlineData("PBLost", P, N, N)]
		[InlineData("PBGold", P, G, G)]
		[InlineData("NoPBGained", M, P, N)]
		[InlineData("NoPBLost", M, M, N)]
		[InlineData("NoPBGold", M, G, G)]
		[InlineData("goldahead\r\n", P, G, G)]
		[InlineData("  Behind ", M, N, N)]
		[InlineData("Sideways", N, N, N)]
		[InlineData("", N, N, N)]
		[InlineData(null, N, N, N)]
		public void SignKeys_RunSegmentAndGold(string? sign, string run, string segment, string gold) {
			Assert.Equal((run, segment, gold), SplitColors.SignKeys(sign));
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
				new("Livesplit: Splits List Highlights Gold", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#ffc800")),
				new("Livesplit: Splits List Highlights AheadGained", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#000086")),
				new SplitColors.FilterColor("Livesplit: Splits List Highlights BehindLost", "Color Correction 2", "color_multiply", ColorUtil.ToOBS("#af0000")),
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
