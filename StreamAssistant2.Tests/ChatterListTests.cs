using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using Xunit;

namespace StreamAssistant2.Tests {
	// Tests that reach Games' static state (directly, or through OBS stream start) run one at a time.
	[CollectionDefinition(Name)]
	public class GamesStateCollection {
		public const string Name = "GamesState";
	}

	// ChatterList is static, so each test starts from Reset(). Its chat message goes nowhere because
	// IRC isn't connected in tests. OBS stream start also runs the go-live game setup, so the Helix
	// fetch is stubbed to keep these tests off Twitch.
	[Collection(GamesStateCollection.Name)]
	public class ChatterListTests : IDisposable {
		readonly Func<Task<string?>> _savedFetch = Games.FetchCategoryId;

		public ChatterListTests() {
			ChatterList.Reset();
			Games.FetchCategoryId = () => Task.FromResult<string?>(null);
		}

		public void Dispose() {
			Games.FetchCategoryId = _savedFetch;
		}

		[Fact]
		public void FirstMessage_IsNew_SecondIsNot() {
			Assert.True(ChatterList.AddChatter("alice"));
			Assert.False(ChatterList.AddChatter("alice"));
		}

		[Fact]
		public void DifferentNames_AreEachNew() {
			Assert.True(ChatterList.AddChatter("alice"));
			Assert.True(ChatterList.AddChatter("bob"));
		}

		[Fact]
		public void Reset_MakesKnownChattersNewAgain() {
			ChatterList.AddChatter("alice");
			ChatterList.Reset();
			Assert.True(ChatterList.AddChatter("alice"));
		}

		[Fact]
		public void SameNameFromManyThreads_IsNewExactlyOnce() {
			int news = 0;
			Parallel.For(0, 1000, _ => {
				if (ChatterList.AddChatter("alice")) {
					Interlocked.Increment(ref news);
				}
			});
			Assert.Equal(1, news);
		}

		[Fact]
		public void ManyNamesFromManyThreads_AreEachNewOnce() {
			int news = 0;
			Parallel.For(0, 2000, i => {
				if (ChatterList.AddChatter("user" + (i % 500))) {
					Interlocked.Increment(ref news);
				}
			});
			Assert.Equal(500, news);
		}

		// The OBS handler is called directly with the event args obs-websocket would raise; no OBS connection is made.
		static void ObsStreamState(string state) {
			JObject data = new() { ["outputActive"] = state == "OBS_WEBSOCKET_OUTPUT_STARTED", ["outputState"] = state };
			ObsConnection.OnStreamStateChanged(null, new StreamStateChangedEventArgs(new OutputStateChanged(data)));
		}

		[Fact]
		public void ObsEventArgs_ParseTheState() {
			JObject data = new() { ["outputActive"] = true, ["outputState"] = "OBS_WEBSOCKET_OUTPUT_STARTED" };
			Assert.Equal(OutputState.OBS_WEBSOCKET_OUTPUT_STARTED, new OutputStateChanged(data).State);
		}

		[Fact]
		public void ObsStreamStarted_ResetsList() {
			ChatterList.AddChatter("alice");
			ObsStreamState("OBS_WEBSOCKET_OUTPUT_STARTED");
			Assert.True(ChatterList.AddChatter("alice"));
		}

		[Theory]
		[InlineData("OBS_WEBSOCKET_OUTPUT_STARTING")]
		[InlineData("OBS_WEBSOCKET_OUTPUT_STOPPING")]
		[InlineData("OBS_WEBSOCKET_OUTPUT_STOPPED")]
		public void ObsOtherStates_DoNotReset(string state) {
			ChatterList.AddChatter("alice");
			ObsStreamState(state);
			Assert.False(ChatterList.AddChatter("alice"));
		}

		[Fact]
		public void AddingWhileResetting_DoesNotThrow() {
			Parallel.For(0, 2000, i => {
				if (i % 50 == 0) {
					ChatterList.Reset();
				}
				else {
					ChatterList.AddChatter("user" + i);
				}
			});
		}
	}
}
