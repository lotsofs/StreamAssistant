using System.Globalization;

namespace StreamAssistant2 {
	internal static class SplitWatcher {
		const string INDEX_FOLDER = @"CurrentSplit\AllSplits";
		const string NUMBER_FILE = "Number.txt";
		const string COMPLETED_FILE = "Completed.txt";
		const string PREVIOUS_RUN_TIME = @"PreviousSplit\AllSplits\RealTime\RunTime.txt";
		const string PREVIOUS_SIGN = @"PreviousSplit\AllSplits\RealTime\CurrentComparison\Sign.txt";

		static readonly object _lock = new();
		static FileSystemWatcher? _watcher;
		static int? _last;
		static string _previousRunTime = "";
		static string _previousSign = "";

		/// <summary>
		/// The split index last read from LiveSplit's files; null before the watcher has read one.
		/// </summary>
		internal static int? CurrentIndex {
			get { lock (_lock) return _last; }
		}

		/// <summary>
		/// Watches LiveSplit's current split number and completed count under LivesplitGenerated and passes each split on
		/// to the left panel; logs and stays off when the folder isn't set or doesn't exist.
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
				string number = Path.Combine(folder, NUMBER_FILE);
				string completed = Path.Combine(folder, COMPLETED_FILE);
				_previousRunTime = Path.Combine(directory, PREVIOUS_RUN_TIME);
				_previousSign = Path.Combine(directory, PREVIOUS_SIGN);
				lock (_lock) {
					_last = Read(number, completed);
				}
				_watcher = new FileSystemWatcher(folder) {
					Filters = { NUMBER_FILE, COMPLETED_FILE },
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
				};
				_watcher.Changed += (_, _) => OnChanged(number, completed);
				_watcher.Created += (_, _) => OnChanged(number, completed);
				_watcher.Renamed += (_, _) => OnChanged(number, completed);
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
		/// On a change to the number or completed file: when the index changed, passes it on to the sidebar and has the
		/// previous split's sign handled off this thread, with the split checked for a skip when the index moved on by one.
		/// A read that fails or isn't a number is ignored.
		/// </summary>
		static void OnChanged(string number, string completed) {
			try {
				bool split;
				bool moved;
				DateTime indexWritten = default;
				lock (_lock) {
					int? now = Read(number, completed);
					if (now == null) {
						return;
					}
					split = IsSplit(_last, now.Value);
					moved = now != _last;
					_last = now;
					if (moved) {
						DateTime numberWritten = File.GetLastWriteTimeUtc(number);
						DateTime completedWritten = File.GetLastWriteTimeUtc(completed);
						indexWritten = numberWritten > completedWritten ? numberWritten : completedWritten;
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
		/// Once LiveSplit has rewritten the previous split's sign, written after the index files and its times: for a split,
		/// tells the left panel unless the time is empty (a skip), then passes the sign on to the game's colour. Still goes on
		/// when the sign isn't rewritten within 0.5 s; a time that can't be read still counts as a split.
		/// </summary>
		static async Task AfterIndexChangeAsync(DateTime indexWritten, bool split) {
			for (int wait = 0; wait < 10 && File.GetLastWriteTimeUtc(_previousSign) < indexWritten; wait++) {
				await Task.Delay(50);
			}
			if (split && !IsSkip(ReadText(_previousRunTime))) {
				LeftPanel.OnSplit();
			}
			GameColor.OnSign(ReadSign());
		}

		/// <summary>
		/// LiveSplit's sign for the previous split over all splits, in real time against the current comparison; null before
		/// the watcher has started or when it can't be read.
		/// </summary>
		internal static string? ReadSign() {
			return _previousSign.Length > 0 ? ReadText(_previousSign) : null;
		}

		/// <summary>
		/// The split index from the number and completed files, retrying while LiveSplit has them locked; null if they can't
		/// be read or hold no index.
		/// </summary>
		static int? Read(string number, string completed) {
			return Index(ReadText(number), ReadText(completed));
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
		/// The 0-based split index from LiveSplit's current split number and completed count: the number - 1 during a run;
		/// with no number ("-"), the count after the finish, else -1 before a run. Null for any other number text, or a
		/// count that isn't a number when it's needed.
		/// </summary>
		internal static int? Index(string? number, string? completed) {
			if (number == null) {
				return null;
			}
			if (ParseIndex(number) is int current) {
				return current - 1;
			}
			if (number.Trim() != "-") {
				return null;
			}
			return completed != null && ParseIndex(completed) is int done ? (done > 0 ? done : -1) : null;
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
