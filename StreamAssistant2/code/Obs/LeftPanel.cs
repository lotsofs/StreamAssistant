using System.Text.Json;

namespace StreamAssistant2 {
	internal static class LeftPanel {
		/// <summary>
		/// One panel: the groups it shows, the source texts and files it sets first, and how long it stays, in seconds.
		/// </summary>
		internal sealed record Panel(List<string> Show, Dictionary<string, string> Text, Dictionary<string, string> File, double Seconds);

		/// <summary>
		/// A game's panels, and the groups shown for the whole game.
		/// </summary>
		internal sealed record PanelSet(List<string> Always, List<Panel> Panels) {
			/// <summary>
			/// The panels shown in turn after a LiveSplit split, before the rotation carries on; none for most games.
			/// </summary>
			internal List<Panel> Previous { get; init; } = [];
		}

		sealed class PanelSetDto {
			public List<string?>? Always { get; set; }
			public List<PanelDto?>? Panels { get; set; }
			public List<PanelDto?>? Previous { get; set; }
		}

		sealed class PanelDto {
			public List<string?>? Show { get; set; }
			public Dictionary<string, string?>? Text { get; set; }
			public Dictionary<string, string?>? File { get; set; }
			public double Seconds { get; set; }
		}

		/// <summary>
		/// The OBS side of a rotation: whether OBS is ready, a scene's top-level source names, showing or hiding a source
		/// in a scene, setting one input setting, and the wait between panels.
		/// </summary>
		internal sealed record ObsCalls(
			Func<bool> IsReady,
			Func<string, List<string>> SceneItems,
			Action<string, string, bool> SetEnabled,
			Action<string, string, string> SetSetting,
			Func<TimeSpan, CancellationToken, Task> Delay);

		const string PANELS_FILE = "leftpanel.json";
		const string DEFAULT_KEY = "default";
		const string GROUP_PREFIX = "_Left Panel: ";
		const string SCENE = "!Scene: Left Panel";
		const string SCENE_COLORED = "!Scene: Left Panel Colored";

		internal static readonly TimeSpan OBS_WAIT = TimeSpan.FromSeconds(5);

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
		static readonly ObsCalls _obsCalls = new(() => ObsConnection.IsReady, Obs.GetSceneItemNames, Obs.SetSourceEnabled, Obs.SetInputSetting, Task.Delay);
		static readonly object _stepLock = new();

