using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Configuration;
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
		
		#region initialize

		public Form_NotificationsSettings(AudioPlayer ap) {
			InitializeComponent();
			audioPlayer = ap;

			Notifications_AudioDeviceComboBox_Update();
			Notifications_SoundsComboBoxes_Update();
			Notifications_VolumeTexts_Update();
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
		public void Notifications_SoundsComboBoxes_Update() {
			string[] files = Directory.GetFiles(@"Sounds\");

			// fill comboboxes with options
			for (int i = 0; i < files.Length; i++) {
				files[i] = Path.GetFileName(files[i]);

				subscription_comboBox.Items.Add(files[i]);
				bits_ComboBox.Items.Add(files[i]);
				donation_ComboBox.Items.Add(files[i]);
			}

			// set the combobox selected item to what it should be
			subscription_comboBox.SelectedItem = audioPlayer.soundFiles[(int)TwitchEvents.Subscription];
			bits_ComboBox.SelectedItem = audioPlayer.soundFiles[(int)TwitchEvents.Bits];
			donation_ComboBox.SelectedItem = audioPlayer.soundFiles[(int)TwitchEvents.Donation];
			ValidateSounds();
		}

		/// <summary>
		/// Set the proper value of the volume boxes
		/// </summary>
		public void Notifications_VolumeTexts_Update() {
			subscription_VolumeText.Text = audioPlayer.volumes[(int)TwitchEvents.Subscription].ToString();
			bits_VolumeText.Text = audioPlayer.volumes[(int)TwitchEvents.Bits].ToString();
			donation_VolumeText.Text = audioPlayer.volumes[(int)TwitchEvents.Donation].ToString();
		}

		#endregion

		#region Apply settings

		/// <summary>
		/// Applies the settings and saves
		/// </summary>
		void ApplySettings() {
			audioPlayer.SetAudioDevice(audioDevice_ComboBox.SelectedIndex);

			audioPlayer.soundFiles[(int)TwitchEvents.Subscription] = subscription_comboBox.SelectedItem.ToString();
			audioPlayer.soundFiles[(int)TwitchEvents.Bits] = bits_ComboBox.SelectedItem.ToString();
			audioPlayer.soundFiles[(int)TwitchEvents.Donation] = donation_ComboBox.SelectedItem.ToString();

			audioPlayer.volumes[(int)TwitchEvents.Subscription] = int.Parse(subscription_VolumeText.Text);
			audioPlayer.volumes[(int)TwitchEvents.Bits] = int.Parse(bits_VolumeText.Text);
			audioPlayer.volumes[(int)TwitchEvents.Donation] = int.Parse(donation_VolumeText.Text);
		}

		/// <summary>
		/// Save settings to file
		/// </summary>
		void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.AppSettings.Settings["SoundDevice"].Value = audioPlayer.CurrentDeviceId.ToString();

			config.AppSettings.Settings["SubscriptionSound"].Value = audioPlayer.soundFiles[(int)TwitchEvents.Subscription];
			config.AppSettings.Settings["BitsSound"].Value = audioPlayer.soundFiles[(int)TwitchEvents.Bits];
			config.AppSettings.Settings["DonationSound"].Value = audioPlayer.soundFiles[(int)TwitchEvents.Donation];

			config.AppSettings.Settings["SubscriptionVolume"].Value = audioPlayer.volumes[(int)TwitchEvents.Subscription].ToString();
			config.AppSettings.Settings["BitsVolume"].Value = audioPlayer.volumes[(int)TwitchEvents.Bits].ToString();
			config.AppSettings.Settings["DonationVolume"].Value = audioPlayer.volumes[(int)TwitchEvents.Donation].ToString();

			config.Save();
		}

		#endregion

		#region data validation

		/// <summary>
		/// Checks if the value in the specified textbox is a valid integer, and clamps it between 0 and 100%
		/// </summary>
		/// <param name="textBox"></param>
		void ValidateVolume(TextBox textBox) {
			int vol;
			if (!int.TryParse(textBox.Text, out vol)) {
				vol = 0;
			}
			else {
				vol = Math.Max(0, Math.Min(100, vol));
			}
			textBox.Text = vol.ToString();
		}

		/// <summary>
		/// Checks if the values in the sounds comboboxes are valid items
		/// </summary>
		void ValidateSounds() {
			if (subscription_comboBox.SelectedIndex == -1) {
				subscription_comboBox.SelectedIndex = 0;
			}
			if (bits_ComboBox.SelectedIndex == -1) {
				bits_ComboBox.SelectedIndex = 0;
			}
			if (donation_ComboBox.SelectedIndex == -1) {
				donation_ComboBox.SelectedIndex = 0;
			}
		}

		#endregion

		#region bottom buttons
		private void button_Apply_Click(object sender, EventArgs e) {
			ApplySettings();
			SaveSettings();
		}

		private void button_Ok_Click(object sender, EventArgs e) {
			ApplySettings();
			SaveSettings();
			this.Close();
		}

		private void button1_Click(object sender, EventArgs e) {
			this.Close();
		}
		#endregion

		#region play buttons

		private void subscription_Button_Click(object sender, EventArgs e) {
			string sound = subscription_comboBox.SelectedItem.ToString();
			int volume = int.Parse(subscription_VolumeText.Text);
			audioPlayer.PlaySound(sound, volume);
		}

		private void bits_Button_Click(object sender, EventArgs e) {
			string sound = bits_ComboBox.SelectedItem.ToString();
			int volume = int.Parse(bits_VolumeText.Text);
			audioPlayer.PlaySound(sound, volume);
		}

		private void donation_Button_Click(object sender, EventArgs e) {
			string sound = donation_ComboBox.SelectedItem.ToString();
			int volume = int.Parse(donation_VolumeText.Text);
			audioPlayer.PlaySound(sound, volume);
		}

		#endregion

		#region volume input boxes

		private void subscription_VolumeText_Leave(object sender, EventArgs e) {
			ValidateVolume(subscription_VolumeText);
		}

		private void bits_VolumeText_Leave(object sender, EventArgs e) {
			ValidateVolume(bits_VolumeText);

		}

		private void donation_VolumeText_Leave(object sender, EventArgs e) {
			ValidateVolume(donation_VolumeText);
		}

		#endregion
	}
}
