﻿using System;
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
				case Sounds.IndianAnthem:
				case Sounds.Flush:
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
				default:
					break;
			}
		}

	}
}
