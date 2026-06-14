using NAudio.Wave;

namespace StreamAssistant2 {
	internal static class Sound {

		internal enum Sounds {
			TribalHymn,
			TheClap,
			Team17Applauds,
			IndianAnthem,
			Flush,
			Warning,
		}

		private const string SOUNDS_FOLDER = "D:\\Repositories\\Stream-Resources\\Alert Sounds\\";

		static void PlaySound(string file, float volume = 1.0f) {
			var audioFile = new AudioFileReader(file);
			var outputDevice = new WaveOutEvent();

			audioFile.Volume = volume;
			
			outputDevice.Init(audioFile);
			outputDevice.PlaybackStopped += (_, _) => {
				outputDevice.Dispose();
				audioFile.Dispose();
			};

			outputDevice.Play();

		}

		internal static void PlaySound(Sounds s) {
			switch (s) {
				case Sounds.TribalHymn:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "Tribal hymn.mp3"), 0.5f);
					break;
				case Sounds.TheClap:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "theclap.mp3"), 0.4f);
					break;
				case Sounds.Team17Applauds:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "Team17-Applauds.mp3"), 0.75f);
					break;
				case Sounds.IndianAnthem:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "IndianAnthem.mp3"), 0.4f);
					break;
				case Sounds.Flush:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "Flush.wav"), 0.4f);
					break;
				case Sounds.Warning:
					PlaySound(Path.Combine(SOUNDS_FOLDER, "Warning.wav"), 0.6f);
					break;
				default:
					break;
			}
		}

		internal static void PlaySoundDelayed(Sounds s, int delayInMs) {
			Clock.AddJob(new Clock.ScheduledJob {
				GetNextRun = () => {
					DateTime now = DateTime.UtcNow;
					DateTime next = now + TimeSpan.FromMilliseconds(delayInMs);
					return next;
				},
				Action = async () => { 
					PlaySound(s);
					await Task.CompletedTask; 
				}
			});
		}

	}
}
