using Xunit;

namespace StreamAssistant2.Tests {
	public class IrcCredentialsTests {
		readonly TwitchAuthConfig _broadcaster = new() { Login = "lotsofs", AccessToken = "broadcaster-token" };

		[Fact]
		public void BotSet_ChatsAsBot() {
			var bot = new TwitchAuthConfig { Login = "botsofs", AccessToken = "bot-token" };
			Assert.Same(bot, TwitchIRCManager.ChatAccount(bot, _broadcaster));
		}

		[Fact]
		public void NoBotToken_ChatsAsBroadcaster() {
			var bot = new TwitchAuthConfig { Login = "botsofs" };
			Assert.Same(_broadcaster, TwitchIRCManager.ChatAccount(bot, _broadcaster));
		}

		[Fact]
		public void NoBotLogin_ChatsAsBroadcaster() {
			var bot = new TwitchAuthConfig { AccessToken = "bot-token" };
			Assert.Same(_broadcaster, TwitchIRCManager.ChatAccount(bot, _broadcaster));
		}

		[Fact]
		public void NoBotBlock_ChatsAsBroadcaster() {
			Assert.Same(_broadcaster, TwitchIRCManager.ChatAccount(new TwitchAuthConfig(), _broadcaster));
		}
	}
}
