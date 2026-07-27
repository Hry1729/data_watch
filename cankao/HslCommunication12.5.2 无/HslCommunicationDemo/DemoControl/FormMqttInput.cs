using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.MQTT;

namespace HslCommunicationDemo.DemoControl
{
	public class FormMqttInput : Form
	{
		private IContainer components = null;

		private Label label1;

		private TextBox textBox_ip;

		private TextBox textBox_port;

		private Label label2;

		private TextBox textBox_name;

		private Label label3;

		private TextBox textBox_password;

		private Label label4;

		private TextBox textBox_clientid;

		private Label label5;

		private TextBox textBox_read_topic;

		private Label label6;

		private TextBox textBox_write_topic;

		private Label label7;

		private Button button1;

		public MqttConnectionOptions MqttConnectionOptions { get; set; }

		public string ReadTopic { get; set; }

		public string WriteTopic { get; set; }

		public FormMqttInput()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			MqttConnectionOptions = new MqttConnectionOptions();
			MqttConnectionOptions.IpAddress = textBox_ip.Text;
			MqttConnectionOptions.Port = int.Parse(textBox_port.Text);
			MqttConnectionOptions.ClientId = textBox_clientid.Text;
			if (!string.IsNullOrEmpty(textBox_name.Text))
			{
				MqttConnectionOptions.Credentials = new MqttCredential(textBox_name.Text, textBox_password.Text);
			}
			ReadTopic = textBox_read_topic.Text;
			WriteTopic = textBox_write_topic.Text;
			base.DialogResult = DialogResult.OK;
		}

		private void FormMqttInput_Load(object sender, EventArgs e)
		{
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_name = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_password = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_clientid = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_read_topic = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_write_topic = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(27, 33);
			this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(62, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "MQTT IP:";
			this.textBox_ip.Location = new System.Drawing.Point(111, 30);
			this.textBox_ip.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(306, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.textBox_port.Location = new System.Drawing.Point(111, 64);
			this.textBox_port.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(306, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "1883";
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.Location = new System.Drawing.Point(27, 67);
			this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(83, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "MQTT PORT:";
			this.textBox_name.Location = new System.Drawing.Point(111, 99);
			this.textBox_name.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_name.Name = "textBox_name";
			this.textBox_name.Size = new System.Drawing.Size(306, 23);
			this.textBox_name.TabIndex = 5;
			this.label3.AutoSize = true;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.Location = new System.Drawing.Point(27, 102);
			this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(73, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "UserName:";
			this.textBox_password.Location = new System.Drawing.Point(111, 135);
			this.textBox_password.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_password.Name = "textBox_password";
			this.textBox_password.Size = new System.Drawing.Size(306, 23);
			this.textBox_password.TabIndex = 7;
			this.label4.AutoSize = true;
			this.label4.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label4.Location = new System.Drawing.Point(27, 138);
			this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(67, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "Password:";
			this.textBox_clientid.Location = new System.Drawing.Point(111, 174);
			this.textBox_clientid.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_clientid.Name = "textBox_clientid";
			this.textBox_clientid.Size = new System.Drawing.Size(306, 23);
			this.textBox_clientid.TabIndex = 9;
			this.label5.AutoSize = true;
			this.label5.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label5.Location = new System.Drawing.Point(27, 177);
			this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(60, 17);
			this.label5.TabIndex = 8;
			this.label5.Text = "Client ID:";
			this.textBox_read_topic.Location = new System.Drawing.Point(162, 211);
			this.textBox_read_topic.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_read_topic.Name = "textBox_read_topic";
			this.textBox_read_topic.Size = new System.Drawing.Size(255, 23);
			this.textBox_read_topic.TabIndex = 11;
			this.textBox_read_topic.Text = "DeviceToMqtt";
			this.label6.AutoSize = true;
			this.label6.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label6.Location = new System.Drawing.Point(27, 214);
			this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(127, 17);
			this.label6.TabIndex = 10;
			this.label6.Text = "Device->Mqtt Topic:";
			this.textBox_write_topic.Location = new System.Drawing.Point(162, 243);
			this.textBox_write_topic.Margin = new System.Windows.Forms.Padding(4);
			this.textBox_write_topic.Name = "textBox_write_topic";
			this.textBox_write_topic.Size = new System.Drawing.Size(255, 23);
			this.textBox_write_topic.TabIndex = 13;
			this.textBox_write_topic.Text = "MqttToDevice";
			this.label7.AutoSize = true;
			this.label7.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label7.Location = new System.Drawing.Point(27, 246);
			this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(127, 17);
			this.label7.TabIndex = 12;
			this.label7.Text = "Mqtt->Device Topic:";
			this.button1.Location = new System.Drawing.Point(139, 286);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(153, 38);
			this.button1.TabIndex = 14;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(450, 346);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox_write_topic);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.textBox_read_topic);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.textBox_clientid);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.textBox_password);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox_name);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox_port);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_ip);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "FormMqttInput";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormMqttInput";
			base.Load += new System.EventHandler(FormMqttInput_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
