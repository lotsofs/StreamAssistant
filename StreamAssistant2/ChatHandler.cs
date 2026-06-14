using System.Text.RegularExpressions;

namespace StreamAssistant2 {
	internal static class ChatHandler {

		public class ChatMessage {
			public string Username;
			public string Text;
			public string Raw;
		}

		static ChatCommand[] _commands = [
			new ChatCommand (
				"!civilians",
				"^((?=.*!civilians)|(?=.*(why|how))(?=.*(killing|kill|shoot|shooting))(?=.*(hostages|hostage|civilians|innocents|innocent|civilian))).*$",
				"By equipping a certain gun+ammo type (most FMJ rounds work), a civilian can be shot in the head once, causing them to go down without dying. This is a faster method of securing them than handcuffing."
			),
			new ChatCommand (
				"!ktane",
				"^((?=.*!manual|!ktane)|(?=.*(why|are|what))(?=.*(doing))(?=.*(homework|home work))).*$",
				"The streamer is playing Keep Talking And Nobody Explodes: https://keeptalkinggame.com/ , but with a lot of mods installed: https://ktane.timwi.de/ . His personalized manual is available here: https://lotsofs.github.io/KTANE/Praetorian.html . Co-players are likely twitch.tv/powerslavealfons & Whalien who doesn't stream (Youtube https://www.youtube.com/channel/UC77kb-xOgn0qDfuucbz1H8A)"
			),
			new ChatCommand (
				"!language",
				"^((?=.*(!language|!spanish|!french|!turkish|!italian|!portuguese|!macedonian))|(?=.*(why))(?=.*(spanish|italian|portuguese|french|turkish|macedonian))).*$",
				"The streamer plays games in a different language when he is already familiar with the game's text in Englis in order to learn another language. Where available, this will be Turkish. Otherwise it'll be Spanish."
			),
			new ChatCommand (
				"!skeys",
				"^((?=.*!skeys)|(?=.*(what|how))(?=.*(display|show))(?=.*(press|key|input))).*$",
				"The program used to show the keyboard/mouse inputs is called S Keys 9 and was made by the streamer himself. It can be found at https://github.com/lotsofs/S-Keys-9/releases"
			),
			new ChatCommand (
				"!song",
				"^((?=.*!song)|(?=.*(song))(?=.*(dynasty))).*$",
				"The Song dynasty ([sʊ̂ŋ]; Chinese: 宋朝; pinyin: Sòng cháo; 960–1279) was an imperial dynasty of China that began in 960 and lasted until 1279."
			),
			new ChatCommand (
				"!sssa",
				"^((?=.*!sssa)|(?=.*(what))(?=.*(sssa))).*$",
				"The goal of SSSA is to deliver almost every vehicle in the game to a location. Only exceptions are vehicles that cannot be acquired without the use of glitches. (hotrina, hotrinb, rc vehicles, decorative flying vehicles)"
			),
			//new ChatCommand (
			//	"!wherefrom",
			//	"",
			//	"This is a weird question to be asking a streamer first thing upon entering. Are you looking for someone to stalk? Anyway, the streamer currently lives somewhere in the Netherlands."
			//),
			//new ChatCommand (
			//	"",
			//	"",
			//	""
			//),
			//new ChatCommand (
			//	"",
			//	"",
			//	""
			//),
			//new ChatCommand (
			//	"",
			//	"",
			//	""
			//),
			//new ChatCommand (
			//	"",
			//	"",
			//	""
			//),
			//new ChatCommand (
			//	"",
			//	"",
			//	""
			//),
		];

		static string GetUserName(string prefix) {
			if (string.IsNullOrEmpty(prefix)) {
				return "";
			}

			int colonIndex = prefix.IndexOf(":");
			if (colonIndex == -1) {
				return "";
			}

			int exclIndex = prefix.IndexOf('!');
			if (exclIndex == -1) {
				return "";
			}
			return prefix.Substring(colonIndex+1, exclIndex-colonIndex-1).ToLower();
		}

		public static void ProcessMessage(string raw) {
			try {
				int spaceIndex = raw.IndexOf(' ');
				string tags = raw.StartsWith('@') ? raw.Substring(1, spaceIndex - 1) : "";
				string content = raw.StartsWith('@') ? raw.Substring(spaceIndex + 1) : raw;

				spaceIndex = content.IndexOf(' ');
				string target = content.Substring(0, spaceIndex);
				string message = content.Substring(spaceIndex + 1);
				string username = GetUserName(target);

				spaceIndex = message.IndexOf(' ');
				string messageType = message.Substring(0, spaceIndex);
				string body = message.Substring(spaceIndex + 1);

				switch (messageType) {
					case "USERSTATE":
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ChatIncoming, message);
						break;
					case "PRIVMSG":
						spaceIndex = body.IndexOf(' ');
						string channel = body.Substring(0, spaceIndex);
						string chatContent = body.Substring(spaceIndex + 2);
						HandlePrivMsg(chatContent, username);
						break;
					case "001":
					case "002":
					case "003":
					case "004":
					case "375":
					case "372":
					case "376":
						ConsoleLogger.LogToFile(raw);
						break;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ChatIncoming, raw);
						break;
				}
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error 7");
				ConsoleLogger.LogToFile(ex);
				// TODO: Handle
				return;
			}
		}

		static void HandlePrivMsg(string chatContent, string username) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ChatIncoming, $"{username}: {chatContent}");

			ChatterList.AddChatter(username);
			if (username == "lotsofs" || username == "botsofs") {
				CheckForAdminCommands(chatContent);
			}
			else {
				CheckForCommands(chatContent, username);
			}
		}

		static void CheckForAdminCommands(string text) {
			if (text.StartsWith("!stoppaneltimer")) {
				// MsgQueue.Enqueue(MsgTypes.Termint, "");
			}
			else if (text.StartsWith("!changecolorrandom ")) {
				// Coloring.RandomColor();
			}
			else if (text.StartsWith("!changecolor ")) {
				// Coloring.ChangeColor(inputMsg.Substring(13));
			}
			else if (text.StartsWith("!test ")) {
				string args = text.Substring(6);
				TwitchEventSub.SendTest(args);
			}
		}

		static void CheckForCommands(string text, string username) {
			foreach (ChatCommand cmd in _commands) {
				string outputMsg = "";
				Match match = Regex.Match(text, cmd.RegEx);
				if (match.Success) {
					outputMsg = string.Format("@{0}: {2}", username, cmd.Name, cmd.Output);
					// MsgQueue.Enqueue(MsgTypes.ChatMsg, outputMsg);
				}
			}
		}
	}
}
