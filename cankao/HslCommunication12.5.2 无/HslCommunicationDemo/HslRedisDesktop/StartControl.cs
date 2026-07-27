using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunicationDemo.Properties;

namespace HslRedisDesktop
{
	public class StartControl : UserControl
	{
		private IContainer components = null;

		private Panel panel1;

		private PictureBox pictureBox1;

		private Label label2;

		private Label label1;

		private Label label5;

		private Label label4;

		private Label label3;

		public StartControl()
		{
			InitializeComponent();
		}

		private void panel1_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawLine(Pens.LightGray, new Point(0, 78), new Point(panel1.Width, 78));
		}

		private void StartControl_Load(object sender, EventArgs e)
		{
			label2.Text = "Version: " + Utils.Version.ToString();
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
			this.label5 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.pictureBox1);
			this.panel1.Location = new System.Drawing.Point(134, 161);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(365, 116);
			this.panel1.TabIndex = 0;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.label5.AutoSize = true;
			this.label5.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 134);
			this.label5.ForeColor = System.Drawing.Color.Black;
			this.label5.Location = new System.Drawing.Point(155, 87);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(145, 17);
			this.label5.TabIndex = 5;
			this.label5.Text = "HslCommunication.dll";
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.Color.Gray;
			this.label4.Location = new System.Drawing.Point(68, 87);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(92, 17);
			this.label4.TabIndex = 4;
			this.label4.Text = "Supported by ";
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.Color.Gray;
			this.label3.Location = new System.Drawing.Point(177, 49);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(156, 17);
			this.label3.TabIndex = 3;
			this.label3.Text = "Developed by Richard.Hu";
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Gray;
			this.label2.Location = new System.Drawing.Point(73, 49);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(86, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Version: 1.0.0";
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 24f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(73, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(285, 41);
			this.label1.TabIndex = 1;
			this.label1.Text = "HslRedis Desktop";
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.redis;
			this.pictureBox1.Location = new System.Drawing.Point(3, 3);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(64, 64);
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "StartControl";
			base.Size = new System.Drawing.Size(612, 440);
			base.Load += new System.EventHandler(StartControl_Load);
			base.SizeChanged += new System.EventHandler(StartControl_SizeChanged);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
