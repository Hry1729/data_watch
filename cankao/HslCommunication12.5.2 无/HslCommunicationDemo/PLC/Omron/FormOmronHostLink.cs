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

namespace HslCommunicationDemo.PLC.Omron
{
	public class FormOmronHostLink : HslFormContent
	{
		private OmronHostLink omronHostLink = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label23;

		private TextBox textBox17;

		private Label label25;

		private TextBox textBox16;

		private Label label24;

		private ComboBox comboBox1;

		private TextBox textBox_station;

		private Label label1;

		private UserControlHead userControlHead1;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormOmronHostLink()
		{
			InitializeComponent();
			omronHostLink = new OmronHostLink();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox1.DataSource = SoftBasic.GetEnumValues<DataFormat>();
			comboBox1.SelectedItem = DataFormat.CDAB;
			Language(Program.Language);
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
				label25.Text = "Net Num";
				label23.Text = "PC Net Num";
				label1.Text = "Station:";
				button1.Text = "Open";
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
			if (!byte.TryParse(textBox_station.Text, out var result))
			{
				DemoUtils.ShowMessage("PLC Station input wrong！");
				return;
			}
			if (!byte.TryParse(textBox15.Text, out var result2))
			{
				DemoUtils.ShowMessage("PLC SID input wrong！");
				return;
			}
			if (!byte.TryParse(textBox16.Text, out var result3))
			{
				DemoUtils.ShowMessage("PLC DA2 input wrong！");
				return;
			}
			if (!byte.TryParse(textBox17.Text, out var result4))
			{
				DemoUtils.ShowMessage("PC SA2 input wrong");
				return;
			}
			omronHostLink?.Close();
			omronHostLink = new OmronHostLink();
			try
			{
				pipeSelectControl1.IniPipe(omronHostLink);
				omronHostLink.UnitNumber = result;
				omronHostLink.SID = result2;
				omronHostLink.DA2 = result3;
				omronHostLink.SA2 = result4;
				omronHostLink.ByteTransform.DataFormat = (DataFormat)comboBox1.SelectedItem;
				omronHostLink.LogNet = base.LogNet;
				OperateResult operateResult = DeviceConnectPLC(omronHostLink);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(omronHostLink, "D100");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(omronHostLink, "D100", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadWordRandom(omronHostLink.Read, "D100;A100;C100;H100");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronHostLink.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => omronHostLink.ReadFromCoreServer(m), "Fins Core", "example: 01 01 B1 00 0A 00 00 01");
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(omronHostLink, "UnitNumber", "SID", "DA2", "SA2", "ByteTransform.DataFormat");
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
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
			pipeSelectControl1.ExtraCloseAction(omronHostLink);
			omronHostLink?.Close();
		}

		private void test()
		{
			bool content = omronHostLink.ReadBool("D100.7").Content;
			short content2 = omronHostLink.ReadInt16("D100").Content;
			ushort content3 = omronHostLink.ReadUInt16("D100").Content;
			int content4 = omronHostLink.ReadInt32("D100").Content;
			uint content5 = omronHostLink.ReadUInt32("D100").Content;
			float content6 = omronHostLink.ReadFloat("D100").Content;
			long content7 = omronHostLink.ReadInt64("D100").Content;
			ulong content8 = omronHostLink.ReadUInt64("D100").Content;
			double content9 = omronHostLink.ReadDouble("D100").Content;
			string content10 = omronHostLink.ReadString("D100", 5).Content;
			omronHostLink.Write("D100", (short)51);
			omronHostLink.Write("D100", (short)12345);
			omronHostLink.Write("D100", (ushort)45678);
			omronHostLink.Write("D100", 3456789123u);
			omronHostLink.Write("D100", 123.456f);
			omronHostLink.Write("D100", 1234556434534545L);
			omronHostLink.Write("D100", 523434234234343uL);
			omronHostLink.Write("D100", 123.456);
			omronHostLink.Write("D100", "K123456789");
			OperateResult<byte[]> operateResult = omronHostLink.Read("D100", 5);
			if (operateResult.IsSuccess)
			{
				short num = omronHostLink.ByteTransform.TransInt16(operateResult.Content, 0);
				short num2 = omronHostLink.ByteTransform.TransInt16(operateResult.Content, 2);
				short num3 = omronHostLink.ByteTransform.TransInt16(operateResult.Content, 4);
				short num4 = omronHostLink.ByteTransform.TransInt16(operateResult.Content, 6);
				short num5 = omronHostLink.ByteTransform.TransInt16(operateResult.Content, 7);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox1.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUnitNumber, textBox16.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPCUnitNumber, textBox17.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDeviceId, textBox15.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			comboBox1.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			textBox16.Text = element.Attribute(DemoDeviceList.XmlUnitNumber).Value;
			textBox17.Text = element.Attribute(DemoDeviceList.XmlPCUnitNumber).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlDeviceId).Value;
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
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.textBox17);
			this.panel1.Controls.Add(this.label25);
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 63);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(6, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "7";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.Even;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 41;
			this.pipeSelectControl1.TcpPortText = "2000";
			this.pipeSelectControl1.UdpPortText = "2000";
			this.textBox_station.Location = new System.Drawing.Point(65, 33);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(40, 23);
			this.textBox_station.TabIndex = 40;
			this.textBox_station.Text = "0";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(11, 36);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 39;
			this.label1.Text = "站号：";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(597, 32);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(95, 25);
			this.comboBox1.TabIndex = 14;
			this.textBox17.Location = new System.Drawing.Point(347, 33);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(56, 23);
			this.textBox17.TabIndex = 13;
			this.textBox17.Text = "0";
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(254, 36);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(92, 17);
			this.label25.TabIndex = 12;
			this.label25.Text = "上位机单元号：";
			this.textBox16.Location = new System.Drawing.Point(187, 33);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(56, 23);
			this.textBox16.TabIndex = 11;
			this.textBox16.Text = "0";
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(111, 36);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(77, 17);
			this.label24.TabIndex = 10;
			this.label24.Text = "PLC单元号：";
			this.textBox15.Location = new System.Drawing.Point(508, 33);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(56, 23);
			this.textBox15.TabIndex = 9;
			this.textBox15.Text = "0";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(423, 36);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(80, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "设备标识号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(903, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(87, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(809, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(82, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 101);
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
			this.userControlHead1.ProtocolInfo = "Omron HostLink";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
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
			base.Name = "FormOmronHostLink";
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
