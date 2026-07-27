using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core.Pipe;
using HslCommunication.LogNet;
using HslCommunication.WebSocket;
using HslCommunicationDemo.DemoControl;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormWebsocketQANet : HslFormContent
	{
		private WebSocketQANet wsClient;

		private IContainer components = null;

		private Panel panel1;

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

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private UserControlHead userControlHead1;

		private TextBox textBox10;

		private Label label4;

		private TextBox textBox9;

		private Label label2;

		private TextBox textBox11;

		private Label label5;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private RadioButton radioButton5;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		private Panel panel3;

		private Label label7;

		private Button button5;

		private CheckBox checkBox_hex_log;

		private CheckBox checkBox_ssl_tls;

		public FormWebsocketQANet()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Websocket同步访问客户端";
				label1.Text = "Ip地址：";
				label3.Text = "端口号：";
				button1.Text = "连接";
				button2.Text = "断开连接";
				label9.Text = "Payload：";
				button3.Text = "发送";
				button4.Text = "清空";
				label12.Text = "接收：";
				button5.Text = "压力测试";
			}
			else
			{
				Text = "Websocket QA Test";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label9.Text = "Payload:";
				button3.Text = "Send";
				button4.Text = "Clear";
				label12.Text = "Receive:";
				button5.Text = "Press Test";
			}
		}

		private void FormWebsocketQANet_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			panel2.Enabled = true;
			if (!string.IsNullOrEmpty(textBox9.Text) || !string.IsNullOrEmpty(textBox10.Text))
			{
			}
			wsClient = new WebSocketQANet(textBox1.Text, int.Parse(textBox2.Text));
			wsClient.LogNet = new LogNetSingle(string.Empty);
			wsClient.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			if (checkBox_ssl_tls.Checked)
			{
				wsClient.CommunicationPipe = new PipeSslNet(textBox1.Text, int.Parse(textBox2.Text), false);
			}
			OperateResult operateResult = null;
			operateResult = wsClient.ConnectServer();
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			try
			{
				Invoke((Action)delegate
				{
					if ((checkBox_hex_log.Checked || e.HslMessage.Degree != HslMessageDegree.DEBUG) && radioButton2.Checked)
					{
						textBox8.AppendText(e.HslMessage.ToString() + Environment.NewLine);
					}
				});
			}
			catch
			{
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			wsClient.ConnectClose();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = wsClient.ReadFromServer(textBox4.Text);
			if (operateResult.IsSuccess)
			{
				string json = operateResult.Content;
				if (radioButton4.Checked)
				{
					try
					{
						json = XElement.Parse(json).ToString();
					}
					catch
					{
					}
				}
				else if (radioButton5.Checked)
				{
					try
					{
						json = JObject.Parse(json).ToString();
					}
					catch
					{
					}
				}
				if (radioButton2.Checked)
				{
					wsClient.LogNet?.WriteInfo(json);
				}
				else if (radioButton1.Checked)
				{
					textBox8.Text = json;
				}
			}
			else
			{
				DemoUtils.ShowMessage("read Failed:" + operateResult.Message);
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			button5.Enabled = false;
			DemoUtils.ShowMessage("暂时不支持");
		}

		private void ThreadPoolTest()
		{
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTimeout, textBox11.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox11.Text = element.Attribute(DemoDeviceList.XmlTimeout).Value;
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
			this.checkBox_ssl_tls = new System.Windows.Forms.CheckBox();
			this.button5 = new System.Windows.Forms.Button();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.checkBox_hex_log = new System.Windows.Forms.CheckBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton5 = new System.Windows.Forms.RadioButton();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_ssl_tls);
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.label7);
			this.panel1.Controls.Add(this.textBox11);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox10);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(934, 72);
			this.panel1.TabIndex = 7;
			this.checkBox_ssl_tls.AutoSize = true;
			this.checkBox_ssl_tls.Location = new System.Drawing.Point(724, 41);
			this.checkBox_ssl_tls.Name = "checkBox_ssl_tls";
			this.checkBox_ssl_tls.Size = new System.Drawing.Size(78, 21);
			this.checkBox_ssl_tls.TabIndex = 28;
			this.checkBox_ssl_tls.Text = "SSL/TLS?";
			this.checkBox_ssl_tls.UseVisualStyleBackColor = true;
			this.button5.Location = new System.Drawing.Point(838, 3);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(91, 28);
			this.button5.TabIndex = 19;
			this.button5.Text = "压力测试";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.label7.AutoSize = true;
			this.label7.ForeColor = System.Drawing.Color.Gray;
			this.label7.Location = new System.Drawing.Point(341, 44);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(338, 17);
			this.label7.TabIndex = 18;
			this.label7.Text = "主题只对hsl的websocket服务器有效，用户名密码暂时不支持";
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
			this.textBox10.Location = new System.Drawing.Point(244, 41);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(91, 23);
			this.textBox10.TabIndex = 13;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(180, 44);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(73, 41);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(91, 23);
			this.textBox9.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 44);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "用户名：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(664, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(567, 5);
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
			this.textBox2.Text = "1883";
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
			this.panel2.Controls.Add(this.checkBox_hex_log);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Location = new System.Drawing.Point(3, 111);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(934, 539);
			this.panel2.TabIndex = 13;
			this.checkBox_hex_log.AutoSize = true;
			this.checkBox_hex_log.Location = new System.Drawing.Point(181, 183);
			this.checkBox_hex_log.Name = "checkBox_hex_log";
			this.checkBox_hex_log.Size = new System.Drawing.Size(81, 21);
			this.checkBox_hex_log.TabIndex = 27;
			this.checkBox_hex_log.Text = "Hex Log?";
			this.checkBox_hex_log.UseVisualStyleBackColor = true;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.radioButton5);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Location = new System.Drawing.Point(621, 179);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(184, 28);
			this.panel3.TabIndex = 26;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(123, 3);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(52, 21);
			this.radioButton5.TabIndex = 28;
			this.radioButton5.Text = "Json";
			this.radioButton5.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Checked = true;
			this.radioButton3.Location = new System.Drawing.Point(13, 3);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(50, 21);
			this.radioButton3.TabIndex = 26;
			this.radioButton3.TabStop = true;
			this.radioButton3.Text = "Text";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Location = new System.Drawing.Point(69, 3);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(48, 21);
			this.radioButton4.TabIndex = 27;
			this.radioButton4.Text = "Xml";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(537, 174);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(74, 21);
			this.radioButton2.TabIndex = 25;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "追加显示";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(537, 193);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(74, 21);
			this.radioButton1.TabIndex = 24;
			this.radioButton1.Text = "覆盖显示";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(867, 320);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(4, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(61, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "receive：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(833, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
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
			this.textBox4.Size = new System.Drawing.Size(867, 171);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(5, 6);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(66, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "Payload：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/12303098.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "WebSocket";
			this.userControlHead1.Size = new System.Drawing.Size(941, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(941, 650);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormWebsocketQANet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Websocket同步访问客户端";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormWebsocketQANet_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
