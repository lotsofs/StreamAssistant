using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Subscriptions {

		static Queue<string> _giftees = new();
		static Dictionary<string, CommunityGiftSub> _giftBombs = new();
		static Dictionary<string, TaskCompletionSource> _giftBombWaiters = new();
		static object _giftBombsLock = new();

		// How long bomb recipients wait for their bomb before chat is told it never came
		internal static TimeSpan GiftlessWait = TimeSpan.FromSeconds(30);

		internal static async Task HandleSubNotif(JsonElement n) {
			// bool chatter_is_anonymous = n.GetProperty("chatter_is_anonymous").GetBoolean();
			// string chatter_user_login = n.GetProperty("chatter_user_login").GetString() ?? (chatter_is_anonymous ? "Anonymous" : "Unknown User");
			string chatter_user_login = n.ReadString("chatter_user_login", "Unknown User");
			int sub_tier = n.ReadInt("sub.sub_tier") / 1000;
			bool is_prime = n.ReadBool("sub.is_prime");
			int duration_months = n.ReadInt("sub.duration_months");

			string msg = SubscriptionMessages.BuildSubMessage(chatter_user_login, sub_tier, is_prime, duration_months);

			Sound.PlaySound(Sound.Sounds.TribalHymn);
			await Task.Delay(4200);
			TextToSpeech.EnqueueSpeech(msg);
		}

		internal static async Task HandleResubNotif(JsonElement n) {
			string chatter_user_login = n.ReadString("chatter_user_login", "Unknown User");
			int sub_tier = n.ReadInt("resub.sub_tier") / 1000;
			bool is_prime = n.ReadBool("resub.is_prime");
			int cumulative_months = n.ReadInt("resub.cumulative_months");
			int duration_months = n.ReadInt("resub.duration_months");
			int streak_months = n.ReadInt("resub.streak_months");
			bool is_gift = n.ReadBool("resub.is_gift");
			string text = LanguageFilter.ReplaceBadWords(n.ReadString("message.text"));

			if (is_gift) {
				TwitchIRCManager.SendMessage("🎁");
			}

			// * * Some bug that causes the streak to be written in duration? * *
			//
			// if (duration_months > 1) {
			// 	msg += $", It is a {duration_months} month sub";
			// }
			string msg = SubscriptionMessages.BuildResubMessage(chatter_user_login, sub_tier, is_prime, cumulative_months, streak_months, text);

			Sound.PlaySound(Sound.Sounds.TribalHymn);
			await Task.Delay(4200);
			TextToSpeech.EnqueueSpeech(msg);
		}

		internal static async Task HandleSubGiftNotif(JsonElement n) {
			string community_gift_id = n.ReadString("sub_gift.community_gift_id");
			string recipient_user_login = n.ReadString("sub_gift.recipient_user_login", "Unknown User");
			if (!string.IsNullOrEmpty(community_gift_id)) {
				// Gift sub from bomb. Could happen before or after the bomb notif itself.
				var bomb = GetOrCreateBomb(community_gift_id, "Bomb created with id ", out bool created);
				if (bomb.Announced) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{recipient_user_login} arrived after gift bomb {community_gift_id} was announced");
					return;
				}
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, $"{recipient_user_login} added to {community_gift_id}");
				bomb.AddRecipient(recipient_user_login);
				if (created) {
					FireForget.Run("SUB2", "giftless check", () => ReportIfGiftlessAsync(bomb));
				}
				return;
			}

			// Targeted gift sub
			bool chatter_is_anonymous = n.ReadBool("chatter_is_anonymous");
			string chatter_user_login = n.ReadString("chatter_user_login", chatter_is_anonymous ? "Anonymous" : "Unknown User");
			int sub_tier = n.ReadInt("sub_gift.sub_tier") / 1000;
			int cumulative_total = n.ReadInt("sub_gift.cumulative_total");
			int duration_months = n.ReadInt("sub_gift.duration_months");

			string msg = SubscriptionMessages.BuildGiftMessage(chatter_user_login, sub_tier, recipient_user_login, cumulative_total, duration_months);

			Sound.PlaySound(Sound.Sounds.TheClap);
			await Task.Delay(1000);
			Sound.PlaySound(Sound.Sounds.TribalHymn);
			await Task.Delay(4200);
			TextToSpeech.EnqueueSpeech(msg);
		}

		internal static async Task HandleCommunitySubGiftNotif(JsonElement n) {
			try {
				n = n.Clone(); // Clone element so it doesn't get disposed
				string id = n.ReadString("community_sub_gift.id");
				if (string.IsNullOrEmpty(id)) {
					TwitchIRCManager.SendMessage("Something went wrong with the commie gift sub");
					return;
				}
				int total = n.ReadInt("community_sub_gift.total");
				CommunityGiftSub bomb;
				lock (_giftBombsLock) {
					bomb = GetOrCreateBomb(id, "Bomb created FROM MAIN EVENT with id ", out _);
					bomb.SetExpected(total);
				}

				// Get this data before the await or the JsonElement will be disposed in the meantime.
				// A bit redundant since we're already cloning n anyway.
				int sub_tier = n.ReadInt("community_sub_gift.sub_tier") / 1000;
				int cumulative_total = n.ReadInt("community_sub_gift.cumulative_total");
				bool chatter_is_anonymous = n.ReadBool("chatter_is_anonymous");
				string chatter_user_login = n.ReadString("chatter_user_login", chatter_is_anonymous ? "Anonymous" : "Unknown User");
				
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, $"Before await");
				var recipients = await bomb.WaitForRecipientsAsync();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, "After await");

				bomb.MarkAnnounced();
				if (recipients.Count < total) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Gift bomb {id}: {recipients.Count} of {total} recipients arrived in time");
				}
				string msg = SubscriptionMessages.BuildBombMessage(chatter_user_login, total, sub_tier, cumulative_total, recipients);
				for (int i = 0; i < total; i++) {
					Sound.PlaySound(Sound.Sounds.TribalHymn);
					await Task.Delay(66);
				}
				await Task.Delay(3800);
				Sound.PlaySound(Sound.Sounds.TheClap);
				await Task.Delay(3000);
				TextToSpeech.EnqueueSpeech(msg);
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error SUB1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		// After GiftlessWait, if the bomb event never came: drops the entry, tells chat and logs who got a sub
		internal static async Task ReportIfGiftlessAsync(CommunityGiftSub bomb) {
			await Task.Delay(GiftlessWait);
			lock (_giftBombsLock) {
				if (bomb.HasExpected) {
					return;
				}
				if (_giftBombs.TryGetValue(bomb.Id, out var current) && current == bomb) {
					_giftBombs.Remove(bomb.Id);
				}
			}
			var recipients = bomb.RecipientsSoFar();
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Gift bomb {bomb.Id} never arrived; recipients: {string.Join(", ", recipients)}");
			TwitchIRCManager.SendMessage(SubscriptionMessages.BuildGiftlessMessage(recipients));
		}

		// The bomb for an id, created (and logged with createdMessage) if new
		static CommunityGiftSub GetOrCreateBomb(string id, string createdMessage, out bool created) {
			lock (_giftBombsLock) {
				created = false;
				if (_giftBombs.TryGetValue(id, out var bomb)) {
					return bomb;
				}
				created = true;
				DateTime now = DateTime.UtcNow;
				SweepOldBombs(_giftBombs, now);
				bomb = new CommunityGiftSub(id, now);
				_giftBombs[id] = bomb;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, createdMessage + id);
				return bomb;
			}
		}

		// Removes bombs created over an hour before nowUtc
		internal static void SweepOldBombs(Dictionary<string, CommunityGiftSub> bombs, DateTime nowUtc) {
			foreach (var old in bombs.Where(kv => nowUtc - kv.Value.CreatedUtc > TimeSpan.FromHours(1)).Select(kv => kv.Key).ToList()) {
				bombs.Remove(old);
			}
		}

	}
}
