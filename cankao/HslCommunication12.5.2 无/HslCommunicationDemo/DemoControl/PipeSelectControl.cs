using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.MQTT;

namespace HslCommunicationDemo.DemoControl
{
	public class PipeSelectControl : UserControl
	{
		private int dtu_connected = 0;

		private SettingPipe settingPipe = SettingPipe.TcpPipe;

		private SettingTcpIP settingTcpIP = null;

		private SettingTcpDebug settingTcpDebug = null;

		private SettingTcpIP settingUdpIP = null;

		private SettingMqtt settingMqtt = new SettingMqtt();

		private SettingSerial settingSerial = null;

		private NetworkAlienClient dtuServer;

		private BinaryCommunication deviceTcpNet;

		private IContainer components = null;

		private Panel panel_tcp;

		private TextBox textBox_tcp_port;

		private Label label3;

		private TextBox textBox_tcp_ip;

		private Label label1;

		private TextBox textBox_tcp_connectTimeout;

		private Label label2;

		private Panel panel_serial;

		private ComboBox comboBox_com_port;

		private ComboBox comboBox_com_parity;

		private Label label5;

		private TextBox textBox_com_stopbits;

		private Label label6;

		private TextBox textBox_com_databits;

		private Label label25;

		private Label label26;

		private Label label27;

		private Panel panel_udp;

		private TextBox textBox_udp_receiveTimeout;

		private Label label7;

		private TextBox textBox_udp_port;

		private Label label9;

		private TextBox textBox_udp_ip;

		private Label label10;

		private ComboBox comboBox_pipe_select;

		private Panel panel_mqtt;

		private TextBox textBox_mqtt_port;

		private Label label14;

		private TextBox textBox_mqtt_ip;

		private Label label15;

		private TextBox textBox_mqtt_name;

		private Label label12;

		private LinkLabel linkLabel_mqtt_more;

		private LinkLabel linkLabel_udp_more;

		private LinkLabel linkLabel_tcp_more;

		private TextBox textBox_tcp_receive;

		private Label label4;

		private Label label_info_pipe;

		private ComboBox comboBox_com_baudrate;

		private LinkLabel linkLabel_serial_more;

		private Panel panel_dtu;

		private LinkLabel linkLabel_dtu_state;

		private TextBox textBox_dtu_id;

		private TextBox textBox_dtu_port;

		private Label label11;

		private TextBox textBox_dtu_pwd;

		private Label label13;

		private CheckBox checkBox_dtu_back;

		private Label label8;

		private LinkLabel linkLabel_dtu_message;

		private CheckBox checkBox_useRegister;

		public string TcpPortText
		{
			get
			{
				return textBox_tcp_port.Text;
			}
			set
			{
				textBox_tcp_port.Text = value;
			}
		}

		public string UdpPortText
		{
			get
			{
				return textBox_udp_port.Text;
			}
			set
			{
				textBox_udp_port.Text = value;
			}
		}

		public string SerialBaudRate
		{
			get
			{
				return comboBox_com_baudrate.Text;
			}
			set
			{
				comboBox_com_baudrate.Text = value;
			}
		}

		public string SerialDataBits
		{
			get
			{
				return textBox_com_databits.Text;
			}
			set
			{
				textBox_com_databits.Text = value;
			}
		}

		public string SerialStopBits
		{
			get
			{
				return textBox_com_stopbits.Text;
			}
			set
			{
				textBox_com_stopbits.Text = value;
			}
		}

		public Parity SerialParity
		{
			get
			{
				switch (comboBox_com_parity.SelectedIndex)
				{
				case 0:
					return Parity.None;
				case 1:
					return Parity.Odd;
				case 2:
					return Parity.Even;
				default:
					return Parity.None;
				}
			}
			set
			{
				switch (value)
				{
				case Parity.None:
					comboBox_com_parity.SelectedIndex = 0;
					break;
				case Parity.Odd:
					comboBox_com_parity.SelectedIndex = 1;
					break;
				case Parity.Even:
					comboBox_com_parity.SelectedIndex = 2;
					break;
				}
			}
		}

		public SettingPipe SettingPipe
		{
			get
			{
				return settingPipe;
			}
			set
			{
				switch (value)
				{
				case SettingPipe.TcpPipe:
					comboBox_pipe_select.SelectedIndex = 0;
					break;
				case SettingPipe.UdpPipe:
					comboBox_pipe_select.SelectedIndex = 1;
					break;
				case SettingPipe.SerialPipe:
					comboBox_pipe_select.SelectedIndex = 2;
					break;
				case SettingPipe.MqttPipe:
					comboBox_pipe_select.SelectedIndex = 3;
					break;
				case SettingPipe.SslPipe:
					comboBox_pipe_select.SelectedIndex = 4;
					break;
				case SettingPipe.DTU:
					comboBox_pipe_select.SelectedIndex = 5;
					break;
				case SettingPipe.MoxaCom:
					comboBox_pipe_select.SelectedIndex = 6;
					break;
				case SettingPipe.Debug:
					comboBox_pipe_select.SelectedIndex = 7;
					break;
				default:
					SetVisible(value);
					break;
				}
				SetVisible(value);
			}
		}

		public PipeSelectControl()
		{
			InitializeComponent();
			comboBox_pipe_select.DataSource = new string[8] { "TcpIp", "UdpIp", "Serial", "Mqtt", "SSL/TLS", "DTU", "MoxaCom", "Debug" };
			string[] portNames = SerialPort.GetPortNames();
			comboBox_com_port.DataSource = portNames;
			if (portNames != null && portNames.Length != 0)
			{
				comboBox_com_port.SelectedIndex = 0;
			}
			panel_mqtt.Visible = false;
			panel_mqtt.Location = panel_tcp.Location;
			panel_serial.Visible = false;
			panel_serial.Location = panel_tcp.Location;
			panel_udp.Visible = false;
			panel_udp.Location = panel_tcp.Location;
			panel_dtu.Visible = false;
			panel_dtu.Location = panel_tcp.Location;
			comboBox_pipe_select.SelectedIndex = 0;
			comboBox_pipe_select.SelectedIndexChanged += ComboBox_pipe_select_SelectedIndexChanged;
			if (Program.Language == 2)
			{
				comboBox_com_parity.DataSource = new string[3] { "None", "Odd", "Even" };
			}
			comboBox_com_parity.SelectedIndex = 0;
			checkBox_dtu_back.CheckedChanged += CheckBox_dtu_back_CheckedChanged;
		}

		private void ComboBox_pipe_select_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (comboBox_pipe_select.SelectedIndex)
			{
			case 0:
				SetVisible(SettingPipe.TcpPipe);
				break;
			case 1:
				SetVisible(SettingPipe.UdpPipe);
				break;
			case 2:
				SetVisible(SettingPipe.SerialPipe);
				break;
			case 3:
				SetVisible(SettingPipe.MqttPipe);
				break;
			case 4:
				SetVisible(SettingPipe.SslPipe);
				break;
			case 5:
				SetVisible(SettingPipe.DTU);
				break;
			case 6:
				SetVisible(SettingPipe.MoxaCom);
				break;
			case 7:
				SetVisible(SettingPipe.Debug);
				break;
			}
		}

		private void PipeSelectControl_Load(object sender, EventArgs e)
		{
			if (Program.Language != 1)
			{
				label1.Text = (label10.Text = (label15.Text = "Ip:"));
				label3.Text = (label9.Text = (label14.Text = "Port:"));
				label2.Text = "Conn-Time:";
				label4.Text = (label7.Text = "Recv-Time:");
				label12.Text = "Name:";
				label27.Text = "Com:";
				label26.Text = "BaudRate:";
				label6.Text = "StopBits:";
				label5.Text = "Parity:";
				label25.Text = "Databits:";
				label_info_pipe.Text = "Pipe";
				linkLabel_dtu_message.Text = "MSG";
				label13.Text = "Pwd:";
				label11.Text = "Port:";
				checkBox_dtu_back.Text = "Back MSG?";
			}
		}

