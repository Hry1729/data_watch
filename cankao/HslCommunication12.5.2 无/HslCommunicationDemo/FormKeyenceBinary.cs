using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.LogNet;
using HslCommunication.Profinet.Keyence;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Keyence;
using HslCommunicationDemo.PLC.Melsec;

namespace HslCommunicationDemo
{
	public class FormKeyenceBinary : HslFormContent
	{
		private KeyenceMcNet keyence_net = null;

		private McQna3EControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private UserControlHead userControlHead1;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private CheckBox checkBox_EnableWriteBitToWordRegister;

		private CheckBox checkBox_string_reverse;

		private PipeSelectControl pipeSelectControl1;

		public FormKeyenceBinary()
		{
			InitializeComponent();
			keyence_net = new KeyenceMcNet();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new McQna3EControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(HslCommunicationDemo.PLC.Keyence.Helper.GetKeyenceMcAddress());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Keyence Read PLC Demo";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				checkBox_string_reverse.Text = "string reverse by word";
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
			keyence_net.ConnectClose();
			keyence_net.LogNet = base.LogNet;
			keyence_net.EnableWriteBitToWordRegister = checkBox_EnableWriteBitToWordRegister.Checked;
			keyence_net.ByteTransform.IsStringReverseByteWord = checkBox_string_reverse.Checked;
			try
			{
				pipeSelectControl1.IniPipe(keyence_net);
				OperateResult operateResult = DeviceConnectPLC(keyence_net);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(keyence_net, "D100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(keyence_net, "D100", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadRandom(keyence_net.ReadRandom, "D100;W100;D500");
					userControlReadWriteDevice1.BatchRead.SetReadWordRandom(keyence_net.ReadRandom, "D100;W100;D500");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => keyence_net.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					control.SetDevice(keyence_net, "D100", DemoUtils.PlcDeviceName);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(keyence_net, "EnableWriteBitToWordRegister", "ByteTransform.IsStringReverseByteWord");
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + ":" + operateResult.Message);
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
			pipeSelectControl1.ExtraCloseAction(keyence_net);
			keyence_net?.ConnectClose();
		}

		private void test1()
		{
			OperateResult<bool[]> operateResult = keyence_net.ReadBool("M100", 10);
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
			if (!keyence_net.Write("M100", value).IsSuccess)
			{
			}
		}

		private void test3()
		{
			short content = keyence_net.ReadInt16("D100").Content;
			ushort content2 = keyence_net.ReadUInt16("D100").Content;
			int content3 = keyence_net.ReadInt32("D100").Content;
			uint content4 = keyence_net.ReadUInt32("D100").Content;
			long content5 = keyence_net.ReadInt64("D100").Content;
			ulong content6 = keyence_net.ReadUInt64("D100").Content;
			float content7 = keyence_net.ReadFloat("D100").Content;
			double content8 = keyence_net.ReadDouble("D100").Content;
			string content9 = keyence_net.ReadString("D100", 10).Content;
		}

		private void test4()
		{
			keyence_net.Write("D100", (short)5);
			keyence_net.Write("D100", (ushort)5);
			keyence_net.Write("D100", 5);
			keyence_net.Write("D100", 5u);
			keyence_net.Write("D100", 5L);
			keyence_net.Write("D100", 5uL);
			keyence_net.Write("D100", 5f);
			keyence_net.Write("D100", 5.0);
			keyence_net.Write("D100", "12345678");
		}

		private void test5()
		{
			OperateResult<byte[]> operateResult = keyence_net.Read("D100", 10);
			if (operateResult.IsSuccess)
			{
				int num = keyence_net.ByteTransform.TransInt32(operateResult.Content, 0);
				float num2 = keyence_net.ByteTransform.TransSingle(operateResult.Content, 4);
				short num3 = keyence_net.ByteTransform.TransInt16(operateResult.Content, 8);
				string @string = Encoding.ASCII.GetString(operateResult.Content, 10, 10);
			}
		}

		private void test6()
		{
			OperateResult<UserType> operateResult = keyence_net.ReadCustomer<UserType>("D100");
			if (operateResult.IsSuccess)
			{
				UserType content = operateResult.Content;
			}
			keyence_net.WriteCustomer("D100", new UserType());
			keyence_net.LogNet = new LogNetSingle(Application.StartupPath + "\\Logs.txt");
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue("EnableWriteBitToWordRegister", checkBox_EnableWriteBitToWordRegister.Checked);
			element.SetAttributeValue("IsStringReverseByteWord", checkBox_string_reverse.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			checkBox_EnableWriteBitToWordRegister.Checked = HslFormContent.GetXmlValue(element, "EnableWriteBitToWordRegister", false, bool.Parse);
			checkBox_string_reverse.Checked = HslFormContent.GetXmlValue(element, "IsStringReverseByteWord", false, bool.Parse);
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
			this.checkBox_string_reverse = new System.Windows.Forms.CheckBox();
			this.checkBox_EnableWriteBitToWordRegister = new System.Windows.Forms.CheckBox();
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
			this.panel1.Controls.Add(this.checkBox_string_reverse);
			this.panel1.Controls.Add(this.checkBox_EnableWriteBitToWordRegister);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 62);
			this.panel1.TabIndex = 0;
			this.checkBox_string_reverse.AutoSize = true;
			this.checkBox_string_reverse.Location = new System.Drawing.Point(6, 35);
			this.checkBox_string_reverse.Name = "checkBox_string_reverse";
			this.checkBox_string_reverse.Size = new System.Drawing.Size(87, 21);
			this.checkBox_string_reverse.TabIndex = 26;
			this.checkBox_string_reverse.Text = "字符串颠倒";
			this.checkBox_string_reverse.UseVisualStyleBackColor = true;
			this.checkBox_EnableWriteBitToWordRegister.AutoSize = true;
			this.checkBox_EnableWriteBitToWordRegister.Location = new System.Drawing.Point(219, 35);
			this.checkBox_EnableWriteBitToWordRegister.Name = "checkBox_EnableWriteBitToWordRegister";
			this.checkBox_EnableWriteBitToWordRegister.Size = new System.Drawing.Size(228, 21);
			this.checkBox_EnableWriteBitToWordRegister.TabIndex = 10;
			this.checkBox_EnableWriteBitToWordRegister.Text = "Enable Write Bit To Word Register";
			this.checkBox_EnableWriteBitToWordRegister.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(898, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(801, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
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
			this.pipeSelectControl1.Location = new System.Drawing.Point(2, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 27;
			this.pipeSelectControl1.TcpPortText = "6000";
			this.pipeSelectControl1.UdpPortText = "6000";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormKeyenceBinary";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "基恩士 PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
