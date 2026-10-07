using System.Text.Json;
using Xunit;
using static StreamAssistant2.TestArgs;

namespace StreamAssistant2.Tests {
	public class TestArgsTests {
		static readonly Param[] Required = [new IntParam("gifts", 1, 1, 100)];
		static readonly Param[] Optional = [
			new BoolParam("late"),
			new IntParam("missing", 0, 0, 100),
			new BoolParam("anon"),
			new TextParam("msg"),
		];

		static Values? P(string argument) => TestArgs.Parse(argument, Required, Optional);

		[Fact]
		public void RequiredOnly_FillsDefaults() {
			Values v = P("5")!;
			Assert.Equal(5, v.Int("gifts"));
			Assert.False(v.Bool("late"));
			Assert.Equal(0, v.Int("missing"));
			Assert.Equal("", v.Text("msg"));
		}

		[Fact]
		public void Keywords_AnyOrder_TextTakesTheRest() {
			Values v = P("5 anon missing 2 late msg hello there")!;
			Assert.True(v.Bool("late"));
			Assert.True(v.Bool("anon"));
			Assert.Equal(2, v.Int("missing"));
			Assert.Equal("hello there", v.Text("msg"));
		}

		[Fact]
		public void Positional_InOrder_TextTakesTheRest() {
			Values v = P("5 true 2 false hello there")!;
			Assert.True(v.Bool("late"));
			Assert.Equal(2, v.Int("missing"));
			Assert.False(v.Bool("anon"));
			Assert.Equal("hello there", v.Text("msg"));
		}

		[Fact]
		public void PositionalPrefix_UsesDefaultsForTheRest() {
			Values v = P("5 false 3")!;
			Assert.Equal(3, v.Int("missing"));
			Assert.False(v.Bool("anon"));
		}

		[Fact]
		public void KeywordsAreCaseInsensitive() {
			Assert.True(P("5 LATE")!.Bool("late"));
		}

		[Theory]
		[InlineData("")]
		[InlineData("0")]
		[InlineData("101")]
		[InlineData("x")]
		[InlineData("5 missing")]
		[InlineData("5 missing x")]
		[InlineData("5 missing 101")]
		[InlineData("5 loud")]
		[InlineData("5 2")]
		[InlineData("5 true late")]
		public void Rejects(string argument) {
			Assert.Null(P(argument));
		}

		[Fact]
		public void NoRequired_PositionalStartsAtFirstWord() {
			Values v = TestArgs.Parse("2 true", [], [new IntParam("tier", 1, 1, 3), new BoolParam("prime")])!;
			Assert.Equal(2, v.Int("tier"));
			Assert.True(v.Bool("prime"));
		}

		[Fact]
		public void Usage_ShowsBothForms() {
			string usage = TestArgs.Usage("bomb", Required, Optional);
			Assert.Equal("!test bomb <gifts> [late] [missing <n>] [anon] [msg <text…>] | !test bomb <gifts> <late> <missing> <anon> <msg>", usage);
		}
	}

	public class TestScriptTests {
		static List<TestEvent> Build(TestScript script, string argument) =>
			script.Build(TestArgs.Parse(argument, script.Required, script.Optional)!);

		static JsonElement Single(TestScript script, string argument) {
			List<TestEvent> events = Build(script, argument);
			Assert.Single(events);
			return events[0].Event;
		}

		static string Notice(TestEvent e) => e.Event.GetProperty("notice_type").GetString()!;

		[Fact]
		public void ScriptNamesAreUnique() {
			var names = TestEventRunner.Scripts.Select(s => s.Name.ToLowerInvariant()).ToList();
			Assert.Equal(names.Count, names.Distinct().Count());
		}

		[Fact]
		public void Sub_CarriesTierPrimeMonths() {
			JsonElement e = Single(TestSubs.Sub, "3 true 6");
			Assert.Equal("sub", e.GetProperty("notice_type").GetString());
			JsonElement sub = e.GetProperty("sub");
			Assert.Equal("3000", sub.GetProperty("sub_tier").GetString());
			Assert.True(sub.GetProperty("is_prime").GetBoolean());
			Assert.Equal(6, sub.GetProperty("duration_months").GetInt32());
		}

		[Fact]
		public void Resub_CarriesEverythingTheHandlerReads() {
			JsonElement e = Single(TestSubs.Resub, "months 20 streak 5 tier 2 gift msg nice stream");
			JsonElement resub = e.GetProperty("resub");
			Assert.Equal(20, resub.GetProperty("cumulative_months").GetInt32());
			Assert.Equal(5, resub.GetProperty("streak_months").GetInt32());
			Assert.Equal("2000", resub.GetProperty("sub_tier").GetString());
			Assert.False(resub.GetProperty("is_prime").GetBoolean());
			Assert.True(resub.GetProperty("is_gift").GetBoolean());
			Assert.Equal(1, resub.GetProperty("duration_months").GetInt32());
			Assert.Equal("nice stream", e.GetProperty("message").GetProperty("text").GetString());
		}

