using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Core.Net;

namespace HslCommunicationDemo.DemoControl
{
	public class FormPipeSessionList : Form
	{
		private bool show = false;

		private CommunicationServer server;

		private Timer timer;

		private IContainer components = null;

		private PipeSessionListControl pipeSessionListControl1;

		public FormPipeSessionList()
		{
			InitializeComponent();
			base.Shown += FormPipeSessionList_Shown;
			timer = new Timer();
			timer.Interval = 1000;
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		private void FormPipeSessionList_Shown(object sender, EventArgs e)
		{
			show = true;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (server != null && show)
			{
				try
				{
					pipeSessionListControl1.SetPipeSessions(server.GetPipeSessions());
				}
				catch
				{
				}
			}
		}

		public void SetPipeSessions(PipeSession[] sessions)
		{
			pipeSessionListControl1.SetPipeSessions(sessions);
		}

		public void SetPipeSessions(CommunicationServer server)
		{
			this.server = server;
			pipeSessionListControl1.SetPipeSessions(this.server.GetPipeSessions());
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.DemoControl.FormPipeSessionList));
			this.pipeSessionListControl1 = new HslCommunicationDemo.DemoControl.PipeSessionListControl();
			base.SuspendLayout();
			this.pipeSessionListControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pipeSessionListControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSessionListControl1.Location = new System.Drawing.Point(0, 0);
			this.pipeSessionListControl1.Name = "pipeSessionListControl1";
			this.pipeSessionListControl1.Size = new System.Drawing.Size(800, 450);
			this.pipeSessionListControl1.TabIndex = 0;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.SystemColors.Control;
			base.ClientSize = new System.Drawing.Size(800, 450);
			base.Controls.Add(this.pipeSessionListControl1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Name = "FormPipeSessionList";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "SessionList";
			base.ResumeLayout(false);
		}
	}
}
