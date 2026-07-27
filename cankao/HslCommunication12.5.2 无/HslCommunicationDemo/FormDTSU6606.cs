using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Instrument.Delixi;
using HslCommunicationDemo.DemoControl;
using HslControls;

namespace HslCommunicationDemo
{
	public class FormDTSU6606 : HslFormContent
	{
		private DTSU6606Serial delixi = null;

		private Timer timer;

		private bool timerRead = false;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label21;

		private CheckBox checkBox1;

		private CheckBox checkBox3;

		private ComboBox comboBox2;

		private UserControlHead userControlHead1;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private GroupBox groupBox5;

		private HslCurve hslCurve1;

		private Label label51;

		private TextBox textBox28;

		private Label label52;

		private Label label49;

		private TextBox textBox27;

		private Label label50;

		private Label label47;

		private TextBox textBox26;

		private Label label48;

		private Label label45;

		private TextBox textBox25;

		private Label label46;

		private Label label43;

		private TextBox textBox24;

		private Label label44;

		private Label label41;

		private TextBox textBox23;

		private Label label42;

		private Label label39;

		private TextBox textBox22;

		private Label label40;

		private Label label37;

		private TextBox textBox21;

		private Label label38;

		private Label label35;

		private TextBox textBox20;

		private Label label36;

		private Label label33;

		private TextBox textBox19;

		private Label label34;

		private Label label31;

		private TextBox textBox18;

		private Label label32;

		private Label label29;

		private TextBox textBox14;

		private Label label30;

		private Label label27;

		private TextBox textBox13;

		private Label label28;

		private Label label25;

		private TextBox textBox11;

		private Label label26;

		private Label label19;

		private TextBox textBox10;

		private Label label20;

		private Label label17;

		private TextBox textBox9;

		private Label label18;

		private Label label10;

		private Label label11;

		private Label label12;

		private Label label13;

		private Label label14;

		private Label label15;

		private Label label16;

		private TextBox textBox8;

		private Label label9;

		private TextBox textBox7;

		private Label label8;

		private TextBox textBox6;

		private Label label7;

		private TextBox textBox5;

		private Label label6;

		private TextBox textBox4;

		private Label label5;

		private TextBox textBox3;

		private Label label4;

		private TextBox textBox1;

		private Label label2;

		private Button button3;

		private Label label53;

		private TextBox textBox12;

		private Label label54;

		private Button button4;

		private PipeSelectControl pipeSelectControl1;

		private TextBox textBox2;

