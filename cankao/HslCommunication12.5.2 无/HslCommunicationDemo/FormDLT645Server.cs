using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core.Net;
using HslCommunication.Instrument.DLT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Instrument;

namespace HslCommunicationDemo
{
	public class FormDLT645Server : HslFormContent
	{
		private DLT645Server dLT645Server;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private CheckBox checkBox_string_reverse;

		private UserControlHead userControlHead1;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private TextBox textBox1;

		private Label label1;

		private CheckBox checkBox_enableFE;

		private CheckBox checkBox_station_match;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		public FormDLT645Server()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
			checkBox_station_match.CheckedChanged += CheckBox_station_match_CheckedChanged;
			checkBox_enableFE.CheckedChanged += CheckBox_enableFE_CheckedChanged;
			checkBox_string_reverse.CheckedChanged += CheckBox3_CheckedChanged;
		}

		private void CheckBox_enableFE_CheckedChanged(object sender, EventArgs e)
		{
			if (dLT645Server != null)
			{
				dLT645Server.EnableCodeFE = checkBox_enableFE.Checked;
			}
		}

		private void CheckBox_station_match_CheckedChanged(object sender, EventArgs e)
		{
			if (dLT645Server != null)
			{
				dLT645Server.StationMatch = checkBox_station_match.Checked;
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (dLT645Server != null)
			{
				dLT645Server.StringReverse = checkBox_string_reverse.Checked;
			}
		}

		private void FormDLT645Server_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "DLT645 Virtual Server[supports TCP and Serial";
				checkBox_string_reverse.Text = "str-reverse";
			}
			else
			{
				checkBox_station_match.Text = "是否站号匹配";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(DLTHelper.GetDlt645Address());
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = button5_Click;
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!serverSettingControl1.ButtonStart.Enabled)
			{
				button11_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				dLT645Server = new DLT645Server();
				dLT645Server.Station = textBox1.Text;
				dLT645Server.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				dLT645Server.OnDataReceived += BusTcpServer_OnDataReceived;
				dLT645Server.StringReverse = checkBox_string_reverse.Checked;
				dLT645Server.EnableCodeFE = checkBox_enableFE.Checked;
				dLT645Server.StationMatch = checkBox_station_match.Checked;
				sslServerControl1.InitializeServer(dLT645Server);
				if (serverSettingControl1.ServerStart(dLT645Server))
				{
					userControlReadWriteServer1.SetReadWriteServer(dLT645Server, "02-01-01-00");
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("server", "", dLT645Server, sslServerControl1, "Station", "StringReverse", "EnableCodeFE", "StationMatch");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			dLT645Server?.CloseSerialSlave();
			dLT645Server?.ServerClose();
		}

		private void BusTcpServer_OnDataReceived(object sender, object source, byte[] modbus)
		{
			if (source is AppSession appSession)
			{
				string ipAddress = appSession.IpAddress;
			}
			if (source is SerialPort serialPort)
			{
				string portName = serialPort.PortName;
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (dLT645Server != null)
			{
				try
				{
					OperateResult operateResult = dLT645Server.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("Start Failed：" + operateResult.Message);
					}
					else
					{
						serverSettingControl1.ButtonSerial.Enabled = false;
						codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, dLT645Server, sslServerControl1, "Station", "StringReverse", "EnableCodeFE", "StationMatch");
					}
					return;
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Start Failed：" + ex.Message);
					return;
				}
			}
			DemoUtils.ShowMessage("Start tcp server first please!");
		}

		public override void SaveXmlParameter(XElement element)
		{
			sslServerControl1.SaveXmlParameter(element);
			serverSettingControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox_string_reverse.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox1.Text);
			element.SetAttributeValue("EnableFE", checkBox_enableFE.Checked);
			element.SetAttributeValue("StationMatch", checkBox_station_match.Checked);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			checkBox_string_reverse.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
			textBox1.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStation, "1", (string m) => m);
			checkBox_enableFE.Checked = HslFormContent.GetXmlValue(element, "EnableFE", false, bool.Parse);
			checkBox_station_match.Checked = HslFormContent.GetXmlValue(element, "StationMatch", false, bool.Parse);
			userControlReadWriteServer1.LoadDataTable(element);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void panel1_Paint(object sender, PaintEventArgs e)
		{
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
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.checkBox_station_match = new System.Windows.Forms.CheckBox();
			this.checkBox_enableFE = new System.Windows.Forms.CheckBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox_string_reverse = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.checkBox_station_match);
			this.panel1.Controls.Add(this.checkBox_enableFE);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.checkBox_string_reverse);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 89);
			this.panel1.TabIndex = 0;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(0, 57);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(992, 30);
			this.sslServerControl1.TabIndex = 33;
			this.checkBox_station_match.AutoSize = true;
			this.checkBox_station_match.Location = new System.Drawing.Point(233, 33);
			this.checkBox_station_match.Name = "checkBox_station_match";
			this.checkBox_station_match.Size = new System.Drawing.Size(107, 21);
			this.checkBox_station_match.TabIndex = 32;
			this.checkBox_station_match.Text = "Station Match";
			this.checkBox_station_match.UseVisualStyleBackColor = true;
			this.checkBox_enableFE.AutoSize = true;
			this.checkBox_enableFE.Location = new System.Drawing.Point(464, 33);
			this.checkBox_enableFE.Name = "checkBox_enableFE";
			this.checkBox_enableFE.Size = new System.Drawing.Size(116, 21);
			this.checkBox_enableFE.TabIndex = 31;
			this.checkBox_enableFE.Text = "Enable FE code";
			this.checkBox_enableFE.UseVisualStyleBackColor = true;
			this.textBox1.Location = new System.Drawing.Point(61, 31);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(166, 23);
			this.textBox1.TabIndex = 30;
			this.textBox1.Text = "1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(5, 34);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 29;
			this.label1.Text = "站号：";
			this.checkBox_string_reverse.AutoSize = true;
			this.checkBox_string_reverse.Location = new System.Drawing.Point(358, 33);
			this.checkBox_string_reverse.Name = "checkBox_string_reverse";
			this.checkBox_string_reverse.Size = new System.Drawing.Size(87, 21);
			this.checkBox_string_reverse.TabIndex = 27;
			this.checkBox_string_reverse.Text = "字符串颠倒";
			this.checkBox_string_reverse.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 127);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 516);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(995, 514);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7782315.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "DLT645 Tcp + Serial";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(2, 2);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(986, 30);
			this.serverSettingControl1.TabIndex = 34;
			this.serverSettingControl1.TextPort = "2000";
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-E-1";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormDLT645Server";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "DLT645虚拟PLC【支持网口和串口】";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormDLT645Server_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
