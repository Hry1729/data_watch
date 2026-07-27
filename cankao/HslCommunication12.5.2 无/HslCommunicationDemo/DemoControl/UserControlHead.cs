using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.BasicFramework;

namespace HslCommunicationDemo.DemoControl
{
	public class UserControlHead : UserControl
	{
		private IContainer components = null;

		private Label label2;

		private LinkLabel linkLabel1;

		private Label label4;

		private Label label5;

		private LinkLabel linkLabel2;

		private LinkLabel linkLabel3;

		private Panel panel1;

		private PictureBox pictureBox1;

		[Browsable(true)]
		[Category("HslCommunicationDemo")]
		[DefaultValue("http://www.hslcommunication.cn")]
		public string HelpLink
		{
			get
			{
				return linkLabel1.Text;
			}
			set
			{
				linkLabel1.Text = value;
			}
		}

		[Browsable(true)]
		[Category("HslCommunicationDemo")]
		[DefaultValue("Hsl")]
		public string ProtocolInfo
		{
			get
			{
				return label5.Text;
			}
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					label4.Visible = false;
				}
				else
				{
					label4.Visible = true;
				}
				label5.Text = value;
			}
		}

		[Browsable(true)]
		[Category("HslCommunicationDemo")]
		[DefaultValue(false)]
		public bool SupportListVisiable
		{
			get
			{
				return linkLabel3.Visible;
			}
			set
			{
				linkLabel3.Visible = value;
			}
		}

		[Browsable(true)]
		[Category("HslCommunicationDemo")]
		[DefaultValue(true)]
		public bool SaveDeviceVisiable
		{
			get
			{
				return linkLabel2.Visible;
			}
			set
			{
				linkLabel2.Visible = value;
			}
		}

		public event EventHandler<EventArgs> SaveConnectEvent;

		public UserControlHead()
		{
			InitializeComponent();
		}

		private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
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

		private void UserControlHead_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
				label2.Text = "博客地址：";
				label4.Text = "使用协议：";
				linkLabel2.Text = "保存连接";
				linkLabel3.Text = "支持列表";
			}
			else
			{
				label2.Text = "Blogs:";
				label4.Text = "Protocols:";
				linkLabel2.Text = "Save Connect";
			}
			if (!Program.ShowAuthorInfomation)
			{
				label2.Visible = false;
				linkLabel1.Visible = false;
			}
			BackColor = FormMain.ThemeColor;
		}

		public void SetText1(string text)
		{
			label2.Text = text;
		}

		public void SetProtocolImage(Image image)
		{
			pictureBox1.Image = image;
		}

		private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (this.SaveConnectEvent == null)
			{
				DemoUtils.ShowMessage(new NotImplementedException().Message);
			}
			else
			{
				this.SaveConnectEvent?.Invoke(sender, new EventArgs());
			}
		}

		private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Windows.Forms.Control control = base.Parent;
			while (!(control is HslFormContent) && control != null && control.Parent != null)
			{
				control = control.Parent;
			}
			if (control != null)
			{
				using (FormDeviceSupport formDeviceSupport = new FormDeviceSupport(control.GetType().Name))
				{
					formDeviceSupport.ShowDialog();
				}
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
			this.label2 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.linkLabel2 = new System.Windows.Forms.LinkLabel();
			this.linkLabel3 = new System.Windows.Forms.LinkLabel();
			this.panel1 = new System.Windows.Forms.Panel();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.White;
			this.label2.Location = new System.Drawing.Point(29, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 12;
			this.label2.Text = "博客地址：";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.LinkColor = System.Drawing.Color.FromArgb(255, 255, 128);
			this.linkLabel1.Location = new System.Drawing.Point(96, 6);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(194, 17);
			this.linkLabel1.TabIndex = 13;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "http://www.hslcommunication.cn";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(LinkLabel1_LinkClicked);
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.Color.White;
			this.label4.Location = new System.Drawing.Point(480, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 14;
			this.label4.Text = "使用协议：";
			this.label5.AutoSize = true;
			this.label5.ForeColor = System.Drawing.Color.White;
			this.label5.Location = new System.Drawing.Point(554, 6);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(26, 17);
			this.label5.TabIndex = 15;
			this.label5.Text = "Hsl";
			this.linkLabel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel2.LinkColor = System.Drawing.Color.Lavender;
			this.linkLabel2.Location = new System.Drawing.Point(911, 2);
			this.linkLabel2.Name = "linkLabel2";
			this.linkLabel2.Size = new System.Drawing.Size(95, 26);
			this.linkLabel2.TabIndex = 16;
			this.linkLabel2.TabStop = true;
			this.linkLabel2.Text = "Save Connect";
			this.linkLabel2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel2_LinkClicked);
			this.linkLabel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel3.LinkColor = System.Drawing.Color.Cyan;
			this.linkLabel3.Location = new System.Drawing.Point(811, 2);
			this.linkLabel3.Name = "linkLabel3";
			this.linkLabel3.Size = new System.Drawing.Size(89, 26);
			this.linkLabel3.TabIndex = 17;
			this.linkLabel3.TabStop = true;
			this.linkLabel3.Text = "Support List";
			this.linkLabel3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.linkLabel3.Visible = false;
			this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel3_LinkClicked);
			this.panel1.BackColor = System.Drawing.Color.White;
			this.panel1.Controls.Add(this.pictureBox1);
			this.panel1.Location = new System.Drawing.Point(6, 5);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(18, 18);
			this.panel1.TabIndex = 18;
			this.pictureBox1.Location = new System.Drawing.Point(1, 1);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(16, 16);
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.linkLabel3);
			base.Controls.Add(this.linkLabel2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.label5);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.MinimumSize = new System.Drawing.Size(800, 32);
			base.Name = "UserControlHead";
			base.Size = new System.Drawing.Size(1008, 32);
			base.Load += new System.EventHandler(UserControlHead_Load);
			this.panel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
