namespace StreamAssistant2 {
	// Changes the broadcaster's and the bot account's chat name colours on a timer, while the bot runs
	internal static class NameColor {
		internal const string SCOPE = "user:manage:chat_color";

		// The colours any account may set, with their hex; a hex colour needs Turbo or Prime
		internal static readonly (string Name, string Hex)[] Named = [
			("blue", "#0000ff"), ("blue_violet", "#8a2be2"), ("cadet_blue", "#5f9ea0"), ("chocolate", "#d2691e"), ("coral", "#ff7f50"),
			("dodger_blue", "#1e90ff"), ("firebrick", "#b22222"), ("golden_rod", "#daa520"), ("green", "#008000"), ("hot_pink", "#ff69b4"),
			("orange_red", "#ff4500"), ("red", "#ff0000"), ("sea_green", "#2e8b57"), ("spring_green", "#00ff7f"), ("yellow_green", "#9acd32"),
		];

		// A colour to send, and its name, lowercase hex and source for the dashboard
		internal sealed record Choice(string Color, string Name, string Hex, string Source);

		// Why an account's colour isn't changing, if it isn't
		internal enum Note { None, Waiting, NoToken, Failed, Off }

		// An account's last colour and when it was set, and its note with the reason
		internal sealed record Status(string Login, Choice? Color, DateTime? SetAt, Note Note, string? Reason);

		// One account's part of the dashboard bar
		internal sealed record StatusPart(string Label, string? Hex, string Text, string Age, string Note, bool Bad, string? ToolTip);

		// An account whose colour changes, and how its changes have gone
		sealed class Account(TwitchAuthConfig credentials, Func<string?, Choice> pick) {
			internal TwitchAuthConfig Credentials { get; } = credentials;
			// The next colour after the last one sent
			internal Func<string?, Choice> Pick { get; } = pick;
			internal TwitchToken? Token { get; set; }
			// Replaced whole, for the window to read
			internal volatile Status Status = new(credentials.Login, null, null, Note.Waiting, null);
		}

		static volatile Account[] _accounts = [];

		// Starts the loop for the broadcaster, and for the bot account when chat goes through one
		internal static void Start() {
			TwitchAuthConfig broadcaster = Config.Data.TwitchAuth;
			TwitchAuthConfig bot = TwitchIRCManager.ChatAccount(Config.Data.TwitchBot, broadcaster);
			List<Account> accounts = [new Account(broadcaster, _ => TableColor(ColorTableRegistry.GetRandomColor()))];
			if (bot != broadcaster) {
				accounts.Add(new Account(bot, last => PickNamed(last, Random.Shared)));
			}
			_accounts = accounts.ToArray();
			_ = RunAsync(_accounts);
		}

		// Changes every account's colour, then waits the interval
		static async Task RunAsync(Account[] accounts) {
			while (true) {
				try {
					foreach (Account account in accounts) {
						await ChangeAsync(account);
					}
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error NCL2");
					ConsoleLogger.LogToFile(ex);
				}
				await Task.Delay(TimeSpan.FromSeconds(Settings.Data.NameColor.IntervalSeconds));
			}
		}

		// Sends the account's next colour, validating its token first, and records how it went
		static async Task ChangeAsync(Account account) {
			Status was = account.Status;
			if (was.Note == Note.NoToken) {
				return;
			}
			if (!Settings.Data.NameColor.Enabled) {
				account.Status = was with { Note = Note.Off, Reason = "NameColor.Enabled is false" };
				return;
			}
			Choice next = account.Pick(was.Color?.Color);
			string login = account.Credentials.Login;
			try {
				if (account.Token == null) {
					TwitchToken token = await TwitchHelixApi.ValidateTokenAsync(account.Credentials.AccessToken);
					if (!token.Scopes.Contains(SCOPE)) {
						string reason = $"{login}'s token lacks {SCOPE}";
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Name colour: {reason}, its colour stays as is");
						account.Status = was with { Note = Note.NoToken, Reason = reason };
						return;
					}
					account.Token = token;
				}
				await TwitchHelixApi.SetChatColorAsync(account.Token, account.Credentials.AccessToken, next.Color);
			}
			catch (Exception ex) {
				if (was.Note != Note.Failed) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error NCL1: {login}: {ex.Message}");
					ConsoleLogger.LogToFile(ex);
				}
				account.Status = was with { Note = Note.Failed, Reason = ex.Message };
				return;
			}
			if (was.Note == Note.Failed) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Name colour: {login} changes again");
			}
			account.Status = new Status(login, next, DateTime.UtcNow, Note.None, null);
		}

		// A named colour other than the last
		internal static Choice PickNamed(string? last, Random random) {
			(string Name, string Hex)[] others = Named.Where(color => color.Name != last).ToArray();
			(string name, string hex) = others[random.Next(others.Length)];
			return new Choice(name, name, hex, "twitch");
		}

		// A table colour, sent as its hex in upper case
		internal static Choice TableColor(NamedColor color) {
			return new Choice(color.Hex.ToUpperInvariant(), color.OriginalName, color.Hex, color.Source);
		}

		// The dashboard bar's parts, one per account
		internal static List<StatusPart> StatusParts(DateTime now) {
			return _accounts.Select(account => Part(account.Status, now)).ToList();
		}

		// An account's part: its login capitalised, its last colour and the time since, and its note
		internal static StatusPart Part(Status status, DateTime now) {
			string label = status.Login.Length == 0 ? "" : char.ToUpperInvariant(status.Login[0]) + status.Login[1..];
			Choice? color = status.Color;
			string text = color == null ? "" : color.Name == color.Hex ? $"{color.Hex} ({color.Source})" : $"{color.Name} {color.Hex} ({color.Source})";
			string age = status.SetAt is DateTime at ? Age(now - at) : "";
			(string note, bool bad) = status.Note switch {
				Note.Waiting => ("waiting", false),
				Note.NoToken => ("NO TOKEN", true),
				Note.Failed => ("FAILED", true),
				Note.Off => ("OFF", false),
				_ => ("", false),
			};
			return new StatusPart(label, color?.Hex, text, age, note, bad, status.Reason);
		}

		// "mm:ss", or "h:mm:ss" from an hour
		internal static string Age(TimeSpan age) {
			if (age < TimeSpan.Zero) {
				age = TimeSpan.Zero;
			}
			return age.TotalHours >= 1 ? $"{(int)age.TotalHours}:{age:mm\\:ss}" : age.ToString(@"mm\:ss");
		}
	}
}
