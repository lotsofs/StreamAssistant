using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Subscriptions {

		static Queue<string> _giftees = new();
		static Dictionary<string, CommunityGiftSub> _giftBombs = new();
		static Dictionary<string, TaskCompletionSource> _giftBombWaiters = new();
		static object _giftBombsLock = new();

		internal static async Task HandleSubNotif(JsonElement n) {
			// bool chatter_is_anonymous = n.GetProperty("chatter_is_anonymous").GetBoolean();
			// string chatter_user_login = n.GetProperty("chatter_user_login").GetString() ?? (chatter_is_anonymous ? "Anonymous" : "Unknown User");
			string chatter_user_login = n.ReadString("chatter_user_login", "Unknown User");
			int sub_tier = n.ReadInt("sub.sub_tier") / 1000;
			bool is_prime = n.ReadBool("sub.is_prime");
			int duration_months = n.ReadInt("sub.duration_months");

			string msg = $"{chatter_user_login} subscribed";
			if (is_prime) {
				msg += $" with prime";
			}
			else if (sub_tier > 1) {
				msg += $" at tier {sub_tier}";
			}
			if (duration_months > 1) {
				msg += $"It is a {duration_months} month sub. ";
			}

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

			string msg = $"{chatter_user_login} subscribed";
			if (is_prime) {
				msg += $" with prime";
			}
			else if (sub_tier > 1) {
				msg += $" at tier {sub_tier}";
			}
			if (cumulative_months > 1) {
				msg += $", They've subscribed for {cumulative_months} months";
			}
			if (streak_months > 1) {
				msg += $", Currently on a {streak_months} month streak";
			}
			// * * Some bug that causes the streak to be written in duration? * *
			//
			// if (duration_months > 1) {
			// 	msg += $", It is a {duration_months} month sub";
			// }
			if (!string.IsNullOrEmpty(text)) {
				msg += $": {text}";
			}

			Sound.PlaySound(Sound.Sounds.TribalHymn);
			await Task.Delay(4200);
			TextToSpeech.EnqueueSpeech(msg);
		}

		internal static async Task HandleSubGiftNotif(JsonElement n) {
			string community_gift_id = n.ReadString("sub_gift.community_gift_id");
			string recipient_user_login = n.ReadString("sub_gift.recipient_user_login", "Unknown User");
			if (!string.IsNullOrEmpty(community_gift_id)) {
				// Gift sub from bomb. Could happen before or after the bomb notif itself.
				if (!_giftBombs.TryGetValue(community_gift_id, out var bomb)) {
					bomb = new CommunityGiftSub(community_gift_id);
					_giftBombs[community_gift_id] = bomb;
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, "Bomb created with id " + community_gift_id);
				}
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, $"{recipient_user_login} added to {community_gift_id}");
				bomb.AddRecipient(recipient_user_login);
				TwitchIRCManager.SendMessage("💣");
				return;
			}

			// Targeted gift sub
			bool chatter_is_anonymous = n.ReadBool("chatter_is_anonymous");
			string chatter_user_login = n.ReadString("chatter_user_login", chatter_is_anonymous ? "Anonymous" : "Unknown User");
			int sub_tier = n.ReadInt("sub_gift.sub_tier") / 1000;
			int cumulative_total = n.ReadInt("sub_gift.cumulative_total");
			int duration_months = n.ReadInt("sub_gift.duration_months");
					
			string msg = $"{chatter_user_login} gifted a tier {sub_tier} sub to {recipient_user_login}";
			if (cumulative_total > 1) {
				msg += $". They have given {cumulative_total} gift subs in the channel";
			}
			else if (cumulative_total == 1) {
				msg += ". This is their first gift sub in the channel";
			}
			if (duration_months > 1) {
				msg += $", It is a {duration_months} month gift";
			}

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
				if (!_giftBombs.TryGetValue(id, out var bomb)) {
					bomb = new CommunityGiftSub(id);
					_giftBombs[id] = bomb;
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, "Bomb created FROM MAIN EVENT with id " + id);
				}
				bomb.SetExpected(total);

				// Get this data before the await or the JsonElement will be disposed in the meantime.
				// A bit redundant since we're already cloning n anyway.
				int sub_tier = n.ReadInt("community_sub_gift.sub_tier") / 1000;
				int cumulative_total = n.ReadInt("community_sub_gift.cumulative_total");
				bool chatter_is_anonymous = n.ReadBool("chatter_is_anonymous");
				string chatter_user_login = n.ReadString("chatter_user_login", chatter_is_anonymous ? "Anonymous" : "Unknown User");
				
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, $"Before await");
				var recipients = await bomb.WaitForRecipientsAsync();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.None, "After await");

				string tier = sub_tier > 1 ? "" : $"tier {sub_tier} ";
				string msg = $"{chatter_user_login} is gifting {total} {tier} subs to Lots Of Ess's community! ";
				if (cumulative_total > total) {
					msg += $"They've gifted a total of {cumulative_total} in the channel! ";
				}
				msg += "Congratulations to: ";
				foreach (string name in recipients) {
					msg += $"{name}, ";
				}
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
		
		// private const string MSG_TEST_PREFIX = "TEST: ";
		// private const string MSG_SUB_FIRST_PRIME = "{0} subscribed with Prime. ";
		// private const string MSG_SUB_FIRST_TIERED = "{0} subscribed at {1}. ";
		// private const string MSG_SUB_STREAK = "Currently on a {0} month streak. ";
		// private const string MSG_SUB_CUMULATIVE = "They've subscribed for {0} months. ";
		// private const string MSG_GIFT_BASIC = "{0} gifted a {1} sub to {2}. ";
		// private const string MSG_GIFT_TOTAL = "They have given {0} Gift Subs in the channel. ";
		// private const string MSG_GIFT_FIRST = "This is their first Gift Sub in the channel. ";
		// private const string MSG_GIFT_MULTIMONTH = "It's a {0} month gift. ";
		// private const string MSG_BOMB_LONG = "{0} is gifting {1} {2} Subs to Lots Of Ess's community! They've gifted a total of {3} in the channel! Congratulations to: ";
		// private const string MSG_BOMB_SHORT = "{0} is gifting {1} {2} Subs to Lots Of Ess's community!";

		// private const string ERROR_NO_BOMBEES = "🛢️ An error occured processing batch gift sub from {0}. Expected {1} recipients, received {2}.";
		// private const string ERROR_NO_BOMBERS = "🛢️ An error occured processing batch gift subs. {0} recipients received a gift, but no such gift was given.";

		// private const float SUB_PAYOUT = 0.09f; // Cheapest sub (Turkey Twitch Prime). Anything extra I pocket for food.

		// private class GiftBomb {
		// 	internal long GiftCount;
		// 	internal string Tier;
		// 	internal long TotalGifts;
		// 	internal string Gifter;

		// 	internal GiftBomb(string gifter, long count, string tier, long total) {
		// 		Gifter = gifter;
		// 		GiftCount = count;
		// 		TotalGifts = total;
		// 		Tier = tier;
		// 	}
		// }
		
		// private static DateTime _lastWork = DateTime.MinValue;
		// private static Queue<string> _giftBombees = new Queue<string>();
		// private static Queue<GiftBomb> _giftBombs = new Queue<GiftBomb>();
		
		// // Generates basic sub message (X subscribed with tier Y)
		// // Excluding any resub additions (eg month streak, personal message)
		// private static string GenerateSubMessage(Dictionary<string, object> variables) {
		// 	string msg = string.Empty;
		// 	bool isTest = variables.ContainsKey("isTest") && (bool)variables["isTest"]; 
		// 	string user = variables["userName"].ToString() ?? "Somebody";
		// 	if (isTest) {
		// 		user = "Test User";
		// 	}
		// 	string tier = variables["tier"].ToString() ?? "tier 0";
		// 	if (tier == "prime") {
		// 		msg = string.Format(MSG_SUB_FIRST_PRIME, user);
		// 	}
		// 	else {
		// 		msg = string.Format(MSG_SUB_FIRST_TIERED, user, tier);
		// 	}
		// 	if (isTest) {
		// 		msg = MSG_TEST_PREFIX + msg;
		// 	}
		// 	return msg;
		// }

		// private static string GenerateResubMessage(Dictionary<string, object> variables) {
		// 	string msg = GenerateSubMessage(variables);
		// 	long monthStreak = (long)variables["monthStreak"];
		// 	long cumulative = (long)variables["cumulative"];
		// 	string message = variables["rawInput"].ToString() ?? "";
		// 	if (cumulative > 0) {
		// 		msg += string.Format(MSG_SUB_CUMULATIVE, cumulative);
		// 	}
		// 	if (monthStreak > 0) {
		// 		msg += string.Format(MSG_SUB_STREAK, monthStreak);
		// 	}
		// 	if (!string.IsNullOrEmpty(message)) {
		// 		msg += message;
		// 	}
		// 	return msg;
		// }

		// private static string GenerateGiftMessage(Dictionary<string, object> variables) {
		// 	string gifter = variables["userName"].ToString() ?? "Somebody";
		// 	string tier = variables["tier"].ToString() ?? "tier 0";
		// 	string recipientUser = variables["recipientUser"].ToString() ?? "Someone";
		// 	bool isTest = variables.ContainsKey("isTest") && (bool)variables["isTest"]; 
		// 	if (isTest) {
		// 		bool anonymous = (bool)variables["anonymous"];
		// 		gifter = anonymous ? "Anonymous" : "Test User";
		// 		recipientUser = "Test Victim";
		// 	}
		// 	string msg = string.Format(MSG_GIFT_BASIC, gifter, tier, recipientUser);
		// 	long totalSubsGifted = (long)variables["totalSubsGifted"];
		// 	if (totalSubsGifted > 1) {
		// 		msg += string.Format(MSG_GIFT_TOTAL, totalSubsGifted);
		// 	}
		// 	else {
		// 		msg += MSG_GIFT_FIRST;
		// 	}
		// 	long monthsGifted = (long)variables["monthsGifted"];
		// 	if (monthsGifted > 1) {
		// 		msg += string.Format(MSG_GIFT_MULTIMONTH, monthsGifted);
		// 	}
		// 	return msg;
		// }

		// private static void AddMoneyBasedOnTier(string tier) {
		// 	switch (tier) {
		// 		case "tier 1":
		// 			Money.Current += SUB_PAYOUT;
		// 			break;
		// 		case "tier 2":
		// 			Money.Current += SUB_PAYOUT * 2;
		// 			break;
		// 		case "tier 3":
		// 			Money.Current += SUB_PAYOUT * 5;
		// 			break;
		// 		case "prime":
		// 			Money.Current += SUB_PAYOUT;
		// 			break;
		// 		default:
		// 			break;
		// 	}
		// }

		// internal static string HandleTwitchSub(Dictionary<string, object> variables) {
		// 	Sound.PlaySound(Sound.Sounds.TribalHymn);
		// 	string msg = GenerateSubMessage(variables);
		// 	MsgQueue.TimedEnqueue(4100, MsgTypes.TextToS, msg);
		// 	//MsgQueue.Enqueue(MsgTypes.ChatMsg, msg);
		// 	return msg;
		// }

		// internal static string HandleTwitchResub(Dictionary<string, object> variables) {
		// 	Sound.PlaySound(Sound.Sounds.TribalHymn);
		// 	string msg = GenerateResubMessage(variables);
		// 	MsgQueue.TimedEnqueue(4100, MsgTypes.TextToS, msg);
		// 	//MsgQueue.Enqueue(MsgTypes.ChatMsg, msg);
		// 	return msg;
		// }

		// internal static string HandleGiftSub(Dictionary<string, object> variables) {
		// 	bool fromBomb = (bool)variables["fromGiftBomb"];
		// 	if (fromBomb) {
		// 		return "fromBomb";
		// 	}
			
		// 	Sound.PlaySound(Sound.Sounds.TheClap);
		// 	Sound.PlaySoundDelayed(Sound.Sounds.TribalHymn, 1000);
		// 	string msg = GenerateGiftMessage(variables);
		// 	MsgQueue.TimedEnqueue(1000 + 4100, MsgTypes.TextToS, msg);
		// 	//MsgQueue.Enqueue(MsgTypes.ChatMsg, msg);
		// 	return msg;
		// }

		// // When a gift bomb happens: Store it for processing later. Because gift bombs contain no recipient information
		// // We need to fetch each separately.
		// internal static void HandleGiftBomb(Dictionary<string, object> variables) {
		// 	string gifter = variables["userName"].ToString() ?? "somebody";
		// 	string tier = variables["tier"].ToString() ?? "tier 0";
		// 	long count = (long)variables["gifts"];
		// 	long total = (long)variables["totalGifts"];

		// 	bool isTest = variables.ContainsKey("isTest") && (bool)variables["isTest"];
		// 	if (isTest) {
		// 		gifter = "Test User";
		// 	}

		// 	string msg1 = string.Format(MSG_BOMB_SHORT, gifter, count, tier);
		// 	MsgQueue.Enqueue(MsgTypes.ChatMsg, msg1);
		// 	string msg2 = string.Format(MSG_BOMB_LONG, gifter, count, tier, total);
		// 	for (int i = 0; i < count; i++) {
		// 		Sound.PlaySound(Sound.Sounds.TribalHymn);
		// 		string giftee = "UnknownRecipient";
		// 		if (variables.ContainsKey("gift.recipientUserName" + i)) {
		// 			giftee = variables["gift.recipientUserName" + i].ToString() ?? "UnknownRecipient";
		// 		}
		// 		msg2 += giftee + ", ";
		// 	}
		// 	Sound.PlaySoundDelayed(Sound.Sounds.TheClap, 5000);
		// 	MsgQueue.TimedEnqueue(8000, MsgTypes.TextToS, msg2);
		// }
	}
}
