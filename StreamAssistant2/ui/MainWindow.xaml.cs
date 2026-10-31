using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StreamAssistant2 {
	public partial class MainWindow : Window {
		const int MAX_LINES = 5000;

		public sealed class LogLine(string text, Brush brush) {
			public string Text { get; } = text;
			public Brush Brush { get; } = brush;
		}

		// A row of the splits column: the nesting guides, the strokes of the path to the current split drawn over them, the
		// two characters before the name (a marker or the end of the branch) in their colour, the name in its colour, the
		// name column's width (NaN to fit), the run and segment times, the dots before the segment time, the row's
		// background, hover text, and on the root's row the colour of the root's dot
		public sealed class SplitLine(string guides, string path, string before, Brush beforeBrush, string text, Brush brush, double nameWidth,
			SplitCell run, SplitCell segment, string dots, Brush background, string? toolTip, Brush? dot = null) {
			public string Guides { get; } = guides;
			public string Path { get; } = path;
			public string Before { get; } = before;
			public Brush BeforeBrush { get; } = beforeBrush;
			public string Text { get; } = text;
			public Brush Brush { get; } = brush;
			public double NameWidth { get; } = nameWidth;
			public SplitCell Run { get; } = run;
			public SplitCell Segment { get; } = segment;
			public string Dots { get; } = dots;
			public Brush Background { get; } = background;
			public string? ToolTip { get; } = toolTip;
			public Brush? Dot { get; } = dot;
		}

		// A time on a splits row, or a column heading, in its colour and its column's width
		public sealed class SplitCell(string text, Brush brush, double width) {
			public static readonly SplitCell None = new("", Brushes.Transparent, 0);

			public string Text { get; } = text;
			public Brush Brush { get; } = brush;
			public double Width { get; } = width;
		}

		// Raises PropertyChanged for a property that changed, so the list updates its rows in place
		public abstract class Notifying : INotifyPropertyChanged {
			public event PropertyChangedEventHandler? PropertyChanged;

			// Sets the field and tells the bindings, when the value differs
			protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "") {
				if (EqualityComparer<T>.Default.Equals(field, value)) {
					return;
				}
				field = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			}
		}

		// A line of the OBS sources list, kept and updated in place so its row (and an open tooltip) survives a redraw:
		// its label (the part before the arrow) and the rest in their own colours, hover text and colour swatch when it
		// has them; a filter row has chips and an age, a heading its own look
		public sealed class SourceLine : Notifying {
			string _label = "", _rest = "";
			Brush _labelBrush = Brushes.Transparent, _brush = Brushes.Transparent;
			string? _toolTip, _after;
			Brush? _swatch;
			bool _isRow, _isHeading;

			public string Label { get => _label; set => Set(ref _label, value); }
			public Brush LabelBrush { get => _labelBrush; set => Set(ref _labelBrush, value); }
			public string Rest { get => _rest; set => Set(ref _rest, value); }
			public Brush Brush { get => _brush; set => Set(ref _brush, value); }
			public string? ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
			public Brush? Swatch { get => _swatch; set => Set(ref _swatch, value); }
			public string? After { get => _after; set => Set(ref _after, value); }
			public bool IsRow { get => _isRow; set => Set(ref _isRow, value); }
			public bool IsHeading { get => _isHeading; set => Set(ref _isHeading, value); }
			public ObservableCollection<ChipView> Chips { get; } = new();
		}

		// One swatch of a filter row, updated in place: its fill (clear while unset), edge, code and hover text
		public sealed class ChipView : Notifying {
			string _code = "", _toolTip = "";
			Brush _fill = Brushes.Transparent, _edge = Brushes.Transparent, _codeBrush = Brushes.Transparent;

			public string Code { get => _code; set => Set(ref _code, value); }
			public Brush Fill { get => _fill; set => Set(ref _fill, value); }
			public Brush Edge { get => _edge; set => Set(ref _edge, value); }
			public Brush CodeBrush { get => _codeBrush; set => Set(ref _codeBrush, value); }
			public string ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
		}

		// An account's part of the name colour bar, updated in place so its tooltip stays open: the separator before it,
		// the login, the colour's swatch and description, the time since it was set, and a note
		public sealed class NameColorView : Notifying {
			string _separator = "", _label = "", _text = "", _age = "", _note = "";
			Brush? _swatch;
			Brush _noteBrush = Brushes.Transparent;
			string? _toolTip;

			public string Separator { get => _separator; set => Set(ref _separator, value); }
			public string Label { get => _label; set => Set(ref _label, value); }
			public Brush? Swatch { get => _swatch; set => Set(ref _swatch, value); }
			public string Text { get => _text; set => Set(ref _text, value); }
			public string Age { get => _age; set => Set(ref _age, value); }
			public string Note { get => _note; set => Set(ref _note, value); }
			public Brush NoteBrush { get => _noteBrush; set => Set(ref _noteBrush, value); }
			public string? ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
		}

		// An entry of the splits column's comparison dropdown, updated in place so an open dropdown stays open: its text,
		// hover text, and the choice it makes
		public sealed class ComparisonView : Notifying {
			string _label = "", _choice = "";
			string? _toolTip;

			public string Label { get => _label; set => Set(ref _label, value); }
			public string? ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
			public string Choice { get => _choice; set => Set(ref _choice, value); }
		}

		// An entry of the splits column's list dropdown: its text, hover text, and the list it shows
		internal sealed record ListEntry(string Label, string ToolTip, SplitList.ListKind Kind);

		// The list dropdown's entries
		static readonly ListEntry[] _listEntries = [
			new("Surrounding", "The splits around the current one, or the one scrolled to in LiveSplit (Output to File's Split list)",
				SplitList.ListKind.Surrounding),
			new("Completed splits", "Every split done this run (Output to File's Completed splits)", SplitList.ListKind.Completed),
			new("Remaining splits", "The current split and every one after it (Output to File's Remaining splits)", SplitList.ListKind.Remaining),
			new("All splits", "Every split of the run (Output to File's Full split list)", SplitList.ListKind.Full),
		];

		static readonly Dictionary<ConsoleLogger.ColorType, Brush> _logColors = new() {
			[ConsoleLogger.ColorType.None] = MakeBrush("#EEEEEE"),
			[ConsoleLogger.ColorType.Error] = MakeBrush("#EE4444"),
			[ConsoleLogger.ColorType.ChatIncoming] = MakeBrush("#EEEEBB"),
			[ConsoleLogger.ColorType.ChatOutgoing] = MakeBrush("#EEEE44"),
			[ConsoleLogger.ColorType.Notification] = MakeBrush("#44EE44"),
			[ConsoleLogger.ColorType.ConnectionNotification] = MakeBrush("#BBEEBB"),
			[ConsoleLogger.ColorType.Helix] = MakeBrush("#44EEEE"),
			[ConsoleLogger.ColorType.EventSubNotification] = MakeBrush("#4488EE"),
			[ConsoleLogger.ColorType.EventSubConfusion] = MakeBrush("#88BBEE"),
			[ConsoleLogger.ColorType.StreamElementsNotification] = MakeBrush("#44CC99"),
			[ConsoleLogger.ColorType.StreamElementsConfusion] = MakeBrush("#99DDCC"),
			[ConsoleLogger.ColorType.AdNotification] = MakeBrush("#0088EE"),
			[ConsoleLogger.ColorType.Important] = MakeBrush("#EE44EE"),
			[ConsoleLogger.ColorType.SceneChangesImportant] = MakeBrush("#EEBB88"),
			[ConsoleLogger.ColorType.SceneChanges] = MakeBrush("#EE8844"),
			[ConsoleLogger.ColorType.SceneChangesUnimportant] = MakeBrush("#BB8844"),
		};

		static readonly Brush _statusText = MakeBrush("#EEEEEE");
		static readonly Brush _statusHealthy = MakeBrush("#CCCCCC");
		static readonly Brush _statusWarn = MakeBrush("#EEEE44");
		static readonly Brush _statusBad = MakeBrush("#EE8844");
		static readonly Brush _statusDead = MakeBrush("#EE4444");
		static readonly Brush _nameColorDim = MakeBrush("#888888");

		static readonly Dictionary<LeftPanel.LineKind, Brush> _leftPanelColors = new() {
			[LeftPanel.LineKind.Title] = MakeBrush("#CCCCCC"),
			[LeftPanel.LineKind.Heading] = MakeBrush("#888888"),
			[LeftPanel.LineKind.Item] = MakeBrush("#EEEEEE"),
			[LeftPanel.LineKind.Marked] = MakeBrush("#EEEEEE"),
			[LeftPanel.LineKind.On] = MakeBrush("#EE8844"),
			[LeftPanel.LineKind.Current] = MakeBrush("#EE8844"),
		};
		static readonly Brush _leftPanelMissing = MakeBrush("#EE4444");
		static readonly Dictionary<ObsInputs.LineKind, Brush> _obsSourceColors = new() {
			[ObsInputs.LineKind.Title] = MakeBrush("#CCCCCC"),
			[ObsInputs.LineKind.Heading] = MakeBrush("#7FB2E5"),
			[ObsInputs.LineKind.Item] = MakeBrush("#EEEEEE"),
			[ObsInputs.LineKind.Dim] = MakeBrush("#888888"),
			[ObsInputs.LineKind.Failed] = MakeBrush("#EE4444"),
		};
		// Splits column lines
		static readonly Dictionary<SplitList.LineKind, Brush> _splitColors = new() {
			[SplitList.LineKind.Title] = MakeBrush("#CCCCCC"),
			[SplitList.LineKind.Problem] = MakeBrush("#EE4444"),
			[SplitList.LineKind.Header] = MakeBrush("#888888"),
			[SplitList.LineKind.Outer] = MakeBrush("#EEEEEE"),
			[SplitList.LineKind.Inner] = MakeBrush("#888888"),
			[SplitList.LineKind.ScrolledTo] = MakeBrush("#EEEEEE"),
			[SplitList.LineKind.Selected] = MakeBrush("#EE8844"),
			[SplitList.LineKind.SelectedInner] = MakeBrush("#A85F30"),
			[SplitList.LineKind.More] = MakeBrush("#888888"),
		};
		// The marker before a marked splits line's name, the tree guides' colour, and the path's to the current split
		const string SPLIT_MARKER = "> ";
		static readonly Brush _splitGuide = MakeBrush("#555555");
		static readonly Brush _splitPath = MakeBrush("#EE8844");
		// The gap between a splits line's run and segment times, and one level of the dots after it
		const string SPLIT_GAP = "  ";
		const string SPLIT_DOT = "· ";
		// Every other splits row's background
		static readonly Brush _splitRowOdd = MakeBrush("#1C1C1C");
		// LiveSplit's default colours for a completed split's delta
		static readonly Dictionary<SplitList.Highlight, Brush> _splitHighlights = new() {
			[SplitList.Highlight.None] = MakeBrush("#EEEEEE"),
			[SplitList.Highlight.Gold] = MakeBrush("#D8AF1F"),
			[SplitList.Highlight.AheadGained] = MakeBrush("#00CC36"),
			[SplitList.Highlight.AheadLost] = MakeBrush("#52CC73"),
			[SplitList.Highlight.BehindGained] = MakeBrush("#CC5C52"),
			[SplitList.Highlight.BehindLost] = MakeBrush("#CC1200"),
		};
		static readonly Brush _chipEdge = MakeBrush("#666666");
		static readonly Brush _sourceLabel = MakeBrush("#B9D6F2");
		static readonly Brush _sourceLabelDim = MakeBrush("#6F8296");
		double? _charWidth;
		readonly Dictionary<string, Brush> _swatches = new();

		readonly ObservableCollection<LogLine> _lines = new();
		readonly DispatcherTimer _statusTimer;
		ScrollViewer? _logScroller, _splitsScroller;
		List<LeftPanel.StatusLine>? _leftPanelLines;
		List<ObsInputs.StatusLine>? _obsSourceLines;
		// The splits column's last draw, and the list it was of
		List<SplitList.StatusLine>? _splitLines;
		SplitList.ListKind? _splitList;
		// The widest split name (with its tree guides) and time this run, in characters, the widest tree guides, and the
		// deepest level
		int _splitNameChars, _splitCellChars, _splitLeadChars, _splitLevels;
		// The name column's width limit at the last draw, and its width then, in characters
		int _splitNameLimit, _splitNameWidth;
		// The comparison dropdown's entries, and whether the window is setting its selection
		readonly ObservableCollection<ComparisonView> _comparisonViews = new();
		bool _fillingComparisons;
		readonly ObservableCollection<SourceLine> _obsSourceViews = new();
		List<NameColor.StatusPart>? _nameColorParts;
		readonly ObservableCollection<NameColorView> _nameColorViews = new();

		public MainWindow() {
			InitializeComponent();
			LogList.ItemsSource = _lines;
			ObsSourcesList.ItemsSource = _obsSourceViews;
			NameColorList.ItemsSource = _nameColorViews;
			ComparisonBox.ItemsSource = _comparisonViews;
			ListChoiceBox.ItemsSource = _listEntries;
			ListChoiceBox.SelectedItem = _listEntries.First(entry => entry.Kind == SplitList.ListChoice);
			ConsoleLogger.LineLogged += OnLineLogged;

			_statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => UpdateStatus(), Dispatcher);
			UpdateStatus();

			Loaded += (_, _) => {
				_logScroller = FindChild<ScrollViewer>(LogList);
				_splitsScroller = FindChild<ScrollViewer>(SplitsList);
			};
			Closed += (_, _) => {
				ConsoleLogger.LineLogged -= OnLineLogged;
				_statusTimer.Stop();
			};
		}

		static Brush MakeBrush(string hex) {
			SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(hex));
			brush.Freeze();
			return brush;
		}

		static Brush LogBrush(ConsoleLogger.ColorType type) => _logColors.GetValueOrDefault(type, _logColors[ConsoleLogger.ColorType.None]);

		void UpdateStatus() {
			ShowAge(IrcAge, TwitchIRCManager.TimeSinceLastPing, ConnectionHealth.Irc);
			ShowAge(EventSubAge, TwitchEventSub.KeepAliveTimer.Elapsed, ConnectionHealth.EventSub);
			if (StreamElementsSocket.IsEnabled) {
				ShowAge(StreamElementsAge, StreamElementsSocket.SinceLastMessage.Elapsed, ConnectionHealth.StreamElements);
			}
			else {
				StreamElementsAge.Text = "off";
				StreamElementsAge.Foreground = _statusHealthy;
			}
			UpdateNameColors();
			UpdateLeftPanel();
			UpdateObsSources();
			UpdateSplits();
		}

		/// <summary>
		/// Redraws the name colour bar when its parts differ from the last draw: a colour changed, an account's note
		/// changed, or an age reached another second.
		/// </summary>
		void UpdateNameColors() {
			List<NameColor.StatusPart> parts = NameColor.StatusParts(DateTime.UtcNow);
			if (_nameColorParts != null && parts.SequenceEqual(_nameColorParts)) {
				return;
			}
			_nameColorParts = parts;
			Resize(_nameColorViews, parts.Count, () => new NameColorView());
			for (int i = 0; i < parts.Count; i++) {
				Fill(_nameColorViews[i], parts[i], i == 0);
			}
		}

		/// <summary>
		/// Puts an account's part into its view, after a separator unless it's the first; the note red when it's a problem.
		/// </summary>
		void Fill(NameColorView view, NameColor.StatusPart part, bool first) {
			view.Separator = first ? "" : "  ·  ";
			view.Label = part.Label;
			view.Swatch = SwatchBrush(part.Hex);
			view.Text = part.Text;
			view.Age = part.Age;
			view.Note = part.Note;
			view.NoteBrush = part.Bad ? _statusDead : _nameColorDim;
			view.ToolTip = part.ToolTip;
		}

		/// <summary>
		/// Redraws the left-panel column when its lines differ from the last draw: the rotation moved on, or its timer
		/// reached another second.
		/// </summary>
		void UpdateLeftPanel() {
			List<LeftPanel.StatusLine> lines = LeftPanel.StatusLines(LeftPanel.Status, DateTime.UtcNow);
			if (_leftPanelLines != null && lines.SequenceEqual(_leftPanelLines)) {
				return;
			}
			_leftPanelLines = lines;
			LeftPanelList.ItemsSource = lines.Select(line => new LogLine(line.Text, line.Missing ? _leftPanelMissing : _leftPanelColors[line.Kind])).ToList();
		}

		/// <summary>
		/// Redraws the OBS sources list when its lines differ from the last draw: a source was set, OBS connected or
		/// disconnected, or an age reached another second.
		/// </summary>
		void UpdateObsSources() {
			List<ObsInputs.StatusLine> lines = ObsInputs.StatusLines(ObsInputs.Current.Snapshot(), ObsConnection.IsReady, DateTime.UtcNow,
				ObsInputs.Current.Visible(), ObsCharsPerLine(), ObsInputs.Current.Showing());
			if (_obsSourceLines != null && lines.SequenceEqual(_obsSourceLines)) {
				return;
			}
			_obsSourceLines = lines;
			Resize(_obsSourceViews, lines.Count, () => new SourceLine());
			for (int i = 0; i < lines.Count; i++) {
				Fill(_obsSourceViews[i], lines[i]);
			}
		}

		/// <summary>
		/// Redraws the splits column when its lines differ from the last draw: LiveSplit split, scrolled or changed its
		/// times, or the comparison or list changed. The title and the list dropdown go in the bar above the list, as wide
		/// as the name column so the comparison dropdown after them stands over the times. The name and time columns are as
		/// wide as the widest seen this run, the name column at most SplitList.NameWidth characters (or the widest tree
		/// guides and one more), and every row as wide as the deepest level's; a segment time moves two characters right
		/// per level; a completed split's time takes its highlight's colour, any other its line's. A draw of another list
		/// shows the current split. A changed width limit redraws too.
		/// </summary>
		void UpdateSplits() {
			SplitList.Snapshot? snapshot = SplitList.Current;
			ListChoiceBox.Visibility = snapshot != null ? Visibility.Visible : Visibility.Collapsed;
			UpdateComparisons(snapshot);
			List<SplitList.StatusLine> lines = SplitList.StatusLines(snapshot);
			bool newList = snapshot?.List != _splitList;
			_splitList = snapshot?.List;
			int limit = Settings.Data.SplitList.NameWidth;
			if (_splitLines != null && lines.SequenceEqual(_splitLines) && limit == _splitNameLimit) {
				return;
			}
			_splitLines = lines;
			_splitNameLimit = limit;
			foreach (SplitList.StatusLine line in lines) {
				if (line.Run != null && line.Segment != null) {
					int lead = Math.Max(line.Guides.Length, SPLIT_MARKER.Length);
					_splitNameChars = Math.Max(_splitNameChars, lead + line.Text.Length);
					_splitLeadChars = Math.Max(_splitLeadChars, lead);
					_splitCellChars = Math.Max(_splitCellChars, Math.Max(line.Run.Text.Length, line.Segment.Text.Length) + 2);
					_splitLevels = Math.Max(_splitLevels, line.Level);
				}
			}
			_splitNameWidth = Math.Min(_splitNameChars, Math.Max(limit, _splitLeadChars + 1));
			SplitsTitle.Text = lines[0].Text;
			SplitsTitle.Foreground = _splitColors[lines[0].Kind];
			SplitsHead.MinWidth = lines.Count > 1 ? (_splitNameWidth + 1) * CharWidth() : 0;
			int header = lines.FindIndex(line => line.Kind == SplitList.LineKind.Header);
			SplitsList.ItemsSource = lines.Select((line, i) => SplitView(line, header >= 0 && i > header && (i - header) % 2 == 0)).Skip(1).ToList();
			if (newList) {
				ShowCurrentSplit(lines, snapshot?.List == SplitList.ListKind.Completed);
			}
		}

		/// <summary>
		/// Scrolls the splits list, once laid out, to centre the first line holding the current split; without one, to its
		/// end for the completed splits, else to its top. The list scrolls by rows.
		/// </summary>
		void ShowCurrentSplit(List<SplitList.StatusLine> lines, bool completed) {
			int item = lines.FindIndex(line => line.Kind is SplitList.LineKind.Selected or SplitList.LineKind.SelectedInner) - 1;
			Dispatcher.InvokeAsync(() => {
				if (_splitsScroller == null) {
					return;
				}
				if (item >= 0) {
					_splitsScroller.ScrollToVerticalOffset(Math.Max(0, item - _splitsScroller.ViewportHeight / 2));
				}
				else if (completed) {
					_splitsScroller.ScrollToEnd();
				}
				else {
					_splitsScroller.ScrollToTop();
				}
			}, DispatcherPriority.Loaded);
		}

		/// <summary>
		/// Makes the entry picked in the list dropdown the list the column shows.
		/// </summary>
		void ListPicked(object sender, SelectionChangedEventArgs e) {
			if (ListChoiceBox.SelectedItem is ListEntry entry) {
				SplitList.ListChoice = entry.Kind;
			}
		}

		/// <summary>
		/// Fills the comparison dropdown in place from the last read: Cycle (with the comparison it shows while chosen), the
		/// one selected in LiveSplit, then each comparison written; selects the dashboard's choice. Hidden while the column
		/// has no rows.
		/// </summary>
		void UpdateComparisons(SplitList.Snapshot? snapshot) {
			ComparisonBox.Visibility = snapshot?.Rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
			if (snapshot == null) {
				return;
			}
			string shown = snapshot.Shown == SplitList.CURRENT ? snapshot.CurrentName : SplitList.ComparisonName(snapshot.Shown);
			string cycle = SplitList.Choice == SplitList.CYCLE && shown.Length > 0 ? $"Cycle ({shown})" : "Cycle";
			List<(string Label, string? ToolTip, string Choice)> entries = [
				(cycle, $"Changes comparison every {Settings.Data.SplitList.CycleSeconds:0.#} s", SplitList.CYCLE),
				(snapshot.CurrentName.Length > 0 ? $"Current ({snapshot.CurrentName})" : "Current", "The comparison selected in LiveSplit", SplitList.CURRENT),
				.. (snapshot.Comparisons ?? []).Select(folder => (SplitList.ComparisonName(folder), (string?)null, folder)),
			];
			_fillingComparisons = true;
			Resize(_comparisonViews, entries.Count, () => new ComparisonView());
			for (int i = 0; i < entries.Count; i++) {
				_comparisonViews[i].Label = entries[i].Label;
				_comparisonViews[i].ToolTip = entries[i].ToolTip;
				_comparisonViews[i].Choice = entries[i].Choice;
			}
			ComparisonView? chosen = _comparisonViews.FirstOrDefault(view => view.Choice == SplitList.Choice);
			if (ComparisonBox.SelectedItem != chosen) {
				ComparisonBox.SelectedItem = chosen;
			}
			_fillingComparisons = false;
		}

		/// <summary>
		/// Makes the entry picked in the comparison dropdown the dashboard's choice.
		/// </summary>
		void ComparisonPicked(object sender, SelectionChangedEventArgs e) {
			if (!_fillingComparisons && ComparisonBox.SelectedItem is ComparisonView view) {
				SplitList.Choice = view.Choice;
			}
		}

		/// <summary>
		/// A line's row: a title as wide as its text and without times; the root's with its guides and the path over them,
		/// and the root's dot at the left edge, orange when the path reaches it; any other line with its tree guides, the
		/// path to the current split over them, the last two characters in its colour with a marker when it's marked, its
		/// name cut short to fit the name column, in the columns' widths, with a repeated run time grey and its segment time right after a faint dot per level, its cell
		/// wider by the dots of the levels below it down to the deepest; on the darker or the lighter background.
		/// </summary>
		SplitLine SplitView(SplitList.StatusLine line, bool lighter) {
			if (line.Kind == SplitList.LineKind.Root) {
				return new SplitLine(line.Guides, line.Path, "", _splitGuide, "", _splitGuide, double.NaN, SplitCell.None, SplitCell.None, "", Brushes.Transparent, null,
					line.Path.Length > 0 ? _splitPath : _splitGuide);
			}
			Brush brush = _splitColors[line.Kind];
			if (line.Run == null || line.Segment == null) {
				return new SplitLine("", "", "", brush, line.Text, brush, double.NaN, SplitCell.None, SplitCell.None, "", Brushes.Transparent, line.ToolTip);
			}
			string lead = line.Guides.PadRight(SPLIT_MARKER.Length);
			string guides = lead[..^SPLIT_MARKER.Length];
			string path = line.Path[..Math.Min(line.Path.Length, guides.Length)];
			string before = line.Marked ? SPLIT_MARKER : lead[^SPLIT_MARKER.Length..];
			double charWidth = CharWidth();
			double cellWidth = _splitCellChars * charWidth;
			Brush runBrush = line.RepeatsRun ? _splitColors[SplitList.LineKind.Inner] : CellBrush(line.Kind, line.Run);
			return new SplitLine(guides, path, before, line.Marked ? brush : _splitGuide, SplitList.Shorten(line.Text, _splitNameWidth - lead.Length), brush,
				(_splitNameWidth + 1) * charWidth,
				new SplitCell(line.Run.Text, runBrush, cellWidth),
				new SplitCell(line.Segment.Text, CellBrush(line.Kind, line.Segment), cellWidth + (_splitLevels - line.Level) * SPLIT_DOT.Length * charWidth),
				SPLIT_GAP + string.Concat(Enumerable.Repeat(SPLIT_DOT, line.Level)), lighter ? _splitRowOdd : Brushes.Transparent, line.ToolTip);
		}

		/// <summary>
		/// A splits cell's colour: the highlight's for a completed split's time, else its line's.
		/// </summary>
		static Brush CellBrush(SplitList.LineKind kind, SplitList.Cell cell) {
			return cell.Completed ? _splitHighlights[cell.Highlight] : _splitColors[kind];
		}

		/// <summary>
		/// Grows or shrinks the list at its end to the count, keeping the rows already there.
		/// </summary>
		static void Resize<T>(ObservableCollection<T> items, int count, Func<T> make) {
			while (items.Count > count) {
				items.RemoveAt(items.Count - 1);
			}
			while (items.Count < count) {
				items.Add(make());
			}
		}

		/// <summary>
		/// Puts a line into a row: the label in light blue on a plain line, a greyed blue on a dim one, red on a failed one.
		/// </summary>
		void Fill(SourceLine view, ObsInputs.StatusLine line) {
			Brush brush = _obsSourceColors[line.Kind];
			string label = line.Label != null && line.Text.StartsWith(line.Label, StringComparison.Ordinal) ? line.Label : "";
			view.Label = label;
			view.LabelBrush = line.Kind switch {
				ObsInputs.LineKind.Item => _sourceLabel,
				ObsInputs.LineKind.Dim => _sourceLabelDim,
				_ => brush,
			};
			view.Rest = line.Text[label.Length..];
			view.Brush = brush;
			view.ToolTip = line.ToolTip;
			view.Swatch = SwatchBrush(line.Swatch);
			view.After = line.After;
			view.IsRow = line.Chips != null;
			view.IsHeading = line.Kind == ObsInputs.LineKind.Heading;
			IReadOnlyList<ObsInputs.Chip> chips = line.Chips ?? [];
			Resize(view.Chips, chips.Count, () => new ChipView());
			for (int i = 0; i < chips.Count; i++) {
				Fill(view.Chips[i], chips[i]);
			}
		}

		/// <summary>
		/// How many characters of the window's font fit on one line of the OBS sources list; null before it's laid out.
		/// </summary>
		int? ObsCharsPerLine() {
			double available = ObsSourcesList.ActualWidth;
			if (available <= 0) {
				return null;
			}
			return (int)(available / CharWidth());
		}

		/// <summary>
		/// The width of one character of the window's font, measured once.
		/// </summary>
		double CharWidth() {
			_charWidth ??= new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
			return _charWidth.Value;
		}

		/// <summary>
		/// A filter row's swatch: filled with its colour, clear while unset; edge and code red when OBS refused it, the code
		/// grey while unset or while its row's group is hidden.
		/// </summary>
		void Fill(ChipView view, ObsInputs.Chip chip) {
			Brush failed = _obsSourceColors[ObsInputs.LineKind.Failed];
			view.Code = chip.Code;
			view.Fill = SwatchBrush(chip.Hex) ?? Brushes.Transparent;
			view.Edge = chip.Failed ? failed : _chipEdge;
			view.CodeBrush = chip.Failed ? failed : chip.Hex == null || chip.Hidden ? _obsSourceColors[ObsInputs.LineKind.Dim] : _obsSourceColors[ObsInputs.LineKind.Item];
			view.ToolTip = chip.ToolTip;
		}

		/// <summary>
		/// A frozen brush for a "#rrggbb" swatch, made once per colour; null without one.
		/// </summary>
		Brush? SwatchBrush(string? hex) {
			if (hex == null) {
				return null;
			}
			if (!_swatches.TryGetValue(hex, out Brush? brush)) {
				brush = MakeBrush(hex);
				_swatches[hex] = brush;
			}
			return brush;
		}

		/// <summary>
		/// Grey while healthy, yellow past the warn threshold, orange past bad, red past dead.
		/// </summary>
		static void ShowAge(Run run, TimeSpan age, ConnectionHealth.Thresholds thresholds) {
			run.Text = age.ToString(@"mm\:ss");
			run.Foreground = thresholds.Classify(age) switch {
				ConnectionHealth.Band.Dead => _statusDead,
				ConnectionHealth.Band.Bad => _statusBad,
				ConnectionHealth.Band.Warn => _statusWarn,
				_ => _statusHealthy,
			};
		}

		// Raised on whichever thread logged the line.
		void OnLineLogged(ConsoleLogger.ColorType type, string message) {
			if (Dispatcher.HasShutdownStarted) {
				return;
			}
			_ = Dispatcher.InvokeAsync(() => AddLine(new LogLine(message, LogBrush(type))));
		}

		/// <summary>
		/// Appends a line, following the end of the log only while the view is already at the bottom.
		/// </summary>
		void AddLine(LogLine line) {
			bool atBottom = _logScroller == null || _logScroller.VerticalOffset >= _logScroller.ScrollableHeight - 1;
			_lines.Add(line);
			if (_lines.Count > MAX_LINES) {
				_lines.RemoveAt(0);
			}
			if (atBottom) {
				_logScroller?.ScrollToEnd();
			}
		}

		void CopySelected(object sender, ExecutedRoutedEventArgs e) {
			// SelectedItems is in click order; copy in log order instead.
			string text = string.Join(Environment.NewLine, _lines.Where(LogList.SelectedItems.Contains).Select(l => l.Text));
			if (text.Length == 0) {
				return;
			}
			try {
				Clipboard.SetText(text);
			}
			catch (Exception ex) {
				Debug.WriteLine(ex);
			}
		}

		static T? FindChild<T>(DependencyObject parent) where T : DependencyObject {
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child is T match) {
					return match;
				}
				if (FindChild<T>(child) is T nested) {
					return nested;
				}
			}
			return null;
		}
	}
}
