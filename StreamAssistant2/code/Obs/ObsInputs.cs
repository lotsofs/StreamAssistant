using System.Globalization;

namespace StreamAssistant2 {
	// What the bot last set on each OBS input and filter, for the dashboard
	internal static class ObsInputs {
		internal enum Kind { Capture, Image, Text, Volume, Filter, Other }

		internal enum LineKind { Title, Heading, Item, Dim, Failed }

		// One setting on a source as last set; Error is OBS's refusal, null when it took
		internal sealed record Entry(string Source, string Setting, Kind Kind, string Value, DateTime SetUtc, string? Error = null);

		// One swatch of a filter row: its short code, its colour (null while unset), hover text, whether OBS refused it,
		// and whether its row's group is hidden
		internal sealed record Chip(string Code, string? Hex, string ToolTip, bool Failed = false, bool Hidden = false);

		// One line of the dashboard's OBS sources list: Label is the part before the arrow (or a filter row's name), with
		// its hover text and colour swatch if any; a filter row has its swatches in Chips and its age in After
		internal sealed record StatusLine(string Text, LineKind Kind, string? ToolTip = null, string? Swatch = null, IReadOnlyList<Chip>? Chips = null, string? After = null, string? Label = null) {
			// Equal when every field is, the chips compared one by one
			public bool Equals(StatusLine? other) {
				return other != null && Text == other.Text && Kind == other.Kind && ToolTip == other.ToolTip && Swatch == other.Swatch && After == other.After
					&& Label == other.Label && (Chips == null ? other.Chips == null : other.Chips != null && Chips.SequenceEqual(other.Chips));
			}

			// Hashes the fields Equals compares, the chips by count
			public override int GetHashCode() {
				return HashCode.Combine(Text, Kind, ToolTip, Swatch, After, Label, Chips?.Count);
			}
		}

		// Whether the bot last showed or hid an item in a scene
		internal sealed record Visibility(string Scene, string Item, bool Enabled);

		// A filter row's swatch: its code, and the source and filter it shows
		sealed record ChipSpec(string Code, string Source, string Filter);

		// The filter rows, each with the group that shows its sources: the layout colours, their fade twins, the split
		// list highlights and the previous split
		static readonly (string Label, string Group, ChipSpec[] Chips)[] _filterRows = [
			("Layout", LayoutColoring.COLORABLES, LayoutChips("")),
			("Fade", LayoutColoring.COLORABLES + LayoutColoring.TWIN, LayoutChips(LayoutColoring.TWIN)),
			("Highlights", SplitColors.HIGHLIGHT_GROUP, [
				new("AG", Highlight("ListGainedAhead"), SplitColors.HIGHLIGHT_FILTER),
				new("AL", Highlight("ListLostAhead"), SplitColors.HIGHLIGHT_FILTER),
				new("BG", Highlight("ListGainedBehind"), SplitColors.HIGHLIGHT_FILTER),
				new("BL", Highlight("ListLostBehind"), SplitColors.HIGHLIGHT_FILTER),
				new("Au", Highlight("ListGold"), SplitColors.HIGHLIGHT_FILTER),
			]),
			("Previous", SplitColors.PREVIOUS_GROUP, [
				new("Run", SplitColors.PREVIOUS_RUN, SplitColors.PREVIOUS_FILTER),
				new("Seg", SplitColors.PREVIOUS_SEGMENT, SplitColors.PREVIOUS_FILTER),
				new("Au", SplitColors.PREVIOUS_GOLD, SplitColors.PREVIOUS_FILTER),
			]),
		];

		// Sources that join their main source's swatch when set to the same colour
		static readonly (string Attachment, string Main)[] _attachments = [
			(LayoutColoring.INNER_ATTACHMENT, LayoutColoring.INNER),
			(LayoutColoring.OUTER_ATTACHMENT, LayoutColoring.OUTER),
			(LayoutColoring.INNER_ATTACHMENT + LayoutColoring.TWIN, LayoutColoring.INNER + LayoutColoring.TWIN),
			(LayoutColoring.OUTER_ATTACHMENT + LayoutColoring.TWIN, LayoutColoring.OUTER + LayoutColoring.TWIN),
		];

		// The inner, outer and text swatches, of the real sources or their twins
		static ChipSpec[] LayoutChips(string twin) => [
			new("In", LayoutColoring.INNER + twin, LayoutColoring.FILTER),
			new("Out", LayoutColoring.OUTER + twin, LayoutColoring.FILTER),
			new("Txt", LayoutColoring.TEXT_SCENE + twin, LayoutColoring.FILTER),
		];

