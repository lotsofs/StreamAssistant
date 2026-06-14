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

		const string LOG_DIRECTORY = @"D:\Repositories\Stream-Resources\Bot Data\AssistantLogs\";
		static string _logPath = Path.Combine(LOG_DIRECTORY, DateTime.Now.ToString("yyyy-MM-dd HHmmss")+".log");
		static readonly SemaphoreSlim _semaphore = new(1,1);
		static readonly SemaphoreSlim _writeLock = new(1,1);

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
			ZDB = ConsoleColor.DarkBlue,
			Important = ConsoleColor.Magenta,
			ZDM = ConsoleColor.DarkMagenta,
			None = ConsoleColor.White,
			ZA = ConsoleColor.Gray,
			ZDA = ConsoleColor.DarkGray,
			ZK = ConsoleColor.Black,
		}

		public static void Start() {
			_ = StartAsync();
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
				Directory.CreateDirectory(Path.Combine(LOG_DIRECTORY, "Custom"));
				await File.AppendAllTextAsync(Path.Combine(LOG_DIRECTORY, "Custom", fileName), text.ToString());
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
				Directory.CreateDirectory(LOG_DIRECTORY);
				await File.AppendAllTextAsync(_logPath, text + Environment.NewLine + Environment.NewLine);
			}
			finally {
				_semaphore.Release();
			}
		}

		async static Task StartAsync() {
			await CreateConnectionAsync();
		}

		async static Task CreateConnectionAsync() {
			DisposePipe();
			
			_pipeName = $"S.StreamAssistant.{Environment.ProcessId}";

			string executable = Path.Combine(AppContext.BaseDirectory,"logger/StreamAssistantLog.exe");

			Debug.WriteLine(_process);
			_process = Process.Start(new ProcessStartInfo {
				FileName = executable,
				Arguments = _pipeName,
				UseShellExecute = true
			});

			_pipe = new NamedPipeServerStream(
				_pipeName,
				PipeDirection.Out,
				1,
				PipeTransmissionMode.Byte,
				PipeOptions.Asynchronous
			);

			await _pipe.WaitForConnectionAsync();

			_writer = new BinaryWriter(_pipe, Encoding.UTF8, leaveOpen: true);
		}
		
		static async Task ColoredLineAsync(ConsoleColor color, object text) {
			if (text == null) {
				return;
			}

			string message = $"[{TimeStamp()}] {text}";
			await LogToFileAsync(message);

			await _writeLock.WaitAsync();
			try {
				if (_pipe == null || _writer == null || !_pipe.IsConnected) {
					await RestartAsync();
				}
				_writer.Write((byte)color);
				byte[] bytes = Encoding.UTF8.GetBytes(message);
				_writer.Write(bytes.Length);
				_writer.Write(bytes);
				_writer.Flush();
			}
			catch (Exception ex) {
				LogToFile(ex);
				await RestartAsync();
				
				_writer.Write((byte)color);
				byte[] bytes = Encoding.UTF8.GetBytes(message);
				_writer.Write(bytes.Length);
				_writer.Write(bytes);
				_writer.Flush();
			}
			finally {
				_writeLock.Release();
			}
		}

		static async Task RestartAsync() {
			DisposePipe();


			try {
				if (!_process.HasExited) {
					_process.Kill(true);
				}
			}
			catch (InvalidOperationException ex) {
				LogToFile(ex);
				// Process exited anyway
			}

			await CreateConnectionAsync();
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
