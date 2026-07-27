using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunicationDemo;
using HslCommunicationDemo.Properties;

namespace HslRedisDesktop
{
	public class ZSetValueInsertForm : Form
	{
		private RedisSettings redisSettings = null;

		private string key = string.Empty;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox3;

		private Label label3;

		private Button button1;

		private Label label4;

		private TextBox textBox4;

		private TextBox textBox5;

		private Label label5;

		public ZSetValueInsertForm(RedisSettings settings, string key)
		{
			InitializeComponent();
			redisSettings = settings;
			this.key = key;
		}

		private void StringValueEditForm_Load(object sender, EventArgs e)
		{
			textBox1.Text = $"{redisSettings.Name}[{redisSettings.IpAddress}:{redisSettings.Port}]";
			textBox2.Text = key;
			textBox4.Text = redisSettings.DBBlock.ToString();
			textBox5.Text = "0";
			base.Icon = Icon.FromHandle(Resources.zset.GetHicon());
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!double.TryParse(textBox5.Text, out var result))
			{
				DemoUtils.ShowMessage("得分的值不能转化为数值，请重新输入！");
				return;
			}
			OperateResult operateResult = redisSettings.Redis.ZSetAdd(key, textBox3.Text, result);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("有序集合元素写入成功！");
			}
			else
			{
				DemoUtils.ShowMessage("有序集合元素写入失败！" + operateResult.Message);
			}
		}

		private void StringValueEditForm_Shown(object sender, EventArgs e)
		{
			textBox3.Focus();
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
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(12, 16);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(69, 20);
			this.label1.TabIndex = 0;
			this.label1.Text = "服务器：";
			this.textBox1.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(87, 13);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(332, 27);
			this.textBox1.TabIndex = 1;
			this.textBox2.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox2.Location = new System.Drawing.Point(87, 56);
			this.textBox2.Name = "textBox2";
			this.textBox2.ReadOnly = true;
			this.textBox2.Size = new System.Drawing.Size(531, 27);
			this.textBox2.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.Location = new System.Drawing.Point(12, 59);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(51, 20);
			this.label2.TabIndex = 2;
			this.label2.Text = "Key：";
			this.textBox3.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox3.Location = new System.Drawing.Point(87, 132);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox3.Size = new System.Drawing.Size(531, 201);
			this.textBox3.TabIndex = 5;
			this.label3.AutoSize = true;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.Location = new System.Drawing.Point(12, 132);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(87, 20);
			this.label3.TabIndex = 4;
			this.label3.Text = "Member：";
			this.button1.Location = new System.Drawing.Point(256, 346);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(163, 47);
			this.button1.TabIndex = 6;
			this.button1.Text = "确认";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label4.AutoSize = true;
			this.label4.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label4.Location = new System.Drawing.Point(434, 16);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 20);
			this.label4.TabIndex = 7;
			this.label4.Text = "DB：";
			this.textBox4.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox4.Location = new System.Drawing.Point(500, 13);
			this.textBox4.Name = "textBox4";
			this.textBox4.ReadOnly = true;
			this.textBox4.Size = new System.Drawing.Size(118, 27);
			this.textBox4.TabIndex = 8;
			this.textBox5.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox5.Location = new System.Drawing.Point(87, 93);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(531, 27);
			this.textBox5.TabIndex = 12;
			this.label5.AutoSize = true;
			this.label5.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label5.Location = new System.Drawing.Point(12, 96);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(66, 20);
			this.label5.TabIndex = 11;
			this.label5.Text = "Score：";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.ClientSize = new System.Drawing.Size(652, 407);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "ZSetValueInsertForm";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "ZSetValueInsertForm";
			base.Load += new System.EventHandler(StringValueEditForm_Load);
			base.Shown += new System.EventHandler(StringValueEditForm_Shown);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
