using Streamer.bot.Plugin.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class ChatMessages {

		class ChatCommand {
			internal string RegEx;
			internal string Output;
			internal string Name;

			internal ChatCommand(string name, string regEx, string output) {
				RegEx = regEx;
				Output = output;
				Name = name;
			}
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

		public static List<string> ChatterList = new List<string>();

		public static void AddChatterToList(string chatter) {
			if (ChatterList.Contains(chatter)) {
				return;
			}
			ChatterList.Add(chatter);
			MsgQueue.Enqueue(MsgTypes.ChatMsg, "Test " + ChatterList.Count);
			if (chatter.ToLower() == "lotsofs") {
				MsgQueue.Enqueue(MsgTypes.ChatMsg, "YOOO BRO");
				return;
			}
		}

		internal static void Process(Dictionary<string, object> variables) {
			string chatter = variables["userName"].ToString() ?? "Test User";
			AddChatterToList(chatter);
			
			if (chatter.ToLower() == "botsofs") {
				return;
			}

			CheckCommand(variables);
		}

		internal static void CheckCommand(Dictionary<string, object> variables) {
			string inputMsg = variables["message"].ToString() ?? string.Empty;
			inputMsg = inputMsg.ToLowerInvariant();
			foreach (ChatCommand cmd in _commands) {
				string outputMsg = "";
				Match match = Regex.Match(inputMsg, cmd.RegEx);
				string user = variables["userName"].ToString() ?? "Someone";
				if (match.Success) {
					outputMsg = string.Format("{0} used {1}: {2}", user, cmd.Name, cmd.Output);
				}
				MsgQueue.Enqueue(MsgTypes.ChatMsg, outputMsg);
			}
		}
		// Regex: ^((?=.*!song)|(?=.*(song))(?=.*(dynasty))).*$
	}
}
