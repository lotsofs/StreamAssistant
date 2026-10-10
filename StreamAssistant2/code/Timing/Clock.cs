namespace StreamAssistant2 {
	// Time-of-day work on every whole minute: the OBS clock text, and the date in chat at local midnight
	internal static class Clock {
		const string SOURCE = "Text: Time Of Day";
		const string CLOCK_FORMAT = "yyyy'-'MM'-'dd HH':'mm";
		const string DATE_FORMAT = "yyyy'-'MM'-'dd";

		static DateTime _today;

		// Starts the clock loop; today's date is not posted
		internal static void Start() {
			_today = DateTime.Now.Date;
			_ = WatchAsync();
		}

		// Writes the clock, and posts the date when the day has changed, on every whole minute
		static async Task WatchAsync() {
			while (true) {
				await Task.Delay(ClockMarks.UntilNextMinute(DateTime.Now));
				DateTime now = DateTime.Now;
				try {
					WriteObs(now);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error CLK1");
					ConsoleLogger.LogToFile(ex);
				}
				try {
					PostDateIfNewDay(now);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error CLK2");
					ConsoleLogger.LogToFile(ex);
				}
			}
		}

		// Sets the OBS clock text, if OBS is ready
		static void WriteObs(DateTime now) {
			if (!ObsConnection.IsReady) {
				return;
			}
			Obs.SetInputSetting(SOURCE, "text", ClockText(now));
		}

		// Posts the date to chat on the first tick of a new day
		static void PostDateIfNewDay(DateTime now) {
			if (now.Date == _today) {
				return;
			}
			_today = now.Date;
			TwitchIRCManager.SendMessage(now.ToString(DATE_FORMAT));
		}

		// "yyyy-MM-dd HH:mm" in any culture
		internal static string ClockText(DateTime t) => t.ToString(CLOCK_FORMAT);
	}
}
