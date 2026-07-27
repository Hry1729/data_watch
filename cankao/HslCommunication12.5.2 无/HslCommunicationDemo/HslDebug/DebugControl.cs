using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.BasicFramework;
using HslCommunication.Serial;

namespace HslCommunicationDemo.HslDebug
{
	public class DebugControl : UserControl
	{
		private List<SocketDebugSession> sessions;

		private int listIndex = 0;

		private List<PacketMessageItem> packetMessages;

		private System.Windows.Forms.Timer timer;

		private System.Windows.Forms.Timer sendtimer;

		private long sendCount = 0L;

		private long recvCount = 0L;

		private long sendTick = 0L;

		private long recvTick = 0L;

		private IContainer components = null;

		private LinkLabel linkLabel1;

		private Label label_recv_count;

		private TextBox textBox5;

		private Label label11;

		private TextBox textBox_send_intenal;

		private CheckBox checkBox_send_cycle;

		private CheckBox checkBox_show_header;

		private RichTextBox richTextBox_main;

		private Label label_send_count;

		private Panel panel4;

		private RadioButton radioButton_append_crc16;

		private RadioButton radioButton_append_0d0a;

		private RadioButton radioButton_append_none;

		private RadioButton radioButton_append_0a;

		private RadioButton radioButton_append_0d;

		private Label label_append;

		private Button button5;

		private Label label8;

		private CheckBox checkBox_show_send;

		private Label label7;

		private Button button_send;

		private Label label6;

		private CheckBox checkBox_stop_show;

		private CheckBox checkBox_auto_return;

		private Panel panel2;

		private RadioButton radioButton_send_cycle;

		private RadioButton radioButton_send_text;

		private LinkLabel linkLabel_build_message;

		private ComboBox comboBox_sessions;

		private Label label_session_count;

		private Panel panel1;

		private Label label3;

		private ListBox listBox1;

		private Panel panel5;

		private RadioButton radioButton_send_all;

		private RadioButton radioButton_send_single;

		private TextBox textBox_send_info;

		private ComboBox comboBox_send_encoding;

		private ComboBox comboBox_recv_encoding;

		private Label label_send_tick;

		private Label label_recv_tick;

		public DebugControl()
		{
			InitializeComponent();
			packetMessages = new List<PacketMessageItem>();
			sessions = new List<SocketDebugSession>();
			base.Disposed += DebugControl_Disposed;
			panel4.Paint += Panel3_Paint;
			panel5.Paint += Panel3_Paint;
			linkLabel1.LinkClicked += linkLabel1_LinkClicked;
			button_send.Click += button_send_Click;
			linkLabel_build_message.Click += button_build_message_Click;
			checkBox_send_cycle.CheckedChanged += CheckBox_send_cycle_CheckedChanged;
			listBox1.MouseDoubleClick += ListBox1_MouseDoubleClick;
		}

		private void DebugControl_Disposed(object sender, EventArgs e)
		{
			if (checkBox_send_cycle.Checked)
			{
				checkBox_send_cycle.Checked = false;
			}
			timer?.Dispose();
		}

		private void DebugControl_Load(object sender, EventArgs e)
		{
			timer = new System.Windows.Forms.Timer();
			timer.Interval = 500;
			timer.Tick += Timer_Tick;
			timer.Start();
			List<string> list = new List<string>();
			list.Add("Binary");
			list.AddRange(DemoUtils.GetEncodings());
			comboBox_send_encoding.DataSource = list.ToArray();
			comboBox_send_encoding.SelectedIndex = 0;
			comboBox_recv_encoding.DataSource = list.ToArray();
			comboBox_recv_encoding.SelectedIndex = 0;
			if (Program.Language == 1)
			{
				label6.Text = "发送:";
				label7.Text = "显示区域:";
				checkBox_show_send.Text = "是否显示发送数据";
				button_send.Text = "发送数据";
				label8.Text = "已选择字节数：";
			}
			else
			{
				linkLabel_build_message.Text = "Build Msg";
				label6.Text = "Send:";
				label7.Text = "Display:";
				checkBox_show_send.Text = "Display send data";
				button_send.Text = "Send Data";
				label8.Text = "Bytes selected:";
				checkBox_stop_show.Text = "Stop Show";
				checkBox_auto_return.Text = "Auto Ret";
				button5.Text = "Clear";
				checkBox_show_header.Text = "Display message header?";
				label3.Text = "Msg(d-click)";
				checkBox_send_cycle.Text = "Cycle";
				radioButton_send_text.Text = "SendText";
				radioButton_send_cycle.Text = "SendList";
				radioButton_send_single.Text = "Single";
				radioButton_send_all.Text = "All";
				textBox_send_info.Text = "<sleep=100> Single line can split and delay send";
			}
			linkLabel1.Text = string.Format(GetLinkLabelPacketMessageText(), 0);
			label_session_count.Text = string.Format(GetSessionCountText(), sessions.Count);
			richTextBox_main.ScrollToCaret();
		}

		private void ListBox1_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (listBox1.SelectedItem is PacketMessageItem packetMessageItem)
			{
				textBox5.Text = packetMessageItem.Message;
			}
		}

