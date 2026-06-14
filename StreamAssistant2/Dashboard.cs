using System.Diagnostics;
using System.Text;
using StreamAssistant2;

namespace StreamAssistant2 {
	public static class Dashboard {
		static StringBuilder _input = new();
		
		public static void Start() {
			_ = WriteLoop();
		}

		static async Task WriteLoop() {
			while (true) {
				Console.SetCursorPosition(0, 0);
				Console.BackgroundColor = ConsoleColor.Black;
				Console.ForegroundColor = ConsoleColor.White;

				Console.Write($"Twitch IRC time since last ping: ");

				TimeSpan timeSinceLastPing = TwitchIRCManager.TimeSinceLastPing;
				if (timeSinceLastPing.TotalSeconds > 420) {
					Console.BackgroundColor = ConsoleColor.DarkRed;
				}
				else if (timeSinceLastPing.TotalSeconds > 360) {
					Console.ForegroundColor = ConsoleColor.Red;
				}
				else if (timeSinceLastPing.TotalSeconds > 300) {
					Console.ForegroundColor = ConsoleColor.Yellow;
				}
				else {
					Console.ForegroundColor = ConsoleColor.Gray;
				}
				Console.Write($"{timeSinceLastPing:mm\\:ss}");
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write(" | Event Sub time since last keepalive: ");
				TimeSpan timeSinceLastKeepAlive = TwitchEventSub.KeepAliveTimer.Elapsed;
				if (timeSinceLastKeepAlive.TotalSeconds > 15) {
					Console.BackgroundColor = ConsoleColor.DarkRed;
				}
				else if (timeSinceLastKeepAlive.TotalSeconds > 12) {
					Console.ForegroundColor = ConsoleColor.Red;
				}
				else if (timeSinceLastKeepAlive.TotalSeconds > 10) {
					Console.ForegroundColor = ConsoleColor.Yellow;
				}
				else {
					Console.ForegroundColor = ConsoleColor.Gray;
				}
				Console.Write($"{timeSinceLastKeepAlive:mm\\:ss}");

				Console.BackgroundColor = ConsoleColor.Black;
				Console.ForegroundColor = ConsoleColor.White;

				await Task.Delay(15);
			}
		}
	}
}
