using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS7Plus : HslFormContent
	{
		private SiemensS7Plus siemensTcpNet = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private SiemensS7PlusControl siemensS7PlusControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox_slot;

		private Label label24;

		private TextBox textBox_rack;

		private Label label23;

		protected UserControlHead userControlHead1;

		private TextBox textBox_localTSAP;

		private Label label5;

		private TextBox textBox_pdu;

		private Label label6;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private TextBox textBox_session_id;

		private Label label2;

		private TextBox textBox_order;

		private Label label4;

		private PipeSelectControl pipeSelectControl1;

		public FormSiemensS7Plus()
		{
			InitializeComponent();
			siemensTcpNet = new SiemensS7Plus();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetSiemensS7Address());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			siemensS7PlusControl = new SiemensS7PlusControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(siemensS7PlusControl, false, "Browser");
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Siemens Read PLC Demo";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				pipeSelectControl1.IniPipe(siemensTcpNet);
				siemensTcpNet.LogNet = base.LogNet;
				OperateResult operateResult = DeviceConnectPLC(siemensTcpNet);
				if (operateResult.IsSuccess)
				{
					textBox_pdu.Text = siemensTcpNet.PDULength.ToString();
					textBox_session_id.Text = "0x" + siemensTcpNet.SessionID.ToString("X8");
					textBox_order.Text = siemensTcpNet.OrderNumber;
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(siemensTcpNet, "8A0E0001.A");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(siemensTcpNet, "8A0E0001.A", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => siemensTcpNet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(siemensTcpNet);
					siemensS7PlusControl.SetDevice(siemensTcpNet, "");
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			userControlReadWriteDevice1.SetEnable(false);
			pipeSelectControl1.ExtraCloseAction(siemensTcpNet);
			siemensTcpNet?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlRack, textBox_rack.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSlot, textBox_slot.Text);
			element.SetAttributeValue("LocalTSAP", textBox_localTSAP.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox_rack.Text = element.Attribute(DemoDeviceList.XmlRack).Value;
			textBox_slot.Text = element.Attribute(DemoDeviceList.XmlSlot).Value;
			textBox_localTSAP.Text = HslFormContent.GetXmlValue(element, "LocalTSAP", string.Empty, (string m) => m);
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
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
			this.textBox_order = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_session_id = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_pdu = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_localTSAP = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_slot = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox_rack = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox_order);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox_session_id);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox_pdu);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox_localTSAP);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox_slot);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.textBox_rack);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.textBox_order.Location = new System.Drawing.Point(656, 35);
			this.textBox_order.Name = "textBox_order";
			this.textBox_order.ReadOnly = true;
			this.textBox_order.Size = new System.Drawing.Size(160, 23);
			this.textBox_order.TabIndex = 22;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(604, 38);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(55, 17);
			this.label4.TabIndex = 21;
			this.label4.Text = "Order：";
			this.textBox_session_id.Location = new System.Drawing.Point(402, 35);
			this.textBox_session_id.Name = "textBox_session_id";
			this.textBox_session_id.ReadOnly = true;
			this.textBox_session_id.Size = new System.Drawing.Size(91, 23);
			this.textBox_session_id.TabIndex = 20;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(338, 38);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(64, 17);
			this.label2.TabIndex = 19;
			this.label2.Text = "Session：";
			this.textBox_pdu.Location = new System.Drawing.Point(552, 35);
			this.textBox_pdu.Name = "textBox_pdu";
			this.textBox_pdu.ReadOnly = true;
			this.textBox_pdu.Size = new System.Drawing.Size(40, 23);
			this.textBox_pdu.TabIndex = 17;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(502, 38);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(45, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "PDU：";
			this.textBox_localTSAP.Location = new System.Drawing.Point(275, 35);
			this.textBox_localTSAP.Name = "textBox_localTSAP";
			this.textBox_localTSAP.Size = new System.Drawing.Size(52, 23);
			this.textBox_localTSAP.TabIndex = 15;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(190, 38);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(79, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "LocalTSAP：";
			this.textBox_slot.Location = new System.Drawing.Point(142, 35);
			this.textBox_slot.Name = "textBox_slot";
			this.textBox_slot.Size = new System.Drawing.Size(33, 23);
			this.textBox_slot.TabIndex = 11;
			this.textBox_slot.Text = "0";
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(96, 38);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(42, 17);
			this.label24.TabIndex = 10;
			this.label24.Text = "Slot：";
			this.textBox_rack.Location = new System.Drawing.Point(57, 35);
			this.textBox_rack.Name = "textBox_rack";
			this.textBox_rack.Size = new System.Drawing.Size(33, 23);
			this.textBox_rack.TabIndex = 9;
			this.textBox_rack.Text = "0";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(6, 38);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(48, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "Rack：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(903, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(88, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(814, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(86, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 103);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 538);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 536);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8685855.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "s7-plus";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(3, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(794, 28);
			this.pipeSelectControl1.TabIndex = 23;
			this.pipeSelectControl1.TcpPortText = "102";
			this.pipeSelectControl1.UdpPortText = "102";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSiemensS7Plus";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "西门子PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