		private void CheckBox_send_cycle_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox_send_cycle.Checked)
			{
				if (!int.TryParse(textBox_send_intenal.Text, out var result))
				{
					DemoUtils.ShowMessage("The interval input is abnormal and requires the input of numeric text");
					return;
				}
				sendtimer?.Dispose();
				sendtimer = new System.Windows.Forms.Timer();
				sendtimer.Interval = result;
				sendtimer.Tick += Sendtimer_Tick;
				sendtimer.Start();
			}
			else
			{
				sendtimer?.Stop();
				sendtimer?.Dispose();
			}
		}

		private void Sendtimer_Tick(object sender, EventArgs e)
		{
			if (radioButton_send_cycle.Checked)
			{
				if (packetMessages == null || packetMessages.Count == 0)
				{
					return;
				}
				if (listIndex >= packetMessages.Count)
				{
					listIndex = 0;
				}
				textBox5.Text = packetMessages[listIndex].Message;
				listIndex++;
			}
			button_send.PerformClick();
		}

		private void Panel3_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawRectangle(Pens.LightGray, new Rectangle(0, 0, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 1));
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(richTextBox_main.Text))
			{
				return;
			}
			string selectedText = richTextBox_main.SelectedText;
			if (!string.IsNullOrEmpty(selectedText))
			{
				if (comboBox_send_encoding.SelectedIndex == 0)
				{
					byte[] array = SoftBasic.HexStringToBytes(selectedText);
					label8.Text = ((Program.Language == 1) ? "已选择字节数：" : "Bytes selected:") + array.Length;
				}
				else
				{
					label8.Text = ((Program.Language == 1) ? "已选择字节数：" : "Bytes selected:") + selectedText.Length;
				}
			}
		}

		private void button_clear_Click(object sender, EventArgs e)
		{
			richTextBox_main.Clear();
			Interlocked.Exchange(ref sendCount, 0L);
			Interlocked.Exchange(ref recvCount, 0L);
			label_send_count.Text = "Send Count: " + sendCount;
			label_recv_count.Text = "Recv Count: " + recvCount;
			Interlocked.Exchange(ref sendTick, 0L);
			Interlocked.Exchange(ref recvTick, 0L);
			label_send_tick.Text = "S-Tick: " + sendTick;
			label_recv_tick.Text = "R-Tick: " + recvTick;
		}

		private void button_build_message_Click(object sender, EventArgs e)
		{
			using (FormMessageCreate formMessageCreate = new FormMessageCreate())
			{
				if (formMessageCreate.ShowDialog() == DialogResult.OK && formMessageCreate.MessageCreate != null)
				{
					if (formMessageCreate.MessageCreate.HexBinary)
					{
						comboBox_send_encoding.SelectedIndex = 0;
					}
					else
					{
						comboBox_send_encoding.SelectedIndex = 1;
					}
					textBox5.Text = formMessageCreate.MessageCreate.Result;
				}
			}
		}

		private string GetLinkLabelPacketMessageText()
		{
			return (Program.Language == 1) ? "管理({0})" : "Set({0})";
		}

		private string GetSessionCountText()
		{
			return (Program.Language == 1) ? "会话数量: {0}" : "Onlines: {0}";
		}

		private void RenderMessage(int code, string msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				return;
			}
			int length = richTextBox_main.Text.Length;
			richTextBox_main.AppendText(msg);
			richTextBox_main.Select(length, msg.Length);
			if (code < 0)
			{
				richTextBox_main.SelectionColor = Color.Gray;
			}
			else
			{
				switch (code)
				{
				case 0:
					richTextBox_main.SelectionColor = Color.Green;
					break;
				case 1:
					richTextBox_main.SelectionColor = Color.Blue;
					break;
				case 4:
					richTextBox_main.SelectionColor = Color.Purple;
					break;
				default:
					richTextBox_main.SelectionColor = Color.Red;
					break;
				}
			}
			richTextBox_main.AppendText(Environment.NewLine);
		}

		public void RenderSendReceiveContent(SocketDebugSession session, int code, byte[] data, string msg = null, string head = null)
		{
			if (checkBox_stop_show.Checked || (code == 1 && !checkBox_show_send.Checked))
			{
				return;
			}
			if (checkBox_show_header.Checked)
			{
				int length = ((data == null) ? (-1) : data.Length);
				object dataFormat;
				switch (code)
				{
				default:
					dataFormat = null;
					break;
				case 1:
					dataFormat = comboBox_send_encoding.SelectedItem.ToString();
					break;
				case 0:
					dataFormat = comboBox_recv_encoding.SelectedItem.ToString();
					break;
				}
				RenderMessage(-1, GetTextHeader(session, code, length, head, (string)dataFormat));
			}
			if (data != null)
			{
				switch (code)
				{
				case 1:
					Interlocked.Add(ref sendCount, data.Length);
					label_send_count.Text = "S-byts:" + sendCount;
					break;
				case 0:
					Interlocked.Add(ref recvCount, data.Length);
					Interlocked.Increment(ref recvTick);
					label_recv_count.Text = "R-byts:" + recvCount;
					label_recv_tick.Text = "R-Tick:" + recvTick;
					break;
				}
				string msg2 = null;
				switch (code)
				{
				case 1:
					msg2 = ((comboBox_send_encoding.SelectedIndex != 0) ? ((comboBox_send_encoding.SelectedIndex != 1) ? DemoUtils.GetEncodingFromIndex(comboBox_send_encoding.SelectedIndex - 1).GetString(data) : SoftBasic.GetAsciiStringRender(data)) : SoftBasic.ByteToHexString(data, ' '));
					break;
				case 0:
					msg2 = ((comboBox_recv_encoding.SelectedIndex != 0) ? ((comboBox_recv_encoding.SelectedIndex != 1) ? DemoUtils.GetEncodingFromIndex(comboBox_recv_encoding.SelectedIndex - 1).GetString(data) : SoftBasic.GetAsciiStringRender(data)) : SoftBasic.ByteToHexString(data, ' '));
					break;
				case 4:
					msg2 = ((comboBox_send_encoding.SelectedIndex != 0) ? ((comboBox_send_encoding.SelectedIndex != 1) ? DemoUtils.GetEncodingFromIndex(comboBox_send_encoding.SelectedIndex - 1).GetString(data) : SoftBasic.GetAsciiStringRender(data)) : SoftBasic.ByteToHexString(data, ' '));
					break;
				}
				RenderMessage(code, msg2);
			}
			if (!string.IsNullOrEmpty(msg))
			{
				RenderMessage(code, msg);
			}
			richTextBox_main.ScrollToCaret();
			switch (code)
			{
			case 0:
				if (checkBox_auto_return.Checked)
				{
					button_send.PerformClick();
				}
				break;
			case 2:
				RemoveSessions(session);
				break;
			}
		}

		private static string GetTextHeader(SocketDebugSession session, int code, int length, string head = null, string dataFormat = null)
		{
			string empty = string.Empty;
			if (Program.Language == 1)
			{
				object obj;
				switch (code)
				{
				default:
					obj = "无";
					break;
				case 4:
					obj = "DTU";
					break;
				case 3:
					obj = "上线";
					break;
				case 2:
					obj = "关";
					break;
				case 1:
					obj = "发";
					break;
				case 0:
					obj = "收";
					break;
				}
				empty = (string)obj;
			}
			else
			{
				object obj2;
				switch (code)
				{
				default:
					obj2 = "None";
					break;
				case 4:
					obj2 = "DTU";
					break;
				case 3:
					obj2 = "Online";
					break;
				case 2:
					obj2 = "Clos";
					break;
				case 1:
					obj2 = "Send";
					break;
				case 0:
					obj2 = "Recv";
					break;
				}
				empty = (string)obj2;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] ");
			if (session != null)
			{
				if (session.UseMode < 2)
				{
					switch (code)
					{
					case 0:
						stringBuilder.Append($"[{empty} {session.RemoteEndPoint} To {session.LocalEndPoint}]");
						break;
					case 1:
						stringBuilder.Append($"[{empty} {session.LocalEndPoint} To {session.RemoteEndPoint}]");
						break;
					default:
						stringBuilder.Append($"[{empty} {session.RemoteEndPoint}]");
						break;
					}
				}
				else if (session.UseMode == 2)
				{
					switch (code)
					{
					case 0:
						stringBuilder.Append("[" + empty + " From " + session.PortName + "]");
						break;
					case 1:
						stringBuilder.Append("[" + empty + " To " + session.PortName + "]");
						break;
					default:
						stringBuilder.Append("[" + empty + " " + session.PortName + "]");
						break;
					}
				}
			}
			else if (!string.IsNullOrEmpty(head))
			{
				stringBuilder.Append("[" + head + "]");
			}
			if (!string.IsNullOrEmpty(dataFormat))
			{
				stringBuilder.Append("  [" + dataFormat + "]");
			}
			if (length >= 0)
			{
				stringBuilder.Append($"  Bytes Length: {length}");
			}
			return stringBuilder.ToString();
		}

		public void SetSessions(List<SocketDebugSession> sessions)
		{
			if (sessions == null)
			{
				sessions = new List<SocketDebugSession>();
			}
			this.sessions = sessions;
			comboBox_sessions.DataSource = this.sessions.ToArray();
			label_session_count.Text = string.Format(GetSessionCountText(), this.sessions.Count);
		}

		public void AddSessions(SocketDebugSession session)
		{
			if (session != null)
			{
				sessions.Add(session);
				RenderMessage(-1, GetTextHeader(session, 3, -1));
				comboBox_sessions.DataSource = sessions.ToArray();
				label_session_count.Text = string.Format(GetSessionCountText(), sessions.Count);
			}
		}

		public void RemoveSessions(SocketDebugSession session)
		{
			if (session != null)
			{
				if (sessions.RemoveAll((SocketDebugSession m) => m.SessionID == session.SessionID) == 0)
				{
				}
				comboBox_sessions.DataSource = sessions.ToArray();
				session?.Close();
				label_session_count.Text = string.Format(GetSessionCountText(), sessions.Count);
			}
		}

		public void RemoveSessionsAll()
		{
			sessions.ForEach(delegate(SocketDebugSession m)
			{
				m?.Close();
			});
			sessions.Clear();
			comboBox_sessions.DataSource = sessions.ToArray();
			label_session_count.Text = string.Format(GetSessionCountText(), sessions.Count);
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (FormPacketMessage formPacketMessage = new FormPacketMessage(packetMessages))
			{
				if (formPacketMessage.ShowDialog() == DialogResult.OK && formPacketMessage.PacketMessages != null)
				{
					packetMessages = formPacketMessage.PacketMessages;
					linkLabel1.Text = string.Format(GetLinkLabelPacketMessageText(), packetMessages.Count);
					listBox1.DataSource = packetMessages.ToArray();
				}
			}
		}

		private async void button_send_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox5.Text))
			{
				return;
			}
			if (Regex.IsMatch(textBox5.Text, "<sleep=[0-9]+>"))
			{
				string[] lines = textBox5.Lines;
				if (lines == null)
				{
					return;
				}
				button_send.Enabled = false;
				foreach (string str in lines)
				{
					if (string.IsNullOrEmpty(str))
					{
						continue;
					}
					if (Regex.IsMatch(str, "^<sleep=[0-9]+>"))
					{
						int time = Convert.ToInt32(Regex.Match(str, "[0-9]+").Value);
						if (time > 0)
						{
							await Task.Delay(time);
						}
					}
					else
					{
						SendTextInfo(str);
					}
				}
				button_send.Enabled = true;
			}
			else
			{
				SendTextInfo(textBox5.Text);
			}
			Interlocked.Increment(ref sendTick);
			label_send_tick.Text = "S-Tick:" + sendTick;
		}

		private void SendTextInfo(string text)
		{
			byte[] array = null;
			array = ((comboBox_send_encoding.SelectedIndex == 0) ? SoftBasic.HexStringToBytes(text) : ((comboBox_send_encoding.SelectedIndex != 1) ? DemoUtils.GetEncodingFromIndex(comboBox_send_encoding.SelectedIndex - 1).GetBytes(text) : SoftBasic.GetFromAsciiStringRender(text)));
			if (radioButton_append_0d.Checked)
			{
				array = SoftBasic.SpliceArray<byte>(array, new byte[1] { 13 });
			}
			else if (radioButton_append_0a.Checked)
			{
				array = SoftBasic.SpliceArray<byte>(array, new byte[1] { 10 });
			}
			else if (radioButton_append_0d0a.Checked)
			{
				array = SoftBasic.SpliceArray<byte>(array, new byte[2] { 13, 10 });
			}
			else if (radioButton_append_crc16.Checked)
			{
				array = SoftCRC16.CRC16(array);
			}
			SendSourceData(array);
		}

		public void SendSourceData(byte[] send)
		{
			if (send == null)
			{
				return;
			}
			if (radioButton_send_single.Checked)
			{
				if (comboBox_sessions.SelectedItem == null)
				{
					if (checkBox_send_cycle.Checked)
					{
						checkBox_send_cycle.Checked = false;
					}
					DemoUtils.ShowMessage((Program.Language == 1) ? "当前没有可用的连接通道，请先连接上。" : "There are currently no connection channels available, please connect them first.");
				}
				else if (comboBox_sessions.SelectedItem is SocketDebugSession session)
				{
					SendSourceData(session, send);
				}
			}
			else if (sessions == null || sessions.Count == 0)
			{
				if (checkBox_send_cycle.Checked)
				{
					checkBox_send_cycle.Checked = false;
				}
				DemoUtils.ShowMessage((Program.Language == 1) ? "当前没有可用的连接通道，请先连接上。" : "There are currently no connection channels available, please connect them first.");
			}
			else
			{
				for (int i = 0; i < sessions.Count; i++)
				{
					SendSourceData(sessions[i], send);
				}
			}
		}

		public void SendSourceData(SocketDebugSession session, byte[] send)
		{
			if (send == null)
			{
				return;
			}
			try
			{
				if (session.UseMode == 0)
				{
					session.WorkSocket?.Send(send, 0, send.Length, SocketFlags.None);
				}
				else if (session.UseMode == 1)
				{
					session.WorkSocket?.SendTo(send, 0, send.Length, SocketFlags.None, session.RemoteEndPoint);
				}
				else if (session.UseMode == 2)
				{
					session.SerialPort?.Write(send, 0, send.Length);
				}
				RenderSendReceiveContent(session, 1, send);
			}
			catch (Exception ex)
			{
				RenderSendReceiveContent(session, 2, send, "Send failed: " + ex.Message);
				RemoveSessions(session);
			}
		}

		public void LoadXml(XElement element)
		{
			packetMessages.Clear();
			foreach (XElement item in element.Elements("PacketMessageItem"))
			{
				PacketMessageItem packetMessageItem = new PacketMessageItem();
				packetMessageItem.LoadByXmlElement(item);
				packetMessages.Add(packetMessageItem);
			}
			linkLabel1.Text = string.Format(GetLinkLabelPacketMessageText(), packetMessages.Count);
			listBox1.DataSource = packetMessages.ToArray();
		}

		public void SaveXml(XElement element)
		{
			List<PacketMessageItem> list = packetMessages;
			if (list == null || list.Count <= 0)
			{
				return;
			}
			foreach (PacketMessageItem packetMessage in packetMessages)
			{
				element.Add(packetMessage.ToXmlElement());
			}
		}

		private void panel1_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawRectangle(Pens.LightGray, 0, 0, panel1.Width - 1, panel1.Height - 1);
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
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label_recv_count = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_send_intenal = new System.Windows.Forms.TextBox();
			this.checkBox_send_cycle = new System.Windows.Forms.CheckBox();
			this.checkBox_show_header = new System.Windows.Forms.CheckBox();
			this.richTextBox_main = new System.Windows.Forms.RichTextBox();
			this.label_send_count = new System.Windows.Forms.Label();
			this.panel4 = new System.Windows.Forms.Panel();
			this.radioButton_append_crc16 = new System.Windows.Forms.RadioButton();
			this.radioButton_append_0d0a = new System.Windows.Forms.RadioButton();
			this.radioButton_append_none = new System.Windows.Forms.RadioButton();
			this.radioButton_append_0a = new System.Windows.Forms.RadioButton();
			this.radioButton_append_0d = new System.Windows.Forms.RadioButton();
			this.label_append = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.label8 = new System.Windows.Forms.Label();
			this.checkBox_show_send = new System.Windows.Forms.CheckBox();
			this.label7 = new System.Windows.Forms.Label();
			this.button_send = new System.Windows.Forms.Button();
			this.label6 = new System.Windows.Forms.Label();
			this.checkBox_stop_show = new System.Windows.Forms.CheckBox();
			this.checkBox_auto_return = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.radioButton_send_cycle = new System.Windows.Forms.RadioButton();
			this.radioButton_send_text = new System.Windows.Forms.RadioButton();
			this.linkLabel_build_message = new System.Windows.Forms.LinkLabel();
			this.comboBox_sessions = new System.Windows.Forms.ComboBox();
			this.label_session_count = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel5 = new System.Windows.Forms.Panel();
			this.radioButton_send_all = new System.Windows.Forms.RadioButton();
			this.radioButton_send_single = new System.Windows.Forms.RadioButton();
			this.textBox_send_info = new System.Windows.Forms.TextBox();
			this.comboBox_send_encoding = new System.Windows.Forms.ComboBox();
			this.comboBox_recv_encoding = new System.Windows.Forms.ComboBox();
			this.label_send_tick = new System.Windows.Forms.Label();
			this.label_recv_tick = new System.Windows.Forms.Label();
			this.panel4.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel1.SuspendLayout();
			this.panel5.SuspendLayout();
			base.SuspendLayout();
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(83, 3);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(47, 17);
			this.linkLabel1.TabIndex = 64;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "管理(0)";
			this.label_recv_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_recv_count.AutoSize = true;
			this.label_recv_count.ForeColor = System.Drawing.Color.Gray;
			this.label_recv_count.Location = new System.Drawing.Point(469, 526);
			this.label_recv_count.Name = "label_recv_count";
			this.label_recv_count.Size = new System.Drawing.Size(48, 17);
			this.label_recv_count.TabIndex = 62;
			this.label_recv_count.Text = "R-byts:";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(3, 447);
			this.textBox5.Multiline = true;
			this.textBox5.Name = "textBox5";
			this.textBox5.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox5.Size = new System.Drawing.Size(858, 74);
			this.textBox5.TabIndex = 44;
			this.label11.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label11.AutoSize = true;
			this.label11.ForeColor = System.Drawing.Color.Gray;
			this.label11.Location = new System.Drawing.Point(977, 526);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(25, 17);
			this.label11.TabIndex = 61;
			this.label11.Text = "ms";
			this.textBox_send_intenal.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_send_intenal.Location = new System.Drawing.Point(922, 522);
			this.textBox_send_intenal.Name = "textBox_send_intenal";
			this.textBox_send_intenal.Size = new System.Drawing.Size(49, 23);
			this.textBox_send_intenal.TabIndex = 60;
			this.textBox_send_intenal.Text = "1000";
			this.checkBox_send_cycle.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_send_cycle.AutoSize = true;
			this.checkBox_send_cycle.Location = new System.Drawing.Point(867, 525);
			this.checkBox_send_cycle.Name = "checkBox_send_cycle";
			this.checkBox_send_cycle.Size = new System.Drawing.Size(51, 21);
			this.checkBox_send_cycle.TabIndex = 59;
			this.checkBox_send_cycle.Text = "循环";
			this.checkBox_send_cycle.UseVisualStyleBackColor = true;
			this.checkBox_show_header.AutoSize = true;
			this.checkBox_show_header.Checked = true;
			this.checkBox_show_header.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_show_header.Location = new System.Drawing.Point(404, 4);
			this.checkBox_show_header.Name = "checkBox_show_header";
			this.checkBox_show_header.Size = new System.Drawing.Size(135, 21);
			this.checkBox_show_header.TabIndex = 58;
			this.checkBox_show_header.Text = "是否显示消息头信息";
			this.checkBox_show_header.UseVisualStyleBackColor = true;
			this.richTextBox_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.richTextBox_main.Location = new System.Drawing.Point(4, 27);
			this.richTextBox_main.Name = "richTextBox_main";
			this.richTextBox_main.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
			this.richTextBox_main.Size = new System.Drawing.Size(857, 383);
			this.richTextBox_main.TabIndex = 57;
			this.richTextBox_main.Text = "";
			this.label_send_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_send_count.AutoSize = true;
			this.label_send_count.ForeColor = System.Drawing.Color.Gray;
			this.label_send_count.Location = new System.Drawing.Point(368, 526);
			this.label_send_count.Name = "label_send_count";
			this.label_send_count.Size = new System.Drawing.Size(47, 17);
			this.label_send_count.TabIndex = 55;
			this.label_send_count.Text = "S-byts:";
			this.panel4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.panel4.BackColor = System.Drawing.SystemColors.Control;
			this.panel4.Controls.Add(this.radioButton_append_crc16);
			this.panel4.Controls.Add(this.radioButton_append_0d0a);
			this.panel4.Controls.Add(this.radioButton_append_none);
			this.panel4.Controls.Add(this.radioButton_append_0a);
			this.panel4.Controls.Add(this.radioButton_append_0d);
			this.panel4.Controls.Add(this.label_append);
			this.panel4.Location = new System.Drawing.Point(424, 415);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(339, 28);
			this.panel4.TabIndex = 53;
			this.radioButton_append_crc16.AutoSize = true;
			this.radioButton_append_crc16.Location = new System.Drawing.Point(273, 4);
			this.radioButton_append_crc16.Name = "radioButton_append_crc16";
			this.radioButton_append_crc16.Size = new System.Drawing.Size(57, 21);
			this.radioButton_append_crc16.TabIndex = 5;
			this.radioButton_append_crc16.Text = "crc16";
			this.radioButton_append_crc16.UseVisualStyleBackColor = true;
			this.radioButton_append_0d0a.AutoSize = true;
			this.radioButton_append_0d0a.Location = new System.Drawing.Point(217, 4);
			this.radioButton_append_0d0a.Name = "radioButton_append_0d0a";
			this.radioButton_append_0d0a.Size = new System.Drawing.Size(48, 21);
			this.radioButton_append_0d0a.TabIndex = 4;
			this.radioButton_append_0d0a.Text = "\\r\\n";
			this.radioButton_append_0d0a.UseVisualStyleBackColor = true;
			this.radioButton_append_none.AutoSize = true;
			this.radioButton_append_none.Checked = true;
			this.radioButton_append_none.Location = new System.Drawing.Point(67, 4);
			this.radioButton_append_none.Name = "radioButton_append_none";
			this.radioButton_append_none.Size = new System.Drawing.Size(58, 21);
			this.radioButton_append_none.TabIndex = 3;
			this.radioButton_append_none.TabStop = true;
			this.radioButton_append_none.Text = "None";
			this.radioButton_append_none.UseVisualStyleBackColor = true;
			this.radioButton_append_0a.AutoSize = true;
			this.radioButton_append_0a.Location = new System.Drawing.Point(172, 4);
			this.radioButton_append_0a.Name = "radioButton_append_0a";
			this.radioButton_append_0a.Size = new System.Drawing.Size(38, 21);
			this.radioButton_append_0a.TabIndex = 1;
			this.radioButton_append_0a.Text = "\\n";
			this.radioButton_append_0a.UseVisualStyleBackColor = true;
			this.radioButton_append_0d.AutoSize = true;
			this.radioButton_append_0d.Location = new System.Drawing.Point(129, 4);
			this.radioButton_append_0d.Name = "radioButton_append_0d";
			this.radioButton_append_0d.Size = new System.Drawing.Size(36, 21);
			this.radioButton_append_0d.TabIndex = 0;
			this.radioButton_append_0d.Text = "\\r";
			this.radioButton_append_0d.UseVisualStyleBackColor = true;
			this.label_append.AutoSize = true;
			this.label_append.Location = new System.Drawing.Point(4, 6);
			this.label_append.Name = "label_append";
			this.label_append.Size = new System.Drawing.Size(57, 17);
			this.label_append.TabIndex = 2;
			this.label_append.Text = "Append:";
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(908, 1);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(91, 25);
			this.button5.TabIndex = 50;
			this.button5.Text = "清空";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button_clear_Click);
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.Gray;
			this.label8.Location = new System.Drawing.Point(1, 526);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(92, 17);
			this.label8.TabIndex = 48;
			this.label8.Text = "已选择字节数：";
			this.checkBox_show_send.AutoSize = true;
			this.checkBox_show_send.Checked = true;
			this.checkBox_show_send.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_show_send.Location = new System.Drawing.Point(233, 4);
			this.checkBox_show_send.Name = "checkBox_show_send";
			this.checkBox_show_send.Size = new System.Drawing.Size(123, 21);
			this.checkBox_show_send.TabIndex = 47;
			this.checkBox_show_send.Text = "是否显示发送数据";
			this.checkBox_show_send.UseVisualStyleBackColor = true;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(5, 5);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 46;
			this.label7.Text = "显示区域：";
			this.button_send.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button_send.Location = new System.Drawing.Point(865, 448);
			this.button_send.Name = "button_send";
			this.button_send.Size = new System.Drawing.Size(131, 74);
			this.button_send.TabIndex = 45;
			this.button_send.Text = "发送数据";
			this.button_send.UseVisualStyleBackColor = true;
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(3, 420);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 43;
			this.label6.Text = "发送：";
			this.checkBox_stop_show.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_stop_show.Location = new System.Drawing.Point(772, 409);
			this.checkBox_stop_show.Name = "checkBox_stop_show";
			this.checkBox_stop_show.Size = new System.Drawing.Size(89, 21);
			this.checkBox_stop_show.TabIndex = 54;
			this.checkBox_stop_show.Text = "暂停显示";
			this.checkBox_stop_show.UseVisualStyleBackColor = true;
			this.checkBox_auto_return.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_auto_return.AutoSize = true;
			this.checkBox_auto_return.Location = new System.Drawing.Point(772, 427);
			this.checkBox_auto_return.Name = "checkBox_auto_return";
			this.checkBox_auto_return.Size = new System.Drawing.Size(75, 21);
			this.checkBox_auto_return.TabIndex = 56;
			this.checkBox_auto_return.Text = "自动返回";
			this.checkBox_auto_return.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BackColor = System.Drawing.SystemColors.Control;
			this.panel2.Controls.Add(this.radioButton_send_cycle);
			this.panel2.Controls.Add(this.radioButton_send_text);
			this.panel2.Location = new System.Drawing.Point(651, 520);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(192, 28);
			this.panel2.TabIndex = 63;
			this.radioButton_send_cycle.AutoSize = true;
			this.radioButton_send_cycle.Location = new System.Drawing.Point(100, 3);
			this.radioButton_send_cycle.Name = "radioButton_send_cycle";
			this.radioButton_send_cycle.Size = new System.Drawing.Size(98, 21);
			this.radioButton_send_cycle.TabIndex = 1;
			this.radioButton_send_cycle.Text = "列表顺序循环";
			this.radioButton_send_cycle.UseVisualStyleBackColor = true;
			this.radioButton_send_text.AutoSize = true;
			this.radioButton_send_text.Checked = true;
			this.radioButton_send_text.Location = new System.Drawing.Point(7, 3);
			this.radioButton_send_text.Name = "radioButton_send_text";
			this.radioButton_send_text.Size = new System.Drawing.Size(86, 21);
			this.radioButton_send_text.TabIndex = 0;
			this.radioButton_send_text.TabStop = true;
			this.radioButton_send_text.Text = "使用输入框";
			this.radioButton_send_text.UseVisualStyleBackColor = true;
			this.linkLabel_build_message.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel_build_message.AutoSize = true;
			this.linkLabel_build_message.Location = new System.Drawing.Point(586, 526);
			this.linkLabel_build_message.Name = "linkLabel_build_message";
			this.linkLabel_build_message.Size = new System.Drawing.Size(56, 17);
			this.linkLabel_build_message.TabIndex = 65;
			this.linkLabel_build_message.TabStop = true;
			this.linkLabel_build_message.Text = "构建报文";
			this.comboBox_sessions.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_sessions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_sessions.FormattingEnabled = true;
			this.comboBox_sessions.Location = new System.Drawing.Point(46, 416);
			this.comboBox_sessions.Name = "comboBox_sessions";
			this.comboBox_sessions.Size = new System.Drawing.Size(226, 25);
			this.comboBox_sessions.TabIndex = 66;
			this.label_session_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_session_count.AutoSize = true;
			this.label_session_count.ForeColor = System.Drawing.Color.Gray;
			this.label_session_count.Location = new System.Drawing.Point(132, 526);
			this.label_session_count.Name = "label_session_count";
			this.label_session_count.Size = new System.Drawing.Size(70, 17);
			this.label_session_count.TabIndex = 67;
			this.label_session_count.Text = "会话数量: 0";
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.AutoScroll = true;
			this.panel1.Controls.Add(this.listBox1);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.linkLabel1);
			this.panel1.Location = new System.Drawing.Point(862, 27);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(140, 383);
			this.panel1.TabIndex = 68;
			this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(5, 22);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(129, 361);
			this.listBox1.TabIndex = 1;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(5, 3);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(76, 17);
			this.label3.TabIndex = 0;
			this.label3.Text = "数据包(双击)";
			this.panel5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.panel5.BackColor = System.Drawing.SystemColors.Control;
			this.panel5.Controls.Add(this.radioButton_send_all);
			this.panel5.Controls.Add(this.radioButton_send_single);
			this.panel5.Location = new System.Drawing.Point(865, 414);
			this.panel5.Name = "panel5";
			this.panel5.Size = new System.Drawing.Size(131, 28);
			this.panel5.TabIndex = 69;
			this.radioButton_send_all.AutoSize = true;
			this.radioButton_send_all.Location = new System.Drawing.Point(72, 3);
			this.radioButton_send_all.Name = "radioButton_send_all";
			this.radioButton_send_all.Size = new System.Drawing.Size(50, 21);
			this.radioButton_send_all.TabIndex = 1;
			this.radioButton_send_all.Text = "广播";
			this.radioButton_send_all.UseVisualStyleBackColor = true;
			this.radioButton_send_single.AutoSize = true;
			this.radioButton_send_single.Checked = true;
			this.radioButton_send_single.Location = new System.Drawing.Point(7, 3);
			this.radioButton_send_single.Name = "radioButton_send_single";
			this.radioButton_send_single.Size = new System.Drawing.Size(50, 21);
			this.radioButton_send_single.TabIndex = 0;
			this.radioButton_send_single.TabStop = true;
			this.radioButton_send_single.Text = "单播";
			this.radioButton_send_single.UseVisualStyleBackColor = true;
			this.textBox_send_info.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox_send_info.ForeColor = System.Drawing.Color.Gray;
			this.textBox_send_info.Location = new System.Drawing.Point(579, 5);
			this.textBox_send_info.Name = "textBox_send_info";
			this.textBox_send_info.ReadOnly = true;
			this.textBox_send_info.Size = new System.Drawing.Size(311, 16);
			this.textBox_send_info.TabIndex = 70;
			this.textBox_send_info.Text = "<sleep=100> 单独一行可以分割延时发送";
			this.comboBox_send_encoding.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_send_encoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_send_encoding.FormattingEnabled = true;
			this.comboBox_send_encoding.Items.AddRange(new object[3] { "Binary", "ASCII", "Unicode" });
			this.comboBox_send_encoding.Location = new System.Drawing.Point(278, 416);
			this.comboBox_send_encoding.Name = "comboBox_send_encoding";
			this.comboBox_send_encoding.Size = new System.Drawing.Size(140, 25);
			this.comboBox_send_encoding.TabIndex = 71;
			this.comboBox_recv_encoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_recv_encoding.FormattingEnabled = true;
			this.comboBox_recv_encoding.Items.AddRange(new object[3] { "Binary", "ASCII", "Unicode" });
			this.comboBox_recv_encoding.Location = new System.Drawing.Point(82, 1);
			this.comboBox_recv_encoding.Name = "comboBox_recv_encoding";
			this.comboBox_recv_encoding.Size = new System.Drawing.Size(129, 25);
			this.comboBox_recv_encoding.TabIndex = 72;
			this.label_send_tick.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_send_tick.AutoSize = true;
			this.label_send_tick.ForeColor = System.Drawing.Color.Gray;
			this.label_send_tick.Location = new System.Drawing.Point(214, 526);
			this.label_send_tick.Name = "label_send_tick";
			this.label_send_tick.Size = new System.Drawing.Size(46, 17);
			this.label_send_tick.TabIndex = 73;
			this.label_send_tick.Text = "S-Tick:";
			this.label_recv_tick.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_recv_tick.AutoSize = true;
			this.label_recv_tick.ForeColor = System.Drawing.Color.Gray;
			this.label_recv_tick.Location = new System.Drawing.Point(288, 526);
			this.label_recv_tick.Name = "label_recv_tick";
			this.label_recv_tick.Size = new System.Drawing.Size(47, 17);
			this.label_recv_tick.TabIndex = 74;
			this.label_recv_tick.Text = "R-Tick:";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label_recv_tick);
			base.Controls.Add(this.label_send_tick);
			base.Controls.Add(this.comboBox_recv_encoding);
			base.Controls.Add(this.comboBox_send_encoding);
			base.Controls.Add(this.textBox_send_info);
			base.Controls.Add(this.panel5);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.label_session_count);
			base.Controls.Add(this.comboBox_sessions);
			base.Controls.Add(this.linkLabel_build_message);
			base.Controls.Add(this.label_recv_count);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.label11);
			base.Controls.Add(this.textBox_send_intenal);
			base.Controls.Add(this.checkBox_send_cycle);
			base.Controls.Add(this.checkBox_show_header);
			base.Controls.Add(this.richTextBox_main);
			base.Controls.Add(this.label_send_count);
			base.Controls.Add(this.panel4);
			base.Controls.Add(this.button5);
			base.Controls.Add(this.label8);
			base.Controls.Add(this.checkBox_show_send);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.button_send);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.checkBox_stop_show);
			base.Controls.Add(this.checkBox_auto_return);
			base.Controls.Add(this.panel2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "DebugControl";
			base.Size = new System.Drawing.Size(1003, 548);
			base.Load += new System.EventHandler(DebugControl_Load);
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel5.ResumeLayout(false);
			this.panel5.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
