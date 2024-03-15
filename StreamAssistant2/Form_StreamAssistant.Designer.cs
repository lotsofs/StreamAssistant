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
			this.buttonEnable = new System.Windows.Forms.Button();
			this.backgroundWorkerTcp = new System.ComponentModel.BackgroundWorker();
			this.SuspendLayout();
			// 
			// buttonEnable
			// 
			this.buttonEnable.Font = new System.Drawing.Font("Sofia Sans Semi Condensed", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.buttonEnable.Location = new System.Drawing.Point(798, 12);
			this.buttonEnable.Name = "buttonEnable";
			this.buttonEnable.Size = new System.Drawing.Size(121, 61);
			this.buttonEnable.TabIndex = 0;
			this.buttonEnable.Text = "Enable";
			this.buttonEnable.UseVisualStyleBackColor = true;
			this.buttonEnable.Click += new System.EventHandler(this.buttonEnable_Click);
			// 
			// backgroundWorkerTcp
			// 
			this.backgroundWorkerTcp.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorkerTcp_DoWork);
			// 
			// Form_StreamAssistant
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.ControlDark;
			this.ClientSize = new System.Drawing.Size(931, 573);
			this.Controls.Add(this.buttonEnable);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "Form_StreamAssistant";
			this.Text = "Stream Assistant";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_StreamAssistant_FormClosing);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button buttonEnable;
		private System.ComponentModel.BackgroundWorker backgroundWorkerTcp;
	}
}