		// The split list highlight source that takes a split colour key
		static string Highlight(string key) => SplitColors.Highlights.First(h => h.Key == key).Source;

		// The last entry per source and setting
		internal sealed class Store {
			readonly object _lock = new();
			readonly Dictionary<(string Source, string Setting), Entry> _entries = new();
			readonly Dictionary<(string Scene, string Item), bool> _visible = new();
			readonly Dictionary<string, bool> _showing = new();

			// Records a setting OBS took
			internal void Set(string source, string setting, Kind kind, string value, DateTime nowUtc) {
				Put(new Entry(source, setting, kind, value, nowUtc));
			}

			// Records a setting OBS refused
			internal void Failed(string source, string setting, Kind kind, string value, string error, DateTime nowUtc) {
				Put(new Entry(source, setting, kind, value, nowUtc, error));
			}

			// Records an item OBS showed or hid in a scene
			internal void SetVisible(string scene, string item, bool enabled) {
				lock (_lock) {
					_visible[(scene, item)] = enabled;
				}
			}

			// A copy of the entries
			internal List<Entry> Snapshot() {
				lock (_lock) {
					return _entries.Values.ToList();
				}
			}

			// A copy of what was last shown or hidden
			internal List<Visibility> Visible() {
				lock (_lock) {
					return _visible.Select(v => new Visibility(v.Key.Scene, v.Key.Item, v.Value)).ToList();
				}
			}

			// Records whether the source was last found showing in the stream layout
			internal void SetShowing(string source, bool showing) {
				lock (_lock) {
					_showing[source] = showing;
				}
			}

			// A copy of whether each source was last found showing in the stream layout
			internal Dictionary<string, bool> Showing() {
				lock (_lock) {
					return new Dictionary<string, bool>(_showing);
				}
			}

			// Replaces the entry for its source and setting
			void Put(Entry entry) {
				lock (_lock) {
					_entries[(entry.Source, entry.Setting)] = entry;
				}
			}
		}

		// What the OBS wrappers record into
		internal static readonly Store Current = new();

		// File types an image source shows; any other file is a text source's
		static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase) {
			".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".webp", ".jxr", ".psd",
		};

		// The kind of an input setting by its key, and for a file by its type: an image, or a text read from file
		internal static Kind KindOf(string key, string value) => key switch {
			"window" => Kind.Capture,
			"file" => _imageExtensions.Contains(Path.GetExtension(value)) ? Kind.Image : Kind.Text,
			"text" => Kind.Text,
			_ => Kind.Other,
		};

		// Whether the entry sets a file, an image's or a text's
		static bool IsFile(Entry entry) => entry.Setting == "file";

		// "#rrggbb" from OBS's 0xAABBGGRR colour
		internal static string ColorHex(long abgr) {
			long r = abgr & 0xFF;
			long g = (abgr >> 8) & 0xFF;
			long b = (abgr >> 16) & 0xFF;
			return $"#{r:x2}{g:x2}{b:x2}";
		}

		// The value as listed: a capture's executable, a file's name, a text set directly in quotes on one line, a volume
		// in dB
		internal static string Shown(Entry entry) {
			if (entry.Kind == Kind.Text && !IsFile(entry)) {
				return $"\"{entry.Value.ReplaceLineEndings(" ⏎ ")}\"";
			}
			string shown = entry.Kind switch {
				Kind.Capture => entry.Value[(entry.Value.LastIndexOf(':') + 1)..],
				_ when IsFile(entry) => Path.GetFileName(entry.Value),
				Kind.Volume => $"{entry.Value} dB",
				_ => entry.Value,
			};
			return shown.Length == 0 ? "(empty)" : shown;
		}

		// "12 s ago", "5 m ago", "2 h ago", "3 d ago"
		internal static string Ago(TimeSpan age) {
			if (age < TimeSpan.Zero) {
				age = TimeSpan.Zero;
			}
			if (age.TotalSeconds < 60) {
				return $"{(int)age.TotalSeconds} s ago";
			}
			if (age.TotalMinutes < 60) {
				return $"{(int)age.TotalMinutes} m ago";
			}
			if (age.TotalHours < 24) {
				return $"{(int)age.TotalHours} h ago";
			}
			return $"{(int)age.TotalDays} d ago";
		}

		// Room kept for a line's age when shortening it to fit: " · 59 s ago"
		const int AGO_WIDTH = 11;

