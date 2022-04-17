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
		SceneManager _sceneManager;

		#region opening closing

		public Form_StreamAssistant() {
			InitializeComponent();

			_sceneManager = new SceneManager();

			LoadSettings();
		}

		/// <summary>
		/// Form closing
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Form_StreamAssistant_FormClosing(object sender, FormClosingEventArgs e) {
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

			sceneManager_EnabledCheckBox.Checked = bool.Parse(appSettings["SceneManagerEnabled"]);
			_sceneManager.Enabled = sceneManager_EnabledCheckBox.Checked;
		}

		/// <summary>
		/// Save settings to file
		/// </summary>
		public void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.AppSettings.Settings["SceneManagerEnabled"].Value = sceneManager_EnabledCheckBox.Checked.ToString();

			config.Save();
		}

		#endregion

		private void sceneManager_EnabledCheckBox_CheckedChanged(object sender, EventArgs e) {
			_sceneManager.Enabled = sceneManager_EnabledCheckBox.Checked;
		}

		private void sceneManager_ButtonSuspend5_Click(object sender, EventArgs e) {
			_sceneManager.Suspend(300);
		}

		private void sceneManager_ButtonSuspend60_Click(object sender, EventArgs e) {
			_sceneManager.Suspend(3600);
		}

		private void sceneManager_ButtonSuspend720_Click(object sender, EventArgs e) {
			_sceneManager.Suspend(43200);
		}

		private void sceneManager_ButtonSuspend0_Click(object sender, EventArgs e) {
			_sceneManager.Suspend(0);
		}
	}
}
