using System.Diagnostics;

namespace StreamAssistant2 {
	public static class ConsoleLogger {

		/// <summary>
		/// Every ColoredLine, timestamped, raised on the caller's thread in call order.
		/// </summary>
		public static event Action<ColorType, string>? LineLogged;

		static string LogDirectory => Path.Combine(Config.Data.Directories.BotOutput, "AssistantLogs");
		static readonly string _logName = DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".log";
		static string LogPath => Path.Combine(LogDirectory, _logName);
		static readonly SemaphoreSlim _semaphore = new(1,1);
		static bool _fileWritesFailing;

		/// <summary>
		/// What kind of event a log line is about; the window picks each one's colour.
		/// </summary>
		public enum ColorType {
			None,
			Error,
			ChatIncoming,
			ChatOutgoing,
			Notification,
			ConnectionNotification,
			Helix,
			EventSubNotification,
			EventSubConfusion,
			AdNotification,
			Important,
			SceneChangesImportant,
			SceneChanges,
			SceneChangesUnimportant,
		}

		public static void ColoredLine(ColorType colorType, object text) {
			if (text == null) {
				return;
			}
			string message = $"[{TimeStamp()}] {text}";
			_ = LogToFileAsync(message);
			RaiseLineLogged(colorType, message);
		}

		public static void Line(object text) {
			ColoredLine(ColorType.None, text);
		}

		/// <summary>
		/// AssistantLogs\EventSubs: one file per real EventSub event, read back by !test replay.
		/// </summary>
		public static string EventSubDirectory => Path.Combine(LogDirectory, "EventSubs");

		public static void LogToEventSubFile(object text, string fileName) {
			_ = LogToEventSubFileAsync(text, fileName);
		}

		static async Task LogToEventSubFileAsync(object text, string fileName) {
			await _semaphore.WaitAsync();

			try {
				Directory.CreateDirectory(EventSubDirectory);
				await File.AppendAllTextAsync(Path.Combine(EventSubDirectory, fileName), text.ToString());
				_fileWritesFailing = false;
			}
			catch (Exception ex) {
				ReportFileWriteFailure(ex);
			}
			finally {
				_semaphore.Release();
			}
		}

		public static void LogToFile(object text, bool addTimestamp = false) {
			string message = addTimestamp ? $"[{TimeStamp()}] {text}" : $"{text}";
			_ = LogToFileAsync(message);
		}

		static async Task LogToFileAsync(object text) {
			await _semaphore.WaitAsync();

			try {
				Directory.CreateDirectory(LogDirectory);
				await File.AppendAllTextAsync(LogPath, text + Environment.NewLine + Environment.NewLine);
				_fileWritesFailing = false;
			}
			catch (Exception ex) {
				ReportFileWriteFailure(ex);
			}
			finally {
				_semaphore.Release();
			}
		}

		/// <summary>
		/// Shows once that log files can't be written (full or missing drive), until a write
		/// succeeds again. Skips the file, since logging it there would fail too.
		/// </summary>
		static void ReportFileWriteFailure(Exception ex) {
			Debug.WriteLine(ex);
			if (_fileWritesFailing) {
				return;
			}
			_fileWritesFailing = true;
			RaiseLineLogged(ColorType.Error, $"[{TimeStamp()}] Error LOG1: can't write log files to {LogDirectory}: {ex.Message}");
		}

		static void RaiseLineLogged(ColorType type, string message) {
			try {
				LineLogged?.Invoke(type, message);
			}
			catch (Exception ex) {
				Debug.WriteLine(ex);
			}
		}

		public static string TimeStamp(bool fileSafe = false) {
			DateTime localNow = DateTime.Now;
			string stamp = localNow.ToString("yyyy'-'MM'-'dd HH:mm:ss.fff");
			if (fileSafe) {
				stamp = stamp.Replace(':','-');
			}
			return stamp;
		}
	}

}
