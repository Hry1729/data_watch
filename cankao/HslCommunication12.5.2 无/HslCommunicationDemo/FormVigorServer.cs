using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Vigor;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Vigor;

namespace HslCommunicationDemo
{
	public class FormVigorServer : HslFormContent
	{
		private VigorServer vigorServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private TextBox textBox_station;

		private Label label1;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		public FormVigorServer()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "Vigor Virtual Server";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetVigorAddress());
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
				vigorServer = new VigorServer();
				vigorServer.Station = int.Parse(textBox_station.Text);
				vigorServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				vigorServer.OnDataReceived += BusTcpServer_OnDataReceived;
				sslServerControl1.InitializeServer(vigorServer);
				if (serverSettingControl1.ServerStart(vigorServer))
				{
					userControlReadWriteServer1.SetReadWriteServer(vigorServer, "D100");
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("server", "", vigorServer, sslServerControl1, "Station");
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
			vigorServer?.CloseSerialSlave();
			vigorServer?.ServerClose();
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
			if (vigorServer != null)
			{
				try
				{
					vigorServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
					serverSettingControl1.ButtonSerial.Enabled = false;
					codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, vigorServer, sslServerControl1, "Station");
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
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			textBox_station.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStation, "0", (string m) => m);
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
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
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
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.textBox_station.Location = new System.Drawing.Point(58, 35);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(61, 23);
			this.textBox_station.TabIndex = 30;
			this.textBox_station.Text = "0";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 38);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 29;
			this.label1.Text = "站号：";
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(125, 32);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(867, 30);
			this.sslServerControl1.TabIndex = 31;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 103);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 540);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(3, 0);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(989, 535);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7782315.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Vigor Tcp + Serial";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(2, 2);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(990, 30);
			this.serverSettingControl1.TabIndex = 32;
			this.serverSettingControl1.TextPort = "5000";
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-N-1";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormVigorServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "丰炜虚拟PLC【支持网口和串口】";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
