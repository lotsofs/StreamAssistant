using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	 internal class Clock {

		const string URL_UPTIME = @"https://decapi.me/twitch/uptime/lotsofs";
		const long UPTIME_REFRESH_RATE_IN_SECONDS = 2;

		const long TICKS_PER_MICROSECOND = 10;
		const long TICKS_PER_MILLISECOND = 1000 * TICKS_PER_MICROSECOND;
		const long TICKS_PER_SECOND = 1000 * TICKS_PER_MILLISECOND;
		const long TICKS_PER_MINUTE = 60 * TICKS_PER_SECOND;

		private long _previousSecond = 0;
		private long _previousMinute = 0;

		internal string ProcessUptime(string u) {
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
			long seconds = ticks / TICKS_PER_SECOND;
			long minutes = ticks / TICKS_PER_MINUTE;
			if (minutes > _previousMinute) {
				_previousMinute = minutes;
				string time = DateTime.Now.ToString("yyyy'-'MM'-'dd HH:mm");
				Obs.ChangeText(Obs.Sources.Text_Clock, time);
			}
			if (seconds >= _previousSecond + UPTIME_REFRESH_RATE_IN_SECONDS) {
				_previousSecond = seconds;
				string uptime = "";
				using (HttpClient client = new HttpClient()) {
					uptime = await client.GetStringAsync(URL_UPTIME);
				}
				uptime = ProcessUptime(uptime);
				Obs.ChangeText(Obs.Sources.Text_Uptime, "U: " + uptime);
			}
		}
	}
}
