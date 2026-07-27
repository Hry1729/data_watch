using System;
using System.Collections.Generic;
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
	public class FormSiemens200 : HslFormContent
	{
		private SiemensS7Net siemensTcpNet = null;

		private SiemensPLCS siemensPLCSelected = SiemensPLCS.S200Smart;

		private SiemensS7Control control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private SiemensS7WriteControl randomWriteControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		protected UserControlHead userControlHead1;

		private TextBox textBox4;

		private Label label5;

		private TextBox textBox3;

		private Label label4;

		private Label label6;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormSiemens200(SiemensPLCS siemensPLCS)
		{
			InitializeComponent();
			siemensPLCSelected = siemensPLCS;
			siemensTcpNet = new SiemensS7Net(siemensPLCS);
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			if (siemensPLCSelected == SiemensPLCS.S200Smart)
			{
				label4.Visible = false;
				label5.Visible = false;
				textBox3.Visible = false;
				textBox4.Visible = false;
				label6.Visible = false;
			}
			control = new SiemensS7Control();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetSiemensS7Address());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
			randomWriteControl = new SiemensS7WriteControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(randomWriteControl, false, (Program.Language == 1) ? "离散写入" : "Random Write");
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Siemens Read PLC Demo";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "Hexadecimal!!!";
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
				if (siemensPLCSelected == SiemensPLCS.S200)
				{
					siemensTcpNet.LocalTSAP = Convert.ToInt32(textBox4.Text, 16);
					siemensTcpNet.DestTSAP = Convert.ToInt32(textBox3.Text, 16);
				}
				siemensTcpNet.LogNet = base.LogNet;
				OperateResult operateResult = DeviceConnectPLC(siemensTcpNet);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(siemensTcpNet, "M100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(siemensTcpNet, "M100", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadRandom(siemensTcpNet.Read, "M100;M200;DB1.0;Q0");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => siemensTcpNet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					control.SetDevice(siemensTcpNet, "M100");
					randomWriteControl.SetDevice(siemensTcpNet, "M100");
					List<string> list = new List<string>();
					if (siemensPLCSelected == SiemensPLCS.S200)
					{
						list.Add("LocalTSAP");
						list.Add("DestTSAP");
					}
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(siemensTcpNet, list.ToArray());
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
			siemensTcpNet.ConnectClose();
		}

		private void Test1()
		{
			OperateResult<bool> operateResult = siemensTcpNet.ReadBool("M100.4");
			if (operateResult.IsSuccess)
			{
				bool content = operateResult.Content;
			}
			else
			{
				string message = operateResult.Message;
			}
			OperateResult operateResult2 = siemensTcpNet.Write("M100.4", true);
			if (!operateResult2.IsSuccess)
			{
				string message2 = operateResult2.Message;
			}
		}

		private void Test2()
		{
			byte content = siemensTcpNet.ReadByte("M100").Content;
			short content2 = siemensTcpNet.ReadInt16("M100").Content;
			ushort content3 = siemensTcpNet.ReadUInt16("M100").Content;
			int content4 = siemensTcpNet.ReadInt32("M100").Content;
			uint content5 = siemensTcpNet.ReadUInt32("M100").Content;
			float content6 = siemensTcpNet.ReadFloat("M100").Content;
			double content7 = siemensTcpNet.ReadDouble("M100").Content;
			string content8 = siemensTcpNet.ReadString("M100", 10).Content;
			IByteTransform byteTransform = new ReverseBytesTransform();
		}

		private void Test3()
		{
			bool content = siemensTcpNet.ReadBool("M100.7").Content;
			byte content2 = siemensTcpNet.ReadByte("M100").Content;
			short content3 = siemensTcpNet.ReadInt16("M100").Content;
			ushort content4 = siemensTcpNet.ReadUInt16("M100").Content;
			int content5 = siemensTcpNet.ReadInt32("M100").Content;
			uint content6 = siemensTcpNet.ReadUInt32("M100").Content;
			float content7 = siemensTcpNet.ReadFloat("M100").Content;
			long content8 = siemensTcpNet.ReadInt64("M100").Content;
			ulong content9 = siemensTcpNet.ReadUInt64("M100").Content;
			double content10 = siemensTcpNet.ReadDouble("M100").Content;
			string content11 = siemensTcpNet.ReadString("M100", 10).Content;
			siemensTcpNet.Write("M100.7", true);
			siemensTcpNet.Write("M100", 51);
			siemensTcpNet.Write("M100", (short)12345);
			siemensTcpNet.Write("M100", (ushort)45678);
			siemensTcpNet.Write("M100", 123456789);
			siemensTcpNet.Write("M100", 3456789123u);
			siemensTcpNet.Write("M100", 123.456f);
			siemensTcpNet.Write("M100", 1234556434534545L);
			siemensTcpNet.Write("M100", 523434234234343uL);
			siemensTcpNet.Write("M100", 123.456);
			siemensTcpNet.Write("M100", "K123456789");
			OperateResult<byte[]> operateResult = siemensTcpNet.Read("M100", 10);
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
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
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
			this.label6 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox4);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 60);
			this.panel1.TabIndex = 0;
			this.label6.AutoSize = true;
			this.label6.ForeColor = System.Drawing.Color.Red;
			this.label6.Location = new System.Drawing.Point(287, 35);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(82, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "输入16进制!!!";
			this.textBox4.Location = new System.Drawing.Point(89, 32);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(54, 23);
			this.textBox4.TabIndex = 15;
			this.textBox4.Text = "4D57";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(8, 35);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(79, 17);
			this.label5.TabIndex = 14;
			this.label5.Text = "LocalTSAP：";
			this.textBox3.Location = new System.Drawing.Point(221, 32);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(51, 23);
			this.textBox3.TabIndex = 13;
			this.textBox3.Text = "4D57";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(147, 35);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(75, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "DestTSAP：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(901, 1);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(805, 1);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
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
			this.userControlHead1.ProtocolInfo = "s7";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(6, 1);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 17;
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
			base.Name = "FormSiemens200";
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
