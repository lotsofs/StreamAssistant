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
	class Notifications	{
		AudioPlayer audioPlayer;
		string lastSubscriber;
		string lastDonator;
		string lastDonation;
		string lastDonationMessage;
		string lastCheerer;
		string lastCheer;
		string lastCheerMessage;

		public Notifications(AudioPlayer ap) {
			audioPlayer = ap;
		}

		/// <summary>
		/// Subscription happening
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Subscription(FileSystemEventArgs e) {
			string subscriptionInfo = EventInformation(e.FullPath);
			if (string.IsNullOrEmpty(subscriptionInfo)) {
				lastSubscriber = string.Empty;
				return;
			}

			string subscriber = UserName(subscriptionInfo);
			if (subscriber == lastSubscriber) {
				return;
			}

			if (lastSubscriber != null) {
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
				lastCheerer = string.Empty;
				lastCheer = string.Empty;
				lastCheerMessage = string.Empty;
				return;
			}

			string cheerer = UserName(bitsInfo);
			string cheer = Amount(bitsInfo);
			string cheerMessage = Message(bitsInfo);
			if (cheerer == lastCheerer && cheer == lastCheer && cheerMessage == lastCheerMessage) {
				return;
			}

			if (lastCheerer != null) {
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
				lastDonator = string.Empty;
				lastDonation = string.Empty;
				lastDonationMessage = string.Empty;
				return;
			}

			string donator = UserName(donationInfo);
			string donation = Amount(donationInfo);
			string donationMessage = Message(donationInfo);
			if (donator == lastDonator && donation == lastDonation && donationMessage == lastDonationMessage) {
				return;
			}

			if (lastDonator != null) {
				audioPlayer.PlaySound(TwitchEvents.Donation);
			}
			lastDonator = donator;
			lastDonation = donation;
			lastDonationMessage = donationMessage;
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

		string EventInformation(string path) {
			try {
				string info = File.ReadAllText(path);
				return info;
			}
			catch (IOException) {
				return string.Empty;
			}
		}

	}
}
