using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunicationDemo;
using HslCommunicationDemo.Properties;

namespace HslRedisDesktop
{
	public class StringValueEditForm : Form
	{
		private RedisSettings redisSettings = null;

		private string key = string.Empty;

		private string value = string.Empty;

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

		public StringValueEditForm(RedisSettings settings, string key, string value)
		{
			InitializeComponent();
			redisSettings = settings;
			this.key = key;
			this.value = value;
		}

		private void StringValueEditForm_Load(object sender, EventArgs e)
		{
			textBox1.Text = $"{redisSettings.Name}[{redisSettings.IpAddress}:{redisSettings.Port}]";
			textBox2.Text = key;
			textBox3.Text = value;
			textBox4.Text = redisSettings.DBBlock.ToString();
			base.Icon = Icon.FromHandle(Resources.Enum_582.GetHicon());
		}

		private void button1_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = redisSettings.Redis.WriteKey(key, textBox3.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("数据写入成功！请注意，如果本来键值有TTL信息，写入之后TTL将会被清除！");
			}
			else
			{
				DemoUtils.ShowMessage("数据写入失败！" + operateResult.Message);
			}
		}

		private void StringValueEditForm_Shown(object sender, EventArgs e)
		{
			textBox3.Focus();
		}

		private void StringValueEditForm_SizeChanged(object sender, EventArgs e)
		{
			button1.Location = new Point(base.Width / 2 - button1.Width / 2, base.Height - 100);
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
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
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
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox3.Location = new System.Drawing.Point(87, 97);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox3.Size = new System.Drawing.Size(531, 236);
			this.textBox3.TabIndex = 5;
			this.label3.AutoSize = true;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.Location = new System.Drawing.Point(12, 100);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(64, 20);
			this.label3.TabIndex = 4;
			this.label3.Text = "Value：";
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button1.Location = new System.Drawing.Point(253, 346);
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
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Font = new System.Drawing.Font("微软雅黑", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox4.Location = new System.Drawing.Point(500, 13);
			this.textBox4.Name = "textBox4";
			this.textBox4.ReadOnly = true;
			this.textBox4.Size = new System.Drawing.Size(118, 27);
			this.textBox4.TabIndex = 8;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.ClientSize = new System.Drawing.Size(652, 407);
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
			base.Name = "StringValueEditForm";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "StringValueEditForm";
			base.Load += new System.EventHandler(StringValueEditForm_Load);
			base.Shown += new System.EventHandler(StringValueEditForm_Shown);
			base.SizeChanged += new System.EventHandler(StringValueEditForm_SizeChanged);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
