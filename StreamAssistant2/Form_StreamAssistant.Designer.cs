namespace StreamAssistant2
{
	partial class Form_StreamAssistant
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
			this.statsDisplay_EnabledCheckBox = new System.Windows.Forms.CheckBox();
			this.statsDisplay_GroupBox = new System.Windows.Forms.GroupBox();
			this.statsDisplay_ComboBox = new System.Windows.Forms.ComboBox();
			this.notifications_GroupBox = new System.Windows.Forms.GroupBox();
			this.notifications_ConfigButton = new System.Windows.Forms.Button();
			this.notifications_enabledCheckBox = new System.Windows.Forms.CheckBox();
			this.statsDisplay_GroupBox.SuspendLayout();
			this.notifications_GroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// statsDisplay_EnabledCheckBox
			// 
			this.statsDisplay_EnabledCheckBox.AutoSize = true;
			this.statsDisplay_EnabledCheckBox.Enabled = false;
			this.statsDisplay_EnabledCheckBox.Location = new System.Drawing.Point(6, 19);
			this.statsDisplay_EnabledCheckBox.Name = "statsDisplay_EnabledCheckBox";
			this.statsDisplay_EnabledCheckBox.Size = new System.Drawing.Size(59, 17);
			this.statsDisplay_EnabledCheckBox.TabIndex = 0;
			this.statsDisplay_EnabledCheckBox.Text = "Enable";
			this.statsDisplay_EnabledCheckBox.UseVisualStyleBackColor = true;
			this.statsDisplay_EnabledCheckBox.CheckedChanged += new System.EventHandler(this.statsDisplay_EnabledCheckBox_CheckedChanged);
			// 
			// statsDisplay_GroupBox
			// 
			this.statsDisplay_GroupBox.Controls.Add(this.statsDisplay_ComboBox);
			this.statsDisplay_GroupBox.Controls.Add(this.statsDisplay_EnabledCheckBox);
			this.statsDisplay_GroupBox.Location = new System.Drawing.Point(12, 12);
			this.statsDisplay_GroupBox.Name = "statsDisplay_GroupBox";
			this.statsDisplay_GroupBox.Size = new System.Drawing.Size(189, 71);
			this.statsDisplay_GroupBox.TabIndex = 1;
			this.statsDisplay_GroupBox.TabStop = false;
			this.statsDisplay_GroupBox.Text = "Stats Display";
			// 
			// statsDisplay_ComboBox
			// 
			this.statsDisplay_ComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.statsDisplay_ComboBox.Enabled = false;
			this.statsDisplay_ComboBox.FormattingEnabled = true;
			this.statsDisplay_ComboBox.Location = new System.Drawing.Point(6, 42);
			this.statsDisplay_ComboBox.Name = "statsDisplay_ComboBox";
			this.statsDisplay_ComboBox.Size = new System.Drawing.Size(177, 21);
			this.statsDisplay_ComboBox.TabIndex = 1;
			// 
			// notifications_GroupBox
			// 
			this.notifications_GroupBox.Controls.Add(this.notifications_ConfigButton);
			this.notifications_GroupBox.Controls.Add(this.notifications_enabledCheckBox);
			this.notifications_GroupBox.Location = new System.Drawing.Point(12, 89);
			this.notifications_GroupBox.Name = "notifications_GroupBox";
			this.notifications_GroupBox.Size = new System.Drawing.Size(189, 42);
			this.notifications_GroupBox.TabIndex = 2;
			this.notifications_GroupBox.TabStop = false;
			this.notifications_GroupBox.Text = "Notifications";
			// 
			// notifications_ConfigButton
			// 
			this.notifications_ConfigButton.Enabled = false;
			this.notifications_ConfigButton.Location = new System.Drawing.Point(108, 13);
			this.notifications_ConfigButton.Name = "notifications_ConfigButton";
			this.notifications_ConfigButton.Size = new System.Drawing.Size(75, 23);
			this.notifications_ConfigButton.TabIndex = 1;
			this.notifications_ConfigButton.Text = "Configure";
			this.notifications_ConfigButton.UseVisualStyleBackColor = true;
			this.notifications_ConfigButton.Click += new System.EventHandler(this.notifications_ConfigButton_Click);
			// 
			// notifications_enabledCheckBox
			// 
			this.notifications_enabledCheckBox.AutoSize = true;
			this.notifications_enabledCheckBox.Location = new System.Drawing.Point(6, 19);
			this.notifications_enabledCheckBox.Name = "notifications_enabledCheckBox";
			this.notifications_enabledCheckBox.Size = new System.Drawing.Size(59, 17);
			this.notifications_enabledCheckBox.TabIndex = 0;
			this.notifications_enabledCheckBox.Text = "Enable";
			this.notifications_enabledCheckBox.UseVisualStyleBackColor = true;
			this.notifications_enabledCheckBox.CheckedChanged += new System.EventHandler(this.notifications_enabledCheckBox_CheckedChanged);
			// 
			// Form_StreamAssistant
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.notifications_GroupBox);
			this.Controls.Add(this.statsDisplay_GroupBox);
			this.Name = "Form_StreamAssistant";
			this.Text = "Form1";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_StreamAssistant_FormClosing);
			this.statsDisplay_GroupBox.ResumeLayout(false);
			this.statsDisplay_GroupBox.PerformLayout();
			this.notifications_GroupBox.ResumeLayout(false);
			this.notifications_GroupBox.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.CheckBox statsDisplay_EnabledCheckBox;
		private System.Windows.Forms.GroupBox statsDisplay_GroupBox;
		private System.Windows.Forms.ComboBox statsDisplay_ComboBox;
		private System.Windows.Forms.GroupBox notifications_GroupBox;
		private System.Windows.Forms.CheckBox notifications_enabledCheckBox;
		private System.Windows.Forms.Button notifications_ConfigButton;
	}
}

