using Xunit;

namespace StreamAssistant2.Tests {
	public class ColorNameComparerTests {
		static readonly ColorNameComparer Comparer = ColorNameComparer.Instance;

		[Theory]
		[InlineData("Green-Blue", "green blue")]
		[InlineData("GREENBLUE", "green blue")]
		[InlineData("Big dip o’ruby", "big dip oruby")]
		[InlineData("", " - ")]
		public void Equal_IgnoringCaseSpacingAndPunctuation(string x, string y) {
			Assert.True(Comparer.Equals(x, y));
			Assert.Equal(Comparer.GetHashCode(x), Comparer.GetHashCode(y));
		}

		[Theory]
		[InlineData("café", "cafe")]      // accents are significant here; the loose fallback folds them
		[InlineData("red1", "red")]
		[InlineData("red", "blue")]
		public void NotEqual(string x, string y) {
			Assert.False(Comparer.Equals(x, y));
		}

		[Fact]
		public void Nulls() {
			Assert.True(Comparer.Equals(null, null));
			Assert.False(Comparer.Equals(null, ""));
			Assert.False(Comparer.Equals("", null));
		}

		[Theory]
		[InlineData("Café au lait", "cafeaulait")]
		[InlineData("NAVY-Blue", "navyblue")]
		[InlineData("Patina (Rokushō)", "patinarokusho")]
		[InlineData("Big dip o’ruby", "bigdiporuby")]
		[InlineData("Straße", "straße")]      // ß doesn't decompose, so it stays (and becomes a wildcard)
		[InlineData("Ðiết", "ðiet")]          // ế folds to e, Ð stays
		[InlineData("한", "한")]               // Hangul decomposes into several jamo, so it is kept whole
		public void Fold(string name, string expected) {
			Assert.Equal(expected, ColorNameComparer.Fold(name));
		}
	}
}
