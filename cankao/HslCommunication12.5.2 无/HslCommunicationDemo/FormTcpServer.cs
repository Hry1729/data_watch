using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.HslDebug;

namespace HslCommunicationDemo
{
	public class FormTcpServer : HslFormContent
	{
		private Dictionary<string, SocketDebugSession> sessionUdp;

		private Socket socketCore = null;

		private Thread threadReceive;

		private bool IsStarted = false;

		private EndPoint udpEndPoint = null;

		private int bufferLength = 2048;

		private IContainer components = null;

		private Panel panel_main;

		private Panel panel1;

		private Button button_close;

		private Button button_start;

		private TextBox textBox_port;

		private Label label3;

		private UserControlHead userControlHead1;

		private Panel panel_tcp_udp;

		private RadioButton radioButton_udp;

		private RadioButton radioButton_tcp;

		private DebugControl debugControl1;

		private TextBox textBox_buffer_length;

		private Label label2;

		public FormTcpServer()
		{
			InitializeComponent();
			sessionUdp = new Dictionary<string, SocketDebugSession>();
			panel_tcp_udp.Paint += Panel3_Paint;
		}

		private void FormTcpDebug_Load(object sender, EventArgs e)
		{
			panel_main.Enabled = false;
			Language(Program.Language);
		}

