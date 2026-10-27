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
		static readonly Brush _chipEdge = MakeBrush("#666666");
		static readonly Brush _sourceLabel = MakeBrush("#B9D6F2");
		static readonly Brush _sourceLabelDim = MakeBrush("#6F8296");
		double? _charWidth;
		readonly Dictionary<string, Brush> _swatches = new();

		readonly ObservableCollection<LogLine> _lines = new();
		readonly DispatcherTimer _statusTimer;
		ScrollViewer? _logScroller;
		List<LeftPanel.StatusLine>? _leftPanelLines;
		List<ObsInputs.StatusLine>? _obsSourceLines;
		readonly ObservableCollection<SourceLine> _obsSourceViews = new();
		List<NameColor.StatusPart>? _nameColorParts;
		readonly ObservableCollection<NameColorView> _nameColorViews = new();

		public MainWindow() {
			InitializeComponent();
			LogList.ItemsSource = _lines;
			ObsSourcesList.ItemsSource = _obsSourceViews;
			NameColorList.ItemsSource = _nameColorViews;
			ConsoleLogger.LineLogged += OnLineLogged;

			_statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => UpdateStatus(), Dispatcher);
			UpdateStatus();

			Loaded += (_, _) => _logScroller = FindChild<ScrollViewer>(LogList);
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
			_charWidth ??= new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
			return (int)(available / _charWidth.Value);
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
