using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.ModBus;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Modbus;

namespace HslCommunicationDemo
{
	public class FormModbusServer : HslFormContent
	{
		private ModbusTcpServer busTcpServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private CheckBox checkBox3;

		private ComboBox comboBox2;

		private UserControlHead userControlHead1;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private CheckBox checkBox_remote_write;

		private CheckBox checkBox_RtuOverTcp;

		private TextBox textBox_time_min;

		private Label label2;

		private CheckBox checkBox_station_isolation;

		private TextBox textBox_station;

		private Label label1;

		private CheckBox checkBox_forceReceiveOnce;

		private CheckBox checkBox_maskcode;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		public FormModbusServer()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox2.SelectedIndex = 2;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			checkBox_remote_write.CheckedChanged += CheckBox1_CheckedChanged;
			checkBox3.CheckedChanged += CheckBox3_CheckedChanged;
			checkBox_maskcode.CheckedChanged += CheckBox_maskcode_CheckedChanged;
			if (Program.Language == 2)
			{
				Text = "Modbus Virtual Server";
				checkBox3.Text = "str-reverse";
				checkBox_remote_write.Text = "Remote write";
				checkBox_RtuOverTcp.Text = "Rtu Msg";
				label2.Text = "Least Time:";
			}
			else
			{
				checkBox_station_isolation.Text = "站号数据隔离";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetModbusAddressExamples().RemoveLast(4));
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = button5_Click;
		}

