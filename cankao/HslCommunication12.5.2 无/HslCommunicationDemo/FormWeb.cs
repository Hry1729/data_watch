using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo
{
	public class FormWeb : HslFormContent
	{
		private string url = string.Empty;

		private IContainer components = null;

		private WebBrowser webBrowser1;

		public FormWeb(string url)
		{
			InitializeComponent();
			this.url = url;
		}

		private void FormWeb_Load(object sender, EventArgs e)
		{
		}

		private void FormWeb_Shown(object sender, EventArgs e)
		{
			webBrowser1.Navigate(url);
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
			this.webBrowser1 = new System.Windows.Forms.WebBrowser();
			base.SuspendLayout();
			this.webBrowser1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.webBrowser1.Location = new System.Drawing.Point(0, 0);
			this.webBrowser1.MinimumSize = new System.Drawing.Size(20, 20);
			this.webBrowser1.Name = "webBrowser1";
			this.webBrowser1.Size = new System.Drawing.Size(925, 574);
			this.webBrowser1.TabIndex = 0;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(925, 574);
			base.Controls.Add(this.webBrowser1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "FormWeb";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Form";
			base.Load += new System.EventHandler(FormWeb_Load);
			base.Shown += new System.EventHandler(FormWeb_Shown);
			base.ResumeLayout(false);
		}
	}
}
