using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	 internal class Clock {

		const string URL_UPTIME = @"https://decapi.me/twitch/uptime/lotsofs";
		const long UPTIME_REFRESH_RATE_IN_SECONDS = 20;
		const long DISK_REFRESH_RATE_IN_SECONDS = 60;

		const long TICKS_PER_MICROSECOND = 10;
		const long TICKS_PER_MILLISECOND = 1000 * TICKS_PER_MICROSECOND;
		const long TICKS_PER_SECOND = 1000 * TICKS_PER_MILLISECOND;
		const long TICKS_PER_MINUTE = 60 * TICKS_PER_SECOND;

		private long _previousUptimeCheckSecond = 0;
		private long _previousDiskSpaceCheckSecond = 0;
		private long _previousMinute = 0;

		const float GIBIBYTE = 1073741824;
		const float SPAM_BELOW_GB = 1f;
		const float WARN_BELOW_GB = 10f;
		const float NOTIFY_BELOW_GB = 60f;
		const int SPAM_INTERVAL = 1;
		const int WARN_INTERVAL = 15;
		const int NOTIFY_INTERVAL = 60;
		const string DRIVE_LETTER = "A:\\";

		const string SPAM_MSG = "⚠️⚠️⚠️🚨🚨 Alert: Less than {0} GB available on drive {1} - {2:0} B. Take action NOW or your vod will be bad and you will be sad 🚨🚨⚠️⚠️⚠️";
		const string WARN_MSG = "⚠️ Warning: Less than {0} GB available on drive {1} - {2:0.0} GB, take action soon ⚠️";
		const string NOTIFY_MSG = "Less than {0} GB available on drive {1} - {2:0.00} GB";

		internal void ProcessDiskSpace(long min) {
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo drive in drives) {
				if (drive.Name != DRIVE_LETTER) continue;

				float space = drive.AvailableFreeSpace / GIBIBYTE;
				if (space < SPAM_BELOW_GB && min % SPAM_INTERVAL == 0) {
					// Spam
					MsgQueue.Enqueue(MsgTypes.ChatMsg, string.Format(SPAM_MSG, SPAM_BELOW_GB, DRIVE_LETTER, drive.AvailableFreeSpace));
					Sound.PlaySound(Sound.Sounds.Warning);
					MsgQueue.Enqueue(MsgTypes.TextToS, string.Format(SPAM_MSG.Replace("⚠️⚠️⚠️🚨🚨", ""), SPAM_BELOW_GB, DRIVE_LETTER, drive.AvailableFreeSpace));
				}
				else if (space < WARN_BELOW_GB && min % WARN_INTERVAL == 0) {
					// Warn
					MsgQueue.Enqueue(MsgTypes.ChatMsg, string.Format(WARN_MSG, WARN_BELOW_GB, DRIVE_LETTER, space));
				}
				else if (space < NOTIFY_BELOW_GB && min % NOTIFY_INTERVAL == 0) {
					// Notify
					MsgQueue.Enqueue(MsgTypes.ChatMsg, string.Format(NOTIFY_MSG, NOTIFY_BELOW_GB, DRIVE_LETTER, space));
				}
			}
		}

		internal string ProcessUptime(string u) {
			if (u == "?") { return u; }
			
			string[] split = u.Split(' ');
			int hours = 0;
			int minutes = 0;

			for (int i = 0; i < split.Length; i += 2) { 
				switch (split[i+1].Substring(0,2)) {
					case "is": //"X is offline"
						return "Offline";
					case "da": //days
						hours = int.Parse(split[i]) * 24;
						break;
					case "ho": //hours
						hours = int.Parse(split[i]);
						break;
					case "mi": //minutes
						minutes = int.Parse(split[i]);
						break;
					case "se": //seconds
					default:
						break;
				}
			}
			return string.Format("{0}h {1:00}m", hours, minutes);
		}

		internal async void Tick() {
			long ticks = DateTime.Now.Ticks;
			long ms = ticks / TICKS_PER_MILLISECOND;
			long seconds = ticks / TICKS_PER_SECOND;
			long minutes = ticks / TICKS_PER_MINUTE;
			if (minutes > _previousMinute) {
				_previousMinute = minutes;
				string time = DateTime.Now.ToString("yyyy'-'MM'-'dd HH:mm");
				Obs.ChangeTextSourceText(Obs.Sources.Text_Clock, time);
			}
			if (seconds >= _previousUptimeCheckSecond + UPTIME_REFRESH_RATE_IN_SECONDS) {
				_previousUptimeCheckSecond = seconds;
				string uptime = "";
				try {
					using (HttpClient client = new HttpClient()) {
						uptime = await client.GetStringAsync(URL_UPTIME);
					}
				}
				catch ( Exception e ) {
					uptime = "?";
				}
				uptime = ProcessUptime(uptime);
				Obs.ChangeTextSourceText(Obs.Sources.Text_Uptime, "U: " + uptime);
			}
			if (seconds >= _previousDiskSpaceCheckSecond + DISK_REFRESH_RATE_IN_SECONDS) {
				_previousDiskSpaceCheckSecond = seconds;
				ProcessDiskSpace(minutes);
			}
		}
	}
}
