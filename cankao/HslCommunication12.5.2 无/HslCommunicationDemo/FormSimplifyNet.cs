using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Enthernet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormSimplifyNet : HslFormContent
	{
		private NetSimplifyClient simplifyClient = new NetSimplifyClient();

		private int status = 1;

		private int thread_status = 0;

		private int failed = 0;

		private DateTime thread_time_start = DateTime.Now;

		private IContainer components = null;

		private Panel panel1;

		private TextBox textBox3;

		private Label label6;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private TextBox textBox7;

		private Label label11;

		private TextBox textBox6;

		private Label label10;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private Button button5;

		private Button button6;

		private UserControlHead userControlHead1;

		private Button button7;

		private TextBox textBox10;

		private Label label4;

		private TextBox textBox9;

		private Label label2;

		private TextBox textBox11;

		private Label label5;

		public FormSimplifyNet()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			textBox3.Text = Guid.Empty.ToString();
			button2.Enabled = false;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Simplify网络客户端";
				label1.Text = "Ip地址：";
				label3.Text = "端口号：";
				button1.Text = "连接";
				button2.Text = "断开连接";
				label6.Text = "令牌：";
				button5.Text = "启动短连接";
				button6.Text = "压力测试";
				label7.Text = "指令头：";
				label8.Text = "举例：12345 或是1.1.1";
				label9.Text = "数据：";
				button3.Text = "发送";
				label10.Text = "次数：";
				label11.Text = "耗时：";
				button4.Text = "清空";
				label12.Text = "接收：";
			}
			else
			{
				Text = "Simplify Net Client Test";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "Token:";
				button5.Text = "Start a short connection";
				button6.Text = "Pressure test";
				label7.Text = "Command:";
				label8.Text = "Example: 12345 or 1.1.1";
				label9.Text = "Data:";
				button3.Text = "Send";
				label10.Text = "Times:";
				label11.Text = "Take:";
				button4.Text = "Clear";
				label12.Text = "Receive:";
			}
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			simplifyClient.IpAddress = textBox1.Text;
			simplifyClient.Port = int.Parse(textBox2.Text);
			simplifyClient.ReceiveTimeOut = int.Parse(textBox11.Text);
			simplifyClient.Token = new Guid(textBox3.Text);
			simplifyClient.SetLoginAccount(textBox9.Text, textBox10.Text);
			OperateResult connect = await simplifyClient.ConnectServerAsync();
			if (connect.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				button5.Enabled = false;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.ToMessageShowString());
			}
		}

		private async void button2_Click(object sender, EventArgs e)
		{
			button5.Enabled = true;
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			await simplifyClient.ConnectCloseAsync();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (status == 1)
			{
				simplifyClient.IpAddress = textBox1.Text;
				simplifyClient.Port = int.Parse(textBox2.Text);
				simplifyClient.Token = new Guid(textBox3.Text);
				button1.Enabled = false;
				button2.Enabled = false;
				panel2.Enabled = true;
				status = 2;
				button5.Text = ((Program.Language == 1) ? "重新选择连接" : "Choose again");
			}
			else
			{
				status = 1;
				button1.Enabled = true;
				panel2.Enabled = false;
				button5.Text = ((Program.Language == 1) ? "启动短连接" : "Start a short connection");
			}
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			button3.Enabled = false;
			NetHandle handle;
			if (textBox5.Text.IndexOf('.') >= 0)
			{
				string[] values = textBox5.Text.Split('.');
				handle = new NetHandle(byte.Parse(values[0]), byte.Parse(values[1]), ushort.Parse(values[2]));
			}
			else
			{
				handle = int.Parse(textBox5.Text);
			}
			int count = int.Parse(textBox6.Text);
			DateTime start = DateTime.Now;
			for (int i = 0; i < count; i++)
			{
				OperateResult<string> read = await simplifyClient.ReadFromServerAsync(handle, textBox4.Text);
				if (read.IsSuccess)
				{
					textBox8.AppendText(read.Content + Environment.NewLine);
				}
				else
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "读取失败：" : ("Read Failed:" + read.ToMessageShowString()));
				}
			}
			textBox7.Text = (DateTime.Now - start).TotalMilliseconds.ToString("F2");
			button3.Enabled = true;
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private async void Button7_Click(object sender, EventArgs e)
		{
			NetHandle handle;
			if (textBox5.Text.IndexOf('.') >= 0)
			{
				string[] values = textBox5.Text.Split('.');
				handle = new NetHandle(byte.Parse(values[0]), byte.Parse(values[1]), ushort.Parse(values[2]));
			}
			else
			{
				handle = int.Parse(textBox5.Text);
			}
			int count = int.Parse(textBox6.Text);
			DateTime start = DateTime.Now;
			for (int i = 0; i < count; i++)
			{
				OperateResult<NetHandle, string[]> read = await simplifyClient.ReadCustomerFromServerAsync(handle, textBox4.Text.Split(';'));
				if (read.IsSuccess)
				{
					textBox8.Lines = read.Content2;
				}
				else
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "读取失败：" : ("Read Failed:" + read.ToMessageShowString()));
				}
			}
			textBox7.Text = (DateTime.Now - start).TotalMilliseconds.ToString("F2");
		}

		private void button6_Click(object sender, EventArgs e)
		{
			PressureTest2();
		}

		private void PressureTest2()
		{
			thread_status = 3;
			failed = 0;
			thread_time_start = DateTime.Now;
			Thread thread = new Thread(thread_test2);
			thread.IsBackground = true;
			thread.Start();
			Thread thread2 = new Thread(thread_test2);
			thread2.IsBackground = true;
			thread2.Start();
			Thread thread3 = new Thread(thread_test2);
			thread3.IsBackground = true;
			thread3.Start();
			button3.Enabled = false;
		}

		private async void thread_test2()
		{
			for (int count = 1000; count > 0; count--)
			{
				if (!(await simplifyClient.ReadCustomerFromServerAsync(1, "")).IsSuccess)
				{
					failed++;
				}
			}
			thread_end();
		}

		private void thread_end()
		{
			if (Interlocked.Decrement(ref thread_status) == 0)
			{
				Invoke((Action)delegate
				{
					button3.Enabled = true;
					DemoUtils.ShowMessage("Spend：" + (DateTime.Now - thread_time_start).TotalSeconds + Environment.NewLine + " Read Failed：" + failed);
				});
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTimeout, textBox11.Text);
			element.SetAttributeValue(DemoDeviceList.XmlToken, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox11.Text = element.Attribute(DemoDeviceList.XmlTimeout).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlToken).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox10.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox11);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox10);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button6);
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(2, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 72);
			this.panel1.TabIndex = 7;
			this.textBox11.Location = new System.Drawing.Point(446, 8);
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(79, 23);
			this.textBox11.TabIndex = 15;
			this.textBox11.Text = "5000";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(380, 11);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "接收超时：";
			this.textBox10.Location = new System.Drawing.Point(572, 41);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(91, 23);
			this.textBox10.TabIndex = 13;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(508, 44);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(401, 41);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(91, 23);
			this.textBox9.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(337, 44);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "用户名：";
			this.button6.Location = new System.Drawing.Point(798, 39);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(168, 28);
			this.button6.TabIndex = 9;
			this.button6.Text = "压力测试";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(798, 5);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(168, 28);
			this.button5.TabIndex = 8;
			this.button5.Text = "启动短连接";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox3.Location = new System.Drawing.Point(62, 41);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(265, 23);
			this.textBox3.TabIndex = 7;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 44);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 6;
			this.label6.Text = "令牌：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(701, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(604, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(279, 8);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(79, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "12345";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(225, 11);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 8);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(141, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 11);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(2, 110);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 532);
			this.panel2.TabIndex = 13;
			this.button7.Location = new System.Drawing.Point(159, 180);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(148, 28);
			this.button7.TabIndex = 20;
			this.button7.Text = "Send Array";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(Button7_Click);
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(927, 313);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(8, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "接收：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(898, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox7.Location = new System.Drawing.Point(618, 183);
			this.textBox7.Name = "textBox7";
			this.textBox7.ReadOnly = true;
			this.textBox7.Size = new System.Drawing.Size(141, 23);
			this.textBox7.TabIndex = 16;
			this.textBox7.Text = "0";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(564, 186);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 15;
			this.label11.Text = "耗时：";
			this.textBox6.Location = new System.Drawing.Point(403, 183);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(141, 23);
			this.textBox6.TabIndex = 14;
			this.textBox6.Text = "1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(349, 186);
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
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(927, 138);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "数据：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(249, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(134, 17);
			this.label8.TabIndex = 10;
			this.label8.Text = "举例：12345 或是1.1.1";
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(181, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "1";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(56, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "指令头：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7697782.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Hsl - Simplify";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSimplifyNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Simplify网络客户端";
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
