using System.Globalization;
using System.Text.Json;
using Xunit;
using Panel = StreamAssistant2.LeftPanel.Panel;
using PanelSet = StreamAssistant2.LeftPanel.PanelSet;

namespace StreamAssistant2.Tests {
	public class LeftPanelTests {
		[Fact]
		public void Parse_ReadsTheFileShape() {
			var sets = LeftPanel.Parse("""
				{
					"default": { "Always": [ "Sellout Basics" ], "Panels": [ { "Show": [ "Sellout Key Count" ], "Seconds": 220 } ] },
					"GTA3": {
						"Always": [ "Splits Basics" ],
						"Panels": [
							{
								"Show": [ "Splits List Labels 1", "Splits List Values 1" ],
								"Text": { "Livesplit: SL - Header Right 1": "Finished at" },
								"File": { "Livesplit: SL - Current 1": "D:\\Text\\Time_Run_Current.txt" },
								"Seconds": 50
							},
							{ "Show": [ "Splits Key Count" ], "Seconds": 1.5 }
						]
					}
				}
				""");
			Assert.Equal(2, sets.Count);
			Assert.Equal(["Sellout Basics"], sets["default"].Always);
			PanelSet gta3 = sets["GTA3"];
			Assert.Equal(["Splits Basics"], gta3.Always);
			Assert.Equal(2, gta3.Panels.Count);
			Panel first = gta3.Panels[0];
			Assert.Equal(["Splits List Labels 1", "Splits List Values 1"], first.Show);
			Assert.Equal("Finished at", first.Text["Livesplit: SL - Header Right 1"]);
			Assert.Equal(@"D:\Text\Time_Run_Current.txt", first.File["Livesplit: SL - Current 1"]);
			Assert.Equal(50, first.Seconds);
			Assert.Equal(1.5, gta3.Panels[1].Seconds);
			Assert.Empty(gta3.Panels[1].Text);
			Assert.Empty(gta3.Panels[1].File);
		}

