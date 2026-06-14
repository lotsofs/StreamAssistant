
using System.Security.Cryptography;

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
		
		const double GIBIBYTE = 1073741824;
				
		const string DRIVE_LETTER = "A:\\";

		const double SPAM_GB_THRESHOLD = 2;
		const double WARN_GB_THRESHOLD = 15;
		const double NOTIFY_GB_THRESHOLD = 100;
		
		const string SPAM_SPEAK = "Alert: Less than {0} GB available on drive {1}. Take action NOW or your vod will be bad and you will be sad";
		const string SPAM_CHAT = "⚠️⚠️⚠️🚨🚨 Alert: Less than {0} GB available on drive {1} - {2:0} B. Take action NOW or your vod will be bad and you will be sad 🚨🚨⚠️⚠️⚠️";
		const string WARN_CHAT = "⚠️ Warning: Less than {0} GB available on drive {1} - {2:0.0} GB, take action soon ⚠️";
		const string NOTIFY_CHAT = "Less than {0} GB available on drive {1} - {2:0.00} GB";

		static double previousSpace = double.MaxValue;
		static DateTime previousPrintTime = DateTime.UnixEpoch;

		static DriveInfo GetDrive() {
			DriveInfo[] drives = DriveInfo.GetDrives();
			DriveInfo? drive = drives.FirstOrDefault(d => d.Name == DRIVE_LETTER);
			drive ??= drives[0];
			return drive;
		}

		public static void CheckSpaceAndNotify() {
			DriveInfo d = GetDrive();
			double space = d.AvailableFreeSpace;

			if (space < SPAM_GB_THRESHOLD*GIBIBYTE) {
				Notify(1, string.Format(SPAM_CHAT, SPAM_GB_THRESHOLD, DRIVE_LETTER, space.ToString("N0").Replace(","," ")));
				TextToSpeech.EnqueueSpeech(string.Format(SPAM_SPEAK, SPAM_GB_THRESHOLD, DRIVE_LETTER));
				// TODO: Sound
			}
			else if (space < WARN_GB_THRESHOLD*GIBIBYTE) {
				if (previousSpace >= WARN_GB_THRESHOLD*GIBIBYTE) {
					Notify(1, string.Format(WARN_CHAT, WARN_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
				}
				else {
					Notify(12, string.Format(WARN_CHAT, WARN_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
				}
			}
			else if (space < NOTIFY_GB_THRESHOLD*GIBIBYTE) {
				if (previousSpace >= NOTIFY_GB_THRESHOLD*GIBIBYTE) {
					Notify(1, string.Format(NOTIFY_CHAT, NOTIFY_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
				}
				else {
					Notify(60, string.Format(NOTIFY_CHAT, NOTIFY_GB_THRESHOLD, DRIVE_LETTER, space/GIBIBYTE));
				}
			}
			previousSpace = space;
			return;
		}
		
		static void Notify(int intervalInMinutes, string chat) {
			DateTime now = DateTime.UtcNow;
			var timeSinceLast = now - previousPrintTime;
			if (now.Second > 0) {
				// Hack to avoid printing when it is not the whole minute
				return;
			}
			if (timeSinceLast.TotalSeconds <= 20) {
				// Do not write in chat if we just wrote
				return;
			}
			if (now.Minute % intervalInMinutes != 0) {
				// Do not write on off-interval minutes
				return;
			}
			TwitchIRCManager.SendMessage(chat);
			previousPrintTime = now;
		}
	}
}
