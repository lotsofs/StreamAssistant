using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchEventHandler {
		internal static void Handle(string type, JsonElement evtJson) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"notification: {type}");
			try {			
				switch (type) {
					case "channel.ad_break.begin":
						FireForget.Run("TEH_adb", type, () => Ads.Process(evtJson));
						break;
					case "channel.chat.notification":
						HandleChannelChatNotification(evtJson);
						break;
					case "channel.channel_points_custom_reward_redemption.add":
						FireForget.Run("TEH_cpcrra", type, () => ChannelPoints.ProcessAdd(evtJson));
						break;
					case "channel.cheer":
						FireForget.Run("TEH_c", type, () => Cheers.Process(evtJson));
						break;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Event Sub Event happened, but is not handled in code: {type}");
						break;
				}
				ConsoleLogger.LogToFile(evtJson);
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TEH1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		internal static void HandleChannelChatNotification(JsonElement json) {
			string notice_type = json.GetProperty("notice_type").GetString() ?? "???";
			string system_message = json.GetProperty("system_message").GetString() ?? "???";
			string message = json.GetProperty("message").GetProperty("text").GetString() ?? "???";

			string formattedJson = JsonSerializer.Serialize(json, new JsonSerializerOptions{WriteIndented=true});
			ConsoleLogger.LogToCustomFile(formattedJson, $"{notice_type}_{ConsoleLogger.TimeStamp(true)}.log");

			switch (notice_type) {
				case "sub":
					FireForget.Run("TEH_CN_s", notice_type, () => Subscriptions.HandleSubNotif(json));
					break;
				case "resub":
					FireForget.Run("TEH_CN_rs", notice_type, () => Subscriptions.HandleResubNotif(json));
					break;
				case "sub_gift":
					FireForget.Run("TEH_CN_sg", notice_type, () => Subscriptions.HandleSubGiftNotif(json));
					break;
				case "community_sub_gift":
					FireForget.Run("TEH_CN_csg", notice_type, () => Subscriptions.HandleCommunitySubGiftNotif(json));
					break;
				default:
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Unhandled chat notice event. | Type: {notice_type} | System_Message: {system_message} | Message: {message}");
					break;
			}
		}
	}
}
