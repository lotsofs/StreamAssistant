using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class Donations {
		static double DONATION_GOAL_PERCENTAGE = 1f; // Donos go to goal. No bs.
		
		internal static string Process(Dictionary<string, object> variables) {
			double amount = (double)variables["tipAmount"];
			Money.Current += amount * DONATION_GOAL_PERCENTAGE;
			Sound.PlaySound(Sound.Sounds.IndianAnthem);
			string currency = variables["tipCurrency"].ToString() ?? "Money";
			string user = variables["tipUsername"].ToString() ?? "Someone";
			string msg = variables["tipMessage"].ToString() ?? "";
			string read = string.Format("{0} donated {1} {2}: {3}", user, amount, currency, msg);
			MsgQueue.TimedEnqueue(6000, MsgTypes.TextToS, read);
			return read;
		}
	}
}
