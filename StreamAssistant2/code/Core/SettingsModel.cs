using System.Text.Json.Serialization;

namespace StreamAssistant2 {
	/// <summary>
	/// settings.json: the owner's choices, each defaulting to the value the bot used before it was a setting.
	/// </summary>
	public class SettingsModel {
		public DirectoriesSettings Directories { get; set; } = new();
		public RewardsSettings Rewards { get; set; } = new();
		public TrainSettings Train { get; set; } = new();
		public SoundsSettings Sounds { get; set; } = new();
		public SpeechSettings Speech { get; set; } = new();
		public DiskSpaceSettings DiskSpace { get; set; } = new();
		public AdsSettings Ads { get; set; } = new();
		public ChatSettings Chat { get; set; } = new();
		public MoneyGoalSettings MoneyGoal { get; set; } = new();
		public ClockSettings Clock { get; set; } = new();
		public UptimeSettings Uptime { get; set; } = new();
		public NameColorSettings NameColor { get; set; } = new();
		public LayoutSettings Layout { get; set; } = new();
		public GameSettings Game { get; set; } = new();
		public LeftPanelSettings LeftPanel { get; set; } = new();
		public SplitColorsSettings SplitColors { get; set; } = new();
	}

	// Every directory but BotInput; an empty one turns its feature off
	public class DirectoriesSettings {
		// Logs and the database
		public string BotOutput { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StreamAssistant2", "Bot Output");
		public string Trains { get; set; } = "";
		public string Colors { get; set; } = "";
		public string ColorSchemes { get; set; } = "";
		public string Backgrounds { get; set; } = "";
		public string LivesplitGenerated { get; set; } = "";
		public string Layout { get; set; } = "";
		public string Sidebar { get; set; } = "";
		public string Sounds { get; set; } = "";
	}

	// Channel-point reward ids
	public class RewardsSettings {
		public string Train { get; set; } = "0260c648-c3ba-4ff1-920d-dffa5431da74";
		public string ToiletFlush { get; set; } = "cd46e822-f288-47e6-8e8c-c56155603a0e";
		public string ToiletRetrieve { get; set; } = "785967e7-9b58-41eb-aa11-15fed82a72ec";
		public string ColorRandom { get; set; } = "bc78e213-6818-464d-8d5f-3408e9bd2413";
		public string ColorSingle { get; set; } = "ad70a9d9-12fa-44dd-a3fb-dde21ebc4ec1";
		public string ColorTriple { get; set; } = "30168c08-09f6-4557-ab49-b7eaed212978";
	}

	public class TrainSettings {
		public string Scene { get; set; } = "!Scene: Basics Colored";
		public string Source { get; set; } = "Image: Train";
		// How long it shows
		public double Seconds { get; set; } = 62;
		// Train0.png up to Train<Images - 1>.png
		public int Images { get; set; } = 100;
	}

	// A file in the Sounds directory and its volume, 0 to 1
	public class SoundSettings {
		public string File { get; set; } = "";
		public double Volume { get; set; } = 1;
	}

	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public class SoundsSettings {
		public SoundSettings TribalHymn { get; set; } = new() { File = "Tribal hymn.mp3", Volume = 0.5 };
		public SoundSettings TheClap { get; set; } = new() { File = "theclap.mp3", Volume = 0.4 };
		public SoundSettings Team17Applauds { get; set; } = new() { File = "Team17-Applauds.mp3", Volume = 0.75 };
		public SoundSettings IndianAnthem { get; set; } = new() { File = "IndianAnthem.mp3", Volume = 0.4 };
		public SoundSettings Flush { get; set; } = new() { File = "Flush.wav", Volume = 0.4 };
		public SoundSettings Warning { get; set; } = new() { File = "Warning.wav", Volume = 0.6 };
	}

	public class SpeechSettings {
		// An installed SAPI voice
		public string Voice { get; set; } = "Microsoft Catherine";
	}

	public class DiskSpaceSettings {
		// Falls back to the first drive when missing
		public string Drive { get; set; } = @"A:\";
		// Free space under each starts its level
		public double SpamGiB { get; set; } = 2;
		public double WarnGiB { get; set; } = 15;
		public double NotifyGiB { get; set; } = 100;
		// How often a level repeats; each divides 60
		public int SpamEveryMinutes { get; set; } = 1;
		public int WarnEveryMinutes { get; set; } = 12;
		public int NotifyEveryMinutes { get; set; } = 60;
	}

