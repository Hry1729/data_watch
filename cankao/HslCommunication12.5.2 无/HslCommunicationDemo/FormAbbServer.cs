using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.LogNet;
using HslCommunication.Robot.ABB;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormAbbServer : HslFormContent
	{
		private ABBWebApiServer httpServer;

		private bool showLog = true;

		private IContainer components = null;

		private Panel panel1;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private Panel panel2;

		private TextBox textBox4;

		private Label label9;

		private UserControlHead userControlHead1;

		private Button button4;

		private CheckBox checkBox1;

		private Label label1;

		private ComboBox comboBox1;

		private TextBox textBox1;

		private Label label4;

		private TextBox textBox3;

		private Label label2;

		private LinkLabel linkLabel3;

		private LinkLabel linkLabel2;

		private LinkLabel linkLabel1;

		public FormAbbServer()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox1.DataSource = new string[5] { "text/html", "text/plain", "text/xml", "application/xml", "application/json" };
			Language(Program.Language);
			button4.Enabled = false;
		}

		private void Language(int language)
		{
			if (language != 1)
			{
			}
		}

		private void FormAbbServer_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button4_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				httpServer = new ABBWebApiServer();
				httpServer.SetLoginAccount(textBox3.Text, textBox1.Text);
				httpServer.Start(int.Parse(textBox2.Text));
				httpServer.LogNet = new LogNetSingle("");
				httpServer.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				httpServer.IsCrossDomain = checkBox1.Checked;
				panel2.Enabled = true;
				button1.Enabled = false;
				button4.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Started Failed:" + ex.Message);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				if (showLog)
				{
					textBox4.AppendText(e.HslMessage.ToString() + Environment.NewLine);
				}
			});
		}

		private void button4_Click(object sender, EventArgs e)
		{
			httpServer?.Close();
			panel2.Enabled = false;
			button1.Enabled = true;
			button4.Enabled = false;
		}

		private void textBox4_TextChanged(object sender, EventArgs e)
		{
		}

		private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			textBox4.Clear();
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			showLog = false;
		}

		private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			showLog = true;
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCrossDomain, checkBox1.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlContentType, comboBox1.SelectedIndex);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox1.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
			checkBox1.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlCrossDomain).Value);
			comboBox1.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlContentType).Value);
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.button4 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.linkLabel3 = new System.Windows.Forms.LinkLabel();
			this.linkLabel2 = new System.Windows.Forms.LinkLabel();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.button4);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(4, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 79);
			this.panel1.TabIndex = 7;
			this.textBox1.Location = new System.Drawing.Point(256, 42);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(116, 23);
			this.textBox1.TabIndex = 23;
			this.textBox1.Text = "robotics";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(189, 45);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(65, 17);
			this.label4.TabIndex = 22;
			this.label4.Text = "password";
			this.textBox3.Location = new System.Drawing.Point(55, 42);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(128, 23);
			this.textBox3.TabIndex = 21;
			this.textBox3.Text = "Default User";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 45);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(43, 17);
			this.label2.TabIndex = 20;
			this.label2.Text = "Name";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(333, 11);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(80, 17);
			this.label1.TabIndex = 19;
			this.label1.Text = "返回内容方式";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(419, 8);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(121, 25);
			this.comboBox1.TabIndex = 18;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(173, 10);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(123, 21);
			this.checkBox1.TabIndex = 17;
			this.checkBox1.Text = "是否支持跨域请求";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.button4.Location = new System.Drawing.Point(717, 5);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 16;
			this.button4.Text = "关闭服务";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button1.Location = new System.Drawing.Point(620, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(62, 8);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(79, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "80";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 11);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.linkLabel3);
			this.panel2.Controls.Add(this.linkLabel2);
			this.panel2.Controls.Add(this.linkLabel1);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Location = new System.Drawing.Point(4, 120);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 523);
			this.panel2.TabIndex = 13;
			this.linkLabel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel3.AutoSize = true;
			this.linkLabel3.Location = new System.Drawing.Point(934, 11);
			this.linkLabel3.Name = "linkLabel3";
			this.linkLabel3.Size = new System.Drawing.Size(32, 17);
			this.linkLabel3.TabIndex = 14;
			this.linkLabel3.TabStop = true;
			this.linkLabel3.Text = "清空";
			this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel3_LinkClicked);
			this.linkLabel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel2.AutoSize = true;
			this.linkLabel2.Location = new System.Drawing.Point(880, 11);
			this.linkLabel2.Name = "linkLabel2";
			this.linkLabel2.Size = new System.Drawing.Size(32, 17);
			this.linkLabel2.TabIndex = 13;
			this.linkLabel2.TabStop = true;
			this.linkLabel2.Text = "继续";
			this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel2_LinkClicked);
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(829, 11);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(32, 17);
			this.linkLabel1.TabIndex = 12;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "停止";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 31);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(925, 487);
			this.textBox4.TabIndex = 8;
			this.textBox4.TextChanged += new System.EventHandler(textBox4_TextChanged);
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(9, 31);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "日志：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.hslcommunication.cn/";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "html";
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
			base.Name = "FormAbbServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "ABB机器人的虚拟服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormAbbServer_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
