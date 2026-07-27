using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.BasicFramework;
using HslControls;

namespace HslCommunicationDemo
{
	public class FormSeqCreate : HslFormContent
	{
		private SoftNumericalOrder softNumericalOrder;

		private IContainer components = null;

		private Label label1;

		private HslButton userButton3;

		private HslButton userButton2;

		private TextBox textBox1;

		private HslButton userButton1;

		private LinkLabel linkLabel1;

		private Label label2;

		public FormSeqCreate()
		{
			InitializeComponent();
		}

		private void FormSeqCreate_Load(object sender, EventArgs e)
		{
			label1.BackColor = FormLoad.ThemeColor;
			softNumericalOrder = new SoftNumericalOrder("ABC", "yyyyMMdd", 7, Application.StartupPath + "\\numericalOrder.txt");
			if (!Program.ShowAuthorInfomation)
			{
				label2.Visible = false;
				linkLabel1.Visible = false;
			}
		}

		private void userButton1_Click(object sender, EventArgs e)
		{
			string numericalOrder = softNumericalOrder.GetNumericalOrder();
			textBox1.AppendText(numericalOrder + Environment.NewLine);
		}

		private void userButton3_Click(object sender, EventArgs e)
		{
			string numericalOrder = softNumericalOrder.GetNumericalOrder("XYZ");
			textBox1.AppendText(numericalOrder + Environment.NewLine);
		}

		private void userButton2_Click(object sender, EventArgs e)
		{
			DateTime now = DateTime.Now;
			for (int i = 0; i < 1000000; i++)
			{
				string numericalOrder = softNumericalOrder.GetNumericalOrder("XYZ");
			}
			double totalMilliseconds = (DateTime.Now - now).TotalMilliseconds;
			textBox1.AppendText("耗时：" + totalMilliseconds + Environment.NewLine);
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			try
			{
				Process.Start(linkLabel1.Text);
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
			this.label1 = new System.Windows.Forms.Label();
			this.userButton3 = new HslControls.HslButton();
			this.userButton2 = new HslControls.HslButton();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.userButton1 = new HslControls.HslButton();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label2 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label1.BackColor = System.Drawing.Color.LightSkyBlue;
			this.label1.Font = new System.Drawing.Font("楷体", 24f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.ForeColor = System.Drawing.Color.Black;
			this.label1.Location = new System.Drawing.Point(-9, -2);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(1052, 76);
			this.label1.TabIndex = 1;
			this.label1.Text = "流水号生成器测试";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.userButton3.BackColor = System.Drawing.Color.Transparent;
			this.userButton3.CustomerInformation = "";
			this.userButton3.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton3.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton3.Location = new System.Drawing.Point(12, 137);
			this.userButton3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton3.Name = "userButton3";
			this.userButton3.Size = new System.Drawing.Size(136, 42);
			this.userButton3.TabIndex = 8;
			this.userButton3.Text = "获取流水号";
			this.userButton3.Click += new System.EventHandler(userButton3_Click);
			this.userButton2.BackColor = System.Drawing.Color.Transparent;
			this.userButton2.CustomerInformation = "";
			this.userButton2.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton2.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton2.Location = new System.Drawing.Point(12, 187);
			this.userButton2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton2.Name = "userButton2";
			this.userButton2.Size = new System.Drawing.Size(136, 42);
			this.userButton2.TabIndex = 7;
			this.userButton2.Text = "百万流水号性能测试";
			this.userButton2.Click += new System.EventHandler(userButton2_Click);
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Font = new System.Drawing.Font("宋体", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(154, 87);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(848, 557);
			this.textBox1.TabIndex = 6;
			this.userButton1.BackColor = System.Drawing.Color.Transparent;
			this.userButton1.CustomerInformation = "";
			this.userButton1.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton1.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton1.Location = new System.Drawing.Point(12, 87);
			this.userButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton1.Name = "userButton1";
			this.userButton1.Size = new System.Drawing.Size(136, 42);
			this.userButton1.TabIndex = 5;
			this.userButton1.Text = "获取流水号";
			this.userButton1.Click += new System.EventHandler(userButton1_Click);
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.BackColor = System.Drawing.Color.LightSkyBlue;
			this.linkLabel1.Location = new System.Drawing.Point(66, 9);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(287, 17);
			this.linkLabel1.TabIndex = 9;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "http://www.cnblogs.com/dathlin/p/7811489.html";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.label2.AutoSize = true;
			this.label2.BackColor = System.Drawing.Color.LightSkyBlue;
			this.label2.Location = new System.Drawing.Point(17, 9);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "博客：";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.userButton3);
			base.Controls.Add(this.userButton2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.userButton1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSeqCreate";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "序列号生成器";
			base.Load += new System.EventHandler(FormSeqCreate_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
