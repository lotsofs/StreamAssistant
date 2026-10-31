using System.Text.RegularExpressions;

namespace StreamAssistant2 {
	internal static class SplitList {
		// The completed splits' list under LivesplitGenerated, and the levels' folders
		const string COMPLETED_FOLDER = @"CompletedSplits\AllSplits";
		const string ALL_SPLITS = "AllSplits";
		const string SANS_SUBSPLITS = "SansSubsplits";
		const string MIDDLE_LEVEL = "SansSubsplits_Level";
		// Each level's files
		const string NAMES = "Names.txt";
		const string NAMES_RAW = "Names_Raw.txt";
		const string NAMES_INDENT = "Names_Indent_{0}.txt";
		const string CURRENT_HIGHLIGHT = "Highlight_CurrentSplit.txt";
		const string SCROLLED_HIGHLIGHT = "Highlight_ScrolledToSplit.txt";
		const string GOLD_HIGHLIGHT = @"RealTime\Highlight_Gold.txt";
		// A comparison's files in a level, by its folder
		const string RUN_DELTA = @"RealTime\{0}\Delta_Run.txt";
		const string RUN_UPCOMING = @"RealTime\{0}\Time_Run_Upcoming.txt";
		const string SEGMENT_DELTA = @"RealTime\{0}\Delta_Segment.txt";
		const string SEGMENT_UPCOMING = @"RealTime\{0}\Time_Segment_Upcoming.txt";
		// The comparisons' folders in AllSplits, and the name of the one selected in LiveSplit, under LivesplitGenerated
		const string COMPARISONS_FOLDER = @"SplitList\AllSplits\RealTime";
		const string CURRENT_NAME = "CurrentComparison.txt";
		// The folder of the comparison selected in LiveSplit, and the choice that shows each comparison in turn
		internal const string CURRENT = "CurrentComparison";
		internal const string CYCLE = "";
		// The headings of the run and segment columns
		const string RUN_HEADER = "Run";
		const string SEGMENT_HEADER = "Segment";
		// A level's current split number and completed count, and its member count, under LivesplitGenerated
		const string LEVEL_NUMBER = @"CurrentSplit\{0}\Number.txt";
		const string LEVEL_COMPLETED = @"CurrentSplit\{0}\Completed.txt";
		const string LEVEL_TOTAL = "TotalSplits_{0}.txt";
		// A line for splits not shown, and its hover text
		const string MORE_TEXT = "…";
		const string MORE_TOOLTIP = "More splits here than any level's window shows";
		// What ends a name cut short
		const string CUT_TEXT = "…";
		// The column's notes
		const string NOT_SET_NOTE = "LivesplitGenerated not set";
		const string NO_FOLDER_NOTE = "no {0} folder";
		const string LINE_UP_NOTE = "levels don't line up";
		// Failed merges in a row before the column says so
		const int MISSES_BEFORE_NOTE = 3;

		// How often LiveSplit's split lists are read
		static readonly TimeSpan POLL = TimeSpan.FromMilliseconds(500);

		// How a completed split went: a gold, else ahead or behind the comparison, having gained or lost time on its segment
		internal enum Highlight { None, Gold, AheadGained, AheadLost, BehindGained, BehindLost }

		// The split lists the column can show: each level's window around the current or scrolled-to split, the completed
		// splits, the splits not yet completed, and every split
		internal enum ListKind { Surrounding, Completed, Remaining, Full }

		// A split list's folder under LivesplitGenerated, and whether it has completed lines, upcoming lines (the current
		// split's among them) and a scrolled-to line; it leaves out the files only those fill
		sealed record ListFiles(string Folder, bool Completed, bool Upcoming, bool Scrolls);

		// Each list's files
		static readonly Dictionary<ListKind, ListFiles> LISTS = new() {
			[ListKind.Surrounding] = new("SplitList", true, true, true),
			[ListKind.Completed] = new("CompletedSplits", true, false, false),
			[ListKind.Remaining] = new("RemainingSplits", false, true, false),
			[ListKind.Full] = new("FullSplitList", true, true, false),
		};

		// A comparison's highlight files, by its folder, and the highlight each marks
		static readonly (Highlight Kind, string File)[] COMPARISON_HIGHLIGHTS = [
			(Highlight.AheadGained, @"RealTime\{0}\Highlight_AheadGained.txt"),
			(Highlight.AheadLost, @"RealTime\{0}\Highlight_AheadLost.txt"),
			(Highlight.BehindGained, @"RealTime\{0}\Highlight_BehindGained.txt"),
			(Highlight.BehindLost, @"RealTime\{0}\Highlight_BehindLost.txt"),
		];

		// One line of a level's window as LiveSplit wrote it: the split's raw and shown name, its depth, whether it's the
		// level's current or scrolled-to line, its highlight, its run and segment deltas (completed) and its comparison's
		// run and segment times (upcoming)
		internal sealed record ListLine(string Raw, string Name, int Depth, bool Current, bool ScrolledTo, Highlight Highlight, string Delta, string Upcoming,
			string SegmentDelta = "", string SegmentUpcoming = "");

		// A level's window: the level's folder, the deepest split it holds, its lines, the member index of its first line in
		// the level (null when unknown), and the level's member count (0 when unknown)
		internal sealed record Window(string Level, int Depth, IReadOnlyList<ListLine> Lines, int? First = null, int Total = 0);

		// The run's completed splits as CompletedSplits\AllSplits lists them, in run order: each one's raw name and depth
		internal sealed record Completed(IReadOnlyList<string> Raws, IReadOnlyList<int> Depths);

		// A time on a row, as LiveSplit wrote it: a delta when the split is completed, else the comparison's time, with the
		// highlight it takes
		internal sealed record Cell(string Text, bool Completed = false, Highlight Highlight = Highlight.None);

		// One line of a split: a name it has at one or more levels in a row with the same segment, the first of those levels,
		// that segment's time (null when none of those levels' windows holds the split), and whether one of those levels has
		// it as its current split
		internal sealed record Part(string Name, int Level, Cell? Segment, bool Current = false);

