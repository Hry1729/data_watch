using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.DemoControl
{
	public class FormMessageShow : Form
	{
		private IContainer components = null;

		private Label label1;

		private Button button1;

		public FormMessageShow(string message)
		{
			InitializeComponent();
			base.SizeChanged += FormMessageShow_SizeChanged;
			label1.Text = message;
			Graphics graphics = label1.CreateGraphics();
			graphics.PageUnit = GraphicsUnit.Pixel;
			SizeF sizeF = graphics.MeasureString(message, Font, label1.Width);
			if (sizeF.Height > (float)(base.Height - 90 - 20))
			{
				sizeF = graphics.MeasureString(message, Font, label1.Width + 300);
				if (sizeF.Height > 140f)
				{
					base.Size = new Size(base.Width + 300, (int)sizeF.Height + 120);
				}
				else
				{
					base.Size = new Size(base.Width + 300, base.Height);
				}
			}
			else if (sizeF.Width < 200f && sizeF.Height < 30f)
			{
				base.Size = new Size(240, 160);
			}
		}

		private void FormMessageShow_SizeChanged(object sender, EventArgs e)
		{
			button1.Location = new Point(base.Width / 2 - button1.Width / 2, base.Height - 80);
		}

		private void FormMessageShow_Load(object sender, EventArgs e)
		{
		}

		private void button1_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.OK;
		}

		public static DialogResult ShowMessage(string message)
		{
			using (FormMessageShow formMessageShow = new FormMessageShow(message))
			{
				return formMessageShow.ShowDialog();
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
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label1.Location = new System.Drawing.Point(34, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(424, 142);
			this.label1.TabIndex = 0;
			this.label1.Text = "label1";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.button1.Location = new System.Drawing.Point(199, 152);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(97, 30);
			this.button1.TabIndex = 1;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AcceptButton = this.button1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(489, 193);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormMessageShow";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Message";
			base.Load += new System.EventHandler(FormMessageShow_Load);
			base.ResumeLayout(false);
		}
	}
}
