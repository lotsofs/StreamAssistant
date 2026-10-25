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
		internal enum Reward { None, Train, ToiletFlush, ToiletRetrieve, ColorRandom, ColorSingle, ColorTriple }

		/// <summary>
		/// The reward a redemption's reward id is, by the ids in the settings; None for any other.
		/// </summary>
		internal static Reward RewardOf(string rewardId, RewardsSettings rewards) {
			return rewardId switch {
				_ when rewardId == rewards.Train => Reward.Train,
				_ when rewardId == rewards.ToiletFlush => Reward.ToiletFlush,
				_ when rewardId == rewards.ToiletRetrieve => Reward.ToiletRetrieve,
				_ when rewardId == rewards.ColorRandom => Reward.ColorRandom,
				_ when rewardId == rewards.ColorSingle => Reward.ColorSingle,
				_ when rewardId == rewards.ColorTriple => Reward.ColorTriple,
				_ => Reward.None,
			};
		}

		/// <param name="isTest">From !test (a replay): leave the toilet database and the real redemptions alone.</param>
		internal async static Task ProcessAdd(JsonElement evt, bool isTest = false) {
			string rewardId = evt.ReadString("reward.id");

			string redemptionId = evt.ReadString("id");
			string userId = evt.ReadString("user_id");
			string userLogin = evt.ReadString("user_login");
			string userInput = evt.ReadString("user_input");

			bool success = false;

			RewardsSettings rewards = Settings.Data.Rewards;
			switch (RewardOf(rewardId, rewards)) {
				case Reward.ToiletFlush:
					Sound.PlaySound(Sound.Sounds.Flush);
					if (isTest) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Test flush for {userLogin}: not written to the database ({redemptionId})");
						break;
					}
					await Database.InsertFlushAsync(redemptionId, userId, userLogin);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully wrote flush for {userLogin}: {redemptionId}");
					break;
				case Reward.ToiletRetrieve:
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
						await TwitchHelixApi.UpdateRedemption(rewards.ToiletFlush, id, "CANCELED");
					}
					await Database.DeleteFlushesAsync(userId);
					TwitchIRCManager.SendMessage($"🪠 Found and returned {flushes.Count} of {userLogin}'s flushed channel points 🪠");
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Succesfully cleared sewers for {userLogin}: {flushes.Count}");
					break;
				case Reward.Train:
					success = TryStartTrain();
					await CloseRedemption(rewardId, redemptionId, "Train request", "", success, isTest);
					break;
				case Reward.ColorRandom:
					LayoutColoring.ChangeToRandom();
					await CloseRedemption(rewardId, redemptionId, "Color change request", "random", true, isTest);
					break;
				case Reward.ColorSingle:
					success = LayoutColoring.TryChangeToSingle(userInput);
					await CloseRedemption(rewardId, redemptionId, "Color change request", userInput, success, isTest);
					break;
				case Reward.ColorTriple:
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
		/// Shows a random train image on stream for the train's seconds, off the caller's thread, then frees the tracks.
		/// </summary>
		static async Task RunTrainAsync() {
			try {
				await Task.Yield();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, "Choo choo!");
				TrainSettings train = Settings.Data.Train;
				int r = Random.Shared.Next(0, train.Images);
				Obs.SetImageSource(train.Source, Path.Combine(Settings.Data.Directories.Trains, $"Train{r}.png"));
				Obs.SetSourceEnabled(train.Scene, train.Source, true);
				await Task.Delay(TimeSpan.FromSeconds(train.Seconds));
				ClearTrain();
			}
			finally {
				_trainOnTracks = 0;
			}
		}

		/// <summary>
		/// Hides the train and points it back at the empty image.
		/// </summary>
		static void ClearTrain() {
			TrainSettings train = Settings.Data.Train;
			Obs.SetSourceEnabled(train.Scene, train.Source, false);
			Obs.SetImageSource(train.Source, Path.Combine(Settings.Data.Directories.Trains, "Empty.png"));
		}

		/// <summary>
		/// Off OBS's event thread, hides a train OBS shows while none is running, holding the tracks meanwhile.
		/// </summary>
		internal static void OnObsConnected() {
			FireForget.Run("TRN2", "train clear on OBS connect", async () => {
				await Task.Yield();
				if (Interlocked.CompareExchange(ref _trainOnTracks, 1, 0) != 0) {
					return;
				}
				try {
					TrainSettings train = Settings.Data.Train;
					if (Obs.SceneItems(train.Scene, false)?.Any(item => item.Source == train.Source && item.Enabled) == true) {
						ClearTrain();
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, "Leftover train cleared");
					}
				}
				finally {
					_trainOnTracks = 0;
				}
			});
		}

		/// <summary>
		/// Hides a train still running at shutdown; an OBS failure is logged, not thrown.
		/// </summary>
		internal static void OnShutdown() {
			if (_trainOnTracks == 0) {
				return;
			}
			try {
				ClearTrain();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TRN3");
				ConsoleLogger.LogToFile(ex);
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
