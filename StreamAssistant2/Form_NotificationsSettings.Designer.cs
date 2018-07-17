﻿namespace StreamAssistant2
{
	partial class Form_NotificationsSettings
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
			this.audioDevice_ComboBox = new System.Windows.Forms.ComboBox();
			this.audioDevice_Label = new System.Windows.Forms.Label();
			this.button_Ok = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.button_Apply = new System.Windows.Forms.Button();
			this.subscription_Button = new System.Windows.Forms.Button();
			this.subscription_comboBox = new System.Windows.Forms.ComboBox();
			this.groupBox_Sounds = new System.Windows.Forms.GroupBox();
			this.donation_VolumeText = new System.Windows.Forms.TextBox();
			this.donation_Button = new System.Windows.Forms.Button();
			this.donation_ComboBox = new System.Windows.Forms.ComboBox();
			this.bits_VolumeText = new System.Windows.Forms.TextBox();
			this.bits_Button = new System.Windows.Forms.Button();
			this.bits_ComboBox = new System.Windows.Forms.ComboBox();
			this.label_Volume = new System.Windows.Forms.Label();
			this.label_File = new System.Windows.Forms.Label();
			this.label_Test = new System.Windows.Forms.Label();
			this.subscription_VolumeText = new System.Windows.Forms.TextBox();
			this.groupBox_Sounds.SuspendLayout();
			this.SuspendLayout();
			// 
			// audioDevice_ComboBox
			// 
			this.audioDevice_ComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.audioDevice_ComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.audioDevice_ComboBox.FormattingEnabled = true;
			this.audioDevice_ComboBox.Location = new System.Drawing.Point(12, 25);
			this.audioDevice_ComboBox.Name = "audioDevice_ComboBox";
			this.audioDevice_ComboBox.Size = new System.Drawing.Size(324, 21);
			this.audioDevice_ComboBox.TabIndex = 2;
			// 
			// audioDevice_Label
			// 
			this.audioDevice_Label.AutoSize = true;
			this.audioDevice_Label.Location = new System.Drawing.Point(12, 9);
			this.audioDevice_Label.Name = "audioDevice_Label";
			this.audioDevice_Label.Size = new System.Drawing.Size(74, 13);
			this.audioDevice_Label.TabIndex = 3;
			this.audioDevice_Label.Text = "Audio Device:";
			// 
			// button_Ok
			// 
			this.button_Ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.button_Ok.Location = new System.Drawing.Point(173, 190);
			this.button_Ok.Name = "button_Ok";
			this.button_Ok.Size = new System.Drawing.Size(75, 23);
			this.button_Ok.TabIndex = 4;
			this.button_Ok.Text = "Ok";
			this.button_Ok.UseVisualStyleBackColor = true;
			this.button_Ok.Click += new System.EventHandler(this.button_Ok_Click);
			// 
			// button1
			// 
			this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.button1.Location = new System.Drawing.Point(254, 190);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(75, 23);
			this.button1.TabIndex = 5;
			this.button1.Text = "Cancel";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// button_Apply
			// 
			this.button_Apply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.button_Apply.Location = new System.Drawing.Point(92, 190);
			this.button_Apply.Name = "button_Apply";
			this.button_Apply.Size = new System.Drawing.Size(75, 23);
			this.button_Apply.TabIndex = 6;
			this.button_Apply.Text = "Apply";
			this.button_Apply.UseVisualStyleBackColor = true;
			this.button_Apply.Click += new System.EventHandler(this.button_Apply_Click);
			// 
			// subscription_Button
			// 
			this.subscription_Button.Location = new System.Drawing.Point(6, 30);
			this.subscription_Button.Name = "subscription_Button";
			this.subscription_Button.Size = new System.Drawing.Size(75, 23);
			this.subscription_Button.TabIndex = 7;
			this.subscription_Button.Text = "Subscription";
			this.subscription_Button.UseVisualStyleBackColor = true;
			this.subscription_Button.Click += new System.EventHandler(this.subscription_Button_Click);
			// 
			// subscription_comboBox
			// 
			this.subscription_comboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.subscription_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.subscription_comboBox.FormattingEnabled = true;
			this.subscription_comboBox.Items.AddRange(new object[] {
            "None"});
			this.subscription_comboBox.Location = new System.Drawing.Point(87, 32);
			this.subscription_comboBox.Name = "subscription_comboBox";
			this.subscription_comboBox.Size = new System.Drawing.Size(175, 21);
			this.subscription_comboBox.TabIndex = 8;
			// 
			// groupBox_Sounds
			// 
			this.groupBox_Sounds.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox_Sounds.Controls.Add(this.donation_VolumeText);
			this.groupBox_Sounds.Controls.Add(this.donation_Button);
			this.groupBox_Sounds.Controls.Add(this.donation_ComboBox);
			this.groupBox_Sounds.Controls.Add(this.bits_VolumeText);
			this.groupBox_Sounds.Controls.Add(this.bits_Button);
			this.groupBox_Sounds.Controls.Add(this.bits_ComboBox);
			this.groupBox_Sounds.Controls.Add(this.label_Volume);
			this.groupBox_Sounds.Controls.Add(this.label_File);
			this.groupBox_Sounds.Controls.Add(this.label_Test);
			this.groupBox_Sounds.Controls.Add(this.subscription_VolumeText);
			this.groupBox_Sounds.Controls.Add(this.subscription_Button);
			this.groupBox_Sounds.Controls.Add(this.subscription_comboBox);
			this.groupBox_Sounds.Location = new System.Drawing.Point(12, 52);
			this.groupBox_Sounds.Name = "groupBox_Sounds";
			this.groupBox_Sounds.Size = new System.Drawing.Size(324, 121);
			this.groupBox_Sounds.TabIndex = 9;
			this.groupBox_Sounds.TabStop = false;
			this.groupBox_Sounds.Text = "Sounds";
			// 
			// donation_VolumeText
			// 
			this.donation_VolumeText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.donation_VolumeText.Location = new System.Drawing.Point(268, 91);
			this.donation_VolumeText.Name = "donation_VolumeText";
			this.donation_VolumeText.Size = new System.Drawing.Size(49, 20);
			this.donation_VolumeText.TabIndex = 18;
			this.donation_VolumeText.Text = "100";
			this.donation_VolumeText.Leave += new System.EventHandler(this.donation_VolumeText_Leave);
			// 
			// donation_Button
			// 
			this.donation_Button.Location = new System.Drawing.Point(6, 88);
			this.donation_Button.Name = "donation_Button";
			this.donation_Button.Size = new System.Drawing.Size(75, 23);
			this.donation_Button.TabIndex = 16;
			this.donation_Button.Text = "Donation";
			this.donation_Button.UseVisualStyleBackColor = true;
			this.donation_Button.Click += new System.EventHandler(this.donation_Button_Click);
			// 
			// donation_ComboBox
			// 
			this.donation_ComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.donation_ComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.donation_ComboBox.FormattingEnabled = true;
			this.donation_ComboBox.Items.AddRange(new object[] {
            "None"});
			this.donation_ComboBox.Location = new System.Drawing.Point(87, 90);
			this.donation_ComboBox.Name = "donation_ComboBox";
			this.donation_ComboBox.Size = new System.Drawing.Size(175, 21);
			this.donation_ComboBox.TabIndex = 17;
			// 
			// bits_VolumeText
			// 
			this.bits_VolumeText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.bits_VolumeText.Location = new System.Drawing.Point(268, 62);
			this.bits_VolumeText.Name = "bits_VolumeText";
			this.bits_VolumeText.Size = new System.Drawing.Size(49, 20);
			this.bits_VolumeText.TabIndex = 15;
			this.bits_VolumeText.Text = "100";
			this.bits_VolumeText.Leave += new System.EventHandler(this.bits_VolumeText_Leave);
			// 
			// bits_Button
			// 
			this.bits_Button.Location = new System.Drawing.Point(6, 58);
			this.bits_Button.Name = "bits_Button";
			this.bits_Button.Size = new System.Drawing.Size(75, 23);
			this.bits_Button.TabIndex = 13;
			this.bits_Button.Text = "Bits";
			this.bits_Button.UseVisualStyleBackColor = true;
			this.bits_Button.Click += new System.EventHandler(this.bits_Button_Click);
			// 
			// bits_ComboBox
			// 
			this.bits_ComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.bits_ComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.bits_ComboBox.FormattingEnabled = true;
			this.bits_ComboBox.Items.AddRange(new object[] {
            "None"});
			this.bits_ComboBox.Location = new System.Drawing.Point(87, 61);
			this.bits_ComboBox.Name = "bits_ComboBox";
			this.bits_ComboBox.Size = new System.Drawing.Size(175, 21);
			this.bits_ComboBox.TabIndex = 14;
			// 
			// label_Volume
			// 
			this.label_Volume.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label_Volume.AutoSize = true;
			this.label_Volume.Location = new System.Drawing.Point(265, 16);
			this.label_Volume.Name = "label_Volume";
			this.label_Volume.Size = new System.Drawing.Size(53, 13);
			this.label_Volume.TabIndex = 12;
			this.label_Volume.Text = "Volume %";
			// 
			// label_File
			// 
			this.label_File.AutoSize = true;
			this.label_File.Location = new System.Drawing.Point(87, 16);
			this.label_File.Name = "label_File";
			this.label_File.Size = new System.Drawing.Size(57, 13);
			this.label_File.TabIndex = 11;
			this.label_File.Text = "Sound File";
			// 
			// label_Test
			// 
			this.label_Test.AutoSize = true;
			this.label_Test.Location = new System.Drawing.Point(6, 16);
			this.label_Test.Name = "label_Test";
			this.label_Test.Size = new System.Drawing.Size(51, 13);
			this.label_Test.TabIndex = 10;
			this.label_Test.Text = "Test Play";
			// 
			// subscription_VolumeText
			// 
			this.subscription_VolumeText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.subscription_VolumeText.Location = new System.Drawing.Point(268, 33);
			this.subscription_VolumeText.Name = "subscription_VolumeText";
			this.subscription_VolumeText.Size = new System.Drawing.Size(49, 20);
			this.subscription_VolumeText.TabIndex = 9;
			this.subscription_VolumeText.Text = "100";
			this.subscription_VolumeText.Leave += new System.EventHandler(this.subscription_VolumeText_Leave);
			// 
			// Form_NotificationsSettings
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.Control;
			this.ClientSize = new System.Drawing.Size(348, 225);
			this.Controls.Add(this.groupBox_Sounds);
			this.Controls.Add(this.button_Apply);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.button_Ok);
			this.Controls.Add(this.audioDevice_Label);
			this.Controls.Add(this.audioDevice_ComboBox);
			this.Name = "Form_NotificationsSettings";
			this.Text = "Stream Notifications Settings";
			this.groupBox_Sounds.ResumeLayout(false);
			this.groupBox_Sounds.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ComboBox audioDevice_ComboBox;
		private System.Windows.Forms.Label audioDevice_Label;
		private System.Windows.Forms.Button button_Ok;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button_Apply;
		private System.Windows.Forms.Button subscription_Button;
		private System.Windows.Forms.ComboBox subscription_comboBox;
		private System.Windows.Forms.GroupBox groupBox_Sounds;
		private System.Windows.Forms.Label label_Volume;
		private System.Windows.Forms.Label label_File;
		private System.Windows.Forms.Label label_Test;
		private System.Windows.Forms.TextBox subscription_VolumeText;
		private System.Windows.Forms.TextBox donation_VolumeText;
		private System.Windows.Forms.Button donation_Button;
		private System.Windows.Forms.ComboBox donation_ComboBox;
		private System.Windows.Forms.TextBox bits_VolumeText;
		private System.Windows.Forms.Button bits_Button;
		private System.Windows.Forms.ComboBox bits_ComboBox;
	}
}