using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Algorithms.ConnectPool;

namespace HslCommunicationDemo.Algorithms
{
	public class FormConnectPool : HslFormContent
	{
		private ConnectPool<SiemensConnector> siemensConnect = null;

		private IContainer components = null;

		private Label label1;

		private Button button1;

		public FormConnectPool()
		{
			InitializeComponent();
		}

		private void FormConnectPool_Load(object sender, EventArgs e)
		{
			label1.Text = "我们都知道线程池是为了高效的利用线程资源而设计的，此处的连接池也是为了高效的使用连接对象设计的，这个连接对象可以是任何的东西，西门子PLC，Modbus Tcp，Redis，数据库，自定义的连接等等。主需要服务器端支持多连接即可，比如三菱的端口就不支持多连接的功能，所以不能使用连接池。本节目的代码是演示了一个西门子连接池，根据案例可以扩充其他的连接池。";
			siemensConnect = new ConnectPool<SiemensConnector>(() => new SiemensConnector("192.168.1.195"));
			siemensConnect.MaxConnector = 10;
			siemensConnect.ConectionExpireTime = 30;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			SiemensConnector availableConnector = siemensConnect.GetAvailableConnector();
			short content = availableConnector.GetSiemens().ReadInt16("M100").Content;
			siemensConnect.ReturnConnector(availableConnector);
			int usedConnector = siemensConnect.UsedConnector;
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
			this.label1.Location = new System.Drawing.Point(12, 42);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(980, 146);
			this.label1.TabIndex = 0;
			this.label1.Text = "连接池说明";
			this.button1.Location = new System.Drawing.Point(15, 238);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(120, 32);
			this.button1.TabIndex = 1;
			this.button1.Text = "读取数据";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormConnectPool";
			this.Text = "连接池的使用说明";
			base.Load += new System.EventHandler(FormConnectPool_Load);
			base.ResumeLayout(false);
		}
	}
}
