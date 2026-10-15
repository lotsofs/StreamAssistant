using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	// Bomb recipients whose bomb event never arrives must be reported, not silently dropped.
	// Uses the real HandleSubGiftNotif bomb path, which plays no sound and speaks nothing; its chat
	// messages go nowhere because IRC isn't connected in tests.
	public class GiftlessRecipientTests : IDisposable {
		readonly TimeSpan _savedWait = Subscriptions.GiftlessWait;
		readonly List<string> _log = new();

		public GiftlessRecipientTests() {
			Subscriptions.GiftlessWait = TimeSpan.FromMilliseconds(200);
			ConsoleLogger.LineLogged += OnLine;
		}

		public void Dispose() {
			ConsoleLogger.LineLogged -= OnLine;
			Subscriptions.GiftlessWait = _savedWait;
		}

		void OnLine(ConsoleLogger.ColorType _, string line) {
			lock (_log) _log.Add(line);
		}

		List<string> Log { get { lock (_log) return _log.ToList(); } }

		static Task Recipient(string bombId, string login) {
			using var doc = JsonDocument.Parse($"{{\"sub_gift\":{{\"community_gift_id\":\"{bombId}\",\"recipient_user_login\":\"{login}\"}}}}");
			return Subscriptions.HandleSubGiftNotif(doc.RootElement);
		}

		[Fact]
		public async Task RecipientsWithoutBomb_AreReported() {
			string id = "giftless-" + Guid.NewGuid().ToString("N");
			await Recipient(id, "alice");
			await Recipient(id, "bob");
			await Task.Delay(600);
			var lines = Log.Where(l => l.Contains(id) && l.Contains("never arrived")).ToList();
			Assert.Single(lines);
			Assert.Contains("alice", lines[0]);
			Assert.Contains("bob", lines[0]);
		}

		[Fact]
		public async Task BombThatArrived_IsNotReported() {
			var bomb = new CommunityGiftSub("arrived-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow);
			bomb.AddRecipient("alice");
			bomb.SetExpected(1);
			await Subscriptions.ReportIfGiftlessAsync(bomb);
			Assert.DoesNotContain(Log, l => l.Contains(bomb.Id));
		}
	}
}
