using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Robot.KUKA;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo
{
	public class FormKukaTcp : HslFormContent
	{
		private KukaTcpNet kuka = null;

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

		private Button button5;

		private Button button4;

		private Button button3;

		private TextBox textBox5;

		private Label label2;

		private PictureBox pictureBox1;

		private Label label4;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TextBox textBox_code;

		private Label label5;

		public FormKukaTcp()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			Language(Program.Language);
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Kuka Robot Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "address:";
				label7.Text = "result:";
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
			kuka = new KukaTcpNet(textBox1.Text, port);
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
					textBox_code.Text = $"KukaTcpNet kuka = new KukaTcpNet( \"{textBox1.Text}\", {port} );";
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
			textBox_code.Text = "OperateResult<string> read = kuka.ReadString( \"" + textBox3.Text + "\" )";
		}

		private async void button14_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(await kuka.WriteAsync(textBox8.Text, textBox7.Text), textBox8.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
			textBox_code.Text = "OperateResult write = kuka.Write( \"" + textBox8.Text + "\", \"" + textBox7.Text + "\" )";
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			OperateResult result = await kuka.StartProgramAsync(textBox5.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("Start Success");
			}
			else
			{
				DemoUtils.ShowMessage("Start Failed:" + result.Message);
			}
			textBox_code.Text = "OperateResult result = kuka.StartProgram( \"" + textBox5.Text + "\" );";
		}

		private async void button4_Click(object sender, EventArgs e)
		{
			OperateResult result = await kuka.ResetProgramAsync();
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("Reset Success");
			}
			else
			{
				DemoUtils.ShowMessage("Reset Failed:" + result.Message);
			}
			textBox_code.Text = "OperateResult result = kuka.ResetProgram( );";
		}

		private async void button5_Click(object sender, EventArgs e)
		{
			OperateResult result = await kuka.StopProgramAsync();
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("Reset Success");
			}
			else
			{
				DemoUtils.ShowMessage("Reset Failed:" + result.Message);
			}
			textBox_code.Text = "OperateResult result = kuka.StopProgram( );";
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
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.label4 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
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
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
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
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(996, 35);
			this.panel1.TabIndex = 0;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(584, 2);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(477, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(305, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(141, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "9999";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(251, 8);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 5);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(141, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Controls.Add(this.groupBox2);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(2, 71);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(996, 572);
			this.panel2.TabIndex = 1;
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(3, 318);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(985, 248);
			this.tabControl1.TabIndex = 2;
			this.tabPage1.Controls.Add(this.textBox_code);
			this.tabPage1.Controls.Add(this.label5);
			this.tabPage1.Controls.Add(this.pictureBox1);
			this.tabPage1.Controls.Add(this.label4);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(977, 218);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Tip";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(277, 146);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(694, 66);
			this.textBox_code.TabIndex = 25;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(215, 149);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(44, 17);
			this.label5.TabIndex = 24;
			this.label5.Text = "代码：";
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.kuka_help;
			this.pictureBox1.Location = new System.Drawing.Point(9, 50);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(187, 168);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox1.TabIndex = 23;
			this.pictureBox1.TabStop = false;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 13);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(276, 34);
			this.label4.TabIndex = 22;
			this.label4.Text = "本界面根据网友提供的协议开发完成\r\n如果你有什么问题，可以加下面的kuka专家咨询：";
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button5);
			this.groupBox2.Controls.Add(this.button4);
			this.groupBox2.Controls.Add(this.button3);
			this.groupBox2.Controls.Add(this.textBox5);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.button14);
			this.groupBox2.Controls.Add(this.textBox7);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox8);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Location = new System.Drawing.Point(546, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(443, 313);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "单数据写入测试";
			this.button5.Location = new System.Drawing.Point(308, 174);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(82, 28);
			this.button5.TabIndex = 21;
			this.button5.Text = "停止";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(308, 140);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(82, 28);
			this.button4.TabIndex = 20;
			this.button4.Text = "复位";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(308, 106);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(82, 28);
			this.button3.TabIndex = 19;
			this.button3.Text = "启动";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox5.Location = new System.Drawing.Point(64, 109);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(227, 23);
			this.textBox5.TabIndex = 18;
			this.textBox5.Text = "MAIN";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(10, 112);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 17;
			this.label2.Text = "程序名：";
			this.button14.Location = new System.Drawing.Point(307, 24);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(82, 28);
			this.button14.TabIndex = 16;
			this.button14.Text = "字符串写入";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.textBox7.Location = new System.Drawing.Point(63, 56);
			this.textBox7.Name = "textBox7";
			this.textBox7.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox7.Size = new System.Drawing.Size(326, 23);
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
			this.textBox8.Text = "R1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(9, 30);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "地址：";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(537, 313);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.button_read_string.Location = new System.Drawing.Point(426, 24);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(82, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "字符串读取";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(button_read_string_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(63, 56);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(467, 251);
			this.textBox4.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(9, 58);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.textBox3.Location = new System.Drawing.Point(63, 27);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(344, 23);
			this.textBox3.TabIndex = 3;
			this.textBox3.Text = "$MODE_OP";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 30);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "地址：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "KUKA TCP";
			this.userControlHead1.Size = new System.Drawing.Size(1003, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1003, 646);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormKukaTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Kuka机器人访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
