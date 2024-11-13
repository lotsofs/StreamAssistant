using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StreamAssistant2 {
	internal static class Games {

		public class Game {
			public string? Name;
			public List<string>? Executables;
		}

		const string GAMES_FILE_PATH = @"D:\\Repositories\\Stream-Resources\\Input\\Streamerbot\\games.json";
		const string BACKGROUNDS_FILE_PATH = @"D:\\Repositories\\Stream-Resources\\Images\\Backgrounds\\";

		public static Game CurrentGame;

		private static Dictionary<string, Game> _games = new Dictionary<string, Game>();

		internal static void ReadJson() {
			string jsonText = File.ReadAllText(GAMES_FILE_PATH);
			_games = JsonConvert.DeserializeObject<Dictionary<string, Game>>(jsonText) ?? new Dictionary<string, Game>();
		}

		private static void ChangeAudio(Game g) {
			for (int i = 0; i <= 4; i++) {
				Obs.Sources s = Obs.Sources.None;
				switch (i) {
					case 0: s = Obs.Sources.Audio_Game0; break;
					case 1: s = Obs.Sources.Audio_Game1; break;
					case 2: s = Obs.Sources.Audio_Game2; break;
					case 3: s = Obs.Sources.Audio_Game3; break;
					case 4: s = Obs.Sources.Audio_Game4; break;
				}
				if (g.Executables == null || i >= g.Executables.Count) {
					Obs.ChangeAudioSourceWindow(s, "none");
				}
				else {
					Obs.ChangeAudioSourceWindow(s, g.Executables[i]);
				}
			}
		}

		static void ApplyCategoryChanges(string gameId) {
			// Get info on our game from JSON files etc etc
			Game game = _games["0"];
			if (_games.ContainsKey(gameId)) {
				game = _games[gameId];
			}
			CurrentGame = game;
			// Do layout changes
			Obs.ChangeImageSourceFile(Obs.Sources.Image_Background, Path.Combine(BACKGROUNDS_FILE_PATH, game.Name + ".png"));
			Coloring.ChangeColor("gameschemes[" + game.Name + "]", false);
			Debug.WriteLine("gameschemes[" + game.Name + "]");
			// Audio sources
			ChangeAudio(game);
		}

		internal static string ProcessChangeEvent(Dictionary<string, object> variables) {
			// Get the game ID of the change. Report.
			string gameId = variables["gameId"].ToString() ?? "0";
			string gameIdOld = variables["oldGameId"].ToString() ?? "0";
			MsgQueue.Enqueue(MsgTypes.ChatMsg, "Stream category change to " + gameId);
			MsgQueue.Enqueue(MsgTypes.ChatMsg, "From " + gameIdOld);
			ApplyCategoryChanges(gameId);
			return gameId + " <- " + gameIdOld;
		}

		internal static string ProcessGoLiveEvent(Dictionary<string, object> variables) {
			// Get the game ID of the change. Report.
			string gameId = variables["gameId"].ToString() ?? "0";
			MsgQueue.Enqueue(MsgTypes.ChatMsg, "Stream live with category " + gameId);
			ApplyCategoryChanges(gameId);
			return gameId;
		}

	}
}
