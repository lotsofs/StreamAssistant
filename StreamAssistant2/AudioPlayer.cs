using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IrrKlang;
using System.IO;
using System.Diagnostics;

namespace StreamAssistant2
{
	public class AudioPlayer {
		ISoundDeviceList soundDeviceList;
		ISoundEngine soundEngine;
		ISound currentSound;

		public string[] DeviceListDescriptions;
		public int CurrentDeviceId;

		public AudioPlayer() {
			RefreshAudioDeviceList();
			SetAudioDevice();		
		}


		public void PlaySound(string fileName) {
			string soundPath = Path.Combine(@"Sounds\", fileName);
			currentSound = soundEngine.Play2D(soundPath, false);
		}


		/// <summary>
		/// Sets the audio device to play sounds from
		/// </summary>
		/// <param name="index"></param>
		public void SetAudioDevice(int index = 0) {
			soundEngine = new ISoundEngine(SoundOutputDriver.AutoDetect, SoundEngineOptionFlag.DefaultOptions, soundDeviceList.getDeviceID(index));
			CurrentDeviceId = index;
		}

		/// <summary>
		/// Refreshes the list of audio devices
		/// </summary>
		public void RefreshAudioDeviceList() {
			soundDeviceList = new ISoundDeviceList(SoundDeviceListType.PlaybackDevice);
			DeviceListDescriptions = new string[soundDeviceList.DeviceCount];
			for (int i = 0; i < soundDeviceList.DeviceCount; i++) {
				DeviceListDescriptions[i] = soundDeviceList.getDeviceDescription(i);
			}
		}
	}
}
