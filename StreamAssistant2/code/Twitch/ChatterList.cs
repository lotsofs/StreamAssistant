using System.Diagnostics;

namespace StreamAssistant2 {
	public static class ChatterList {
		static List<string> _list = new List<string>();

		internal static void Reset() {
			// TODO: Send chat msg
			_list.Clear();
		}

		internal static void AddChatter(string name) {
			if (_list.Contains(name)) {
				return;
			}
			_list.Add(name);
			TwitchIRCManager.SendMessage(string.Format("Test {0}", _list.Count));
		}
	}
}
