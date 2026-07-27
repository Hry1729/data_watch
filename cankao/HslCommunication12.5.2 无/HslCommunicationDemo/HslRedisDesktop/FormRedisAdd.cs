using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo;
using HslCommunicationDemo.Properties;

namespace HslRedisDesktop
{
	public class FormRedisAdd : Form
	{
		private RedisSettings settings = null;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox3;

		private Label label3;

		private Button button1;

		private Button button2;

		private TextBox textBox4;

		private Label label4;

		private CheckBox checkBox1;

		public RedisSettings Settings => settings;

		public FormRedisAdd(RedisSettings redisSettings)
		{
			InitializeComponent();
			settings = redisSettings ?? new RedisSettings
			{
				IpAddress = "127.0.0.1",
				Port = 6379,
				Password = string.Empty
			};
			if (redisSettings != null)
			{
				textBox4.ReadOnly = true;
			}
		}

		private void FormRedisAdd_Load(object sender, EventArgs e)
		{
			base.Icon = Icon.FromHandle(Resources.action_add_16xLG.GetHicon());
			textBox4.Text = settings.Name;
			textBox1.Text = settings.IpAddress;
			textBox2.Text = settings.Port.ToString();
			textBox3.Text = settings.Password;
			checkBox1.CheckedChanged += CheckBox1_CheckedChanged;
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox1.Checked)
			{
				textBox3.PasswordChar = '\0';
			}
			else
			{
				textBox3.PasswordChar = '*';
			}
		}

		private OperateResult CheckInput()
		{
			if (string.IsNullOrEmpty(textBox4.Text))
			{
				return new OperateResult("Server name can not be empty!");
			}
			if (!IPAddress.TryParse(textBox1.Text, out var _))
			{
				return new OperateResult("Ip address input failed!");
			}
			if (!int.TryParse(textBox2.Text, out var _))
			{
				return new OperateResult("port input failed!");
			}
			return OperateResult.CreateSuccessResult();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = CheckInput();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Input failed!" + operateResult.Message);
				return;
			}
			RedisClient redisClient = new RedisClient(textBox1.Text, int.Parse(textBox2.Text), textBox3.Text);
			OperateResult operateResult2 = redisClient.ConnectServer();
			if (operateResult2.IsSuccess)
			{
				DemoUtils.ShowMessage("Connect Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Connect Failed: " + operateResult2.Message);
			}
			redisClient.ConnectClose();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = CheckInput();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Input failed!" + operateResult.Message);
				return;
			}
			settings.Name = textBox4.Text;
			settings.IpAddress = textBox1.Text;
			settings.Port = int.Parse(textBox2.Text);
			settings.Password = textBox3.Text;
			base.DialogResult = DialogResult.OK;
		}

		private void FormRedisAdd_Shown(object sender, EventArgs e)
		{
			textBox4.Focus();
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
			this.button2 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(32, 54);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(75, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip Address:";
			this.textBox1.Location = new System.Drawing.Point(122, 51);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(246, 23);
			this.textBox1.TabIndex = 1;
			this.textBox2.Location = new System.Drawing.Point(122, 86);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(246, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "6379";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(32, 89);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Port:";
			this.textBox3.Location = new System.Drawing.Point(122, 121);
			this.textBox3.Name = "textBox3";
			this.textBox3.PasswordChar = '*';
			this.textBox3.Size = new System.Drawing.Size(182, 23);
			this.textBox3.TabIndex = 5;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(32, 124);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(67, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "Password:";
			this.button1.Location = new System.Drawing.Point(69, 161);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(118, 30);
			this.button1.TabIndex = 6;
			this.button1.Text = "Connection Test";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.button2.Location = new System.Drawing.Point(206, 161);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(118, 30);
			this.button2.TabIndex = 7;
			this.button2.Text = "Finish";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.textBox4.Location = new System.Drawing.Point(122, 15);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(246, 23);
			this.textBox4.TabIndex = 9;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(32, 18);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(46, 17);
			this.label4.TabIndex = 8;
			this.label4.Text = "Name:";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(310, 123);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(58, 21);
			this.checkBox1.TabIndex = 10;
			this.checkBox1.Text = "Show";
			this.checkBox1.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.ClientSize = new System.Drawing.Size(398, 211);
			base.Controls.Add(this.checkBox1);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(414, 250);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(414, 250);
			base.Name = "FormRedisAdd";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Add or edit a redis client";
			base.Load += new System.EventHandler(FormRedisAdd_Load);
			base.Shown += new System.EventHandler(FormRedisAdd_Shown);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
