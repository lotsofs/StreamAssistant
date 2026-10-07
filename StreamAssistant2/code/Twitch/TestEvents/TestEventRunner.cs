using System.Text.Json;

namespace StreamAssistant2 {
	internal record TestEvent(string Type, JsonElement Event);

	internal record TestScript(string Name, TestArgs.Param[] Required, TestArgs.Param[] Optional, Func<TestArgs.Values, List<TestEvent>> Build) {
		internal string Usage => TestArgs.Usage(Name, Required, Optional);
	}

	/// <summary>
	/// "!test &lt;script&gt; ...": builds simulated EventSub events in code and feeds them to the real handlers.
	/// </summary>
	internal static class TestEventRunner {
		const int DELAY_BETWEEN_MS = 50;

		internal static readonly TestScript[] Scripts = [
			TestSubs.Sub,
			TestSubs.Resub,
			TestSubs.Gift,
			TestGiftBomb.Script,
			TestCheer.Script,
			TestReplay.Script,
		];

		internal static void Run(string argument) {
			int space = argument.IndexOf(' ');
			string name = space < 0 ? argument : argument[..space];
			string rest = space < 0 ? "" : argument[(space + 1)..];

			TestScript? script = Scripts.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (script == null && argument.Length > 0) {
				// Not a script name, so treat the whole argument as a replay file.
				script = TestReplay.Script;
				rest = argument;
			}
			if (script == null) {
				string names = string.Join(", ", Scripts.Select(s => s.Name));
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Test scripts: {names}, or a file name to replay");
				return;
			}

			TestArgs.Values? values = TestArgs.Parse(rest, script.Required, script.Optional);
			if (values == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"Usage: {script.Usage}");
				return;
			}

			List<TestEvent> events = script.Build(values);
			if (events.Count == 0) {
				return;
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"Test {script.Name}: {values}");
			FireForget.Run("TEV1", $"test {script.Name}", () => SendAsync(events));
		}

		static async Task SendAsync(List<TestEvent> events) {
			foreach (TestEvent e in events) {
				TwitchEventHandler.Handle(e.Type, e.Event, isTest: true);
				await Task.Delay(DELAY_BETWEEN_MS);
			}
		}

		internal static TestEvent ChatNotification(object evt) {
			return new TestEvent("channel.chat.notification", JsonSerializer.SerializeToElement(evt));
		}

		internal static string SubTier(int tier) {
			return (tier * 1000).ToString();
		}
	}
}
