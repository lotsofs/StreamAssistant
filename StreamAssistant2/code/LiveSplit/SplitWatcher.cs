using System.Globalization;

namespace StreamAssistant2 {
	internal static class SplitWatcher {
		const string INDEX_FOLDER = @"CurrentSplit\AllSplits";
		const string INDEX_FILE = "Index.txt";

		static readonly object _lock = new();
		static FileSystemWatcher? _watcher;
		static int? _last;

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
		/// On a change to the index file: tells the left panel when the index moved on by one split. A read that fails or
		/// isn't a number is ignored.
		/// </summary>
		static void OnChanged(string path) {
			try {
				bool split;
				lock (_lock) {
					int? now = Read(path);
					if (now == null) {
						return;
					}
					split = IsSplit(_last, now.Value);
					_last = now;
				}
				if (split) {
					LeftPanel.OnSplit();
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error LSW1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// The index in the file, retrying while LiveSplit has it locked; null if it can't be read or isn't a number.
		/// </summary>
		static int? Read(string path) {
			for (int attempt = 0; attempt < 5; attempt++) {
				try {
					return ParseIndex(File.ReadAllText(path));
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
		/// Whether going from the last index to this one is a split (or a skip): up by exactly one, and not the run's start.
		/// </summary>
		internal static bool IsSplit(int? last, int now) {
			return last != null && now == last + 1 && now >= 1;
		}
	}
}
