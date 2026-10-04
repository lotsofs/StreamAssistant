using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Ads {
		const int MINUTES_BETWEEN_ADS = 60;

		static List<Clock.ScheduledJob> _pendingJobs = new();

		internal async static Task Process(JsonElement evt) {
			string is_automatic = evt.GetProperty("is_automatic").GetString() ?? "false";
			int duration_seconds = int.Parse(evt.GetProperty("duration_seconds").GetString() ?? "0");
			string auto = is_automatic == "true" ? "automatic" : "manual";
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.AdNotification, $"Running {auto} ad ({duration_seconds} seconds)");
			await Task.Delay(duration_seconds * 1000);
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.AdNotification, $"Ad break over");

			foreach (var job in _pendingJobs) {
				job.Cancellation.Cancel();
			}
			_pendingJobs.Clear();

			CreateEarlyWarning(MINUTES_BETWEEN_ADS-5, () => WarnChat("Obligatory ad in 5 minutes :("));
			CreateEarlyWarning(MINUTES_BETWEEN_ADS-3, SpamChat);
			CreateEarlyWarning(MINUTES_BETWEEN_ADS-2, () => WarnChat("Obligatory ad in 2 minutes :( :( :("));
			CreateEarlyWarning(MINUTES_BETWEEN_ADS-1, () => WarnChat("Obligatory ad coming right up. Maybe snooze it if something interesting is about to happen."));
		}
		
		internal static async Task WarnChat(string msg) {
			TwitchIRCManager.SendMessage(msg);
			await Task.CompletedTask; 
		}

		internal static async Task SpamChat() {
			int r = Random.Shared.Next(3);
			switch (r) {
				case 0:
					TwitchIRCManager.SendMessage("sssDino If you are enjoying the stream, please consider giving a follow. Follows are free and won't be announced on my stream.");
					break;
				case 1:
					TwitchIRCManager.SendMessage("sssDino If you are enjoying the stream, please consider subscribing if you are able to, it helps support the stream and would be greatly appreciated!");
					break;
				case 2:
					TwitchIRCManager.SendMessage("sssDino Did you know that if you're an Amazon Prime member, you get one free Prime Sub? If you are enjoying the stream, it would be greatly appreciated if you used it on my stream.");
					break;
				default:
					break;
			}
			r = Random.Shared.Next(100);
			if (r == 0) {
				TwitchIRCManager.SendMessage("@LotsOfS Stop making me beg for shit >(");
			}
			await Task.CompletedTask;
		}

		static void CreateEarlyWarning(int minutesFromNow, Func<Task> action) {
			var job = new Clock.ScheduledJob {
				Repeat = false,
				GetNextRun = () => DateTime.UtcNow.AddMinutes(minutesFromNow),
				Action = action
			};

			_pendingJobs.Add(job);
			Clock.AddJob(job);
		}

	}
}
