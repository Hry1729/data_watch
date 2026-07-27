using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;
using HslControls;

namespace HslCommunicationDemo.DemoControl
{
	public class UserControlCurve : UserControl
	{
		private Thread thread = null;

		private int timeSleep = 300;

		private bool isThreadRun = false;

		private IContainer components = null;

		private GroupBox groupBox5;

		private Button button27;

		private TextBox textBox14;

		private Label label18;

		private Label label17;

		private TextBox textBox12;

		private Label label15;

		private HslCurve userCurve1;

		[Browsable(false)]
		public IReadWriteNet ReadWriteNet { get; set; }

		[Category("Appearance")]
		[Description("设置或获取默认的地址信息")]
		[DefaultValue("")]
		public string AddressExample
		{
			get
			{
				return textBox12.Text;
			}
			set
			{
				textBox12.Text = value;
			}
		}

		public UserControlCurve()
		{
			InitializeComponent();
		}

		private void UserControlCurve_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				groupBox5.Text = "Timed reading, curve display";
				label15.Text = "Address:";
				label18.Text = "Interval";
				button27.Text = "Start";
				label17.Text = "This assumes that the type of data is determined for short:";
			}
			userCurve1.SetLeftCurve("A", new float[0], Color.Tomato);
		}

		private void button27_Click(object sender, EventArgs e)
		{
			if (!isThreadRun)
			{
				if (!int.TryParse(textBox14.Text, out timeSleep))
				{
					DemoUtils.ShowMessage("Time input wrong！");
					return;
				}
				button27.Text = "Stop";
				isThreadRun = true;
				thread = new Thread(ThreadReadServer);
				thread.IsBackground = true;
				thread.Start();
			}
			else
			{
				button27.Text = "Start";
				isThreadRun = false;
			}
		}

		private void ThreadReadServer()
		{
			if (ReadWriteNet == null)
			{
				return;
			}
			while (isThreadRun)
			{
				Thread.Sleep(timeSleep);
				try
				{
					OperateResult<short> operateResult = ReadWriteNet.ReadInt16(textBox12.Text);
					if (operateResult.IsSuccess && isThreadRun)
					{
						Invoke(new Action<short>(AddDataCurve), operateResult.Content);
					}
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Read failed：" + ex.Message);
				}
			}
		}

		private void AddDataCurve(short data)
		{
			userCurve1.AddCurveData("A", data);
		}

		public void ThreadQuit()
		{
			isThreadRun = false;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			isThreadRun = false;
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.button27 = new System.Windows.Forms.Button();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.label18 = new System.Windows.Forms.Label();
			this.label17 = new System.Windows.Forms.Label();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.userCurve1 = new HslControls.HslCurve();
			this.groupBox5.SuspendLayout();
			base.SuspendLayout();
			this.groupBox5.Controls.Add(this.button27);
			this.groupBox5.Controls.Add(this.textBox14);
			this.groupBox5.Controls.Add(this.label18);
			this.groupBox5.Controls.Add(this.label17);
			this.groupBox5.Controls.Add(this.textBox12);
			this.groupBox5.Controls.Add(this.label15);
			this.groupBox5.Controls.Add(this.userCurve1);
			this.groupBox5.Location = new System.Drawing.Point(0, 0);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(419, 278);
			this.groupBox5.TabIndex = 5;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "定时读取，曲线显示";
			this.button27.Location = new System.Drawing.Point(343, 25);
			this.button27.Name = "button27";
			this.button27.Size = new System.Drawing.Size(70, 28);
			this.button27.TabIndex = 9;
			this.button27.Text = "启动";
			this.button27.UseVisualStyleBackColor = true;
			this.button27.Click += new System.EventHandler(button27_Click);
			this.textBox14.Location = new System.Drawing.Point(267, 28);
			this.textBox14.Name = "textBox14";
			this.textBox14.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox14.Size = new System.Drawing.Size(70, 21);
			this.textBox14.TabIndex = 8;
			this.textBox14.Text = "300";
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(208, 31);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(41, 12);
			this.label18.TabIndex = 7;
			this.label18.Text = "间隔：";
			this.label17.AutoSize = true;
			this.label17.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label17.Location = new System.Drawing.Point(76, 60);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(215, 12);
			this.label17.TabIndex = 6;
			this.label17.Text = "此处假设确定了数据的类型，为short：";
			this.textBox12.Location = new System.Drawing.Point(78, 27);
			this.textBox12.Name = "textBox12";
			this.textBox12.Size = new System.Drawing.Size(118, 21);
			this.textBox12.TabIndex = 5;
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(11, 31);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(41, 12);
			this.label15.TabIndex = 4;
			this.label15.Text = "地址：";
			this.userCurve1.BackColor = System.Drawing.Color.FromArgb(32, 32, 32);
			this.userCurve1.Location = new System.Drawing.Point(13, 82);
			this.userCurve1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userCurve1.Name = "userCurve1";
			this.userCurve1.Size = new System.Drawing.Size(400, 189);
			this.userCurve1.TabIndex = 0;
			this.userCurve1.ReferenceAxisLeft.Max = 200f;
			this.userCurve1.ReferenceAxisRight.Max = 200f;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.groupBox5);
			base.Name = "UserControlCurve";
			base.Size = new System.Drawing.Size(420, 279);
			base.Load += new System.EventHandler(UserControlCurve_Load);
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
