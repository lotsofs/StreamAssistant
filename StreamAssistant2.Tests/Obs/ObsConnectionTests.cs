using Xunit;

namespace StreamAssistant2.Tests {
	public class ObsConnectionTests {
		[Fact]
		public void DisconnectLine_ADrop_IsLogged_AndLeavesAnUnreachableObsQuiet() {
			Assert.Equal(("Lost connection to OBS: Server stopping.", ""), ObsConnection.DisconnectLine(true, "Server stopping.", null));
			Assert.Equal((null, ""), ObsConnection.DisconnectLine(false, "", ""));
		}

		[Fact]
		public void DisconnectLine_OBSClosedAtStart_IsLoggedOnce() {
			var (line, last) = ObsConnection.DisconnectLine(false, "", null);
			Assert.Equal("Can't reach OBS, connecting when it's up", line);
			Assert.Equal((null, ""), ObsConnection.DisconnectLine(false, "", last));
		}

		[Fact]
		public void DisconnectLine_AFailureWithAReason_IsLoggedOnce_AndANewOneAgain() {
			var (line, last) = ObsConnection.DisconnectLine(false, "Authentication failed.", "");
			Assert.Equal("Can't connect to OBS: Authentication failed.", line);
			Assert.Equal((null, "Authentication failed."), ObsConnection.DisconnectLine(false, "Authentication failed.", last));
			Assert.Equal(("Can't reach OBS, connecting when it's up", ""), ObsConnection.DisconnectLine(false, "", last));
		}

		[Fact]
		public void DisconnectLine_ADropDuringFailures_IsAlwaysLogged() {
			Assert.Equal(("Lost connection to OBS: ", ""), ObsConnection.DisconnectLine(true, "", ""));
		}
	}
}
