namespace StreamAssistant2 {
	// The spoken sentences for subs, resubs, gift subs and gift bombs
	internal static class SubscriptionMessages {
		// "foo subscribed at tier 2. It is a 3 month sub."
		internal static string BuildSubMessage(string user, int tier, bool isPrime, int months) {
			string msg = $"{user} subscribed{TierPhrase(tier, isPrime)}";
			if (months > 1) {
				msg += $". It is a {months} month sub.";
			}
			return msg;
		}

		// "foo subscribed with prime, They've subscribed for 12 months, Currently on a 5 month streak: hello"
		internal static string BuildResubMessage(string user, int tier, bool isPrime, int cumulativeMonths, int streakMonths, string text) {
			string msg = $"{user} subscribed{TierPhrase(tier, isPrime)}";
			if (cumulativeMonths > 1) {
				msg += $", They've subscribed for {cumulativeMonths} months";
			}
			if (streakMonths > 1) {
				msg += $", Currently on a {streakMonths} month streak";
			}
			if (!string.IsNullOrEmpty(text)) {
				msg += $": {text}";
			}
			return msg;
		}

		// "foo gifted a tier 1 sub to bar. They have given 4 gift subs in the channel, It is a 3 month gift"
		internal static string BuildGiftMessage(string gifter, int tier, string recipient, int cumulativeTotal, int months) {
			string msg = $"{gifter} gifted a tier {tier} sub to {recipient}";
			if (cumulativeTotal > 1) {
				msg += $". They have given {cumulativeTotal} gift subs in the channel";
			}
			else if (cumulativeTotal == 1) {
				msg += ". This is their first gift sub in the channel";
			}
			if (months > 1) {
				msg += $", It is a {months} month gift";
			}
			return msg;
		}

		// "foo is gifting 5 tier 3 subs to Lots Of Ess's community! ... Congratulations to: a, b, and 3 other people"
		internal static string BuildBombMessage(string gifter, int total, int tier, int cumulativeTotal, IEnumerable<string> recipients) {
			string tierText = tier > 1 ? $"tier {tier} " : "";
			string msg = $"{gifter} is gifting {total} {tierText}subs to Lots Of Ess's community! ";
			if (cumulativeTotal > total) {
				msg += $"They've gifted a total of {cumulativeTotal} in the channel! ";
			}
			msg += "Congratulations to: ";
			int named = 0;
			foreach (string name in recipients) {
				msg += $"{name}, ";
				named++;
			}
			int missing = total - named;
			if (missing > 0) {
				string people = missing == 1 ? "person" : "people";
				msg += named > 0 ? $"and {missing} other {people}" : $"{missing} {people}";
			}
			return msg;
		}

		// "🛢️ 2 people received a gift sub, but no gift bomb arrived: a, b"
		internal static string BuildGiftlessMessage(IReadOnlyCollection<string> recipients) {
			string people = recipients.Count == 1 ? "person" : "people";
			return $"🛢️ {recipients.Count} {people} received a gift sub, but no gift bomb arrived: {string.Join(", ", recipients)}";
		}

		// " with prime", " at tier 2", or nothing for tier 1
		static string TierPhrase(int tier, bool isPrime) {
			if (isPrime) {
				return " with prime";
			}
			return tier > 1 ? $" at tier {tier}" : "";
		}
	}
}
