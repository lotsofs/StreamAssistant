using System.Globalization;

namespace StreamAssistant2 {
	// Tips' running total toward a goal, kept in the database and shown in OBS
	internal static class MoneyGoal {
		// The goal and total's currency
		static string Currency => Settings.Data.MoneyGoal.Currency;

		// The goal on a first start, in cents
		internal static long DefaultGoalCents => ToCents(Settings.Data.MoneyGoal.DefaultGoal);

		// Adds a tip to the total and shows it
		internal static void Credit(string user, decimal amount, string currency) {
			FireForget.Run("MON1", "money credit", () => CreditAsync(user, amount, currency));
		}

		// Adds the tip in cents, logs it and shows the new total
		static async Task CreditAsync(string user, decimal amount, string currency) {
			long cents = ToCents(amount);
			if (cents <= 0) {
				return;
			}
			await Database.AddMoneyAsync(cents);
			var (current, goal) = await Database.GetMoneyAsync();
			if (string.IsNullOrWhiteSpace(currency) || currency.Equals(Currency, StringComparison.OrdinalIgnoreCase)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Money goal: +{Amount(cents)} {Currency} from {user}, now {Text(current, goal)}");
			}
			else {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Money goal: +{Amount(cents)} {currency} from {user}, counted as {Amount(cents)} {Currency}, now {Text(current, goal)}");
			}
			Show(current, goal);
		}

		// Sets the goal from "!moneygoal <amount>" and shows it
		internal static void TrySetGoal(string argument) {
			long? cents = ParseGoal(argument);
			if (cents == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Not a money goal: \"{argument}\" (try !moneygoal 500)");
				return;
			}
			FireForget.Run("MON2", "money goal change", async () => {
				await Database.SetMoneyGoalAsync(cents.Value);
				var (current, goal) = await Database.GetMoneyAsync();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Money goal set to {Amount(goal)} {Currency}, now {Text(current, goal)}");
				Show(current, goal);
			});
		}

		// Sets the total back to 0 and shows it
		internal static void Reset() {
			FireForget.Run("MON2", "money total reset", async () => {
				var (was, _) = await Database.GetMoneyAsync();
				await Database.ResetMoneyAsync();
				var (current, goal) = await Database.GetMoneyAsync();
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Money total reset (was {Amount(was)} {Currency}), now {Text(current, goal)}");
				Show(current, goal);
			});
		}

		// On an OBS connect: shows the total again, off the connecting thread
		internal static void OnObsConnected() {
			FireForget.Run("MON3", "money goal on OBS connect", async () => {
				await Task.Yield();
				var (current, goal) = await Database.GetMoneyAsync();
				Show(current, goal);
			});
		}

		// Writes the OBS text when OBS is ready; an OBS failure is logged, not thrown
		static void Show(long current, long goal) {
			if (!ObsConnection.IsReady) {
				return;
			}
			string source = Settings.Data.MoneyGoal.Source;
			try {
				Obs.SetInputSetting(source, "text", Text(current, goal));
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {source}: {ex.Message}");
			}
		}

		// Whole cents, half away from zero
		internal static long ToCents(decimal amount) {
			return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
		}

		// "5", or "4.20" with two decimals
		static string Amount(long cents) {
			return Tips.FormatAmount(cents / 100m);
		}

		// "<current> / <goal> <currency>"
		internal static string Text(long currentCents, long goalCents) {
			return $"{Amount(currentCents)} / {Amount(goalCents)} {Currency}";
		}

		// Cents for a positive amount with a dot or comma decimal, or null
		internal static long? ParseGoal(string text) {
			string number = text.Trim().Replace(',', '.');
			if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount)) {
				return null;
			}
			try {
				long cents = ToCents(amount);
				return cents > 0 ? cents : null;
			}
			catch (OverflowException) {
				return null;
			}
		}
	}
}
