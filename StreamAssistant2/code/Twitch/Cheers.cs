
using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Cheers {
		const float MONEY_PER_BIT = 0.005f; // Half of earnings.

		internal async static Task Process(JsonElement evt) {
			bool is_anonymous = evt.ReadBool("is_anonymous");
			string user_login = is_anonymous ? "anonymous" : evt.ReadString("user_login", "unknown_user");
			int bits = evt.ReadInt("bits");
			string message = LanguageFilter.ReplaceBadWords(evt.ReadString("message"));

			// Money.Current += bits * MONEY_PER_BIT;
			Sound.PlaySound(Sound.Sounds.Team17Applauds);
			string read = $"{user_login} cheered {bits}: {message}";
			await Task.Delay(5000);
			_ = TextToSpeech.SpeakAsync(read);
		}
	}
}
