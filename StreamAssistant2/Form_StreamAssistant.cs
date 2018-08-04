using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Specialized;

namespace StreamAssistant2
{
	public partial class Form_StreamAssistant : Form, ISaveable
	{
		AudioPlayer audioPlayer;
		Notifications notifications;
		EventLog eventLog;

		Form_NotificationsSettings notificationsConfig;
		Form_NotificationsViewer notificationsViewer;

		string notificationTextFilesPath;

		bool fileSystemWatchersEnableable;

		#region opening closing

		public Form_StreamAssistant() {
			InitializeComponent();
			audioPlayer = new AudioPlayer();
			eventLog = new EventLog();
			notifications = new Notifications(audioPlayer, eventLog);

			LoadSettings();
		}

		/// <summary>
		/// Form closing
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Form_StreamAssistant_FormClosing(object sender, FormClosingEventArgs e) {
			CloseNotificationsWindows();
			SaveLoad.SaveAll();
		}

		#endregion

		#region save load

		/// <summary>
		/// Loads settings
		/// </summary>
		public void LoadSettings() {
			SaveLoad.OnSave += SaveSettings;

			NameValueCollection appSettings = ConfigurationManager.AppSettings;
			SetFileSystemWatcherPaths(appSettings["NotificationTextFilesPath"]);

			notifications_enabledCheckBox.Checked = bool.Parse(appSettings["NotificationsEnabled"]);
		}

		/// <summary>
		/// Save settings to file
		/// </summary>
		public void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.AppSettings.Settings["NotificationsEnabled"].Value = notifications_enabledCheckBox.Checked.ToString();
			config.AppSettings.Settings["NotificationTextFilesPath"].Value = notificationTextFilesPath;

			config.Save();
		}

		#endregion

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
			notificationsConfig = new Form_NotificationsSettings(audioPlayer, notificationTextFilesPath, eventLog);
			notificationsConfig.Show();
			notificationsConfig.OnTextFilesPathChanged += SetFileSystemWatcherPaths;
		}

		/// <summary>
		/// Opens the viewer panel for showing donation text
		/// </summary>
		public void OpenNotificationsViewer() {
			notificationsViewer = new Form_NotificationsViewer(notifications);
			notificationsViewer.Show();
			notifications.OnDonation += notificationsViewer.UpdateText;
		}

		/// <summary>
		/// Closes all notifcations windows
		/// </summary>
		public void CloseNotificationsWindows() {
			if (notificationsConfig != null) {
				notificationsConfig.Close();
			}
			if (notificationsViewer != null) {
				notificationsViewer.Close();
			}
		}

		/// <summary>
		/// Sets the file system watcher paths
		/// </summary>
		/// <param name="path"></param>
		public void SetFileSystemWatcherPaths(string path) {
			notificationTextFilesPath = path;

			if (notificationTextFilesPath != string.Empty) {
				fileSystemWatcherBits.Path = Path.Combine(path, "cheer");
				fileSystemWatcherSubscription.Path = path;
				fileSystemWatcherDonation.Path = path;
				fileSystemWatchersEnableable = true;
			}
			else {
				fileSystemWatcherBits.Path = string.Empty;
				fileSystemWatcherSubscription.Path = string.Empty;
				fileSystemWatcherDonation.Path = string.Empty;
				fileSystemWatchersEnableable = false;
			}
		}

		/// <summary>
		/// Enables or disables the file system watchers, provided they can be
		/// </summary>
		/// <param name="enabled"></param>
		public void EnableFileSystemWatchers(bool enabled) {
			if (fileSystemWatchersEnableable == false) {
				enabled = false;
			}
			fileSystemWatcherBits.EnableRaisingEvents = enabled;
			fileSystemWatcherSubscription.EnableRaisingEvents = enabled;
			fileSystemWatcherDonation.EnableRaisingEvents = enabled;
		}

		#endregion

		#region notifications interface items

		// config button
		private void notifications_ConfigButton_Click(object sender, EventArgs e) {
			OpenNotificationsConfig();
		}

		// enabled checkbox
		private void notifications_enabledCheckBox_CheckedChanged(object sender, EventArgs e) {
			bool enabled = notifications_enabledCheckBox.Checked;
			// disable buttons
			notifications_ConfigButton.Enabled = enabled;
			notifications_ViewButton.Enabled = enabled;
			// disable utility
			EnableFileSystemWatchers(enabled);
			// close windows
			if (enabled == false) {
				CloseNotificationsWindows();
			}
		}

		// view button
		private void notifications_ViewButton_Click(object sender, EventArgs e) {
			OpenNotificationsViewer();
		}

		#endregion

		#region file system watcher changed calls

		// subs
		private void fileSystemWatcherSubscription_Changed(object sender, FileSystemEventArgs e) {
			notifications.Subscription(e);
		}

		// bits
		private void fileSystemWatcherBits_Changed(object sender, FileSystemEventArgs e) {
			notifications.Bits(e);
		}

		// donations
		private void fileSystemWatcherDonation_Changed(object sender, FileSystemEventArgs e) {
			notifications.Donation(e);
		}

		#endregion


	}
}
