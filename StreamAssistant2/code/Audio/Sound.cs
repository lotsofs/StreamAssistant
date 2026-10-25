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

		/// <summary>
		/// Plays the sound's file from the Sounds directory at its volume; logs instead when the directory isn't set.
		/// </summary>
		internal static void PlaySound(Sounds s) {
			string folder = Settings.Data.Directories.Sounds;
			if (folder.Length == 0) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Sounds not set in settings.json, {s} not played");
				return;
			}
			SoundSettings sound = SettingsFor(s, Settings.Data.Sounds);
			PlaySound(Path.Combine(folder, sound.File), (float)sound.Volume);
		}

		/// <summary>
		/// The sound's file and volume.
		/// </summary>
		internal static SoundSettings SettingsFor(Sounds s, SoundsSettings sounds) => s switch {
			Sounds.TribalHymn => sounds.TribalHymn,
			Sounds.TheClap => sounds.TheClap,
			Sounds.Team17Applauds => sounds.Team17Applauds,
			Sounds.IndianAnthem => sounds.IndianAnthem,
			Sounds.Flush => sounds.Flush,
			Sounds.Warning => sounds.Warning,
			_ => throw new ArgumentOutOfRangeException(nameof(s)),
		};

	}
}
