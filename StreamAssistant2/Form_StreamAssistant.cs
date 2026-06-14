// using System.Diagnostics;
// using Newtonsoft.Json;

// namespace StreamAssistant2
// {
// 	public partial class Form_StreamAssistant : Form, ISaveable {

// 		// readonly Clock _clock = new Clock();

// 		#region opening closing

// 		public Form_StreamAssistant() {
// 			LoadSettings();

// 			Games.ReadJson();
// 			Coloring.ReadFiles();
// 		}

// 		/// <summary>
// 		/// Form closing
// 		/// </summary>
// 		/// <param name="sender"></param>
// 		/// <param name="e"></param>
// 		private void Form_StreamAssistant_FormClosing(object sender, FormClosingEventArgs e) {
// 			SaveLoad.SaveAll();
// 			DisableBot();
// 		}

// 		#endregion

// 		#region save load

// 		/// <summary>
// 		/// Loads settings
// 		/// </summary>
// 		public void LoadSettings() {
// 			//SaveLoad.OnSave += SaveSettings;

// 			//NameValueCollection appSettings = ConfigurationManager.AppSettings;

// 			//sceneManager_EnabledCheckBox.Checked = bool.Parse(appSettings["SceneManagerEnabled"]);
// 			//_sceneManager.Enabled = sceneManager_EnabledCheckBox.Checked;
// 		}

// 		/// <summary>
// 		/// Save settings to file
// 		/// </summary>
// 		public void SaveSettings() {
// 			//Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

// 			//config.AppSettings.Settings["SceneManagerEnabled"].Value = sceneManager_EnabledCheckBox.Checked.ToString();

// 			//config.Save();
// 		}

// 		#endregion

// 		private static void EnableBot() {
// 			_ = TwitchIRCManager.ConnectAsync();
// 			TwitchIRCManager.OnMessage += ChatHandler.ProcessMessage;
// 		}

// 		private static void DisableBot() {
// 			TwitchIRCManager.SendMessage("🍂 Disconnected");
// 			TwitchIRCManager.Disconnect();
// 		}

// 		private void HandleTrigger(string trigger) {
// 			// This can be ignored for now
			
// 			Dictionary<string, object>? variables = JsonConvert.DeserializeObject<Dictionary<string, object>>(trigger);
// 			if (variables == null) return;
// 			if (!variables.ContainsKey("__source")) {
// 				MsgQueue.Enqueue(MsgTypes.ChatMsg, "🚩 Received variables without a source.");
// 				Debug.WriteLine(trigger);
// 				return;
// 			}
// 			bool isTest = variables.ContainsKey("isTest") && (bool)variables["isTest"];
// 			long source = (long)variables["__source"];
// 			Debug.WriteLine("__source: " + source);
// 			//foreach (string o in blah.Keys) {
// 			//	if (blah[o] == null) { 
// 			//		Debug.WriteLine(o + " " + "<null>"); 
// 			//	}
// 			//	else {
// 			//		Debug.WriteLine(o + " " + blah[o].ToString());
// 			//	}
// 			//}
// 			//Debug.WriteLine(variables["__source"].GetType());
// 			string date = variables["actionQueuedAt"].ToString();
// 			string log = string.Format("[{0}] {1}: ", date, ((EventSources)source).ToString());

// 			switch (source) {
// 				case 102:   // TwitchCheer
// 					log += Cheers.Process(variables);
// 					break;
// 				case 103:   // TwitchSub
// 					log += Subscriptions.HandleTwitchSub(variables);
// 					break;
// 				case 104:   // TwitchReSub
// 					log += Subscriptions.HandleTwitchResub(variables);
// 					break;
// 				case 105:   // TwitchGiftSub
// 					log += Subscriptions.HandleGiftSub(variables);
// 					break;
// 				case 106:   // TwitchGiftBomb
// 					Subscriptions.HandleGiftBomb(variables);
// 					break;
// 				case 107:   // TwitchRaid
// 					string raider = isTest ? "Test User" : variables["userName"].ToString() ?? "Someone";
// 					long raiders = (long)variables["viewers"];
// 					MsgQueue.Enqueue(MsgTypes.ChatMsg, "Raid: " + raider + " (" + raiders + ")");
// 					break;
// 				case 112:   // TwitchRewardRedemption
// 					log += ChannelPoints.Process(variables);
// 					break;
// 				case 118:   // TwitchStreamUpdate
// 				case 122:   // TwitchBroadcastUpdate
// 					log += Games.ProcessChangeEvent(variables);
// 					break;
// 				case 133:   // TwitchChatMessage
// 					// log += ChatMessages.Process(variables);
// 					break;
// 				case 154:   // TwitchStreamOnline
// 					// ChatMessages.ResetChatterList();
// 					log += Games.ProcessGoLiveEvent(variables);
// 					break;
// 				case 186:   // TwitchUpcomingAd
// 					log += Ads.UpcomingAdAlert(variables);
// 					break;
// 				case 1201:  // StreamElementsTip
// 					log += Donations.Process(variables);
// 					break;
// 				default:
// 					break;
// 			}

// 			this.textBoxIncoming.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
// 				textBoxIncoming.AppendText(log + Environment.NewLine);
// 			}));
// 		}

// 		private string HandleMessage(string msg) {
// 			string msgType = msg.Substring(0, 7);
// 			string msgContent = "";
// 			if (msg.Length > 8) {
// 				msgContent = msg.Substring(8);
// 			}
// 			switch (msgType) {
// 				case "QueryRs":
// 					string dequeue = "NoneMsg";
// 					if (MsgQueue.TryDequeue(out string? deq)) {
// 						dequeue = deq;
// 						this.textBoxOutgoing.Invoke(new System.Windows.Forms.MethodInvoker(delegate () {
// 							textBoxOutgoing.AppendText(string.Format("[{0}] {1}{2}", DateTime.Now, deq, Environment.NewLine));
// 						}));
// 					}
// 					return dequeue;
// 					break;
// 				case "Trigger":
// 					HandleTrigger(msgContent);
// 					break;
// 				default:
// 					break;
// 			}
// 			return null;
// 		}

// 		private void timerUpdate_Tick(object sender, EventArgs e) {
// 			// _clock.Tick();
// 			MsgQueue.TimedQueueTick();
// 		}
// 	}
// }
