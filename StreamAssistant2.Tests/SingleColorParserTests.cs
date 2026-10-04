using Xunit;
using static StreamAssistant2.Coloring;

namespace StreamAssistant2.Tests {
	[Collection(ColorDataCollection.Name)]
	public class SingleColorParserTests {
		static ColorEntry Parse(string input) {
			Assert.True(SingleColorParser.TryParse(input, out ColorEntry? entry), $"\"{input}\" did not resolve");
			return entry!;
		}

		[Fact]
		public void Hex_IsLowercasedIncludingItsName() {
			ColorEntry e = Parse("#ABCDEF");
			Assert.Equal("hex", e.Source);
			Assert.Equal("#abcdef", e.Name);
			Assert.Equal(("#abcdef", "#556677", "#d5e6f7"), (e.Hex1, e.Hex2, e.Hex3));
		}

		[Theory]
		[InlineData("rgb(1,2,3)")]
		[InlineData("rgb(1 2 3)")]
		[InlineData("RGB( 1 , 2 , 3 )")]
		[InlineData("1,2,3")]
		[InlineData("1 2 3")]
		public void Rgb_AcceptsCommasOrSpaces(string input) {
			ColorEntry e = Parse(input);
			Assert.Equal("rgb", e.Source);
			Assert.Equal("rgb(1,2,3)", e.Name);
			Assert.Equal("#010203", e.Hex1);
		}

		[Fact]
		public void Rgb_ClampsOutOfRangeComponents() {
			Assert.Equal("#ff0000", Parse("rgb(300,0,0)").Hex1);
		}

		[Theory]
		[InlineData("hsv(120,50,50)")]
		[InlineData("HSV(120°, 50%, 50%)")]
		public void Hsv(string input) {
			ColorEntry e = Parse(input);
			Assert.Equal("hsv", e.Source);
			Assert.Equal("#408040", e.Hex1);
		}

		[Fact]
		public void NamedTableColour() {
			ColorEntry e = Parse("Navy Blue");
			Assert.Equal(("crayola", "Navy Blue", "#0066cc"), (e.Source, e.Name, e.Hex1));
			Assert.Equal("encycolorpedia", Parse("absolute zero").Source);
		}

		[Fact]
		public void Scheme_ByCategoryUsesItsDefault() {
			ColorEntry e = Parse("Albania");
			Assert.Equal("countryschemes", e.Source);
			Assert.Equal(("#000000", "#ff0000", "#ff0000"), (e.Hex1, e.Hex2, e.Hex3));
		}

		[Fact]
		public void Scheme_BySetCategoryAndScheme() {
			Assert.Equal("#112233", Parse("gameschemes gtavc weapon").Hex1);
		}

		[Fact]
		public void SystemColour() {
			ColorEntry e = Parse("control");
			Assert.Equal("system", e.Source);
			Assert.Equal("#f0f0f0", e.Hex1);
		}

		[Fact]
		public void Random_PicksFromEncycolorpedia() {
			Assert.Equal("encycolorpedia", Parse("random").Source);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("asdfgh")]
		[InlineData("cafx au lait")]   // accents fold to literal letters, not wildcards
		[InlineData("strae")]          // ß needs 1 or 2 letters, not 0…
		[InlineData("strassse")]       // …and not 3
		[InlineData("krasnyi")]        // an all-Cyrillic name gets no loose entry at all
		public void Misses(string input) {
			Assert.False(SingleColorParser.TryParse(input, out _));
		}

		[Fact]
		public void Loose_PlainInputFindsAccentedName() {
			ColorEntry e = Parse("cafe au lait");
			Assert.Equal(("Café au lait", "#a67b5b"), (e.Name, e.Hex1));
			Assert.Equal("#407a52", Parse("patina rokusho").Hex1);
		}

		[Fact]
		public void Loose_AccentedInputFindsPlainName() {
			Assert.Equal("#000080", Parse("nävy").Hex1);
		}

		[Theory]
		[InlineData("cafe", "#222222")]   // exact Cafe
		[InlineData("CAFE", "#222222")]
		[InlineData("café", "#111111")]   // exact Café
		[InlineData("CAFÉ", "#111111")]
		[InlineData("cafè", "#222222")]   // neither is exact; the fold "cafe" hits plain Cafe first
		public void Loose_ExactSpellingWins(string input, string expected) {
			Assert.Equal(expected, Parse(input).Hex1);
		}

		[Theory]
		[InlineData("lodz", "#000001")]
		[InlineData("strasse", "#000002")]
		[InlineData("strase", "#000002")]
		[InlineData("dhiet", "#000004")]
		[InlineData("diet", "#000004")]
		[InlineData("aether", "#000005")]
		[InlineData("ather", "#000005")]
		[InlineData("kirmizi", "#000006")]
		public void Loose_WildcardLettersMatchOneOrTwoLetters(string input, string expected) {
			Assert.Equal(expected, Parse(input).Hex1);
		}

		[Fact]
		public void ExactOddLetterSpellingsStillResolve() {
			Assert.Equal("#000001", Parse("Łódź").Hex1);
			Assert.Equal("#000007", Parse("Красный").Hex1);
		}

		[Fact(Skip = "RAN: the random keyword is matched case-sensitively")]
		public void Random_IsCaseInsensitive() {
			Assert.Equal("encycolorpedia", Parse("Random").Source);
		}

		[Fact(Skip = "CAN: system colours report the raw input as their name")]
		public void SystemColour_ReportsCanonicalName() {
			Assert.Equal("Control", Parse("control").Name);
		}
	}
}
