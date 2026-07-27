using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;

namespace HslCommunicationDemo.Redis
{
	public class FormRedisInput : Form
	{
		private RedisClient redis;

		private IContainer components = null;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private Button button1;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private TextBox textBox5;

		private Label label5;

		private Button button2;

		private TextBox textBox3;

		private Label label3;

		private TextBox textBox4;

		private Label label4;

		private TabPage tabPage3;

		private Button button3;

		private TextBox textBox6;

		private Label label6;

		private TextBox textBox7;

		private Label label7;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		public FormRedisInput(RedisClient redis)
		{
			InitializeComponent();
			this.redis = redis;
		}

		private void FormRedisInput_Load(object sender, EventArgs e)
		{
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox1.Text))
			{
				DemoUtils.ShowMessage("Key Can not be null");
				return;
			}
			OperateResult operateResult = redis.WriteKey(textBox1.Text, textBox2.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write sucess");
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + operateResult.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox4.Text) || string.IsNullOrEmpty(textBox5.Text))
			{
				DemoUtils.ShowMessage("Key Can not be null");
				return;
			}
			OperateResult operateResult = redis.WriteHashKey(textBox4.Text, textBox5.Text, textBox3.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write sucess");
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + operateResult.Message);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox6.Text) || string.IsNullOrEmpty(textBox7.Text))
			{
				DemoUtils.ShowMessage("Key Can not be null");
				return;
			}
			OperateResult operateResult = null;
			operateResult = ((!radioButton2.Checked) ? redis.ListLeftPush(textBox7.Text, textBox6.Text) : redis.ListRightPush(textBox7.Text, textBox6.Text));
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write sucess");
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + operateResult.Message);
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
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			base.SuspendLayout();
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(411, 231);
			this.tabControl1.TabIndex = 0;
			this.tabPage1.Controls.Add(this.button1);
			this.tabPage1.Controls.Add(this.textBox2);
			this.tabPage1.Controls.Add(this.label2);
			this.tabPage1.Controls.Add(this.textBox1);
			this.tabPage1.Controls.Add(this.label1);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPage1.Size = new System.Drawing.Size(403, 201);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "键值写入";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.button1.Location = new System.Drawing.Point(125, 153);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(140, 31);
			this.button1.TabIndex = 4;
			this.button1.Text = "Write";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(98, 63);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox2.Size = new System.Drawing.Size(249, 77);
			this.textBox2.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(33, 66);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(40, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Value";
			this.textBox1.Location = new System.Drawing.Point(98, 24);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(249, 23);
			this.textBox1.TabIndex = 1;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(33, 27);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(32, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Key:";
			this.tabPage2.Controls.Add(this.textBox5);
			this.tabPage2.Controls.Add(this.label5);
			this.tabPage2.Controls.Add(this.button2);
			this.tabPage2.Controls.Add(this.textBox3);
			this.tabPage2.Controls.Add(this.label3);
			this.tabPage2.Controls.Add(this.textBox4);
			this.tabPage2.Controls.Add(this.label4);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPage2.Size = new System.Drawing.Size(403, 201);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "哈希写入";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.textBox5.Location = new System.Drawing.Point(102, 48);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(249, 23);
			this.textBox5.TabIndex = 11;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(37, 51);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(38, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "Field:";
			this.button2.Location = new System.Drawing.Point(129, 159);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(140, 31);
			this.button2.TabIndex = 9;
			this.button2.Text = "Write";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.textBox3.Location = new System.Drawing.Point(102, 80);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox3.Size = new System.Drawing.Size(249, 66);
			this.textBox3.TabIndex = 8;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(37, 80);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(40, 17);
			this.label3.TabIndex = 7;
			this.label3.Text = "Value";
			this.textBox4.Location = new System.Drawing.Point(102, 14);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(249, 23);
			this.textBox4.TabIndex = 6;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(37, 17);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(32, 17);
			this.label4.TabIndex = 5;
			this.label4.Text = "Key:";
			this.tabPage3.Controls.Add(this.button3);
			this.tabPage3.Controls.Add(this.textBox6);
			this.tabPage3.Controls.Add(this.label6);
			this.tabPage3.Controls.Add(this.textBox7);
			this.tabPage3.Controls.Add(this.label7);
			this.tabPage3.Controls.Add(this.radioButton2);
			this.tabPage3.Controls.Add(this.radioButton1);
			this.tabPage3.Location = new System.Drawing.Point(4, 26);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(403, 201);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "列表写入";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(68, 6);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(78, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.Text = "Left Push";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(225, 6);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(87, 21);
			this.radioButton2.TabIndex = 1;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "Right Push";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.button3.Location = new System.Drawing.Point(126, 148);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(140, 31);
			this.button3.TabIndex = 9;
			this.button3.Text = "Write";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox6.Location = new System.Drawing.Point(99, 76);
			this.textBox6.Multiline = true;
			this.textBox6.Name = "textBox6";
			this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox6.Size = new System.Drawing.Size(249, 61);
			this.textBox6.TabIndex = 8;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(34, 79);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(40, 17);
			this.label6.TabIndex = 7;
			this.label6.Text = "Value";
			this.textBox7.Location = new System.Drawing.Point(99, 37);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(249, 23);
			this.textBox7.TabIndex = 6;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(34, 40);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(32, 17);
			this.label7.TabIndex = 5;
			this.label7.Text = "Key:";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(411, 231);
			base.Controls.Add(this.tabControl1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormRedisInput";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Write Redis";
			base.Load += new System.EventHandler(FormRedisInput_Load);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
