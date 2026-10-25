using Xunit;
using Chip = StreamAssistant2.ObsInputs.Chip;
using Entry = StreamAssistant2.ObsInputs.Entry;
using Kind = StreamAssistant2.ObsInputs.Kind;
using LineKind = StreamAssistant2.ObsInputs.LineKind;
using StatusLine = StreamAssistant2.ObsInputs.StatusLine;

namespace StreamAssistant2.Tests {
	// ObsInputs: the record the OBS wrappers keep for the dashboard, and the lines built from it. Each test uses its own
	// Store, never ObsInputs.Current, and nothing here reaches OBS.
	public class ObsInputsTests {
		static readonly DateTime T0 = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

		[Fact]
		public void Store_LaterSuccessClearsFailure() {
			ObsInputs.Store store = new();
			store.Set("Text: Money Goal", "text", Kind.Text, "0 / 999 EUR", T0);
			store.Failed("Text: Money Goal", "text", Kind.Text, "1 / 999 EUR", "No source", T0.AddSeconds(1));
			Assert.Equal("No source", Assert.Single(store.Snapshot()).Error);
			store.Set("Text: Money Goal", "text", Kind.Text, "2 / 999 EUR", T0.AddSeconds(2));
			Entry entry = Assert.Single(store.Snapshot());
			Assert.Null(entry.Error);
			Assert.Equal("2 / 999 EUR", entry.Value);
		}

		[Fact]
		public void Store_TwoSettingsOnOneSource_AreTwoEntries() {
			ObsInputs.Store store = new();
			store.Set("Game: Game Capture 0", "window", Kind.Capture, "a:b:gta3.exe", T0);
			store.Set("Game: Game Capture 0", "volume", Kind.Volume, "-4", T0);
			Assert.Equal(2, store.Snapshot().Count);
		}

		[Fact]
		public void Store_SnapshotIsACopy() {
			ObsInputs.Store store = new();
			store.Set("Image: Background", "file", Kind.Image, @"C:\a.png", T0);
			List<Entry> snapshot = store.Snapshot();
			store.Set("Image: Train", "file", Kind.Image, @"C:\b.png", T0);
			Assert.Single(snapshot);
		}

		[Theory]
		[InlineData("window", "a:b:gta3.exe", "Capture")]
		[InlineData("file", @"C:\Images\GTA3.png", "Image")]
		[InlineData("file", @"C:\Images\Train.JPG", "Image")]
		[InlineData("file", @"C:\Text\SegmentTime.txt", "Text")]
		[InlineData("text", "U: Offline", "Text")]
		[InlineData("url", "https://x", "Other")]
		public void KindOf_ByKeyAndFileType(string key, string value, string expected) {
			Assert.Equal(Enum.Parse<Kind>(expected), ObsInputs.KindOf(key, value));
		}

		[Theory]
		[InlineData("#ff8800")]
		[InlineData("#000000")]
		[InlineData("#ffffff")]
		[InlineData("#12abef")]
		public void ColorHex_UndoesToOBS(string hex) {
			Assert.Equal(hex, ObsInputs.ColorHex(ColorUtil.ToOBS(hex)));
		}

		[Theory]
		[InlineData("Capture", "window", "PLACEHOLDER-TITLE:PLACEHOLDER-CLASS:gta3.exe", "gta3.exe")]
		[InlineData("Capture", "window", "none", "none")]
		[InlineData("Image", "file", @"D:\Stream-Resources\Images\Backgrounds\GTA3.png", "GTA3.png")]
		[InlineData("Text", "file", @"D:\Stream-Resources\Text\SegmentTime.txt", "SegmentTime.txt")]
		[InlineData("Text", "text", "line one\nline two", "\"line one ⏎ line two\"")]
		[InlineData("Text", "text", "", "\"\"")]
		[InlineData("Volume", "volume", "-4", "-4 dB")]
		[InlineData("Filter", "Color Correction · color_add", "#ff8800", "#ff8800")]
		public void Shown_ByKind(string kind, string setting, string value, string expected) {
			Assert.Equal(expected, ObsInputs.Shown(new Entry("S", setting, Enum.Parse<Kind>(kind), value, T0)));
		}

