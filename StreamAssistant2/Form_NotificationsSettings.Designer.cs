namespace StreamAssistant2
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
			this.testSound_Button = new System.Windows.Forms.Button();
			this.testSound_comboBox = new System.Windows.Forms.ComboBox();
			this.groupBox_Sounds = new System.Windows.Forms.GroupBox();
			this.groupBox_Sounds.SuspendLayout();
			this.SuspendLayout();
			// 
			// audioDevice_ComboBox
			// 
			this.audioDevice_ComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.audioDevice_ComboBox.FormattingEnabled = true;
			this.audioDevice_ComboBox.Location = new System.Drawing.Point(12, 25);
			this.audioDevice_ComboBox.Name = "audioDevice_ComboBox";
			this.audioDevice_ComboBox.Size = new System.Drawing.Size(347, 21);
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
			this.button_Ok.Location = new System.Drawing.Point(196, 283);
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
			this.button1.Location = new System.Drawing.Point(277, 283);
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
			this.button_Apply.Location = new System.Drawing.Point(115, 283);
			this.button_Apply.Name = "button_Apply";
			this.button_Apply.Size = new System.Drawing.Size(75, 23);
			this.button_Apply.TabIndex = 6;
			this.button_Apply.Text = "Apply";
			this.button_Apply.UseVisualStyleBackColor = true;
			this.button_Apply.Click += new System.EventHandler(this.button_Apply_Click);
			// 
			// testSound_Button
			// 
			this.testSound_Button.Location = new System.Drawing.Point(6, 19);
			this.testSound_Button.Name = "testSound_Button";
			this.testSound_Button.Size = new System.Drawing.Size(75, 23);
			this.testSound_Button.TabIndex = 7;
			this.testSound_Button.Text = "Test Sound";
			this.testSound_Button.UseVisualStyleBackColor = true;
			this.testSound_Button.Click += new System.EventHandler(this.testSound_Button_Click);
			// 
			// testSound_comboBox
			// 
			this.testSound_comboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.testSound_comboBox.FormattingEnabled = true;
			this.testSound_comboBox.Location = new System.Drawing.Point(87, 21);
			this.testSound_comboBox.Name = "testSound_comboBox";
			this.testSound_comboBox.Size = new System.Drawing.Size(253, 21);
			this.testSound_comboBox.TabIndex = 8;
			// 
			// groupBox_Sounds
			// 
			this.groupBox_Sounds.Controls.Add(this.testSound_Button);
			this.groupBox_Sounds.Controls.Add(this.testSound_comboBox);
			this.groupBox_Sounds.Location = new System.Drawing.Point(12, 52);
			this.groupBox_Sounds.Name = "groupBox_Sounds";
			this.groupBox_Sounds.Size = new System.Drawing.Size(347, 214);
			this.groupBox_Sounds.TabIndex = 9;
			this.groupBox_Sounds.TabStop = false;
			this.groupBox_Sounds.Text = "Sounds";
			// 
			// NotificationsSettings
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.Control;
			this.ClientSize = new System.Drawing.Size(371, 318);
			this.Controls.Add(this.groupBox_Sounds);
			this.Controls.Add(this.button_Apply);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.button_Ok);
			this.Controls.Add(this.audioDevice_Label);
			this.Controls.Add(this.audioDevice_ComboBox);
			this.Name = "NotificationsSettings";
			this.Text = "Stream Notifications Settings";
			this.groupBox_Sounds.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ComboBox audioDevice_ComboBox;
		private System.Windows.Forms.Label audioDevice_Label;
		private System.Windows.Forms.Button button_Ok;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button_Apply;
		private System.Windows.Forms.Button testSound_Button;
		private System.Windows.Forms.ComboBox testSound_comboBox;
		private System.Windows.Forms.GroupBox groupBox_Sounds;
	}
}