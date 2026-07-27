using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo
{
	public class FormAuthor : Form
	{
		private IContainer components = null;

		private Label label1;

		private Label label2;

		private Label label3;

		private LinkLabel linkLabel1;

		private Label label4;

		public FormAuthor()
		{
			InitializeComponent();
		}

		private void FormAuthor_Load(object sender, EventArgs e)
		{
			label1.BackColor = FormLoad.ThemeColor;
			label2.Text = "    作者2013年毕业于中国计量大学自动化专业，2013年-2017年就职于中策橡胶集团有限公司，从事软件开发，开发了大量的工业现场使用的上位机软件，SCADA软件，条码采集监控软件等等，实战经验丰富。离职后将所有的通信经验凝练成了一个通用库，目前该组件已经在全国的使用率非常高，成功应用于数万个项目中，于2020年成立了胡工物联科技有限公司。" + Environment.NewLine + "    作者致力于工业的信息化，智能化建设，大力推行智能制造2025，工业4.0建设，致力于降低所有传统企业的信息化转型的成本，大力发展以通信为核心，辅以机器视觉，人工智能技术，将传统的工业转型为，高科技，高智能，绿色的现代化企业。";
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			try
			{
				Process.Start(linkLabel1.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
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
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label4 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.label1.Location = new System.Drawing.Point(0, -1);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(536, 27);
			this.label1.TabIndex = 0;
			this.label1.Text = "企业：杭州胡工物联科技有限公司";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.label2.Location = new System.Drawing.Point(3, 26);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(519, 220);
			this.label2.TabIndex = 1;
			this.label2.Text = "        2013年毕业于中国计量大学自动化专业，学历本科";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(12, 240);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(68, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "个人网站：";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(86, 240);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(199, 17);
			this.linkLabel1.TabIndex = 3;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "http://www.hslcommunication.cn/";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(321, 240);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(169, 17);
			this.label4.TabIndex = 4;
			this.label4.Text = "企业联系方式：15516188079";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(534, 266);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormAuthor";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "作者简介";
			base.Load += new System.EventHandler(FormAuthor_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
