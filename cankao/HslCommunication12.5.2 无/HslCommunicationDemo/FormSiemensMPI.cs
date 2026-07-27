using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormSiemensMPI : HslFormContent
	{
		private SiemensMPI siemensMPI = null;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Label label22;

		private Label label21;

		private ComboBox comboBox1;

		private Label label1;

		private TextBox textBox17;

		private Label label3;

		private TextBox textBox16;

		private Label label25;

		private TextBox textBox2;

		private Label label26;

		private Label label27;

		private Button button2;

		private Button button1;

		private ComboBox comboBox3;

		private TextBox textBox15;

		private Label label23;

		private UserControlHead userControlHead1;

		private CheckBox checkBox1;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		public FormSiemensMPI()
		{
			InitializeComponent();
			siemensMPI = new SiemensMPI();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			comboBox3.DataSource = SerialPort.GetPortNames();
			try
			{
				comboBox3.SelectedIndex = 0;
			}
			catch
			{
				comboBox3.Text = "COM3";
			}
			comboBox1.SelectedIndex = 2;
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Siemens Read PLC Demo";
				label1.Text = "parity:";
				label3.Text = "Stop bits";
				label27.Text = "Com:";
				label26.Text = "BaudRate";
				label25.Text = "Data bits";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label21.Text = "Address:";
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
			siemensMPI?.Close();
			siemensMPI = new SiemensMPI();
			try
			{
				siemensMPI.SerialPortInni(delegate(SerialPort sp)
				{
					sp.PortName = comboBox3.Text;
					sp.BaudRate = baudRate;
					sp.DataBits = dataBits;
					sp.StopBits = ((stopBits != 0) ? ((stopBits == 1) ? StopBits.One : StopBits.Two) : StopBits.None);
					sp.Parity = ((comboBox1.SelectedIndex != 0) ? ((comboBox1.SelectedIndex == 1) ? Parity.Odd : Parity.Even) : Parity.None);
				});
				siemensMPI.Open();
				siemensMPI.Station = byte.Parse(textBox15.Text);
				if (checkBox1.Checked)
				{
					OperateResult operateResult = siemensMPI.Handle();
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("Hand Failed:" + operateResult.Message);
						return;
					}
				}
				button2.Enabled = true;
				button1.Enabled = false;
				userControlReadWriteDevice1.SetEnable(true);
				userControlReadWriteDevice1.SetReadWriteNet(siemensMPI, "M100");
				userControlReadWriteDevice1.BatchRead.SetReadWriteNet(siemensMPI, "M100", string.Empty);
				userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => siemensMPI.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
				userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
				codeExampleControl.SetCodeText(siemensMPI, "Station");
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
			siemensMPI?.Close();
		}

		private void Test1()
		{
			OperateResult<bool> operateResult = siemensMPI.ReadBool("M100.4");
			if (operateResult.IsSuccess)
			{
				bool content = operateResult.Content;
			}
			else
			{
				string message = operateResult.Message;
			}
			OperateResult operateResult2 = siemensMPI.Write("M100.4", true);
			if (!operateResult2.IsSuccess)
			{
				string message2 = operateResult2.Message;
			}
		}

		private void Test2()
		{
			byte content = siemensMPI.ReadByte("M100").Content;
			short content2 = siemensMPI.ReadInt16("M100").Content;
			ushort content3 = siemensMPI.ReadUInt16("M100").Content;
			int content4 = siemensMPI.ReadInt32("M100").Content;
			uint content5 = siemensMPI.ReadUInt32("M100").Content;
			float content6 = siemensMPI.ReadFloat("M100").Content;
			double content7 = siemensMPI.ReadDouble("M100").Content;
			string content8 = siemensMPI.ReadString("M100", 10).Content;
			IByteTransform byteTransform = new ReverseBytesTransform();
		}

		private void Test3()
		{
			bool content = siemensMPI.ReadBool("M100.7").Content;
			byte content2 = siemensMPI.ReadByte("M100").Content;
			short content3 = siemensMPI.ReadInt16("M100").Content;
			ushort content4 = siemensMPI.ReadUInt16("M100").Content;
			int content5 = siemensMPI.ReadInt32("M100").Content;
			uint content6 = siemensMPI.ReadUInt32("M100").Content;
			float content7 = siemensMPI.ReadFloat("M100").Content;
			long content8 = siemensMPI.ReadInt64("M100").Content;
			ulong content9 = siemensMPI.ReadUInt64("M100").Content;
			double content10 = siemensMPI.ReadDouble("M100").Content;
			string content11 = siemensMPI.ReadString("M100", 10).Content;
			siemensMPI.Write("M100.7", true);
			siemensMPI.Write("M100", (short)51);
			siemensMPI.Write("M100", (short)12345);
			siemensMPI.Write("M100", (ushort)45678);
			siemensMPI.Write("M100", 123456789);
			siemensMPI.Write("M100", 3456789123u);
			siemensMPI.Write("M100", 123.456f);
			siemensMPI.Write("M100", 1234556434534545L);
			siemensMPI.Write("M100", 523434234234343uL);
			siemensMPI.Write("M100", 123.456);
			siemensMPI.Write("M100", "K123456789");
			OperateResult<byte[]> operateResult = siemensMPI.Read("M100", 10);
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
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.comboBox3 = new System.Windows.Forms.ComboBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label26 = new System.Windows.Forms.Label();
			this.label27 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.label22 = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.comboBox3);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.textBox17);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label25);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label26);
			this.panel1.Controls.Add(this.label27);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 54);
			this.panel1.TabIndex = 0;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(602, 0);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(75, 21);
			this.checkBox1.TabIndex = 41;
			this.checkBox1.Text = "握手检查";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox15.Location = new System.Drawing.Point(548, 15);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(37, 23);
			this.textBox15.TabIndex = 40;
			this.textBox15.Text = "2";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(504, 18);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(44, 17);
			this.label23.TabIndex = 39;
			this.label23.Text = "站号：";
			this.comboBox3.FormattingEnabled = true;
			this.comboBox3.Location = new System.Drawing.Point(62, 12);
			this.comboBox3.Name = "comboBox3";
			this.comboBox3.Size = new System.Drawing.Size(67, 25);
			this.comboBox3.TabIndex = 38;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[3] { "无", "奇", "偶" });
			this.comboBox1.Location = new System.Drawing.Point(449, 15);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(49, 25);
			this.comboBox1.TabIndex = 37;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(413, 18);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 36;
			this.label1.Text = "奇偶：";
			this.textBox17.Location = new System.Drawing.Point(383, 15);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(23, 23);
			this.textBox17.TabIndex = 35;
			this.textBox17.Text = "1";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(329, 18);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 34;
			this.label3.Text = "停止位：";
			this.textBox16.Location = new System.Drawing.Point(292, 15);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(24, 23);
			this.textBox16.TabIndex = 33;
			this.textBox16.Text = "8";
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(238, 18);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(56, 17);
			this.label25.TabIndex = 32;
			this.label25.Text = "数据位：";
			this.textBox2.Location = new System.Drawing.Point(185, 15);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(47, 23);
			this.textBox2.TabIndex = 31;
			this.textBox2.Text = "9600";
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(131, 18);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(56, 17);
			this.label26.TabIndex = 30;
			this.label26.Text = "波特率：";
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(5, 18);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(59, 17);
			this.label27.TabIndex = 28;
			this.label27.Text = "Com口：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(699, 23);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 27;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(602, 23);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 26;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label22.Location = new System.Drawing.Point(841, 0);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(132, 52);
			this.label22.TabIndex = 7;
			this.label22.Text = "M100  I100  Q100 DB100.20   T100 C100";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(767, 0);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(68, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "地址示例：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 92);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 553);
			this.panel2.TabIndex = 1;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MPI";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 551);
			this.userControlReadWriteDevice1.TabIndex = 0;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSiemensMPI";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "西门子PLC访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
