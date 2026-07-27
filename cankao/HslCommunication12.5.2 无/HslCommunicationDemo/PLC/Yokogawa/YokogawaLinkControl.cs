using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Yokogawa;

namespace HslCommunicationDemo.PLC.Yokogawa
{
	public class YokogawaLinkControl : UserControl
	{
		private Button button11;

		private Button button10;

		private Button button9;

		private Button button8;

		private Button button7;

		private Button button6;

		private Button button5;

		private Button button4;

		private TextBox textBox4;

		private Label label5;

		private Button button3;

		private TextBox textBox5;

		private Label label_code;

		private TextBox textBox_code;

		private Label label6;

		private YokogawaLinkTcp yokogawa;

		public YokogawaLinkControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.button11.Location = new System.Drawing.Point(586, 69);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(158, 28);
			this.button11.TabIndex = 40;
			this.button11.Text = "DateTime";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(411, 69);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(169, 28);
			this.button10.TabIndex = 39;
			this.button10.Text = "SystemInfo";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Location = new System.Drawing.Point(411, 137);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(122, 28);
			this.button9.TabIndex = 38;
			this.button9.Text = "ProgramStatus";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Location = new System.Drawing.Point(475, 103);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(58, 28);
			this.button8.TabIndex = 37;
			this.button8.Text = "Stop";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Location = new System.Drawing.Point(411, 103);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(58, 28);
			this.button7.TabIndex = 36;
			this.button7.Text = "Start";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(586, 35);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(158, 28);
			this.button6.TabIndex = 35;
			this.button6.Text = "随机写入读取";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(411, 35);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(169, 28);
			this.button5.TabIndex = 34;
			this.button5.Text = "随机word读取";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(586, 1);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(158, 28);
			this.button4.TabIndex = 33;
			this.button4.Text = "随机Bool写入";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox4.Location = new System.Drawing.Point(63, 75);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(342, 137);
			this.textBox4.TabIndex = 32;
			this.textBox4.Text = "true,true,true,true";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(9, 78);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(32, 17);
			this.label5.TabIndex = 31;
			this.label5.Text = "值：";
			this.button3.Location = new System.Drawing.Point(411, 1);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(169, 28);
			this.button3.TabIndex = 30;
			this.button3.Text = "随机Bool读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox5.Location = new System.Drawing.Point(63, 7);
			this.textBox5.Multiline = true;
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(342, 62);
			this.textBox5.TabIndex = 29;
			this.textBox5.Text = "Y100;Y200;M100;M300";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 10);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 28;
			this.label6.Text = "地址：";
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(9, 221);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 41;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(63, 218);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(885, 58);
			this.textBox_code.TabIndex = 42;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.button11);
			base.Controls.Add(this.button10);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.button9);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.button8);
			base.Controls.Add(this.button3);
			base.Controls.Add(this.button7);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.button6);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.button5);
			base.Controls.Add(this.button4);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "YokogawaLinkControl";
			base.Size = new System.Drawing.Size(951, 279);
			base.Load += new System.EventHandler(YokogawaLinkControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		public void SetDevice(YokogawaLinkTcp yokogawa, string address)
		{
			this.yokogawa = yokogawa;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> operateResult = yokogawa.ReadRandomBool(textBox5.Text.Split(';'));
			if (operateResult.IsSuccess)
			{
				textBox4.Text = operateResult.Content.ToArrayString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = $"OperateResult<bool[]> read = {DemoUtils.PlcDeviceName}.ReadRandomBool( \"{textBox5.Text}\".Split( new char[] {';'} ) );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yokogawa.WriteRandomBool(textBox5.Text.Split(';'), textBox4.Text.ToStringArray<bool>());
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = $"OperateResult write = {DemoUtils.PlcDeviceName}.WriteRandomBool( \"{textBox5.Text}\".Split( new char[] {';'} ), \"{textBox4.Text}\".ToStringArray<bool>( ) );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<short[]> operateResult = yokogawa.ReadRandomInt16(textBox5.Text.Split(';'));
			if (operateResult.IsSuccess)
			{
				textBox4.Text = operateResult.Content.ToArrayString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = $"OperateResult<short[]> read = {DemoUtils.PlcDeviceName}.ReadRandomInt16( \"{textBox5.Text}\".Split( new char[] {';'} ) );";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yokogawa.WriteRandom(textBox5.Text.Split(';'), textBox4.Text.ToStringArray<short>());
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = $"OperateResult write = {DemoUtils.PlcDeviceName}.WriteRandom( \"{textBox5.Text}\".Split( new char[] {';'} ), \"{textBox4.Text}\".ToStringArray<short>( ) );";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yokogawa.Start();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Started Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Started failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult start = " + DemoUtils.PlcDeviceName + ".Start( );";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yokogawa.Stop();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Stop Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Stop failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult stop = " + DemoUtils.PlcDeviceName + ".Stop( );";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = yokogawa.ReadProgramStatus();
			if (operateResult.IsSuccess)
			{
				switch (operateResult.Content)
				{
				case 1:
					textBox4.Text = "1 : RUN";
					break;
				case 2:
					textBox4.Text = "2 : Stop";
					break;
				case 3:
					textBox4.Text = "3 : Debug";
					break;
				case 4:
					textBox4.Text = "4 : Rom writer";
					break;
				default:
					textBox4.Text = operateResult.Content.ToString();
					break;
				}
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<int> read  = " + DemoUtils.PlcDeviceName + ".ReadProgramStatus( );";
		}

		private void button10_Click(object sender, EventArgs e)
		{
			OperateResult<YokogawaSystemInfo> operateResult = yokogawa.ReadSystemInfo();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<YokogawaSystemInfo> read  = " + DemoUtils.PlcDeviceName + ".ReadSystemInfo( );";
		}

		private void button11_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = yokogawa.ReadDateTime();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<DateTime> read  = " + DemoUtils.PlcDeviceName + ".ReadDateTime( );";
		}

		private void YokogawaLinkControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label6.Text = "Address:";
				label5.Text = "Value:";
				label_code.Text = "Code:";
				button3.Text = "Random-bool-read";
				button4.Text = "Random-bool-write";
				button5.Text = "Random-word-read";
				button6.Text = "Random-wrod-write";
			}
		}
	}
}