		[Fact]
		public void Parse_PropertyNamesAreCaseInsensitive() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "always": [ "A" ], "panels": [ { "show": [ "B" ], "text": { "S": "t" }, "file": { "F": "f" }, "seconds": 5 } ] } }""");
			Panel panel = Assert.Single(sets["GTA3"].Panels);
			Assert.Equal(["A"], sets["GTA3"].Always);
			Assert.Equal(["B"], panel.Show);
			Assert.Equal("t", panel.Text["S"]);
			Assert.Equal("f", panel.File["F"]);
			Assert.Equal(5, panel.Seconds);
		}

		[Fact]
		public void Parse_GameNamesAreCaseInsensitive() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Panels": [] } }""");
			Assert.True(sets.ContainsKey("gta3"));
		}

		[Fact]
		public void Parse_MissingListsAndMaps_AreEmpty() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Panels": [ { "Seconds": 5 } ] }, "GTA4": {} }""");
			Panel panel = Assert.Single(sets["GTA3"].Panels);
			Assert.Empty(sets["GTA3"].Always);
			Assert.Empty(panel.Show);
			Assert.Empty(panel.Text);
			Assert.Empty(panel.File);
			Assert.Empty(sets["GTA4"].Always);
			Assert.Empty(sets["GTA4"].Panels);
		}

		[Theory]
		[InlineData(""", "Seconds": 0""")]
		[InlineData(""", "Seconds": -1""")]
		[InlineData("")]
		public void Parse_PanelsWithoutPositiveSeconds_AreSkipped(string seconds) {
			var sets = LeftPanel.Parse($$"""{ "GTA3": { "Panels": [ { "Show": [ "A" ]{{seconds}} }, { "Show": [ "B" ], "Seconds": 5 } ] } }""");
			Panel panel = Assert.Single(sets["GTA3"].Panels);
			Assert.Equal(["B"], panel.Show);
		}

		[Fact]
		public void Parse_EmptyShow_IsKeptAsAPause() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Panels": [ { "Show": [], "Seconds": 1 } ] } }""");
			Panel panel = Assert.Single(sets["GTA3"].Panels);
			Assert.Empty(panel.Show);
			Assert.Equal(1, panel.Seconds);
		}

		[Fact]
		public void Parse_GroupNames_LosePrefixPaddingAndBlanks() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Always": [ "_Left Panel: Splits Basics" ], "Panels": [ { "Show": [ " Splits Key Count ", "", "  ", null ], "Seconds": 5 } ] } }""");
			Assert.Equal(["Splits Basics"], sets["GTA3"].Always);
			Assert.Equal(["Splits Key Count"], sets["GTA3"].Panels[0].Show);
		}

		[Fact]
		public void Parse_NullEntriesAndValues_AreSkipped() {
			var sets = LeftPanel.Parse("""{ "GTA3": null, "GTA4": { "Panels": [ null, { "Show": [ "A" ], "Text": { "S": null, "T": "t" }, "File": { "F": null }, "Seconds": 5 } ] } }""");
			Assert.False(sets.ContainsKey("GTA3"));
			Panel panel = Assert.Single(sets["GTA4"].Panels);
			Assert.Equal(["T"], panel.Text.Keys.ToList());
			Assert.Empty(panel.File);
		}

		[Fact]
		public void Parse_EmptyObject_IsEmpty() {
			Assert.Empty(LeftPanel.Parse("{}"));
		}

		[Fact]
		public void Parse_Previous_IsReadLikePanels_AndMissingIsEmpty() {
			var sets = LeftPanel.Parse("""
				{
					"SWAT4": {
						"Panels": [ { "Show": [ "Splits Current" ], "Seconds": 30 } ],
						"Previous": [ { "Show": [ "_Left Panel: Nothing" ], "Seconds": 2 }, { "Show": [ "Splits Previous" ], "Seconds": 0 } ]
					},
					"GTA3": { "Panels": [] }
				}
				""");
			Panel previous = Assert.Single(sets["SWAT4"].Previous);
			Assert.Equal(["Nothing"], previous.Show);
			Assert.Equal(2, previous.Seconds);
			Assert.Empty(sets["GTA3"].Previous);
		}

		const string GTA3 = """ "GTA3": { "Always": [ "Splits Basics" ], "Panels": [ { "Show": [ "Splits Key Count" ], "Seconds": 50 } ] } """;

		[Fact]
		public void Parse_NameEntry_SharesThatEntrysPanels_AnyCase() {
			var sets = LeftPanel.Parse($$"""{ {{GTA3}}, "GTA4": "GTA3", "GTA5": "gta3" }""");
			Assert.Same(sets["GTA3"], sets["GTA4"]);
			Assert.Same(sets["GTA3"], sets["GTA5"]);
		}

		[Fact]
		public void Parse_NameChain_IsFollowed() {
			var sets = LeftPanel.Parse($$"""{ "GTA5": "GTA4", "GTA4": "GTA3", {{GTA3}} }""");
			Assert.Same(sets["GTA3"], sets["GTA5"]);
		}

		[Theory]
		[InlineData(""" "GTA4": "GTA9" """, "GTA4")]
		[InlineData(""" "GTA4": "GTA5", "GTA5": "GTA4" """, "GTA4")]
		[InlineData(""" "GTA4": 5 """, "GTA4")]
		[InlineData(""" "GTA4": [ "GTA3" ] """, "GTA4")]
		public void Parse_EntryLeadingToNoPanels_IsSkippedAndLogged(string entries, string skipped) {
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			Dictionary<string, PanelSet> sets;
			try {
				sets = LeftPanel.Parse($$"""{ {{GTA3}}, {{entries}} }""");
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			Assert.False(sets.ContainsKey(skipped));
			Assert.True(sets.ContainsKey("GTA3"));
			lock (log) {
				Assert.Contains(log, l => l.Contains("leftpanel.json: skipped " + skipped + ","));
			}
		}

		static readonly Dictionary<string, PanelSet> Known = LeftPanel.Parse("""
			{
				"default": { "Always": [ "Sellout Basics" ], "Panels": [ { "Show": [ "Sellout Goals" ], "Seconds": 30 } ] },
				"GTA3": { "Always": [ "Splits Basics" ], "Panels": [ { "Show": [ "Splits Key Count" ], "Seconds": 50 } ] }
			}
			""");

		[Theory]
		[InlineData("GTA3")]
		[InlineData("gta3")]
		public void Lookup_KnownGame_AnyCase(string name) {
			Assert.Same(Known["GTA3"], LeftPanel.Lookup(Known, name));
		}

		[Fact]
		public void Lookup_UnknownGame_FallsBackToDefault() {
			Assert.Same(Known["default"], LeftPanel.Lookup(Known, "KTANE"));
		}

		[Fact]
		public void Lookup_UnknownGame_WithoutDefault_IsNull() {
			Assert.Null(LeftPanel.Lookup(LeftPanel.Parse("""{ "GTA3": { "Panels": [] } }"""), "KTANE"));
		}

		[RealLeftPanelFact]
		public void RealFile_KeysAreGamesOrDefault_AndNothingIsSkipped() {
			string botInput = ColorData.RealBotInput!;
			string json = File.ReadAllText(Path.Combine(botInput, "leftpanel.json"));
			var sets = LeftPanel.Parse(json);
			HashSet<string> names = Games.Parse(File.ReadAllText(Path.Combine(botInput, "games.json"))).Values.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

			Assert.True(sets.ContainsKey("default"));
			Assert.All(sets.Keys.Where(k => !k.Equals("default", StringComparison.OrdinalIgnoreCase)), k => Assert.Contains(k, names));

			using JsonDocument raw = JsonDocument.Parse(json);
			foreach (JsonProperty entry in raw.RootElement.EnumerateObject()) {
				Assert.True(sets.ContainsKey(entry.Name), $"{entry.Name} has no panels");
				if (entry.Value.ValueKind == JsonValueKind.Object) {
					int rawPanels = entry.Value.TryGetProperty("Panels", out JsonElement panels) ? panels.GetArrayLength() : 0;
					Assert.Equal(rawPanels, sets[entry.Name].Panels.Count);
					int rawPrevious = entry.Value.TryGetProperty("Previous", out JsonElement previous) ? previous.GetArrayLength() : 0;
					Assert.Equal(rawPrevious, sets[entry.Name].Previous.Count);
				}
			}
		}
	}

	public class LeftPanelRotationTests : IDisposable {
		const string SCENE = "!Scene: Left Panel";
		const string COLORED = "!Scene: Left Panel Colored";

		readonly List<string> _calls = new();
		readonly List<string> _log = new();
		readonly CancellationTokenSource _cts = new();
		readonly Dictionary<string, List<string>> _items = new() {
			[SCENE] = ["Full: Blue", "_Left Panel: Basics", "_Left Panel: Labels 1", "_Left Panel: Values 1", "_Left Panel: Values 2", "_Left Panel: Key Count"],
			[COLORED] = ["_Left Panel: Labels 1", "_Left Panel: Gold"],
		};
		readonly HashSet<int> _splitAtWaits = new();
		int _stopAfterWaits = 1;
		int _waits;
		Func<bool> _ready = () => true;
		string? _failingSource;

		public LeftPanelRotationTests() {
			ConsoleLogger.LineLogged += OnLine;
		}

		public void Dispose() {
			ConsoleLogger.LineLogged -= OnLine;
		}

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		List<string> Lines(string text) { lock (_log) return _log.Where(l => l.Contains(text)).ToList(); }

		LeftPanel.ObsCalls Calls() => new(
			() => _ready(),
			scene => { _calls.Add($"list {scene}"); return _items[scene]; },
			(scene, source, on) => _calls.Add($"{(on ? "show" : "hide")} {source} | {scene}"),
			(input, key, value) => {
				if (input == _failingSource) {
					throw new InvalidOperationException("boom");
				}
				_calls.Add($"{key} {input} = {value}");
			},
			(time, token) => {
				_calls.Add("wait " + (time == Timeout.InfiniteTimeSpan ? "forever" : time.TotalSeconds.ToString(CultureInfo.InvariantCulture)));
				if (_splitAtWaits.Contains(++_waits)) {
					LeftPanel.OnSplit();
				}
				if (_waits >= _stopAfterWaits) {
					_cts.Cancel();
				}
				token.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			});

		readonly List<(int?, int?)> _shown = new();

		Task Run(PanelSet set) => LeftPanel.RunAsync("GTA3", set, Calls(), _cts.Token, (panel, previous) => _shown.Add((panel, previous)));

		static (int?, int?) S(int? panel, int? previous = null) => (panel, previous);

		static Panel P(double seconds, params string[] show) => new(show.ToList(), new(), new(), seconds);

		static string Show(string group, string scene = SCENE) => $"show _Left Panel: {group} | {scene}";

		static string Hide(string group, string scene = SCENE) => $"hide _Left Panel: {group} | {scene}";

		List<string> Between(string from, string to) {
			int start = _calls.IndexOf(from) + 1;
			return _calls.Skip(start).Take(_calls.IndexOf(to, start) - start).ToList();
		}

		[Fact]
		public async Task FirstStep_SetsValues_ThenShows_ThenHidesEveryOtherGroup() {
			Panel first = new(["Labels 1", "Values 1"], new() { ["Header"] = "Finished at" }, new() { ["Current 1"] = @"D:\a.txt" }, 50);
			await Run(new PanelSet(["Basics"], [first, P(50, "Key Count")]));
			Assert.Equal([
				$"list {SCENE}", $"list {COLORED}",
				"text Header = Finished at", @"file Current 1 = D:\a.txt",
				Show("Basics"), Show("Labels 1"), Show("Labels 1", COLORED), Show("Values 1"),
				Hide("Values 2"), Hide("Key Count"), Hide("Gold", COLORED),
				"wait 50",
			], _calls);
		}

		[Fact]
		public async Task Steps_SwapOnlyWhatChanges_WaitEachPanelsSeconds_AndWrap() {
			_stopAfterWaits = 4;
			await Run(new PanelSet(["Basics"], [P(50, "Labels 1", "Values 1"), P(30, "Labels 1", "Values 2"), P(1)]));
			Assert.Equal(["wait 50", "wait 30", "wait 1", "wait 50"], _calls.Where(c => c.StartsWith("wait")));
			Assert.Equal([Show("Values 2"), Hide("Values 1")], Between("wait 50", "wait 30"));
			Assert.Equal([Hide("Labels 1"), Hide("Labels 1", COLORED), Hide("Values 2")], Between("wait 30", "wait 1"));
			Assert.Equal([Show("Labels 1"), Show("Labels 1", COLORED), Show("Values 1")], Between("wait 1", "wait 50"));
			Assert.Equal(2, _calls.Count(c => c.StartsWith("list")));
			Assert.Equal([S(0), S(1), S(2), S(0)], _shown);
		}

		[Fact]
		public async Task NotReady_MakesNoObsCalls_WaitsAndTriesAgain() {
			int checks = 0;
			_ready = () => ++checks > 2;
			_stopAfterWaits = 3;
			await Run(new PanelSet(["Basics"], [P(50, "Key Count"), P(50, "Labels 1")]));
			Assert.Equal(["wait 5", "wait 5"], _calls.Take(2));
			Assert.Equal($"list {SCENE}", _calls[2]);
			Assert.Equal("wait 50", _calls[^1]);
			Assert.Contains(Show("Key Count"), _calls);
		}

		[Fact]
		public async Task OnePanel_IsShownOnce_ThenTheRotationEnds() {
			await Run(new PanelSet(["Basics"], [P(50, "Key Count")]));
			Assert.False(_cts.IsCancellationRequested);
			Assert.DoesNotContain(_calls, c => c.StartsWith("wait"));
			Assert.Contains(Show("Key Count"), _calls);
		}

		[Fact]
		public async Task NoPanels_ShowsAlways_HidesTheRest_ThenEnds() {
			await Run(new PanelSet(["Basics"], []));
			Assert.DoesNotContain(_calls, c => c.StartsWith("wait"));
			Assert.Contains(Show("Basics"), _calls);
			Assert.Contains(Hide("Key Count"), _calls);
			Assert.Equal([S(null)], _shown);
		}

		[Fact]
		public async Task Cancelled_EndsQuietly_WithoutObsCalls() {
			_cts.Cancel();
			await Run(new PanelSet(["Basics"], [P(50, "Key Count"), P(50, "Labels 1")]));
			Assert.Empty(_calls);
		}

		[Fact]
		public async Task GroupsOBSDoesntHave_AreLoggedOnce_AndSkipped() {
			_stopAfterWaits = 3;
			await Run(new PanelSet(["Basics"], [P(50, "Key Count", "Big Delta"), P(50, "Little Delta")]));
			Assert.Single(Lines("Left panel: GTA3 has groups OBS doesn't: Big Delta, Little Delta"));
			Assert.DoesNotContain(_calls, c => c.Contains("Delta"));
		}

		[Fact]
		public async Task ThrowingCall_IsLogged_AndTheStepGoesOn() {
			_failingSource = "Header";
			Panel first = new(["Key Count"], new() { ["Header"] = "Finished at" }, new() { ["Current 1"] = @"D:\a.txt" }, 50);
			await Run(new PanelSet(["Basics"], [first, P(50, "Labels 1")]));
			Assert.Single(Lines("Error LPN2: left panel setting the text of Header failed: boom"));
			Assert.Contains(@"file Current 1 = D:\a.txt", _calls);
			Assert.Contains(Show("Key Count"), _calls);
		}

		List<string> Waits() => _calls.Where(c => c.StartsWith("wait")).ToList();

		static PanelSet WithPrevious(List<Panel> panels) => new(["Basics"], panels) { Previous = [P(2), P(25, "Gold"), P(2)] };

		[Fact]
		public async Task SplitDuringAPanel_PlaysPrevious_ThenTheInterruptedPanelAgain() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 5;
			await Run(WithPrevious([P(50, "Labels 1", "Values 1"), P(30, "Key Count")]));
			Assert.Equal(["wait 50", "wait 2", "wait 25", "wait 2", "wait 50"], Waits());
			Assert.Equal([Hide("Labels 1"), Hide("Labels 1", COLORED), Hide("Values 1")], Between("wait 50", "wait 2"));
			Assert.Equal([Show("Gold", COLORED)], Between("wait 2", "wait 25"));
			Assert.Equal([Show("Labels 1"), Show("Labels 1", COLORED), Show("Values 1")], _calls.Skip(_calls.LastIndexOf("wait 2") + 1).SkipLast(1));
			Assert.Equal([S(0), S(0, 0), S(0, 1), S(0, 2), S(0)], _shown);
		}

		[Fact]
		public async Task SplitDuringPrevious_StartsItOver() {
			_splitAtWaits.UnionWith([1, 3]);
			_stopAfterWaits = 7;
			await Run(WithPrevious([P(50, "Labels 1", "Values 1"), P(30, "Key Count")]));
			Assert.Equal(["wait 50", "wait 2", "wait 25", "wait 2", "wait 25", "wait 2", "wait 50"], Waits());
			Assert.Equal([S(0), S(0, 0), S(0, 1), S(0, 0), S(0, 1), S(0, 2), S(0)], _shown);
		}

		[Fact]
		public async Task WithoutPrevious_SplitsAreIgnored() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 3;
			await Run(new PanelSet(["Basics"], [P(50, "Key Count"), P(30, "Labels 1")]));
			Assert.Equal(["wait 50", "wait 30", "wait 50"], Waits());
			Assert.DoesNotContain(Show("Gold", COLORED), _calls);
		}

		[Fact]
		public async Task OnePanelWithPrevious_WaitsForSplits_ThenShowsThePanelAgain() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 5;
			await Run(WithPrevious([P(50, "Key Count")]));
			Assert.Equal(["wait forever", "wait 2", "wait 25", "wait 2", "wait forever"], Waits());
			Assert.Equal(2, _calls.Count(c => c == Show("Key Count")));
			Assert.Equal([S(0), S(0, 0), S(0, 1), S(0, 2), S(0)], _shown);
		}

		static readonly string[] Known = ["Basics", "Labels 1", "Values 1", "Values 2", "Key Count"];

		[Fact]
		public void Transition_UnknownState_ShowsWanted_AndHidesEveryOtherKnownGroup() {
			var (show, hide) = LeftPanel.Transition(null, ["Basics", "Labels 1", "Values 1"], Known);
			Assert.Equal(["Basics", "Labels 1", "Values 1"], show);
			Assert.Equal(["Values 2", "Key Count"], hide);
		}

		[Fact]
		public void Transition_DoubleBufferSwap_ShowsTheHiddenCopy_HidesTheOld_KeepsTheRest() {
			var (show, hide) = LeftPanel.Transition(["Basics", "Labels 1", "Values 1"], ["Basics", "Labels 1", "Values 2"], Known);
			Assert.Equal(["Values 2"], show);
			Assert.Equal(["Values 1"], hide);
		}

		[Fact]
		public void Transition_Pause_HidesTheLastPanel_ButNotAlways() {
			var (show, hide) = LeftPanel.Transition(["Basics", "Key Count"], ["Basics"], Known);
			Assert.Empty(show);
			Assert.Equal(["Key Count"], hide);
		}

		[Fact]
		public void Transition_UnknownGroups_AreLeftOut() {
			var (show, hide) = LeftPanel.Transition(["Basics", "Big Delta"], ["Basics", "Little Delta"], Known);
			Assert.Empty(show);
			Assert.Empty(hide);
		}

		[Fact]
		public void Missing_ListsAlwaysAndPanelGroupsNotKnown_OnceEach_InOrder() {
			PanelSet set = new(["Basics", "Gone"], [P(50, "Big Delta", "Key Count"), P(50, "Big Delta", "Little Delta")]) {
				Previous = [P(2, "Previous", "Key Count")],
			};
			Assert.Equal(["Gone", "Big Delta", "Little Delta", "Previous"], LeftPanel.Missing(set, Known));
		}
	}

	public class LeftPanelStatusLinesTests {
		static readonly PanelSet Set = new(["Splits Basics"], [
			new Panel(["Splits List Labels 1", "Splits List Values 1"], new() { ["Header Left 1"] = "Splits", ["Header Right 1"] = "Finished at" }, new(), 50),
			new Panel([], new(), new(), 1.5),
		]) { Previous = [new Panel(["Splits Previous"], new(), new(), 30)] };

		const string LIST = "Splits List Labels 1, Splits List Values 1 · \"Splits\", \"Finished at\"";

		static readonly DateTime Shown = new(2026, 10, 11, 20, 0, 0, DateTimeKind.Utc);
		static readonly DateTime Now = Shown.AddSeconds(12.7);

		static LeftPanel.RotationStatus Status(int? panel, int? previous = null, bool isDefault = false, bool stopped = false, PanelSet? set = null) {
			return new("GTA3", isDefault, set ?? Set, panel, previous, panel == null ? null : Shown, stopped, CancellationToken.None);
		}

		static LeftPanel.StatusLine L(string text, LeftPanel.LineKind kind) => new(text, kind);

		[Fact]
		public void NoRotation_IsOneTitleLine() {
			Assert.Equal([L("Left panel: none", LeftPanel.LineKind.Title)], LeftPanel.StatusLines(null, Now));
		}

		[Fact]
		public void EverySection_WithAlwaysOn_AndTheLoopPanelUpMarkedAndTimed() {
			Assert.Equal([
				L("Left panel: GTA3", LeftPanel.LineKind.Title),
				L("Always", LeftPanel.LineKind.Heading),
				L("  Splits Basics", LeftPanel.LineKind.On),
				L("Loop", LeftPanel.LineKind.Heading),
				L($"> {LIST} · 12 / 50 s", LeftPanel.LineKind.Current),
				L("  (blank) · 1.5 s", LeftPanel.LineKind.Item),
				L("Previous split", LeftPanel.LineKind.Heading),
				L("  Splits Previous · 30 s", LeftPanel.LineKind.Item),
			], LeftPanel.StatusLines(Status(0), Now));
		}

		[Fact]
		public void DuringASplit_TheLoopPanelToReturnToKeepsItsMarker_AndThePreviousPanelIsCurrentAndTimed() {
			List<LeftPanel.StatusLine> lines = LeftPanel.StatusLines(Status(0, 0), Now);
			Assert.Equal(L($"> {LIST} · 50 s", LeftPanel.LineKind.Marked), lines[4]);
			Assert.Equal(L("> Splits Previous · 12 / 30 s", LeftPanel.LineKind.Current), lines[7]);
		}

		[Fact]
		public void Timer_StopsAtThePanelsSeconds() {
			Assert.Equal(L($"> {LIST} · 50 / 50 s", LeftPanel.LineKind.Current), LeftPanel.StatusLines(Status(0), Shown.AddSeconds(500))[4]);
		}

		[Fact]
		public void Stopped_KeepsTheMarker_ButNoTimer() {
			List<LeftPanel.StatusLine> lines = LeftPanel.StatusLines(Status(0, stopped: true), Now);
			Assert.Equal(L("Left panel: GTA3, stopped", LeftPanel.LineKind.Title), lines[0]);
			Assert.Equal(L($"> {LIST} · 50 s", LeftPanel.LineKind.Current), lines[4]);
		}

		[Fact]
		public void OnlyPanel_StaysUp_SoNoTimer() {
			PanelSet single = new(["Sellout Basics"], [new Panel(["Sellout Key Count"], new(), new(), 220)]);
			Assert.Equal(L("> Sellout Key Count · 220 s", LeftPanel.LineKind.Current), LeftPanel.StatusLines(Status(0, set: single), Now)[4]);
		}

		[Fact]
		public void BeforeTheFirstPanel_NothingIsMarked_AndAlwaysIsNotOnYet() {
			List<LeftPanel.StatusLine> lines = LeftPanel.StatusLines(Status(null, isDefault: true), Now);
			Assert.Equal(L("Left panel: GTA3 (default)", LeftPanel.LineKind.Title), lines[0]);
			Assert.Equal(L("  Splits Basics", LeftPanel.LineKind.Item), lines[2]);
			Assert.DoesNotContain(lines, l => l.Text.StartsWith('>'));
		}

		[Fact]
		public void EmptySections_SayNone_AndNoPreviousMeansNoPreviousSection() {
			LeftPanel.RotationStatus status = new("KTANE", true, new PanelSet([], []), null, null, Shown, false, CancellationToken.None);
			Assert.Equal([
				L("Left panel: KTANE (default)", LeftPanel.LineKind.Title),
				L("Always", LeftPanel.LineKind.Heading),
				L("  (none)", LeftPanel.LineKind.Item),
				L("Loop", LeftPanel.LineKind.Heading),
				L("  (none)", LeftPanel.LineKind.Item),
			], LeftPanel.StatusLines(status, Now));
		}
	}

	// Start, Stop, Resume and the OBS-connect restart, on panels loaded from a temporary leftpanel.json. OBS is never
	// ready in tests, so the rotations started here only wait; Dispose stops the last one and unloads the panels.
	public class LeftPanelStopTests : IDisposable {
		readonly string _dir = Path.Combine(Path.GetTempPath(), "LeftPanelStopTests-" + Guid.NewGuid().ToString("N"));
		readonly string _savedBotInput = Config.Data.Directories.BotInput;
		readonly List<string> _log = new();

		public LeftPanelStopTests() {
			Directory.CreateDirectory(_dir);
			Config.Data.Directories.BotInput = _dir;
			File.WriteAllText(Path.Combine(_dir, "leftpanel.json"), """{ "GTA3": { "Always": [ "Splits Basics" ], "Panels": [ { "Show": [ "Splits Key Count" ], "Seconds": 50 } ] } }""");
			LeftPanel.Load();
			ConsoleLogger.LineLogged += OnLine;
		}

		public void Dispose() {
			ConsoleLogger.LineLogged -= OnLine;
			LeftPanel.Stop();
			File.WriteAllText(Path.Combine(_dir, "leftpanel.json"), "{}");
			LeftPanel.Load();
			Config.Data.Directories.BotInput = _savedBotInput;
			Directory.Delete(_dir, true);
		}

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		int Count(string text) { lock (_log) return _log.Count(l => l.Contains(text)); }

		[Fact]
		public void Stopped_IsNotRestartedByObsConnecting_UntilResumed() {
			LeftPanel.Start("GTA3");
			LeftPanel.Stop();
			LeftPanel.OnObsConnected();
			Assert.Equal(1, Count("Left panel: GTA3"));
			Assert.Equal(1, Count("Left panel rotation stopped"));

			LeftPanel.Resume();
			Assert.Equal(2, Count("Left panel: GTA3"));
			LeftPanel.OnObsConnected();
			Assert.Equal(3, Count("Left panel: GTA3"));
		}

		[Fact]
		public void StartAfterStop_IsRestartedByObsConnectingAgain() {
			LeftPanel.Start("GTA3");
			LeftPanel.Stop();
			LeftPanel.Start("GTA3");
			LeftPanel.OnObsConnected();
			Assert.Equal(3, Count("Left panel: GTA3"));
		}

		[Fact]
		public void Status_FollowsStartAndStop() {
			LeftPanel.Start("GTA3");
			LeftPanel.RotationStatus? status = LeftPanel.Status;
			Assert.NotNull(status);
			Assert.Equal("GTA3", status.Game);
			Assert.False(status.IsDefault);
			Assert.Null(status.Panel);
			Assert.False(status.Stopped);

			LeftPanel.Stop();
			Assert.True(LeftPanel.Status!.Stopped);

			LeftPanel.Start("KTANE");
			Assert.Null(LeftPanel.Status);
		}

		[Fact]
		public void NoRotation_StopAndResumeOnlyLog_AndObsConnectingDoesNothing() {
			LeftPanel.Start("KTANE");
			LeftPanel.Stop();
			LeftPanel.Resume();
			LeftPanel.OnObsConnected();
			Assert.Equal(1, Count("No left panels for KTANE, left as is"));
			Assert.Equal(1, Count("No left panel rotation to stop"));
			Assert.Equal(1, Count("No left panel to start"));
			Assert.Equal(0, Count("Left panel:"));
		}
	}

	/// <summary>
	/// A fact that only runs where paths.json names a BotInput holding leftpanel.json and games.json.
	/// </summary>
	public sealed class RealLeftPanelFactAttribute : FactAttribute {
		public RealLeftPanelFactAttribute() {
			string? botInput = ColorData.RealBotInput;
			if (string.IsNullOrEmpty(botInput)) {
				Skip = "No StreamAssistant2/paths.json found above the test output.";
			}
			else if (!File.Exists(Path.Combine(botInput, "leftpanel.json")) || !File.Exists(Path.Combine(botInput, "games.json"))) {
				Skip = $"No leftpanel.json and games.json in {botInput}";
			}
		}
	}
}