		private void CheckBox_maskcode_CheckedChanged(object sender, EventArgs e)
		{
			if (busTcpServer != null)
			{
				busTcpServer.EnableWriteMaskCode = checkBox_maskcode.Checked;
			}
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (busTcpServer != null)
			{
				busTcpServer.EnableWrite = checkBox_remote_write.Checked;
			}
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (busTcpServer != null)
			{
				switch (comboBox2.SelectedIndex)
				{
				case 0:
					busTcpServer.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					busTcpServer.DataFormat = DataFormat.BADC;
					break;
				case 2:
					busTcpServer.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					busTcpServer.DataFormat = DataFormat.DCBA;
					break;
				}
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (busTcpServer != null)
			{
				busTcpServer.IsStringReverse = checkBox3.Checked;
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			busTcpServer?.ServerClose();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!byte.TryParse(textBox_station.Text, out var result))
			{
				DemoUtils.ShowMessage("Station input wrong!");
				return;
			}
			try
			{
				busTcpServer = new ModbusTcpServer();
				busTcpServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				busTcpServer.OnDataReceived += BusTcpServer_OnDataReceived;
				busTcpServer.EnableWrite = checkBox_remote_write.Checked;
				busTcpServer.Station = result;
				busTcpServer.StationDataIsolation = checkBox_station_isolation.Checked;
				busTcpServer.UseModbusRtuOverTcp = checkBox_RtuOverTcp.Checked;
				busTcpServer.EnableWriteMaskCode = checkBox_maskcode.Checked;
				busTcpServer.ForceSerialReceiveOnce = checkBox_forceReceiveOnce.Checked;
				ComboBox2_SelectedIndexChanged(null, new EventArgs());
				busTcpServer.IsStringReverse = checkBox3.Checked;
				sslServerControl1.InitializeServer(busTcpServer);
				if (serverSettingControl1.ServerStart(busTcpServer))
				{
					userControlReadWriteServer1.SetReadWriteServer(busTcpServer, "100", 10, "modbusServer");
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("modbusServer", string.Empty, busTcpServer, sslServerControl1, "EnableWrite", "EnableIPv6", "Station", "StationDataIsolation", "UseModbusRtuOverTcp", "IsStringReverse", "EnableWriteMaskCode", "DataFormat");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			busTcpServer?.CloseSerialSlave();
			busTcpServer?.ServerClose();
			userControlReadWriteServer1.Close();
		}

		private void BusTcpServer_OnDataReceived(object sender, PipeSession source, byte[] modbus)
		{
			if (source.Communication is PipeTcpNet pipeTcpNet)
			{
				string ipAddress = pipeTcpNet.IpAddress;
			}
			if (source.Communication is PipeSerialPort pipeSerialPort)
			{
				string portName = pipeSerialPort.GetPipe().PortName;
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (busTcpServer != null)
			{
				try
				{
					busTcpServer.SerialReceiveAtleastTime = Convert.ToInt32(textBox_time_min.Text);
					busTcpServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
					serverSettingControl1.ButtonSerial.Enabled = false;
					codeExampleControl.SetCodeText("modbusServer", serverSettingControl1.TextBox_Serial.Text, busTcpServer, sslServerControl1, "EnableWrite", "EnableIPv6", "Station", "StationDataIsolation", "UseModbusRtuOverTcp", "IsStringReverse", "DataFormat", "SerialReceiveAtleastTime");
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
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox2.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
			element.SetAttributeValue("RtuOverTcp", checkBox_RtuOverTcp.Checked);
			element.SetAttributeValue("RemoteWrite", checkBox_remote_write.Checked);
			element.SetAttributeValue("StationIsolation", checkBox_station_isolation.Checked);
			element.SetAttributeValue("Station", textBox_station.Text);
			element.SetAttributeValue("MaskCode", checkBox_maskcode.Checked);
			element.SetAttributeValue("SerialReceiveAtleastTime", textBox_time_min.Text);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			comboBox2.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
			checkBox_RtuOverTcp.Checked = HslFormContent.GetXmlValue(element, "RtuOverTcp", false, bool.Parse);
			checkBox_remote_write.Checked = HslFormContent.GetXmlValue(element, "RemoteWrite", true, bool.Parse);
			checkBox_station_isolation.Checked = HslFormContent.GetXmlValue(element, "StationIsolation", false, bool.Parse);
			textBox_station.Text = HslFormContent.GetXmlValue(element, "Station", "1", (string m) => m);
			checkBox_maskcode.Checked = HslFormContent.GetXmlValue(element, "MaskCode", true, bool.Parse);
			textBox_time_min.Text = HslFormContent.GetXmlValue(element, "SerialReceiveAtleastTime", textBox_time_min.Text, (string m) => m);
			userControlReadWriteServer1.LoadDataTable(element);
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
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.checkBox_maskcode = new System.Windows.Forms.CheckBox();
			this.checkBox_forceReceiveOnce = new System.Windows.Forms.CheckBox();
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox_station_isolation = new System.Windows.Forms.CheckBox();
			this.textBox_time_min = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.checkBox_RtuOverTcp = new System.Windows.Forms.CheckBox();
			this.checkBox_remote_write = new System.Windows.Forms.CheckBox();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.comboBox2);
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.checkBox_maskcode);
			this.panel1.Controls.Add(this.checkBox_forceReceiveOnce);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.checkBox_station_isolation);
			this.panel1.Controls.Add(this.textBox_time_min);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.checkBox_RtuOverTcp);
			this.panel1.Controls.Add(this.checkBox_remote_write);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 90);
			this.panel1.TabIndex = 0;
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox2.Location = new System.Drawing.Point(3, 58);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(84, 25);
			this.comboBox2.TabIndex = 29;
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(1, 3);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(904, 30);
			this.serverSettingControl1.TabIndex = 45;
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-N-1";
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(93, 57);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(899, 30);
			this.sslServerControl1.TabIndex = 44;
			this.checkBox_maskcode.AutoSize = true;
			this.checkBox_maskcode.Checked = true;
			this.checkBox_maskcode.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_maskcode.Location = new System.Drawing.Point(786, 35);
			this.checkBox_maskcode.Name = "checkBox_maskcode";
			this.checkBox_maskcode.Size = new System.Drawing.Size(96, 21);
			this.checkBox_maskcode.TabIndex = 43;
			this.checkBox_maskcode.Text = "MaskCode?";
			this.checkBox_maskcode.UseVisualStyleBackColor = true;
			this.checkBox_forceReceiveOnce.AutoSize = true;
			this.checkBox_forceReceiveOnce.Location = new System.Drawing.Point(668, 34);
			this.checkBox_forceReceiveOnce.Name = "checkBox_forceReceiveOnce";
			this.checkBox_forceReceiveOnce.Size = new System.Drawing.Size(107, 21);
			this.checkBox_forceReceiveOnce.TabIndex = 41;
			this.checkBox_forceReceiveOnce.Text = "ReceiveOnce?";
			this.checkBox_forceReceiveOnce.UseVisualStyleBackColor = true;
			this.textBox_station.Location = new System.Drawing.Point(454, 33);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(41, 23);
			this.textBox_station.TabIndex = 40;
			this.textBox_station.Text = "1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(394, 36);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 17);
			this.label1.TabIndex = 39;
			this.label1.Text = "Station：";
			this.checkBox_station_isolation.AutoSize = true;
			this.checkBox_station_isolation.Location = new System.Drawing.Point(262, 35);
			this.checkBox_station_isolation.Name = "checkBox_station_isolation";
			this.checkBox_station_isolation.Size = new System.Drawing.Size(127, 21);
			this.checkBox_station_isolation.TabIndex = 38;
			this.checkBox_station_isolation.Text = "Station Isolation?";
			this.checkBox_station_isolation.UseVisualStyleBackColor = true;
			this.textBox_time_min.Location = new System.Drawing.Point(607, 32);
			this.textBox_time_min.Name = "textBox_time_min";
			this.textBox_time_min.Size = new System.Drawing.Size(39, 23);
			this.textBox_time_min.TabIndex = 37;
			this.textBox_time_min.Text = "20";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(505, 36);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(92, 17);
			this.label2.TabIndex = 36;
			this.label2.Text = "最短接收时间：";
			this.checkBox_RtuOverTcp.AutoSize = true;
			this.checkBox_RtuOverTcp.Location = new System.Drawing.Point(6, 34);
			this.checkBox_RtuOverTcp.Name = "checkBox_RtuOverTcp";
			this.checkBox_RtuOverTcp.Size = new System.Drawing.Size(99, 21);
			this.checkBox_RtuOverTcp.TabIndex = 32;
			this.checkBox_RtuOverTcp.Text = "使用RTU报文";
			this.checkBox_RtuOverTcp.UseVisualStyleBackColor = true;
			this.checkBox_remote_write.AutoSize = true;
			this.checkBox_remote_write.Checked = true;
			this.checkBox_remote_write.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_remote_write.Location = new System.Drawing.Point(122, 35);
			this.checkBox_remote_write.Name = "checkBox_remote_write";
			this.checkBox_remote_write.Size = new System.Drawing.Size(123, 21);
			this.checkBox_remote_write.TabIndex = 31;
			this.checkBox_remote_write.Text = "是否允许远程写入";
			this.checkBox_remote_write.UseVisualStyleBackColor = true;
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(899, 34);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 27;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 128);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 513);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(3, 0);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(989, 508);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7782315.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Modbus Tcp + Rtu + Ascii";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
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
			base.Name = "FormModbusServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Modbus虚拟服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
