using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace StreamAssistant {
	public class MemRead {

		public int major = 0;
		public int minor = 0;
		public enum regionTypes { GTA, US, Europe, Japan, Steam };
		public regionTypes region = regionTypes.GTA;

		public Process[] p;

		Timer timer1;
		Timer timer2;

		// hard coding this shit for now. Too lazy to do it differently.

		public int address_bulletsFired = 0xC0BA00;
		public int address_bulletsHits = 0xC0BA08;
		//public int address_headshots = 0xC0BA10; //Useless
		public int bulletsFired = 0;
		public int bulletsHit = 0;
		public float accuracy = 0f;

		public int address_propertyDamage = 0xC0B9FC;
		public int address_peopleWastedYou = 0xC0B9EC;
		public int address_peopleWastedOthers = 0xC0B9E8;
		public int address_kgExplosives = 0xC0BA04;
		public int propertyDamage = 0;
		public int peopleWastedYou = 0;
		public int peopleWastedOthers = 0;
		public int kgExplosives = 0;


		public int address_distanceFoot = 0xC0BD74;
		public int address_distanceCar = 0xC0BD78;
		public int address_distanceBicycle = 0xC0BDD4;
		public int address_distanceBike = 0xC0BD7C;
		public int address_distanceHelicopter = 0xC0BD88;
		public int address_distancePlane = 0xC0BD8C;
		public int address_distanceBoat = 0xC0BD80;
		public int address_distanceSwimming = 0xC0BDD0;
		public int address_distanceGolfCart = 0xC0BD84;
		//public int address_distanceJetpack = 0x0; // Doesnt exist gg good game
		public float distanceFoot = 0f;
		public float distanceCar = 0f;
		public float distanceBicycle = 0f;
		public float distanceBike = 0f;
		public float distanceHelicopter = 0f;
		public float distancePlane = 0f;
		public float distanceBoat = 0f;
		public float distanceSwimming = 0f;
		public float distanceGolfCart = 0f;
		public float totalDistanceTraveled = 0f;

		public int address_legitimateKills = 0xC0BACC;
		public int address_timesWasted = 0xC0BA24;
		public int address_timesBusted = 0xC0BA1C;
		public int address_firefighterLevel = 0xC0BA84;
		public int address_firefighterTargets = 0xC0BA6C;
		public int address_paramedicLevel = 0xC0BA80;
		public int address_paramedicTargets = 0xC0BA64;
		public int address_vigilanteTargets = 0xC0BA68;
		public int address_money = 0xC0F948;
		public int address_aircraftDestroyed = 0xC0B9F8;
		public int address_timesCheated = 0xC0BA2C;
		public int address_percentage = 0xC0BD68;
		public int legitimateKills = 0;
		public int timesWasted = 0;
		public int timesBusted = 0;
		public int firefighterLevel = 0;
		public int firefighterTargets = 0;
		public int paramedicLevel = 0;
		public int paramedicTargets = 0;
		public int vigilanteTargets = 0;
		public int moneyOwned = 0;
		public int aircraftDestroyed = 0;
		public int timesCheated = 0;
		public int percentage = 0;
		public int criminalRating = 0;

		string statsText;

		public bool gameRunning = false;
		public bool toolRunning = false;

		Form1 form1;

		// Dont know what this does
		const int PROCESS_WM_READ = 0x0010;

		// Allows me to read memory from processes
		[DllImport("kernel32.dll")]
		public static extern Int32 ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer, UInt32 size, out IntPtr lpNumberOfBytesRead);

		// Sloppy way of detecting the game. It's still gonna throw unhandled exceptions but at least it won't be doing stuff when the game isn't running.
		public void detectGame() {
			p = Process.GetProcessesByName("gta-sa");
			if (p.Length != 0) {
				gameRunning = true;
			}
			else {
				gameRunning = false;
			}
		}

		public MemRead(Form1 form1) {
			this.form1 = form1;
		}

		public void processStats() {
			try {
				/* --------- distance -------- */
				distanceFoot = ReadValue(p[0].Handle, address_distanceFoot, true, false);
				distanceCar = ReadValue(p[0].Handle, address_distanceCar, true, false);
				distanceBicycle = ReadValue(p[0].Handle, address_distanceBicycle, true, false);
				distanceBike = ReadValue(p[0].Handle, address_distanceBike, true, false);
				distanceHelicopter = ReadValue(p[0].Handle, address_distanceHelicopter, true, false);
				distancePlane = ReadValue(p[0].Handle, address_distancePlane, true, false);
				distanceBoat = ReadValue(p[0].Handle, address_distanceBoat, true, false);
				distanceSwimming = ReadValue(p[0].Handle, address_distanceSwimming, true, false);
				distanceGolfCart = ReadValue(p[0].Handle, address_distanceGolfCart, true, false);
				totalDistanceTraveled = distanceBicycle + distanceBike + distanceBoat + distanceCar + distanceFoot + distanceHelicopter + distancePlane + distanceSwimming + distanceGolfCart;
				statsText = "Distance Traveled: \n   " + (totalDistanceTraveled /** (10f / 3f)*/).ToString() + " m";
				/* --------- shots fired -------- */
				bulletsFired = ReadValue(p[0].Handle, address_bulletsFired, false, true);
				bulletsHit = ReadValue(p[0].Handle, address_bulletsHits, false, true);
				accuracy = (float)bulletsHit / (float)bulletsFired * 100f;
				statsText = statsText + "\nAccuracy: \n   " + bulletsHit.ToString() + "/" + bulletsFired.ToString() + " (" + accuracy.ToString("0.00") + "%)";
				/* --------- Explosives ----------- */
				kgExplosives = ReadValue(p[0].Handle, address_kgExplosives, false, true);
				statsText = statsText + "\nExplosives Used: \n   " + kgExplosives;
				/* --------- Destruction ---------- */
				propertyDamage = ReadValue(p[0].Handle, address_propertyDamage, false, true);
				peopleWastedOthers = ReadValue(p[0].Handle, address_peopleWastedOthers, false, true);
				peopleWastedYou = ReadValue(p[0].Handle, address_peopleWastedYou, false, true);
				statsText = statsText + " kg\nKills: \n   " + peopleWastedYou.ToString() + " by player \n   " + peopleWastedOthers.ToString() + " by others\n   " + (peopleWastedOthers + peopleWastedYou).ToString() + " Total\nProperty Damage: \n   $" + propertyDamage.ToString() + ".00";
				/* -------- Criminal Rating --------- */
				legitimateKills = ReadValue(p[0].Handle, address_legitimateKills, false, true);
				timesWasted = ReadValue(p[0].Handle, address_timesWasted, false, true);
				timesBusted = ReadValue(p[0].Handle, address_timesBusted, false, true);
				firefighterLevel = ReadValue(p[0].Handle, address_firefighterLevel, false, true);
				firefighterTargets = ReadValue(p[0].Handle, address_firefighterTargets, false, true);
				paramedicLevel = ReadValue(p[0].Handle, address_paramedicLevel, false, true);
				paramedicTargets = ReadValue(p[0].Handle, address_paramedicTargets, false, true);
				vigilanteTargets = ReadValue(p[0].Handle, address_vigilanteTargets, false, true);
				moneyOwned = ReadValue(p[0].Handle, address_money, false, true);
				aircraftDestroyed = ReadValue(p[0].Handle, address_aircraftDestroyed, false, true);
				timesCheated = ReadValue(p[0].Handle, address_timesCheated, false, true);
				percentage = ReadValue(p[0].Handle, address_percentage, true, false);
				criminalRating = legitimateKills + ((firefighterLevel + paramedicLevel) * 10) + (aircraftDestroyed * 30) + firefighterTargets + paramedicTargets + vigilanteTargets - (timesCheated * 10) - (timesBusted * 3) - (timesWasted * 3);
				//criminalRating = (float)legitimateKills - (float)timesBusted*3f - (float)timesWasted*3f + ((float)firefighterLevel *10f) + ((float)paramedicLevel * 10f) + ((float)moneyOwned / 5000f) + ((float)aircraftDestroyed * 30f) + (float)firefighterTargets + (float)vigilanteTargets + (float)paramedicTargets - ((float)timesCheated * 10f);
				criminalRating += (moneyOwned / 5000);
				if (bulletsFired > 100) {
					criminalRating += (500 * bulletsHit / bulletsFired);
				}
				criminalRating += (1000 * (int)percentage / 187);
				statsText = statsText + "\nCriminal Rating: \n   " + criminalRating.ToString();
				/* --------- Percentage -------- */
				statsText = statsText + "\nPercentage Completed: \n   " + ((percentage / 187f) * 100f).ToString("0.00");


				form1.label2.Text = statsText;
			}
			catch (InvalidOperationException) {
				Debug.WriteLine("InvalidOperationException");
				gameRunning = false;
				return;
			}
		}

		// Bitconverter to return whatever is in that memory address to an integer so I can work with it
		private int ReadValue(IntPtr handle, long address, bool floatRequested, bool fourBytes) {
			if (floatRequested) {
				Single floatInstead = ReadFloat(handle, address);
				return Convert.ToInt32(floatInstead);
			}
			else if (!fourBytes) {
				return (int)ReadBytes(handle, address, 1)[0];
			}
			else {
				return BitConverter.ToInt32(ReadBytes(handle, address, 4), 0);
			}
		}

		private static Single ReadFloat(IntPtr handle, long address) {
			return BitConverter.ToSingle(ReadBytes(handle, address, 4), 0);
		}

		// Read memory
		private static byte[] ReadBytes(IntPtr handle, long address, uint bytesToRead) {
			IntPtr ptrBytesRead;
			byte[] buffer = new byte[bytesToRead];
			ReadProcessMemory(handle, new IntPtr(address), buffer, bytesToRead, out ptrBytesRead);
			return buffer;
		}

		public void InitTimer() {
			timer1 = new Timer();
			timer1.Tick += new EventHandler(Timer1Tick);
			timer1.Interval = 40;
			timer1.Start();

			timer2 = new Timer();
			timer2.Tick += new EventHandler(Timer2Tick);
			timer2.Interval = 5000;
			timer2.Start();
		}

		void Timer1Tick(object sender, EventArgs e) {
			if (gameRunning) {
				processStats();
			}
		}

		void Timer2Tick(object sender, EventArgs e) {
			if (toolRunning) {
				detectGame();
			}
		}



	}
}
