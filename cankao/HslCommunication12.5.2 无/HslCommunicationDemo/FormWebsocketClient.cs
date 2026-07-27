using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.WebSocket;
using HslCommunicationDemo.DemoControl;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormWebsocketClient : HslFormContent
	{
		private WebSocketClient wsClient;

		private System.Windows.Forms.Timer timer1s;

		private long receive_times = 0L;

		private long receive_bytes = 0L;

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

		private TextBox textBox11;

		private Label label5;

		private TextBox textBox3;

		private Label label6;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private RadioButton radioButton5;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		private Panel panel3;

		private Label label7;

		private TextBox textBox5;

		private Label label8;

		private Label label10;

		private CheckBox checkBox1;

		private ComboBox comboBox1;

		private Label label11;

		private CheckBox checkBox_logHex;

		private RadioButton radioButton_hex;

		private CheckBox checkBox_SSL;

		private Button button_ssl_file;

		private TextBox textBox_ssl_ca;

		private CheckBox checkBox2;

		private LinkLabel linkLabel1;

		private Label label2;

		private Button button_stop;

		private Label label13;

		private Label label14;

		private Label label4;

		private Label label15;

		private Label label16;

		public FormWebsocketClient()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			comboBox1.DataSource = DemoUtils.GetEncodings();
			comboBox1.SelectedIndex = 3;
			Language(Program.Language);
			timer1s = new System.Windows.Forms.Timer();
			timer1s.Interval = 1000;
			timer1s.Tick += Timer1s_Tick;
			timer1s.Start();
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Websocket客户端";
				label1.Text = "Ip地址：";
				label3.Text = "端口号：";
				button1.Text = "连接";
				button2.Text = "断开连接";
				label6.Text = "主题：";
				label9.Text = "Payload：";
				button3.Text = "发送";
				button4.Text = "清空";
				label12.Text = "接收：";
				button_ssl_file.Text = "文件";
			}
			else
			{
				Text = "Websocket Client Test";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "Topic:";
				label9.Text = "Payload:";
				button3.Text = "Send";
				button4.Text = "Clear";
				label12.Text = "Receive:";
				label5.Text = "Connect-T:";
				checkBox2.Text = "GET Carray Host?";
				radioButton2.Text = "Append";
				radioButton1.Text = "Coverage";
				checkBox_SSL.Text = "Cert";
			}
		}

		private void FormWebsocketClient_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			panel2.Enabled = true;
			wsClient?.ConnectClose();
			if (textBox1.Text.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || textBox1.Text.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
			{
				wsClient = new WebSocketClient(textBox1.Text);
			}
			else
			{
				wsClient = new WebSocketClient(textBox1.Text, int.Parse(textBox2.Text), textBox5.Text);
			}
			wsClient.LogNet = new LogNetSingle(string.Empty);
			wsClient.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			wsClient.OnClientApplicationMessageReceive += WebSocket_OnWebSocketMessageReceived;
			wsClient.GetCarryHostAndPort = checkBox2.Checked;
			if (!int.TryParse(textBox11.Text, out var result))
			{
				DemoUtils.ShowMessage("Connect time is not correct!");
				return;
			}
			wsClient.ConnectTimeOut = result;
			if (checkBox_SSL.Checked)
			{
				wsClient.UseSSL(textBox_ssl_ca.Text);
			}
			OperateResult operateResult = null;
			operateResult = ((!string.IsNullOrEmpty(textBox3.Text)) ? wsClient.ConnectServer(textBox3.Text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries)) : wsClient.ConnectServer());
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				receive_times = 0L;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (textBox1.Text.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || textBox1.Text.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
			{
				stringBuilder.AppendLine("WebSocketClient wsClient = new WebSocketClient( \"" + textBox1.Text + "\" );");
			}
			else
			{
				stringBuilder.AppendLine($"WebSocketClient wsClient = new WebSocketClient( \"{textBox1.Text}\", {int.Parse(textBox2.Text)}, \"{textBox5.Text}\" );");
			}
			stringBuilder.AppendLine("wsClient.GetCarryHostAndPort = " + checkBox2.Checked.ToString().ToLower() + ";");
			if (checkBox_SSL.Checked)
			{
				stringBuilder.AppendLine("wsClient.UseSSL( \"" + textBox_ssl_ca.Text + "\" );");
			}
			stringBuilder.AppendLine("wsClient.ConnectTimeOut = " + textBox11.Text + ";");
			if (string.IsNullOrEmpty(textBox3.Text))
			{
				stringBuilder.AppendLine("OperateResult connect = wsClient.ConnectServer( );");
			}
			else
			{
				stringBuilder.AppendLine("OperateResult connect = wsClient.ConnectServer( \"" + textBox3.Text + "\".Split( new char[] {','}, StringSplitOptions.RemoveEmptyEntries ) );");
			}
			stringBuilder.AppendLine("//wsClient.OnClientApplicationMessageReceive += WebSocket_OnWebSocketMessageReceived;  // 收到数据触发事件");
			stringBuilder.AppendLine("//wsClient.SendServer( \"ABCD1234\" );   // 发送数据到服务器上去");
			textBox8.Text = stringBuilder.ToString();
		}

		private void WsClient_OnNetworkError(object sender, EventArgs e)
		{
			if (!(sender is WebSocketClient webSocketClient))
			{
				return;
			}
			webSocketClient.LogNet?.WriteInfo("网络异常，准备10秒后重新连接。");
			while (true)
			{
				Thread.Sleep(10000);
				webSocketClient.LogNet?.WriteInfo("准备重新连接服务器...");
				if (webSocketClient.IsClosed)
				{
					return;
				}
				if (webSocketClient.ConnectServer().IsSuccess)
				{
					break;
				}
				webSocketClient.LogNet?.WriteInfo("连接失败，准备10秒后重新连接。");
			}
			webSocketClient.LogNet?.WriteInfo("连接服务器成功！");
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
			if (wsClient != null)
			{
				label4.Text = receive_times.ToString();
				label13.Text = SoftBasic.GetSizeDescription(receive_bytes);
			}
		}

		private void WebSocket_OnWebSocketMessageReceived(WebSocketMessage message)
		{
			try
			{
				receive_times++;
				if (message.Payload != null)
				{
					receive_bytes += message.Payload.Length;
				}
				if (checkBox_logHex.Checked)
				{
					wsClient.LogNet?.WriteDebug(wsClient.ToString(), $"OpCode[{message.OpCode}] HasMask[{message.HasMask}] Payload: {message.Payload.ToHexString(' ')}");
				}
				Invoke((Action)delegate
				{
					string empty = string.Empty;
					if (radioButton_hex.Checked)
					{
						empty = message.Payload.ToHexString(' ');
					}
					else
					{
						empty = DemoUtils.GetEncodingFromIndex(comboBox1.SelectedIndex).GetString(message.Payload);
						if (radioButton4.Checked)
						{
							try
							{
								empty = XElement.Parse(empty).ToString();
							}
							catch
							{
							}
						}
						else if (radioButton5.Checked)
						{
							try
							{
								empty = JObject.Parse(empty).ToString();
							}
							catch
							{
							}
						}
					}
					if (radioButton2.Checked)
					{
						wsClient.LogNet?.WriteInfo(empty);
					}
					else if (radioButton1.Checked)
					{
						textBox8.Text = empty;
					}
				});
			}
			catch
			{
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			try
			{
				Invoke((Action)delegate
				{
					if (radioButton2.Checked)
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
			wsClient?.ConnectClose();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = wsClient.SendServer(checkBox1.Checked, textBox4.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Failed:" + operateResult.Message);
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUrl, textBox5.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTimeout, textBox11.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTopic, textBox3.Text);
			element.SetAttributeValue("GetCarryHostAndPort", checkBox2.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox5.Text = element.Attribute(DemoDeviceList.XmlUrl).Value;
			textBox11.Text = element.Attribute(DemoDeviceList.XmlTimeout).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlTopic).Value;
			checkBox2.Checked = HslFormContent.GetXmlValue(element, "GetCarryHostAndPort", false, bool.Parse);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button_ssl_file_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox_ssl_ca.Text = openFileDialog.FileName;
				}
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.button_ssl_file = new System.Windows.Forms.Button();
			this.textBox_ssl_ca = new System.Windows.Forms.TextBox();
			this.checkBox_SSL = new System.Windows.Forms.CheckBox();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button_stop = new System.Windows.Forms.Button();
			this.checkBox_logHex = new System.Windows.Forms.CheckBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label11 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_hex = new System.Windows.Forms.RadioButton();
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
			this.panel1.Controls.Add(this.linkLabel1);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.button_ssl_file);
			this.panel1.Controls.Add(this.textBox_ssl_ca);
			this.panel1.Controls.Add(this.checkBox_SSL);
			this.panel1.Controls.Add(this.textBox5);
			this.panel1.Controls.Add(this.label7);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox11);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.label10);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 97);
			this.panel1.TabIndex = 7;
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(897, 72);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(57, 17);
			this.linkLabel1.TabIndex = 27;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "<Code>";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(669, 38);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(143, 21);
			this.checkBox2.TabIndex = 26;
			this.checkBox2.Text = "GET 携带 Host 信息?";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.button_ssl_file.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_ssl_file.Location = new System.Drawing.Point(779, 66);
			this.button_ssl_file.Name = "button_ssl_file";
			this.button_ssl_file.Size = new System.Drawing.Size(91, 28);
			this.button_ssl_file.TabIndex = 25;
			this.button_ssl_file.Text = "FILE";
			this.button_ssl_file.UseVisualStyleBackColor = true;
			this.button_ssl_file.Click += new System.EventHandler(button_ssl_file_Click);
			this.textBox_ssl_ca.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_ssl_ca.Location = new System.Drawing.Point(62, 69);
			this.textBox_ssl_ca.Name = "textBox_ssl_ca";
			this.textBox_ssl_ca.Size = new System.Drawing.Size(711, 23);
			this.textBox_ssl_ca.TabIndex = 24;
			this.checkBox_SSL.AutoSize = true;
			this.checkBox_SSL.Location = new System.Drawing.Point(8, 71);
			this.checkBox_SSL.Name = "checkBox_SSL";
			this.checkBox_SSL.Size = new System.Drawing.Size(51, 21);
			this.checkBox_SSL.TabIndex = 23;
			this.checkBox_SSL.Text = "证书";
			this.checkBox_SSL.UseVisualStyleBackColor = true;
			this.textBox5.Location = new System.Drawing.Point(498, 6);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(291, 23);
			this.textBox5.TabIndex = 21;
			this.label7.AutoSize = true;
			this.label7.ForeColor = System.Drawing.Color.FromArgb(128, 128, 255);
			this.label7.Location = new System.Drawing.Point(249, 42);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(206, 17);
			this.label7.TabIndex = 18;
			this.label7.Text = "主题只对hsl的websocket服务器有效";
			this.textBox3.Location = new System.Drawing.Point(62, 38);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(181, 23);
			this.textBox3.TabIndex = 17;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(5, 41);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "主题：";
			this.textBox11.Location = new System.Drawing.Point(929, 36);
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(51, 23);
			this.textBox11.TabIndex = 15;
			this.textBox11.Text = "5000";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(845, 39);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "连接超时：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(892, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(795, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(392, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(65, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(337, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(59, 7);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(274, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(5, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(460, 9);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(35, 17);
			this.label10.TabIndex = 22;
			this.label10.Text = "url：";
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label8.Location = new System.Drawing.Point(495, 33);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(145, 17);
			this.label8.TabIndex = 20;
			this.label8.Text = "url举例: /A/B?C=123456";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label15);
			this.panel2.Controls.Add(this.label16);
			this.panel2.Controls.Add(this.label13);
			this.panel2.Controls.Add(this.label14);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.button_stop);
			this.panel2.Controls.Add(this.checkBox_logHex);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.checkBox1);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Location = new System.Drawing.Point(3, 136);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 507);
			this.panel2.TabIndex = 13;
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label15.AutoSize = true;
			this.label15.ForeColor = System.Drawing.Color.Gray;
			this.label15.Location = new System.Drawing.Point(575, 483);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(298, 17);
			this.label15.TabIndex = 37;
			this.label15.Text = "wsClient.SendServer( bool mask, string message )";
			this.label16.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label16.AutoSize = true;
			this.label16.ForeColor = System.Drawing.Color.Gray;
			this.label16.Location = new System.Drawing.Point(461, 483);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(104, 17);
			this.label16.TabIndex = 36;
			this.label16.Text = "Code Example：";
			this.label13.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label13.AutoSize = true;
			this.label13.ForeColor = System.Drawing.Color.Gray;
			this.label13.Location = new System.Drawing.Point(367, 483);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(15, 17);
			this.label13.TabIndex = 35;
			this.label13.Text = "0";
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label14.AutoSize = true;
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(291, 483);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(76, 17);
			this.label14.TabIndex = 34;
			this.label14.Text = "Total Size：";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.Color.Gray;
			this.label4.Location = new System.Drawing.Point(207, 483);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(15, 17);
			this.label4.TabIndex = 33;
			this.label4.Text = "0";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Gray;
			this.label2.Location = new System.Drawing.Point(105, 483);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(102, 17);
			this.label2.TabIndex = 32;
			this.label2.Text = "Receive Times：";
			this.button_stop.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_stop.Location = new System.Drawing.Point(848, 180);
			this.button_stop.Name = "button_stop";
			this.button_stop.Size = new System.Drawing.Size(68, 28);
			this.button_stop.TabIndex = 31;
			this.button_stop.Text = "暂停";
			this.button_stop.UseVisualStyleBackColor = true;
			this.checkBox_logHex.AutoSize = true;
			this.checkBox_logHex.Location = new System.Drawing.Point(8, 240);
			this.checkBox_logHex.Name = "checkBox_logHex";
			this.checkBox_logHex.Size = new System.Drawing.Size(81, 21);
			this.checkBox_logHex.TabIndex = 30;
			this.checkBox_logHex.Text = "Log Hex?";
			this.checkBox_logHex.UseVisualStyleBackColor = true;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(364, 182);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(100, 25);
			this.comboBox1.TabIndex = 29;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(291, 186);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(65, 17);
			this.label11.TabIndex = 28;
			this.label11.Text = "Encoding:";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(209, 185);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(65, 21);
			this.checkBox1.TabIndex = 27;
			this.checkBox1.Text = "Mask?";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.panel3.Controls.Add(this.radioButton_hex);
			this.panel3.Controls.Add(this.radioButton5);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Location = new System.Drawing.Point(559, 179);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(243, 28);
			this.panel3.TabIndex = 26;
			this.radioButton_hex.AutoSize = true;
			this.radioButton_hex.Location = new System.Drawing.Point(3, 3);
			this.radioButton_hex.Name = "radioButton_hex";
			this.radioButton_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_hex.TabIndex = 29;
			this.radioButton_hex.Text = "Hex";
			this.radioButton_hex.UseVisualStyleBackColor = true;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(165, 3);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(52, 21);
			this.radioButton5.TabIndex = 28;
			this.radioButton5.Text = "Json";
			this.radioButton5.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Checked = true;
			this.radioButton3.Location = new System.Drawing.Point(55, 3);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(50, 21);
			this.radioButton3.TabIndex = 26;
			this.radioButton3.TabStop = true;
			this.radioButton3.Text = "Text";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Location = new System.Drawing.Point(111, 3);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(48, 21);
			this.radioButton4.TabIndex = 27;
			this.radioButton4.Text = "Xml";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(479, 174);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(74, 21);
			this.radioButton2.TabIndex = 25;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "追加显示";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(479, 193);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(74, 21);
			this.radioButton1.TabIndex = 24;
			this.radioButton1.Text = "覆盖显示";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(107, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(881, 264);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(4, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(64, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "Receive：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(921, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(68, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(105, 180);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "发送";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(105, 3);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(883, 171);
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
			base.Name = "FormWebsocketClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Websocket客户端";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormWebsocketClient_FormClosing);
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
