namespace StreamAssistant2 {
	internal static class FireForget {
		internal static void Run(string code, string what, Func<Task> work) {
			_ = RunAsync(code, what, work);
		}

		static async Task RunAsync(string code, string what, Func<Task> work) {
			try {
				await work();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error {code}: {what} failed: {ex.Message}");
				ConsoleLogger.LogToFile(ex);
			}
		}
	}
}
