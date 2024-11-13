using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class LeftPanel {
		class Scheme {
			public bool UsesSplits = false;
		}

		static string SCENE = "!Scene: Left Panel";

		static int _nextPanelAtMs = 0;
		static int _lockPanelsUntilMs = 0;

		static bool _secondary_splitsList_Value = false;
		static bool _secondary_splitsList_Labels = false;
		static bool _secondary_currentSplit = false;

		static List<string> _currentActiveSources = new List<string>();

		static void CheckTiming(int ms) {
			if (ms < _nextPanelAtMs) {
				return;
			}
		}

		static void HideCurrentPanel() {
			foreach (string source in _currentActiveSources) {
				MsgQueue.Enqueue(MsgTypes.HideSrc, string.Format("{0}|{1}", SCENE, source));
			}
			_currentActiveSources.Clear();
		}

		static void Show_SplitsList_Value() {
			//"_Left Panel: Splits List Values ";
			//"_Left Panel: Splits List Labels ";
			//"_Left Panel: Splits Current ";
			string source = _secondary_splitsList_Value ? "_Left Panel: Splits List Values 1" : "_Left Panel: Splits List Values 2";
			// Set text
			// 

			if (_secondary_splitsList_Value) { }

		}

		static void ChangeScheme(Scheme s) {
			MsgQueue.Enqueue(MsgTypes.HideAll, SCENE);
			if (s.UsesSplits) {
				MsgQueue.Enqueue(MsgTypes.ShowSrc, "_Left Panel: Splits Basics");
			}

		}
	}
}
