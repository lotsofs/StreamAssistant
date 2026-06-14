using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistantLog
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static async Task Main(string[] args) {
			try {
				Console.Title = "Stream Assistant Log";
				ConsoleHelper.DisableQuickEdit();
				ConsoleHelper.SetIcon();
				Console.CursorVisible = false;

				if (args.Length == 0) {
					Console.WriteLine("No pipe name provided");
					Console.ReadKey();
					return;
				}

				string pipeName = args[0];
				using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);

				await pipe.ConnectAsync();
				// Console.WriteLine("Connected");

				using var reader = new BinaryReader(pipe);

				while (true) {
					ConsoleColor color = ConsoleColor.White;
					try {
						color = (ConsoleColor)reader.ReadByte();
						Console.ForegroundColor = color;
					}
					catch (ArgumentException ex) {
						Console.WriteLine($"INVALID CONSOLE COLOR: {color}");
						Console.WriteLine(ex);
					}

					int length = reader.ReadInt32();
					byte[] bytes = reader.ReadBytes(length);

					string message = Encoding.UTF8.GetString(bytes);
					Console.WriteLine(message);

					if (message.EndsWith("SHUTDOWN!")) {
						break;
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine();
				Console.WriteLine(ex);
			}
			Console.WriteLine();
			Console.WriteLine("Press any key...");
			Console.ReadKey();
		}
	}

	static class ConsoleHelper {
		const int STD_INPUT_HANDLE = -10;

		const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
		const uint ENABLE_EXTENDED_FLAGS = 0x0080;

		const uint WM_SETICON = 0x0080;
		const uint IMAGE_ICON = 1;
		const uint LR_LOADFROMFILE = 0x0010;

		[DllImport("kernel32.dll")]
		static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		static extern IntPtr SendMessage( IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		static extern IntPtr LoadImage( IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);


		public static void DisableQuickEdit() {
			var handle = GetStdHandle(STD_INPUT_HANDLE);

			if (!GetConsoleMode(handle, out uint mode))
				return;

			mode &= ~ENABLE_QUICK_EDIT_MODE;
			mode |= ENABLE_EXTENDED_FLAGS;

			SetConsoleMode(handle, mode);
		}

		public static void SetIcon() {
			// IntPtr hIcon = LoadImage(IntPtr.Zero, "icon.ico", IMAGE_ICON, 0, 0, LR_LOADFROMFILE);

			// if (hIcon == IntPtr.Zero) {
			// 	return;
			// }

			// IntPtr hwnd = GetConsoleWindow();

			// SendMessage(hwnd, WM_SETICON, (IntPtr)0, hIcon);
			// SendMessage(hwnd, WM_SETICON, (IntPtr)1, hIcon);
		}
	}

}
