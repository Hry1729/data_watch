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
using HslCommunication.Core;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.HslDebug;

namespace HslCommunicationDemo
{
	public class FormTcpDebug : HslFormContent
	{
		private byte[] dtuBuffer = new byte[2048];

		private PipeTcpNet pipeDtu;

		private RemoteConnectInfo dtuRemoteConnect = null;

		private SocketDebugSession socketDebugSession;

		private bool connectSuccess = false;

		private byte[] buffer = new byte[2048];

		private List<byte[]> connectHandShake;

		private EndPoint udpEndPoint = null;

		private Thread threadReceive;

		private bool IsStarted = false;

		private bool isBinary = true;

		private AsyncCallback asyncCallback_ReceiveCallBack = null;

		private IContainer components = null;

		private Panel panel_main;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox_port;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private UserControlHead userControlHead1;

		private Button button_edit_hand;

		private Label label4;

		private Panel panel_tcp_udp;

		private RadioButton radioButton_udp;

		private RadioButton radioButton_tcp;

		private Label label10;

		private TextBox textBox_localPort;

		private Label label9;

		private DebugControl debugControl1;

		private TextBox textBox_buffer_length;

		private Label label2;

		private CheckBox checkBox1;

		public FormTcpDebug()
		{
			InitializeComponent();
			connectHandShake = new List<byte[]>();
			asyncCallback_ReceiveCallBack = ReceiveCallBack;
		}

