using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	public class JsonElementExtensionsTests {
		static readonly JsonElement Json = JsonDocument.Parse("""
			{
				"chatter_user_login": "someone",
				"chatter_is_anonymous": false,
				"nothing": null,
				"sub": { "sub_tier": "2000", "duration_months": 3, "is_prime": true },
				"ad": { "duration_seconds": "60", "is_automatic": "true" },
				"bad": { "number": "lots", "flag": "maybe", "huge": 99999999999 }
			}
			""").RootElement.Clone();

		[Fact]
		public void ReadString_FollowsDottedPaths() {
			Assert.Equal("someone", Json.ReadString("chatter_user_login"));
			Assert.Equal("2000", Json.ReadString("sub.sub_tier"));
		}

		[Theory]
		[InlineData("missing")]
		[InlineData("sub.missing")]
		[InlineData("missing.deeper")]
		[InlineData("nothing")]
		[InlineData("nothing.deeper")]
		[InlineData("sub")]
		[InlineData("sub.duration_months")]
		[InlineData("chatter_user_login.deeper")]
		public void ReadString_GivesTheFallbackForMissingNullOrWrongKind(string path) {
			Assert.Equal("fallback", Json.ReadString(path, "fallback"));
			Assert.Equal("", Json.ReadString(path));
		}

		[Fact]
		public void ReadInt_ReadsNumbersAndNumericStrings() {
			Assert.Equal(3, Json.ReadInt("sub.duration_months"));
			Assert.Equal(2000, Json.ReadInt("sub.sub_tier"));
			Assert.Equal(60, Json.ReadInt("ad.duration_seconds"));
		}

		[Theory]
		[InlineData("missing")]
		[InlineData("nothing")]
		[InlineData("sub")]
		[InlineData("bad.number")]
		[InlineData("bad.huge")]
		[InlineData("sub.is_prime")]
		public void ReadInt_GivesTheFallbackOtherwise(string path) {
			Assert.Equal(-1, Json.ReadInt(path, -1));
			Assert.Equal(0, Json.ReadInt(path));
		}

		[Fact]
		public void ReadBool_ReadsBooleansAndBooleanStrings() {
			Assert.True(Json.ReadBool("sub.is_prime"));
			Assert.False(Json.ReadBool("chatter_is_anonymous", true));
			Assert.True(Json.ReadBool("ad.is_automatic"));
		}

		[Theory]
		[InlineData("missing")]
		[InlineData("nothing")]
		[InlineData("bad.flag")]
		[InlineData("sub.duration_months")]
		public void ReadBool_GivesTheFallbackOtherwise(string path) {
			Assert.True(Json.ReadBool(path, true));
			Assert.False(Json.ReadBool(path));
		}

		[Fact]
		public void ReadElement_MissingOrNullIsUndefined_AndSafeToReadFrom() {
			Assert.Equal(JsonValueKind.Undefined, Json.ReadElement("missing.deeper").ValueKind);
			Assert.Equal(JsonValueKind.Undefined, Json.ReadElement("nothing").ValueKind);
			Assert.Equal("x", Json.ReadElement("missing").ReadString("anything", "x"));
			Assert.Equal(JsonValueKind.Object, Json.ReadElement("sub").ValueKind);
			Assert.Equal(3, Json.ReadElement("sub").ReadInt("duration_months"));
		}
	}
}
