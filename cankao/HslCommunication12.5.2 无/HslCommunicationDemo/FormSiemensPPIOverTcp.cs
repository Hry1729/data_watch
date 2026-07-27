using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensPPIOverTcp : HslFormContent
	{
		private SiemensPPIOverTcp siemensPPI = null;

		private SiemensPPIControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private TextBox textBox15;

		private Label label23;

		private UserControlHead userControlHead1;

		private Button button2;

		private Button button1;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormSiemensPPIOverTcp()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new SiemensPPIControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetSiemensPPIAddress());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
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
			siemensPPI?.ConnectClose();
			siemensPPI = new SiemensPPIOverTcp();
			siemensPPI.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(siemensPPI);
				siemensPPI.Station = byte.Parse(textBox15.Text);
				OperateResult operateResult = DeviceConnectPLC(siemensPPI);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(siemensPPI, "V100");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(siemensPPI, "V100", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => siemensPPI.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					control.SetDevice(siemensPPI, "M100");
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(siemensPPI, "Station");
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
			pipeSelectControl1.ExtraCloseAction(siemensPPI);
			siemensPPI?.ConnectClose();
		}

		private void Test1()
		{
			OperateResult<bool> operateResult = siemensPPI.ReadBool("M100.4");
			if (operateResult.IsSuccess)
			{
				bool content = operateResult.Content;
			}
			else
			{
				string message = operateResult.Message;
			}
			OperateResult operateResult2 = siemensPPI.Write("M100.4", true);
			if (!operateResult2.IsSuccess)
			{
				string message2 = operateResult2.Message;
			}
		}

		private void Test2()
		{
			byte content = siemensPPI.ReadByte("M100").Content;
			short content2 = siemensPPI.ReadInt16("M100").Content;
			ushort content3 = siemensPPI.ReadUInt16("M100").Content;
			int content4 = siemensPPI.ReadInt32("M100").Content;
			uint content5 = siemensPPI.ReadUInt32("M100").Content;
			float content6 = siemensPPI.ReadFloat("M100").Content;
			double content7 = siemensPPI.ReadDouble("M100").Content;
			string content8 = siemensPPI.ReadString("M100", 10).Content;
			IByteTransform byteTransform = new ReverseBytesTransform();
		}

		private void Test3()
		{
			bool content = siemensPPI.ReadBool("M100.7").Content;
			byte content2 = siemensPPI.ReadByte("M100").Content;
			short content3 = siemensPPI.ReadInt16("M100").Content;
			ushort content4 = siemensPPI.ReadUInt16("M100").Content;
			int content5 = siemensPPI.ReadInt32("M100").Content;
			uint content6 = siemensPPI.ReadUInt32("M100").Content;
			float content7 = siemensPPI.ReadFloat("M100").Content;
			long content8 = siemensPPI.ReadInt64("M100").Content;
			ulong content9 = siemensPPI.ReadUInt64("M100").Content;
			double content10 = siemensPPI.ReadDouble("M100").Content;
			string content11 = siemensPPI.ReadString("M100", 10).Content;
			siemensPPI.Write("M100.7", true);
			siemensPPI.Write("M100", 51);
			siemensPPI.Write("M100", (short)12345);
			siemensPPI.Write("M100", (ushort)45678);
			siemensPPI.Write("M100", 123456789);
			siemensPPI.Write("M100", 3456789123u);
			siemensPPI.Write("M100", 123.456f);
			siemensPPI.Write("M100", 1234556434534545L);
			siemensPPI.Write("M100", 523434234234343uL);
			siemensPPI.Write("M100", 123.456);
			siemensPPI.Write("M100", "K123456789");
			OperateResult<byte[]> operateResult = siemensPPI.Read("M100", 10);
			if (operateResult.IsSuccess)
			{
				byte b = operateResult.Content[0];
				byte b2 = operateResult.Content[1];
				byte b3 = operateResult.Content[2];
				byte b4 = operateResult.Content[3];
				byte b5 = operateResult.Content[4];
				byte b6 = operateResult.Content[5];
				byte b7 = operateResult.Content[6];
				byte b8 = operateResult.Content[7];
				byte b9 = operateResult.Content[8];
				byte b10 = operateResult.Content[9];
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox15.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
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
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Location = new System.Drawing.Point(3, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 62);
			this.panel1.TabIndex = 0;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(901, 2);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 46;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(804, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 45;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox15.Location = new System.Drawing.Point(63, 34);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(37, 23);
			this.textBox15.TabIndex = 40;
			this.textBox15.Text = "2";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(8, 37);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(44, 17);
			this.label23.TabIndex = 39;
			this.label23.Text = "站号：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 97);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 544);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 542);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8685855.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "PPI Over Tcp";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 2);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 47;
			this.pipeSelectControl1.TcpPortText = "2000";
			this.pipeSelectControl1.UdpPortText = "2000";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSiemensPPIOverTcp";
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
