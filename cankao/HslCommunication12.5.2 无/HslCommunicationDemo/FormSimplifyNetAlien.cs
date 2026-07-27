using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Enthernet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormSimplifyNetAlien : HslFormContent
	{
		private NetSimplifyClient simplifyClient = new NetSimplifyClient();

		private NetworkAlienClient networkAlien = null;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private TextBox textBox7;

		private Label label11;

		private TextBox textBox6;

		private Label label10;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private Label label8;

		private TextBox textBox5;

		private Label label7;

		private Label label22;

		private TextBox textBox1;

		private Label label1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private UserControlHead userControlHead1;

		public FormSimplifyNetAlien()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			NetHandle netHandle = default(NetHandle);
			if (textBox5.Text.IndexOf('.') >= 0)
			{
				string[] array = textBox5.Text.Split('.');
				netHandle = new NetHandle(byte.Parse(array[0]), byte.Parse(array[1]), ushort.Parse(array[2]));
			}
			else
			{
				netHandle = int.Parse(textBox5.Text);
			}
			int num = int.Parse(textBox6.Text);
			DateTime now = DateTime.Now;
			for (int i = 0; i < num; i++)
			{
				OperateResult<string> operateResult = simplifyClient.ReadFromServer(netHandle, textBox4.Text);
				if (operateResult.IsSuccess)
				{
					textBox8.AppendText(operateResult.Content + Environment.NewLine);
				}
				else
				{
					DemoUtils.ShowMessage("读取失败：" + operateResult.ToMessageShowString());
				}
			}
			textBox7.Text = (DateTime.Now - now).TotalMilliseconds.ToString("F2");
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void button1_Click_1(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("端口输入不正确！");
				return;
			}
			simplifyClient = new NetSimplifyClient(textBox1.Text, result);
			try
			{
				simplifyClient.ConnectionId = textBox1.Text;
				NetworkAlienStart(result);
				simplifyClient.ConnectServer(null);
				DemoUtils.ShowMessage("等待服务器的连接！");
				button1.Enabled = false;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void NetworkAlienStart(int port)
		{
			networkAlien = new NetworkAlienClient();
			networkAlien.OnClientConnected += NetworkAlien_OnClientConnected;
			networkAlien.ServerStart(port);
		}

		private void NetworkAlien_OnClientConnected(PipeDtuNet session)
		{
			if (session.DTU == simplifyClient.ConnectionId)
			{
				Invoke((Action)delegate
				{
					panel2.Enabled = true;
					button2.Enabled = true;
				});
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
			this.label22 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 61);
			this.panel1.TabIndex = 7;
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(478, 19);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(97, 17);
			this.label22.TabIndex = 22;
			this.label22.Text = "(11位ASCII字符)";
			this.textBox1.Location = new System.Drawing.Point(299, 16);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(171, 23);
			this.textBox1.TabIndex = 21;
			this.textBox1.Text = "12345678901";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(238, 19);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(62, 17);
			this.label1.TabIndex = 20;
			this.label1.Text = "DTU ID：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(873, 13);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 16;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button1.Location = new System.Drawing.Point(770, 13);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 15;
			this.button1.Text = "创建";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click_1);
			this.textBox2.Location = new System.Drawing.Point(72, 16);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(76, 23);
			this.textBox2.TabIndex = 14;
			this.textBox2.Text = "10000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(18, 19);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 13;
			this.label3.Text = "端口号：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(2, 102);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 540);
			this.panel2.TabIndex = 13;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(927, 316);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(8, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "接收：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(898, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox7.Location = new System.Drawing.Point(456, 183);
			this.textBox7.Name = "textBox7";
			this.textBox7.ReadOnly = true;
			this.textBox7.Size = new System.Drawing.Size(141, 23);
			this.textBox7.TabIndex = 16;
			this.textBox7.Text = "0";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(402, 186);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 15;
			this.label11.Text = "耗时：";
			this.textBox6.Location = new System.Drawing.Point(241, 183);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(141, 23);
			this.textBox6.TabIndex = 14;
			this.textBox6.Text = "1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(187, 186);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 13;
			this.label10.Text = "次数：";
			this.button3.Location = new System.Drawing.Point(62, 180);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "发送";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(927, 138);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "数据：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(249, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(134, 17);
			this.label8.TabIndex = 10;
			this.label8.Text = "举例：12345 或是1.1.1";
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(181, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "1";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(56, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "指令头：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7697782.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
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
			base.Name = "FormSimplifyNetAlien";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Simplify网络客户端";
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
