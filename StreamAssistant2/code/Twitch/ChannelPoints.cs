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
		
		/// <param name="isTest">From !test (a replay): leave the toilet database and the real redemptions alone.</param>
		internal async static Task ProcessAdd(JsonElement evt, bool isTest = false) {
			string rewardId = evt.ReadString("reward.id");

			string redemptionId = evt.ReadString("id");
			string userId = evt.ReadString("user_id");
			string userLogin = evt.ReadString("user_login");
			string userInput = evt.ReadString("user_input");

			bool success = false;

			switch (rewardId) {
				case REWARD_ID_TOILET_FLUSH:
					Sound.PlaySound(Sound.Sounds.Flush);
					if (isTest) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Test flush for {userLogin}: not written to the database ({redemptionId})");
						break;
					}
					await Database.InsertFlushAsync(redemptionId, userId, userLogin);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully wrote flush for {userLogin}: {redemptionId}");
					break;
				case REWARD_ID_TOILET_RETRIEVE:
					var flushes = await Database.RetrieveFlushesAsync(userId);
					if (isTest) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Test retrieve for {userLogin}: would return {flushes.Count} flush(es); database, Helix and chat left alone");
						break;
					}
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
					success = TryStartTrain();
					await CloseRedemption(rewardId, redemptionId, "Train request", "", success, isTest);
					break;
				case REWARD_ID_COLOR_RANDOM:
					LayoutColoring.ChangeToRandom();
					await CloseRedemption(rewardId, redemptionId, "Color change request", "random", true, isTest);
					break;
				case REWARD_ID_COLOR_SINGLE:
					success = LayoutColoring.TryChangeToSingle(userInput);
					await CloseRedemption(rewardId, redemptionId, "Color change request", userInput, success, isTest);
					break;
				case REWARD_ID_COLOR_TRIPLE:
					success = LayoutColoring.TryChangeToTriple(userInput);
					await CloseRedemption(rewardId, redemptionId, "Color change request", userInput, success, isTest);
					break;
				default:
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Unhandled channel point reward redemption id: {rewardId}");
					ConsoleLogger.LogToFile(evt);
					break;
			}
		}

		static int _trainOnTracks;

		/// <summary>
		/// Starts a train unless one is already showing, in which case it says so in chat and returns false.
		/// </summary>
		internal static bool TryStartTrain() {
			if (Interlocked.CompareExchange(ref _trainOnTracks, 1, 0) != 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, "Train ignored: one is already showing");
				TwitchIRCManager.SendMessage("🚂 A train is already on the tracks 🚂");
				return false;
			}
			FireForget.Run("TRN1", "train", RunTrainAsync);
			return true;
		}

		/// <summary>
		/// Shows a random train image on stream for 62 seconds, off the caller's thread, then frees the tracks.
		/// </summary>
		static async Task RunTrainAsync() {
			try {
				await Task.Yield();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, "Choo choo!");
				int r = Random.Shared.Next(0, 100);
				Obs.SetImageSource("Image: Train", Path.Combine(Config.Data.Directories.Trains, $"Train{r}.png"));
				Obs.SetSourceEnabled("!Scene: Basics Colored", "Image: Train", true);
				await Task.Delay(62000);
				Obs.SetSourceEnabled("!Scene: Basics Colored", "Image: Train", false);
				Obs.SetImageSource("Image: Train", Path.Combine(Config.Data.Directories.Trains, "Empty.png"));
			}
			finally {
				_trainOnTracks = 0;
			}
		}

		/// <summary>
		/// Marks a redemption fulfilled, or refunds it when it couldn't be carried out, and logs which. Only logs for a test.
		/// </summary>
		static async Task CloseRedemption(string rewardId, string redemptionId, string request, string detail, bool success, bool isTest) {
			string status = success ? "FULFILLED" : "CANCELED";
			if (isTest) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Test redemption: would mark {redemptionId} {status}; Helix not called");
				return;
			}
			try {
				await TwitchHelixApi.UpdateRedemption(rewardId, redemptionId, status);
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, RedemptionLine(request, detail, success));
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error CP1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// "&lt;request&gt; fulfilled: &lt;detail&gt;" or "… refunded", without the colon when there's no detail.
		/// </summary>
		internal static string RedemptionLine(string request, string detail, bool success) {
			string outcome = success ? "fulfilled" : "refunded";
			return detail == "" ? $"{request} {outcome}" : $"{request} {outcome}: {detail}";
		}
	}
}
