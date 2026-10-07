using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestSubs {
		const string SUBBER = "testsubber";
		const string GIFTER = "testgifter";
		const string GIFTEE = "testgiftee";

		internal static readonly TestScript Sub = new(
			"sub",
			[],
			[
				new IntParam("tier", 1, 1, 3),
				new BoolParam("prime"),
				new IntParam("months", 1, 1, 12),
			],
			BuildSub);

		internal static readonly TestScript Resub = new(
			"resub",
			[],
			[
				new IntParam("months", 12, 1, 1000),
				new IntParam("streak", 3, 0, 1000),
				new IntParam("tier", 1, 1, 3),
				new BoolParam("prime"),
				new BoolParam("gift"),
				new TextParam("msg"),
			],
			BuildResub);

		internal static readonly TestScript Gift = new(
			"gift",
			[],
			[
				new IntParam("tier", 1, 1, 3),
				new IntParam("total", 1, 0, 10000),
				new IntParam("months", 1, 1, 12),
				new BoolParam("anon"),
			],
			BuildGift);

		internal static List<TestEvent> BuildSub(Values v) {
			return [TestEventRunner.ChatNotification(new {
				notice_type = "sub",
				system_message = $"{SUBBER} subscribed at Tier {v.Int("tier")}.",
				message = new { text = "" },
				chatter_user_login = SUBBER,
				chatter_is_anonymous = false,
				sub = new {
					sub_tier = TestEventRunner.SubTier(v.Int("tier")),
					is_prime = v.Bool("prime"),
					duration_months = v.Int("months"),
				},
			})];
		}

		internal static List<TestEvent> BuildResub(Values v) {
			return [TestEventRunner.ChatNotification(new {
				notice_type = "resub",
				system_message = $"{SUBBER} subscribed at Tier {v.Int("tier")}. They've subscribed for {v.Int("months")} months!",
				message = new { text = v.Text("msg") },
				chatter_user_login = SUBBER,
				chatter_is_anonymous = false,
				resub = new {
					cumulative_months = v.Int("months"),
					duration_months = 1,
					streak_months = v.Int("streak"),
					sub_tier = TestEventRunner.SubTier(v.Int("tier")),
					is_prime = v.Bool("prime"),
					is_gift = v.Bool("gift"),
				},
			})];
		}

		internal static List<TestEvent> BuildGift(Values v) {
			bool anonymous = v.Bool("anon");
			return [TestEventRunner.ChatNotification(new {
				notice_type = "sub_gift",
				system_message = $"{(anonymous ? "An anonymous gifter" : GIFTER)} gifted a Tier {v.Int("tier")} sub to {GIFTEE}!",
				message = new { text = "" },
				chatter_user_login = anonymous ? null : GIFTER,
				chatter_is_anonymous = anonymous,
				sub_gift = new {
					duration_months = v.Int("months"),
					cumulative_total = anonymous ? null : (int?)v.Int("total"),
					recipient_user_login = GIFTEE,
					sub_tier = TestEventRunner.SubTier(v.Int("tier")),
					community_gift_id = (string?)null,
				},
			})];
		}
	}
}
