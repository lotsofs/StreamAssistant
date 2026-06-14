using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchEventHandler {
		internal static void Handle(string type, JsonElement json) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"notification: {type}");
			try {			
				switch (type) {
					case "channel.chat.notification":
						HandleChannelChatNotification(json);
						break;
					case "channel.channel_points_custom_reward_redemption.add":
						_ = ChannelPoints.ProcessAdd(json);
						break;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Event Sub Event happened, but is not handled in code: {type}");
						break;
				}
				ConsoleLogger.LogToFile(json);
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
					_ = Subscriptions.HandleSubNotif(json);
					break;
				case "resub":
					_ = Subscriptions.HandleResubNotif(json);
					break;
				case "sub_gift":
					_ = Subscriptions.HandleSubGiftNotif(json);
					break;
				case "community_sub_gift":
					_ = Subscriptions.HandleCommunitySubGiftNotif(json);
					break;
				default:
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Unhandled chat notice event. | Type: {notice_type} | System_Message: {system_message} | Message: {message}");
					break;
			}
		}
	}
}
