using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using IrrKlang;

namespace StreamAssistant2
{
	public partial class Form_NotificationsSettings : Form
	{
		AudioPlayer audioPlayer;

		public Form_NotificationsSettings(AudioPlayer ap) {
			InitializeComponent();
			audioPlayer = ap;

			Notifications_AudioDeviceComboBox_Update();
			Notifications_SoundsComboBox_Update();
		}

		/// <summary>
		/// Fills the audio devices combo box with a list of audio devices
		/// </summary>
		public void Notifications_AudioDeviceComboBox_Update() {
			audioDevice_ComboBox.Items.Clear();
			for (int i = 0; i < audioPlayer.DeviceListDescriptions.Length; i++) {
				audioDevice_ComboBox.Items.Add(audioPlayer.DeviceListDescriptions[i]);
			}
			audioDevice_ComboBox.SelectedIndex = audioPlayer.CurrentDeviceId;
		}

		/// <summary>
		/// Fills the sounds combo box with a list of available sounds in the Sounds folder
		/// </summary>
		public void Notifications_SoundsComboBox_Update() {
			string[] files = Directory.GetFiles(@"Sounds\");

			testSound_comboBox.Items.Clear();
			for (int i = 0; i < files.Length; i++) {
				files[i] = Path.GetFileName(files[i]);
				testSound_comboBox.Items.Add(files[i]);
			}
			testSound_comboBox.SelectedIndex = 0;
		}

		/// <summary>
		/// Applies the settings and saves
		/// </summary>
		void ApplySettings() {
			audioPlayer.SetAudioDevice(audioDevice_ComboBox.SelectedIndex);
		}

		#region bottom buttons
		private void button_Apply_Click(object sender, EventArgs e) {
			ApplySettings();
		}

		private void button_Ok_Click(object sender, EventArgs e) {
			ApplySettings();
			this.Close();
		}

		private void button1_Click(object sender, EventArgs e) {
			this.Close();
		}
		#endregion

		private void testSound_Button_Click(object sender, EventArgs e) {
			string sound = testSound_comboBox.SelectedItem.ToString();
			audioPlayer.PlaySound(sound);
		}
	}
}
