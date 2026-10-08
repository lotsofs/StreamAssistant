namespace StreamAssistant2 {
	public static class ChatterList {
		static readonly HashSet<string> _set = new();
		static readonly object _lock = new();

		/// <summary>
		/// Forgets every chatter and logs how many there were.
		/// </summary>
		internal static void Reset() {
			int count;
			lock (_lock) {
				count = _set.Count;
				_set.Clear();
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Chatter list reset @ {count}");
		}

		/// <summary>
		/// Records a chatter and posts the new count, greeting the streamer; returns false if they were already listed.
		/// </summary>
		internal static bool AddChatter(string name) {
			int count;
			lock (_lock) {
				if (!_set.Add(name)) {
					return false;
				}
				count = _set.Count;
			}
			TwitchIRCManager.SendMessage(string.Format("Test {0}", count));
			if (name == "lotsofs") {
				TwitchIRCManager.SendMessage("YOOO BRO");
			}
			return true;
		}
	}
}
