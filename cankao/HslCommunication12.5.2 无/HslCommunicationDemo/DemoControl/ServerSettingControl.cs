using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Device;

namespace HslCommunicationDemo.DemoControl
{
	public class ServerSettingControl : UserControl
	{
		private ToolTip toolTip1;

		private IContainer components = null;

		private TextBox textBox_port;

		private Label label3;

		private ComboBox comboBox_tcp_udp;

		private ComboBox comboBox_ipv4;

		private TextBox textBox_local_ip;

		private Label label4;

		private Button button_close;

		private Button button_start_sertial;

		private TextBox textBox_serialPort;

		private Label label14;

		private Button button_start;

		[Browsable(false)]
		public Button ButtonStart => button_start;

		[Browsable(false)]
		public Button ButtonClose => button_close;

		[Browsable(false)]
		public Button ButtonSerial => button_start_sertial;

		[Browsable(false)]
		public TextBox TextBox_Serial => textBox_serialPort;

		[Description("获取或设置当前的端口号信息")]
		[DefaultValue("502")]
		public string TextPort
		{
			get
			{
				return textBox_port.Text;
			}
			set
			{
				textBox_port.Text = value;
			}
		}

		[Description("获取或设置使用TCP,UDP,还是都使用")]
		[DefaultValue(0)]
		public int TcpUdpIndex
		{
			get
			{
				return comboBox_tcp_udp.SelectedIndex;
			}
			set
			{
				comboBox_tcp_udp.SelectedIndex = value;
			}
		}

		[Description("获取或设置当前是否使用IPv4，还是IPv6")]
		[DefaultValue(0)]
		public int IpV4V6
		{
			get
			{
				return comboBox_ipv4.SelectedIndex;
			}
			set
			{
				comboBox_ipv4.SelectedIndex = value;
			}
		}

		[Description("当前串口的默认信息")]
		public string TextSerialInfo
		{
			get
			{
				return textBox_serialPort.Text;
			}
			set
			{
				textBox_serialPort.Text = value;
			}
		}

		public Action<object, EventArgs> buttonStartAction { get; set; }

		public Action<object, EventArgs> buttonCloseAction { get; set; }

		public Action<object, EventArgs> buttonSerialAction { get; set; }

		public ServerSettingControl()
		{
			InitializeComponent();
			comboBox_tcp_udp.SelectedIndex = 0;
			comboBox_ipv4.SelectedIndex = 0;
			toolTip1 = new ToolTip();
			toolTip1.SetToolTip(label3, "支持输入两个端口(6000:6001)，同时开启TCP及UDP，如果只输入一个端口号(BOTH)的话，UDP端口就是TCP+1");
		}

