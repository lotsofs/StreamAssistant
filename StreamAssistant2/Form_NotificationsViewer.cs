using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StreamAssistant2
{
	public partial class Form_NotificationsViewer : Form
	{
		const string DefaultText = "No donations yet";

		public Form_NotificationsViewer(Notifications notif) {
			InitializeComponent();
			UpdateTextStartup(notif.LastDonation());
		}

		/// <summary>
		/// Updates the text to show the last donation for the user to read
		/// </summary>
		/// <param name="text"></param>
		public void UpdateText(string text) {
			donationInfoLabel.Text = text;
		}

		/// <summary>
		/// Updates the text to show the last donation for the user to read. if there is none, puts a default message
		/// </summary>
		/// <param name="text"></param>
		public void UpdateTextStartup(string text) {
			if (string.IsNullOrEmpty(text)) {
				donationInfoLabel.Text = DefaultText;
			}
			else {
				donationInfoLabel.Text = text;
			}
		}

	}
}
