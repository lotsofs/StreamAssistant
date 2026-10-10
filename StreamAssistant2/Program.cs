using System.Windows;

// SAPI text-to-speech, OBS, WPF and drive letters: the bot only runs on Windows.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace StreamAssistant2
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args) {
			try {
				Config.Load();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message, "Stream Assistant", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			Application app = new();
			MainWindow window = new();
			// On the thread pool, so the bot's fire-and-forget loops don't pick up the UI thread's
			// synchronization context and run their continuations on it.
			window.Loaded += (_, _) => Task.Run(StartBotAsync);

			app.Run(window);

			TwitchIRCManager.SendMessage("🍂 Shutting Down");
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "SHUTDOWN!");
			DisableBot();
		}

		static async Task StartBotAsync() {
			try {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "STARTED!");

				Coloring.Load();
				Games.Load();

				await Database.InitAsync();

				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Data loaded");

				DiskSpace.Start();
				TwitchUptime.Start();
				ConnectionHealth.Start();

				EnableBot();
				FireForget.Run("GMS_boot", "boot game setup", Games.OnBootAsync);

				TextToSpeech.ReportStart();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error PRG1: startup failed: {ex.Message}");
				ConsoleLogger.LogToFile(ex);
			}
		}

		private static void EnableBot() {
			ObsConnection.Connect();
			
			TwitchIRCManager.Connect();
			TwitchIRCManager.OnMessage += ChatHandler.ProcessMessage;

			TwitchHelixApi.Init();

			TwitchEventSub.Connect();

			StreamElementsSocket.Connect();

			LayoutColoring.StartWorker();
		}

		private static void DisableBot() {
			ObsConnection.Disconnect();
			
			TwitchIRCManager.Disconnect();

			TwitchEventSub.Disconnect();

			StreamElementsSocket.Disconnect();
		}

	}

}
