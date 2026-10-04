using Xunit;
using static StreamAssistant2.Coloring;

namespace StreamAssistant2.Tests {
	/// <summary>
	/// The separator-weight rule documented in CLAUDE.md, against the fixture tables.
	/// </summary>
	[Collection(ColorDataCollection.Name)]
	public class TripleColorParserTests {
		static ColorEntry Parse(string input) {
			Assert.True(TripleColorParser.TryParse(input, out ColorEntry? entry), $"\"{input}\" did not resolve");
			return entry!;
		}

		[Theory]
		[InlineData("navy blue", "Navy Blue")]                    // spaces bind tightest: one crayola name
		[InlineData("navy,blue", "Navy|Blue")]                    // a comma splits it
		[InlineData("crayola,navy,blue", "Navy Blue")]            // nothing tighter matches, so the scan crosses both commas
		[InlineData("red green blue", "Red|Green-Blue")]          // all-space input is deliberately ambiguous; longest wins
		[InlineData("red, green, blue", "Red|Green|Blue")]
		[InlineData("navy blue, red", "Navy Blue|Red")]
		[InlineData("red, green, blue, navy", "Red|Green|Blue")]  // at most three
		public void SeparatorWeightRule(string input, string expectedNames) {
			Assert.Equal(expectedNames, Parse(input).Name);
		}

		[Fact]
		public void ThreeColoursFillTheThreeSlots() {
			ColorEntry e = Parse("red, green, blue");
			Assert.Equal(("#ff0000", "#008000", "#0000ff"), (e.Hex1, e.Hex2, e.Hex3));
		}

		[Fact]
		public void OnePlainColour_PadsWithItsDarkenLightenPair() {
			ColorEntry e = Parse("red");
			Assert.Equal(("#ff0000", "#7f0000", "#ff7f7f"), (e.Hex1, e.Hex2, e.Hex3));
		}

		[Fact]
		public void OneScheme_KeepsItsOwnOuterAndText() {
			ColorEntry e = Parse("Albania");
			Assert.Equal(("#000000", "#ff0000", "#ff0000"), (e.Hex1, e.Hex2, e.Hex3));
		}

		[Fact]
		public void InputIsCappedAt32Tokens() {
			// Tokens 31 and 32 are "navy blue"; "red" and "green" fall past the cap.
			string input = string.Join(" ", Enumerable.Range(0, 30).Select(i => "junk" + i)) + " navy blue red green";
			Assert.Equal("Navy Blue", Parse(input).Name);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("asdf qwer")]
		public void Misses(string input) {
			Assert.False(TripleColorParser.TryParse(input, out _));
		}
	}
}
