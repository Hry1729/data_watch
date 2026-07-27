using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.HslDebug;

namespace HslCommunicationDemo
{
	public class FormSerialDebug : HslFormContent
	{
		private byte[] dtuBuffer = new byte[2048];

		private PipeTcpNet pipeDtu;

		private RemoteConnectInfo dtuRemoteConnect = null;

		private SocketDebugSession socketDebugSession;

		private MqttClient mqttClient;

		private IContainer components = null;

		private Panel panel1;

		private ComboBox comboBox_Parity;

		private Label label24;

		private TextBox textBox_StopBit;

		private Label label23;

		private TextBox textBox_DataBits;

		private Label label22;

		private Button button2;

		private Button button1;

		private TextBox textBox_BaudRate;

		private Label label3;

		private Label label1;

		private Panel panel2;

		private ComboBox comboBox_portName;

		private UserControlHead userControlHead1;

		private CheckBox checkBox5;

		private Panel panel3;

		private TextBox textBox_mqtt_topic;

		private Label label2;

		private TextBox textBox_mqtt_port;

		private Label label4;

		private TextBox textBox_mqtt_ip;

		private Label label5;

		private CheckBox checkBox_useMqtt;

		private TextBox textBox_mqtt_password;

		private Label label8;

		private TextBox textBox_mqtt_name;

		private Label label9;

		private DebugControl debugControl1;

		private CheckBox checkBox_until_empty;

		private CheckBox checkBox1;

		public FormSerialDebug()
		{
			InitializeComponent();
		}