		// A split in the merged list: its raw name, its depth, whether it's the current or scrolled-to split, its run time,
		// its lines (its own name first, then each section it closes, innermost first), the levels showing it, the level of
		// the splits not shown just before it (none when there are none), and whether splits not shown follow it at the end
		internal sealed record Row(string Raw, int Depth, bool Current, bool ScrolledTo, Cell Run, IReadOnlyList<Part> Parts, IReadOnlyList<string> Levels,
			int? MoreBefore = null, bool MoreAfter = false);

		// What the column shows: the merged rows, the levels' folders (coarsest first), a note on why there are none or
		// they're old (a problem when it's red), the folders of the comparisons written, the folder of the one read, the
		// name of the one selected in LiveSplit, and the list read
		internal sealed record Snapshot(IReadOnlyList<Row> Rows, IReadOnlyList<string> Levels, string? Note = null, bool Problem = false,
			IReadOnlyList<string>? Comparisons = null, string Shown = CURRENT, string CurrentName = "", ListKind List = ListKind.Surrounding);

		// What a line of the splits column is: the title, the title with a problem, the column headings, a split's outermost
		// line or one inside it, a line holding the current split at its level (a split's outermost or one inside it), the
		// scrolled-to split's outermost, splits not shown, or the root the tree hangs from
		internal enum LineKind { Title, Problem, Header, Outer, Inner, Selected, SelectedInner, ScrolledTo, More, Root }

		// One line of the dashboard's splits column: its name (or the title), the run and segment times (or the headings;
		// none on a title), the level it nests at, the hover text, whether it's marked as holding the current split or as the
		// scrolled-to split, the tree guides before its name, whether its run time repeats its split's outermost line's, and
		// the strokes of its guides on the path to the current split, at their places over the guides
		internal sealed record StatusLine(string Text, LineKind Kind, Cell? Run = null, Cell? Segment = null, int Level = 0, string? ToolTip = null, bool Marked = false,
			string Guides = "", bool RepeatsRun = false, string Path = "");

		// The width of a level in the tree guides
		const int GUIDE_WIDTH = 3;

		// The last read
		static volatile Snapshot? _current;
		// The dashboard's choice of comparison, the folder last read, and when the cycle last moved on
		static volatile string _choice = CURRENT;
		static string _shown = CURRENT;
		static DateTime _movedAt;
		// The dashboard's choice of list
		static volatile ListKind _list = ListKind.Surrounding;

		/// <summary>
		/// The last read of LiveSplit's split lists; null before the first.
		/// </summary>
		internal static Snapshot? Current => _current;

		/// <summary>
		/// The comparison the dashboard shows: a written comparison's folder, CURRENT, or CYCLE.
		/// </summary>
		internal static string Choice {
			get => _choice;
			set => _choice = value;
		}

		/// <summary>
		/// The list the dashboard shows.
		/// </summary>
		internal static ListKind ListChoice {
			get => _list;
			set => _list = value;
		}

		/// <summary>
		/// Starts reading LiveSplit's split lists every POLL.
		/// </summary>
		internal static void Start() {
			_ = WatchAsync();
		}

		/// <summary>
		/// Every POLL, reads and merges the split lists; logs a failure once until a read succeeds again.
		/// </summary>
		static async Task WatchAsync() {
			int misses = 0;
			bool failed = false;
			while (true) {
				try {
					misses = ReadOnce(misses);
					failed = false;
				}
				catch (Exception ex) {
					if (!failed) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LSL1");
						ConsoleLogger.LogToFile(ex);
					}
					failed = true;
				}
				await Task.Delay(POLL);
			}
		}

		/// <summary>
		/// Reads the chosen list's levels under LivesplitGenerated against the comparison the choice resolves to and
		/// publishes the merged rows, or a note when there's no folder; a merge that fails keeps the last rows, with a note
		/// once it has failed MISSES_BEFORE_NOTE times in a row. A choice no longer written falls back to CURRENT. Returns
		/// the failures in a row.
		/// </summary>
		static int ReadOnce(int misses) {
			string directory = Settings.Data.Directories.LivesplitGenerated;
			if (string.IsNullOrEmpty(directory)) {
				_current = new Snapshot([], [], NOT_SET_NOTE);
				return 0;
			}
			ListKind list = _list;
			if (!Directory.Exists(Path.Combine(directory, LISTS[list].Folder))) {
				_current = new Snapshot([], [], string.Format(NO_FOLDER_NOTE, LISTS[list].Folder), List: list);
				return 0;
			}
			string currentName = ReadShared(Path.Combine(directory, CURRENT_NAME)).Trim();
			string comparisons = Path.Combine(directory, COMPARISONS_FOLDER);
			List<string> written = Written(Directory.Exists(comparisons) ? Directory.GetDirectories(comparisons).Select(Path.GetFileName).OfType<string>() : [],
				comparison => ReadShared(Path.Combine(directory, LISTS[ListKind.Surrounding].Folder, ALL_SPLITS, string.Format(RUN_UPCOMING, comparison))));
			string chosen = _choice;
			(_shown, _movedAt, string choice) = Resolve(chosen, written, currentName, _shown, _movedAt, DateTime.UtcNow,
				TimeSpan.FromSeconds(Settings.Data.SplitList.CycleSeconds));
			if (choice != chosen) {
				Interlocked.CompareExchange(ref _choice, choice, chosen);
			}
			bool finished = Finished(ReadShared(Path.Combine(directory, string.Format(LEVEL_NUMBER, ALL_SPLITS))),
				ReadShared(Path.Combine(directory, string.Format(LEVEL_COMPLETED, ALL_SPLITS))));
			if (ReadLevels(directory, finished, _shown, list) is (List<Window> levels, Completed completed) && Merge(levels, finished, completed) is List<Row> rows) {
				_current = new Snapshot(rows, levels.Select(window => window.Level).ToList(), null, false, written, _shown, currentName, list);
				return 0;
			}
			misses++;
			if (misses >= MISSES_BEFORE_NOTE) {
				_current = new Snapshot(_current?.Rows ?? [], _current?.Levels ?? [], LINE_UP_NOTE, true, written, _shown, currentName, _current?.List ?? list);
			}
			return misses;
		}

