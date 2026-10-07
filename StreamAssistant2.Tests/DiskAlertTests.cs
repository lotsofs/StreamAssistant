using Xunit;

namespace StreamAssistant2.Tests {
	public class DiskAlertTests {
		const double GIB = 1073741824;

		static DateTime At(int hour, int minute, double second = 0) =>
			new DateTime(2026, 10, 7, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

		[Theory]
		[InlineData(100, "Ok")]
		[InlineData(99.9, "Notify")]
		[InlineData(15, "Notify")]
		[InlineData(14.9, "Warn")]
		[InlineData(2, "Warn")]
		[InlineData(1.9, "Spam")]
		[InlineData(0, "Spam")]
		public void Classify_UsesTheThresholds(double freeGib, string expected) {
			Assert.Equal(expected, DiskSpace.Classify(freeGib * GIB).ToString());
		}

		[Fact]
		public void NothingAlertsWhileOk() {
			DiskSpace.AlertState state = new();
			Assert.Null(state.Update(500 * GIB, At(12, 0)));
			Assert.Null(state.Update(500 * GIB, At(13, 0)));
		}

		[Fact]
		public void FirstCrossingAlertsImmediately() {
			DiskSpace.AlertState state = new();
			DiskSpace.DiskAlert? alert = state.Update(50 * GIB, At(12, 5, 30));
			Assert.Equal(DiskSpace.DiskLevel.Notify, alert?.Level);
		}

		[Fact]
		public void Notify_RepeatsOnTheHourOnly() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(50 * GIB, At(12, 5)));
			Assert.Null(state.Update(50 * GIB, At(12, 6)));
			Assert.Null(state.Update(50 * GIB, At(12, 48)));
			Assert.Null(state.Update(50 * GIB, At(12, 59)));
			Assert.NotNull(state.Update(50 * GIB, At(13, 0)));
			Assert.Null(state.Update(50 * GIB, At(13, 1)));
		}

		[Fact]
		public void Warn_RepeatsOnTheTwelveMinuteMarks() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(10 * GIB, At(12, 3)));
			for (int minute = 4; minute < 12; minute++) {
				Assert.Null(state.Update(10 * GIB, At(12, minute)));
			}
			Assert.NotNull(state.Update(10 * GIB, At(12, 12)));
			Assert.Null(state.Update(10 * GIB, At(12, 13)));
			Assert.Null(state.Update(10 * GIB, At(12, 23)));
			Assert.NotNull(state.Update(10 * GIB, At(12, 24)));
			Assert.NotNull(state.Update(10 * GIB, At(12, 36)));
			Assert.NotNull(state.Update(10 * GIB, At(12, 48)));
			Assert.NotNull(state.Update(10 * GIB, At(13, 0)));
		}

		[Fact]
		public void Spam_AlertsEveryMinuteButNotTwiceInOne() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(1 * GIB, At(12, 0)));
			Assert.Null(state.Update(1 * GIB, At(12, 0, 30)));
			Assert.NotNull(state.Update(1 * GIB, At(12, 1)));
			Assert.NotNull(state.Update(1 * GIB, At(12, 2)));
		}

		[Fact]
		public void ACheckJustBeforeTheMarkDoesNotAlertButTheNextOneDoes() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(10 * GIB, At(11, 48)));
			Assert.Null(state.Update(10 * GIB, At(11, 59, 59.9)));
			Assert.NotNull(state.Update(10 * GIB, At(12, 0, 0)));
		}

		[Fact]
		public void WorseningAlertsAtOnce() {
			DiskSpace.AlertState state = new();
			Assert.Equal(DiskSpace.DiskLevel.Notify, state.Update(50 * GIB, At(12, 0))?.Level);
			Assert.Equal(DiskSpace.DiskLevel.Warn, state.Update(10 * GIB, At(12, 1))?.Level);
			Assert.Equal(DiskSpace.DiskLevel.Spam, state.Update(1 * GIB, At(12, 2))?.Level);
		}

		[Fact]
		public void RecoveryResetsSoTheNextCrossingAlertsAtOnce() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(10 * GIB, At(12, 3)));
			Assert.Null(state.Update(200 * GIB, At(12, 4)));
			Assert.NotNull(state.Update(10 * GIB, At(12, 5)));
		}

		[Fact]
		public void ImprovingBetweenLevelsDoesNotAlertEarly() {
			DiskSpace.AlertState state = new();
			Assert.NotNull(state.Update(1 * GIB, At(12, 0)));
			Assert.Null(state.Update(10 * GIB, At(12, 0, 30)));
		}

	}
}
