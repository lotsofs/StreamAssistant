using System.Text.Json;

namespace StreamAssistant2 {
	// Twitch's own stream online and offline events
	internal static class StreamEvents {
		// Logs Twitch marking the stream live and polls the uptime
		internal static void Online(JsonElement evt) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Twitch: stream online ({evt.ReadString("started_at", "?")})");
			TwitchUptime.PollNow();
		}

		// Logs Twitch marking the stream offline
		internal static void Offline() {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Twitch: stream offline");
		}
	}
}