		/// <summary>
		/// The comparisons' folders other than CURRENT whose run times file in AllSplits has text (read takes a folder and
		/// gives that file's text), sorted ignoring case.
		/// </summary>
		internal static List<string> Written(IEnumerable<string> folders, Func<string, string> read) {
			return folders.Where(folder => !folder.Equals(CURRENT, StringComparison.OrdinalIgnoreCase) && read(folder).Length > 0)
				.Order(StringComparer.OrdinalIgnoreCase).ToList();
		}

		/// <summary>
		/// The comparisons a cycle goes through: the written ones, after CURRENT when the comparison selected in LiveSplit
		/// has no folder among them.
		/// </summary>
		internal static List<string> CycleList(IReadOnlyList<string> written, string currentName) {
			return written.Contains(ComparisonFolder(currentName), StringComparer.OrdinalIgnoreCase) ? [.. written] : [CURRENT, .. written];
		}

		/// <summary>
		/// The comparison to read for the choice, when the cycle last moved on, and the choice. A folder is read while it's
		/// written, else CURRENT, which becomes the choice; outside a cycle, the cycle counts as moving on now. On CYCLE,
		/// the one shown stays until the interval has passed, then the next in the cycle list follows; one not in the list
		/// (CURRENT counting as its comparison's folder) gives way at once to the list's first.
		/// </summary>
		internal static (string Shown, DateTime MovedAt, string Choice) Resolve(string choice, IReadOnlyList<string> written, string currentName, string shown,
			DateTime movedAt, DateTime now, TimeSpan interval) {
			if (choice != CYCLE) {
				string folder = choice == CURRENT || written.Contains(choice) ? choice : CURRENT;
				return (folder, now, folder);
			}
			List<string> cycle = CycleList(written, currentName);
			int index = cycle.IndexOf(shown);
			if (index < 0 && shown == CURRENT) {
				index = cycle.FindIndex(folder => folder.Equals(ComparisonFolder(currentName), StringComparison.OrdinalIgnoreCase));
			}
			if (index < 0) {
				return (cycle[0], now, CYCLE);
			}
			if (now - movedAt < interval) {
				return (cycle[index], movedAt, CYCLE);
			}
			return (cycle[(index + 1) % cycle.Count], now, CYCLE);
		}

		/// <summary>
		/// A comparison's folder as Output to File names it: without its spaces, with '_' for each character a folder name
		/// can't have and for trailing dots; "_" when nothing is left.
		/// </summary>
		internal static string ComparisonFolder(string name) {
			char[] folder = name.Replace(" ", "").ToCharArray();
			char[] invalid = Path.GetInvalidFileNameChars();
			for (int i = 0; i < folder.Length; i++) {
				if (Array.IndexOf(invalid, folder[i]) >= 0) {
					folder[i] = '_';
				}
			}
			for (int i = folder.Length - 1; i >= 0 && folder[i] == '.'; i--) {
				folder[i] = '_';
			}
			return folder.Length == 0 ? "_" : new string(folder);
		}

		/// <summary>
		/// A comparison's folder as a name: a space wherever a lowercase letter or digit meets an uppercase one.
		/// </summary>
		internal static string ComparisonName(string folder) {
			return Regex.Replace(folder, @"(?<=[\p{Ll}\d])(?=\p{Lu})", " ");
		}

		/// <summary>
		/// Every level of the list LiveSplit writes under LivesplitGenerated, coarsest first: SansSubsplits,
		/// SansSubsplits_Level2 and up while each has members, then AllSplits, each against the comparison in the folder and
		/// with where its window starts in the level (from the completed splits when its current line doesn't say) and the
		/// level's member count; and the completed splits. Null when a level can't be read whole.
		/// </summary>
		static (List<Window> Levels, Completed Completed)? ReadLevels(string directory, bool finished, string comparison, ListKind list) {
			string folder = Path.Combine(directory, LISTS[list].Folder);
			List<string> names = [];
			if (LevelTotal(directory, SANS_SUBSPLITS) > 0) {
				names.Add(SANS_SUBSPLITS);
				for (int k = 2; LevelTotal(directory, MIDDLE_LEVEL + k) > 0; k++) {
					names.Add(MIDDLE_LEVEL + k);
				}
			}
			names.Add(ALL_SPLITS);
			List<Window> levels = [];
			for (int depth = 0; depth < names.Count; depth++) {
				string level = Path.Combine(folder, names[depth]);
				if (ParseLevel(names[depth], depth, file => ReadShared(Path.Combine(level, file)), comparison, list) is not Window window) {
					return null;
				}
				int total = LevelTotal(directory, names[depth]);
				string number = ReadShared(Path.Combine(directory, string.Format(LEVEL_NUMBER, names[depth])));
				levels.Add(window with { First = FirstMember(window, number, finished, total), Total = total });
			}
			string completedFolder = Path.Combine(directory, COMPLETED_FOLDER);
			Completed completed = ParseCompleted(names.Count, file => ReadShared(Path.Combine(completedFolder, file))) ?? new Completed([], []);
			return (levels.Select(window => window.First == null ? window with { First = FirstFromCompleted(window, completed) } : window).ToList(), completed);
		}

		/// <summary>
		/// The level's member count under LivesplitGenerated; 0 when the run hasn't the level or it can't be read.
		/// </summary>
		static int LevelTotal(string directory, string level) {
			return SplitWatcher.ParseIndex(ReadShared(Path.Combine(directory, string.Format(LEVEL_TOTAL, level)))) ?? 0;
		}

