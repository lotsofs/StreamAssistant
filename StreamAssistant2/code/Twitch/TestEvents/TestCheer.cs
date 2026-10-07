using System.Text.Json;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestCheer {
		internal static readonly TestScript Script = new(
			"cheer",
			[new IntParam("bits", 100, 1, 1000000)],
			[
				new BoolParam("anon"),
				new TextParam("msg"),
			],
			Build);

		internal static List<TestEvent> Build(Values v) {
			bool anonymous = v.Bool("anon");
			JsonElement evt = JsonSerializer.SerializeToElement(new {
				is_anonymous = anonymous,
				user_login = anonymous ? null : "testcheerer",
				bits = v.Int("bits"),
				message = v.Text("msg"),
			});
			return [new TestEvent("channel.cheer", evt)];
		}
	}
}
