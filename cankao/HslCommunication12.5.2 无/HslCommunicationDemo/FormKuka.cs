using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Robot.KUKA;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormKuka : HslFormContent
	{
		private KukaAvarProxyNet kuka = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

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

		private UserControlHead userControlHead1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private Label label2;

		private LinkLabel linkLabel1;

		private Label label4;

		private LinkLabel linkLabel2;

		private Label label5;

		private TextBox textBox_code;

		private Label label_code;

		public FormKuka()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(new DeviceAddressExample[25]
			{
				new DeviceAddressExample("$OV_PRO", "OV_PRO variable", false, false, "ReadString / ReadStringAsync"),
				new DeviceAddressExample("$OV_JOG", "OV_JOG variable", false, false, "ReadString / ReadStringAsync"),
				new DeviceAddressExample("$AXIS_ACT", "AXIS_ACT variable", false, false, "ReadString / ReadStringAsync"),
				new DeviceAddressExample("$AUT", "", false, false, "[String] “自动模式”信号声明"),
				new DeviceAddressExample("$AXIS_INT", "", false, false, "[String] 中断时机器人的位置"),
				new DeviceAddressExample("$MODE_OP", "MODE_OP variable", false, false, "[String] 显示当前操作模式"),
				new DeviceAddressExample("$OUT[1]", "OUT[1] variable", false, false, "ReadString / ReadStringAsync"),
				new DeviceAddressExample("$ACC_C", "", false, false, "[String] 在主运行中的轨迹、旋转和转动加速度"),
				new DeviceAddressExample("$ACT_TOOL", "", false, false, "[String] 当前的刀具坐标系统号"),
				new DeviceAddressExample("$COSYS", "", false, false, "[String] 步进坐标系统"),
				new DeviceAddressExample("$COUNT_I[32]", "", false, false, "String 自由使用整数变量"),
				new DeviceAddressExample("$PHGINFO", "", false, false, "[String] KCP CPU和软件版本的系列号"),
				new DeviceAddressExample("$POS_ACT", "", false, false, "[String] 笛卡儿坐标系中当前机器人位置"),
				new DeviceAddressExample("$POS_FOR", "", false, false, "[String] 笛卡儿坐标系中当前运动程序段的目标位置"),
				new DeviceAddressExample("$PRO_IP.NAME[32]", "", false, false, "[String] 主运行的程序段名称"),
				new DeviceAddressExample("$PRO_IP.SNR_C", "", false, false, "[String] 提前运行的程序段号"),
				new DeviceAddressExample("$PRO_START", "", false, false, "[String] 开始程序/命令执行"),
				new DeviceAddressExample("$ROBROOT", "", false, false, "[String] 机器人在world坐标系中的位置"),
				new DeviceAddressExample("$ROBTRAFO", "", false, false, "[String] 机器人名称"),
				new DeviceAddressExample("$TIME_POS[n]", "", false, false, "[String] 轴定位时间"),
				new DeviceAddressExample("$TOOL.A", "", false, false, "[String] 绕Z轴旋转"),
				new DeviceAddressExample("$KR_SERIALNO", "", false, false, "[String] 机器人序列号"),
				new DeviceAddressExample("$MODEL_NAME", "", false, false, "[String] 机器人类"),
				new DeviceAddressExample("$JUS_TOOL_NO", "", false, false, "[String] EMT 控制当前的刀具号"),
				new DeviceAddressExample("tool_data[1]", "frame variable", false, false, "ReadString / ReadStringAsync")
			});
			DemoUtils.AddSpecialFunctionTab(tabControl1, addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Kuka Robot Demo";
				label1.Text = "Ip:";
				label2.Text = "You need to install a VB program on the robot controller, which comes from github: ";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "address:";
				label7.Text = "result:";
				label5.Text = "If you need more robot system variable names, please become an enterprise user object, thanks for the support.";
				button_read_string.Text = "r-string";
				label10.Text = "Address:";
				label9.Text = "Value:";
				button14.Text = "w-string";
				groupBox1.Text = "Single Data Read test";
				groupBox2.Text = "Single Data Write test";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var port))
			{
				DemoUtils.ShowMessage("端口输入格式不正确！");
				return;
			}
			kuka?.ConnectClose();
			kuka = new KukaAvarProxyNet(textBox1.Text, port);
			kuka.LogNet = base.LogNet;
			try
			{
				OperateResult connect = await kuka.ConnectServerAsync();
				if (connect.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					codeExampleControl.SetCodeText("robot", kuka);
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.Message);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			kuka.ConnectClose();
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
		}

		private async void button_read_string_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(await kuka.ReadStringAsync(textBox3.Text), textBox3.Text, textBox4);
			textBox_code.Text = "OperateResult<byte[]> read = kuka.Read( \"" + textBox3.Text + "\" )";
		}

		private async void button14_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(await kuka.WriteAsync(textBox8.Text, textBox7.Text), textBox8.Text);
				textBox_code.Text = "OperateResult write = kuka.Write( \"" + textBox8.Text + "\", \"" + textBox7.Text + "\" )";
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

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			try
			{
				Process.Start(linkLabel1.Text);
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
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
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.label5 = new System.Windows.Forms.Label();
			this.linkLabel2 = new System.Windows.Forms.LinkLabel();
			this.label4 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button14 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button_read_string = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1000, 46);
			this.panel1.TabIndex = 0;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(600, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(493, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(368, 8);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(94, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "7000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(314, 11);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 8);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(231, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 11);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.label_code);
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Controls.Add(this.groupBox2);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(3, 85);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1000, 558);
			this.panel2.TabIndex = 1;
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(8, 327);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(983, 226);
			this.tabControl1.TabIndex = 2;
			this.tabPage1.Controls.Add(this.label5);
			this.tabPage1.Controls.Add(this.linkLabel2);
			this.tabPage1.Controls.Add(this.label4);
			this.tabPage1.Controls.Add(this.linkLabel1);
			this.tabPage1.Controls.Add(this.label2);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(975, 196);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Doc";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(10, 117);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(392, 17);
			this.label5.TabIndex = 5;
			this.label5.Text = "如果需要更多的机器人系统变量名称，请成为企业用户对象，感谢支持。";
			this.linkLabel2.AutoSize = true;
			this.linkLabel2.Location = new System.Drawing.Point(10, 62);
			this.linkLabel2.Name = "linkLabel2";
			this.linkLabel2.Size = new System.Drawing.Size(322, 17);
			this.linkLabel2.TabIndex = 4;
			this.linkLabel2.TabStop = true;
			this.linkLabel2.Text = "https://github.com/akselov/kukavarproxy-msg-format";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(10, 87);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(585, 17);
			this.label4.TabIndex = 3;
			this.label4.Text = "KukavarProxy is a TCP/IP server that enables reading and writing robot variables over the network.";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(10, 39);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(264, 17);
			this.linkLabel1.TabIndex = 2;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "https://github.com/lionpeloux/KukavarProxy";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(10, 12);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(345, 17);
			this.label2.TabIndex = 1;
			this.label2.Text = "需要在机器人控制器安装一个VB程序，该程序来自于 github： ";
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button14);
			this.groupBox2.Controls.Add(this.textBox7);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox8);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Location = new System.Drawing.Point(549, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(442, 263);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "单数据写入测试";
			this.button14.Location = new System.Drawing.Point(354, 24);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(82, 28);
			this.button14.TabIndex = 16;
			this.button14.Text = "字符串写入";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.textBox7.Location = new System.Drawing.Point(63, 56);
			this.textBox7.Name = "textBox7";
			this.textBox7.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox7.Size = new System.Drawing.Size(373, 23);
			this.textBox7.TabIndex = 5;
			this.textBox7.Text = "10";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(9, 60);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 4;
			this.label9.Text = "值：";
			this.textBox8.Location = new System.Drawing.Point(63, 27);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(285, 23);
			this.textBox8.TabIndex = 3;
			this.textBox8.Text = "$OV_PRO";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(9, 30);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "地址：";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(8, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(532, 263);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.button_read_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_string.Location = new System.Drawing.Point(422, 24);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(105, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "字符串读取";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(button_read_string_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(63, 56);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(464, 201);
			this.textBox4.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(9, 58);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(63, 27);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(353, 23);
			this.textBox3.TabIndex = 3;
			this.textBox3.Text = "$OV_PRO";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 30);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "地址：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://blog.davidrobot.com/2019/03/hsl_for_kuka.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "KUKAVARPROXY";
			this.userControlHead1.Size = new System.Drawing.Size(1007, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(9, 277);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 3;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(71, 272);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(920, 49);
			this.textBox_code.TabIndex = 4;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1007, 647);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormKuka";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Kuka机器人访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
