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
using Streamer.bot.Plugin.Interface;
using System.Reflection;
using Newtonsoft.Json;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.Logging;

namespace StreamAssistant2
{
	public partial class Form_StreamAssistant : Form, ISaveable {
		static bool _tcpEnabled;
		IPAddress _ipAddress;
		int _port = 49152;
		StreamReader _str;
		StreamWriter _stw;

		TcpClient _client;
		TcpListener _listener;

		Clock _clock = new Clock();

		#region opening closing

		public Form_StreamAssistant() {
			InitializeComponent();

			LoadSettings();

			Games.ReadJson();
			Coloring.ReadFiles();
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

		private void startServer() {
			//object[] values = { (int) 12, (long) 10653, (byte) 12, (sbyte) -5,
			//	   16.3, "string" };
			//foreach (var value in values) {
			//	Type t = value.GetType();
			//	Debug.WriteLine(t);
			//}
			//return;

			//Type thisType = typeof(Debug);
			//MethodInfo theMethod = thisType.GetMethod("WriteLine", new[] {typeof(string)} );
			//theMethod.Invoke(null, new object[] { "asdf" });
			//return;

			buttonEnable.Text = "Enabled";
			buttonEnable.Enabled = false;
			_ipAddress = new IPAddress(0x0100007f);
			_listener = new TcpListener(_ipAddress, _port);
			_listener.Start();
			_tcpEnabled = true;
			backgroundWorkerTcp.RunWorkerAsync();
		}

		private void buttonEnable_Click(object sender, EventArgs e) {
			startServer();
			timerUpdate.Enabled = true;
		}

		private void HandleTrigger(string trigger) {
			Dictionary<string, object>? variables = JsonConvert.DeserializeObject<Dictionary<string, object>>(trigger);
			if (variables == null) return;
			if (!variables.ContainsKey("__source")) {
				MsgQueue.Enqueue(MsgTypes.ChatMsg, "🚩 Received variables without a source.");
				Debug.WriteLine(trigger);
				return;
			}
			bool isTest = variables.ContainsKey("isTest") && (bool)variables["isTest"];
			long source = (long)variables["__source"];
			Debug.WriteLine("__source: " + source);
			//foreach (string o in blah.Keys) {
			//	if (blah[o] == null) { 
			//		Debug.WriteLine(o + " " + "<null>"); 
			//	}
			//	else {
			//		Debug.WriteLine(o + " " + blah[o].ToString());
			//	}
			//}
			//Debug.WriteLine(variables["__source"].GetType());
			string date = variables["actionQueuedAt"].ToString();
			string log = string.Format("[{0}] {1}: ", date, ((EventSources)source).ToString());

			switch (source) {
				case 102:   // TwitchCheer
					log += Cheers.Process(variables);
					break;
				case 103:   // TwitchSub
					log += Subscriptions.HandleTwitchSub(variables);
					break;
				case 104:   // TwitchReSub
					log += Subscriptions.HandleTwitchResub(variables);
					break;
				case 105:   // TwitchGiftSub
					log += Subscriptions.HandleGiftSub(variables);
					break;
				case 106:   // TwitchGiftBomb
					Subscriptions.HandleGiftBomb(variables);
					break;
				case 107:   // TwitchRaid
					string raider = isTest ? "Test User" : variables["userName"].ToString() ?? "Someone";
					long raiders = (long)variables["viewers"];
					MsgQueue.Enqueue(MsgTypes.ChatMsg, "Raid: " + raider + " (" + raiders + ")");
					break;
				case 112:   // TwitchRewardRedemption
					log += ChannelPoints.Process(variables);
					break;
				case 118:   // TwitchStreamUpdate
				case 122:   // TwitchBroadcastUpdate
					log += Games.ProcessChangeEvent(variables);
					break;
				case 133:   // TwitchChatMessage
					log += ChatMessages.Process(variables);
					break;
				case 154:   // TwitchStreamOnline
					log += Games.ProcessGoLiveEvent(variables);
					break;
				case 186:   // TwitchUpcomingAd
					log += Ads.UpcomingAdAlert(variables);
					break;
				case 1201:  // StreamElementsTip
					log += Donations.Process(variables);
					break;
				default:
					break;
			}

			this.textBoxIncoming.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
				textBoxIncoming.AppendText(log + Environment.NewLine);
			}));
		}

		private string HandleMessage(string msg) {
			string msgType = msg.Substring(0, 7);
			string msgContent = "";
			if (msg.Length > 8) {
				msgContent = msg.Substring(8);
			}
			switch (msgType) {
				case "QueryRs":
					string dequeue = "NoneMsg";
					if (MsgQueue.TryDequeue(out string? deq)) {
						dequeue = deq;
						this.textBoxOutgoing.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
							textBoxOutgoing.AppendText(string.Format("[{0}] {1}{2}", DateTime.Now, deq, Environment.NewLine));
						}));
					}
					return dequeue;
					break;
				case "Trigger":
					HandleTrigger(msgContent);
					break;
				default:
					break;
			}
			return null;
		}

		//private async void backgroundWorkerTcp_DoWork(object sender, DoWorkEventArgs e) {
		private void backgroundWorkerTcp_DoWork(object sender, DoWorkEventArgs e) {
			while (_tcpEnabled) {
				this.buttonEnable.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
					buttonEnable.Text = "Waiting";
				}));
				_client = _listener.AcceptTcpClient();

				_str = new StreamReader(_client.GetStream());
				_stw = new StreamWriter(_client.GetStream());
				_stw.AutoFlush = true;

				while (_client.Connected) {

					//try {
					string? s = _str.ReadLine();
					string reply = HandleMessage(s);
					if (!string.IsNullOrEmpty(reply)) {
						_stw.WriteLine(reply);
					}

					_client.GetStream().Close();
					_client.Close();
					//}
					//catch (Exception ex) {
					//MessageBox.Show(ex.Message.ToString());
					//}
				}
			}
			buttonEnable.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
				buttonEnable.Enabled = true;
				buttonEnable.Text = "Enable";
			}));
		}

		private void buttonTest_Click(object sender, EventArgs e) {
			Debug.WriteLine("Bbutton");
			_stw.WriteLine("Button");
			//buttonEnable.Text = "Enabled";
			//buttonEnable.Enabled = false;
			//_ipAddress = new IPAddress(0x0100007f);
			//_listener = new TcpListener(_ipAddress, _port);
			//_listener.Start();
			//_tcpEnabled = true;
			//backgroundWorkerTcp.RunWorkerAsync();
		}

		private void timerUpdate_Tick(object sender, EventArgs e) {
			_clock.Tick();
			MsgQueue.TimedQueueTick();
		}

		private void label2_Click(object sender, EventArgs e) {

		}

		private void textBoxIncoming_TextChanged(object sender, EventArgs e) {

		}
	}
}
