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
		const string REWARD_ID_COLOR_RANDOM = "bc78e213-6818-464d-8d5f-3408e9bd2413";
		const string REWARD_ID_COLOR_SINGLE = "ad70a9d9-12fa-44dd-a3fb-dde21ebc4ec1";
		const string REWARD_ID_COLOR_TRIPLE = "30168c08-09f6-4557-ab49-b7eaed212978";
		
		internal async static Task ProcessAdd(JsonElement evt) {
			string rewardId = evt.GetProperty("reward").GetProperty("id").GetString() ?? "";

			string redemptionId = evt.GetProperty("id").GetString() ?? "";
			string userId = evt.GetProperty("user_id").GetString() ?? "";
			string userLogin = evt.GetProperty("user_login").GetString() ?? "";
			string userInput = evt.GetProperty("user_input").GetString() ?? "";

			bool success = false;

			switch (rewardId) {
				case REWARD_ID_TOILET_FLUSH:
					Sound.PlaySound(Sound.Sounds.Flush);
					await Database.InsertFlushAsync(redemptionId, userId, userLogin);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully wrote flush for {userLogin}: {redemptionId}");
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
					await CloseColorRedemption(rewardId, redemptionId, true);
					break;
				case REWARD_ID_COLOR_SINGLE:
					success = LayoutColoring.TryChangeToSingle(userInput);
					await CloseColorRedemption(rewardId, redemptionId, success);
					break;
				case REWARD_ID_COLOR_TRIPLE:
					success = LayoutColoring.TryChangeToTriple(userInput);
					await CloseColorRedemption(rewardId, redemptionId, success);
					break;
				default:
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Unhandled channel point reward redemption id: {rewardId}");
					ConsoleLogger.LogToFile(evt);
					break;
			}
		}

		/// <summary>
		/// Marks a colour redemption fulfilled, or refunds it when the colour wasn't found.
		/// </summary>
		static async Task CloseColorRedemption(string rewardId, string redemptionId, bool success) {
			try {
				await TwitchHelixApi.UpdateRedemption(rewardId, redemptionId, success ? "FULFILLED" : "CANCELED");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error CP1");
				ConsoleLogger.LogToFile(ex);
			}
		}
	}
}
