using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Ads {
		const int MINUTES_BETWEEN_ADS = 60;

		static CancellationTokenSource? _warnings;

		internal async static Task Process(JsonElement evt) {
			bool is_automatic = evt.ReadBool("is_automatic");
			int duration_seconds = evt.ReadInt("duration_seconds");
			string auto = is_automatic ? "automatic" : "manual";
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.AdNotification, $"Running {auto} ad ({duration_seconds} seconds)");
			await Task.Delay(duration_seconds * 1000);
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.AdNotification, $"Ad break over");

			CancellationTokenSource next = new();
			Interlocked.Exchange(ref _warnings, next)?.Cancel();
			_ = RunScheduleAsync(BuildSchedule(), next.Token);
		}

		static IReadOnlyList<(TimeSpan At, Action Send)> BuildSchedule() {
			return [
				(TimeSpan.FromMinutes(MINUTES_BETWEEN_ADS - 5), () => WarnChat("Obligatory ad in 5 minutes :(")),
				(TimeSpan.FromMinutes(MINUTES_BETWEEN_ADS - 3), SpamChat),
				(TimeSpan.FromMinutes(MINUTES_BETWEEN_ADS - 2), () => WarnChat("Obligatory ad in 2 minutes :( :( :(")),
				(TimeSpan.FromMinutes(MINUTES_BETWEEN_ADS - 1), () => WarnChat("Obligatory ad coming right up. Maybe snooze it if something interesting is about to happen.")),
			];
		}

		/// <summary>
		/// Runs each entry's Send once the given time after now has passed, in order, until cancelled.
		/// A Send that throws is logged and doesn't stop the later ones.
		/// </summary>
		internal static async Task RunScheduleAsync(IReadOnlyList<(TimeSpan At, Action Send)> schedule, CancellationToken token) {
			DateTime start = DateTime.UtcNow;
			try {
				foreach ((TimeSpan at, Action send) in schedule) {
					TimeSpan wait = start + at - DateTime.UtcNow;
					if (wait > TimeSpan.Zero) {
						await Task.Delay(wait, token);
					}
					token.ThrowIfCancellationRequested();
					try {
						send();
					}
					catch (Exception ex) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error ADS1");
						ConsoleLogger.LogToFile(ex);
					}
				}
			}
			catch (OperationCanceledException) {
			}
		}

		internal static void WarnChat(string msg) {
			TwitchIRCManager.SendMessage(msg);
		}

		internal static void SpamChat() {
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
		}

	}
}
