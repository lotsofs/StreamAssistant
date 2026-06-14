using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace StreamAssistant2
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static async Task Main(string[] args) {
			Console.Title = "Stream Assistant";
			Console.CursorVisible = false;

			ConsoleHelper.DisableQuickEdit();
			ConsoleHelper.SetIcon();

			Config.Load();

			TaskCompletionSource shutdownTcs = new TaskCompletionSource();

			Console.CancelKeyPress += (_, e) => {
				e.Cancel = true;
				shutdownTcs.TrySetResult();
			};

			AppDomain.CurrentDomain.ProcessExit += (_, _) => {
				shutdownTcs.TrySetResult();
			};

			ConsoleLogger.Start();
			Dashboard.Start();
			await Task.Delay(1000);

			await Database.InitAsync();

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "STARTED!");

			Clock.Start();
			Clock.AddGenericJobs();

			EnableBot();

			TextToSpeech.ReportStart();

			await shutdownTcs.Task;

			TwitchIRCManager.SendMessage("🍂 Shutting Down");
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "SHUTDOWN!");
			DisableBot();
			ConsoleLogger.Dispose();
		}

		private static void EnableBot() {
			ObsConnection.Connect();
			
			TwitchIRCManager.Connect();
			TwitchIRCManager.OnMessage += ChatHandler.ProcessMessage;

			TwitchHelixApi.Init();

			TwitchEventSub.Connect();
		}

		private static void DisableBot() {
			ObsConnection.Disconnect();
			
			TwitchIRCManager.Disconnect();

			TwitchEventSub.Disconnect();
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
			IntPtr hIcon = LoadImage(IntPtr.Zero, "icon.ico", IMAGE_ICON, 0, 0, LR_LOADFROMFILE);

			if (hIcon == IntPtr.Zero) {
				return;
			}

			IntPtr hwnd = GetConsoleWindow();

			SendMessage(hwnd, WM_SETICON, (IntPtr)0, hIcon);
			SendMessage(hwnd, WM_SETICON, (IntPtr)1, hIcon);
		}
	}

}
