using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Freedom;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormFreedomUdpNet : HslFormContent
	{
		private FreedomUdpNet freedom = null;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Label label1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox_port;

		private Label label3;

		private TextBox textBox_ip;

		private UserControlHead userControlHead1;

		private Label label2;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private Panel panel3;

		private Label label4;

		private CheckBox checkBox3;

		private ComboBox comboBox2;

		public FormFreedomUdpNet()
		{
			InitializeComponent();
			freedom = new FreedomUdpNet();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			Language(Program.Language);
			comboBox2.SelectedIndex = 3;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (freedom != null)
			{
				switch (comboBox2.SelectedIndex)
				{
				case 0:
					freedom.ByteTransform.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					freedom.ByteTransform.DataFormat = DataFormat.BADC;
					break;
				case 2:
					freedom.ByteTransform.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					freedom.ByteTransform.DataFormat = DataFormat.DCBA;
					break;
				}
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Free protocol access based on Udp ip";
				label1.Text = "Ip:";
				label3.Text = "Port:";
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
			freedom.IpAddress = textBox_ip.Text;
			if (!int.TryParse(textBox_port.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			freedom.LogNet = base.LogNet;
			freedom.ByteTransform = new RegularByteTransform();
			ComboBox2_SelectedIndexChanged(null, null);
			freedom.ByteTransform.IsStringReverseByteWord = checkBox3.Checked;
			freedom.Port = result;
			button1.Enabled = false;
			DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
			button2.Enabled = true;
			button1.Enabled = false;
			userControlReadWriteDevice1.SetEnable(true);
			userControlReadWriteDevice1.SetReadWriteNet(freedom, "", true);
			userControlReadWriteDevice1.BatchRead.SetReadWriteNet(freedom, "", string.Empty);
			userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => freedom.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
			userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
			codeExampleControl.SetCodeText(freedom, "ByteTransform");
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			userControlReadWriteDevice1.SetEnable(false);
			freedom?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_port.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox2.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox_port.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			comboBox2.SelectedIndex = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlDataFormat, 0, int.Parse);
			checkBox3.Checked = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStringReverse, false, bool.Parse);
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
			this.panel3 = new System.Windows.Forms.Panel();
			this.label4 = new System.Windows.Forms.Label();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.panel3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 86);
			this.panel1.TabIndex = 0;
			this.panel3.Controls.Add(this.label4);
			this.panel3.Controls.Add(this.checkBox3);
			this.panel3.Controls.Add(this.comboBox2);
			this.panel3.Location = new System.Drawing.Point(253, -1);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(163, 86);
			this.panel3.TabIndex = 33;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(3, 9);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(137, 17);
			this.label4.TabIndex = 31;
			this.label4.Text = "ByteTransform Setting";
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(3, 61);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 29;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox2.Location = new System.Drawing.Point(3, 32);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(128, 25);
			this.comboBox2.TabIndex = 30;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(433, 43);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(368, 34);
			this.label2.TabIndex = 32;
			this.label2.Text = "注意：地址框框里使用原始字节报文信息，也可以加上偏移结果信息\r\nstx=6;00 00 00 00 00 06 01 03 00 00 00 01\r\n";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(533, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(436, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_port.Location = new System.Drawing.Point(70, 40);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(162, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "6000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 43);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(70, 8);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(162, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 11);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 125);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 517);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 515);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Freedom Udp";
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
			base.Name = "FormFreedomUdpNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "基于UDP/IP自由协议访问";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
