using System.Globalization;
using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	// The pure parts of Tips and StreamElementsEventHandler. Tips.Process itself plays a sound, posts to chat
	// and speaks, so it's never called here.
	public class TipsTests {
		[Theory]
		[InlineData("5", "5")]
		[InlineData("100.00", "100")]
		[InlineData("4.2", "4.20")]
		[InlineData("4.20", "4.20")]
		[InlineData("0.5", "0.50")]
		[InlineData("1234.567", "1234.57")]
		public void FormatAmount_WholeOrTwoDecimals(string amount, string expected) {
			Assert.Equal(expected, Tips.FormatAmount(decimal.Parse(amount, CultureInfo.InvariantCulture)));
		}

		// The old Donations code used the machine culture, which on a Dutch Windows says "4,2".
		[Fact]
		public void FormatAmount_UsesADotInAnyCulture() {
			CultureInfo saved = CultureInfo.CurrentCulture;
			try {
				CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
				Assert.Equal("4.20", Tips.FormatAmount(4.2m));
				Assert.Equal("4.20 EUR", Tips.Money(4.2m, "EUR"));
			}
			finally {
				CultureInfo.CurrentCulture = saved;
			}
		}

		[Fact]
		public void Money_WithoutCurrency_HasNoTrailingSpace() {
			Assert.Equal("5", Tips.Money(5m, ""));
		}

		[Fact]
		public void Sentence_WithMessage() {
			Assert.Equal("someone donated 4.20 EUR: hello there", Tips.Sentence("someone", 4.2m, "EUR", "hello there"));
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void Sentence_WithoutMessage_DropsTheColon(string message) {
			Assert.Equal("someone donated 5 USD", Tips.Sentence("someone", 5m, "USD", message));
		}

		[Fact]
		public void ChatLine() {
			Assert.Equal("💸 someone tipped 4.20 EUR 💸", Tips.ChatLine("someone", 4.2m, "EUR"));
		}

		[Fact]
		public void IsNew_OncePerId_EmptyAlways() {
			string id = Guid.NewGuid().ToString("N");
			Assert.True(Tips.IsNew(id));
			Assert.False(Tips.IsNew(id));
			Assert.True(Tips.IsNew(Guid.NewGuid().ToString("N")));
			Assert.True(Tips.IsNew(""));
			Assert.True(Tips.IsNew(""));
		}

		// The example channel.tips payload from docs.streamelements.com, with its email address.
		static readonly JsonElement RealShape = JsonDocument.Parse("""
			{
				"donation": {
					"user": { "username": "Styler", "geo": "ZZ", "email": "styler@streamelements.com", "channel": "5ad23dcc18fff500d78c5348" },
					"message": "",
					"amount": 4.2,
					"currency": "USD",
					"paymentMethod": "scheme"
				},
				"_id": "67b5f39d07ecd4c594e60f73",
				"channel": "5ad23dcc18fff500d78c5348",
				"provider": "paypal",
				"approved": "allowed",
				"status": "success"
			}
			""").RootElement.Clone();

		[Fact]
		public void RealShape_ReadsWhatTipsReads() {
			Assert.Equal("Styler", RealShape.ReadString("donation.user.username"));
			Assert.Equal(4.2m, RealShape.ReadDecimal("donation.amount"));
			Assert.Equal("USD", RealShape.ReadString("donation.currency"));
			Assert.Equal("67b5f39d07ecd4c594e60f73", RealShape.ReadString("_id"));
		}

		[Fact]
		public void Redact_DropsOnlyTheEmail() {
			string json = StreamElementsEventHandler.Redact(RealShape).ToJsonString();
			Assert.DoesNotContain("email", json);
			Assert.DoesNotContain("styler@streamelements.com", json);
			JsonElement redacted = JsonDocument.Parse(json).RootElement;
			Assert.Equal("Styler", redacted.ReadString("donation.user.username"));
			Assert.Equal("ZZ", redacted.ReadString("donation.user.geo"));
			Assert.Equal(4.2m, redacted.ReadDecimal("donation.amount"));
			Assert.Equal("67b5f39d07ecd4c594e60f73", redacted.ReadString("_id"));
		}

		[Fact]
		public void Redact_LeavesOtherShapesAlone() {
			Assert.Equal("{}", StreamElementsEventHandler.Redact(default).ToJsonString());
			Assert.Equal("[1,2]", StreamElementsEventHandler.Redact(JsonDocument.Parse("[1,2]").RootElement).ToJsonString());
			Assert.Equal("""{"donation":"odd"}""", StreamElementsEventHandler.Redact(JsonDocument.Parse("""{"donation":"odd"}""").RootElement).ToJsonString());
		}
	}
}
