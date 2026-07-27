using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Profinet.Keyence;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormKeyenceSR2000 : HslFormContent
	{
		private KeyenceSR2000SeriesTcp keyence = null;

		private IContainer components = null;

		private Panel panel1;

		private Label label1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private GroupBox groupBox1;

		private Button button_read_string;

		private TextBox textBox4;

		private Label label7;

		private TextBox textBox3;

		private Label label6;

		private Label label22;

		private Label label21;

		private UserControlHead userControlHead1;

		private Button button3;

		private Button button4;

		private Button button5;

		private Button button6;

		private Button button9;

		private Button button8;

		private Button button7;

		private Button button12;

		private Button button11;

		private Button button10;

		private Label label2;

		private TextBox textBox_code;

		public FormKeyenceSR2000()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			panel2.Enabled = false;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Keyence SR2000 Code Reader Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label21.Text = "Address:";
				label6.Text = "address:";
				label7.Text = "result:";
				button_read_string.Text = "r-string";
				groupBox1.Text = "Single Data Read test";
				label22.Text = "";
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
			keyence?.ConnectClose();
			keyence = new KeyenceSR2000SeriesTcp(textBox_ip.Text, port);
			keyence.LogNet = base.LogNet;
			try
			{
				OperateResult connect = await keyence.ConnectServerAsync();
				if (connect.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.ToMessageShowString());
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
			keyence.ConnectClose();
		}

		private void button_read_string_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadCustomer(textBox3.Text), textBox3.Text, textBox4);
			textBox_code.Text = "OperateResult<string> read = keyence.ReadCustomer( \"" + textBox3.Text + "\" );   // 自定义命令";
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadBarcode(), "ReadBarcode", textBox4);
			textBox_code.Text = "OperateResult<string> read = keyence.ReadBarcode( );   // 读取条码信息";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(keyence.Reset());
			textBox_code.Text = "OperateResult result = keyence.Reset( );  // 复位命令";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(keyence.OpenIndicator());
			textBox_code.Text = "OperateResult result = keyence.OpenIndicator( );  // 打开指示灯";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(keyence.CloseIndicator());
			textBox_code.Text = "OperateResult result = keyence.CloseIndicator( );  // 关闭指示灯";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadVersion(), "ReadVersion", textBox4);
			textBox_code.Text = "OperateResult<string> read = keyence.ReadVersion( );   // 读取版本号";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadCommandState(), "ReadCommandState", textBox4);
			textBox_code.Text = "OperateResult<string> read = keyence.ReadCommandState( );   // 读取命令状态，none:不处理；wait：等待设置反映；update：正在更新";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadErrorState(), "ReadErrorState", textBox4);
			textBox_code.Text = "OperateResult<string> read = keyence.ReadErrorState( );   // 读取错误状态";
		}

		private void button10_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(keyence.ReadRecord(), "ReadRecord", textBox4);
			textBox_code.Text = "OperateResult<int[]> read = keyence.ReadRecord( );   // 读取扫码器的扫码记录，返回数组数据，分别是成功次数，失败次数，ERROR次数，稳定次数，时机输入次数";
		}

		private void button11_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(keyence.Lock());
			textBox_code.Text = "OperateResult result = keyence.Lock( );  // 锁定扫码设备";
		}

		private void button12_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(keyence.UnLock());
			textBox_code.Text = "OperateResult result = keyence.UnLock( );  // 解除锁定扫码设备";
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
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button12 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button_read_string = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
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
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 46);
			this.panel1.TabIndex = 0;
			this.label22.Location = new System.Drawing.Point(776, 1);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(197, 45);
			this.label22.TabIndex = 7;
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(702, 1);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(68, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "地址示例：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(584, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(477, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(305, 8);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(141, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "7000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(251, 11);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(62, 8);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(141, 23);
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
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(3, 84);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 561);
			this.panel2.TabIndex = 1;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.textBox_code);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.button12);
			this.groupBox1.Controls.Add(this.button11);
			this.groupBox1.Controls.Add(this.button10);
			this.groupBox1.Controls.Add(this.button9);
			this.groupBox1.Controls.Add(this.button8);
			this.groupBox1.Controls.Add(this.button7);
			this.groupBox1.Controls.Add(this.button6);
			this.groupBox1.Controls.Add(this.button5);
			this.groupBox1.Controls.Add(this.button4);
			this.groupBox1.Controls.Add(this.button3);
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(989, 553);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.button12.Location = new System.Drawing.Point(867, 22);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(89, 28);
			this.button12.TabIndex = 26;
			this.button12.Text = "解锁";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button11.Location = new System.Drawing.Point(772, 22);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(89, 28);
			this.button11.TabIndex = 25;
			this.button11.Text = "锁定";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(677, 22);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(89, 28);
			this.button10.TabIndex = 24;
			this.button10.Text = "扫码记录";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Location = new System.Drawing.Point(582, 22);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(89, 28);
			this.button9.TabIndex = 23;
			this.button9.Text = "错误状态";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Location = new System.Drawing.Point(487, 22);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(89, 28);
			this.button8.TabIndex = 22;
			this.button8.Text = "命令状态";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Location = new System.Drawing.Point(392, 22);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(89, 28);
			this.button7.TabIndex = 21;
			this.button7.Text = "版本";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(297, 22);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(89, 28);
			this.button6.TabIndex = 20;
			this.button6.Text = "关闭指示灯";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(202, 22);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(89, 28);
			this.button5.TabIndex = 19;
			this.button5.Text = "打开指示灯";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(107, 22);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(89, 28);
			this.button4.TabIndex = 18;
			this.button4.Text = "复位";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(12, 22);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(89, 28);
			this.button3.TabIndex = 17;
			this.button3.Text = "读条码";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button_read_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_string.Location = new System.Drawing.Point(867, 96);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(116, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "自定义命令";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(button_read_string_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(63, 128);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(920, 355);
			this.textBox4.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(9, 130);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(63, 99);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(798, 23);
			this.textBox3.TabIndex = 3;
			this.textBox3.Text = "KEYENCE";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 102);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "命令：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "SR 2000";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 487);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(51, 17);
			this.label2.TabIndex = 27;
			this.label2.Text = "Code：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(63, 487);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(920, 60);
			this.textBox_code.TabIndex = 28;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormKeyenceSR2000";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "基恩士SR2000扫码Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
