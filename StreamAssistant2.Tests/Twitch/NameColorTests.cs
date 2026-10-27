using Xunit;

namespace StreamAssistant2.Tests {
	public class NameColorTests {
		static readonly DateTime Now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
		static readonly NameColor.Choice RawUmber = new("#826644", "Raw umber", "#826644", "encycolorpedia");

		[Fact]
		public void PickNamed_NeverRepeatsTheLast() {
			Random random = new(1);
			string? last = null;
			for (int i = 0; i < 200; i++) {
				string next = NameColor.PickNamed(last, random).Color;
				Assert.NotEqual(last, next);
				last = next;
			}
		}

		[Fact]
		public void PickNamed_ReachesEveryColour() {
			Random random = new(2);
			HashSet<string> seen = new();
			for (int i = 0; i < 500; i++) {
				seen.Add(NameColor.PickNamed(null, random).Color);
			}
			Assert.Equal(NameColor.Named.Select(color => color.Name).Order(), seen.Order());
		}

		[Fact]
		public void PickNamed_HasItsHexFromTwitch() {
			Random random = new(3);
			for (int i = 0; i < 100; i++) {
				NameColor.Choice pick = NameColor.PickNamed(null, random);
				Assert.Equal(new NameColor.Choice(pick.Color, pick.Color, NameColor.Named.Single(color => color.Name == pick.Color).Hex, "twitch"), pick);
			}
		}

		[Theory]
		[InlineData("red", "#ff0000")]
		[InlineData("blue_violet", "#8a2be2")]
		[InlineData("yellow_green", "#9acd32")]
		public void Named_HexesAreTwitchs(string name, string hex) {
			Assert.Contains((name, hex), NameColor.Named);
		}

		[Fact]
		public void Named_AreTwitchsFifteen_EachWithALowercaseHex() {
			Assert.Equal(15, NameColor.Named.Length);
			Assert.Equal(15, NameColor.Named.Select(color => color.Name).Distinct().Count());
			Assert.All(NameColor.Named, color => Assert.Matches("^#[0-9a-f]{6}$", color.Hex));
		}

		[Fact]
		public void TableColor_SendsUppercase_KeepsNameHexAndSource() {
			Assert.Equal(new NameColor.Choice("#E97451", "Burnt Sienna", "#e97451", "crayola"), NameColor.TableColor(new NamedColor("crayola", "Burnt Sienna", "#e97451")));
		}

		[Fact]
		public void Part_SetColour_ShowsNameHexSourceAndAge() {
			NameColor.StatusPart part = NameColor.Part(new NameColor.Status("lotsofs", RawUmber, Now.AddSeconds(-42), NameColor.Note.None, null), Now);
			Assert.Equal(new NameColor.StatusPart("Lotsofs", "#826644", "Raw umber #826644 (encycolorpedia)", "00:42", "", false, null), part);
		}

		[Fact]
		public void Part_RandomRgb_ShowsTheHexOnce() {
			NameColor.Choice rgb = NameColor.TableColor(new NamedColor("random rgb", "#0a0b0c", "#0a0b0c"));
			Assert.Equal("#0a0b0c (random rgb)", NameColor.Part(new NameColor.Status("lotsofs", rgb, Now, NameColor.Note.None, null), Now).Text);
		}

		[Fact]
		public void Part_NamedColour_ShowsTwitchAsTheSource() {
			NameColor.Choice coral = new("coral", "coral", "#ff7f50", "twitch");
			Assert.Equal("coral #ff7f50 (twitch)", NameColor.Part(new NameColor.Status("botsofs", coral, Now, NameColor.Note.None, null), Now).Text);
		}

		[Fact]
		public void Part_NoToken_IsRedWithTheReason() {
			NameColor.StatusPart part = NameColor.Part(new NameColor.Status("botsofs", null, null, NameColor.Note.NoToken, "botsofs's token lacks user:manage:chat_color"), Now);
			Assert.Equal(new NameColor.StatusPart("Botsofs", null, "", "", "NO TOKEN", true, "botsofs's token lacks user:manage:chat_color"), part);
		}

