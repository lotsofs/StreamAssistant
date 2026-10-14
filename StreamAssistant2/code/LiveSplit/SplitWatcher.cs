using System.Globalization;

namespace StreamAssistant2 {
	internal static class SplitWatcher {
		const string INDEX_FOLDER = @"CurrentSplit\AllSplits";
		const string INDEX_FILE = "Index.txt";
		const string PREVIOUS_RUN_TIME = @"PreviousSplit\AllSplits\RealTime\RunTime.txt";
		const string PREVIOUS_SIGN = @"PreviousSplit\AllSplits\RealTime\Sign.txt";

		static readonly object _lock = new();
		static FileSystemWatcher? _watcher;
		static int? _last;
		static string _previousRunTime = "";
		static string _previousSign = "";

		/// <summary>
		/// The split index last read from LiveSplit's file; null before the watcher has read one.
		/// </summary>
		internal static int? CurrentIndex {
			get { lock (_lock) return _last; }
		}

		/// <summary>
		/// Watches LiveSplit's split index under LivesplitGenerated and passes each split on to the left panel; logs and
		/// stays off when the folder isn't set or doesn't exist.
		/// </summary>
		internal static void Start() {
			try {
				if (_watcher != null) {
					return;
				}
				string directory = Config.Data.Directories.LivesplitGenerated;
				if (string.IsNullOrEmpty(directory)) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "LivesplitGenerated not set in paths.json, previous-split panel off");
					return;
				}
				string folder = Path.Combine(directory, INDEX_FOLDER);
				if (!Directory.Exists(folder)) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {folder}, previous-split panel off");
					return;
				}
				string path = Path.Combine(folder, INDEX_FILE);
				_previousRunTime = Path.Combine(directory, PREVIOUS_RUN_TIME);
				_previousSign = Path.Combine(directory, PREVIOUS_SIGN);
				lock (_lock) {
					_last = Read(path);
				}
				_watcher = new FileSystemWatcher(folder, INDEX_FILE) {
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
				};
				_watcher.Changed += (_, _) => OnChanged(path);
				_watcher.Created += (_, _) => OnChanged(path);
				_watcher.Renamed += (_, _) => OnChanged(path);
				_watcher.Error += (_, e) => ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error LSW2: {e.GetException().Message}");
				_watcher.EnableRaisingEvents = true;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, "Watching LiveSplit's split index");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LSW3");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// On a change to the index file: when the index changed, passes it on to the sidebar and has the previous split's
		/// sign handled off this thread, with the split checked for a skip when the index moved on by one. A read that fails
		/// or isn't a number is ignored.
		/// </summary>
		static void OnChanged(string path) {
			try {
				bool split;
				bool moved;
				DateTime indexWritten = default;
				lock (_lock) {
					int? now = Read(path);
					if (now == null) {
						return;
					}
					split = IsSplit(_last, now.Value);
					moved = now != _last;
					_last = now;
					if (moved) {
						indexWritten = File.GetLastWriteTimeUtc(path);
					}
				}
				if (moved) {
					GameSidebar.OnIndexChanged();
					FireForget.Run("LSW4", "index change", () => AfterIndexChangeAsync(indexWritten, split));
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LSW1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Once LiveSplit has rewritten the previous split's sign, written after its times: for a split, tells the left
		/// panel unless the time is empty (a skip), then passes the sign on to the game's colour. Still goes on when the
		/// sign isn't rewritten within 2 s; a time that can't be read still counts as a split.
		/// </summary>
		static async Task AfterIndexChangeAsync(DateTime indexWritten, bool split) {
			for (int wait = 0; wait < 40 && File.GetLastWriteTimeUtc(_previousSign) < indexWritten; wait++) {
				await Task.Delay(50);
			}
			if (split && !IsSkip(ReadText(_previousRunTime))) {
				LeftPanel.OnSplit();
			}
			GameColor.OnSign(ReadSign());
		}

		/// <summary>
		/// LiveSplit's sign for the previous split over all splits, in real time; null before the watcher has started or
		/// when it can't be read.
		/// </summary>
		internal static string? ReadSign() {
			return _previousSign.Length > 0 ? ReadText(_previousSign) : null;
		}

		/// <summary>
		/// The index in the file, retrying while LiveSplit has it locked; null if it can't be read or isn't a number.
		/// </summary>
		static int? Read(string path) {
			return ReadText(path) is string text ? ParseIndex(text) : null;
		}

		/// <summary>
		/// The file's text, retrying while LiveSplit has it locked; null if it can't be read.
		/// </summary>
		internal static string? ReadText(string path) {
			for (int attempt = 0; attempt < 5; attempt++) {
				try {
					return File.ReadAllText(path);
				}
				catch (IOException) {
					Thread.Sleep(20);
				}
			}
			return null;
		}

		/// <summary>
		/// The whole number in the text, or null.
		/// </summary>
		internal static int? ParseIndex(string text) {
			return int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index) ? index : null;
		}

		/// <summary>
		/// Whether going from the last index to this one moved on one split (a split or a skip): up by exactly one, and not
		/// the run's start.
		/// </summary>
		internal static bool IsSplit(int? last, int now) {
			return last != null && now == last + 1 && now >= 1;
		}

		/// <summary>
		/// Whether the previous split's time is empty ("-"), which LiveSplit writes for a skipped split.
		/// </summary>
		internal static bool IsSkip(string? previousRunTime) {
			return previousRunTime?.Trim() == "-";
		}
	}
}
