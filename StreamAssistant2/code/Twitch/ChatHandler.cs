namespace StreamAssistant2 {
	internal static class ChatHandler {

		static readonly TimeSpan COOLDOWN = TimeSpan.FromSeconds(60);

		static readonly Cooldowns _cooldowns = new(COOLDOWN);

		// FAQ commands, first match wins
		internal static readonly ChatCommand[] _commands = [
			new ChatCommand (
				"!civilians",
				@"^((?=.*!civilians)|(?=.*\b(why|how))(?=.*\b(kill|shoot))(?=.*\b(hostage|civilian|innocent))).*$",
				"By equipping a certain gun+ammo type (most FMJ rounds work), a civilian can be shot in the head once, causing them to go down without dying. This is a faster method of securing them than handcuffing."
			),
			new ChatCommand (
				"!ktane",
				@"^((?=.*(!manual|!ktane))|(?=.*\b(why|are|what))(?=.*\bdoing)(?=.*\b(homework|home work))).*$",
				"The streamer is playing Keep Talking And Nobody Explodes: https://keeptalkinggame.com/ , but with a lot of mods installed: https://ktane.timwi.de/ . His personalized manual is available here: https://lotsofs.github.io/KTANE/Praetorian.html . Co-players are likely twitch.tv/powerslavealfons & Whalien who doesn't stream (Youtube https://www.youtube.com/channel/UC77kb-xOgn0qDfuucbz1H8A)"
			),
			new ChatCommand (
				"!language",
				@"^((?=.*(!language|!spanish|!french|!turkish|!italian|!portuguese|!macedonian))|(?=.*\bwhy)(?=.*\b(spanish|italian|portuguese|french|turkish|macedonian))).*$",
				"The streamer plays games in a different language when he is already familiar with the game's text in English in order to learn another language. Where available, this will be Turkish. Otherwise it'll be Spanish."
			),
			new ChatCommand (
				"!skeys",
				@"^((?=.*!skeys)|(?=.*\b(what|how))(?=.*\b(display|show))(?=.*\b(press|key|input))).*$",
				"The program used to show the keyboard/mouse inputs is called S Keys 9 and was made by the streamer himself. It can be found at https://github.com/lotsofs/S-Keys-9/releases"
			),
			new ChatCommand (
				"!song",
				@"^((?=.*!song)|(?=.*\bsong)(?=.*\bdynasty)).*$",
				"The Song dynasty ([sʊ̂ŋ]; Chinese: 宋朝; pinyin: Sòng cháo; 960–1279) was an imperial dynasty of China that began in 960 and lasted until 1279."
			),
			new ChatCommand (
				"!sssa",
				@"^((?=.*!sssa)|(?=.*\bwhat)(?=.*\bsssa)).*$",
				"The goal of SSSA is to deliver almost every vehicle in the game to a location. Only exceptions are vehicles that cannot be acquired without the use of glitches. (hotrina, hotrinb, rc vehicles, decorative flying vehicles)"
			),
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
					case "JOIN":
					case "PART":
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
			if (IsAdmin(username) && CheckForAdminCommands(chatContent)) {
				return;
			}
			CheckForCommands(chatContent, username);
		}

		// The broadcaster's or the bot account's login
		static bool IsAdmin(string username) {
			return username.Length > 0 && (username == Config.Data.TwitchAuth.Login || username == Config.Data.TwitchBot.Login);
		}

		// Runs the admin command the text starts with; false if it isn't one
		internal static bool CheckForAdminCommands(string text) {
			// "!command the rest" → command and its argument; the argument may be empty.
			int spaceIndex = text.IndexOf(' ');
			string command = spaceIndex < 0 ? text : text[..spaceIndex];
			string argument = spaceIndex < 0 ? "" : text[(spaceIndex + 1)..].Trim();

			switch (command) {
				case "!stoppaneltimer":
					LeftPanel.Stop();
					break;
				case "!startpaneltimer":
					LeftPanel.Resume();
					break;
				case "!nextpanelitem":
					LeftPanel.Next();
					break;
				// The colour commands mirror the three channel-point colour rewards.
				case "!changecolor":
					LayoutColoring.TryChangeToSingle(argument);
					break;
				case "!changecolors":
					LayoutColoring.TryChangeToTriple(argument);
					break;
				case "!changecolorrandom":
					LayoutColoring.ChangeToRandom();
					break;
				case "!test":
					TestEventRunner.Run(argument);
					break;
				case "!train":
					ChannelPoints.TryStartTrain();
					break;
				case "!changegame":
				case "!changecategory":
					Games.TryChangeCategory(argument);
					break;
				case "!changepanel":
					Games.TryChangePanel(argument);
					break;
				case "!changecapture":
					Games.TryChangeCapture(argument);
					break;
				case "!reloaddata":
					DataFiles.Reload();
					break;
				case "!moneygoal":
					MoneyGoal.TrySetGoal(argument);
					break;
				case "!resetmoney":
					MoneyGoal.Reset();
					break;
				default:
					return false;
			}
			return true;
		}

		// Answers the first FAQ command the message matches, unless the user had it within the cooldown
		static void CheckForCommands(string text, string username) {
			ChatCommand? cmd = FindCommand(text);
			if (cmd == null) {
				return;
			}
			if (!_cooldowns.TryUse(username, cmd.Name, DateTime.UtcNow)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"FAQ {cmd.Name} for {username}: on cooldown");
				return;
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"FAQ {cmd.Name} for {username}");
			TwitchIRCManager.SendMessage(Reply(cmd, username));
		}

		// First FAQ command matching the text, or null
		internal static ChatCommand? FindCommand(string text) {
			foreach (ChatCommand cmd in _commands) {
				if (cmd.RegEx.IsMatch(text)) {
					return cmd;
				}
			}
			return null;
		}

		// "@<user> <output>"
		internal static string Reply(ChatCommand cmd, string username) {
			return $"@{username} {cmd.Output}";
		}

		// Last answer per user and command
		internal sealed class Cooldowns {
			readonly TimeSpan _length;
			readonly Dictionary<(string User, string Command), DateTime> _lastUtc = [];
			readonly object _lock = new();

			internal Cooldowns(TimeSpan length) {
				_length = length;
			}

			// Whether the user may have the command now; if so, starts its cooldown
			internal bool TryUse(string user, string command, DateTime nowUtc) {
				lock (_lock) {
					if (_lastUtc.TryGetValue((user, command), out DateTime last) && nowUtc - last < _length) {
						return false;
					}
					_lastUtc[(user, command)] = nowUtc;
					return true;
				}
			}
		}
	}
}
