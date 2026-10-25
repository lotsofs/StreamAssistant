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
			var sets = LeftPanel.Parse("""{ "GTA3": { "Always": [ "_LP: Splits Basics", "_LPC: Splits Previous Values" ], "Panels": [ { "Show": [ " Splits Key Count ", "", "  ", null ], "Seconds": 5 } ] } }""");
			Assert.Equal(["Splits Basics", "Splits Previous Values"], sets["GTA3"].Always);
			Assert.Equal(["Splits Key Count"], sets["GTA3"].Panels[0].Show);
		}

		[Theory]
		[InlineData("_LP: Splits Basics", "!Scene: Left Panel", "Splits Basics")]
		[InlineData("_LPC: Splits Previous Values", "!Scene: Left Panel Colored", "Splits Previous Values")]
		[InlineData("_LP: Splits Main List Labels", "!Scene: Left Panel Colored", null)]
		[InlineData("_LPC: Splits Previous Values", "!Scene: Left Panel", null)]
		[InlineData("_Left Panel: Splits Basics", "!Scene: Left Panel", null)]
		[InlineData("_LP:Splits Basics", "!Scene: Left Panel", null)]
		[InlineData("Full: Blue", "!Scene: Left Panel", null)]
		public void GroupName_EachSceneItsOwnPrefix(string item, string scene, string? group) {
			Assert.Equal(group, LeftPanel.GroupName(item, scene));
		}

		[Theory]
		[InlineData("Splits Current 2", "Splits Current", 2)]
		[InlineData("Splits List Values 12", "Splits List Values", 12)]
		[InlineData("Splits Basics", null, 0)]
		[InlineData("Values 2b", null, 0)]
		[InlineData("Values ", null, 0)]
		[InlineData("12", null, 0)]
		public void Numbered_SplitsOffATrailingNumber(string name, string? base_, int number) {
			Assert.Equal(base_ == null ? null : (base_, number), LeftPanel.Numbered(name));
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
		public void Parse_Name_IsTrimmed_AndBlankOrMissingIsNone() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Panels": [ { "Name": " Splits: Run Delta ", "Show": [ "A" ], "Seconds": 5 }, { "Name": " ", "Show": [ "B" ], "Seconds": 5 }, { "Show": [ "C" ], "Seconds": 5 } ] } }""");
			Assert.Equal(["Splits: Run Delta", null, null], sets["GTA3"].Panels.Select(p => p.Name));
		}

		[Fact]
		public void Parse_Keep_IsCleanedLikeShow_AndMissingIsEmpty() {
			var sets = LeftPanel.Parse("""{ "GTA3": { "Panels": [ { "Show": [ "A" ], "Keep": [ " _LP: Splits List Labels ", "", null ], "Seconds": 5 }, { "Show": [ "B" ], "Seconds": 5 } ] } }""");
			Assert.Equal(["Splits List Labels"], sets["GTA3"].Panels[0].Keep);
			Assert.Empty(sets["GTA3"].Panels[1].Keep);
		}

		[Fact]
		public void Parse_Previous_IsReadLikePanels_AndMissingIsEmpty() {
			var sets = LeftPanel.Parse("""
				{
					"SWAT4": {
						"Panels": [ { "Show": [ "Splits Current" ], "Seconds": 30 } ],
						"Previous": [ { "Show": [ "_LP: Nothing" ], "Seconds": 2 }, { "Show": [ "Splits Previous" ], "Seconds": 0 } ]
					},
					"GTA3": { "Panels": [] }
				}
				""");
			Panel previous = Assert.Single(sets["SWAT4"].Previous);
			Assert.Equal(["Nothing"], previous.Show);
			Assert.Equal(2, previous.Seconds);
			Assert.Empty(sets["GTA3"].Previous);
		}

		[Fact]
		public void Parse_Sign_IsTrimmed_SharedByAName_AndBlankOrMissingIsNone() {
			var sets = LeftPanel.Parse("""
				{
					"SWAT4": { "Sign": "  D:\\Sign.txt ", "Panels": [] },
					"SS2": "SWAT4",
					"GTA3": { "Sign": " ", "Panels": [] },
					"HITMAN": { "Panels": [] }
				}
				""");
			Assert.Equal(@"D:\Sign.txt", sets["SWAT4"].Sign);
			Assert.Equal(@"D:\Sign.txt", sets["SS2"].Sign);
			Assert.Null(sets["GTA3"].Sign);
			Assert.Null(sets["HITMAN"].Sign);
		}

		const string GTA3 = """ "GTA3": { "Always": [ "Splits Basics" ], "Panels": [ { "Show": [ "Splits Key Count" ], "Seconds": 50 } ] } """;

		[Fact]
		public void Parse_NameEntry_SharesThatEntrysPanels_AnyCase() {
			var sets = LeftPanel.Parse($$"""{ {{GTA3}}, "GTA4": "GTA3", "GTA5": "gta3" }""");
			Assert.Same(sets["GTA3"], sets["GTA4"]);
			Assert.Same(sets["GTA3"], sets["GTA5"]);
			Assert.Equal("GTA3", sets["GTA4"].Entry);
		}

		[Fact]
		public void Parse_NameChain_IsFollowed() {
			var sets = LeftPanel.Parse($$"""{ "GTA5": "GTA4", "GTA4": "GTA3", {{GTA3}} }""");
			Assert.Same(sets["GTA3"], sets["GTA5"]);
			Assert.Equal("GTA3", sets["GTA5"].Entry);
		}

		[Theory]
		[InlineData("SWAT4", false, "Run_NoSubsplits", "SWAT4 (Run_NoSubsplits)")]
		[InlineData("gta3", false, "GTA3", "gta3")]
		[InlineData("KTANE", true, "default", "KTANE (default)")]
		[InlineData("GTA3", false, "", "GTA3")]
		public void Which_NamesTheEntryWhenItIsntTheGamesOwn(string game, bool isDefault, string entry, string expected) {
			Assert.Equal(expected, LeftPanel.Which(game, isDefault, new PanelSet([], []) { Entry = entry }));
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
		public void RealFile_KeysAreGamesDefaultOrNamedByAnotherEntry_AndNothingIsSkipped() {
			string botInput = ColorData.RealBotInput!;
			string json = File.ReadAllText(Path.Combine(botInput, "leftpanel.json"));
			var sets = LeftPanel.Parse(json);
			HashSet<string> names = Games.Parse(File.ReadAllText(Path.Combine(botInput, "games.json"))).Values.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

			using JsonDocument raw = JsonDocument.Parse(json);
			HashSet<string> named = raw.RootElement.EnumerateObject().Where(e => e.Value.ValueKind == JsonValueKind.String).Select(e => e.Value.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
			Assert.True(sets.ContainsKey("default"));
			Assert.All(sets.Keys.Where(k => !k.Equals("default", StringComparison.OrdinalIgnoreCase)), k => Assert.True(names.Contains(k) || named.Contains(k), $"{k} is neither a games.json game nor named by another entry"));

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

		static readonly string[] RealFamilies = ["Splits Current", "Splits List Labels", "Splits List Values"];

		static string Unnumbered(string name) => LeftPanel.Numbered(name) is (string base_, int _) ? base_ : name;

		// Nothing carries over to a fresh copy, so panels moving the same families must each set every source those copies need.
		[RealLeftPanelFact]
		public void RealFile_PanelsMovingTheSameFamilies_SetTheSameSources() {
			var sets = LeftPanel.Parse(File.ReadAllText(Path.Combine(ColorData.RealBotInput!, "leftpanel.json")));
			foreach (PanelSet set in sets.Values.Distinct()) {
				var moving = set.Panels.Concat(set.Previous)
					.Select(p => (Panel: p, Families: string.Join(", ", p.Show.Select(Unnumbered).Where(RealFamilies.Contains).Distinct().Order())))
					.Where(p => p.Families.Length > 0);
				foreach (var group in moving.GroupBy(p => p.Families)) {
					var sources = group.Select(p => (p.Panel.Name, Sources: string.Join(", ", p.Panel.Text.Keys.Concat(p.Panel.File.Keys).Select(Unnumbered).Distinct().Order()))).ToList();
					Assert.All(sources, s => Assert.True(s.Sources == sources[0].Sources,
						$"{set.Entry}: {s.Name} sets {s.Sources}, but {sources[0].Name} sets {sources[0].Sources} for {group.Key}"));
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
			[SCENE] = ["Full: Blue", "_LP: Basics", "_LP: Labels 1", "_LP: Labels 2", "_LP: Values 1", "_LP: Values 2", "_LP: Key Count", "_LPC: Wrong Scene"],
			[COLORED] = [SCENE, "_LP: Labels 1", "_LPC: Gold"],
		};
		readonly HashSet<int> _splitAtWaits = new();
		readonly HashSet<int> _skipAtWaits = new();
		int _stopAfterWaits = 1;
		int _waits;
		Func<bool> _ready = () => true;
		Func<List<string>?> _inputs = () => null;
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
				if (_skipAtWaits.Contains(_waits)) {
					LeftPanel.SkipPanel();
					return Task.Delay(Timeout.Infinite, token);
				}
				if (_waits >= _stopAfterWaits) {
					_cts.Cancel();
				}
				token.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}) {
			SetFilter = (source, filter, property, color) => _calls.Add($"filter {source} | {filter} | {property} = {color}"),
			InputNames = () => _inputs(),
		};

		readonly List<(int?, int?)> _shown = new();

		Task Run(PanelSet set) => LeftPanel.RunAsync("GTA3", set, Calls(), _cts.Token, (panel, previous) => _shown.Add((panel, previous)));

		static (int?, int?) S(int? panel, int? previous = null) => (panel, previous);

		static Panel P(double seconds, params string[] show) => new(show.ToList(), new(), new(), seconds);

		static string Show(string group, string scene = SCENE, string prefix = "_LP: ") => $"show {prefix}{group} | {scene}";

		static string Hide(string group, string scene = SCENE, string prefix = "_LP: ") => $"hide {prefix}{group} | {scene}";

		List<string> Between(string from, string to) {
			int start = _calls.IndexOf(from) + 1;
			return _calls.Skip(start).Take(_calls.IndexOf(to, start) - start).ToList();
		}

		[Fact]
		public async Task FirstStep_SetsValues_ThenShows_ThenHidesEveryOtherGroup() {
			Panel first = new(["Labels", "Values"], new() { ["Header"] = "Finished at" }, new() { ["Current 2"] = @"D:\a.txt" }, 50);
			await Run(new PanelSet(["Basics"], [first, P(50, "Key Count")]));
			Assert.Equal([
				$"list {SCENE}", $"list {COLORED}",
				$"hide {SCENE} | {COLORED}",
				"text Header 1 = Finished at", @"file Current 1 = D:\a.txt",
				Show("Basics"), Show("Labels 1"), Show("Values 1"),
				Hide("Labels 2"), Hide("Values 2"), Hide("Key Count"), Hide("Gold", COLORED, "_LPC: "),
				"wait 50",
			], _calls);
		}

		[Fact]
		public async Task WithOBSsInputs_OnlySourcesWithNumberedCopies_TakeTheCopysNumber() {
			_inputs = () => ["Header 1", "Header 2", "Current 1", "Current 2", "Shared", "Full: Blue"];
			Panel first = new(["Labels", "Values"], new() { ["Header"] = "Finished at" }, new() { ["Current"] = @"D:\a.txt", ["Shared"] = @"D:\s.txt" }, 50);
			await Run(new PanelSet(["Basics"], [first, P(50, "Key Count")]));
			Assert.Equal(["text Header 1 = Finished at", @"file Current 1 = D:\a.txt", @"file Shared = D:\s.txt"], _calls.Where(c => c.StartsWith("text ") || c.StartsWith("file ")));
		}

		[Fact]
		public async Task InputsThatCantBeListed_AreLogged_AndEverySourceTakesTheCopysNumber() {
			_inputs = () => throw new InvalidOperationException("boom");
			Panel first = new(["Labels", "Values"], new(), new() { ["Shared"] = @"D:\s.txt" }, 50);
			await Run(new PanelSet(["Basics"], [first, P(50, "Key Count")]));
			Assert.Single(Lines("Error LPN2: left panel listing OBS's inputs failed: boom"));
			Assert.Contains(@"file Shared 1 = D:\s.txt", _calls);
			Assert.Contains(Show("Labels 1"), _calls);
		}

		[Fact]
		public async Task Steps_MoveToTheOtherCopies_SettingOnlyTheirOwnValues_WaitEachPanelsSeconds_AndWrap() {
			_stopAfterWaits = 4;
			Panel finishedAt = new(["Labels", "Values"], new() { ["Names"] = "n" }, new() { ["Current"] = "a" }, 50);
			Panel runDelta = new(["Labels", "Values"], new(), new() { ["Current"] = "b" }, 30);
			await Run(new PanelSet(["Basics"], [finishedAt, runDelta, P(1)]));
			Assert.Equal(["wait 50", "wait 30", "wait 1", "wait 50"], _calls.Where(c => c.StartsWith("wait")));
			Assert.Equal(["file Current 2 = b", Show("Labels 2"), Show("Values 2"), Hide("Labels 1"), Hide("Values 1")], Between("wait 50", "wait 30"));
			Assert.Equal([Hide("Labels 2"), Hide("Values 2")], Between("wait 30", "wait 1"));
			Assert.Equal(["text Names 1 = n", "file Current 1 = a", Show("Labels 1"), Show("Values 1")], Between("wait 1", "wait 50"));
			Assert.Equal(2, _calls.Count(c => c.StartsWith("list")));
			Assert.Equal([S(0), S(1), S(2), S(0)], _shown);
		}

		[Fact]
		public async Task AKeptFamily_StaysUp_WhileTheOthersMove() {
			_stopAfterWaits = 2;
			Panel finishedAt = new(["Labels", "Values"], new() { ["Names"] = "n" }, new() { ["Current"] = "a" }, 50);
			Panel runDelta = new(["Labels", "Values"], new(), new() { ["Current"] = "b" }, 30) { Keep = ["Labels"] };
			await Run(new PanelSet(["Basics"], [finishedAt, runDelta]));
			Assert.Equal(["file Current 2 = b", Show("Values 2"), Hide("Values 1")], Between("wait 50", "wait 30"));
		}

		[Fact]
		public async Task SplitDuringAPanelThatKeepsAFamily_BringsTheKeptCopyBack_Untouched() {
			_splitAtWaits.Add(2);
			_stopAfterWaits = 6;
			Panel finishedAt = new(["Labels", "Values"], new() { ["Names"] = "n" }, new() { ["Current"] = "a" }, 50);
			Panel runDelta = new(["Values"], new(), new() { ["Current"] = "b" }, 30) { Keep = ["Labels"] };
			await Run(WithPrevious([finishedAt, runDelta]));
			Assert.Equal(["wait 50", "wait 30", "wait 2", "wait 25", "wait 2", "wait 30"], Waits());
			Assert.Equal(["file Current 2 = b", Show("Labels 1"), Show("Values 2")], _calls.Skip(_calls.LastIndexOf("wait 2") + 1).SkipLast(1));
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
		public async Task GroupsOBSDoesntHave_AreLoggedAndReportedOnce_AndSkipped() {
			_stopAfterWaits = 3;
			List<List<string>> reported = new();
			await LeftPanel.RunAsync("GTA3", new PanelSet(["Basics"], [P(50, "Key Count", "Big Delta"), P(50, "Little Delta")]), Calls(), _cts.Token, missing: reported.Add);
			Assert.Single(Lines("Left panel: GTA3 has groups OBS doesn't: Big Delta, Little Delta"));
			Assert.Equal(["Big Delta", "Little Delta"], Assert.Single(reported));
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
			Assert.Equal([Hide("Labels 1"), Hide("Values 1")], Between("wait 50", "wait 2"));
			Assert.Equal([Show("Gold", COLORED, "_LPC: ")], Between("wait 2", "wait 25"));
			Assert.Equal([Show("Labels 1"), Show("Values 1")], _calls.Skip(_calls.LastIndexOf("wait 2") + 1).SkipLast(1));
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
			Assert.DoesNotContain(Show("Gold", COLORED, "_LPC: "), _calls);
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

		// A skipped wait never ends by itself, so only the skip can move the rotation on; the timeout catches a hang.
		[Fact]
		public async Task Skip_DuringALoopPanel_MovesOnAtOnce() {
			_skipAtWaits.Add(1);
			_stopAfterWaits = 3;
			await Run(new PanelSet(["Basics"], [P(50, "Key Count"), P(30, "Labels 1")])).WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(["wait 50", "wait 30", "wait 50"], Waits());
			Assert.Equal([S(0), S(1), S(0)], _shown);
		}

		[Fact]
		public async Task Skip_DuringPrevious_MovesToTheNextPreviousPanel() {
			_splitAtWaits.Add(1);
			_skipAtWaits.Add(2);
			_stopAfterWaits = 5;
			await Run(WithPrevious([P(50, "Labels 1", "Values 1"), P(30, "Key Count")])).WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(["wait 50", "wait 2", "wait 25", "wait 2", "wait 50"], Waits());
			Assert.Equal([S(0), S(0, 0), S(0, 1), S(0, 2), S(0)], _shown);
		}

		[Fact]
		public async Task Skip_OnASinglePanelWithPrevious_ShowsItAgain() {
			_skipAtWaits.Add(1);
			_stopAfterWaits = 2;
			await Run(WithPrevious([P(50, "Key Count")])).WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(["wait forever", "wait forever"], Waits());
			Assert.Equal([S(0), S(0)], _shown);
		}

		static readonly string[] Known = ["Basics", "Labels 1", "Values 1", "Values 2", "Key Count"];

		static readonly Dictionary<string, string> SplitColorMap = new() {
			["None"] = "#ffffff", ["LabelPositive"] = "#0000ff", ["LabelNegative"] = "#ff0000", ["LabelGold"] = "#ffc800",
			["ListGold"] = "#ffc800", ["ListGainedAhead"] = "#000086",
		};

		static string Filter(string source, string filter, string hex) => $"filter {source} | {filter} | color_multiply = {ColorUtil.ToOBS(hex)}";

		List<string> Filters() => _calls.Where(c => c.StartsWith("filter")).ToList();

		static PanelSet WithSign(List<Panel> panels) => WithPrevious(panels) with { Sign = "sign.txt" };

		[Fact]
		public async Task SplitColours_TheFirstStep_ColoursTheHighlights_BeforeShowing_AndLaterStepsDont() {
			_stopAfterWaits = 2;
			await LeftPanel.RunAsync("SWAT4", new PanelSet(["Basics"], [P(50, "Key Count"), P(30)]), Calls(), _cts.Token, colors: SplitColorMap);
			Assert.Equal([
				$"list {SCENE}", $"list {COLORED}", $"hide {SCENE} | {COLORED}",
				Filter("Livesplit: SL Highlights Gold", "Color Correction 2", "#ffc800"),
				Filter("Livesplit: SL Highlights AheadGained", "Color Correction 2", "#000086"),
				Show("Basics"),
			], _calls.Take(6));
			Assert.Equal(2, Filters().Count);
		}

		[Fact]
		public async Task SplitColours_EachPreviousStep_ColoursTheDifferencesByTheSign_BeforeShowing() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 3;
			Queue<string> signs = new(["GoldAhead", "Behind"]);
			List<string> read = new();
			await LeftPanel.RunAsync("SWAT4", WithSign([P(50, "Key Count"), P(30)]), Calls(), _cts.Token, colors: SplitColorMap,
				readText: file => { read.Add(file); return signs.Dequeue(); });
			Assert.Equal(["sign.txt", "sign.txt"], read);
			Assert.Equal([
				Filter("Livesplit: Previous Run", "Color Correction", "#0000ff"),
				Filter("Livesplit: Previous Segment", "Color Correction", "#ffc800"),
				Filter("Livesplit: Previous Gold", "Color Correction", "#ffc800"),
				Hide("Key Count"),
			], Between("wait 50", "wait 2"));
			Assert.Equal([
				Filter("Livesplit: Previous Run", "Color Correction", "#ff0000"),
				Filter("Livesplit: Previous Segment", "Color Correction", "#ffffff"),
				Filter("Livesplit: Previous Gold", "Color Correction", "#ffffff"),
				Show("Gold", COLORED, "_LPC: "),
			], Between("wait 2", "wait 25"));
		}

		[Fact]
		public async Task SplitColours_WithoutASign_ThePreviousSplitIsLeftAlone() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 3;
			await LeftPanel.RunAsync("SWAT4", WithPrevious([P(50, "Key Count"), P(30)]), Calls(), _cts.Token, colors: SplitColorMap,
				readText: _ => throw new InvalidOperationException("read"));
			Assert.DoesNotContain(Filters(), f => f.Contains("Previous"));
		}

		[Fact]
		public async Task SplitColours_None_MeanNoFilterCalls() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 3;
			await LeftPanel.RunAsync("SWAT4", WithSign([P(50, "Key Count"), P(30)]), Calls(), _cts.Token, readText: _ => "GoldAhead");
			Assert.Empty(Filters());
		}

		[Fact]
		public async Task SplitColours_AnUnreadableSign_IsLogged_AndLeavesThemAsTheyAre() {
			_splitAtWaits.Add(1);
			_stopAfterWaits = 2;
			await LeftPanel.RunAsync("SWAT4", WithSign([P(50, "Key Count"), P(30)]), Calls(), _cts.Token, colors: SplitColorMap, readText: _ => null);
			Assert.DoesNotContain(Filters(), f => f.Contains("Previous"));
			Assert.Single(Lines("Left panel: can't read sign.txt, previous-split colours left as is"));
		}

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
			Assert.Equal(["Gone", "Big Delta", "Little Delta", "Previous"], LeftPanel.Missing(set, Known.Contains));
		}

		[Fact]
		public void Missing_CountsKeptGroups() {
			PanelSet set = new(["Basics"], [P(50, "Key Count") with { Keep = ["Gone"] }]);
			Assert.Equal(["Gone"], LeftPanel.Missing(set, Known.Contains));
		}
	}

	public class LeftPanelPlanTests {
		static readonly LeftPanel.Layout Layout = MakeLayout();

		static LeftPanel.Layout MakeLayout() {
			string[] names = ["Splits Basics", "Splits Key Count", "Splits Current 1", "Splits Current 2", "Splits Current 3",
				"Splits List Labels 1", "Splits List Labels 2", "Splits List Values 1", "Splits List Values 2"];
			Dictionary<string, List<(string Scene, string Item)>> groups = names.ToDictionary(n => n, n => new List<(string Scene, string Item)> { ("!Scene: Left Panel", "_LP: " + n) });
			Dictionary<string, SortedDictionary<int, string>> families = new() {
				["Splits Current"] = new() { [1] = "Splits Current 1", [2] = "Splits Current 2", [3] = "Splits Current 3" },
				["Splits List Labels"] = new() { [1] = "Splits List Labels 1", [2] = "Splits List Labels 2" },
				["Splits List Values"] = new() { [1] = "Splits List Values 1", [2] = "Splits List Values 2" },
			};
			return new(groups, families, false);
		}

		readonly Dictionary<string, int> _last = new();

		static Panel Make(string[] show, Dictionary<string, string>? text = null, Dictionary<string, string>? file = null) => new(show.ToList(), text ?? new(), file ?? new(), 10);

		(HashSet<string> Wanted, List<(string Source, string Key, string Value)> Settings) Plan(Panel? panel, IReadOnlyCollection<string>? up, params string[] always) {
			return LeftPanel.Plan(Layout, always, panel, up, _last);
		}

		static readonly string[] Lists = ["Splits List Labels", "Splits List Values"];

		[Fact]
		public void NothingUp_TheFamiliesTakeCopyOne_AndCopySourcesItsNumber() {
			var (wanted, settings) = Plan(Make(Lists, text: new() { ["Livesplit: SL - Names 2"] = "n" }, file: new() { ["Livesplit: SL - Current 3"] = "f" }), null, "Splits Basics");
			Assert.Equal(["Splits Basics", "Splits List Labels 1", "Splits List Values 1"], wanted);
			Assert.Equal([("Livesplit: SL - Names 1", "text", "n"), ("Livesplit: SL - Current 1", "file", "f")], settings);
		}

		[Fact]
		public void TheFamiliesMoveTogether_ToTheLowestCopyHiddenInEach_SettingOnlyThePanelsOwnValues() {
			Plan(Make(Lists, text: new() { ["Livesplit: SL - Names"] = "n" }, file: new() { ["Livesplit: SL - Current"] = "a" }), null);
			var (wanted, settings) = Plan(Make(Lists, file: new() { ["Livesplit: SL - Current"] = "b" }), ["Splits Basics", "Splits List Labels 1", "Splits List Values 1"], "Splits Basics");
			Assert.Equal(["Splits Basics", "Splits List Labels 2", "Splits List Values 2"], wanted);
			Assert.Equal([("Livesplit: SL - Current 2", "file", "b")], settings);
		}

		[Theory]
		[InlineData(1, 2)]
		[InlineData(2, 1)]
		public void ConsecutiveCurrentPanels_Alternate(int up, int next) {
			var (wanted, settings) = Plan(Make(["Splits Current"], text: new() { ["Livesplit: Current Index"] = "5" }), [$"Splits Current {up}"]);
			Assert.Equal([$"Splits Current {next}"], wanted);
			Assert.Equal([($"Livesplit: Current Index {next}", "text", "5")], settings);
		}

		[Fact]
		public void WithKnownCopySources_OnlyThoseTakeTheCopysNumber_AndAnyOtherIsSetAsWritten() {
			LeftPanel.Layout layout = Layout with { CopySources = ["Livesplit: SL - Names", "Livesplit: SL - Current"] };
			Panel panel = Make(Lists, text: new() { ["Livesplit: SL - Names"] = "n" },
				file: new() { ["Livesplit: SL - Current 3"] = "c", ["Livesplit: SL Highlights AheadGained"] = "h", ["Foo 3"] = "f" });
			var (wanted, settings) = LeftPanel.Plan(layout, [], panel, null, _last);
			Assert.Equal(["Splits List Labels 1", "Splits List Values 1"], wanted);
			Assert.Equal([
				("Livesplit: SL - Names 1", "text", "n"),
				("Livesplit: SL - Current 1", "file", "c"),
				("Livesplit: SL Highlights AheadGained", "file", "h"),
				("Foo 3", "file", "f"),
			], settings);
		}

		[Fact]
		public void NoCopyNumberHiddenInEveryFamily_EachTakesItsOwnLowest_SourcesTheFirstFamilysNumber() {
			var (wanted, settings) = Plan(Make(Lists, file: new() { ["Livesplit: SL - Current 1"] = "f" }), ["Splits List Labels 1", "Splits List Values 2"]);
			Assert.Equal(["Splits List Labels 2", "Splits List Values 1"], wanted);
			Assert.Equal([("Livesplit: SL - Current 2", "file", "f")], settings);
		}

		[Fact]
		public void NumberedNames_StandForTheirFamily() {
			var (wanted, _) = Plan(Make(["Splits List Values 1"]), ["Splits List Values 1"]);
			Assert.Equal(["Splits List Values 2"], wanted);
		}

		[Fact]
		public void AnAlwaysFamily_KeepsItsCopy() {
			var (wanted, _) = Plan(Make(["Splits Key Count"]), ["Splits Current 3"], "Splits Current");
			Assert.Equal(["Splits Current 3", "Splits Key Count"], wanted);
		}

		static readonly string[] KeepLabels = ["Splits List Labels"];

		[Fact]
		public void AKeptFamilyThatsUp_StaysOnItsCopy_TheOthersMove_AndCopySourcesTakeTheirNumber_AndNothingIsWrittenToTheKeptOne() {
			Plan(Make(Lists, text: new() { ["Livesplit: SL - Names"] = "n" }, file: new() { ["Livesplit: SL - Current"] = "a" }), null);
			var (wanted, settings) = Plan(Make(Lists, file: new() { ["Livesplit: SL - Current"] = "b" }) with { Keep = [.. KeepLabels] }, ["Splits List Labels 1", "Splits List Values 1"]);
			Assert.Equal(["Splits List Labels 1", "Splits List Values 2"], wanted);
			Assert.Equal([("Livesplit: SL - Current 2", "file", "b")], settings);
		}

		[Fact]
		public void AKeptFamilyThatIsntUp_ComesBackOnTheCopyLastShown_AndTheOthersAvoidIt() {
			Plan(Make(Lists), ["Splits List Labels 1", "Splits List Values 1"]);
			var (wanted, _) = Plan(Make(["Splits List Values"]) with { Keep = [.. KeepLabels] }, ["Splits Basics"]);
			Assert.Equal(["Splits List Labels 2", "Splits List Values 1"], wanted);
		}

		[Fact]
		public void AKeptFamilyNeverShown_MovesAsUsual() {
			var (wanted, _) = Plan(Make(Lists) with { Keep = [.. KeepLabels] }, ["Splits List Values 1"]);
			Assert.Equal(["Splits List Labels 2", "Splits List Values 2"], wanted);
		}

		[Fact]
		public void MovingFamilies_AvoidTheKeptCopysNumber_ElseTakeIt() {
			var (avoided, _) = Plan(Make(Lists) with { Keep = [.. KeepLabels] }, ["Splits List Labels 1"]);
			Assert.Equal(["Splits List Labels 1", "Splits List Values 2"], avoided);
			var (taken, _) = Plan(Make(Lists) with { Keep = [.. KeepLabels] }, ["Splits List Labels 1", "Splits List Values 2"]);
			Assert.Equal(["Splits List Labels 1", "Splits List Values 1"], taken);
		}

		[Fact]
		public void KeepAlone_ShowsTheGroup_AFamilyOnItsCopy_AndAPlainGroupAsIs() {
			var (wanted, _) = Plan(Make(["Splits List Values"]) with { Keep = ["Splits List Labels", "Splits Key Count"] }, ["Splits List Labels 1", "Splits List Values 1", "Splits Key Count"]);
			Assert.Equal(["Splits Key Count", "Splits List Labels 1", "Splits List Values 2"], wanted);
		}

		[Fact]
		public void EveryFamilyKept_CopySourcesGoOnTheKeptCopy() {
			var (wanted, settings) = Plan(Make(["Splits Current"], text: new() { ["Livesplit: Current Index"] = "5" }) with { Keep = ["Splits Current 1"] }, ["Splits Current 2"]);
			Assert.Equal(["Splits Current 2"], wanted);
			Assert.Equal([("Livesplit: Current Index 2", "text", "5")], settings);
		}

		[Fact]
		public void WithoutCopies_SourcesAreSetAsWritten_AndUnknownNamesLeftOut() {
			var (wanted, settings) = Plan(Make(["Nope", "Splits Key Count"], text: new() { ["Livesplit: Other 2"] = "y", ["Livesplit: Plain"] = "z" }), null, "Splits Basics");
			Assert.Equal(["Splits Basics", "Splits Key Count"], wanted);
			Assert.Equal([("Livesplit: Other 2", "text", "y"), ("Livesplit: Plain", "text", "z")], settings);
			Assert.Empty(_last);
		}

		[Fact]
		public void WithCopies_UnnumberedSourcesGetTheCopysNumber_AndTheCopyIsRecorded() {
			var (_, settings) = Plan(Make(["Splits Current"], text: new() { ["Livesplit: Current Name"] = "x" }), ["Splits Current 1"]);
			Assert.Equal([("Livesplit: Current Name 2", "text", "x")], settings);
			Assert.Equal(2, _last["Splits Current"]);
		}

		[Theory]
		[InlineData("Splits Current", true)]
		[InlineData("Splits Current 2", true)]
		[InlineData("Splits Basics", true)]
		[InlineData("Split Basics", false)]
		[InlineData("Splits SSSA", false)]
		public void Exists_FamiliesTheirNumberedNamesAndSingleGroups(string name, bool exists) {
			Assert.Equal(exists, LeftPanel.Exists(Layout, name));
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
		public void TheTitle_NamesTheEntryAGameSharesPanelsWith() {
			Assert.Equal(L("Left panel: GTA3 (Run_NoSubsplits)", LeftPanel.LineKind.Title), LeftPanel.StatusLines(Status(0, set: Set with { Entry = "Run_NoSubsplits" }), Now)[0]);
		}

		[Fact]
		public void NamedPanel_ShowsItsName_InsteadOfItsGroupsAndTexts() {
			PanelSet named = new(["Splits Basics"], [
				new Panel(["Splits List Labels 1", "Splits List Values 2"], new() { ["Header Right 2"] = "Run Delta" }, new(), 50) { Name = "Splits: Run Delta" },
				new Panel([], new(), new(), 1),
			]);
			Assert.Equal(L("> Splits: Run Delta · 12 / 50 s", LeftPanel.LineKind.Current), LeftPanel.StatusLines(Status(0, set: named), Now)[4]);
		}

		[Fact]
		public void LinesWithAGroupOBSDoesntHave_AreMissing_MarkerAndTimerKept() {
			LeftPanel.RotationStatus status = Status(0) with { Missing = ["Splits Basics", "Splits List Values 1", "Splits Previous"] };
			List<LeftPanel.StatusLine> lines = LeftPanel.StatusLines(status, Now);
			Assert.Equal(new LeftPanel.StatusLine("  Splits Basics", LeftPanel.LineKind.On, true), lines[2]);
			Assert.Equal(new LeftPanel.StatusLine($"> {LIST} · 12 / 50 s", LeftPanel.LineKind.Current, true), lines[4]);
			Assert.False(lines[5].Missing);
			Assert.Equal(new LeftPanel.StatusLine("  Splits Previous · 30 s", LeftPanel.LineKind.Item, true), lines[7]);
		}

		[Fact]
		public void AnUnnamedPanel_ListsItsKeptGroupsAfterShow_AndAMissingOneFlagsIt() {
			PanelSet set = new(["Splits Basics"], [new Panel(["Splits List Values"], new(), new(), 15) { Keep = ["Splits List Highlights", "Splits List Labels"] }, new Panel([], new(), new(), 1)]);
			LeftPanel.RotationStatus status = Status(0, set: set) with { Missing = ["Splits List Highlights"] };
			Assert.Equal(new LeftPanel.StatusLine("> Splits List Values, Splits List Highlights, Splits List Labels · 12 / 15 s", LeftPanel.LineKind.Current, true), LeftPanel.StatusLines(status, Now)[4]);
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
	[Collection(ColorDataCollection.Name)]
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
		public void Start_LineSaysWhereTheSplitColoursCameFrom() {
			LeftPanel.Start("GTA3");
			Assert.Equal(1, Count("Left panel: GTA3, 1 panel, no split colours"));
		}

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
		public void Next_WithoutARotation_StoppedOrWithOnePanel_OnlyLogs() {
			LeftPanel.Start("KTANE");
			LeftPanel.Next();
			LeftPanel.Start("GTA3");
			LeftPanel.Next();
			LeftPanel.Stop();
			LeftPanel.Next();
			Assert.Equal(2, Count("No left panel rotation to move on"));
			Assert.Equal(1, Count("Only one left panel, nothing to move on to"));
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