		/// <summary>
		/// The completed splits from CompletedSplits\AllSplits' files (read takes a path within that folder), for a run of
		/// that many levels: each one's raw name and depth, from the indent file it's in, else its leading dashes up to the
		/// deepest level; null when the files don't all have a line per split.
		/// </summary>
		internal static Completed? ParseCompleted(int levelCount, Func<string, string> read) {
			string[] raw = Lines(read(NAMES_RAW));
			string[][] indents = Enumerable.Range(0, levelCount).Select(d => Lines(read(string.Format(NAMES_INDENT, d)))).ToArray();
			if (indents.Any(file => file.Length != raw.Length)) {
				return null;
			}
			int[] depths = Enumerable.Range(0, raw.Length).Select(i => Array.FindIndex(indents, file => file[i].Length > 0) is int d and >= 0
				? d : Math.Min(raw[i].TakeWhile(c => c == '-').Count(), levelCount - 1)).ToArray();
			return new Completed(raw, depths);
		}

		/// <summary>
		/// Where the window starts in its level, from the completed splits: the one place in the level's completed members
		/// (those no deeper than the window holds) where its completed lines match by raw name and its other lines come after
		/// them; null when it has no completed line, or the place isn't one.
		/// </summary>
		internal static int? FirstFromCompleted(Window window, Completed completed) {
			if (!window.Lines.Any(line => line.Delta.Length > 0)) {
				return null;
			}
			List<int> members = Members(completed, window.Depth);
			int? found = null;
			for (int start = 0; start <= members.Count; start++) {
				bool fits = true;
				for (int i = 0; i < window.Lines.Count && fits; i++) {
					bool done = start + i < members.Count;
					fits = done == (window.Lines[i].Delta.Length > 0) && (!done || completed.Raws[members[start + i]] == window.Lines[i].Raw);
				}
				if (fits) {
					if (found != null) {
						return null;
					}
					found = start;
				}
			}
			return found;
		}

		/// <summary>
		/// The run indices of the completed splits no deeper than the depth: a level's completed members, in order.
		/// </summary>
		static List<int> Members(Completed completed, int depth) {
			return Enumerable.Range(0, completed.Depths.Count).Where(i => completed.Depths[i] <= depth).ToList();
		}

		/// <summary>
		/// The member index of the window's first line in its level: from the level's current split number when the window
		/// has the current line, else 0 before the start and the level's last lines after the finish; null when scrolled away
		/// from the current split, or when the numbers can't be read or put it before the level's first member.
		/// </summary>
		internal static int? FirstMember(Window window, string number, bool finished, int total) {
			int current = IndexOf(window.Lines, line => line.Current);
			if (current >= 0) {
				return SplitWatcher.ParseIndex(number) is int n && n - 1 >= current ? n - 1 - current : null;
			}
			if (window.Lines.Any(line => line.ScrolledTo)) {
				return null;
			}
			if (!finished) {
				return 0;
			}
			return total > 0 && total >= window.Lines.Count ? total - window.Lines.Count : null;
		}

		/// <summary>
		/// Whether the run is finished: no current split number, and splits completed.
		/// </summary>
		internal static bool Finished(string number, string completed) {
			return number.Trim() == "-" && SplitWatcher.Index(number, completed) > 0;
		}

		/// <summary>
		/// The file's text, read without keeping LiveSplit from writing it; empty when it's missing or can't be read.
		/// </summary>
		internal static string ReadShared(string path) {
			try {
				using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using StreamReader reader = new(stream);
				return reader.ReadToEnd();
			}
			catch (IOException) {
				return "";
			}
			catch (UnauthorizedAccessException) {
				return "";
			}
		}

		/// <summary>
		/// The file's lines, each ended by a line break as LiveSplit writes them; none for an empty file.
		/// </summary>
		internal static string[] Lines(string text) {
			if (text.Length == 0) {
				return [];
			}
			string[] lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
			return text.EndsWith('\n') ? lines[..^1] : lines;
		}

		/// <summary>
		/// A level's window of the list from its files (read takes a path within the level's folder), holding splits up to
		/// the depth, with the times and highlights of the comparison in the folder; a file the list doesn't write counts
		/// as a blank line per split. Null when the files don't all have a line per split. A line's depth is the indent file
		/// it's in, else its raw name's leading dashes up to the depth.
		/// </summary>
		internal static Window? ParseLevel(string level, int depth, Func<string, string> read, string comparison = CURRENT, ListKind list = ListKind.Surrounding) {
			ListFiles has = LISTS[list];
			string[] raw = Lines(read(NAMES_RAW));
			string[] Read(string file, bool written) => written ? Lines(read(file)) : Enumerable.Repeat("", raw.Length).ToArray();
			string[] names = Lines(read(NAMES));
			string[][] indents = Enumerable.Range(0, depth + 1).Select(d => Lines(read(string.Format(NAMES_INDENT, d)))).ToArray();
			string[] current = Read(CURRENT_HIGHLIGHT, has.Upcoming);
			string[] scrolled = Read(SCROLLED_HIGHLIGHT, has.Scrolls);
			string[] gold = Read(GOLD_HIGHLIGHT, has.Completed);
			string[][] highlights = COMPARISON_HIGHLIGHTS.Select(h => Read(string.Format(h.File, comparison), has.Completed)).ToArray();
			string[] delta = Read(string.Format(RUN_DELTA, comparison), has.Completed);
			string[] upcoming = Read(string.Format(RUN_UPCOMING, comparison), has.Upcoming);
			string[] segmentDelta = Read(string.Format(SEGMENT_DELTA, comparison), has.Completed);
			string[] segmentUpcoming = Read(string.Format(SEGMENT_UPCOMING, comparison), has.Upcoming);
			string[][] files = [names, .. indents, current, scrolled, gold, .. highlights, delta, upcoming, segmentDelta, segmentUpcoming];
			if (files.Any(file => file.Length != raw.Length)) {
				return null;
			}
			List<ListLine> lines = [];
			for (int i = 0; i < raw.Length; i++) {
				int lineDepth = Array.FindIndex(indents, file => file[i].Length > 0);
				if (lineDepth < 0) {
					lineDepth = Math.Min(raw[i].TakeWhile(c => c == '-').Count(), depth);
				}
				int marked = Array.FindIndex(highlights, file => file[i].Length > 0);
				Highlight highlight = gold[i].Length > 0 ? Highlight.Gold : marked >= 0 ? COMPARISON_HIGHLIGHTS[marked].Kind : Highlight.None;
				lines.Add(new ListLine(raw[i], names[i], lineDepth, current[i].Length > 0, scrolled[i].Length > 0, highlight, delta[i], upcoming[i],
					segmentDelta[i], segmentUpcoming[i]));
			}
			return new Window(level, depth, lines);
		}

