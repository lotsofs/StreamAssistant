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
		public Form_NotificationsViewer(Notifications notif) {
			InitializeComponent();
			UpdateText(notif.LastDonation());
		}

		/// <summary>
		/// Updates the text to show the last donation for the user to read
		/// </summary>
		/// <param name="text"></param>
		public void UpdateText(string text) {
			donationInfoLabel.Text = text;
		}
	}
}