		static Dictionary<string, PanelSet> _sets = new(StringComparer.OrdinalIgnoreCase);
		static CancellationTokenSource? _rotation;
		static volatile string? _game;
		static volatile bool _stopped;
		static TaskCompletionSource _split = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Loads leftpanel.json from BotInput; a missing or broken file leaves no panels.
		/// </summary>
		internal static void Load() {
			try {
				string path = Path.Combine(Config.Data.Directories.BotInput, PANELS_FILE);
				if (!File.Exists(path)) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{PANELS_FILE} not found, left panel off");
					return;
				}
				_sets = Parse(File.ReadAllText(path));
				int games = _sets.Keys.Count(k => !k.Equals(DEFAULT_KEY, StringComparison.OrdinalIgnoreCase));
				string fallback = _sets.ContainsKey(DEFAULT_KEY) ? " and a default" : ", no default";
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Loaded left panels for {games} games{fallback}");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LPN1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Game name (any case) → its panel set. An entry that is another entry's name shares that entry's set; one that
		/// leads to no set is logged and skipped. Null entries, and panels without a positive Seconds, are skipped.
		/// </summary>
		internal static Dictionary<string, PanelSet> Parse(string json) {
			var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions) ?? new();
			Dictionary<string, PanelSet> sets = new(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);
			foreach (var (name, entry) in raw) {
				if (entry.ValueKind == JsonValueKind.Object) {
					sets[name] = ToSet(entry.Deserialize<PanelSetDto>(_jsonOptions)!);
				}
				else if (entry.ValueKind == JsonValueKind.String) {
					links[name] = entry.GetString()!;
				}
				else if (entry.ValueKind != JsonValueKind.Null) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{PANELS_FILE}: skipped {name}, not panels or a game name");
				}
			}
			foreach (var (name, target) in links) {
				PanelSet? set = Follow(target, sets, links);
				if (set == null) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{PANELS_FILE}: skipped {name}, \"{target}\" leads to no panels");
					continue;
				}
				sets[name] = set;
			}
			return sets;
		}

		/// <summary>
		/// The panel set an entry's name leads to, following names that are other names; null at a dead end or a loop.
		/// </summary>
		static PanelSet? Follow(string name, Dictionary<string, PanelSet> sets, Dictionary<string, string> links) {
			for (int step = 0; step <= links.Count; step++) {
				if (sets.TryGetValue(name, out PanelSet? set)) {
					return set;
				}
				if (!links.TryGetValue(name, out string? next)) {
					return null;
				}
				name = next;
			}
			return null;
		}

		/// <summary>
		/// One entry's panel set.
		/// </summary>
		static PanelSet ToSet(PanelSetDto dto) {
			return new PanelSet(Groups(dto.Always), ToPanels(dto.Panels)) { Previous = ToPanels(dto.Previous) };
		}

		/// <summary>
		/// A list of panels; null ones, and those without a positive Seconds, are skipped.
		/// </summary>
		static List<Panel> ToPanels(List<PanelDto?>? dtos) {
			List<Panel> panels = new();
			foreach (PanelDto? panel in dtos ?? []) {
				if (panel == null || panel.Seconds <= 0) {
					continue;
				}
				panels.Add(new Panel(Groups(panel.Show), Values(panel.Text), Values(panel.File), panel.Seconds));
			}
			return panels;
		}

		/// <summary>
		/// Group names, trimmed and without the "_Left Panel: " prefix; blank ones are dropped.
		/// </summary>
		static List<string> Groups(List<string?>? names) {
			List<string> groups = new();
			foreach (string? name in names ?? []) {
				string group = name?.Trim() ?? "";
				if (group.StartsWith(GROUP_PREFIX, StringComparison.Ordinal)) {
					group = group[GROUP_PREFIX.Length..].Trim();
				}
				if (group.Length > 0) {
					groups.Add(group);
				}
			}
			return groups;
		}

		/// <summary>
		/// Source → value pairs, without the null values.
		/// </summary>
		static Dictionary<string, string> Values(Dictionary<string, string?>? values) {
			Dictionary<string, string> result = new();
			if (values == null) {
				return result;
			}
			foreach (var (source, value) in values) {
				if (value != null) {
					result[source] = value;
				}
			}
			return result;
		}

		/// <summary>
		/// The game's panel set, else the default one, else null.
		/// </summary>
		internal static PanelSet? Lookup(IReadOnlyDictionary<string, PanelSet> sets, string gameName) {
			if (sets.TryGetValue(gameName, out PanelSet? set)) {
				return set;
			}
			return sets.TryGetValue(DEFAULT_KEY, out PanelSet? fallback) ? fallback : null;
		}

		/// <summary>
		/// Starts rotating the game's panels, else the default ones, in place of any running rotation; with neither, stops
		/// and leaves the panel as is. Does nothing when no panels are loaded.
		/// </summary>
		internal static void Start(string gameName) {
			if (_sets.Count == 0) {
				return;
			}
			_stopped = false;
			CancellationTokenSource next = new();
			Interlocked.Exchange(ref _rotation, next)?.Cancel();
			PanelSet? set = Lookup(_sets, gameName);
			if (set == null) {
				_game = null;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No left panels for {gameName}, left as is");
				return;
			}
			_game = gameName;
			string which = _sets.ContainsKey(gameName) ? gameName : $"{gameName} (default)";
			int count = set.Panels.Count;
			string previous = set.Previous.Count > 0 ? $", {set.Previous.Count} on a split" : "";
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"Left panel: {which}, {count} panel{(count == 1 ? "" : "s")}{previous}");
			FireForget.Run("LPN3", "left panel rotation", () => RunAsync(gameName, set, _obsCalls, next.Token));
		}

		/// <summary>
		/// "!stoppaneltimer": stops the rotation, leaving the current panel up, until the next Start or Resume; an OBS
		/// reconnect doesn't restart it.
		/// </summary>
		internal static void Stop() {
			if (_game == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "No left panel rotation to stop");
				return;
			}
			_stopped = true;
			Interlocked.Exchange(ref _rotation, null)?.Cancel();
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, "Left panel rotation stopped");
		}

		/// <summary>
		/// "!startpaneltimer": starts the last game's rotation again, from its first panel; logs when there is none.
		/// </summary>
		internal static void Resume() {
			string? game = _game;
			if (game == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "No left panel to start");
				return;
			}
			Start(game);
		}

		/// <summary>
		/// A LiveSplit split: wakes the running rotation, which shows its Previous panels if it has any.
		/// </summary>
		internal static void OnSplit() {
			Interlocked.Exchange(ref _split, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
		}

		/// <summary>
		/// Restarts the last game's rotation, unless it was stopped, so a reconnected OBS gets its groups set again.
		/// </summary>
		internal static void OnObsConnected() {
			try {
				string? game = _game;
				if (game != null && !_stopped) {
					Start(game);
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LPN4");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Shows the set's panels in turn until cancelled. The first step with OBS ready finds the groups in both scenes;
		/// each step sets the panel's texts and files, shows its groups and hides the rest, then waits its Seconds. When
		/// the set has Previous panels, a split cuts the wait short and they play before the next panel. While OBS isn't
		/// ready it waits and tries again; with one panel or none, and no Previous, it stops once that is up.
		/// </summary>
		internal static async Task RunAsync(string gameName, PanelSet set, ObsCalls calls, CancellationToken token) {
			await Task.Yield();
			Dictionary<string, List<string>>? scenes = null;
			HashSet<string>? up = null;
			int index = 0;

			// Shows one panel, or only Always for null; false while OBS isn't ready.
			bool Step(Panel? panel) {
				lock (_stepLock) {
					token.ThrowIfCancellationRequested();
					if (!calls.IsReady()) {
						return false;
					}
					scenes ??= FindGroups(gameName, set, calls);
					HashSet<string> wanted = new(set.Always.Concat(panel?.Show ?? []));
					var (show, hide) = Transition(up, wanted, scenes.Keys);
					if (panel != null) {
						SetValues(panel, calls);
					}
					Toggle(show, true, scenes, calls);
					Toggle(hide, false, scenes, calls);
					up = wanted;
					return true;
				}
			}

			// Waits the time, or until the split when the set has Previous panels; true for a split.
			async Task<bool> WaitOrSplit(TimeSpan time, Task split) {
				if (set.Previous.Count == 0) {
					await calls.Delay(time, token);
					return false;
				}
				using CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(token);
				Task delay = calls.Delay(time, wait.Token);
				await Task.WhenAny(delay, split);
				if (split.IsCompleted) {
					wait.Cancel();
					token.ThrowIfCancellationRequested();
					return true;
				}
				await delay;
				return false;
			}

			// Plays the Previous panels once; a split during them starts them over.
			async Task PlayPrevious() {
				for (int i = 0; i < set.Previous.Count; i++) {
					Task split = Volatile.Read(ref _split).Task;
					Step(set.Previous[i]);
					if (await WaitOrSplit(TimeSpan.FromSeconds(set.Previous[i].Seconds), split)) {
						i = -1;
					}
				}
			}

			try {
				while (true) {
					Task split = Volatile.Read(ref _split).Task;
					Panel? panel = set.Panels.Count > 0 ? set.Panels[index] : null;
					if (!Step(panel)) {
						await calls.Delay(OBS_WAIT, token);
						continue;
					}
					bool rotates = set.Panels.Count > 1;
					if (!rotates && set.Previous.Count == 0) {
						return;
					}
					TimeSpan time = rotates ? TimeSpan.FromSeconds(panel!.Seconds) : Timeout.InfiniteTimeSpan;
					if (await WaitOrSplit(time, split)) {
						await PlayPrevious();
					}
					if (set.Panels.Count > 0) {
						index = (index + 1) % set.Panels.Count;
					}
				}
			}
			catch (OperationCanceledException) {
			}
		}

		/// <summary>
		/// The groups to show and to hide to go from the groups up (null: unknown, so any known group) to the wanted ones,
		/// both limited to the known groups.
		/// </summary>
		internal static (List<string> Show, List<string> Hide) Transition(IReadOnlyCollection<string>? up, IReadOnlyCollection<string> wanted, IReadOnlyCollection<string> known) {
			List<string> show = wanted.Where(g => known.Contains(g) && (up == null || !up.Contains(g))).ToList();
			List<string> hide = (up ?? known).Where(g => known.Contains(g) && !wanted.Contains(g)).ToList();
			return (show, hide);
		}

		/// <summary>
		/// The set's groups, Always and every panel's (Previous too), that aren't among the known ones; each once, in order.
		/// </summary>
		internal static List<string> Missing(PanelSet set, IReadOnlyCollection<string> known) {
			return set.Always.Concat(set.Panels.Concat(set.Previous).SelectMany(p => p.Show)).Where(g => !known.Contains(g)).Distinct().ToList();
		}

		/// <summary>
		/// Group name → the left-panel scenes it's a top-level item of; logs the set's groups that are in neither.
		/// </summary>
		static Dictionary<string, List<string>> FindGroups(string gameName, PanelSet set, ObsCalls calls) {
			Dictionary<string, List<string>> scenes = new();
			foreach (string scene in new[] { SCENE, SCENE_COLORED }) {
				List<string> items = [];
				Attempt($"listing {scene}", () => items = calls.SceneItems(scene));
				foreach (string item in items) {
					if (!item.StartsWith(GROUP_PREFIX, StringComparison.Ordinal)) {
						continue;
					}
					string group = item[GROUP_PREFIX.Length..];
					if (!scenes.TryGetValue(group, out List<string>? inScenes)) {
						scenes[group] = inScenes = new();
					}
					inScenes.Add(scene);
				}
			}
			List<string> missing = Missing(set, scenes.Keys);
			if (missing.Count > 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Left panel: {gameName} has groups OBS doesn't: {string.Join(", ", missing)}");
			}
			return scenes;
		}

		/// <summary>
		/// Sets the panel's source texts and files.
		/// </summary>
		static void SetValues(Panel panel, ObsCalls calls) {
			foreach (var (source, text) in panel.Text) {
				Attempt($"setting the text of {source}", () => calls.SetSetting(source, "text", text));
			}
			foreach (var (source, file) in panel.File) {
				Attempt($"setting the file of {source}", () => calls.SetSetting(source, "file", file));
			}
		}

		/// <summary>
		/// Shows or hides each group in every scene it's in.
		/// </summary>
		static void Toggle(List<string> groups, bool enabled, Dictionary<string, List<string>> scenes, ObsCalls calls) {
			string what = enabled ? "showing" : "hiding";
			foreach (string group in groups) {
				foreach (string scene in scenes[group]) {
					Attempt($"{what} {group} in {scene}", () => calls.SetEnabled(scene, GROUP_PREFIX + group, enabled));
				}
			}
		}

		/// <summary>
		/// Runs one OBS call, logging a throw as Error LPN2 instead of passing it on.
		/// </summary>
		static void Attempt(string what, Action call) {
			try {
				call();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error LPN2: left panel {what} failed: {ex.Message}");
				ConsoleLogger.LogToFile(ex);
			}
		}
	}
}
