using System.Text.Json;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestStream {
		internal static readonly TestScript Online = new("online", [], [], BuildOnline);

		internal static readonly TestScript Offline = new("offline", [], [], BuildOffline);

		// One stream.online event from the broadcaster, started now
		internal static List<TestEvent> BuildOnline(Values v) {
			string broadcaster = Config.Data.TwitchAuth.Login;
			JsonElement evt = JsonSerializer.SerializeToElement(new {
				broadcaster_user_login = broadcaster,
				broadcaster_user_name = broadcaster,
				type = "live",
				started_at = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),
			});
			return [new TestEvent("stream.online", evt)];
		}

		// One stream.offline event from the broadcaster
		internal static List<TestEvent> BuildOffline(Values v) {
			string broadcaster = Config.Data.TwitchAuth.Login;
			JsonElement evt = JsonSerializer.SerializeToElement(new {
				broadcaster_user_login = broadcaster,
				broadcaster_user_name = broadcaster,
			});
			return [new TestEvent("stream.offline", evt)];
		}
	}
}