		// How often the image and text sources are checked for showing in the stream layout
		static readonly TimeSpan SHOWING_POLL = TimeSpan.FromSeconds(2);

		// An item in a scene or group: its source, whether it's enabled, and whether it's a scene or group with items of
		// its own
		internal sealed record SceneItem(string Source, bool Enabled, bool IsScene, bool IsGroup);

		// Starts checking, while OBS is ready, which image and text sources are showing in the stream layout
		internal static void Start() {
			_ = WatchShowingAsync();
		}

		// Every SHOWING_POLL, lists what's showing in the stream layout scene and records, for each image and text source
		// the bot has set, whether it's among it; left as it was when the scene can't be listed
		static async Task WatchShowingAsync() {
			while (true) {
				try {
					if (ObsConnection.IsReady && ShowingIn(GameScene.STREAM, Obs.SceneItems) is HashSet<string> showing) {
						foreach (string source in Current.Snapshot().Where(e => e.Kind is Kind.Image or Kind.Text).Select(e => e.Source).Distinct()) {
							Current.SetShowing(source, showing.Contains(source));
						}
					}
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error OBI1");
					ConsoleLogger.LogToFile(ex);
				}
				await Task.Delay(SHOWING_POLL);
			}
		}

		// The sources showing in the scene: its enabled items, and the enabled items of the enabled scenes and groups in it,
		// however deep, each scene or group listed once (list takes a name and whether it's a group); null when the scene
		// itself can't be listed
		internal static HashSet<string>? ShowingIn(string scene, Func<string, bool, List<SceneItem>?> list) {
			if (list(scene, false) is not List<SceneItem> root) {
				return null;
			}
			HashSet<string> showing = new();
			HashSet<string> listed = new() { scene };
			Stack<List<SceneItem>> pending = new([root]);
			while (pending.Count > 0) {
				foreach (SceneItem item in pending.Pop()) {
					if (!item.Enabled) {
						continue;
					}
					showing.Add(item.Source);
					if ((item.IsScene || item.IsGroup) && listed.Add(item.Source) && list(item.Source, item.IsGroup) is List<SceneItem> inner) {
						pending.Push(inner);
					}
				}
			}
			return showing;
		}

		// The dashboard's lines: a title, then the captures, the filter rows, the images and texts by source, and the
		// rest. A filter row whose group was last hidden is dim, and so is a text or image last found not showing in the
		// stream layout; the names of sources set to a file (images, and texts read from file) are shortened to fit a line of
		// width characters
		internal static List<StatusLine> StatusLines(IReadOnlyList<Entry> entries, bool obsReady, DateTime nowUtc, IReadOnlyList<Visibility>? visible = null, int? width = null,
				IReadOnlyDictionary<string, bool>? showing = null) {
			List<StatusLine> lines = [new StatusLine(obsReady ? "OBS sources" : "OBS sources (OBS not connected)", LineKind.Title)];
			if (entries.Count == 0) {
				lines.Add(new StatusLine("Nothing set yet", LineKind.Item));
				return lines;
			}
			HashSet<string> captures = entries.Where(e => e.Kind == Kind.Capture).Select(e => e.Source).ToHashSet();
			Dictionary<string, Entry> volumes = entries.Where(e => e.Kind == Kind.Volume).ToDictionary(e => e.Source);
			Group(lines, "Captures", CaptureLines(entries.Where(e => e.Kind == Kind.Capture), volumes, nowUtc));
			Group(lines, "Filters", FilterLines(entries.Where(e => e.Kind == Kind.Filter).ToList(), visible ?? [], nowUtc));
			Group(lines, "Images", BySource(entries.Where(e => e.Kind == Kind.Image)).Select(e => NotShowing(Line(e, null, nowUtc, width), showing?.GetValueOrDefault(e.Source, true) == false)));
			Group(lines, "Text", BySource(entries.Where(e => e.Kind == Kind.Text)).Select(e => NotShowing(Line(e, null, nowUtc, width), showing?.GetValueOrDefault(e.Source, true) == false)));
			Group(lines, "Other", BySource(entries.Where(e => e.Kind == Kind.Other || (e.Kind == Kind.Volume && !captures.Contains(e.Source))))
				.Select(e => Line(e, null, nowUtc)));
			return lines;
		}

