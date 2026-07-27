using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormSerialToTcp : HslFormContent
	{
		private SerialPort SP_ReadData = null;

		private Socket socketCore = null;

		private Socket client = null;

		private byte[] buffer = new byte[2048];

		private IContainer components = null;

		private Panel panel1;

		private ComboBox comboBox1;

		private Label label24;

		private TextBox textBox17;

		private Label label23;

		private TextBox textBox16;

		private Label label22;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private Label label1;

		private Panel panel2;

		private TextBox textBox6;

		private CheckBox checkBox3;

		private Label label7;

		private ComboBox comboBox2;

		private UserControlHead userControlHead1;

		private CheckBox checkBox5;

		private TextBox textBox1;

		private Label label2;

		private CheckBox checkBox1;

		public FormSerialToTcp()
		{
			InitializeComponent();
		}

		private void FormSerialDebug_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox1.SelectedIndex = 0;
			comboBox2.DataSource = SerialPort.GetPortNames();
			try
			{
				comboBox2.SelectedIndex = 0;
			}
			catch
			{
				comboBox2.Text = "COM3";
			}
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "串口调试助手";
				label1.Text = "Com口：";
				label3.Text = "波特率:";
				label22.Text = "数据位:";
				label23.Text = "停止位:";
				label24.Text = "奇偶：";
				button1.Text = "打开串口";
				button2.Text = "关闭串口";
				label7.Text = "数据接收区：";
				checkBox3.Text = "是否显示发送数据";
				comboBox1.DataSource = new string[3] { "无", "奇", "偶" };
			}
			else
			{
				Text = "Serial Debug Tools";
				label1.Text = "Com:";
				label3.Text = "Baud rate:";
				label22.Text = "Data bits:";
				label23.Text = "Stop bits:";
				label24.Text = "parity:";
				button1.Text = "Open";
				button2.Text = "Close";
				label7.Text = "Data receiving Area:";
				checkBox3.Text = "Whether to display send data";
				comboBox1.DataSource = new string[3] { "None", "Odd", "Even" };
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "波特率输入错误！" : "Baud rate input error");
				return;
			}
			if (!int.TryParse(textBox16.Text, out var result2))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "数据位输入错误！" : "Data bits input error");
				return;
			}
			if (!int.TryParse(textBox17.Text, out var result3))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "停止位输入错误！" : "Stop bits input error");
				return;
			}
			SP_ReadData = new SerialPort();
			SP_ReadData.PortName = comboBox2.Text;
			SP_ReadData.BaudRate = result;
			SP_ReadData.DataBits = result2;
			SerialPort sP_ReadData = SP_ReadData;
			int stopBits;
			switch (result3)
			{
			default:
				stopBits = 2;
				break;
			case 1:
				stopBits = 1;
				break;
			case 0:
				stopBits = 0;
				break;
			}
			sP_ReadData.StopBits = (StopBits)stopBits;
			SP_ReadData.Parity = ((comboBox1.SelectedIndex != 0) ? ((comboBox1.SelectedIndex == 1) ? Parity.Odd : Parity.Even) : Parity.None);
			SP_ReadData.RtsEnable = checkBox5.Checked;
			try
			{
				SP_ReadData.DataReceived += SP_ReadData_DataReceived;
				SP_ReadData.Open();
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
			try
			{
				socketCore = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				socketCore.Bind(new IPEndPoint(0L, int.Parse(textBox1.Text)));
				socketCore.Listen(500);
				socketCore.BeginAccept(AsyncAcceptCallback, socketCore);
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			catch (Exception ex2)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + Environment.NewLine + ex2.Message);
			}
		}

		protected void AsyncAcceptCallback(IAsyncResult iar)
		{
			if (!(iar.AsyncState is Socket socket))
			{
				return;
			}
			ClientSession session = new ClientSession();
			try
			{
				client = socket.EndAccept(iar);
				session.Socket = client;
				session.EndPoint = (IPEndPoint)client.RemoteEndPoint;
				client.BeginReceive(buffer, 0, 2048, SocketFlags.None, ReceiveCallBack, session);
				Invoke((Action)delegate
				{
					DemoUtils.AppendTextBox(textBox6, session.EndPoint.ToString(), "Client Online");
				});
			}
			catch (ObjectDisposedException)
			{
				client = null;
				return;
			}
			catch
			{
				client = null;
				client?.Close();
			}
			socket.BeginAccept(AsyncAcceptCallback, socket);
		}

		private void ReceiveCallBack(IAsyncResult ar)
		{
			ClientSession client = ar.AsyncState as ClientSession;
			if (client == null)
			{
				return;
			}
			try
			{
				int num = client.Socket.EndReceive(ar);
				if (num == 0)
				{
					Invoke((Action)delegate
					{
						client.Socket.Close();
						DemoUtils.AppendTextBox(textBox6, client.EndPoint.ToString(), "Client Offline");
					});
					client = null;
					return;
				}
				client.Socket.BeginReceive(buffer, 0, 2048, SocketFlags.None, ReceiveCallBack, client);
				byte[] data = new byte[num];
				Array.Copy(buffer, 0, data, 0, num);
				SP_ReadData.Write(data, 0, data.Length);
				Invoke((Action)delegate
				{
					string empty = string.Empty;
					DemoUtils.AppendTextBox(message: (!checkBox1.Checked) ? SoftBasic.GetAsciiStringRender(data) : SoftBasic.ByteToHexString(data, ' '), textBox: textBox6, key: "Tcp-Serial");
				});
			}
			catch
			{
				Invoke((Action)delegate
				{
					this.client = null;
					DemoUtils.AppendTextBox(textBox6, "Client Offline", (Program.Language == 1) ? "服务器断开连接。" : "DisConnect from remote");
				});
			}
		}

		private void SP_ReadData_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			List<byte> buffer = new List<byte>();
			byte[] array = new byte[1024];
			while (true)
			{
				Thread.Sleep(20);
				if (SP_ReadData.BytesToRead < 1)
				{
					break;
				}
				int num = SP_ReadData.Read(array, 0, Math.Min(SP_ReadData.BytesToRead, array.Length));
				byte[] array2 = new byte[num];
				Array.Copy(array, 0, array2, 0, num);
				buffer.AddRange(array2);
			}
			if (buffer.Count != 0)
			{
				try
				{
					byte[] array3 = buffer.ToArray();
					client.Send(array3, 0, array3.Length, SocketFlags.None);
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Client Send Failed! " + ex.Message);
					return;
				}
				Invoke((Action)delegate
				{
					string empty = string.Empty;
					DemoUtils.AppendTextBox(message: (!checkBox1.Checked) ? SoftBasic.GetAsciiStringRender(buffer.ToArray()) : SoftBasic.ByteToHexString(buffer.ToArray(), ' '), textBox: textBox6, key: "Serial-Tcp");
				});
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				SP_ReadData.Close();
				button2.Enabled = false;
				button1.Enabled = true;
				panel2.Enabled = false;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void FormSerialToTcp_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.checkBox5 = new System.Windows.Forms.CheckBox();
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.label22 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.checkBox5);
			this.panel1.Controls.Add(this.comboBox2);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.label24);
			this.panel1.Controls.Add(this.textBox17);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(4, 37);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(995, 74);
			this.panel1.TabIndex = 7;
			this.textBox1.Location = new System.Drawing.Point(81, 39);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(118, 23);
			this.textBox1.TabIndex = 19;
			this.textBox1.Text = "502";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(10, 42);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(69, 17);
			this.label2.TabIndex = 18;
			this.label2.Text = "Tcp Port：";
			this.checkBox5.AutoSize = true;
			this.checkBox5.Location = new System.Drawing.Point(578, 12);
			this.checkBox5.Name = "checkBox5";
			this.checkBox5.Size = new System.Drawing.Size(84, 21);
			this.checkBox5.TabIndex = 17;
			this.checkBox5.Text = "RtsEnable";
			this.checkBox5.UseVisualStyleBackColor = true;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Location = new System.Drawing.Point(62, 7);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(84, 25);
			this.comboBox2.TabIndex = 16;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[3] { "无", "奇", "偶" });
			this.comboBox1.Location = new System.Drawing.Point(503, 9);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(59, 25);
			this.comboBox1.TabIndex = 15;
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(460, 12);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(44, 17);
			this.label24.TabIndex = 14;
			this.label24.Text = "奇偶：";
			this.textBox17.Location = new System.Drawing.Point(430, 9);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(23, 23);
			this.textBox17.TabIndex = 13;
			this.textBox17.Text = "1";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(371, 12);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(56, 17);
			this.label23.TabIndex = 12;
			this.label23.Text = "停止位：";
			this.textBox16.Location = new System.Drawing.Point(334, 9);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(24, 23);
			this.textBox16.TabIndex = 11;
			this.textBox16.Text = "8";
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(272, 12);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(56, 17);
			this.label22.TabIndex = 10;
			this.label22.Text = "数据位：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(331, 37);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭转换";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(219, 37);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开转换";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(219, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(47, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "9600";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(152, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "波特率：";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Com口：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.checkBox1);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.checkBox3);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(4, 116);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(995, 523);
			this.panel2.TabIndex = 13;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(334, 3);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(135, 21);
			this.checkBox1.TabIndex = 22;
			this.checkBox1.Text = "是否使用二进制通信";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox6.Location = new System.Drawing.Point(11, 30);
			this.textBox6.Multiline = true;
			this.textBox6.Name = "textBox6";
			this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox6.Size = new System.Drawing.Size(976, 486);
			this.textBox6.TabIndex = 21;
			this.checkBox3.AutoSize = true;
			this.checkBox3.Checked = true;
			this.checkBox3.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox3.Location = new System.Drawing.Point(146, 3);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(123, 21);
			this.checkBox3.TabIndex = 19;
			this.checkBox3.Text = "是否显示发送数据";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 4);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(80, 17);
			this.label7.TabIndex = 18;
			this.label7.Text = "数据接收区：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "serial";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSerialToTcp";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "串口转TCP调试工具";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSerialToTcp_FormClosing);
			base.Load += new System.EventHandler(FormSerialDebug_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
