using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.PLC.Common
{
	public class ConnectControl : UserControl
	{
		private IContainer components = null;

		private TextBox textBox_lora_head;

		private Label label4;

		private TextBox textBox_connect_timeout;

		private Label label2;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private ComboBox comboBox_connect_type;

		public ConnectControl()
		{
			InitializeComponent();
			comboBox_connect_type.SelectedIndexChanged += ComboBox_connect_type_SelectedIndexChanged;
			if (Program.Language == 1)
			{
				comboBox_connect_type.DataSource = new string[3] { "直连", "Lora", "MQTT" };
			}
			else
			{
				comboBox_connect_type.DataSource = new string[3] { "Direct", "Lora", "MQTT" };
			}
			DemoUtils.SetDeviveIp(textBox_ip);
		}

		private void ComboBox_connect_type_SelectedIndexChanged(object sender, EventArgs e)
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
			this.textBox_lora_head = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_connect_timeout = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox_connect_type = new System.Windows.Forms.ComboBox();
			base.SuspendLayout();
			this.textBox_lora_head.Location = new System.Drawing.Point(634, 3);
			this.textBox_lora_head.Name = "textBox_lora_head";
			this.textBox_lora_head.Size = new System.Drawing.Size(109, 23);
			this.textBox_lora_head.TabIndex = 38;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(563, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(77, 17);
			this.label4.TabIndex = 37;
			this.label4.Text = "LoraHead：";
			this.textBox_connect_timeout.Location = new System.Drawing.Point(500, 3);
			this.textBox_connect_timeout.Name = "textBox_connect_timeout";
			this.textBox_connect_timeout.Size = new System.Drawing.Size(52, 23);
			this.textBox_connect_timeout.TabIndex = 36;
			this.textBox_connect_timeout.Text = "5000";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(429, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 35;
			this.label2.Text = "连接超时：";
			this.textBox2.Location = new System.Drawing.Point(361, 3);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(58, 23);
			this.textBox2.TabIndex = 34;
			this.textBox2.Text = "502";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(307, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 33;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(126, 3);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(175, 23);
			this.textBox_ip.TabIndex = 32;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(72, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 31;
			this.label1.Text = "Ip地址：";
			this.comboBox_connect_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_connect_type.FormattingEnabled = true;
			this.comboBox_connect_type.Location = new System.Drawing.Point(1, 2);
			this.comboBox_connect_type.Name = "comboBox_connect_type";
			this.comboBox_connect_type.Size = new System.Drawing.Size(65, 25);
			this.comboBox_connect_type.TabIndex = 39;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.comboBox_connect_type);
			base.Controls.Add(this.textBox_lora_head);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox_connect_timeout);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox_ip);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ConnectControl";
			base.Size = new System.Drawing.Size(859, 29);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
