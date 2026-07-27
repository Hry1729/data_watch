using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Robot.EFORT;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.Robot
{
	public class FormEfort : HslFormContent
	{
		protected CodeExampleControl codeExampleControl;

		private ER7BC10 efortRobot;

		private Thread threadRead;

		private int timeSpeep = 1000;

		private bool isReadPlc = false;

		private IContainer components = null;

		private Panel panel1;

		protected Button button2;

		protected Button button1;

		protected TextBox textBox2;

		private Label label3;

		protected TextBox textBox1;

		private Label label1;

		protected Panel panel2;

		private Button button3;

		private TextBox textBox3;

		private Label label6;

		private Button button_read_data;

		protected UserControlHead userControlHead1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private EfortRobotControl efortRobotControl1;

		private Label label74;

		public FormEfort()
		{
			InitializeComponent();
		}

		private void FormEfort_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			threadRead = new Thread(ThreadReadRobot);
			threadRead.IsBackground = true;
			threadRead.Start();
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void RenderErrorMessage(string msg)
		{
			if (base.InvokeRequired)
			{
				try
				{
					Invoke(new Action<string>(RenderErrorMessage), msg);
					return;
				}
				catch
				{
					return;
				}
			}
			label74.Text = "异常消息：[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + msg;
			label74.BackColor = Color.Blue;
			ThreadPool.QueueUserWorkItem(Reset, null);
		}

		private void Reset(object obj)
		{
			Thread.Sleep(2000);
			label74.Invoke((Action)delegate
			{
				label74.BackColor = Color.Transparent;
			});
		}

		private void FormEfort_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		protected virtual async void button1_Click(object sender, EventArgs e)
		{
			try
			{
				efortRobot = new ER7BC10(textBox1.Text, int.Parse(textBox2.Text));
				efortRobot.ConnectTimeOut = 2000;
				OperateResult connect = await efortRobot.ConnectServerAsync();
				if (connect.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button1.Enabled = false;
					button2.Enabled = true;
					panel2.Enabled = true;
					codeExampleControl.SetCodeText("robot", efortRobot);
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.ToMessageShowString());
				}
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		protected virtual void button2_Click(object sender, EventArgs e)
		{
			isReadPlc = false;
			efortRobot?.ConnectClose();
			button2.Enabled = false;
			panel2.Enabled = false;
			button1.Enabled = true;
			threadRead?.Abort();
			button3.Enabled = true;
		}

		protected virtual OperateResult<EfortData> ReadFromRobot()
		{
			return efortRobot.ReadEfortData();
		}

		private void button_read_short_Click(object sender, EventArgs e)
		{
			OperateResult<EfortData> operateResult = ReadFromRobot();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("读取失败！" + operateResult.Message);
			}
			else
			{
				efortRobotControl1.RenderRobotData(operateResult.Content);
			}
		}

		private void ThreadReadRobot()
		{
			while (true)
			{
				Thread.Sleep(timeSpeep);
				if (isReadPlc)
				{
					OperateResult<EfortData> operateResult = ReadFromRobot();
					if (operateResult.IsSuccess)
					{
						efortRobotControl1.RenderRobotData(operateResult.Content);
					}
					else
					{
						RenderErrorMessage(operateResult.Message);
					}
				}
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			try
			{
				timeSpeep = int.Parse(textBox3.Text);
				isReadPlc = true;
				button3.Enabled = false;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.efortRobotControl1 = new HslCommunicationDemo.Robot.EfortRobotControl();
			this.label74 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button_read_data = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 36);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 43);
			this.panel1.TabIndex = 12;
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(875, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(772, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(302, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(76, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "8008";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(248, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 9);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(174, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.AutoScroll = true;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.efortRobotControl1);
			this.panel2.Controls.Add(this.label74);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox3);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.button_read_data);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(982, 520);
			this.panel2.TabIndex = 13;
			this.efortRobotControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.efortRobotControl1.Location = new System.Drawing.Point(4, 35);
			this.efortRobotControl1.Name = "efortRobotControl1";
			this.efortRobotControl1.Size = new System.Drawing.Size(958, 479);
			this.efortRobotControl1.TabIndex = 109;
			this.label74.ForeColor = System.Drawing.Color.Red;
			this.label74.Location = new System.Drawing.Point(510, 0);
			this.label74.Name = "label74";
			this.label74.Size = new System.Drawing.Size(463, 37);
			this.label74.TabIndex = 108;
			this.label74.Text = "异常信息：";
			this.button3.Location = new System.Drawing.Point(395, 4);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(109, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "开始";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox3.Location = new System.Drawing.Point(289, 6);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(89, 23);
			this.textBox3.TabIndex = 11;
			this.textBox3.Text = "1000";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(213, 8);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 10;
			this.label6.Text = "定时刷新：";
			this.button_read_data.Location = new System.Drawing.Point(11, 3);
			this.button_read_data.Name = "button_read_data";
			this.button_read_data.Size = new System.Drawing.Size(109, 28);
			this.button_read_data.TabIndex = 9;
			this.button_read_data.Text = "刷新数据";
			this.button_read_data.UseVisualStyleBackColor = true;
			this.button_read_data.Click += new System.EventHandler(button_read_short_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "new";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(4, 86);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(996, 556);
			this.tabControl1.TabIndex = 15;
			this.tabPage1.Controls.Add(this.panel2);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(988, 526);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Basic Function";
			this.tabPage1.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormEfort";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "埃夫特机器人(新版，报文对齐)";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormEfort_FormClosing);
			base.Load += new System.EventHandler(FormEfort_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
