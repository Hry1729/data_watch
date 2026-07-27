using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Profinet.AllenBradley;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.AllenBrandly;

namespace HslCommunicationDemo
{
	public class FormAllenBrandly : HslFormContent
	{
		private AllenBradleyNet allenBradleyNet = null;

		private AllenBrandlyControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private AllenBrandlyTagTypeCacheControl AllenBrandlyTagTypeCacheControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label23;

		private UserControlHead userControlHead1;

		private TextBox textBox_router;

		private Label label8;

		private Label label9;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		private CheckBox checkBox1;

		private CheckBox checkBox2;

		public FormAllenBrandly()
		{
			InitializeComponent();
			allenBradleyNet = new AllenBradleyNet("192.168.0.110");
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new AllenBrandlyControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			AllenBrandlyTagTypeCacheControl = new AllenBrandlyTagTypeCacheControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(AllenBrandlyTagTypeCacheControl, false, (Program.Language == 1) ? "变量缓存" : "Tag Cache");
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetCIPAddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "AllenBrandly Read PLC Demo";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				checkBox1.Text = "Context Match";
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
			if (!byte.TryParse(textBox15.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.SlotInputWrong);
				return;
			}
			allenBradleyNet.Slot = result;
			allenBradleyNet.ContextCheck = checkBox1.Checked;
			allenBradleyNet.ReadArrayUseSegment = checkBox2.Checked;
			allenBradleyNet.LogNet = base.LogNet;
			if (!string.IsNullOrEmpty(textBox_router.Text))
			{
				allenBradleyNet.MessageRouter = new MessageRouter(textBox_router.Text);
			}
			try
			{
				pipeSelectControl1.IniPipe(allenBradleyNet);
				OperateResult operateResult = DeviceConnectPLC(allenBradleyNet);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(allenBradleyNet, "A1", true, 1);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(allenBradleyNet, "A1", string.Empty);
					userControlReadWriteDevice1.BatchRead.SetReadRandom(allenBradleyNet.Read, "A1;A2    Length input \"1;1\"");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => allenBradleyNet.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => allenBradleyNet.ReadEipFromServer(m), "EIP", "EIP Message, example: ");
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => allenBradleyNet.ReadCipFromServer(m), "CIP", "CIP Message, example: ");
					control.SetDevice(allenBradleyNet, "A1");
					AllenBrandlyTagTypeCacheControl.SetDevice(allenBradleyNet);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					List<string> list = new List<string>();
					list.Add("Slot");
					if (allenBradleyNet.ContextCheck)
					{
						list.Add("ContextCheck");
					}
					if (!allenBradleyNet.ReadArrayUseSegment)
					{
						list.Add("ReadArrayUseSegment");
					}
					if (!string.IsNullOrEmpty(textBox_router.Text))
					{
						list.Add("MessageRouter");
					}
					codeExampleControl.SetCodeText(allenBradleyNet, list.ToArray());
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
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
			pipeSelectControl1.ExtraCloseAction(allenBradleyNet);
			allenBradleyNet?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlSlot, textBox15.Text);
			element.SetAttributeValue("Router", textBox_router.Text);
			element.SetAttributeValue("Context", checkBox1.Checked.ToString());
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlSlot).Value;
			textBox_router.Text = HslFormContent.GetXmlValue(element, "Router", textBox_router.Text, (string m) => m);
			checkBox1.Checked = HslFormContent.GetXmlValue(element, "Context", false, (string m) => bool.Parse(m));
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
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_router = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.label9 = new System.Windows.Forms.Label();
			this.label23 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.textBox_router);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(993, 65);
			this.panel1.TabIndex = 0;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(590, 38);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(123, 21);
			this.checkBox1.TabIndex = 16;
			this.checkBox1.Text = "Context 信息匹配";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 4);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 15;
			this.pipeSelectControl1.TcpPortText = "44818";
			this.pipeSelectControl1.UdpPortText = "44818";
			this.textBox15.Location = new System.Drawing.Point(52, 37);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(32, 23);
			this.textBox15.TabIndex = 9;
			this.textBox15.Text = "0";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(90, 40);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(47, 17);
			this.label8.TabIndex = 12;
			this.label8.Text = "Router";
			this.textBox_router.Location = new System.Drawing.Point(148, 37);
			this.textBox_router.Name = "textBox_router";
			this.textBox_router.Size = new System.Drawing.Size(167, 23);
			this.textBox_router.TabIndex = 13;
			this.button1.Location = new System.Drawing.Point(811, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(79, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label9.AutoSize = true;
			this.label9.ForeColor = System.Drawing.Color.DarkGray;
			this.label9.Location = new System.Drawing.Point(327, 40);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(228, 17);
			this.label9.TabIndex = 14;
			this.label9.Text = "(if use router, example: 1.15.2.18.1.12)";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(8, 40);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(41, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "slot：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(900, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(76, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 103);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(993, 549);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(991, 547);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/9607929.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "CIP";
			this.userControlHead1.Size = new System.Drawing.Size(1000, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.checkBox2.AutoSize = true;
			this.checkBox2.Checked = true;
			this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox2.Location = new System.Drawing.Point(740, 38);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(150, 21);
			this.checkBox2.TabIndex = 17;
			this.checkBox2.Text = "数组使用0x52片段读取";
			this.checkBox2.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1000, 651);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormAllenBrandly";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "AB PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