		// A split while merging: its line at the finest level holding it, whether that's AllSplits, the levels holding it, its
		// line and that line's index in each of their windows by level, the depth of the line before it in each of those
		// windows (none when it's first), and the levels where a merged split between it and that line is one level deeper
		sealed class Entry {
			public ListLine Line { get; }
			public bool InAllSplits { get; }
			public List<string> Levels { get; } = [];
			public Dictionary<int, ListLine> ByLevel { get; } = [];
			public Dictionary<int, int> LineIndex { get; } = [];
			public Dictionary<int, int> PreviousDepth { get; } = [];
			public HashSet<int> DeeperBefore { get; } = [];

			public Entry(Window window, int index, int level, bool inAllSplits) {
				Line = window.Lines[index];
				InAllSplits = inAllSplits;
				Add(window, index, level);
			}

			// Records the split's line at another level, the window's line at the index
			public void Add(Window window, int index, int level) {
				Levels.Add(window.Level);
				ByLevel[level] = window.Lines[index];
				LineIndex[level] = index;
				if (index > 0) {
					PreviousDepth[level] = window.Lines[index - 1].Depth;
				}
			}
		}

		/// <summary>
		/// The levels' windows (coarsest first, AllSplits last) merged into one list in run order; null when they don't line
		/// up. Each coarser level's line is placed by its distance from the level's anchor: the same split as a merged one
		/// at its depth, or before or after all of them. The completed splits, when given, place the completed ones in the run.
		/// </summary>
		internal static List<Row>? Merge(IReadOnlyList<Window> levels, bool finished, Completed? completed = null) {
			Window all = levels[^1];
			if (all.Lines.Count == 0) {
				return [];
			}
			bool scrolled = all.Lines.Any(line => line.ScrolledTo);
			if (Disagree(levels, line => line.ScrolledTo) || (!scrolled && Disagree(levels, line => line.Current))) {
				return null;
			}
			List<Entry> merged = Enumerable.Range(0, all.Lines.Count).Select(i => new Entry(all, i, levels.Count - 1, true)).ToList();
			int at = Anchor(all, finished);
			for (int level = levels.Count - 2; level >= 0; level--) {
				Window window = levels[level];
				List<int> members = Enumerable.Range(0, merged.Count).Where(i => merged[i].Line.Depth <= window.Depth).ToList();
				int shift = members.Count(i => i < at) - Anchor(window, finished);
				List<Entry> earlier = [];
				List<Entry> later = [];
				for (int i = 0; i < window.Lines.Count; i++) {
					ListLine line = window.Lines[i];
					int k = i + shift;
					if (k < 0) {
						earlier.Add(new Entry(window, i, level, false));
					}
					else if (k >= members.Count) {
						later.Add(new Entry(window, i, level, false));
					}
					else if (merged[members[k]].Line.Raw != line.Raw) {
						return null;
					}
					else {
						merged[members[k]].Add(window, i, level);
					}
				}
				merged = [.. earlier, .. merged, .. later];
				at += earlier.Count;
			}
			MarkDeeperBefore(merged);
			return MarkHidden(merged.Select(entry => ToRow(entry, levels.Count)).ToList(), merged, levels, completed);
		}

		/// <summary>
		/// Marks where splits not shown lie. Before a row, unless a window shows every split across that boundary or the run
		/// indices the windows allow make it follow the row before directly; before the first row, unless it must be the run's
		/// first split. At the shallowest depth of the splits hidden there when they're all completed and placed, else one
		/// level deeper than the deepest window that rules out shallower ones there. After the last row, at the top level,
		/// unless a window ends at its level's last member, which is the run's last split.
		/// </summary>
		static List<Row> MarkHidden(List<Row> rows, List<Entry> merged, IReadOnlyList<Window> levels, Completed? completed) {
			(int[] lo, int[] hi) = RunIndices(merged, levels, completed);
			int finest = levels.Count - 1;
			for (int j = 0; j < rows.Count; j++) {
				int shown = ShownUpTo(merged, levels, j);
				if (shown < finest && (j == 0 ? hi[0] > 0 : hi[j] - lo[j - 1] > 1)) {
					int from = j == 0 ? -1 : lo[j - 1];
					bool placed = (j == 0 || lo[j - 1] == hi[j - 1]) && lo[j] == hi[j];
					rows[j] = rows[j] with { MoreBefore = placed ? HiddenDepth(from, lo[j], completed) ?? shown + 1 : shown + 1 };
				}
			}
			bool ends = levels.Any(window => window.First is int first && window.Total > 0 && first + window.Lines.Count == window.Total)
				|| (levels[^1].Total > 0 && lo[^1] >= levels[^1].Total - 1);
			if (!ends) {
				rows[^1] = rows[^1] with { MoreAfter = true };
			}
			return rows;
		}