		private void FormTcpDebug_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "TCP/IP调试助手";
				label1.Text = "Ip地址：";
				label3.Text = "端口号：";
				button1.Text = "连接";
				button2.Text = "关闭连接";
				return;
			}
			Text = "TCP/IP Debug Tools";
			label1.Text = "Ip:";
			label3.Text = "Port:";
			button1.Text = "Connect";
			button2.Text = "Disconnect";
			label9.Text = "Local Port：";
			label10.Text = "(If empty, automatically assigned)";
			button_edit_hand.Text = "Edit handshake";
			label2.Text = "Buffer Len:";
			checkBox1.Text = "Go to Dtu?";
		}

		private void button_edit_hand_Click(object sender, EventArgs e)
		{
			using (FormHandShake formHandShake = new FormHandShake(connectHandShake))
			{
				if (formHandShake.ShowDialog(this) == DialogResult.OK)
				{
					connectHandShake = formHandShake.HandShake;
					label4.Text = ((Program.Language == 1) ? "握手包：" : "HandShanke: ") + formHandShake.HandShake?.Count;
				}
			}
		}

		private void FormTcpDebug_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
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
			try
			{
				if (result != buffer.Length)
				{
					buffer = new byte[result];
				}
				socketDebugSession?.WorkSocket?.Close();
				IPAddress iPAddress = IPAddress.Parse(HslHelper.GetIpAddressFromInput(textBox_ip.Text));
				if (radioButton_tcp.Checked)
				{
					Socket socketCore = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
					if (!string.IsNullOrEmpty(textBox_localPort.Text))
					{
						OperateResult<int> operateResult = DemoUtils.ParseInt(textBox_localPort.Text);
						if (!operateResult.IsSuccess)
						{
							DemoUtils.ShowMessage("Local Port input failed: " + operateResult.Message);
							return;
						}
						socketCore.Bind(new IPEndPoint(IPAddress.Any, operateResult.Content));
					}
					connectSuccess = false;
					new Thread((ThreadStart)delegate
					{
						Thread.Sleep(5000);
						if (!connectSuccess)
						{
							socketCore?.Close();
						}
					}).Start();
					socketCore.Connect(iPAddress, int.Parse(textBox_port.Text));
					connectSuccess = true;
					socketDebugSession = new SocketDebugSession(socketCore);
					debugControl1.AddSessions(socketDebugSession);
					if (connectHandShake.Count > 0)
					{
						for (int i = 0; i < connectHandShake.Count; i++)
						{
							debugControl1.SendSourceData(socketDebugSession, connectHandShake[i]);
							int length = socketCore.Receive(buffer, 0, buffer.Length, SocketFlags.None);
							debugControl1.RenderSendReceiveContent(socketDebugSession, 0, buffer.SelectBegin(length));
						}
					}
					socketDebugSession.WorkSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, asyncCallback_ReceiveCallBack, socketCore);
				}
				else
				{
					udpEndPoint = new IPEndPoint(iPAddress, int.Parse(textBox_port.Text));
					Socket socket = new Socket(iPAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
					if (!string.IsNullOrEmpty(textBox_localPort.Text))
					{
						OperateResult<int> operateResult2 = DemoUtils.ParseInt(textBox_localPort.Text);
						if (!operateResult2.IsSuccess)
						{
							DemoUtils.ShowMessage("Local Port input failed: " + operateResult2.Message);
							return;
						}
						socket.Bind(new IPEndPoint((udpEndPoint.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, operateResult2.Content));
					}
					socket.SendTo(new byte[0], udpEndPoint);
					socketDebugSession = new SocketDebugSession(socket, udpEndPoint);
					debugControl1.AddSessions(socketDebugSession);
					IsStarted = true;
					threadReceive = new Thread(ThreadReceiveUdpCycle)
					{
						IsBackground = true
					};
					threadReceive.Start();
					if (connectHandShake.Count > 0)
					{
						for (int j = 0; j < connectHandShake.Count; j++)
						{
							debugControl1.SendSourceData(socketDebugSession, connectHandShake[j]);
							Thread.Sleep(100);
						}
					}
				}
				if (checkBox1.Checked)
				{
					ThreadPool.QueueUserWorkItem(ConnectDtuIp, dtuRemoteConnect);
				}
				button1.Enabled = false;
				button2.Enabled = true;
				panel_main.Enabled = true;
				panel_tcp_udp.Enabled = false;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + Environment.NewLine + ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			IsStarted = false;
			button1.Enabled = true;
			button2.Enabled = false;
			panel_tcp_udp.Enabled = true;
			debugControl1.RemoveSessionsAll();
			if (dtuRemoteConnect != null)
			{
				dtuRemoteConnect.Session?.Communication?.CloseCommunication();
			}
		}

		protected virtual void ThreadReceiveUdpCycle()
		{
			EndPoint remoteEP = new IPEndPoint((udpEndPoint.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0);
			while (IsStarted)
			{
				int num = 0;
				try
				{
					num = socketDebugSession.WorkSocket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEP);
					byte[] data = buffer.SelectBegin(num);
					if (num > 0)
					{
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(socketDebugSession, 0, data);
						});
					}
					if (checkBox1.Checked && pipeDtu != null && dtuRemoteConnect != null)
					{
						pipeDtu.Send(data);
						RenderDtuData(dtuRemoteConnect, pipeDtu, null, 4, data, string.Empty, $"Send DTU {dtuRemoteConnect.EndPoint}");
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(socketDebugSession, 2, null, ((Program.Language == 1) ? "服务器断开连接。原因：" : "DisConnect from remote, reason: ") + ex2.Message);
						button1.Enabled = true;
						button2.Enabled = false;
						panel_tcp_udp.Enabled = true;
					});
					break;
				}
			}
		}

		private void ReceiveCallBack(IAsyncResult ar)
		{
			try
			{
				int num = socketDebugSession.WorkSocket.EndReceive(ar);
				byte[] data = new byte[num];
				if (num > 0)
				{
					Array.Copy(buffer, 0, data, 0, num);
				}
				socketDebugSession.WorkSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, asyncCallback_ReceiveCallBack, socketDebugSession.WorkSocket);
				if (num == 0)
				{
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(socketDebugSession, 2, null, (Program.Language == 1) ? "远程关闭连接" : "Remote closes connection");
						button1.Enabled = true;
						button2.Enabled = false;
						panel_tcp_udp.Enabled = true;
					});
					return;
				}
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(socketDebugSession, 0, data);
				});
				if (checkBox1.Checked && pipeDtu != null && dtuRemoteConnect != null)
				{
					pipeDtu.Send(data);
					RenderDtuData(dtuRemoteConnect, pipeDtu, null, 4, data, string.Empty, $"Send DTU {dtuRemoteConnect.EndPoint}");
				}
			}
			catch (ObjectDisposedException)
			{
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(socketDebugSession, 2, null, (Program.Language == 1) ? "客户端关闭连接" : "Client closes connection");
					button1.Enabled = true;
					button2.Enabled = false;
					panel_tcp_udp.Enabled = true;
				});
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(socketDebugSession, 2, null, ((Program.Language == 1) ? "服务器断开连接: " : "DisConnect from remote: ") + ex3.Message);
					button1.Enabled = true;
					button2.Enabled = false;
					panel_tcp_udp.Enabled = true;
				});
			}
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox1.Checked)
			{
				using (FormInputAlien formInputAlien = new FormInputAlien())
				{
					if (formInputAlien.ShowDialog() == DialogResult.OK)
					{
						if (formInputAlien.UseHslDtuServer)
						{
							dtuRemoteConnect = new RemoteConnectInfo(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.DTU, formInputAlien.Pwd, formInputAlien.NeedAckDtuResult);
						}
						else
						{
							dtuRemoteConnect = new RemoteConnectInfo(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.CustomizeDTU);
						}
					}
					return;
				}
			}
			dtuRemoteConnect = null;
		}

		private void RenderDtuData(RemoteConnectInfo remoteConnect, PipeTcpNet pipeDtu, SocketDebugSession session, int code, byte[] data, string msg = null, string head = null)
		{
			if (code == 2 && (button1.Enabled || !checkBox1.Checked))
			{
				pipeDtu.CloseCommunication();
				pipeDtu = null;
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(session, code, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Finish", head);
				});
				return;
			}
			Invoke((Action)delegate
			{
				debugControl1.RenderSendReceiveContent(session, code, data, msg, head);
			});
			if (code != 2)
			{
				return;
			}
			pipeDtu.CloseCommunication();
			pipeDtu = null;
			HslHelper.ThreadSleep(10000);
			if (!button1.Enabled && checkBox1.Checked)
			{
				ThreadPool.QueueUserWorkItem(ConnectDtuIp, remoteConnect);
				return;
			}
			Invoke((Action)delegate
			{
				debugControl1.RenderSendReceiveContent(session, code, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Finish", head);
			});
		}

		public void ConnectDtuIp(object obj)
		{
			RemoteConnectInfo remoteConnect = obj as RemoteConnectInfo;
			if (remoteConnect == null)
			{
				return;
			}
			pipeDtu = new PipeTcpNet(remoteConnect.EndPoint.Address.ToString(), remoteConnect.EndPoint.Port);
			pipeDtu.ConnectTimeOut = 10000;
			OperateResult<bool> operateResult = pipeDtu.OpenCommunication();
			if (!operateResult.IsSuccess)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Socket Connected Failed : {operateResult.Message} 10s later retry...");
				return;
			}
			if (remoteConnect.DtuBytes != null)
			{
				if (!pipeDtu.Send(remoteConnect.DtuBytes).IsSuccess)
				{
					RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Socket Connected Failed : {operateResult.Message} 10s later retry...");
					return;
				}
				Invoke((Action)delegate
				{
					debugControl1.RenderSendReceiveContent(null, 4, remoteConnect.DtuBytes, null, $"Ini DTU {remoteConnect.EndPoint}");
				});
			}
			if (remoteConnect.NeedAckResult)
			{
				OperateResult<byte[]> check = pipeDtu.ReceiveMessage(new AlienMessage(), null, false);
				if (check.IsSuccess)
				{
					Invoke((Action)delegate
					{
						debugControl1.RenderSendReceiveContent(null, 0, check.Content, null, $"Ini DTU {remoteConnect.EndPoint} Back");
					});
					if (check.Content.Length >= 6)
					{
						switch (check.Content[5])
						{
						case 1:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.DeviceCurrentIsLoginRepeat} 10s later retry...");
							return;
						case 2:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.DeviceCurrentIsLoginForbidden} 10s later retry...");
							return;
						case 3:
							RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {StringResources.Language.PasswordCheckFailed} 10s later retry...");
							return;
						}
					}
				}
			}
			remoteConnect.Session = new PipeSession
			{
				Communication = pipeDtu
			};
			try
			{
				pipeDtu.Socket.BeginReceive(dtuBuffer, 0, dtuBuffer.Length, SocketFlags.None, DtuAsyncCallback, remoteConnect);
			}
			catch (Exception ex)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex.Message} 10s later retry...");
			}
		}

		private void DtuAsyncCallback(IAsyncResult ar)
		{
			RemoteConnectInfo remoteConnect = ar.AsyncState as RemoteConnectInfo;
			if (remoteConnect == null)
			{
				return;
			}
			try
			{
				int num = (remoteConnect.Session.Communication as PipeTcpNet).Socket.EndReceive(ar);
				if (num == 0)
				{
					RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] Closed, 10s later retry...");
					return;
				}
				byte[] data = dtuBuffer.SelectBegin(num);
				RenderDtuData(remoteConnect, pipeDtu, null, 4, data, string.Empty, $"Revc DTU {remoteConnect.EndPoint}");
				if (socketDebugSession != null)
				{
					try
					{
						socketDebugSession.SendData(data);
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(socketDebugSession, 1, data, null, $"Send DTU {remoteConnect.EndPoint}");
						});
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						Invoke((Action)delegate
						{
							debugControl1.RenderSendReceiveContent(socketDebugSession, -1, null, "socketDebugSession.SendData failed: " + ex2.Message);
						});
					}
				}
			}
			catch (Exception ex3)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex3.Message} 10s later retry...");
				return;
			}
			try
			{
				pipeDtu.Socket.BeginReceive(dtuBuffer, 0, dtuBuffer.Length, SocketFlags.None, DtuAsyncCallback, remoteConnect);
			}
			catch (Exception ex4)
			{
				RenderDtuData(remoteConnect, pipeDtu, null, 2, null, $"RemoteConnectInfo[{remoteConnect.EndPoint}] {ex4.Message} 10s later retry...");
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_port.Text);
			element.SetAttributeValue("Tcp", radioButton_tcp.Checked);
			element.SetAttributeValue("Local", textBox_localPort.Text);
			element.SetAttributeValue("BufferLength", textBox_buffer_length.Text);
			element.RemoveNodes();
			List<byte[]> list = connectHandShake;
			if (list != null && list.Count > 0)
			{
				foreach (byte[] item in connectHandShake)
				{
					XElement xElement = new XElement("HandShake");
					xElement.SetAttributeValue("Message", item.ToHexString());
					element.Add(xElement);
				}
			}
			debugControl1.SaveXml(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, "127.0.0.1", (string m) => m);
			textBox_port.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlPort, "502", (string m) => m);
			if (HslFormContent.GetXmlValue(element, "Tcp", true, bool.Parse))
			{
				radioButton_tcp.Checked = true;
			}
			else
			{
				radioButton_udp.Checked = true;
			}
			textBox_localPort.Text = HslFormContent.GetXmlValue(element, "Local", "", (string m) => m);
			textBox_buffer_length.Text = HslFormContent.GetXmlValue(element, "BufferLength", "2048", (string m) => m);
			debugControl1.LoadXml(element);
			connectHandShake.Clear();
			foreach (XElement item in element.Elements("HandShake"))
			{
				XAttribute xAttribute = item.Attribute("Message");
				if (xAttribute != null)
				{
					connectHandShake.Add(xAttribute.Value.ToHexBytes());
				}
			}
			label4.Text = ((Program.Language == 1) ? "握手包：" : "HandShanke: ") + connectHandShake?.Count;
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
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox_buffer_length = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_localPort = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.panel_tcp_udp = new System.Windows.Forms.Panel();
			this.radioButton_udp = new System.Windows.Forms.RadioButton();
			this.radioButton_tcp = new System.Windows.Forms.RadioButton();
			this.button_edit_hand = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel_main.SuspendLayout();
			this.panel1.SuspendLayout();
			this.panel_tcp_udp.SuspendLayout();
			base.SuspendLayout();
			this.panel_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_main.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel_main.Controls.Add(this.debugControl1);
			this.panel_main.Location = new System.Drawing.Point(3, 97);
			this.panel_main.Name = "panel_main";
			this.panel_main.Size = new System.Drawing.Size(998, 536);
			this.panel_main.TabIndex = 20;
			this.debugControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.debugControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.debugControl1.Location = new System.Drawing.Point(0, 0);
			this.debugControl1.Name = "debugControl1";
			this.debugControl1.Size = new System.Drawing.Size(996, 534);
			this.debugControl1.TabIndex = 0;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.textBox_buffer_length);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.label10);
			this.panel1.Controls.Add(this.textBox_localPort);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.panel_tcp_udp);
			this.panel1.Controls.Add(this.button_edit_hand);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(998, 60);
			this.panel1.TabIndex = 14;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(740, 35);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(94, 21);
			this.checkBox1.TabIndex = 25;
			this.checkBox1.Text = "转远程DTU?";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox1.CheckedChanged += new System.EventHandler(checkBox1_CheckedChanged);
			this.textBox_buffer_length.Location = new System.Drawing.Point(635, 33);
			this.textBox_buffer_length.Name = "textBox_buffer_length";
			this.textBox_buffer_length.Size = new System.Drawing.Size(83, 23);
			this.textBox_buffer_length.TabIndex = 24;
			this.textBox_buffer_length.Text = "2048";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(562, 36);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 23;
			this.label2.Text = "缓冲大小：";
			this.label10.AutoSize = true;
			this.label10.ForeColor = System.Drawing.Color.Gray;
			this.label10.Location = new System.Drawing.Point(379, 35);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(124, 17);
			this.label10.TabIndex = 22;
			this.label10.Text = "(如果为空，自动分配)";
			this.textBox_localPort.Location = new System.Drawing.Point(296, 32);
			this.textBox_localPort.Name = "textBox_localPort";
			this.textBox_localPort.Size = new System.Drawing.Size(77, 23);
			this.textBox_localPort.TabIndex = 21;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(217, 35);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(59, 17);
			this.label9.TabIndex = 20;
			this.label9.Text = "本地端口:";
			this.panel_tcp_udp.Controls.Add(this.radioButton_udp);
			this.panel_tcp_udp.Controls.Add(this.radioButton_tcp);
			this.panel_tcp_udp.Location = new System.Drawing.Point(51, 32);
			this.panel_tcp_udp.Name = "panel_tcp_udp";
			this.panel_tcp_udp.Size = new System.Drawing.Size(146, 23);
			this.panel_tcp_udp.TabIndex = 19;
			this.radioButton_udp.AutoSize = true;
			this.radioButton_udp.Location = new System.Drawing.Point(73, 1);
			this.radioButton_udp.Name = "radioButton_udp";
			this.radioButton_udp.Size = new System.Drawing.Size(51, 21);
			this.radioButton_udp.TabIndex = 1;
			this.radioButton_udp.Text = "UDP";
			this.radioButton_udp.UseVisualStyleBackColor = true;
			this.radioButton_tcp.AutoSize = true;
			this.radioButton_tcp.Checked = true;
			this.radioButton_tcp.Location = new System.Drawing.Point(10, 1);
			this.radioButton_tcp.Name = "radioButton_tcp";
			this.radioButton_tcp.Size = new System.Drawing.Size(48, 21);
			this.radioButton_tcp.TabIndex = 0;
			this.radioButton_tcp.TabStop = true;
			this.radioButton_tcp.Text = "TCP";
			this.radioButton_tcp.UseVisualStyleBackColor = true;
			this.button_edit_hand.Location = new System.Drawing.Point(550, 1);
			this.button_edit_hand.Name = "button_edit_hand";
			this.button_edit_hand.Size = new System.Drawing.Size(115, 31);
			this.button_edit_hand.TabIndex = 18;
			this.button_edit_hand.Text = "编辑握手包";
			this.button_edit_hand.UseVisualStyleBackColor = true;
			this.button_edit_hand.Click += new System.EventHandler(button_edit_hand_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(468, 8);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(63, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "握手包：0";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(785, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(688, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_port.Location = new System.Drawing.Point(374, 5);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(77, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "502";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(320, 8);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "Port：";
			this.textBox_ip.Location = new System.Drawing.Point(51, 5);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(258, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(32, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "TCP/UDP Clinet";
			this.userControlHead1.Size = new System.Drawing.Size(1005, 32);
			this.userControlHead1.TabIndex = 21;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1005, 636);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel_main);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormTcpDebug";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "TCP/IP调试助手";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormTcpDebug_FormClosing);
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
