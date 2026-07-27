using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.WebSocket;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormWebsocketServer : HslFormContent
	{
		private System.Windows.Forms.Timer timer1s;

		private WebSocketServer wsServer;

		private Random random = new Random();

		private long count = 0L;

		private bool startThreadPublish = false;

		private Thread thread;

		private bool isStop = false;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private UserControlHead userControlHead1;

		private Button button5;

		private Button button6;

		private CheckBox checkBox1;

		private Label label2;

		private Button button7;

		private CheckBox checkBox2;

		private CheckBox checkBox3;

		private Button button8;

		private ListBox listBox1;

		private Label label5;

		private Button button9;

		private CheckBox checkBox_willcard;

		private Panel panel3;

		private Button button10;

		private TextBox textBox_certFile;

		private CheckBox checkBox_ssl;

		private TextBox textBox_ssl_password;

		private Label label1;

		private Label label4;

		private TextBox textBox_code;

		private ComboBox comboBox1;

		private Label label6;

		private Label label_tick;

		public FormWebsocketServer()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			comboBox1.SelectedIndex = 0;
			Language(Program.Language);
			timer1s = new System.Windows.Forms.Timer();
			timer1s.Interval = 1000;
			timer1s.Tick += Timer1s_Tick;
			timer1s.Start();
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
			if (wsServer != null)
			{
				label2.Text = "Online Count:" + wsServer.OnlineCount;
				listBox1.DataSource = wsServer.OnlineSessions;
				if (Program.Language == 1)
				{
					label_tick.Text = "接收次数: " + count;
				}
				else
				{
					label_tick.Text = "Recv Tick: " + count;
				}
			}
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Websocket服务器";
				label3.Text = "端口：";
				button1.Text = "启动服务";
				button2.Text = "关闭服务";
				button5.Text = "广播100K数据";
				label7.Text = "Topic：";
				label8.Text = "主题";
				label9.Text = "Payload：";
				button3.Text = "广播内容";
				button4.Text = "清空";
				label12.Text = "接收：";
				checkBox2.Text = "是否开启订阅缓存";
			}
			else
			{
				Text = "Websocket Server Test";
				label3.Text = "Port:";
				button1.Text = "Start";
				button2.Text = "Close";
				button5.Text = "Publish 100K Data";
				label7.Text = "Topic:";
				label8.Text = "";
				label9.Text = "Payload:";
				button3.Text = "Publish Payload";
				button4.Text = "Clear";
				label12.Text = "Receive:";
				checkBox2.Text = "Start Topic Cache";
				checkBox3.Text = "Send test message back when client connect";
				button8.Text = "web test";
				checkBox_willcard.Text = "Topic willcard?";
				button6.Text = "Broadcast";
				button7.Text = "Stop";
			}
		}

		private void FormWebsocketServer_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				wsServer = new WebSocketServer();
				wsServer.OnClientApplicationMessageReceive += WebSocket_OnClientApplicationMessageReceive;
				wsServer.OnClientConnected += WsServer_OnClientConnected;
				wsServer.IsTopicRetain = checkBox2.Checked;
				if (checkBox_ssl.Checked)
				{
					wsServer.UseSSL(textBox_certFile.Text, textBox_ssl_password.Text);
				}
				wsServer.ServerStart(int.Parse(textBox2.Text));
				wsServer.LogNet = new LogNetSingle("");
				wsServer.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				wsServer.TopicWildcard = checkBox_willcard.Checked;
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage("Start Success");
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("WebSocketServer wsServer = new WebSocketServer( );");
				stringBuilder.AppendLine($"wsServer.IsTopicRetain = {checkBox2.Checked};");
				if (checkBox_ssl.Checked)
				{
					stringBuilder.AppendLine("wsServer.UseSSL( \"" + textBox_certFile.Text + "\", \"" + textBox_ssl_password.Text + "\" );");
				}
				stringBuilder.Append($"wsServer.ServerStart( {int.Parse(textBox2.Text)} );");
				textBox_code.Text = stringBuilder.ToString();
				count = 0L;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Start Failed : " + ex.Message);
			}
		}

		private void WsServer_OnClientConnected(WebSocketSession session)
		{
			if (checkBox3.Checked && !session.IsQASession)
			{
				wsServer.SendClientPayload(session, "This a test message when client connect, url:" + session.Url);
			}
		}

		private void WebSocket_OnClientApplicationMessageReceive(WebSocketSession session, WebSocketMessage message)
		{
			count++;
			if (session.IsQASession)
			{
				wsServer.SendClientPayload(session, Encoding.UTF8.GetString(message.Payload) + " " + random.Next(1000, 10000));
			}
			Invoke((Action)delegate
			{
				if (!isStop)
				{
					if (message.OpCode == 1)
					{
						textBox8.AppendText($"OpCode:[{message.OpCode}] Mask:[{message.HasMask}] Payload:[{Encoding.UTF8.GetString(message.Payload)}]" + Environment.NewLine);
					}
					else
					{
						textBox8.AppendText($"OpCode:[{message.OpCode}] Mask:[{message.HasMask}] Payload:[{message.Payload.ToHexString(' ')}]" + Environment.NewLine);
					}
				}
			});
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox8.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			});
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button5.Enabled = true;
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			wsServer.ServerClose();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (comboBox1.SelectedIndex == 0)
			{
				wsServer.PublishAllClientPayload(textBox4.Text);
				textBox_code.Text = "wsServer.PublishAllClientPayload( \"" + textBox4.Text + "\" );  // opCode = 1";
			}
			else
			{
				wsServer.PublishAllClientPayload(textBox4.Text.ToHexBytes());
				textBox_code.Text = "wsServer.PublishAllClientPayload( \"" + textBox4.Text + "\".ToHexBytes( ) ); // opCode = 2";
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void Button5_Click(object sender, EventArgs e)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < 102400; i++)
			{
				stringBuilder.Append("2");
			}
			wsServer.PublishAllClientPayload(stringBuilder.ToString());
		}

		private void button9_Click(object sender, EventArgs e)
		{
			if (startThreadPublish)
			{
				startThreadPublish = false;
				button9.Text = "高频发布测试";
				return;
			}
			startThreadPublish = true;
			button9.Text = ((Program.Language == 1) ? "停止" : "Stop");
			if (thread == null)
			{
				thread = new Thread(ThreadTest);
				thread.IsBackground = true;
				thread.Start();
			}
		}

		private void ThreadTest()
		{
			int num = 0;
			while (true)
			{
				Thread.Sleep(200);
				if (startThreadPublish)
				{
					DateTime now = DateTime.Now;
					for (int i = 0; i < 10; i++)
					{
						byte[] array = new byte[1024];
						random.NextBytes(array);
						wsServer.PublishAllClientPayload(array.ToHexString() + "\r\n上次平均耗时：" + num + "ms");
					}
					num = Convert.ToInt32((DateTime.Now - now).TotalSeconds / 10.0);
				}
			}
		}

		private void button6_Click_1(object sender, EventArgs e)
		{
			wsServer.PublishClientPayload(textBox5.Text, textBox4.Text);
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (!isStop)
			{
				button7.Text = ((Program.Language == 1) ? "继续" : "Continue");
				isStop = true;
			}
			else
			{
				isStop = false;
				button7.Text = ((Program.Language == 1) ? "暂停" : "Stop");
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			string contents = "<html>\r\n\t<head>\r\n\t\t<title>\r\n\t\t\t测试的websocket信息-hslcommunication\r\n\t\t</title>\r\n\t</head>\r\n\t<body>\r\n\t\t<div id=\"hsl\"></div>\r\n\t</body>\r\n\t<script type=\"text/javascript\">\r\n\t\tif (\"WebSocket\" in window)\r\n\t\t{\r\n\t\t\t// 打开一个 web socket\r\n\t\t\tvar ws = new WebSocket('ws" + (checkBox_ssl.Checked ? "s" : "") + "://127.0.0.1:" + textBox2.Text + "');\r\n\t\t\tvar count = 0;\r\n\t\t\tws.onopen = function()\r\n\t\t\t{\r\n\t\t\t\t console.log('已经打开...');\r\n\t\t\t};\r\n\t\t\tws.onmessage = function (evt)\r\n\t\t\t{\r\n\t\t\t\tvar received_msg = evt.data;\r\n\t\t\t\tvar obj = document.getElementById('hsl');\r\n\t\t\t\tobj.innerText = received_msg;\r\n\t\t\t\tcount++;\r\n\t\t\t};\r\n\t\t\tvar int=self.setInterval(\"clock()\",1000);\r\n\t\t\tfunction clock()\r\n\t\t\t{\r\n\t\t\t\t console.log('接收次数:' + count);\r\n\t\t\t}\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\tvar obj = document.getElementById('hsl');\r\n\t\t\tobj.innerText = '您的浏览器不支持 WebSocket!';\r\n\t\t}\r\n\t</script>\r\n</html>";
			try
			{
				File.WriteAllText("websocket.html", contents, Encoding.UTF8);
				Process.Start("websocket.html");
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTagCache, checkBox2.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlRetureMessage, checkBox3.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			checkBox2.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlTagCache).Value);
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlRetureMessage).Value);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button10_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox_certFile.Text = openFileDialog.FileName;
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
			this.textBox_ssl_password = new System.Windows.Forms.TextBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.button10 = new System.Windows.Forms.Button();
			this.textBox_certFile = new System.Windows.Forms.TextBox();
			this.checkBox_ssl = new System.Windows.Forms.CheckBox();
			this.checkBox_willcard = new System.Windows.Forms.CheckBox();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label_tick = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.button9 = new System.Windows.Forms.Button();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_ssl_password);
			this.panel1.Controls.Add(this.panel3);
			this.panel1.Controls.Add(this.checkBox_willcard);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 72);
			this.panel1.TabIndex = 7;
			this.textBox_ssl_password.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_ssl_password.Location = new System.Drawing.Point(810, 5);
			this.textBox_ssl_password.Name = "textBox_ssl_password";
			this.textBox_ssl_password.Size = new System.Drawing.Size(178, 23);
			this.textBox_ssl_password.TabIndex = 13;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.button10);
			this.panel3.Controls.Add(this.textBox_certFile);
			this.panel3.Controls.Add(this.checkBox_ssl);
			this.panel3.Location = new System.Drawing.Point(442, 35);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(546, 31);
			this.panel3.TabIndex = 10;
			this.button10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button10.Location = new System.Drawing.Point(502, 1);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(44, 28);
			this.button10.TabIndex = 29;
			this.button10.Text = "FILE";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.textBox_certFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_certFile.Location = new System.Drawing.Point(83, 4);
			this.textBox_certFile.Name = "textBox_certFile";
			this.textBox_certFile.Size = new System.Drawing.Size(413, 23);
			this.textBox_certFile.TabIndex = 11;
			this.checkBox_ssl.AutoSize = true;
			this.checkBox_ssl.Location = new System.Drawing.Point(18, 6);
			this.checkBox_ssl.Name = "checkBox_ssl";
			this.checkBox_ssl.Size = new System.Drawing.Size(59, 21);
			this.checkBox_ssl.TabIndex = 10;
			this.checkBox_ssl.Text = "SSL？";
			this.checkBox_ssl.UseVisualStyleBackColor = true;
			this.checkBox_willcard.AutoSize = true;
			this.checkBox_willcard.Location = new System.Drawing.Point(149, 41);
			this.checkBox_willcard.Name = "checkBox_willcard";
			this.checkBox_willcard.Size = new System.Drawing.Size(99, 21);
			this.checkBox_willcard.TabIndex = 9;
			this.checkBox_willcard.Text = "订阅通配符？";
			this.checkBox_willcard.UseVisualStyleBackColor = true;
			this.checkBox3.AutoSize = true;
			this.checkBox3.Checked = true;
			this.checkBox3.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox3.Location = new System.Drawing.Point(278, 41);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(147, 21);
			this.checkBox3.TabIndex = 8;
			this.checkBox3.Text = "是否回发一条测试数据";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Checked = true;
			this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox2.Location = new System.Drawing.Point(11, 40);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(123, 21);
			this.checkBox2.TabIndex = 7;
			this.checkBox2.Text = "是否开启订阅缓存";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(390, 8);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(288, 21);
			this.checkBox1.TabIndex = 6;
			this.checkBox1.Text = "启用用户名和密码(用户名 admin  密码: 123456)";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(288, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(191, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(62, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(112, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(710, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(100, 17);
			this.label1.TabIndex = 14;
			this.label1.Text = "SSL Password：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label_tick);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.button9);
			this.panel2.Controls.Add(this.listBox1);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.button8);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.button6);
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(3, 111);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 531);
			this.panel2.TabIndex = 13;
			this.label_tick.AutoSize = true;
			this.label_tick.ForeColor = System.Drawing.Color.Gray;
			this.label_tick.Location = new System.Drawing.Point(360, 186);
			this.label_tick.Name = "label_tick";
			this.label_tick.Size = new System.Drawing.Size(70, 17);
			this.label_tick.TabIndex = 40;
			this.label_tick.Text = "接收次数: 0";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[2] { "Text", "Binary" });
			this.comboBox1.Location = new System.Drawing.Point(62, 181);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(89, 25);
			this.comboBox1.TabIndex = 39;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(2, 186);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(56, 17);
			this.label6.TabIndex = 38;
			this.label6.Text = "Frame：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(62, 461);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(926, 65);
			this.textBox_code.TabIndex = 36;
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(2, 464);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(51, 17);
			this.label4.TabIndex = 35;
			this.label4.Text = "Code：";
			this.button9.Location = new System.Drawing.Point(586, 180);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(128, 28);
			this.button9.TabIndex = 34;
			this.button9.Text = "高频发布测试";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.BackColor = System.Drawing.Color.LightGray;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(649, 33);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(339, 140);
			this.listBox1.TabIndex = 33;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(652, 11);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(93, 17);
			this.label5.TabIndex = 32;
			this.label5.Text = "Online Client：";
			this.button8.Location = new System.Drawing.Point(718, 180);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(102, 28);
			this.button8.TabIndex = 29;
			this.button8.Text = "测试网页";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(825, 180);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(77, 28);
			this.button7.TabIndex = 28;
			this.button7.Text = "暂停";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(902, 11);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(86, 17);
			this.label2.TabIndex = 27;
			this.label2.Text = "Online Count:";
			this.button6.Location = new System.Drawing.Point(259, 180);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(91, 28);
			this.button6.TabIndex = 26;
			this.button6.Text = "广播订阅";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click_1);
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(477, 4);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(164, 28);
			this.button5.TabIndex = 20;
			this.button5.Text = "发布100K数据";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(Button5_Click);
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(926, 243);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(2, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(64, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "Receive：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(905, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(83, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(162, 180);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "广播内容";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(581, 138);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(2, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(66, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "Payload：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(317, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(56, 17);
			this.label8.TabIndex = 10;
			this.label8.Text = "主题信息";
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(249, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(2, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(52, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "Topic：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/12303098.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Websocket";
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
			base.Name = "FormWebsocketServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Websocket 服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormWebsocketServer_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
