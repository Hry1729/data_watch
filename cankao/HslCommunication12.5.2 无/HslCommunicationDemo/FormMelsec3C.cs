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
	public class FormMelsec3C : HslFormContent
	{
		private MelsecA3CNet melsecA3C = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label21;

		private UserControlHead userControlHead1;

		private CheckBox checkBox_sumcheck;

		private Label label2;

		private ComboBox comboBox_format;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private CheckBox checkBox_EnableWriteBitToWordRegister;

		private PipeSelectControl pipeSelectControl1;

		public FormMelsec3C()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox_format.SelectedIndex = 0;
			comboBox_format.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			checkBox_sumcheck.CheckedChanged += CheckBox1_CheckedChanged;
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetMcAddress());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (melsecA3C != null)
			{
				melsecA3C.SumCheck = checkBox_sumcheck.Checked;
			}
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (melsecA3C != null)
			{
				melsecA3C.Format = int.Parse(comboBox_format.SelectedItem.ToString());
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Melsec Read PLC Demo";
				button1.Text = "Open";
				button2.Text = "Close";
				label21.Text = "Address:";
				label2.Text = "Format:";
			}
			else
			{
				checkBox_EnableWriteBitToWordRegister.Text = "支持使用位写入字寄存器(实际读字，修改位，写字)";
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
			melsecA3C?.Close();
			melsecA3C = new MelsecA3CNet();
			melsecA3C.LogNet = base.LogNet;
			melsecA3C.EnableWriteBitToWordRegister = checkBox_EnableWriteBitToWordRegister.Checked;
			try
			{
				pipeSelectControl1.IniPipe(melsecA3C);
				melsecA3C.Station = byte.Parse(textBox15.Text);
				melsecA3C.SumCheck = checkBox_sumcheck.Checked;
				melsecA3C.Format = int.Parse(comboBox_format.SelectedItem.ToString());
				OperateResult operateResult = DeviceConnectPLC(melsecA3C);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(melsecA3C, "D100");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(melsecA3C, "D100", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => melsecA3C.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(melsecA3C, "Station", "EnableWriteBitToWordRegister", "SumCheck", "Format");
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
			pipeSelectControl1.ExtraCloseAction(melsecA3C);
			melsecA3C?.Close();
		}

		private void test1()
		{
			OperateResult<bool[]> operateResult = melsecA3C.ReadBool("M100", 10);
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
			if (!melsecA3C.Write("M100", value).IsSuccess)
			{
			}
		}

		private void test3()
		{
			short content = melsecA3C.ReadInt16("D100").Content;
			ushort content2 = melsecA3C.ReadUInt16("D100").Content;
			int content3 = melsecA3C.ReadInt32("D100").Content;
			uint content4 = melsecA3C.ReadUInt32("D100").Content;
			long content5 = melsecA3C.ReadInt64("D100").Content;
			ulong content6 = melsecA3C.ReadUInt64("D100").Content;
			float content7 = melsecA3C.ReadFloat("D100").Content;
			double content8 = melsecA3C.ReadDouble("D100").Content;
			string content9 = melsecA3C.ReadString("D100", 10).Content;
		}

		private void test4()
		{
			melsecA3C.Write("D100", (short)5);
			melsecA3C.Write("D100", (ushort)5);
			melsecA3C.Write("D100", 5);
			melsecA3C.Write("D100", 5u);
			melsecA3C.Write("D100", 5L);
			melsecA3C.Write("D100", 5uL);
			melsecA3C.Write("D100", 5f);
			melsecA3C.Write("D100", 5.0);
			melsecA3C.Write("D100", "12345678");
		}

		private void test5()
		{
			OperateResult<byte[]> operateResult = melsecA3C.Read("D100", 10);
			if (operateResult.IsSuccess)
			{
				int num = melsecA3C.ByteTransform.TransInt32(operateResult.Content, 0);
				float num2 = melsecA3C.ByteTransform.TransSingle(operateResult.Content, 4);
				short num3 = melsecA3C.ByteTransform.TransInt16(operateResult.Content, 8);
				string @string = Encoding.ASCII.GetString(operateResult.Content, 10, 10);
			}
		}

		private void test6()
		{
			OperateResult<UserType> operateResult = melsecA3C.ReadCustomer<UserType>("D100");
			if (operateResult.IsSuccess)
			{
				UserType content = operateResult.Content;
			}
			melsecA3C.WriteCustomer("D100", new UserType());
			melsecA3C.LogNet = new LogNetSingle(Application.StartupPath + "\\Logs.txt");
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox15.Text);
			element.SetAttributeValue("EnableWriteBitToWordRegister", checkBox_EnableWriteBitToWordRegister.Text);
			element.SetAttributeValue("Sumcheck", checkBox_sumcheck.Checked);
			element.SetAttributeValue("MelsecFormat", comboBox_format.SelectedIndex);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			checkBox_EnableWriteBitToWordRegister.Checked = HslFormContent.GetXmlValue(element, "EnableWriteBitToWordRegister", false, bool.Parse);
			checkBox_sumcheck.Checked = HslFormContent.GetXmlValue(element, "Sumcheck", checkBox_sumcheck.Checked, bool.Parse);
			comboBox_format.SelectedIndex = HslFormContent.GetXmlValue(element, "MelsecFormat", comboBox_format.SelectedIndex, int.Parse);
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
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.checkBox_EnableWriteBitToWordRegister = new System.Windows.Forms.CheckBox();
			this.comboBox_format = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.checkBox_sumcheck = new System.Windows.Forms.CheckBox();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
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
			this.panel1.Controls.Add(this.checkBox_EnableWriteBitToWordRegister);
			this.panel1.Controls.Add(this.comboBox_format);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.checkBox_sumcheck);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 60);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(4, 1);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 41;
			this.pipeSelectControl1.TcpPortText = "2000";
			this.pipeSelectControl1.UdpPortText = "2000";
			this.checkBox_EnableWriteBitToWordRegister.AutoSize = true;
			this.checkBox_EnableWriteBitToWordRegister.Location = new System.Drawing.Point(7, 33);
			this.checkBox_EnableWriteBitToWordRegister.Name = "checkBox_EnableWriteBitToWordRegister";
			this.checkBox_EnableWriteBitToWordRegister.Size = new System.Drawing.Size(228, 21);
			this.checkBox_EnableWriteBitToWordRegister.TabIndex = 40;
			this.checkBox_EnableWriteBitToWordRegister.Text = "Enable Write Bit To Word Register";
			this.checkBox_EnableWriteBitToWordRegister.UseVisualStyleBackColor = true;
			this.comboBox_format.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_format.FormattingEnabled = true;
			this.comboBox_format.Items.AddRange(new object[4] { "1", "2", "3", "4" });
			this.comboBox_format.Location = new System.Drawing.Point(650, 29);
			this.comboBox_format.Name = "comboBox_format";
			this.comboBox_format.Size = new System.Drawing.Size(68, 25);
			this.comboBox_format.TabIndex = 39;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(589, 31);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 34;
			this.label2.Text = "格式：";
			this.checkBox_sumcheck.AutoSize = true;
			this.checkBox_sumcheck.Checked = true;
			this.checkBox_sumcheck.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_sumcheck.Location = new System.Drawing.Point(467, 32);
			this.checkBox_sumcheck.Name = "checkBox_sumcheck";
			this.checkBox_sumcheck.Size = new System.Drawing.Size(87, 21);
			this.checkBox_sumcheck.TabIndex = 33;
			this.checkBox_sumcheck.Text = "SumCheck";
			this.checkBox_sumcheck.UseVisualStyleBackColor = true;
			this.textBox15.Location = new System.Drawing.Point(393, 32);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(43, 23);
			this.textBox15.TabIndex = 28;
			this.textBox15.Text = "0";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(337, 35);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 27;
			this.label21.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(893, 1);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(797, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 97);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 546);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 544);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Qna 3C";
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
			base.Name = "FormMelsec3C";
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
