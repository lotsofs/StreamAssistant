using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	class SceneManager {
		enum Games { None, SWAT4 };
		enum Files { None, CurrentSplit_Index, PreviousSplit_Sign }

		FileSystemWatcher _watcher;
		FileSystemWatcher _watcherKTANE;
		Timer clock;

		Games _currentGame = Games.None;
		Dictionary<Files, string> oldValues = new Dictionary<Files, string>() {
			{ Files.CurrentSplit_Index, "-2" },
			{ Files.PreviousSplit_Sign, "None" }
		};

		public SceneManager() {
			_watcher = new FileSystemWatcher(@"D:\Files\Stream2022\Text\Livesplit Generated\");
			_watcher.Changed += _watcher_Changed;
			_watcher.EnableRaisingEvents = true;
			CheckCurrentGame();

			_watcherKTANE = new FileSystemWatcher(@"C:\Users\w10-upgrade\AppData\LocalLow\Steel Crate Games\Keep Talking and Nobody Explodes\StreamInfo\");
			_watcherKTANE.Changed += _watcherKTANE_Changed;
			_watcherKTANE.EnableRaisingEvents = true;

			//clock = new Timer();

		}

		//public void WriteTime

		void CheckCurrentGame(string path = @"D:\Files\Stream2022\Text\Livesplit Generated\GameName.txt") {

			string gameName = "";

			int failsafe = 50;
			while (failsafe > 0) {
				try {
					gameName = File.ReadAllLines(path)[0];
					break;
				}
				catch (IOException) {
					failsafe--;
				}
			}
			if (failsafe <= 0) return;
			gameName = gameName.ToLowerInvariant().Trim();

			switch (gameName) {
				case "swat 4":
				case "swat4":
					_currentGame = Games.SWAT4;
					break;
				default:
					_currentGame = Games.None;
					break;
			}
		}

		private void _watcher_Changed(object sender, FileSystemEventArgs e) {
			if (e.Name == "GameName.txt") {
				CheckCurrentGame(e.FullPath);
			} 

			switch (_currentGame) {
				case Games.SWAT4:
					ProcessSwat4(sender, e);
					break;
				default:
					break;
			}

		}

		void ProcessSwat4(object sender, FileSystemEventArgs e) {
			string directory;
			int failsafe = 50;
			switch (e.Name) {
				case "CurrentSplit_Index.txt":
					string indexS = "";
					
					while (failsafe > 0) {
						try {
							indexS = File.ReadAllLines(e.FullPath)[0];
							break;
						}
						catch (IOException) {
							failsafe--;
						}
					}
					if (failsafe <= 0) return;

					if (oldValues[Files.CurrentSplit_Index] == indexS) {
						break;
					}
					oldValues[Files.CurrentSplit_Index] = indexS;

					directory = @"D:\Files\Stream2022\Images\Games\SWAT4";
					string destinationR = Path.Combine(directory, "Dynamic_Right.png");
					string destinationL = Path.Combine(directory, "Dynamic_Left.png");
					string originR;
					string originL;

					if (!int.TryParse(indexS, out int index)) return;
					index += 1;
					if (index > 0 && index <= 20) {
						originR = Path.Combine(directory, string.Format(@"R{0:00}.png", index));
						originL = Path.Combine(directory, string.Format(@"L{0:00}.png", index));
					}
					else {
						originR = Path.Combine(directory, "R00.png");
						originL = Path.Combine(directory, "L00.png");
					}
					File.Copy(originR, destinationR, true);
					File.Copy(originL, destinationL, true);
					File.SetLastWriteTime(destinationR, DateTime.Now);
					File.SetLastWriteTime(destinationL, DateTime.Now);
					break;
				case "PreviousSplit_RealTime_Sign.txt":
					string value = "";

					while (failsafe > 0) {
						try {
							value = File.ReadAllLines(e.FullPath)[0];
							break;
						}
						catch (IOException) {
							failsafe--;
						}
					}
					if (failsafe <= 0) return;

					value = value.ToLowerInvariant().Trim();
					if (oldValues[Files.PreviousSplit_Sign] == value) {
						break;
					}
					oldValues[Files.PreviousSplit_Sign] = value;

					directory = @"D:\Files\Stream2022\Images\Games\SWAT4\Layouts";
					string destination = Path.Combine(directory, "Borders_Dynamic.png");
					string origin;

					Debug.WriteLine(value);

					switch (value) {
						case "ahead":
							origin = Path.Combine(directory, "Borders_Blue.png");
							break;
						case "behind":
							origin = Path.Combine(directory, "Borders_Red.png");
							break;
						case "pb":
						case "gold":
							origin = Path.Combine(directory, "Borders_Gold.png");
							break;
						case "nopb":
						case "undetermined":
						default:
							origin = Path.Combine(directory, "Borders_White.png");
							break;
					}

					File.Copy(origin, destination, true);
					File.SetLastWriteTime(destination, DateTime.Now);
					break;
				default:
					break;
			}
		}

		private void _watcherKTANE_Changed(object sender, FileSystemEventArgs e) {
			int failsafe = 50;
			switch (e.Name) {
				case "strikes.txt":
					int lineCount = 0;
					while (failsafe > 0) {
						try {
							lineCount = File.ReadAllLines(e.FullPath).Length;
							break;
						}
						catch (IOException) {
							failsafe--;
						}
					}
					if (failsafe <= 0) return;
					if (lineCount > 0) {
						string directoryS = @"D:\Files\Stream2022\Images\Games\KTANE\Borders";
						string destinationS = Path.Combine(directoryS, "Borders_Dynamic.png");
						string originS = Path.Combine(directoryS, "Borders_StrikeRed.png");
						File.Copy(originS, destinationS, true);
						File.SetLastWriteTime(destinationS, DateTime.Now);
					}
					break;
				case "solvecount.txt":
					int total = 1;
					int solved = 0;
					while (failsafe > 0) {
						try {
							string[] texts = File.ReadAllLines(e.FullPath);
							if (texts.Length == 0) return;
							string[] words = texts[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
							int.TryParse(words[4].Trim(), out total);
							int.TryParse(words[1].Trim(), out solved);
							break;
						}
						catch (IOException) {
							failsafe--;
						}
					}
					if (failsafe <= 0) return;
					string directoryM = @"D:\Files\Stream2022\Images\Games\KTANE\Borders";
					string destinationM = Path.Combine(directoryM, "Borders_Dynamic.png");
					string originM;
					if (solved >= total) {
						originM = Path.Combine(directoryM, "Borders_SolvedGreen.png");
					}
					else {
						originM = Path.Combine(directoryM, "Borders_UnsolvedGrey.png");
					}
					File.Copy(originM, destinationM, true);
					File.SetLastWriteTime(destinationM, DateTime.Now);
					break;
			}
		}

	}
}
