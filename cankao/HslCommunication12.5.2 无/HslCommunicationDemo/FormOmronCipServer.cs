using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Omron;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.AllenBrandly;

namespace HslCommunicationDemo
{
	public class FormOmronCipServer : HslFormContent
	{
		private OmronCipServer cipServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private Label label2;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private CheckBox checkBox1;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		public FormOmronCipServer()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "Omron Cip Virtual Server [support single value]";
				checkBox1.Text = "Create Tag when write";
			}
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetCIPAddressExamples());
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
				cipServer = new OmronCipServer();
				cipServer.ActiveTimeSpan = TimeSpan.FromHours(1.0);
				cipServer.OnDataReceived += BusTcpServer_OnDataReceived;
				cipServer.CreateTagWithWrite = checkBox1.Checked;
				sslServerControl1.InitializeServer(cipServer);
				short[] array = new short[2000];
				float[] array2 = new float[2000];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = (short)(i + 1);
					array2[i] = array[i];
				}
				if (serverSettingControl1.ServerStart(cipServer))
				{
					cipServer.AddTagValue("A", (short)10);
					cipServer.AddTagValue("A1", array2);
					cipServer.AddTagValue("B", 123);
					cipServer.AddTagValue("C", 123f);
					cipServer.AddTagValue("D", array);
					cipServer.AddTagValue("E", true);
					cipServer.AddTagValue("F", "12345", 100);
					cipServer.AddTagValue("G", new string[5]
					{
						"123",
						"123456",
						string.Empty,
						"abcd",
						"测试"
					}, 100);
					cipServer.AddTagValue("AB.C", new short[5] { 1, 2, 3, 4, 5 });
					cipServer.AddTagValue("M", new uint[4] { 1u, 2u, 3u, 4u });
					cipServer.AddTagValue("REAL500", new float[500]);
					cipServer.AddTagValue("N", 100L);
					userControlReadWriteServer1.SetEnable(true);
					userControlReadWriteServer1.SetReadWriteServer(cipServer, "A", 1);
					codeExampleControl.SetCodeText("server", "", cipServer, sslServerControl1, "CreateTagWithWrite");
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
			cipServer?.ServerClose();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			cipServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
			serverSettingControl1.ButtonSerial.Enabled = false;
			codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, cipServer, sslServerControl1, "CreateTagWithWrite");
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

		private void userControlReadWriteServer1_Load(object sender, EventArgs e)
		{
		}

		public override void SaveXmlParameter(XElement element)
		{
			sslServerControl1.SaveXmlParameter(element);
			serverSettingControl1.SaveXmlParameter(element);
			element.SetAttributeValue("CreateTagWithWrite", checkBox1.Checked);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			checkBox1.Checked = HslFormContent.GetXmlValue(element, "CreateTagWithWrite", false, bool.Parse);
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
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.label2 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(153, 31);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(835, 30);
			this.sslServerControl1.TabIndex = 31;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(3, 35);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(111, 21);
			this.checkBox1.TabIndex = 30;
			this.checkBox1.Text = "写入时创建标签";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Location = new System.Drawing.Point(3, 105);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 537);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(2, 25);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(990, 511);
			this.userControlReadWriteServer1.TabIndex = 19;
			this.userControlReadWriteServer1.Load += new System.EventHandler(userControlReadWriteServer1_Load);
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(8, 5);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(893, 17);
			this.label2.TabIndex = 18;
			this.label2.Text = "服务器值列表：   A short ; A1  float[2000]  ;  B int ;  C   float ;     D  short[2000];     E  bool;     F  string ;  G  sting[5];   AB.C  short[5];   M  uint[4];  N   long";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Cip";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(2, 1);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(958, 30);
			this.serverSettingControl1.TabIndex = 32;
			this.serverSettingControl1.TextPort = "44818";
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
			base.Name = "FormOmronCipServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Omron PLC虚拟服务器【仅支持单数据，一维数组】";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
