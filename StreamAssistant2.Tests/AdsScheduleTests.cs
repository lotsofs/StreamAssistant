using Xunit;

namespace StreamAssistant2.Tests {
	public class AdsScheduleTests {
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

		static (TimeSpan At, Action Send) Entry(int ms, Action send) => (TimeSpan.FromMilliseconds(ms), send);

		[Fact]
		public async Task EntriesFireInOrder() {
			List<int> fired = [];
			await Ads.RunScheduleAsync([
				Entry(20, () => fired.Add(1)),
				Entry(40, () => fired.Add(2)),
				Entry(60, () => fired.Add(3)),
			], CancellationToken.None).WaitAsync(Timeout);
			Assert.Equal([1, 2, 3], fired);
		}

		[Fact]
		public async Task CancellingStopsTheRest() {
			List<int> fired = [];
			TaskCompletionSource firstFired = new(TaskCreationOptions.RunContinuationsAsynchronously);
			using CancellationTokenSource cts = new();
			Task run = Ads.RunScheduleAsync([
				Entry(10, () => { fired.Add(1); firstFired.SetResult(); }),
				Entry(60_000, () => fired.Add(2)),
			], cts.Token);

			await firstFired.Task.WaitAsync(Timeout);
			cts.Cancel();
			await run.WaitAsync(Timeout);
			Assert.Equal([1], fired);
		}

		[Fact]
		public async Task AThrowingSendDoesNotStopLaterOnes() {
			List<int> fired = [];
			await Ads.RunScheduleAsync([
				Entry(10, () => throw new InvalidOperationException("boom")),
				Entry(30, () => fired.Add(2)),
			], CancellationToken.None).WaitAsync(Timeout);
			Assert.Equal([2], fired);
		}

		[Fact]
		public async Task CancellingOneScheduleLeavesOtherWorkRunning() {
			int heartbeat = 0;
			using CancellationTokenSource stopHeartbeat = new();
			Task beat = Task.Run(async () => {
				while (!stopHeartbeat.IsCancellationRequested) {
					Interlocked.Increment(ref heartbeat);
					await Task.Delay(20);
				}
			});

			using CancellationTokenSource cts = new();
			Task run = Ads.RunScheduleAsync([Entry(50, () => { })], cts.Token);
			cts.Cancel();
			await run.WaitAsync(Timeout);

			await Task.Delay(300);
			int afterCancel = Volatile.Read(ref heartbeat);
			await Task.Delay(300);
			Assert.True(Volatile.Read(ref heartbeat) > afterCancel);

			stopHeartbeat.Cancel();
			await beat.WaitAsync(Timeout);
		}

		[Fact]
		public async Task AnAlreadyCancelledTokenFiresNothing() {
			List<int> fired = [];
			using CancellationTokenSource cts = new();
			cts.Cancel();
			await Ads.RunScheduleAsync([Entry(0, () => fired.Add(1))], cts.Token).WaitAsync(Timeout);
			Assert.Empty(fired);
		}
	}
}
