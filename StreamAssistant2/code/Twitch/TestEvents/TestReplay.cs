using System.Text.Json;
using System.Text.Json.Nodes;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2 {
	/// <summary>
	/// Replays a real EventSub event saved in AssistantLogs\EventSubs. A gift bomb brings its recipients'
	/// sub_gift files along, in their original order, under a fresh id so replays don't collide.
	/// </summary>
	internal static class TestReplay {
		const int MAX_LISTED = 5;

		internal static readonly TestScript Script = new(
			"replay",
			[new TextParam("file")],
			[],
			Build);

		static List<TestEvent> Build(Values v) {
			List<TestEvent> events = Load(ConsoleLogger.EventSubDirectory, v.Text("file"), out string message);
			ConsoleLogger.ColoredLine(events.Count == 0 ? ConsoleLogger.ColorType.EventSubConfusion : ConsoleLogger.ColorType.EventSubNotification, message);
			return events;
		}

		/// <summary>
		/// The file is matched by its name, with or without ".log", or by a prefix that only one file has.
		/// </summary>
		internal static List<TestEvent> Load(string directory, string query, out string message) {
			if (!Directory.Exists(directory)) {
				message = $"No replay folder at {directory}";
				return [];
			}
			string[] names = Directory.GetFiles(directory, "*.log").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();

			string? name = Find(names, query, out message);
			if (name == null) {
				return [];
			}

			List<(string Name, JsonNode Node)> events = Read(directory, name).Select(n => (name, n)).ToList();
			JsonNode? bomb = events.Select(e => e.Node).FirstOrDefault(n => n["notice_type"]?.GetValue<string>() == "community_sub_gift");
			if (bomb != null && GiftId(bomb) is string bombId) {
				foreach (string other in names.Where(n => n != name && n.StartsWith("sub_gift_", StringComparison.Ordinal))) {
					events.AddRange(Read(directory, other).Where(n => GiftId(n) == bombId).Select(n => (other, n)));
				}
				events = events.OrderBy(e => Stamp(e.Name), StringComparer.Ordinal).ToList();
			}

			Dictionary<string, string> freshIds = new();
			foreach ((_, JsonNode node) in events) {
				if (GiftId(node) is string id) {
					if (!freshIds.TryGetValue(id, out string? fresh)) {
						fresh = $"replay-{Guid.NewGuid():N}";
						freshIds[id] = fresh;
					}
					SetGiftId(node, fresh);
				}
			}

			int fileCount = events.Select(e => e.Name).Distinct().Count();
			message = $"Replaying {name}: {events.Count} event(s) from {fileCount} file(s)";
			return events.Select(e => new TestEvent(TypeOf(e.Name), JsonSerializer.SerializeToElement(e.Node))).ToList();
		}

		/// <summary>
		/// The EventSub type a dump file holds: "channel.cheer_&lt;stamp&gt;.log" names it outright, while a name
		/// without a dot ("resub_&lt;stamp&gt;.log") is a chat notification's notice_type.
		/// </summary>
		internal static string TypeOf(string name) {
			string bare = Path.GetFileNameWithoutExtension(name);
			int underscore = bare.LastIndexOf('_');
			string prefix = underscore < 0 ? bare : bare[..underscore];
			return prefix.Contains('.') ? prefix : "channel.chat.notification";
		}

		internal static string? Find(string[] names, string query, out string message) {
			string wanted = query.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ? query : query + ".log";
			string? exact = names.FirstOrDefault(n => n.Equals(wanted, StringComparison.OrdinalIgnoreCase));
			if (exact != null) {
				message = "";
				return exact;
			}

			string[] matches = names.Where(n => n.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray();
			if (matches.Length == 1) {
				message = "";
				return matches[0];
			}
			if (matches.Length == 0) {
				string scripts = string.Join(", ", TestEventRunner.Scripts.Select(s => s.Name));
				message = $"No test script or replay file matches \"{query}\". Scripts: {scripts}";
				return null;
			}
			string listed = string.Join(", ", matches.Take(MAX_LISTED));
			message = $"{matches.Length} replay files match \"{query}\": {listed}{(matches.Length > MAX_LISTED ? ", …" : "")}";
			return null;
		}

		/// <summary>
		/// The "yyyy-MM-dd HH-mm-ss.fff" ending of "&lt;notice_type&gt;_&lt;stamp&gt;.log", which sorts by time.
		/// </summary>
		static string Stamp(string name) {
			string bare = Path.GetFileNameWithoutExtension(name);
			int underscore = bare.LastIndexOf('_');
			return underscore < 0 ? bare : bare[(underscore + 1)..];
		}

		static List<JsonNode> Read(string directory, string name) {
			return SplitValues(File.ReadAllText(Path.Combine(directory, name))).Select(v => JsonNode.Parse(v)!).ToList();
		}

		/// <summary>
		/// Splits concatenated top-level JSON objects. Two notifications in the same millisecond share a
		/// file name, so the second is appended straight after the first.
		/// </summary>
		internal static List<string> SplitValues(string text) {
			List<string> values = new();
			int depth = 0;
			int start = -1;
			bool inString = false;
			bool escaped = false;
			for (int i = 0; i < text.Length; i++) {
				char c = text[i];
				if (inString) {
					if (escaped) {
						escaped = false;
					}
					else if (c == '\\') {
						escaped = true;
					}
					else if (c == '"') {
						inString = false;
					}
					continue;
				}
				switch (c) {
					case '"':
						inString = true;
						break;
					case '{':
					case '[':
						if (depth == 0) {
							start = i;
						}
						depth++;
						break;
					case '}':
					case ']':
						depth--;
						if (depth == 0) {
							values.Add(text[start..(i + 1)]);
						}
						break;
				}
			}
			return values;
		}

		/// <summary>
		/// The bomb id a community_sub_gift carries, or a sub_gift that is part of a bomb; null otherwise.
		/// </summary>
		static string? GiftId(JsonNode node) {
			JsonNode? id = node["community_sub_gift"]?["id"] ?? node["sub_gift"]?["community_gift_id"];
			return id?.GetValueKind() == JsonValueKind.String ? id.GetValue<string>() : null;
		}

		static void SetGiftId(JsonNode node, string id) {
			if (node["community_sub_gift"] is JsonObject bomb) {
				bomb["id"] = id;
			}
			if (node["sub_gift"] is JsonObject gift) {
				gift["community_gift_id"] = id;
			}
		}
	}
}