		public void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue("PipeSetting", settingPipe);
			if (settingPipe == SettingPipe.TcpPipe || settingPipe == SettingPipe.SslPipe)
			{
				element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_tcp_ip.Text);
				element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_tcp_port.Text);
				element.SetAttributeValue(DemoDeviceList.XmlConnectTimeOut, textBox_tcp_connectTimeout.Text);
				element.SetAttributeValue(DemoDeviceList.XmlReceiveTimeOut, textBox_tcp_receive.Text);
				if (settingTcpIP != null)
				{
					element.SetAttributeValue("PipeSleepTime", settingTcpIP.SleepTime);
					element.SetAttributeValue("SocketKeepAliveTime", settingTcpIP.SocketKeepAliveTime);
					element.SetAttributeValue("IsPersistentConnection", settingTcpIP.IsPersistentConnection);
					element.SetAttributeValue("LocalIpAddress", settingTcpIP.LocalIpAddress);
					element.SetAttributeValue("LocalPort", settingTcpIP.LocalPort);
					element.SetAttributeValue("CloseOnRecvTimeOutTick", settingTcpIP.CloseOnRecvTimeOutTick);
					if (!string.IsNullOrEmpty(settingTcpIP.SendBeforeHex))
					{
						element.SetAttributeValue("SendBeforeHex", settingTcpIP.SendBeforeHex);
					}
				}
			}
			else if (settingPipe == SettingPipe.Debug)
			{
				element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_tcp_ip.Text);
				element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_tcp_port.Text);
				element.SetAttributeValue(DemoDeviceList.XmlConnectTimeOut, textBox_tcp_connectTimeout.Text);
				element.SetAttributeValue(DemoDeviceList.XmlReceiveTimeOut, textBox_tcp_receive.Text);
				if (settingTcpDebug != null)
				{
					element.SetAttributeValue("PipeSleepTime", settingTcpDebug.SleepTime);
					element.SetAttributeValue("SocketKeepAliveTime", settingTcpDebug.SocketKeepAliveTime);
					element.SetAttributeValue("IsPersistentConnection", settingTcpDebug.IsPersistentConnection);
					element.SetAttributeValue("LocalIpAddress", settingTcpDebug.LocalIpAddress);
					element.SetAttributeValue("LocalPort", settingTcpDebug.LocalPort);
					element.SetAttributeValue("ClientKey", settingTcpDebug.ClientKey);
					if (!string.IsNullOrEmpty(settingTcpDebug.SendBeforeHex))
					{
						element.SetAttributeValue("SendBeforeHex", settingTcpDebug.SendBeforeHex);
					}
				}
			}
			else if (settingPipe == SettingPipe.UdpPipe)
			{
				element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_udp_ip.Text);
				element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_udp_port.Text);
				element.SetAttributeValue(DemoDeviceList.XmlReceiveTimeOut, textBox_udp_receiveTimeout.Text);
				if (settingUdpIP != null)
				{
					element.SetAttributeValue("PipeSleepTime", settingUdpIP.SleepTime);
					element.SetAttributeValue("SocketKeepAliveTime", settingUdpIP.SocketKeepAliveTime);
					element.SetAttributeValue("IsPersistentConnection", settingUdpIP.IsPersistentConnection);
					element.SetAttributeValue("LocalIpAddress", settingUdpIP.LocalIpAddress);
					element.SetAttributeValue("LocalPort", settingUdpIP.LocalPort);
					if (!string.IsNullOrEmpty(settingUdpIP.SendBeforeHex))
					{
						element.SetAttributeValue("SendBeforeHex", settingUdpIP.SendBeforeHex);
					}
				}
			}
			else if (settingPipe == SettingPipe.SerialPipe || settingPipe == SettingPipe.MoxaCom)
			{
				element.SetAttributeValue(DemoDeviceList.XmlCom, comboBox_com_port.Text);
				element.SetAttributeValue(DemoDeviceList.XmlBaudRate, comboBox_com_baudrate.Text);
				element.SetAttributeValue(DemoDeviceList.XmlDataBits, textBox_com_databits.Text);
				element.SetAttributeValue(DemoDeviceList.XmlStopBit, textBox_com_stopbits.Text);
				element.SetAttributeValue(DemoDeviceList.XmlParity, comboBox_com_parity.SelectedIndex);
				if (settingSerial != null)
				{
					element.SetAttributeValue("PipeSleepTime", settingSerial.SleepTime);
					element.SetAttributeValue("RtsEnable", settingSerial.RtsEnable);
					element.SetAttributeValue("DtrEnable", settingSerial.DtrEnable);
					element.SetAttributeValue("ReceiveTimeOut", settingSerial.ReceiveTimeOut);
					if (!string.IsNullOrEmpty(settingSerial.SendBeforeHex))
					{
						element.SetAttributeValue("SendBeforeHex", settingSerial.SendBeforeHex);
					}
				}
			}
			else if (settingPipe == SettingPipe.MqttPipe)
			{
				element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_mqtt_ip.Text);
				element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_mqtt_port.Text);
				element.SetAttributeValue("MqttName", textBox_mqtt_name.Text);
				element.SetAttributeValue("MqttPassword", settingMqtt.Password);
				element.SetAttributeValue("MqttClientID", settingMqtt.ClientID);
				element.SetAttributeValue("MqttUseRSAProvider", settingMqtt.UseRSAProvider);
				element.SetAttributeValue("Device2Mqtt", settingMqtt.Device2Mqtt);
				element.SetAttributeValue("Mqtt2Device", settingMqtt.Mqtt2Device);
			}
			else if (settingPipe == SettingPipe.DTU)
			{
				element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_dtu_port.Text);
				element.SetAttributeValue("DTUBack", checkBox_dtu_back.Checked);
				element.SetAttributeValue("DTUId", textBox_dtu_id.Text);
				element.SetAttributeValue("DTUPwd", textBox_dtu_pwd.Text);
				element.SetAttributeValue("DTUUseRegister", checkBox_useRegister.Checked);
			}
		}

		public void LoadXmlParameter(XElement element, SettingPipe settingPipeDefault)
		{
			SettingPipe settingPipe = (SettingPipe = HslFormContent.GetXmlEnum(element, "PipeSetting", settingPipeDefault));
			if (settingPipe == SettingPipe.TcpPipe || settingPipe == SettingPipe.SslPipe)
			{
				textBox_tcp_ip.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, textBox_tcp_ip.Text, (string m) => m);
				textBox_tcp_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_tcp_port.Text, (string m) => m);
				textBox_tcp_connectTimeout.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlConnectTimeOut, textBox_tcp_connectTimeout.Text, (string m) => m);
				textBox_tcp_receive.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlReceiveTimeOut, textBox_tcp_receive.Text, (string m) => m);
				if (element.Attribute("PipeSleepTime") != null)
				{
					settingTcpIP = new SettingTcpIP();
					settingTcpIP.SleepTime = HslFormContent.GetXmlValue(element, "PipeSleepTime", settingTcpIP.SleepTime, int.Parse);
					settingTcpIP.LocalIpAddress = HslFormContent.GetXmlValue(element, "LocalIpAddress", settingTcpIP.LocalIpAddress, (string m) => m);
					settingTcpIP.LocalPort = HslFormContent.GetXmlValue(element, "LocalPort", settingTcpIP.LocalPort, int.Parse);
					settingTcpIP.CloseOnRecvTimeOutTick = HslFormContent.GetXmlValue(element, "CloseOnRecvTimeOutTick", settingTcpIP.CloseOnRecvTimeOutTick, int.Parse);
					settingTcpIP.SocketKeepAliveTime = HslFormContent.GetXmlValue(element, "SocketKeepAliveTime", settingTcpIP.SocketKeepAliveTime, int.Parse);
					settingTcpIP.IsPersistentConnection = HslFormContent.GetXmlValue(element, "IsPersistentConnection", settingTcpIP.IsPersistentConnection, bool.Parse);
					settingTcpIP.SendBeforeHex = HslFormContent.GetXmlValue(element, "SendBeforeHex", settingTcpIP.SendBeforeHex, (string m) => m);
				}
				return;
			}
			switch (settingPipe)
			{
			case SettingPipe.Debug:
				textBox_tcp_ip.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, textBox_tcp_ip.Text, (string m) => m);
				textBox_tcp_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_tcp_port.Text, (string m) => m);
				textBox_tcp_connectTimeout.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlConnectTimeOut, textBox_tcp_connectTimeout.Text, (string m) => m);
				textBox_tcp_receive.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlReceiveTimeOut, textBox_tcp_receive.Text, (string m) => m);
				if (element.Attribute("PipeSleepTime") != null)
				{
					settingTcpDebug = new SettingTcpDebug();
					settingTcpDebug.SleepTime = HslFormContent.GetXmlValue(element, "PipeSleepTime", settingTcpDebug.SleepTime, int.Parse);
					settingTcpDebug.LocalIpAddress = HslFormContent.GetXmlValue(element, "LocalIpAddress", settingTcpDebug.LocalIpAddress, (string m) => m);
					settingTcpDebug.LocalPort = HslFormContent.GetXmlValue(element, "LocalPort", settingTcpDebug.LocalPort, int.Parse);
					settingTcpDebug.SocketKeepAliveTime = HslFormContent.GetXmlValue(element, "SocketKeepAliveTime", settingTcpDebug.SocketKeepAliveTime, int.Parse);
					settingTcpDebug.IsPersistentConnection = HslFormContent.GetXmlValue(element, "IsPersistentConnection", settingTcpDebug.IsPersistentConnection, bool.Parse);
					settingTcpDebug.SendBeforeHex = HslFormContent.GetXmlValue(element, "SendBeforeHex", settingTcpDebug.SendBeforeHex, (string m) => m);
					settingTcpDebug.ClientKey = HslFormContent.GetXmlValue(element, "ClientKey", settingTcpDebug.ClientKey, ushort.Parse);
				}
				break;
			case SettingPipe.UdpPipe:
				textBox_udp_ip.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, textBox_udp_ip.Text, (string m) => m);
				textBox_udp_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_udp_port.Text, (string m) => m);
				textBox_udp_receiveTimeout.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlReceiveTimeOut, textBox_udp_receiveTimeout.Text, (string m) => m);
				if (element.Attribute("PipeSleepTime") != null)
				{
					settingUdpIP = new SettingTcpIP();
					settingUdpIP.SleepTime = HslFormContent.GetXmlValue(element, "PipeSleepTime", settingUdpIP.SleepTime, int.Parse);
					settingUdpIP.LocalIpAddress = HslFormContent.GetXmlValue(element, "LocalIpAddress", settingUdpIP.LocalIpAddress, (string m) => m);
					settingUdpIP.LocalPort = HslFormContent.GetXmlValue(element, "LocalPort", settingUdpIP.LocalPort, int.Parse);
					settingUdpIP.SocketKeepAliveTime = HslFormContent.GetXmlValue(element, "SocketKeepAliveTime", settingUdpIP.SocketKeepAliveTime, int.Parse);
					settingUdpIP.IsPersistentConnection = HslFormContent.GetXmlValue(element, "IsPersistentConnection", settingUdpIP.IsPersistentConnection, bool.Parse);
					settingUdpIP.SendBeforeHex = HslFormContent.GetXmlValue(element, "SendBeforeHex", settingUdpIP.SendBeforeHex, (string m) => m);
				}
				break;
			default:
				if (settingPipe != SettingPipe.MoxaCom)
				{
					switch (settingPipe)
					{
					case SettingPipe.MqttPipe:
						textBox_mqtt_ip.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, textBox_mqtt_ip.Text, (string m) => m);
						textBox_mqtt_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_mqtt_port.Text, (string m) => m);
						textBox_mqtt_name.Text = HslFormContent.GetXmlValue(element, "MqttName", textBox_mqtt_name.Text, (string m) => m);
						settingMqtt.Password = HslFormContent.GetXmlValue(element, "MqttPassword", settingMqtt.Password, (string m) => m);
						settingMqtt.ClientID = HslFormContent.GetXmlValue(element, "MqttClientID", settingMqtt.ClientID, (string m) => m);
						settingMqtt.UseRSAProvider = HslFormContent.GetXmlValue(element, "MqttUseRSAProvider", settingMqtt.UseRSAProvider, bool.Parse);
						settingMqtt.Device2Mqtt = HslFormContent.GetXmlValue(element, "Device2Mqtt", settingMqtt.Device2Mqtt, (string m) => m);
						settingMqtt.Mqtt2Device = HslFormContent.GetXmlValue(element, "Mqtt2Device", settingMqtt.Mqtt2Device, (string m) => m);
						break;
					case SettingPipe.DTU:
						textBox_dtu_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_dtu_port.Text, (string m) => m);
						checkBox_dtu_back.Checked = HslFormContent.GetXmlValue(element, "DTUBack", checkBox_dtu_back.Checked, bool.Parse);
						textBox_dtu_id.Text = HslFormContent.GetXmlValue(element, "DTUId", textBox_dtu_id.Text, (string m) => m);
						textBox_dtu_pwd.Text = HslFormContent.GetXmlValue(element, "DTUPwd", textBox_dtu_pwd.Text, (string m) => m);
						checkBox_useRegister.Checked = HslFormContent.GetXmlValue(element, "DTUUseRegister", checkBox_useRegister.Checked, bool.Parse);
						break;
					}
					break;
				}
				goto case SettingPipe.SerialPipe;
			case SettingPipe.SerialPipe:
				comboBox_com_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlCom, comboBox_com_port.Text, (string m) => m);
				comboBox_com_baudrate.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlBaudRate, comboBox_com_baudrate.Text, (string m) => m);
				textBox_com_databits.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlDataBits, textBox_com_databits.Text, (string m) => m);
				textBox_com_stopbits.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStopBit, textBox_com_stopbits.Text, (string m) => m);
				comboBox_com_parity.SelectedIndex = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlParity, comboBox_com_parity.SelectedIndex, int.Parse);
				if (element.Attribute("PipeSleepTime") != null)
				{
					settingSerial = new SettingSerial();
					settingSerial.SleepTime = HslFormContent.GetXmlValue(element, "PipeSleepTime", settingSerial.SleepTime, int.Parse);
					settingSerial.RtsEnable = HslFormContent.GetXmlValue(element, "RtsEnable", settingSerial.RtsEnable, bool.Parse);
					settingSerial.DtrEnable = HslFormContent.GetXmlValue(element, "DtrEnable", settingSerial.DtrEnable, bool.Parse);
					settingSerial.SendBeforeHex = HslFormContent.GetXmlValue(element, "SendBeforeHex", settingSerial.SendBeforeHex, (string m) => m);
					settingSerial.ReceiveTimeOut = HslFormContent.GetXmlValue(element, "ReceiveTimeOut", settingSerial.ReceiveTimeOut, int.Parse);
				}
				break;
			}
		}

		public void SetVisible(SettingPipe settingPipe)
		{
			if (settingPipe == SettingPipe.TcpPipe || settingPipe == SettingPipe.Debug)
			{
				panel_tcp.Visible = true;
				panel_udp.Visible = false;
				panel_mqtt.Visible = false;
				panel_serial.Visible = false;
				panel_dtu.Visible = false;
			}
			else if (settingPipe == SettingPipe.SslPipe)
			{
				panel_tcp.Visible = true;
				panel_udp.Visible = false;
				panel_mqtt.Visible = false;
				panel_serial.Visible = false;
				panel_dtu.Visible = false;
			}
			else if (settingPipe == SettingPipe.UdpPipe)
			{
				panel_udp.Visible = true;
				panel_tcp.Visible = false;
				panel_mqtt.Visible = false;
				panel_serial.Visible = false;
				panel_dtu.Visible = false;
			}
			else if (settingPipe == SettingPipe.SerialPipe || settingPipe == SettingPipe.MoxaCom)
			{
				panel_serial.Visible = true;
				panel_udp.Visible = false;
				panel_tcp.Visible = false;
				panel_mqtt.Visible = false;
				panel_dtu.Visible = false;
			}
			else
			{
				switch (settingPipe)
				{
				case SettingPipe.MqttPipe:
					panel_mqtt.Visible = true;
					panel_serial.Visible = false;
					panel_udp.Visible = false;
					panel_tcp.Visible = false;
					panel_dtu.Visible = false;
					break;
				case SettingPipe.DTU:
					panel_mqtt.Visible = false;
					panel_serial.Visible = false;
					panel_udp.Visible = false;
					panel_tcp.Visible = false;
					panel_dtu.Visible = true;
					break;
				}
			}
			this.settingPipe = settingPipe;
		}

		public void IniPipe(BinaryCommunication device)
		{
			deviceTcpNet = device;
			if (settingPipe == SettingPipe.TcpPipe)
			{
				device.CommunicationPipe = CreatePipeTcpNet(device);
			}
			else if (settingPipe == SettingPipe.Debug)
			{
				device.CommunicationPipe = CreatePipeDebugNet(device);
			}
			else if (settingPipe == SettingPipe.UdpPipe)
			{
				device.CommunicationPipe = CreatePipeUdpNet(device);
			}
			else if (settingPipe == SettingPipe.SerialPipe)
			{
				device.CommunicationPipe = CreatePipeSerialPort(device);
			}
			else if (settingPipe == SettingPipe.MoxaCom)
			{
				device.CommunicationPipe = CreatePipeMoxa(device);
			}
			else if (settingPipe == SettingPipe.SslPipe)
			{
				device.CommunicationPipe = CreatePipeSSLNet(device);
			}
			else if (settingPipe == SettingPipe.MqttPipe)
			{
				device.CommunicationPipe = CreatePipeMqttClient();
			}
			else if (settingPipe == SettingPipe.DTU)
			{
				if (deviceTcpNet == null)
				{
					throw new Exception("当前的协议不支持DTU模式");
				}
				if (dtuServer != null)
				{
					dtuServer.ServerClose();
				}
				dtuServer = new NetworkAlienClient();
				dtuServer.LogNet = deviceTcpNet.LogNet;
				dtuServer.UseRegistrationPackage = checkBox_useRegister.Checked;
				dtuServer.IsResponseAck = checkBox_dtu_back.Checked;
				if (!string.IsNullOrEmpty(textBox_dtu_pwd.Text))
				{
					dtuServer.SetPassword(Encoding.ASCII.GetBytes(textBox_dtu_pwd.Text));
				}
				dtuServer.OnClientConnected += DtuServer_OnClientConnected;
				dtuServer.ServerStart(int.Parse(textBox_dtu_port.Text));
				linkLabel_dtu_state.Text = "Wait Connect...";
				linkLabel_dtu_state.LinkColor = Color.DodgerBlue;
				deviceTcpNet.SetDtuPipe(new PipeDtuNet
				{
					DtuServer = dtuServer,
					DTU = textBox_dtu_id.Text,
					Pwd = textBox_dtu_pwd.Text
				});
			}
		}

		public string GetTcpIpAddress()
		{
			return textBox_tcp_ip.Text;
		}

		public string GetTcpPort()
		{
			return textBox_tcp_port.Text;
		}

		private void CheckBox_dtu_back_CheckedChanged(object sender, EventArgs e)
		{
			if (dtuServer != null)
			{
				dtuServer.IsResponseAck = checkBox_dtu_back.Checked;
			}
		}

		public void ExtraCloseAction(BinaryCommunication device)
		{
			if (dtuServer != null)
			{
				dtu_connected = 0;
				linkLabel_dtu_state.Text = "Stopped";
				linkLabel_dtu_state.LinkColor = Color.Gray;
				dtuServer.ServerClose();
				dtuServer = null;
			}
		}

		private void DeviceTcpNet_OnConnectClose(object sender, EventArgs e)
		{
			dtu_connected = 0;
			linkLabel_dtu_state.Text = "Stopped";
			linkLabel_dtu_state.LinkColor = Color.Gray;
			if (dtuServer != null)
			{
				dtuServer.ServerClose();
				dtuServer = null;
			}
		}

		private void DtuServer_OnClientConnected(PipeDtuNet session)
		{
			if (dtuServer.UseRegistrationPackage)
			{
				if (session.DTU == textBox_dtu_id.Text)
				{
					if (deviceTcpNet.CommunicationPipe != null && !deviceTcpNet.CommunicationPipe.IsConnectError())
					{
						dtuServer.LogNet?.WriteDebug(dtuServer.ToString(), "DTU:" + session.DTU + " Login Repeat");
						session?.CloseCommunication();
						return;
					}
					OperateResult connect = deviceTcpNet.SetDtuPipe(session);
					dtu_connected++;
					Invoke((Action)delegate
					{
						if (connect.IsSuccess)
						{
							linkLabel_dtu_state.Text = $"Connected[{dtu_connected}]";
							linkLabel_dtu_state.LinkColor = Color.Green;
							linkLabel_dtu_state.BackColor = Color.Cyan;
						}
						else
						{
							DemoUtils.ShowMessage("Connect failed: " + connect.Message);
							linkLabel_dtu_state.Text = "Connect failed";
							linkLabel_dtu_state.LinkColor = Color.Tomato;
						}
					});
					HslHelper.ThreadSleep(2000);
					if (connect.IsSuccess)
					{
						linkLabel_dtu_state.BackColor = System.Windows.Forms.Control.DefaultBackColor;
					}
				}
				else
				{
					dtuServer.LogNet?.WriteDebug(dtuServer.ToString(), "DTU:" + session.DTU + " Login Forbidden");
					session?.CloseCommunication();
					Invoke((Action)delegate
					{
						linkLabel_dtu_state.Text = "ID not same";
						linkLabel_dtu_state.LinkColor = Color.Tomato;
					});
				}
				return;
			}
			if (deviceTcpNet.CommunicationPipe != null && !deviceTcpNet.CommunicationPipe.IsConnectError())
			{
				dtuServer.LogNet?.WriteDebug(dtuServer.ToString(), $"Ip:{session} Login Repeat");
				session?.CloseCommunication();
				return;
			}
			OperateResult connect2 = deviceTcpNet.SetDtuPipe(session);
			dtu_connected++;
			Invoke((Action)delegate
			{
				if (connect2.IsSuccess)
				{
					linkLabel_dtu_state.Text = $"Connected[{dtu_connected}]";
					linkLabel_dtu_state.LinkColor = Color.Green;
					linkLabel_dtu_state.BackColor = Color.Cyan;
				}
				else
				{
					DemoUtils.ShowMessage("Connect failed: " + connect2.Message);
					linkLabel_dtu_state.Text = "Connect failed";
					linkLabel_dtu_state.LinkColor = Color.Tomato;
				}
			});
			HslHelper.ThreadSleep(2000);
			if (connect2.IsSuccess)
			{
				linkLabel_dtu_state.BackColor = System.Windows.Forms.Control.DefaultBackColor;
			}
		}

		private void SetPipeTcpSetting(BinaryCommunication binaryCommunication, PipeTcpNet pipe, SettingTcpIP setting)
		{
			if (setting != null)
			{
				pipe.SleepTime = setting.SleepTime;
				pipe.SocketKeepAliveTime = setting.SocketKeepAliveTime;
				pipe.IsPersistentConnection = setting.IsPersistentConnection;
				if (!string.IsNullOrEmpty(setting.LocalIpAddress) || setting.LocalPort > 0)
				{
					pipe.LocalBinding = new IPEndPoint(string.IsNullOrEmpty(setting.LocalIpAddress) ? IPAddress.Any : IPAddress.Parse(setting.LocalIpAddress), (setting.LocalPort >= 0) ? setting.LocalPort : 0);
				}
				if (setting.CloseOnRecvTimeOutTick != 1)
				{
					pipe.CloseOnRecvTimeOutTick = setting.CloseOnRecvTimeOutTick;
				}
				if (!string.IsNullOrEmpty(setting.SendBeforeHex) && binaryCommunication != null)
				{
					binaryCommunication.SendBeforeHex = setting.SendBeforeHex;
				}
			}
		}

		public PipeTcpNet CreatePipeTcpNet(BinaryCommunication binaryCommunication)
		{
			PipeTcpNet pipeTcpNet = new PipeTcpNet(textBox_tcp_ip.Text, int.Parse(textBox_tcp_port.Text));
			pipeTcpNet.ConnectTimeOut = int.Parse(textBox_tcp_connectTimeout.Text);
			pipeTcpNet.ReceiveTimeOut = int.Parse(textBox_tcp_receive.Text);
			SetPipeTcpSetting(binaryCommunication, pipeTcpNet, settingTcpIP);
			return pipeTcpNet;
		}

		public PipeDebugRemote CreatePipeDebugNet(BinaryCommunication binaryCommunication)
		{
			PipeDebugRemote pipeDebugRemote = new PipeDebugRemote(textBox_tcp_ip.Text, int.Parse(textBox_tcp_port.Text));
			pipeDebugRemote.ConnectTimeOut = int.Parse(textBox_tcp_connectTimeout.Text);
			pipeDebugRemote.ReceiveTimeOut = int.Parse(textBox_tcp_receive.Text);
			SetPipeTcpSetting(binaryCommunication, pipeDebugRemote, settingTcpDebug);
			if (settingTcpDebug != null)
			{
				pipeDebugRemote.ClientKey = settingTcpDebug.ClientKey;
			}
			return pipeDebugRemote;
		}

		public PipeSslNet CreatePipeSSLNet(BinaryCommunication binaryCommunication)
		{
			PipeSslNet pipeSslNet = new PipeSslNet(textBox_tcp_ip.Text, int.Parse(textBox_tcp_port.Text), false);
			pipeSslNet.ConnectTimeOut = int.Parse(textBox_tcp_connectTimeout.Text);
			pipeSslNet.ReceiveTimeOut = int.Parse(textBox_tcp_receive.Text);
			SetPipeTcpSetting(binaryCommunication, pipeSslNet, settingTcpIP);
			return pipeSslNet;
		}

		public PipeUdpNet CreatePipeUdpNet(BinaryCommunication binaryCommunication)
		{
			PipeUdpNet pipeUdpNet = new PipeUdpNet(textBox_udp_ip.Text, int.Parse(textBox_udp_port.Text));
			pipeUdpNet.ReceiveTimeOut = int.Parse(textBox_udp_receiveTimeout.Text);
			SetPipeTcpSetting(binaryCommunication, pipeUdpNet, settingUdpIP);
			return pipeUdpNet;
		}

		private Parity GetSerialParity()
		{
			return (comboBox_com_parity.SelectedIndex != 0) ? ((comboBox_com_parity.SelectedIndex == 1) ? Parity.Odd : Parity.Even) : Parity.None;
		}

		private StopBits GetSerialStopBits()
		{
			return (textBox_com_stopbits.Text.Trim() == "1") ? StopBits.One : ((textBox_com_stopbits.Text.Trim() == "2") ? StopBits.Two : ((!(textBox_com_stopbits.Text.Trim() == "0")) ? StopBits.OnePointFive : StopBits.None));
		}

		public PipeSerialPort CreatePipeSerialPort(BinaryCommunication binaryCommunication)
		{
			PipeSerialPort pipeSerialPort = new PipeSerialPort();
			pipeSerialPort.SerialPortInni(delegate(SerialPort p)
			{
				p.PortName = comboBox_com_port.Text;
				p.BaudRate = int.Parse(comboBox_com_baudrate.Text);
				p.DataBits = int.Parse(textBox_com_databits.Text);
				p.StopBits = GetSerialStopBits();
				p.Parity = GetSerialParity();
				if (settingSerial != null)
				{
					p.RtsEnable = settingSerial.RtsEnable;
					p.DtrEnable = settingSerial.DtrEnable;
				}
			});
			if (settingSerial != null)
			{
				pipeSerialPort.SleepTime = settingSerial.SleepTime;
				pipeSerialPort.ReceiveTimeOut = settingSerial.ReceiveTimeOut;
				if (!string.IsNullOrEmpty(settingSerial.SendBeforeHex))
				{
					binaryCommunication.SendBeforeHex = settingSerial.SendBeforeHex;
				}
			}
			return pipeSerialPort;
		}

		public PipeMoxa CreatePipeMoxa(BinaryCommunication binaryCommunication)
		{
			PipeMoxa pipeMoxa = new PipeMoxa();
			pipeMoxa.SerialPortInni(comboBox_com_port.Text, int.Parse(comboBox_com_baudrate.Text), int.Parse(textBox_com_databits.Text), GetSerialStopBits(), GetSerialParity());
			if (settingSerial != null)
			{
				pipeMoxa.SleepTime = settingSerial.SleepTime;
				if (!string.IsNullOrEmpty(settingSerial.SendBeforeHex))
				{
					binaryCommunication.SendBeforeHex = settingSerial.SendBeforeHex;
				}
			}
			return pipeMoxa;
		}

		public PipeMqttClient CreatePipeMqttClient()
		{
			MqttConnectionOptions mqttConnectionOptions = new MqttConnectionOptions();
			mqttConnectionOptions.IpAddress = textBox_mqtt_ip.Text;
			mqttConnectionOptions.Port = int.Parse(textBox_mqtt_port.Text);
			if (!string.IsNullOrEmpty(textBox_mqtt_name.Text))
			{
				mqttConnectionOptions.Credentials = new MqttCredential(textBox_mqtt_name.Text, (settingMqtt == null) ? string.Empty : settingMqtt.Password);
			}
			if (settingMqtt != null)
			{
				mqttConnectionOptions.ClientId = settingMqtt.ClientID;
				mqttConnectionOptions.UseRSAProvider = settingMqtt.UseRSAProvider;
			}
			MqttClient mqttClient = new MqttClient(mqttConnectionOptions);
			OperateResult operateResult = mqttClient.ConnectServer();
			if (!operateResult.IsSuccess)
			{
				throw new Exception("Connect mqtt server [" + textBox_mqtt_ip.Text + "] failed: " + operateResult.IsSuccess);
			}
			return new PipeMqttClient(mqttClient, settingMqtt.Device2Mqtt, settingMqtt.Mqtt2Device);
		}

		private void linkLabel_tcp_more_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormPropertyModify formPropertyModify = new FormPropertyModify())
			{
				if (SettingPipe == SettingPipe.Debug)
				{
					SettingTcpDebug @object = new SettingTcpDebug(settingTcpDebug ?? new SettingTcpDebug());
					formPropertyModify.SetObject(@object);
					if (formPropertyModify.ShowDialog() == DialogResult.OK)
					{
						settingTcpDebug = @object;
					}
				}
				else
				{
					SettingTcpIP object2 = new SettingTcpIP(settingTcpIP ?? new SettingTcpIP());
					formPropertyModify.SetObject(object2);
					if (formPropertyModify.ShowDialog() == DialogResult.OK)
					{
						settingTcpIP = object2;
					}
				}
			}
		}

		private void linkLabel_udp_more_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormPropertyModify formPropertyModify = new FormPropertyModify())
			{
				SettingTcpIP @object = new SettingTcpIP(settingUdpIP ?? new SettingTcpIP());
				formPropertyModify.SetObject(@object);
				if (formPropertyModify.ShowDialog() == DialogResult.OK)
				{
					settingUdpIP = @object;
				}
			}
		}

		private void linkLabel_mqtt_more_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormPropertyModify formPropertyModify = new FormPropertyModify())
			{
				SettingMqtt @object = new SettingMqtt(settingMqtt ?? new SettingMqtt());
				formPropertyModify.SetObject(@object);
				if (formPropertyModify.ShowDialog() == DialogResult.OK)
				{
					settingMqtt = @object;
				}
			}
		}

		private void linkLabel_serial_more_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormPropertyModify formPropertyModify = new FormPropertyModify())
			{
				SettingSerial @object = new SettingSerial(settingSerial ?? new SettingSerial());
				formPropertyModify.SetObject(@object);
				if (formPropertyModify.ShowDialog() == DialogResult.OK)
				{
					settingSerial = @object;
				}
			}
		}

		private void linkLabel_dtu_message_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormDtuMessageCreate formDtuMessageCreate = new FormDtuMessageCreate())
			{
				formDtuMessageCreate.SetDtuId(textBox_dtu_id.Text, textBox_dtu_pwd.Text);
				if (formDtuMessageCreate.ShowDialog() != DialogResult.OK)
				{
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
			this.panel_tcp = new System.Windows.Forms.Panel();
			this.textBox_tcp_receive = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.linkLabel_tcp_more = new System.Windows.Forms.LinkLabel();
			this.textBox_tcp_connectTimeout = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_tcp_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_tcp_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel_serial = new System.Windows.Forms.Panel();
			this.comboBox_com_baudrate = new System.Windows.Forms.ComboBox();
			this.linkLabel_serial_more = new System.Windows.Forms.LinkLabel();
			this.comboBox_com_port = new System.Windows.Forms.ComboBox();
			this.comboBox_com_parity = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_com_stopbits = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_com_databits = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.label26 = new System.Windows.Forms.Label();
			this.label27 = new System.Windows.Forms.Label();
			this.panel_udp = new System.Windows.Forms.Panel();
			this.linkLabel_udp_more = new System.Windows.Forms.LinkLabel();
			this.textBox_udp_receiveTimeout = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_udp_port = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_udp_ip = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.comboBox_pipe_select = new System.Windows.Forms.ComboBox();
			this.panel_mqtt = new System.Windows.Forms.Panel();
			this.linkLabel_mqtt_more = new System.Windows.Forms.LinkLabel();
			this.textBox_mqtt_name = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_mqtt_port = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox_mqtt_ip = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.label_info_pipe = new System.Windows.Forms.Label();
			this.panel_dtu = new System.Windows.Forms.Panel();
			this.checkBox_useRegister = new System.Windows.Forms.CheckBox();
			this.linkLabel_dtu_message = new System.Windows.Forms.LinkLabel();
			this.textBox_dtu_pwd = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.checkBox_dtu_back = new System.Windows.Forms.CheckBox();
			this.textBox_dtu_id = new System.Windows.Forms.TextBox();
			this.textBox_dtu_port = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.linkLabel_dtu_state = new System.Windows.Forms.LinkLabel();
			this.panel_tcp.SuspendLayout();
			this.panel_serial.SuspendLayout();
			this.panel_udp.SuspendLayout();
			this.panel_mqtt.SuspendLayout();
			this.panel_dtu.SuspendLayout();
			base.SuspendLayout();
			this.panel_tcp.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_tcp.Controls.Add(this.textBox_tcp_receive);
			this.panel_tcp.Controls.Add(this.label4);
			this.panel_tcp.Controls.Add(this.linkLabel_tcp_more);
			this.panel_tcp.Controls.Add(this.textBox_tcp_connectTimeout);
			this.panel_tcp.Controls.Add(this.label2);
			this.panel_tcp.Controls.Add(this.textBox_tcp_port);
			this.panel_tcp.Controls.Add(this.label3);
			this.panel_tcp.Controls.Add(this.textBox_tcp_ip);
			this.panel_tcp.Controls.Add(this.label1);
			this.panel_tcp.Location = new System.Drawing.Point(0, 0);
			this.panel_tcp.Name = "panel_tcp";
			this.panel_tcp.Size = new System.Drawing.Size(679, 29);
			this.panel_tcp.TabIndex = 0;
			this.textBox_tcp_receive.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_tcp_receive.Location = new System.Drawing.Point(582, 2);
			this.textBox_tcp_receive.Name = "textBox_tcp_receive";
			this.textBox_tcp_receive.Size = new System.Drawing.Size(50, 23);
			this.textBox_tcp_receive.TabIndex = 47;
			this.textBox_tcp_receive.Text = "10000";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(509, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 46;
			this.label4.Text = "接收超时：";
			this.linkLabel_tcp_more.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_tcp_more.AutoSize = true;
			this.linkLabel_tcp_more.Location = new System.Drawing.Point(635, 6);
			this.linkLabel_tcp_more.Name = "linkLabel_tcp_more";
			this.linkLabel_tcp_more.Size = new System.Drawing.Size(39, 17);
			this.linkLabel_tcp_more.TabIndex = 45;
			this.linkLabel_tcp_more.TabStop = true;
			this.linkLabel_tcp_more.Text = "more";
			this.linkLabel_tcp_more.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel_tcp_more_LinkClicked);
			this.textBox_tcp_connectTimeout.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_tcp_connectTimeout.Location = new System.Drawing.Point(452, 2);
			this.textBox_tcp_connectTimeout.Name = "textBox_tcp_connectTimeout";
			this.textBox_tcp_connectTimeout.Size = new System.Drawing.Size(50, 23);
			this.textBox_tcp_connectTimeout.TabIndex = 9;
			this.textBox_tcp_connectTimeout.Text = "5000";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(379, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 8;
			this.label2.Text = "连接超时：";
			this.textBox_tcp_port.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_tcp_port.Location = new System.Drawing.Point(325, 2);
			this.textBox_tcp_port.Name = "textBox_tcp_port";
			this.textBox_tcp_port.Size = new System.Drawing.Size(50, 23);
			this.textBox_tcp_port.TabIndex = 7;
			this.textBox_tcp_port.Text = "6000";
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(261, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 6;
			this.label3.Text = "端口号：";
			this.textBox_tcp_ip.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_tcp_ip.Location = new System.Drawing.Point(59, 3);
			this.textBox_tcp_ip.Name = "textBox_tcp_ip";
			this.textBox_tcp_ip.Size = new System.Drawing.Size(196, 23);
			this.textBox_tcp_ip.TabIndex = 5;
			this.textBox_tcp_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 4;
			this.label1.Text = "Ip地址：";
			this.panel_serial.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_serial.Controls.Add(this.comboBox_com_baudrate);
			this.panel_serial.Controls.Add(this.linkLabel_serial_more);
			this.panel_serial.Controls.Add(this.comboBox_com_port);
			this.panel_serial.Controls.Add(this.comboBox_com_parity);
			this.panel_serial.Controls.Add(this.label5);
			this.panel_serial.Controls.Add(this.textBox_com_stopbits);
			this.panel_serial.Controls.Add(this.label6);
			this.panel_serial.Controls.Add(this.textBox_com_databits);
			this.panel_serial.Controls.Add(this.label25);
			this.panel_serial.Controls.Add(this.label26);
			this.panel_serial.Controls.Add(this.label27);
			this.panel_serial.Location = new System.Drawing.Point(0, 72);
			this.panel_serial.Name = "panel_serial";
			this.panel_serial.Size = new System.Drawing.Size(679, 29);
			this.panel_serial.TabIndex = 1;
			this.comboBox_com_baudrate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_com_baudrate.FormattingEnabled = true;
			this.comboBox_com_baudrate.Items.AddRange(new object[8] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
			this.comboBox_com_baudrate.Location = new System.Drawing.Point(253, 2);
			this.comboBox_com_baudrate.Name = "comboBox_com_baudrate";
			this.comboBox_com_baudrate.Size = new System.Drawing.Size(80, 25);
			this.comboBox_com_baudrate.TabIndex = 46;
			this.comboBox_com_baudrate.Text = "9600";
			this.linkLabel_serial_more.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_serial_more.AutoSize = true;
			this.linkLabel_serial_more.Location = new System.Drawing.Point(635, 5);
			this.linkLabel_serial_more.Name = "linkLabel_serial_more";
			this.linkLabel_serial_more.Size = new System.Drawing.Size(39, 17);
			this.linkLabel_serial_more.TabIndex = 45;
			this.linkLabel_serial_more.TabStop = true;
			this.linkLabel_serial_more.Text = "more";
			this.linkLabel_serial_more.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel_serial_more_LinkClicked);
			this.comboBox_com_port.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_com_port.FormattingEnabled = true;
			this.comboBox_com_port.Location = new System.Drawing.Point(60, 2);
			this.comboBox_com_port.Name = "comboBox_com_port";
			this.comboBox_com_port.Size = new System.Drawing.Size(119, 25);
			this.comboBox_com_port.TabIndex = 41;
			this.comboBox_com_parity.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_com_parity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_com_parity.FormattingEnabled = true;
			this.comboBox_com_parity.Items.AddRange(new object[3] { "无", "奇", "偶" });
			this.comboBox_com_parity.Location = new System.Drawing.Point(568, 2);
			this.comboBox_com_parity.Name = "comboBox_com_parity";
			this.comboBox_com_parity.Size = new System.Drawing.Size(63, 25);
			this.comboBox_com_parity.TabIndex = 40;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(521, 6);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(44, 17);
			this.label5.TabIndex = 39;
			this.label5.Text = "奇偶：";
			this.textBox_com_stopbits.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_com_stopbits.Location = new System.Drawing.Point(489, 3);
			this.textBox_com_stopbits.Name = "textBox_com_stopbits";
			this.textBox_com_stopbits.Size = new System.Drawing.Size(25, 23);
			this.textBox_com_stopbits.TabIndex = 38;
			this.textBox_com_stopbits.Text = "1";
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(433, 6);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(56, 17);
			this.label6.TabIndex = 37;
			this.label6.Text = "停止位：";
			this.textBox_com_databits.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_com_databits.Location = new System.Drawing.Point(402, 3);
			this.textBox_com_databits.Name = "textBox_com_databits";
			this.textBox_com_databits.Size = new System.Drawing.Size(24, 23);
			this.textBox_com_databits.TabIndex = 36;
			this.textBox_com_databits.Text = "8";
			this.label25.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(342, 6);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(56, 17);
			this.label25.TabIndex = 35;
			this.label25.Text = "数据位：";
			this.label26.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(186, 6);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(56, 17);
			this.label26.TabIndex = 33;
			this.label26.Text = "波特率：";
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(3, 6);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(59, 17);
			this.label27.TabIndex = 32;
			this.label27.Text = "Com口：";
			this.panel_udp.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_udp.Controls.Add(this.linkLabel_udp_more);
			this.panel_udp.Controls.Add(this.textBox_udp_receiveTimeout);
			this.panel_udp.Controls.Add(this.label7);
			this.panel_udp.Controls.Add(this.textBox_udp_port);
			this.panel_udp.Controls.Add(this.label9);
			this.panel_udp.Controls.Add(this.textBox_udp_ip);
			this.panel_udp.Controls.Add(this.label10);
			this.panel_udp.Location = new System.Drawing.Point(0, 36);
			this.panel_udp.Name = "panel_udp";
			this.panel_udp.Size = new System.Drawing.Size(679, 29);
			this.panel_udp.TabIndex = 2;
			this.linkLabel_udp_more.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_udp_more.AutoSize = true;
			this.linkLabel_udp_more.Location = new System.Drawing.Point(635, 5);
			this.linkLabel_udp_more.Name = "linkLabel_udp_more";
			this.linkLabel_udp_more.Size = new System.Drawing.Size(39, 17);
			this.linkLabel_udp_more.TabIndex = 44;
			this.linkLabel_udp_more.TabStop = true;
			this.linkLabel_udp_more.Text = "more";
			this.linkLabel_udp_more.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel_udp_more_LinkClicked);
			this.textBox_udp_receiveTimeout.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_udp_receiveTimeout.Location = new System.Drawing.Point(582, 2);
			this.textBox_udp_receiveTimeout.Name = "textBox_udp_receiveTimeout";
			this.textBox_udp_receiveTimeout.Size = new System.Drawing.Size(50, 23);
			this.textBox_udp_receiveTimeout.TabIndex = 11;
			this.textBox_udp_receiveTimeout.Text = "10000";
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(509, 6);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 10;
			this.label7.Text = "接收超时：";
			this.textBox_udp_port.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_udp_port.Location = new System.Drawing.Point(455, 2);
			this.textBox_udp_port.Name = "textBox_udp_port";
			this.textBox_udp_port.Size = new System.Drawing.Size(50, 23);
			this.textBox_udp_port.TabIndex = 7;
			this.textBox_udp_port.Text = "6000";
			this.label9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(391, 6);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(56, 17);
			this.label9.TabIndex = 6;
			this.label9.Text = "端口号：";
			this.textBox_udp_ip.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_udp_ip.Location = new System.Drawing.Point(59, 3);
			this.textBox_udp_ip.Name = "textBox_udp_ip";
			this.textBox_udp_ip.Size = new System.Drawing.Size(326, 23);
			this.textBox_udp_ip.TabIndex = 5;
			this.textBox_udp_ip.Text = "127.0.0.1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(3, 5);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(56, 17);
			this.label10.TabIndex = 4;
			this.label10.Text = "Ip地址：";
			this.comboBox_pipe_select.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_pipe_select.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_pipe_select.FormattingEnabled = true;
			this.comboBox_pipe_select.Location = new System.Drawing.Point(715, 2);
			this.comboBox_pipe_select.Name = "comboBox_pipe_select";
			this.comboBox_pipe_select.Size = new System.Drawing.Size(73, 25);
			this.comboBox_pipe_select.TabIndex = 3;
			this.panel_mqtt.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_mqtt.Controls.Add(this.linkLabel_mqtt_more);
			this.panel_mqtt.Controls.Add(this.textBox_mqtt_name);
			this.panel_mqtt.Controls.Add(this.label12);
			this.panel_mqtt.Controls.Add(this.textBox_mqtt_port);
			this.panel_mqtt.Controls.Add(this.label14);
			this.panel_mqtt.Controls.Add(this.textBox_mqtt_ip);
			this.panel_mqtt.Controls.Add(this.label15);
			this.panel_mqtt.Location = new System.Drawing.Point(0, 109);
			this.panel_mqtt.Name = "panel_mqtt";
			this.panel_mqtt.Size = new System.Drawing.Size(679, 29);
			this.panel_mqtt.TabIndex = 4;
			this.linkLabel_mqtt_more.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_mqtt_more.AutoSize = true;
			this.linkLabel_mqtt_more.Location = new System.Drawing.Point(635, 6);
			this.linkLabel_mqtt_more.Name = "linkLabel_mqtt_more";
			this.linkLabel_mqtt_more.Size = new System.Drawing.Size(39, 17);
			this.linkLabel_mqtt_more.TabIndex = 44;
			this.linkLabel_mqtt_more.TabStop = true;
			this.linkLabel_mqtt_more.Text = "more";
			this.linkLabel_mqtt_more.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel_mqtt_more_LinkClicked);
			this.textBox_mqtt_name.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_mqtt_name.Location = new System.Drawing.Point(515, 3);
			this.textBox_mqtt_name.Name = "textBox_mqtt_name";
			this.textBox_mqtt_name.Size = new System.Drawing.Size(117, 23);
			this.textBox_mqtt_name.TabIndex = 9;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(454, 6);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(56, 17);
			this.label12.TabIndex = 8;
			this.label12.Text = "用户名：";
			this.textBox_mqtt_port.Location = new System.Drawing.Point(370, 2);
			this.textBox_mqtt_port.Name = "textBox_mqtt_port";
			this.textBox_mqtt_port.Size = new System.Drawing.Size(76, 23);
			this.textBox_mqtt_port.TabIndex = 7;
			this.textBox_mqtt_port.Text = "1883";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(306, 6);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(56, 17);
			this.label14.TabIndex = 6;
			this.label14.Text = "端口号：";
			this.textBox_mqtt_ip.Location = new System.Drawing.Point(59, 3);
			this.textBox_mqtt_ip.Name = "textBox_mqtt_ip";
			this.textBox_mqtt_ip.Size = new System.Drawing.Size(246, 23);
			this.textBox_mqtt_ip.TabIndex = 5;
			this.textBox_mqtt_ip.Text = "127.0.0.1";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(3, 5);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(56, 17);
			this.label15.TabIndex = 4;
			this.label15.Text = "Ip地址：";
			this.label_info_pipe.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_info_pipe.AutoSize = true;
			this.label_info_pipe.ForeColor = System.Drawing.Color.MediumOrchid;
			this.label_info_pipe.Location = new System.Drawing.Point(679, 6);
			this.label_info_pipe.Name = "label_info_pipe";
			this.label_info_pipe.Size = new System.Drawing.Size(32, 17);
			this.label_info_pipe.TabIndex = 7;
			this.label_info_pipe.Text = "管道";
			this.panel_dtu.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_dtu.Controls.Add(this.checkBox_useRegister);
			this.panel_dtu.Controls.Add(this.linkLabel_dtu_message);
			this.panel_dtu.Controls.Add(this.textBox_dtu_pwd);
			this.panel_dtu.Controls.Add(this.label13);
			this.panel_dtu.Controls.Add(this.checkBox_dtu_back);
			this.panel_dtu.Controls.Add(this.textBox_dtu_id);
			this.panel_dtu.Controls.Add(this.textBox_dtu_port);
			this.panel_dtu.Controls.Add(this.label11);
			this.panel_dtu.Controls.Add(this.label8);
			this.panel_dtu.Controls.Add(this.linkLabel_dtu_state);
			this.panel_dtu.Location = new System.Drawing.Point(0, 145);
			this.panel_dtu.Name = "panel_dtu";
			this.panel_dtu.Size = new System.Drawing.Size(679, 29);
			this.panel_dtu.TabIndex = 8;
			this.checkBox_useRegister.AutoSize = true;
			this.checkBox_useRegister.Checked = true;
			this.checkBox_useRegister.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_useRegister.Location = new System.Drawing.Point(114, 5);
			this.checkBox_useRegister.Name = "checkBox_useRegister";
			this.checkBox_useRegister.Size = new System.Drawing.Size(63, 21);
			this.checkBox_useRegister.TabIndex = 50;
			this.checkBox_useRegister.Text = "注册包";
			this.checkBox_useRegister.UseVisualStyleBackColor = true;
			this.linkLabel_dtu_message.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_dtu_message.AutoSize = true;
			this.linkLabel_dtu_message.Location = new System.Drawing.Point(632, 6);
			this.linkLabel_dtu_message.Name = "linkLabel_dtu_message";
			this.linkLabel_dtu_message.Size = new System.Drawing.Size(44, 17);
			this.linkLabel_dtu_message.TabIndex = 49;
			this.linkLabel_dtu_message.TabStop = true;
			this.linkLabel_dtu_message.Text = "注册包";
			this.linkLabel_dtu_message.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel_dtu_message_LinkClicked);
			this.textBox_dtu_pwd.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_dtu_pwd.Location = new System.Drawing.Point(462, 3);
			this.textBox_dtu_pwd.Name = "textBox_dtu_pwd";
			this.textBox_dtu_pwd.Size = new System.Drawing.Size(58, 23);
			this.textBox_dtu_pwd.TabIndex = 47;
			this.textBox_dtu_pwd.Text = "123456";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(416, 6);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 48;
			this.label13.Text = "密码：";
			this.checkBox_dtu_back.AutoSize = true;
			this.checkBox_dtu_back.Checked = true;
			this.checkBox_dtu_back.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_dtu_back.Location = new System.Drawing.Point(185, 5);
			this.checkBox_dtu_back.Name = "checkBox_dtu_back";
			this.checkBox_dtu_back.Size = new System.Drawing.Size(99, 21);
			this.checkBox_dtu_back.TabIndex = 45;
			this.checkBox_dtu_back.Text = "返回注册结果";
			this.checkBox_dtu_back.UseVisualStyleBackColor = true;
			this.textBox_dtu_id.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_dtu_id.Location = new System.Drawing.Point(321, 3);
			this.textBox_dtu_id.Name = "textBox_dtu_id";
			this.textBox_dtu_id.Size = new System.Drawing.Size(90, 23);
			this.textBox_dtu_id.TabIndex = 9;
			this.textBox_dtu_id.Text = "12345678901";
			this.textBox_dtu_port.Location = new System.Drawing.Point(58, 3);
			this.textBox_dtu_port.Name = "textBox_dtu_port";
			this.textBox_dtu_port.Size = new System.Drawing.Size(53, 23);
			this.textBox_dtu_port.TabIndex = 7;
			this.textBox_dtu_port.Text = "10010";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(2, 7);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(56, 17);
			this.label11.TabIndex = 6;
			this.label11.Text = "端口号：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(292, 6);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(24, 17);
			this.label8.TabIndex = 46;
			this.label8.Text = "ID:";
			this.linkLabel_dtu_state.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_dtu_state.LinkColor = System.Drawing.Color.Gray;
			this.linkLabel_dtu_state.Location = new System.Drawing.Point(523, 6);
			this.linkLabel_dtu_state.Name = "linkLabel_dtu_state";
			this.linkLabel_dtu_state.Size = new System.Drawing.Size(110, 17);
			this.linkLabel_dtu_state.TabIndex = 44;
			this.linkLabel_dtu_state.TabStop = true;
			this.linkLabel_dtu_state.Text = "Stopped";
			this.linkLabel_dtu_state.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label_info_pipe);
			base.Controls.Add(this.panel_dtu);
			base.Controls.Add(this.panel_mqtt);
			base.Controls.Add(this.comboBox_pipe_select);
			base.Controls.Add(this.panel_udp);
			base.Controls.Add(this.panel_serial);
			base.Controls.Add(this.panel_tcp);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "PipeSelectControl";
			base.Size = new System.Drawing.Size(790, 192);
			base.Load += new System.EventHandler(PipeSelectControl_Load);
			this.panel_tcp.ResumeLayout(false);
			this.panel_tcp.PerformLayout();
			this.panel_serial.ResumeLayout(false);
			this.panel_serial.PerformLayout();
			this.panel_udp.ResumeLayout(false);
			this.panel_udp.PerformLayout();
			this.panel_mqtt.ResumeLayout(false);
			this.panel_mqtt.PerformLayout();
			this.panel_dtu.ResumeLayout(false);
			this.panel_dtu.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
