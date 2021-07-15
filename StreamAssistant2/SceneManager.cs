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
		Timer clock;

		Games _currentGame = Games.None;
		Dictionary<Files, string> oldValues = new Dictionary<Files, string>() {
			{ Files.CurrentSplit_Index, "-2" },
			{ Files.PreviousSplit_Sign, "None" }
		};

		public SceneManager() {
			_watcher = new FileSystemWatcher(@"D:\Files\Stream2021\Text\Livesplit Generated\");
			_watcher.Changed += _watcher_Changed;
			_watcher.EnableRaisingEvents = true;
			CheckCurrentGame();

			//clock = new Timer();

		}

		//public void WriteTime

		void CheckCurrentGame(string path = @"D:\Files\Stream2021\Text\Livesplit Generated\GameName.txt") {

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

					directory = @"D:\Files\Stream2021\Stream Images\SWAT4";
					string destinationR = Path.Combine(directory, "Dynamic_Right.png");
					string destinationL = Path.Combine(directory, "Dynamic_Left.png");
					string originR;
					string originL;

					if (!int.TryParse(indexS, out int index)) return;
					index += 1;
					if (index > 0 && index <= 13) {
						originR = Path.Combine(directory, string.Format("Level{0:00}.png", index));
						originL = Path.Combine(directory, string.Format("L{0:00}.png", index));
					}
					else {
						originR = Path.Combine(directory, "Level00.png");
						originL = Path.Combine(directory, "L00.png");
					}
					File.Copy(originR, destinationR, true);
					File.Copy(originL, destinationL, true);
					File.SetLastWriteTime(destinationR, DateTime.Now);
					File.SetLastWriteTime(destinationL, DateTime.Now);
					break;
				case "PreviousSplit_Sign.txt":
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

					directory = @"D:\Files\Stream2021\Stream Layouts\SWAT4";
					string destination = Path.Combine(directory, "Borders_Dynamic.png");
					string origin;

					switch (value) {
						case "ahead":
							origin = Path.Combine(directory, "Borders_Blue.png");
							break;
						case "behind":
							origin = Path.Combine(directory, "Borders_Red.png");
							break;
						case "pb":
							origin = Path.Combine(directory, "Borders_Gold.png");
							break;
						case "nopb":
						case "undetermined":
						default:
							origin = Path.Combine(directory, "Borders_White.png");
							break;
							break;
					}

					File.Copy(origin, destination, true);
					File.SetLastWriteTime(destination, DateTime.Now);
					break;
				default:
					break;
			}
		}
	}
}
