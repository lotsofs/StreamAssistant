using System.Globalization;
using System.Text.Json;

namespace StreamAssistant2 {
	internal static class LeftPanel {
		/// <summary>
		/// One panel: the groups it shows, the source texts and files it sets first, and how long it stays, in seconds.
		/// </summary>
		internal sealed record Panel(List<string> Show, Dictionary<string, string> Text, Dictionary<string, string> File, double Seconds) {
			/// <summary>
			/// What the dashboard calls the panel instead of listing its groups; null for none.
			/// </summary>
			internal string? Name { get; init; }

			/// <summary>
			/// Groups shown as they are: a family stays on the copy that's up instead of moving to a fresh one.
			/// </summary>
			internal List<string> Keep { get; init; } = [];

			/// <summary>
			/// Every group the panel shows: Show, then Keep, each once.
			/// </summary>
			internal IEnumerable<string> Shown => Show.Concat(Keep).Distinct();
		}

		/// <summary>
		/// A game's panels, and the groups shown for the whole game.
		/// </summary>
		internal sealed record PanelSet(List<string> Always, List<Panel> Panels) {
			/// <summary>
			/// The panels shown in turn after a LiveSplit split, before the rotation carries on; none for most games.
			/// </summary>
			internal List<Panel> Previous { get; init; } = [];

			/// <summary>
			/// LiveSplit's sign file for the previous split, which colours its differences during the Previous panels; null
			/// for none.
			/// </summary>
			internal string? Sign { get; init; }

			/// <summary>
			/// The leftpanel.json entry that defines the set, which entries naming it share; blank for one made in code.
			/// </summary>
			internal string Entry { get; init; } = "";
		}

		sealed class PanelSetDto {
			public List<string?>? Always { get; set; }
			public List<PanelDto?>? Panels { get; set; }
			public List<PanelDto?>? Previous { get; set; }
			public string? Sign { get; set; }
		}

		sealed class PanelDto {
			public string? Name { get; set; }
			public List<string?>? Show { get; set; }
			public Dictionary<string, string?>? Text { get; set; }
			public Dictionary<string, string?>? File { get; set; }
			public double Seconds { get; set; }
			public List<string?>? Keep { get; set; }
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
			Func<TimeSpan, CancellationToken, Task> Delay) {
			/// <summary>
			/// Setting a colour on a source's filter: the source, the filter, the filter's setting and the OBS colour.
			/// </summary>
			internal Action<string, string, string, long> SetFilter { get; init; } = (_, _, _, _) => { };

			/// <summary>
			/// The names of every input in OBS; null when they can't be listed.
			/// </summary>
			internal Func<List<string>?> InputNames { get; init; } = () => null;
		}

		/// <summary>
		/// What OBS has for the left panel: each group's places (scene and item name), the numbered groups as copies of a
		/// family ("Splits Current" → 1: "Splits Current 1", …), and whether the colored scene holds the reference item.
		/// </summary>
		internal sealed record Layout(
			Dictionary<string, List<(string Scene, string Item)>> Groups,
			Dictionary<string, SortedDictionary<int, string>> Families,
			bool HasReference) {
			/// <summary>
			/// The sources OBS has numbered copies of, without the number ("Livesplit: SL - Names 1" → "Livesplit: SL -
			/// Names"); null when unknown, which makes every source a panel with families sets a copy source.
			/// </summary>
			internal HashSet<string>? CopySources { get; init; }
		}

		/// <summary>
		/// What the rotation is showing, for the dashboard: the game, its panels, the loop panel up (or to return to
		/// after a split; null before the first), the Previous panel playing (null outside a split), when the panel now
		/// up was shown (UTC; null before the first), whether it was stopped, and the rotation it belongs to.
		/// </summary>
		internal sealed record RotationStatus(string Game, bool IsDefault, PanelSet Set, int? Panel, int? Previous, DateTime? Since, bool Stopped, CancellationToken Rotation) {
			/// <summary>
			/// The set's groups OBS doesn't have, once the rotation has looked; none before.
			/// </summary>
			internal IReadOnlyCollection<string> Missing { get; init; } = [];
		}

		internal enum LineKind { Title, Heading, Item, On, Current, Marked }

