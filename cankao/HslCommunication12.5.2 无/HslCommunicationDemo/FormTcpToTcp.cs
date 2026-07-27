using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Enthernet;
using HslCommunication.LogNet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormTcpToTcp : HslFormContent
	{
		private TcpForward tcpForward;

		private Timer timer;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private Panel panel2;

		private TextBox textBox6;

		private Label label7;

		private UserControlHead userControlHead1;

		private TextBox textBox1;

		private Label label2;

		private CheckBox checkBox1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox3;

		private Label label1;

		private ListBox listBox1;

		private TextBox textBox_cache_size;

		private Label label4;

		public FormTcpToTcp()
		{
			InitializeComponent();
		}

		private void FormSerialDebug_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			timer = new Timer();
			timer.Interval = 1000;
			timer.Tick += Timer_Tick;
			timer.Start();
			checkBox1.CheckedChanged += CheckBox1_CheckedChanged;
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (tcpForward != null)
			{
				tcpForward.LogMsgFormatBinary = checkBox1.Checked;
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (tcpForward != null)
			{
				listBox1.DataSource = tcpForward.GetPipeSessions();
			}
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Tcp2TCP";
				label7.Text = "数据接收区：";
				checkBox1.Text = "使用二进制显示";
				button1.Text = "打开转换";
				button2.Text = "关闭转换";
				label4.Text = "缓冲区:";
				label1.Text = "远程IP:";
				label3.Text = "远程端口:";
				label2.Text = "本地端口:";
			}
			else
			{
				Text = "Tcp2TCP";
				label7.Text = "Data receiving Area:";
				checkBox1.Text = "use binary format to show";
				button1.Text = "Open tcp to tcp";
				button2.Text = "Close forward";
				label4.Text = "Cache Size:";
				label1.Text = "Remote IP:";
				label3.Text = "Remote Port:";
				label2.Text = "Local Port:";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_cache_size.Text, out var result))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "缓存大小输入错误！" : "Cache size input error");
				return;
			}
			if (!int.TryParse(textBox2.Text, out var result2))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "端口号输入错误！" : "IpAddress port input error");
				return;
			}
			try
			{
				tcpForward = new TcpForward(int.Parse(textBox1.Text), textBox3.Text, result2);
				tcpForward.LogMsgFormatBinary = checkBox1.Checked;
				tcpForward.LogNet = new LogNetSingle("");
				tcpForward.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				tcpForward.CacheSize = result;
				tcpForward.ServerStart();
				button1.Enabled = false;
				button2.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + Environment.NewLine + ex.Message);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<object, HslEventArgs>(LogNet_BeforeSaveToFile), sender, e);
			}
			else
			{
				textBox6.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				tcpForward.ServerClose();
				button2.Enabled = false;
				button1.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void FormTcpToTcp_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue("LocalPort", textBox1.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox3.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox1.Text = element.Attribute("LocalPort").Value;
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
			this.textBox_cache_size = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_cache_size);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.listBox1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(4, 37);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(995, 67);
			this.panel1.TabIndex = 7;
			this.textBox_cache_size.Location = new System.Drawing.Point(260, 35);
			this.textBox_cache_size.Name = "textBox_cache_size";
			this.textBox_cache_size.Size = new System.Drawing.Size(56, 23);
			this.textBox_cache_size.TabIndex = 34;
			this.textBox_cache_size.Text = "2048";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(176, 38);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(82, 17);
			this.label4.TabIndex = 33;
			this.label4.Text = "Cache Size：";
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.BackColor = System.Drawing.Color.LightGray;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(568, 5);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(422, 55);
			this.listBox1.TabIndex = 32;
			this.textBox2.Location = new System.Drawing.Point(493, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(65, 23);
			this.textBox2.TabIndex = 23;
			this.textBox2.Text = "1000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(394, 8);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(93, 17);
			this.label3.TabIndex = 22;
			this.label3.Text = "Remote Port：";
			this.textBox3.Location = new System.Drawing.Point(82, 5);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(295, 23);
			this.textBox3.TabIndex = 21;
			this.textBox3.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(80, 17);
			this.label1.TabIndex = 20;
			this.label1.Text = "Remote IP：";
			this.textBox1.Location = new System.Drawing.Point(81, 35);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(90, 23);
			this.textBox1.TabIndex = 19;
			this.textBox1.Text = "2000";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(3, 38);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(78, 17);
			this.label2.TabIndex = 18;
			this.label2.Text = "Local Port：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(453, 32);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(108, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭转换";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(322, 32);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(125, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开转换";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.checkBox1);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(4, 107);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(995, 533);
			this.panel2.TabIndex = 13;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(152, 3);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(135, 21);
			this.checkBox1.TabIndex = 22;
			this.checkBox1.Text = "是否使用二进制显示";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox6.Location = new System.Drawing.Point(6, 30);
			this.textBox6.Multiline = true;
			this.textBox6.Name = "textBox6";
			this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox6.Size = new System.Drawing.Size(984, 495);
			this.textBox6.TabIndex = 21;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(3, 4);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(80, 17);
			this.label7.TabIndex = 18;
			this.label7.Text = "数据接收区：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "TCP - TCP";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
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
			base.Name = "FormTcpToTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Tcp2TCP";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormTcpToTcp_FormClosing);
			base.Load += new System.EventHandler(FormSerialDebug_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
