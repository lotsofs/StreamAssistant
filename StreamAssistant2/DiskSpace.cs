
namespace StreamAssistant2 {
	internal class DiskSpace {
		internal class DiskSpaceCheck {
			internal double threshold;
			internal string chatMessage;
			internal string ttsMessage;
			internal string audioFile;

			internal DiskSpaceCheck(double th, string chat, string tts, string audio) {
				threshold = th;
				chatMessage = chat;
				ttsMessage = tts;
				audioFile = audio;
			}
		}

		// Ordered by severity: a higher value is worse.
		internal enum DiskLevel {
			Ok,
			Notify,
			Warn,
			Spam,
		}

		internal sealed record DiskAlert(DiskLevel Level, double FreeBytes);

		/// <summary>
		/// Decides when a free-space reading deserves a message. A level that gets worse alerts at once.
		/// A level that persists alerts on the first check inside each new window of its interval
		/// (every 12 minutes means :00, :12, :24, :36 and :48), so repeats land on the clock mark whenever
		/// the check happens to run. Nothing here depends on the second or minute of the reading.
		/// </summary>
		internal sealed class AlertState {
			DiskLevel _lastLevel = DiskLevel.Ok;
			DateTime _lastAlertUtc = DateTime.MinValue;

			internal DiskAlert? Update(double freeBytes, DateTime nowUtc) {
				DiskLevel level = Classify(freeBytes);
				if (level == DiskLevel.Ok) {
					_lastLevel = DiskLevel.Ok;
					return null;
				}

				TimeSpan interval = TimeInterval(level);
				bool newWindow = ClockMarks.Window(nowUtc, interval) > ClockMarks.Window(_lastAlertUtc, interval);

				bool worse = level > _lastLevel;
				_lastLevel = level;
				if (!worse && !newWindow) {
					return null;
				}
				_lastAlertUtc = nowUtc;
				return new DiskAlert(level, freeBytes);
			}
		}

		const double GIBIBYTE = 1073741824;

		const string DRIVE_LETTER = "A:\\";

		const double SPAM_GB_THRESHOLD = 2;
		const double WARN_GB_THRESHOLD = 15;
		const double NOTIFY_GB_THRESHOLD = 100;

		const string SPAM_SPEAK = "Alert: Less than {0} GB available on drive {1}. Take action NOW or your vod will be bad and you will be sad";
		const string SPAM_CHAT = "⚠️⚠️⚠️🚨🚨 Alert: Less than {0} GB available on drive {1} - {2:0} B. Take action NOW or your vod will be bad and you will be sad 🚨🚨⚠️⚠️⚠️";
		const string WARN_CHAT = "⚠️ Warning: Less than {0} GB available on drive {1} - {2:0.0} GB, take action soon ⚠️";
		const string NOTIFY_CHAT = "Less than {0} GB available on drive {1} - {2:0.00} GB";

		static readonly AlertState _state = new();

		internal static DiskLevel Classify(double freeBytes) {
			if (freeBytes < SPAM_GB_THRESHOLD * GIBIBYTE) {
				return DiskLevel.Spam;
			}
			if (freeBytes < WARN_GB_THRESHOLD * GIBIBYTE) {
				return DiskLevel.Warn;
			}
			if (freeBytes < NOTIFY_GB_THRESHOLD * GIBIBYTE) {
				return DiskLevel.Notify;
			}
			return DiskLevel.Ok;
		}

		// Every interval divides an hour, so windows of the tick count start exactly on the clock marks.
		internal static TimeSpan TimeInterval(DiskLevel level) => level switch {
			DiskLevel.Spam => TimeSpan.FromMinutes(1),
			DiskLevel.Warn => TimeSpan.FromMinutes(12),
			DiskLevel.Notify => TimeSpan.FromMinutes(60),
			_ => throw new ArgumentOutOfRangeException(nameof(level)),
		};

		internal static void Start() {
			_ = WatchAsync();
		}

		static async Task WatchAsync() {
			while (true) {
				await Task.Delay(ClockMarks.UntilNextMinute(DateTime.UtcNow));
				try {
					CheckSpaceAndNotify();
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error DSK1");
					ConsoleLogger.LogToFile(ex);
				}
			}
		}

		static DriveInfo GetDrive() {
			DriveInfo[] drives = DriveInfo.GetDrives();
			DriveInfo? drive = drives.FirstOrDefault(d => d.Name == DRIVE_LETTER);
			drive ??= drives[0];
			return drive;
		}

		static void CheckSpaceAndNotify() {
			double space = GetDrive().AvailableFreeSpace;
			DiskAlert? alert = _state.Update(space, DateTime.UtcNow);
			if (alert == null) {
				return;
			}

			switch (alert.Level) {
				case DiskLevel.Spam:
					TwitchIRCManager.SendMessage(string.Format(SPAM_CHAT, SPAM_GB_THRESHOLD, DRIVE_LETTER, space.ToString("N0").Replace(","," ")));
					TextToSpeech.EnqueueSpeech(string.Format(SPAM_SPEAK, SPAM_GB_THRESHOLD, DRIVE_LETTER));
					// TODO: Sound
					break;
				case DiskLevel.Warn:
					TwitchIRCManager.SendMessage(string.Format(WARN_CHAT, WARN_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
					break;
				case DiskLevel.Notify:
					TwitchIRCManager.SendMessage(string.Format(NOTIFY_CHAT, NOTIFY_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
					break;
			}
		}
	}
}
