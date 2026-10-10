using System.Text.Json;
using System.Text.Json.Nodes;

namespace StreamAssistant2 {
	// Dispatches StreamElements events by topic
	internal static class StreamElementsEventHandler {
		// Prefix that marks a StreamElements event in !test types and EventSubs file names
		internal const string TYPE_PREFIX = "se.";

		static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

		/// <param name="isTest">From a !test script: don't write the EventSubs dump, which holds real events only.</param>
		internal static void Handle(string topic, JsonElement data, bool isTest = false) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsNotification, $"SE notification: {topic}");
			try {
				string redacted = Redact(data).ToJsonString(_indented);
				if (!isTest) {
					ConsoleLogger.LogToEventSubFile(redacted, $"{TYPE_PREFIX}{topic}_{ConsoleLogger.TimeStamp(true)}.log");
				}
				switch (topic) {
					case StreamElementsSocket.TIPS_TOPIC:
						JsonElement tip = data.Clone();
						FireForget.Run("SEH_t", "tip", () => Tips.Process(tip));
						break;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements event happened, but is not handled in code: {topic}");
						break;
				}
				ConsoleLogger.LogToFile(redacted);
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error SEH1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		// The event without the tipper's email address
		internal static JsonNode Redact(JsonElement data) {
			if (data.ValueKind == JsonValueKind.Undefined) {
				return new JsonObject();
			}
			JsonNode node = JsonNode.Parse(data.GetRawText()) ?? new JsonObject();
			if (node is JsonObject root && root["donation"] is JsonObject donation && donation["user"] is JsonObject user) {
				user.Remove("email");
			}
			return node;
		}
	}
}
