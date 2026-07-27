using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Cimon;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Cimon;

namespace HslCommunicationDemo
{
	public class FormCimonServer : HslFormContent
	{
		private CimonServer cimonServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private Label label2;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private SslServerControl sslServerControl1;

		private TextBox textBox_frameNo;

		private ServerSettingControl serverSettingControl1;

		public FormCimonServer()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "Cimon Virtual Server";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetCimonAddress());
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = Button5_Click;
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
				cimonServer = new CimonServer();
				cimonServer.FrameNo = byte.Parse(textBox_frameNo.Text);
				cimonServer.OnDataReceived += BusTcpServer_OnDataReceived;
				sslServerControl1.InitializeServer(cimonServer);
				if (serverSettingControl1.ServerStart(cimonServer))
				{
					userControlReadWriteServer1.SetReadWriteServer(cimonServer, "D100");
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("server", "", cimonServer, sslServerControl1, "FrameNo");
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
			cimonServer?.ServerClose();
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

		private void Button5_Click(object sender, EventArgs e)
		{
			if (cimonServer != null)
			{
				try
				{
					cimonServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
					serverSettingControl1.ButtonSerial.Enabled = false;
					codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, cimonServer, sslServerControl1, "FrameNo");
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
			serverSettingControl1.SaveXmlParameter(element);
			sslServerControl1.SaveXmlParameter(element);
			element.SetAttributeValue("FrameNo", textBox_frameNo.Text);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			textBox_frameNo.Text = HslFormContent.GetXmlValue(element, "FrameNo", "1", (string m) => m);
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
			this.textBox_frameNo = new System.Windows.Forms.TextBox();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.label2 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.textBox_frameNo);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.textBox_frameNo.Location = new System.Drawing.Point(86, 34);
			this.textBox_frameNo.Name = "textBox_frameNo";
			this.textBox_frameNo.Size = new System.Drawing.Size(45, 23);
			this.textBox_frameNo.TabIndex = 36;
			this.textBox_frameNo.Text = "1";
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(140, 32);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(852, 30);
			this.sslServerControl1.TabIndex = 35;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(5, 37);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(65, 17);
			this.label2.TabIndex = 32;
			this.label2.Text = "FrameNo:";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 101);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 541);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(4, 3);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(992, 533);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "HMI Protocol";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(3, 2);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(904, 30);
			this.serverSettingControl1.TabIndex = 37;
			this.serverSettingControl1.TextPort = "10260";
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
			base.Name = "FormCimonServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "CimonServer";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
