using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.MQTT;

namespace HslCommunicationDemo.Vip
{
	public class FormHslCertificate : Form
	{
		private IContainer components = null;

		private HslCertificateControl hslCertificateControl1;

		public FormHslCertificate(MqttSyncClient rpc, CertificateItem item)
		{
			InitializeComponent();
			hslCertificateControl1.SetRpcClient(rpc);
			hslCertificateControl1.RenderHslCertificate(item);
		}

		public void RenderHslCertificate(CertificateItem cert)
		{
			hslCertificateControl1.RenderHslCertificate(cert);
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
			this.hslCertificateControl1 = new HslCommunicationDemo.Vip.HslCertificateControl();
			base.SuspendLayout();
			this.hslCertificateControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.hslCertificateControl1.Location = new System.Drawing.Point(3, 3);
			this.hslCertificateControl1.Name = "hslCertificateControl1";
			this.hslCertificateControl1.Size = new System.Drawing.Size(730, 468);
			this.hslCertificateControl1.TabIndex = 0;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(738, 482);
			base.Controls.Add(this.hslCertificateControl1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormHslCertificate";
			this.Text = "HslCertificate [证书界面]";
			base.ResumeLayout(false);
		}
	}
}