		[Fact]
		public void StatusLines_TextReadFromFile_ListedByFileNameWithItsPath() {
			List<Entry> entries = [new("Livesplit: Current Segment 1", "file", Kind.Text, @"C:\Text\SegmentTime.txt", T0)];
			StatusLine line = ObsInputs.StatusLines(entries, true, T0)[2];
			Assert.Equal("Livesplit: Current Segment 1 → SegmentTime.txt · 0 s ago", line.Text);
			Assert.Equal($@"Livesplit: Current Segment 1{Environment.NewLine}C:\Text\SegmentTime.txt", line.ToolTip);
			Assert.Equal("Livesplit: C S 1 → SegmentTime.txt · 0 s ago", ObsInputs.StatusLines(entries, true, T0, width: 45)[2].Text);
		}

		[Theory]
		[InlineData(0, "0 s ago")]
		[InlineData(59, "59 s ago")]
		[InlineData(60, "1 m ago")]
		[InlineData(59 * 60 + 59, "59 m ago")]
		[InlineData(2 * 3600 + 5, "2 h ago")]
		[InlineData(24 * 3600 - 1, "23 h ago")]
		[InlineData(3 * 24 * 3600 + 7, "3 d ago")]
		[InlineData(-3, "0 s ago")]
		public void Ago_InSecondsMinutesHours(int seconds, string expected) {
			Assert.Equal(expected, ObsInputs.Ago(TimeSpan.FromSeconds(seconds)));
		}

		[Fact]
		public void StatusLines_NothingSet() {
			var lines = ObsInputs.StatusLines([], obsReady: true, T0);
			Assert.Equal(["OBS sources", "Nothing set yet"], lines.Select(l => l.Text));
		}

		[Fact]
		public void StatusLines_NotConnectedTitle() {
			var lines = ObsInputs.StatusLines([], obsReady: false, T0);
			Assert.Equal("OBS sources (OBS not connected)", lines[0].Text);
			Assert.Equal(LineKind.Title, lines[0].Kind);
		}

		[Fact]
		public void StatusLines_GroupsInOrder_SortedBySource_EmptyGroupsLeftOut() {
			List<Entry> entries = [
				new("Text: Time Of Day", "text", Kind.Text, "2026-10-16 12:00", T0),
				new("Image: Train", "file", Kind.Image, @"C:\Train1.png", T0),
				new("Image: Background", "file", Kind.Image, @"C:\GTA3.png", T0),
				new("Game: Game Capture 0", "window", Kind.Capture, "a:b:gta3.exe", T0),
				new("Some Source", "Blur · size", Kind.Filter, "12", T0),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0.AddSeconds(12));
			Assert.Equal([
				"OBS sources",
				"Captures",
				"Game: Game Capture 0 → gta3.exe · 12 s ago",
				"Filters",
				"Some Source · Blur · size → 12 · 12 s ago",
				"Images",
				"Image: Background → GTA3.png · 12 s ago",
				"Image: Train → Train1.png · 12 s ago",
				"Text",
				"Text: Time Of Day → \"2026-10-16 12:00\" · 12 s ago",
			], lines.Select(l => l.Text));
			Assert.Equal([LineKind.Title, LineKind.Heading, LineKind.Item, LineKind.Heading, LineKind.Item, LineKind.Heading, LineKind.Item, LineKind.Item, LineKind.Heading, LineKind.Item],
				lines.Select(l => l.Kind));
		}

		[Fact]
		public void StatusLines_VolumeJoinsItsCapture_AgeFromTheNewer() {
			List<Entry> entries = [
				new("Audio 5: App Capture 0", "window", Kind.Capture, "a:b:gta3.exe", T0),
				new("Audio 5: App Capture 0", "volume", Kind.Volume, "-4", T0.AddSeconds(5)),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0.AddSeconds(10));
			Assert.Equal(["OBS sources", "Captures", "Audio 5: App Capture 0 → gta3.exe · -4 dB · 5 s ago"], lines.Select(l => l.Text));
		}

		[Fact]
		public void StatusLines_VolumeWithoutCapture_GoesUnderOther() {
			List<Entry> entries = [new("Audio: Z5 Game0", "volume", Kind.Volume, "-13.17", T0)];
			var lines = ObsInputs.StatusLines(entries, true, T0);
			Assert.Equal(["OBS sources", "Other", "Audio: Z5 Game0 · volume → -13.17 dB · 0 s ago"], lines.Select(l => l.Text));
		}

