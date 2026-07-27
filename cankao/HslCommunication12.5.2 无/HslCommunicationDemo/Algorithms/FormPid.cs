using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Algorithms.PID;
using HslCommunicationDemo.DemoControl;
using HslControls;

namespace HslCommunicationDemo.Algorithms
{
	public class FormPid : HslFormContent
	{
		private PIDHelper pIDHelper;

		private Timer timer;

		private IContainer components = null;

		private HslCurve userCurve1;

		private Label label1;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox3;

		private Label label3;

		private TextBox textBox4;

		private Label label4;

		private UserControlHead userControlHead1;

		private TextBox textBox5;

		private Label label5;

		private Button button1;

		private Button button2;

		public FormPid()
		{
			InitializeComponent();
		}

		private void FormPid_Load(object sender, EventArgs e)
		{
			userCurve1.SetLeftCurve("Pid", null, Color.Green);
			pIDHelper = new PIDHelper();
			timer = new Timer();
			timer.Interval = 100;
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			userCurve1.AddCurveData("Pid", (float)pIDHelper.PidCalculate());
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			try
			{
				pIDHelper.SetValue = double.Parse(textBox5.Text);
			}
			catch
			{
				DemoUtils.ShowMessage("Input Wrong, please check the input value!");
			}
		}

		private void Button2_Click(object sender, EventArgs e)
		{
			try
			{
				pIDHelper.Kp = double.Parse(textBox1.Text);
				pIDHelper.Ki = double.Parse(textBox2.Text);
				pIDHelper.Kd = double.Parse(textBox3.Text);
				pIDHelper.DeadBand = double.Parse(textBox4.Text);
			}
			catch
			{
				DemoUtils.ShowMessage("Input Wrong, please check the input value!");
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.userCurve1 = new HslControls.HslCurve();
			this.button2 = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 50);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(18, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "P:";
			this.textBox1.Location = new System.Drawing.Point(39, 47);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(100, 23);
			this.textBox1.TabIndex = 2;
			this.textBox1.Text = "0.7";
			this.textBox2.Location = new System.Drawing.Point(196, 47);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(100, 23);
			this.textBox2.TabIndex = 4;
			this.textBox2.Text = "0.4";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(169, 50);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(15, 17);
			this.label2.TabIndex = 3;
			this.label2.Text = "I:";
			this.textBox3.Location = new System.Drawing.Point(366, 47);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(100, 23);
			this.textBox3.TabIndex = 6;
			this.textBox3.Text = "0";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(339, 50);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(20, 17);
			this.label3.TabIndex = 5;
			this.label3.Text = "D:";
			this.textBox4.Location = new System.Drawing.Point(563, 47);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(100, 23);
			this.textBox4.TabIndex = 8;
			this.textBox4.Text = "2";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(491, 50);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(74, 17);
			this.label4.TabIndex = 7;
			this.label4.Text = "DeadBlock:";
			this.textBox5.Location = new System.Drawing.Point(69, 92);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(100, 23);
			this.textBox5.TabIndex = 11;
			this.textBox5.Text = "1";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(12, 95);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(43, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "Value:";
			this.button1.Location = new System.Drawing.Point(205, 95);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(102, 24);
			this.button1.TabIndex = 12;
			this.button1.Text = "set value";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			this.userCurve1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userCurve1.BackColor = System.Drawing.Color.Transparent;
			this.userCurve1.IsAbscissaStrech = true;
			this.userCurve1.Location = new System.Drawing.Point(12, 125);
			this.userCurve1.Name = "userCurve1";
			this.userCurve1.Size = new System.Drawing.Size(992, 508);
			this.userCurve1.StrechDataCountMax = 100;
			this.userCurve1.TabIndex = 0;
			this.userCurve1.ReferenceAxisLeft.Max = 400f;
			this.userCurve1.ReferenceAxisRight.Max = 400f;
			this.button2.Location = new System.Drawing.Point(694, 44);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(102, 24);
			this.button2.TabIndex = 13;
			this.button2.Text = "set para";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(Button2_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 9;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.userCurve1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormPid";
			this.Text = "FormPid";
			base.Load += new System.EventHandler(FormPid_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
