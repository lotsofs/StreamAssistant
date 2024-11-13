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
			labelIncoming = new Label();
			labelOutgoing = new Label();
			textBoxIncoming = new TextBox();
			textBoxOutgoing = new TextBox();
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
			// labelIncoming
			// 
			labelIncoming.AutoSize = true;
			labelIncoming.Font = new Font("Segoe UI", 22F);
			labelIncoming.Location = new Point(12, -3);
			labelIncoming.Name = "labelIncoming";
			labelIncoming.Size = new Size(150, 41);
			labelIncoming.TabIndex = 4;
			labelIncoming.Text = "Incoming:";
			// 
			// labelOutgoing
			// 
			labelOutgoing.AutoSize = true;
			labelOutgoing.Font = new Font("Segoe UI", 22F);
			labelOutgoing.Location = new Point(12, 331);
			labelOutgoing.Name = "labelOutgoing";
			labelOutgoing.Size = new Size(153, 41);
			labelOutgoing.TabIndex = 5;
			labelOutgoing.Text = "Outgoing:";
			labelOutgoing.Click += label2_Click;
			// 
			// textBoxIncoming
			// 
			textBoxIncoming.AcceptsReturn = true;
			textBoxIncoming.AcceptsTab = true;
			textBoxIncoming.Location = new Point(12, 43);
			textBoxIncoming.Multiline = true;
			textBoxIncoming.Name = "textBoxIncoming";
			textBoxIncoming.ReadOnly = true;
			textBoxIncoming.ScrollBars = ScrollBars.Vertical;
			textBoxIncoming.Size = new Size(370, 272);
			textBoxIncoming.TabIndex = 6;
			textBoxIncoming.WordWrap = false;
			textBoxIncoming.TextChanged += textBoxIncoming_TextChanged;
			// 
			// textBoxOutgoing
			// 
			textBoxOutgoing.AcceptsReturn = true;
			textBoxOutgoing.AcceptsTab = true;
			textBoxOutgoing.ImeMode = ImeMode.NoControl;
			textBoxOutgoing.Location = new Point(12, 377);
			textBoxOutgoing.Multiline = true;
			textBoxOutgoing.Name = "textBoxOutgoing";
			textBoxOutgoing.ReadOnly = true;
			textBoxOutgoing.ScrollBars = ScrollBars.Vertical;
			textBoxOutgoing.Size = new Size(370, 272);
			textBoxOutgoing.TabIndex = 7;
			textBoxOutgoing.WordWrap = false;
			// 
			// Form_StreamAssistant
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = SystemColors.ControlDark;
			ClientSize = new Size(1086, 661);
			Controls.Add(textBoxOutgoing);
			Controls.Add(textBoxIncoming);
			Controls.Add(labelOutgoing);
			Controls.Add(labelIncoming);
			Controls.Add(buttonTest);
			Controls.Add(buttonEnable);
			Icon = (Icon)resources.GetObject("$this.Icon");
			Margin = new Padding(4, 3, 4, 3);
			Name = "Form_StreamAssistant";
			Text = "Stream Assistant";
			FormClosing += Form_StreamAssistant_FormClosing;
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private System.Windows.Forms.Button buttonEnable;
		private System.ComponentModel.BackgroundWorker backgroundWorkerTcp;
		private Button buttonTest;
		private System.Windows.Forms.Timer timerUpdate;
		private Label labelIncoming;
		private Label labelOutgoing;
		private TextBox textBoxIncoming;
		private TextBox textBoxOutgoing;
	}
}

