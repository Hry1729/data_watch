using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunicationDemo.Properties;

namespace HslRedisDesktop
{
	public class ContactControl : UserControl
	{
		private IContainer components = null;

		private Panel panel1;

		private PictureBox pictureBox2;

		private PictureBox pictureBox1;

		private TextBox textBox5;

		private TextBox textBox4;

		private TextBox textBox3;

		private TextBox textBox2;

		private TextBox textBox1;

		public ContactControl()
		{
			InitializeComponent();
		}

		private void panel1_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawLine(Pens.LightGray, new Point(0, 35), new Point(panel1.Width, 35));
			e.Graphics.DrawLine(Pens.LightGray, new Point(100, 75), new Point(panel1.Width - 100, 75));
			e.Graphics.DrawLine(Pens.LightGray, new Point(100, 110), new Point(panel1.Width - 100, 110));
		}

		private void StartControl_Load(object sender, EventArgs e)
		{
			StartControl_SizeChanged(null, null);
		}

		private void StartControl_SizeChanged(object sender, EventArgs e)
		{
			panel1.Location = new Point(base.Width / 2 - panel1.Width / 2, base.Height / 2 - panel1.Height / 2);
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.panel1.Controls.Add(this.pictureBox2);
			this.panel1.Controls.Add(this.pictureBox1);
			this.panel1.Controls.Add(this.textBox5);
			this.panel1.Controls.Add(this.textBox4);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Location = new System.Drawing.Point(73, 86);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(473, 291);
			this.panel1.TabIndex = 0;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.textBox1.BackColor = System.Drawing.Color.White;
			this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox1.Location = new System.Drawing.Point(8, 5);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(125, 16);
			this.textBox1.TabIndex = 3;
			this.textBox1.Text = "QQ群1: 592132877";
			this.textBox2.BackColor = System.Drawing.Color.White;
			this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox2.Location = new System.Drawing.Point(167, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.ReadOnly = true;
			this.textBox2.Size = new System.Drawing.Size(125, 16);
			this.textBox2.TabIndex = 4;
			this.textBox2.Text = "QQ群2: 948305931";
			this.textBox3.BackColor = System.Drawing.Color.White;
			this.textBox3.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox3.Location = new System.Drawing.Point(345, 5);
			this.textBox3.Name = "textBox3";
			this.textBox3.ReadOnly = true;
			this.textBox3.Size = new System.Drawing.Size(125, 16);
			this.textBox3.TabIndex = 5;
			this.textBox3.Text = "QQ群3: 767856490";
			this.textBox4.BackColor = System.Drawing.Color.White;
			this.textBox4.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox4.Location = new System.Drawing.Point(132, 45);
			this.textBox4.Name = "textBox4";
			this.textBox4.ReadOnly = true;
			this.textBox4.Size = new System.Drawing.Size(215, 16);
			this.textBox4.TabIndex = 6;
			this.textBox4.Text = "QQ群Vip: 838185568  (赞助240元)";
			this.textBox5.BackColor = System.Drawing.Color.White;
			this.textBox5.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox5.Location = new System.Drawing.Point(145, 85);
			this.textBox5.Name = "textBox5";
			this.textBox5.ReadOnly = true;
			this.textBox5.Size = new System.Drawing.Size(173, 16);
			this.textBox5.TabIndex = 7;
			this.textBox5.Text = "Email: hsl200909@163.com";
			this.pictureBox2.Image = HslCommunicationDemo.Properties.Resources.weipay;
			this.pictureBox2.Location = new System.Drawing.Point(245, 121);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(128, 154);
			this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox2.TabIndex = 9;
			this.pictureBox2.TabStop = false;
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.alipay;
			this.pictureBox1.Location = new System.Drawing.Point(99, 121);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(128, 154);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox1.TabIndex = 8;
			this.pictureBox1.TabStop = false;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ContactControl";
			base.Size = new System.Drawing.Size(612, 440);
			base.Load += new System.EventHandler(StartControl_Load);
			base.SizeChanged += new System.EventHandler(StartControl_SizeChanged);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
