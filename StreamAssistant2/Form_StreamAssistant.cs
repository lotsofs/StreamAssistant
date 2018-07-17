using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StreamAssistant2
{
	public partial class Form_StreamAssistant : Form
	{
		AudioPlayer audioPlayer;
		Form_NotificationsSettings notificationsConfig;

		public Form_StreamAssistant() {
			InitializeComponent();
			audioPlayer = new AudioPlayer();

			System.Collections.Specialized.NameValueCollection appSettings = ConfigurationManager.AppSettings;
			notifications_enabledCheckBox.Checked = bool.Parse(appSettings["NotificationsEnabled"]);
		}

		public void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.AppSettings.Settings["NotificationsEnabled"].Value = notifications_enabledCheckBox.Checked.ToString();

			config.Save();
		}

		#region stats display

		private void statsDisplay_EnabledCheckBox_CheckedChanged(object sender, EventArgs e) {
			statsDisplay_ComboBox.Enabled = statsDisplay_EnabledCheckBox.Checked;
		}

		#endregion

		#region notifications

		/// <summary>
		/// Opens the notifications configuration panel
		/// </summary>
		public void OpenNotificationsConfig() {
			notificationsConfig = new Form_NotificationsSettings(audioPlayer);
			notificationsConfig.Show();
		}

		private void notifications_ConfigButton_Click(object sender, EventArgs e) {
			OpenNotificationsConfig();
		}

		private void notifications_enabledCheckBox_CheckedChanged(object sender, EventArgs e) {
			notifications_ConfigButton.Enabled = notifications_enabledCheckBox.Checked;
		}

		#endregion

		private void Form_StreamAssistant_FormClosing(object sender, FormClosingEventArgs e) {
			if (notificationsConfig != null) {
				notificationsConfig.Close();
			}
			SaveSettings();
		}
	}
}
