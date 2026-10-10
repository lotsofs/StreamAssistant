using System.Globalization;
using System.Text.Json;

namespace StreamAssistant2 {
	// StreamElements tips: anthem and chat line, then TTS
	internal static class Tips {
		const int TTS_DELAY_MS = 6000;
		// Fallback for a tip without a name
		const string NO_NAME = "Someone";

		// Ids alerted this run
		static readonly HashSet<string> _alerted = new();

		// Plays the alert for a channel.tips event
		internal static async Task Process(JsonElement data) {
			string id = data.ReadString("_id");
			if (!IsNew(id)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"Tip {id} already alerted, skipped");
				return;
			}
			string user = LanguageFilter.ReplaceBadWords(data.ReadString("donation.user.username"));
			if (string.IsNullOrWhiteSpace(user)) {
				user = NO_NAME;
			}
			decimal amount = data.ReadDecimal("donation.amount");
			string currency = data.ReadString("donation.currency");
			string message = LanguageFilter.ReplaceBadWords(data.ReadString("donation.message"));
			string status = data.ReadString("status", "?");
			string approved = data.ReadString("approved", "?");

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsNotification, $"Tip: {user} {Money(amount, currency)} (status {status}, approved {approved})");
			Sound.PlaySound(Sound.Sounds.IndianAnthem);
			TwitchIRCManager.SendMessage(ChatLine(user, amount, currency));
			await Task.Delay(TTS_DELAY_MS);
			_ = TextToSpeech.SpeakAsync(Sentence(user, amount, currency, message));
		}

		// False for an id already alerted this run; an empty id is always new
		internal static bool IsNew(string id) {
			if (id.Length == 0) {
				return true;
			}
			lock (_alerted) {
				return _alerted.Add(id);
			}
		}

		// "5", or "4.20" with two decimals
		internal static string FormatAmount(decimal amount) {
			string format = amount == decimal.Truncate(amount) ? "0" : "0.00";
			return amount.ToString(format, CultureInfo.InvariantCulture);
		}

		// "<amount> <currency>"
		internal static string Money(decimal amount, string currency) {
			return $"{FormatAmount(amount)} {currency}".TrimEnd();
		}

		// "<user> donated <amount> <currency>: <message>", without ": <message>" when there is none
		internal static string Sentence(string user, decimal amount, string currency, string message) {
			string sentence = $"{user} donated {Money(amount, currency)}";
			return string.IsNullOrWhiteSpace(message) ? sentence : $"{sentence}: {message}";
		}

		// "💸 <user> tipped <amount> <currency> 💸"
		internal static string ChatLine(string user, decimal amount, string currency) {
			return $"💸 {user} tipped {Money(amount, currency)} 💸";
		}
	}
}
