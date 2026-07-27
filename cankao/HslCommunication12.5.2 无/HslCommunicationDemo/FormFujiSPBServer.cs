using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Fuji;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Fuji;

namespace HslCommunicationDemo
{
	public class FormFujiSPBServer : HslFormContent
	{
		private FujiSPBServer sPBServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private CheckBox checkBox3;

		private UserControlHead userControlHead1;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private TextBox textBox1;

		private Label label1;

		private SslServerControl sslServerControl1;

		private ComboBox comboBox1;

		private ServerSettingControl serverSettingControl1;

		public FormFujiSPBServer()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			checkBox3.CheckedChanged += CheckBox3_CheckedChanged;
			comboBox1.SelectedIndex = 3;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			if (Program.Language == 2)
			{
				Text = "Fuji SPB Server";
				checkBox3.Text = "str-reverse";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetSPBAddressExamples());
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = button5_Click;
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (sPBServer != null)
			{
				switch (comboBox1.SelectedIndex)
				{
				case 0:
					sPBServer.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					sPBServer.DataFormat = DataFormat.BADC;
					break;
				case 2:
					sPBServer.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					sPBServer.DataFormat = DataFormat.DCBA;
					break;
				}
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (sPBServer != null)
			{
				sPBServer.IsStringReverse = checkBox3.Checked;
			}
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
				sPBServer = new FujiSPBServer();
				sPBServer.Station = int.Parse(textBox1.Text);
				sPBServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				sPBServer.OnDataReceived += BusTcpServer_OnDataReceived;
				sslServerControl1.InitializeServer(sPBServer);
				sPBServer.IsStringReverse = checkBox3.Checked;
				userControlReadWriteServer1.SetReadWriteServer(sPBServer, "D100");
				ComboBox1_SelectedIndexChanged(sender, e);
				if (serverSettingControl1.ServerStart(sPBServer))
				{
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("server", "", sPBServer, sslServerControl1, "Station", "DataFormat", "IsStringReverse");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			userControlReadWriteServer1.Close();
			sPBServer?.ServerClose();
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
			if (sPBServer != null)
			{
				try
				{
					sPBServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
					serverSettingControl1.ButtonSerial.Enabled = false;
					codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, sPBServer, "Station");
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
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
			sslServerControl1.SaveXmlParameter(element);
			serverSettingControl1.SaveXmlParameter(element);
			userControlReadWriteServer1.GetDataTable(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox1.SelectedIndex);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			textBox1.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStation, "1", (string m) => m);
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
			comboBox1.SelectedIndex = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlDataFormat, 3, int.Parse);
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
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(2, 3);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(986, 30);
			this.serverSettingControl1.TabIndex = 33;
			this.serverSettingControl1.TextPort = "9600";
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-N-1";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox1.Location = new System.Drawing.Point(218, 34);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(90, 25);
			this.comboBox1.TabIndex = 32;
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(316, 31);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(676, 30);
			this.sslServerControl1.TabIndex = 31;
			this.textBox1.Location = new System.Drawing.Point(58, 35);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(36, 23);
			this.textBox1.TabIndex = 30;
			this.textBox1.Text = "0";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 38);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 29;
			this.label1.Text = "站号：";
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(100, 37);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 27;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 104);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 539);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(3, 3);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(989, 531);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7782315.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "FujiSPB Tcp + Serial";
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
			base.Name = "FormFujiSPBServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "富士SPB虚拟PLC【支持网口和串口】";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
