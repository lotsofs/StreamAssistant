using System.Collections.Concurrent;
using System.Diagnostics;
using System.Speech.Synthesis;

namespace StreamAssistant2 {
	internal static class TextToSpeech {
		class SpeechItem(string text) {
			public string Text = text;
			public TaskCompletionSource Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		static readonly SpeechSynthesizer synth = new();
		static readonly ConcurrentQueue<SpeechItem> queue = new();
		static readonly SemaphoreSlim signal = new(0);
		static readonly CancellationTokenSource cts = new();
		
		static readonly Task worker;
		
		static TextToSpeech() {
			synth.SelectVoice("Microsoft Catherine");
			worker = Task.Run(ProcessQueueAsync);
		}

		public static void EnqueueSpeech(string text) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"TTS Enqueue: {text}");
			_ = SpeakAsync(text);
		}

		public static Task SpeakAsync(string text) {
			if (string.IsNullOrWhiteSpace(text)) {
				return Task.CompletedTask;
			}

			SpeechItem speechItem = new SpeechItem(text);
			queue.Enqueue(speechItem);

			signal.Release();
			return speechItem.Tcs.Task;
		}

		static async Task ProcessQueueAsync() {
			while (!cts.IsCancellationRequested) {
				await signal.WaitAsync(cts.Token).ConfigureAwait(false);

				if (!queue.TryDequeue(out var item)) {
					continue;
				}

				try {
					await SpeakInternalAsync(item.Text, cts.Token).ConfigureAwait(false);
					item.Tcs.TrySetResult();
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error 5");
					ConsoleLogger.LogToFile(ex);
					item.Tcs.TrySetException(ex);
				}
			}
		}

		static Task SpeakInternalAsync(string text, CancellationToken token) {
			return Task.Run(() => {
				var done = new ManualResetEvent(false);
				Exception? error = null;

				void Handler(object? s, SpeakCompletedEventArgs e) {
					synth.SpeakCompleted -= Handler;
					error = e.Error;
					done.Set();
				}

				synth.SpeakCompleted += Handler;
				synth.SpeakAsync(text);
				ConsoleLogger.LogToFile($"TTS Speaking: {text}");
				done.WaitOne();
				if (error != null) {
					throw error;
				}
			}, token);
		}

		internal static void StopSpeech() {
			synth.SpeakAsyncCancelAll();
		}

		internal static void PurgeQueue() {
			queue.Clear();
		}

		internal static void Dispose() {
			cts.Cancel();
			synth.Dispose();
			signal.Dispose();
			cts.Dispose();
		}

		public static void ReportStart() {
			EnqueueSpeech("Text to speech system online");
		}

	}
}
