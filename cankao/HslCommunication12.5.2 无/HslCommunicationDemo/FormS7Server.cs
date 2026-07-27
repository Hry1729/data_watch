using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Siemens;

namespace HslCommunicationDemo
{
	public class FormS7Server : HslFormContent
	{
		private short m60 = 0;

		private bool m62 = false;

		private float m64 = 1.1f;

		private SiemensS7Server s7NetServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private S7ServerDbControl dbControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		public FormS7Server()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "S7 Virtual Server [data support i,q,m,db block read and write, DB1, DB2, DB3, others need add]";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetSiemensS7Address());
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			dbControl = new S7ServerDbControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(dbControl, false, "Db Blocks");
			dbControl.Enabled = false;
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = button5_Click;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			m60++;
			s7NetServer.Write("M60", m60);
			s7NetServer.Write("M62", !m62);
			m62 = !m62;
			m64 += 1f;
			if (m64 > 2000f)
			{
				m64 = 1.1f;
			}
			s7NetServer.Write("M64", m64);
			s7NetServer.Write("M70", "A" + DateTime.Now.Minute + DateTime.Now.Second);
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
				s7NetServer = new SiemensS7Server();
				s7NetServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				s7NetServer.OnDataReceived += BusTcpServer_OnDataReceived;
				sslServerControl1.InitializeServer(s7NetServer);
				if (serverSettingControl1.ServerStart(s7NetServer))
				{
					userControlReadWriteServer1.SetReadWriteServer(s7NetServer, "M100");
					userControlReadWriteServer1.SetEnable(true);
					dbControl.SetServer(s7NetServer);
					dbControl.Enabled = true;
					codeExampleControl.SetCodeText("server", "", s7NetServer, sslServerControl1);
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
			s7NetServer?.ServerClose();
			dbControl.Enabled = false;
		}

		private void button5_Click(object sender, EventArgs e)
		{
			s7NetServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
			serverSettingControl1.ButtonSerial.Enabled = false;
			codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, s7NetServer, sslServerControl1);
		}

		private void BusTcpServer_OnDataReceived(object sender, object source, byte[] receive)
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

		public override void SaveXmlParameter(XElement element)
		{
			sslServerControl1.SaveXmlParameter(element);
			serverSettingControl1.SaveXmlParameter(element);
			userControlReadWriteServer1.GetDataTable(element);
			dbControl.SaveToXElement(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			userControlReadWriteServer1.LoadDataTable(element);
			dbControl.LoadFromXElement(element);
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
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 66);
			this.panel1.TabIndex = 0;
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(3, 1);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(904, 30);
			this.serverSettingControl1.TabIndex = 36;
			this.serverSettingControl1.TextPort = "102";
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-N-1";
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(3, 31);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(985, 30);
			this.sslServerControl1.TabIndex = 35;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 105);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 538);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(2, 2);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(990, 531);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/10425797.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "s7";
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
			base.Name = "FormS7Server";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "s7虚拟服务器【数据支持I，Q，M，DB块读写，DB块含1,2,3，其他DB块需要手动添加】";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
