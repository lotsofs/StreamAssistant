namespace StreamAssistant2 {
	// Age thresholds for IRC, EventSub and StreamElements, and a loop that logs when a connection goes bad or recovers
	internal static class ConnectionHealth {
		// Best to worst
		internal enum Band {
			Healthy,
			Warn,
			Bad,
			Dead,
		}

		// Ages where Warn, Bad and Dead start
		internal sealed record Thresholds(TimeSpan Warn, TimeSpan Bad, TimeSpan Dead) {
			// Band for an age
			internal Band Classify(TimeSpan age) {
				if (age > Dead) {
					return Band.Dead;
				}
				if (age > Bad) {
					return Band.Bad;
				}
				if (age > Warn) {
					return Band.Warn;
				}
				return Band.Healthy;
			}
		}

		// Warn, Bad, Dead per connection; Dead is the reconnect timeout
		internal static readonly Thresholds Irc = new(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(360), TwitchIRCManager.SilenceTimeout);
		internal static readonly Thresholds EventSub = new(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15), TwitchEventSub.KeepAliveTimeout);
		internal static readonly Thresholds StreamElements = new(TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(50), StreamElementsSocket.SilenceTimeout);

		// A line to log: going bad, or recovered
		internal sealed record Report(bool Recovered, string Text);

		// One per connection. Reports once on reaching Bad, once on getting back to Healthy
		internal sealed class Tracker(string name, Thresholds thresholds) {
			// Reached Bad, not yet back to Healthy
			bool _degraded;
			// Oldest age while degraded
			TimeSpan _longest;

			// Takes the current age; returns a line to log, or null
			internal Report? Update(TimeSpan age, bool hasData) {
				// No data: skip
				if (!hasData) {
					return null;
				}
				Band band = thresholds.Classify(age);
				if (!_degraded) {
					if (band < Band.Bad) {
						return null;
					}
					_degraded = true;
					_longest = age;
					return new Report(false, $"{name} quiet for {Format(age)}, reconnecting at {Format(thresholds.Dead)}");
				}
				// Still degraded
				if (band != Band.Healthy) {
					_longest = age > _longest ? age : _longest;
					return null;
				}
				_degraded = false;
				return new Report(true, $"{name} recovered after {Format(_longest)} of silence");
			}
		}

		// mm:ss
		internal static string Format(TimeSpan age) => $"{(int)age.TotalMinutes:00}:{age.Seconds:00}";

		static readonly Tracker _irc = new("IRC", Irc);
		static readonly Tracker _eventSub = new("EventSub", EventSub);
		static readonly Tracker _streamElements = new("StreamElements", StreamElements);

		// Starts the watch loop
		internal static void Start() {
			_ = WatchAsync();
		}

		// Every second: updates the trackers and logs what they report
		static async Task WatchAsync() {
			while (true) {
				await Task.Delay(TimeSpan.FromSeconds(1));
				try {
					Log(_irc.Update(TwitchIRCManager.TimeSinceLastPing, TwitchIRCManager.HasReceivedLine));
					Log(_eventSub.Update(TwitchEventSub.KeepAliveTimer.Elapsed, TwitchEventSub.IsConnected));
					Log(_streamElements.Update(StreamElementsSocket.SinceLastMessage.Elapsed, StreamElementsSocket.IsConnected));
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error CHL1");
					ConsoleLogger.LogToFile(ex);
				}
			}
		}

		// Writes a report to the window and log file
		static void Log(Report? report) {
			if (report == null) {
				return;
			}
			var type = report.Recovered ? ConsoleLogger.ColorType.ConnectionNotification : ConsoleLogger.ColorType.Important;
			ConsoleLogger.ColoredLine(type, report.Text);
		}
	}
}
