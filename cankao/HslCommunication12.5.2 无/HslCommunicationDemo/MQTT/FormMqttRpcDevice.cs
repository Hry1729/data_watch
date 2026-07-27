using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.MQTT
{
	public class FormMqttRpcDevice : HslFormContent
	{
		private MqttRpcDevice rpc;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private CheckBox checkBox_rsa;

		private TextBox textBox3;

		private Label label6;

		private TextBox textBox10;

		private Label label4;

		private TextBox textBox9;

		private Label label2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private Panel panel2;

		private UserControlHead userControlHead1;

		private TextBox textBox_deviceTopic;

		private Label label5;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		public FormMqttRpcDevice()
		{
			InitializeComponent();
		}

		private void FormMqttRpcDevice_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			panel2.Enabled = false;
			button2.Enabled = false;
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			MqttConnectionOptions options = new MqttConnectionOptions
			{
				IpAddress = textBox_ip.Text,
				Port = int.Parse(textBox2.Text),
				ClientId = textBox3.Text,
				UseRSAProvider = checkBox_rsa.Checked
			};
			if (!string.IsNullOrEmpty(textBox9.Text) || !string.IsNullOrEmpty(textBox10.Text))
			{
				options.Credentials = new MqttCredential(textBox9.Text, textBox10.Text);
			}
			button1.Enabled = false;
			rpc = new MqttRpcDevice(options, textBox_deviceTopic.Text);
			OperateResult connect = await rpc.ConnectServerAsync();
			if (connect.IsSuccess)
			{
				panel2.Enabled = true;
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				userControlReadWriteDevice1.SetReadWriteNet(rpc, "100", true);
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
				codeExampleControl.SetCodeText(rpc, "DeviceTopic");
			}
			else
			{
				button1.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.Message + Environment.NewLine + "Error: " + connect.ErrorCode);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			userControlReadWriteDevice1.SetEnable(false);
			rpc?.ConnectClose();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCompanyID, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
			element.SetAttributeValue("DeviceTopic", textBox_deviceTopic.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlCompanyID).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox10.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
			textBox_deviceTopic.Text = element.Attribute("DeviceTopic").Value;
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormMqttRpcDevice_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
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
			this.textBox_deviceTopic = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.checkBox_rsa = new System.Windows.Forms.CheckBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_deviceTopic);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.checkBox_rsa);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox10);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1001, 61);
			this.panel1.TabIndex = 8;
			this.textBox_deviceTopic.Location = new System.Drawing.Point(575, 32);
			this.textBox_deviceTopic.Name = "textBox_deviceTopic";
			this.textBox_deviceTopic.Size = new System.Drawing.Size(134, 23);
			this.textBox_deviceTopic.TabIndex = 20;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(497, 35);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 19;
			this.label5.Text = "设备主题：";
			this.checkBox_rsa.AutoSize = true;
			this.checkBox_rsa.Location = new System.Drawing.Point(705, 5);
			this.checkBox_rsa.Name = "checkBox_rsa";
			this.checkBox_rsa.Size = new System.Drawing.Size(287, 21);
			this.checkBox_rsa.TabIndex = 18;
			this.checkBox_rsa.Text = "RSA加密 (支持v10.2.0版本及以上的MqttServer)";
			this.checkBox_rsa.UseVisualStyleBackColor = true;
			this.textBox3.Location = new System.Drawing.Point(86, 32);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(405, 23);
			this.textBox3.TabIndex = 17;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 35);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(80, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "客户端标识：";
			this.textBox10.Location = new System.Drawing.Point(584, 3);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(113, 23);
			this.textBox10.TabIndex = 13;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(520, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(417, 3);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(91, 23);
			this.textBox9.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(341, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "用户名：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(823, 29);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(719, 29);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(243, 3);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(79, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(189, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(62, 3);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(115, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 99);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1001, 513);
			this.panel2.TabIndex = 9;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(999, 511);
			this.userControlReadWriteDevice1.TabIndex = 1;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.hsltechnology.cn/";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MRPC";
			this.userControlHead1.Size = new System.Drawing.Size(1008, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 10;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1008, 615);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "FormMqttRpcDevice";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "MqttRpcDevice [基于MRPC的PLC设备访问]";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttRpcDevice_FormClosing);
			base.Load += new System.EventHandler(FormMqttRpcDevice_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