		public FormDTSU6606()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox2.SelectedIndex = 0;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			checkBox3.CheckedChanged += CheckBox3_CheckedChanged;
			Language(Program.Language);
			hslCurve1.SetLeftCurve("电压A", null);
			hslCurve1.SetLeftCurve("电压B", null);
			hslCurve1.SetLeftCurve("电压C", null);
			hslCurve1.SetRightCurve("频率", null);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Delixi Read Demo";
				label21.Text = "station";
				checkBox1.Text = "address from 0";
				checkBox3.Text = "string reverse";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label11.Text = "Address:";
				label12.Text = "length:";
				label13.Text = "Results:";
				label16.Text = "Message:";
				label14.Text = "Results:";
				groupBox5.Text = "Special function test";
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (delixi != null)
			{
				delixi.IsStringReverse = checkBox3.Checked;
			}
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (delixi != null)
			{
				switch (comboBox2.SelectedIndex)
				{
				case 0:
					delixi.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					delixi.DataFormat = DataFormat.BADC;
					break;
				case 2:
					delixi.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					delixi.DataFormat = DataFormat.DCBA;
					break;
				}
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
				DemoUtils.ShowMessage("Station input wrong！");
				return;
			}
			delixi?.Close();
			delixi = new DTSU6606Serial(result);
			delixi.AddressStartWithZero = checkBox1.Checked;
			delixi.LogNet = base.LogNet;
			ComboBox2_SelectedIndexChanged(null, new EventArgs());
			delixi.IsStringReverse = checkBox3.Checked;
			try
			{
				pipeSelectControl1.IniPipe(delixi);
				OperateResult operateResult = DeviceConnectPLC(delixi);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					userControlReadWriteOp1.SetReadWriteNet(delixi, "100");
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
			panel2.Enabled = false;
			pipeSelectControl1.ExtraCloseAction(delixi);
			delixi.Close();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox15.Text);
			element.SetAttributeValue(DemoDeviceList.XmlAddressStartWithZero, checkBox1.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox2.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			checkBox1.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlAddressStartWithZero).Value);
			comboBox2.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<ElectricalParameters> operateResult = delixi.ReadElectricalParameters();
			if (operateResult.IsSuccess)
			{
				ShowElectrical(operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
		}

		private void ShowElectrical(ElectricalParameters electrical)
		{
			textBox1.Text = electrical.VoltageA.ToString();
			textBox3.Text = electrical.VoltageB.ToString();
			textBox4.Text = electrical.VoltageC.ToString();
			textBox5.Text = electrical.CurrentA.ToString();
			textBox6.Text = electrical.CurrentB.ToString();
			textBox7.Text = electrical.CurrentC.ToString();
			textBox8.Text = electrical.Frequency.ToString();
			textBox9.Text = electrical.InstantaneousActivePowerA.ToString();
			textBox10.Text = electrical.InstantaneousActivePowerB.ToString();
			textBox11.Text = electrical.InstantaneousActivePowerC.ToString();
			textBox13.Text = electrical.InstantaneousTotalActivePower.ToString();
			textBox14.Text = electrical.InstantaneousReactivePowerA.ToString();
			textBox18.Text = electrical.InstantaneousReactivePowerB.ToString();
			textBox19.Text = electrical.InstantaneousReactivePowerC.ToString();
			textBox20.Text = electrical.InstantaneousTotalReactivePower.ToString();
			textBox21.Text = electrical.InstantaneousApparentPowerA.ToString();
			textBox22.Text = electrical.InstantaneousApparentPowerB.ToString();
			textBox23.Text = electrical.InstantaneousApparentPowerC.ToString();
			textBox24.Text = electrical.InstantaneousTotalApparentPower.ToString();
			textBox25.Text = electrical.PowerFactorA.ToString();
			textBox26.Text = electrical.PowerFactorB.ToString();
			textBox27.Text = electrical.PowerFactorC.ToString();
			textBox28.Text = electrical.TotalPowerFactor.ToString();
		}

		private void button4_Click(object sender, EventArgs e)
		{
			if (timerRead)
			{
				timerRead = false;
				timer?.Dispose();
				button4.Text = "定时读取";
				return;
			}
			timerRead = true;
			timer = new Timer();
			timer.Interval = int.Parse(textBox12.Text);
			timer.Tick += Timer_Tick;
			timer.Start();
			button4.Text = "Stop";
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			OperateResult<ElectricalParameters> operateResult = delixi.ReadElectricalParameters();
			if (operateResult.IsSuccess)
			{
				ShowElectrical(operateResult.Content);
				hslCurve1.AddCurveData(new string[4] { "电压A", "电压B", "电压C", "频率" }, new float[4]
				{
					operateResult.Content.VoltageA,
					operateResult.Content.VoltageB,
					operateResult.Content.VoltageC,
					operateResult.Content.Frequency
				});
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
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.label53 = new System.Windows.Forms.Label();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label54 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.hslCurve1 = new HslControls.HslCurve();
			this.label51 = new System.Windows.Forms.Label();
			this.textBox28 = new System.Windows.Forms.TextBox();
			this.label52 = new System.Windows.Forms.Label();
			this.label49 = new System.Windows.Forms.Label();
			this.textBox27 = new System.Windows.Forms.TextBox();
			this.label50 = new System.Windows.Forms.Label();
			this.label47 = new System.Windows.Forms.Label();
			this.textBox26 = new System.Windows.Forms.TextBox();
			this.label48 = new System.Windows.Forms.Label();
			this.label45 = new System.Windows.Forms.Label();
			this.textBox25 = new System.Windows.Forms.TextBox();
			this.label46 = new System.Windows.Forms.Label();
			this.label43 = new System.Windows.Forms.Label();
			this.textBox24 = new System.Windows.Forms.TextBox();
			this.label44 = new System.Windows.Forms.Label();
			this.label41 = new System.Windows.Forms.Label();
			this.textBox23 = new System.Windows.Forms.TextBox();
			this.label42 = new System.Windows.Forms.Label();
			this.label39 = new System.Windows.Forms.Label();
			this.textBox22 = new System.Windows.Forms.TextBox();
			this.label40 = new System.Windows.Forms.Label();
			this.label37 = new System.Windows.Forms.Label();
			this.textBox21 = new System.Windows.Forms.TextBox();
			this.label38 = new System.Windows.Forms.Label();
			this.label35 = new System.Windows.Forms.Label();
			this.textBox20 = new System.Windows.Forms.TextBox();
			this.label36 = new System.Windows.Forms.Label();
			this.label33 = new System.Windows.Forms.Label();
			this.textBox19 = new System.Windows.Forms.TextBox();
			this.label34 = new System.Windows.Forms.Label();
			this.label31 = new System.Windows.Forms.Label();
			this.textBox18 = new System.Windows.Forms.TextBox();
			this.label32 = new System.Windows.Forms.Label();
			this.label29 = new System.Windows.Forms.Label();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.label30 = new System.Windows.Forms.Label();
			this.label27 = new System.Windows.Forms.Label();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.label28 = new System.Windows.Forms.Label();
			this.label25 = new System.Windows.Forms.Label();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label26 = new System.Windows.Forms.Label();
			this.label19 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label20 = new System.Windows.Forms.Label();
			this.label17 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label18 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox5.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.comboBox2);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Location = new System.Drawing.Point(5, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 64);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(8, 2);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "2400";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.Even;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 29;
			this.pipeSelectControl1.TcpPortText = "502";
			this.pipeSelectControl1.UdpPortText = "502";
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox2.Location = new System.Drawing.Point(277, 33);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(117, 25);
			this.comboBox2.TabIndex = 28;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(117, 37);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(106, 21);
			this.checkBox1.TabIndex = 9;
			this.checkBox1.Text = "首地址从0开始";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox15.Location = new System.Drawing.Point(52, 34);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(37, 23);
			this.textBox15.TabIndex = 7;
			this.textBox15.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(8, 37);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(901, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(804, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(406, 35);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 26;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteOp1);
			this.panel2.Controls.Add(this.groupBox5);
			this.panel2.Location = new System.Drawing.Point(5, 103);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 570);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(4, 2);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(988, 272);
			this.userControlReadWriteOp1.TabIndex = 5;
			this.groupBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox5.Controls.Add(this.textBox2);
			this.groupBox5.Controls.Add(this.label53);
			this.groupBox5.Controls.Add(this.textBox12);
			this.groupBox5.Controls.Add(this.label54);
			this.groupBox5.Controls.Add(this.button4);
			this.groupBox5.Controls.Add(this.hslCurve1);
			this.groupBox5.Controls.Add(this.label51);
			this.groupBox5.Controls.Add(this.textBox28);
			this.groupBox5.Controls.Add(this.label52);
			this.groupBox5.Controls.Add(this.label49);
			this.groupBox5.Controls.Add(this.textBox27);
			this.groupBox5.Controls.Add(this.label50);
			this.groupBox5.Controls.Add(this.label47);
			this.groupBox5.Controls.Add(this.textBox26);
			this.groupBox5.Controls.Add(this.label48);
			this.groupBox5.Controls.Add(this.label45);
			this.groupBox5.Controls.Add(this.textBox25);
			this.groupBox5.Controls.Add(this.label46);
			this.groupBox5.Controls.Add(this.label43);
			this.groupBox5.Controls.Add(this.textBox24);
			this.groupBox5.Controls.Add(this.label44);
			this.groupBox5.Controls.Add(this.label41);
			this.groupBox5.Controls.Add(this.textBox23);
			this.groupBox5.Controls.Add(this.label42);
			this.groupBox5.Controls.Add(this.label39);
			this.groupBox5.Controls.Add(this.textBox22);
			this.groupBox5.Controls.Add(this.label40);
			this.groupBox5.Controls.Add(this.label37);
			this.groupBox5.Controls.Add(this.textBox21);
			this.groupBox5.Controls.Add(this.label38);
			this.groupBox5.Controls.Add(this.label35);
			this.groupBox5.Controls.Add(this.textBox20);
			this.groupBox5.Controls.Add(this.label36);
			this.groupBox5.Controls.Add(this.label33);
			this.groupBox5.Controls.Add(this.textBox19);
			this.groupBox5.Controls.Add(this.label34);
			this.groupBox5.Controls.Add(this.label31);
			this.groupBox5.Controls.Add(this.textBox18);
			this.groupBox5.Controls.Add(this.label32);
			this.groupBox5.Controls.Add(this.label29);
			this.groupBox5.Controls.Add(this.textBox14);
			this.groupBox5.Controls.Add(this.label30);
			this.groupBox5.Controls.Add(this.label27);
			this.groupBox5.Controls.Add(this.textBox13);
			this.groupBox5.Controls.Add(this.label28);
			this.groupBox5.Controls.Add(this.label25);
			this.groupBox5.Controls.Add(this.textBox11);
			this.groupBox5.Controls.Add(this.label26);
			this.groupBox5.Controls.Add(this.label19);
			this.groupBox5.Controls.Add(this.textBox10);
			this.groupBox5.Controls.Add(this.label20);
			this.groupBox5.Controls.Add(this.label17);
			this.groupBox5.Controls.Add(this.textBox9);
			this.groupBox5.Controls.Add(this.label18);
			this.groupBox5.Controls.Add(this.label10);
			this.groupBox5.Controls.Add(this.label11);
			this.groupBox5.Controls.Add(this.label12);
			this.groupBox5.Controls.Add(this.label13);
			this.groupBox5.Controls.Add(this.label14);
			this.groupBox5.Controls.Add(this.label15);
			this.groupBox5.Controls.Add(this.label16);
			this.groupBox5.Controls.Add(this.textBox8);
			this.groupBox5.Controls.Add(this.label9);
			this.groupBox5.Controls.Add(this.textBox7);
			this.groupBox5.Controls.Add(this.label8);
			this.groupBox5.Controls.Add(this.textBox6);
			this.groupBox5.Controls.Add(this.label7);
			this.groupBox5.Controls.Add(this.textBox5);
			this.groupBox5.Controls.Add(this.label6);
			this.groupBox5.Controls.Add(this.textBox4);
			this.groupBox5.Controls.Add(this.label5);
			this.groupBox5.Controls.Add(this.textBox3);
			this.groupBox5.Controls.Add(this.label4);
			this.groupBox5.Controls.Add(this.textBox1);
			this.groupBox5.Controls.Add(this.label2);
			this.groupBox5.Controls.Add(this.button3);
			this.groupBox5.Location = new System.Drawing.Point(5, 277);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(987, 286);
			this.groupBox5.TabIndex = 4;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "特殊功能测试";
			this.label53.AutoSize = true;
			this.label53.Location = new System.Drawing.Point(786, 32);
			this.label53.Name = "label53";
			this.label53.Size = new System.Drawing.Size(25, 17);
			this.label53.TabIndex = 86;
			this.label53.Text = "ms";
			this.textBox12.Location = new System.Drawing.Point(705, 29);
			this.textBox12.Name = "textBox12";
			this.textBox12.Size = new System.Drawing.Size(78, 23);
			this.textBox12.TabIndex = 85;
			this.textBox12.Text = "1000";
			this.label54.AutoSize = true;
			this.label54.Location = new System.Drawing.Point(659, 32);
			this.label54.Name = "label54";
			this.label54.Size = new System.Drawing.Size(32, 17);
			this.label54.TabIndex = 84;
			this.label54.Text = "间隔";
			this.button4.Location = new System.Drawing.Point(837, 26);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 83;
			this.button4.Text = "定时读取";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.hslCurve1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslCurve1.BackColor = System.Drawing.Color.White;
			this.hslCurve1.FontCalibration = new System.Drawing.Font("微软雅黑", 9f);
			this.hslCurve1.Location = new System.Drawing.Point(594, 62);
			this.hslCurve1.Name = "hslCurve1";
			this.hslCurve1.ReferenceAxisLeft.Max = 250f;
			this.hslCurve1.Size = new System.Drawing.Size(387, 183);
			this.hslCurve1.TabIndex = 82;
			this.label51.AutoSize = true;
			this.label51.Location = new System.Drawing.Point(561, 231);
			this.label51.Name = "label51";
			this.label51.Size = new System.Drawing.Size(0, 17);
			this.label51.TabIndex = 81;
			this.textBox28.Location = new System.Drawing.Point(480, 226);
			this.textBox28.Name = "textBox28";
			this.textBox28.Size = new System.Drawing.Size(78, 23);
			this.textBox28.TabIndex = 80;
			this.label52.AutoSize = true;
			this.label52.Location = new System.Drawing.Point(387, 229);
			this.label52.Name = "label52";
			this.label52.Size = new System.Drawing.Size(80, 17);
			this.label52.TabIndex = 79;
			this.label52.Text = "总相功率因数";
			this.label49.AutoSize = true;
			this.label49.Location = new System.Drawing.Point(561, 201);
			this.label49.Name = "label49";
			this.label49.Size = new System.Drawing.Size(0, 17);
			this.label49.TabIndex = 78;
			this.textBox27.Location = new System.Drawing.Point(480, 198);
			this.textBox27.Name = "textBox27";
			this.textBox27.Size = new System.Drawing.Size(78, 23);
			this.textBox27.TabIndex = 77;
			this.label50.AutoSize = true;
			this.label50.Location = new System.Drawing.Point(387, 201);
			this.label50.Name = "label50";
			this.label50.Size = new System.Drawing.Size(76, 17);
			this.label50.TabIndex = 76;
			this.label50.Text = "C相功率因数";
			this.label47.AutoSize = true;
			this.label47.Location = new System.Drawing.Point(561, 174);
			this.label47.Name = "label47";
			this.label47.Size = new System.Drawing.Size(0, 17);
			this.label47.TabIndex = 75;
			this.textBox26.Location = new System.Drawing.Point(480, 171);
			this.textBox26.Name = "textBox26";
			this.textBox26.Size = new System.Drawing.Size(78, 23);
			this.textBox26.TabIndex = 74;
			this.label48.AutoSize = true;
			this.label48.Location = new System.Drawing.Point(387, 174);
			this.label48.Name = "label48";
			this.label48.Size = new System.Drawing.Size(76, 17);
			this.label48.TabIndex = 73;
			this.label48.Text = "B相功率因数";
			this.label45.AutoSize = true;
			this.label45.Location = new System.Drawing.Point(560, 145);
			this.label45.Name = "label45";
			this.label45.Size = new System.Drawing.Size(0, 17);
			this.label45.TabIndex = 72;
			this.textBox25.Location = new System.Drawing.Point(479, 142);
			this.textBox25.Name = "textBox25";
			this.textBox25.Size = new System.Drawing.Size(78, 23);
			this.textBox25.TabIndex = 71;
			this.label46.AutoSize = true;
			this.label46.Location = new System.Drawing.Point(386, 145);
			this.label46.Name = "label46";
			this.label46.Size = new System.Drawing.Size(76, 17);
			this.label46.TabIndex = 70;
			this.label46.Text = "A相功率因数";
			this.label43.AutoSize = true;
			this.label43.Location = new System.Drawing.Point(560, 116);
			this.label43.Name = "label43";
			this.label43.Size = new System.Drawing.Size(31, 17);
			this.label43.TabIndex = 69;
			this.label43.Text = "kVA";
			this.textBox24.Location = new System.Drawing.Point(479, 113);
			this.textBox24.Name = "textBox24";
			this.textBox24.Size = new System.Drawing.Size(78, 23);
			this.textBox24.TabIndex = 68;
			this.label44.AutoSize = true;
			this.label44.Location = new System.Drawing.Point(386, 116);
			this.label44.Name = "label44";
			this.label44.Size = new System.Drawing.Size(92, 17);
			this.label44.TabIndex = 67;
			this.label44.Text = "瞬时总视在功率";
			this.label41.AutoSize = true;
			this.label41.Location = new System.Drawing.Point(560, 88);
			this.label41.Name = "label41";
			this.label41.Size = new System.Drawing.Size(31, 17);
			this.label41.TabIndex = 66;
			this.label41.Text = "kVA";
			this.textBox23.Location = new System.Drawing.Point(479, 85);
			this.textBox23.Name = "textBox23";
			this.textBox23.Size = new System.Drawing.Size(78, 23);
			this.textBox23.TabIndex = 65;
			this.label42.AutoSize = true;
			this.label42.Location = new System.Drawing.Point(386, 88);
			this.label42.Name = "label42";
			this.label42.Size = new System.Drawing.Size(88, 17);
			this.label42.TabIndex = 64;
			this.label42.Text = "瞬时C视在功率";
			this.label39.AutoSize = true;
			this.label39.Location = new System.Drawing.Point(560, 60);
			this.label39.Name = "label39";
			this.label39.Size = new System.Drawing.Size(31, 17);
			this.label39.TabIndex = 63;
			this.label39.Text = "kVA";
			this.textBox22.Location = new System.Drawing.Point(479, 57);
			this.textBox22.Name = "textBox22";
			this.textBox22.Size = new System.Drawing.Size(78, 23);
			this.textBox22.TabIndex = 62;
			this.label40.AutoSize = true;
			this.label40.Location = new System.Drawing.Point(386, 60);
			this.label40.Name = "label40";
			this.label40.Size = new System.Drawing.Size(88, 17);
			this.label40.TabIndex = 61;
			this.label40.Text = "瞬时B视在功率";
			this.label37.AutoSize = true;
			this.label37.Location = new System.Drawing.Point(560, 32);
			this.label37.Name = "label37";
			this.label37.Size = new System.Drawing.Size(31, 17);
			this.label37.TabIndex = 60;
			this.label37.Text = "kVA";
			this.textBox21.Location = new System.Drawing.Point(479, 29);
			this.textBox21.Name = "textBox21";
			this.textBox21.Size = new System.Drawing.Size(78, 23);
			this.textBox21.TabIndex = 59;
			this.label38.AutoSize = true;
			this.label38.Location = new System.Drawing.Point(386, 32);
			this.label38.Name = "label38";
			this.label38.Size = new System.Drawing.Size(88, 17);
			this.label38.TabIndex = 58;
			this.label38.Text = "瞬时A视在功率";
			this.label35.AutoSize = true;
			this.label35.Location = new System.Drawing.Point(343, 229);
			this.label35.Name = "label35";
			this.label35.Size = new System.Drawing.Size(33, 17);
			this.label35.TabIndex = 57;
			this.label35.Text = "kvar";
			this.textBox20.Location = new System.Drawing.Point(262, 226);
			this.textBox20.Name = "textBox20";
			this.textBox20.Size = new System.Drawing.Size(78, 23);
			this.textBox20.TabIndex = 56;
			this.label36.AutoSize = true;
			this.label36.Location = new System.Drawing.Point(169, 229);
			this.label36.Name = "label36";
			this.label36.Size = new System.Drawing.Size(92, 17);
			this.label36.TabIndex = 55;
			this.label36.Text = "瞬时总无功功率";
			this.label33.AutoSize = true;
			this.label33.Location = new System.Drawing.Point(343, 201);
			this.label33.Name = "label33";
			this.label33.Size = new System.Drawing.Size(33, 17);
			this.label33.TabIndex = 54;
			this.label33.Text = "kvar";
			this.textBox19.Location = new System.Drawing.Point(262, 198);
			this.textBox19.Name = "textBox19";
			this.textBox19.Size = new System.Drawing.Size(78, 23);
			this.textBox19.TabIndex = 53;
			this.label34.AutoSize = true;
			this.label34.Location = new System.Drawing.Point(169, 201);
			this.label34.Name = "label34";
			this.label34.Size = new System.Drawing.Size(88, 17);
			this.label34.TabIndex = 52;
			this.label34.Text = "瞬时C无功功率";
			this.label31.AutoSize = true;
			this.label31.Location = new System.Drawing.Point(343, 172);
			this.label31.Name = "label31";
			this.label31.Size = new System.Drawing.Size(33, 17);
			this.label31.TabIndex = 51;
			this.label31.Text = "kvar";
			this.textBox18.Location = new System.Drawing.Point(262, 169);
			this.textBox18.Name = "textBox18";
			this.textBox18.Size = new System.Drawing.Size(78, 23);
			this.textBox18.TabIndex = 50;
			this.label32.AutoSize = true;
			this.label32.Location = new System.Drawing.Point(169, 172);
			this.label32.Name = "label32";
			this.label32.Size = new System.Drawing.Size(88, 17);
			this.label32.TabIndex = 49;
			this.label32.Text = "瞬时B无功功率";
			this.label29.AutoSize = true;
			this.label29.Location = new System.Drawing.Point(343, 146);
			this.label29.Name = "label29";
			this.label29.Size = new System.Drawing.Size(33, 17);
			this.label29.TabIndex = 48;
			this.label29.Text = "kvar";
			this.textBox14.Location = new System.Drawing.Point(262, 143);
			this.textBox14.Name = "textBox14";
			this.textBox14.Size = new System.Drawing.Size(78, 23);
			this.textBox14.TabIndex = 47;
			this.label30.AutoSize = true;
			this.label30.Location = new System.Drawing.Point(169, 146);
			this.label30.Name = "label30";
			this.label30.Size = new System.Drawing.Size(88, 17);
			this.label30.TabIndex = 46;
			this.label30.Text = "瞬时A无功功率";
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(343, 117);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(24, 17);
			this.label27.TabIndex = 45;
			this.label27.Text = "kw";
			this.textBox13.Location = new System.Drawing.Point(262, 114);
			this.textBox13.Name = "textBox13";
			this.textBox13.Size = new System.Drawing.Size(78, 23);
			this.textBox13.TabIndex = 44;
			this.label28.AutoSize = true;
			this.label28.Location = new System.Drawing.Point(169, 117);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(92, 17);
			this.label28.TabIndex = 43;
			this.label28.Text = "瞬时总有功功率";
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(343, 88);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(24, 17);
			this.label25.TabIndex = 42;
			this.label25.Text = "kw";
			this.textBox11.Location = new System.Drawing.Point(262, 85);
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(78, 23);
			this.textBox11.TabIndex = 41;
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(169, 88);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(88, 17);
			this.label26.TabIndex = 40;
			this.label26.Text = "瞬时C有功功率";
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(343, 60);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(24, 17);
			this.label19.TabIndex = 39;
			this.label19.Text = "kw";
			this.textBox10.Location = new System.Drawing.Point(262, 57);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(78, 23);
			this.textBox10.TabIndex = 38;
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(169, 60);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(88, 17);
			this.label20.TabIndex = 37;
			this.label20.Text = "瞬时B有功功率";
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(343, 32);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(24, 17);
			this.label17.TabIndex = 36;
			this.label17.Text = "kw";
			this.textBox9.Location = new System.Drawing.Point(262, 29);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(78, 23);
			this.textBox9.TabIndex = 35;
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(169, 32);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(88, 17);
			this.label18.TabIndex = 34;
			this.label18.Text = "瞬时A有功功率";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(141, 229);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(23, 17);
			this.label10.TabIndex = 33;
			this.label10.Text = "Hz";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(141, 201);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(16, 17);
			this.label11.TabIndex = 32;
			this.label11.Text = "A";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(141, 174);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(16, 17);
			this.label12.TabIndex = 31;
			this.label12.Text = "A";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(141, 145);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(16, 17);
			this.label13.TabIndex = 30;
			this.label13.Text = "A";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(141, 116);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(16, 17);
			this.label14.TabIndex = 29;
			this.label14.Text = "V";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(141, 88);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(16, 17);
			this.label15.TabIndex = 28;
			this.label15.Text = "V";
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(141, 60);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(16, 17);
			this.label16.TabIndex = 27;
			this.label16.Text = "V";
			this.textBox8.Location = new System.Drawing.Point(60, 226);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(78, 23);
			this.textBox8.TabIndex = 26;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(14, 229);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 25;
			this.label9.Text = "频率";
			this.textBox7.Location = new System.Drawing.Point(60, 198);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(78, 23);
			this.textBox7.TabIndex = 24;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(14, 201);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(40, 17);
			this.label8.TabIndex = 23;
			this.label8.Text = "电流C";
			this.textBox6.Location = new System.Drawing.Point(60, 171);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(78, 23);
			this.textBox6.TabIndex = 22;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(14, 174);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(40, 17);
			this.label7.TabIndex = 21;
			this.label7.Text = "电流B";
			this.textBox5.Location = new System.Drawing.Point(60, 142);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(78, 23);
			this.textBox5.TabIndex = 20;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(14, 145);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(40, 17);
			this.label6.TabIndex = 19;
			this.label6.Text = "电流A";
			this.textBox4.Location = new System.Drawing.Point(60, 113);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(78, 23);
			this.textBox4.TabIndex = 18;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(14, 116);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(40, 17);
			this.label5.TabIndex = 17;
			this.label5.Text = "电压C";
			this.textBox3.Location = new System.Drawing.Point(60, 85);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(78, 23);
			this.textBox3.TabIndex = 16;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(14, 88);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(40, 17);
			this.label4.TabIndex = 15;
			this.label4.Text = "电压B";
			this.textBox1.Location = new System.Drawing.Point(60, 57);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(78, 23);
			this.textBox1.TabIndex = 14;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(14, 60);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(40, 17);
			this.label2.TabIndex = 13;
			this.label2.Text = "电压A";
			this.button3.Location = new System.Drawing.Point(41, 22);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "读取电表";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Modbus Rtu";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.textBox2.Location = new System.Drawing.Point(6, 255);
			this.textBox2.Name = "textBox2";
			this.textBox2.ReadOnly = true;
			this.textBox2.Size = new System.Drawing.Size(975, 23);
			this.textBox2.TabIndex = 87;
			this.textBox2.Text = "OperateResult<ElectricalParameters> read = modbus.ReadElectricalParameters( );";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 675);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormDTSU6606";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "DTSU6606";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
