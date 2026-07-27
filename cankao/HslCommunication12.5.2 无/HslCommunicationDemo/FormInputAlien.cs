using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;
using HslCommunication;

namespace HslCommunicationDemo
{
	public class FormInputAlien : HslFormContent
	{
		private IContainer components = null;

		private Label label1;

		private Label label2;

		private Label label3;

		private TextBox textBox_ip;

		private TextBox textBox2;

		private TextBox textBox3;

		private Button button1;

		private TextBox textBox4;

		private Label label4;

		private Panel panel1;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private Panel panel2;

		private TextBox textBox1;

		private Label label5;

		private CheckBox checkBox_ack_result;

		private Panel panel3;

		private RadioButton radioButton_input_ascii;

		private RadioButton radioButton_input_hex;

		public bool UseHslDtuServer { get; set; } = true;

		public string IpAddress { get; set; }

		public int Port { get; set; }

		public bool NeedAckDtuResult { get; set; }

		public string DTU { get; set; }

		public string Pwd { get; set; }

		public byte[] CustomizeDTU { get; set; }

		public FormInputAlien()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			UseHslDtuServer = radioButton1.Checked;
			if (!IPAddress.TryParse(textBox_ip.Text, out var address))
			{
				DemoUtils.ShowMessage("IP地址填写失败");
				return;
			}
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("端口号填写失败！");
				return;
			}
			IpAddress = address.ToString();
			Port = result;
			if (UseHslDtuServer)
			{
				DTU = textBox3.Text;
				Pwd = textBox4.Text;
				NeedAckDtuResult = checkBox_ack_result.Checked;
			}
			else if (!string.IsNullOrEmpty(textBox1.Text))
			{
				if (radioButton_input_hex.Checked)
				{
					CustomizeDTU = textBox1.Text.ToHexBytes();
				}
				else if (radioButton_input_ascii.Checked)
				{
					CustomizeDTU = Encoding.ASCII.GetBytes(textBox1.Text);
				}
			}
			base.DialogResult = DialogResult.OK;
		}

		private void FormInputAlien_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			checkBox_ack_result.Checked = true;
			if (Program.Language == 2)
			{
				label1.Text = "Ip:";
				label2.Text = "Port:";
				label3.Text = "ID:";
				label4.Text = "Pwd:";
				radioButton1.Text = "Use HSL-DTU package";
				radioButton2.Text = "Customize package";
				checkBox_ack_result.Text = "Need ack DTU result";
				label5.Text = "DTU-Msg:";
				button1.Text = "OK";
			}
			panel2.Visible = false;
		}

		private void radioButton1_CheckedChanged(object sender, EventArgs e)
		{
			panel2.Visible = false;
		}

		private void radioButton2_CheckedChanged(object sender, EventArgs e)
		{
			panel2.Visible = true;
			panel2.BringToFront();
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.FormInputAlien));
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.checkBox_ack_result = new System.Windows.Forms.CheckBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_input_hex = new System.Windows.Forms.RadioButton();
			this.radioButton_input_ascii = new System.Windows.Forms.RadioButton();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(15, 47);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip 地址：";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(211, 47);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 1;
			this.label2.Text = "端口号：";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(16, 80);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(57, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "唯一ID：";
			this.textBox_ip.Location = new System.Drawing.Point(87, 44);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(120, 23);
			this.textBox_ip.TabIndex = 3;
			this.textBox_ip.Text = "127.0.0.1";
			this.textBox2.Location = new System.Drawing.Point(269, 44);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(72, 23);
			this.textBox2.TabIndex = 4;
			this.textBox2.Text = "10010";
			this.textBox3.Location = new System.Drawing.Point(87, 77);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(254, 23);
			this.textBox3.TabIndex = 5;
			this.textBox3.Text = "12345678901";
			this.button1.Location = new System.Drawing.Point(127, 184);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 6;
			this.button1.Text = "确认";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox4.Location = new System.Drawing.Point(87, 106);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(254, 23);
			this.textBox4.TabIndex = 8;
			this.textBox4.Text = "123456";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(16, 109);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 7;
			this.label4.Text = "密码：";
			this.panel1.Controls.Add(this.radioButton2);
			this.panel1.Controls.Add(this.radioButton1);
			this.panel1.Location = new System.Drawing.Point(7, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(334, 37);
			this.panel1.TabIndex = 9;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(177, 8);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(134, 21);
			this.radioButton2.TabIndex = 1;
			this.radioButton2.Text = "自定义报文的注册包";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton2.CheckedChanged += new System.EventHandler(radioButton2_CheckedChanged);
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(12, 8);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(144, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "使用HSL格式的注册包";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.radioButton1.CheckedChanged += new System.EventHandler(radioButton1_CheckedChanged);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.textBox1);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Location = new System.Drawing.Point(7, 75);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(339, 103);
			this.panel2.TabIndex = 10;
			this.textBox1.Location = new System.Drawing.Point(80, 4);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(254, 96);
			this.textBox1.TabIndex = 5;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(10, 5);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(56, 17);
			this.label5.TabIndex = 2;
			this.label5.Text = "注册包：";
			this.checkBox_ack_result.AutoSize = true;
			this.checkBox_ack_result.Location = new System.Drawing.Point(19, 138);
			this.checkBox_ack_result.Name = "checkBox_ack_result";
			this.checkBox_ack_result.Size = new System.Drawing.Size(99, 21);
			this.checkBox_ack_result.TabIndex = 11;
			this.checkBox_ack_result.Text = "要求返回结果";
			this.checkBox_ack_result.UseVisualStyleBackColor = true;
			this.panel3.Controls.Add(this.radioButton_input_ascii);
			this.panel3.Controls.Add(this.radioButton_input_hex);
			this.panel3.Location = new System.Drawing.Point(3, 31);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(74, 68);
			this.panel3.TabIndex = 6;
			this.radioButton_input_hex.AutoSize = true;
			this.radioButton_input_hex.Checked = true;
			this.radioButton_input_hex.Location = new System.Drawing.Point(9, 21);
			this.radioButton_input_hex.Name = "radioButton_input_hex";
			this.radioButton_input_hex.Size = new System.Drawing.Size(50, 21);
			this.radioButton_input_hex.TabIndex = 0;
			this.radioButton_input_hex.Text = "HEX";
			this.radioButton_input_hex.UseVisualStyleBackColor = true;
			this.radioButton_input_ascii.AutoSize = true;
			this.radioButton_input_ascii.Location = new System.Drawing.Point(9, 44);
			this.radioButton_input_ascii.Name = "radioButton_input_ascii";
			this.radioButton_input_ascii.Size = new System.Drawing.Size(57, 21);
			this.radioButton_input_ascii.TabIndex = 1;
			this.radioButton_input_ascii.Text = "ASCII";
			this.radioButton_input_ascii.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(361, 222);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.textBox_ip);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.checkBox_ack_result);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormInputAlien";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormInputAlien";
			base.Load += new System.EventHandler(FormInputAlien_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
