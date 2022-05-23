using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamAssistant2 {
	class SceneManager {
		List<Game> _games = new List<Game> {
			new Game(){
				Name = "SWAT4",
				Scenes = new List<Game.Scene> {
					new Game.Scene("SWAT4 - Current Split", 30f),
					new Game.Scene("SWAT4 - List Finished At", 15f),
					new Game.Scene("SWAT4 - List Transition", 2.1f),
					new Game.Scene("SWAT4 - List Big Delta", 15f),
					new Game.Scene("SWAT4 - List Transition", 2.1f),
					new Game.Scene("SWAT4 - List Little Delta", 15f),
					new Game.Scene("SWAT4 - List Transition", 2.1f),
					new Game.Scene("SWAT4 - List Gold Delta", 15f),
					//new Game.Scene("SWAT4 - Previous Split", 10),
					new Game.Scene("SWAT4 - Sellout Goals", 25f),
					new Game.Scene("SWAT4 - Sellout List", 5f),
					new Game.Scene("SWAT4 - Key Counter", 60f),
					
				},
				PreviousSplitScene = new Game.Scene("SWAT4 - Previous Split", 25f)
			},
			new Game(){
				Name = "Unreal Tournament 2004",
				Scenes = new List<Game.Scene> {
					new Game.Scene("UT2004 - Current Split", 30f),
					new Game.Scene("UT2004 - List Finished At", 30f),
					new Game.Scene("UT2004 - Sellout Goals", 25f),
					new Game.Scene("UT2004 - Sellout List", 5f),
					new Game.Scene("UT2004 - Key Counter", 60f),

				},
				PreviousSplitScene = new Game.Scene("UT2004 - Previous Split", 20f)
			},
				new Game(){
				Name = "Age of Empires 2: Definitive Edition",
				Scenes = new List<Game.Scene> {
					new Game.Scene("AoE2DE - Current Split", 30f),
					new Game.Scene("AoE2DE - List Finished At", 15f),
					new Game.Scene("AoE2DE - List Transition", 2.1f),
					new Game.Scene("AoE2DE - List Big Delta", 15f),
					new Game.Scene("AoE2DE - List Transition", 2.1f),
					new Game.Scene("AoE2DE - List Little Delta", 15f),
					new Game.Scene("AoE2DE - List Transition", 2.1f),
					new Game.Scene("AoE2DE - List Gold Delta", 15f),
					new Game.Scene("AoE2DE - Sellout Goals", 25f),
					new Game.Scene("AoE2DE - Sellout List", 5f),
					new Game.Scene("AoE2DE - Key Counter", 60f),

				},
				PreviousSplitScene = new Game.Scene("AoE2DE - Previous Split", 45f)
			},
			new Game(){
				Name = "fonv",
				Scenes = new List<Game.Scene> {
					new Game.Scene("FONV - Sellout Goal", 30f),
					new Game.Scene("FONV - Key Counter", 60f),
				},
				PreviousSplitScene = new Game.Scene("AoE2DE - Previous Split", 45f)
			},
		};

		enum Files { None, CurrentSplit_Index, PreviousSplit_Sign }

		const string ASSISTANT_GENERATED_DIRECTORY = @"D:\Files\Stream2022\Text\Assistant Generated";

		bool _enabled = true;

		string _overrideGame = "";
		public string OverrideGame {
			get { return _overrideGame; }
			set {
				_overrideGame = value.Trim().ToLowerInvariant();
				CheckCurrentGame(null);
				if (_currentGame == null) {
					File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TargetScene.txt"), "");
					return;
				}
			}
		}

		public bool Enabled {
			get { return _enabled; }
			set {
				Enable(value);
			}
		}

		int _previousIndex = -1;

		FileSystemWatcher _watcher;
		FileSystemWatcher _watcherKTANE;
		Timer _clock;
		string _time;

		int _elapsedTime;
		int _elapsedScenes;
		float _targetTime;
		bool _goToPreviousScene = false;

		Game _currentGame = null;
		Dictionary<Files, string> oldValues = new Dictionary<Files, string>() {
			{ Files.CurrentSplit_Index, "-2" },
			{ Files.PreviousSplit_Sign, "None" }
		};

		public SceneManager() {
			LoadSettings();
			
			_watcher = new FileSystemWatcher(@"D:\Files\Stream2022\Text\Livesplit Generated\");
			_watcher.Changed += _watcher_Changed;
			_watcher.EnableRaisingEvents = false;

			_watcherKTANE = new FileSystemWatcher(@"C:\Users\w10-upgrade\AppData\LocalLow\Steel Crate Games\Keep Talking and Nobody Explodes\StreamInfo\");
			_watcherKTANE.Changed += _watcherKTANE_Changed;
			_watcherKTANE.EnableRaisingEvents = false;

			_clock = new Timer(1000);
			_clock.Elapsed += TimerTick;
			_clock.AutoReset = true;
			_clock.Enabled = true;

			System.Windows.Forms.Application.ApplicationExit += new EventHandler(Shutdown);
		}

		/// <summary>
		/// Toggle on/off
		/// </summary>
		/// <param name="enabled"></param>
		public void Enable(bool enabled) {
			_enabled = enabled; 
			_watcher.EnableRaisingEvents = enabled;
			_watcherKTANE.EnableRaisingEvents = enabled;
			if (enabled) {
				CheckCurrentGame();
			}
			else {
				string directory = @"D:\Files\Stream2022\Text\Assistant Generated";
				File.WriteAllText(Path.Combine(directory, "TargetScene.txt"), string.Empty);
			}
		}

		/// <summary>
		/// When closing program reset the file
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Shutdown(object sender, EventArgs e) {
			string directory = @"D:\Files\Stream2022\Text\Assistant Generated"; 
			File.WriteAllText(Path.Combine(directory, "TargetScene.txt"), string.Empty);
		}

		public void Suspend(int duration) {
			_elapsedTime = 0;
			File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TargetScene.txt"), "");
			_goToPreviousScene = false;
			_targetTime = duration;
		}

		#region save load

		public void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.Save();
		}

		public void LoadSettings() {
			SaveLoad.OnSave += SaveSettings;
			System.Collections.Specialized.NameValueCollection appSettings = ConfigurationManager.AppSettings;
		}

		#endregion


		void TimerTick(Object source, ElapsedEventArgs e) {
			// time of day // TODO: Make this separate
			string newTime = string.Format("{0}", DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("de-DE")));
			if (newTime != _time) {
				_time = newTime;
				File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TimeOfDay.txt"), newTime);
			}

			if (!_enabled) return;

			// change scenes in a timely fashion
			_elapsedTime++;
			if (_currentGame == null) return;

			if (_goToPreviousScene && _elapsedTime >= 2.1f) {
				_elapsedTime = 0;
				File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TargetScene.txt"), _currentGame.PreviousSplitScene.Name);
				_goToPreviousScene = false;
				_targetTime = _currentGame.PreviousSplitScene.Duration;
			}

			if (_elapsedTime >= _targetTime) {
				_elapsedTime = 0;
				_elapsedScenes++;
				_elapsedScenes %= _currentGame.Scenes.Count;
				File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TargetScene.txt"), _currentGame.Scenes[_elapsedScenes].Name);
				// transition scene
				_targetTime = _currentGame.Scenes[_elapsedScenes].Duration;
			}
		}

		void CheckCurrentGame(string path = @"D:\Files\Stream2022\Text\Livesplit Generated\GameName.txt") {
			string gameName = "";

			if (!string.IsNullOrEmpty(_overrideGame)) {
				_currentGame = _games.Find(x => x.Name == _overrideGame);
				return;
			}

			if (string.IsNullOrEmpty(path)) {
				_currentGame = null;
				return;
			}
			int failsafe = 50;
			while (failsafe > 0) {
				try {
					gameName = File.ReadAllLines(path)[0];
					break;
				}
				catch (IOException) {
					failsafe--;
				}
				catch (IndexOutOfRangeException) {
					// no game name set in splits file
					_currentGame = null;
					break;
				}
			}
			if (failsafe <= 0) return;
			gameName = gameName.Trim();
			string gameNameLower = gameName.ToLowerInvariant();

			switch (gameNameLower) {
				case "swat 4":
				case "swat4":
					_currentGame = _games.Find(x => x.Name == "SWAT4");
					break;
				default:
					_currentGame = _games.Find(x => x.Name == gameName);
					break;
			}
		}

		private void _watcher_Changed(object sender, FileSystemEventArgs e) {
			if (e.Name == "GameName.txt") {
				CheckCurrentGame(e.FullPath);
			}

			if (_currentGame == null) {
				File.WriteAllText(Path.Combine(ASSISTANT_GENERATED_DIRECTORY, "TargetScene.txt"), "");
				return;
			}
			switch (_currentGame.Name) {
				case "SWAT4":
					ProcessSwat4(sender, e);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Do an action based on SWAT4 livesplit text output files that have changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void ProcessSwat4(object sender, FileSystemEventArgs e) {
			string directory;
			int failsafe = 50;
			switch (e.Name) {
				case "CurrentSplit_Index.txt":
					// The Current Split Changed
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

					// Check if the scene áctually changed
					if (oldValues[Files.CurrentSplit_Index] == indexS) {
						break;
					}
					oldValues[Files.CurrentSplit_Index] = indexS;

					// Tell OBS to show the 'previous split' scene if our split count went up
					if (!int.TryParse(indexS, out int index)) return;
					index += 1;
					if (index > 1 && index == _previousIndex + 1) {
						_goToPreviousScene = true;
					}
					_previousIndex = index;
					
					// Change various layout imagery
					directory = @"D:\Files\Stream2022\Images\Games\SWAT4";
					string destinationR = Path.Combine(directory, "Dynamic_Right.png");
					string destinationL = Path.Combine(directory, "Dynamic_Left.png");
					string originR;
					string originL;

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
					// change the color of the layout borders
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
