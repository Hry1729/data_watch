using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Enthernet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormPushNet : HslFormContent
	{
		private NetPushClient pushClient;

		private int receiveCount = 0;

		private IContainer components = null;

		private Panel panel1;

		private Label label21;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private TextBox textBox3;

		private Label label6;

		private TextBox textBox15;

		private Panel panel2;

		private TextBox textBox4;

		private Label label9;

		private Label label10;

		private Label label8;

		private Label label7;

		private UserControlHead userControlHead1;

		public FormPushNet()
		{
			InitializeComponent();
		}

		private void FormPushNet_Load(object sender, EventArgs e)
		{
			textBox15.Text = Guid.Empty.ToString();
			panel2.Enabled = false;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("端口号输入错误");
				return;
			}
			if (string.IsNullOrEmpty(textBox3.Text))
			{
				DemoUtils.ShowMessage("关键字不能为空");
				return;
			}
			pushClient?.ClosePush();
			pushClient = new NetPushClient(textBox1.Text, result, textBox3.Text);
			OperateResult operateResult = pushClient.CreatePush(PushFromServer);
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				DemoUtils.ShowMessage("成功");
				panel2.Enabled = true;
			}
			else
			{
				DemoUtils.ShowMessage("失败：" + operateResult.Message);
			}
		}

		private void PushFromServer(NetPushClient pushClient, string data)
		{
			if (base.IsHandleCreated)
			{
				Invoke((Action<string>)delegate(string m)
				{
					label8.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
					receiveCount++;
					label9.Text = receiveCount.ToString();
					textBox4.Text = m;
				}, data);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			pushClient?.ClosePush();
			panel2.Enabled = false;
			button1.Enabled = true;
			button2.Enabled = false;
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlToken, textBox15.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTopic, textBox3.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlToken).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlTopic).Value;
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
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox15);
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
			this.panel1.Size = new System.Drawing.Size(997, 54);
			this.panel1.TabIndex = 1;
			this.textBox3.Location = new System.Drawing.Point(673, 14);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(76, 23);
			this.textBox3.TabIndex = 9;
			this.textBox3.Text = "A";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(619, 17);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(56, 17);
			this.label6.TabIndex = 8;
			this.label6.Text = "关键字：";
			this.textBox15.Location = new System.Drawing.Point(386, 14);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(227, 23);
			this.textBox15.TabIndex = 7;
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(332, 17);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "令牌：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(875, 11);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(772, 11);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(250, 14);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(76, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "12345";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(196, 17);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 14);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(128, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 17);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(3, 93);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 550);
			this.panel2.TabIndex = 12;
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(11, 69);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(977, 470);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(82, 38);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(15, 17);
			this.label9.TabIndex = 6;
			this.label9.Text = "0";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(8, 38);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(68, 17);
			this.label10.TabIndex = 5;
			this.label10.Text = "接收次数：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(82, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(15, 17);
			this.label8.TabIndex = 4;
			this.label8.Text = "0";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 3;
			this.label7.Text = "接收时间：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8992315.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Hsl - push net";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 13;
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
			base.Name = "FormPushNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "数据推送客户端";
			base.Load += new System.EventHandler(FormPushNet_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
