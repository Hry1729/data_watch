using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Enthernet;
using HslCommunication.LogNet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormPlainSocket : HslFormContent
	{
		private NetPlainSocket plainSocket = null;

		private IContainer components = null;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private TextBox textBox6;

		private Label label10;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private UserControlHead userControlHead1;

		private Label label11;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private Label label2;

		public FormPlainSocket()
		{
			InitializeComponent();
		}

		private void FormComplexNet_Load(object sender, EventArgs e)
		{
			button2.Enabled = false;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!IPAddress.TryParse(textBox1.Text, out var _))
			{
				DemoUtils.ShowMessage("IP地址填写不正确");
				return;
			}
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("port填写不正确");
				return;
			}
			try
			{
				plainSocket = new NetPlainSocket(textBox1.Text, result);
				plainSocket.LogNet = new LogNetSingle(string.Empty);
				plainSocket.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				plainSocket.ReceivedString += PlainSocket_ReceivedString;
				if (radioButton1.Checked)
				{
					plainSocket.Encoding = Encoding.ASCII;
				}
				if (radioButton2.Checked)
				{
					plainSocket.Encoding = Encoding.Default;
				}
				if (radioButton3.Checked)
				{
					plainSocket.Encoding = Encoding.Unicode;
				}
				if (radioButton4.Checked)
				{
					plainSocket.Encoding = Encoding.UTF8;
				}
				OperateResult operateResult = plainSocket.ConnectServer();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("Connect Success!");
					button1.Enabled = false;
					button2.Enabled = true;
					panel2.Enabled = true;
				}
				else
				{
					DemoUtils.ShowMessage("Connect Failed:" + operateResult.Message);
				}
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox8.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			});
		}

		private void PlainSocket_ReceivedString(string obj)
		{
			ShowTextInfo(" " + obj);
		}

		private void ComplexClient_MessageAlerts(string text)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<string>(ComplexClient_MessageAlerts), text);
			}
			else
			{
				label11.Text = text;
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			plainSocket.ConnectClose();
			button1.Enabled = true;
			button2.Enabled = false;
		}

		private void ShowTextInfo(string text)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<string>(ShowTextInfo), text);
			}
			else
			{
				textBox8.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " : " + text + Environment.NewLine);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox6.Text, out var result))
			{
				DemoUtils.ShowMessage("数据发送次数输入异常");
				return;
			}
			int num = 0;
			for (int i = 0; i < result; i++)
			{
				if (!plainSocket.SendString(textBox4.Text).IsSuccess)
				{
					num++;
				}
			}
			if (num > 0)
			{
				DemoUtils.ShowMessage("Send Faield count: " + num);
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
			this.panel2 = new System.Windows.Forms.Panel();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2.SuspendLayout();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Enabled = false;
			this.panel2.Location = new System.Drawing.Point(3, 115);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 527);
			this.panel2.TabIndex = 20;
			this.label11.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(59, 504);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(32, 17);
			this.label11.TabIndex = 20;
			this.label11.Text = "消息";
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Font = new System.Drawing.Font("宋体", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(926, 286);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(8, 216);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "接收：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(897, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.textBox6.Location = new System.Drawing.Point(241, 183);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(141, 23);
			this.textBox6.TabIndex = 14;
			this.textBox6.Text = "1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(187, 186);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 13;
			this.label10.Text = "次数：";
			this.button3.Location = new System.Drawing.Point(62, 180);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "发送";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 3);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(926, 171);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 8);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "数据：";
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.radioButton4);
			this.panel1.Controls.Add(this.radioButton3);
			this.panel1.Controls.Add(this.radioButton2);
			this.panel1.Controls.Add(this.radioButton1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 73);
			this.panel1.TabIndex = 14;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Checked = true;
			this.radioButton4.Location = new System.Drawing.Point(299, 39);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(55, 21);
			this.radioButton4.TabIndex = 10;
			this.radioButton4.TabStop = true;
			this.radioButton4.Text = "UTF8";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(204, 39);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(74, 21);
			this.radioButton3.TabIndex = 9;
			this.radioButton3.Text = "Unicode";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(134, 39);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(55, 21);
			this.radioButton2.TabIndex = 8;
			this.radioButton2.Text = "ANSI";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(58, 39);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(57, 21);
			this.radioButton1.TabIndex = 7;
			this.radioButton1.Text = "ASCII";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(8, 41);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 6;
			this.label2.Text = "编码：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(490, 7);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(393, 7);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(277, 10);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(85, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "12345";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(219, 13);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 10);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(141, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 13);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7697782.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 21;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormPlainSocket";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormPlainSocket";
			base.Load += new System.EventHandler(FormComplexNet_Load);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