		/// <summary>
		/// The deepest level whose window shows every split of that level across the boundary before the merged split at the
		/// index (the top, for 0): one with lines on both sides, or starting at its level's first member after the boundary, or
		/// ending at its last before it; -1 for none.
		/// </summary>
		static int ShownUpTo(List<Entry> merged, IReadOnlyList<Window> levels, int index) {
			for (int level = levels.Count - 1; level >= 0; level--) {
				Window window = levels[level];
				int first = merged.FindIndex(entry => entry.ByLevel.ContainsKey(level));
				int last = merged.FindLastIndex(entry => entry.ByLevel.ContainsKey(level));
				if (first < 0) {
					continue;
				}
				bool before = first <= index - 1 || window.First == 0;
				bool after = last >= index || (window.First is int start && window.Total > 0 && start + window.Lines.Count == window.Total);
				if (before && after) {
					return level;
				}
			}
			return -1;
		}

		/// <summary>
		/// The shallowest depth of the splits strictly between two run indices (-1 for the run's start), when they're all
		/// completed; null otherwise.
		/// </summary>
		internal static int? HiddenDepth(int from, int to, Completed? completed) {
			if (completed == null || to > completed.Depths.Count || to - from < 2) {
				return null;
			}
			return Enumerable.Range(from + 1, to - from - 1).Min(i => completed.Depths[i]);
		}

		/// <summary>
		/// The lowest and highest run index each merged split can have: at least its member index in each level whose window
		/// start is known, and no closer to the run's end than that level's members after it; exactly the run index of that
		/// member among the completed splits when it's completed; each after the one before.
		/// </summary>
		static (int[] Lo, int[] Hi) RunIndices(List<Entry> merged, IReadOnlyList<Window> levels, Completed? completed) {
			int total = levels[^1].Total;
			List<int>[] done = levels.Select(window => completed == null ? [] : Members(completed, window.Depth)).ToArray();
			int[] lo = new int[merged.Count];
			int[] hi = new int[merged.Count];
			for (int j = 0; j < merged.Count; j++) {
				hi[j] = total > 0 ? total - 1 : int.MaxValue / 2;
				foreach ((int level, int index) in merged[j].LineIndex) {
					if (levels[level].First is not int first) {
						continue;
					}
					int member = first + index;
					lo[j] = Math.Max(lo[j], member);
					if (total > 0 && levels[level].Total > 0) {
						hi[j] = Math.Min(hi[j], total - levels[level].Total + member);
					}
					if (member < done[level].Count) {
						lo[j] = Math.Max(lo[j], done[level][member]);
						hi[j] = Math.Min(hi[j], done[level][member]);
					}
				}
			}
			for (int j = 1; j < merged.Count; j++) {
				lo[j] = Math.Max(lo[j], lo[j - 1] + 1);
			}
			for (int j = merged.Count - 2; j >= 0; j--) {
				hi[j] = Math.Min(hi[j], hi[j + 1] - 1);
			}
			return (lo, hi);
		}

		/// <summary>
		/// For each split and each level whose window has a line before it, the nearest earlier merged split in that window:
		/// marks the level when a merged split between the two is one level deeper.
		/// </summary>
		static void MarkDeeperBefore(List<Entry> merged) {
			for (int j = 0; j < merged.Count; j++) {
				foreach (int level in merged[j].PreviousDepth.Keys) {
					int previous = j - 1;
					while (previous >= 0 && !merged[previous].ByLevel.ContainsKey(level)) {
						previous--;
					}
					if (merged.Skip(previous + 1).Take(j - previous - 1).Any(between => between.Line.Depth == level + 1)) {
						merged[j].DeeperBefore.Add(level);
					}
				}
			}
		}

		/// <summary>
		/// Whether some windows have a line marked and others don't.
		/// </summary>
		static bool Disagree(IReadOnlyList<Window> levels, Func<ListLine, bool> marked) {
			return levels.Select(window => window.Lines.Any(marked)).Distinct().Count() > 1;
		}

		/// <summary>
		/// The window's line holding the split the windows are laid out around: the scrolled-to line, else the current one,
		/// else the first before the start and the last after the finish.
		/// </summary>
		static int Anchor(Window window, bool finished) {
			int scrolled = IndexOf(window.Lines, line => line.ScrolledTo);
			if (scrolled >= 0) {
				return scrolled;
			}
			int current = IndexOf(window.Lines, line => line.Current);
			if (current >= 0) {
				return current;
			}
			return finished ? window.Lines.Count - 1 : 0;
		}

