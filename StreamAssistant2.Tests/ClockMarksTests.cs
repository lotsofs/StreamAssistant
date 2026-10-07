using Xunit;

namespace StreamAssistant2.Tests {
	public class ClockMarksTests {
		static DateTime At(int hour, int minute, double second = 0) =>
			new DateTime(2026, 10, 7, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

		[Fact]
		public void Window_StepsExactlyOnTheMarks() {
			TimeSpan twelve = TimeSpan.FromMinutes(12);
			long window = ClockMarks.Window(At(12, 0), twelve);
			Assert.Equal(window, ClockMarks.Window(At(12, 11, 59.9), twelve));
			Assert.Equal(window + 1, ClockMarks.Window(At(12, 12), twelve));
			Assert.Equal(window + 2, ClockMarks.Window(At(12, 24), twelve));
			Assert.Equal(window + 5, ClockMarks.Window(At(13, 0), twelve));
		}

		[Fact]
		public void Window_HourIntervalStepsOnTheHour() {
			TimeSpan hour = TimeSpan.FromHours(1);
			Assert.Equal(ClockMarks.Window(At(12, 0), hour), ClockMarks.Window(At(12, 59, 59.9), hour));
			Assert.Equal(ClockMarks.Window(At(12, 0), hour) + 1, ClockMarks.Window(At(13, 0), hour));
		}

		[Fact]
		public void UntilNextMinute_IsTheTimeToTheBoundary() {
			Assert.Equal(TimeSpan.FromSeconds(30), ClockMarks.UntilNextMinute(At(12, 0, 30)));
			Assert.Equal(TimeSpan.FromMinutes(1), ClockMarks.UntilNextMinute(At(12, 0)));
		}

		[Fact]
		public void UntilNextMinute_NeverReturnsLessThanTheFloor() {
			Assert.Equal(TimeSpan.FromMilliseconds(10), ClockMarks.UntilNextMinute(At(11, 59, 59.9999)));
		}

		[Fact]
		public void NextLocalMidnight_JustBeforeMidnightIsTheComingMidnight() {
			DateTime now = new(2026, 10, 7, 23, 59, 59, 900);
			Assert.Equal(new DateTime(2026, 10, 8, 0, 0, 0), ClockMarks.NextLocalMidnight(now));
		}

		[Fact]
		public void NextLocalMidnight_ExactlyMidnightIsTheFollowingOne() {
			DateTime now = new(2026, 10, 8, 0, 0, 0);
			Assert.Equal(new DateTime(2026, 10, 9, 0, 0, 0), ClockMarks.NextLocalMidnight(now));
		}

		[Fact]
		public void NextLocalMidnight_MiddayIsTheNextMidnight() {
			Assert.Equal(new DateTime(2026, 10, 8, 0, 0, 0), ClockMarks.NextLocalMidnight(new DateTime(2026, 10, 7, 12, 30, 0)));
		}

		[Fact]
		public void NextLocalMidnight_CrossesMonthAndYearEnds() {
			Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0), ClockMarks.NextLocalMidnight(new DateTime(2026, 12, 31, 18, 0, 0)));
		}
	}
}