		// The line dim, with a note on hover, when its source isn't showing in the stream layout; a failed line stays as it is
		static StatusLine NotShowing(StatusLine line, bool notShowing) {
			if (!notShowing || line.Kind != LineKind.Item) {
				return line;
			}
			string note = $"not showing in {GameScene.STREAM}";
			return line with { Kind = LineKind.Dim, ToolTip = line.ToolTip == null ? note : $"{line.ToolTip}{Environment.NewLine}{note}" };
		}

		// Adds the heading and the group's lines, or nothing when it has none
		static void Group(List<StatusLine> lines, string heading, IEnumerable<StatusLine> group) {
			List<StatusLine> list = group.ToList();
			if (list.Count == 0) {
				return;
			}
			lines.Add(new StatusLine(heading, LineKind.Heading));
			lines.AddRange(list);
		}

		// The entries sorted by source, then setting
		static IEnumerable<Entry> BySource(IEnumerable<Entry> entries) {
			return entries.OrderBy(e => e.Source, StringComparer.Ordinal).ThenBy(e => e.Setting, StringComparer.Ordinal);
		}

		// Each used capture slot with its volume, then the unused ones together on one dim line; a failed slot stays apart
		static IEnumerable<StatusLine> CaptureLines(IEnumerable<Entry> captures, Dictionary<string, Entry> volumes, DateTime nowUtc) {
			List<Entry> sorted = BySource(captures).ToList();
			List<Entry> unused = sorted.Where(e => e.Error == null && Shown(e).Equals(GameCapture.NONE, StringComparison.OrdinalIgnoreCase)).ToList();
			foreach (Entry entry in sorted.Except(unused)) {
				yield return Line(entry, volumes.GetValueOrDefault(entry.Source), nowUtc);
			}
			if (unused.Count > 0) {
				yield return NoneLine(unused, nowUtc);
			}
		}

