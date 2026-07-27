using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.LogNet;
using HslCommunication.Profinet.Melsec;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Melsec;

namespace HslCommunicationDemo
{
	public class FormMelsecSerialOverTcp : HslFormContent
	{
		private MelsecFxSerialOverTcp melsec_net = null;

		private MelsecSerialControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private UserControlHead userControlHead1;

		private CheckBox checkBox_newVersion;

		private CheckBox checkBox_got;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormMelsecSerialOverTcp()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			checkBox_newVersion.CheckedChanged += CheckBox1_CheckedChanged;
			control = new MelsecSerialControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetFxSerialAddress());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (melsec_net != null)
			{
				melsec_net.IsNewVersion = checkBox_newVersion.Checked;
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Melsec Read PLC Demo";
				checkBox_newVersion.Text = "New Version Message?";
				checkBox_got.Text = "Use Got";
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
			melsec_net?.ConnectClose();
			melsec_net = new MelsecFxSerialOverTcp();
			melsec_net.IsNewVersion = checkBox_newVersion.Checked;
			melsec_net.UseGOT = checkBox_got.Checked;
			melsec_net.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(melsec_net);
				OperateResult operateResult = DeviceConnectPLC(melsec_net);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(melsec_net, "D100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(melsec_net, "D100", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => melsec_net.ReadFromCoreServer(m), string.Empty, string.Empty);
					control.SetDevice(melsec_net, "D100");
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(melsec_net, "IsNewVersion", "UseGOT", "SleepTime");
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
			pipeSelectControl1.ExtraCloseAction(melsec_net);
			melsec_net?.ConnectClose();
		}

		private void test1()
		{
			OperateResult<bool[]> operateResult = melsec_net.ReadBool("M100", 10);
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

		private void test3()
		{
			short content = melsec_net.ReadInt16("D100").Content;
			ushort content2 = melsec_net.ReadUInt16("D100").Content;
			int content3 = melsec_net.ReadInt32("D100").Content;
			uint content4 = melsec_net.ReadUInt32("D100").Content;
			long content5 = melsec_net.ReadInt64("D100").Content;
			ulong content6 = melsec_net.ReadUInt64("D100").Content;
			float content7 = melsec_net.ReadFloat("D100").Content;
			double content8 = melsec_net.ReadDouble("D100").Content;
			string content9 = melsec_net.ReadString("D100", 10).Content;
		}

		private void test4()
		{
			melsec_net.Write("D100", (short)5);
			melsec_net.Write("D100", (ushort)5);
			melsec_net.Write("D100", 5);
			melsec_net.Write("D100", 5u);
			melsec_net.Write("D100", 5L);
			melsec_net.Write("D100", 5uL);
			melsec_net.Write("D100", 5f);
			melsec_net.Write("D100", 5.0);
			melsec_net.Write("D100", "12345678");
		}

		private void test5()
		{
			OperateResult<byte[]> operateResult = melsec_net.Read("D100", 10);
			if (operateResult.IsSuccess)
			{
				int num = melsec_net.ByteTransform.TransInt32(operateResult.Content, 0);
				float num2 = melsec_net.ByteTransform.TransSingle(operateResult.Content, 4);
				short num3 = melsec_net.ByteTransform.TransInt16(operateResult.Content, 8);
				string @string = Encoding.ASCII.GetString(operateResult.Content, 10, 10);
			}
		}

		private void test6()
		{
			OperateResult<UserType> operateResult = melsec_net.ReadCustomer<UserType>("D100");
			if (operateResult.IsSuccess)
			{
				UserType content = operateResult.Content;
			}
			melsec_net.WriteCustomer("D100", new UserType());
			melsec_net.LogNet = new LogNetSingle(Application.StartupPath + "\\Logs.txt");
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue("NewVersion", checkBox_newVersion.Checked);
			element.SetAttributeValue("UseGOT", checkBox_got.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			checkBox_newVersion.Checked = HslFormContent.GetXmlValue(element, "NewVersion", checkBox_newVersion.Checked, bool.Parse);
			checkBox_got.Checked = HslFormContent.GetXmlValue(element, "UseGOT", checkBox_got.Checked, bool.Parse);
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button_active_plc_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = melsec_net.ActivePlc();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Active Successful");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
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
			this.checkBox_got = new System.Windows.Forms.CheckBox();
			this.checkBox_newVersion = new System.Windows.Forms.CheckBox();
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
			this.panel1.Controls.Add(this.checkBox_got);
			this.panel1.Controls.Add(this.checkBox_newVersion);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(4, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 60);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 34;
			this.pipeSelectControl1.TcpPortText = "5014";
			this.pipeSelectControl1.UdpPortText = "5014";
			this.checkBox_got.AutoSize = true;
			this.checkBox_got.Location = new System.Drawing.Point(160, 36);
			this.checkBox_got.Name = "checkBox_got";
			this.checkBox_got.Size = new System.Drawing.Size(77, 21);
			this.checkBox_got.TabIndex = 33;
			this.checkBox_got.Text = "GOT连接";
			this.checkBox_got.UseVisualStyleBackColor = true;
			this.checkBox_newVersion.AutoSize = true;
			this.checkBox_newVersion.Checked = true;
			this.checkBox_newVersion.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_newVersion.Location = new System.Drawing.Point(7, 36);
			this.checkBox_newVersion.Name = "checkBox_newVersion";
			this.checkBox_newVersion.Size = new System.Drawing.Size(99, 21);
			this.checkBox_newVersion.TabIndex = 32;
			this.checkBox_newVersion.Text = "新版本报文？";
			this.checkBox_newVersion.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(900, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(803, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 100);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 542);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 540);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "fx serial over tcp";
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
			base.Name = "FormMelsecSerialOverTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "三菱PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
