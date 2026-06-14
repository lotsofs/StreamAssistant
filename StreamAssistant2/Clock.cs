
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	 internal class Clock {
		public class ScheduledJob {
			public Func<DateTime> GetNextRun;
			public Func<Task> Action;
			public DateTime NextRun;
			public bool Repeat;
		}

		private static TaskCompletionSource<bool> _wakeUp = new();

		private static List<ScheduledJob> _jobs = new List<ScheduledJob>();

		public static void Start() {
			_ = RunScheduler();
		}

		public static void AddJob(ScheduledJob job) {
			job.NextRun = job.GetNextRun();
			_jobs.Add(job);
			_wakeUp.TrySetResult(true);
		}

		static async Task RunScheduler() {
			while (true) {
				if (_jobs.Count == 0) {
					await Task.Delay(1000);
					continue;
				}

				var nextJob = _jobs.OrderBy(j => j.NextRun).First();
				
				var delay = nextJob.NextRun - DateTime.UtcNow;
				if (delay < TimeSpan.Zero) {
					delay = TimeSpan.Zero;
				}
				
				var delayTask = Task.Delay(delay);
				var wakeTask = _wakeUp.Task;
				var finished = await Task.WhenAny(delayTask, wakeTask);
				if (finished == wakeTask) {
					_wakeUp = new TaskCompletionSource<bool>();
					continue;
				}

				try {
					await nextJob.Action();
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error 6");
					ConsoleLogger.LogToFile(ex);
					// TODO: print something.
				}

				if (nextJob.Repeat) {
					nextJob.NextRun = nextJob.GetNextRun();
				}
				else {
					_jobs.Remove(nextJob);
				}
			}
		}

		public static void AddGenericJobs() {
			ScheduledJob diskNotify = new ScheduledJob {
				Repeat = true,
				GetNextRun = () => {
					DateTime now = DateTime.UtcNow;
					DateTime next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
					if (next <= now) {
						next = next.AddMinutes(1);
					}
					return next;
				},
				Action = async () => { 
					DiskSpace.CheckSpaceAndNotify();
					await Task.CompletedTask; 
				}
			};
			ScheduledJob clockCheck = new ScheduledJob {
				Repeat = true,
				GetNextRun = () => {
					DateTime now = DateTime.UtcNow;
					DateTime next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
					if (next <= now) {
						next = next.AddMinutes(1);
					}
					return next;
				},
				Action = async () => { 
					TwitchUptime.ClockCheck();
					await Task.CompletedTask; 
				}
			};
			ScheduledJob uptimeCheck = new ScheduledJob {
				Repeat = true,
				GetNextRun = () => {
					DateTime now = DateTime.UtcNow;
					DateTime next = now.AddSeconds(TwitchUptime.SecondsUntilNextMinute);
					return next;
				},
				Action = async () => { 
					await TwitchUptime.UptimeCheck();
					await Task.CompletedTask; 
				}
			};
			AddJob(diskNotify);
			AddJob(clockCheck);
			AddJob(uptimeCheck);
		}


	}
}
