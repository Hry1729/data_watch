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
	public class FormOmron : HslFormContent
	{
		private OmronFinsNet omronFinsNet = null;

		private FinsTcpControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label23;

		private TextBox textBox16;

		private Label label24;

		private ComboBox comboBox1;

		private UserControlHead userControlHead1;

		private TextBox textBox3;

		private Label label2;

		private CheckBox checkBox_isstringreverse;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private CheckBox checkBox_receive_until_empty;

		private Label label5;

		private ComboBox comboBox_plcType;

		private PipeSelectControl pipeSelectControl1;

		public FormOmron()
		{
			InitializeComponent();
			omronFinsNet = new OmronFinsNet();
			omronFinsNet.ConnectTimeOut = 2000;
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox1.DataSource = SoftBasic.GetEnumValues<DataFormat>();
			comboBox1.SelectedItem = DataFormat.CDAB;
			comboBox_plcType.DataSource = SoftBasic.GetEnumValues<OmronPlcType>();
			comboBox_plcType.SelectedItem = OmronPlcType.CSCJ;
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
				label24.Text = "Unit Num";
				label23.Text = "PC Net Num";
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
			if (!byte.TryParse(textBox16.Text, out var result))
			{
				DemoUtils.ShowMessage("PLC DA2 input wrong！");
				return;
			}
			try
			{
				pipeSelectControl1.IniPipe(omronFinsNet);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
				return;
			}
			omronFinsNet.DA2 = result;
			omronFinsNet.ByteTransform.DataFormat = (DataFormat)comboBox1.SelectedItem;
			omronFinsNet.PlcType = (OmronPlcType)comboBox_plcType.SelectedItem;
			omronFinsNet.LogNet = base.LogNet;
			omronFinsNet.ByteTransform.IsStringReverseByteWord = checkBox_isstringreverse.Checked;
			omronFinsNet.ReceiveUntilEmpty = checkBox_receive_until_empty.Checked;
			OperateResult operateResult = DeviceConnectPLC(omronFinsNet);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
				button2.Enabled = true;
				button1.Enabled = false;
				userControlReadWriteDevice1.SetEnable(true);
				textBox15.Text = omronFinsNet.SA1.ToString();
				textBox3.Text = omronFinsNet.DA1.ToString();
				userControlReadWriteDevice1.SetReadWriteNet(omronFinsNet, "D100", true);
				userControlReadWriteDevice1.BatchRead.SetReadWriteNet(omronFinsNet, "D100", string.Empty);
				userControlReadWriteDevice1.BatchRead.SetReadWordRandom(omronFinsNet.Read, "D100;A100;C100;H100");
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronFinsNet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronFinsNet.ReadFromCoreServer(m), "Fins Core", "example: 01 01 B1 00 0A 00 00 01");
				control.SetDevice(omronFinsNet, "D100");
				userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
				codeExampleControl.SetCodeText(omronFinsNet, "PlcType", "DA2", "ReceiveUntilEmpty", "ByteTransform.DataFormat", "ByteTransform.IsStringReverseByteWord");
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + " " + operateResult.ToMessageShowString());
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			userControlReadWriteDevice1.SetEnable(false);
			pipeSelectControl1.ExtraCloseAction(omronFinsNet);
			omronFinsNet?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlUnitNumber, textBox16.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox1.SelectedIndex);
			element.SetAttributeValue("ReceiveUntilEmpty", checkBox_receive_until_empty.Checked);
			element.SetAttributeValue("IsStringReverseByteWord", checkBox_isstringreverse.Checked);
			element.SetAttributeValue("OmronType", comboBox_plcType.SelectedIndex);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox16.Text = element.Attribute(DemoDeviceList.XmlUnitNumber).Value;
			comboBox1.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox_receive_until_empty.Checked = HslFormContent.GetXmlValue(element, "ReceiveUntilEmpty", checkBox_receive_until_empty.Checked, bool.Parse);
			checkBox_isstringreverse.Checked = HslFormContent.GetXmlValue(element, "IsStringReverseByteWord", checkBox_isstringreverse.Checked, bool.Parse);
			comboBox_plcType.SelectedIndex = HslFormContent.GetXmlValue(element, "OmronType", comboBox_plcType.SelectedIndex, int.Parse);
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
			bool content = omronFinsNet.ReadBool("D100.7").Content;
			short content2 = omronFinsNet.ReadInt16("D100").Content;
			ushort content3 = omronFinsNet.ReadUInt16("D100").Content;
			int content4 = omronFinsNet.ReadInt32("D100").Content;
			uint content5 = omronFinsNet.ReadUInt32("D100").Content;
			float content6 = omronFinsNet.ReadFloat("D100").Content;
			long content7 = omronFinsNet.ReadInt64("D100").Content;
			ulong content8 = omronFinsNet.ReadUInt64("D100").Content;
			double content9 = omronFinsNet.ReadDouble("D100").Content;
			string content10 = omronFinsNet.ReadString("D100", 5).Content;
			omronFinsNet.Write("D100", (short)51);
			omronFinsNet.Write("D100", (short)12345);
			omronFinsNet.Write("D100", (ushort)45678);
			omronFinsNet.Write("D100", 3456789123u);
			omronFinsNet.Write("D100", 123.456f);
			omronFinsNet.Write("D100", 1234556434534545L);
			omronFinsNet.Write("D100", 523434234234343uL);
			omronFinsNet.Write("D100", 123.456);
			omronFinsNet.Write("D100", "K123456789");
			OperateResult<byte[]> operateResult = omronFinsNet.Read("D100", 5);
			if (operateResult.IsSuccess)
			{
				short num = omronFinsNet.ByteTransform.TransInt16(operateResult.Content, 0);
				short num2 = omronFinsNet.ByteTransform.TransInt16(operateResult.Content, 2);
				short num3 = omronFinsNet.ByteTransform.TransInt16(operateResult.Content, 4);
				short num4 = omronFinsNet.ByteTransform.TransInt16(operateResult.Content, 6);
				short num5 = omronFinsNet.ByteTransform.TransInt16(operateResult.Content, 7);
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
			this.checkBox_receive_until_empty = new System.Windows.Forms.CheckBox();
			this.checkBox_isstringreverse = new System.Windows.Forms.CheckBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.comboBox_plcType);
			this.panel1.Controls.Add(this.checkBox_receive_until_empty);
			this.panel1.Controls.Add(this.checkBox_isstringreverse);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1017, 60);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(5, 2);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 30;
			this.pipeSelectControl1.TcpPortText = "9600";
			this.pipeSelectControl1.UdpPortText = "9600";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(5, 34);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(39, 17);
			this.label5.TabIndex = 29;
			this.label5.Text = "Type:";
			this.comboBox_plcType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_plcType.FormattingEnabled = true;
			this.comboBox_plcType.Location = new System.Drawing.Point(56, 30);
			this.comboBox_plcType.Name = "comboBox_plcType";
			this.comboBox_plcType.Size = new System.Drawing.Size(109, 25);
			this.comboBox_plcType.TabIndex = 28;
			this.checkBox_receive_until_empty.AutoSize = true;
			this.checkBox_receive_until_empty.Location = new System.Drawing.Point(169, 33);
			this.checkBox_receive_until_empty.Name = "checkBox_receive_until_empty";
			this.checkBox_receive_until_empty.Size = new System.Drawing.Size(147, 21);
			this.checkBox_receive_until_empty.TabIndex = 18;
			this.checkBox_receive_until_empty.Text = "Receive Until Empty?";
			this.checkBox_receive_until_empty.UseVisualStyleBackColor = true;
			this.checkBox_isstringreverse.AutoSize = true;
			this.checkBox_isstringreverse.Checked = true;
			this.checkBox_isstringreverse.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_isstringreverse.Location = new System.Drawing.Point(772, 35);
			this.checkBox_isstringreverse.Name = "checkBox_isstringreverse";
			this.checkBox_isstringreverse.Size = new System.Drawing.Size(127, 21);
			this.checkBox_isstringreverse.TabIndex = 17;
			this.checkBox_isstringreverse.Text = "Is string reverse?";
			this.checkBox_isstringreverse.UseVisualStyleBackColor = true;
			this.textBox3.Location = new System.Drawing.Point(712, 32);
			this.textBox3.Name = "textBox3";
			this.textBox3.ReadOnly = true;
			this.textBox3.Size = new System.Drawing.Size(45, 23);
			this.textBox3.TabIndex = 16;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(665, 35);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 15;
			this.label2.Text = "DA1：";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(452, 31);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(80, 25);
			this.comboBox1.TabIndex = 14;
			this.textBox16.Location = new System.Drawing.Point(390, 32);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(56, 23);
			this.textBox16.TabIndex = 11;
			this.textBox16.Text = "0";
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(314, 35);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(77, 17);
			this.label24.TabIndex = 10;
			this.label24.Text = "PLC单元号：";
			this.textBox15.Location = new System.Drawing.Point(589, 32);
			this.textBox15.Name = "textBox15";
			this.textBox15.ReadOnly = true;
			this.textBox15.Size = new System.Drawing.Size(45, 23);
			this.textBox15.TabIndex = 9;
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(542, 35);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(42, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "SA1：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(903, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(75, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(818, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(81, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 97);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1017, 558);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(1015, 556);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7469679.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Fins-Tcp";
			this.userControlHead1.Size = new System.Drawing.Size(1024, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1024, 658);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormOmron";
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