		[Fact]
		public void StatusLines_FailureIsRedWithTheError() {
			List<Entry> entries = [new("Text: Money Goal", "text", Kind.Text, "0 / 999 EUR", T0, "No source was found")];
			StatusLine line = ObsInputs.StatusLines(entries, true, T0.AddMinutes(2))[2];
			Assert.Equal("Text: Money Goal → \"0 / 999 EUR\" · failed: No source was found · 2 m ago", line.Text);
			Assert.Equal(LineKind.Failed, line.Kind);
		}

		[Fact]
		public void StatusLines_FailedVolumeMakesItsCaptureRed() {
			List<Entry> entries = [
				new("Game: Game Capture 0", "window", Kind.Capture, "a:b:gta3.exe", T0),
				new("Game: Game Capture 0", "volume", Kind.Volume, "-4", T0, "refused"),
			];
			Assert.Equal(LineKind.Failed, ObsInputs.StatusLines(entries, true, T0)[2].Kind);
		}

		[Fact]
		public void StatusLines_ImageHoverIsTheFullPath() {
			List<Entry> entries = [new("Image: Background", "file", Kind.Image, @"D:\Images\GTA3.png", T0)];
			StatusLine line = ObsInputs.StatusLines(entries, true, T0)[2];
			Assert.Equal($@"Image: Background{Environment.NewLine}D:\Images\GTA3.png", line.ToolTip);
			Assert.Null(line.Swatch);
		}

		[Fact]
		public void StatusLines_UnknownFilterIsVerboseWithSwatch() {
			List<Entry> entries = [Filter("Some Source", "Color Correction", "color_multiply", "#ff8800")];
			var lines = ObsInputs.StatusLines(entries, true, T0);
			Assert.Equal(["OBS sources", "Filters", "Some Source · Color Correction · color_multiply → #ff8800 · 0 s ago"], lines.Select(l => l.Text));
			Assert.Equal("#ff8800", lines[2].Swatch);
			Assert.Null(lines[2].Chips);
		}

		[Fact]
		public void StatusLines_FilterWithoutAColour_HasNoSwatch() {
			List<Entry> entries = [new("Some Source", "Blur · size", Kind.Filter, "12", T0)];
			Assert.Null(ObsInputs.StatusLines(entries, true, T0)[2].Swatch);
		}

		// A colour filter entry as Obs.SetFilterProperty records it
		static Entry Filter(string source, string filter, string property, string hex, DateTime? at = null, string? error = null) =>
			new(source, $"{filter} · {property}", Kind.Filter, hex, at ?? T0, error);

		// The layout's five filter entries as one colour change sets them
		static List<Entry> Layout(string twin, string inner, string outer, string text) => [
			Filter(LayoutColoring.INNER + twin, LayoutColoring.FILTER, "color_multiply", inner),
			Filter(LayoutColoring.INNER_ATTACHMENT + twin, LayoutColoring.FILTER, "color_multiply", inner),
			Filter(LayoutColoring.OUTER + twin, LayoutColoring.FILTER, "color_add", outer),
			Filter(LayoutColoring.OUTER_ATTACHMENT + twin, LayoutColoring.FILTER, "color_add", outer),
			Filter(LayoutColoring.TEXT_SCENE + twin, LayoutColoring.FILTER, "color_multiply", text),
		];

		static IEnumerable<(string Code, string? Hex)> Codes(StatusLine row) => row.Chips!.Select(c => (c.Code, c.Hex));

		[Fact]
		public void StatusLines_LayoutAndFadeRows_AttachmentsJoinTheirSwatch() {
			List<Entry> entries = [.. Layout("", "#ff0000", "#00ff00", "#0000ff"), .. Layout(LayoutColoring.TWIN, "#110000", "#001100", "#000011")];
			var lines = ObsInputs.StatusLines(entries, true, T0.AddSeconds(3));
			Assert.Equal(["OBS sources", "Filters", "Layout", "Fade"], lines.Select(l => l.Text));
			Assert.Equal([("In", "#ff0000"), ("Out", "#00ff00"), ("Txt", "#0000ff")], Codes(lines[2]));
			Assert.Equal([("In", "#110000"), ("Out", "#001100"), ("Txt", "#000011")], Codes(lines[3]));
			Assert.Equal("· 3 s ago", lines[2].After);
			Assert.Contains(LayoutColoring.INNER_ATTACHMENT, lines[2].Chips![0].ToolTip);
			Assert.DoesNotContain("ago", lines[2].Chips![0].ToolTip);
		}

