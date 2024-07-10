using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal class MsgQueue {

		private class TimedQueueItem {
			internal string Item;
			internal int DelayMs;

			internal TimedQueueItem(string k, int d) {
				Item = k;
				DelayMs = d;
			}
		}

		private static List<TimedQueueItem> _timedQueue = new List<TimedQueueItem>();

		private static Queue<string> _msgQueue = new Queue<string>();

		private static DateTime _prev = DateTime.MinValue;

		internal static void Enqueue(MsgTypes type, string item, bool filterBadWords = true) {
			if (filterBadWords && type == MsgTypes.ChatMsg || type == MsgTypes.TextToS ) {
				item = LanguageFilter.ReplaceBadWords(item);
			}
			
			string m = string.Format("{0}:{1}", type.ToString(), item);
			_msgQueue.Enqueue(m);
		}

		internal static bool TryDequeue(out string item) {
			bool success = _msgQueue.TryDequeue(out string outItem);
			item = outItem;
			return success;
		}

		internal static void TimedEnqueue(int delayInMs, MsgTypes type, string item) {
			string m = string.Format("{0}:{1}", type.ToString(), item);
			_timedQueue.Add(new TimedQueueItem(m, delayInMs));
		}

		internal static void TimedQueueTick() {
			if (_prev == DateTime.MinValue) {
				_prev = DateTime.Now;
				return;
			}
			
			DateTime now = DateTime.Now;
			TimeSpan diff = now - _prev;
			int ms = (int)diff.TotalMilliseconds;
			for (int i = _timedQueue.Count - 1; i >= 0; i--) {
				TimedQueueItem queuedItem = _timedQueue[i];
				queuedItem.DelayMs -= ms;
				if (queuedItem.DelayMs <= 0) {
					_msgQueue.Enqueue(queuedItem.Item);
					_timedQueue.Remove(queuedItem);	
				}
			}
			_prev = now;
		}
	}
}