		private void ServerSettingControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label3.Text = "Port:";
				label4.Text = "Ip:";
				button_start.Text = "Start";
				button_close.Text = "Close";
				label14.Text = "Serial:";
				button_start_sertial.Text = "Start";
			}
			button_close.Enabled = false;
			button_start_sertial.Enabled = false;
		}

		public bool ServerStart(DeviceServer deviceServer)
		{
			int result = 0;
			int result2 = 0;
			if (textBox_port.Text.Contains(";") || textBox_port.Text.Contains(":") || textBox_port.Text.Contains("-"))
			{
				string[] array = textBox_port.Text.Split(new char[3] { ';', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length >= 1)
				{
					if (!int.TryParse(array[0], out result))
					{
						DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
						return false;
					}
					if (!int.TryParse(array[1], out result2))
					{
						DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
						return false;
					}
				}
			}
			else
			{
				if (!int.TryParse(textBox_port.Text, out result))
				{
					DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
					return false;
				}
				result2 = result + 1;
			}
			deviceServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
			deviceServer.EnableIPv6 = comboBox_ipv4.SelectedIndex == 1;
			if (!string.IsNullOrEmpty(textBox_local_ip.Text.Trim()))
			{
				deviceServer.LocalAddress = IPAddress.Parse(textBox_local_ip.Text);
			}
			if (comboBox_tcp_udp.SelectedIndex == 0)
			{
				deviceServer.ServerStart(result);
			}
			else if (comboBox_tcp_udp.SelectedIndex == 1)
			{
				deviceServer.ServerStart(result, false);
			}
			else
			{
				deviceServer.ServerStart(result, result2);
			}
			button_start.Enabled = false;
			button_close.Enabled = true;
			button_start_sertial.Enabled = true;
			return true;
		}

		private void button_start_Click(object sender, EventArgs e)
		{
			buttonStartAction?.Invoke(sender, e);
		}

		private void button_close_Click(object sender, EventArgs e)
		{
			buttonCloseAction?.Invoke(sender, e);
			button_start.Enabled = true;
			button_close.Enabled = false;
			button_start_sertial.Enabled = false;
		}

		private void button_start_sertial_Click(object sender, EventArgs e)
		{
			buttonSerialAction?.Invoke(sender, e);
		}

		public void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_port.Text);
			element.SetAttributeValue("TcpUdpBoth", comboBox_tcp_udp.SelectedIndex);
			element.SetAttributeValue("IpV4V6", comboBox_ipv4.SelectedIndex);
			element.SetAttributeValue("LocalIp", textBox_local_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCom, textBox_serialPort.Text);
		}

		public void LoadXmlParameter(XElement element)
		{
			textBox_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, textBox_port.Text, (string m) => m);
			comboBox_tcp_udp.SelectedIndex = HslFormContent.GetXmlValue(element, "TcpUdpBoth", 0, int.Parse);
			comboBox_ipv4.SelectedIndex = HslFormContent.GetXmlValue(element, "IpV4V6", 0, int.Parse);
			textBox_local_ip.Text = HslFormContent.GetXmlValue(element, "LocalIp", "", (string m) => m);
			textBox_serialPort.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlCom, textBox_serialPort.Text, (string m) => m);
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
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.comboBox_tcp_udp = new System.Windows.Forms.ComboBox();
			this.comboBox_ipv4 = new System.Windows.Forms.ComboBox();
			this.textBox_local_ip = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.button_close = new System.Windows.Forms.Button();
			this.button_start_sertial = new System.Windows.Forms.Button();
			this.textBox_serialPort = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.button_start = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.textBox_port.Location = new System.Drawing.Point(60, 2);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(56, 23);
			this.textBox_port.TabIndex = 5;
			this.textBox_port.Text = "502";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3, 5);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "端口号：";
			this.comboBox_tcp_udp.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_tcp_udp.FormattingEnabled = true;
			this.comboBox_tcp_udp.Items.AddRange(new object[3] { "TCP", "UDP", "Both" });
			this.comboBox_tcp_udp.Location = new System.Drawing.Point(121, 1);
			this.comboBox_tcp_udp.Name = "comboBox_tcp_udp";
			this.comboBox_tcp_udp.Size = new System.Drawing.Size(60, 25);
			this.comboBox_tcp_udp.TabIndex = 6;
			this.comboBox_ipv4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_ipv4.FormattingEnabled = true;
			this.comboBox_ipv4.Items.AddRange(new object[2] { "IPv4", "IPv6" });
			this.comboBox_ipv4.Location = new System.Drawing.Point(187, 1);
			this.comboBox_ipv4.Name = "comboBox_ipv4";
			this.comboBox_ipv4.Size = new System.Drawing.Size(60, 25);
			this.comboBox_ipv4.TabIndex = 7;
			this.textBox_local_ip.Location = new System.Drawing.Point(310, 2);
			this.textBox_local_ip.Name = "textBox_local_ip";
			this.textBox_local_ip.Size = new System.Drawing.Size(112, 23);
			this.textBox_local_ip.TabIndex = 48;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(253, 5);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(55, 17);
			this.label4.TabIndex = 47;
			this.label4.Text = "本地ip：";
			this.button_close.Enabled = false;
			this.button_close.Location = new System.Drawing.Point(518, 0);
			this.button_close.Name = "button_close";
			this.button_close.Size = new System.Drawing.Size(83, 28);
			this.button_close.TabIndex = 53;
			this.button_close.Text = "关闭服务";
			this.button_close.UseVisualStyleBackColor = true;
			this.button_close.Click += new System.EventHandler(button_close_Click);
			this.button_start_sertial.Location = new System.Drawing.Point(807, 0);
			this.button_start_sertial.Name = "button_start_sertial";
			this.button_start_sertial.Size = new System.Drawing.Size(91, 28);
			this.button_start_sertial.TabIndex = 52;
			this.button_start_sertial.Text = "启动串口";
			this.button_start_sertial.UseVisualStyleBackColor = true;
			this.button_start_sertial.Click += new System.EventHandler(button_start_sertial_Click);
			this.textBox_serialPort.Location = new System.Drawing.Point(660, 3);
			this.textBox_serialPort.Name = "textBox_serialPort";
			this.textBox_serialPort.Size = new System.Drawing.Size(144, 23);
			this.textBox_serialPort.TabIndex = 51;
			this.textBox_serialPort.Text = "COM4-9600-8-N-1";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(606, 6);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(44, 17);
			this.label14.TabIndex = 50;
			this.label14.Text = "串口：";
			this.button_start.Location = new System.Drawing.Point(428, 0);
			this.button_start.Name = "button_start";
			this.button_start.Size = new System.Drawing.Size(83, 28);
			this.button_start.TabIndex = 49;
			this.button_start.Text = "启动服务";
			this.button_start.UseVisualStyleBackColor = true;
			this.button_start.Click += new System.EventHandler(button_start_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.button_close);
			base.Controls.Add(this.button_start_sertial);
			base.Controls.Add(this.textBox_serialPort);
			base.Controls.Add(this.label14);
			base.Controls.Add(this.button_start);
			base.Controls.Add(this.textBox_local_ip);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.comboBox_ipv4);
			base.Controls.Add(this.comboBox_tcp_udp);
			base.Controls.Add(this.textBox_port);
			base.Controls.Add(this.label3);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ServerSettingControl";
			base.Size = new System.Drawing.Size(904, 30);
			base.Load += new System.EventHandler(ServerSettingControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
