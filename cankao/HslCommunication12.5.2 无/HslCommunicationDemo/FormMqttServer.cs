using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.CNC.Fanuc;
using HslCommunication.Core;
using HslCommunication.LogNet;
using HslCommunication.MQTT;
using HslCommunication.Profinet.AllenBradley;
using HslCommunication.Profinet.Siemens;
using HslCommunication.Reflection;
using HslCommunicationDemo.DemoControl;
using HslControls;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormMqttServer : HslFormContent
	{
		public class Student
		{
			public string Name { get; set; }

			public int Age { get; set; }

			public string ID { get; set; }
		}

		private Button button_publish = null;

		private int[] datas = new int[30];

		private string[] texts = new string[30];

		private int dataCount = 30;

		private int lastMinute = -1;

		private int lastHour = -1;

		private int lastDay = -1;

		private System.Windows.Forms.Timer timer1s;

		private System.Windows.Forms.Timer timer_publish;

		private MqttServer mqttServer;

		private SiemensS7Net siemens;

		private System.Threading.Timer timerPublish;

		private int publishTick = 0;

		private long receiveCount = 0L;

		private long lastReceiveCount = 0L;

		private long receiveSize = 0L;

		private Dictionary<string, TopicSaveItem> all_dicts = new Dictionary<string, TopicSaveItem>();

		private TopicSaveItem selectedItem = null;

		private Random random = new Random();

		private bool isStop = false;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private Panel panel2;

		private TextBox textBox8;

		private Button button_clear;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private UserControlHead userControlHead1;

		private TextBox textBox_publish_clientID;

		private Label label1;

		private Button button5;

		private Button button6;

		private CheckBox checkBox1;

		private Label label2;

		private Button button_stop;

		private Label label_receive_count;

		private CheckBox checkBox3;

		private ListBox listBox1;

		private Label label5;

		private CheckBox checkBox2;

		private CheckBox checkBox_long_message_hide;

		private CheckBox checkBox_publish_isHex;

		private Panel panel3;

		private RadioButton radioButton_recv_UTF8;

		private RadioButton radioButton_recv_ASCII;

		private RadioButton radioButton_recv_Hex;

		private RadioButton radioButton_recv_Unicode;

		private RadioButton radioButton_recv_gb2312;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private TabPage tabPage3;

		private TextBox textBox_session_port;

		private Label label12;

		private TextBox textBox_session_ip;

		private Label label11;

		private TextBox textBox_session_onlineTime;

		private Label label10;

		private Label label6;

		private ComboBox comboBox_session_select;

		private TextBox textBox_session_clientID;

		private Label label13;

		private Label label15;

		private TextBox textBox_session_rsa;

		private Label label14;

		private TextBox textBox_session_topics;

		private TextBox textBox_session_willTopic;

		private Label label17;

		private TextBox textBox_session_userName;

		private Label label16;

		private Label label18;

		private TextBox textBox1;

		private CheckBox checkBox_publish_timer;

		private DataGridView dataGridView1;

		private TextBox textBox_topic_publishSession;

		private Label label20;

		private TextBox textBox_topic_publishTime;

		private Label label19;

		private TextBox textBox_topic_payload;

		private Label label22;

		private TextBox textBox_session_activeTime;

		private Label label23;

		private CheckBox checkBox_retain;

		private Button button4;

		private TextBox textBox_login_password;

		private Label label_login_password;

		private TextBox textBox_login_name;

		private Label label_login_name;

		private TabPage tabPage4;

		private Panel panel4;

		private Label label_receive_size;

		private Panel panel5;

		private RadioButton radioButton_every_minute;

		private RadioButton radioButton_every_seconds;

		private RadioButton radioButton_every_hour;

		private HslBarChart hslBarChart1;

		private RadioButton radioButton_every_day;

		private CheckBox checkBox_skip_zero;

		private Button button_publish_test;

		private SslServerControl sslServerControl1;

		private TextBox textBox_topic_topic;

		private Label label4;

		private Button button7;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private Panel panel6;

		private RadioButton radioButton_topic_render_json;

		private RadioButton radioButton_topic_render_text;

		private Label label24;

		private Label label_topic_size;

		private Label label21;

		private CheckBox checkBox_topic_retain;

		public FormMqttServer()
		{
			InitializeComponent();
			comboBox_session_select.SelectedIndexChanged += ComboBox_session_select_SelectedIndexChanged;
			radioButton_every_seconds.CheckedChanged += RadioButton_every_seconds_CheckedChanged;
			radioButton_every_minute.CheckedChanged += RadioButton_every_seconds_CheckedChanged;
			radioButton_every_hour.CheckedChanged += RadioButton_every_seconds_CheckedChanged;
			radioButton_every_day.CheckedChanged += RadioButton_every_seconds_CheckedChanged;
		}

		private void RadioButton_every_seconds_CheckedChanged(object sender, EventArgs e)
		{
			if (sender is RadioButton radioButton && radioButton.Checked)
			{
				datas = new int[dataCount];
				texts = new string[dataCount];
				lastMinute = -1;
				lastHour = -1;
				lastDay = -1;
				hslBarChart1.SetDataSource(datas, texts);
			}
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			Language(Program.Language);
			timer1s = new System.Windows.Forms.Timer();
			timer1s.Interval = 1000;
			timer1s.Tick += Timer1s_Tick;
			timer1s.Start();
		}

		private void checkBox_publish_timer_CheckedChanged(object sender, EventArgs e)
		{
			int result;
			if (!checkBox_publish_timer.Checked)
			{
				button_publish = null;
				timer_publish?.Stop();
				timer_publish?.Dispose();
			}
			else if (!int.TryParse(textBox1.Text, out result))
			{
				DemoUtils.ShowMessage("Timer interval input wrong!");
			}
			else
			{
				timer_publish = new System.Windows.Forms.Timer();
				timer_publish.Interval = result;
				timer_publish.Tick += Timer_publish_Tick;
				timer_publish.Start();
			}
		}

		private void Timer_publish_Tick(object sender, EventArgs e)
		{
			if (button_publish != null)
			{
				button_publish.PerformClick();
			}
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
			if (mqttServer == null)
			{
				return;
			}
			long num = receiveCount;
			label2.Text = "Online Count:" + mqttServer.OnlineCount;
			label_receive_count.Text = "Receive Count: " + num;
			label_receive_size.Text = "Size: " + SoftBasic.GetSizeDescription(receiveSize);
			listBox1.DataSource = mqttServer.OnlineSessions;
			int num2 = (int)(num - lastReceiveCount);
			lastReceiveCount = num;
			DateTime now = DateTime.Now;
			if (radioButton_every_seconds.Checked)
			{
				if (!checkBox_skip_zero.Checked || num2 != 0)
				{
					SoftBasic.AddArrayData(ref datas, new int[1] { num2 }, dataCount);
					SoftBasic.AddArrayData(ref texts, new string[1] { DateTime.Now.Second.ToString() }, dataCount);
				}
				hslBarChart1.SetDataSource(datas, texts);
			}
			else if (radioButton_every_minute.Checked)
			{
				if (now.Minute != lastMinute)
				{
					lastMinute = now.Minute;
					SoftBasic.AddArrayData(ref datas, new int[1] { num2 }, dataCount);
					SoftBasic.AddArrayData(ref texts, new string[1] { lastMinute + "M" }, dataCount);
				}
				else
				{
					datas[datas.Length - 1] += num2;
				}
				hslBarChart1.SetDataSource(datas, texts);
			}
			else if (radioButton_every_hour.Checked)
			{
				if (now.Hour != lastHour)
				{
					lastHour = now.Hour;
					SoftBasic.AddArrayData(ref datas, new int[1] { num2 }, dataCount);
					SoftBasic.AddArrayData(ref texts, new string[1] { lastHour + "M" }, dataCount);
				}
				else
				{
					datas[datas.Length - 1] += num2;
				}
				hslBarChart1.SetDataSource(datas, texts);
			}
			else if (radioButton_every_day.Checked)
			{
				if (now.Day != lastDay)
				{
					lastDay = now.Day;
					SoftBasic.AddArrayData(ref datas, new int[1] { num2 }, dataCount);
					SoftBasic.AddArrayData(ref texts, new string[1] { lastDay + "D" }, dataCount);
				}
				else
				{
					datas[datas.Length - 1] += num2;
				}
				hslBarChart1.SetDataSource(datas, texts);
			}
		}

		private void Language(int language)
		{
			if (language != 1)
			{
				Text = "Mqtt Server Test";
				label3.Text = "Port:";
				button1.Text = "Start";
				button2.Text = "Close";
				button5.Text = "Publish Id";
				label7.Text = "Topic:";
				label8.Text = "";
				label9.Text = "Payload:";
				button3.Text = "Publish All";
				button_clear.Text = "Clear";
				button6.Text = "Publish";
				label12.Text = "Receive:";
				checkBox3.Text = "Send test message back when client connect";
				checkBox2.Text = "Willcards";
				checkBox_publish_timer.Text = "Timer";
				button_stop.Text = "Stop";
				checkBox_long_message_hide.Text = "Long text for partial display";
				checkBox1.Text = "Enable the username and password below:";
				label_login_name.Text = "Name:";
				label_login_password.Text = "Password:";
				radioButton_every_seconds.Text = "Seconds";
				radioButton_every_minute.Text = "Minute";
				radioButton_every_hour.Text = "Hour";
				radioButton_every_day.Text = "Day";
				checkBox_skip_zero.Text = "Skip times 0?";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				siemens = new SiemensS7Net(SiemensPLCS.S1200, "127.0.0.1");
				mqttServer = new MqttServer();
				mqttServer.OnClientApplicationMessageReceive += MqttServer_OnClientApplicationMessageReceive;
				mqttServer.OnClientConnected += MqttServer_OnClientConnected;
				mqttServer.OnClientDisConnected += MqttServer_OnClientDisConnected;
				mqttServer.TopicWildcard = checkBox2.Checked;
				if (checkBox1.Checked)
				{
					mqttServer.ClientVerification += MqttServer_ClientVerification;
				}
				mqttServer.RegisterMqttRpcApi("Account", this);
				mqttServer.RegisterMqttRpcApi("Siemens", siemens);
				mqttServer.RegisterMqttRpcApi("TimeOut", typeof(HslTimeOut));
				mqttServer.RegisterMqttRpcApi("Fanuc", new FanucSeries0i("127.0.0.1"));
				mqttServer.RegisterMqttRpcApi("PCCC", new AllenBradleyPcccNet("127.0.0.1"));
				sslServerControl1.InitializeServer(mqttServer);
				mqttServer.ServerStart(int.Parse(textBox2.Text));
				mqttServer.LogNet = new LogNetSingle("");
				mqttServer.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				mqttServer.RegisterMqttRpcApi("Log", mqttServer.LogNet);
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage("Start Success");
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Start Failed : " + ex.Message);
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			timerPublish = new System.Threading.Timer(TimerPublish, null, 0, 1000);
		}

		private void TimerPublish(object obj)
		{
			mqttServer.PublishTopicPayload("A", Encoding.UTF8.GetBytes("A" + publishTick));
			mqttServer.PublishTopicPayload("B", Encoding.UTF8.GetBytes("B" + publishTick));
			mqttServer.PublishTopicPayload("C", Encoding.UTF8.GetBytes("C" + publishTick));
			mqttServer.PublishTopicPayload("D", Encoding.UTF8.GetBytes("D" + publishTick));
			publishTick++;
		}

		private void MqttServer_OnClientConnected(MqttSession session)
		{
			if (checkBox3.Checked)
			{
				mqttServer.PublishTopicPayload(session, "HslMqtt", Encoding.UTF8.GetBytes("This is a test message"));
			}
			Invoke((Action)delegate
			{
				comboBox_session_select.DataSource = mqttServer.OnlineSessions;
			});
		}

		private void MqttServer_OnClientDisConnected(MqttSession session)
		{
			Invoke((Action)delegate
			{
				comboBox_session_select.DataSource = mqttServer.OnlineSessions;
			});
		}

		private void ComboBox_session_select_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (comboBox_session_select.SelectedItem is MqttSession mqttSession)
			{
				textBox_session_onlineTime.Text = mqttSession.OnlineTime.ToString(DemoUtils.DateTimeFormate);
				textBox_session_activeTime.Text = mqttSession.ActiveTime.ToString(DemoUtils.DateTimeFormate);
				textBox_session_ip.Text = mqttSession.EndPoint.Address.ToString();
				textBox_session_port.Text = mqttSession.EndPoint.Port.ToString();
				textBox_session_clientID.Text = mqttSession.ClientId;
				textBox_session_rsa.Text = mqttSession.IsAesCryptography.ToString();
				textBox_session_userName.Text = mqttSession.UserName;
				textBox_session_willTopic.Text = mqttSession.WillTopic;
				textBox_session_topics.Lines = mqttSession.GetTopics();
			}
		}

		private int MqttServer_ClientVerification(MqttSession mqttSession, string clientId, string userName, string passwrod)
		{
			if (userName == textBox_login_name.Text.Trim() && passwrod == textBox_login_password.Text.Trim())
			{
				return 0;
			}
			if (userName == "hsl" && passwrod == "123456")
			{
				mqttSession.DeveloperPermissions = true;
				return 0;
			}
			return 5;
		}

		private TopicSaveItem GetSavedTopicItem(MqttSession session, MqttClientApplicationMessage message)
		{
			TopicSaveItem result;
			lock (all_dicts)
			{
				if (all_dicts.ContainsKey(message.Topic))
				{
					all_dicts[message.Topic].Session = session;
					all_dicts[message.Topic].Message = message;
					all_dicts[message.Topic].ReceiveDateTime = DateTime.Now;
					all_dicts[message.Topic].Count++;
					result = all_dicts[message.Topic];
				}
				else
				{
					TopicSaveItem topicSaveItem = new TopicSaveItem();
					topicSaveItem.Topic = message.Topic;
					topicSaveItem.Message = message;
					topicSaveItem.ReceiveDateTime = DateTime.Now;
					topicSaveItem.Session = session;
					topicSaveItem.Count = 1L;
					result = topicSaveItem;
					all_dicts.Add(message.Topic, topicSaveItem);
				}
			}
			return result;
		}

		private void DeleteTopicItem(TopicSaveItem item)
		{
			lock (all_dicts)
			{
				if (all_dicts.ContainsKey(item.Topic))
				{
					all_dicts.Remove(item.Topic);
				}
			}
		}

		private void MqttServer_OnClientApplicationMessageReceive(MqttSession session, MqttClientApplicationMessage message)
		{
			if (message.Topic == "ndiwh是本地AIHDniwd")
			{
				mqttServer.PublishTopicPayload(session, message.Topic, message.Payload);
			}
			if (session.Protocol == "HUSL")
			{
				if (message.Topic == "A")
				{
					mqttServer.PublishTopicPayload(session, "B", Encoding.UTF8.GetBytes("这是回传的一条测试数据"));
				}
				else if (message.Topic == "B")
				{
					Thread.Sleep(1000);
					for (int i = 0; i < 10; i++)
					{
						Thread.Sleep(1000);
						mqttServer.ReportProgress(session, ((i + 1) * 10).ToString(), $"当前正在处理{i + 1}步");
					}
					Thread.Sleep(1000);
					mqttServer.PublishTopicPayload(session, StringResources.Language.SuccessText, Encoding.UTF8.GetBytes("B操作处理成功"));
				}
				else if (message.Topic == "C")
				{
					byte[] array = new byte[1048576];
					for (int j = 0; j < array.Length; j++)
					{
						array[j] = 48;
					}
					mqttServer.PublishTopicPayload(session, "C", array);
				}
				else if (message.Topic == "D")
				{
					mqttServer.ReportOperateResult(session, (Program.Language == 1) ? "当前的功能码不支持！" : "The current function code is not supported!");
				}
				else if (message.Topic == "E")
				{
					if (random.Next(100) < 50)
					{
						mqttServer.ReportOperateResult(session, new OperateResult<string>((Program.Language == 1) ? "当前的结果为失败信息" : "The current result is a failure message"));
					}
					else
					{
						mqttServer.ReportOperateResult(session, OperateResult.CreateSuccessResult((Program.Language == 1) ? "成功" : "Successful"));
					}
				}
				else if (message.Topic == "F")
				{
					mqttServer.PublishTopicPayload(session, "list", Encoding.UTF8.GetBytes(JArray.FromObject(MqttHelper.GetSyncServicesApiInformationFromObject(this)).ToString()));
				}
				else
				{
					mqttServer.ReportOperateResult(session, (Program.Language == 1) ? "不支持的注册接口信息" : "Unsupported information about the registered interface");
				}
			}
			else if (session.Protocol == "MQTT")
			{
				TopicSaveItem currentItem = GetSavedTopicItem(session, message);
				Invoke((Action)delegate
				{
					if (currentItem != null)
					{
						if (currentItem.ViewRow != null)
						{
							currentItem.RenderRow();
						}
						else
						{
							int index = dataGridView1.Rows.Add();
							currentItem.RenderRow(dataGridView1.Rows[index]);
						}
						if (currentItem == selectedItem)
						{
							RenderTopicSaveItem(currentItem);
						}
					}
				});
			}
			Invoke((Action)delegate
			{
				receiveCount++;
				receiveSize += message.Payload.LongLength;
				if (!isStop)
				{
					textBox8.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Cliend Id[" + message.ClientId + "] Topic:[" + message.Topic + "] Payload" + (message.Retain ? "-Retain" : "") + ":[" + GetStringFromPayload(message.Payload) + "]" + Environment.NewLine);
				}
			});
		}

		private void RenderTopicSaveItem(TopicSaveItem item)
		{
			if (item != null)
			{
				textBox_topic_topic.Text = item.Topic;
				checkBox_topic_retain.Checked = item.Message.Retain;
				textBox_topic_publishTime.Text = item.ReceiveDateTime.ToString(DemoUtils.DateTimeFormate);
				if (item.Session != null)
				{
					textBox_topic_publishSession.Text = item.Session.ToString();
				}
				else
				{
					textBox_topic_publishSession.Text = string.Empty;
				}
				string json = GetStringFromPayload(item.Message.Payload);
				if (radioButton_topic_render_json.Checked)
				{
					try
					{
						json = JObject.Parse(json).ToString();
					}
					catch
					{
					}
				}
				textBox_topic_payload.Text = json;
				if (item.Message.Payload != null)
				{
					label_topic_size.Text = SoftBasic.GetSizeDescription(item.Message.Payload.LongLength);
				}
				else
				{
					label_topic_size.Text = "0";
				}
			}
			else
			{
				textBox_topic_topic.Text = string.Empty;
				checkBox_topic_retain.Checked = false;
				textBox_topic_publishTime.Text = string.Empty;
				textBox_topic_publishSession.Text = string.Empty;
				textBox_topic_payload.Text = string.Empty;
				label_topic_size.Text = "0";
			}
		}

		private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0 && dataGridView1.SelectedRows[0].Tag is TopicSaveItem item)
			{
				RenderTopicSaveItem(item);
				selectedItem = item;
			}
		}

		private string GetStringFromPayload(byte[] payload)
		{
			string text = string.Empty;
			if (payload == null)
			{
				payload = new byte[0];
			}
			if (radioButton_recv_Hex.Checked)
			{
				text = payload.ToHexString(' ');
			}
			else if (radioButton_recv_ASCII.Checked)
			{
				text = Encoding.ASCII.GetString(payload);
			}
			else if (radioButton_recv_Unicode.Checked)
			{
				text = Encoding.Unicode.GetString(payload);
			}
			else if (radioButton_recv_UTF8.Checked)
			{
				text = Encoding.UTF8.GetString(payload);
			}
			else if (radioButton_recv_gb2312.Checked)
			{
				text = Encoding.GetEncoding("gb2312").GetString(payload);
			}
			if (text != null && text.Length > 100 && checkBox_long_message_hide.Checked)
			{
				text = text.Substring(0, 100) + "...";
			}
			return text;
		}

		private void button7_Click_1(object sender, EventArgs e)
		{
			if (selectedItem != null && !string.IsNullOrEmpty(textBox_topic_topic.Text))
			{
				dataGridView1.Rows.Remove(selectedItem.ViewRow);
				DeleteTopicItem(selectedItem);
				selectedItem = null;
				RenderTopicSaveItem(null);
			}
		}

		[HslMqttApi("检查账户的信息")]
		[HslMqttPermission(ClientID = "AAA")]
		public OperateResult<string> CheckName(string name, short value)
		{
			if (value < 10)
			{
				return new OperateResult<string>("值不能小于10");
			}
			return OperateResult.CreateSuccessResult("成功:" + name + " 年龄:" + value);
		}

		[HslMqttApi("读取设备的信息，\r\naddress: 设备的地址 \r\nlength: 读取的数据长度")]
		public OperateResult<string> ReadInt(string address, short length)
		{
			return OperateResult.CreateSuccessResult("成功:" + address);
		}

		[HslMqttApi("读取设备的信息，address: 设备的地址 length: 读取的数据长度")]
		public OperateResult<int, string> ReadABC(string address)
		{
			return OperateResult.CreateSuccessResult(random.Next(1000), "成功:" + address);
		}

		[HslMqttApi("读取设备的Int16信息，address: 设备的地址 length: 读取的数据长度")]
		public OperateResult<short> ReadInt16(string address = "100", short length = 10)
		{
			return OperateResult.CreateSuccessResult((short)random.Next(10000));
		}

		[HslMqttApi("获取两个数的除法信息，a: 第一个数 b: 第二个数")]
		public OperateResult<int> Devide(int a = 200, int b = 10)
		{
			return OperateResult.CreateSuccessResult(a / b);
		}

		[HslMqttApi("读取设备的Int16数组信息，address: 设备的地址 length: 读取的数据长度")]
		public OperateResult<short[]> ReadInt16Array(string address, short length)
		{
			short[] array = new short[10];
			for (int i = 0; i < 10; i++)
			{
				array[i] = (short)random.Next(10000);
			}
			return OperateResult.CreateSuccessResult(array);
		}

		[HslMqttApi("异步的读取方法，需要传入字符串的值")]
		public async Task<short> ReadDatabaseAsync(string abc = "123")
		{
			await Task.Delay(1000);
			return await Task.FromResult(short.Parse(abc));
		}

		[HslMqttApi("异步的读取方法，需要传入字符串的值")]
		public Task WriteDatabaseAsync(string abc = "123")
		{
			return Task.Delay(500);
		}

		[HslMqttApi("读取设备的Int16信息，address: 设备的地址 length: 读取的数据长度")]
		public short ReadFloat(ISessionContext context, string address, short length = 12345)
		{
			if (context.UserName != "hsl")
			{
				return -100;
			}
			return (short)random.Next(10000);
		}

		[HslMqttApi("读取设备的学生信息\r\naddress: 设备的地址 length: 读取的数据长度")]
		public Student ReadStudent(string address = "M100", short length = 10)
		{
			return new Student
			{
				Name = "张三",
				Age = 23,
				ID = "1012312321"
			};
		}

		[HslMqttApi("读取设备的学生信息\r\naddress: 设备的地址 length: 读取的数据长度")]
		public OperateResult<Student> ReadStudentResult(string address, short length)
		{
			if (random.Next(1000) < 500)
			{
				return OperateResult.CreateSuccessResult(new Student
				{
					Name = "张三",
					Age = 23,
					ID = "1012312321"
				});
			}
			return new OperateResult<Student>("读取失败");
		}

		[HslMqttApi(Description = "写入设备的多个学生信息\r\naddress: 设备的地址 length: 读取的数据长度")]
		public OperateResult<string> WriteMultiStudentResult(string address, short length, Student[] student)
		{
			if (random.Next(1000) < 500)
			{
				if (student == null)
				{
					return new OperateResult<string>("student is null");
				}
				for (int i = 0; i < student.Length; i++)
				{
				}
				return OperateResult.CreateSuccessResult($"学生信息写入成功，数组数量：{student.Length} ID列表:{student.Select((Student m) => m.ID).ToArray().ToArrayString()}");
			}
			return new OperateResult<string>("写入失败");
		}

		[HslMqttApi(Description = "写入设备的学生信息\r\naddress: 设备的地址 length: 读取的数据长度")]
		public OperateResult<string> WriteStudentResult(string address, short length, Student student)
		{
			if (random.Next(1000) < 500)
			{
				if (student == null)
				{
					return new OperateResult<string>("student is null");
				}
				return OperateResult.CreateSuccessResult("学生信息写入成功：ID:" + student.ID + " Name:" + student.Name);
			}
			return new OperateResult<string>("写入失败");
		}

		[HslMqttApi("启动设备的接口信息")]
		public OperateResult<string> StartDevice(DateTime start)
		{
			return OperateResult.CreateSuccessResult(start.ToString());
		}

		[HslMqttApi("StopDevice", "关闭设备的接口信息")]
		public OperateResult<int> asdgasasdasd()
		{
			return OperateResult.CreateSuccessResult(random.Next(10000));
		}

		[HslMqttApi(HttpMethod = "POST")]
		public string GetJObjectTest(JObject json)
		{
			return json.ToString();
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
			mqttServer?.ServerClose();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			mqttServer.PublishAllClientTopicPayload(textBox5.Text, checkBox_publish_isHex.Checked ? textBox4.Text.ToHexBytes() : Encoding.UTF8.GetBytes(textBox4.Text), checkBox_retain.Checked);
			button_publish = button3;
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
			receiveCount = 0L;
		}

		private void Button5_Click(object sender, EventArgs e)
		{
			mqttServer.PublishTopicPayload(textBox_publish_clientID.Text, textBox5.Text, checkBox_publish_isHex.Checked ? textBox4.Text.ToHexBytes() : Encoding.UTF8.GetBytes(textBox4.Text), checkBox_retain.Checked);
			button_publish = button5;
		}

		private void button6_Click_1(object sender, EventArgs e)
		{
			mqttServer.PublishTopicPayload(textBox5.Text, checkBox_publish_isHex.Checked ? textBox4.Text.ToHexBytes() : Encoding.UTF8.GetBytes(textBox4.Text), checkBox_retain.Checked);
			button_publish = button6;
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (!isStop)
			{
				button_stop.Text = "继续";
				isStop = true;
			}
			else
			{
				isStop = false;
				button_stop.Text = "暂停";
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlRetureMessage, checkBox3.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlRetureMessage).Value);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button4_Click_1(object sender, EventArgs e)
		{
			if (comboBox_session_select.SelectedItem is MqttSession session)
			{
				mqttServer.RemoveAndCloseSession(session, "");
			}
		}

		private void button_statistics_start_Click(object sender, EventArgs e)
		{
		}

		private void button_publish_test_Click(object sender, EventArgs e)
		{
			textBox5.Text = "HSL:MQTT:TEST:10000";
			for (int i = 0; i < 10000; i++)
			{
				mqttServer.PublishTopicPayload("HSL:MQTT:TEST:10000", BitConverter.GetBytes(i));
			}
			MessageBox.Show("Publish finish");
		}

		private void FormMqttServer_FormClosing(object sender, FormClosingEventArgs e)
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.panel1 = new System.Windows.Forms.Panel();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.textBox_login_password = new System.Windows.Forms.TextBox();
			this.label_login_password = new System.Windows.Forms.Label();
			this.textBox_login_name = new System.Windows.Forms.TextBox();
			this.label_login_name = new System.Windows.Forms.Label();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label_receive_size = new System.Windows.Forms.Label();
			this.label18 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.checkBox_publish_timer = new System.Windows.Forms.CheckBox();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.button_publish_test = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.button_stop = new System.Windows.Forms.Button();
			this.button_clear = new System.Windows.Forms.Button();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox_session_activeTime = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox_session_willTopic = new System.Windows.Forms.TextBox();
			this.label17 = new System.Windows.Forms.Label();
			this.textBox_session_userName = new System.Windows.Forms.TextBox();
			this.label16 = new System.Windows.Forms.Label();
			this.textBox_session_topics = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_session_rsa = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox_session_clientID = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox_session_port = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_session_ip = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_session_onlineTime = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.comboBox_session_select = new System.Windows.Forms.ComboBox();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.label_topic_size = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.checkBox_topic_retain = new System.Windows.Forms.CheckBox();
			this.panel6 = new System.Windows.Forms.Panel();
			this.radioButton_topic_render_json = new System.Windows.Forms.RadioButton();
			this.radioButton_topic_render_text = new System.Windows.Forms.RadioButton();
			this.label24 = new System.Windows.Forms.Label();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox_topic_topic = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_topic_payload = new System.Windows.Forms.TextBox();
			this.label22 = new System.Windows.Forms.Label();
			this.textBox_topic_publishSession = new System.Windows.Forms.TextBox();
			this.label20 = new System.Windows.Forms.Label();
			this.textBox_topic_publishTime = new System.Windows.Forms.TextBox();
			this.label19 = new System.Windows.Forms.Label();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.panel4 = new System.Windows.Forms.Panel();
			this.hslBarChart1 = new HslControls.HslBarChart();
			this.panel5 = new System.Windows.Forms.Panel();
			this.checkBox_skip_zero = new System.Windows.Forms.CheckBox();
			this.radioButton_every_day = new System.Windows.Forms.RadioButton();
			this.radioButton_every_hour = new System.Windows.Forms.RadioButton();
			this.radioButton_every_minute = new System.Windows.Forms.RadioButton();
			this.radioButton_every_seconds = new System.Windows.Forms.RadioButton();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_recv_gb2312 = new System.Windows.Forms.RadioButton();
			this.radioButton_recv_Unicode = new System.Windows.Forms.RadioButton();
			this.radioButton_recv_UTF8 = new System.Windows.Forms.RadioButton();
			this.radioButton_recv_ASCII = new System.Windows.Forms.RadioButton();
			this.radioButton_recv_Hex = new System.Windows.Forms.RadioButton();
			this.checkBox_publish_isHex = new System.Windows.Forms.CheckBox();
			this.checkBox_long_message_hide = new System.Windows.Forms.CheckBox();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label_receive_count = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.textBox_publish_clientID = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.checkBox_retain = new System.Windows.Forms.CheckBox();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.panel6.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.tabPage4.SuspendLayout();
			this.panel4.SuspendLayout();
			this.panel5.SuspendLayout();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.textBox_login_password);
			this.panel1.Controls.Add(this.label_login_password);
			this.panel1.Controls.Add(this.textBox_login_name);
			this.panel1.Controls.Add(this.label_login_name);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(999, 65);
			this.panel1.TabIndex = 7;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(2, 32);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(993, 30);
			this.sslServerControl1.TabIndex = 15;
			this.textBox_login_password.Location = new System.Drawing.Point(902, 7);
			this.textBox_login_password.Name = "textBox_login_password";
			this.textBox_login_password.Size = new System.Drawing.Size(83, 23);
			this.textBox_login_password.TabIndex = 14;
			this.textBox_login_password.Text = "123456";
			this.label_login_password.AutoSize = true;
			this.label_login_password.Location = new System.Drawing.Point(847, 10);
			this.label_login_password.Name = "label_login_password";
			this.label_login_password.Size = new System.Drawing.Size(44, 17);
			this.label_login_password.TabIndex = 13;
			this.label_login_password.Text = "密码：";
			this.textBox_login_name.Location = new System.Drawing.Point(740, 7);
			this.textBox_login_name.Name = "textBox_login_name";
			this.textBox_login_name.Size = new System.Drawing.Size(100, 23);
			this.textBox_login_name.TabIndex = 12;
			this.textBox_login_name.Text = "admin";
			this.label_login_name.AutoSize = true;
			this.label_login_name.Location = new System.Drawing.Point(678, 10);
			this.label_login_name.Name = "label_login_name";
			this.label_login_name.Size = new System.Drawing.Size(56, 17);
			this.label_login_name.TabIndex = 11;
			this.label_login_name.Text = "用户名：";
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(147, 9);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(87, 21);
			this.checkBox2.TabIndex = 10;
			this.checkBox2.Text = "主题通配符";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(589, 9);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(87, 21);
			this.checkBox1.TabIndex = 6;
			this.checkBox1.Text = "启用用户名";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(335, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(238, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(58, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(78, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(435, 9);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(147, 21);
			this.checkBox3.TabIndex = 9;
			this.checkBox3.Text = "是否回发一条测试数据";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label_receive_size);
			this.panel2.Controls.Add(this.label18);
			this.panel2.Controls.Add(this.textBox1);
			this.panel2.Controls.Add(this.checkBox_publish_timer);
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.checkBox_publish_isHex);
			this.panel2.Controls.Add(this.checkBox_long_message_hide);
			this.panel2.Controls.Add(this.listBox1);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.label_receive_count);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.button6);
			this.panel2.Controls.Add(this.textBox_publish_clientID);
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.checkBox_retain);
			this.panel2.Location = new System.Drawing.Point(3, 102);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(999, 539);
			this.panel2.TabIndex = 13;
			this.label_receive_size.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_receive_size.AutoSize = true;
			this.label_receive_size.Location = new System.Drawing.Point(550, 517);
			this.label_receive_size.Name = "label_receive_size";
			this.label_receive_size.Size = new System.Drawing.Size(34, 17);
			this.label_receive_size.TabIndex = 44;
			this.label_receive_size.Text = "Size:";
			this.label18.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(962, 186);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(25, 17);
			this.label18.TabIndex = 42;
			this.label18.Text = "ms";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(889, 183);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(68, 23);
			this.textBox1.TabIndex = 41;
			this.textBox1.Text = "1000";
			this.checkBox_publish_timer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_publish_timer.AutoSize = true;
			this.checkBox_publish_timer.Location = new System.Drawing.Point(807, 184);
			this.checkBox_publish_timer.Name = "checkBox_publish_timer";
			this.checkBox_publish_timer.Size = new System.Drawing.Size(75, 21);
			this.checkBox_publish_timer.TabIndex = 40;
			this.checkBox_publish_timer.Text = "定时发布";
			this.checkBox_publish_timer.UseVisualStyleBackColor = true;
			this.checkBox_publish_timer.CheckedChanged += new System.EventHandler(checkBox_publish_timer_CheckedChanged);
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Location = new System.Drawing.Point(2, 211);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(996, 302);
			this.tabControl1.TabIndex = 39;
			this.tabPage1.Controls.Add(this.button_publish_test);
			this.tabPage1.Controls.Add(this.textBox8);
			this.tabPage1.Controls.Add(this.button_stop);
			this.tabPage1.Controls.Add(this.button_clear);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(988, 272);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "LogInfo";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.button_publish_test.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button_publish_test.ForeColor = System.Drawing.Color.Silver;
			this.button_publish_test.Location = new System.Drawing.Point(901, 238);
			this.button_publish_test.Name = "button_publish_test";
			this.button_publish_test.Size = new System.Drawing.Size(81, 28);
			this.button_publish_test.TabIndex = 29;
			this.button_publish_test.Text = "发布测试";
			this.button_publish_test.UseVisualStyleBackColor = true;
			this.button_publish_test.Click += new System.EventHandler(button_publish_test_Click);
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(3, 3);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(892, 269);
			this.textBox8.TabIndex = 18;
			this.button_stop.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_stop.Location = new System.Drawing.Point(901, 6);
			this.button_stop.Name = "button_stop";
			this.button_stop.Size = new System.Drawing.Size(81, 28);
			this.button_stop.TabIndex = 28;
			this.button_stop.Text = "暂停";
			this.button_stop.UseVisualStyleBackColor = true;
			this.button_stop.Click += new System.EventHandler(button7_Click);
			this.button_clear.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_clear.Location = new System.Drawing.Point(901, 40);
			this.button_clear.Name = "button_clear";
			this.button_clear.Size = new System.Drawing.Size(81, 28);
			this.button_clear.TabIndex = 17;
			this.button_clear.Text = "清空";
			this.button_clear.UseVisualStyleBackColor = true;
			this.button_clear.Click += new System.EventHandler(button4_Click);
			this.tabPage2.Controls.Add(this.button4);
			this.tabPage2.Controls.Add(this.textBox_session_activeTime);
			this.tabPage2.Controls.Add(this.label23);
			this.tabPage2.Controls.Add(this.textBox_session_willTopic);
			this.tabPage2.Controls.Add(this.label17);
			this.tabPage2.Controls.Add(this.textBox_session_userName);
			this.tabPage2.Controls.Add(this.label16);
			this.tabPage2.Controls.Add(this.textBox_session_topics);
			this.tabPage2.Controls.Add(this.label15);
			this.tabPage2.Controls.Add(this.textBox_session_rsa);
			this.tabPage2.Controls.Add(this.label14);
			this.tabPage2.Controls.Add(this.textBox_session_clientID);
			this.tabPage2.Controls.Add(this.label13);
			this.tabPage2.Controls.Add(this.textBox_session_port);
			this.tabPage2.Controls.Add(this.label12);
			this.tabPage2.Controls.Add(this.textBox_session_ip);
			this.tabPage2.Controls.Add(this.label11);
			this.tabPage2.Controls.Add(this.textBox_session_onlineTime);
			this.tabPage2.Controls.Add(this.label10);
			this.tabPage2.Controls.Add(this.label6);
			this.tabPage2.Controls.Add(this.comboBox_session_select);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(988, 272);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Sessions";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(881, 4);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(104, 28);
			this.button4.TabIndex = 27;
			this.button4.Text = "Logoff";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click_1);
			this.textBox_session_activeTime.Location = new System.Drawing.Point(101, 67);
			this.textBox_session_activeTime.Name = "textBox_session_activeTime";
			this.textBox_session_activeTime.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_activeTime.TabIndex = 26;
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(6, 70);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(86, 17);
			this.label23.TabIndex = 25;
			this.label23.Text = "Active Time：";
			this.textBox_session_willTopic.Location = new System.Drawing.Point(101, 250);
			this.textBox_session_willTopic.Name = "textBox_session_willTopic";
			this.textBox_session_willTopic.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_willTopic.TabIndex = 24;
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(6, 253);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(73, 17);
			this.label17.TabIndex = 23;
			this.label17.Text = "WillTopic：";
			this.textBox_session_userName.Location = new System.Drawing.Point(101, 187);
			this.textBox_session_userName.Name = "textBox_session_userName";
			this.textBox_session_userName.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_userName.TabIndex = 22;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(6, 190);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(82, 17);
			this.label16.TabIndex = 21;
			this.label16.Text = "UserName：";
			this.textBox_session_topics.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_session_topics.Location = new System.Drawing.Point(498, 37);
			this.textBox_session_topics.Multiline = true;
			this.textBox_session_topics.Name = "textBox_session_topics";
			this.textBox_session_topics.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_session_topics.Size = new System.Drawing.Size(484, 230);
			this.textBox_session_topics.TabIndex = 20;
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(402, 43);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(76, 17);
			this.label15.TabIndex = 19;
			this.label15.Text = "Sub-Topics:";
			this.textBox_session_rsa.Location = new System.Drawing.Point(101, 217);
			this.textBox_session_rsa.Name = "textBox_session_rsa";
			this.textBox_session_rsa.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_rsa.TabIndex = 18;
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(6, 220);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(70, 17);
			this.label14.TabIndex = 17;
			this.label14.Text = "RSA/AES：";
			this.textBox_session_clientID.Location = new System.Drawing.Point(101, 157);
			this.textBox_session_clientID.Name = "textBox_session_clientID";
			this.textBox_session_clientID.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_clientID.TabIndex = 16;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(6, 160);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(69, 17);
			this.label13.TabIndex = 15;
			this.label13.Text = "Client ID：";
			this.textBox_session_port.Location = new System.Drawing.Point(101, 128);
			this.textBox_session_port.Name = "textBox_session_port";
			this.textBox_session_port.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_port.TabIndex = 14;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(6, 131);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(60, 17);
			this.label12.TabIndex = 13;
			this.label12.Text = "Ip Port：";
			this.textBox_session_ip.Location = new System.Drawing.Point(101, 98);
			this.textBox_session_ip.Name = "textBox_session_ip";
			this.textBox_session_ip.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_ip.TabIndex = 12;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(6, 101);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(84, 17);
			this.label11.TabIndex = 11;
			this.label11.Text = "Ip Address：";
			this.textBox_session_onlineTime.Location = new System.Drawing.Point(101, 37);
			this.textBox_session_onlineTime.Name = "textBox_session_onlineTime";
			this.textBox_session_onlineTime.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_onlineTime.TabIndex = 10;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(6, 40);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(89, 17);
			this.label10.TabIndex = 9;
			this.label10.Text = "Online Time：";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(6, 10);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(64, 17);
			this.label6.TabIndex = 8;
			this.label6.Text = "Session：";
			this.comboBox_session_select.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_session_select.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_session_select.FormattingEnabled = true;
			this.comboBox_session_select.Location = new System.Drawing.Point(103, 6);
			this.comboBox_session_select.Name = "comboBox_session_select";
			this.comboBox_session_select.Size = new System.Drawing.Size(773, 25);
			this.comboBox_session_select.TabIndex = 0;
			this.tabPage3.Controls.Add(this.label_topic_size);
			this.tabPage3.Controls.Add(this.label21);
			this.tabPage3.Controls.Add(this.checkBox_topic_retain);
			this.tabPage3.Controls.Add(this.panel6);
			this.tabPage3.Controls.Add(this.label24);
			this.tabPage3.Controls.Add(this.button7);
			this.tabPage3.Controls.Add(this.textBox_topic_topic);
			this.tabPage3.Controls.Add(this.label4);
			this.tabPage3.Controls.Add(this.textBox_topic_payload);
			this.tabPage3.Controls.Add(this.label22);
			this.tabPage3.Controls.Add(this.textBox_topic_publishSession);
			this.tabPage3.Controls.Add(this.label20);
			this.tabPage3.Controls.Add(this.textBox_topic_publishTime);
			this.tabPage3.Controls.Add(this.label19);
			this.tabPage3.Controls.Add(this.dataGridView1);
			this.tabPage3.Location = new System.Drawing.Point(4, 26);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(988, 272);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Topics";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.label_topic_size.AutoSize = true;
			this.label_topic_size.ForeColor = System.Drawing.Color.Gray;
			this.label_topic_size.Location = new System.Drawing.Point(452, 113);
			this.label_topic_size.Name = "label_topic_size";
			this.label_topic_size.Size = new System.Drawing.Size(27, 17);
			this.label_topic_size.TabIndex = 26;
			this.label_topic_size.Text = "0 B";
			this.label21.AutoSize = true;
			this.label21.ForeColor = System.Drawing.Color.Gray;
			this.label21.Location = new System.Drawing.Point(452, 93);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(43, 17);
			this.label21.TabIndex = 25;
			this.label21.Text = "Size：";
			this.checkBox_topic_retain.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_topic_retain.AutoSize = true;
			this.checkBox_topic_retain.Location = new System.Drawing.Point(910, 33);
			this.checkBox_topic_retain.Name = "checkBox_topic_retain";
			this.checkBox_topic_retain.Size = new System.Drawing.Size(63, 21);
			this.checkBox_topic_retain.TabIndex = 24;
			this.checkBox_topic_retain.Text = "Retain";
			this.checkBox_topic_retain.UseVisualStyleBackColor = true;
			this.panel6.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.panel6.Controls.Add(this.radioButton_topic_render_json);
			this.panel6.Controls.Add(this.radioButton_topic_render_text);
			this.panel6.Location = new System.Drawing.Point(779, 243);
			this.panel6.Name = "panel6";
			this.panel6.Size = new System.Drawing.Size(200, 28);
			this.panel6.TabIndex = 23;
			this.radioButton_topic_render_json.AutoSize = true;
			this.radioButton_topic_render_json.Location = new System.Drawing.Point(68, 3);
			this.radioButton_topic_render_json.Name = "radioButton_topic_render_json";
			this.radioButton_topic_render_json.Size = new System.Drawing.Size(52, 21);
			this.radioButton_topic_render_json.TabIndex = 2;
			this.radioButton_topic_render_json.Text = "Json";
			this.radioButton_topic_render_json.UseVisualStyleBackColor = true;
			this.radioButton_topic_render_text.AutoSize = true;
			this.radioButton_topic_render_text.Checked = true;
			this.radioButton_topic_render_text.Location = new System.Drawing.Point(3, 3);
			this.radioButton_topic_render_text.Name = "radioButton_topic_render_text";
			this.radioButton_topic_render_text.Size = new System.Drawing.Size(50, 21);
			this.radioButton_topic_render_text.TabIndex = 0;
			this.radioButton_topic_render_text.TabStop = true;
			this.radioButton_topic_render_text.Text = "Text";
			this.radioButton_topic_render_text.UseVisualStyleBackColor = true;
			this.label24.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(722, 248);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(61, 17);
			this.label24.TabIndex = 22;
			this.label24.Text = "Format：";
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(909, 0);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(73, 28);
			this.button7.TabIndex = 21;
			this.button7.Text = "Delete";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click_1);
			this.textBox_topic_topic.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_topic_topic.Location = new System.Drawing.Point(565, 3);
			this.textBox_topic_topic.Name = "textBox_topic_topic";
			this.textBox_topic_topic.Size = new System.Drawing.Size(338, 23);
			this.textBox_topic_topic.TabIndex = 20;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(452, 5);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(52, 17);
			this.label4.TabIndex = 19;
			this.label4.Text = "Topic：";
			this.textBox_topic_payload.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_topic_payload.Location = new System.Drawing.Point(520, 59);
			this.textBox_topic_payload.Multiline = true;
			this.textBox_topic_payload.Name = "textBox_topic_payload";
			this.textBox_topic_payload.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_topic_payload.Size = new System.Drawing.Size(464, 182);
			this.textBox_topic_payload.TabIndex = 18;
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(452, 62);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(66, 17);
			this.label22.TabIndex = 17;
			this.label22.Text = "Payload：";
			this.textBox_topic_publishSession.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_topic_publishSession.Location = new System.Drawing.Point(565, 30);
			this.textBox_topic_publishSession.Name = "textBox_topic_publishSession";
			this.textBox_topic_publishSession.Size = new System.Drawing.Size(338, 23);
			this.textBox_topic_publishSession.TabIndex = 14;
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(452, 33);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(109, 17);
			this.label20.TabIndex = 13;
			this.label20.Text = "Publish Session：";
			this.textBox_topic_publishTime.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox_topic_publishTime.Location = new System.Drawing.Point(562, 245);
			this.textBox_topic_publishTime.Name = "textBox_topic_publishTime";
			this.textBox_topic_publishTime.Size = new System.Drawing.Size(153, 23);
			this.textBox_topic_publishTime.TabIndex = 12;
			this.label19.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(518, 248);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(48, 17);
			this.label19.TabIndex = 11;
			this.label19.Text = "Time：";
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(223, 223, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column3, this.Column1, this.Column2);
			this.dataGridView1.Location = new System.Drawing.Point(4, 3);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(442, 270);
			this.dataGridView1.TabIndex = 0;
			this.dataGridView1.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView1_CellMouseDoubleClick);
			this.Column3.HeaderText = "ID";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.Width = 60;
			this.Column1.HeaderText = "Topic";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 260;
			this.Column2.HeaderText = "Count";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.tabPage4.Controls.Add(this.panel4);
			this.tabPage4.Location = new System.Drawing.Point(4, 26);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(988, 272);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "Statistics";
			this.tabPage4.UseVisualStyleBackColor = true;
			this.panel4.Controls.Add(this.hslBarChart1);
			this.panel4.Controls.Add(this.panel5);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(3, 3);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(982, 266);
			this.panel4.TabIndex = 0;
			this.hslBarChart1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslBarChart1.BarBackColor = System.Drawing.Color.SlateBlue;
			this.hslBarChart1.Location = new System.Drawing.Point(0, 38);
			this.hslBarChart1.Name = "hslBarChart1";
			this.hslBarChart1.ShowBarValueFormat = "{0}";
			this.hslBarChart1.Size = new System.Drawing.Size(978, 225);
			this.hslBarChart1.TabIndex = 1;
			this.hslBarChart1.Text = "hslBarChart1";
			this.hslBarChart1.UseGradient = true;
			this.panel5.Controls.Add(this.checkBox_skip_zero);
			this.panel5.Controls.Add(this.radioButton_every_day);
			this.panel5.Controls.Add(this.radioButton_every_hour);
			this.panel5.Controls.Add(this.radioButton_every_minute);
			this.panel5.Controls.Add(this.radioButton_every_seconds);
			this.panel5.Location = new System.Drawing.Point(1, 1);
			this.panel5.Name = "panel5";
			this.panel5.Size = new System.Drawing.Size(958, 35);
			this.panel5.TabIndex = 0;
			this.checkBox_skip_zero.AutoSize = true;
			this.checkBox_skip_zero.Checked = true;
			this.checkBox_skip_zero.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_skip_zero.Location = new System.Drawing.Point(78, 8);
			this.checkBox_skip_zero.Name = "checkBox_skip_zero";
			this.checkBox_skip_zero.Size = new System.Drawing.Size(96, 21);
			this.checkBox_skip_zero.TabIndex = 8;
			this.checkBox_skip_zero.Text = "跳过次数 0 ?";
			this.checkBox_skip_zero.UseVisualStyleBackColor = true;
			this.radioButton_every_day.AutoSize = true;
			this.radioButton_every_day.Location = new System.Drawing.Point(374, 7);
			this.radioButton_every_day.Name = "radioButton_every_day";
			this.radioButton_every_day.Size = new System.Drawing.Size(50, 21);
			this.radioButton_every_day.TabIndex = 7;
			this.radioButton_every_day.Text = "每天";
			this.radioButton_every_day.UseVisualStyleBackColor = true;
			this.radioButton_every_hour.AutoSize = true;
			this.radioButton_every_hour.Location = new System.Drawing.Point(284, 7);
			this.radioButton_every_hour.Name = "radioButton_every_hour";
			this.radioButton_every_hour.Size = new System.Drawing.Size(62, 21);
			this.radioButton_every_hour.TabIndex = 3;
			this.radioButton_every_hour.Text = "每小时";
			this.radioButton_every_hour.UseVisualStyleBackColor = true;
			this.radioButton_every_minute.AutoSize = true;
			this.radioButton_every_minute.Checked = true;
			this.radioButton_every_minute.Location = new System.Drawing.Point(191, 7);
			this.radioButton_every_minute.Name = "radioButton_every_minute";
			this.radioButton_every_minute.Size = new System.Drawing.Size(62, 21);
			this.radioButton_every_minute.TabIndex = 1;
			this.radioButton_every_minute.TabStop = true;
			this.radioButton_every_minute.Text = "每分钟";
			this.radioButton_every_minute.UseVisualStyleBackColor = true;
			this.radioButton_every_seconds.AutoSize = true;
			this.radioButton_every_seconds.Location = new System.Drawing.Point(5, 7);
			this.radioButton_every_seconds.Name = "radioButton_every_seconds";
			this.radioButton_every_seconds.Size = new System.Drawing.Size(50, 21);
			this.radioButton_every_seconds.TabIndex = 0;
			this.radioButton_every_seconds.Text = "每秒";
			this.radioButton_every_seconds.UseVisualStyleBackColor = true;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.panel3.Controls.Add(this.radioButton_recv_gb2312);
			this.panel3.Controls.Add(this.radioButton_recv_Unicode);
			this.panel3.Controls.Add(this.radioButton_recv_UTF8);
			this.panel3.Controls.Add(this.radioButton_recv_ASCII);
			this.panel3.Controls.Add(this.radioButton_recv_Hex);
			this.panel3.Location = new System.Drawing.Point(5, 513);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(343, 24);
			this.panel3.TabIndex = 38;
			this.radioButton_recv_gb2312.AutoSize = true;
			this.radioButton_recv_gb2312.Location = new System.Drawing.Point(264, 2);
			this.radioButton_recv_gb2312.Name = "radioButton_recv_gb2312";
			this.radioButton_recv_gb2312.Size = new System.Drawing.Size(71, 21);
			this.radioButton_recv_gb2312.TabIndex = 4;
			this.radioButton_recv_gb2312.TabStop = true;
			this.radioButton_recv_gb2312.Text = "GB2312";
			this.radioButton_recv_gb2312.UseVisualStyleBackColor = true;
			this.radioButton_recv_Unicode.AutoSize = true;
			this.radioButton_recv_Unicode.Location = new System.Drawing.Point(184, 2);
			this.radioButton_recv_Unicode.Name = "radioButton_recv_Unicode";
			this.radioButton_recv_Unicode.Size = new System.Drawing.Size(74, 21);
			this.radioButton_recv_Unicode.TabIndex = 3;
			this.radioButton_recv_Unicode.TabStop = true;
			this.radioButton_recv_Unicode.Text = "Unicode";
			this.radioButton_recv_Unicode.UseVisualStyleBackColor = true;
			this.radioButton_recv_UTF8.AutoSize = true;
			this.radioButton_recv_UTF8.Checked = true;
			this.radioButton_recv_UTF8.Location = new System.Drawing.Point(123, 2);
			this.radioButton_recv_UTF8.Name = "radioButton_recv_UTF8";
			this.radioButton_recv_UTF8.Size = new System.Drawing.Size(55, 21);
			this.radioButton_recv_UTF8.TabIndex = 2;
			this.radioButton_recv_UTF8.TabStop = true;
			this.radioButton_recv_UTF8.Text = "UTF8";
			this.radioButton_recv_UTF8.UseVisualStyleBackColor = true;
			this.radioButton_recv_ASCII.AutoSize = true;
			this.radioButton_recv_ASCII.Location = new System.Drawing.Point(60, 2);
			this.radioButton_recv_ASCII.Name = "radioButton_recv_ASCII";
			this.radioButton_recv_ASCII.Size = new System.Drawing.Size(57, 21);
			this.radioButton_recv_ASCII.TabIndex = 1;
			this.radioButton_recv_ASCII.TabStop = true;
			this.radioButton_recv_ASCII.Text = "ASCII";
			this.radioButton_recv_ASCII.UseVisualStyleBackColor = true;
			this.radioButton_recv_Hex.AutoSize = true;
			this.radioButton_recv_Hex.Location = new System.Drawing.Point(6, 2);
			this.radioButton_recv_Hex.Name = "radioButton_recv_Hex";
			this.radioButton_recv_Hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_recv_Hex.TabIndex = 0;
			this.radioButton_recv_Hex.TabStop = true;
			this.radioButton_recv_Hex.Text = "Hex";
			this.radioButton_recv_Hex.UseVisualStyleBackColor = true;
			this.checkBox_publish_isHex.AutoSize = true;
			this.checkBox_publish_isHex.Location = new System.Drawing.Point(5, 62);
			this.checkBox_publish_isHex.Name = "checkBox_publish_isHex";
			this.checkBox_publish_isHex.Size = new System.Drawing.Size(55, 21);
			this.checkBox_publish_isHex.TabIndex = 36;
			this.checkBox_publish_isHex.Text = "Hex?";
			this.checkBox_publish_isHex.UseVisualStyleBackColor = true;
			this.checkBox_long_message_hide.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_long_message_hide.AutoSize = true;
			this.checkBox_long_message_hide.Checked = true;
			this.checkBox_long_message_hide.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_long_message_hide.Location = new System.Drawing.Point(694, 516);
			this.checkBox_long_message_hide.Name = "checkBox_long_message_hide";
			this.checkBox_long_message_hide.Size = new System.Drawing.Size(99, 21);
			this.checkBox_long_message_hide.TabIndex = 33;
			this.checkBox_long_message_hide.Text = "超长消息简略";
			this.checkBox_long_message_hide.UseVisualStyleBackColor = true;
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.BackColor = System.Drawing.Color.LightGray;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(579, 36);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(413, 140);
			this.listBox1.TabIndex = 31;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(579, 10);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(93, 17);
			this.label5.TabIndex = 30;
			this.label5.Text = "Online Client：";
			this.label_receive_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_receive_count.AutoSize = true;
			this.label_receive_count.Location = new System.Drawing.Point(379, 517);
			this.label_receive_count.Name = "label_receive_count";
			this.label_receive_count.Size = new System.Drawing.Size(93, 17);
			this.label_receive_count.TabIndex = 29;
			this.label_receive_count.Text = "Receive Count:";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(882, 11);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(86, 17);
			this.label2.TabIndex = 27;
			this.label2.Text = "Online Count:";
			this.button6.Location = new System.Drawing.Point(171, 180);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(91, 28);
			this.button6.TabIndex = 26;
			this.button6.Text = "发布订阅";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click_1);
			this.textBox_publish_clientID.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_publish_clientID.Location = new System.Drawing.Point(444, 182);
			this.textBox_publish_clientID.Name = "textBox_publish_clientID";
			this.textBox_publish_clientID.Size = new System.Drawing.Size(345, 23);
			this.textBox_publish_clientID.TabIndex = 25;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(370, 186);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 24;
			this.label1.Text = "Client Id：";
			this.button5.Location = new System.Drawing.Point(269, 180);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(96, 28);
			this.button5.TabIndex = 20;
			this.button5.Text = "广播指定id";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(Button5_Click);
			this.button3.Location = new System.Drawing.Point(75, 180);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "广播所有";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(511, 138);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(3, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(66, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "Payload：";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label8.Location = new System.Drawing.Point(523, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(40, 17);
			this.label8.TabIndex = 10;
			this.label8.Text = "(主题)";
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(455, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(2, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(52, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "Topic：";
			this.checkBox_retain.AutoSize = true;
			this.checkBox_retain.Checked = true;
			this.checkBox_retain.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_retain.Location = new System.Drawing.Point(2, 183);
			this.checkBox_retain.Name = "checkBox_retain";
			this.checkBox_retain.Size = new System.Drawing.Size(69, 21);
			this.checkBox_retain.TabIndex = 43;
			this.checkBox_retain.Text = "Retain?";
			this.checkBox_retain.UseVisualStyleBackColor = true;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/12312952.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MQTT v3.1.1";
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
			base.Name = "FormMqttServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "MQTT 服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttServer_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			this.panel6.ResumeLayout(false);
			this.panel6.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.tabPage4.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			this.panel5.ResumeLayout(false);
			this.panel5.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
