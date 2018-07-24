namespace StreamAssistant2
{
	partial class Form_NotificationsViewer
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_NotificationsViewer));
			this.donationInfoLabel = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// donationInfoLabel
			// 
			this.donationInfoLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.donationInfoLabel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
			this.donationInfoLabel.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.donationInfoLabel.Location = new System.Drawing.Point(0, 0);
			this.donationInfoLabel.Margin = new System.Windows.Forms.Padding(0);
			this.donationInfoLabel.Name = "donationInfoLabel";
			this.donationInfoLabel.Size = new System.Drawing.Size(533, 371);
			this.donationInfoLabel.TabIndex = 0;
			this.donationInfoLabel.Text = resources.GetString("donationInfoLabel.Text");
			// 
			// Form_NotificationsViewer
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(533, 224);
			this.Controls.Add(this.donationInfoLabel);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "Form_NotificationsViewer";
			this.Text = "Stream Assistant: Notifications Viewer";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label donationInfoLabel;
	}
}