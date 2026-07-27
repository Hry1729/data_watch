using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Robot.YASKAWA;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormYRC1000 : HslFormContent
	{
		private YRC1000TcpNet YRC1000Tcp = null;

		private IContainer components = null;

		private Panel panel1;

		private Label label1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private GroupBox groupBox2;

		private Button button14;

		private TextBox textBox7;

		private Label label9;

		private TextBox textBox8;

		private Label label10;

		private GroupBox groupBox1;

		private Button button_read_string;

		private TextBox textBox4;

		private Label label7;

		private TextBox textBox3;

		private Label label6;

		private Label label22;

		private Label label21;

		private Label label8;

		private UserControlHead userControlHead1;

		private Button button4;

		private Button button3;

		private Button button20;

		private Button button19;

		private Button button18;

		private TextBox textBox6;

		private Label label5;

		private Button button17;

		private Button button16;

		private Button button15;

		private Button button13;

		private Label label4;

		private Button button12;

		private Button button11;

		private Button button10;

		private Button button9;

		private Button button8;

		private TextBox textBox5;

		private Label label2;

		private Button button7;

		private Button button6;

		private Button button5;

		private Label label11;

		private ComboBox comboBox1;

		private Button button22;

		private Button button21;

		private Label label_code;

		private TextBox textBox_code;

		public FormYRC1000()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox1.DataSource = new string[12]
			{
				"基坐标", "机器坐标", "用户1", "用户2", "用户3", "用户4", "用户5", "用户6", "用户7", "用户8",
				"用户9", "用户10"
			};
			comboBox1.SelectedIndex = 0;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "YASKAWA Robot Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label21.Text = "Address:";
				label6.Text = "address:";
				label7.Text = "result:";
				button_read_string.Text = "r-string";
				label10.Text = "Address:";
				label9.Text = "Value:";
				button14.Text = "w-string";
				groupBox1.Text = "Single Data Read test";
				groupBox2.Text = "Single Data Write test";
				label22.Text = "Parameter name of robot";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void readResultRender<T>(OperateResult<T> result, string address, TextBox textBox)
		{
			if (result.IsSuccess)
			{
				textBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + $"[{address}] {result.Content}{Environment.NewLine}");
				return;
			}
			DemoUtils.ShowMessage(DateTime.Now.ToString("[HH:mm:ss] ") + "[" + address + "] Read Failed" + Environment.NewLine + " Reason：" + result.ToMessageShowString());
		}

		private void writeResultRender(OperateResult result, string address)
		{
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage(DateTime.Now.ToString("[HH:mm:ss] ") + "[" + address + "] Write Success");
				return;
			}
			DemoUtils.ShowMessage(DateTime.Now.ToString("[HH:mm:ss] ") + "[" + address + "] Write Failed" + Environment.NewLine + " Reason：" + result.ToMessageShowString());
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var port))
			{
				DemoUtils.ShowMessage("端口输入格式不正确！");
				return;
			}
			YRC1000Tcp?.ConnectClose();
			YRC1000Tcp = new YRC1000TcpNet(textBox1.Text, port);
			YRC1000Tcp.LogNet = base.LogNet;
			try
			{
				if ((await YRC1000Tcp.ConnectServerAsync()).IsSuccess)
				{
					DemoUtils.ShowMessage("连接成功！");
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					textBox_code.Text = $"YRC1000TcpNet YRC1000Tcp = new YRC1000TcpNet( \"{textBox1.Text}\", {port} );";
				}
				else
				{
					DemoUtils.ShowMessage("连接失败！");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			YRC1000Tcp.ConnectClose();
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
		}

		private async void button_read_string_Click(object sender, EventArgs e)
		{
			readResultRender(await YRC1000Tcp.ReadStringAsync(textBox3.Text), textBox3.Text, textBox4);
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadString( \"" + textBox3.Text + "\" )";
		}

		private async void button14_Click(object sender, EventArgs e)
		{
			try
			{
				writeResultRender(await YRC1000Tcp.WriteAsync(textBox8.Text, textBox7.Text), textBox8.Text);
				textBox_code.Text = "OperateResult write = YRC1000Tcp.Write( \"" + textBox8.Text + "\", \"" + textBox7.Text + "\" );";
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadALARM();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadALARM( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadPOSJ();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadPOSJ( );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<YRCRobotData> operateResult = YRC1000Tcp.ReadPOSC(comboBox1.SelectedIndex, true);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = $"OperateResult<YRCRobotData> read = YRC1000Tcp.ReadPOSC( {comboBox1.SelectedIndex}, true );";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> operateResult = YRC1000Tcp.ReadStats();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + "[ 0] 单步              " + operateResult.Content[0] + Environment.NewLine + "[ 1] 1循环             " + operateResult.Content[1] + Environment.NewLine + "[ 2] 自动连续          " + operateResult.Content[2] + Environment.NewLine + "[ 3] 运行中            " + operateResult.Content[3] + Environment.NewLine + "[ 4] 运转中            " + operateResult.Content[4] + Environment.NewLine + "[ 5] 示教              " + operateResult.Content[5] + Environment.NewLine + "[ 6] 在线              " + operateResult.Content[6] + Environment.NewLine + "[ 7] 命令模式          " + operateResult.Content[7] + Environment.NewLine + "[ 9] 示教编程器HOLD中   " + operateResult.Content[9] + Environment.NewLine + "[10] 外部HOLD中        " + operateResult.Content[10] + Environment.NewLine + "[11] 命令HOLD中        " + operateResult.Content[11] + Environment.NewLine + "[12] 发生警报          " + operateResult.Content[12] + Environment.NewLine + "[13] 发生错误          " + operateResult.Content[13] + Environment.NewLine + "[14] 伺服ON            " + operateResult.Content[14] + Environment.NewLine;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<bool[]> read = YRC1000Tcp.ReadStats( );";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadJSeq();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + "程序名，行编号，步编号: " + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadJSeq( );";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadByteVariable(textBox5.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadByteVariable( \"" + textBox5.Text + "\" );";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadIntegerVariable(textBox5.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadIntegerVariable( \"" + textBox5.Text + "\" );";
		}

		private void button10_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadDoubleIntegerVariable(textBox5.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadDoubleIntegerVariable( \"" + textBox5.Text + "\" );";
		}

		private void button11_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadRealVariable(textBox5.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadRealVariable( \"" + textBox5.Text + "\" );";
		}

		private void button12_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = YRC1000Tcp.ReadStringVariable(textBox5.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = YRC1000Tcp.ReadStringVariable( \"" + textBox5.Text + "\" );";
		}

		private void button13_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Hold(true);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("HOLD ON Success");
			}
			else
			{
				DemoUtils.ShowMessage("HOLD ON Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Hold( true );";
		}

		private void button15_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Hold(false);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("HOLD OFF Success");
			}
			else
			{
				DemoUtils.ShowMessage("HOLD OFF Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Hold( false );";
		}

		private void button16_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Reset();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("RESET Success");
			}
			else
			{
				DemoUtils.ShowMessage("RESET Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Reset( );";
		}

		private void button17_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Cancel();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Cancel Success");
			}
			else
			{
				DemoUtils.ShowMessage("Cancel Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Cancel( );";
		}

		private void button21_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Svon(true);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SVON ON Success");
			}
			else
			{
				DemoUtils.ShowMessage("SVON ON Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Svon( true );";
		}

		private void button22_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Svon(false);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SVON OFF Success");
			}
			else
			{
				DemoUtils.ShowMessage("SVON OFF Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Svon( false );";
		}

		private void button18_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Start(textBox6.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Start Success");
			}
			else
			{
				DemoUtils.ShowMessage("Start Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Start( \"" + textBox6.Text + "\" );";
		}

		private void button19_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.Delete(textBox6.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Delte Success");
			}
			else
			{
				DemoUtils.ShowMessage("Delte Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.Delete( \"" + textBox6.Text + "\" );";
		}

		private void button20_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = YRC1000Tcp.SetMJ(textBox6.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Set Success");
			}
			else
			{
				DemoUtils.ShowMessage("Set Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = YRC1000Tcp.SetMJ( \"" + textBox6.Text + "\" );";
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
			this.label22 = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button14 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button22 = new System.Windows.Forms.Button();
			this.button21 = new System.Windows.Forms.Button();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button20 = new System.Windows.Forms.Button();
			this.button19 = new System.Windows.Forms.Button();
			this.button18 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button17 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.button13 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.button12 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.label8 = new System.Windows.Forms.Label();
			this.button_read_string = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 42);
			this.panel1.TabIndex = 0;
			this.label22.Location = new System.Drawing.Point(651, 0);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(341, 45);
			this.label22.TabIndex = 7;
			this.label22.Text = "RALARM      IOREAD;50010,24        HOLD;0      RPOSC:0,0";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(565, 0);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(80, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "自定义示例：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(468, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(361, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(272, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(83, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "80";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(218, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 9);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(141, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.label_code);
			this.panel2.Controls.Add(this.groupBox2);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(3, 80);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 563);
			this.panel2.TabIndex = 1;
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button14);
			this.groupBox2.Controls.Add(this.textBox7);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox8);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Location = new System.Drawing.Point(653, 2);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(339, 498);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "单数据写入测试";
			this.button14.Location = new System.Drawing.Point(251, 24);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(82, 28);
			this.button14.TabIndex = 16;
			this.button14.Text = "字符串写入";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox7.Location = new System.Drawing.Point(63, 56);
			this.textBox7.Multiline = true;
			this.textBox7.Name = "textBox7";
			this.textBox7.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox7.Size = new System.Drawing.Size(270, 418);
			this.textBox7.TabIndex = 5;
			this.textBox7.Text = "10";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(9, 58);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 4;
			this.label9.Text = "值：";
			this.textBox8.Location = new System.Drawing.Point(63, 27);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(132, 23);
			this.textBox8.TabIndex = 3;
			this.textBox8.Text = "IOWRITE";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(9, 30);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "地址：";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button22);
			this.groupBox1.Controls.Add(this.button21);
			this.groupBox1.Controls.Add(this.comboBox1);
			this.groupBox1.Controls.Add(this.button20);
			this.groupBox1.Controls.Add(this.button19);
			this.groupBox1.Controls.Add(this.button18);
			this.groupBox1.Controls.Add(this.textBox6);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.button17);
			this.groupBox1.Controls.Add(this.button16);
			this.groupBox1.Controls.Add(this.button15);
			this.groupBox1.Controls.Add(this.button13);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.button12);
			this.groupBox1.Controls.Add(this.button11);
			this.groupBox1.Controls.Add(this.button10);
			this.groupBox1.Controls.Add(this.button9);
			this.groupBox1.Controls.Add(this.button8);
			this.groupBox1.Controls.Add(this.textBox5);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.button7);
			this.groupBox1.Controls.Add(this.button6);
			this.groupBox1.Controls.Add(this.button5);
			this.groupBox1.Controls.Add(this.button4);
			this.groupBox1.Controls.Add(this.button3);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.label11);
			this.groupBox1.Location = new System.Drawing.Point(4, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(643, 497);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.button22.Location = new System.Drawing.Point(483, 118);
			this.button22.Name = "button22";
			this.button22.Size = new System.Drawing.Size(80, 28);
			this.button22.TabIndex = 43;
			this.button22.Text = "Svon OFF";
			this.button22.UseVisualStyleBackColor = true;
			this.button22.Click += new System.EventHandler(button22_Click);
			this.button21.Location = new System.Drawing.Point(397, 118);
			this.button21.Name = "button21";
			this.button21.Size = new System.Drawing.Size(80, 28);
			this.button21.TabIndex = 42;
			this.button21.Text = "Svon ON";
			this.button21.UseVisualStyleBackColor = true;
			this.button21.Click += new System.EventHandler(button21_Click);
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(450, 58);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(82, 25);
			this.comboBox1.TabIndex = 41;
			this.button20.Location = new System.Drawing.Point(384, 149);
			this.button20.Name = "button20";
			this.button20.Size = new System.Drawing.Size(82, 28);
			this.button20.TabIndex = 39;
			this.button20.Text = "设定主程序";
			this.button20.UseVisualStyleBackColor = true;
			this.button20.Click += new System.EventHandler(button20_Click);
			this.button19.Location = new System.Drawing.Point(296, 149);
			this.button19.Name = "button19";
			this.button19.Size = new System.Drawing.Size(82, 28);
			this.button19.TabIndex = 38;
			this.button19.Text = "删除程序";
			this.button19.UseVisualStyleBackColor = true;
			this.button19.Click += new System.EventHandler(button19_Click);
			this.button18.Location = new System.Drawing.Point(205, 149);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(82, 28);
			this.button18.TabIndex = 37;
			this.button18.Text = "启动程序";
			this.button18.UseVisualStyleBackColor = true;
			this.button18.Click += new System.EventHandler(button18_Click);
			this.textBox6.Location = new System.Drawing.Point(71, 152);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(128, 23);
			this.textBox6.TabIndex = 36;
			this.textBox6.Text = "*";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(9, 155);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(56, 17);
			this.label5.TabIndex = 35;
			this.label5.Text = "程序名：";
			this.button17.Location = new System.Drawing.Point(326, 118);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(65, 28);
			this.button17.TabIndex = 34;
			this.button17.Text = "Cancel";
			this.button17.UseVisualStyleBackColor = true;
			this.button17.Click += new System.EventHandler(button17_Click);
			this.button16.Location = new System.Drawing.Point(255, 118);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(65, 28);
			this.button16.TabIndex = 33;
			this.button16.Text = "Reset";
			this.button16.UseVisualStyleBackColor = true;
			this.button16.Click += new System.EventHandler(button16_Click);
			this.button15.Location = new System.Drawing.Point(163, 118);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(86, 28);
			this.button15.TabIndex = 32;
			this.button15.Text = "Hold OFF";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button13.Location = new System.Drawing.Point(71, 118);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(86, 28);
			this.button13.TabIndex = 31;
			this.button13.Text = "Hold ON";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(9, 124);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 30;
			this.label4.Text = "控制操作：";
			this.button12.Location = new System.Drawing.Point(536, 88);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(95, 28);
			this.button12.TabIndex = 29;
			this.button12.Text = "字符串读取";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button11.Location = new System.Drawing.Point(435, 88);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(95, 28);
			this.button11.TabIndex = 28;
			this.button11.Text = "实数读取";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(338, 88);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(91, 28);
			this.button10.TabIndex = 27;
			this.button10.Text = "双整型读取";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Location = new System.Drawing.Point(250, 88);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(82, 28);
			this.button9.TabIndex = 26;
			this.button9.Text = "整型读取";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Location = new System.Drawing.Point(162, 88);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(82, 28);
			this.button8.TabIndex = 25;
			this.button8.Text = "字节读取";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.textBox5.Location = new System.Drawing.Point(71, 91);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(85, 23);
			this.textBox5.TabIndex = 24;
			this.textBox5.Text = "000";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 94);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 23;
			this.label2.Text = "变量地址：";
			this.button7.Location = new System.Drawing.Point(361, 57);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(83, 28);
			this.button7.TabIndex = 22;
			this.button7.Text = "程序名读取";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(276, 57);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(79, 28);
			this.button6.TabIndex = 21;
			this.button6.Text = "状态读取";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(536, 56);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(101, 28);
			this.button5.TabIndex = 20;
			this.button5.Text = "姿态坐标读取";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(179, 57);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 19;
			this.button4.Text = "关节坐标读取";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(71, 57);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(102, 28);
			this.button3.TabIndex = 18;
			this.button3.Text = "报警信息读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(55, 476);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(296, 17);
			this.label8.TabIndex = 17;
			this.label8.Text = "如果是带命令数据的。使用点，分号，冒号来区分指令";
			this.button_read_string.Location = new System.Drawing.Point(555, 24);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(82, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "字符串读取";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(button_read_string_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(58, 179);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(579, 294);
			this.textBox4.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(9, 179);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.textBox3.Location = new System.Drawing.Point(95, 27);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(454, 23);
			this.textBox3.TabIndex = 3;
			this.textBox3.Text = "RALARM";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 30);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(80, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "自定义命令：";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(9, 63);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(68, 17);
			this.label11.TabIndex = 40;
			this.label11.Text = "状态读取：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "YASKAWA - Ethernet 服务器功能(非高速)";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 508);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 24;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(62, 504);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(924, 52);
			this.textBox_code.TabIndex = 25;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormYRC1000";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "安川机器人访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