		// "<kind> <slots>; … → none", slots grouped under their kind's short name, aged by the newest
		static StatusLine NoneLine(List<Entry> unused, DateTime nowUtc) {
			List<(string Kind, int Slot)> slots = [];
			List<string> unnumbered = [];
			foreach (Entry entry in unused) {
				int space = entry.Source.LastIndexOf(' ');
				if (space > 0 && int.TryParse(entry.Source[(space + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int slot)) {
					slots.Add((GameCapture.KindName(entry.Source[..space]), slot));
				}
				else {
					unnumbered.Add(entry.Source);
				}
			}
			IEnumerable<string> parts = slots.Count > 0 ? [GameCapture.SlotGroups(slots), .. unnumbered] : unnumbered;
			string label = string.Join("; ", parts);
			DateTime last = unused.Max(e => e.SetUtc);
			return new StatusLine($"{label} → {GameCapture.NONE} · {Ago(nowUtc - last)}", LineKind.Dim, Label: label);
		}

		// The swatch rows that have anything set, then every filter they don't show, verbose and sorted by source
		static List<StatusLine> FilterLines(List<Entry> filters, IReadOnlyList<Visibility> visible, DateTime nowUtc) {
			Dictionary<(string Source, string Filter), Entry> byFilter = new();
			foreach (Entry entry in filters) {
				byFilter.TryAdd((entry.Source, FilterOf(entry)), entry);
			}
			HashSet<Entry> shown = new();
			List<StatusLine> lines = [];
			foreach (var (label, group, specs) in _filterRows) {
				bool hidden = !IsShown(group, visible);
				string hiddenNote = hidden ? $"{Environment.NewLine}{group} hidden" : "";
				List<Chip> chips = [];
				DateTime? newest = null;
				foreach (ChipSpec spec in specs) {
					if (!byFilter.TryGetValue((spec.Source, spec.Filter), out Entry? entry)) {
						chips.Add(new Chip(spec.Code, null, $"{spec.Source} · {spec.Filter}: not set{hiddenNote}", Hidden: hidden));
						continue;
					}
					shown.Add(entry);
					string toolTip = Undated(entry);
					foreach (var (attachment, _) in _attachments.Where(a => a.Main == spec.Source)) {
						if (byFilter.TryGetValue((attachment, spec.Filter), out Entry? joined) && joined.Error == null && joined.Value == entry.Value) {
							shown.Add(joined);
							toolTip += Environment.NewLine + Undated(joined);
						}
					}
					chips.Add(new Chip(spec.Code, IsHex(entry.Value) ? entry.Value : null, toolTip + hiddenNote, entry.Error != null, hidden));
					newest = newest is DateTime n && n > entry.SetUtc ? n : entry.SetUtc;
				}
				if (newest is DateTime last) {
					lines.Add(new StatusLine(label, hidden ? LineKind.Dim : LineKind.Item, Chips: chips, After: $"· {Ago(nowUtc - last)}", Label: label));
				}
			}
			lines.AddRange(BySource(filters.Where(e => !shown.Contains(e))).Select(e => Line(e, null, nowUtc)));
			return lines;
		}

		// Whether the bot last showed the group, by its name in any scene, or as a left-panel group under either prefix and
		// in any copy; true when it never showed or hid it
		static bool IsShown(string group, IReadOnlyList<Visibility> visible) {
			List<Visibility> matching = visible.Where(v => GroupOf(v) == group).ToList();
			return matching.Count == 0 || matching.Any(v => v.Enabled);
		}

		// The item's group name, without a left-panel prefix or copy number
		static string GroupOf(Visibility item) {
			string name = LeftPanel.GroupName(item.Item, item.Scene) ?? item.Item;
			return LeftPanel.Numbered(name)?.Base ?? name;
		}

		// A filter entry's filter name, the part of its setting before the property
		static string FilterOf(Entry entry) {
			int separator = entry.Setting.IndexOf(" · ", StringComparison.Ordinal);
			return separator < 0 ? entry.Setting : entry.Setting[..separator];
		}

		// "<source> → <shown>", or "<source> · <setting> → <shown>" for filters and others, then volume, failure and age;
		// the part before the arrow is the label, and a file's (an image's or a text's) is shortened to fit width characters
		static StatusLine Line(Entry entry, Entry? volume, DateTime nowUtc, int? width = null) {
			var (label, rest, error) = Describe(entry, volume);
			DateTime last = volume != null && volume.SetUtc > entry.SetUtc ? volume.SetUtc : entry.SetUtc;
			string ago = $" · {Ago(nowUtc - last)}";
			if (IsFile(entry) && width is int chars) {
				label = Abbreviate(label, chars - rest.Length - Math.Max(ago.Length, AGO_WIDTH));
			}
			string? toolTip = !IsFile(entry) ? null : entry.Value.Length > 0 ? $"{entry.Source}{Environment.NewLine}{entry.Value}" : entry.Source;
			string? swatch = entry.Kind == Kind.Filter && IsHex(entry.Value) ? entry.Value : null;
			return new StatusLine(label + rest + ago, error != null ? LineKind.Failed : LineKind.Item, toolTip, swatch, Label: label);
		}

		// A line without its age, split at the arrow, and the failure in it if any
		static (string Label, string Tail, string? Error) Describe(Entry entry, Entry? volume) {
			bool named = entry.Kind is Kind.Capture or Kind.Image or Kind.Text;
			string label = named ? entry.Source : $"{entry.Source} · {entry.Setting}";
			string rest = $" → {Shown(entry)}";
			if (volume != null) {
				rest += $" · {Shown(volume)}";
			}
			string? error = entry.Error ?? volume?.Error;
			if (error != null) {
				rest += $" · failed: {error}";
			}
			return (label, rest, error);
		}

		// An entry's line without its age, for a tooltip that shouldn't change every second
		static string Undated(Entry entry) {
			var (label, rest, _) = Describe(entry, null);
			return label + rest;
		}

		// The name shortened to the length, word by word from the last, each to its first letter with the brackets and
		// punctuation around it: "Border: Colorable Outer (Transitionary)" → "Border: Colorable O (T)"; all words
		// shortened when it still doesn't fit
		internal static string Abbreviate(string name, int length) {
			if (name.Length <= length) {
				return name;
			}
			string[] words = name.Split(' ');
			for (int i = words.Length - 1; i >= 0 && string.Join(' ', words).Length > length; i--) {
				words[i] = Initial(words[i]);
			}
			return string.Join(' ', words);
		}

		// A word's first letter or digit, keeping what comes before it and the punctuation after: "(Transitionary)" → "(T)",
		// "Border:" → "B:"; a word without either as it is
		static string Initial(string word) {
			int first = 0;
			while (first < word.Length && !char.IsLetterOrDigit(word[first])) {
				first++;
			}
			if (first == word.Length) {
				return word;
			}
			int end = word.Length;
			while (end > first + 1 && !char.IsLetterOrDigit(word[end - 1])) {
				end--;
			}
			return word[..(first + 1)] + word[end..];
		}

		// Whether the text is "#rrggbb"
		static bool IsHex(string text) {
			return text.Length == 7 && text[0] == '#' && text[1..].All(Uri.IsHexDigit);
		}
	}
}
