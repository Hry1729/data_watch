using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Profinet.AllenBradley;
using HslCommunication.Profinet.Melsec;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormMelsecCipNet : HslFormContent
	{
		private MelsecCipNet cip = null;

		private IContainer components = null;

		private Panel panel1;

		private Label label1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private GroupBox groupBox4;

		private TextBox textBox11;

		private Label label14;

		private Button button26;

		private TextBox textBox13;

		private Label label16;

		private GroupBox groupBox3;

		private TextBox textBox10;

		private Label label13;

		private Button button25;

		private TextBox textBox6;

		private Label label11;

		private Label label22;

		private TextBox textBox9;

		private Label label12;

		private TextBox textBox15;

		private Label label23;

		private UserControlHead userControlHead1;

		private TextBox textBox12;

		private Label label2;

		private Button button3;

		private Button button4;

		private GroupBox groupBox5;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private Label label6;

		private Button button5;

		private TextBox textBox3;

		private Label label5;

		private TextBox textBox4;

		private Label label7;

		public FormMelsecCipNet()
		{
			InitializeComponent();
			cip = new MelsecCipNet("192.168.0.110");
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			panel2.Enabled = false;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "AllenBrandly Read PLC Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label11.Text = "Address:";
				button25.Text = "Bulk Read";
				label13.Text = "Results:";
				label16.Text = "Message:";
				label14.Text = "Results:";
				button26.Text = "Read";
				button3.Text = "Build";
				label2.Text = "Start:";
				groupBox3.Text = "Bulk Read test";
				groupBox4.Text = "CIP reading test, hex string needs to be filled in";
				groupBox5.Text = "Special function test";
				label22.Text = "Tag name, if the bool array is of type int, access begin with \"i=\"";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			if (!byte.TryParse(textBox15.Text, out var result2))
			{
				DemoUtils.ShowMessage(DemoUtils.SlotInputWrong);
				return;
			}
			cip.IpAddress = textBox_ip.Text;
			cip.Port = result;
			cip.Slot = result2;
			cip.LogNet = base.LogNet;
			try
			{
				OperateResult operateResult = cip.ConnectServer();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					userControlReadWriteOp1.SetReadWriteNet(cip, "A1", true, 1);
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			cip.ConnectClose();
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
		}

		private void button25_Click(object sender, EventArgs e)
		{
			try
			{
				OperateResult<byte[]> operateResult = null;
				operateResult = (textBox6.Text.Contains(";") ? cip.Read(textBox6.Text.Split(';')) : cip.ReadSegment(textBox6.Text, ushort.Parse(textBox12.Text), ushort.Parse(textBox9.Text)));
				if (operateResult.IsSuccess)
				{
					textBox10.Text = "Result：" + SoftBasic.ByteToHexString(operateResult.Content);
				}
				else
				{
					DemoUtils.ShowMessage("Read failed：" + operateResult.ToMessageShowString());
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Read failed：" + ex.Message);
			}
		}

		private void button26_Click(object sender, EventArgs e)
		{
			OperateResult<byte[]> operateResult = cip.ReadCipFromServer(SoftBasic.HexStringToBytes(textBox13.Text));
			if (operateResult.IsSuccess)
			{
				textBox11.Text = "Result：" + SoftBasic.ByteToHexString(operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read failed：" + operateResult.ToMessageShowString());
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult<byte[]> operateResult = cip.ReadEipFromServer(SoftBasic.HexStringToBytes(textBox13.Text));
			if (operateResult.IsSuccess)
			{
				textBox11.Text = "Result：" + SoftBasic.ByteToHexString(operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read failed：" + operateResult.ToMessageShowString());
			}
		}

		private void Button3_Click(object sender, EventArgs e)
		{
			try
			{
				byte[] inBytes = AllenBradleyHelper.PackRequestReadSegment(textBox6.Text, ushort.Parse(textBox12.Text), ushort.Parse(textBox9.Text));
				textBox10.Text = "Result：" + SoftBasic.ByteToHexString(inBytes, ' ');
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Build failed：" + ex.Message);
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> operateResult = cip.ReadBoolArray(textBox3.Text);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = $"Result[{DateTime.Now:HH:mm:ss}]：" + SoftBasic.ArrayFormat(operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read failed：" + operateResult.ToMessageShowString());
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSlot, textBox15.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlSlot).Value;
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
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.label22 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.button26 = new System.Windows.Forms.Button();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.label16 = new System.Windows.Forms.Label();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.button25 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox5.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox3.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 42);
			this.panel1.TabIndex = 0;
			this.textBox15.Location = new System.Drawing.Point(389, 9);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(32, 23);
			this.textBox15.TabIndex = 9;
			this.textBox15.Text = "0";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(345, 12);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(41, 17);
			this.label23.TabIndex = 8;
			this.label23.Text = "slot：";
			this.label22.Location = new System.Drawing.Point(760, 2);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(213, 45);
			this.label22.TabIndex = 7;
			this.label22.Text = "变量的标签名，bool数组如果是int类型，则带i=开头访问";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(575, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(482, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(87, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(285, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(52, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "44818";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(231, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(62, 9);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(163, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteOp1);
			this.panel2.Controls.Add(this.groupBox5);
			this.panel2.Controls.Add(this.groupBox4);
			this.panel2.Controls.Add(this.groupBox3);
			this.panel2.Location = new System.Drawing.Point(3, 81);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 562);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteOp1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(4, 2);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(988, 240);
			this.userControlReadWriteOp1.TabIndex = 5;
			this.groupBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox5.Controls.Add(this.textBox4);
			this.groupBox5.Controls.Add(this.label7);
			this.groupBox5.Controls.Add(this.label6);
			this.groupBox5.Controls.Add(this.button5);
			this.groupBox5.Controls.Add(this.textBox3);
			this.groupBox5.Controls.Add(this.label5);
			this.groupBox5.Location = new System.Drawing.Point(573, 243);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(419, 314);
			this.groupBox5.TabIndex = 4;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "特殊功能测试";
			this.textBox4.Location = new System.Drawing.Point(60, 76);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(341, 186);
			this.textBox4.TabIndex = 14;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 78);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 13;
			this.label7.Text = "结果：";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(6, 19);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(255, 17);
			this.label6.TabIndex = 12;
			this.label6.Text = "批量读取bool的方法，调用：ReadBoolArray";
			this.button5.Location = new System.Drawing.Point(276, 36);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(125, 28);
			this.button5.TabIndex = 11;
			this.button5.Text = "读取";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox3.Location = new System.Drawing.Point(60, 39);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(199, 23);
			this.textBox3.TabIndex = 10;
			this.textBox3.Text = "A1";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(6, 42);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(44, 17);
			this.label5.TabIndex = 9;
			this.label5.Text = "地址：";
			this.groupBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox4.Controls.Add(this.button4);
			this.groupBox4.Controls.Add(this.textBox11);
			this.groupBox4.Controls.Add(this.label14);
			this.groupBox4.Controls.Add(this.button26);
			this.groupBox4.Controls.Add(this.textBox13);
			this.groupBox4.Controls.Add(this.label16);
			this.groupBox4.Location = new System.Drawing.Point(4, 403);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(563, 154);
			this.groupBox4.TabIndex = 3;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "CIP报文读取测试，此处需要填入完整的16进制报文字符串";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(509, 24);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(48, 28);
			this.button4.TabIndex = 11;
			this.button4.Text = "eip";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox11.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox11.Location = new System.Drawing.Point(63, 60);
			this.textBox11.Multiline = true;
			this.textBox11.Name = "textBox11";
			this.textBox11.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox11.Size = new System.Drawing.Size(494, 88);
			this.textBox11.TabIndex = 10;
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(9, 62);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(44, 17);
			this.label14.TabIndex = 9;
			this.label14.Text = "结果：";
			this.button26.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button26.Location = new System.Drawing.Point(455, 24);
			this.button26.Name = "button26";
			this.button26.Size = new System.Drawing.Size(48, 28);
			this.button26.TabIndex = 8;
			this.button26.Text = "cip";
			this.button26.UseVisualStyleBackColor = true;
			this.button26.Click += new System.EventHandler(button26_Click);
			this.textBox13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox13.Location = new System.Drawing.Point(63, 27);
			this.textBox13.Name = "textBox13";
			this.textBox13.Size = new System.Drawing.Size(386, 23);
			this.textBox13.TabIndex = 5;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(9, 30);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(44, 17);
			this.label16.TabIndex = 4;
			this.label16.Text = "报文：";
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Controls.Add(this.button3);
			this.groupBox3.Controls.Add(this.textBox12);
			this.groupBox3.Controls.Add(this.label2);
			this.groupBox3.Controls.Add(this.textBox9);
			this.groupBox3.Controls.Add(this.label12);
			this.groupBox3.Controls.Add(this.textBox10);
			this.groupBox3.Controls.Add(this.label13);
			this.groupBox3.Controls.Add(this.button25);
			this.groupBox3.Controls.Add(this.textBox6);
			this.groupBox3.Controls.Add(this.label11);
			this.groupBox3.Location = new System.Drawing.Point(4, 243);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(563, 154);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "批量读取测试，分号间隔";
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(475, 24);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(82, 28);
			this.button3.TabIndex = 15;
			this.button3.Text = "报文生成";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(Button3_Click);
			this.textBox12.Location = new System.Drawing.Point(229, 27);
			this.textBox12.Name = "textBox12";
			this.textBox12.Size = new System.Drawing.Size(34, 23);
			this.textBox12.TabIndex = 14;
			this.textBox12.Text = "0";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(178, 30);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 13;
			this.label2.Text = "起始：";
			this.textBox9.Location = new System.Drawing.Point(320, 27);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(35, 23);
			this.textBox9.TabIndex = 12;
			this.textBox9.Text = "1";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(266, 30);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 11;
			this.label12.Text = "长度：";
			this.textBox10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox10.Location = new System.Drawing.Point(63, 60);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox10.Size = new System.Drawing.Size(494, 88);
			this.textBox10.TabIndex = 10;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(9, 62);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 9;
			this.label13.Text = "结果：";
			this.button25.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button25.Location = new System.Drawing.Point(407, 24);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(62, 28);
			this.button25.TabIndex = 8;
			this.button25.Text = "读取";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.textBox6.Location = new System.Drawing.Point(63, 27);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(111, 23);
			this.textBox6.TabIndex = 5;
			this.textBox6.Text = "A1;A2";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(9, 30);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 4;
			this.label11.Text = "地址：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/9607929.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Melsec CIP";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMelsecCipNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Melsec Cip访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
