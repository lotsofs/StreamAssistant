using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	public sealed class TestReplayTests : IDisposable {
		readonly string _dir = Path.Combine(Path.GetTempPath(), "replay-" + Guid.NewGuid().ToString("N"));

		public TestReplayTests() {
			Directory.CreateDirectory(_dir);
			Write("resub_2026-07-04 13-21-21.782.log", """{ "notice_type": "resub", "chatter_user_login": "real" }""");
			Write("sub_2026-07-05 10-00-00.000.log", """{ "notice_type": "sub" }""");
			Write("sub_2026-07-05 11-00-00.000.log", """{ "notice_type": "sub" }""");
			// A real bomb: recipients logged around the bomb event, one before it and one after.
			Write("sub_gift_2026-06-12 23-30-43.323.log", Gift("111", "first"));
			Write("community_sub_gift_2026-06-12 23-30-43.586.log", """{ "notice_type": "community_sub_gift", "community_sub_gift": { "id": "111", "total": 2 } }""");
			Write("sub_gift_2026-06-12 23-30-43.700.log", Gift("111", "second"));
			// Two recipients in the same millisecond: the second is appended to the same file.
			Write("sub_gift_2026-06-12 23-30-43.800.log", Gift("111", "third") + "\n" + Gift("111", "fourth"));
			Write("sub_gift_2026-06-12 23-31-00.000.log", Gift("222", "otherbomb"));
			Write("sub_gift_2026-06-13 09-00-00.000.log", """{ "notice_type": "sub_gift", "sub_gift": { "community_gift_id": null, "recipient_user_login": "targeted" } }""");
		}

		public void Dispose() {
			Directory.Delete(_dir, true);
		}

		void Write(string name, string json) {
			File.WriteAllText(Path.Combine(_dir, name), json);
		}

		static string Gift(string id, string recipient) {
			return $$"""{ "notice_type": "sub_gift", "sub_gift": { "community_gift_id": "{{id}}", "recipient_user_login": "{{recipient}}" } }""";
		}

		List<TestEvent> Load(string query) => TestReplay.Load(_dir, query, out _);

		[Theory]
		[InlineData("resub_2026-07-04 13-21-21.782")]
		[InlineData("resub_2026-07-04 13-21-21.782.log")]
		[InlineData("RESUB_2026-07-04")]
		public void FindsByNameOrUniquePrefix(string query) {
			List<TestEvent> events = Load(query);
			Assert.Single(events);
			Assert.Equal("channel.chat.notification", events[0].Type);
			Assert.Equal("real", events[0].Event.GetProperty("chatter_user_login").GetString());
		}

		[Fact]
		public void AmbiguousPrefix_SendsNothingAndListsMatches() {
			List<TestEvent> events = TestReplay.Load(_dir, "sub_2026-07-05", out string message);
			Assert.Empty(events);
			Assert.Contains("2 replay files match", message);
		}

		[Fact]
		public void NoMatch_SendsNothing() {
			List<TestEvent> events = TestReplay.Load(_dir, "raid", out string message);
			Assert.Empty(events);
			Assert.Contains("No test script or replay file", message);
		}

		[Fact]
		public void MissingFolder_SendsNothing() {
			Assert.Empty(TestReplay.Load(Path.Combine(_dir, "nope"), "sub", out _));
		}

		[Fact]
		public void Bomb_BringsItsRecipientsInFileOrder_UnderAFreshSharedId() {
			List<TestEvent> events = Load("community_sub_gift");
			Assert.Equal(5, events.Count);
			Assert.Equal("community_sub_gift", events[1].Event.GetProperty("notice_type").GetString());
			var recipients = events.Where((_, i) => i != 1).Select(e => e.Event.GetProperty("sub_gift").GetProperty("recipient_user_login").GetString());
			Assert.Equal(["first", "second", "third", "fourth"], recipients);

			string id = events[1].Event.GetProperty("community_sub_gift").GetProperty("id").GetString()!;
			Assert.NotEqual("111", id);
			Assert.All(events.Where((_, i) => i != 1), e => Assert.Equal(id, e.Event.GetProperty("sub_gift").GetProperty("community_gift_id").GetString()));

			string again = Load("community_sub_gift")[1].Event.GetProperty("community_sub_gift").GetProperty("id").GetString()!;
			Assert.NotEqual(id, again);
		}

		[Fact]
		public void BombRecipientAlone_GetsAFreshIdToo() {
			List<TestEvent> events = Load("sub_gift_2026-06-12 23-31");
			Assert.Single(events);
			Assert.NotEqual("222", events[0].Event.GetProperty("sub_gift").GetProperty("community_gift_id").GetString());
		}

		[Theory]
		[InlineData("resub_2026-07-04 13-21-21.782.log", "channel.chat.notification")]
		[InlineData("community_sub_gift_2026-06-12 23-30-43.586.log", "channel.chat.notification")]
		[InlineData("channel.cheer_2026-08-01 10-00-00.000.log", "channel.cheer")]
		[InlineData("channel.channel_points_custom_reward_redemption.add_2026-08-01 10-00-00.000.log", "channel.channel_points_custom_reward_redemption.add")]
		[InlineData("channel.ad_break.begin_2026-08-01 10-00-00.000", "channel.ad_break.begin")]
		public void TypeOf_ReadsTheTypeFromTheName(string name, string type) {
			Assert.Equal(type, TestReplay.TypeOf(name));
		}

		[Fact]
		public void NonChatEvent_ReplaysWithItsOwnType() {
			Write("channel.cheer_2026-08-01 10-00-00.000.log", """{ "bits": 100, "is_anonymous": false }""");
			List<TestEvent> events = Load("channel.cheer");
			Assert.Single(events);
			Assert.Equal("channel.cheer", events[0].Type);
			Assert.Equal(100, events[0].Event.GetProperty("bits").GetInt32());
		}

		[Fact]
		public void DoubledFile_ReplaysBothEvents() {
			Assert.Equal(2, Load("sub_gift_2026-06-12 23-30-43.800").Count);
		}

		[Fact]
		public void SplitValues_IgnoresBracesAndQuotesInsideStrings() {
			string a = """{ "text": "a } \" { b", "list": [1, {"x": "]"}] }""";
			string b = """{ "n": 2 }""";
			Assert.Equal([a, b], TestReplay.SplitValues(a + b));
			Assert.Equal([a, b], TestReplay.SplitValues(a + "\r\n\r\n" + b + "\n"));
		}

		[Fact]
		public void TargetedGift_KeepsItsNullId() {
			List<TestEvent> events = Load("sub_gift_2026-06-13");
			Assert.Single(events);
			Assert.Equal(JsonValueKind.Null, events[0].Event.GetProperty("sub_gift").GetProperty("community_gift_id").ValueKind);
		}
	}
}
