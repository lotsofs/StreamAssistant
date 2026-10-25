
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class TwitchUptime {
		const string URL_UPTIME = @"https://decapi.me/twitch/uptime/";

		static readonly TimeSpan MIN_GAP = TimeSpan.FromSeconds(5);

		private static readonly HttpClient _httpClient = new HttpClient();
		static readonly SemaphoreSlim _wake = new(0, 1);

		static int _lastSecondsListed = 60;

		static DateTime _lastUptimeCheck = DateTime.MinValue;

		public static int SecondsUntilNextMinute { 
			get {
				return 60 - _lastSecondsListed;
			}
		}

		internal static void Start() {
			_ = UptimeLoopAsync();
		}

		// Polls at once instead of at the next aimed minute
		internal static void PollNow() {
			try {
				_wake.Release();
			}
			catch (SemaphoreFullException) {
				// already pending
			}
		}

		// Polls, then waits until just after the next uptime minute or a wake
		static async Task UptimeLoopAsync() {
			while (true) {
				try {
					await UptimeCheck();
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error UpT5");
					ConsoleLogger.LogToFile(ex);
				}
				await _wake.WaitAsync(TimeSpan.FromSeconds(SecondsUntilNextMinute));
			}
		}

		// Fetches the uptime and writes it to OBS
		static async Task UptimeCheck() {
			// At most one request per MIN_GAP; a wake too soon waits out the rest
			TimeSpan since = DateTime.UtcNow - _lastUptimeCheck;
			if (since < MIN_GAP) {
				await Task.Delay(MIN_GAP - since);
			}
			_lastUptimeCheck = DateTime.UtcNow;

			// Do the thing
			string formattedUptime;
			HttpResponseMessage response;
			try {
				response = await _httpClient.GetAsync(URL_UPTIME + Config.Data.TwitchAuth.Login);
				
				var statusCode = response.StatusCode;
				if (response.IsSuccessStatusCode) {
					string rawUptime = await response.Content.ReadAsStringAsync();
					formattedUptime = ProcessUptime(rawUptime);
				}
				else {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Uptime request: HTTP Response Code {(int)statusCode}");
					formattedUptime = "?";
					_lastSecondsListed = 0;
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error UpT4: Connection lost");
				ConsoleLogger.LogToFile(ex);
				formattedUptime = "?";
				_lastSecondsListed = 0;
			}
			if (ObsConnection.IsReady) {
				Obs.SetInputSetting(Settings.Data.Uptime.Source, "text", "U: " + formattedUptime);
			}
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
				return "?";
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
