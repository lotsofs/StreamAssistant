using Xunit;

namespace StreamAssistant2.Tests {
	// MoneyGoal's amounts, OBS text and !moneygoal parsing. Credit, TrySetGoal and Reset reach the database
	// and OBS, so they're never called here.
	public class MoneyGoalTests {
		[Theory]
		[InlineData("4.2", 420)]
		[InlineData("5", 500)]
		[InlineData("0.005", 1)]
		[InlineData("0.004", 0)]
		[InlineData("12.345", 1235)]
		public void ToCents_RoundsHalfAwayFromZero(string amount, long expected) {
			Assert.Equal(expected, MoneyGoal.ToCents(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
		}

		[Theory]
		[InlineData(0, 99900, "0 / 999 EUR")]
		[InlineData(1654, 50000, "16.54 / 500 EUR")]
		[InlineData(420, 12550, "4.20 / 125.50 EUR")]
		public void Text_IsCurrentSlashGoal(long current, long goal, string expected) {
			Assert.Equal(expected, MoneyGoal.Text(current, goal));
		}

		[Fact]
		public void DefaultGoal_Is999() {
			Assert.Equal("0 / 999 EUR", MoneyGoal.Text(0, MoneyGoal.DEFAULT_GOAL_CENTS));
		}

		[Theory]
		[InlineData("500", 50000L)]
		[InlineData(" 500 ", 50000L)]
		[InlineData("12.50", 1250L)]
		[InlineData("12,5", 1250L)]
		[InlineData("0.01", 1L)]
		[InlineData("0", null)]
		[InlineData("0.001", null)]
		[InlineData("-5", null)]
		[InlineData("abc", null)]
		[InlineData("500 EUR", null)]
		[InlineData("1,000.50", null)]
		[InlineData("99999999999999999999", null)]
		[InlineData("", null)]
		public void ParseGoal_PositiveAmountsOnly(string text, long? expected) {
			Assert.Equal(expected, MoneyGoal.ParseGoal(text));
		}
	}
}
