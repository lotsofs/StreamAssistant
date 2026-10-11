using System.Globalization;

namespace StreamAssistant2 {
	internal static class SplitWatcher {
		const string INDEX_FOLDER = @"CurrentSplit\AllSplits";
		const string INDEX_FILE = "Index.txt";
		const string PREVIOUS_RUN_TIME = @"PreviousSplit\AllSplits\RealTime\RunTime.txt";

		static readonly object _lock = new();
		static FileSystemWatcher? _watcher;
		static int? _last;
		static string _previousRunTime = "";

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
		/// On a change to the index file: when the index moved on by one, has the split checked for a skip off this thread.
		/// A read that fails or isn't a number is ignored.
		/// </summary>
		static void OnChanged(string path) {
			try {
				bool split;
				DateTime indexWritten = default;
				lock (_lock) {
					int? now = Read(path);
					if (now == null) {
						return;
					}
					split = IsSplit(_last, now.Value);
					_last = now;
					if (split) {
						indexWritten = File.GetLastWriteTimeUtc(path);
					}
				}
				if (split) {
					FireForget.Run("LSW4", "split check", () => CheckSplitAsync(indexWritten));
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LSW1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Tells the left panel about a split once LiveSplit has rewritten the previous split's time, unless that time is
		/// empty: a skip. Still a split when the time isn't rewritten within 2 s or can't be read.
		/// </summary>
		static async Task CheckSplitAsync(DateTime indexWritten) {
			for (int wait = 0; wait < 40 && File.GetLastWriteTimeUtc(_previousRunTime) < indexWritten; wait++) {
				await Task.Delay(50);
			}
			if (!IsSkip(ReadText(_previousRunTime))) {
				LeftPanel.OnSplit();
			}
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
		static string? ReadText(string path) {
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