		[Fact]
		public void StatusLines_AttachmentOfAnotherColour_IsListedVerbosely() {
			List<Entry> entries = Layout("", "#ff0000", "#00ff00", "#0000ff");
			entries[1] = Filter(LayoutColoring.INNER_ATTACHMENT, LayoutColoring.FILTER, "color_multiply", "#123456");
			var lines = ObsInputs.StatusLines(entries, true, T0);
			Assert.Equal(["OBS sources", "Filters", "Layout", $"{LayoutColoring.INNER_ATTACHMENT} · Color Correction · color_multiply → #123456 · 0 s ago"], lines.Select(l => l.Text));
			Assert.DoesNotContain(LayoutColoring.INNER_ATTACHMENT, lines[2].Chips![0].ToolTip);
		}

		[Fact]
		public void StatusLines_UnsetSwatchInAShownRow_HasNoColour() {
			List<Entry> entries = [Filter(LayoutColoring.INNER, LayoutColoring.FILTER, "color_multiply", "#ff0000")];
			StatusLine row = ObsInputs.StatusLines(entries, true, T0)[2];
			Assert.Equal([("In", "#ff0000"), ("Out", null), ("Txt", null)], Codes(row));
			Assert.Contains("not set", row.Chips![1].ToolTip);
		}

		[Fact]
		public void StatusLines_HighlightsAndPreviousRows_InTheirOrder() {
			List<Entry> entries = [
				Filter("Livesplit: SL Highlights Gold", SplitColors.HIGHLIGHT_FILTER, "color_multiply", "#ffd700"),
				Filter("Livesplit: SL Highlights AheadGained", SplitColors.HIGHLIGHT_FILTER, "color_multiply", "#00cc00"),
				Filter("Livesplit: SL Highlights AheadLost", SplitColors.HIGHLIGHT_FILTER, "color_multiply", "#66cc66"),
				Filter("Livesplit: SL Highlights BehindGained", SplitColors.HIGHLIGHT_FILTER, "color_multiply", "#cc6666"),
				Filter("Livesplit: SL Highlights BehindLost", SplitColors.HIGHLIGHT_FILTER, "color_multiply", "#cc0000"),
				Filter(SplitColors.PREVIOUS_GOLD, SplitColors.PREVIOUS_FILTER, "color_multiply", "#ffffff"),
				Filter(SplitColors.PREVIOUS_SEGMENT, SplitColors.PREVIOUS_FILTER, "color_multiply", "#00ff00"),
				Filter(SplitColors.PREVIOUS_RUN, SplitColors.PREVIOUS_FILTER, "color_multiply", "#ff0000"),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0);
			Assert.Equal(["OBS sources", "Filters", "Highlights", "Previous"], lines.Select(l => l.Text));
			Assert.Equal([("AG", "#00cc00"), ("AL", "#66cc66"), ("BG", "#cc6666"), ("BL", "#cc0000"), ("Au", "#ffd700")], Codes(lines[2]));
			Assert.Equal([("Run", "#ff0000"), ("Seg", "#00ff00"), ("Au", "#ffffff")], Codes(lines[3]));
		}

		[Fact]
		public void StatusLines_RefusedSwatch_IsMarkedWithTheError() {
			List<Entry> entries = [Filter(LayoutColoring.OUTER, LayoutColoring.FILTER, "color_add", "#00ff00", error: "No filter")];
			Chip chip = ObsInputs.StatusLines(entries, true, T0)[2].Chips![1];
			Assert.True(chip.Failed);
			Assert.Contains("failed: No filter", chip.ToolTip);
		}