		/// <summary>
		/// The index of the first line that matches; -1 for none.
		/// </summary>
		static int IndexOf(IReadOnlyList<ListLine> lines, Func<ListLine, bool> match) {
			for (int i = 0; i < lines.Count; i++) {
				if (match(lines[i])) {
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// A merged split's row: a line for each run of its levels where it has the same name and the same segment, finest
		/// first, so its own name (the AllSplits one when it's in that window, else the one its raw name gives) then each
		/// section it closes, named or not; each line with its segment time from the finest of its levels' windows holding
		/// the split, and current when one of those levels has the split as its current one. The row's current and scrolled-to
		/// only from AllSplits; the run time from its finest level.
		/// </summary>
		static Row ToRow(Entry entry, int levelCount) {
			ListLine line = entry.Line;
			List<string> names = SplitNames(line.Raw, levelCount);
			if (entry.InAllSplits) {
				names[^1] = line.Name;
			}
			Cell?[] cells = Enumerable.Range(0, levelCount)
				.Select(level => entry.ByLevel.TryGetValue(level, out ListLine? at) ? Time(at, at.SegmentDelta, at.SegmentUpcoming) : null).ToArray();
			int depth = Math.Min(line.Depth, levelCount - 1);
			List<Part> parts = [];
			for (int last = levelCount - 1; last >= depth;) {
				int first = last;
				while (first > depth && names[first - 1] == names[last] && !SegmentGrows(entry, first - 1, cells)) {
					first--;
				}
				bool current = Enumerable.Range(first, last - first + 1).Any(level => entry.ByLevel.TryGetValue(level, out ListLine? at) && at.Current);
				parts.Add(new Part(names[last], first, cells[first..(last + 1)].LastOrDefault(cell => cell != null), current));
				last = first - 1;
			}
			return new Row(line.Raw, line.Depth, entry.InAllSplits && line.Current, entry.InAllSplits && line.ScrolledTo, Time(line, line.Delta, line.Upcoming),
				parts, entry.Levels);
		}

		/// <summary>
		/// Whether the split's segment at the level is longer than at the next finer one: the split before it at the finer
		/// level is deeper than the level. That split is the line before it in some level's window at least as fine whose
		/// depth is at most the finer level. When no window tells: yes when a merged split one level deeper lies between it
		/// and the line before it at the level, else whether its two segment times differ.
		/// </summary>
		static bool SegmentGrows(Entry entry, int level, Cell?[] cells) {
			for (int finer = level + 1; finer < cells.Length; finer++) {
				if (entry.PreviousDepth.TryGetValue(finer, out int previous) && previous <= level + 1) {
					return previous == level + 1;
				}
			}
			if (entry.DeeperBefore.Contains(level)) {
				return true;
			}
			return cells[level] is Cell coarse && cells[level + 1] is Cell fine && coarse != fine;
		}

		/// <summary>
		/// A time of the line: the delta when the split is completed (its run delta is written), else the comparison's time.
		/// </summary>
		static Cell Time(ListLine line, string delta, string upcoming) {
			bool completed = line.Delta.Length > 0;
			return new Cell(completed ? delta : upcoming, completed, line.Highlight);
		}

		/// <summary>
		/// The split's name at each level, as LiveSplit shows it: a leading {section} is the name at that level and is
		/// dropped for the deeper ones, else one leading dash is dropped.
		/// </summary>
		internal static List<string> SplitNames(string raw, int levelCount) {
			List<string> names = [];
			string name = raw;
			for (int level = 0; level < levelCount; level++) {
				int closing = name.StartsWith('{') ? name.IndexOf('}') : -1;
				if (closing >= 0) {
					names.Add(name[1..closing]);
					name = name[(closing + 1)..];
					continue;
				}
				names.Add(WithoutDashes(name));
				if (name.StartsWith('-')) {
					name = name[1..];
				}
			}
			return names;
		}

		/// <summary>
		/// The name without its leading dashes and one space after them.
		/// </summary>
		static string WithoutDashes(string name) {
			string rest = name.TrimStart('-');
			return rest.StartsWith(' ') ? rest[1..] : rest;
		}

		/// <summary>
		/// The dashboard's lines: a title with the snapshot's note; then, with rows, the column headings and a line per part of
		/// each split, nesting at the part's first level with its tree guides (the top level's hanging from a root below the
		/// last line, which gets a line of its own), the split's outermost part told apart from
		/// those inside it, every part with the run time, repeated on the inner ones. Each part that holds the current split at
		/// one of its levels is marked and told apart, with the guides from it to its section on the path; the scrolled-to
		/// split's outermost part is marked. A line for splits not
		/// shown goes where a row says there are some. An own name no window shows is left out, and so are the sections outward
		/// no window shows while each is alone in the next; when the own name was alone, so is that row's line for splits not
		/// shown. Any such line alone in its section goes too.
		/// </summary>
		internal static List<StatusLine> StatusLines(Snapshot? snapshot) {
			if (snapshot == null || (snapshot.Rows.Count == 0 && snapshot.Note == null)) {
				return [new StatusLine("Splits: none", LineKind.Title)];
			}
			List<StatusLine> lines = [snapshot.Note == null ? new StatusLine("Splits", LineKind.Title)
				: new StatusLine($"Splits ({snapshot.Note})", snapshot.Problem ? LineKind.Problem : LineKind.Title)];
			if (snapshot.Rows.Count == 0) {
				return lines;
			}
			lines.Add(new StatusLine("", LineKind.Header, new Cell(RUN_HEADER), new Cell(SEGMENT_HEADER), 0,
				$"{RUN_HEADER}, then each line's {SEGMENT_HEADER.ToLowerInvariant()}, nested by level: {string.Join(", ", snapshot.Levels)}"));
			int first = lines.Count;
			int? previous = null;
			foreach (Row row in snapshot.Rows) {
				int alone = Alone(row, previous);
				previous = row.Parts[^1].Level;
				if (row.MoreBefore is int level && alone == 0) {
					lines.Add(MoreLine(level));
				}
				string toolTip = $"{row.Raw}\nIn {string.Join(", ", row.Levels)}";
				int start = alone == 0 && row.Parts.Count > 1 && row.Parts[0].Segment == null ? 1 : alone;
				for (int i = start; i < row.Parts.Count; i++) {
					Part part = row.Parts[i];
					bool outer = i == row.Parts.Count - 1;
					LineKind kind = part.Current ? (outer ? LineKind.Selected : LineKind.SelectedInner)
						: outer ? (row.ScrolledTo ? LineKind.ScrolledTo : LineKind.Outer)
						: LineKind.Inner;
					lines.Add(new StatusLine(part.Name, kind, row.Run, part.Segment ?? new Cell(""), part.Level, toolTip,
						part.Current || (outer && row.ScrolledTo), RepeatsRun: !outer));
				}
				if (row.MoreAfter) {
					lines.Add(MoreLine(0));
				}
			}
			List<StatusLine> rows = lines.Skip(first).ToList();
			HashSet<int> empty = EmptySections(rows);
			rows = rows.Where((_, i) => !empty.Contains(i)).ToList();
			List<int> levels = rows.Select(line => line.Level + 1).ToList();
			List<string> guides = TreeGuides(levels);
			List<string> path = PathGuides(levels, rows.Select(line => line.Kind is LineKind.Selected or LineKind.SelectedInner).ToList());
			return [.. lines.Take(first), .. rows.Select((line, i) => line with { Guides = guides[i], Path = path[i] }),
				new StatusLine("", LineKind.Root, Guides: RootGuides(guides[^1]), Path: RootGuides(path[^1]))];
		}

		/// <summary>
		/// The lines for splits not shown that are all their section holds: the section, the next line below at a lower level,
		/// has no split's line inside it, only lines like these. One with no section below stays.
		/// </summary>
		internal static HashSet<int> EmptySections(IReadOnlyList<StatusLine> lines) {
			HashSet<int> empty = [];
			for (int i = 0; i < lines.Count; i++) {
				if (lines[i].Kind != LineKind.More) {
					continue;
				}
				int section = i + 1;
				while (section < lines.Count && lines[section].Level >= lines[i].Level) {
					section++;
				}
				if (section == lines.Count) {
					continue;
				}
				int top = section - 1;
				while (top >= 0 && lines[top].Level > lines[section].Level) {
					top--;
				}
				if (Enumerable.Range(top + 1, section - top - 1).All(inside => lines[inside].Kind == LineKind.More)) {
					empty.Add(i);
				}
			}
			return empty;
		}

		/// <summary>
		/// How many of the row's parts to leave out, from its own name outward: each that no window shows the split at (no
		/// segment time) and is the only split shown inside the next part, because the row before ends at that part's level
		/// or above (or there is none). The outermost part always stays.
		/// </summary>
		internal static int Alone(Row row, int? previous) {
			int alone = 0;
			while (alone < row.Parts.Count - 1 && row.Parts[alone].Segment == null && !(previous > row.Parts[alone + 1].Level)) {
				alone++;
			}
			return alone;
		}

		/// <summary>
		/// A line for splits not shown, nesting at the level, its times too.
		/// </summary>
		static StatusLine MoreLine(int level) {
			return new StatusLine(MORE_TEXT, LineKind.More, new Cell(MORE_TEXT), new Cell(MORE_TEXT), level, MORE_TOOLTIP);
		}

		/// <summary>
		/// The tree guides before each line's name, GUIDE_WIDTH characters per level, for lines nesting at these levels, a
		/// section below the lines inside it. A line's section is the next line below at a lower level (none: one level up,
		/// below the last). Each section's vertical runs up from it to its first line, at the section's level: ┌ at that line,
		/// ├ at each of its other lines, │ past the deeper lines between; a line's branch runs ─ to just before its name.
		/// </summary>
		internal static List<string> TreeGuides(IReadOnlyList<int> levels) {
			int count = levels.Count;
			char[][] rows = levels.Select(level => Enumerable.Repeat(' ', GUIDE_WIDTH * level).ToArray()).ToArray();
			(int Index, int Level)[] sections = Sections(levels);
			foreach (var section in Enumerable.Range(0, count).Where(i => levels[i] > 0).GroupBy(i => sections[i])) {
				int top = section.Min();
				int x = GUIDE_WIDTH * section.Key.Level;
				for (int row = top; row < section.Key.Index; row++) {
					if (sections[row] != section.Key) {
						rows[row][x] = '│';
						continue;
					}
					rows[row][x] = row == top ? '┌' : '├';
					for (int dash = x + 1; dash < rows[row].Length - 1; dash++) {
						rows[row][dash] = '─';
					}
				}
			}
			return rows.Select(row => new string(row)).ToList();
		}

		/// <summary>
		/// The strokes of each line's tree guides on the path from the lines on it down to their sections, for lines nesting
		/// at these levels, at their places in the guides with spaces before them: a line's branch as ┌ and ─ from its section's
		/// vertical to its name, and that vertical as │ past the lines between.
		/// </summary>
		internal static List<string> PathGuides(IReadOnlyList<int> levels, IReadOnlyList<bool> onPath) {
			(int Index, int Level)[] sections = Sections(levels);
			char[][] rows = levels.Select(level => Enumerable.Repeat(' ', GUIDE_WIDTH * level).ToArray()).ToArray();
			for (int i = 0; i < levels.Count; i++) {
				if (!onPath[i] || levels[i] == 0) {
					continue;
				}
				int x = GUIDE_WIDTH * sections[i].Level;
				rows[i][x] = rows[i][x] == '│' ? '├' : '┌';
				for (int dash = x + 1; dash < rows[i].Length - 1; dash++) {
					rows[i][dash] = '─';
				}
				for (int row = i + 1; row < sections[i].Index; row++) {
					rows[row][x] = '│';
				}
			}
			return rows.Select(row => new string(row).TrimEnd(' ')).ToList();
		}

		/// <summary>
		/// The root's guides below the last line, from that line's guides or path strokes: each vertical through it ends at the
		/// root's row, ╵ at the left edge (the root's place) or ┘ further right, ┴ where another runs on to it, joined to the
		/// left edge by ─, which is └ there, or ╶ when no vertical reaches it.
		/// </summary>
		internal static string RootGuides(string last) {
			List<int> verticals = Enumerable.Range(0, last.Length).Where(x => last[x] is '│' or '├' or '┌').ToList();
			if (verticals.Count == 0) {
				return "";
			}
			char[] row = Enumerable.Repeat('─', verticals[^1] + 1).ToArray();
			foreach (int x in verticals) {
				row[x] = x == verticals[^1] ? '┘' : '┴';
			}
			row[0] = verticals[0] > 0 ? '╶' : verticals.Count > 1 ? '└' : '╵';
			return new string(row);
		}

		/// <summary>
		/// The name in at most that many characters: whole when it fits, else cut short before a …, without the spaces the
		/// cut leaves at its end.
		/// </summary>
		internal static string Shorten(string name, int room) {
			if (name.Length <= room) {
				return name;
			}
			return name[..Math.Max(0, room - 1)].TrimEnd() + CUT_TEXT;
		}

		/// <summary>
		/// Each line's section, for lines nesting at these levels: the index and level of the next line below at a lower
		/// level; none: past the last, one level up.
		/// </summary>
		static (int Index, int Level)[] Sections(IReadOnlyList<int> levels) {
			(int Index, int Level)[] sections = new (int, int)[levels.Count];
			for (int i = 0; i < levels.Count; i++) {
				int below = i + 1;
				while (below < levels.Count && levels[below] >= levels[i]) {
					below++;
				}
				sections[i] = (below, below < levels.Count ? levels[below] : levels[i] - 1);
			}
			return sections;
		}
	}
}
