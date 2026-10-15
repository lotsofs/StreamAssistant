using System.Globalization;
using Xunit;

namespace StreamAssistant2.Tests {
	public class ClockTests {
		[Fact]
		public void ClockText_IsIsoDateAndTimeWhateverTheCultureSeparators() {
			CultureInfo previous = CultureInfo.CurrentCulture;
			CultureInfo odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			odd.DateTimeFormat.TimeSeparator = ".";
			odd.DateTimeFormat.DateSeparator = "/";
			try {
				CultureInfo.CurrentCulture = odd;
				Assert.Equal("2026-10-10 09:05", Clock.ClockText(new DateTime(2026, 10, 10, 9, 5, 59)));
			}
			finally {
				CultureInfo.CurrentCulture = previous;
			}
		}
	}
}
