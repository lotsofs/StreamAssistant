using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class ConsoleLogger {

		static string? _pipeName;
		static NamedPipeServerStream? _pipe;
		static BinaryWriter? _writer;
		static Process? _process;

		static string LogDirectory => Path.Combine(Config.Data.Directories.BotOutput, "AssistantLogs");
		static readonly string _logName = DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".log";
		static string LogPath => Path.Combine(LogDirectory, _logName);
		static readonly SemaphoreSlim _semaphore = new(1,1);
		static readonly SemaphoreSlim _writeLock = new(1,1);
		static bool _fileWritesFailing;

		static readonly TimeSpan VIEWER_CONNECT_TIMEOUT = TimeSpan.FromSeconds(10);
		static readonly TimeSpan VIEWER_RETRY_INTERVAL = TimeSpan.FromSeconds(30);
		static Task<bool>? _connection;
		static DateTime _nextViewerAttempt = DateTime.MinValue;
		static bool _viewerFailing;

		public enum ColorType {
			Error = ConsoleColor.Red,
			ZDR = ConsoleColor.DarkRed,
			ChatIncoming = ConsoleColor.Yellow,
			ChatOutgoing = ConsoleColor.DarkYellow,
			Notification = ConsoleColor.Green,
			ConnectionNotification = ConsoleColor.DarkGreen,
			Helix = ConsoleColor.Cyan,
			EventSubNotification = ConsoleColor.DarkCyan,
			EventSubConfusion = ConsoleColor.Blue,
			AdNotification = ConsoleColor.DarkBlue,
			Important = ConsoleColor.Magenta,
			ZDM = ConsoleColor.DarkMagenta,
			None = ConsoleColor.White,
			ZA = ConsoleColor.Gray,
			ZDA = ConsoleColor.DarkGray,
			ZK = ConsoleColor.Black,
		}

		public static void Start() {
			_connection ??= CreateConnectionAsync();
		}
		
		public static void ColoredLine(ColorType colorType, object text) {
			_ = ColoredLineAsync((ConsoleColor)colorType, text);
		}

		public static void Line(object text) {
			ColoredLine(ColorType.None, text);
		}

		public static void LogToCustomFile(object text, string fileName) {
			_ = LogToCustomFileAsync(text, fileName);
		}

		static async Task LogToCustomFileAsync(object text, string fileName) {
			await _semaphore.WaitAsync();

			try {
				Directory.CreateDirectory(Path.Combine(LogDirectory, "Custom"));
				await File.AppendAllTextAsync(Path.Combine(LogDirectory, "Custom", fileName), text.ToString());
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
		/// Tells the viewer once that log files can't be written (full or missing drive), until a
		/// write succeeds again. Goes straight to the pipe: logging it to file would fail too.
		/// </summary>
		static void ReportFileWriteFailure(Exception ex) {
			Debug.WriteLine(ex);
			if (_fileWritesFailing) {
				return;
			}
			_fileWritesFailing = true;
			_ = WriteToViewerAsync(ConsoleColor.Red, $"[{TimeStamp()}] Error LOG1: can't write log files to {LogDirectory}: {ex.Message}");
		}

		/// <summary>
		/// Starts the viewer and waits for it to connect. On failure (missing exe, no connection
		/// within the timeout) leaves no pipe behind, logs Error LOG2 to the file once per failure
		/// streak, and returns false; the next attempt waits VIEWER_RETRY_INTERVAL.
		/// </summary>
		async static Task<bool> CreateConnectionAsync() {
			DisposePipe();
			_nextViewerAttempt = DateTime.UtcNow + VIEWER_RETRY_INTERVAL;

			_pipeName = $"S.StreamAssistant.{Environment.ProcessId}";

			string executable = Path.Combine(AppContext.BaseDirectory,"logger/StreamAssistantLog.exe");

			try {
				_process = Process.Start(new ProcessStartInfo {
					FileName = executable,
					Arguments = _pipeName,
					UseShellExecute = true
				}) ?? throw new InvalidOperationException("Process.Start returned no process");

				_pipe = new NamedPipeServerStream(
					_pipeName,
					PipeDirection.Out,
					1,
					PipeTransmissionMode.Byte,
					PipeOptions.Asynchronous
				);

				using CancellationTokenSource timeout = new(VIEWER_CONNECT_TIMEOUT);
				await _pipe.WaitForConnectionAsync(timeout.Token);

				_writer = new BinaryWriter(_pipe, Encoding.UTF8, leaveOpen: true);
				_viewerFailing = false;
				return true;
			}
			catch (Exception ex) {
				DisposePipe();
				if (!_viewerFailing) {
					_viewerFailing = true;
					LogToFile($"[{TimeStamp()}] Error LOG2: couldn't start the log viewer ({executable}): {ex.Message}");
				}
				return false;
			}
		}
		
		static async Task ColoredLineAsync(ConsoleColor color, object text) {
			if (text == null) {
				return;
			}

			string message = $"[{TimeStamp()}] {text}";
			// Not awaited: a failing file write must never hold up or kill the viewer line.
			_ = LogToFileAsync(message);
			await WriteToViewerAsync(color, message);
		}

		/// <summary>
		/// Sends one line to the viewer. Waits for a connection attempt in progress; if there is no
		/// viewer and it isn't time to retry, or the retry fails, the line goes to the file only.
		/// </summary>
		static async Task WriteToViewerAsync(ConsoleColor color, string message) {
			await _writeLock.WaitAsync();
			try {
				if (_connection is { IsCompleted: false }) {
					await _connection;
				}
				if (_pipe == null || _writer == null || !_pipe.IsConnected) {
					if (DateTime.UtcNow < _nextViewerAttempt || !await RestartAsync()) {
						return;
					}
				}
				_writer!.Write((byte)color);
				byte[] bytes = Encoding.UTF8.GetBytes(message);
				_writer.Write(bytes.Length);
				_writer.Write(bytes);
				_writer.Flush();
			}
			catch (Exception ex) {
				// The viewer went away mid-write: drop the pipe so the next line reconnects.
				LogToFile(ex);
				DisposePipe();
			}
			finally {
				_writeLock.Release();
			}
		}

		static Task<bool> RestartAsync() {
			try {
				if (_process != null && !_process.HasExited) {
					_process.Kill(true);
				}
			}
			catch (Exception ex) {
				LogToFile(ex);
			}
			_connection = CreateConnectionAsync();
			return _connection;
		}

		static void DisposePipe() {
			try { 
				_writer?.Dispose(); 
			} 
			catch (Exception ex) {
				LogToFile(ex);
			}
			try { 
				_pipe?.Dispose(); 
			} 
			catch (Exception ex) {
				LogToFile(ex);
			}

			_writer = null;
			_pipe = null;
		}

		public static void Dispose() {
			DisposePipe();
			try {
				_process?.Dispose();
			}
			catch (Exception ex) {
				LogToFile(ex);
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
