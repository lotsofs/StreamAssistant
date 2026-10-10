using System.Text.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using Xunit;
using Capture = StreamAssistant2.Games.Capture;
using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2.Tests {
	// Going live: the Helix fetch is replaced so no test reaches Twitch. Games' state is static, so each
	// test sets what it needs and restores it.
	[Collection(GamesStateCollection.Name)]
	public class GamesGoLiveTests : IDisposable {
		readonly string? _savedId = Games.CategoryId;
		readonly Func<Task<string?>> _savedFetch = Games.FetchCategoryId;
		readonly Func<Action, bool> _savedWhen = Games.WhenObsConnected;
		readonly List<string> _log = new();
		int _fetches;

		public GamesGoLiveTests() {
			ConsoleLogger.LineLogged += OnLine;
		}

		public void Dispose() {
			ConsoleLogger.LineLogged -= OnLine;
			Games.CategoryId = _savedId;
			Games.FetchCategoryId = _savedFetch;
			Games.WhenObsConnected = _savedWhen;
		}

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		List<string> Lines(string text) { lock (_log) return _log.Where(l => l.Contains(text)).ToList(); }

		void FetchReturns(string? id) {
			Games.FetchCategoryId = () => { Interlocked.Increment(ref _fetches); return Task.FromResult(id); };
		}

		static string NewId() => "test-" + Guid.NewGuid().ToString("N");

		[Fact]
		public async Task KnownCategory_IsApplied_WithoutFetching() {
			string id = NewId();
			Games.CategoryId = id;
			FetchReturns("unused");
			await Games.OnStreamStartedAsync();
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
			Assert.Equal(0, _fetches);
		}

		[Fact]
		public async Task UnknownCategory_IsFetched_AppliedAndStored() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			await Games.OnStreamStartedAsync();
			Assert.Equal(1, _fetches);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
			Assert.Equal(id, Games.CategoryId);
		}

		[Fact]
		public async Task FetchFindsNothing_NoSetup() {
			Games.CategoryId = null;
			FetchReturns(null);
			await Games.OnStreamStartedAsync();
			Assert.Empty(Lines("Game setup:"));
			Assert.Single(Lines("category unknown"));
			Assert.Null(Games.CategoryId);
		}

		[Fact]
		public async Task AfterFetch_UpdateWithSameCategory_DoesNothing() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			await Games.OnStreamStartedAsync();
			Games.HandleUpdate(JsonSerializer.SerializeToElement(new { category_id = id, title = "new title" }));
			await Task.Delay(300);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
		}

		[Fact]
		public async Task ObsStreamStart_SetsUpTheGame() {
			string id = NewId();
			Games.CategoryId = id;
			JObject data = new() { ["outputActive"] = true, ["outputState"] = "OBS_WEBSOCKET_OUTPUT_STARTED" };
			ObsConnection.OnStreamStateChanged(null, new StreamStateChangedEventArgs(new OutputStateChanged(data)));
			await Task.Delay(300);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
		}

		// Captures what the boot asks to run on OBS connect, so a test can "connect" OBS whenever it likes.
		Action? _onObsConnect;

		void ObsConnectsLater() {
			_onObsConnect = null;
			Games.WhenObsConnected = a => { _onObsConnect = a; return false; };
		}

		async Task ObsConnects() {
			_onObsConnect!();
			await Task.Delay(300);
		}

		static bool ObsIsUp(Action action) {
			action();
			return true;
		}

		// !changegame / !changecategory. No games are loaded in tests, so ids are numeric; name lookup is ResolveCategory's tests.
		[Fact]
		public async Task ChangeCommand_AppliesOnce_ThenSaysAlready() {
			string id = Random.Shared.NextInt64(1_000_000_000_000, 9_999_999_999_999).ToString();
			Games.CategoryId = null;
			Games.TryChangeCategory(id);
			await Task.Delay(300);
			Games.TryChangeCategory(id);
			await Task.Delay(300);
			Assert.Equal(id, Games.CategoryId);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
			Assert.Single(Lines($"Category is already {id}"));
		}

		[Fact]
		public void ChangeCommand_UnknownInput_ChangesNothing() {
			string id = NewId();
			Games.CategoryId = id;
			Games.TryChangeCategory("nonsense");
			Assert.Equal(id, Games.CategoryId);
			Assert.Single(Lines("No game or category id \"nonsense\""));
		}

		[Fact]
		public async Task Boot_ObsAlreadyUp_StoresAndAppliesAtOnce() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			Games.WhenObsConnected = ObsIsUp;
			await Games.OnBootAsync();
			await Task.Delay(300);
			Assert.Equal(id, Games.CategoryId);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
			Assert.Single(Lines("Boot: category"), l => l.Contains(id));
			Assert.Empty(Lines("OBS not ready"));
		}

		[Fact]
		public async Task Boot_ObsConnectsLater_AppliesThen() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			ObsConnectsLater();
			await Games.OnBootAsync();
			await Task.Delay(300);
			Assert.Equal(id, Games.CategoryId);
			Assert.Empty(Lines("Game setup:"));
			Assert.Single(Lines("Boot: OBS not ready"));
			await ObsConnects();
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
		}

		[Fact]
		public async Task Boot_CategoryChangeDuringFetch_SkipsBootSetup() {
			string stale = NewId();
			string changed = NewId();
			Games.CategoryId = null;
			ObsConnectsLater();
			// The category changes on Twitch while the boot's Helix fetch is still out; Helix then answers with the old one.
			Games.FetchCategoryId = () => {
				Games.HandleUpdate(JsonSerializer.SerializeToElement(new { category_id = changed }));
				return Task.FromResult<string?>(stale);
			};
			await Games.OnBootAsync();
			await Task.Delay(300);
			Assert.Equal(changed, Games.CategoryId);
			Assert.Single(Lines("Game setup:"), l => l.Contains(changed));
			Assert.Empty(Lines(stale));
			Assert.Single(Lines("boot setup skipped"), l => l.Contains(changed));
			Assert.Null(_onObsConnect);
		}

		[Fact]
		public async Task Boot_AppliesOnlyOnce_EvenIfCalledAgain() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			ObsConnectsLater();
			await Games.OnBootAsync();
			await ObsConnects();
			await ObsConnects();
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
		}

		[Fact]
		public async Task Boot_ThenSameCategoryUpdate_DoesNothingMore() {
			string id = NewId();
			Games.CategoryId = null;
			FetchReturns(id);
			Games.WhenObsConnected = ObsIsUp;
			await Games.OnBootAsync();
			Games.HandleUpdate(JsonSerializer.SerializeToElement(new { category_id = id, title = "new title" }));
			await Task.Delay(300);
			Assert.Single(Lines("Game setup:"), l => l.Contains(id));
		}

		[Fact]
		public async Task Boot_FetchFindsNothing_StoresNothing_WaitsForNothing() {
			Games.CategoryId = null;
			FetchReturns(null);
			ObsConnectsLater();
			await Games.OnBootAsync();
			Assert.Null(Games.CategoryId);
			Assert.Single(Lines("Boot: category unknown"));
			Assert.Null(_onObsConnect);
		}

		[Theory]
		[InlineData("""{"data":[{"broadcaster_id":"1","game_id":"461492","game_name":"KTANE","title":"t"}]}""", "461492")]
		[InlineData("""{"data":[{"game_id":""}]}""", "")]
		[InlineData("""{"data":[]}""", null)]
		[InlineData("""{}""", null)]
		public void ParseCategoryId(string json, string? expected) {
			Assert.Equal(expected, TwitchHelixApi.ParseCategoryId(json));
		}
	}

	[Collection(GamesStateCollection.Name)]
	public class GamesTests {
		[Fact]
		public void GameBackgroundPath_ExistingFile_IsCombinedPath() {
			string expected = Path.Combine(@"D:\Backgrounds", "KTANE.png");
			Assert.Equal(expected, GameBackground.PathFor(@"D:\Backgrounds", "KTANE", p => p == expected));
		}

		[Fact]
		public void GameBackgroundPath_MissingFile_IsNull() {
			Assert.Null(GameBackground.PathFor(@"D:\Backgrounds", "None", _ => false));
		}

		[Fact]
		public void GameBackgroundPath_NoDirectory_IsNull_WithoutChecking() {
			bool checkedFile = false;
			Assert.Null(GameBackground.PathFor("", "KTANE", _ => checkedFile = true));
			Assert.False(checkedFile);
		}

		[Fact]
		public void GameColorSchemeInput_Known_IsSetAndCategory() {
			string? asked = null;
			string? input = GameColor.SchemeInput("KTANE", s => { asked = s; return true; });
			Assert.Equal("gameschemes KTANE", input);
			Assert.Equal("gameschemes KTANE", asked);
		}

		[Fact]
		public void GameColorSchemeInput_Unknown_IsNull() {
			Assert.Null(GameColor.SchemeInput("RoN", _ => false));
		}

		[Fact]
		public void CategoryChangeMessage_SameId_IsNoChange() {
			Assert.Null(Games.CategoryChangeMessage("461492", "461492"));
		}

		[Fact]
		public void CategoryChangeMessage_DifferentId_NamesBoth() {
			Assert.Equal("Stream category change from 19731 to 461492", Games.CategoryChangeMessage("19731", "461492"));
		}

		[Fact]
		public void CategoryChangeMessage_UnknownOld_NamesOnlyNew() {
			Assert.Equal("Stream category change to 461492", Games.CategoryChangeMessage(null, "461492"));
		}

		[Fact]
		public void CategoryChangeMessage_FromNoCategory_IsAChange() {
			Assert.Equal("Stream category change from  to 461492", Games.CategoryChangeMessage("", "461492"));
		}

		// Through the real dispatch: the bot's own chat goes nowhere because IRC isn't connected in tests.
		[Fact]
		public async Task HandleUpdate_RepeatedCategory_AppliesOnce() {
			string id = "test-" + Guid.NewGuid().ToString("N");
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			try {
				JsonElement evt = JsonSerializer.SerializeToElement(new { category_id = id, category_name = "Test", title = "a" });
				JsonElement retitled = JsonSerializer.SerializeToElement(new { category_id = id, category_name = "Test", title = "b" });
				TwitchEventHandler.Handle("channel.update", evt, isTest: true);
				TwitchEventHandler.Handle("channel.update", retitled, isTest: true);
				await Task.Delay(300);
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			lock (log) {
				Assert.Single(log, l => l.Contains("Game setup:") && l.Contains(id));
			}
		}

		static List<string> Exes(List<Capture> captures) => captures.Select(c => c.Executable).ToList();

		static List<Capture> C(params string[] executables) => executables.Select(e => new Capture(e)).ToList();

		[Fact]
		public void Parse_SlotObjects_CarryVolume_PlainStringsDont() {
			var games = Games.Parse("""
				{ "1": { "Name": "X", "GameCaptureExecutable": [
					{ "Exe": "game.exe", "Volume": -6.5 },
					"plain.exe",
					{ "exe": "lower.exe", "volume": -2 },
					{ "Exe": "novolume.exe" },
					{ "Volume": -3 },
					5
				] } }
				""");
			Assert.Equal([new Capture("game.exe", -6.5), new Capture("plain.exe"), new Capture("lower.exe", -2), new Capture("novolume.exe")], games["1"].GameCapture);
		}

		[Fact]
		public void ParseDefaultVolume_ReadsDefaultEntry_WhichIsNotAGame() {
			string json = """{ "default": { "Volume": -4 }, "1": { "Name": "GTA3" } }""";
			Assert.Equal(-4, Games.ParseDefaultVolume(json));
			Assert.Equal(["1"], Games.Parse(json).Keys.ToList());
		}

		[Theory]
		[InlineData("""{ "1": { "Name": "GTA3" } }""")]
		[InlineData("""{ "default": { "Name": "Default" } }""")]
		public void ParseDefaultVolume_Missing_IsNull(string json) {
			Assert.Null(Games.ParseDefaultVolume(json));
		}

		[Fact]
		public void ParseDefaultVolume_KeyIsCaseInsensitive() {
			Assert.Equal(-3, Games.ParseDefaultVolume("""{ "Default": { "Volume": -3 } }"""));
		}

		[Theory]
		[InlineData(-6.0, -4.0, -6.0)]
		[InlineData(null, -4.0, -4.0)]
		[InlineData(null, null, null)]
		[InlineData(-150.0, null, -100.0)]
		[InlineData(30.0, null, 26.0)]
		public void CaptureVolumeFor_OwnThenDefault_Clamped(double? own, double? fallback, double? expected) {
			Assert.Equal(expected, GameCapture.VolumeFor(new Capture("x.exe", own), fallback));
		}

		[Fact]
		public void CaptureShownLine_WithAndWithoutVolume() {
			Assert.Equal("Game: Game Capture 0 → ktane.exe, shown, -4 dB", GameCapture.ShownLine("Game: Game Capture 0", "ktane.exe", -4));
			Assert.Equal("Game: Game Capture 0 → ktane.exe, shown", GameCapture.ShownLine("Game: Game Capture 0", "ktane.exe", null));
		}

		[Fact]
		public void Parse_ReadsTheFileShape() {
			var games = Games.Parse("""
				{
					"0": { "Name": "None" },
					"461492": { "Name": "KTANE", "GameCaptureExecutable": [ "ktane.exe" ] },
					"6670": {
						"Name": "SWAT4",
						"GameCaptureExecutable": [ "Swat4X.exe" ],
						"WindowCaptureExecutable": [ "Launcher.exe" ],
						"AudioCaptureExecutable": [ "Swat4.exe", "Swat4X.exe" ]
					}
				}
				""");
			Assert.Equal(3, games.Count);
			Assert.Equal("KTANE", games["461492"].Name);
			Assert.Equal(["ktane.exe"], Exes(games["461492"].GameCapture));
			Assert.Equal(["Swat4X.exe"], Exes(games["6670"].GameCapture));
			Assert.Equal(["Launcher.exe"], Exes(games["6670"].WindowCapture));
			Assert.Equal(["Swat4.exe", "Swat4X.exe"], Exes(games["6670"].AudioCapture));
		}

		[Fact]
		public void Parse_PropertyNamesAreCaseInsensitive() {
			var games = Games.Parse("""{ "1": { "name": "GTA3", "audiocaptureexecutable": [ "gta3.exe" ] } }""");
			Assert.Equal("GTA3", games["1"].Name);
			Assert.Equal(["gta3.exe"], Exes(games["1"].AudioCapture));
		}

		[Fact]
		public void Parse_MissingExecutableLists_AreEmpty() {
			var games = Games.Parse("""{ "1": { "Name": "GTA3" } }""");
			Assert.Empty(games["1"].GameCapture);
			Assert.Empty(games["1"].WindowCapture);
			Assert.Empty(games["1"].AudioCapture);
		}

		[Fact]
		public void Parse_OldExecutablesKey_IsIgnored() {
			var games = Games.Parse("""{ "1": { "Name": "GTA3", "Executables": [ "gta3.exe" ] } }""");
			Assert.Empty(games["1"].AudioCapture);
		}

		[Fact]
		public void CaptureAssignments_FillInOrder_PadWithNone_FromZero() {
			var slots = GameCapture.Assignments("Game: Game Capture ", C("a.exe"));
			Assert.Equal([("Game: Game Capture 0", "a.exe"), ("Game: Game Capture 1", "none"), ("Game: Game Capture 2", "none")], slots.Select(s => (s.Source, s.Capture.Executable)));
		}

		[Fact]
		public void CaptureAssignments_IgnoreExtras() {
			var slots = GameCapture.Assignments("P ", C("a", "b", "c", "d"));
			Assert.Equal(GameCapture.SLOTS, slots.Count);
			Assert.Equal(("P 2", new Capture("c")), slots[2]);
		}

		[Fact]
		public void CaptureAssignments_Empty_AllNone() {
			Assert.All(GameCapture.Assignments("P ", []), s => Assert.Equal("none", s.Capture.Executable));
		}

		[Theory]
		[InlineData("ktane.exe", true)]
		[InlineData("none", false)]
		[InlineData("None", false)]
		public void CaptureIsUsed_NoneIsUnused(string executable, bool used) {
			Assert.Equal(used, GameCapture.IsUsed(executable));
		}

		[Fact]
		public void CaptureHiddenLine_GroupsSlotsByKind() {
			string line = GameCapture.HiddenLine([
				("Game: Game Capture", 1), ("Game: Game Capture", 2),
				("Game: Window Capture", 0), ("Game: Window Capture", 1), ("Game: Window Capture", 2),
				("Audio 5: App Capture", 2),
			]);
			Assert.Equal("→ none, hidden: Game: Game Capture 1, 2; Game: Window Capture 0, 1, 2; Audio 5: App Capture 2", line);
		}

		[Fact]
		public void RunStep_ThrowingStep_IsLogged_AndDoesNotThrow() {
			List<string> log = new();
			void OnLine(ConsoleLogger.ColorType _, string line) { lock (log) log.Add(line); }
			ConsoleLogger.LineLogged += OnLine;
			try {
				Games.RunStep("colour", () => throw new InvalidOperationException("boom"));
				bool ran = false;
				Games.RunStep("capture sources", () => ran = true);
				Assert.True(ran);
			}
			finally {
				ConsoleLogger.LineLogged -= OnLine;
			}
			lock (log) {
				Assert.Single(log, l => l.Contains("Error GMS2: game setup colour failed: boom"));
			}
		}

		[Theory]
		[InlineData("Color change request", "navy blue", true, "Color change request fulfilled: navy blue")]
		[InlineData("Color change request", "nonsense", false, "Color change request refunded: nonsense")]
		[InlineData("Train request", "", true, "Train request fulfilled")]
		[InlineData("Train request", "", false, "Train request refunded")]
		public void RedemptionLine_FulfilledOrRefunded(string request, string detail, bool success, string expected) {
			Assert.Equal(expected, ChannelPoints.RedemptionLine(request, detail, success));
		}

		[Fact]
		public void CaptureWindowValue_IsTitleClassExecutable() {
			Assert.Equal("PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:ktane.exe", GameCapture.WindowValue("ktane.exe"));
		}

		[Theory]
		[InlineData("""{ "1": { "AudioCaptureExecutable": [ "a.exe" ] } }""")]
		[InlineData("""{ "1": { "Name": "" } }""")]
		[InlineData("""{ "1": null }""")]
		public void Parse_EntriesWithoutName_AreSkipped(string json) {
			Assert.Empty(Games.Parse(json));
		}

		[Fact]
		public void Parse_EmptyObject_IsEmpty() {
			Assert.Empty(Games.Parse("{}"));
		}

		static readonly Dictionary<string, Game> Known = new() {
			["0"] = new Game("None", [], [], []),
			["461492"] = new Game("KTANE", C("ktane.exe"), [], []),
			["example"] = new Game("Example", [], [], []),
		};

		[Theory]
		[InlineData("461492", "461492")]
		[InlineData("KTANE", "461492")]
		[InlineData("ktane", "461492")]
		[InlineData("example", "example")]
		[InlineData("Example", "example")]
		[InlineData("999", "999")]
		[InlineData("nonsense", null)]
		[InlineData("12a", null)]
		public void ResolveCategory_KeyNameOrDigits(string input, string? expected) {
			Assert.Equal(expected, Games.ResolveCategory(Known, input));
		}

		[Fact]
		public void Lookup_KnownId() {
			Assert.Equal("KTANE", Games.Lookup(Known, "461492").Name);
		}

		[Fact]
		public void Lookup_UnknownId_FallsBackToZero() {
			Assert.Same(Known["0"], Games.Lookup(Known, "999"));
		}

		[Fact]
		public void Lookup_UnknownId_WithoutZero_IsNone() {
			Game game = Games.Lookup(new Dictionary<string, Game>(), "999");
			Assert.Equal("None", game.Name);
			Assert.Empty(game.GameCapture);
			Assert.Empty(game.WindowCapture);
			Assert.Empty(game.AudioCapture);
		}
	}
}