	public class AdsSettings {
		// Time from an ad break's end to the next; the warnings come 5, 3, 2 and 1 minutes before it
		public int IntervalMinutes { get; set; } = 60;
	}

	public class ChatSettings {
		// A user gets the same FAQ answer at most this often
		public double FaqCooldownSeconds { get; set; } = 60;
	}

	public class MoneyGoalSettings {
		public string Source { get; set; } = "Text: Money Goal";
		public string Currency { get; set; } = "EUR";
		// The goal on a first start, until !moneygoal
		public decimal DefaultGoal { get; set; } = 999;
	}

	public class ClockSettings {
		public string Source { get; set; } = "Text: Time Of Day";
	}

	public class UptimeSettings {
		public string Source { get; set; } = "Text: Stream Uptime";
	}

	public class NameColorSettings {
		// False leaves both chat names' colours alone
		public bool Enabled { get; set; } = true;
		// How often the chat names change colour
		public double IntervalSeconds { get; set; } = 60;
	}

	public class LayoutSettings {
		public string StreamScene { get; set; } = "Basic Stream Layout";
		// Each followed by a space and the aspect
		public string LayoutScenePrefix { get; set; } = "!Scene: Layout";
		public string GamesScenePrefix { get; set; } = "!Scene: Games";
		public string Colorables { get; set; } = "!Layout: Colorables";
		// After a name and a space, names its fade twin
		public string TwinSuffix { get; set; } = "(Transitionary)";
		public string InnerBorder { get; set; } = "Border: Colorable Inner";
		public string InnerAttachment { get; set; } = "Border: Colorable Inner Attachment";
		public string OuterBorder { get; set; } = "Border: Colorable Outer";
		public string OuterAttachment { get; set; } = "Border: Colorable Outer Attachment";
		public string TextScene { get; set; } = "!Scene: All Colorable";
		public string Filter { get; set; } = "Color Correction";
		// The fade's waits: after showing the twins, after hiding the real group, after recolouring it
		public double ShowTwinsSeconds { get; set; } = 5.5;
		public double HideRealSeconds { get; set; } = 2.5;
		public double RecolorSeconds { get; set; } = 1;

		// The fade twin of a source, scene or group
		public string Twin(string name) => $"{name} {TwinSuffix}";
	}

	public class GameSettings {
		public string BackgroundSource { get; set; } = "Image: Background";
		// By position, they show <game>_<n>.png, <game>_2_<n>.png, <game>_3_<n>.png, …
		public string[] SidebarSources { get; set; } = ["Sidebar: Dynamic 1", "Sidebar: Dynamic 2"];
		// Each followed by a space and the slot number
		public string GameCapturePrefix { get; set; } = "Game: Game Capture";
		public string WindowCapturePrefix { get; set; } = "Game: Window Capture";
		public string AudioCapturePrefix { get; set; } = "Audio 5: App Capture";
		// Slots of each kind, numbered from 0
		public int CaptureSlots { get; set; } = 3;
	}

	public class LeftPanelSettings {
		public string Scene { get; set; } = "!Scene: Left Panel";
		public string ColoredScene { get; set; } = "!Scene: Left Panel Colored";
		// Each followed by a space and the group name
		public string Prefix { get; set; } = "_LP:";
		public string ColoredPrefix { get; set; } = "_LPC:";
	}

	public class SplitColorsSettings {
		// The left-panel group holding the split list highlights, their filter and sources
		public string HighlightGroup { get; set; } = "Splits List Highlights";
		public string HighlightFilter { get; set; } = "Color Correction 2";
		public string HighlightGold { get; set; } = "Livesplit: SL Highlights Gold";
		public string HighlightAheadGained { get; set; } = "Livesplit: SL Highlights AheadGained";
		public string HighlightAheadLost { get; set; } = "Livesplit: SL Highlights AheadLost";
		public string HighlightBehindGained { get; set; } = "Livesplit: SL Highlights BehindGained";
		public string HighlightBehindLost { get; set; } = "Livesplit: SL Highlights BehindLost";
		// The left-panel group holding the previous split's differences, their filter and sources
		public string PreviousGroup { get; set; } = "Splits Previous Values";
		public string PreviousFilter { get; set; } = "Color Correction";
		public string PreviousRun { get; set; } = "Livesplit: Previous Run";
		public string PreviousSegment { get; set; } = "Livesplit: Previous Segment";
		public string PreviousGold { get; set; } = "Livesplit: Previous Gold";
	}
}