		[Fact]
		public void StatusLines_RowOfAHiddenGroup_IsDimWithHiddenSwatches() {
			List<Entry> entries = Layout("", "#ff0000", "#00ff00", "#0000ff");
			List<ObsInputs.Visibility> visible = [
				new("!Scene: Layout 16x9", LayoutColoring.COLORABLES, false),
				new("!Scene: Layout 5x4", LayoutColoring.COLORABLES, false),
			];
			StatusLine row = ObsInputs.StatusLines(entries, true, T0, visible)[2];
			Assert.Equal(LineKind.Dim, row.Kind);
			Assert.All(row.Chips!, chip => Assert.True(chip.Hidden));
			Assert.Contains($"{LayoutColoring.COLORABLES} hidden", row.Chips![0].ToolTip);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(null)]
		public void StatusLines_RowOfAShownOrUntouchedGroup_IsPlain(bool? enabled) {
			List<Entry> entries = Layout("", "#ff0000", "#00ff00", "#0000ff");
			List<ObsInputs.Visibility> visible = enabled is bool on ? [new("!Scene: Layout 16x9", LayoutColoring.COLORABLES, on)] : [];
			StatusLine row = ObsInputs.StatusLines(entries, true, T0, visible)[2];
			Assert.Equal(LineKind.Item, row.Kind);
			Assert.All(row.Chips!, chip => Assert.False(chip.Hidden));
		}

		// The previous-split group as the left panel names it in its coloured scene, in a numbered copy
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void StatusLines_LeftPanelGroup_MatchedThroughPrefixAndCopy(bool enabled) {
			List<Entry> entries = [Filter(SplitColors.PREVIOUS_RUN, SplitColors.PREVIOUS_FILTER, "color_multiply", "#ff0000")];
			List<ObsInputs.Visibility> visible = [new("!Scene: Left Panel Colored", $"_LPC: {SplitColors.PREVIOUS_GROUP} 2", enabled)];
			Assert.Equal(enabled ? LineKind.Item : LineKind.Dim, ObsInputs.StatusLines(entries, true, T0, visible)[2].Kind);
		}

		[Fact]
		public void StatusLines_LabelIsThePartBeforeTheArrow() {
			List<Entry> entries = [
				Capture("Game: Game Capture 0", "gta3.exe"),
				new("Some Source", "Blur · size", Kind.Filter, "12", T0),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0);
			Assert.Equal("Game: Game Capture 0", lines[2].Label);
			Assert.Equal("Some Source · Blur · size", lines[4].Label);
			Assert.Null(lines[0].Label);
			Assert.Null(lines[1].Label);
		}

		[Theory]
		[InlineData(39, "Border: Colorable Outer (Transitionary)")]
		[InlineData(35, "Border: Colorable Outer (T)")]
		[InlineData(25, "Border: Colorable O (T)")]
		[InlineData(20, "Border: C O (T)")]
		[InlineData(10, "B: C O (T)")]
		[InlineData(4, "B: C O (T)")]
		public void Abbreviate_FromTheLastWord(int length, string expected) {
			Assert.Equal(expected, ObsInputs.Abbreviate("Border: Colorable Outer (Transitionary)", length));
		}

		[Theory]
		[InlineData(null, "Border: Colorable Outer (Transitionary) → border.png · 0 s ago")]
		[InlineData(200, "Border: Colorable Outer (Transitionary) → border.png · 0 s ago")]
		[InlineData(45, "Border: C O (T) → border.png · 0 s ago")]
		public void StatusLines_ImageNamesShortenedToFitTheWidth(int? width, string expected) {
			List<Entry> entries = [new("Border: Colorable Outer (Transitionary)", "file", Kind.Image, @"C:\border.png", T0)];
			StatusLine line = ObsInputs.StatusLines(entries, true, T0, width: width)[2];
			Assert.Equal(expected, line.Text);
			Assert.StartsWith("Border: Colorable Outer (Transitionary)", line.ToolTip);
		}

		[Fact]
		public void StatusLines_ImageNotShowingInTheLayout_IsDimWithANote() {
			List<Entry> entries = [
				new("Image: Train", "file", Kind.Image, @"C:\Empty.png", T0),
				new("Image: Background", "file", Kind.Image, @"C:\GTA3.png", T0),
			];
			Dictionary<string, bool> active = new() { ["Image: Train"] = false, ["Image: Background"] = true };
			var lines = ObsInputs.StatusLines(entries, true, T0, showing: active);
			StatusLine background = lines.Single(l => l.Text.StartsWith("Image: Background"));
			StatusLine train = lines.Single(l => l.Text.StartsWith("Image: Train"));
			Assert.Equal(LineKind.Item, background.Kind);
			Assert.Equal(LineKind.Dim, train.Kind);
			Assert.EndsWith("not showing in Basic Stream Layout", train.ToolTip);
		}

