using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace StreamAssistant {
	public partial class Form1 : Form {
		protected IrrKlang.ISoundDeviceList sdl;
		protected IrrKlang.ISoundEngine irrKlangEngine;
		protected IrrKlang.ISound currentlyPlayingSound;

		string subscriber;
		string subscriber2;

		Timer timer2;

		//string cheerer;
		//string cheerer2;
		string cheer;
		string cheer2;

		//string donator;
		//string donator2;
		string donation;
		string donation2;

		StreamWriter outputFile;

		public MemRead memRead;

		public Form1() {
			InitializeComponent();
			memRead = new MemRead(this);

			memRead.toolRunning = checkBox1.Checked;
			memRead.InitTimer();


			IrrKlang.ISoundDeviceList sdl = new IrrKlang.ISoundDeviceList(IrrKlang.SoundDeviceListType.PlaybackDevice);
			for (int i = 0; i < sdl.DeviceCount; i++) {
				comboBox1.Items.Add(sdl.getDeviceDescription(i));
			}
			comboBox1.SelectedIndex = 4;
			
			irrKlangEngine = new IrrKlang.ISoundEngine(IrrKlang.SoundOutputDriver.AutoDetect, IrrKlang.SoundEngineOptionFlag.DefaultOptions, sdl.getDeviceID(comboBox1.SelectedIndex));

			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = @"D:\\Apps\\TWITCH ALERTS\\txt";
			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.EnableRaisingEvents = true;

			outputFile = new StreamWriter(@"Output.txt");
			outputFile.AutoFlush = true;


			timer2 = new Timer();
			timer2.Tick += new EventHandler(TextChange);
			timer2.Interval = 1000;
			timer2.Start();

		}

		private void TextChange(object sender, EventArgs e) {
			label1.Text = donation;
		}

		private void OnChanged(object source, FileSystemEventArgs e) {
			if (e.Name == "session_most_recent_subscriber.txt") {
				try {
					subscriber2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (subscriber2 != subscriber) {
					button2_Click(null, null);
					subscriber = subscriber2;
				}
			}


			if (e.Name == "session_most_recent_cheerer.txt") {
				// play sound, but only if cheer is triple digits or above
				try {
					cheer2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (cheer2 != cheer) {
					char[] cheer3 = cheer2.ToCharArray();
					cheer3 = Array.FindAll<char>(cheer3, (c => (char.IsDigit(c))));
					if (cheer3.Length > 2) {
						button1_Click(null, null);
					}
					cheer = cheer2;
				}
			}
			/*else if (e.Name == "most_recent_cheerer.txt") {
				// dump cheerer + amount to text file
				try {
					cheerer2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (cheerer2 != cheerer) {
					outputFile.WriteLine(cheerer2);
					cheerer = cheerer2;
				}
			}*/


			else if (e.Name == "session_most_recent_donator.txt") {
				// play sound, 
				try {
					donation2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (donation2 != donation) {
					//char[] donation3 = donation2.ToCharArray();
					//donation3 = Array.FindAll<char>(donation3, (c => (char.IsDigit(c))));
					//int donation4 = int.Parse(new string(donation3));
					if (donation != null) {
						button4_Click(null, null);
					}
					donation = donation2;
				}
			}
			/*else if (e.Name == "most_recent_donator.txt") {
				// dump cheerer + amount to text file
				try {
					cheerer2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (cheerer2 != cheerer) {
					outputFile.WriteLine(cheerer2);
					cheerer = cheerer2;
				}
			}

			/*else if (e.Name == "session_most_recent_donator.txt") {
				try {
					donator2 = File.ReadAllText(e.FullPath);
				}
				catch (System.IO.IOException) {
					Debug.WriteLine("CRITICAL ERROR");
				}
				if (donator2 != donator) {
					button4_Click(null, null);
					donator = donator2;
				}
				outputFile.WriteLine(donator2);
			}*/
		}

		private void button1_Click(object sender, EventArgs e) {
			currentlyPlayingSound = irrKlangEngine.Play2D("Sounds/Team17-Applauds.wav", false);
			currentlyPlayingSound.Volume = 0.7f;
			//currentlyPlayingSound.Volume = 100;	
		}

		private void button2_Click(object sender, EventArgs e) {
			currentlyPlayingSound = irrKlangEngine.Play2D("Sounds/Tribal hymn.wav", false);
			currentlyPlayingSound.Volume = 0.7f;
		}

		private void button3_Click(object sender, EventArgs e) {
			currentlyPlayingSound = irrKlangEngine.Play2D("Sounds/HOLYDONKEY.WAV", false);
			currentlyPlayingSound.Volume = 0.4f;
		}

		private void button4_Click(object sender, EventArgs e) {
			currentlyPlayingSound = irrKlangEngine.Play2D("Sounds/IndianAnthem.wav", false);
			currentlyPlayingSound.Volume = 0.4f;
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) {
			Debug.WriteLine(comboBox1.SelectedIndex.ToString());
		}

		private void label1_Click(object sender, EventArgs e) {

		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e) {
			memRead.toolRunning = checkBox1.Checked;
		}

		private void comboBox2_SelectedIndexChanged(object sender, EventArgs e) {
			switch (comboBox2.SelectedItem.ToString()) {
				default:
					break;
				case "GTASA":
					memRead.selectedGame = MemRead.games.SanAndreas;
					break;
				case "GTA3":
					memRead.selectedGame = MemRead.games.III;
					Debug.WriteLine(memRead.selectedGame);
					break;
			}
		}
	}
}
