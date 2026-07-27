using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Toledo;
using HslCommunicationDemo.DemoControl;
using HslControls;

namespace HslCommunicationDemo.Toledo
{
	public class FormToledoTcpServer : HslFormContent
	{
		private ToledoTcpServer toledoTcpServer;

		private long receiveTimes = 0L;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel2;

		private TextBox textBox6;

		private CheckBox checkBox4;

		private Label label7;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private ToledoDataControl toledoDataControl1;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private TextBox textBox3;

		private Label label4;

		private Button button3;

		private TextBox textBox4;

		private Label label5;

		private HslDialPlate hslDialPlate1;

		private HslCurve hslCurve1;

		private Label label8;

		private Button button4;

		private Button button5;

		private TextBox textBox_serialPort;

		private Label label6;

		private BorderPanel borderPanel2;

		private RadioButton radioButton_udp;

		private RadioButton radioButton_tcp;

		private TextBox textBox_time;

		private Label label10;

		private TextBox textBox_pipe;

		private Label label9;

		public FormToledoTcpServer()
		{
			InitializeComponent();
			toledoTcpServer = new ToledoTcpServer();
			toledoTcpServer.OnToledoStandardDataReceived += ToledoTcpServer_OnToledoStandardDataReceived;
		}

		private void FormToledoTcpServer_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			Language(Program.Language);
			hslCurve1.SetLeftCurve("重量", null, Color.Red);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Toledo Tcp Server Demo";
				button1.Text = "Start Server";
				button2.Text = "Close Server";
				label7.Text = "Data Area:";
				checkBox4.Text = "Whether to show time";
				label3.Text = "Port:";
				label6.Text = "Serial:";
				button5.Text = "Open Serial";
				button4.Text = "Close Serial";
			}
		}

		private void FormToledoTcpServer_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
			if (!button5.Enabled)
			{
				button4_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "端口号输入错误！" : "Port input wrong");
				return;
			}
			try
			{
				toledoTcpServer.ServerStart(result);
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void ToledoTcpServer_OnToledoStandardDataReceived(object sender, ToledoStandardData e)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<object, ToledoStandardData>(ToledoTcpServer_OnToledoStandardDataReceived), sender, e);
				return;
			}
			if (sender is PipeSession pipeSession)
			{
				textBox_pipe.Text = pipeSession.Communication.ToString();
			}
			textBox_time.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			receiveTimes++;
			StringBuilder stringBuilder = new StringBuilder();
			if (checkBox4.Checked)
			{
				stringBuilder.Append(DateTime.Now.ToString() + Environment.NewLine);
			}
			stringBuilder.Append(e.ToJsonString() + Environment.NewLine);
			textBox6.Text = stringBuilder.ToString();
			textBox1.Text = e.SourceData.ToHexString(' ');
			textBox3.Text = SoftBasic.GetAsciiStringRender(e.SourceData);
			toledoDataControl1.SetToledoData(e);
			hslCurve1.AddCurveData("重量", e.Weight);
			hslDialPlate1.Value = e.Weight;
			label2.Text = "Receive Times:" + receiveTimes;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				toledoTcpServer?.ServerClose();
				button2.Enabled = false;
				button1.Enabled = true;
				panel2.Enabled = false;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (float.TryParse(textBox4.Text, out var result))
			{
				hslCurve1.ReferenceAxisLeft.Max = result;
				hslDialPlate1.MaxValue = result;
			}
			else
			{
				DemoUtils.ShowMessage("Input Wrong");
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = toledoTcpServer.StartSerialSlave(textBox_serialPort.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Serial open failed: " + operateResult.Message);
				return;
			}
			panel2.Enabled = true;
			button4.Enabled = true;
			button5.Enabled = false;
		}

		private void button4_Click(object sender, EventArgs e)
		{
			toledoTcpServer.CloseSerialSlave();
			button4.Enabled = false;
			button5.Enabled = true;
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox_time = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_pipe = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.hslDialPlate1 = new HslControls.HslDialPlate();
			this.hslCurve1 = new HslControls.HslCurve();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.toledoDataControl1 = new HslCommunicationDemo.Toledo.ToledoDataControl();
			this.checkBox4 = new System.Windows.Forms.CheckBox();
			this.label7 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button4 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox_serialPort = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.borderPanel2 = new HslCommunicationDemo.DemoControl.BorderPanel();
			this.radioButton_udp = new System.Windows.Forms.RadioButton();
			this.radioButton_tcp = new System.Windows.Forms.RadioButton();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel2.SuspendLayout();
			this.panel1.SuspendLayout();
			this.borderPanel2.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Toledo Tcp Server";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 16;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_time);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.textBox_pipe);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.hslDialPlate1);
			this.panel2.Controls.Add(this.hslCurve1);
			this.panel2.Controls.Add(this.textBox3);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.textBox1);
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.toledoDataControl1);
			this.panel2.Controls.Add(this.checkBox4);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(3, 78);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 566);
			this.panel2.TabIndex = 19;
			this.textBox_time.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_time.Location = new System.Drawing.Point(406, 39);
			this.textBox_time.Name = "textBox_time";
			this.textBox_time.Size = new System.Drawing.Size(354, 23);
			this.textBox_time.TabIndex = 52;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(352, 42);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(39, 17);
			this.label10.TabIndex = 51;
			this.label10.Text = "Time:";
			this.textBox_pipe.Location = new System.Drawing.Point(62, 39);
			this.textBox_pipe.Name = "textBox_pipe";
			this.textBox_pipe.Size = new System.Drawing.Size(269, 23);
			this.textBox_pipe.TabIndex = 50;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 42);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(36, 17);
			this.label9.TabIndex = 49;
			this.label9.Text = "Pipe:";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(4, 543);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(272, 17);
			this.label8.TabIndex = 48;
			this.label8.Text = "支持连续标准输出的格式，支持连续扩展输出格式";
			this.button3.Location = new System.Drawing.Point(310, 132);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(91, 28);
			this.button3.TabIndex = 32;
			this.button3.Text = "Setting";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Location = new System.Drawing.Point(194, 135);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(100, 23);
			this.textBox4.TabIndex = 31;
			this.textBox4.Text = "100";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(116, 138);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(72, 17);
			this.label5.TabIndex = 30;
			this.label5.Text = "Max Value:";
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox6.Location = new System.Drawing.Point(555, 137);
			this.textBox6.Multiline = true;
			this.textBox6.Name = "textBox6";
			this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox6.Size = new System.Drawing.Size(209, 379);
			this.textBox6.TabIndex = 21;
			this.hslDialPlate1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.hslDialPlate1.BorderColor = System.Drawing.Color.FromArgb(0, 0, 0);
			this.hslDialPlate1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.hslDialPlate1.Location = new System.Drawing.Point(792, 329);
			this.hslDialPlate1.Name = "hslDialPlate1";
			this.hslDialPlate1.Size = new System.Drawing.Size(192, 188);
			this.hslDialPlate1.TabIndex = 29;
			this.hslCurve1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslCurve1.FontCalibration = new System.Drawing.Font("微软雅黑", 9f);
			this.hslCurve1.Location = new System.Drawing.Point(3, 184);
			this.hslCurve1.Name = "hslCurve1";
			this.hslCurve1.Size = new System.Drawing.Size(546, 347);
			this.hslCurve1.TabIndex = 28;
			this.hslCurve1.Title = "重力曲线图";
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(62, 97);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(698, 23);
			this.textBox3.TabIndex = 27;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(8, 99);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(42, 17);
			this.label4.TabIndex = 26;
			this.label4.Text = "ASCII:";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(324, 11);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(93, 17);
			this.label2.TabIndex = 25;
			this.label2.Text = "Receive Times:";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(62, 70);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(698, 23);
			this.textBox1.TabIndex = 24;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 73);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(33, 17);
			this.label1.TabIndex = 23;
			this.label1.Text = "Hex:";
			this.toledoDataControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.toledoDataControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.toledoDataControl1.Location = new System.Drawing.Point(770, 3);
			this.toledoDataControl1.Name = "toledoDataControl1";
			this.toledoDataControl1.Size = new System.Drawing.Size(216, 320);
			this.toledoDataControl1.TabIndex = 22;
			this.checkBox4.AutoSize = true;
			this.checkBox4.Checked = true;
			this.checkBox4.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox4.Location = new System.Drawing.Point(103, 10);
			this.checkBox4.Name = "checkBox4";
			this.checkBox4.Size = new System.Drawing.Size(99, 21);
			this.checkBox4.TabIndex = 20;
			this.checkBox4.Text = "是否显示时间";
			this.checkBox4.UseVisualStyleBackColor = true;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(80, 17);
			this.label7.TabIndex = 18;
			this.label7.Text = "数据接收区：";
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button4);
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.textBox_serialPort);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.borderPanel2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 41);
			this.panel1.TabIndex = 18;
			this.button4.Enabled = false;
			this.button4.Location = new System.Drawing.Point(849, 5);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(102, 28);
			this.button4.TabIndex = 40;
			this.button4.Text = "关闭串口";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button5.Location = new System.Drawing.Point(744, 5);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(99, 28);
			this.button5.TabIndex = 39;
			this.button5.Text = "打开串口";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox_serialPort.Location = new System.Drawing.Point(572, 8);
			this.textBox_serialPort.Name = "textBox_serialPort";
			this.textBox_serialPort.Size = new System.Drawing.Size(162, 23);
			this.textBox_serialPort.TabIndex = 38;
			this.textBox_serialPort.Text = "COM1-9600-8-N-1";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(511, 11);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 37;
			this.label6.Text = "串口：";
			this.borderPanel2.Controls.Add(this.radioButton_udp);
			this.borderPanel2.Controls.Add(this.radioButton_tcp);
			this.borderPanel2.Location = new System.Drawing.Point(147, 5);
			this.borderPanel2.Name = "borderPanel2";
			this.borderPanel2.Size = new System.Drawing.Size(131, 28);
			this.borderPanel2.TabIndex = 36;
			this.radioButton_udp.AutoSize = true;
			this.radioButton_udp.Location = new System.Drawing.Point(70, 3);
			this.radioButton_udp.Name = "radioButton_udp";
			this.radioButton_udp.Size = new System.Drawing.Size(51, 21);
			this.radioButton_udp.TabIndex = 1;
			this.radioButton_udp.Text = "Udp";
			this.radioButton_udp.UseVisualStyleBackColor = true;
			this.radioButton_tcp.AutoSize = true;
			this.radioButton_tcp.Checked = true;
			this.radioButton_tcp.Location = new System.Drawing.Point(6, 3);
			this.radioButton_tcp.Name = "radioButton_tcp";
			this.radioButton_tcp.Size = new System.Drawing.Size(47, 21);
			this.radioButton_tcp.TabIndex = 0;
			this.radioButton_tcp.TabStop = true;
			this.radioButton_tcp.Text = "Tcp";
			this.radioButton_tcp.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(394, 5);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(102, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(289, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(99, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(69, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(70, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "6000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口：";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormToledoTcpServer";
			this.Text = "托利多网口调试工具";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormToledoTcpServer_FormClosing);
			base.Load += new System.EventHandler(FormToledoTcpServer_Load);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.borderPanel2.ResumeLayout(false);
			this.borderPanel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