		[Fact]
		public void StatusLines_TextNotShowingInTheLayout_IsDimWithANote() {
			List<Entry> entries = [
				new("Livesplit: Current Split", "text", Kind.Text, "Mission 3", T0),
				new("Text: Time Of Day", "text", Kind.Text, "12:00", T0),
			];
			Dictionary<string, bool> showing = new() { ["Livesplit: Current Split"] = false, ["Text: Time Of Day"] = true };
			var lines = ObsInputs.StatusLines(entries, true, T0, showing: showing);
			StatusLine split = lines.Single(l => l.Text.StartsWith("Livesplit: Current Split"));
			Assert.Equal(LineKind.Dim, split.Kind);
			Assert.Equal("not showing in Basic Stream Layout", split.ToolTip);
			Assert.Equal(LineKind.Item, lines.Single(l => l.Text.StartsWith("Text: Time Of Day")).Kind);
		}

		[Fact]
		public void StatusLines_ImageOBSHasNotAnsweredFor_IsPlain() {
			List<Entry> entries = [new("Image: Train", "file", Kind.Image, @"C:\Empty.png", T0)];
			Assert.Equal(LineKind.Item, ObsInputs.StatusLines(entries, true, T0, showing: new Dictionary<string, bool>())[2].Kind);
		}

		[Fact]
		public void StatusLines_FailedImageNotShowing_StaysRed() {
			List<Entry> entries = [new("Image: Train", "file", Kind.Image, @"C:\Empty.png", T0, "No source")];
			Dictionary<string, bool> active = new() { ["Image: Train"] = false };
			Assert.Equal(LineKind.Failed, ObsInputs.StatusLines(entries, true, T0, showing: active)[2].Kind);
		}

		[Fact]
		public void StatusLine_EqualityComparesChips() {
			StatusLine a = new("Layout", LineKind.Item, Chips: [new Chip("In", "#ff0000", "t")], After: "0 s ago");
			StatusLine b = new("Layout", LineKind.Item, Chips: [new Chip("In", "#ff0000", "t")], After: "0 s ago");
			StatusLine c = new("Layout", LineKind.Item, Chips: [new Chip("In", "#00ff00", "t")], After: "0 s ago");
			Assert.Equal(a, b);
			Assert.Equal(a.GetHashCode(), b.GetHashCode());
			Assert.NotEqual(a, c);
		}

		// A capture slot as GameCapture sets it
		static Entry Capture(string source, string executable, DateTime? at = null, string? error = null) =>
			new(source, "window", Kind.Capture, GameCapture.WindowValue(executable), at ?? T0, error);

		[Fact]
		public void StatusLines_UnusedCaptures_OneDimLineGroupedByKind() {
			List<Entry> entries = [
				Capture("Game: Game Capture 0", "gta3.exe"),
				Capture("Game: Game Capture 1", "none"),
				Capture("Game: Game Capture 2", "none", T0.AddSeconds(3)),
				Capture("Game: Window Capture 0", "none"),
				Capture("Audio 5: App Capture 0", "none"),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0.AddSeconds(10));
			Assert.Equal([
				"OBS sources",
				"Captures",
				"Game: Game Capture 0 → gta3.exe · 10 s ago",
				"Audio capture 0; Game capture 1, 2; Window capture 0 → none · 7 s ago",
			], lines.Select(l => l.Text));
			Assert.Equal(LineKind.Dim, lines[3].Kind);
			Assert.Equal("Audio capture 0; Game capture 1, 2; Window capture 0", lines[3].Label);
		}

		[Fact]
		public void StatusLines_RefusedUnusedCapture_StaysApartInRed() {
			List<Entry> entries = [Capture("Game: Game Capture 1", "none", error: "No source")];
			StatusLine line = ObsInputs.StatusLines(entries, true, T0)[2];
			Assert.Equal("Game: Game Capture 1 → none · failed: No source · 0 s ago", line.Text);
			Assert.Equal(LineKind.Failed, line.Kind);
		}