		/// <summary>
		/// One line of the dashboard's left-panel column, and whether it names a group OBS doesn't have.
		/// </summary>
		internal sealed record StatusLine(string Text, LineKind Kind, bool Missing = false);

		const string PANELS_FILE = "leftpanel.json";
		const string DEFAULT_KEY = "default";
		const string PREFIX = "_LP: ";
		const string PREFIX_COLORED = "_LPC: ";
		const string SCENE = "!Scene: Left Panel";
		const string SCENE_COLORED = "!Scene: Left Panel Colored";

		internal static readonly TimeSpan OBS_WAIT = TimeSpan.FromSeconds(5);

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
		static readonly ObsCalls _obsCalls = new(() => ObsConnection.IsReady, Obs.GetSceneItemNames, Obs.SetSourceEnabled, Obs.SetInputSetting, Task.Delay) {
			SetFilter = (source, filter, property, color) => Obs.SetFilterProperty(source, filter, property, color),
			InputNames = Obs.InputNames,
		};
		static readonly object _stepLock = new();
		static readonly object _statusLock = new();

		static Dictionary<string, PanelSet> _sets = new(StringComparer.OrdinalIgnoreCase);
		static CancellationTokenSource? _rotation;
		static volatile string? _game;
		static volatile bool _stopped;
		static TaskCompletionSource _split = new(TaskCreationOptions.RunContinuationsAsynchronously);
		static TaskCompletionSource _skip = new(TaskCreationOptions.RunContinuationsAsynchronously);
		static RotationStatus? _status;

		/// <summary>
		/// Whether any panels are loaded.
		/// </summary>
		internal static bool IsLoaded => _sets.Count > 0;

		/// <summary>
		/// Whether leftpanel.json has an entry of this name (any case).
		/// </summary>
		internal static bool HasPanels(string name) => _sets.ContainsKey(name);

		/// <summary>
		/// What the rotation is showing now; null when there is none.
		/// </summary>
		internal static RotationStatus? Status {
			get { lock (_statusLock) return _status; }
		}

