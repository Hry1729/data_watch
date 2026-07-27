using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunication.BasicFramework
{
	/// <summary>
	/// 用来测试版软件授权的窗口
	/// </summary>
	public class FormAuthorize : Form
	{
		private SoftAuthorize softAuthorize = null;

		private Func<string, string> Encrypt = null;

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private IContainer components = null;

		private TextBox textBox2;

		private TextBox textBox1;

		private Label label2;

		private Label label1;

		private Button userButton1;

		private LinkLabel linkLabel1;

		private string AboutCode { get; set; } = "";

		/// <summary>
		/// 实例化授权注册窗口
		/// </summary>
		/// <param name="authorize"></param>
		/// <param name="aboutCode">提示关于怎么获取注册码的信息</param>
		/// <param name="encrypt">加密的方法</param>
		public FormAuthorize(SoftAuthorize authorize, string aboutCode, Func<string, string> encrypt)
		{
			InitializeComponent();
			softAuthorize = authorize;
			AboutCode = aboutCode;
			Encrypt = encrypt;
		}

		private void FormAuthorize_Load(object sender, EventArgs e)
		{
			textBox1.Text = softAuthorize.GetMachineCodeString();
		}

		private void userButton1_Click(object sender, EventArgs e)
		{
			if (softAuthorize.CheckAuthorize(textBox2.Text, Encrypt))
			{
				base.DialogResult = DialogResult.OK;
			}
			else
			{
				MessageBox.Show("注册码不正确");
			}
		}

		private void linkLabel1_Click(object sender, EventArgs e)
		{
			MessageBox.Show(AboutCode);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.userButton1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.textBox2.Font = new System.Drawing.Font("宋体", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox2.Location = new System.Drawing.Point(124, 78);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(292, 30);
			this.textBox2.TabIndex = 7;
			this.textBox1.Font = new System.Drawing.Font("宋体", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(124, 35);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(292, 30);
			this.textBox1.TabIndex = 6;
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.Location = new System.Drawing.Point(12, 77);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(110, 31);
			this.label2.TabIndex = 5;
			this.label2.Text = "注册码：";
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(12, 34);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(110, 31);
			this.label1.TabIndex = 4;
			this.label1.Text = "机器码：";
			this.linkLabel1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.linkLabel1.Location = new System.Drawing.Point(295, 174);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(137, 23);
			this.linkLabel1.TabIndex = 9;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "关于注册码";
			this.linkLabel1.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.linkLabel1.Click += new System.EventHandler(linkLabel1_Click);
			this.userButton1.BackColor = System.Drawing.Color.Transparent;
			this.userButton1.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton1.Location = new System.Drawing.Point(135, 130);
			this.userButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton1.Name = "userButton1";
			this.userButton1.Size = new System.Drawing.Size(166, 33);
			this.userButton1.TabIndex = 8;
			this.userButton1.Text = "注册";
			this.userButton1.Click += new System.EventHandler(userButton1_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(435, 193);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.userButton1);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(451, 232);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(451, 232);
			base.Name = "FormAuthorize";
			base.ShowIcon = false;
			base.ShowInTaskbar = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "注册软件";
			base.TopMost = true;
			base.Load += new System.EventHandler(FormAuthorize_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
