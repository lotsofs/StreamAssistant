
using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Cheers {
		static float MONEY_PER_BIT = 0.005f; // Half of earnings.

		internal async static Task Process(JsonElement evt) {
			bool is_anonymous = evt.GetProperty("is_anonymous").GetBoolean();
			string user_login = is_anonymous ? "anonymous" : (evt.GetProperty("user_login").GetString() ?? "unknown_user");
			int bits = evt.GetProperty("bits").GetInt32();
			string message = evt.GetProperty("message").GetString() ?? "";
			message = LanguageFilter.ReplaceBadWords(message);

			// Money.Current += bits * MONEY_PER_BIT;
			Sound.PlaySound(Sound.Sounds.Team17Applauds);
			string read = $"{user_login} cheered {bits}: {message}";
			await Task.Delay(5000);
			_ = TextToSpeech.SpeakAsync(read);
		}
	}
}
