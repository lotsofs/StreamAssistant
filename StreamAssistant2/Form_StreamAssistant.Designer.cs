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
			this.sceneManager_Label = new System.Windows.Forms.Label();
			this.sceneManager_EnabledCheckBox = new System.Windows.Forms.CheckBox();
			this.sceneManager_groupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// sceneManager_groupBox
			// 
			this.sceneManager_groupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_Label);
			this.sceneManager_groupBox.Controls.Add(this.sceneManager_EnabledCheckBox);
			this.sceneManager_groupBox.Location = new System.Drawing.Point(12, 12);
			this.sceneManager_groupBox.Name = "sceneManager_groupBox";
			this.sceneManager_groupBox.Size = new System.Drawing.Size(261, 158);
			this.sceneManager_groupBox.TabIndex = 3;
			this.sceneManager_groupBox.TabStop = false;
			this.sceneManager_groupBox.Text = "Scene Manager";
			// 
			// sceneManager_Label
			// 
			this.sceneManager_Label.AutoSize = true;
			this.sceneManager_Label.Location = new System.Drawing.Point(3, 39);
			this.sceneManager_Label.Name = "sceneManager_Label";
			this.sceneManager_Label.Size = new System.Drawing.Size(95, 13);
			this.sceneManager_Label.TabIndex = 1;
			this.sceneManager_Label.Text = "I\'m hardcoding this";
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
	}
}

