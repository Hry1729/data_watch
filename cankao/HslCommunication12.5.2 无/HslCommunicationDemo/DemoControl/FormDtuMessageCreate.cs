using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication;

namespace HslCommunicationDemo.DemoControl
{
	public class FormDtuMessageCreate : Form
	{
		private IContainer components = null;

		private Label label1;

		private TextBox textBox_id;

		private Label label2;

		private Label label3;

		private TextBox textBox_password;

		private Label label4;

		private TextBox textBox_ip_address;

		private Label label5;

		private TextBox textBox_ip_port;

		private Label label6;

		private Button button1;

		private TextBox textBox5;

		private CheckBox checkBox1;

		private CheckBox checkBox2;

		private Label label7;

		private Panel panel1;

		private RadioButton radioButton1;

		private RadioButton radioButton2;

		public FormDtuMessageCreate()
		{
			InitializeComponent();
		}

		public void SetDtuId(string id, string pwd)
		{
			textBox_id.Text = id;
			textBox_password.Text = pwd;
		}

		private void FormDtuMessageCreate_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "DTU Message Create";
				label1.Text = "Unique ID:";
				label2.Text = "(Up to 11 chars)";
				label3.Text = "Pwd:";
				label4.Text = "(6 chars)";
				button1.Text = "Calculate DTU package";
				checkBox1.Text = "Return the DTU result";
				checkBox2.Text = "Length use ASCII";
				label7.Text = "Format:";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_id.Text))
			{
				DemoUtils.ShowMessage("Unique ID can't be null");
				return;
			}
			if (!Regex.IsMatch(textBox_id.Text, "^[0-9]{1,11}$"))
			{
				DemoUtils.ShowMessage("Unique IDs can only be between 1 and 11 characters");
				return;
			}
			if (!Regex.IsMatch(textBox_password.Text, "^[\\S]{6}$"))
			{
				DemoUtils.ShowMessage("The password needs 6 characters");
				return;
			}
			byte[] array = new byte[30];
			array[0] = 72;
			array[1] = 83;
			array[2] = 76;
			if (checkBox2.Checked)
			{
				array[3] = 49;
				array[4] = 57;
			}
			else
			{
				array[4] = 25;
			}
			Encoding.ASCII.GetBytes(textBox_id.Text).CopyTo(array, 5);
			Encoding.ASCII.GetBytes(textBox_password.Text).CopyTo(array, 16);
			if (!string.IsNullOrEmpty(textBox_ip_address.Text))
			{
				try
				{
					IPAddress.Parse(textBox_ip_address.Text).GetAddressBytes().CopyTo(array, 22);
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Ip Address input wrong: " + ex.Message);
					return;
				}
			}
			if (!string.IsNullOrEmpty(textBox_ip_port.Text))
			{
				if (!ushort.TryParse(textBox_ip_port.Text, out var result))
				{
					DemoUtils.ShowMessage("Port input wrong ");
					return;
				}
				array[26] = BitConverter.GetBytes(result)[0];
				array[27] = BitConverter.GetBytes(result)[1];
			}
			if (!checkBox1.Checked)
			{
				array[28] = 1;
			}
			if (radioButton1.Checked)
			{
				textBox5.Text = array.ToHexString();
				return;
			}
			array[3] = 49;
			array[4] = 57;
			for (int i = 0; i < 11; i++)
			{
				if (array[i + 5] == 0)
				{
					array[i + 5] = 32;
				}
			}
			for (int j = 22; j < 28; j++)
			{
				array[j] = 48;
			}
			if (array[28] == 0)
			{
				array[28] = 48;
			}
			else
			{
				array[28] = 49;
			}
			array[29] = 48;
			textBox5.Text = Encoding.ASCII.GetString(array);
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.DemoControl.FormDtuMessageCreate));
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_id = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_password = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_ip_address = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_ip_port = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.label7 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(18, 19);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(81, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "唯一ID标识：";
			this.textBox_id.Location = new System.Drawing.Point(105, 16);
			this.textBox_id.Name = "textBox_id";
			this.textBox_id.Size = new System.Drawing.Size(201, 23);
			this.textBox_id.TabIndex = 1;
			this.textBox_id.Text = "10000";
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Gray;
			this.label2.Location = new System.Drawing.Point(312, 19);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(90, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "(最多11个字符)";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(439, 19);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 3;
			this.label3.Text = "密码：";
			this.textBox_password.Location = new System.Drawing.Point(495, 16);
			this.textBox_password.Name = "textBox_password";
			this.textBox_password.Size = new System.Drawing.Size(98, 23);
			this.textBox_password.TabIndex = 4;
			this.textBox_password.Text = "000000";
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.Color.Gray;
			this.label4.Location = new System.Drawing.Point(599, 19);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(59, 17);
			this.label4.TabIndex = 5;
			this.label4.Text = "(6个字符)";
			this.textBox_ip_address.Location = new System.Drawing.Point(106, 53);
			this.textBox_ip_address.Name = "textBox_ip_address";
			this.textBox_ip_address.Size = new System.Drawing.Size(167, 23);
			this.textBox_ip_address.TabIndex = 7;
			this.textBox_ip_address.Text = "192.168.0.1";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(19, 56);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(74, 17);
			this.label5.TabIndex = 6;
			this.label5.Text = "IP Address:";
			this.textBox_ip_port.Location = new System.Drawing.Point(381, 53);
			this.textBox_ip_port.Name = "textBox_ip_port";
			this.textBox_ip_port.Size = new System.Drawing.Size(81, 23);
			this.textBox_ip_port.TabIndex = 9;
			this.textBox_ip_port.Text = "10010";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(294, 56);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(50, 17);
			this.label6.TabIndex = 8;
			this.label6.Text = "IP Port:";
			this.button1.Location = new System.Drawing.Point(245, 103);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(176, 37);
			this.button1.TabIndex = 10;
			this.button1.Text = "计算二进制注册包";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox5.Location = new System.Drawing.Point(12, 154);
			this.textBox5.Multiline = true;
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(670, 89);
			this.textBox5.TabIndex = 11;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(493, 55);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(99, 21);
			this.checkBox1.TabIndex = 12;
			this.checkBox1.Text = "返回注册结果";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(22, 86);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(106, 21);
			this.checkBox2.TabIndex = 13;
			this.checkBox2.Text = "长度使用ASCII";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(12, 247);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 14;
			this.label7.Text = "格式选择：";
			this.panel1.Controls.Add(this.radioButton2);
			this.panel1.Controls.Add(this.radioButton1);
			this.panel1.Location = new System.Drawing.Point(89, 242);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(146, 32);
			this.panel1.TabIndex = 15;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(73, 4);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(57, 21);
			this.radioButton2.TabIndex = 1;
			this.radioButton2.Text = "ASCII";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(3, 4);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(48, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "Hex";
			this.radioButton1.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(694, 267);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.checkBox2);
			base.Controls.Add(this.checkBox1);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox_ip_port);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.textBox_ip_address);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox_password);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_id);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Margin = new System.Windows.Forms.Padding(4);
			this.MaximumSize = new System.Drawing.Size(710, 306);
			this.MinimumSize = new System.Drawing.Size(710, 306);
			base.Name = "FormDtuMessageCreate";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "DTU消息创建界面";
			base.Load += new System.EventHandler(FormDtuMessageCreate_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
