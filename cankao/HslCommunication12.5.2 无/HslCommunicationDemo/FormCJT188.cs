using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Instrument.CJT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Instrument;

namespace HslCommunicationDemo
{
	public class FormCJT188 : HslFormContent
	{
		private CJT188 cjt188 = null;

		private CJT188Control control;

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

		private Label label5;

		private TextBox textBox_type;

		private Label label4;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormCJT188()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			control = new CJT188Control();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(CJT188Helper.GetCJT188Address());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "CJT188 Read Demo";
				label_address.Text = "station";
				label_password.Text = "Pwd:";
				label_op_code.Text = "Op Code";
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
			cjt188?.Close();
			cjt188 = new CJT188(textBox_station.Text);
			cjt188.LogNet = base.LogNet;
			cjt188.InstrumentType = Convert.ToByte(textBox_type.Text, 16);
			cjt188.EnableCodeFE = checkBox_enable_Fe.Checked;
			try
			{
				pipeSelectControl1.IniPipe(cjt188);
				OperateResult operateResult = DeviceConnectPLC(cjt188);
				if (operateResult.IsSuccess)
				{
					userControlReadWriteDevice1.SetEnable(true);
					button1.Enabled = false;
					panel2.Enabled = true;
					userControlReadWriteDevice1.SetReadWriteNet(cjt188, "90-1F");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(cjt188, "90-1F", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => cjt188.ReadFromCoreServer(m, true, false), string.Empty, "68 00 00 00 00 00 01 68 11 04 00 00 00 00 10 16");
					control.SetDevice(cjt188, "90-1F");
					userControlReadWriteDevice1.SetDeviceVariableName("cjt");
					codeExampleControl.SetCodeText("cjt", cjt188, "InstrumentType", "EnableCodeFE");
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
			userControlReadWriteDevice1.SetEnable(false);
			button1.Enabled = true;
			panel2.Enabled = false;
			pipeSelectControl1.ExtraCloseAction(cjt188);
			cjt188.Close();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			element.SetAttributeValue("InstrumentType", textBox_type.Text);
			element.SetAttributeValue("FeHead", checkBox_enable_Fe.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			textBox_type.Text = HslFormContent.GetXmlValue(element, "InstrumentType", textBox_type.Text, (string m) => m);
			checkBox_enable_Fe.Checked = HslFormContent.GetXmlValue(element, "FeHead", checkBox_enable_Fe.Checked, bool.Parse);
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
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_type = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
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
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox_type);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.checkBox_enable_Fe);
			this.panel1.Controls.Add(this.textBox_op_code);
			this.panel1.Controls.Add(this.label_op_code);
			this.panel1.Controls.Add(this.textBox_password);
			this.panel1.Controls.Add(this.label_password);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label_address);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(4, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(922, 39);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(32, 17);
			this.label5.TabIndex = 45;
			this.label5.Text = "HEX";
			this.textBox_type.Location = new System.Drawing.Point(865, 36);
			this.textBox_type.Name = "textBox_type";
			this.textBox_type.Size = new System.Drawing.Size(48, 23);
			this.textBox_type.TabIndex = 44;
			this.textBox_type.Text = "19";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(797, 39);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(56, 17);
			this.label4.TabIndex = 43;
			this.label4.Text = "表类型：";
			this.checkBox_enable_Fe.AutoSize = true;
			this.checkBox_enable_Fe.Location = new System.Drawing.Point(695, 38);
			this.checkBox_enable_Fe.Name = "checkBox_enable_Fe";
			this.checkBox_enable_Fe.Size = new System.Drawing.Size(96, 21);
			this.checkBox_enable_Fe.TabIndex = 35;
			this.checkBox_enable_Fe.Text = "FE FE head?";
			this.checkBox_enable_Fe.UseVisualStyleBackColor = true;
			this.textBox_op_code.Location = new System.Drawing.Point(294, 36);
			this.textBox_op_code.Name = "textBox_op_code";
			this.textBox_op_code.ReadOnly = true;
			this.textBox_op_code.Size = new System.Drawing.Size(144, 23);
			this.textBox_op_code.TabIndex = 34;
			this.label_op_code.AutoSize = true;
			this.label_op_code.Location = new System.Drawing.Point(210, 39);
			this.label_op_code.Name = "label_op_code";
			this.label_op_code.Size = new System.Drawing.Size(80, 17);
			this.label_op_code.TabIndex = 33;
			this.label_op_code.Text = "操作者代码：";
			this.textBox_password.Location = new System.Drawing.Point(56, 36);
			this.textBox_password.Name = "textBox_password";
			this.textBox_password.ReadOnly = true;
			this.textBox_password.Size = new System.Drawing.Size(147, 23);
			this.textBox_password.TabIndex = 32;
			this.label_password.AutoSize = true;
			this.label_password.Location = new System.Drawing.Point(6, 39);
			this.label_password.Name = "label_password";
			this.label_password.Size = new System.Drawing.Size(44, 17);
			this.label_password.TabIndex = 31;
			this.label_password.Text = "密码：";
			this.textBox_station.Location = new System.Drawing.Point(495, 35);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(180, 23);
			this.textBox_station.TabIndex = 7;
			this.textBox_station.Text = "78330015040963";
			this.label_address.AutoSize = true;
			this.label_address.Location = new System.Drawing.Point(447, 38);
			this.label_address.Name = "label_address";
			this.label_address.Size = new System.Drawing.Size(44, 17);
			this.label_address.TabIndex = 6;
			this.label_address.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(899, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(802, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(4, 102);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1000, 541);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(998, 539);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "CJT188 2004";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(5, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 46;
			this.pipeSelectControl1.TcpPortText = "502";
			this.pipeSelectControl1.UdpPortText = "502";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormCJT188";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "CJT188 访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
