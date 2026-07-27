using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core.Pipe;
using HslCommunication.DTU;
using HslCommunication.LogNet;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormDtuServer : HslFormContent
	{
		private ILogNet logNet = null;

		private DTUServer dTUServer = null;

		private Timer timer;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private UserControlHead userControlHead1;

		private ListBox listBox1;

		private Label label5;

		private Label label2;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private ComboBox comboBox1;

		private Label label7;

		private TextBox textBox1;

		private TextBox textBox3;

		private Label label1;

		public FormDtuServer()
		{
			InitializeComponent();
			logNet = new LogNetSingle("");
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			Language(Program.Language);
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			timer = new Timer();
			timer.Tick += Timer_Tick;
			timer.Interval = 1000;
			timer.Start();
			logNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			if (base.IsHandleCreated)
			{
				Invoke((Action)delegate
				{
					textBox1.AppendText(e.HslMessage.ToString() + Environment.NewLine);
				});
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (dTUServer == null)
			{
				return;
			}
			PipeDtuNet[] pipeSessions = dTUServer.GetPipeSessions();
			listBox1.DataSource = pipeSessions.Where((PipeDtuNet m) => m != null).ToArray();
			label2.Text = "Client Count:" + pipeSessions.Length;
			int num = 0;
			for (int i = 0; i < pipeSessions.Length; i++)
			{
				if (pipeSessions[i] != null && !pipeSessions[i].IsConnectError())
				{
					num++;
				}
			}
			label5.Text = "Online Count：" + num;
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Alien Modbus Tcp Read Demo";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
		}

		private void DtuServerStart(int port)
		{
			List<DTUSettingType> list = new List<DTUSettingType>();
			list.Add(new DTUSettingType
			{
				DtuId = "10000",
				DtuType = "ModbusRtuOverTcp",
				JsonParameter = new
				{
					Station = 1
				}.ToJsonString()
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10001",
				DtuType = "ModbusRtuOverTcp",
				JsonParameter = new
				{
					Station = 1
				}.ToJsonString()
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10002",
				DtuType = "ModbusRtuOverTcp",
				JsonParameter = new
				{
					Station = 1
				}.ToJsonString()
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10003",
				DtuType = "ModbusRtuOverTcp",
				JsonParameter = new
				{
					Station = 1
				}.ToJsonString()
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10004",
				DtuType = "ModbusTcpNet",
				JsonParameter = new
				{
					Station = 1
				}.ToJsonString()
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10005",
				DtuType = "MelsecMcNet"
			});
			list.Add(new DTUSettingType
			{
				DtuId = "10006",
				DtuType = "SiemensS7Net",
				JsonParameter = new
				{
					SiemensPLCS = SiemensPLCS.S1200.ToString()
				}.ToJsonString()
			});
			dTUServer = new DTUServer(list);
			dTUServer.OnClientConnected += NetworkAlien_OnClientConnected;
			dTUServer.LogNet = logNet;
			dTUServer.SetPassword(Encoding.ASCII.GetBytes(textBox3.Text));
			dTUServer.ServerStart(port);
			comboBox1.DataSource = list.ToArray();
			if (list.Count > 0)
			{
				comboBox1.SelectedIndex = 0;
			}
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (dTUServer != null && comboBox1.SelectedItem is DTUSettingType dTUSettingType && !string.IsNullOrEmpty(dTUSettingType.DtuId))
			{
				userControlReadWriteOp1.SetReadWriteNet(dTUServer[dTUSettingType.DtuId], "0");
			}
		}

		private void NetworkAlien_OnClientConnected(PipeDtuNet pipe)
		{
			Invoke((Action)delegate
			{
			});
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("端口输入不正确！");
				return;
			}
			try
			{
				DtuServerStart(result);
				DemoUtils.ShowMessage("等待服务器的连接！");
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			dTUServer?.ServerClose();
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
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label7 = new System.Windows.Forms.Label();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 42);
			this.panel1.TabIndex = 0;
			this.textBox3.Location = new System.Drawing.Point(221, 7);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(76, 23);
			this.textBox3.TabIndex = 7;
			this.textBox3.Text = "123456";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(168, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 6;
			this.label1.Text = "密码：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(406, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(309, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "创建";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(74, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(76, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "10010";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(20, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox1);
			this.panel2.Controls.Add(this.userControlReadWriteOp1);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.listBox1);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Location = new System.Drawing.Point(3, 80);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 563);
			this.panel2.TabIndex = 1;
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(339, 3);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(649, 242);
			this.textBox1.TabIndex = 38;
			this.userControlReadWriteOp1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(5, 287);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(987, 275);
			this.userControlReadWriteOp1.TabIndex = 37;
			this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(52, 254);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(481, 25);
			this.comboBox1.TabIndex = 36;
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(7, 258);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(39, 17);
			this.label7.TabIndex = 35;
			this.label7.Text = "List：";
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.listBox1.BackColor = System.Drawing.Color.LightGray;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(4, 3);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(328, 242);
			this.listBox1.TabIndex = 34;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(795, 258);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(95, 17);
			this.label5.TabIndex = 33;
			this.label5.Text = "Online Count：";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(643, 258);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(81, 17);
			this.label2.TabIndex = 32;
			this.label2.Text = "Client Count:";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7885368.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Dtu";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormDtuServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "DTU 服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