		private void FormSerialDebug_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox_Parity.SelectedIndex = 0;
			comboBox_portName.DataSource = SerialPort.GetPortNames();
			try
			{
				comboBox_portName.SelectedIndex = 0;
			}
			catch
			{
				comboBox_portName.Text = "COM3";
			}
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "串口调试助手";
				label1.Text = "Com口：";
				label3.Text = "波特率:";
				label22.Text = "数据位:";
				label23.Text = "停止位:";
				label24.Text = "奇偶：";
				button1.Text = "打开串口";
				button2.Text = "关闭串口";
				comboBox_Parity.DataSource = new string[3] { "无", "奇", "偶" };
			}
			else
			{
				Text = "Serial Debug Tools";
				label1.Text = "Com:";
				label3.Text = "Baud rate:";
				label22.Text = "Data bits:";
				label23.Text = "Stop bits:";
				label24.Text = "parity:";
				button1.Text = "Open";
				button2.Text = "Close";
				label8.Text = "Pwd:";
				label2.Text = "Topic:";
				label9.Text = "Name:";
				checkBox_useMqtt.Text = "Use MQTT";
				comboBox_Parity.DataSource = new string[3] { "None", "Odd", "Even" };
				checkBox1.Text = "Go to Dtu?";
			}
		}

		private void FormSerialDebug_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_BaudRate.Text, out var result))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "波特率输入错误！" : "Baud rate input error");
				return;
			}
			if (!int.TryParse(textBox_DataBits.Text, out var result2))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "数据位输入错误！" : "Data bits input error");
				return;
			}
			if (!int.TryParse(textBox_StopBit.Text, out var result3))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "停止位输入错误！" : "Stop bits input error");
				return;
			}
			if (checkBox_useMqtt.Checked)
			{
				mqttClient?.ConnectClose();
				mqttClient = new MqttClient(new MqttConnectionOptions
				{
					IpAddress = textBox_mqtt_ip.Text,
					Port = int.Parse(textBox_mqtt_port.Text),
					Credentials = new MqttCredential(textBox_mqtt_name.Text, textBox_mqtt_password.Text),
					ConnectTimeout = 3000
				});
				if (!mqttClient.ConnectServer().IsSuccess)
				{
					DemoUtils.ShowMessage("MQTT Connected failed, not use it");
					mqttClient = null;
				}
			}
			SerialPort serialPort = new SerialPort();
			serialPort.PortName = comboBox_portName.Text;
			serialPort.BaudRate = result;
			serialPort.DataBits = result2;
			int stopBits;
			switch (result3)
			{
			default:
				stopBits = 2;
				break;
			case 1:
				stopBits = 1;
				break;
			case 0:
				stopBits = 0;
				break;
			}
			serialPort.StopBits = (StopBits)stopBits;
			serialPort.Parity = ((comboBox_Parity.SelectedIndex != 0) ? ((comboBox_Parity.SelectedIndex == 1) ? Parity.Odd : Parity.Even) : Parity.None);
			serialPort.RtsEnable = checkBox5.Checked;
			try
			{
				serialPort.DataReceived += SP_ReadData_DataReceived;
				serialPort.Open();
				socketDebugSession = new SocketDebugSession(serialPort, serialPort.PortName);
				debugControl1.AddSessions(socketDebugSession);
				if (checkBox1.Checked)
				{
					ThreadPool.QueueUserWorkItem(ConnectDtuIp, dtuRemoteConnect);
				}
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void SP_ReadData_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			List<byte> buffer = new List<byte>();
			byte[] array = new byte[1024];
			if (checkBox_until_empty.Checked)
			{
				while (true)
				{
					Thread.Sleep(20);
					if (socketDebugSession.SerialPort.BytesToRead < 1)
					{
						break;
					}
					int length = socketDebugSession.SerialPort.Read(array, 0, Math.Min(socketDebugSession.SerialPort.BytesToRead, array.Length));
					buffer.AddRange(array.SelectBegin(length));
				}
			}
			else
			{
				int num = socketDebugSession.SerialPort.Read(array, 0, array.Length);
				if (num > 0)
				{
					buffer.AddRange(array.SelectBegin(num));
				}
			}
			if (buffer.Count != 0)
			{
				mqttClient?.PublishMessage(new MqttApplicationMessage
				{
					Topic = textBox_mqtt_topic.Text,
					Payload = buffer.ToArray()
				});
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(socketDebugSession, 0, buffer.ToArray());
				});
				if (checkBox1.Checked && pipeDtu != null && dtuRemoteConnect != null)
				{
					array = buffer.ToArray();
					pipeDtu?.Send(array);
					RenderDtuData(dtuRemoteConnect, pipeDtu, null, 4, array, string.Empty, $"Send DTU {dtuRemoteConnect.EndPoint}");
				}
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				debugControl1.RemoveSessions(socketDebugSession);
				button2.Enabled = false;
				button1.Enabled = true;
				panel2.Enabled = false;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlBaudRate, textBox_BaudRate.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataBits, textBox_DataBits.Text);
			element.SetAttributeValue(DemoDeviceList.XmlStopBit, textBox_StopBit.Text);
			element.SetAttributeValue(DemoDeviceList.XmlParity, comboBox_Parity.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox_portName.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlRtsEnable, checkBox5.Checked);
			element.SetAttributeValue("UseMqtt", checkBox_useMqtt.Checked);
			element.SetAttributeValue("MqttIP", textBox_mqtt_ip.Text);
			element.SetAttributeValue("MqttPort", textBox_mqtt_port.Text);
			element.SetAttributeValue("MqttName", textBox_mqtt_name.Text);
			element.SetAttributeValue("MqttPwd", textBox_mqtt_password.Text);
			element.SetAttributeValue("MqttTopic", textBox_mqtt_topic.Text);
			element.RemoveNodes();
			debugControl1.SaveXml(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_BaudRate.Text = element.Attribute(DemoDeviceList.XmlBaudRate).Value;
			textBox_DataBits.Text = element.Attribute(DemoDeviceList.XmlDataBits).Value;
			textBox_StopBit.Text = element.Attribute(DemoDeviceList.XmlStopBit).Value;
			comboBox_Parity.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlParity).Value);
			comboBox_portName.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox5.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlRtsEnable).Value);
			checkBox_useMqtt.Checked = HslFormContent.GetXmlValue(element, "UseMqtt", false, bool.Parse);
			textBox_mqtt_ip.Text = HslFormContent.GetXmlValue(element, "MqttIP", "", (string m) => m);
			textBox_mqtt_port.Text = HslFormContent.GetXmlValue(element, "MqttPort", "", (string m) => m);
			textBox_mqtt_name.Text = HslFormContent.GetXmlValue(element, "MqttName", "", (string m) => m);
			textBox_mqtt_password.Text = HslFormContent.GetXmlValue(element, "MqttPwd", "", (string m) => m);
			textBox_mqtt_topic.Text = HslFormContent.GetXmlValue(element, "MqttTopic", "", (string m) => m);
			debugControl1.LoadXml(element);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox1.Checked)
			{
				using (FormInputAlien formInputAlien = new FormInputAlien())
				{
					if (formInputAlien.ShowDialog() == DialogResult.OK)
					{
						if (formInputAlien.UseHslDtuServer)
						{
							dtuRemoteConnect = new RemoteConnectInfo(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.DTU, formInputAlien.Pwd, formInputAlien.NeedAckDtuResult);
						}
						else
						{
							dtuRemoteConnect = new RemoteConnectInfo(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.CustomizeDTU);
						}
					}
					return;
				}
			}
			dtuRemoteConnect = null;
		}

		private void RenderDtuData(RemoteConnectInfo remoteConnect, PipeTcpNet pipeDtu, SocketDebugSession session, int code, byte[] data, string msg = null, string head = null)
		{
			if (code == 2 && (button1.Enabled || !checkBox1.Checked))
			{
				pipeDtu.CloseCommunication();
				pipeDtu = null;
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(session, code, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Finish", head);
				});
				return;
			}
			Invoke((Action)delegate
			{
				debugControl1.RenderSendReceiveContent(session, code, data, msg, head);
			});
			if (code != 2)
			{
				return;
			}
			pipeDtu.CloseCommunication();
			pipeDtu = null;
			HslHelper.ThreadSleep(10000);
			if (!button1.Enabled && checkBox1.Checked)
			{
				ThreadPool.QueueUserWorkItem(ConnectDtuIp, remoteConnect);
				return;
			}
			Invoke((Action)delegate
			{
				debugControl1.RenderSendReceiveContent(session, code, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Finish", head);
			});
		}

		public void ConnectDtuIp(object obj)
		{
			RemoteConnectInfo remoteConnect = obj as RemoteConnectInfo;
			if (remoteConnect == null)
			{
				return;
			}
			pipeDtu = new PipeTcpNet(remoteConnect.EndPoint.Address.ToString(), remoteConnect.EndPoint.Port);
			pipeDtu.ConnectTimeOut = 10000;
			OperateResult<bool> operateResult = pipeDtu.OpenCommunication();
			if (!operateResult.IsSuccess)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Socket Connected Failed : {operateResult.Message} 10s later retry...");
				return;
			}
			if (remoteConnect.DtuBytes != null)
			{
				if (!pipeDtu.Send(remoteConnect.DtuBytes).IsSuccess)
				{
					RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Socket Connected Failed : {operateResult.Message} 10s later retry...");
					return;
				}
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(null, 4, remoteConnect.DtuBytes, null, $"Ini DTU {remoteConnect.EndPoint}");
				});
			}
			if (remoteConnect.NeedAckResult)
			{
				OperateResult<byte[]> check = pipeDtu.ReceiveMessage(new AlienMessage(), null, false);
				if (check.IsSuccess)
				{
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(null, 0, check.Content, null, $"Ini DTU {remoteConnect.EndPoint} Back");
					});
					if (check.Content.Length >= 6)
					{
						switch (check.Content[5])
						{
						case 1:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.DeviceCurrentIsLoginRepeat} 10s later retry...");
							return;
						case 2:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.DeviceCurrentIsLoginForbidden} 10s later retry...");
							return;
						case 3:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.PasswordCheckFailed} 10s later retry...");
							return;
						}
					}
				}
			}
			remoteConnect.Session = new PipeSession
			{
				Communication = pipeDtu
			};
			try
			{
				pipeDtu.Socket.BeginReceive(dtuBuffer, 0, dtuBuffer.Length, SocketFlags.None, DtuAsyncCallback, remoteConnect);
			}
			catch (Exception ex)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex.Message} 10s later retry...");
			}
		}

		private void DtuAsyncCallback(IAsyncResult ar)
		{
			RemoteConnectInfo remoteConnect = ar.AsyncState as RemoteConnectInfo;
			if (remoteConnect == null)
			{
				return;
			}
			try
			{
				int num = (remoteConnect.Session.Communication as PipeTcpNet).Socket.EndReceive(ar);
				if (num == 0)
				{
					RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Closed, 10s later retry...");
					return;
				}
				byte[] data = dtuBuffer.SelectBegin(num);
				RenderDtuData(remoteConnect, pipeDtu, null, 4, data, string.Empty, $"Revc DTU {remoteConnect.EndPoint}");
				if (socketDebugSession != null)
				{
					try
					{
						socketDebugSession.SendData(data);
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(socketDebugSession, 1, data, null, $"Send DTU {remoteConnect.EndPoint}");
						});
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(socketDebugSession, -1, null, "socketDebugSession.SendData failed: " + ex2.Message);
						});
					}
				}
			}
			catch (Exception ex3)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex3.Message} 10s later retry...");
				return;
			}
			try
			{
				pipeDtu.Socket.BeginReceive(dtuBuffer, 0, dtuBuffer.Length, SocketFlags.None, DtuAsyncCallback, remoteConnect);
			}
			catch (Exception ex4)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex4.Message} 10s later retry...");
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
			this.checkBox_until_empty = new System.Windows.Forms.CheckBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox_mqtt_password = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_mqtt_name = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_mqtt_topic = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_mqtt_port = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_mqtt_ip = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.checkBox_useMqtt = new System.Windows.Forms.CheckBox();
			this.checkBox5 = new System.Windows.Forms.CheckBox();
			this.comboBox_portName = new System.Windows.Forms.ComboBox();
			this.comboBox_Parity = new System.Windows.Forms.ComboBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox_StopBit = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox_DataBits = new System.Windows.Forms.TextBox();
			this.label22 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_BaudRate = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.debugControl1 = new HslCommunicationDemo.HslDebug.DebugControl();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_until_empty);
			this.panel1.Controls.Add(this.panel3);
			this.panel1.Controls.Add(this.checkBox5);
			this.panel1.Controls.Add(this.comboBox_portName);
			this.panel1.Controls.Add(this.comboBox_Parity);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.textBox_StopBit);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.textBox_DataBits);
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_BaudRate);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(993, 71);
			this.panel1.TabIndex = 7;
			this.checkBox_until_empty.AutoSize = true;
			this.checkBox_until_empty.Checked = true;
			this.checkBox_until_empty.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_until_empty.Location = new System.Drawing.Point(695, 8);
			this.checkBox_until_empty.Name = "checkBox_until_empty";
			this.checkBox_until_empty.Size = new System.Drawing.Size(93, 21);
			this.checkBox_until_empty.TabIndex = 19;
			this.checkBox_until_empty.Text = "Until Empty";
			this.checkBox_until_empty.UseVisualStyleBackColor = true;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel3.Controls.Add(this.checkBox1);
			this.panel3.Controls.Add(this.textBox_mqtt_password);
			this.panel3.Controls.Add(this.label8);
			this.panel3.Controls.Add(this.textBox_mqtt_name);
			this.panel3.Controls.Add(this.label9);
			this.panel3.Controls.Add(this.textBox_mqtt_topic);
			this.panel3.Controls.Add(this.label2);
			this.panel3.Controls.Add(this.textBox_mqtt_port);
			this.panel3.Controls.Add(this.label4);
			this.panel3.Controls.Add(this.textBox_mqtt_ip);
			this.panel3.Controls.Add(this.label5);
			this.panel3.Controls.Add(this.checkBox_useMqtt);
			this.panel3.Location = new System.Drawing.Point(3, 34);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(985, 32);
			this.panel3.TabIndex = 18;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(852, 5);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(76, 21);
			this.checkBox1.TabIndex = 24;
			this.checkBox1.Text = "转发DTU";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox1.CheckedChanged += new System.EventHandler(checkBox1_CheckedChanged);
			this.textBox_mqtt_password.Location = new System.Drawing.Point(587, 3);
			this.textBox_mqtt_password.Name = "textBox_mqtt_password";
			this.textBox_mqtt_password.Size = new System.Drawing.Size(76, 23);
			this.textBox_mqtt_password.TabIndex = 23;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(537, 6);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(44, 17);
			this.label8.TabIndex = 22;
			this.label8.Text = "密码：";
			this.textBox_mqtt_name.Location = new System.Drawing.Point(416, 4);
			this.textBox_mqtt_name.Name = "textBox_mqtt_name";
			this.textBox_mqtt_name.Size = new System.Drawing.Size(110, 23);
			this.textBox_mqtt_name.TabIndex = 21;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(363, 7);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(56, 17);
			this.label9.TabIndex = 20;
			this.label9.Text = "用户名：";
			this.textBox_mqtt_topic.Location = new System.Drawing.Point(730, 4);
			this.textBox_mqtt_topic.Name = "textBox_mqtt_topic";
			this.textBox_mqtt_topic.Size = new System.Drawing.Size(103, 23);
			this.textBox_mqtt_topic.TabIndex = 19;
			this.textBox_mqtt_topic.Text = "Data";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(671, 7);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 18;
			this.label2.Text = "主题：";
			this.textBox_mqtt_port.Location = new System.Drawing.Point(296, 3);
			this.textBox_mqtt_port.Name = "textBox_mqtt_port";
			this.textBox_mqtt_port.Size = new System.Drawing.Size(55, 23);
			this.textBox_mqtt_port.TabIndex = 17;
			this.textBox_mqtt_port.Text = "1883";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(247, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 16;
			this.label4.Text = "Port：";
			this.textBox_mqtt_ip.Location = new System.Drawing.Point(131, 4);
			this.textBox_mqtt_ip.Name = "textBox_mqtt_ip";
			this.textBox_mqtt_ip.Size = new System.Drawing.Size(110, 23);
			this.textBox_mqtt_ip.TabIndex = 15;
			this.textBox_mqtt_ip.Text = "127.0.0.1";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(99, 7);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(31, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "IP：";
			this.checkBox_useMqtt.AutoSize = true;
			this.checkBox_useMqtt.Location = new System.Drawing.Point(4, 5);
			this.checkBox_useMqtt.Name = "checkBox_useMqtt";
			this.checkBox_useMqtt.Size = new System.Drawing.Size(87, 21);
			this.checkBox_useMqtt.TabIndex = 0;
			this.checkBox_useMqtt.Text = "转发MQTT";
			this.checkBox_useMqtt.UseVisualStyleBackColor = true;
			this.checkBox5.AutoSize = true;
			this.checkBox5.Location = new System.Drawing.Point(596, 8);
			this.checkBox5.Name = "checkBox5";
			this.checkBox5.Size = new System.Drawing.Size(84, 21);
			this.checkBox5.TabIndex = 17;
			this.checkBox5.Text = "RtsEnable";
			this.checkBox5.UseVisualStyleBackColor = true;
			this.comboBox_portName.FormattingEnabled = true;
			this.comboBox_portName.Location = new System.Drawing.Point(62, 4);
			this.comboBox_portName.Name = "comboBox_portName";
			this.comboBox_portName.Size = new System.Drawing.Size(105, 25);
			this.comboBox_portName.TabIndex = 16;
			this.comboBox_Parity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_Parity.FormattingEnabled = true;
			this.comboBox_Parity.Items.AddRange(new object[3] { "无", "奇", "偶" });
			this.comboBox_Parity.Location = new System.Drawing.Point(524, 6);
			this.comboBox_Parity.Name = "comboBox_Parity";
			this.comboBox_Parity.Size = new System.Drawing.Size(59, 25);
			this.comboBox_Parity.TabIndex = 15;
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(481, 9);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(44, 17);
			this.label24.TabIndex = 14;
			this.label24.Text = "奇偶：";
			this.textBox_StopBit.Location = new System.Drawing.Point(451, 6);
			this.textBox_StopBit.Name = "textBox_StopBit";
			this.textBox_StopBit.Size = new System.Drawing.Size(23, 23);
			this.textBox_StopBit.TabIndex = 13;
			this.textBox_StopBit.Text = "1";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(392, 9);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(56, 17);
			this.label23.TabIndex = 12;
			this.label23.Text = "停止位：";
			this.textBox_DataBits.Location = new System.Drawing.Point(355, 6);
			this.textBox_DataBits.Name = "textBox_DataBits";
			this.textBox_DataBits.Size = new System.Drawing.Size(24, 23);
			this.textBox_DataBits.TabIndex = 11;
			this.textBox_DataBits.Text = "8";
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(293, 9);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(56, 17);
			this.label22.TabIndex = 10;
			this.label22.Text = "数据位：";
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(892, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(795, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_BaudRate.Location = new System.Drawing.Point(240, 6);
			this.textBox_BaudRate.Name = "textBox_BaudRate";
			this.textBox_BaudRate.Size = new System.Drawing.Size(47, 23);
			this.textBox_BaudRate.TabIndex = 3;
			this.textBox_BaudRate.Text = "9600";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(173, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "波特率：";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Com口：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.debugControl1);
			this.panel2.Location = new System.Drawing.Point(3, 107);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(993, 557);
			this.panel2.TabIndex = 13;
			this.debugControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.debugControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.debugControl1.Location = new System.Drawing.Point(0, 0);
			this.debugControl1.Name = "debugControl1";
			this.debugControl1.Size = new System.Drawing.Size(991, 555);
			this.debugControl1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "serial";
			this.userControlHead1.Size = new System.Drawing.Size(1000, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1000, 666);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSerialDebug";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "串口调试工具";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSerialDebug_FormClosing);
			base.Load += new System.EventHandler(FormSerialDebug_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
