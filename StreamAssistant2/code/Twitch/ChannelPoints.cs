using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class ChannelPoints {
		const string REWARD_ID_TRAIN = "0260c648-c3ba-4ff1-920d-dffa5431da74";
		const string REWARD_ID_TOILET_FLUSH = "cd46e822-f288-47e6-8e8c-c56155603a0e";
		const string REWARD_ID_TOILET_RETRIEVE = "785967e7-9b58-41eb-aa11-15fed82a72ec";
		const string REWARD_ID_COLOR_RANDOM = "d1ca4789-8461-40a0-9335-e9080fd91f29";
		const string REWARD_ID_COLOR_SINGLE = "6fbb1ffa-555b-4e24-9cb1-f784d9f63689";
		const string REWARD_ID_COLOR_TRIPLE = "76c02fbc-d4ad-4fb7-984d-d057c9ebb03f";
		
		internal async static Task ProcessAdd(JsonElement evt) {
			string rewardId = evt.GetProperty("reward").GetProperty("id").GetString() ?? "";

			string flushId = evt.GetProperty("id").GetString() ?? "";
			string userId = evt.GetProperty("user_id").GetString() ?? "";
			string userLogin = evt.GetProperty("user_login").GetString() ?? "";
			string userInput = evt.GetProperty("user_input").GetString() ?? "";

			bool success = false;

			switch (rewardId) {
				case REWARD_ID_TOILET_FLUSH:
					Sound.PlaySound(Sound.Sounds.Flush);
					await Database.InsertFlushAsync(flushId, userId, userLogin);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully wrote flush for {userLogin}: {flushId}");
					break;
				case REWARD_ID_TOILET_RETRIEVE:
					var flushes = await Database.RetrieveFlushesAsync(userId);
					if (flushes.Count == 0) {
						TwitchIRCManager.SendMessage($"🪠 Digging through the sewers far and wide, the workers didn't find any of {userLogin}'s stuff 🪠");
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully cleared sewers for {userLogin}: NONE");
						return;
					}
					foreach (string id in flushes) {
						await TwitchHelixApi.UpdateRedemption(REWARD_ID_TOILET_FLUSH, id, "CANCELED");
					}
					await Database.DeleteFlushesAsync(userId);
					TwitchIRCManager.SendMessage($"🪠 Found and returned {flushes.Count} of {userLogin}'s flushed channel points 🪠");
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully cleared sewers for {userLogin}: {flushes.Count}");
					break;
				case REWARD_ID_TRAIN:
					int r = Random.Shared.Next(0, 100);
					Obs.SetImageSource("Image: Train", Path.Combine(Config.Data.Directories.Trains, $"Train{r}.png"));
					Obs.SetSourceEnabled("!Scene: Basics Colored", "Image: Train", true);
					await Task.Delay(62000);
					Obs.SetSourceEnabled("!Scene: Basics Colored", "Image: Train", false);
					Obs.SetImageSource("Image: Train", Path.Combine(Config.Data.Directories.Trains, "Empty.png"));
					break;
				case REWARD_ID_COLOR_RANDOM:
					LayoutColoring.ChangeToRandom();
					break;
				case REWARD_ID_COLOR_SINGLE:
					success = LayoutColoring.TryChangeToSingle(userInput);
					if (success) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Color change request fulfilled: {userInput}");
					}
					else {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Color change request FAILED: {userInput}");
					}
					break;
				case REWARD_ID_COLOR_TRIPLE:
					success = LayoutColoring.TryChangeToTriple(userInput);
					if (success) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Color change request fulfilled: {userInput}");
					}
					else {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Color change request FAILED: {userInput}");
					}
					break;
				default:
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Unhandled channel point reward redemption id: {rewardId}");
					ConsoleLogger.LogToFile(evt);
					break;
			}
		}
	}
}
