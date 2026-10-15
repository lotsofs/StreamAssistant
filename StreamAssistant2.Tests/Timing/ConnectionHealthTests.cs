using Xunit;
using Band = StreamAssistant2.ConnectionHealth.Band;

namespace StreamAssistant2.Tests {
	public class ConnectionHealthTests {
		static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

		static ConnectionHealth.Tracker NewTracker() => new("EventSub", ConnectionHealth.EventSub);

		[Theory]
		[InlineData(0, (int)Band.Healthy)]
		[InlineData(12, (int)Band.Healthy)]
		[InlineData(12.1, (int)Band.Warn)]
		[InlineData(15, (int)Band.Warn)]
		[InlineData(15.1, (int)Band.Bad)]
		[InlineData(20, (int)Band.Bad)]
		[InlineData(20.1, (int)Band.Dead)]
		public void EventSub_Classify(double seconds, int expected) {
			Assert.Equal((Band)expected, ConnectionHealth.EventSub.Classify(S(seconds)));
		}

		[Theory]
		[InlineData(300, (int)Band.Healthy)]
		[InlineData(301, (int)Band.Warn)]
		[InlineData(361, (int)Band.Bad)]
		[InlineData(421, (int)Band.Dead)]
		public void Irc_Classify(double seconds, int expected) {
			Assert.Equal((Band)expected, ConnectionHealth.Irc.Classify(S(seconds)));
		}

		// A healthy StreamElements connection counts up to the 30 s probe and resets.
		[Theory]
		[InlineData(30, (int)Band.Healthy)]
		[InlineData(40, (int)Band.Healthy)]
		[InlineData(40.1, (int)Band.Warn)]
		[InlineData(50, (int)Band.Warn)]
		[InlineData(50.1, (int)Band.Bad)]
		[InlineData(70, (int)Band.Bad)]
		[InlineData(70.1, (int)Band.Dead)]
		public void StreamElements_Classify(double seconds, int expected) {
			Assert.Equal((Band)expected, ConnectionHealth.StreamElements.Classify(S(seconds)));
		}

		[Fact]
		public void Dead_IsTheReconnectTimeout() {
			Assert.Equal(TwitchIRCManager.SilenceTimeout, ConnectionHealth.Irc.Dead);
			Assert.Equal(TwitchEventSub.KeepAliveTimeout, ConnectionHealth.EventSub.Dead);
			Assert.Equal(StreamElementsSocket.SilenceTimeout, ConnectionHealth.StreamElements.Dead);
		}

		[Fact]
		public void NoData_IsSilentEvenWhenOld() {
			var t = NewTracker();
			Assert.Null(t.Update(S(1000), false));
			Assert.Null(t.Update(S(0), true));
		}

		[Fact]
		public void WarnOnly_IsSilent() {
			var t = NewTracker();
			Assert.Null(t.Update(S(13), true));
			Assert.Null(t.Update(S(14), true));
			Assert.Null(t.Update(S(1), true));
		}

		[Fact]
		public void EnteringBad_ReportsOnce_ThenDeadAddsNothing() {
			var t = NewTracker();
			Assert.Null(t.Update(S(13), true));
			var report = t.Update(S(16), true);
			Assert.NotNull(report);
			Assert.False(report!.Recovered);
			Assert.Equal("EventSub quiet for 00:16, reconnecting at 00:20", report.Text);
			Assert.Null(t.Update(S(18), true));
			Assert.Null(t.Update(S(21), true));
		}

		[Fact]
		public void JumpingStraightToDead_Reports() {
			var t = NewTracker();
			Assert.NotNull(t.Update(S(25), true));
		}

		[Fact]
		public void WobbleAcrossBad_ReportsOnce() {
			var t = NewTracker();
			Assert.NotNull(t.Update(S(16), true));
			Assert.Null(t.Update(S(14), true));
			Assert.Null(t.Update(S(16), true));
			Assert.Null(t.Update(S(14), true));
		}

		[Fact]
		public void Recovery_ReportsOnceWithTheLongestSilence() {
			var t = NewTracker();
			t.Update(S(16), true);
			t.Update(S(18.5), true);
			var report = t.Update(S(0.2), true);
			Assert.NotNull(report);
			Assert.True(report!.Recovered);
			Assert.Equal("EventSub recovered after 00:18 of silence", report.Text);
			Assert.Null(t.Update(S(1), true));
		}

		[Fact]
		public void RecoveryThroughWarn_ReportsOnlyAtHealthy() {
			var t = NewTracker();
			t.Update(S(16), true);
			Assert.Null(t.Update(S(13), true));
			Assert.NotNull(t.Update(S(2), true));
		}

		[Fact]
		public void ReconnectGap_IsIgnored_ThenRecoveryReports() {
			var t = NewTracker();
			t.Update(S(19), true);
			Assert.Null(t.Update(S(0.5), false));
			Assert.Null(t.Update(S(3), false));
			var report = t.Update(S(0.1), true);
			Assert.NotNull(report);
			Assert.True(report!.Recovered);
		}

		[Fact]
		public void AfterRecovery_ANewDegradationReportsAgain() {
			var t = NewTracker();
			t.Update(S(16), true);
			t.Update(S(0), true);
			Assert.NotNull(t.Update(S(16), true));
		}

		[Theory]
		[InlineData(0, "00:00")]
		[InlineData(65, "01:05")]
		[InlineData(420, "07:00")]
		[InlineData(3725, "62:05")]
		public void Format_MinutesAndSeconds(double seconds, string expected) {
			Assert.Equal(expected, ConnectionHealth.Format(S(seconds)));
		}
	}
}
