using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Core.Net;
using HslCommunication.Enthernet.Tcp;
using HslCommunication.LogNet;

namespace HslCommunicationDemo.DemoControl
{
	public class DebugRemoteControl : UserControl
	{
		private DebugRemoteServer server;

		private BinaryCommunication communication;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private Button button1;

		private TextBox textBox2;

		private Label label2;

		private ListBox listBox1;

		private Button button2;

		private Label label3;

		private Label label4;

		public DebugRemoteControl()
		{
			InitializeComponent();
			button2.Enabled = false;
		}

		private void DebugRemoteControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label1.Text = "Port:";
				button1.Text = "Start";
				button2.Text = "Close";
				label2.Text = "Onlines:";
				label4.Text = "(The client uses the Debug pipe to connect to this port)";
			}
		}

		public void SetCommunication(BinaryCommunication communication)
		{
			this.communication = communication;
			if (this.communication == null)
			{
				button1.Enabled = false;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox1.Text, out var result))
			{
				DemoUtils.ShowMessage("Port input wrong!");
				return;
			}
			try
			{
				server = new DebugRemoteServer();
				server.LogNet = new LogNetSingle("");
				server.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				server.GetCommunicationServer().OnClientOnline += DebugRemoteControl_OnClientOnline;
				server.GetCommunicationServer().OnClientOffline += DebugRemoteControl_OnClientOnline;
				server.SetDeviceCommunication(communication);
				server.ServerStart(result);
				button1.Enabled = false;
				button2.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void DebugRemoteControl_OnClientOnline(object server, PipeSession session)
		{
			try
			{
				if (!base.IsDisposed)
				{
					Invoke((Action)delegate
					{
						listBox1.DataSource = this.server.GetCommunicationServer().GetPipeSessions();
					});
				}
			}
			catch
			{
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			try
			{
				if (!base.IsDisposed)
				{
					Invoke((Action)delegate
					{
						textBox2.AppendText(e.HslMessage.ToString() + Environment.NewLine);
					});
				}
			}
			catch
			{
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			server.ServerClose();
			button1.Enabled = true;
			button2.Enabled = false;
		}

		public void Close()
		{
			if (button2.Enabled)
			{
				button2_Click(null, new EventArgs());
			}
		}

		private void label3_Click(object sender, EventArgs e)
		{
			FormMain.OpenWebside("http://www.hsltechnology.cn/Doc/HslCommunication?chapter=HslCommChapter7-9");
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.button2 = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(58, 7);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(66, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "8000";
			this.button1.Location = new System.Drawing.Point(129, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(82, 31);
			this.button1.TabIndex = 2;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox2.Location = new System.Drawing.Point(3, 39);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox2.Size = new System.Drawing.Size(494, 344);
			this.textBox2.TabIndex = 3;
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(500, 9);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(80, 17);
			this.label2.TabIndex = 4;
			this.label2.Text = "在线客户端：";
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(503, 39);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(287, 344);
			this.listBox1.TabIndex = 5;
			this.button2.Location = new System.Drawing.Point(213, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(78, 31);
			this.button2.TabIndex = 6;
			this.button2.Text = "关闭服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.ForeColor = System.Drawing.Color.FromArgb(128, 128, 255);
			this.label3.Location = new System.Drawing.Point(755, 7);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(35, 17);
			this.label3.TabIndex = 7;
			this.label3.Text = "Help";
			this.label3.Click += new System.EventHandler(label3_Click);
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoEllipsis = true;
			this.label4.ForeColor = System.Drawing.Color.Gray;
			this.label4.Location = new System.Drawing.Point(300, 10);
			this.label4.MinimumSize = new System.Drawing.Size(129, 17);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(194, 17);
			this.label4.TabIndex = 8;
			this.label4.Text = "(客户端使用Debug管道来连接此端口)";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label3);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.listBox1);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.label4);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "DebugRemoteControl";
			base.Size = new System.Drawing.Size(793, 386);
			base.Load += new System.EventHandler(DebugRemoteControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
