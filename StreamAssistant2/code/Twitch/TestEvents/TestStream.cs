using System.Text.Json;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestStream {
		const string BROADCASTER = "lotsofs";

		internal static readonly TestScript Online = new("online", [], [], BuildOnline);

		internal static readonly TestScript Offline = new("offline", [], [], BuildOffline);

		// One stream.online event, started now
		internal static List<TestEvent> BuildOnline(Values v) {
			JsonElement evt = JsonSerializer.SerializeToElement(new {
				broadcaster_user_login = BROADCASTER,
				broadcaster_user_name = BROADCASTER,
				type = "live",
				started_at = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),
			});
			return [new TestEvent("stream.online", evt)];
		}

		// One stream.offline event
		internal static List<TestEvent> BuildOffline(Values v) {
			JsonElement evt = JsonSerializer.SerializeToElement(new {
				broadcaster_user_login = BROADCASTER,
				broadcaster_user_name = BROADCASTER,
			});
			return [new TestEvent("stream.offline", evt)];
		}
	}
}
