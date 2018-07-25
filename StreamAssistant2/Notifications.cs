using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamAssistant2
{
	public class Notifications	{
		AudioPlayer audioPlayer;

		string lastSubscriber;

		string lastDonator;
		string lastDonation;
		string lastDonationMessage;

		public Action<string> OnDonation;

		string lastCheerer;
		string lastCheer;
		string lastCheerMessage;

		#region init

		public Notifications(AudioPlayer ap) {
			audioPlayer = ap;
		}

		#endregion

		public string LastDonation() {
			string donationInfo = String.Format("{0} {1} {2}", lastDonator, lastDonation, lastDonationMessage);
			return donationInfo;
		}

		#region twitch events happening

		/// <summary>
		/// Subscription happening
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Subscription(FileSystemEventArgs e) {
			string subscriptionInfo = EventInformation(e.FullPath);
			if (string.IsNullOrEmpty(subscriptionInfo)) {
				return;
			}

			string subscriber = UserName(subscriptionInfo);
			if (subscriber == lastSubscriber) {
				return;
			}

			if (!string.IsNullOrEmpty(lastSubscriber)) {
				audioPlayer.PlaySound(TwitchEvents.Subscription);
			}
			lastSubscriber = subscriber;
		}

		/// <summary>
		/// Bits happening
		/// </summary>
		/// <param name="e"></param>
		public void Bits(FileSystemEventArgs e) {
			string bitsInfo = EventInformation(e.FullPath);
			if (string.IsNullOrEmpty(bitsInfo)) {
				return;
			}

			string cheerer = UserName(bitsInfo);
			string cheer = Amount(bitsInfo);
			string cheerMessage = Message(bitsInfo);
			if (cheerer == lastCheerer && cheer == lastCheer && cheerMessage == lastCheerMessage) {
				return;
			}

			if (!string.IsNullOrEmpty(lastCheerer)) {
				audioPlayer.PlaySound(TwitchEvents.Bits);
			}
			lastCheerer = cheerer;
			lastCheer = cheer;
			lastCheerMessage = cheerMessage;
		}

		/// <summary>
		/// Donation happening
		/// </summary>
		/// <param name="e"></param>
		public void Donation(FileSystemEventArgs e) {
			string donationInfo = EventInformation(e.FullPath);
			if (string.IsNullOrEmpty(donationInfo)) {
				return;
			}

			string donator = UserName(donationInfo);
			string donation = Amount(donationInfo);
			string donationMessage = Message(donationInfo);
			if (donator == lastDonator && donation == lastDonation && donationMessage == lastDonationMessage) {
				return;
			}

			if (!string.IsNullOrEmpty(lastDonator)) {
				audioPlayer.PlaySound(TwitchEvents.Donation);
			}
			lastDonator = donator;
			lastDonation = donation;
			lastDonationMessage = donationMessage;

			if (OnDonation != null) {
				OnDonation(donationInfo);
			}
		}

		#endregion

		#region extract information from text files

		/// <summary>
		/// reads the text file and returns the text contained within
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		string EventInformation(string path) {
			try {
				string info = File.ReadAllText(path);
				return info;
			}
			catch (IOException) {
				return string.Empty;
			}
		}

		/// <summary>
		/// returns the donation/bits message which will be the third word and beyond
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		string Message(string info) {
			int first = 0;
			for (int i = 0; i < info.Length; i++) {
				if (info[i] == ' ') {
					first = i + 1;
					break;
				}
			}
			for (int i = first; i < info.Length; i++) {
				if (info[i] == ' ') {
					return info.Substring(i + 1);
				}
			}
			return info.Substring(first);
		}


		/// <summary>
		/// returns the donation/bits amount or subscription months count, which will be the second word
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		string Amount(string info) {
			int first = 0;
			for (int i = 0; i < info.Length; i++) {
				if (info[i] == ' ') {
					first = i + 1;
					break;
				}
			}
			for (int i = first; i < info.Length; i++) {
				if (info[i] == ' ') {
					return info.Substring(first, i - first);
				}
			}
			return info.Substring(first);
		}

		/// <summary>
		/// Gets the username of the sub/bits/donation which will be the first word
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		string UserName(string info) {
			for (int i = 0; i < info.Length; i++) {
				if (info[i] == ' ') {
					return info.Substring(0, i);
				}
			}
			return info;
		}

		#endregion



	}
}
