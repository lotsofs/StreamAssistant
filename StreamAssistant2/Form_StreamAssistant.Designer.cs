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
			components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_StreamAssistant));
			buttonEnable = new Button();
			backgroundWorkerTcp = new System.ComponentModel.BackgroundWorker();
			buttonTest = new Button();
			timerUpdate = new System.Windows.Forms.Timer(components);
			SuspendLayout();
			// 
			// buttonEnable
			// 
			buttonEnable.Font = new Font("Sofia Sans Semi Condensed", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
			buttonEnable.Location = new Point(931, 14);
			buttonEnable.Margin = new Padding(4, 3, 4, 3);
			buttonEnable.Name = "buttonEnable";
			buttonEnable.Size = new Size(141, 70);
			buttonEnable.TabIndex = 0;
			buttonEnable.Text = "Enable";
			buttonEnable.UseVisualStyleBackColor = true;
			buttonEnable.Click += buttonEnable_Click;
			// 
			// backgroundWorkerTcp
			// 
			backgroundWorkerTcp.DoWork += backgroundWorkerTcp_DoWork;
			// 
			// buttonTest
			// 
			buttonTest.Font = new Font("Sofia Sans Semi Condensed", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
			buttonTest.Location = new Point(931, 99);
			buttonTest.Margin = new Padding(4, 3, 4, 3);
			buttonTest.Name = "buttonTest";
			buttonTest.Size = new Size(141, 70);
			buttonTest.TabIndex = 1;
			buttonTest.Text = "Test";
			buttonTest.UseVisualStyleBackColor = true;
			buttonTest.Click += buttonTest_Click;
			// 
			// timerUpdate
			// 
			timerUpdate.Interval = 15;
			timerUpdate.Tick += timerUpdate_Tick;
			// 
			// Form_StreamAssistant
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = SystemColors.ControlDark;
			ClientSize = new Size(1086, 661);
			Controls.Add(buttonTest);
			Controls.Add(buttonEnable);
			Icon = (Icon)resources.GetObject("$this.Icon");
			Margin = new Padding(4, 3, 4, 3);
			Name = "Form_StreamAssistant";
			Text = "Stream Assistant";
			FormClosing += Form_StreamAssistant_FormClosing;
			ResumeLayout(false);
		}

		#endregion

		private System.Windows.Forms.Button buttonEnable;
		private System.ComponentModel.BackgroundWorker backgroundWorkerTcp;
		private Button buttonTest;
		private System.Windows.Forms.Timer timerUpdate;
	}
}

