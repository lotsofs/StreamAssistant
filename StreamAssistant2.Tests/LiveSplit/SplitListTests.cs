using Xunit;
using static StreamAssistant2.SplitList;

namespace StreamAssistant2.Tests {
	public class SplitListTests {
		// LiveSplit.OutputToFile's NestedRun: subsplits two deep in the section that 0009 ends, then top-level splits up to 0020
		static readonly string[] NESTED = [
			"0001", "-0002", "--0003", "--0004", "-0005", "-0006", "--0007", "-0008",
			.. Enumerable.Range(9, 12).Select(i => i.ToString("0000")),
		];

		static string[] Raws(params int[] splits) => splits.Select(s => NESTED[s - 1]).ToArray();

		static ListLine Line(string raw, bool current = false, bool scrolled = false) {
			return new ListLine(raw, raw.TrimStart('-'), raw.TakeWhile(c => c == '-').Count(), current, scrolled, Highlight.None, "", "1:00");
		}

		// A window of NestedRun's splits by number, with the current or scrolled-to mark on one line (from 0), or none
		static Window Level(string level, int depth, int[] splits, int marked = -1, bool scrolled = false) {
			return new Window(level, depth, Raws(splits).Select((raw, i) => Line(raw, i == marked && !scrolled, i == marked && scrolled)).ToList());
		}

		static List<Row> Merged(bool finished, params Window[] levels) {
			List<Row>? rows = Merge(levels, finished);
			Assert.NotNull(rows);
			return rows;
		}

