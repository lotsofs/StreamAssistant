using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class Sound {

		internal enum Sounds {
			TribalHymn,
			TheClap,
			Team17Applauds,
			IndianAnthem,
			Flush,
			Warning,
		}

		private const string FOLDER = "D:\\Repositories\\Stream-Resources\\Alert Sounds\\";

		internal static void PlaySound(Sounds s) {
			switch (s) {
				case Sounds.TribalHymn:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.5f, Path.Combine(FOLDER, "Tribal hymn.mp3")));
					break;
				case Sounds.TheClap:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.4f, Path.Combine(FOLDER, "theclap.mp3")));
					break;
				case Sounds.Team17Applauds:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.75f, Path.Combine(FOLDER, "Team17-Applauds.mp3")));
					break;
				case Sounds.IndianAnthem:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.4f, Path.Combine(FOLDER, "IndianAnthem.mp3")));
					break;
				case Sounds.Flush:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.4f, Path.Combine(FOLDER, "Flush.wav")));
					break;
				case Sounds.Warning:
					MsgQueue.Enqueue(MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.6f, Path.Combine(FOLDER, "Warning.wav")));
					break;
				default:
					break;
			}
		}

		internal static void PlaySoundDelayed(Sounds s, int delayInMs) {
			switch (s) {
				case Sounds.TribalHymn:
					MsgQueue.TimedEnqueue(delayInMs, MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.5f, Path.Combine(FOLDER, "Tribal hymn.mp3")));
					break;
				case Sounds.TheClap:
					MsgQueue.TimedEnqueue(delayInMs, MsgTypes.PlaySfx, string.Format("{0}|{1}", 0.4f, Path.Combine(FOLDER, "theclap.mp3")));
					break;
				case Sounds.Team17Applauds:
				case Sounds.IndianAnthem:
				case Sounds.Flush:
				case Sounds.Warning:
				default:
					break;
			}
		}

	}
}
