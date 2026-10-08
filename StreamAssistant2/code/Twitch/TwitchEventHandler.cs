using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchEventHandler {
		static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

		/// <param name="isTest">From a !test script: don't write the EventSubs dump, which holds real events only.</param>
		internal static void Handle(string type, JsonElement evtJson, bool isTest = false) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"notification: {type}");
			try {
				if (!isTest) {
					DumpEvent(type, evtJson);
				}
				switch (type) {
					case "channel.ad_break.begin":
						FireForget.Run("TEH_adb", type, () => Ads.Process(evtJson));
						break;
					case "channel.chat.notification":
						HandleChannelChatNotification(evtJson);
						break;
					case "channel.channel_points_custom_reward_redemption.add":
						FireForget.Run("TEH_cpcrra", type, () => ChannelPoints.ProcessAdd(evtJson, isTest));
						break;
					case "channel.cheer":
						FireForget.Run("TEH_c", type, () => Cheers.Process(evtJson));
						break;
					case "channel.update":
						Games.HandleUpdate(evtJson);
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

		/// <summary>
		/// Writes the event, pretty-printed, to AssistantLogs\EventSubs for !test replay. Chat notifications
		/// are named by notice_type ("resub_&lt;stamp&gt;.log"), everything else by EventSub type
		/// ("channel.cheer_&lt;stamp&gt;.log"); notice types never contain a dot, which tells them apart.
		/// </summary>
		static void DumpEvent(string type, JsonElement evtJson) {
			string name = type == "channel.chat.notification" ? evtJson.ReadString("notice_type", "???") : type;
			string formattedJson = JsonSerializer.Serialize(evtJson, _indented);
			ConsoleLogger.LogToEventSubFile(formattedJson, $"{name}_{ConsoleLogger.TimeStamp(true)}.log");
		}

		internal static void HandleChannelChatNotification(JsonElement json) {
			string notice_type = json.ReadString("notice_type", "???");
			string system_message = json.ReadString("system_message", "???");
			string message = json.ReadString("message.text", "???");

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
