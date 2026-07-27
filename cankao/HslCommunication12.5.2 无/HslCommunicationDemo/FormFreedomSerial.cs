using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Freedom;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormFreedomSerial : HslFormContent
	{
		private FreedomSerial freedom = null;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private UserControlHead userControlHead1;

		private CheckBox checkBox5;

		private ComboBox comboBox3;

		private ComboBox comboBox1;

		private TextBox textBox17;

		private Label label23;

		private TextBox textBox16;

		private Label label22;

		private TextBox textBox2;

		private Label label3;

		private Label label1;

		private Label label24;

		private Label label2;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private Panel panel3;

		private Label label4;

		private CheckBox checkBox3;

		private ComboBox comboBox2;

		public FormFreedomSerial()
		{
			InitializeComponent();
			freedom = new FreedomSerial();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			comboBox1.SelectedIndex = 0;
			comboBox2.SelectedIndex = 3;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			comboBox3.DataSource = SerialPort.GetPortNames();
			try
			{
				comboBox3.SelectedIndex = 0;
			}
			catch
			{
				comboBox3.Text = "COM3";
			}
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
				Text = "Free protocol access based on serial port";
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
			if (!int.TryParse(textBox2.Text, out var baudRate))
			{
				DemoUtils.ShowMessage(DemoUtils.BaudRateInputWrong);
				return;
			}
			if (!int.TryParse(textBox16.Text, out var dataBits))
			{
				DemoUtils.ShowMessage(DemoUtils.DataBitsInputWrong);
				return;
			}
			if (!int.TryParse(textBox17.Text, out var stopBits))
			{
				DemoUtils.ShowMessage(DemoUtils.StopBitInputWrong);
				return;
			}
			freedom?.Close();
			freedom = new FreedomSerial();
			freedom.LogNet = base.LogNet;
			freedom.ByteTransform = new RegularByteTransform();
			ComboBox2_SelectedIndexChanged(null, new EventArgs());
			freedom.ByteTransform.IsStringReverseByteWord = checkBox3.Checked;
			freedom.SerialPortInni(delegate(SerialPort sp)
			{
				sp.PortName = comboBox3.Text;
				sp.BaudRate = baudRate;
				sp.DataBits = dataBits;
				sp.StopBits = ((stopBits != 0) ? ((stopBits == 1) ? StopBits.One : StopBits.Two) : StopBits.None);
				sp.Parity = ((comboBox1.SelectedIndex != 0) ? ((comboBox1.SelectedIndex == 1) ? Parity.Odd : Parity.Even) : Parity.None);
			});
			freedom.RtsEnable = checkBox5.Checked;
			button1.Enabled = false;
			OperateResult operateResult = freedom.Open();
			if (operateResult.IsSuccess)
			{
				button2.Enabled = true;
				button1.Enabled = false;
				userControlReadWriteDevice1.SetEnable(true);
				userControlReadWriteDevice1.SetReadWriteNet(freedom, "");
				userControlReadWriteDevice1.BatchRead.SetReadWriteNet(freedom, "", string.Empty);
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => freedom.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
				userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
				codeExampleControl.SetCodeText(freedom, "ByteTransform");
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
				button1.Enabled = true;
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			userControlReadWriteDevice1.SetEnable(false);
			freedom?.Close();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlCom, comboBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlBaudRate, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlDataBits, textBox16.Text);
			element.SetAttributeValue(DemoDeviceList.XmlStopBit, textBox17.Text);
			element.SetAttributeValue(DemoDeviceList.XmlParity, comboBox1.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlRtsEnable, checkBox5.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox2.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			comboBox3.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlCom, "COM1", (string m) => m);
			textBox2.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlBaudRate, "9600", (string m) => m);
			textBox16.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlDataBits, "8", (string m) => m);
			textBox17.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlStopBit, "1", (string m) => m);
			comboBox1.SelectedIndex = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlParity, 0, int.Parse);
			checkBox5.Checked = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlRtsEnable, true, bool.Parse);
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
			this.checkBox5 = new System.Windows.Forms.CheckBox();
			this.comboBox3 = new System.Windows.Forms.ComboBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.label22 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.label24 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
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
			this.panel1.Controls.Add(this.checkBox5);
			this.panel1.Controls.Add(this.comboBox3);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.textBox17);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 86);
			this.panel1.TabIndex = 0;
			this.panel3.Controls.Add(this.label4);
			this.panel3.Controls.Add(this.checkBox3);
			this.panel3.Controls.Add(this.comboBox2);
			this.panel3.Location = new System.Drawing.Point(326, 1);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(180, 86);
			this.panel3.TabIndex = 43;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(3, 7);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(137, 17);
			this.label4.TabIndex = 31;
			this.label4.Text = "ByteTransform Setting";
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(4, 59);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 29;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox2.Location = new System.Drawing.Point(3, 31);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(128, 25);
			this.comboBox2.TabIndex = 30;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(524, 32);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(255, 51);
			this.label2.TabIndex = 42;
			this.label2.Text = "注意：地址框框里使用原始字节报文信息，\r\n也可以加上偏移结果信息\r\nstx=6;00 00 00 00 00 06 01 03 00 00 00 01\r\n";
			this.checkBox5.AutoSize = true;
			this.checkBox5.Location = new System.Drawing.Point(9, 62);
			this.checkBox5.Name = "checkBox5";
			this.checkBox5.Size = new System.Drawing.Size(84, 21);
			this.checkBox5.TabIndex = 41;
			this.checkBox5.Text = "RtsEnable";
			this.checkBox5.UseVisualStyleBackColor = true;
			this.comboBox3.FormattingEnabled = true;
			this.comboBox3.Location = new System.Drawing.Point(61, 5);
			this.comboBox3.Name = "comboBox3";
			this.comboBox3.Size = new System.Drawing.Size(109, 25);
			this.comboBox3.TabIndex = 40;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[3] { "无", "奇", "偶" });
			this.comboBox1.Location = new System.Drawing.Point(256, 34);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(57, 25);
			this.comboBox1.TabIndex = 39;
			this.textBox17.Location = new System.Drawing.Point(167, 34);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(36, 23);
			this.textBox17.TabIndex = 37;
			this.textBox17.Text = "1";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(109, 37);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(56, 17);
			this.label23.TabIndex = 36;
			this.label23.Text = "停止位：";
			this.textBox16.Location = new System.Drawing.Point(62, 34);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(37, 23);
			this.textBox16.TabIndex = 35;
			this.textBox16.Text = "8";
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(8, 37);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(56, 17);
			this.label22.TabIndex = 34;
			this.label22.Text = "数据位：";
			this.textBox2.Location = new System.Drawing.Point(246, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(67, 23);
			this.textBox2.TabIndex = 33;
			this.textBox2.Text = "9600";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(182, 8);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 32;
			this.label3.Text = "波特率：";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 31;
			this.label1.Text = "Com口：";
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(212, 37);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(44, 17);
			this.label24.TabIndex = 38;
			this.label24.Text = "奇偶：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(625, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(528, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 125);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 516);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 514);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Freedom Serial";
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
			base.Name = "FormFreedomSerial";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "基于串口的自由协议访问";
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
