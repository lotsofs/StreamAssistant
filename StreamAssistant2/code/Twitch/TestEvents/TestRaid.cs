using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestRaid {
		// The raider's login and display name
		const string RAIDER_LOGIN = "testraider";
		const string RAIDER_NAME = "TestRaider";

		// !test raid [viewers]
		internal static readonly TestScript Script = new(
			"raid",
			[],
			[new IntParam("viewers", 10, 1, 100000)],
			Build);

		// One raid chat notification from TestRaider
		internal static List<TestEvent> Build(Values v) {
			return [TestEventRunner.ChatNotification(new {
				notice_type = "raid",
				system_message = $"{v.Int("viewers")} raiders from {RAIDER_NAME} have joined!",
				message = new { text = "" },
				chatter_user_login = RAIDER_LOGIN,
				chatter_user_name = RAIDER_NAME,
				chatter_is_anonymous = false,
				raid = new {
					user_name = RAIDER_NAME,
					user_login = RAIDER_LOGIN,
					viewer_count = v.Int("viewers"),
				},
			})];
		}
	}
}