		private void Panel3_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawRectangle(Pens.LightGray, new Rectangle(0, 0, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 1));
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "TCP/IP调试助手";
				label3.Text = "端口号：";
				button_start.Text = "启动";
				button_close.Text = "关闭";
			}
			else
			{
				Text = "TCP/IP Debug Tools";
				label3.Text = "Port:";
				button_start.Text = "Start";
				button_close.Text = "Close";
				label2.Text = "Buffer Len:";
			}
		}

		private void ThreadReceiveUdpCycle()
		{
			byte[] data = new byte[bufferLength];
			while (IsStarted)
			{
				int length = 0;
				try
				{
					EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					length = socketCore.ReceiveFrom(data, 0, data.Length, SocketFlags.None, ref remoteEP);
					udpEndPoint = remoteEP;
					SocketDebugSession session = null;
					string key = udpEndPoint.ToString();
					if (sessionUdp.ContainsKey(key))
					{
						session = sessionUdp[key];
					}
					else
					{
						session = new SocketDebugSession(socketCore, udpEndPoint);
						session.CloseWhenRemove = false;
						sessionUdp.Add(key, session);
						Invoke((Action)delegate
						{
							debugControl1.AddSessions(session);
						});
					}
					if (length > 0)
					{
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(session, 0, data.SelectBegin(length));
						});
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(new SocketDebugSession(socketCore, udpEndPoint)
						{
							CloseWhenRemove = false
						}, 2, null, ((Program.Language == 1) ? "服务器断开连接: " : "DisConnect from remote: ") + ex2.Message);
						debugControl1.RemoveSessionsAll();
					});
				}
			}
		}

		protected void AsyncAcceptCallback(IAsyncResult iar)
		{
			if (!(iar.AsyncState is Socket socket))
			{
				return;
			}
			Socket socket2 = null;
			try
			{
				socket2 = socket.EndAccept(iar);
				SocketDebugSession session = new SocketDebugSession(socket2);
				session.Buffer = new byte[bufferLength];
				socket2.BeginReceive(session.Buffer, 0, session.Buffer.Length, SocketFlags.None, ReceiveCallBack, session);
				Invoke((Action)delegate
				{
					debugControl1.AddSessions(session);
				});
			}
			catch (ObjectDisposedException)
			{
				Invoke((Action)delegate
				{
					debugControl1.RemoveSessionsAll();
				});
				return;
			}
			catch
			{
				socket2?.Close();
			}
			try
			{
				socket.BeginAccept(AsyncAcceptCallback, socket);
			}
			catch (ObjectDisposedException)
			{
			}
		}

		private void ReceiveCallBack(IAsyncResult ar)
		{
			SocketDebugSession client = ar.AsyncState as SocketDebugSession;
			if (client == null)
			{
				return;
			}
			try
			{
				int num = client.WorkSocket.EndReceive(ar);
				if (num == 0)
				{
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(client, 2, null, (Program.Language == 1) ? "远程关闭连接" : "Remote closes connection");
					});
					return;
				}
				byte[] data = new byte[num];
				Array.Copy(client.Buffer, 0, data, 0, num);
				client.WorkSocket.BeginReceive(client.Buffer, 0, client.Buffer.Length, SocketFlags.None, ReceiveCallBack, client);
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(client, 0, data);
				});
			}
			catch (ObjectDisposedException)
			{
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(client, 2, null, (Program.Language == 1) ? "服务器关闭连接" : "Server closes connection");
				});
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(client, 2, null, ((Program.Language == 1) ? "服务器断开连接: " : "DisConnect from remote: ") + ex3.Message);
				});
			}
		}

		private void FormTcpServer_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button_start.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_buffer_length.Text, out var result))
			{
				DemoUtils.ShowMessage("Buffer length input wrong!");
				return;
			}
			bufferLength = result;
			try
			{
				if (radioButton_tcp.Checked)
				{
					socketCore = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					socketCore.Bind(new IPEndPoint(0L, int.Parse(textBox_port.Text)));
					socketCore.Listen(500);
					socketCore.BeginAccept(AsyncAcceptCallback, socketCore);
				}
				else
				{
					socketCore = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					socketCore.Bind(new IPEndPoint(IPAddress.Any, int.Parse(textBox_port.Text)));
					threadReceive = new Thread(ThreadReceiveUdpCycle)
					{
						IsBackground = true
					};
					threadReceive.Start();
					IsStarted = true;
				}
				button_start.Enabled = false;
				button_close.Enabled = true;
				panel_main.Enabled = true;
				panel_tcp_udp.Enabled = false;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(StringResources.Language.NetEngineStart + Environment.NewLine + ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			IsStarted = false;
			button_start.Enabled = true;
			button_close.Enabled = false;
			panel_main.Enabled = false;
			panel_tcp_udp.Enabled = true;
			socketCore?.Close();
			debugControl1.RemoveSessionsAll();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_port.Text);
			element.SetAttributeValue("Tcp", radioButton_tcp.Checked);
			element.SetAttributeValue("BufferLength", textBox_buffer_length.Text);
			element.RemoveNodes();
			debugControl1.SaveXml(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, "502", (string m) => m);
			if (HslFormContent.GetXmlValue(element, "Tcp", true, bool.Parse))
			{
				radioButton_tcp.Checked = true;
			}
			else
			{
				radioButton_udp.Checked = true;
			}
			textBox_buffer_length.Text = HslFormContent.GetXmlValue(element, "BufferLength", "2048", (string m) => m);
			debugControl1.LoadXml(element);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
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
			this.panel_main = new System.Windows.Forms.Panel();
			this.debugControl1 = new HslCommunicationDemo.HslDebug.DebugControl();
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox_buffer_length = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.panel_tcp_udp = new System.Windows.Forms.Panel();
			this.radioButton_udp = new System.Windows.Forms.RadioButton();
			this.radioButton_tcp = new System.Windows.Forms.RadioButton();
			this.button_close = new System.Windows.Forms.Button();
			this.button_start = new System.Windows.Forms.Button();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel_main.SuspendLayout();
			this.panel1.SuspendLayout();
			this.panel_tcp_udp.SuspendLayout();
			base.SuspendLayout();
			this.panel_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_main.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel_main.Controls.Add(this.debugControl1);
			this.panel_main.Location = new System.Drawing.Point(3, 82);
			this.panel_main.Name = "panel_main";
			this.panel_main.Size = new System.Drawing.Size(1016, 561);
			this.panel_main.TabIndex = 20;
			this.debugControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.debugControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.debugControl1.Location = new System.Drawing.Point(0, 0);
			this.debugControl1.Name = "debugControl1";
			this.debugControl1.Size = new System.Drawing.Size(1014, 559);
			this.debugControl1.TabIndex = 0;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_buffer_length);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.panel_tcp_udp);
			this.panel1.Controls.Add(this.button_close);
			this.panel1.Controls.Add(this.button_start);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1016, 43);
			this.panel1.TabIndex = 14;
			this.textBox_buffer_length.Location = new System.Drawing.Point(376, 9);
			this.textBox_buffer_length.Name = "textBox_buffer_length";
			this.textBox_buffer_length.Size = new System.Drawing.Size(83, 23);
			this.textBox_buffer_length.TabIndex = 26;
			this.textBox_buffer_length.Text = "2048";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(298, 12);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 25;
			this.label2.Text = "缓冲大小：";
			this.panel_tcp_udp.Controls.Add(this.radioButton_udp);
			this.panel_tcp_udp.Controls.Add(this.radioButton_tcp);
			this.panel_tcp_udp.Location = new System.Drawing.Point(144, 6);
			this.panel_tcp_udp.Name = "panel_tcp_udp";
			this.panel_tcp_udp.Size = new System.Drawing.Size(148, 29);
			this.panel_tcp_udp.TabIndex = 6;
			this.radioButton_udp.AutoSize = true;
			this.radioButton_udp.Location = new System.Drawing.Point(73, 4);
			this.radioButton_udp.Name = "radioButton_udp";
			this.radioButton_udp.Size = new System.Drawing.Size(51, 21);
			this.radioButton_udp.TabIndex = 1;
			this.radioButton_udp.Text = "UDP";
			this.radioButton_udp.UseVisualStyleBackColor = true;
			this.radioButton_tcp.AutoSize = true;
			this.radioButton_tcp.Checked = true;
			this.radioButton_tcp.Location = new System.Drawing.Point(10, 4);
			this.radioButton_tcp.Name = "radioButton_tcp";
			this.radioButton_tcp.Size = new System.Drawing.Size(48, 21);
			this.radioButton_tcp.TabIndex = 0;
			this.radioButton_tcp.TabStop = true;
			this.radioButton_tcp.Text = "TCP";
			this.radioButton_tcp.UseVisualStyleBackColor = true;
			this.button_close.Enabled = false;
			this.button_close.Location = new System.Drawing.Point(582, 7);
			this.button_close.Name = "button_close";
			this.button_close.Size = new System.Drawing.Size(91, 28);
			this.button_close.TabIndex = 5;
			this.button_close.Text = "关闭服务";
			this.button_close.UseVisualStyleBackColor = true;
			this.button_close.Click += new System.EventHandler(button2_Click);
			this.button_start.Location = new System.Drawing.Point(485, 7);
			this.button_start.Name = "button_start";
			this.button_start.Size = new System.Drawing.Size(91, 28);
			this.button_start.TabIndex = 4;
			this.button_start.Text = "启动服务";
			this.button_start.UseVisualStyleBackColor = true;
			this.button_start.Click += new System.EventHandler(button1_Click);
			this.textBox_port.Location = new System.Drawing.Point(62, 9);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(76, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "502";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "Port：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "TCP/UDP Server";
			this.userControlHead1.Size = new System.Drawing.Size(1023, 32);
			this.userControlHead1.TabIndex = 21;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1023, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel_main);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormTcpServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "TCP/IP调试助手";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormTcpServer_FormClosing);
			base.Load += new System.EventHandler(FormTcpDebug_Load);
			this.panel_main.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel_tcp_udp.ResumeLayout(false);
			this.panel_tcp_udp.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
