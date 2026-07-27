using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Profinet.Siemens;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class SiemensWebApiControl : UserControl
	{
		private SiemensWebApi siemensTcpNet;

		private IContainer components = null;

		private GroupBox groupBox5;

		private Button button5;

		private Button button4;

		private Button button8;

		private Button button7;

		private Label label19;

		private TextBox textBox7;

		private Label label10;

		private TextBox textBox8;

		private Label label9;

		private GroupBox groupBox3;

		private Button button3;

		private TextBox textBox10;

		private Label label13;

		private Button button25;

		private TextBox textBox6;

		private Label label11;

		private Label label_code;

		private TextBox textBox_code;

		public SiemensWebApiControl()
		{
			InitializeComponent();
		}

		public void SetReadWriteNet(SiemensWebApi siemensTcpNet)
		{
			this.siemensTcpNet = siemensTcpNet;
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			OperateResult<JToken[]> read = await siemensTcpNet.ReadAsync(textBox6.Text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries));
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed：" + read.ToMessageShowString());
			}
			else
			{
				JArray json = new JArray();
				for (int i = 0; i < read.Content.Length; i++)
				{
					json.Add(read.Content[i]);
				}
				textBox10.Text = json.ToString();
			}
			textBox_code.Text = $"OperateResult<JToken[]> read = {DemoUtils.PlcDeviceName}.Read( \"{textBox6.Text}\".Split( new char[] {';'}, StringSplitOptions.RemoveEmptyEntries ) );";
		}

		private void button25_Click(object sender, EventArgs e)
		{
			try
			{
				OperateResult<byte[]> operateResult = siemensTcpNet.Read(textBox6.Text, 0);
				if (operateResult.IsSuccess)
				{
					textBox10.Text = "Result：" + SoftBasic.ByteToHexString(operateResult.Content);
				}
				else
				{
					DemoUtils.ShowMessage("Read Failed：" + operateResult.ToMessageShowString());
				}
				textBox_code.Text = "OperateResult<byte[]> read = " + DemoUtils.PlcDeviceName + ".Read( \"" + textBox6.Text + "\", 0 );";
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Read Failed：" + ex.Message);
			}
		}

		private async void button4_Click(object sender, EventArgs e)
		{
			OperateResult<double> read = await siemensTcpNet.ReadVersionAsync();
			if (read.IsSuccess)
			{
				DemoUtils.ShowMessage(read.Content.ToString());
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + read.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<double> read = " + DemoUtils.PlcDeviceName + ".ReadVersion( );";
		}

		private async void button5_Click(object sender, EventArgs e)
		{
			OperateResult read = await siemensTcpNet.ReadPingAsync();
			if (read.IsSuccess)
			{
				DemoUtils.ShowMessage("Ping Success");
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + read.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult read = " + DemoUtils.PlcDeviceName + ".ReadPing( );";
		}

		private async void button7_Click_1(object sender, EventArgs e)
		{
			OperateResult<DateTime> read = await siemensTcpNet.ReadDateTimeAsync(textBox8.Text);
			if (read.IsSuccess)
			{
				textBox7.Text = read.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + read.Message);
			}
			textBox_code.Text = "OperateResult<DateTime> read = " + DemoUtils.PlcDeviceName + ".ReadDateTime( \"" + textBox8.Text + "\" );";
		}

		private async void button8_Click(object sender, EventArgs e)
		{
			if (DateTime.TryParse(textBox7.Text, out var value))
			{
				DemoUtils.WriteResultRender(await siemensTcpNet.WriteAsync(textBox8.Text, value), textBox8.Text);
			}
			else
			{
				DemoUtils.ShowMessage("DateTime Data is not corrent: " + textBox7.Text);
			}
			textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".Write( \"" + textBox8.Text + "\", DateTime.Parse( \"" + textBox7.Text + "\" ) )";
		}

		private void SiemensWebApiControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				button7.Text = "r-time";
				label11.Text = "Address:";
				button25.Text = "Bulk Read";
				label13.Text = "Results:";
				label10.Text = "Address:";
				label9.Text = "Value:";
				label19.Text = "Note: The value of the string needs to be converted";
				button8.Text = "w-time";
				groupBox3.Text = "Bulk Read test";
				groupBox5.Text = "Special function test";
				button3.Text = "Order";
				label_code.Text = "Code:";
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
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.label19 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.button25 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.groupBox5.SuspendLayout();
			this.groupBox3.SuspendLayout();
			base.SuspendLayout();
			this.groupBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox5.Controls.Add(this.button5);
			this.groupBox5.Controls.Add(this.button4);
			this.groupBox5.Controls.Add(this.button8);
			this.groupBox5.Controls.Add(this.button7);
			this.groupBox5.Controls.Add(this.label19);
			this.groupBox5.Controls.Add(this.textBox7);
			this.groupBox5.Controls.Add(this.label10);
			this.groupBox5.Controls.Add(this.textBox8);
			this.groupBox5.Controls.Add(this.label9);
			this.groupBox5.Location = new System.Drawing.Point(423, 3);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(419, 219);
			this.groupBox5.TabIndex = 6;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "特殊功能测试";
			this.button5.Location = new System.Drawing.Point(122, 181);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(107, 30);
			this.button5.TabIndex = 25;
			this.button5.Text = "Ping";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(9, 181);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(107, 30);
			this.button4.TabIndex = 24;
			this.button4.Text = "Version";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button8.Location = new System.Drawing.Point(320, 27);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(82, 28);
			this.button8.TabIndex = 21;
			this.button8.Text = "time写入";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Location = new System.Drawing.Point(232, 27);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(82, 28);
			this.button7.TabIndex = 17;
			this.button7.Text = "time读取";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click_1);
			this.label19.ForeColor = System.Drawing.Color.Red;
			this.label19.Location = new System.Drawing.Point(58, 89);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(147, 58);
			this.label19.TabIndex = 17;
			this.label19.Text = "注意：值的字符串需要能转化成对应的数据类型";
			this.textBox7.Location = new System.Drawing.Point(74, 63);
			this.textBox7.Name = "textBox7";
			this.textBox7.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox7.Size = new System.Drawing.Size(152, 23);
			this.textBox7.TabIndex = 5;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(6, 33);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "地址：";
			this.textBox8.Location = new System.Drawing.Point(74, 30);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(152, 23);
			this.textBox8.TabIndex = 3;
			this.textBox8.Text = "\"全局DB\".Static_14";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(6, 65);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 4;
			this.label9.Text = "值：";
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Controls.Add(this.button3);
			this.groupBox3.Controls.Add(this.textBox10);
			this.groupBox3.Controls.Add(this.label13);
			this.groupBox3.Controls.Add(this.button25);
			this.groupBox3.Controls.Add(this.textBox6);
			this.groupBox3.Controls.Add(this.label11);
			this.groupBox3.Location = new System.Drawing.Point(2, 3);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(415, 219);
			this.groupBox3.TabIndex = 5;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "批量读取测试";
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(239, 24);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(82, 28);
			this.button3.TabIndex = 15;
			this.button3.Text = "批量读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox10.Location = new System.Drawing.Point(63, 60);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox10.Size = new System.Drawing.Size(346, 153);
			this.textBox10.TabIndex = 10;
			this.textBox10.Text = "批量读取时，多个标签按照 ';' 英文的分号进行分割";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(9, 62);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 9;
			this.label13.Text = "结果：";
			this.button25.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button25.Location = new System.Drawing.Point(327, 24);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(82, 28);
			this.button25.TabIndex = 8;
			this.button25.Text = "原始读取";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox6.Location = new System.Drawing.Point(63, 27);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(170, 23);
			this.textBox6.TabIndex = 5;
			this.textBox6.Text = "\"全局DB\".Static_14";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(9, 30);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 4;
			this.label11.Text = "地址：";
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(11, 226);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 10;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(65, 223);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(777, 51);
			this.textBox_code.TabIndex = 11;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.groupBox5);
			base.Controls.Add(this.groupBox3);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "SiemensWebApiControl";
			base.Size = new System.Drawing.Size(848, 276);
			base.Load += new System.EventHandler(SiemensWebApiControl_Load);
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
