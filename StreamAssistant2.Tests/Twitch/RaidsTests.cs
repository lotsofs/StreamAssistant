using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	// Raids.ChatLine against what Twitch sends. Raids.Handle posts to chat, so it's never called here.
	public class RaidsTests {
		// A real raid notification (AssistantLogs\EventSubs\raid_2026-06-12 23-46-46.480.log), trimmed to what
		// tells it apart. user_name is the display name; user_login is the lower-case login.
		static readonly JsonElement RealShape = JsonDocument.Parse("""
			{
				"chatter_user_login": "dana_tv",
				"chatter_user_name": "Dana_TV",
				"chatter_is_anonymous": false,
				"system_message": "13 raiders from Dana_TV have joined!",
				"message": { "text": "", "fragments": [] },
				"notice_type": "raid",
				"sub": null,
				"raid": {
					"user_id": "121355152",
					"user_name": "Dana_TV",
					"user_login": "dana_tv",
					"viewer_count": 13,
					"profile_image_url": "https://static-cdn.jtvnw.net/jtv_user_pictures/x-profile_image-300x300.jpeg"
				},
				"unraid": null,
				"shared_chat_raid": null
			}
			""").RootElement.Clone();

		[Fact]
		public void RealShape_DisplayNameAndViewers() {
			Assert.Equal("Raid: Dana_TV (13)", Raids.ChatLine(RealShape));
		}

		// A notification without its raid block (or with it null) must still give a line, not throw.
		[Theory]
		[InlineData("""{ "notice_type": "raid" }""")]
		[InlineData("""{ "notice_type": "raid", "raid": null }""")]
		public void NoRaidBlock_FallsBack(string json) {
			Assert.Equal("Raid: Someone (0)", Raids.ChatLine(JsonDocument.Parse(json).RootElement));
		}
	}
}
