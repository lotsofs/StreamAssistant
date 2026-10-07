namespace StreamAssistant2 {
	/// <summary>
	/// Wall-clock arithmetic for work that should land on clock marks (every whole minute, every 12
	/// minutes past the hour, local midnight). Pure functions of the time passed in.
	/// </summary>
	internal static class ClockMarks {
		static readonly TimeSpan MIN_WAKE_DELAY = TimeSpan.FromMilliseconds(10);

		/// <summary>
		/// Which window of the given length the time falls in, numbered from the start of the day 1 AD.
		/// The number goes up by one exactly at each mark: for a 12 minute interval, at :00, :12, :24,
		/// :36 and :48, as long as the interval divides an hour.
		/// </summary>
		internal static long Window(DateTime t, TimeSpan interval) => t.Ticks / interval.Ticks;

		/// <summary>
		/// Time until the next whole minute, never less than a few milliseconds so a wake that lands
		/// just before the boundary can't spin.
		/// </summary>
		internal static TimeSpan UntilNextMinute(DateTime now) {
			TimeSpan wait = TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(now.Ticks % TimeSpan.TicksPerMinute);
			return wait < MIN_WAKE_DELAY ? MIN_WAKE_DELAY : wait;
		}

		/// <summary>
		/// The first local midnight strictly after the given time.
		/// </summary>
		internal static DateTime NextLocalMidnight(DateTime now) => now.Date.AddDays(1);
	}
}
