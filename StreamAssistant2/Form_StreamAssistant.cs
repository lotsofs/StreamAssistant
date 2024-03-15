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
using System.Net.Sockets;
using System.Net;
using System.Runtime.Versioning;

namespace StreamAssistant2
{
	public partial class Form_StreamAssistant : Form, ISaveable
	{
		static bool _tcpEnabled;
		IPAddress _ipAddress;
		int _port = 49152;
		StreamReader _str;
		StreamWriter _stw;

		TcpClient _client;
		TcpListener _listener;

		#region opening closing

		public Form_StreamAssistant() {
			InitializeComponent();

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
			//SaveLoad.OnSave += SaveSettings;

			//NameValueCollection appSettings = ConfigurationManager.AppSettings;

			//sceneManager_EnabledCheckBox.Checked = bool.Parse(appSettings["SceneManagerEnabled"]);
			//_sceneManager.Enabled = sceneManager_EnabledCheckBox.Checked;
		}

		/// <summary>
		/// Save settings to file
		/// </summary>
		public void SaveSettings() {
			//Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			//config.AppSettings.Settings["SceneManagerEnabled"].Value = sceneManager_EnabledCheckBox.Checked.ToString();

			//config.Save();
		}

		#endregion

		private void buttonEnable_Click(object sender, EventArgs e) {
			buttonEnable.Text = "Enabled";
			buttonEnable.Enabled = false;
			_ipAddress = new IPAddress(0x0100007f);
			_listener = new TcpListener(_ipAddress, _port);
			_listener.Start();
			_tcpEnabled = true;
			backgroundWorkerTcp.RunWorkerAsync();
		}

		//private async void backgroundWorkerTcp_DoWork(object sender, DoWorkEventArgs e) {
		private void backgroundWorkerTcp_DoWork(object sender, DoWorkEventArgs e) {
			while (_tcpEnabled) {
				this.buttonEnable.Invoke(new MethodInvoker(delegate () {
					buttonEnable.Text = "Waiting";
				}));
				_client = _listener.AcceptTcpClient();

				_str = new StreamReader(_client.GetStream());
				_stw = new StreamWriter(_client.GetStream());
				_stw.AutoFlush = true;

				if (_client.Connected) {
					buttonEnable.Invoke(new MethodInvoker(delegate () {
						buttonEnable.Text = "Reading";
					}));
					try {
						string? s = _str.ReadLine();
						// do something with the received information
						_client.GetStream().Close();
						_client.Close();
					}
					catch (Exception ex) {
						MessageBox.Show(ex.Message.ToString());
					}
				}
			}
			buttonEnable.Invoke(new MethodInvoker(delegate () {
				buttonEnable.Enabled = true;
				buttonEnable.Text = "Reading";
			}));
		}
	}
}
