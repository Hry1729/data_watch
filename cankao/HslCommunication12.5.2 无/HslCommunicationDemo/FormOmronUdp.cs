using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Profinet.Omron;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Omron;

namespace HslCommunicationDemo
{
	public class FormOmronUdp : HslFormContent
	{
		private OmronFinsUdp omronFinsUdp = null;

		private FinsTcpControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button1;

		private TextBox textBox_sa1;

		private Label label23;

		private ComboBox comboBox1;

		private UserControlHead userControlHead1;

		private TextBox textBox_gct;

		private Label label2;

		private CheckBox checkBox_isstringreverse;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private Button button2;

		private TextBox textBox_da1;

		private Label label4;

		private ComboBox comboBox_plcType;

		private Label label5;

		private PipeSelectControl pipeSelectControl1;

		private TextBox textBox_sid;

		private Label label_sid;

		public FormOmronUdp()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox1.DataSource = SoftBasic.GetEnumValues<DataFormat>();
			comboBox1.SelectedItem = DataFormat.CDAB;
			comboBox_plcType.DataSource = SoftBasic.GetEnumValues<OmronPlcType>();
			comboBox_plcType.SelectedItem = OmronPlcType.CSCJ;
			button2.Enabled = false;
			Language(Program.Language);
			control = new FinsTcpControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetOmronAddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Omron Read PLC Demo";
				label23.Text = "PC Net Num";
				button1.Text = "Create";
				button2.Text = "Close";
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
			if (!byte.TryParse(textBox_sa1.Text, out var result))
			{
				DemoUtils.ShowMessage("SA1 Input Wrong！");
				return;
			}
			if (!byte.TryParse(textBox_gct.Text, out var result2))
			{
				DemoUtils.ShowMessage("GCT Input Wrong！");
				return;
			}
			if (!byte.TryParse(textBox_sid.Text, out var result3))
			{
				DemoUtils.ShowMessage("SID Input Wrong！");
				return;
			}
			omronFinsUdp = new OmronFinsUdp();
			omronFinsUdp.LogNet = base.LogNet;
			omronFinsUdp.SA1 = result;
			omronFinsUdp.GCT = result2;
			omronFinsUdp.SID = result3;
			userControlReadWriteDevice1.SetEnable(true);
			if (!string.IsNullOrEmpty(textBox_da1.Text))
			{
				if (!byte.TryParse(textBox_da1.Text, out var result4))
				{
					DemoUtils.ShowMessage("DA1 Input Wrong！");
					return;
				}
				omronFinsUdp.DA1 = result4;
			}
			omronFinsUdp.ByteTransform.DataFormat = (DataFormat)comboBox1.SelectedItem;
			omronFinsUdp.ByteTransform.IsStringReverseByteWord = checkBox_isstringreverse.Checked;
			omronFinsUdp.PlcType = (OmronPlcType)comboBox_plcType.SelectedItem;
			try
			{
				pipeSelectControl1.IniPipe(omronFinsUdp);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
			OperateResult operateResult = DeviceConnectPLC(omronFinsUdp);
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				userControlReadWriteDevice1.SetReadWriteNet(omronFinsUdp, "D100");
				userControlReadWriteDevice1.BatchRead.SetReadWriteNet(omronFinsUdp, "D100", string.Empty);
				userControlReadWriteDevice1.BatchRead.SetReadWordRandom(omronFinsUdp.Read, "D100;A100;C100;H100");
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronFinsUdp.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronFinsUdp.ReadFromCoreServer(m), "Fins Core", "example: 01 01 B1 00 0A 00 00 01");
				control.SetDevice(omronFinsUdp, "D100");
				userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
				codeExampleControl.SetCodeText(omronFinsUdp, "PlcType", "SA1", "GCT", "DA1", "SID", "ByteTransform.DataFormat", "ByteTransform.IsStringReverseByteWord");
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			userControlReadWriteDevice1.SetEnable(false);
			button1.Enabled = true;
			button2.Enabled = false;
			pipeSelectControl1.ExtraCloseAction(omronFinsUdp);
			omronFinsUdp?.CommunicationPipe?.CloseCommunication();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlNetNumber, textBox_sa1.Text);
			element.SetAttributeValue("DA1", textBox_da1.Text);
			element.SetAttributeValue("GCT", textBox_gct.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox1.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox_isstringreverse.Checked);
			element.SetAttributeValue("OmronType", comboBox_plcType.SelectedIndex);
			element.SetAttributeValue("SID", textBox_sid.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.UdpPipe);
			textBox_sa1.Text = element.Attribute(DemoDeviceList.XmlNetNumber).Value;
			textBox_da1.Text = HslFormContent.GetXmlValue(element, "DA1", textBox_da1.Text, (string m) => m);
			textBox_gct.Text = HslFormContent.GetXmlValue(element, "GCT", textBox_gct.Text, (string m) => m);
			textBox_sid.Text = HslFormContent.GetXmlValue(element, "SID", textBox_sid.Text, (string m) => m);
			checkBox_isstringreverse.Checked = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStringReverse, checkBox_isstringreverse.Checked, bool.Parse);
			comboBox_plcType.SelectedIndex = HslFormContent.GetXmlValue(element, "OmronType", comboBox_plcType.SelectedIndex, int.Parse);
			comboBox1.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void test()
		{
			bool content = omronFinsUdp.ReadBool("D100.7").Content;
			short content2 = omronFinsUdp.ReadInt16("D100").Content;
			ushort content3 = omronFinsUdp.ReadUInt16("D100").Content;
			int content4 = omronFinsUdp.ReadInt32("D100").Content;
			uint content5 = omronFinsUdp.ReadUInt32("D100").Content;
			float content6 = omronFinsUdp.ReadFloat("D100").Content;
			long content7 = omronFinsUdp.ReadInt64("D100").Content;
			ulong content8 = omronFinsUdp.ReadUInt64("D100").Content;
			double content9 = omronFinsUdp.ReadDouble("D100").Content;
			string content10 = omronFinsUdp.ReadString("D100", 5).Content;
			omronFinsUdp.Write("D100", (short)51);
			omronFinsUdp.Write("D100", (short)12345);
			omronFinsUdp.Write("D100", (ushort)45678);
			omronFinsUdp.Write("D100", 3456789123u);
			omronFinsUdp.Write("D100", 123.456f);
			omronFinsUdp.Write("D100", 1234556434534545L);
			omronFinsUdp.Write("D100", 523434234234343uL);
			omronFinsUdp.Write("D100", 123.456);
			omronFinsUdp.Write("D100", "K123456789");
			OperateResult<byte[]> operateResult = omronFinsUdp.Read("D100", 5);
			if (operateResult.IsSuccess)
			{
				short num = omronFinsUdp.ByteTransform.TransInt16(operateResult.Content, 0);
				short num2 = omronFinsUdp.ByteTransform.TransInt16(operateResult.Content, 2);
				short num3 = omronFinsUdp.ByteTransform.TransInt16(operateResult.Content, 4);
				short num4 = omronFinsUdp.ByteTransform.TransInt16(operateResult.Content, 6);
				short num5 = omronFinsUdp.ByteTransform.TransInt16(operateResult.Content, 7);
			}
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
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.label5 = new System.Windows.Forms.Label();
			this.comboBox_plcType = new System.Windows.Forms.ComboBox();
			this.textBox_da1 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.checkBox_isstringreverse = new System.Windows.Forms.CheckBox();
			this.textBox_gct = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox_sa1 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.textBox_sid = new System.Windows.Forms.TextBox();
			this.label_sid = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_sid);
			this.panel1.Controls.Add(this.label_sid);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.comboBox_plcType);
			this.panel1.Controls.Add(this.textBox_da1);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.checkBox_isstringreverse);
			this.panel1.Controls.Add(this.textBox_gct);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.textBox_sa1);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 64);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 1);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.UdpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 28;
			this.pipeSelectControl1.TcpPortText = "9600";
			this.pipeSelectControl1.UdpPortText = "9600";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(783, 37);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(39, 17);
			this.label5.TabIndex = 27;
			this.label5.Text = "Type:";
			this.comboBox_plcType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_plcType.FormattingEnabled = true;
			this.comboBox_plcType.Location = new System.Drawing.Point(828, 33);
			this.comboBox_plcType.Name = "comboBox_plcType";
			this.comboBox_plcType.Size = new System.Drawing.Size(109, 25);
			this.comboBox_plcType.TabIndex = 26;
			this.textBox_da1.Location = new System.Drawing.Point(273, 35);
			this.textBox_da1.Name = "textBox_da1";
			this.textBox_da1.Size = new System.Drawing.Size(40, 23);
			this.textBox_da1.TabIndex = 22;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(161, 38);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(103, 17);
			this.label4.TabIndex = 21;
			this.label4.Text = "目标网络号(DA1):";
			this.button2.Location = new System.Drawing.Point(907, 2);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(82, 28);
			this.button2.TabIndex = 20;
			this.button2.Text = "关闭";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.checkBox_isstringreverse.AutoSize = true;
			this.checkBox_isstringreverse.Checked = true;
			this.checkBox_isstringreverse.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_isstringreverse.Location = new System.Drawing.Point(636, 37);
			this.checkBox_isstringreverse.Name = "checkBox_isstringreverse";
			this.checkBox_isstringreverse.Size = new System.Drawing.Size(127, 21);
			this.checkBox_isstringreverse.TabIndex = 19;
			this.checkBox_isstringreverse.Text = "Is string reverse?";
			this.checkBox_isstringreverse.UseVisualStyleBackColor = true;
			this.textBox_gct.Location = new System.Drawing.Point(360, 35);
			this.textBox_gct.Name = "textBox_gct";
			this.textBox_gct.Size = new System.Drawing.Size(44, 23);
			this.textBox_gct.TabIndex = 16;
			this.textBox_gct.Text = "2";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(319, 38);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 17);
			this.label2.TabIndex = 15;
			this.label2.Text = "GCT:";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(502, 34);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(112, 25);
			this.comboBox1.TabIndex = 14;
			this.textBox_sa1.Location = new System.Drawing.Point(115, 35);
			this.textBox_sa1.Name = "textBox_sa1";
			this.textBox_sa1.Size = new System.Drawing.Size(40, 23);
			this.textBox_sa1.TabIndex = 9;
			this.textBox_sa1.Text = "1";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(4, 38);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(110, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "本机网络号(SA1)：";
			this.button1.Location = new System.Drawing.Point(816, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(82, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 102);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 541);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 539);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7469679.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Fins Udp";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.textBox_sid.Location = new System.Drawing.Point(451, 34);
			this.textBox_sid.Name = "textBox_sid";
			this.textBox_sid.Size = new System.Drawing.Size(44, 23);
			this.textBox_sid.TabIndex = 30;
			this.textBox_sid.Text = "0";
			this.label_sid.AutoSize = true;
			this.label_sid.Location = new System.Drawing.Point(410, 37);
			this.label_sid.Name = "label_sid";
			this.label_sid.Size = new System.Drawing.Size(31, 17);
			this.label_sid.TabIndex = 29;
			this.label_sid.Text = "SID:";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormOmronUdp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "欧姆龙PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
