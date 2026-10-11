using Xunit;

namespace StreamAssistant2.Tests {
	public class SplitWatcherTests {
		[Theory]
		[InlineData(0, 1, true)]
		[InlineData(5, 6, true)]
		[InlineData(-1, 0, false)]
		[InlineData(6, 5, false)]
		[InlineData(6, -1, false)]
		[InlineData(6, 6, false)]
		[InlineData(5, 7, false)]
		public void IsSplit_OnlyUpByOne_PastTheRunsStart(int last, int now, bool split) {
			Assert.Equal(split, SplitWatcher.IsSplit(last, now));
		}

		[Fact]
		public void IsSplit_WithoutALastIndex_IsNot() {
			Assert.False(SplitWatcher.IsSplit(null, 1));
		}

		[Theory]
		[InlineData("-", true)]
		[InlineData(" -\r\n", true)]
		[InlineData("1:23.45", false)]
		[InlineData("12:03:04", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void IsSkip_OnlyAnEmptyTime(string? previousRunTime, bool skip) {
			Assert.Equal(skip, SplitWatcher.IsSkip(previousRunTime));
		}

		[Theory]
		[InlineData("3", 3)]
		[InlineData(" 3\r\n", 3)]
		[InlineData("-1", -1)]
		[InlineData("", null)]
		[InlineData("-", null)]
		[InlineData("x", null)]
		public void ParseIndex_AWholeNumber_ElseNull(string text, int? expected) {
			Assert.Equal(expected, SplitWatcher.ParseIndex(text));
		}
	}
}
