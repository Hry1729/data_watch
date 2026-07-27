using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Net;
using HslCommunication.Enthernet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormComplexNet : HslFormContent
	{
		private NetComplexClient complexClient = null;

		private AutoResetEvent autoResetWait = new AutoResetEvent(false);

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

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private Panel panel1;

		private TextBox textBox9;

		private Label label13;

		private Button button6;

		private TextBox textBox3;

		private Label label6;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Button button5;

		private Label label11;

		private UserControlHead userControlHead1;

		public FormComplexNet()
		{
			InitializeComponent();
		}

		private void FormComplexNet_Load(object sender, EventArgs e)
		{
			button2.Enabled = false;
			textBox3.Text = Guid.Empty.ToString();
			button5.Enabled = false;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!IPAddress.TryParse(textBox1.Text, out var address))
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
				complexClient = new NetComplexClient();
				complexClient.ClientAlias = textBox9.Text;
				complexClient.EndPointServer = new IPEndPoint(address, result);
				complexClient.Token = new Guid(textBox3.Text);
				complexClient.AcceptString += ComplexClient_AcceptString;
				complexClient.AcceptByte += ComplexClient_AcceptByte;
				complexClient.MessageAlerts += ComplexClient_MessageAlerts;
				complexClient.ClientStart();
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
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

		private void ComplexClient_AcceptByte(AppSession session, NetHandle handle, byte[] data)
		{
			ShowTextInfo($"{DateTime.Now} [{session.IpEndPoint}] [{handle}] {SoftBasic.ByteToHexString(data)}");
		}

		private void ComplexClient_AcceptString(AppSession session, NetHandle handle, string data)
		{
			ShowTextInfo($"{DateTime.Now} [{session.IpEndPoint}] [{handle}] {data}");
		}

		private void button2_Click(object sender, EventArgs e)
		{
			complexClient.ClientClose();
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
		}

		private void ShowTextInfo(string text)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<string>(ShowTextInfo), text);
			}
			else
			{
				textBox8.AppendText(text + Environment.NewLine);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			NetHandle netHandle = default(NetHandle);
			if (textBox5.Text.IndexOf('.') >= 0)
			{
				string[] array = textBox5.Text.Split('.');
				netHandle = new NetHandle(byte.Parse(array[0]), byte.Parse(array[1]), ushort.Parse(array[2]));
			}
			else
			{
				netHandle = int.Parse(textBox5.Text);
			}
			if (!int.TryParse(textBox6.Text, out var result))
			{
				DemoUtils.ShowMessage("数据发送次数输入异常");
				return;
			}
			for (int i = 0; i < result; i++)
			{
				complexClient.Send(netHandle, textBox4.Text);
			}
		}

		private void button6_Click(object sender, EventArgs e)
		{
			Thread thread = new Thread(ServerPressureTest);
			thread.IsBackground = true;
			thread.Start();
			button6.Enabled = false;
			button5.Enabled = true;
		}

		private void ServerPressureTest()
		{
			NetComplexClient[] array = new NetComplexClient[100];
			for (int i = 0; i < 100; i++)
			{
				array[i] = new NetComplexClient();
				array[i].EndPointServer = new IPEndPoint(IPAddress.Parse(textBox1.Text), int.Parse(textBox2.Text));
				array[i].ClientAlias = "Client" + (i + 1);
				array[i].ClientStart();
			}
			Thread.Sleep(2000);
			int num = 0;
			for (int j = 0; j < 100; j++)
			{
				for (int k = 0; k < 100; k++)
				{
					array[k].Send(1, "测试消息" + num++);
				}
			}
			autoResetWait.WaitOne();
			for (int l = 0; l < 100; l++)
			{
				array[l].ClientClose();
			}
			Invoke((Action)delegate
			{
				button6.Enabled = true;
			});
		}

		private void button5_Click(object sender, EventArgs e)
		{
			autoResetWait.Set();
			button5.Enabled = false;
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlToken, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlAlias, textBox9.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlToken).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlAlias).Value;
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
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.button6 = new System.Windows.Forms.Button();
			this.label6 = new System.Windows.Forms.Label();
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
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Enabled = false;
			this.panel2.Location = new System.Drawing.Point(3, 111);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 531);
			this.panel2.TabIndex = 20;
			this.label11.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(59, 507);
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
			this.textBox8.Size = new System.Drawing.Size(926, 290);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(8, 217);
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
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(926, 138);
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
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label13);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.button6);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 73);
			this.panel1.TabIndex = 14;
			this.button5.Enabled = false;
			this.button5.Location = new System.Drawing.Point(833, 35);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(140, 28);
			this.button5.TabIndex = 12;
			this.button5.Text = "关闭";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox9.Location = new System.Drawing.Point(444, 10);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(107, 23);
			this.textBox9.TabIndex = 11;
			this.textBox9.Text = "test name";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(390, 13);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 10;
			this.label13.Text = "别名：";
			this.textBox3.Location = new System.Drawing.Point(62, 43);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(489, 23);
			this.textBox3.TabIndex = 7;
			this.button6.Location = new System.Drawing.Point(833, 7);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(140, 28);
			this.button6.TabIndex = 9;
			this.button6.Text = "多客户端压力测试";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 46);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 6;
			this.label6.Text = "令牌：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(683, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(586, 5);
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
			this.textBox2.Text = "12346";
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
			this.userControlHead1.ProtocolInfo = "Hsl - Complex";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 21;
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
			base.Name = "FormComplexNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormComplexNet";
			base.Load += new System.EventHandler(FormComplexNet_Load);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
