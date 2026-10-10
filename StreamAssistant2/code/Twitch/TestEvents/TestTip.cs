using System.Text.Json;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	internal static class TestTip {
		internal static readonly TestScript Script = new(
			"tip",
			[new DecimalParam("amount", 5m, 0.01m, 100000m)],
			[new TextParam("msg")],
			Build);

		// One StreamElements channel.tips event from testtipper, under a fresh id
		internal static List<TestEvent> Build(Values v) {
			JsonElement data = JsonSerializer.SerializeToElement(new {
				donation = new {
					user = new { username = "testtipper" },
					message = v.Text("msg"),
					amount = v.Decimal("amount"),
					currency = "EUR",
				},
				_id = $"test-{Guid.NewGuid():N}",
				provider = "test",
				approved = "allowed",
				status = "success",
			});
			return [new TestEvent(StreamElementsEventHandler.TYPE_PREFIX + StreamElementsSocket.TIPS_TOPIC, data)];
		}
	}
}
