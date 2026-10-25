using Xunit;

namespace StreamAssistant2.Tests {
	public class ChannelPointsTests {
		static readonly RewardsSettings Rewards = new() {
			Train = "t", ToiletFlush = "f", ToiletRetrieve = "r", ColorRandom = "cr", ColorSingle = "cs", ColorTriple = "ct",
		};

		[Theory]
		[InlineData("t", "Train")]
		[InlineData("f", "ToiletFlush")]
		[InlineData("r", "ToiletRetrieve")]
		[InlineData("cr", "ColorRandom")]
		[InlineData("cs", "ColorSingle")]
		[InlineData("ct", "ColorTriple")]
		[InlineData("other", "None")]
		[InlineData("T", "None")]
		public void RewardOf_MatchesTheSettingsIds(string id, string expected) {
			Assert.Equal(expected, ChannelPoints.RewardOf(id, Rewards).ToString());
		}

		[Fact]
		public void RewardOf_DefaultsAreTheRealRewards() {
			Assert.Equal(ChannelPoints.Reward.Train, ChannelPoints.RewardOf("0260c648-c3ba-4ff1-920d-dffa5431da74", new RewardsSettings()));
		}
	}
}