		[Fact]
		public void StatusLines_ImagesAndTexts_ByName_NotByAge() {
			List<Entry> entries = [
				new("Image: Train", "file", Kind.Image, @"C:\Train1.png", T0),
				new("Image: Background", "file", Kind.Image, @"C:\GTA3.png", T0.AddSeconds(5)),
				new("Text: Time Of Day", "text", Kind.Text, "12:00", T0.AddSeconds(2)),
				new("Text: Money Goal", "text", Kind.Text, "0 / 999 EUR", T0.AddSeconds(8)),
			];
			var lines = ObsInputs.StatusLines(entries, true, T0.AddSeconds(10));
			Assert.Equal([
				"OBS sources",
				"Images",
				"Image: Background → GTA3.png · 5 s ago",
				"Image: Train → Train1.png · 10 s ago",
				"Text",
				"Text: Money Goal → \"0 / 999 EUR\" · 2 s ago",
				"Text: Time Of Day → \"12:00\" · 8 s ago",
			], lines.Select(l => l.Text));
		}

		// A fake OBS: each scene or group by name with its items; the requests it was asked, in order
		sealed class FakeScenes {
			readonly Dictionary<string, List<ObsInputs.SceneItem>> _scenes;
			public List<(string Name, bool Group)> Asked { get; } = [];

			public FakeScenes(Dictionary<string, List<ObsInputs.SceneItem>> scenes) {
				_scenes = scenes;
			}

			public List<ObsInputs.SceneItem>? List(string name, bool group) {
				Asked.Add((name, group));
				return _scenes.GetValueOrDefault(name);
			}
		}

		static ObsInputs.SceneItem Item(string source, bool enabled = true) => new(source, enabled, false, false);
		static ObsInputs.SceneItem Scene(string source, bool enabled = true) => new(source, enabled, true, false);
		static ObsInputs.SceneItem GroupItem(string source, bool enabled = true) => new(source, enabled, true, true);

		[Fact]
		public void ShowingIn_FollowsEnabledScenesAndGroups() {
			FakeScenes obs = new(new() {
				["Basic Stream Layout"] = [Item("Image: Background"), Scene("!Scene: Layout 16x9"), Scene("!Scene: Layout 5x4", enabled: false), Item("Image: Train", enabled: false)],
				["!Scene: Layout 16x9"] = [GroupItem("!Layout: Colorables"), GroupItem("!Layout: Colorables (Transitionary)", enabled: false)],
				["!Scene: Layout 5x4"] = [Item("Border: Colorable Inner 5x4")],
				["!Layout: Colorables"] = [Item("Border: Colorable Inner")],
				["!Layout: Colorables (Transitionary)"] = [Item("Border: Colorable Inner (Transitionary)")],
			});
			HashSet<string> showing = ObsInputs.ShowingIn("Basic Stream Layout", obs.List)!;
			Assert.Contains("Image: Background", showing);
			Assert.Contains("Border: Colorable Inner", showing);
			Assert.DoesNotContain("Image: Train", showing);
			Assert.DoesNotContain("Border: Colorable Inner (Transitionary)", showing);
			Assert.DoesNotContain("Border: Colorable Inner 5x4", showing);
			Assert.Contains(("!Layout: Colorables", true), obs.Asked);
			Assert.DoesNotContain(("!Scene: Layout 5x4", false), obs.Asked);
		}

		[Fact]
		public void ShowingIn_ListsEachSceneOnce_EvenInALoop() {
			FakeScenes obs = new(new() {
				["Basic Stream Layout"] = [Scene("A"), Scene("A")],
				["A"] = [Item("Image: X"), Scene("Basic Stream Layout")],
			});
			HashSet<string> showing = ObsInputs.ShowingIn("Basic Stream Layout", obs.List)!;
			Assert.Contains("Image: X", showing);
			Assert.Equal([("Basic Stream Layout", false), ("A", false)], obs.Asked);
		}

		[Fact]
		public void ShowingIn_NullWhenTheSceneCantBeListed_EmptyForAnInnerOne() {
			Assert.Null(ObsInputs.ShowingIn("Basic Stream Layout", new FakeScenes(new()).List));
			FakeScenes obs = new(new() { ["Basic Stream Layout"] = [Scene("Missing"), Item("Image: Y")] });
			Assert.Equal(["Image: Y", "Missing"], ObsInputs.ShowingIn("Basic Stream Layout", obs.List)!.Order());
		}
	}
}