		[Fact]
		public void Part_Failed_KeepsTheLastColourAndAge() {
			NameColor.StatusPart part = NameColor.Part(new NameColor.Status("lotsofs", RawUmber, Now.AddMinutes(-3), NameColor.Note.Failed, "Chat colour failed (400)"), Now);
			Assert.Equal(new NameColor.StatusPart("Lotsofs", "#826644", "Raw umber #826644 (encycolorpedia)", "03:00", "FAILED", true, "Chat colour failed (400)"), part);
		}

		[Fact]
		public void Part_FailedBeforeAnyColour_IsTheNoteAlone() {
			NameColor.StatusPart part = NameColor.Part(new NameColor.Status("lotsofs", null, null, NameColor.Note.Failed, "x"), Now);
			Assert.Equal(new NameColor.StatusPart("Lotsofs", null, "", "", "FAILED", true, "x"), part);
		}

		[Theory]
		[InlineData("Waiting", "waiting")]
		[InlineData("Off", "OFF")]
		public void Part_WaitingAndOff_AreNotProblems(string note, string text) {
			NameColor.StatusPart part = NameColor.Part(new NameColor.Status("botsofs", null, null, Enum.Parse<NameColor.Note>(note), null), Now);
			Assert.Equal(text, part.Note);
			Assert.False(part.Bad);
		}

		[Fact]
		public void Part_EmptyLogin_HasNoLabel() {
			Assert.Equal("", NameColor.Part(new NameColor.Status("", null, null, NameColor.Note.Waiting, null), Now).Label);
		}

		[Theory]
		[InlineData(0, "00:00")]
		[InlineData(59, "00:59")]
		[InlineData(61, "01:01")]
		[InlineData(3599, "59:59")]
		[InlineData(3723, "1:02:03")]
		[InlineData(90061, "25:01:01")]
		[InlineData(-5, "00:00")]
		public void Age_IsMinutesAndSeconds_ThenHours(int seconds, string expected) {
			Assert.Equal(expected, NameColor.Age(TimeSpan.FromSeconds(seconds)));
		}

		[Theory]
		[InlineData("#E97451", "https://api.twitch.tv/helix/chat/color?user_id=123&color=%23E97451")]
		[InlineData("blue_violet", "https://api.twitch.tv/helix/chat/color?user_id=123&color=blue_violet")]
		public void ChatColorUrl_EscapesTheHash(string color, string expected) {
			Assert.Equal(expected, TwitchHelixApi.ChatColorUrl("123", color));
		}

		[Fact]
		public void ParseToken_ReadsTheFields() {
			TwitchToken token = TwitchHelixApi.ParseToken("""
				{"client_id":"abc","login":"botsofs","scopes":["chat:read","chat:edit","user:manage:chat_color"],"user_id":"141981764","expires_in":5520838}
				""")!;
			Assert.Equal("abc", token.ClientId);
			Assert.Equal("botsofs", token.Login);
			Assert.Equal("141981764", token.UserId);
			Assert.Equal(["chat:read", "chat:edit", NameColor.SCOPE], token.Scopes);
		}

		[Fact]
		public void ParseToken_NoScopes_IsEmpty() {
			Assert.Empty(TwitchHelixApi.ParseToken("""{"client_id":"abc","login":"x","scopes":null,"user_id":"1"}""")!.Scopes);
		}

		[Theory]
		[InlineData("""{"client_id":"abc","scopes":[]}""")]
		[InlineData("""{"user_id":"1","scopes":[]}""")]
		[InlineData("""{"status":401,"message":"invalid access token"}""")]
		public void ParseToken_NoClientOrUser_IsNull(string json) {
			Assert.Null(TwitchHelixApi.ParseToken(json));
		}
	}
}
