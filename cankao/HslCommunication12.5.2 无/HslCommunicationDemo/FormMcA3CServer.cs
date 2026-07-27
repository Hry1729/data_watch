using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Melsec;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Melsec;

namespace HslCommunicationDemo
{
	public class FormMcA3CServer : HslFormContent
	{
		private MelsecA3CServer mcNetServer;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private CheckBox checkBox_sumcheck;

		private UserControlReadWriteServer userControlReadWriteServer1;

		private Label label1;

		private ComboBox comboBox_format;

		private SslServerControl sslServerControl1;

		private ServerSettingControl serverSettingControl1;

		private TextBox textBox_station;

		private Label label4;

		public FormMcA3CServer()
		{
			InitializeComponent();
			mcNetServer = new MelsecA3CServer();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox_format.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			comboBox_format.SelectedIndex = 0;
			if (Program.Language == 2)
			{
				Text = "MC Virtual Server";
				label4.Text = "Station:";
			}
			checkBox_sumcheck.CheckedChanged += CheckBox2_CheckedChanged;
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetMcServerAddress());
			userControlReadWriteServer1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteServer1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteServer1.SetEnable(false);
			serverSettingControl1.buttonStartAction = button1_Click;
			serverSettingControl1.buttonCloseAction = button11_Click;
			serverSettingControl1.buttonSerialAction = button5_Click;
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (mcNetServer != null)
			{
				mcNetServer.Format = int.Parse(comboBox_format.SelectedItem.ToString());
			}
		}

		private void CheckBox2_CheckedChanged(object sender, EventArgs e)
		{
			if (mcNetServer != null)
			{
				mcNetServer.SumCheck = checkBox_sumcheck.Checked;
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
				mcNetServer.SumCheck = checkBox_sumcheck.Checked;
				mcNetServer.Format = int.Parse(comboBox_format.SelectedItem.ToString());
				mcNetServer.Station = byte.Parse(textBox_station.Text);
				sslServerControl1.InitializeServer(mcNetServer);
				if (serverSettingControl1.ServerStart(mcNetServer))
				{
					userControlReadWriteServer1.SetReadWriteServer(mcNetServer, "D100");
					userControlReadWriteServer1.SetEnable(true);
					codeExampleControl.SetCodeText("server", "", mcNetServer, sslServerControl1, "Station", "SumCheck", "Format");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (mcNetServer != null)
			{
				mcNetServer.StartSerialSlave(serverSettingControl1.TextBox_Serial.Text);
				serverSettingControl1.ButtonSerial.Enabled = false;
				codeExampleControl.SetCodeText("server", serverSettingControl1.TextBox_Serial.Text, mcNetServer, "Station", "SumCheck", "Format");
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			userControlReadWriteServer1.Close();
			mcNetServer?.CloseSerialSlave();
			mcNetServer?.ServerClose();
		}

		private void MelsecMcServer_OnDataReceived(object sender, object source, byte[] receive)
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
			element.SetAttributeValue("SumCheck", checkBox_sumcheck.Checked);
			element.SetAttributeValue("Format", comboBox_format.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			userControlReadWriteServer1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			sslServerControl1.LoadXmlParameter(element);
			serverSettingControl1.LoadXmlParameter(element);
			checkBox_sumcheck.Checked = HslFormContent.GetXmlValue(element, "SumCheck", true, bool.Parse);
			comboBox_format.SelectedIndex = HslFormContent.GetXmlValue(element, "Format", 0, int.Parse);
			textBox_station.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStation, "0", (string m) => m);
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
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.serverSettingControl1 = new HslCommunicationDemo.DemoControl.ServerSettingControl();
			this.sslServerControl1 = new HslCommunicationDemo.DemoControl.SslServerControl();
			this.comboBox_format = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox_sumcheck = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteServer1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteServer();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.serverSettingControl1);
			this.panel1.Controls.Add(this.sslServerControl1);
			this.panel1.Controls.Add(this.comboBox_format);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.checkBox_sumcheck);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 68);
			this.panel1.TabIndex = 0;
			this.textBox_station.Location = new System.Drawing.Point(278, 38);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(47, 23);
			this.textBox_station.TabIndex = 44;
			this.textBox_station.Text = "0";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(216, 42);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 43;
			this.label4.Text = "站号：";
			this.serverSettingControl1.buttonCloseAction = null;
			this.serverSettingControl1.buttonSerialAction = null;
			this.serverSettingControl1.buttonStartAction = null;
			this.serverSettingControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.serverSettingControl1.Location = new System.Drawing.Point(3, 3);
			this.serverSettingControl1.Name = "serverSettingControl1";
			this.serverSettingControl1.Size = new System.Drawing.Size(904, 30);
			this.serverSettingControl1.TabIndex = 42;
			this.serverSettingControl1.TextPort = "6000";
			this.serverSettingControl1.TextSerialInfo = "COM4-9600-8-N-1";
			this.sslServerControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.sslServerControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.sslServerControl1.Location = new System.Drawing.Point(331, 34);
			this.sslServerControl1.Name = "sslServerControl1";
			this.sslServerControl1.Size = new System.Drawing.Size(661, 30);
			this.sslServerControl1.TabIndex = 41;
			this.comboBox_format.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_format.FormattingEnabled = true;
			this.comboBox_format.Items.AddRange(new object[4] { "1", "2", "3", "4" });
			this.comboBox_format.Location = new System.Drawing.Point(48, 37);
			this.comboBox_format.Name = "comboBox_format";
			this.comboBox_format.Size = new System.Drawing.Size(68, 25);
			this.comboBox_format.TabIndex = 38;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 40);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 31;
			this.label1.Text = "格式：";
			this.checkBox_sumcheck.AutoSize = true;
			this.checkBox_sumcheck.Checked = true;
			this.checkBox_sumcheck.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_sumcheck.Location = new System.Drawing.Point(123, 40);
			this.checkBox_sumcheck.Name = "checkBox_sumcheck";
			this.checkBox_sumcheck.Size = new System.Drawing.Size(87, 21);
			this.checkBox_sumcheck.TabIndex = 30;
			this.checkBox_sumcheck.Text = "SumCheck";
			this.checkBox_sumcheck.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteServer1);
			this.panel2.Location = new System.Drawing.Point(3, 107);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 535);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteServer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteServer1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteServer1.Location = new System.Drawing.Point(3, 2);
			this.userControlReadWriteServer1.Name = "userControlReadWriteServer1";
			this.userControlReadWriteServer1.Size = new System.Drawing.Size(989, 528);
			this.userControlReadWriteServer1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MC A3C Server";
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
			base.Name = "FormMcA3CServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "mc-A3C虚拟服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
