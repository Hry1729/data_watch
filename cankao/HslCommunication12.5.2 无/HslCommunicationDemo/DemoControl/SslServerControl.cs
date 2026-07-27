using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;

namespace HslCommunicationDemo.DemoControl
{
	public class SslServerControl : UserControl
	{
		private IContainer components = null;

		private TextBox textBox_cert_password;

		private Label label1;

		private Button button_cert;

		private TextBox textBox_cert;

		private CheckBox checkBox_ssl;

		private BorderPanel borderPanel1;

		public SslServerControl()
		{
			InitializeComponent();
		}

		private void button_cert_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox_cert.Text = openFileDialog.FileName;
				}
			}
		}

		public void InitializeServer(DeviceServer server)
		{
			if (checkBox_ssl.Checked)
			{
				server.UseSSL(textBox_cert.Text, textBox_cert_password.Text);
			}
		}

		public void InitializeServer(CommunicationTcpServer server)
		{
			if (checkBox_ssl.Checked)
			{
				server.UseSSL(textBox_cert.Text, textBox_cert_password.Text);
			}
		}

		public bool IsUseSSL()
		{
			return checkBox_ssl.Checked;
		}

		public void AddServerCode(StringBuilder sb, string name = "server")
		{
			if (IsUseSSL())
			{
				sb.AppendLine(name + ".UseSSL( \"" + textBox_cert.Text + "\", \"" + textBox_cert_password.Text + "\" );");
			}
		}

		public void SaveXmlParameter(XElement element)
		{
			if (checkBox_ssl.Checked)
			{
				element.SetAttributeValue("UseSsl", checkBox_ssl.Checked);
			}
			if (!string.IsNullOrEmpty(textBox_cert.Text))
			{
				element.SetAttributeValue("SslCertFile", textBox_cert.Text);
			}
			if (!string.IsNullOrEmpty(textBox_cert_password.Text))
			{
				element.SetAttributeValue("SslCertPwd", textBox_cert_password.Text);
			}
		}

		public void LoadXmlParameter(XElement element)
		{
			checkBox_ssl.Checked = HslFormContent.GetXmlValue(element, "UseSsl", false, bool.Parse);
			textBox_cert.Text = HslFormContent.GetXmlValue(element, "SslCertFile", "", (string m) => m);
			textBox_cert_password.Text = HslFormContent.GetXmlValue(element, "SslCertPwd", "", (string m) => m);
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
			this.textBox_cert_password = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button_cert = new System.Windows.Forms.Button();
			this.textBox_cert = new System.Windows.Forms.TextBox();
			this.checkBox_ssl = new System.Windows.Forms.CheckBox();
			this.borderPanel1 = new HslCommunicationDemo.DemoControl.BorderPanel();
			this.borderPanel1.SuspendLayout();
			base.SuspendLayout();
			this.textBox_cert_password.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_cert_password.Location = new System.Drawing.Point(736, 3);
			this.textBox_cert_password.Name = "textBox_cert_password";
			this.textBox_cert_password.PasswordChar = '*';
			this.textBox_cert_password.Size = new System.Drawing.Size(197, 23);
			this.textBox_cert_password.TabIndex = 46;
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(654, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(76, 17);
			this.label1.TabIndex = 45;
			this.label1.Text = "Password：";
			this.button_cert.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_cert.Location = new System.Drawing.Point(564, 1);
			this.button_cert.Name = "button_cert";
			this.button_cert.Size = new System.Drawing.Size(84, 27);
			this.button_cert.TabIndex = 44;
			this.button_cert.Text = "Cert File";
			this.button_cert.UseVisualStyleBackColor = true;
			this.button_cert.Click += new System.EventHandler(button_cert_Click);
			this.textBox_cert.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_cert.Location = new System.Drawing.Point(82, 3);
			this.textBox_cert.Name = "textBox_cert";
			this.textBox_cert.Size = new System.Drawing.Size(476, 23);
			this.textBox_cert.TabIndex = 43;
			this.checkBox_ssl.AutoSize = true;
			this.checkBox_ssl.Location = new System.Drawing.Point(4, 4);
			this.checkBox_ssl.Name = "checkBox_ssl";
			this.checkBox_ssl.Size = new System.Drawing.Size(72, 21);
			this.checkBox_ssl.TabIndex = 42;
			this.checkBox_ssl.Text = "SSL/TLS";
			this.checkBox_ssl.UseVisualStyleBackColor = true;
			this.borderPanel1.Controls.Add(this.checkBox_ssl);
			this.borderPanel1.Controls.Add(this.textBox_cert_password);
			this.borderPanel1.Controls.Add(this.textBox_cert);
			this.borderPanel1.Controls.Add(this.label1);
			this.borderPanel1.Controls.Add(this.button_cert);
			this.borderPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.borderPanel1.Location = new System.Drawing.Point(0, 0);
			this.borderPanel1.Name = "borderPanel1";
			this.borderPanel1.Size = new System.Drawing.Size(938, 30);
			this.borderPanel1.TabIndex = 47;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.borderPanel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "SslServerControl";
			base.Size = new System.Drawing.Size(938, 30);
			this.borderPanel1.ResumeLayout(false);
			this.borderPanel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
