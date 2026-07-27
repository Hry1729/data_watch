using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Profinet.AllenBradley;

namespace HslCommunicationDemo.HslDebug
{
	public class FormHandShake : Form
	{
		private List<byte[]> handShake = null;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private Panel panel3;

		private RadioButton radioButton_hex;

		private RadioButton radioButton_binary;

		private Label label6;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox3;

		private Label label3;

		private Label label4;

		private Button button1;

		private Button button2;

		private Button button3;

		private Button button_clear;

		private Button button_ok;

		private Button button_cancel;

		public List<byte[]> HandShake => handShake;

		public FormHandShake(List<byte[]> handShake)
		{
			InitializeComponent();
			this.handShake = handShake;
		}

		private void button_clear_Click(object sender, EventArgs e)
		{
			textBox1.Text = string.Empty;
			textBox2.Text = string.Empty;
			textBox3.Text = string.Empty;
		}

		private void button_ok_Click(object sender, EventArgs e)
		{
			List<byte[]> list = new List<byte[]>();
			if (!string.IsNullOrEmpty(textBox1.Text))
			{
				list.Add(radioButton_binary.Checked ? textBox1.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox1.Text));
			}
			if (!string.IsNullOrEmpty(textBox2.Text))
			{
				list.Add(radioButton_binary.Checked ? textBox2.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox2.Text));
			}
			if (!string.IsNullOrEmpty(textBox3.Text))
			{
				list.Add(radioButton_binary.Checked ? textBox3.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox3.Text));
			}
			handShake = list;
			base.DialogResult = DialogResult.OK;
		}

		private void FormHandShake_Load(object sender, EventArgs e)
		{
			if (handShake != null)
			{
				if (handShake.Count > 0)
				{
					textBox1.Text = (radioButton_binary.Checked ? handShake[0].ToHexString(' ') : SoftBasic.GetAsciiStringRender(handShake[0]));
				}
				if (handShake.Count > 1)
				{
					textBox2.Text = (radioButton_binary.Checked ? handShake[1].ToHexString(' ') : SoftBasic.GetAsciiStringRender(handShake[1]));
				}
				if (handShake.Count > 2)
				{
					textBox3.Text = (radioButton_binary.Checked ? handShake[2].ToHexString(' ') : SoftBasic.GetAsciiStringRender(handShake[2]));
				}
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			textBox1.Text = new byte[22]
			{
				3, 0, 0, 22, 17, 224, 0, 0, 0, 1,
				0, 192, 1, 10, 193, 2, 1, 2, 194, 2,
				1, 0
			}.ToHexString(' ');
			textBox2.Text = new byte[25]
			{
				3, 0, 0, 25, 2, 240, 128, 50, 1, 0,
				0, 4, 0, 0, 8, 0, 0, 240, 0, 0,
				1, 0, 1, 1, 224
			}.ToHexString(' ');
			textBox3.Text = string.Empty;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			textBox1.Text = new byte[20]
			{
				70, 73, 78, 83, 0, 0, 0, 12, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			}.ToHexString(' ');
			textBox2.Text = string.Empty;
			textBox3.Text = string.Empty;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			textBox1.Text = AllenBradleyHelper.RegisterSessionHandle().ToHexString(' ');
			textBox2.Text = string.Empty;
			textBox3.Text = string.Empty;
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_hex = new System.Windows.Forms.RadioButton();
			this.radioButton_binary = new System.Windows.Forms.RadioButton();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button_clear = new System.Windows.Forms.Button();
			this.button_ok = new System.Windows.Forms.Button();
			this.button_cancel = new System.Windows.Forms.Button();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 39);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(63, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "握手报文1";
			this.textBox1.Location = new System.Drawing.Point(96, 36);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(609, 23);
			this.textBox1.TabIndex = 1;
			this.panel3.BackColor = System.Drawing.SystemColors.Control;
			this.panel3.Controls.Add(this.radioButton_hex);
			this.panel3.Controls.Add(this.radioButton_binary);
			this.panel3.Location = new System.Drawing.Point(96, 4);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(155, 28);
			this.panel3.TabIndex = 32;
			this.radioButton_hex.AutoSize = true;
			this.radioButton_hex.Location = new System.Drawing.Point(86, 3);
			this.radioButton_hex.Name = "radioButton_hex";
			this.radioButton_hex.Size = new System.Drawing.Size(57, 21);
			this.radioButton_hex.TabIndex = 1;
			this.radioButton_hex.Text = "ASCII";
			this.radioButton_hex.UseVisualStyleBackColor = true;
			this.radioButton_binary.AutoSize = true;
			this.radioButton_binary.Checked = true;
			this.radioButton_binary.Location = new System.Drawing.Point(7, 3);
			this.radioButton_binary.Name = "radioButton_binary";
			this.radioButton_binary.Size = new System.Drawing.Size(62, 21);
			this.radioButton_binary.TabIndex = 0;
			this.radioButton_binary.TabStop = true;
			this.radioButton_binary.Text = "Binary";
			this.radioButton_binary.UseVisualStyleBackColor = true;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(12, 9);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 31;
			this.label6.Text = "数据格式：";
			this.textBox2.Location = new System.Drawing.Point(96, 69);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(609, 23);
			this.textBox2.TabIndex = 34;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(12, 72);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(63, 17);
			this.label2.TabIndex = 33;
			this.label2.Text = "握手报文2";
			this.textBox3.Location = new System.Drawing.Point(96, 104);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(609, 23);
			this.textBox3.TabIndex = 36;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(12, 107);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(63, 17);
			this.label3.TabIndex = 35;
			this.label3.Text = "握手报文3";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(12, 141);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(59, 17);
			this.label4.TabIndex = 37;
			this.label4.Text = "常见握手:";
			this.button1.Location = new System.Drawing.Point(93, 135);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(86, 27);
			this.button1.TabIndex = 38;
			this.button1.Text = "SiemensS7";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.button2.Location = new System.Drawing.Point(185, 135);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(86, 27);
			this.button2.TabIndex = 39;
			this.button2.Text = "FinsTcp";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button3.Location = new System.Drawing.Point(277, 135);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(86, 27);
			this.button3.TabIndex = 40;
			this.button3.Text = "cip";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button_clear.Location = new System.Drawing.Point(619, 4);
			this.button_clear.Name = "button_clear";
			this.button_clear.Size = new System.Drawing.Size(86, 27);
			this.button_clear.TabIndex = 41;
			this.button_clear.Text = "Clear";
			this.button_clear.UseVisualStyleBackColor = true;
			this.button_clear.Click += new System.EventHandler(button_clear_Click);
			this.button_ok.Location = new System.Drawing.Point(277, 273);
			this.button_ok.Name = "button_ok";
			this.button_ok.Size = new System.Drawing.Size(138, 41);
			this.button_ok.TabIndex = 42;
			this.button_ok.Text = "OK";
			this.button_ok.UseVisualStyleBackColor = true;
			this.button_ok.Click += new System.EventHandler(button_ok_Click);
			this.button_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.button_cancel.Location = new System.Drawing.Point(-421, 285);
			this.button_cancel.Name = "button_cancel";
			this.button_cancel.Size = new System.Drawing.Size(138, 41);
			this.button_cancel.TabIndex = 43;
			this.button_cancel.Text = "CANCEL";
			this.button_cancel.UseVisualStyleBackColor = true;
			base.AcceptButton = this.button_ok;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.CancelButton = this.button_cancel;
			base.ClientSize = new System.Drawing.Size(717, 338);
			base.Controls.Add(this.button_cancel);
			base.Controls.Add(this.button_ok);
			base.Controls.Add(this.button_clear);
			base.Controls.Add(this.button3);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.panel3);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormHandShake";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "HandShake";
			base.Load += new System.EventHandler(FormHandShake_Load);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
