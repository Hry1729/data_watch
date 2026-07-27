using System;
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
	public class FormDLT645With1997OverTcp : HslFormContent
	{
		private DLT645With1997OverTcp dLT645 = null;

		private DLT645Control control;

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

		private TextBox textBox_op_code;

		private Label label_op_code;

		private TextBox textBox_password;

		private Label label_password;

		private CheckBox checkBox_enable_Fe;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		private CheckBox checkBox_dataId;

		public FormDLT645With1997OverTcp()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new DLT645Control();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(DLTHelper.GetDlt6451997Address());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "DLT645/1997 Read Demo";
				label_address.Text = "station";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				textBox_password.Text = "Pwd:";
				textBox_op_code.Text = "Op Code:";
				checkBox_dataId.Text = "Check DataId";
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
			dLT645?.ConnectClose();
			dLT645 = new DLT645With1997OverTcp(textBox_station.Text);
			dLT645.LogNet = base.LogNet;
			dLT645.EnableCodeFE = checkBox_enable_Fe.Checked;
			dLT645.CheckDataId = checkBox_dataId.Checked;
			try
			{
				pipeSelectControl1.IniPipe(dLT645);
				OperateResult operateResult = DeviceConnectPLC(dLT645);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(dLT645, "B6-11", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(dLT645, "B6-11", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => dLT645.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					control.SetDevice(dLT645, "B6-11");
					userControlReadWriteDevice1.SetDeviceVariableName("dlt");
					codeExampleControl.SetCodeText("dlt", dLT645, "Station", "EnableCodeFE", "CheckDataId");
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
			pipeSelectControl1.ExtraCloseAction(dLT645);
			dLT645.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
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
			this.checkBox_dataId = new System.Windows.Forms.CheckBox();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.checkBox_enable_Fe = new System.Windows.Forms.CheckBox();
			this.textBox_op_code = new System.Windows.Forms.TextBox();
			this.label_op_code = new System.Windows.Forms.Label();
			this.textBox_password = new System.Windows.Forms.TextBox();
			this.label_password = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.checkBox_dataId);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.checkBox_enable_Fe);
			this.panel1.Controls.Add(this.textBox_op_code);
			this.panel1.Controls.Add(this.label_op_code);
			this.panel1.Controls.Add(this.textBox_password);
			this.panel1.Controls.Add(this.label_password);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label_address);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 62);
			this.panel1.TabIndex = 0;
			this.checkBox_dataId.AutoSize = true;
			this.checkBox_dataId.Checked = true;
			this.checkBox_dataId.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_dataId.Location = new System.Drawing.Point(888, 34);
			this.checkBox_dataId.Name = "checkBox_dataId";
			this.checkBox_dataId.Size = new System.Drawing.Size(97, 21);
			this.checkBox_dataId.TabIndex = 43;
			this.checkBox_dataId.Text = "检查DataID?";
			this.checkBox_dataId.UseVisualStyleBackColor = true;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 2);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 40;
			this.pipeSelectControl1.TcpPortText = "2000";
			this.pipeSelectControl1.UdpPortText = "2000";
			this.checkBox_enable_Fe.AutoSize = true;
			this.checkBox_enable_Fe.Location = new System.Drawing.Point(750, 34);
			this.checkBox_enable_Fe.Name = "checkBox_enable_Fe";
			this.checkBox_enable_Fe.Size = new System.Drawing.Size(130, 21);
			this.checkBox_enable_Fe.TabIndex = 39;
			this.checkBox_enable_Fe.Text = "FE FE FE FE head?";
			this.checkBox_enable_Fe.UseVisualStyleBackColor = true;
			this.textBox_op_code.Location = new System.Drawing.Point(330, 32);
			this.textBox_op_code.Name = "textBox_op_code";
			this.textBox_op_code.ReadOnly = true;
			this.textBox_op_code.Size = new System.Drawing.Size(159, 23);
			this.textBox_op_code.TabIndex = 38;
			this.label_op_code.AutoSize = true;
			this.label_op_code.Location = new System.Drawing.Point(244, 35);
			this.label_op_code.Name = "label_op_code";
			this.label_op_code.Size = new System.Drawing.Size(80, 17);
			this.label_op_code.TabIndex = 37;
			this.label_op_code.Text = "操作者代码：";
			this.textBox_password.Location = new System.Drawing.Point(58, 32);
			this.textBox_password.Name = "textBox_password";
			this.textBox_password.ReadOnly = true;
			this.textBox_password.Size = new System.Drawing.Size(180, 23);
			this.textBox_password.TabIndex = 36;
			this.label_password.AutoSize = true;
			this.label_password.Location = new System.Drawing.Point(8, 35);
			this.label_password.Name = "label_password";
			this.label_password.Size = new System.Drawing.Size(44, 17);
			this.label_password.TabIndex = 35;
			this.label_password.Text = "密码：";
			this.textBox_station.Location = new System.Drawing.Point(554, 32);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(190, 23);
			this.textBox_station.TabIndex = 7;
			this.textBox_station.Text = "1";
			this.label_address.AutoSize = true;
			this.label_address.Location = new System.Drawing.Point(506, 35);
			this.label_address.Name = "label_address";
			this.label_address.Size = new System.Drawing.Size(44, 17);
			this.label_address.TabIndex = 6;
			this.label_address.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(902, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(805, 3);
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
			this.userControlHead1.ProtocolInfo = "DLT 645 1997 Over Tcp";
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
			base.Name = "FormDLT645With1997OverTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "DLT645访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
