using System.Text.Json;

namespace StreamAssistant2 {
	// Raid chat line
	internal static class Raids {
		// Posts the raid to chat
		internal static void Handle(JsonElement n) {
			TwitchIRCManager.SendMessage(ChatLine(n));
		}

		// "Raid: <user> (<viewers>)"
		internal static string ChatLine(JsonElement n) {
			string user = n.ReadString("raid.user_name", "Someone");
			int viewers = n.ReadInt("raid.viewer_count");
			return $"Raid: {user} ({viewers})";
		}
	}
}
