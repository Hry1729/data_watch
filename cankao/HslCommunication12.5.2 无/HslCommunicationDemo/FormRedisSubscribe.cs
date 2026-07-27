using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormRedisSubscribe : HslFormContent
	{
		private RedisSubscribe redisSubscribe = null;

		private int count = 0;

		private Random random = new Random();

		private IContainer components = null;

		private Panel panel1;

		private TextBox textBox3;

		private Label label6;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private Label label10;

		private Label label11;

		private Label label12;

		private UserControlHead userControlHead1;

		private Button button4;

		private Button button3;

		private TextBox textBox5;

		private Label label7;

		public FormRedisSubscribe()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			if (Program.Language == 2)
			{
				label1.Text = "IP:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "DisConnect";
				label6.Text = "Password";
				label7.Text = "Keyword: all input is batch subscription";
				button3.Text = "Subscribe";
				button4.Text = "UnSubscribe";
				label12.Text = "Time:";
				label10.Text = "Count:";
				label9.Text = "Data:";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				redisSubscribe = new RedisSubscribe(textBox1.Text, int.Parse(textBox2.Text));
				redisSubscribe.Password = textBox3.Text;
				redisSubscribe.OnRedisMessageReceived += RedisSubscribe_OnRedisMessageReceived;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("输入错误：" + ex.Message);
				return;
			}
			OperateResult operateResult = redisSubscribe.ConnectServer();
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
			}
		}

		private void RedisSubscribe_OnRedisMessageReceived(string topic, string message)
		{
			ShowSubscribe(topic, message);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			redisSubscribe.ConnectClose();
		}

		private void ShowSubscribe(string key, string content)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<string, string>(ShowSubscribe), key, content);
				return;
			}
			count++;
			label11.Text = DateTime.Now.ToString();
			label8.Text = count.ToString();
			textBox4.AppendText(DateTime.Now.ToString() + "  Topic:[" + key + "] Message:" + content);
			textBox4.AppendText(Environment.NewLine);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (redisSubscribe.SubscribeMessage(textBox5.Text).IsSuccess)
			{
				DemoUtils.ShowMessage("订阅成功");
			}
			else
			{
				DemoUtils.ShowMessage("订阅失败");
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			if (redisSubscribe.UnSubscribeMessage(textBox5.Text).IsSuccess)
			{
				DemoUtils.ShowMessage("取消订阅成功");
			}
			else
			{
				DemoUtils.ShowMessage("取消订阅失败");
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
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(15, 43);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(978, 83);
			this.panel1.TabIndex = 7;
			this.textBox3.Location = new System.Drawing.Point(78, 47);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(368, 23);
			this.textBox3.TabIndex = 7;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 50);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 6;
			this.label6.Text = "密码：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(584, 11);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(475, 11);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(305, 14);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(141, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "6379";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(251, 17);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(78, 14);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(125, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 17);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Location = new System.Drawing.Point(15, 133);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(977, 508);
			this.panel2.TabIndex = 13;
			this.button4.Location = new System.Drawing.Point(383, 31);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(103, 25);
			this.button4.TabIndex = 20;
			this.button4.Text = "取消订阅";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(274, 31);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(103, 25);
			this.button3.TabIndex = 19;
			this.button3.Text = "订阅";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox5.Location = new System.Drawing.Point(62, 32);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(206, 23);
			this.textBox5.TabIndex = 18;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 8);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(164, 17);
			this.label7.TabIndex = 17;
			this.label7.Text = "关键字：都输入就是批量订阅";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(441, 75);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(15, 17);
			this.label8.TabIndex = 15;
			this.label8.Text = "0";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(367, 75);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(68, 17);
			this.label10.TabIndex = 14;
			this.label10.Text = "接收次数：";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(133, 75);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(15, 17);
			this.label11.TabIndex = 13;
			this.label11.Text = "0";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(59, 75);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(68, 17);
			this.label12.TabIndex = 12;
			this.label12.Text = "接收时间：";
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 95);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(904, 407);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 101);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "数据：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/9998013.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Redis";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormRedisSubscribe";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Redis订阅客户端";
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