		[Fact]
		public void Gift_IsTargeted() {
			JsonElement e = Single(TestSubs.Gift, "tier 2 total 7 months 3");
			JsonElement gift = e.GetProperty("sub_gift");
			Assert.Equal(JsonValueKind.Null, gift.GetProperty("community_gift_id").ValueKind);
			Assert.Equal("2000", gift.GetProperty("sub_tier").GetString());
			Assert.Equal(7, gift.GetProperty("cumulative_total").GetInt32());
			Assert.Equal(3, gift.GetProperty("duration_months").GetInt32());
			Assert.False(string.IsNullOrEmpty(gift.GetProperty("recipient_user_login").GetString()));
		}

		[Fact]
		public void Gift_Anonymous_HasNoLoginOrTotal() {
			JsonElement e = Single(TestSubs.Gift, "anon");
			Assert.True(e.GetProperty("chatter_is_anonymous").GetBoolean());
			Assert.Equal(JsonValueKind.Null, e.GetProperty("chatter_user_login").ValueKind);
			Assert.Equal(JsonValueKind.Null, e.GetProperty("sub_gift").GetProperty("cumulative_total").ValueKind);
		}

		[Fact]
		public void Cheer_Named() {
			List<TestEvent> events = Build(TestCheer.Script, "500 msg hi there");
			Assert.Equal("channel.cheer", events[0].Type);
			JsonElement e = events[0].Event;
			Assert.Equal(500, e.GetProperty("bits").GetInt32());
			Assert.False(e.GetProperty("is_anonymous").GetBoolean());
			Assert.False(string.IsNullOrEmpty(e.GetProperty("user_login").GetString()));
			Assert.Equal("hi there", e.GetProperty("message").GetString());
		}

		[Fact]
		public void Cheer_Anonymous() {
			JsonElement e = Single(TestCheer.Script, "100 anon");
			Assert.True(e.GetProperty("is_anonymous").GetBoolean());
			Assert.Equal("", e.GetProperty("message").GetString());
		}

		[Fact]
		public void Bomb_FirstThenEveryRecipient() {
			List<TestEvent> events = Build(TestGiftBomb.Script, "3");
			Assert.Equal(4, events.Count);
			Assert.All(events, e => Assert.Equal("channel.chat.notification", e.Type));
			Assert.Equal("community_sub_gift", Notice(events[0]));
			Assert.All(events.Skip(1), e => Assert.Equal("sub_gift", Notice(e)));
		}

		[Fact]
		public void Bomb_Late_PutsBombLast() {
			List<TestEvent> events = Build(TestGiftBomb.Script, "3 late");
			Assert.Equal("community_sub_gift", Notice(events[^1]));
		}

		[Fact]
		public void Bomb_Missing_SendsFewerButAnnouncesTheTotal_AndClamps() {
			List<TestEvent> events = Build(TestGiftBomb.Script, "5 missing 2");
			Assert.Equal(4, events.Count);
			Assert.Equal(5, events[0].Event.GetProperty("community_sub_gift").GetProperty("total").GetInt32());
			Assert.Single(Build(TestGiftBomb.Script, "5 missing 9"));
		}

		[Fact]
		public void Bomb_RecipientsShareTheIdAndAreUnique_FreshIdPerBuild() {
			List<TestEvent> events = Build(TestGiftBomb.Script, "4");
			string id = events[0].Event.GetProperty("community_sub_gift").GetProperty("id").GetString()!;
			var gifts = events.Skip(1).Select(e => e.Event.GetProperty("sub_gift")).ToList();
			Assert.All(gifts, g => Assert.Equal(id, g.GetProperty("community_gift_id").GetString()));
			Assert.Equal(4, gifts.Select(g => g.GetProperty("recipient_user_login").GetString()).Distinct().Count());
			string again = Build(TestGiftBomb.Script, "4")[0].Event.GetProperty("community_sub_gift").GetProperty("id").GetString()!;
			Assert.NotEqual(id, again);
		}

		[Fact]
		public void Bomb_TierAndAnonymity_Positional() {
			List<TestEvent> events = Build(TestGiftBomb.Script, "2 false 0 true 2");
			JsonElement bomb = events[0].Event;
			Assert.Equal("2000", bomb.GetProperty("community_sub_gift").GetProperty("sub_tier").GetString());
			Assert.True(bomb.GetProperty("chatter_is_anonymous").GetBoolean());
			Assert.Equal(JsonValueKind.Null, bomb.GetProperty("chatter_user_login").ValueKind);
			Assert.Equal(JsonValueKind.Null, bomb.GetProperty("community_sub_gift").GetProperty("cumulative_total").ValueKind);
			Assert.Equal(JsonValueKind.Null, events[1].Event.GetProperty("sub_gift").GetProperty("cumulative_total").ValueKind);
		}
	}
}