		/// <summary>
		/// Loads leftpanel.json from BotInput; a missing or broken file keeps the panels loaded before, if any.
		/// </summary>
		internal static void Load() {
			try {
				string path = Path.Combine(Config.Data.Directories.BotInput, PANELS_FILE);
				if (!File.Exists(path)) {
					string kept = _sets.Count > 0 ? "keeping the left panels loaded" : "left panel off";
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{PANELS_FILE} not found, {kept}");
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
					sets[name] = ToSet(entry.Deserialize<PanelSetDto>(_jsonOptions)!, name);
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
		/// One entry's panel set, under the entry's name.
		/// </summary>
		static PanelSet ToSet(PanelSetDto dto, string entry) {
			return new PanelSet(Groups(dto.Always), ToPanels(dto.Panels)) {
				Previous = ToPanels(dto.Previous),
				Sign = string.IsNullOrWhiteSpace(dto.Sign) ? null : dto.Sign.Trim(),
				Entry = entry,
			};
		}

		/// <summary>
		/// The game with the leftpanel.json entry its panels come from, when that isn't its own: "KTANE (default)" for the
		/// fallback, "SWAT4 (Run_NoSubsplits)" for an entry it names; the game alone otherwise.
		/// </summary>
		internal static string Which(string game, bool isDefault, PanelSet set) {
			if (isDefault) {
				return $"{game} (default)";
			}
			return set.Entry.Length > 0 && !set.Entry.Equals(game, StringComparison.OrdinalIgnoreCase) ? $"{game} ({set.Entry})" : game;
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
				panels.Add(new Panel(Groups(panel.Show), Values(panel.Text), Values(panel.File), panel.Seconds) {
					Name = string.IsNullOrWhiteSpace(panel.Name) ? null : panel.Name.Trim(),
					Keep = Groups(panel.Keep),
				});
			}
			return panels;
		}

		/// <summary>
		/// Group names, trimmed and without an "_LP: " or "_LPC: " prefix; blank ones are dropped.
		/// </summary>
		static List<string> Groups(List<string?>? names) {
			List<string> groups = new();
			foreach (string? name in names ?? []) {
				string trimmed = name?.Trim() ?? "";
				string group = (GroupName(trimmed, SCENE) ?? GroupName(trimmed, SCENE_COLORED) ?? trimmed).Trim();
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
		/// Starts rotating the game's panels, else the default ones, in place of any running rotation, with the game's split
		/// colours; with neither, stops and leaves the panel as is. Does nothing when no panels are loaded.
		/// </summary>
		internal static void Start(string gameName) {
			if (_sets.Count == 0) {
				return;
			}
			_stopped = false;
			CancellationTokenSource next = new();
			PanelSet? set = Lookup(_sets, gameName);
			bool isDefault = !_sets.ContainsKey(gameName);
			lock (_statusLock) {
				Interlocked.Exchange(ref _rotation, next)?.Cancel();
				_status = set == null ? null : new RotationStatus(gameName, isDefault, set, null, null, null, false, next.Token);
			}
			if (set == null) {
				_game = null;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No left panels for {gameName}, left as is");
				return;
			}
			_game = gameName;
			string which = Which(gameName, isDefault, set);
			int count = set.Panels.Count;
			string onSplit = set.Previous.Count > 0 ? $", {set.Previous.Count} on a split" : "";
			var colors = SplitColors.For(gameName);
			string colored = colors is (string from, _) ? $", split colours from {from}" : ", no split colours";
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"Left panel: {which}, {count} panel{(count == 1 ? "" : "s")}{onSplit}{colored}");
			FireForget.Run("LPN3", "left panel rotation", () => RunAsync(gameName, set, _obsCalls, next.Token,
				(panel, previous) => Report(next.Token, status => status with { Panel = panel, Previous = previous, Since = DateTime.UtcNow }),
				gone => Report(next.Token, status => status with { Missing = gone }),
				colors?.Colors));
		}

		/// <summary>
		/// Applies a change to the status, if the rotation reporting it is still the current one.
		/// </summary>
		static void Report(CancellationToken rotation, Func<RotationStatus, RotationStatus> change) {
			lock (_statusLock) {
				if (_status != null && _status.Rotation == rotation && !rotation.IsCancellationRequested) {
					_status = change(_status);
				}
			}
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
			lock (_statusLock) {
				Interlocked.Exchange(ref _rotation, null)?.Cancel();
				if (_status != null) {
					_status = _status with { Stopped = true };
				}
			}
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
		/// "!nextpanelitem": cuts the panel up short, so the rotation moves on to the next one now; logs when there is no
		/// rotation to move on.
		/// </summary>
		internal static void Next() {
			RotationStatus? status = Status;
			if (status == null || status.Stopped) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "No left panel rotation to move on");
				return;
			}
			if (status.Set.Panels.Count <= 1 && status.Set.Previous.Count == 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Only one left panel, nothing to move on to");
				return;
			}
			SkipPanel();
		}

		/// <summary>
		/// Wakes the running rotation's wait, which then moves on as though the panel's time were up.
		/// </summary>
		internal static void SkipPanel() {
			Interlocked.Exchange(ref _skip, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
		}

		/// <summary>
		/// Restarts the last game's rotation, unless it was stopped, so a reconnected OBS gets its groups set again.
		/// </summary>
		internal static void OnObsConnected() {
			try {
				Restart();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LPN4");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Starts the last game's rotation again, from its first panel, unless it was stopped.
		/// </summary>
		internal static void Restart() {
			string? game = _game;
			if (game != null && !_stopped) {
				Start(game);
			}
		}

		/// <summary>
		/// Shows the set's panels in turn until cancelled. The first step with OBS ready finds the groups in both scenes;
		/// each step sets the panel's texts and files, shows its groups and hides the rest, then waits its Seconds. When
		/// the set has Previous panels, a split cuts the wait short; they play, then the interrupted panel again. A skip
		/// cuts any wait short as though its time were up. While
		/// OBS isn't ready it waits and tries again; with one panel or none, and no Previous, it stops once that is up.
		/// Each panel shown is passed to shown: the loop panel's index, and the Previous panel's during a split; the set's
		/// groups OBS doesn't have are passed to missing once, when the groups are found. With split colours, the first
		/// step colours the split list highlights, and each Previous step colours the previous split's differences by the
		/// set's sign file, read through readText.
		/// </summary>
		internal static async Task RunAsync(string gameName, PanelSet set, ObsCalls calls, CancellationToken token, Action<int?, int?>? shown = null, Action<List<string>>? missing = null, IReadOnlyDictionary<string, string>? colors = null, Func<string, string?>? readText = null) {
			await Task.Yield();
			Layout? layout = null;
			HashSet<string>? up = null;
			Dictionary<string, int> last = new();
			int index = 0;

			// Shows one panel, or only Always for null, and reports it; false while OBS isn't ready.
			bool Step(Panel? panel, int? previous) {
				lock (_stepLock) {
					token.ThrowIfCancellationRequested();
					if (!calls.IsReady()) {
						return false;
					}
					if (layout == null) {
						layout = FindLayout(gameName, set, calls);
						if (layout.HasReference) {
							Attempt($"hiding {SCENE} in {SCENE_COLORED}", () => calls.SetEnabled(SCENE_COLORED, SCENE, false));
						}
						if (colors != null) {
							SetFilters(SplitColors.HighlightFilters(colors), calls);
						}
						Layout found = layout;
						missing?.Invoke(Missing(set, name => Exists(found, name)));
					}
					if (previous != null && colors != null && set.Sign != null) {
						ColorPrevious(set.Sign, colors, calls, readText ?? SplitWatcher.ReadText);
					}
					var (wanted, settings) = Plan(layout, set.Always, panel, up, last);
					var (show, hide) = Transition(up, wanted, layout.Groups.Keys);
					foreach (var (source, key, value) in settings) {
						Attempt($"setting the {key} of {source}", () => calls.SetSetting(source, key, value));
					}
					Toggle(show, true, layout.Groups, calls);
					Toggle(hide, false, layout.Groups, calls);
					up = wanted;
					shown?.Invoke(set.Panels.Count > 0 ? index : null, previous);
					return true;
				}
			}

			// Waits the time, or until the skip, or until the split when the set has Previous panels; true for a split.
			async Task<bool> WaitOrSplit(TimeSpan time, Task split, Task skip) {
				bool splits = set.Previous.Count > 0;
				using CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(token);
				Task delay = calls.Delay(time, wait.Token);
				await Task.WhenAny(splits ? [delay, skip, split] : [delay, skip]);
				bool isSplit = splits && split.IsCompleted;
				if (isSplit || skip.IsCompleted) {
					wait.Cancel();
					token.ThrowIfCancellationRequested();
					return isSplit;
				}
				await delay;
				return false;
			}

			// Plays the Previous panels once; a split during them starts them over.
			async Task PlayPrevious() {
				for (int i = 0; i < set.Previous.Count; i++) {
					Task split = Volatile.Read(ref _split).Task;
					Task skip = Volatile.Read(ref _skip).Task;
					Step(set.Previous[i], i);
					if (await WaitOrSplit(TimeSpan.FromSeconds(set.Previous[i].Seconds), split, skip)) {
						i = -1;
					}
				}
			}

			try {
				while (true) {
					Task split = Volatile.Read(ref _split).Task;
					Task skip = Volatile.Read(ref _skip).Task;
					Panel? panel = set.Panels.Count > 0 ? set.Panels[index] : null;
					if (!Step(panel, null)) {
						await calls.Delay(OBS_WAIT, token);
						continue;
					}
					bool rotates = set.Panels.Count > 1;
					if (!rotates && set.Previous.Count == 0) {
						return;
					}
					TimeSpan time = rotates ? TimeSpan.FromSeconds(panel!.Seconds) : Timeout.InfiniteTimeSpan;
					if (await WaitOrSplit(time, split, skip)) {
						await PlayPrevious();
						continue;
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
		/// The set's groups, Always and every panel's (Previous too), that don't exist; each once, in order.
		/// </summary>
		internal static List<string> Missing(PanelSet set, Func<string, bool> exists) {
			return set.Always.Concat(set.Panels.Concat(set.Previous).SelectMany(p => p.Shown)).Where(g => !exists(g)).Distinct().ToList();
		}

		/// <summary>
		/// Whether a name from the file is a group OBS has: a family of copies, a copy's numbered name, or a single group.
		/// </summary>
		internal static bool Exists(Layout layout, string name) {
			return FamilyOf(layout, name) != null || layout.Groups.ContainsKey(name);
		}

		/// <summary>
		/// The family a name from the file stands for: the name itself when it's a family, the name without its number
		/// when that is one ("Splits Current 2" → "Splits Current"); null otherwise.
		/// </summary>
		static string? FamilyOf(Layout layout, string name) {
			if (layout.Families.ContainsKey(name)) {
				return name;
			}
			return Numbered(name) is (string base_, int _) && layout.Families.ContainsKey(base_) ? base_ : null;
		}

		/// <summary>
		/// A name ending in a space and a whole number, split into the rest and the number; null for any other name.
		/// </summary>
		internal static (string Base, int Number)? Numbered(string name) {
			int space = name.LastIndexOf(' ');
			if (space <= 0 || space == name.Length - 1 || !name[(space + 1)..].All(char.IsAsciiDigit)) {
				return null;
			}
			return int.TryParse(name[(space + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? (name[..space], number) : null;
		}

		/// <summary>
		/// What to put up for a panel (null: only Always) with these groups up: the groups wanted, by OBS name, and the
		/// texts and files to set. A family the panel keeps stays on the copy up, or, with none up, comes back on the copy
		/// last records for it; the panel's other families all move to the lowest copy number hidden in each of them,
		/// avoiding the kept copies' numbers when they can (each to its own lowest hidden copy when there's no such number),
		/// and last records the copy each moves to; an Always family keeps the copy up. In a panel with families, a copy
		/// source (one the layout's CopySources has, or any while those are unknown) gets the number of the first family
		/// that moves, else of the first kept one (in place of any it ends in); other sources, and every source in a panel
		/// without families, are set as written. Group names OBS doesn't have are left out.
		/// </summary>
		internal static (HashSet<string> Wanted, List<(string Source, string Key, string Value)> Settings) Plan(Layout layout, IEnumerable<string> always, Panel? panel, IReadOnlyCollection<string>? up, Dictionary<string, int> last) {
			bool IsUp(string group) => up?.Contains(group) == true;
			HashSet<string> wanted = new();
			foreach (string name in always) {
				if (FamilyOf(layout, name) is string family) {
					SortedDictionary<int, string> copies = layout.Families[family];
					wanted.Add(copies.Values.FirstOrDefault(IsUp) ?? copies.Values.First());
				}
				else if (layout.Groups.ContainsKey(name)) {
					wanted.Add(name);
				}
			}

			List<string> families = new();
			foreach (string name in panel?.Shown ?? []) {
				if (FamilyOf(layout, name) is string family) {
					if (!families.Contains(family)) {
						families.Add(family);
					}
				}
				else if (layout.Groups.ContainsKey(name)) {
					wanted.Add(name);
				}
			}
			HashSet<string> keep = (panel?.Keep ?? []).Select(name => FamilyOf(layout, name)).OfType<string>().ToHashSet();
			List<string> moving = new();
			List<int> kept = new();
			foreach (string family in families) {
				SortedDictionary<int, string> copies = layout.Families[family];
				int? held = copies.Where(item => IsUp(item.Value)).Select(item => (int?)item.Key).FirstOrDefault()
					?? (last.TryGetValue(family, out int shown) && copies.ContainsKey(shown) ? shown : null);
				if (keep.Contains(family) && held is int copy) {
					wanted.Add(copies[copy]);
					kept.Add(copy);
				}
				else {
					moving.Add(family);
				}
			}
			int? number = null;
			if (moving.Count > 0) {
				List<int> free = moving.Select(f => layout.Families[f].Where(copy => !IsUp(copy.Value)).Select(copy => copy.Key))
					.Aggregate((a, b) => a.Intersect(b)).Order().ToList();
				int? common = free.Where(n => !kept.Contains(n)).Cast<int?>().FirstOrDefault() ?? free.Cast<int?>().FirstOrDefault();
				foreach (string family in moving) {
					SortedDictionary<int, string> copies = layout.Families[family];
					int pick = common is int shared ? shared : copies.Keys.Where(n => !IsUp(copies[n])).DefaultIfEmpty(copies.Keys.First()).First();
					number ??= pick;
					wanted.Add(copies[pick]);
					last[family] = pick;
				}
			}
			number ??= kept.Cast<int?>().FirstOrDefault();

			// The source itself, or with families a copy source's copy: the number in place of any it ends in.
			string Target(string source) {
				string unnumbered = Numbered(source) is (string base_, int _) ? base_ : source;
				if (number is not int chosen || layout.CopySources?.Contains(unnumbered) == false) {
					return source;
				}
				return $"{unnumbered} {chosen}";
			}
			List<(string Source, string Key, string Value)> settings = new();
			foreach (var (source, text) in panel?.Text ?? []) {
				settings.Add((Target(source), "text", text));
			}
			foreach (var (source, file) in panel?.File ?? []) {
				settings.Add((Target(source), "file", file));
			}
			return (wanted, settings);
		}

		/// <summary>
		/// The dashboard's lines for a rotation: a title, then Always (on once something is shown), the loop and the
		/// Previous panels, with the panel up marked and timed against its Seconds; during a split the loop panel to
		/// return to keeps its marker. A panel that stays up for good (stopped, or the only one) gets no timer. A line
		/// with a group OBS doesn't have is flagged Missing.
		/// </summary>
		internal static List<StatusLine> StatusLines(RotationStatus? status, DateTime now) {
			if (status == null) {
				return [new StatusLine("Left panel: none", LineKind.Title)];
			}
			string title = $"Left panel: {Which(status.Game, status.IsDefault, status.Set)}{(status.Stopped ? ", stopped" : "")}";
			List<StatusLine> lines = [new StatusLine(title, LineKind.Title), new StatusLine("Always", LineKind.Heading)];
			if (status.Set.Always.Count == 0) {
				lines.Add(new StatusLine("  (none)", LineKind.Item));
			}
			foreach (string group in status.Set.Always) {
				lines.Add(new StatusLine($"  {group}", status.Since == null ? LineKind.Item : LineKind.On, status.Missing.Contains(group)));
			}
			lines.Add(new StatusLine("Loop", LineKind.Heading));
			if (status.Set.Panels.Count == 0) {
				lines.Add(new StatusLine("  (none)", LineKind.Item));
			}
			bool timed = !status.Stopped && status.Since != null;
			for (int i = 0; i < status.Set.Panels.Count; i++) {
				LineKind kind = i != status.Panel ? LineKind.Item : status.Previous == null ? LineKind.Current : LineKind.Marked;
				DateTime? since = kind == LineKind.Current && timed && status.Set.Panels.Count > 1 ? status.Since : null;
				lines.Add(PanelLine(status.Set.Panels[i], kind, since, now, status.Missing));
			}
			if (status.Set.Previous.Count > 0) {
				lines.Add(new StatusLine("Previous split", LineKind.Heading));
				for (int i = 0; i < status.Set.Previous.Count; i++) {
					bool current = i == status.Previous;
					lines.Add(PanelLine(status.Set.Previous[i], current ? LineKind.Current : LineKind.Item, current && timed ? status.Since : null, now, status.Missing));
				}
			}
			return lines;
		}

		/// <summary>
		/// One panel's line: "> " unless a plain item, its Name or else its groups ("(blank)" for none) and texts in
		/// quotes, and its seconds, as whole seconds elapsed since it was shown out of them when it is timed; Missing when
		/// one of its groups is.
		/// </summary>
		static StatusLine PanelLine(Panel panel, LineKind kind, DateTime? since, DateTime now, IReadOnlyCollection<string> missing) {
			string marker = kind == LineKind.Item ? "  " : "> ";
			List<string> panelGroups = panel.Shown.ToList();
			string groups = panelGroups.Count == 0 ? "(blank)" : string.Join(", ", panelGroups);
			string texts = panel.Text.Count == 0 ? "" : " · " + string.Join(", ", panel.Text.Values.Select(text => $"\"{text}\""));
			string label = panel.Name ?? groups + texts;
			string seconds = panel.Seconds.ToString(CultureInfo.InvariantCulture);
			string elapsed = since is DateTime shown ? $"{(int)Math.Clamp((now - shown).TotalSeconds, 0, panel.Seconds)} / " : "";
			return new StatusLine($"{marker}{label} · {elapsed}{seconds} s", kind, panelGroups.Any(missing.Contains));
		}

		/// <summary>
		/// The group name in a scene item: "_LP: name" in the left-panel scene, "_LPC: name" in the colored one; null for
		/// any other item, including the other scene's prefix.
		/// </summary>
		internal static string? GroupName(string item, string scene) {
			string? prefix = scene == SCENE ? PREFIX : scene == SCENE_COLORED ? PREFIX_COLORED : null;
			if (prefix != null && item.StartsWith(prefix, StringComparison.Ordinal)) {
				return item[prefix.Length..];
			}
			return null;
		}

		/// <summary>
		/// What OBS has for the left panel, read once per rotation: the groups in both scenes and the copies among them, and
		/// the sources with numbered copies. Logs the set's groups that aren't there.
		/// </summary>
		static Layout FindLayout(string gameName, PanelSet set, ObsCalls calls) {
			Dictionary<string, List<(string Scene, string Item)>> groups = new();
			bool hasReference = false;
			foreach (string scene in new[] { SCENE, SCENE_COLORED }) {
				List<string> items = [];
				Attempt($"listing {scene}", () => items = calls.SceneItems(scene));
				foreach (string item in items) {
					hasReference |= scene == SCENE_COLORED && item == SCENE;
					if (GroupName(item, scene) is not string group) {
						continue;
					}
					if (!groups.TryGetValue(group, out List<(string Scene, string Item)>? places)) {
						groups[group] = places = new();
					}
					places.Add((scene, item));
				}
			}
			Dictionary<string, SortedDictionary<int, string>> families = new();
			foreach (string group in groups.Keys) {
				if (Numbered(group) is (string family, int number)) {
					if (!families.TryGetValue(family, out SortedDictionary<int, string>? copies)) {
						families[family] = copies = new();
					}
					copies[number] = group;
				}
			}
			List<string>? inputs = null;
			Attempt("listing OBS's inputs", () => inputs = calls.InputNames());
			HashSet<string>? copySources = inputs?.Select(input => Numbered(input)?.Base).OfType<string>().ToHashSet();
			Layout layout = new(groups, families, hasReference) { CopySources = copySources };
			List<string> missing = Missing(set, name => Exists(layout, name));
			if (missing.Count > 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Left panel: {gameName} has groups OBS doesn't: {string.Join(", ", missing)}");
			}
			return layout;
		}

		/// <summary>
		/// Shows or hides each group in every scene it's in, by its item name there.
		/// </summary>
		static void Toggle(List<string> groups, bool enabled, Dictionary<string, List<(string Scene, string Item)>> scenes, ObsCalls calls) {
			string what = enabled ? "showing" : "hiding";
			foreach (string group in groups) {
				foreach (var (scene, item) in scenes[group]) {
					Attempt($"{what} {group} in {scene}", () => calls.SetEnabled(scene, item, enabled));
				}
			}
		}

		/// <summary>
		/// Colours the previous split's differences by the sign in the file; logs and leaves them when it can't be read.
		/// </summary>
		static void ColorPrevious(string signFile, IReadOnlyDictionary<string, string> colors, ObsCalls calls, Func<string, string?> readText) {
			string? sign;
			try {
				sign = readText(signFile);
			}
			catch (Exception ex) {
				ConsoleLogger.LogToFile(ex);
				sign = null;
			}
			if (sign == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Left panel: can't read {signFile}, previous-split colours left as is");
				return;
			}
			SetFilters(SplitColors.PreviousFilters(colors, sign), calls);
		}

		/// <summary>
		/// Sets each filter colour, logging any that fails.
		/// </summary>
		static void SetFilters(List<SplitColors.FilterColor> filters, ObsCalls calls) {
			foreach (SplitColors.FilterColor filter in filters) {
				Attempt($"colouring {filter.Source}", () => calls.SetFilter(filter.Source, filter.Filter, filter.Property, filter.Color));
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
