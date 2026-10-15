using Xunit;

namespace StreamAssistant2.Tests {
	// The spoken sentences for subs, resubs, gifts and gift bombs, and the gift-bomb bookkeeping.
	// Pure functions and CommunityGiftSub only: nothing here plays sound, speaks or posts to chat.
	public class SubscriptionMessageTests {
		[Theory]
		[InlineData(1, false, 1, "foo subscribed")]
		[InlineData(2, false, 1, "foo subscribed at tier 2")]
		[InlineData(3, false, 1, "foo subscribed at tier 3")]
		[InlineData(1, true, 1, "foo subscribed with prime")]
		[InlineData(1, false, 3, "foo subscribed. It is a 3 month sub.")]
		[InlineData(2, false, 3, "foo subscribed at tier 2. It is a 3 month sub.")]
		[InlineData(1, true, 6, "foo subscribed with prime. It is a 6 month sub.")]
		public void Sub(int tier, bool prime, int months, string expected) {
			Assert.Equal(expected, SubscriptionMessages.BuildSubMessage("foo", tier, prime, months));
		}

		// SBM: the month count used to be glued on with no separator ("foo subscribedIt is a 3 month sub.")
		[Fact]
		public void Sub_MonthsAreSeparated() {
			Assert.DoesNotContain("subscribedIt", SubscriptionMessages.BuildSubMessage("foo", 1, false, 3));
		}

		[Theory]
		[InlineData(1, false, 1, 0, "", "foo subscribed")]
		[InlineData(1, true, 12, 5, "", "foo subscribed with prime, They've subscribed for 12 months, Currently on a 5 month streak")]
		[InlineData(2, false, 12, 1, "hello", "foo subscribed at tier 2, They've subscribed for 12 months: hello")]
		[InlineData(1, false, 1, 0, "hi", "foo subscribed: hi")]
		public void Resub(int tier, bool prime, int cumulative, int streak, string text, string expected) {
			Assert.Equal(expected, SubscriptionMessages.BuildResubMessage("foo", tier, prime, cumulative, streak, text));
		}

		[Theory]
		[InlineData(1, 1, 1, "foo gifted a tier 1 sub to bar. This is their first gift sub in the channel")]
		[InlineData(2, 4, 1, "foo gifted a tier 2 sub to bar. They have given 4 gift subs in the channel")]
		[InlineData(1, 0, 1, "foo gifted a tier 1 sub to bar")]
		[InlineData(1, 4, 3, "foo gifted a tier 1 sub to bar. They have given 4 gift subs in the channel, It is a 3 month gift")]
		public void Gift(int tier, int cumulative, int months, string expected) {
			Assert.Equal(expected, SubscriptionMessages.BuildGiftMessage("foo", tier, "bar", cumulative, months));
		}

		// SBM: the tier test was inverted (tier 1 announced, 2 and 3 not) and left a double space
		[Theory]
		[InlineData(1, "foo is gifting 5 subs to Lots Of Ess's community! ")]
		[InlineData(2, "foo is gifting 5 tier 2 subs to Lots Of Ess's community! ")]
		[InlineData(3, "foo is gifting 5 tier 3 subs to Lots Of Ess's community! ")]
		public void Bomb_TierOnlyAboveOne(int tier, string expectedStart) {
			string msg = SubscriptionMessages.BuildBombMessage("foo", 5, tier, 5, ["a", "b"]);
			Assert.StartsWith(expectedStart, msg);
			Assert.DoesNotContain("  ", msg);
		}

		[Fact]
		public void Bomb_CumulativeAndRecipients() {
			string msg = SubscriptionMessages.BuildBombMessage("foo", 2, 1, 10, ["a", "b"]);
			Assert.Equal("foo is gifting 2 subs to Lots Of Ess's community! They've gifted a total of 10 in the channel! Congratulations to: a, b, ", msg);
		}

		// Recipients that didn't arrive within the 10 s wait are counted, not dropped
		[Theory]
		[InlineData(5, new[] { "a", "b" }, "Congratulations to: a, b, and 3 other people")]
		[InlineData(3, new[] { "a", "b" }, "Congratulations to: a, b, and 1 other person")]
		[InlineData(4, new string[0], "Congratulations to: 4 people")]
		[InlineData(1, new string[0], "Congratulations to: 1 person")]
		[InlineData(2, new[] { "a", "b" }, "Congratulations to: a, b, ")]
		public void Bomb_MissingRecipientsAreCounted(int total, string[] recipients, string expectedEnd) {
			Assert.EndsWith(expectedEnd, SubscriptionMessages.BuildBombMessage("foo", total, 1, total, recipients));
		}

		[Theory]
		[InlineData(new[] { "a", "b" }, "🛢️ 2 people received a gift sub, but no gift bomb arrived: a, b")]
		[InlineData(new[] { "a" }, "🛢️ 1 person received a gift sub, but no gift bomb arrived: a")]
		public void Giftless(string[] recipients, string expected) {
			Assert.Equal(expected, SubscriptionMessages.BuildGiftlessMessage(recipients));
		}

		[Fact]
		public void Bomb_NoCumulativeWhenThisIsAll() {
			Assert.DoesNotContain("total", SubscriptionMessages.BuildBombMessage("foo", 2, 1, 2, ["a", "b"]));
		}

		static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

		[Fact]
		public async Task Bomb_RecipientsBeforeExpected_Completes() {
			var bomb = new CommunityGiftSub("x", DateTime.UtcNow);
			bomb.AddRecipient("a");
			bomb.AddRecipient("b");
			bomb.SetExpected(2);
			var started = DateTime.UtcNow;
			var recipients = await bomb.WaitForRecipientsAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(new HashSet<string> { "a", "b" }, recipients);
			Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
		}

		[Fact]
		public async Task Bomb_RecipientsAfterExpected_Completes() {
			var bomb = new CommunityGiftSub("x", DateTime.UtcNow);
			bomb.SetExpected(2);
			var wait = bomb.WaitForRecipientsAsync(TimeSpan.FromSeconds(5));
			bomb.AddRecipient("a");
			bomb.AddRecipient("b");
			Assert.Equal(2, (await wait).Count);
		}

		[Fact]
		public async Task Bomb_MissingRecipients_TimesOutWithPartialList() {
			var bomb = new CommunityGiftSub("x", DateTime.UtcNow);
			bomb.SetExpected(3);
			bomb.AddRecipient("a");
			var recipients = await bomb.WaitForRecipientsAsync(Short);
			Assert.Equal(new HashSet<string> { "a" }, recipients);
		}

		[Fact]
		public void Sweep_DropsBombsOlderThanAnHour() {
			var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
			var bombs = new Dictionary<string, CommunityGiftSub> {
				["old"] = new("old", now.AddMinutes(-61)),
				["recent"] = new("recent", now.AddMinutes(-59)),
				["new"] = new("new", now),
			};
			Subscriptions.SweepOldBombs(bombs, now);
			Assert.Equal(new[] { "new", "recent" }, bombs.Keys.OrderBy(k => k));
		}
	}
}
