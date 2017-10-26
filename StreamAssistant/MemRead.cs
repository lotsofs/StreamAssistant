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

		// hard coding this shit for now. Too lazy to do it differently and Im not planning on publishing this.

		#region GTASA stuff
		public int addressGtaSA_bulletsFired = 0xC0BA00;
		public int addressGtaSA_bulletsHits = 0xC0BA08;
		//public int address_headshots = 0xC0BA10; //Useless
		public int gtaSA_bulletsFired = 0;
		public int gtaSA_bulletsHit = 0;
		public float gtaSA_accuracy = 0f;
		public int addressGtaSA_propertyDamage = 0xC0B9FC;
		public int addressGtaSA_peopleWastedYou = 0xC0B9EC;
		public int addressGtaSA_peopleWastedOthers = 0xC0B9E8;
		public int addressGtaSA_kgExplosives = 0xC0BA04;
		public int gtaSA_propertyDamage = 0;
		public int gtaSA_peopleWastedYou = 0;
		public int gtaSA_peopleWastedOthers = 0;
		public int gtaSA_kgExplosives = 0;
		public int addressGtaSA_distanceFoot = 0xC0BD74;
		public int addressGtaSA_distanceCar = 0xC0BD78;
		public int addressGtaSA_distanceBicycle = 0xC0BDD4;
		public int addressGtaSA_distanceBike = 0xC0BD7C;
		public int addressGtaSA_distanceHelicopter = 0xC0BD88;
		public int addressGtaSA_distancePlane = 0xC0BD8C;
		public int addressGtaSA_distanceBoat = 0xC0BD80;
		public int addressGtaSA_distanceSwimming = 0xC0BDD0;
		public int addressGtaSA_distanceGolfCart = 0xC0BD84;
		//public int addressGtaSA_distanceJetpack = 0x0; // Doesnt exist gg good game
		public float gtaSA_distanceFoot = 0f;
		public float gtaSA_distanceCar = 0f;
		public float gtaSA_distanceBicycle = 0f;
		public float gtaSA_distanceBike = 0f;
		public float gtaSA_distanceHelicopter = 0f;
		public float gtaSA_distancePlane = 0f;
		public float gtaSA_distanceBoat = 0f;
		public float gtaSA_distanceSwimming = 0f;
		public float gtaSA_distanceGolfCart = 0f;
		public float gtaSA_totalDistanceTraveled = 0f;
		public int addressGtaSA_legitimateKills = 0xC0BACC;
		public int addressGtaSA_timesWasted = 0xC0BA24;
		public int addressGtaSA_timesBusted = 0xC0BA1C;
		public int addressGtaSA_firefighterLevel = 0xC0BA84;
		public int addressGtaSA_firefighterTargets = 0xC0BA6C;
		public int addressGtaSA_paramedicLevel = 0xC0BA80;
		public int addressGtaSA_paramedicTargets = 0xC0BA64;
		public int addressGtaSA_vigilanteTargets = 0xC0BA68;
		public int addressGtaSA_money = 0xC0F948;
		public int addressGtaSA_aircraftDestroyed = 0xC0B9F8;
		public int addressGtaSA_timesCheated = 0xC0BA2C;
		public int addressGtaSA_percentage = 0xC0BD68;
		public int gtaSA_legitimateKills = 0;
		public int gtaSA_timesWasted = 0;
		public int gtaSA_timesBusted = 0;
		public int gtaSA_firefighterLevel = 0;
		public int gtaSA_firefighterTargets = 0;
		public int gtaSA_paramedicLevel = 0;
		public int gtaSA_paramedicTargets = 0;
		public int gtaSA_vigilanteTargets = 0;
		public int gtaSA_moneyOwned = 0;
		public int gtaSA_aircraftDestroyed = 0;
		public int gtaSA_timesCheated = 0;
		public int gtaSA_percentage = 0;
		public int gtaSA_criminalRating = 0;
		#endregion
		#region GTA3 stuff
		public int addressGtaIII_bulletsFired = 0x953368;
		public int addressGtaIII_bulletsHits = 0x96CE84;
		public int gtaIII_bulletsFired = 0;
		public int gtaIII_bulletsHit = 0;
		public float gtaIII_accuracy = 0f;

		//public int addressGtaIII_propertyDamage = 0xC0B9FC;
		public int addressGtaIII_peopleWastedYou = 0x901D70;
		public int addressGtaIII_peopleWastedOthers = 0x8F2E44;
		//public int addressGtaIII_kgExplosives = 0xC0BA04;
		//public int gtaIII_propertyDamage = 0;
		public int gtaIII_peopleWastedYou = 0;
		public int gtaIII_peopleWastedOthers = 0;
		//public int gtaIII_kgExplosives = 0;
		public int addressGtaIII_distanceFoot = 0x951810;
		public int addressGtaIII_distanceCar = 0x95086C;
		//public int addressGtaIII_distanceBicycle = 0xC0BDD4;
		//public int addressGtaIII_distanceBike = 0xC0BD7C;
		//public int addressGtaIII_distanceHelicopter = 0xC0BD88;
		//public int addressGtaIII_distancePlane = 0xC0BD8C;
		//public int addressGtaIII_distanceBoat = 0xC0BD80;
		//public int addressGtaIII_distanceSwimming = 0xC0BDD0;
		//public int addressGtaIII_distanceGolfCart = 0xC0BD84;
		//public int addressGtaIII_distanceJetpack = 0x0; // Doesnt exist gg good game
		public float gtaIII_distanceFoot = 0f;
		public float gtaIII_distanceCar = 0f;
		//public float gtaIII_distanceBicycle = 0f;
		//public float gtaIII_distanceBike = 0f;
		//public float gtaIII_distanceHelicopter = 0f;
		//public float gtaIII_distancePlane = 0f;
		//public float gtaIII_distanceBoat = 0f;
		//public float gtaIII_distanceSwimming = 0f;
		//public float gtaIII_distanceGolfCart = 0f;
		public float gtaIII_totalDistanceTraveled = 0f;
		public int addressGtaIII_legitimateKills = 0x9062FC;
		public int addressGtaIII_timesWasted = 0x8F2DD0;
		public int addressGtaIII_timesBusted = 0x8F2DE0;
		//public int addressGtaIII_firefighterLevel = 0xC0BA84;
		public int addressGtaIII_firefighterTargets = 0x9061E0;
		public int addressGtaIII_paramedicLevel = 0x902BF8;
		public int addressGtaIII_paramedicTargets = 0x9059D4;
		public int addressGtaIII_vigilanteTargets = 0x90270C;
		public int addressGtaIII_money = 0x951694;
		public int addressGtaIII_aircraftDestroyed = 0x8f2b54;
		//public int addressGtaIII_timesCheated = 0xC0BA2C;
		public int addressGtaIII_percentage = 0x90651C;
		public int gtaIII_legitimateKills = 0;
		public int gtaIII_timesWasted = 0;
		public int gtaIII_timesBusted = 0;
		//public int gtaIII_firefighterLevel = 0;
		public int gtaIII_firefighterTargets = 0;
		public int gtaIII_paramedicLevel = 0;
		public int gtaIII_paramedicTargets = 0;
		public int gtaIII_vigilanteTargets = 0;
		public int gtaIII_moneyOwned = 0;
		public int gtaIII_aircraftDestroyed = 0;
		//public int gtaIII_timesCheated = 0;
		public int gtaIII_percentage = 0;
		public int gtaIII_criminalRating = 0;
		public int addressGtaIII_carsDestroyed = 0x951580;
		public int gtaIII_carsDestroyed = 0;
		public int addressGtaIII_lastRequestsPassed = 0x75B7C4;
		public int addressGtaIII_aditoPassed = 0x75B8CC;
		public int gtaIII_lastRequestsPassed = 0;
		public int gtaIII_aditoPassed = 0;
		public int addressGtaIII_wakaGarageEntered = 0x75E860;
		public int addressGtaIII_wakaPassed = 0x75E874;
		public int gtaIII_wakaGarageEntered = 0;
		public int gtaIII_wakaPassed = 0;

		#endregion


		public enum games { SanAndreas, III }
		public games selectedGame;
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
			if (selectedGame == games.SanAndreas) {
				p = Process.GetProcessesByName("gta-sa");
				if (p.Length != 0) {
					gameRunning = true;
				}
				else {
					gameRunning = false;
				}
			}
			else if (selectedGame == games.III) {
				p = Process.GetProcessesByName("gta3");
				if (p.Length != 0) {
					gameRunning = true;
				}
				else {
					gameRunning = false;
				}
			}
		}

		public MemRead(Form1 form1) {
			this.form1 = form1;
		}

		public void processStats() {
			try {
				if (selectedGame == games.SanAndreas) {
					/* --------- distance -------- */
					gtaSA_distanceFoot = ReadValue(p[0].Handle, addressGtaSA_distanceFoot, true, false);
					gtaSA_distanceCar = ReadValue(p[0].Handle, addressGtaSA_distanceCar, true, false);
					gtaSA_distanceBicycle = ReadValue(p[0].Handle, addressGtaSA_distanceBicycle, true, false);
					gtaSA_distanceBike = ReadValue(p[0].Handle, addressGtaSA_distanceBike, true, false);
					gtaSA_distanceHelicopter = ReadValue(p[0].Handle, addressGtaSA_distanceHelicopter, true, false);
					gtaSA_distancePlane = ReadValue(p[0].Handle, addressGtaSA_distancePlane, true, false);
					gtaSA_distanceBoat = ReadValue(p[0].Handle, addressGtaSA_distanceBoat, true, false);
					gtaSA_distanceSwimming = ReadValue(p[0].Handle, addressGtaSA_distanceSwimming, true, false);
					gtaSA_distanceGolfCart = ReadValue(p[0].Handle, addressGtaSA_distanceGolfCart, true, false);
					gtaSA_totalDistanceTraveled = gtaSA_distanceBicycle + gtaSA_distanceBike + gtaSA_distanceBoat + gtaSA_distanceCar + gtaSA_distanceFoot + gtaSA_distanceHelicopter + gtaSA_distancePlane + gtaSA_distanceSwimming + gtaSA_distanceGolfCart;
					statsText = "Distance Traveled: \n   " + (gtaSA_totalDistanceTraveled /** (10f / 3f)*/).ToString() + " m";
					/* --------- shots fired -------- */
					gtaSA_bulletsFired = ReadValue(p[0].Handle, addressGtaSA_bulletsFired, false, true);
					gtaSA_bulletsHit = ReadValue(p[0].Handle, addressGtaSA_bulletsHits, false, true);
					gtaSA_accuracy = (float)gtaSA_bulletsHit / (float)gtaSA_bulletsFired * 100f;
					statsText = statsText + "\nAccuracy: \n   " + gtaSA_bulletsHit.ToString() + "/" + gtaSA_bulletsFired.ToString() + " (" + gtaSA_accuracy.ToString("0.00") + "%)";
					/* --------- Explosives ----------- */
					gtaSA_kgExplosives = ReadValue(p[0].Handle, addressGtaSA_kgExplosives, false, true);
					statsText = statsText + "\nExplosives Used: \n   " + gtaSA_kgExplosives;
					/* --------- Destruction ---------- */
					gtaSA_propertyDamage = ReadValue(p[0].Handle, addressGtaSA_propertyDamage, false, true);
					gtaSA_peopleWastedOthers = ReadValue(p[0].Handle, addressGtaSA_peopleWastedOthers, false, true);
					gtaSA_peopleWastedYou = ReadValue(p[0].Handle, addressGtaSA_peopleWastedYou, false, true);
					statsText = statsText + " kg\nKills: \n   " + gtaSA_peopleWastedYou.ToString() + " by player \n   " + gtaSA_peopleWastedOthers.ToString() + " by others\n   " + (gtaSA_peopleWastedOthers + gtaSA_peopleWastedYou).ToString() + " Total\nProperty Damage: \n   $" + gtaSA_propertyDamage.ToString() + ".00";
					/* -------- Criminal Rating --------- */
					gtaSA_legitimateKills = ReadValue(p[0].Handle, addressGtaSA_legitimateKills, false, true);
					gtaSA_timesWasted = ReadValue(p[0].Handle, addressGtaSA_timesWasted, false, true);
					gtaSA_timesBusted = ReadValue(p[0].Handle, addressGtaSA_timesBusted, false, true);
					gtaSA_firefighterLevel = ReadValue(p[0].Handle, addressGtaSA_firefighterLevel, false, true);
					gtaSA_firefighterTargets = ReadValue(p[0].Handle, addressGtaSA_firefighterTargets, false, true);
					gtaSA_paramedicLevel = ReadValue(p[0].Handle, addressGtaSA_paramedicLevel, false, true);
					gtaSA_paramedicTargets = ReadValue(p[0].Handle, addressGtaSA_paramedicTargets, false, true);
					gtaSA_vigilanteTargets = ReadValue(p[0].Handle, addressGtaSA_vigilanteTargets, false, true);
					gtaSA_moneyOwned = ReadValue(p[0].Handle, addressGtaSA_money, false, true);
					gtaSA_aircraftDestroyed = ReadValue(p[0].Handle, addressGtaSA_aircraftDestroyed, false, true);
					gtaSA_timesCheated = ReadValue(p[0].Handle, addressGtaSA_timesCheated, false, true);
					gtaSA_percentage = ReadValue(p[0].Handle, addressGtaSA_percentage, true, false);
					gtaSA_criminalRating = gtaSA_legitimateKills + ((gtaSA_firefighterLevel + gtaSA_paramedicLevel) * 10) + (gtaSA_aircraftDestroyed * 30) + gtaSA_firefighterTargets + gtaSA_paramedicTargets + gtaSA_vigilanteTargets - (gtaSA_timesCheated * 10) - (gtaSA_timesBusted * 3) - (gtaSA_timesWasted * 3);
					//criminalRating = (float)legitimateKills - (float)timesBusted*3f - (float)timesWasted*3f + ((float)firefighterLevel *10f) + ((float)paramedicLevel * 10f) + ((float)moneyOwned / 5000f) + ((float)aircraftDestroyed * 30f) + (float)firefighterTargets + (float)vigilanteTargets + (float)paramedicTargets - ((float)timesCheated * 10f);
					gtaSA_criminalRating += (gtaSA_moneyOwned / 5000);
					if (gtaSA_bulletsFired > 100) {
						gtaSA_criminalRating += (500 * gtaSA_bulletsHit / gtaSA_bulletsFired);
					}
					gtaSA_criminalRating += (1000 * (int)gtaSA_percentage / 187);
					statsText = statsText + "\nCriminal Rating: \n   " + gtaSA_criminalRating.ToString();
					/* --------- Percentage -------- */
					statsText = statsText + "\nPercentage Completed: \n   " + ((gtaSA_percentage / 187f) * 100f).ToString("0.00");
					form1.label2.Text = statsText;
				}
				else if (selectedGame == games.III) {
					/* --------- distance -------- */
					gtaIII_distanceFoot = ReadValue(p[0].Handle, addressGtaIII_distanceFoot, true, false);
					gtaIII_distanceCar = ReadValue(p[0].Handle, addressGtaIII_distanceCar, true, false);
					gtaIII_totalDistanceTraveled = gtaIII_distanceCar + gtaIII_distanceFoot;
					statsText = "Distance Traveled: \n   " + (gtaIII_totalDistanceTraveled).ToString() + " m";
					/* --------- shots fired -------- */
					gtaIII_bulletsFired = ReadValue(p[0].Handle, addressGtaIII_bulletsFired, false, true);
					gtaIII_bulletsHit = ReadValue(p[0].Handle, addressGtaIII_bulletsHits, false, true);
					gtaIII_accuracy = (float)gtaIII_bulletsHit / (float)gtaIII_bulletsFired * 100f;
					gtaIII_wakaGarageEntered = ReadValue(p[0].Handle, addressGtaIII_wakaGarageEntered, false, true);
					if (gtaIII_wakaGarageEntered == 1 && gtaIII_wakaPassed != 1) {
						gtaIII_wakaPassed = ReadValue(p[0].Handle, addressGtaIII_wakaPassed, false, true);
						if (addressGtaIII_wakaPassed != 1) {
							statsText = statsText + "\nAccuracy: \n   " + "?" + "/" + gtaIII_bulletsFired.ToString() + " (" + "?" + "%) -- ERROR";
						}
					}
					else {
						statsText = statsText + "\nAccuracy: \n   " + gtaIII_bulletsHit.ToString() + "/" + gtaIII_bulletsFired.ToString() + " (" + gtaIII_accuracy.ToString("0.00") + "%)";
					}
					/* --------- Explosives ----------- */
					gtaIII_carsDestroyed = ReadValue(p[0].Handle, addressGtaIII_carsDestroyed, false, true);
					statsText = statsText + "\nCars Blown Up: \n   " + gtaIII_carsDestroyed;
					/* --------- Destruction ---------- */
					gtaIII_aircraftDestroyed = ReadValue(p[0].Handle, addressGtaIII_aircraftDestroyed, false, true);
					gtaIII_peopleWastedOthers = ReadValue(p[0].Handle, addressGtaIII_peopleWastedOthers, false, true);
					gtaIII_peopleWastedYou = ReadValue(p[0].Handle, addressGtaIII_peopleWastedYou, false, true);
					if (gtaIII_wakaGarageEntered == 1 && gtaIII_wakaPassed != 1) {
						statsText = statsText + "\nHelicopters Blown Up: \n   " + gtaIII_aircraftDestroyed.ToString() + "\nKills: \n   " + "?" + " by player -- ERROR \n   " + gtaIII_peopleWastedOthers.ToString() + " by others\n   " + "?" + " Total -- ERROR";
					}
					else {
						statsText = statsText + "\nHelicopters Blown Up: \n   " + gtaIII_aircraftDestroyed.ToString() + "\nKills: \n   " + gtaIII_peopleWastedYou.ToString() + " by player \n   " + gtaIII_peopleWastedOthers.ToString() + " by others\n   " + (gtaIII_peopleWastedOthers + gtaIII_peopleWastedYou).ToString() + " Total";
					}
					/* -------- Criminal Rating --------- */
					gtaIII_legitimateKills = ReadValue(p[0].Handle, addressGtaIII_legitimateKills, false, true);
					gtaIII_timesWasted = ReadValue(p[0].Handle, addressGtaIII_timesWasted, false, true);
					gtaIII_timesBusted = ReadValue(p[0].Handle, addressGtaIII_timesBusted, false, true);
					//gtaIII_firefighterLevel = ReadValue(p[0].Handle, addressGtaIII_firefighterLevel, false, true);
					gtaIII_firefighterTargets = ReadValue(p[0].Handle, addressGtaIII_firefighterTargets, false, true);
					gtaIII_paramedicLevel = ReadValue(p[0].Handle, addressGtaIII_paramedicLevel, false, true);
					gtaIII_paramedicTargets = ReadValue(p[0].Handle, addressGtaIII_paramedicTargets, false, true);
					gtaIII_vigilanteTargets = ReadValue(p[0].Handle, addressGtaIII_vigilanteTargets, false, true);
					gtaIII_moneyOwned = ReadValue(p[0].Handle, addressGtaIII_money, false, true);
					gtaIII_aircraftDestroyed = ReadValue(p[0].Handle, addressGtaIII_aircraftDestroyed, false, true);
					//gtaIII_timesCheated = ReadValue(p[0].Handle, addressGtaIII_timesCheated, false, true);
					gtaIII_percentage = ReadValue(p[0].Handle, addressGtaIII_percentage, false, true);
					gtaIII_criminalRating = gtaIII_legitimateKills + ((gtaIII_paramedicLevel) * 10) + (gtaIII_aircraftDestroyed * 30) + gtaIII_firefighterTargets + gtaIII_paramedicTargets + gtaIII_vigilanteTargets - (gtaIII_timesBusted * 3) - (gtaIII_timesWasted * 3);
					gtaIII_criminalRating += (gtaIII_moneyOwned / 5000);
					if (gtaIII_bulletsFired > 100) {
						gtaIII_criminalRating += (500 * gtaIII_bulletsHit / gtaIII_bulletsFired);
					}
					gtaIII_criminalRating += (1000 * (int)gtaIII_percentage / 154);
					//not bothering with doing the staunton/shoreside limiting. I wont reach the threshold.
					if (gtaIII_wakaGarageEntered == 1 && gtaIII_wakaPassed != 1) {
						statsText = statsText + "\nCriminal Rating: \n   " + "? -- ERROR";
					}
					else {
						statsText = statsText + "\nCriminal Rating: \n   " + gtaIII_criminalRating.ToString();
					}
					/* --------- Percentage -------- */
					Debug.WriteLine(gtaIII_percentage);
					statsText = statsText + "\nPercentage Completed: \n   " + ((gtaIII_percentage / 154f) * 100f).ToString("0.00");
					if (gtaIII_wakaGarageEntered == 1 && gtaIII_wakaPassed != 1) {
						statsText = statsText + "\nERROR: Player in garage during Waka-Gashira...";
					}
					form1.label2.Text = statsText;
				}
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
