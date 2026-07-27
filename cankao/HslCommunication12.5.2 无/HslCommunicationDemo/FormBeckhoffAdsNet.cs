using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Beckhoff;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Beckhoff;

namespace HslCommunicationDemo
{
	public class FormBeckhoffAdsNet : HslFormContent
	{
		private BeckhoffAdsNet beckhoffAdsNet = null;

		private BeckhoffAdsNetControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private UserControlHead userControlHead1;

		private TextBox textBox14;

		private Label label5;

		private Label label8;

		private TextBox textBox15;

		private Label label15;

		private CheckBox checkBox_tag;

		private Label label17;

		private TextBox textBox_ams_port;

		private CheckBox checkBox_auto;

		private Label label6;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormBeckhoffAdsNet()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			checkBox_auto.CheckedChanged += CheckBox_auto_CheckedChanged;
			CheckBox_auto_CheckedChanged(checkBox_auto, e);
			control = new BeckhoffAdsNetControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetDeviceAddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void CheckBox_auto_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox_auto.Checked)
			{
				textBox14.Enabled = false;
				textBox15.Enabled = false;
				textBox_ams_port.Enabled = true;
			}
			else
			{
				textBox14.Enabled = true;
				textBox15.Enabled = true;
				textBox_ams_port.Enabled = false;
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Beckhoff AdsNet Read PLC Demo";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label8.Text = "case: 192.168.1.100.1.1:801 or 192.168.1.100.1.1";
				checkBox_tag.Text = "Tag Cache";
				label17.Text = "TwinCAT2, port number 801; TwinCAT3, port number 851";
				checkBox_auto.Text = "Auto Ams NetID";
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
			beckhoffAdsNet?.ConnectClose();
			beckhoffAdsNet = new BeckhoffAdsNet();
			beckhoffAdsNet.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(beckhoffAdsNet);
				if (checkBox_auto.Checked)
				{
					beckhoffAdsNet.UseAutoAmsNetID = true;
					if (!string.IsNullOrEmpty(textBox_ams_port.Text))
					{
						beckhoffAdsNet.AmsPort = ushort.Parse(textBox_ams_port.Text);
					}
				}
				else
				{
					beckhoffAdsNet.SetTargetAMSNetId(textBox14.Text);
					beckhoffAdsNet.SetSenderAMSNetId(textBox15.Text);
				}
				beckhoffAdsNet.UseTagCache = checkBox_tag.Checked;
				OperateResult operateResult = DeviceConnectPLC(beckhoffAdsNet);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(beckhoffAdsNet, "M100", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(beckhoffAdsNet, "M100", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadRandom(beckhoffAdsNet.Read, "M100;s=MAIN.dd;s=MAIN.a         Length input: 2;4;2");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => beckhoffAdsNet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					control.SetDevice(beckhoffAdsNet, "M100");
					if (checkBox_auto.Checked)
					{
						textBox14.Text = beckhoffAdsNet.GetTargetAMSNetId();
						textBox15.Text = beckhoffAdsNet.GetSenderAMSNetId();
						codeExampleControl.SetCodeText(beckhoffAdsNet, "UseAutoAmsNetID", "UseTagCache", "AmsPort");
						return;
					}
					if (string.IsNullOrEmpty(textBox14.Text))
					{
						textBox14.Text = beckhoffAdsNet.GetTargetAMSNetId();
					}
					if (string.IsNullOrEmpty(textBox15.Text))
					{
						textBox15.Text = beckhoffAdsNet.GetSenderAMSNetId();
					}
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(beckhoffAdsNet, "UseAutoAmsNetID", "UseTagCache");
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + Environment.NewLine + operateResult.ToMessageShowString());
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
			pipeSelectControl1.ExtraCloseAction(beckhoffAdsNet);
			beckhoffAdsNet?.ConnectClose();
		}

		private void Test1()
		{
			OperateResult<bool> operateResult = beckhoffAdsNet.ReadBool("M100.4");
			if (operateResult.IsSuccess)
			{
				bool content = operateResult.Content;
			}
			else
			{
				string message = operateResult.Message;
			}
			OperateResult operateResult2 = beckhoffAdsNet.Write("M100.4", true);
			if (!operateResult2.IsSuccess)
			{
				string message2 = operateResult2.Message;
			}
		}

		private void Test2()
		{
			byte content = beckhoffAdsNet.ReadByte("M100").Content;
			short content2 = beckhoffAdsNet.ReadInt16("M100").Content;
			ushort content3 = beckhoffAdsNet.ReadUInt16("M100").Content;
			int content4 = beckhoffAdsNet.ReadInt32("M100").Content;
			uint content5 = beckhoffAdsNet.ReadUInt32("M100").Content;
			float content6 = beckhoffAdsNet.ReadFloat("M100").Content;
			double content7 = beckhoffAdsNet.ReadDouble("M100").Content;
			string content8 = beckhoffAdsNet.ReadString("M100", 10).Content;
			IByteTransform byteTransform = new ReverseBytesTransform();
		}

		private void Test3()
		{
			bool content = beckhoffAdsNet.ReadBool("M100.7").Content;
			byte content2 = beckhoffAdsNet.ReadByte("M100").Content;
			short content3 = beckhoffAdsNet.ReadInt16("M100").Content;
			ushort content4 = beckhoffAdsNet.ReadUInt16("M100").Content;
			int content5 = beckhoffAdsNet.ReadInt32("M100").Content;
			uint content6 = beckhoffAdsNet.ReadUInt32("M100").Content;
			float content7 = beckhoffAdsNet.ReadFloat("M100").Content;
			long content8 = beckhoffAdsNet.ReadInt64("M100").Content;
			ulong content9 = beckhoffAdsNet.ReadUInt64("M100").Content;
			double content10 = beckhoffAdsNet.ReadDouble("M100").Content;
			string content11 = beckhoffAdsNet.ReadString("M100", 10).Content;
			beckhoffAdsNet.Write("M100.7", true);
			beckhoffAdsNet.Write("M100", 51);
			beckhoffAdsNet.Write("M100", (short)12345);
			beckhoffAdsNet.Write("M100", (ushort)45678);
			beckhoffAdsNet.Write("M100", 123456789);
			beckhoffAdsNet.Write("M100", 3456789123u);
			beckhoffAdsNet.Write("M100", 123.456f);
			beckhoffAdsNet.Write("M100", 1234556434534545L);
			beckhoffAdsNet.Write("M100", 523434234234343uL);
			beckhoffAdsNet.Write("M100", 123.456);
			beckhoffAdsNet.Write("M100", "K123456789");
			OperateResult<byte[]> operateResult = beckhoffAdsNet.Read("M100", 10);
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
			element.SetAttributeValue(DemoDeviceList.XmlTarget, textBox14.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSender, textBox15.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTagCache, checkBox_tag.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox14.Text = element.Attribute(DemoDeviceList.XmlTarget).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlSender).Value;
			checkBox_tag.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlTagCache).Value);
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void userControlHead1_Load(object sender, EventArgs e)
		{
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
			this.textBox_ams_port = new System.Windows.Forms.TextBox();
			this.label17 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.checkBox_auto = new System.Windows.Forms.CheckBox();
			this.label6 = new System.Windows.Forms.Label();
			this.checkBox_tag = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox_ams_port);
			this.panel1.Controls.Add(this.label17);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label15);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.textBox14);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.checkBox_auto);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.checkBox_tag);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1011, 85);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(6, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 21;
			this.pipeSelectControl1.TcpPortText = "48898";
			this.pipeSelectControl1.UdpPortText = "48898";
			this.textBox_ams_port.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_ams_port.Location = new System.Drawing.Point(928, 36);
			this.textBox_ams_port.Name = "textBox_ams_port";
			this.textBox_ams_port.Size = new System.Drawing.Size(72, 23);
			this.textBox_ams_port.TabIndex = 19;
			this.textBox_ams_port.Text = "851";
			this.label17.ForeColor = System.Drawing.Color.FromArgb(192, 64, 0);
			this.label17.Location = new System.Drawing.Point(415, 62);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(585, 17);
			this.label17.TabIndex = 16;
			this.label17.Text = "TwinCAT2，端口号801,811,821,831；TwinCAT3，端口号为851,852,853";
			this.label17.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.textBox15.Location = new System.Drawing.Point(97, 58);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(179, 23);
			this.textBox15.TabIndex = 14;
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(3, 61);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(98, 17);
			this.label15.TabIndex = 13;
			this.label15.Text = "Sender NetId：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(282, 36);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(167, 17);
			this.label8.TabIndex = 12;
			this.label8.Text = "示例：192.168.1.100.1.1:801";
			this.textBox14.Location = new System.Drawing.Point(97, 32);
			this.textBox14.Name = "textBox14";
			this.textBox14.Size = new System.Drawing.Size(179, 23);
			this.textBox14.TabIndex = 11;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(3, 35);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(95, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "Target NetId：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(911, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(94, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(812, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(93, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.checkBox_auto.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_auto.AutoSize = true;
			this.checkBox_auto.Location = new System.Drawing.Point(729, 37);
			this.checkBox_auto.Name = "checkBox_auto";
			this.checkBox_auto.Size = new System.Drawing.Size(115, 21);
			this.checkBox_auto.TabIndex = 17;
			this.checkBox_auto.Text = "自动AMS NetId";
			this.checkBox_auto.UseVisualStyleBackColor = true;
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label6.AutoSize = true;
			this.label6.ForeColor = System.Drawing.Color.Green;
			this.label6.Location = new System.Drawing.Point(859, 39);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(64, 17);
			this.label6.TabIndex = 20;
			this.label6.Text = "Ams Port:";
			this.checkBox_tag.AutoSize = true;
			this.checkBox_tag.Checked = true;
			this.checkBox_tag.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_tag.Location = new System.Drawing.Point(285, 59);
			this.checkBox_tag.Name = "checkBox_tag";
			this.checkBox_tag.Size = new System.Drawing.Size(75, 21);
			this.checkBox_tag.TabIndex = 15;
			this.checkBox_tag.Text = "标签缓存";
			this.checkBox_tag.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 124);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1011, 532);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(1009, 530);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/12051529.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "ADS";
			this.userControlHead1.Size = new System.Drawing.Size(1018, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.userControlHead1.Load += new System.EventHandler(userControlHead1_Load);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1018, 658);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormBeckhoffAdsNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "倍福PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
