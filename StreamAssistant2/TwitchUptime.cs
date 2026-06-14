
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class TwitchUptime {
		const string URL_UPTIME = @"https://decapi.me/twitch/uptime/lotsofs";

		const int UPTIME_READ_TIMEOUT = 5;

		private static readonly HttpClient _httpClient = new HttpClient();

		static int _previousMinute = -1;
		static string _previousUptime = "";
		static int _lastSecondsListed = 60;

		static DateTime _lastUptimeCheck = DateTime.MinValue;

		public static int SecondsUntilNextMinute { 
			get {
				return 60 - _lastSecondsListed;
			}
		}

		public static void ClockCheck() {
			DateTime localNow = DateTime.Now;
			if (localNow.Minute == _previousMinute) {
				return;
			}
			if (localNow.Minute == 0 && localNow.Hour == 0) {
				TwitchIRCManager.SendMessage(localNow.ToString("yyyy'-'MM'-'dd"));
			}
			// TODO: Update OBS text
			_previousMinute = localNow.Minute;
		}

		public static async Task UptimeCheck() {
			// Don't spam the server. Prevent rate limits.
			DateTime now = DateTime.UtcNow;
			TimeSpan duration = now - _lastUptimeCheck;
			double secs = duration.TotalSeconds;
			if (secs < UPTIME_READ_TIMEOUT) {
				_lastSecondsListed = 0;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Attempted to read uptime twice in {UPTIME_READ_TIMEOUT} seconds. {secs}s");
				return;
			}
			_lastUptimeCheck = now;
			
			// Do the thing
			string formattedUptime;
			HttpResponseMessage response;
			try {
				response = await _httpClient.GetAsync(URL_UPTIME);
				
				var statusCode = response.StatusCode;
				if (response.IsSuccessStatusCode) {
					string rawUptime = await response.Content.ReadAsStringAsync();
					formattedUptime = ProcessUptime(rawUptime);
				}
				else {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Uptime request: HTTP Response Code {(int)statusCode}");
					formattedUptime = ((int)statusCode).ToString();
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error UpT4: Connection lost");
				ConsoleLogger.LogToFile(ex);
				formattedUptime = "Error";
			}
			if (formattedUptime == _previousUptime) {
				return;
			}
			_previousUptime = formattedUptime;
			// TODO: Write in OBS
		}

		static string ProcessUptime(string input) {
			if (input.EndsWith(" is offline")) {
				_lastSecondsListed = 0;
				return "Offline";
			}
			int hours = 0;
			int minutes = 0;

			Regex regex = new Regex(@"(?:(\d+)\s*day)?s?,?\s*(?:(\d+)\s*hour)?s?,?\s*(?:(\d+)\s*minute)?s?,?\s*(?:(\d+)\s*second)?s?", RegexOptions.IgnoreCase);

			var match = regex.Match(input);
			if (!match.Success) {
				TwitchIRCManager.SendMessage("❌ Regex Error: " + input);
				return "Error";
			}

			if (!string.IsNullOrEmpty(match.Groups[2].Value)) {
				hours = int.Parse(match.Groups[2].Value);
			}
			if (!string.IsNullOrEmpty(match.Groups[1].Value)) {
				int days = int.Parse(match.Groups[1].Value);
				hours += days*24;
			}
			if (!string.IsNullOrEmpty(match.Groups[3].Value)) {
				minutes = int.Parse(match.Groups[3].Value);
			}
			if (!string.IsNullOrEmpty(match.Groups[4].Value)) {
				_lastSecondsListed = int.Parse(match.Groups[4].Value);
			}
			else {
				_lastSecondsListed = 0;
			}
			return string.Format("{0}h {1:00}m", hours, minutes);
		}
	}
}
