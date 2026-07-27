using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Instrument.DLT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Instrument;

namespace HslCommunicationDemo
{
	public class FormDLT698OverTcp : HslFormContent
	{
		private DLT698OverTcp dLT698 = null;

		private DLT698Control control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox_station;

		private Label label_address;

		private UserControlHead userControlHead1;

		private CheckBox checkBox_enable_Fe;

		private CheckBox checkBox_useSecurityResquest;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private TextBox textBox_ca;

		private Label label_ca;

		private PipeSelectControl pipeSelectControl1;

		private CheckBox checkBox_IsServerActivePush;

		public FormDLT698OverTcp()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new DLT698Control();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(DLTHelper.GetDlt698Address());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "DLT698 Read Demo";
				label_address.Text = "station";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label_ca.Text = "        CA:";
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
			if (!byte.TryParse(textBox_ca.Text, out var result))
			{
				DemoUtils.ShowMessage("CA input wrong!");
				textBox_ca.Focus();
				return;
			}
			dLT698?.ConnectClose();
			dLT698 = new DLT698OverTcp(textBox_station.Text);
			dLT698.LogNet = base.LogNet;
			dLT698.EnableCodeFE = checkBox_enable_Fe.Checked;
			dLT698.UseSecurityResquest = checkBox_useSecurityResquest.Checked;
			dLT698.CA = result;
			dLT698.IsServerActivePush = checkBox_IsServerActivePush.Checked;
			try
			{
				pipeSelectControl1.IniPipe(dLT698);
				OperateResult operateResult = DeviceConnectPLC(dLT698);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(dLT698, "20-00-02-00");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(dLT698, "20-00-02-00", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => dLT698.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => dLT698.ReadByApdu(m), "Apdu", "Apdu Message: 05 01 01 20 10 02 00 00");
					control.SetDevice(dLT698, "20-00-02-00");
					userControlReadWriteDevice1.SetDeviceVariableName("dlt");
					List<string> list = new List<string>();
					list.Add("Station");
					if (checkBox_enable_Fe.Checked)
					{
						list.Add("EnableCodeFE");
					}
					list.Add("UseSecurityResquest");
					list.Add("CA");
					if (checkBox_IsServerActivePush.Checked)
					{
						list.Add("IsServerActivePush");
					}
					codeExampleControl.SetCodeText("dlt", dLT698, list.ToArray());
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
			pipeSelectControl1.ExtraCloseAction(dLT698);
			dLT698.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			element.SetAttributeValue("UseSecurityResquest", checkBox_useSecurityResquest.Checked);
			element.SetAttributeValue("CA", textBox_ca.Text);
			if (checkBox_enable_Fe.Checked)
			{
				element.SetAttributeValue("EnableCodeFE", checkBox_enable_Fe.Checked);
			}
			if (checkBox_IsServerActivePush.Checked)
			{
				element.SetAttributeValue("IsServerActivePush", checkBox_IsServerActivePush.Checked);
			}
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			checkBox_useSecurityResquest.Checked = HslFormContent.GetXmlValue(element, "UseSecurityResquest", checkBox_useSecurityResquest.Checked, bool.Parse);
			textBox_ca.Text = HslFormContent.GetXmlValue(element, "CA", textBox_ca.Text, (string m) => m);
			checkBox_enable_Fe.Checked = HslFormContent.GetXmlValue(element, "EnableCodeFE", checkBox_enable_Fe.Checked, bool.Parse);
			checkBox_IsServerActivePush.Checked = HslFormContent.GetXmlValue(element, "IsServerActivePush", checkBox_IsServerActivePush.Checked, bool.Parse);
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
			this.checkBox_IsServerActivePush = new System.Windows.Forms.CheckBox();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.textBox_ca = new System.Windows.Forms.TextBox();
			this.label_ca = new System.Windows.Forms.Label();
			this.checkBox_useSecurityResquest = new System.Windows.Forms.CheckBox();
			this.checkBox_enable_Fe = new System.Windows.Forms.CheckBox();
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label_address = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.checkBox_IsServerActivePush);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox_ca);
			this.panel1.Controls.Add(this.label_ca);
			this.panel1.Controls.Add(this.checkBox_useSecurityResquest);
			this.panel1.Controls.Add(this.checkBox_enable_Fe);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label_address);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 62);
			this.panel1.TabIndex = 0;
			this.checkBox_IsServerActivePush.AutoSize = true;
			this.checkBox_IsServerActivePush.Location = new System.Drawing.Point(748, 34);
			this.checkBox_IsServerActivePush.Name = "checkBox_IsServerActivePush";
			this.checkBox_IsServerActivePush.Size = new System.Drawing.Size(135, 21);
			this.checkBox_IsServerActivePush.TabIndex = 44;
			this.checkBox_IsServerActivePush.Text = "IsServerActivePush";
			this.checkBox_IsServerActivePush.UseVisualStyleBackColor = true;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(6, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.Even;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 43;
			this.pipeSelectControl1.TcpPortText = "2000";
			this.pipeSelectControl1.UdpPortText = "2000";
			this.textBox_ca.Location = new System.Drawing.Point(84, 32);
			this.textBox_ca.Name = "textBox_ca";
			this.textBox_ca.Size = new System.Drawing.Size(56, 23);
			this.textBox_ca.TabIndex = 42;
			this.textBox_ca.Text = "0";
			this.label_ca.AutoSize = true;
			this.label_ca.Location = new System.Drawing.Point(8, 35);
			this.label_ca.Name = "label_ca";
			this.label_ca.Size = new System.Drawing.Size(71, 17);
			this.label_ca.TabIndex = 41;
			this.label_ca.Text = "客户机地址:";
			this.checkBox_useSecurityResquest.AutoSize = true;
			this.checkBox_useSecurityResquest.Checked = true;
			this.checkBox_useSecurityResquest.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_useSecurityResquest.Location = new System.Drawing.Point(172, 34);
			this.checkBox_useSecurityResquest.Name = "checkBox_useSecurityResquest";
			this.checkBox_useSecurityResquest.Size = new System.Drawing.Size(129, 21);
			this.checkBox_useSecurityResquest.TabIndex = 40;
			this.checkBox_useSecurityResquest.Text = "Security Resquest";
			this.checkBox_useSecurityResquest.UseVisualStyleBackColor = true;
			this.checkBox_enable_Fe.AutoSize = true;
			this.checkBox_enable_Fe.Location = new System.Drawing.Point(593, 34);
			this.checkBox_enable_Fe.Name = "checkBox_enable_Fe";
			this.checkBox_enable_Fe.Size = new System.Drawing.Size(130, 21);
			this.checkBox_enable_Fe.TabIndex = 39;
			this.checkBox_enable_Fe.Text = "FE FE FE FE head?";
			this.checkBox_enable_Fe.UseVisualStyleBackColor = true;
			this.textBox_station.Location = new System.Drawing.Point(386, 32);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(190, 23);
			this.textBox_station.TabIndex = 7;
			this.textBox_station.Text = "1";
			this.label_address.AutoSize = true;
			this.label_address.Location = new System.Drawing.Point(338, 35);
			this.label_address.Name = "label_address";
			this.label_address.Size = new System.Drawing.Size(44, 17);
			this.label_address.TabIndex = 6;
			this.label_address.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(901, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(804, 4);
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
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "DLT 698.45 Over Tcp";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
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
			base.Name = "FormDLT698OverTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "DLT698访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
