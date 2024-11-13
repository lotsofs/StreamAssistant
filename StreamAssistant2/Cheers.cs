using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class Cheers {
		static float MONEY_PER_BIT = 0.005f; // Half of earnings.
		
		internal static string Process(Dictionary<string, object> variables) {
			long amount = (long)variables["bits"];
			Money.Current += amount * MONEY_PER_BIT;
			Sound.PlaySound(Sound.Sounds.Team17Applauds);
			string user = variables["user"].ToString() ?? "Someone";
			string msg = variables["message"].ToString() ?? "";
			string read = string.Format("{0} cheers: {1}", user, msg);
			MsgQueue.TimedEnqueue(5000, MsgTypes.TextToS, read);

			return read;
		}
	}
}
