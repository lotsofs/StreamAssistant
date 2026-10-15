using System.Drawing;
using Xunit;

namespace StreamAssistant2.Tests {
	public class ColorUtilTests {
		[Theory]
		[InlineData("#ABCDEF", "#abcdef")]
		[InlineData("abcdef", "#abcdef")]
		[InlineData("#AbC123", "#abc123")]
		[InlineData(" #abcdef ", "#abcdef")]
		public void TryNormalizeHex_AcceptsAndLowercases(string input, string expected) {
			Assert.True(ColorUtil.TryNormalizeHex(input, out string hex));
			Assert.Equal(expected, hex);
		}

		[Theory]
		[InlineData("#abcde")]
		[InlineData("#abcdefa")]
		[InlineData("#ghijkl")]
		[InlineData("##abcdef")]
		[InlineData("")]
		[InlineData("#ab cdef")]
		[InlineData("#١٢٣٤٥٦")]
		[InlineData(null)]
		public void TryNormalizeHex_RejectsAnythingButSixHexDigits(string? input) {
			Assert.False(ColorUtil.TryNormalizeHex(input, out _));
		}

		[Theory]
		[InlineData("#000000", "#000000")]
		[InlineData("#FFFFFF", "#7f7f7f")]
		[InlineData("#abcdef", "#556677")]
		[InlineData("123456", "#091a2b")]
		[InlineData("#FF8001", "#7f4000")]
		public void Darken_HalvesEachChannel(string input, string expected) {
			Assert.Equal(expected, ColorUtil.Darken(input));
		}

		[Theory]
		[InlineData("#000000", "#7f7f7f")]
		[InlineData("#FFFFFF", "#ffffff")]
		[InlineData("#abcdef", "#d5e6f7")]
		[InlineData("123456", "#8899aa")]
		[InlineData("#ff8001", "#ffbf80")]
		public void Lighten_MovesEachChannelHalfwayToWhite(string input, string expected) {
			Assert.Equal(expected, ColorUtil.Lighten(input));
		}

		[Fact]
		public void ColorOverloads_MatchTheStringOnes() {
			Assert.Equal(Color.FromArgb(127, 127, 127), ColorUtil.Darken(Color.White));
			Assert.Equal(Color.FromArgb(127, 127, 127), ColorUtil.Lighten(Color.Black));
			Assert.Equal(Color.FromArgb(213, 230, 247), ColorUtil.Lighten(Color.FromArgb(171, 205, 239)));
		}

		[Fact]
		public void ToHex_IsLowercaseAndClamps() {
			Assert.Equal("#abcdef", ColorUtil.ToHex(Color.FromArgb(171, 205, 239)));
			Assert.Equal("#00ff80", ColorUtil.ToHex(-5, 300, 128));
		}

		[Theory]
		[InlineData("#000000", 4278190080L)]
		[InlineData("#FFFFFF", 4294967295L)]
		[InlineData("#abcdef", 4293905835L)]
		[InlineData("123456", 4283839506L)]
		[InlineData("#ff8001", 4278288639L)]
		public void ToOBS_IsOpaqueAbgr(string input, long expected) {
			Assert.Equal(expected, ColorUtil.ToOBS(input));
		}

		[Theory]
		[InlineData(0, 1, 1, 255, 0, 0)]
		[InlineData(120, 0.5, 0.5, 64, 128, 64)]
		[InlineData(359.9, 1, 1, 255, 0, 0)]
		[InlineData(-60, 1, 1, 255, 0, 255)]      // hue wraps
		[InlineData(720, 2, -1, 0, 0, 0)]         // saturation and value clamp
		[InlineData(300, 0.25, 0.75, 191, 143, 191)]
		public void HsvToRgb(double h, double s, double v, int r, int g, int b) {
			Assert.Equal((r, g, b), ColorUtil.HsvToRgb(h, s, v));
		}
	}
}
