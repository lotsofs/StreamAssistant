namespace StreamAssistant2 {
	internal static class DataFiles {
		static readonly object _reloadLock = new();

		/// <summary>
		/// "!reloaddata": reloads the colour data, games.json and leftpanel.json off the caller's thread, one reload at a
		/// time, then runs the current category's game setup; with no category yet, restarts the left-panel rotation instead.
		/// </summary>
		internal static void Reload() {
			FireForget.Run("DAT1", "data reload", async () => {
				await Task.Yield();
				lock (_reloadLock) {
					Coloring.Load();
					Games.Load();
					LeftPanel.Load();
					if (Games.CategoryId is string id) {
						Games.Apply(id);
					}
					else {
						LeftPanel.Restart();
					}
				}
			});
		}
	}
}
