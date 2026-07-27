using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.BasicFramework;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormMail : HslFormContent
	{
		private IContainer components = null;

		private Panel panel2;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private TextBox textBox5;

		private Label label7;

		private Label label1;

		private TextBox textBox1;

		private Panel panel1;

		private Label label3;

		private TextBox textBox2;

		private Button button1;

		private TextBox textBox3;

		private Label label6;

		private TextBox textBox6;

		private Label label8;

		private UserControlHead userControlHead1;

		public FormMail()
		{
			InitializeComponent();
		}

		private void FormMail_Load(object sender, EventArgs e)
		{
			textBox3.Text = "<html><body style=\"background-color:PowderBlue;\"><h1>Look! Styles and colors</h1><p style=\"font-family:verdana;color:red\">This text is in Verdana and red</p><p style=\"font-family:times;color:green\">This text is in Times and green</p><p style=\"font-size:30px\">This text is 30 pixels high</p></body></html> ";
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "邮件发送测试";
				label7.Text = "发送地址：";
				label9.Text = "主题：";
				label1.Text = "内容：(字符串)";
				textBox1.Text = "测试主题";
				textBox4.Text = "测试内容";
				button3.Text = "发送";
				label8.Text = "发送地址：";
				label6.Text = "主题：";
				label3.Text = "内容：(html)";
				textBox2.Text = "测试主题";
				button1.Text = "发送";
			}
			else
			{
				Text = "Mail Send Test";
				label7.Text = "Address:";
				label9.Text = "Subject:";
				label1.Text = "String Content:";
				textBox1.Text = "Test Subject";
				textBox4.Text = "Test Content";
				button3.Text = "Send";
				label8.Text = "Address:";
				label6.Text = "Subject:";
				label3.Text = "Html Content:";
				textBox2.Text = "Test Subject";
				button1.Text = "Send";
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			try
			{
				SoftMail.MailSystem163.SendMail(textBox5.Text, textBox1.Text, textBox4.Text);
				DemoUtils.ShowMessage("发送成功！");
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				SoftMail.MailSystem163.SendMail(textBox6.Text, textBox2.Text, textBox3.Text, true);
				DemoUtils.ShowMessage("发送成功！");
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
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
			this.panel2 = new System.Windows.Forms.Panel();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2.SuspendLayout();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.textBox1);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(13, 51);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(977, 279);
			this.panel2.TabIndex = 27;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 71);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(88, 17);
			this.label1.TabIndex = 14;
			this.label1.Text = "内容：(字符串)";
			this.textBox1.Location = new System.Drawing.Point(102, 38);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(181, 23);
			this.textBox1.TabIndex = 13;
			this.textBox1.Text = "测试主题";
			this.button3.Location = new System.Drawing.Point(102, 238);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "发送";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Location = new System.Drawing.Point(102, 68);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(852, 164);
			this.textBox4.TabIndex = 8;
			this.textBox4.Text = "测试内容";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 41);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "主题：";
			this.textBox5.Location = new System.Drawing.Point(102, 8);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(181, 23);
			this.textBox5.TabIndex = 9;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "发送地址：";
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox6);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Location = new System.Drawing.Point(13, 336);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(977, 297);
			this.panel1.TabIndex = 28;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 71);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(77, 17);
			this.label3.TabIndex = 14;
			this.label3.Text = "内容：(html)";
			this.textBox2.Location = new System.Drawing.Point(102, 38);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(181, 23);
			this.textBox2.TabIndex = 13;
			this.textBox2.Text = "测试主题";
			this.button1.Location = new System.Drawing.Point(102, 238);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 12;
			this.button1.Text = "发送";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox3.Location = new System.Drawing.Point(102, 68);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(852, 164);
			this.textBox3.TabIndex = 8;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 41);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 11;
			this.label6.Text = "主题：";
			this.textBox6.Location = new System.Drawing.Point(102, 8);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(181, 23);
			this.textBox6.TabIndex = 9;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(8, 11);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(68, 17);
			this.label8.TabIndex = 7;
			this.label8.Text = "发送地址：";
			this.userControlHead1.BackColor = System.Drawing.Color.MediumPurple;
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8463613.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "smtp";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 29;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.panel2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMail";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Mail Send Test";
			base.Load += new System.EventHandler(FormMail_Load);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