		[Fact]
		public void Merge_ScrolledUpTo0005_EveryLevelsWindowInRunOrder() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1, true),
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2, true),
				Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true));
			Assert.Equal(Raws(Enumerable.Range(1, 12).ToArray()), rows.Select(r => r.Raw));
			Assert.Equal([0, 1, 2, 2, 1, 1, 2, 1, 0, 0, 0, 0], rows.Select(r => r.Depth));
			Assert.Equal(["-0005"], rows.Where(r => r.ScrolledTo).Select(r => r.Raw));
			Assert.Equal(["AllSplits", "SansSubsplits_Level2"], rows.Single(r => r.Raw == "-0005").Levels);
			Assert.Equal(["SansSubsplits_Level2", "SansSubsplits"], rows.Single(r => r.Raw == "0001").Levels);
		}

		[Fact]
		public void Merge_AroundTheCurrentSplit_MarksOnlyAllSplitsCurrent() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1),
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2),
				Level("AllSplits", 2, [3, 4, 5, 6, 7], 2));
			Assert.Equal(Raws(Enumerable.Range(1, 12).ToArray()), rows.Select(r => r.Raw));
			Assert.Equal(["-0005"], rows.Where(r => r.Current).Select(r => r.Raw));
		}

		// A level's window starting at its first member, of a level with that many
		static Window From(Window window, int first, int total) => window with { First = first, Total = total };

		// NestedRun's members at each level: SansSubsplits, Level2, AllSplits
		static readonly int[] TOP = [1, .. Enumerable.Range(9, 12)];
		static readonly int[] MIDDLE = [1, 2, 5, 6, 8, .. Enumerable.Range(9, 12)];
		static readonly int[] ALL = [.. Enumerable.Range(1, 20)];

		[Fact]
		public void Merge_TheFullList_EverySplitInRunOrder_NoneHidden() {
			// On 0005, which 0009 closes at the top level
			List<Row> rows = Merged(false,
				From(Level("SansSubsplits", 0, TOP, 1), 0, 13),
				From(Level("SansSubsplits_Level2", 1, MIDDLE, 2), 0, 17),
				From(Level("AllSplits", 2, ALL, 4), 0, 20));
			Assert.Equal(Raws(ALL), rows.Select(r => r.Raw));
			Assert.All(rows, r => Assert.Equal(3 - Math.Min(r.Depth, 2), r.Levels.Count));
			Assert.All(rows, r => Assert.Null(r.MoreBefore));
			Assert.False(rows[^1].MoreAfter);
			Assert.Equal(["-0005"], rows.Where(r => r.Current).Select(r => r.Raw));
		}

		[Fact]
		public void Merge_TheCompletedList_TheSplitsBeforeTheCurrent_ThenSplitsNotShown() {
			// On 0005: 0001 to 0004 done; at the top level only 0001, at Level2 0001 and 0002
			List<Row> rows = Merged(false,
				From(Level("SansSubsplits", 0, [1]), 0, 13),
				From(Level("SansSubsplits_Level2", 1, [1, 2]), 0, 17),
				From(Level("AllSplits", 2, [1, 2, 3, 4]), 0, 20));
			Assert.Equal(Raws(1, 2, 3, 4), rows.Select(r => r.Raw));
			Assert.All(rows, r => Assert.Null(r.MoreBefore));
			Assert.True(rows[^1].MoreAfter);
		}

		[Fact]
		public void Merge_TheRemainingList_SplitsNotShownThenTheCurrentOnward() {
			// On 0005: from 0009 at the top level, from 0005 at Level2 and AllSplits
			List<Row> rows = Merged(false,
				From(Level("SansSubsplits", 0, TOP[1..], 0), 1, 13),
				From(Level("SansSubsplits_Level2", 1, MIDDLE[2..], 0), 2, 17),
				From(Level("AllSplits", 2, ALL[4..], 0), 4, 20));
			Assert.Equal(Raws(ALL[4..]), rows.Select(r => r.Raw));
			Assert.Equal(0, rows[0].MoreBefore);
			Assert.All(rows.Skip(1), r => Assert.Null(r.MoreBefore));
			Assert.False(rows[^1].MoreAfter);
			Assert.Equal("-0005", rows.Single(r => r.Current).Raw);
		}

		[Fact]
		public void Merge_BeforeTheStart_EveryWindowFromTheFirstSplit() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12]),
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8]),
				Level("AllSplits", 2, [1, 2, 3, 4, 5]));
			Assert.Equal(Raws(1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12), rows.Select(r => r.Raw));
		}

		[Fact]
		public void Merge_AfterTheFinish_EveryWindowToTheLastSplit() {
			List<Row> rows = Merged(true,
				Level("SansSubsplits", 0, [16, 17, 18, 19, 20]),
				Level("SansSubsplits_Level2", 1, [16, 17, 18, 19, 20]),
				Level("AllSplits", 2, [16, 17, 18, 19, 20]));
			Assert.Equal(Raws(16, 17, 18, 19, 20), rows.Select(r => r.Raw));
			Assert.All(rows, r => Assert.Equal(3, r.Levels.Count));
		}

		[Fact]
		public void Merge_CoarserWindowsPastTheEnd_GoAfter() {
			// Current split 0003, no splits before it and one after
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [9, 10], 0),
				Level("SansSubsplits_Level2", 1, [5, 6], 0),
				Level("AllSplits", 2, [3, 4], 0));
			Assert.Equal(Raws(3, 4, 5, 6, 9, 10), rows.Select(r => r.Raw));
		}

		[Fact]
		public void Merge_ACoarserWindowsLinesBeforeItsAnchor_GoBefore() {
			// Current split 0007, one split before it and none after
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9], 1),
				Level("SansSubsplits_Level2", 1, [6, 8], 1),
				Level("AllSplits", 2, [6, 7], 1));
			Assert.Equal(Raws(1, 6, 7, 8, 9), rows.Select(r => r.Raw));
		}

		[Fact]
		public void Merge_ARawNameThatDoesntMatch_IsNull() {
			Window level2 = Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2, true);
			Window wrong = level2 with { Lines = [.. level2.Lines.Take(2), Line("-0099", scrolled: true), .. level2.Lines.Skip(3)] };
			Assert.Null(Merge([Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1, true), wrong, Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true)], false));
		}

		[Fact]
		public void Merge_ScrolledInOneLevelOnly_IsNull() {
			Assert.Null(Merge([Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1), Level("AllSplits", 1, [3, 4, 5, 6, 7], 2, true)], false));
		}

		[Fact]
		public void Merge_CurrentInOneLevelOnly_IsNull() {
			Assert.Null(Merge([Level("SansSubsplits", 0, [1, 9, 10, 11, 12]), Level("AllSplits", 1, [3, 4, 5, 6, 7], 2)], false));
		}

		[Fact]
		public void Merge_AllSplitsAlone_IsItsWindow() {
			List<Row> rows = Merged(false, new Window("AllSplits", 0, [Line("A"), Line("B", current: true), Line("C")]));
			Assert.Equal(["A", "B", "C"], rows.Select(r => r.Raw));
			Assert.True(rows[1].Current);
			Assert.All(rows, r => Assert.Single(r.Parts));
		}

		[Fact]
		public void Merge_NoSplits_IsEmpty() {
			Assert.Empty(Merged(false, new Window("AllSplits", 0, [])));
		}

		// A row's parts as their names and first levels
		static (string, int)[] Parts(Row row) => row.Parts.Select(p => (p.Name, p.Level)).ToArray();

		[Fact]
		public void Merge_Parts_FromTheRawNames_OwnNameThenEachSectionOutward() {
			// GTASA any%, around Drive By: two splits before the current one and two after
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [Line("{Los Santos}{Los Santos Finale}TGS", current: true), Line("{Badlands}{Streetrace Finale}AYGTSF")]),
				new Window("SansSubsplits_Level2", 1, [Line("-{Start of the run}Big Smoke"), Line("-{Grove Street Part 1}Jeffrey"),
					Line("-{Grove Street Part 2}HSLR", current: true), Line("-{West Los Santos}BD"), Line("-{RTGS}S.A.M.")]),
				new Window("AllSplits", 2, [Line("--LAB"), Line("--Running Dog"), Line("--Drive By", current: true), Line("--Sweet's Girl"), Line("--CV")]));
			Assert.Equal(["Big Smoke", "Jeffrey", "LAB", "Running Dog", "Drive By", "Sweet's Girl", "CV", "HSLR", "BD", "S.A.M.", "TGS", "AYGTSF"],
				rows.Select(r => r.Parts[0].Name));
			Assert.Equal([("Jeffrey", 2), ("Grove Street Part 1", 1)], Parts(rows[1]));
			Assert.Equal([("TGS", 2), ("Los Santos Finale", 1), ("Los Santos", 0)], Parts(rows[10]));
			Assert.Equal([("LAB", 2)], Parts(rows[2]));
			Assert.Equal(["Drive By"], rows.Where(r => r.Current).Select(r => r.Parts[0].Name));
		}

		[Fact]
		public void Merge_Parts_EachWithItsOwnLevelsSegments() {
			// GTASA 100%, around N.O.E: Learning to Fly ends Desert Second Half and Desert, and is past AllSplits' window
			ListLine fly = Line("{Desert}{Desert Second Half}Learning to Fly") with { Upcoming = "6:20:48" };
			ListLine noe = Line("--N.O.E", current: true) with { SegmentUpcoming = "3:36" };
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [fly with { Name = "Desert", SegmentUpcoming = "54:57" }, Line("{Las Venturas}{LV Finale}Breaking the Bank", current: true)]),
				new Window("SansSubsplits_Level2", 1, [fly with { Name = "Desert Second Half", SegmentUpcoming = "9:25" }, Line("-{LV Introduction}Don Peyote", current: true)]),
				new Window("SansSubsplits_Level3", 2, [fly with { Name = "Learning to Fly", SegmentUpcoming = "7:54" }, noe]),
				new Window("AllSplits", 3, [noe]));
			Assert.Equal(["{Desert}{Desert Second Half}Learning to Fly", "--N.O.E", "-{LV Introduction}Don Peyote", "{Las Venturas}{LV Finale}Breaking the Bank"],
				rows.Select(r => r.Raw));
			Row row = rows[0];
			Assert.Equal([("Learning to Fly", 2), ("Desert Second Half", 1), ("Desert", 0)], Parts(row));
			Assert.Equal([new Cell("7:54"), new Cell("9:25"), new Cell("54:57")], row.Parts.Select(p => p.Segment));
			Assert.Equal(new Cell("6:20:48"), row.Run);
			Assert.Equal([("N.O.E", 2)], Parts(rows[1]));
			Assert.Equal(new Cell("3:36"), rows[1].Parts[0].Segment);
		}

		[Fact]
		public void Merge_Parts_ASectionWithoutAName_GetsItsOwnLine() {
			// Like GTASA's Buy Wang Cars after SS35–SS39: B's segment at L2 and L3 runs from A over x and y, at All from y
			ListLine b = Line("-B", current: true);
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [Line("A"), Line("C", current: true)]),
				new Window("SansSubsplits_Level2", 1, [Line("A"), b with { SegmentUpcoming = "0:30" }]),
				new Window("SansSubsplits_Level3", 2, [Line("A"), b with { SegmentUpcoming = "0:30" }]),
				new Window("AllSplits", 3, [Line("A"), Line("---x"), Line("---y"), b with { SegmentUpcoming = "0:10" }]));
			Row row = rows.Single(r => r.Raw == "-B");
			Assert.Equal([("B", 3), ("B", 1)], Parts(row));
			Assert.Equal([new Cell("0:10"), new Cell("0:30")], row.Parts.Select(p => p.Segment));
		}

		[Fact]
		public void Merge_Parts_ASectionAtEachLevelItGrows() {
			// Like GTASA's Monster: from Yay at L2, from Buy Wang Cars at L3, from Ø28 at All
			ListLine m = Line("--M", current: true);
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [Line("Y"), Line("Z", current: true)]),
				new Window("SansSubsplits_Level2", 1, [Line("Y"), Line("-N", current: true)]),
				new Window("SansSubsplits_Level3", 2, [Line("Y"), m]),
				new Window("SansSubsplits_Level4", 3, [Line("Y"), Line("---W"), m]),
				new Window("AllSplits", 4, [Line("Y"), Line("---W"), Line("----o"), m]));
			Assert.Equal([("M", 4), ("M", 3), ("M", 2)], Parts(rows.Single(r => r.Raw == "--M")));
			Assert.Equal([("W", 3)], Parts(rows.Single(r => r.Raw == "---W")));
		}

		[Fact]
		public void Merge_Parts_PastAllSplitsWindow_ADeeperSplitShownBetweenGivesTheSection() {
			// Like GTASA's Monster just past AllSplits' window: o, seen in AllSplits, lies between P and M at L2
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [Line("Z", current: true)]),
				new Window("SansSubsplits_Level2", 1, [Line("-P"), Line("-M", current: true)]),
				new Window("AllSplits", 2, [Line("-P"), Line("--o", current: true)]));
			Assert.Equal(["-P", "--o", "-M", "Z"], rows.Select(r => r.Raw));
			Assert.Equal([("M", 2), ("M", 1)], Parts(rows[2]));
		}

		[Theory]
		[InlineData("0:10", "0:10", new[] { 0 })]
		[InlineData("0:30", "0:10", new[] { 1, 0 })]
		public void Merge_Parts_NoLineBefore_TheSegmentTimesDecide(string sans, string all, int[] levels) {
			// S opens both windows, so nothing says which split comes before it
			List<Row> rows = Merged(false,
				new Window("SansSubsplits", 0, [Line("S", current: true) with { SegmentUpcoming = sans }, Line("U")]),
				new Window("AllSplits", 1, [Line("S", current: true) with { SegmentUpcoming = all }, Line("-t"), Line("U")]));
			Assert.Equal(levels, rows[0].Parts.Select(p => p.Level));
		}

		[Fact]
		public void Merge_Parts_TheAllSplitsNameForItsOwnLine() {
			// With subsplits off, AllSplits shows the split's deepest name, which its raw name at one level doesn't give
			List<Row> rows = Merged(false, new Window("AllSplits", 0, [Line("{A}B") with { Name = "B" }]));
			Assert.Equal([("B", 0)], Parts(rows[0]));
		}

		[Fact]
		public void Merge_Times_DeltaWhenCompleted_ElseTheComparisonTime() {
			ListLine done = new("A", "A", 0, false, false, Highlight.AheadGained, "−1.0", "", "−0.4", "");
			ListLine next = new("B", "B", 0, true, false, Highlight.None, "", "1:23", "", "0:41");
			List<Row> rows = Merged(false, new Window("AllSplits", 0, [done, next]));
			Assert.Equal(new Cell("−1.0", true, Highlight.AheadGained), rows[0].Run);
			Assert.Equal(new Cell("−0.4", true, Highlight.AheadGained), rows[0].Parts[0].Segment);
			Assert.Equal(new Cell("1:23"), rows[1].Run);
			Assert.Equal(new Cell("0:41"), rows[1].Parts[0].Segment);
		}

		[Fact]
		public void Merge_Segments_EachLevelsOwn_NoneWhereItsWindowLacksTheSplit() {
			// A2 ends Area A and is in both windows: its own segment in AllSplits, the section's in SansSubsplits
			ListLine a1 = new("-A1", "A1", 1, false, false, Highlight.AheadGained, "−1.0", "", "−1.0", "");
			ListLine a2 = new("{Area A}A2", "A2", 0, false, false, Highlight.AheadLost, "−0.5", "", "+0.5", "");
			ListLine a2Section = a2 with { Name = "Area A", Highlight = Highlight.Gold, SegmentDelta = "−0.5" };
			ListLine b1 = new("-B1", "B1", 1, true, false, Highlight.None, "", "0:30", "", "0:10");
			ListLine b2Section = new("{Area B}B2", "Area B", 0, true, false, Highlight.None, "", "0:50", "", "0:30");
			List<Row> rows = Merged(false, new Window("SansSubsplits", 0, [a2Section, b2Section]), new Window("AllSplits", 1, [a1, a2, b1]));
			Assert.Equal(["-A1", "{Area A}A2", "-B1", "{Area B}B2"], rows.Select(r => r.Raw));
			Assert.Equal(new Cell("−1.0", true, Highlight.AheadGained), rows[0].Parts.Single().Segment);
			Assert.Equal([("A2", 1), ("Area A", 0)], Parts(rows[1]));
			Assert.Equal([new Cell("+0.5", true, Highlight.AheadLost), new Cell("−0.5", true, Highlight.Gold)], rows[1].Parts.Select(p => p.Segment));
			Assert.Equal(new Cell("−0.5", true, Highlight.AheadLost), rows[1].Run);
			Assert.Equal(new Cell("0:10"), rows[2].Parts.Single().Segment);
			Assert.Equal([("B2", 1), ("Area B", 0)], Parts(rows[3]));
			Assert.Equal([null, new Cell("0:30")], rows[3].Parts.Select(p => p.Segment));
			Assert.Equal(new Cell("0:50"), rows[3].Run);
		}

		// The files LiveSplit writes for a level's window with these lines
		static Dictionary<string, string> Files(int depth, params ListLine[] lines) {
			const string FILL = "███";
			string File(Func<ListLine, string> line) => string.Concat(lines.Select(l => line(l) + "\n"));
			Dictionary<string, string> files = new() {
				["Names_Raw.txt"] = File(l => l.Raw),
				["Names.txt"] = File(l => l.Name),
				["Highlight_CurrentSplit.txt"] = File(l => l.Current ? FILL : ""),
				["Highlight_ScrolledToSplit.txt"] = File(l => l.ScrolledTo ? FILL : ""),
				[@"RealTime\Highlight_Gold.txt"] = File(l => l.Highlight == Highlight.Gold ? FILL : ""),
				[@"RealTime\CurrentComparison\Delta_Run.txt"] = File(l => l.Delta),
				[@"RealTime\CurrentComparison\Time_Run_Upcoming.txt"] = File(l => l.Upcoming),
				[@"RealTime\CurrentComparison\Delta_Segment.txt"] = File(l => l.SegmentDelta),
				[@"RealTime\CurrentComparison\Time_Segment_Upcoming.txt"] = File(l => l.SegmentUpcoming),
			};
			foreach (Highlight kind in new[] { Highlight.AheadGained, Highlight.AheadLost, Highlight.BehindGained, Highlight.BehindLost }) {
				files[$@"RealTime\CurrentComparison\Highlight_{kind}.txt"] = File(l => l.Highlight == kind ? FILL : "");
			}
			for (int d = 0; d <= depth; d++) {
				files[$"Names_Indent_{d}.txt"] = File(l => l.Depth == d ? l.Name : "");
			}
			return files;
		}

		static Window? Parse(string level, int depth, Dictionary<string, string> files) {
			return ParseLevel(level, depth, file => files.GetValueOrDefault(file, ""));
		}

		[Fact]
		public void ParseLevel_ReadsEveryFile() {
			ListLine[] lines = [
				new("-{Sec}A", "A", 1, false, false, Highlight.Gold, "−0.5", "", "−0.3", ""),
				new("--B", "B", 2, false, false, Highlight.BehindLost, "+1.0", "", "+1.5", ""),
				new("--C", "C", 2, true, false, Highlight.None, "", "1:00", "", "0:10"),
				new("-D", "D", 1, false, true, Highlight.None, "", "2:00", "", "0:20"),
				new("E", "E", 0, false, false, Highlight.None, "", "-", "", "-"),
			];
			Window? window = Parse("AllSplits", 2, Files(2, lines));
			Assert.NotNull(window);
			Assert.Equal(("AllSplits", 2), (window.Level, window.Depth));
			Assert.Equal(lines, window.Lines);
		}

		[Fact]
		public void ParseLevel_AFileWithAnotherLineCount_IsNull() {
			Dictionary<string, string> files = Files(0, Line("A"), Line("B"));
			files[@"RealTime\CurrentComparison\Delta_Run.txt"] = "\n";
			Assert.Null(Parse("AllSplits", 0, files));
		}

		[Fact]
		public void ParseLevel_AMissingIndentFile_IsNull() {
			Dictionary<string, string> files = Files(0, Line("A"));
			Assert.Null(Parse("AllSplits", 1, files));
		}

		[Fact]
		public void ParseLevel_ABlankName_TakesItsDepthFromTheDashes_UpToTheLevels() {
			Dictionary<string, string> files = Files(1, new ListLine("--", "", 0, false, false, Highlight.None, "", ""), new ListLine("-", "", 0, false, false, Highlight.None, "", ""));
			Window? window = Parse("AllSplits", 1, files);
			Assert.NotNull(window);
			Assert.Equal([1, 1], window.Lines.Select(l => l.Depth));
		}

		[Fact]
		public void ParseLevel_NoSplits_IsEmpty() {
			Window? window = Parse("AllSplits", 0, []);
			Assert.NotNull(window);
			Assert.Empty(window.Lines);
		}

		[Fact]
		public void ParseLevel_ANamedComparison_ReadsItsFolder() {
			ListLine[] best = [Line("A") with { Delta = "−0.5", Upcoming = "", Highlight = Highlight.AheadGained }, Line("B") with { Upcoming = "2:00" }];
			Dictionary<string, string> files = Files(0, Line("A"), Line("B"));
			foreach ((string file, string text) in Files(0, best).Where(f => f.Key.Contains(@"\CurrentComparison\"))) {
				files[file.Replace(@"\CurrentComparison\", @"\BestSegments\")] = text;
			}
			Window? window = ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), "BestSegments");
			Assert.NotNull(window);
			Assert.Equal(best, window.Lines);
		}

		// The files without those whose names hold any of the parts
		static Dictionary<string, string> Without(Dictionary<string, string> files, params string[] parts) {
			return files.Where(file => !parts.Any(file.Key.Contains)).ToDictionary();
		}

		[Fact]
		public void ParseLevel_TheCompletedList_WithoutTheUpcomingFiles() {
			ListLine[] lines = [
				Line("A") with { Upcoming = "", Delta = "−0.5", SegmentDelta = "−0.5", Highlight = Highlight.AheadGained },
				Line("B") with { Upcoming = "", Delta = "+1.0", SegmentDelta = "+1.5", Highlight = Highlight.BehindLost },
			];
			Dictionary<string, string> files = Without(Files(0, lines), "Upcoming", "Highlight_CurrentSplit", "Highlight_ScrolledToSplit");
			Assert.Equal(lines, ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), CURRENT, ListKind.Completed)?.Lines);
		}

		[Fact]
		public void ParseLevel_TheRemainingList_WithoutTheCompletedFiles() {
			ListLine[] lines = [Line("A", current: true) with { SegmentUpcoming = "0:10" }, Line("B") with { SegmentUpcoming = "0:20" }];
			Dictionary<string, string> files = Without(Files(0, lines), "Delta_", "Highlight_Gold", "Highlight_Ahead", "Highlight_Behind", "Highlight_ScrolledToSplit");
			Assert.Equal(lines, ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), CURRENT, ListKind.Remaining)?.Lines);
		}

		[Fact]
		public void ParseLevel_TheFullList_WithoutTheScrolledToFile() {
			ListLine[] lines = [Line("A") with { Upcoming = "", Delta = "+1.0", Highlight = Highlight.BehindLost }, Line("B", current: true)];
			Dictionary<string, string> files = Without(Files(0, lines), "Highlight_ScrolledToSplit");
			Assert.Equal(lines, ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), CURRENT, ListKind.Full)?.Lines);
		}

		[Fact]
		public void ParseLevel_AFileTheListWrites_Missing_IsNull() {
			Dictionary<string, string> files = Without(Files(0, Line("A")), "Delta_Run");
			Assert.Null(ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), CURRENT, ListKind.Completed));
			Assert.Null(ParseLevel("AllSplits", 0, file => files.GetValueOrDefault(file, ""), CURRENT, ListKind.Surrounding));
		}

		// Output to File's own cases
		[Theory]
		[InlineData("Personal Best", "PersonalBest")]
		[InlineData(" My  Rival ", "MyRival")]
		[InlineData("A/B", "A_B")]
		[InlineData("a:b*c?", "a_b_c_")]
		[InlineData("Run.", "Run_")]
		[InlineData("a. ", "a_")]
		[InlineData("", "_")]
		[InlineData(" ", "_")]
		public void ComparisonFolder_AsOutputToFileNamesIt(string comparison, string folder) {
			Assert.Equal(folder, ComparisonFolder(comparison));
		}

		[Theory]
		[InlineData("PersonalBest", "Personal Best")]
		[InlineData("BalancedPB", "Balanced PB")]
		[InlineData("GolfHCP", "Golf HCP")]
		[InlineData("Run2Go", "Run2 Go")]
		[InlineData("None", "None")]
		[InlineData("xcvb", "xcvb")]
		public void ComparisonName_ASpaceWhereALowercaseLetterOrDigitMeetsAnUppercaseOne(string folder, string name) {
			Assert.Equal(name, ComparisonName(folder));
		}

		[Fact]
		public void Written_LeavesOutEmptyFilesAndCurrentComparison_SortedIgnoringCase() {
			Dictionary<string, string> upcoming = new() {
				["xcvb"] = "", ["PersonalBest"] = "1:00\n", [CURRENT] = "1:00\n", ["none"] = "\n", ["BestSegments"] = "0:50\n",
			};
			Assert.Equal(["BestSegments", "none", "PersonalBest"], Written(upcoming.Keys, folder => upcoming[folder]));
		}

		[Fact]
		public void CycleList_TheCurrentComparisonHasItsOwnFolder_TheWrittenOnes() {
			Assert.Equal(["BestSegments", "PersonalBest"], CycleList(["BestSegments", "PersonalBest"], "Personal Best"));
		}

		[Fact]
		public void CycleList_TheCurrentComparisonHasNone_CurrentFirst() {
			Assert.Equal([CURRENT, "BestSegments"], CycleList(["BestSegments"], "Personal Best"));
		}

		[Fact]
		public void CycleList_NoneWritten_OnlyCurrent() {
			Assert.Equal([CURRENT], CycleList([], "Personal Best"));
		}

		static readonly DateTime NOW = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
		static readonly TimeSpan INTERVAL = TimeSpan.FromSeconds(30);
		static readonly string[] WRITTEN = ["BestSegments", "PersonalBest", "WorstSegments"];

		[Fact]
		public void Resolve_Current_ReadsItAndTheCycleCountsAsMovingNow() {
			Assert.Equal((CURRENT, NOW, CURRENT), Resolve(CURRENT, WRITTEN, "Personal Best", "BestSegments", NOW.AddMinutes(-5), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_AWrittenFolder_ReadsIt() {
			Assert.Equal(("WorstSegments", NOW, "WorstSegments"), Resolve("WorstSegments", WRITTEN, "Personal Best", CURRENT, NOW.AddMinutes(-5), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_AFolderNoLongerWritten_FallsBackToCurrent() {
			Assert.Equal((CURRENT, NOW, CURRENT), Resolve("xcvb", WRITTEN, "Personal Best", "xcvb", NOW.AddMinutes(-5), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleBeforeTheInterval_KeepsTheOneShown() {
			DateTime moved = NOW.AddSeconds(-29);
			Assert.Equal(("PersonalBest", moved, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", "PersonalBest", moved, NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleAtTheInterval_TheNext() {
			Assert.Equal(("WorstSegments", NOW, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", "PersonalBest", NOW.AddSeconds(-30), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleAtTheEnd_WrapsToTheFirst() {
			Assert.Equal(("BestSegments", NOW, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", "WorstSegments", NOW.AddSeconds(-30), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleFromCurrent_CountsAsItsComparisonsFolder() {
			DateTime moved = NOW.AddSeconds(-1);
			Assert.Equal(("PersonalBest", moved, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", CURRENT, moved, NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleFromOneNotInTheList_TheFirstAtOnce() {
			Assert.Equal(("BestSegments", NOW, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", "xcvb", NOW.AddSeconds(-1), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleWhileTheCurrentComparisonHasNoFolder_GoesThroughCurrent() {
			Assert.Equal((CURRENT, NOW, CYCLE), Resolve(CYCLE, WRITTEN, "Latest Run", "WorstSegments", NOW.AddSeconds(-30), NOW, INTERVAL));
		}

		[Fact]
		public void Resolve_CycleChosenAfterAComparison_KeepsItForTheInterval() {
			(string shown, DateTime moved, _) = Resolve("BestSegments", WRITTEN, "Personal Best", CURRENT, NOW.AddMinutes(-5), NOW, INTERVAL);
			Assert.Equal(("BestSegments", NOW, CYCLE), Resolve(CYCLE, WRITTEN, "Personal Best", shown, moved, NOW.AddSeconds(29), INTERVAL));
		}

		[Theory]
		[InlineData("", new string[0])]
		[InlineData("\n", new[] { "" })]
		[InlineData("a\n", new[] { "a" })]
		[InlineData("a\n\nb\n", new[] { "a", "", "b" })]
		[InlineData("a\r\nb\r\n", new[] { "a", "b" })]
		[InlineData("a\nb", new[] { "a", "b" })]
		public void Lines_EachEndedByALineBreak(string text, string[] expected) {
			Assert.Equal(expected, SplitList.Lines(text));
		}

		[Theory]
		[InlineData("-{Grove Street Part 1}Jeffrey", 3, new[] { "{Grove Street Part 1}Jeffrey", "Grove Street Part 1", "Jeffrey" })]
		[InlineData("{Los Santos}{Los Santos Finale}TGS", 3, new[] { "Los Santos", "Los Santos Finale", "TGS" })]
		[InlineData("--Ryder", 3, new[] { "Ryder", "Ryder", "Ryder" })]
		[InlineData("- -40", 2, new[] { "-40", "-40" })]
		[InlineData("{A}B", 1, new[] { "A" })]
		[InlineData("{A}B", 2, new[] { "A", "B" })]
		[InlineData("{A B", 1, new[] { "{A B" })]
		public void SplitNames_AsLiveSplitShowsThem(string raw, int levels, string[] expected) {
			Assert.Equal(expected, SplitNames(raw, levels));
		}

		[Theory]
		[InlineData("-", "20", true)]
		[InlineData(" -\r\n", "20", true)]
		[InlineData("-", "0", false)]
		[InlineData("4", "3", false)]
		[InlineData("", "", false)]
		public void Finished_NoNumberAndSplitsCompleted(string number, string completed, bool finished) {
			Assert.Equal(finished, Finished(number, completed));
		}

		[Fact]
		public void ReadShared_ReadsAFileLiveSplitHasOpen() {
			string path = Path.Combine(Path.GetTempPath(), $"SplitListTests_{Guid.NewGuid():N}.txt");
			try {
				using (FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					writer.Write("0001\n"u8);
					writer.Flush();
					Assert.Equal("0001\n", ReadShared(path));
				}
			}
			finally {
				File.Delete(path);
			}
		}

		[Fact]
		public void ReadShared_AMissingFile_IsEmpty() {
			Assert.Equal("", ReadShared(Path.Combine(Path.GetTempPath(), $"SplitListTests_{Guid.NewGuid():N}.txt")));
		}

		[Fact]
		public void StatusLines_NoSnapshot_None() {
			Assert.Equal([new StatusLine("Splits: none", LineKind.Title)], StatusLines(null));
			Assert.Equal([new StatusLine("Splits: none", LineKind.Title)], StatusLines(new Snapshot([], ["AllSplits"])));
		}

		[Fact]
		public void StatusLines_ANote_InTheTitle_RedForAProblem() {
			Assert.Equal([new StatusLine("Splits (no SplitList folder)", LineKind.Title)], StatusLines(new Snapshot([], [], "no SplitList folder")));
			Assert.Equal(LineKind.Problem, StatusLines(new Snapshot([], [], "levels don't line up", true))[0].Kind);
		}

		[Fact]
		public void StatusLines_RowsKeptWithAProblem_KeepTheirHeadings() {
			Row row = new("A", 0, false, false, new Cell("1:00"), [new Part("A", 0, new Cell("1:00"))], ["AllSplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([row], ["AllSplits"], "levels don't line up", true));
			Assert.Equal([LineKind.Problem, LineKind.Header, LineKind.Outer, LineKind.Root], lines.Select(l => l.Kind));
		}

		[Fact]
		public void StatusLines_APartPerLine_TheRunRepeatedInside_TheSegmentAtThePartsLevel() {
			Cell gold = new("−2.0", true, Highlight.Gold);
			Cell blank = new("");
			Row current = new("--Drive By", 2, true, false, new Cell("1:00"), [new Part("Drive By", 2, new Cell("0:10"), true)], ["AllSplits"]);
			Row scrolled = new("-{Grove Street Part 1}Jeffrey", 1, false, true, gold,
				[new Part("Jeffrey", 2, null), new Part("Grove Street Part 1", 1, gold)], ["SansSubsplits_Level2"]);
			Row plain = new("{Los Santos}{Los Santos Finale}TGS", 0, false, false, new Cell("4:40"),
				[new Part("TGS", 2, null), new Part("Los Santos Finale", 1, null), new Part("Los Santos", 0, new Cell("4:40"))],
				["SansSubsplits_Level2", "SansSubsplits"]);
			string jeffrey = "-{Grove Street Part 1}Jeffrey\nIn SansSubsplits_Level2";
			string tgs = "{Los Santos}{Los Santos Finale}TGS\nIn SansSubsplits_Level2, SansSubsplits";
			Assert.Equal([
				new StatusLine("Splits", LineKind.Title),
				new StatusLine("", LineKind.Header, new Cell("Run"), new Cell("Segment"), 0,
					"Run, then each line's segment, nested by level: SansSubsplits, SansSubsplits_Level2, AllSplits"),
				new StatusLine("Drive By", LineKind.Selected, new Cell("1:00"), new Cell("0:10"), 2, "--Drive By\nIn AllSplits", true, "      ┌─ ", Path: "      ┌─"),
				// Jeffrey isn't in a window at its levels, so it's left out, though Drive By is in its section
				new StatusLine("Grove Street Part 1", LineKind.ScrolledTo, gold, gold, 1, jeffrey, true, "   ┌─ "),
				// TGS isn't either; Los Santos Finale isn't, but isn't alone in Los Santos, so it stays without a segment
				new StatusLine("Los Santos Finale", LineKind.Inner, new Cell("4:40"), blank, 1, tgs, false, "   ├─ ", true),
				// The top level hangs from a root below the last line
				new StatusLine("Los Santos", LineKind.Outer, new Cell("4:40"), new Cell("4:40"), 0, tgs, false, "┌─ "),
				new StatusLine("", LineKind.Root, Guides: "╵"),
			], StatusLines(new Snapshot([current, scrolled, plain], ["SansSubsplits", "SansSubsplits_Level2", "AllSplits"])));
		}

		[Fact]
		public void StatusLines_TheRoot_BelowTheLastLine_OnThePathFromAMarkedTopLevelLine() {
			Row first = new("A", 0, true, false, new Cell("1:00"), [new Part("A", 0, new Cell("1:00"), true)], ["AllSplits"]);
			Row last = new("B", 0, false, false, new Cell("2:00"), [new Part("B", 0, new Cell("1:00"))], ["AllSplits"]);
			Assert.Equal(new StatusLine("", LineKind.Root, Guides: "╵", Path: "╵"), StatusLines(new Snapshot([first, last], ["AllSplits"]))[^1]);
		}

		[Fact]
		public void RootGuides_TheTopLevelsVertical_EndsAtTheRoot() {
			Assert.Equal("╵", RootGuides("├─ "));
			Assert.Equal("╵", RootGuides("┌─"));
			Assert.Equal("╵", RootGuides("│"));
		}

		[Fact]
		public void RootGuides_VerticalsFurtherRight_JoinedToTheRoot() {
			Assert.Equal("└──┘", RootGuides("│  ├─ "));
			Assert.Equal("└──┴──┘", RootGuides("│  │  ┌─ "));
			Assert.Equal("╶──┘", RootGuides("   ├──── "));
		}

		[Fact]
		public void Shorten_ANameThatFits_Whole() {
			Assert.Equal("Taxi Fare 50", Shorten("Taxi Fare 50", 12));
			Assert.Equal("Taxi Fare 50", Shorten("Taxi Fare 50", 40));
		}

		[Fact]
		public void Shorten_ALongerName_CutBeforeAnEllipsis_InTheRoom() {
			Assert.Equal("SS25: To the left…", Shorten("SS25: To the left of first arch on parallel wall", 19));
			Assert.Equal("Taxi Fare…", Shorten("Taxi Fare 50", 11));
		}

		[Fact]
		public void Shorten_NoRoom_TheEllipsisAlone() {
			Assert.Equal("…", Shorten("Taxi Fare 50", 1));
			Assert.Equal("…", Shorten("Taxi Fare 50", 0));
		}

		[Fact]
		public void RootGuides_NoVertical_None() {
			Assert.Equal("", RootGuides(""));
			Assert.Equal("", RootGuides("      "));
		}

		[Fact]
		public void StatusLines_TheCurrentSplit_ItsOwnLineAndSectionsSelected() {
			Row row = new("{San Fierro}{San Fierro Finale}Yay Ka-Boom-Boom", 0, true, false, new Cell("5:25:50"),
				[new Part("Yay Ka-Boom-Boom", 2, new Cell("3:18"), true), new Part("San Fierro Finale", 1, new Cell("25:47"), true),
					new Part("San Fierro", 0, new Cell("2:40:16"), true)],
				["AllSplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([row], ["SansSubsplits", "SansSubsplits_Level2", "AllSplits"]))[2..^1];
			Assert.Equal([("Yay Ka-Boom-Boom", LineKind.SelectedInner, true, true), ("San Fierro Finale", LineKind.SelectedInner, true, true),
					("San Fierro", LineKind.Selected, true, false)],
				lines.Select(l => (l.Text, l.Kind, l.Marked, l.RepeatsRun)));
			Assert.All(lines, l => Assert.Equal("5:25:50", l.Run!.Text));
		}

		[Fact]
		public void StatusLines_OnASubsplit_TheSectionsHoldingItSelected() {
			// GTASA 100% on T11: at L4 to L6 Start The Green Sabre holds it, above that The Green Sabre's sections
			Row t11 = new("------T11", 6, true, false, new Cell("1:48:59"), [new Part("T11", 6, new Cell("0:11"), true)], ["AllSplits"]);
			Row start = new("---Start The Green Sabre", 3, false, false, new Cell("1:49:23"),
				[new Part("Start The Green Sabre", 6, new Cell("0:23")), new Part("Start The Green Sabre", 3, new Cell("0:35"), true)], ["AllSplits"]);
			Row sabre = new("{Los Santos}{LS Finale}The Green Sabre", 0, false, false, new Cell("1:52:12"),
				[new Part("The Green Sabre", 4, new Cell("0:01")), new Part("The Green Sabre", 3, new Cell("2:49")), new Part("The Green Sabre", 2, new Cell("3:24"), true),
					new Part("LS Finale", 1, null, true), new Part("Los Santos", 0, new Cell("1:52:12"), true)],
				["AllSplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([t11, start, sabre], ["SansSubsplits", "AllSplits"]))[2..^1];
			Assert.Equal([
				(6, LineKind.Selected, true),
				(6, LineKind.Inner, false), (3, LineKind.Selected, true),
				(4, LineKind.Inner, false), (3, LineKind.Inner, false), (2, LineKind.SelectedInner, true), (1, LineKind.SelectedInner, true), (0, LineKind.Selected, true),
			], lines.Select(l => (l.Level, l.Kind, l.Marked)));
		}

		[Fact]
		public void TreeGuides_EachSectionsVerticalFromItsFirstLine_PastDeeperLines() {
			// A in B, D in E, B and E in F
			Assert.Equal(["   ┌─ ", "┌─ ", "│  ┌─ ", "├─ ", ""], TreeGuides([2, 1, 2, 1, 0]));
		}

		[Fact]
		public void TreeGuides_ALineLevelsDeeperThanItsSection_BranchesToItsName() {
			Assert.Equal(["   ┌──── ", "   ├──── ", "┌─ ", ""], TreeGuides([3, 3, 1, 0]));
		}

		[Fact]
		public void TreeGuides_NoSectionBelow_OneLevelUpBelowTheLast() {
			Assert.Equal(["┌─ ", "├─ "], TreeGuides([1, 1]));
			Assert.Equal(["   ┌─ ", "┌─ "], TreeGuides([2, 1]));
		}

		[Fact]
		public void TreeGuides_TopLevelOnly_None() {
			Assert.Equal(["", ""], TreeGuides([0, 0]));
		}

		[Fact]
		public void PathGuides_EachLinesBranchFromItsSectionsVertical_ThenTheVerticalDownToIt() {
			// Plant 3 and its siblings in Enter Mothership; it and the inner AYGTSF in AYGTSF; that in Badlands, two levels up;
			// Badlands and Wear Flowers on the root
			int[] levels = [5, 5, 5, 4, 4, 3, 1, 2];
			bool[] onPath = [true, false, false, true, false, true, true, false];
			Assert.Equal([
				"            ┌─",
				"            │",
				"            │",
				"         ┌─",
				"         │",
				"   ┌────",
				"┌─",
				"│",
			], PathGuides(levels, onPath));
		}

		[Fact]
		public void PathGuides_ASiblingOnThePathToo_TheVerticalThroughItsBranch() {
			Assert.Equal(["   ┌─", "   ├─", "┌─"], PathGuides([2, 2, 1], [true, true, true]));
		}

		[Fact]
		public void PathGuides_NoneOnIt_None() {
			Assert.Equal(["", "", "", ""], PathGuides([2, 1, 2, 1], [false, false, false, false]));
		}

		[Fact]
		public void FirstMember_FromTheCurrentLine_ElseTheRunsStartOrEnd() {
			Window running = Level("AllSplits", 2, [3, 4, 5, 6, 7], 2);
			Assert.Equal(2, FirstMember(running, "5", false, 20));
			Assert.Null(FirstMember(running, "-", false, 20));
			Window idle = Level("AllSplits", 2, [1, 2, 3, 4, 5]);
			Assert.Equal(0, FirstMember(idle, "-", false, 20));
			Assert.Equal(15, FirstMember(Level("AllSplits", 2, [16, 17, 18, 19, 20]), "-", true, 20));
			Assert.Null(FirstMember(Level("AllSplits", 2, [16, 17, 18, 19, 20]), "-", true, 0));
			Assert.Null(FirstMember(Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true), "14", false, 20));
		}

		[Fact]
		public void FirstMember_NumbersPlacingTheWindowBeforeTheLevelsStart_Unknown() {
			// An undone split: LiveSplit has written the new number, not yet the window with the current line one higher
			Assert.Null(FirstMember(Level("AllSplits", 2, [1, 2, 3, 4, 5], 2), "2", false, 20));
			Assert.Equal(0, FirstMember(Level("AllSplits", 2, [1, 2, 3, 4, 5], 2), "3", false, 20));
			// Finished, with fewer members than the window has lines
			Assert.Null(FirstMember(Level("AllSplits", 2, [16, 17, 18, 19, 20]), "-", true, 3));
			Assert.Equal(0, FirstMember(Level("AllSplits", 2, [16, 17, 18, 19, 20]), "-", true, 5));
		}

		[Fact]
		public void Merge_WindowAheadOfItsNumber_MergesWithItsPlaceUnknown() {
			// Error LSL1: the first line placed at member -1 indexed the completed splits with it
			Window window = Level("AllSplits", 2, [1, 2, 3, 4, 5], 2);
			window = window with { First = FirstMember(window, "2", false, 20), Total = 20 };
			List<Row>? rows = Merge([window], false, new Completed(Raws(1, 2), [0, 1]));
			Assert.NotNull(rows);
			Assert.Equal(Raws(1, 2, 3, 4, 5), rows.Select(r => r.Raw));
		}

		[Fact]
		public void Merge_Hidden_NoneWhereTheWindowsPlaceEverySplitNextToTheOneBefore_MoreAfter() {
			// NestedRun on 0005: 13 top-level splits, 17 at Level2, 20 in all
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1) with { First = 0, Total = 13 },
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2) with { First = 0, Total = 17 },
				Level("AllSplits", 2, [3, 4, 5, 6, 7], 2) with { First = 2, Total = 20 });
			Assert.All(rows, r => Assert.Null(r.MoreBefore));
			Assert.Equal([false, false, false, false, false, false, false, false, false, false, false, true], rows.Select(r => r.MoreAfter));
		}

		[Fact]
		public void Merge_Hidden_WherePlacesAreUnknown_EveryGapOutsideAllSplits_OneDeeperThanTheWindowAcrossIt() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1, true),
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2, true),
				Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true));
			Assert.Equal([0, 2, 2, null, null, null, null, 2, 1, 1, 1, 1], rows.Select(r => r.MoreBefore));
			Assert.True(rows[^1].MoreAfter);
		}

		[Fact]
		public void Merge_Hidden_AfterTheFinish_BeforeTheFirst_NoneAfterTheLast() {
			List<Row> rows = Merged(true,
				Level("SansSubsplits", 0, [16, 17, 18, 19, 20]) with { First = 8, Total = 13 },
				Level("SansSubsplits_Level2", 1, [16, 17, 18, 19, 20]) with { First = 12, Total = 17 },
				Level("AllSplits", 2, [16, 17, 18, 19, 20]) with { First = 15, Total = 20 });
			Assert.Equal([0, null, null, null, null], rows.Select(r => r.MoreBefore));
			Assert.False(rows[^1].MoreAfter);
		}

		[Fact]
		public void Merge_Hidden_BeforeTheStart_NoneBeforeTheFirst() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12]) with { First = 0, Total = 13 },
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8]) with { First = 0, Total = 17 },
				Level("AllSplits", 2, [1, 2, 3, 4, 5]) with { First = 0, Total = 20 });
			Assert.Equal(Raws(1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12), rows.Select(r => r.Raw));
			// AllSplits shows the run's first five splits, so nothing before them or between them. 0007 is hidden, but the
			// counts only say one deeper split lies somewhere after 0005, so every gap after it gets a line: at Level2's depth
			// where Level2 shows every Level2 split across it, else one level up
			Assert.Equal([null, null, null, null, null, 2, 2, 1, 1, 1, 1], rows.Select(r => r.MoreBefore));
			Assert.True(rows[^1].MoreAfter);
		}

		[Fact]
		public void StatusLines_LinesForSplitsNotShown_AtTheirLevels() {
			Row a = new("--A", 2, false, false, new Cell("1:00"), [new Part("A", 2, new Cell("0:10"))], ["AllSplits"], MoreBefore: 1);
			Row b = new("B", 0, false, false, new Cell("2:00"), [new Part("B", 0, new Cell("1:00"))], ["AllSplits"], MoreBefore: 1, MoreAfter: true);
			List<StatusLine> lines = StatusLines(new Snapshot([a, b], ["SansSubsplits", "SansSubsplits_Level2", "AllSplits"]))[2..^1];
			Assert.Equal([("…", LineKind.More, 1), ("A", LineKind.Outer, 2), ("…", LineKind.More, 1), ("B", LineKind.Outer, 0), ("…", LineKind.More, 0)],
				lines.Select(l => (l.Text, l.Kind, l.Level)));
			Assert.Equal(["   ┌─ ", "   │  ┌─ ", "   ├─ ", "┌─ ", "├─ "], lines.Select(l => l.Guides));
			Assert.All(lines.Where(l => l.Kind == LineKind.More), l => Assert.Equal(("…", "…"), (l.Run!.Text, l.Segment!.Text)));
		}

		// Learning to Fly in Desert Second Half in Desert, seen only at the two top levels unless given its own segment
		static Row Fly(Cell? own = null) => new("{Desert}{Desert Second Half}Learning to Fly", 0, false, false, new Cell("6:20:48"),
			[new Part("Learning to Fly", 2, own), new Part("Desert Second Half", 1, new Cell("9:25")), new Part("Desert", 0, new Cell("54:57"))], ["SansSubsplits"]);

		[Fact]
		public void Alone_AnOwnNameNoWindowShows_AloneInItsSection_LeftOut() {
			Assert.Equal(1, Alone(Fly(), 0));
			Assert.Equal(1, Alone(Fly(), 1));
			Assert.Equal(1, Alone(Fly(), null));
		}

		[Fact]
		public void Alone_ASplitShownInsideTheSection_KeepsIt() {
			// The row before ends inside Desert Second Half
			Assert.Equal(0, Alone(Fly(), 2));
		}

		[Fact]
		public void Alone_AWindowShowsIt_KeepsIt() {
			Assert.Equal(0, Alone(Fly(new Cell("7:54")), 0));
		}

		[Fact]
		public void Alone_EachSectionOutward_WhileNoWindowShowsIt_NeverTheOutermost() {
			Row credits = new("{RTLS}{The End}{End of the Line Part 3}Credits", 0, false, false, new Cell("13:12:01"),
				[new Part("Credits", 3, null), new Part("End of the Line Part 3", 2, null), new Part("The End", 1, null), new Part("RTLS", 0, new Cell("1:46:52"))],
				["SansSubsplits"]);
			Assert.Equal(3, Alone(credits, 0));
			Row single = new("A", 0, false, false, new Cell("1:00"), [new Part("A", 0, null)], ["SansSubsplits"]);
			Assert.Equal(0, Alone(single, null));
		}

		[Fact]
		public void Alone_AShownOwnName_KeepsTheSectionsBetween() {
			// Buy Wang Cars at All, its section at L4 to L6 not in those windows
			Row wang = new("---Buy Wang Cars", 3, false, false, new Cell("5:26:36"),
				[new Part("Buy Wang Cars", 6, new Cell("0:23")), new Part("Buy Wang Cars", 3, null), new Part("Yay", 0, new Cell("1:00"))], ["AllSplits"]);
			Assert.Equal(0, Alone(wang, 0));
		}

		[Fact]
		public void StatusLines_AnOwnNameAloneInItsSection_LeftOut_WithTheLineForSplitsNotShownAboveIt() {
			Row before = new("Top", 0, false, false, new Cell("5:00:00"), [new Part("Top", 0, new Cell("1:00"))], ["SansSubsplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([before, Fly() with { MoreBefore = 1 }], ["SansSubsplits", "SansSubsplits_Level2", "AllSplits"]))[2..^1];
			Assert.Equal(["Top", "Desert Second Half", "Desert"], lines.Select(l => l.Text));
			Assert.Equal(["┌─ ", "│  ┌─ ", "├─ "], lines.Select(l => l.Guides));
		}

		[Fact]
		public void StatusLines_AShownOwnName_KeepsTheLineForSplitsNotShownAboveIt() {
			Row before = new("Top", 0, false, false, new Cell("5:00:00"), [new Part("Top", 0, new Cell("1:00"))], ["SansSubsplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([before, Fly(new Cell("7:54")) with { MoreBefore = 1 }],
				["SansSubsplits", "SansSubsplits_Level2", "AllSplits"]))[2..^1];
			Assert.Equal(["Top", "…", "Learning to Fly", "Desert Second Half", "Desert"], lines.Select(l => l.Text));
		}

		[Fact]
		public void StatusLines_AnOwnNameNoWindowShows_AmongShownSplits_LeftOut_KeepingTheLineForSplitsNotShownAboveIt() {
			// Enter Mothership past L5's window, which ends at Plant 5; Plant 6 onward hidden
			Row start = new("---Start AYGTSF", 3, false, false, new Cell("2:42:30"), [new Part("Start AYGTSF", 3, new Cell("1:03"))], ["AllSplits"]);
			Row plant = new("----Plant 5", 4, false, false, new Cell("2:42:48"), [new Part("Plant 5", 4, new Cell("0:08"))], ["AllSplits"]);
			Row mothership = new("---Enter Mothership", 3, false, false, new Cell("2:43:51"),
				[new Part("Enter Mothership", 4, null), new Part("Enter Mothership", 3, new Cell("1:21"))], ["SansSubsplits_Level4"], MoreBefore: 4);
			List<StatusLine> lines = StatusLines(new Snapshot([start, plant, mothership], ["SansSubsplits", "SansSubsplits_Level2", "SansSubsplits_Level3",
				"SansSubsplits_Level4", "SansSubsplits_Level5", "AllSplits"]))[2..^1];
			Assert.Equal([("Start AYGTSF", 3), ("Plant 5", 4), ("…", 4), ("Enter Mothership", 3)], lines.Select(l => (l.Text, l.Level)));
			Assert.Equal("1:21", lines[^1].Segment!.Text);
		}

		// NestedRun after 13 splits: 0001 to 0013 completed
		static readonly Completed THIRTEEN = new(NESTED[..13], NESTED[..13].Select(raw => raw.TakeWhile(c => c == '-').Count()).ToList());

		// The window with every line completed
		static Window Done(Window window) => window with { Lines = window.Lines.Select(line => line with { Delta = "+1.0" }).ToList() };

		[Fact]
		public void ParseCompleted_RawNamesAndDepths() {
			Dictionary<string, string> files = new() {
				["Names_Raw.txt"] = "0001\n-0002\n--\n",
				["Names_Indent_0.txt"] = "0001\n\n\n",
				["Names_Indent_1.txt"] = "\n0002\n\n",
				["Names_Indent_2.txt"] = "\n\n\n",
			};
			Completed? completed = ParseCompleted(3, file => files.GetValueOrDefault(file, ""));
			Assert.NotNull(completed);
			Assert.Equal(["0001", "-0002", "--"], completed.Raws);
			// The blank name's depth from its dashes
			Assert.Equal([0, 1, 2], completed.Depths);
			files["Names_Indent_2.txt"] = "\n";
			Assert.Null(ParseCompleted(3, file => files.GetValueOrDefault(file, "")));
		}

		[Fact]
		public void FirstFromCompleted_WhereItsCompletedLinesMatch() {
			Assert.Equal(2, FirstFromCompleted(Done(Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true)), THIRTEEN));
			Assert.Equal(0, FirstFromCompleted(Done(Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2, true)), THIRTEEN));
			Assert.Equal(0, FirstFromCompleted(Done(Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1, true)), THIRTEEN));
			// The completed lines followed by upcoming ones: 0013 completed, 0014 and 0015 not
			Window edge = Level("AllSplits", 2, [13, 14, 15], 0, true);
			edge = edge with { Lines = [edge.Lines[0] with { Delta = "+1.0" }, .. edge.Lines.Skip(1)] };
			Assert.Equal(12, FirstFromCompleted(edge, THIRTEEN));
		}

		[Fact]
		public void FirstFromCompleted_NoCompletedLine_OrNoPlace_Unknown() {
			Assert.Null(FirstFromCompleted(Level("AllSplits", 2, [14, 15, 16], 0, true), THIRTEEN));
			Window wrong = Done(Level("AllSplits", 2, [3, 4, 5], 0, true));
			wrong = wrong with { Lines = [.. wrong.Lines.Take(2), Line("--0099", scrolled: false) with { Delta = "+1.0" }] };
			Assert.Null(FirstFromCompleted(wrong, THIRTEEN));
		}

		[Theory]
		[InlineData(1, 4, 2)]
		[InlineData(0, 8, 1)]
		[InlineData(-1, 2, 0)]
		public void HiddenDepth_TheShallowestCompletedBetween(int from, int to, int depth) {
			Assert.Equal(depth, HiddenDepth(from, to, THIRTEEN));
		}

		[Theory]
		[InlineData(5, 20)]
		[InlineData(1, 2)]
		public void HiddenDepth_NotAllCompleted_OrNoneBetween_Unknown(int from, int to) {
			Assert.Null(HiddenDepth(from, to, THIRTEEN));
			Assert.Null(HiddenDepth(1, 4, null));
		}

		[Fact]
		public void Merge_Hidden_ScrolledIntoThePast_TheCompletedSplitsPlaceEveryLine() {
			// Scrolled up to 0005 after 13 splits: without the completed splits every gap outside AllSplits got a line
			List<Window> levels = [
				Done(Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1, true)),
				Done(Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2, true)),
				Done(Level("AllSplits", 2, [3, 4, 5, 6, 7], 2, true)),
			];
			levels = levels.Select(window => window with { First = FirstFromCompleted(window, THIRTEEN) }).ToList();
			List<Row>? rows = Merge(levels, false, THIRTEEN);
			Assert.NotNull(rows);
			Assert.All(rows, r => Assert.Null(r.MoreBefore));
			Assert.True(rows[^1].MoreAfter);
		}

		[Fact]
		public void Merge_Hidden_ACompletedGap_AtTheShallowestHiddenDepth() {
			// 0003 and 0004, both two deep, hidden between 0002 and 0005
			List<Window> levels = [
				Done(Level("SansSubsplits", 0, [1, 9], 1, true)),
				Done(Level("SansSubsplits_Level2", 1, [2, 5], 1, true)),
				Done(Level("AllSplits", 2, [5, 6], 0, true)),
			];
			levels = levels.Select(window => window with { First = FirstFromCompleted(window, THIRTEEN) }).ToList();
			List<Row>? rows = Merge(levels, false, THIRTEEN);
			Assert.NotNull(rows);
			Assert.Equal(Raws(1, 2, 5, 6, 9), rows.Select(r => r.Raw));
			Assert.Equal([null, null, 2, null, 1], rows.Select(r => r.MoreBefore));
		}

		static StatusLine SplitAt(int level) => new("x", LineKind.Outer, new Cell(""), new Cell(""), level);
		static StatusLine MoreAt(int level) => new("…", LineKind.More, new Cell(""), new Cell(""), level);

		[Fact]
		public void EmptySections_ALineForSplitsNotShown_AllItsSectionHolds_Goes() {
			// GTASA's Tanker Commander: shown at Level2, its own splits not, after Badlands
			Assert.Equal([1], EmptySections([SplitAt(1), MoreAt(2), SplitAt(1)]));
			Assert.Equal([1, 2], EmptySections([SplitAt(1), MoreAt(2), MoreAt(3), SplitAt(1)]));
			Assert.Equal([0], EmptySections([MoreAt(1), SplitAt(0)]));
		}

		[Fact]
		public void EmptySections_ASplitShownInTheSection_KeepsIt() {
			// SCYR Cp 13 to 18 shown after the …, all in Scotch Bonnet Yellow Route
			Assert.Empty(EmptySections([MoreAt(2), SplitAt(2), SplitAt(2), SplitAt(1)]));
			Assert.Empty(EmptySections([SplitAt(2), MoreAt(2), SplitAt(1)]));
			// A deeper split above it, inside the same section
			Assert.Empty(EmptySections([SplitAt(4), MoreAt(2), SplitAt(1)]));
		}

		[Fact]
		public void EmptySections_NoSectionBelow_Stays() {
			Assert.Empty(EmptySections([SplitAt(0), MoreAt(0)]));
			Assert.Empty(EmptySections([SplitAt(0), MoreAt(1)]));
		}

		[Fact]
		public void StatusLines_ALineForSplitsNotShown_AloneInAShownSplitsSection_LeftOut() {
			Row badlands = new("Badlands", 0, false, false, new Cell("1:00:00"), [new Part("Badlands", 1, new Cell("2:15"))], ["SansSubsplits_Level2"]);
			Row tanker = new("Tanker Commander", 0, false, false, new Cell("1:03:00"), [new Part("Tanker Commander", 1, new Cell("3:37"))], ["SansSubsplits_Level2"],
				MoreBefore: 2);
			Row santos = new("Los Santos", 0, false, false, new Cell("1:10:00"), [new Part("Los Santos", 0, new Cell("1:10:00"))], ["SansSubsplits"]);
			List<StatusLine> lines = StatusLines(new Snapshot([badlands, tanker, santos], ["SansSubsplits", "SansSubsplits_Level2", "AllSplits"]))[2..^1];
			Assert.Equal(["Badlands", "Tanker Commander", "Los Santos"], lines.Select(l => l.Text));
			Assert.Equal(["   ┌─ ", "   ├─ ", "┌─ "], lines.Select(l => l.Guides));
		}

		[Fact]
		public void Merge_Parts_CurrentWhereALevelHasTheSplitAsItsCurrent() {
			List<Row> rows = Merged(false,
				Level("SansSubsplits", 0, [1, 9, 10, 11, 12], 1),
				Level("SansSubsplits_Level2", 1, [1, 2, 5, 6, 8], 2),
				Level("AllSplits", 2, [3, 4, 5, 6, 7], 2));
			Assert.Equal(["-0005", "0009"], rows.Where(r => r.Parts.Any(p => p.Current)).Select(r => r.Raw));
			Assert.Equal(["-0005"], rows.Where(r => r.Current).Select(r => r.Raw));
		}
	}
}
