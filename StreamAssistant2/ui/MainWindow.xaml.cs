using System.Collections.ObjectModel;
using System.Diagnostics;
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

		static readonly Dictionary<ConsoleLogger.ColorType, Brush> _logColors = new() {
			[ConsoleLogger.ColorType.None] = MakeBrush("#EEEEEE"),
			[ConsoleLogger.ColorType.Error] = MakeBrush("#EE4444"),
			[ConsoleLogger.ColorType.ChatIncoming] = MakeBrush("#EEEEBB"),
			[ConsoleLogger.ColorType.ChatOutgoing] = MakeBrush("#EEEE88"),
			[ConsoleLogger.ColorType.Notification] = MakeBrush("#44EE44"),
			[ConsoleLogger.ColorType.ConnectionNotification] = MakeBrush("#BBEEBB"),
			[ConsoleLogger.ColorType.Helix] = MakeBrush("#44EEEE"),
			[ConsoleLogger.ColorType.EventSubNotification] = MakeBrush("#4488EE"),
			[ConsoleLogger.ColorType.EventSubConfusion] = MakeBrush("#88BBEE"),
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

		readonly ObservableCollection<LogLine> _lines = new();
		readonly DispatcherTimer _statusTimer;
		ScrollViewer? _logScroller;

		public MainWindow() {
			InitializeComponent();
			LogList.ItemsSource = _lines;
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
