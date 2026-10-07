using Xunit;

namespace StreamAssistant2.Tests {
	public class FireForgetTests {
		static async Task<List<string>> Capture(Action act) {
			List<string> lines = new();
			Action<ConsoleLogger.ColorType, string> sink = (_, line) => { lock (lines) lines.Add(line); };
			ConsoleLogger.LineLogged += sink;
			try {
				act();
				await Task.Delay(300);
			}
			finally {
				ConsoleLogger.LineLogged -= sink;
			}
			lock (lines) return lines.ToList();
		}

		[Fact]
		public async Task ThrowAfterAwait_LogsTeh2() {
			var lines = await Capture(() => FireForget.Run("T1", "after", async () => {
				await Task.Yield();
				throw new InvalidOperationException("boom");
			}));
			Assert.Contains(lines, l => l.Contains("Error T1") && l.Contains("after") && l.Contains("boom"));
		}

		[Fact]
		public async Task Line_NamesCodeAndWhat() {
			var lines = await Capture(() => FireForget.Run("X9", "thing", () => throw new InvalidOperationException("bad")));
			Assert.Contains(lines, l => l.Contains("Error X9: thing failed: bad"));
		}

		[Fact]
		public async Task ThrowBeforeFirstAwait_LogsTeh2() {
			var lines = await Capture(() => FireForget.Run("T1", "sync", () => throw new InvalidOperationException("early")));
			Assert.Contains(lines, l => l.Contains("Error T1") && l.Contains("early"));
		}

		[Fact]
		public void SynchronousPart_RunsOnCallingThreadBeforeRunReturns() {
			int callerThread = Environment.CurrentManagedThreadId;
			int handlerThread = -1;
			bool ran = false;
			FireForget.Run("T1", "thread", async () => {
				handlerThread = Environment.CurrentManagedThreadId;
				ran = true;
				await Task.Delay(50);
			});
			Assert.True(ran);
			Assert.Equal(callerThread, handlerThread);
		}
	}
}
