using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Profinet.LSIS;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Lsis;

namespace HslCommunicationDemo
{
	public class FormLsisFEnet : HslFormContent
	{
		private LSFastEnet fastEnet = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox_slot;

		private Label label15;

		private UserControlHead userControlHead1;

		private ComboBox cboxModel;

		private Label label2;

		private Label label4;

		private ComboBox cboxCompanyID;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		private TextBox textBox_baseNo;

		private Label label1;

		public FormLsisFEnet()
		{
			InitializeComponent();
			fastEnet = new LSFastEnet();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			cboxModel.DataSource = Enum.GetNames(typeof(LSCpuInfo));
			cboxCompanyID.SelectedIndex = 0;
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetLsisCnetAddress());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Lsis Read PLC Demo";
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
			if (!byte.TryParse(textBox_slot.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.SlotInputWrong);
				return;
			}
			if (!byte.TryParse(textBox_baseNo.Text, out var result2))
			{
				DemoUtils.ShowMessage("BaseNo input wrong");
				return;
			}
			fastEnet.SlotNo = result;
			fastEnet.BaseNo = result2;
			fastEnet.SetCpuType = cboxModel.Text;
			fastEnet.CompanyID = cboxCompanyID.Text;
			fastEnet.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(fastEnet);
				OperateResult operateResult = DeviceConnectPLC(fastEnet);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(fastEnet, "MB100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(fastEnet, "MB100", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => fastEnet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(fastEnet, "SlotNo", "BaseNo", "SetCpuType", "CompanyID");
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
			pipeSelectControl1.ExtraCloseAction(fastEnet);
			fastEnet?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlCpuType, cboxModel.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCompanyID, cboxCompanyID.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSlot, textBox_slot.Text);
			element.SetAttributeValue("BaseNo", textBox_baseNo.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			cboxModel.Text = element.Attribute(DemoDeviceList.XmlCpuType).Value;
			cboxCompanyID.Text = element.Attribute(DemoDeviceList.XmlCompanyID).Value;
			textBox_slot.Text = element.Attribute(DemoDeviceList.XmlSlot).Value;
			textBox_baseNo.Text = HslFormContent.GetXmlValue(element, "BaseNo", textBox_baseNo.Text, (string m) => m);
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
			this.textBox_baseNo = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.label4 = new System.Windows.Forms.Label();
			this.cboxCompanyID = new System.Windows.Forms.ComboBox();
			this.cboxModel = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_slot = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_baseNo);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.cboxCompanyID);
			this.panel1.Controls.Add(this.cboxModel);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox_slot);
			this.panel1.Controls.Add(this.label15);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(4, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 66);
			this.panel1.TabIndex = 0;
			this.textBox_baseNo.Location = new System.Drawing.Point(204, 37);
			this.textBox_baseNo.Name = "textBox_baseNo";
			this.textBox_baseNo.Size = new System.Drawing.Size(52, 23);
			this.textBox_baseNo.TabIndex = 16;
			this.textBox_baseNo.Text = "0";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(137, 40);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(66, 17);
			this.label1.TabIndex = 15;
			this.label1.Text = "BaseNo：";
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(5, 4);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 14;
			this.pipeSelectControl1.TcpPortText = "2004";
			this.pipeSelectControl1.UdpPortText = "2004";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(446, 39);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(79, 17);
			this.label4.TabIndex = 13;
			this.label4.Text = "CompanyID:";
			this.cboxCompanyID.FormattingEnabled = true;
			this.cboxCompanyID.Items.AddRange(new object[3] { "LSIS-XGT", "LGIS-GLOGA", "MASTER-K" });
			this.cboxCompanyID.Location = new System.Drawing.Point(533, 36);
			this.cboxCompanyID.Name = "cboxCompanyID";
			this.cboxCompanyID.Size = new System.Drawing.Size(89, 25);
			this.cboxCompanyID.TabIndex = 12;
			this.cboxModel.FormattingEnabled = true;
			this.cboxModel.Location = new System.Drawing.Point(357, 36);
			this.cboxModel.Name = "cboxModel";
			this.cboxModel.Size = new System.Drawing.Size(71, 25);
			this.cboxModel.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(295, 39);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(60, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "cpuType:";
			this.textBox_slot.Location = new System.Drawing.Point(69, 37);
			this.textBox_slot.Name = "textBox_slot";
			this.textBox_slot.Size = new System.Drawing.Size(52, 23);
			this.textBox_slot.TabIndex = 9;
			this.textBox_slot.Text = "3";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(7, 40);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(42, 17);
			this.label15.TabIndex = 8;
			this.label15.Text = "Slot：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(897, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(799, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(4, 105);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 537);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 535);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Fast Enet 协议";
			this.userControlHead1.Size = new System.Drawing.Size(1005, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 3;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1005, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormLsisFEnet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "LSIS PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
