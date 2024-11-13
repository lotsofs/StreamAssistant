using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class Ads {
		internal static string UpcomingAdAlert(Dictionary<string, object> variables) {
			long minutes = (long)variables["minutes"];
			switch (minutes) {
				case 1:
					MsgQueue.Enqueue(MsgTypes.ChatMsg, "Obligatory ad coming right up. Maybe snooze it if something interesting is about to happen.", false);
					break;
				case 2:
					MsgQueue.Enqueue(MsgTypes.ChatMsg, "Obligatory ad in 2 minutes :( :( :(");
					break;
				case 3:
					System.Random random = new System.Random();
					int r = random.Next(3);
					switch (r) {
						case 0:
							MsgQueue.Enqueue(MsgTypes.ChatMsg, "sssDino If you are enjoying the stream, please consider giving a follow. Follows are free and won't be announced on my stream.");
							break;
						case 1:
							MsgQueue.Enqueue(MsgTypes.ChatMsg, "sssDino If you are enjoying the stream, please consider subscribing if you are able to, it helps support the stream and would be greatly appreciated!");
							break;
						case 2:
							MsgQueue.Enqueue(MsgTypes.ChatMsg, "sssDino Did you know that if you're an Amazon Prime member, you get one free Prime Sub? If you are enjoying the stream, it would be greatly appreciated if you used it on my stream.");
							break;
						default:
							break;
					}
					r = random.Next(100);
					if (r == 0) {
						MsgQueue.Enqueue(MsgTypes.ChatMsg, "@LotsOfS Stop making me beg for shit >:(");
					}
					break;
				case 4:
					break;
				case 5:
					MsgQueue.Enqueue(MsgTypes.ChatMsg, "Obligatory ad in 5 minutes :(");
					break;
				default:
					break;
			}
			return minutes.ToString();
		}

	}
}
