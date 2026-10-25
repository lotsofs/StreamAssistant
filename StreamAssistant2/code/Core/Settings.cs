using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StreamAssistant2 {
	/// <summary>
	/// Loads settings.json from BotInput, at startup and on !reloaddata; a missing section or key keeps its default.
	/// </summary>
	internal static class Settings {
		const string SETTINGS_FILE = "settings.json";
		const string SETTINGS_TEMPLATE = "settings.json.example";

		static readonly JsonSerializerOptions _jsonOptions = new() {
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		};

		static volatile SettingsModel _data = new();

		/// <summary>
		/// The settings in use, replaced whole by each load; the defaults before the first.
		/// </summary>
		internal static SettingsModel Data {
			get => _data;
			set => _data = value;
		}

		/// <summary>
		/// Loads settings.json and logs which keys differ from the defaults and which the file leaves out. At startup a
		/// missing file is first copied from the settings.json.example template; on a reload it keeps the settings in use. A
		/// broken one throws at startup, and otherwise is logged and the settings in use kept. BotOutput keeps its startup value.
		/// </summary>
		internal static void Load(bool atStartup = false) {
			string path = Path.Combine(Config.Data.Directories.BotInput, SETTINGS_FILE);
			bool created = false;
			if (!File.Exists(path)) {
				if (!atStartup || !File.Exists(SETTINGS_TEMPLATE)) {
					string kept = atStartup ? "using the defaults" : "keeping the settings loaded";
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{SETTINGS_FILE} not found, {kept}");
					return;
				}
				File.Copy(SETTINGS_TEMPLATE, path);
				created = true;
			}
			string json;
			SettingsModel loaded;
			try {
				json = File.ReadAllText(path);
				loaded = Parse(json);
			}
			catch (Exception ex) {
				if (atStartup) {
					throw new InvalidDataException($"{path}: {ex.Message}", ex);
				}
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error SET1: {SETTINGS_FILE}: {ex.Message}");
				ConsoleLogger.LogToFile(ex);
				return;
			}
			if (!atStartup && loaded.Directories.BotOutput != Data.Directories.BotOutput) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "BotOutput changes after a restart");
				loaded.Directories.BotOutput = Data.Directories.BotOutput;
			}
			Data = loaded;
			if (created) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Created {path} from {SETTINGS_TEMPLATE}, every setting at its default");
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, LoadedLine(Changed(loaded)));
			List<string> missing = Missing(json);
			if (missing.Count > 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, MissingLine(missing));
			}
		}

		/// <summary>
		/// The dotted keys of the sections and values a valid settings JSON leaves out, in the model's order; a section left
		/// out whole is one key.
		/// </summary>
		internal static List<string> Missing(string json) {
			JsonNodeOptions nodeOptions = new() { PropertyNameCaseInsensitive = true };
			JsonDocumentOptions documentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
			List<string> missing = new();
			Absent(JsonSerializer.SerializeToNode(new SettingsModel()), JsonNode.Parse(json, nodeOptions, documentOptions), "", missing);
			return missing;
		}

		/// <summary>
		/// Adds the keys under path that the defaults have and the file doesn't.
		/// </summary>
		static void Absent(JsonNode? defaults, JsonNode? file, string path, List<string> missing) {
			if (defaults is not JsonObject section) {
				return;
			}
			foreach (var (key, value) in section) {
				string child = path.Length == 0 ? key : $"{path}.{key}";
				if (file is JsonObject inFile && inFile.TryGetPropertyValue(key, out JsonNode? found)) {
					Absent(value, found, child, missing);
				}
				else {
					missing.Add(child);
				}
			}
		}

		/// <summary>
		/// "settings.json lacks Train, Sounds.Flush.Volume; their defaults apply (settings.json.example has every key)".
		/// </summary>
		internal static string MissingLine(IReadOnlyList<string> missing) {
			return $"{SETTINGS_FILE} lacks {string.Join(", ", missing)}; their defaults apply ({SETTINGS_TEMPLATE} has every key)";
		}

		/// <summary>
		/// The settings in the JSON over the defaults, with the directories' environment variables expanded; throws for bad
		/// JSON, an unknown key, a null or a value out of range, naming the key.
		/// </summary>
		internal static SettingsModel Parse(string json) {
			SettingsModel model = JsonSerializer.Deserialize<SettingsModel>(json, _jsonOptions) ?? throw new InvalidDataException("the settings are null");
			RejectNulls(model, "");
			CheckRanges(model);
			foreach (PropertyInfo property in typeof(DirectoriesSettings).GetProperties()) {
				property.SetValue(model.Directories, Environment.ExpandEnvironmentVariables((string)property.GetValue(model.Directories)!));
			}
			return model;
		}

		/// <summary>
		/// Throws for a null section, value or list item.
		/// </summary>
		static void RejectNulls(object section, string path) {
			foreach (PropertyInfo property in section.GetType().GetProperties()) {
				string key = path + property.Name;
				object? value = property.GetValue(section);
				if (value == null) {
					throw new InvalidDataException($"{key} is null");
				}
				if (value is string[] items) {
					if (items.Any(item => item == null)) {
						throw new InvalidDataException($"{key} holds a null");
					}
				}
				else if (value is not string && value.GetType().IsClass) {
					RejectNulls(value, key + ".");
				}
			}
		}

		/// <summary>
		/// Throws for a number out of its range.
		/// </summary>
		static void CheckRanges(SettingsModel s) {
			AtLeast(s.Train.Seconds, 0, "Train.Seconds");
			AtLeast(s.Train.Images, 1, "Train.Images");
			foreach (PropertyInfo property in typeof(SoundsSettings).GetProperties()) {
				double volume = ((SoundSettings)property.GetValue(s.Sounds)!).Volume;
				if (volume < 0 || volume > 1) {
					throw new InvalidDataException($"Sounds.{property.Name}.Volume is {volume}, not from 0 to 1");
				}
			}
			AtLeast(s.DiskSpace.SpamGiB, 0, "DiskSpace.SpamGiB");
			AtLeast(s.DiskSpace.WarnGiB, 0, "DiskSpace.WarnGiB");
			AtLeast(s.DiskSpace.NotifyGiB, 0, "DiskSpace.NotifyGiB");
			DividesHour(s.DiskSpace.SpamEveryMinutes, "DiskSpace.SpamEveryMinutes");
			DividesHour(s.DiskSpace.WarnEveryMinutes, "DiskSpace.WarnEveryMinutes");
			DividesHour(s.DiskSpace.NotifyEveryMinutes, "DiskSpace.NotifyEveryMinutes");
			AtLeast(s.Ads.IntervalMinutes, 5, "Ads.IntervalMinutes");
			AtLeast(s.Chat.FaqCooldownSeconds, 0, "Chat.FaqCooldownSeconds");
			AtLeast((double)s.MoneyGoal.DefaultGoal, 0.01, "MoneyGoal.DefaultGoal");
			AtLeast(s.Layout.ShowTwinsSeconds, 0, "Layout.ShowTwinsSeconds");
			AtLeast(s.Layout.HideRealSeconds, 0, "Layout.HideRealSeconds");
			AtLeast(s.Layout.RecolorSeconds, 0, "Layout.RecolorSeconds");
			AtLeast(s.Game.CaptureSlots, 1, "Game.CaptureSlots");
		}

		/// <summary>
		/// Throws when the value is below the least.
		/// </summary>
		static void AtLeast(double value, double least, string key) {
			if (value < least) {
				throw new InvalidDataException($"{key} is {value}, less than {least}");
			}
		}

		/// <summary>
		/// Throws when the minutes don't divide an hour.
		/// </summary>
		static void DividesHour(int minutes, string key) {
			if (minutes < 1 || 60 % minutes != 0) {
				throw new InvalidDataException($"{key} is {minutes}, which doesn't divide 60");
			}
		}

		/// <summary>
		/// The dotted keys whose values differ from the defaults, in the model's order.
		/// </summary>
		internal static List<string> Changed(SettingsModel model) {
			List<string> changed = new();
			Diff(JsonSerializer.SerializeToNode(new SettingsModel()), JsonSerializer.SerializeToNode(model), "", changed);
			return changed;
		}

		/// <summary>
		/// Adds the keys under path whose values differ.
		/// </summary>
		static void Diff(JsonNode? before, JsonNode? after, string path, List<string> changed) {
			if (before is JsonObject a && after is JsonObject b) {
				foreach (var (key, value) in b) {
					Diff(a[key], value, path.Length == 0 ? key : $"{path}.{key}", changed);
				}
				return;
			}
			if (before?.ToJsonString() != after?.ToJsonString()) {
				changed.Add(path);
			}
		}

		/// <summary>
		/// "Loaded settings.json, all defaults", or the changed keys by section: "Loaded settings.json, changed Train: Seconds,
		/// Images; Sounds: Flush.Volume".
		/// </summary>
		internal static string LoadedLine(IReadOnlyList<string> changed) {
			if (changed.Count == 0) {
				return $"Loaded {SETTINGS_FILE}, all defaults";
			}
			var sections = changed.GroupBy(key => key[..key.IndexOf('.')]).Select(g => $"{g.Key}: {string.Join(", ", g.Select(key => key[(g.Key.Length + 1)..]))}");
			return $"Loaded {SETTINGS_FILE}, changed {string.Join("; ", sections)}";
		}
	}
}
