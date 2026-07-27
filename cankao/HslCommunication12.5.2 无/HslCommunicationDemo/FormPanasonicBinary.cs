using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.LogNet;
using HslCommunication.Profinet.Panasonic;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Panasonic;

namespace HslCommunicationDemo
{
	public class FormPanasonicBinary : HslFormContent
	{
		private PanasonicMcNet panasonic_net = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private UserControlHead userControlHead1;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormPanasonicBinary()
		{
			InitializeComponent();
			panasonic_net = new PanasonicMcNet();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetMCAddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Panasonic Read PLC Demo";
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
			panasonic_net.ConnectClose();
			panasonic_net.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(panasonic_net);
				OperateResult operateResult = DeviceConnectPLC(panasonic_net);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(panasonic_net, "D100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(panasonic_net, "D100", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadRandom(panasonic_net.ReadRandom, "D100;D200;D500");
					userControlReadWriteDevice1.BatchRead.SetReadWordRandom(panasonic_net.ReadRandom, "D100;D200;D500");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => panasonic_net.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(panasonic_net);
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
			pipeSelectControl1.ExtraCloseAction(panasonic_net);
			panasonic_net?.ConnectClose();
		}

		private void test1()
		{
			OperateResult<bool[]> operateResult = panasonic_net.ReadBool("M100", 10);
			if (operateResult.IsSuccess)
			{
				bool flag = operateResult.Content[0];
				bool flag2 = operateResult.Content[9];
			}
			else
			{
				Console.WriteLine("Read failed: " + operateResult.ToMessageShowString());
			}
		}

		private void test2()
		{
			bool[] value = new bool[10] { true, false, true, true, false, true, false, true, true, false };
			if (!panasonic_net.Write("M100", value).IsSuccess)
			{
			}
		}

		private void test3()
		{
			short content = panasonic_net.ReadInt16("D100").Content;
			ushort content2 = panasonic_net.ReadUInt16("D100").Content;
			int content3 = panasonic_net.ReadInt32("D100").Content;
			uint content4 = panasonic_net.ReadUInt32("D100").Content;
			long content5 = panasonic_net.ReadInt64("D100").Content;
			ulong content6 = panasonic_net.ReadUInt64("D100").Content;
			float content7 = panasonic_net.ReadFloat("D100").Content;
			double content8 = panasonic_net.ReadDouble("D100").Content;
			string content9 = panasonic_net.ReadString("D100", 10).Content;
		}

		private void test4()
		{
			panasonic_net.Write("D100", (short)5);
			panasonic_net.Write("D100", (ushort)5);
			panasonic_net.Write("D100", 5);
			panasonic_net.Write("D100", 5u);
			panasonic_net.Write("D100", 5L);
			panasonic_net.Write("D100", 5uL);
			panasonic_net.Write("D100", 5f);
			panasonic_net.Write("D100", 5.0);
			panasonic_net.Write("D100", "12345678");
		}

		private void test5()
		{
			OperateResult<byte[]> operateResult = panasonic_net.Read("D100", 10);
			if (operateResult.IsSuccess)
			{
				int num = panasonic_net.ByteTransform.TransInt32(operateResult.Content, 0);
				float num2 = panasonic_net.ByteTransform.TransSingle(operateResult.Content, 4);
				short num3 = panasonic_net.ByteTransform.TransInt16(operateResult.Content, 8);
				string @string = Encoding.ASCII.GetString(operateResult.Content, 10, 10);
			}
		}

		private void test6()
		{
			OperateResult<UserType> operateResult = panasonic_net.ReadCustomer<UserType>("D100");
			if (operateResult.IsSuccess)
			{
				UserType content = operateResult.Content;
			}
			panasonic_net.WriteCustomer("D100", new UserType());
			panasonic_net.LogNet = new LogNetSingle(Application.StartupPath + "\\Logs.txt");
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
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 43);
			this.panel1.TabIndex = 0;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(898, 7);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(800, 7);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 82);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 559);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 557);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MC Qna3E Binary";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 6);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 6;
			this.pipeSelectControl1.TcpPortText = "5000";
			this.pipeSelectControl1.UdpPortText = "5000";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormPanasonicBinary";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "松下 PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
