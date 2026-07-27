using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.MQTT;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormMqttClient : HslFormContent
	{
		private System.Windows.Forms.Timer timer1s;

		private MqttClient mqttClient;

		private StringBuilder code = new StringBuilder();

		private long receiveCount = 0L;

		private Lazy<int[]> receiveTest = new Lazy<int[]>(() => new int[10000]);

		private bool receiveTestMode = false;

		private string mqtt_will_topic;

		private string mqtt_will_message;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private UserControlHead userControlHead1;

		private TextBox textBox10;

		private Label label4;

		private TextBox textBox9;

		private Label label2;

		private TextBox textBox11;

		private Label label5;

		private TextBox textBox3;

		private Label label6;

		private Button button7;

		private Button button8;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private RadioButton radioButton_json;

		private RadioButton radioButton_xml;

		private RadioButton radioButton_text;

		private Panel panel3;

		private Button button9;

		private Label label10;

		private Button button_publish;

		private TextBox textBox6;

		private Label label11;

		private CheckBox checkBox1;

		private CheckBox checkBox_rsa;

		private Button button_will_topic;

		private Panel panel4;

		private ListBox listBox1;

		private Label label13;

		private ComboBox comboBox1;

		private CheckBox checkBox_long_message_hide;

		private CheckBox checkBox_debug_info_show;

		private RadioButton radioButton_binary;

		private Button button3;

		private CheckBox checkBox_publish_isHex;

		private Button button_certificate;

		private TextBox textBox_certificate;

		private Label label14;

		private CheckBox checkBox_sslSecure;

		private CheckBox checkBox_SslTls;

		private LinkLabel linkLabel1;

		private TextBox textBox_code;

		private Label label15;

		private CheckBox checkBox_regex_filter;

		private TextBox textBox_regex_filter;

		private Button button5;

		private Button button6;

		public FormMqttClient()
		{
			InitializeComponent();
			timer1s = new System.Windows.Forms.Timer();
			timer1s.Interval = 1000;
			timer1s.Tick += Timer1s_Tick;
			timer1s.Start();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			panel2.Enabled = false;
			button2.Enabled = false;
			comboBox1.DataSource = SoftBasic.GetEnumValues<MqttQualityOfServiceLevel>();
			Language(Program.Language);
			listBox1.MouseDoubleClick += ListBox1_MouseDoubleClick;
		}

		private void ListBox1_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (listBox1.SelectedItem is string text)
			{
				textBox5.Text = text;
			}
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				checkBox_debug_info_show.Text = "调试信息显示";
				return;
			}
			Text = "Mqtt Client Test";
			label1.Text = "Ip:";
			label3.Text = "Port:";
			button1.Text = "Connect";
			button2.Text = "Disconnect";
			label7.Text = "Topic:";
			label8.Text = "";
			label9.Text = "Payload:";
			button4.Text = "Clear";
			label12.Text = "Receive:";
			button9.Text = "Press Test";
			button7.Text = "subscribe";
			button8.Text = "unsubscribe";
			button_publish.Text = "Publish";
			button_will_topic.Text = "Will";
			label6.Text = "ClientID:";
			label2.Text = "Name:";
			label4.Text = "Pwd:";
			button3.Text = "Window-Sub";
			radioButton2.Text = "Append";
			radioButton1.Text = "Cover";
			label5.Text = "Timeout";
			checkBox_long_message_hide.Text = "Long Msg Hide";
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			if (checkBox_rsa.Checked && checkBox_SslTls.Checked)
			{
				DemoUtils.ShowMessage("无法同时勾选RSA加密通信及SSL加密通信！\r\nIt is not possible to check both RSA encryption and SSL encryption options.");
				return;
			}
			MqttConnectionOptions options = new MqttConnectionOptions
			{
				IpAddress = textBox_ip.Text,
				Port = int.Parse(textBox2.Text),
				ClientId = textBox3.Text,
				KeepAlivePeriod = TimeSpan.FromSeconds(int.Parse(textBox6.Text)),
				UseRSAProvider = checkBox_rsa.Checked,
				CleanSession = true,
				UseSSL = checkBox_SslTls.Checked
			};
			if (!string.IsNullOrEmpty(textBox9.Text) || !string.IsNullOrEmpty(textBox10.Text))
			{
				options.Credentials = new MqttCredential(textBox9.Text, textBox10.Text);
			}
			if (!string.IsNullOrEmpty(mqtt_will_topic))
			{
				options.WillMessage = new MqttApplicationMessage
				{
					Topic = mqtt_will_topic,
					Payload = Encoding.UTF8.GetBytes(mqtt_will_message)
				};
			}
			if (checkBox_SslTls.Checked)
			{
				options.CertificateFile = textBox_certificate.Text;
				options.SSLSecure = checkBox_sslSecure.Checked;
			}
			button1.Enabled = false;
			mqttClient?.ConnectClose();
			mqttClient = new MqttClient(options);
			mqttClient.LogNet = new LogNetSingle(string.Empty);
			mqttClient.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			mqttClient.OnMqttMessageReceived += MqttClient_OnMqttMessageReceived;
			OperateResult connect = await mqttClient.ConnectServerAsync();
			if (connect.IsSuccess)
			{
				panel2.Enabled = true;
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				listBox1.DataSource = mqttClient.SubcribeTopics;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
				code = CodeExampleControl.CreateStringBulider(mqttClient);
			}
			else
			{
				mqttClient = null;
				button1.Enabled = true;
				DemoUtils.ShowMessage(connect.ToMessageShowString());
			}
		}

		private void MqttClient_OnNetworkError(object sender, EventArgs e)
		{
			if (!(sender is MqttClient mqttClient))
			{
				return;
			}
			mqttClient.LogNet?.WriteInfo("网络异常，准备10秒后重新连接。");
			while (true)
			{
				Thread.Sleep(10000);
				mqttClient.LogNet?.WriteInfo("准备重新连接服务器...");
				if (mqttClient.ConnectServer().IsSuccess)
				{
					break;
				}
				mqttClient.LogNet?.WriteInfo("连接失败，准备10秒后重新连接。");
			}
			mqttClient.LogNet?.WriteInfo("连接服务器成功！");
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
			label10.Text = "Receive Count: " + receiveCount;
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (!receiveTestMode)
			{
				return;
			}
			int[] value = receiveTest.Value;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Test time: " + DateTime.Now.ToString());
			stringBuilder.AppendLine("Data Count: " + value.Sum());
			int num = 0;
			for (int i = 0; i < value.Length; i++)
			{
				if (value[i] != 1)
				{
					stringBuilder.AppendLine($"Data[{i}] = {value[i]};");
					num++;
				}
				value[i] = 0;
			}
			stringBuilder.AppendLine("Wrong Count: " + num);
			textBox8.Text = stringBuilder.ToString();
		}

		private void button6_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < 10000; i++)
			{
				mqttClient.PublishMessage(new MqttApplicationMessage
				{
					Topic = "HSL:MQTT:TEST:10000",
					Payload = BitConverter.GetBytes(i),
					Retain = true
				});
			}
			MessageBox.Show("Publish finish");
		}

		private void MqttClient_OnMqttMessageReceived(MqttClient client, MqttApplicationMessage message)
		{
			string topic = message.Topic;
			byte[] payload = message.Payload;
			try
			{
				Interlocked.Increment(ref receiveCount);
				if (topic == "HSL:MQTT:TEST:10000")
				{
					receiveTestMode = true;
					int num = BitConverter.ToInt32(payload, 0);
					receiveTest.Value[num]++;
					return;
				}
				Invoke((Action)delegate
				{
					string text = string.Empty;
					if (radioButton_binary.Checked)
					{
						text = payload.ToHexString(' ');
					}
					else if (radioButton_text.Checked)
					{
						text = Encoding.UTF8.GetString(payload);
					}
					else if (radioButton_xml.Checked)
					{
						try
						{
							text = XElement.Parse(Encoding.UTF8.GetString(payload)).ToString();
						}
						catch
						{
							text = Encoding.UTF8.GetString(payload);
						}
					}
					else if (radioButton_json.Checked)
					{
						try
						{
							text = JObject.Parse(Encoding.UTF8.GetString(payload)).ToString();
						}
						catch
						{
							text = Encoding.UTF8.GetString(payload);
						}
					}
					bool flag = true;
					if (checkBox_regex_filter.Checked)
					{
						flag = Regex.IsMatch(text, textBox_regex_filter.Text);
					}
					if (flag)
					{
						if (checkBox_long_message_hide.Checked && text != null && text.Length > 200)
						{
							text = text.Substring(200);
						}
						if (radioButton2.Checked)
						{
							textBox8.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $" Topic[{topic}] Retain[{message.Retain}]: " + Environment.NewLine + text + Environment.NewLine);
						}
						else if (radioButton1.Checked)
						{
							textBox8.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $" Topic[{topic}] Retain[{message.Retain}]: " + Environment.NewLine + text;
						}
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
				if (checkBox_debug_info_show.Checked && radioButton2.Checked)
				{
					Invoke((Action)delegate
					{
						textBox8.AppendText(e.HslMessage.ToString() + Environment.NewLine);
					});
				}
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
			mqttClient.ConnectClose();
		}

		private void button10_Click(object sender, EventArgs e)
		{
			if (comboBox1.SelectedItem is MqttQualityOfServiceLevel mqttQualityOfServiceLevel)
			{
				OperateResult operateResult = mqttClient.PublishMessage(new MqttApplicationMessage
				{
					QualityOfServiceLevel = mqttQualityOfServiceLevel,
					Topic = textBox5.Text,
					Payload = (checkBox_publish_isHex.Checked ? textBox4.Text.ToHexBytes() : Encoding.UTF8.GetBytes(textBox4.Text)),
					Retain = checkBox1.Checked
				});
				if (!operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("Send Failed:" + operateResult.Message);
				}
				textBox_code.Text = "OperateResult send = mqttClient.PublishMessage( new MqttApplicationMessage( ) { QualityOfServiceLevel = " + $"QualityOfServiceLevel.{mqttQualityOfServiceLevel}, Topic=\"{textBox5.Text}\", " + "Retain=" + checkBox1.Checked.ToString().ToLower() + ", Payload = " + (checkBox_publish_isHex.Checked ? ("\"" + textBox4.Text + "\".ToHexBytes( )") : ("Encoding.UTF8.GetBytes( \"" + textBox4.Text + "\")")) + " }";
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void Button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mqttClient.SubscribeMessage(new string[1] { textBox5.Text });
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SubscribeMessage Failed:" + operateResult.Message);
			}
			else
			{
				listBox1.DataSource = mqttClient.SubcribeTopics;
			}
			textBox_code.Text = "OperateResult send = mqttClient.SubscribeMessage( new string[] {\"" + textBox5.Text + "\"} );";
		}

		private void Button8_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mqttClient.UnSubscribeMessage(textBox5.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("UnSubscribeMessage Failed:" + operateResult.Message);
			}
			else
			{
				listBox1.DataSource = mqttClient.SubcribeTopics;
			}
			textBox_code.Text = "OperateResult send = mqttClient.UnSubscribeMessage( \"" + textBox5.Text + "\" );";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < 20; i++)
			{
				ThreadPool.QueueUserWorkItem(ThreadPoolSendTest, new MqttApplicationMessage
				{
					QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
					Topic = "ndiwh是本地AIHDniwd",
					Payload = Encoding.UTF8.GetBytes(textBox4.Text + (i + 1))
				});
			}
		}

		private async void ThreadPoolSendTest(object obj)
		{
			if (obj is MqttApplicationMessage message)
			{
				for (int i = 0; i < 100; i++)
				{
					await mqttClient.PublishMessageAsync(message);
				}
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTimeout, textBox11.Text);
			element.SetAttributeValue(DemoDeviceList.XmlKeepLive, textBox6.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCompanyID, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
			element.SetAttributeValue("WillTopic", mqtt_will_topic);
			element.SetAttributeValue("WillMessage", mqtt_will_message);
			element.SetAttributeValue("certificate", textBox_certificate.Text);
			element.SetAttributeValue("sslSecure", checkBox_sslSecure.Checked);
			element.SetAttributeValue("rsa", checkBox_rsa.Checked);
			element.SetAttributeValue("SSLTLS", checkBox_SslTls.Checked);
			element.SetAttributeValue("LongMessageHide", checkBox_long_message_hide.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox11.Text = element.Attribute(DemoDeviceList.XmlTimeout).Value;
			textBox6.Text = element.Attribute(DemoDeviceList.XmlKeepLive).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlCompanyID).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox10.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
			mqtt_will_topic = ((element.Attribute("WillTopic") == null) ? string.Empty : element.Attribute("WillTopic").Value);
			mqtt_will_message = ((element.Attribute("WillMessage") == null) ? string.Empty : element.Attribute("WillMessage").Value);
			textBox_certificate.Text = ((element.Attribute("certificate") == null) ? string.Empty : element.Attribute("certificate").Value);
			checkBox_sslSecure.Checked = element.Attribute("sslSecure") != null && bool.Parse(element.Attribute("sslSecure").Value);
			checkBox_rsa.Checked = element.Attribute("rsa") != null && bool.Parse(element.Attribute("rsa").Value);
			checkBox_SslTls.Checked = HslFormContent.GetXmlValue(element, "SSLTLS", false, bool.Parse);
			checkBox_long_message_hide.Checked = HslFormContent.GetXmlValue(element, "LongMessageHide", false, bool.Parse);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button_will_topic_Click(object sender, EventArgs e)
		{
			using (FormWillTopicSetting formWillTopicSetting = new FormWillTopicSetting(mqtt_will_topic, mqtt_will_message))
			{
				if (formWillTopicSetting.ShowDialog() == DialogResult.OK)
				{
					mqtt_will_topic = formWillTopicSetting.Topic;
					mqtt_will_message = formWillTopicSetting.Message;
				}
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			new FormMqttSubscribe(mqttClient).Show();
		}

		private void button_certificate_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox_certificate.Text = openFileDialog.FileName;
				}
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			textBox8.Text = code.ToString();
		}

		private void FormMqttClient_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
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
			this.checkBox_SslTls = new System.Windows.Forms.CheckBox();
			this.checkBox_sslSecure = new System.Windows.Forms.CheckBox();
			this.button_certificate = new System.Windows.Forms.Button();
			this.textBox_certificate = new System.Windows.Forms.TextBox();
			this.button_will_topic = new System.Windows.Forms.Button();
			this.checkBox_rsa = new System.Windows.Forms.CheckBox();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
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
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.checkBox_regex_filter = new System.Windows.Forms.CheckBox();
			this.textBox_regex_filter = new System.Windows.Forms.TextBox();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.checkBox_publish_isHex = new System.Windows.Forms.CheckBox();
			this.button3 = new System.Windows.Forms.Button();
			this.checkBox_debug_info_show = new System.Windows.Forms.CheckBox();
			this.checkBox_long_message_hide = new System.Windows.Forms.CheckBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button_publish = new System.Windows.Forms.Button();
			this.label10 = new System.Windows.Forms.Label();
			this.button9 = new System.Windows.Forms.Button();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_binary = new System.Windows.Forms.RadioButton();
			this.radioButton_json = new System.Windows.Forms.RadioButton();
			this.radioButton_text = new System.Windows.Forms.RadioButton();
			this.radioButton_xml = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.label15 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel4 = new System.Windows.Forms.Panel();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label13 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel4.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_SslTls);
			this.panel1.Controls.Add(this.checkBox_sslSecure);
			this.panel1.Controls.Add(this.button_certificate);
			this.panel1.Controls.Add(this.textBox_certificate);
			this.panel1.Controls.Add(this.button_will_topic);
			this.panel1.Controls.Add(this.checkBox_rsa);
			this.panel1.Controls.Add(this.textBox6);
			this.panel1.Controls.Add(this.label11);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
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
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.label14);
			this.panel1.Location = new System.Drawing.Point(4, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(994, 87);
			this.panel1.TabIndex = 7;
			this.checkBox_SslTls.AutoSize = true;
			this.checkBox_SslTls.Location = new System.Drawing.Point(11, 62);
			this.checkBox_SslTls.Name = "checkBox_SslTls";
			this.checkBox_SslTls.Size = new System.Drawing.Size(72, 21);
			this.checkBox_SslTls.TabIndex = 37;
			this.checkBox_SslTls.Text = "SSL/TLS";
			this.checkBox_SslTls.UseVisualStyleBackColor = true;
			this.checkBox_sslSecure.AutoSize = true;
			this.checkBox_sslSecure.Location = new System.Drawing.Point(742, 63);
			this.checkBox_sslSecure.Name = "checkBox_sslSecure";
			this.checkBox_sslSecure.Size = new System.Drawing.Size(181, 21);
			this.checkBox_sslSecure.TabIndex = 36;
			this.checkBox_sslSecure.Text = "SSL Secure ?(server check)";
			this.checkBox_sslSecure.UseVisualStyleBackColor = true;
			this.button_certificate.Location = new System.Drawing.Point(660, 57);
			this.button_certificate.Name = "button_certificate";
			this.button_certificate.Size = new System.Drawing.Size(63, 28);
			this.button_certificate.TabIndex = 35;
			this.button_certificate.Text = "Select";
			this.button_certificate.UseVisualStyleBackColor = true;
			this.button_certificate.Click += new System.EventHandler(button_certificate_Click);
			this.textBox_certificate.Location = new System.Drawing.Point(142, 61);
			this.textBox_certificate.Name = "textBox_certificate";
			this.textBox_certificate.Size = new System.Drawing.Size(510, 23);
			this.textBox_certificate.TabIndex = 34;
			this.button_will_topic.Location = new System.Drawing.Point(922, 33);
			this.button_will_topic.Name = "button_will_topic";
			this.button_will_topic.Size = new System.Drawing.Size(63, 28);
			this.button_will_topic.TabIndex = 32;
			this.button_will_topic.Text = "遗嘱";
			this.button_will_topic.UseVisualStyleBackColor = true;
			this.button_will_topic.Click += new System.EventHandler(button_will_topic_Click);
			this.checkBox_rsa.AutoSize = true;
			this.checkBox_rsa.Location = new System.Drawing.Point(621, 8);
			this.checkBox_rsa.Name = "checkBox_rsa";
			this.checkBox_rsa.Size = new System.Drawing.Size(168, 21);
			this.checkBox_rsa.TabIndex = 31;
			this.checkBox_rsa.Text = "RSA加密 (需要HSL服务器)";
			this.checkBox_rsa.UseVisualStyleBackColor = true;
			this.textBox6.Location = new System.Drawing.Point(563, 7);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(50, 23);
			this.textBox6.TabIndex = 19;
			this.textBox6.Text = "100";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(489, 10);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(77, 17);
			this.label11.TabIndex = 18;
			this.label11.Text = "KeepAlive：";
			this.textBox3.Location = new System.Drawing.Point(87, 34);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(290, 23);
			this.textBox3.TabIndex = 17;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 37);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(80, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "客户端标识：";
			this.textBox11.Location = new System.Drawing.Point(431, 7);
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(46, 23);
			this.textBox11.TabIndex = 15;
			this.textBox11.Text = "5000";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(357, 10);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "接收超时：";
			this.textBox10.Location = new System.Drawing.Point(720, 34);
			this.textBox10.Name = "textBox10";
			this.textBox10.PasswordChar = '*';
			this.textBox10.Size = new System.Drawing.Size(196, 23);
			this.textBox10.TabIndex = 13;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(658, 37);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(447, 34);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(205, 23);
			this.textBox9.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(383, 37);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "用户名：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(895, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(798, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(299, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(55, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(245, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(62, 7);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(177, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(89, 64);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(50, 17);
			this.label14.TabIndex = 33;
			this.label14.Text = "CA File:";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.button6);
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.checkBox_regex_filter);
			this.panel2.Controls.Add(this.textBox_regex_filter);
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.linkLabel1);
			this.panel2.Controls.Add(this.checkBox_publish_isHex);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.checkBox_debug_info_show);
			this.panel2.Controls.Add(this.checkBox_long_message_hide);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.button_publish);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.button9);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.button8);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.checkBox1);
			this.panel2.Controls.Add(this.label15);
			this.panel2.Location = new System.Drawing.Point(196, 123);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(802, 507);
			this.panel2.TabIndex = 13;
			this.button6.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button6.ForeColor = System.Drawing.Color.DarkGray;
			this.button6.Location = new System.Drawing.Point(3, 397);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(56, 28);
			this.button6.TabIndex = 42;
			this.button6.Text = "Test-P";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button5.ForeColor = System.Drawing.Color.DarkGray;
			this.button5.Location = new System.Drawing.Point(2, 426);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(56, 28);
			this.button5.TabIndex = 41;
			this.button5.Text = "Test-R";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.checkBox_regex_filter.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_regex_filter.AutoSize = true;
			this.checkBox_regex_filter.Location = new System.Drawing.Point(556, 481);
			this.checkBox_regex_filter.Name = "checkBox_regex_filter";
			this.checkBox_regex_filter.Size = new System.Drawing.Size(75, 21);
			this.checkBox_regex_filter.TabIndex = 40;
			this.checkBox_regex_filter.Text = "正则过滤";
			this.checkBox_regex_filter.UseVisualStyleBackColor = true;
			this.textBox_regex_filter.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox_regex_filter.Location = new System.Drawing.Point(418, 480);
			this.textBox_regex_filter.Name = "textBox_regex_filter";
			this.textBox_regex_filter.Size = new System.Drawing.Size(134, 23);
			this.textBox_regex_filter.TabIndex = 39;
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(62, 456);
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ReadOnly = true;
			this.textBox_code.Size = new System.Drawing.Size(733, 23);
			this.textBox_code.TabIndex = 38;
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(639, 483);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(82, 17);
			this.linkLabel1.TabIndex = 36;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "CodeSample";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.checkBox_publish_isHex.AutoSize = true;
			this.checkBox_publish_isHex.Location = new System.Drawing.Point(7, 68);
			this.checkBox_publish_isHex.Name = "checkBox_publish_isHex";
			this.checkBox_publish_isHex.Size = new System.Drawing.Size(55, 21);
			this.checkBox_publish_isHex.TabIndex = 35;
			this.checkBox_publish_isHex.Text = "Hex?";
			this.checkBox_publish_isHex.UseVisualStyleBackColor = true;
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(620, 5);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(98, 28);
			this.button3.TabIndex = 34;
			this.button3.Text = "子窗体订阅";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.checkBox_debug_info_show.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_debug_info_show.AutoSize = true;
			this.checkBox_debug_info_show.Location = new System.Drawing.Point(162, 482);
			this.checkBox_debug_info_show.Name = "checkBox_debug_info_show";
			this.checkBox_debug_info_show.Size = new System.Drawing.Size(128, 21);
			this.checkBox_debug_info_show.TabIndex = 33;
			this.checkBox_debug_info_show.Text = "Debug Info Show";
			this.checkBox_debug_info_show.UseVisualStyleBackColor = true;
			this.checkBox_long_message_hide.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_long_message_hide.AutoSize = true;
			this.checkBox_long_message_hide.Location = new System.Drawing.Point(297, 482);
			this.checkBox_long_message_hide.Name = "checkBox_long_message_hide";
			this.checkBox_long_message_hide.Size = new System.Drawing.Size(99, 21);
			this.checkBox_long_message_hide.TabIndex = 32;
			this.checkBox_long_message_hide.Text = "超长消息简略";
			this.checkBox_long_message_hide.UseVisualStyleBackColor = true;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(148, 180);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(161, 25);
			this.comboBox1.TabIndex = 31;
			this.button_publish.Location = new System.Drawing.Point(315, 179);
			this.button_publish.Name = "button_publish";
			this.button_publish.Size = new System.Drawing.Size(112, 28);
			this.button_publish.TabIndex = 29;
			this.button_publish.Text = "发布";
			this.button_publish.UseVisualStyleBackColor = true;
			this.button_publish.Click += new System.EventHandler(button10_Click);
			this.label10.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(2, 483);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(93, 17);
			this.label10.TabIndex = 28;
			this.label10.Text = "Receive Count:";
			this.button9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button9.Location = new System.Drawing.Point(724, 5);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(71, 28);
			this.button9.TabIndex = 27;
			this.button9.Text = "压力测试";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Visible = false;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.radioButton_binary);
			this.panel3.Controls.Add(this.radioButton_json);
			this.panel3.Controls.Add(this.radioButton_text);
			this.panel3.Controls.Add(this.radioButton_xml);
			this.panel3.Location = new System.Drawing.Point(544, 180);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(246, 28);
			this.panel3.TabIndex = 26;
			this.radioButton_binary.AutoSize = true;
			this.radioButton_binary.Location = new System.Drawing.Point(3, 3);
			this.radioButton_binary.Name = "radioButton_binary";
			this.radioButton_binary.Size = new System.Drawing.Size(62, 21);
			this.radioButton_binary.TabIndex = 29;
			this.radioButton_binary.Text = "Binary";
			this.radioButton_binary.UseVisualStyleBackColor = true;
			this.radioButton_json.AutoSize = true;
			this.radioButton_json.Location = new System.Drawing.Point(175, 3);
			this.radioButton_json.Name = "radioButton_json";
			this.radioButton_json.Size = new System.Drawing.Size(52, 21);
			this.radioButton_json.TabIndex = 28;
			this.radioButton_json.Text = "Json";
			this.radioButton_json.UseVisualStyleBackColor = true;
			this.radioButton_text.AutoSize = true;
			this.radioButton_text.Checked = true;
			this.radioButton_text.Location = new System.Drawing.Point(65, 3);
			this.radioButton_text.Name = "radioButton_text";
			this.radioButton_text.Size = new System.Drawing.Size(50, 21);
			this.radioButton_text.TabIndex = 26;
			this.radioButton_text.TabStop = true;
			this.radioButton_text.Text = "Text";
			this.radioButton_text.UseVisualStyleBackColor = true;
			this.radioButton_xml.AutoSize = true;
			this.radioButton_xml.Location = new System.Drawing.Point(121, 3);
			this.radioButton_xml.Name = "radioButton_xml";
			this.radioButton_xml.Size = new System.Drawing.Size(48, 21);
			this.radioButton_xml.TabIndex = 27;
			this.radioButton_xml.Text = "Xml";
			this.radioButton_xml.UseVisualStyleBackColor = true;
			this.radioButton2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(449, 174);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(74, 21);
			this.radioButton2.TabIndex = 25;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "追加显示";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(449, 192);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(74, 21);
			this.radioButton1.TabIndex = 24;
			this.radioButton1.Text = "覆盖显示";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(510, 5);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(104, 28);
			this.button8.TabIndex = 23;
			this.button8.Text = "取消订阅";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(Button8_Click);
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(406, 5);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(98, 28);
			this.button7.TabIndex = 22;
			this.button7.Text = "订阅";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(Button7_Click);
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(733, 239);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(2, 216);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(64, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "Receive：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(724, 479);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(71, 25);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(733, 138);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(4, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(66, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "Payload：";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(343, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(56, 17);
			this.label8.TabIndex = 10;
			this.label8.Text = "主题信息";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(274, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(4, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(52, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "Topic：";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(62, 184);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(69, 21);
			this.checkBox1.TabIndex = 30;
			this.checkBox1.Text = "Retain?";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(2, 460);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(51, 17);
			this.label15.TabIndex = 37;
			this.label15.Text = "Code：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/11631894.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MQTT";
			this.userControlHead1.Size = new System.Drawing.Size(1002, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel4.Controls.Add(this.listBox1);
			this.panel4.Controls.Add(this.label13);
			this.panel4.Location = new System.Drawing.Point(4, 123);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(189, 507);
			this.panel4.TabIndex = 15;
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(3, 23);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(181, 480);
			this.listBox1.TabIndex = 9;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(3, 3);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(129, 17);
			this.label13.TabIndex = 8;
			this.label13.Text = "Subscribed：(已订阅)";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1002, 635);
			base.Controls.Add(this.panel4);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMqttClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "MQTT客户端";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttClient_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
