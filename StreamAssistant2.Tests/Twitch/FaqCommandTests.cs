using Xunit;

namespace StreamAssistant2.Tests {
	// The public FAQ commands: which command a chat line triggers, the reply, and the cooldown.
	// CheckForCommands sends to chat, so it's never called here; the cooldown gets its own instance.
	public class FaqCommandTests {
		[Theory]
		// The !name forms, anywhere in the message and in any case
		[InlineData("!civilians", "!civilians")]
		[InlineData("hey !ktane", "!ktane")]
		[InlineData("where is the !manual", "!ktane")]
		[InlineData("!language", "!language")]
		[InlineData("!turkish?", "!language")]
		[InlineData("!SKEYS", "!skeys")]
		[InlineData("!song", "!song")]
		[InlineData("!sssa", "!sssa")]
		// The question forms
		[InlineData("Why are you KILLING hostages", "!civilians")]
		[InlineData("how do you shoot civilians without them dying", "!civilians")]
		[InlineData("why are you doing homework", "!ktane")]
		[InlineData("What are you doing, home work?", "!ktane")]
		[InlineData("why is the game in Spanish", "!language")]
		[InlineData("how do you show your keys", "!skeys")]
		[InlineData("what program displays your inputs", "!skeys")]
		[InlineData("is that the Song dynasty", "!song")]
		[InlineData("whats SSSA", "!sssa")]
		// Question words inside other words no longer count
		[InlineData("nice show, any key to continue", null)]
		[InlineData("somehow he keeps showing the keys", null)]
		[InlineData("the showcase was shooting hostages", null)]
		[InlineData("I care about doing homework", null)]
		// Half a question, or plain chat
		[InlineData("kill the hostage", null)]
		[InlineData("is the song in spanish", null)]
		[InlineData("hello chat", null)]
		[InlineData("", null)]
		public void FindCommand_MatchesTheLine(string text, string? expected) {
			Assert.Equal(expected, ChatHandler.FindCommand(text)?.Name);
		}

		// Each command answers its own name, so no earlier command shadows it
		[Fact]
		public void EachName_FindsItsOwnCommand() {
			foreach (ChatCommand cmd in ChatHandler._commands) {
				Assert.Equal(cmd.Name, ChatHandler.FindCommand(cmd.Name)?.Name);
			}
		}

		// An admin's FAQ command falls through to the FAQ
		[Fact]
		public void EachName_IsNotAnAdminCommand() {
			foreach (ChatCommand cmd in ChatHandler._commands) {
				Assert.False(ChatHandler.CheckForAdminCommands(cmd.Name));
			}
			Assert.False(ChatHandler.CheckForAdminCommands("why shoot civilians"));
		}

		[Fact]
		public void TwoCommandsInOneLine_EarlierInTheListWins() {
			Assert.Equal("!skeys", ChatHandler.FindCommand("!sssa and !skeys")?.Name);
		}

		[Fact]
		public void Reply_MentionsTheUser() {
			ChatCommand skeys = ChatHandler.FindCommand("!skeys")!;
			Assert.Equal("@viewer123 " + skeys.Output, ChatHandler.Reply(skeys, "viewer123"));
		}

		static readonly DateTime T0 = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

		static ChatHandler.Cooldowns NewCooldowns() {
			return new ChatHandler.Cooldowns(TimeSpan.FromSeconds(60));
		}

		[Fact]
		public void Cooldown_FirstUse_Allowed() {
			Assert.True(NewCooldowns().TryUse("alice", "!skeys", T0));
		}

		[Fact]
		public void Cooldown_SameUserAndCommand_RefusedUntilItEnds() {
			ChatHandler.Cooldowns cooldowns = NewCooldowns();
			cooldowns.TryUse("alice", "!skeys", T0);
			Assert.False(cooldowns.TryUse("alice", "!skeys", T0.AddSeconds(59)));
			Assert.True(cooldowns.TryUse("alice", "!skeys", T0.AddSeconds(60)));
		}

		[Fact]
		public void Cooldown_OtherUserOrCommand_Allowed() {
			ChatHandler.Cooldowns cooldowns = NewCooldowns();
			cooldowns.TryUse("alice", "!skeys", T0);
			Assert.True(cooldowns.TryUse("bob", "!skeys", T0.AddSeconds(1)));
			Assert.True(cooldowns.TryUse("alice", "!sssa", T0.AddSeconds(1)));
		}

		[Fact]
		public void Cooldown_RefusedTry_DoesNotExtendIt() {
			ChatHandler.Cooldowns cooldowns = NewCooldowns();
			cooldowns.TryUse("alice", "!skeys", T0);
			cooldowns.TryUse("alice", "!skeys", T0.AddSeconds(30));
			Assert.True(cooldowns.TryUse("alice", "!skeys", T0.AddSeconds(60)));
		}
	}
}
