//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using IrrKlang;
//using System.IO;
//using System.Diagnostics;
//using System.Configuration;

//namespace StreamAssistant2
//{
//	public class AudioPlayer : ISaveable {
//		ISoundDeviceList soundDeviceList;
//		ISoundEngine soundEngine;
//		ISound currentSound;

//		public string[] SoundFiles;
//		public int[] Volumes;

//		public string[] DeviceListDescriptions;
//		public int CurrentDeviceId;

//		public AudioPlayer() {
//			RefreshAudioDeviceList();
//			SetAudioDevice();

//			int eventsLength = Enum.GetValues(typeof(TwitchEvents)).Length;
//			SoundFiles = new string[eventsLength];
//			Volumes = new int[eventsLength];

//			LoadSettings();
//		}

//		#region save load

//		public void SaveSettings() {
//			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

//			config.AppSettings.Settings["SoundDevice"].Value = CurrentDeviceId.ToString();

//			config.Save();
//		}

//		public void LoadSettings() {
//			SaveLoad.OnSave += SaveSettings;
//			System.Collections.Specialized.NameValueCollection appSettings = ConfigurationManager.AppSettings;

//			int audioDeviceIndex = int.Parse(appSettings["SoundDevice"]);
//			SetAudioDevice(audioDeviceIndex);
//		}

//		#endregion

//		/// <summary>
//		/// Plays a sound
//		/// </summary>
//		/// <param name="fileName">Name of the soundfile in Sounds/ folder (without the path)</param>
//		/// <param name="volume">Volume in %</param>
//		public void PlaySound(string fileName, int volume) {
//			if (fileName == "None") {
//				return;
//			}
//			string soundPath = Path.Combine(@"Sounds\", fileName);
//			currentSound = soundEngine.Play2D(soundPath, false, true);
//			currentSound.Volume = (float)volume / 100;
//			currentSound.Paused = false;
//		}

//		/// <summary>
//		/// Sets the audio device to play sounds from
//		/// </summary>
//		/// <param name="index"></param>
//		public void SetAudioDevice(int index = 0) {
//			soundEngine = new ISoundEngine(SoundOutputDriver.AutoDetect, SoundEngineOptionFlag.DefaultOptions, soundDeviceList.getDeviceID(index));
//			CurrentDeviceId = index;
//		}

//		/// <summary>
//		/// Refreshes the list of audio devices
//		/// </summary>
//		public void RefreshAudioDeviceList() {
//			soundDeviceList = new ISoundDeviceList(SoundDeviceListType.PlaybackDevice);
//			DeviceListDescriptions = new string[soundDeviceList.DeviceCount];
//			for (int i = 0; i < soundDeviceList.DeviceCount; i++) {
//				DeviceListDescriptions[i] = soundDeviceList.getDeviceDescription(i);
//			}
//		}
//	}
//}
