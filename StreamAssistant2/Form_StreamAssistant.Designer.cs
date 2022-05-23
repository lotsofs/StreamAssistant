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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_StreamAssistant));
			this.sceneManager_groupBox = new System.Windows.Forms.GroupBox();
			this.sceneManager_ButtonSuspend0 = new System.Windows.Forms.Button();
			this.sceneManager_ButtonSuspend720 = new System.Windows.Forms.Button();
			this.sceneManager_ButtonSuspend60 = new System.Windows.Forms.Button();
			this.sceneManager_ButtonSuspend5 = new System.Windows.Forms.Button();
			this.sceneManager_Label = new System.Windows.Forms.Label();
			this.sceneManager_EnabledCheckBox = new System.Windows.Forms.CheckBox();
			this.sceneManager_TextBox = new System.Windows.Forms.TextBox();
			this.sceneManager_groupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// sceneManager_groupBox
			// 
			this.sceneManager_groupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_TextBox);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_ButtonSuspend0);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_ButtonSuspend720);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_ButtonSuspend60);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_ButtonSuspend5);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_Label);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_EnabledCheckBox);
			this.sceneManager_groupBox.Location = new System.Drawing.Point(12, 12);
			this.sceneManager_groupBox.Name = "sceneManager_groupBox";
			this.sceneManager_groupBox.Size = new System.Drawing.Size(261, 158);
			this.sceneManager_groupBox.TabIndex = 3;
			this.sceneManager_groupBox.TabStop = false;
			this.sceneManager_groupBox.Text = "Scene Manager";
			// 
			// sceneManager_ButtonSuspend0
			// 
			this.sceneManager_ButtonSuspend0.Location = new System.Drawing.Point(83, 19);
			this.sceneManager_ButtonSuspend0.Name = "sceneManager_ButtonSuspend0";
			this.sceneManager_ButtonSuspend0.Size = new System.Drawing.Size(172, 23);
			this.sceneManager_ButtonSuspend0.TabIndex = 6;
			this.sceneManager_ButtonSuspend0.Text = "Continue / Next";
			this.sceneManager_ButtonSuspend0.UseVisualStyleBackColor = true;
			this.sceneManager_ButtonSuspend0.Click += new System.EventHandler(this.sceneManager_ButtonSuspend0_Click);
			// 
			// sceneManager_ButtonSuspend720
			// 
			this.sceneManager_ButtonSuspend720.Location = new System.Drawing.Point(83, 106);
			this.sceneManager_ButtonSuspend720.Name = "sceneManager_ButtonSuspend720";
			this.sceneManager_ButtonSuspend720.Size = new System.Drawing.Size(172, 23);
			this.sceneManager_ButtonSuspend720.TabIndex = 5;
			this.sceneManager_ButtonSuspend720.Text = "Suspend for 720 minutes";
			this.sceneManager_ButtonSuspend720.UseVisualStyleBackColor = true;
			this.sceneManager_ButtonSuspend720.Click += new System.EventHandler(this.sceneManager_ButtonSuspend720_Click);
			// 
			// sceneManager_ButtonSuspend60
			// 
			this.sceneManager_ButtonSuspend60.Location = new System.Drawing.Point(83, 77);
			this.sceneManager_ButtonSuspend60.Name = "sceneManager_ButtonSuspend60";
			this.sceneManager_ButtonSuspend60.Size = new System.Drawing.Size(172, 23);
			this.sceneManager_ButtonSuspend60.TabIndex = 4;
			this.sceneManager_ButtonSuspend60.Text = "Suspend for 60 minutes";
			this.sceneManager_ButtonSuspend60.UseVisualStyleBackColor = true;
			this.sceneManager_ButtonSuspend60.Click += new System.EventHandler(this.sceneManager_ButtonSuspend60_Click);
			// 
			// sceneManager_ButtonSuspend5
			// 
			this.sceneManager_ButtonSuspend5.Location = new System.Drawing.Point(83, 48);
			this.sceneManager_ButtonSuspend5.Name = "sceneManager_ButtonSuspend5";
			this.sceneManager_ButtonSuspend5.Size = new System.Drawing.Size(172, 23);
			this.sceneManager_ButtonSuspend5.TabIndex = 3;
			this.sceneManager_ButtonSuspend5.Text = "Suspend for 5 minutes";
			this.sceneManager_ButtonSuspend5.UseVisualStyleBackColor = true;
			this.sceneManager_ButtonSuspend5.Click += new System.EventHandler(this.sceneManager_ButtonSuspend5_Click);
			// 
			// sceneManager_Label
			// 
			this.sceneManager_Label.AutoSize = true;
			this.sceneManager_Label.Location = new System.Drawing.Point(6, 90);
			this.sceneManager_Label.Name = "sceneManager_Label";
			this.sceneManager_Label.Size = new System.Drawing.Size(47, 13);
			this.sceneManager_Label.TabIndex = 1;
			this.sceneManager_Label.Text = "Override";
			// 
			// sceneManager_EnabledCheckBox
			// 
			this.sceneManager_EnabledCheckBox.AutoSize = true;
			this.sceneManager_EnabledCheckBox.Location = new System.Drawing.Point(6, 19);
			this.sceneManager_EnabledCheckBox.Name = "sceneManager_EnabledCheckBox";
			this.sceneManager_EnabledCheckBox.Size = new System.Drawing.Size(59, 17);
			this.sceneManager_EnabledCheckBox.TabIndex = 0;
			this.sceneManager_EnabledCheckBox.Text = "Enable";
			this.sceneManager_EnabledCheckBox.UseVisualStyleBackColor = true;
			this.sceneManager_EnabledCheckBox.CheckedChanged += new System.EventHandler(this.sceneManager_EnabledCheckBox_CheckedChanged);
			// 
			// sceneManager_TextBox
			// 
			this.sceneManager_TextBox.Location = new System.Drawing.Point(6, 106);
			this.sceneManager_TextBox.Name = "sceneManager_TextBox";
			this.sceneManager_TextBox.Size = new System.Drawing.Size(71, 20);
			this.sceneManager_TextBox.TabIndex = 7;
			this.sceneManager_TextBox.TextChanged += new System.EventHandler(this.sceneManager_TextBox_TextChanged);
			// 
			// Form_StreamAssistant
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(285, 182);
			this.Controls.Add(this.sceneManager_groupBox);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "Form_StreamAssistant";
			this.Text = "Stream Assistant";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_StreamAssistant_FormClosing);
			this.sceneManager_groupBox.ResumeLayout(false);
			this.sceneManager_groupBox.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.GroupBox sceneManager_groupBox;
		private System.Windows.Forms.Label sceneManager_Label;
		private System.Windows.Forms.CheckBox sceneManager_EnabledCheckBox;
		private System.Windows.Forms.Button sceneManager_ButtonSuspend5;
		private System.Windows.Forms.Button sceneManager_ButtonSuspend60;
		private System.Windows.Forms.Button sceneManager_ButtonSuspend720;
		private System.Windows.Forms.Button sceneManager_ButtonSuspend0;
		private System.Windows.Forms.TextBox sceneManager_TextBox;
	}
}

