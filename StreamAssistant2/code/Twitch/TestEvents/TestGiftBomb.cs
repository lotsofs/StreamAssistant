using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestGiftBomb {
		internal static readonly TestScript Script = new(
			"bomb",
			[new IntParam("gifts", 1, 1, 100)],
			[
				new BoolParam("late"),
				new IntParam("missing", 0, 0, 100),
				new BoolParam("anon"),
				new IntParam("tier", 1, 1, 3),
			],
			Build);

		/// <summary>
		/// One community_sub_gift plus a sub_gift per recipient, sharing a fresh id. "late" sends the
		/// recipients first; "missing" withholds that many so the announcement waits out its timeout.
		/// </summary>
		internal static List<TestEvent> Build(Values v) {
			int total = v.Int("gifts");
			int sent = total - Math.Min(v.Int("missing"), total);
			bool anonymous = v.Bool("anon");
			int tier = v.Int("tier");
			// Real anonymous bombs carry no login and no channel total.
			string? gifter = anonymous ? null : "testgifter";
			int? cumulativeTotal = anonymous ? null : total + 10;
			string shownGifter = gifter ?? "An anonymous gifter";
			string subTier = TestEventRunner.SubTier(tier);
			string bombId = "test-" + Guid.NewGuid().ToString("N");

			TestEvent bomb = TestEventRunner.ChatNotification(new {
				notice_type = "community_sub_gift",
				system_message = $"{shownGifter} is gifting {total} Tier {tier} Subs to the community!",
				message = new { text = "" },
				chatter_user_login = gifter,
				chatter_is_anonymous = anonymous,
				community_sub_gift = new {
					id = bombId,
					total,
					cumulative_total = cumulativeTotal,
					sub_tier = subTier,
				},
			});

			List<TestEvent> events = new();
			for (int i = 1; i <= sent; i++) {
				string recipient = $"testgiftee{i}";
				events.Add(TestEventRunner.ChatNotification(new {
					notice_type = "sub_gift",
					system_message = $"{shownGifter} gifted a Tier {tier} sub to {recipient}!",
					message = new { text = "" },
					chatter_user_login = gifter,
					chatter_is_anonymous = anonymous,
					sub_gift = new {
						duration_months = 1,
						cumulative_total = anonymous ? null : (int?)0,
						recipient_user_login = recipient,
						sub_tier = subTier,
						community_gift_id = bombId,
					},
				}));
			}

			if (v.Bool("late")) {
				events.Add(bomb);
			}
			else {
				events.Insert(0, bomb);
			}
			return events;
		}
	}
}
