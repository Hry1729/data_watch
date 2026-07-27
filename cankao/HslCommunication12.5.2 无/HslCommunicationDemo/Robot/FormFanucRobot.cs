using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Robot.FANUC;
using HslCommunicationDemo.DemoControl;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo.Robot
{
	public class FormFanucRobot : HslFormContent
	{
		private string[] encodingLists;

		private CodeExampleControl codeExampleControl;

		private FanucInterfaceNet fanuc;

		private int w_sdo = 0;

		private int w_sdi = 0;

		private int w_rdo = 0;

		private int w_rdi = 0;

		private int w_go = 0;

		private int w_gi = 0;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private Button button_read_short;

		private UserControlHead userControlHead1;

		private TextBox textBox8;

		private Label label42;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private Button button4;

		private TabPage tabPage2;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private GroupBox groupBox1;

		private TextBox textBox3;

		private Button button3;

		private Button button5;

		private Button button6;

		private Button button7;

		private Button button8;

		private Button button9;

		private Button button10;

		private Button button11;

		private Button button12;

		private Button button13;

		private Button button14;

		private Button button15;

		private Button button16;

		private Button button17;

		private Button button18;

		private Button button19;

		private Button button21;

		private Button button20;

		private Button button22;

		private Button button24;

		private Button button23;

		private Button button25;

		private Button button26;

		private Button button27;

		private Button button28;

		private Button button29;

		private Button button30;

		private Label label2;

		private ComboBox comboBox1;

		private Button button31;

		private Label label4;

		private TextBox textBox6;

		private Label label7;

		private TextBox textBox5;

		private Label label6;

		private TextBox textBox4;

		private Label label5;

		private Button button32;

		private Label label8;

		private LinkLabel linkLabel1;

		private ComboBox comboBox_encoding;

		private Label label9;

		public FormFanucRobot()
		{
			InitializeComponent();
		}

		private void FormEfort_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			if (Program.Language == 2)
			{
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label2.Text = "The writing operation needs to be careful and the data needs to be checked.";
				tabPage1.Text = "General read";
				tabPage2.Text = "Professional reading and writing";
				groupBox1.Text = "Special Function Test";
			}
			comboBox1.DataSource = (from m in typeof(FanucData).GetProperties()
				select m.Name).ToArray();
			linkLabel1.LinkClicked += LinkLabel1_LinkClicked;
			encodingLists = new string[6] { "Default", "ASCII", "UTF8", "Unicode", "BigEndianUnicode", "GB2312" };
			comboBox_encoding.DataSource = encodingLists;
			comboBox_encoding.SelectedIndex = 0;
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private Encoding GetEncoding()
		{
			string text = ((comboBox_encoding.SelectedItem != null) ? comboBox_encoding.SelectedItem.ToString() : comboBox_encoding.Text);
			switch (text)
			{
			case "Default":
				return Encoding.Default;
			case "ASCII":
				return Encoding.ASCII;
			case "UTF8":
				return Encoding.UTF8;
			case "Unicode":
				return Encoding.Unicode;
			case "BigEndianUnicode":
				return Encoding.BigEndianUnicode;
			default:
				return Encoding.GetEncoding(text);
			}
		}

		private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start(linkLabel1.Text);
		}

		private void FormFanucRobot_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			try
			{
				fanuc = new FanucInterfaceNet(textBox1.Text, int.Parse(textBox2.Text));
				fanuc.ConnectTimeOut = 2000;
				fanuc.LogNet = base.LogNet;
				fanuc.StringEncoding = GetEncoding();
				button1.Enabled = false;
				if ((await fanuc.ConnectServerAsync()).IsSuccess)
				{
					DemoUtils.ShowMessage("连接成功");
					button1.Enabled = false;
					button2.Enabled = true;
					panel2.Enabled = true;
					userControlReadWriteOp1.SetReadWriteNet(fanuc, "D1", true);
					codeExampleControl.SetCodeText("robot", fanuc, "StringEncoding");
				}
				else
				{
					button1.Enabled = true;
					DemoUtils.ShowMessage("连接失败");
				}
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			fanuc?.ConnectClose();
			button2.Enabled = false;
			panel2.Enabled = false;
			button1.Enabled = true;
		}

		private async void button_read_short_Click(object sender, EventArgs e)
		{
			OperateResult<FanucData> read = await fanuc.ReadFanucDataAsync();
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
			else
			{
				textBox8.Text = JObject.FromObject(read.Content).ToString();
			}
		}

		private async void button4_Click(object sender, EventArgs e)
		{
			OperateResult<FanucData> read = await fanuc.ReadFanucDataAsync();
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
			else
			{
				textBox8.Text = read.Content.ToString();
			}
		}

		private void button31_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = fanuc.ReadString(comboBox1.Text.ToString());
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed！" + operateResult.Message);
			}
			else
			{
				textBox8.Text = operateResult.Content;
			}
		}

		private string TransBoolArrayToString(bool[] array)
		{
			return SoftBasic.ArrayFormat(array.Select((bool m) => m ? 1 : 0).ToArray()).Replace(",", "");
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDOAsync(1, 100);
			if (read.IsSuccess)
			{
				textBox3.Text = "SDO:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button5_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDOAsync(10001, 100);
			if (read.IsSuccess)
			{
				textBox3.Text = "SDO[1000x]:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button6_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDOAsync(11001, 100);
			if (read.IsSuccess)
			{
				textBox3.Text = "SDO[1100x]:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button7_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDIAsync(1, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "SDI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button8_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadRDOAsync(1, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "RDO:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button9_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadRDIAsync(1, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "RDI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button10_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSOAsync(0, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "SO:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button11_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSIAsync(0, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "SI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button12_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadUOAsync(1, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "UO:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button13_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadUIAsync(1, 10);
			if (read.IsSuccess)
			{
				textBox3.Text = "UI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button14_Click(object sender, EventArgs e)
		{
			OperateResult<ushort[]> read = await fanuc.ReadGOAsync(1, 3);
			if (read.IsSuccess)
			{
				textBox3.Text = "GO:" + SoftBasic.ArrayFormat(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button15_Click(object sender, EventArgs e)
		{
			OperateResult<ushort[]> read = await fanuc.ReadGOAsync(10001, 3);
			if (read.IsSuccess)
			{
				textBox3.Text = "GO[1000x]:" + SoftBasic.ArrayFormat(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button16_Click(object sender, EventArgs e)
		{
			OperateResult<ushort[]> read = await fanuc.ReadGIAsync(1, 3);
			if (read.IsSuccess)
			{
				textBox3.Text = "GI:" + SoftBasic.ArrayFormat(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button17_Click(object sender, EventArgs e)
		{
			OperateResult<ushort[]> read = await fanuc.ReadGOAsync(1001, 3);
			if (read.IsSuccess)
			{
				textBox3.Text = "AO:" + SoftBasic.ArrayFormat(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button18_Click(object sender, EventArgs e)
		{
			OperateResult<ushort[]> read = await fanuc.ReadGIAsync(1001, 2);
			if (read.IsSuccess)
			{
				textBox3.Text = "AI:" + SoftBasic.ArrayFormat(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button19_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDOAsync(8001, 5);
			if (read.IsSuccess)
			{
				textBox3.Text = "WO:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button20_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDIAsync(8001, 5);
			if (read.IsSuccess)
			{
				textBox3.Text = "WI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button21_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> read = await fanuc.ReadSDIAsync(8401, 1);
			if (read.IsSuccess)
			{
				textBox3.Text = "WSI:" + TransBoolArrayToString(read.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed！" + read.Message);
			}
		}

		private async void button22_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
			{
				return;
			}
			int intStartIndex = ((sender == button22) ? 1 : ((sender == button23) ? 10001 : 11001));
			w_sdo++;
			bool[] value = new bool[100];
			if (w_sdo % 2 == 1)
			{
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = true;
				}
			}
			OperateResult write = await fanuc.WriteSDOAsync((ushort)intStartIndex, value);
			if (write.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success！value:" + value[0]);
			}
			else
			{
				DemoUtils.ShowMessage("Write Failed！" + write.Message);
			}
		}

		private async void button25_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
			{
				return;
			}
			w_sdi++;
			bool[] value = new bool[10];
			if (w_sdi % 2 == 1)
			{
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = true;
				}
			}
			OperateResult write = await fanuc.WriteSDIAsync(1, value);
			if (write.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success！value:" + value[0]);
			}
			else
			{
				DemoUtils.ShowMessage("Write Failed！" + write.Message);
			}
		}

		private async void button26_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
			{
				return;
			}
			w_rdo++;
			bool[] value = new bool[8];
			if (w_rdo % 2 == 1)
			{
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = true;
				}
			}
			OperateResult write = await fanuc.WriteRDOAsync(1, value);
			if (write.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success！value:" + value[0]);
			}
			else
			{
				DemoUtils.ShowMessage("Write Failed！" + write.Message);
			}
		}

		private async void button27_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
			{
				return;
			}
			w_rdi++;
			bool[] value = new bool[8];
			if (w_rdi % 2 == 1)
			{
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = true;
				}
			}
			OperateResult write = await fanuc.WriteRDIAsync(1, value);
			if (write.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success！value:" + value[0]);
			}
			else
			{
				DemoUtils.ShowMessage("Write Failed！" + write.Message);
			}
		}

		private async void button28_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.Cancel)
			{
				int intStartIndex = ((sender == button28) ? 1 : 10001);
				w_go++;
				ushort[] value = new ushort[3];
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = (ushort)((w_go % 3 + 1) * 11);
				}
				OperateResult write = await fanuc.WriteGOAsync((ushort)intStartIndex, value);
				if (write.IsSuccess)
				{
					DemoUtils.ShowMessage("Write Success！value:" + value[0]);
				}
				else
				{
					DemoUtils.ShowMessage("Write Failed！" + write.Message);
				}
			}
		}

		private async void button30_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage(StringResources.Language.WriteWarning, "Write Check", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.Cancel)
			{
				w_gi++;
				ushort[] value = new ushort[3];
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = (ushort)((w_gi % 3 + 1) * 11);
				}
				OperateResult write = await fanuc.WriteGIAsync(1, value);
				if (write.IsSuccess)
				{
					DemoUtils.ShowMessage("Write Success！value:" + value[0]);
				}
				else
				{
					DemoUtils.ShowMessage("Write Failed！" + write.Message);
				}
			}
		}

		private void button32_Click(object sender, EventArgs e)
		{
			try
			{
				OperateResult<byte[]> operateResult = fanuc.Read(byte.Parse(textBox4.Text), ushort.Parse(textBox5.Text), ushort.Parse(textBox6.Text));
				if (operateResult.IsSuccess)
				{
					textBox3.Text = "Data: " + operateResult.Content.ToHexString(' ');
				}
				else
				{
					DemoUtils.ShowMessage("Write Failed！" + operateResult.Message);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Read Failed: " + ex.Message);
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
			this.comboBox_encoding = new System.Windows.Forms.ComboBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.label4 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button31 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button_read_short = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label42 = new System.Windows.Forms.Label();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label8 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button32 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button30 = new System.Windows.Forms.Button();
			this.button29 = new System.Windows.Forms.Button();
			this.button28 = new System.Windows.Forms.Button();
			this.button27 = new System.Windows.Forms.Button();
			this.button26 = new System.Windows.Forms.Button();
			this.button25 = new System.Windows.Forms.Button();
			this.button24 = new System.Windows.Forms.Button();
			this.button23 = new System.Windows.Forms.Button();
			this.button22 = new System.Windows.Forms.Button();
			this.button21 = new System.Windows.Forms.Button();
			this.button20 = new System.Windows.Forms.Button();
			this.button19 = new System.Windows.Forms.Button();
			this.button18 = new System.Windows.Forms.Button();
			this.button17 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.button14 = new System.Windows.Forms.Button();
			this.button13 = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.button3 = new System.Windows.Forms.Button();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.comboBox_encoding);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 43);
			this.panel1.TabIndex = 12;
			this.comboBox_encoding.FormattingEnabled = true;
			this.comboBox_encoding.Location = new System.Drawing.Point(403, 9);
			this.comboBox_encoding.Name = "comboBox_encoding";
			this.comboBox_encoding.Size = new System.Drawing.Size(76, 25);
			this.comboBox_encoding.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(332, 12);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(65, 17);
			this.label9.TabIndex = 7;
			this.label9.Text = "Encoding:";
			this.label2.ForeColor = System.Drawing.Color.Red;
			this.label2.Location = new System.Drawing.Point(485, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(281, 35);
			this.label2.TabIndex = 6;
			this.label2.Text = "写入的操作需要小心，需要对数据进行检查。";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(875, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(772, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(250, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(76, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "60008";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(196, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 9);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(128, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Location = new System.Drawing.Point(3, 82);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1001, 568);
			this.panel2.TabIndex = 13;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(999, 566);
			this.tabControl1.TabIndex = 114;
			this.tabPage1.Controls.Add(this.label4);
			this.tabPage1.Controls.Add(this.comboBox1);
			this.tabPage1.Controls.Add(this.button31);
			this.tabPage1.Controls.Add(this.button4);
			this.tabPage1.Controls.Add(this.button_read_short);
			this.tabPage1.Controls.Add(this.textBox8);
			this.tabPage1.Controls.Add(this.label42);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(991, 536);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "通用读取";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(568, 12);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(93, 17);
			this.label4.TabIndex = 117;
			this.label4.Text = "PropertyInfo：";
			this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(664, 8);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(203, 25);
			this.comboBox1.TabIndex = 116;
			this.button31.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button31.Location = new System.Drawing.Point(873, 6);
			this.button31.Name = "button31";
			this.button31.Size = new System.Drawing.Size(112, 28);
			this.button31.TabIndex = 115;
			this.button31.Text = "Single Read";
			this.button31.UseVisualStyleBackColor = true;
			this.button31.Click += new System.EventHandler(button31_Click);
			this.button4.Location = new System.Drawing.Point(178, 6);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(113, 28);
			this.button4.TabIndex = 113;
			this.button4.Text = "String Read";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button_read_short.Location = new System.Drawing.Point(60, 6);
			this.button_read_short.Name = "button_read_short";
			this.button_read_short.Size = new System.Drawing.Size(112, 28);
			this.button_read_short.TabIndex = 9;
			this.button_read_short.Text = "JSON Read";
			this.button_read_short.UseVisualStyleBackColor = true;
			this.button_read_short.Click += new System.EventHandler(button_read_short_Click);
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(61, 39);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(924, 491);
			this.textBox8.TabIndex = 110;
			this.label42.AutoSize = true;
			this.label42.Location = new System.Drawing.Point(3, 37);
			this.label42.Name = "label42";
			this.label42.Size = new System.Drawing.Size(55, 17);
			this.label42.TabIndex = 109;
			this.label42.Text = "Result：";
			this.tabPage2.Controls.Add(this.linkLabel1);
			this.tabPage2.Controls.Add(this.label8);
			this.tabPage2.Controls.Add(this.groupBox1);
			this.tabPage2.Controls.Add(this.userControlReadWriteOp1);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(991, 536);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "专业读写";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(549, 281);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(490, 17);
			this.linkLabel1.TabIndex = 3;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "http://api.hslcommunication.cn/html/9a252da5-4341-0437-0fb7-27da2b49d3c4.htm";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(6, 282);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(546, 17);
			this.label8.TabIndex = 2;
			this.label8.Text = "Bool地址支持：SDO, SDI, RDI, RDO, UI, UO, SI, SO    字单位地址支持：GI, GO, D，其中D参考：\r\n";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button32);
			this.groupBox1.Controls.Add(this.textBox6);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox5);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.button30);
			this.groupBox1.Controls.Add(this.button29);
			this.groupBox1.Controls.Add(this.button28);
			this.groupBox1.Controls.Add(this.button27);
			this.groupBox1.Controls.Add(this.button26);
			this.groupBox1.Controls.Add(this.button25);
			this.groupBox1.Controls.Add(this.button24);
			this.groupBox1.Controls.Add(this.button23);
			this.groupBox1.Controls.Add(this.button22);
			this.groupBox1.Controls.Add(this.button21);
			this.groupBox1.Controls.Add(this.button20);
			this.groupBox1.Controls.Add(this.button19);
			this.groupBox1.Controls.Add(this.button18);
			this.groupBox1.Controls.Add(this.button17);
			this.groupBox1.Controls.Add(this.button16);
			this.groupBox1.Controls.Add(this.button15);
			this.groupBox1.Controls.Add(this.button14);
			this.groupBox1.Controls.Add(this.button13);
			this.groupBox1.Controls.Add(this.button12);
			this.groupBox1.Controls.Add(this.button11);
			this.groupBox1.Controls.Add(this.button10);
			this.groupBox1.Controls.Add(this.button9);
			this.groupBox1.Controls.Add(this.button8);
			this.groupBox1.Controls.Add(this.button7);
			this.groupBox1.Controls.Add(this.button6);
			this.groupBox1.Controls.Add(this.button5);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.button3);
			this.groupBox1.Location = new System.Drawing.Point(7, 302);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(977, 228);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "特殊功能测试";
			this.button32.Location = new System.Drawing.Point(772, 103);
			this.button32.Name = "button32";
			this.button32.Size = new System.Drawing.Size(77, 25);
			this.button32.TabIndex = 34;
			this.button32.Text = "Read";
			this.button32.UseVisualStyleBackColor = true;
			this.button32.Click += new System.EventHandler(button32_Click);
			this.textBox6.Location = new System.Drawing.Point(841, 77);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(92, 23);
			this.textBox6.TabIndex = 33;
			this.textBox6.Text = "4";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(770, 80);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 32;
			this.label7.Text = "长度：";
			this.textBox5.Location = new System.Drawing.Point(841, 49);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(92, 23);
			this.textBox5.TabIndex = 31;
			this.textBox5.Text = "1";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(770, 52);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 30;
			this.label6.Text = "偏移地址：";
			this.textBox4.Location = new System.Drawing.Point(841, 20);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(92, 23);
			this.textBox4.TabIndex = 29;
			this.textBox4.Text = "70";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(770, 23);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(56, 17);
			this.label5.TabIndex = 28;
			this.label5.Text = "数据块：";
			this.button30.Location = new System.Drawing.Point(681, 96);
			this.button30.Name = "button30";
			this.button30.Size = new System.Drawing.Size(69, 31);
			this.button30.TabIndex = 27;
			this.button30.Text = "w-GI";
			this.button30.UseVisualStyleBackColor = true;
			this.button30.Click += new System.EventHandler(button30_Click);
			this.button29.Location = new System.Drawing.Point(681, 59);
			this.button29.Name = "button29";
			this.button29.Size = new System.Drawing.Size(69, 31);
			this.button29.TabIndex = 26;
			this.button29.Text = "w-GO2";
			this.button29.UseVisualStyleBackColor = true;
			this.button29.Click += new System.EventHandler(button28_Click);
			this.button28.Location = new System.Drawing.Point(681, 22);
			this.button28.Name = "button28";
			this.button28.Size = new System.Drawing.Size(69, 31);
			this.button28.TabIndex = 25;
			this.button28.Text = "w-GO";
			this.button28.UseVisualStyleBackColor = true;
			this.button28.Click += new System.EventHandler(button28_Click);
			this.button27.Location = new System.Drawing.Point(606, 96);
			this.button27.Name = "button27";
			this.button27.Size = new System.Drawing.Size(69, 31);
			this.button27.TabIndex = 24;
			this.button27.Text = "w-RDI";
			this.button27.UseVisualStyleBackColor = true;
			this.button27.Click += new System.EventHandler(button27_Click);
			this.button26.Location = new System.Drawing.Point(606, 59);
			this.button26.Name = "button26";
			this.button26.Size = new System.Drawing.Size(69, 31);
			this.button26.TabIndex = 23;
			this.button26.Text = "w-RDO";
			this.button26.UseVisualStyleBackColor = true;
			this.button26.Click += new System.EventHandler(button26_Click);
			this.button25.Location = new System.Drawing.Point(606, 22);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(69, 31);
			this.button25.TabIndex = 22;
			this.button25.Text = "w-SDI";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.button24.Location = new System.Drawing.Point(492, 96);
			this.button24.Name = "button24";
			this.button24.Size = new System.Drawing.Size(108, 31);
			this.button24.TabIndex = 21;
			this.button24.Text = "w-SDO[1100x]";
			this.button24.UseVisualStyleBackColor = true;
			this.button24.Click += new System.EventHandler(button22_Click);
			this.button23.Location = new System.Drawing.Point(492, 59);
			this.button23.Name = "button23";
			this.button23.Size = new System.Drawing.Size(108, 31);
			this.button23.TabIndex = 20;
			this.button23.Text = "w-SDO[1000x]";
			this.button23.UseVisualStyleBackColor = true;
			this.button23.Click += new System.EventHandler(button22_Click);
			this.button22.Location = new System.Drawing.Point(492, 22);
			this.button22.Name = "button22";
			this.button22.Size = new System.Drawing.Size(108, 31);
			this.button22.TabIndex = 19;
			this.button22.Text = "w-SDO";
			this.button22.UseVisualStyleBackColor = true;
			this.button22.Click += new System.EventHandler(button22_Click);
			this.button21.Location = new System.Drawing.Point(414, 96);
			this.button21.Name = "button21";
			this.button21.Size = new System.Drawing.Size(72, 31);
			this.button21.TabIndex = 18;
			this.button21.Text = "r-WSI";
			this.button21.UseVisualStyleBackColor = true;
			this.button21.Click += new System.EventHandler(button21_Click);
			this.button20.Location = new System.Drawing.Point(414, 59);
			this.button20.Name = "button20";
			this.button20.Size = new System.Drawing.Size(72, 31);
			this.button20.TabIndex = 17;
			this.button20.Text = "r-WI";
			this.button20.UseVisualStyleBackColor = true;
			this.button20.Click += new System.EventHandler(button20_Click);
			this.button19.Location = new System.Drawing.Point(414, 22);
			this.button19.Name = "button19";
			this.button19.Size = new System.Drawing.Size(72, 31);
			this.button19.TabIndex = 16;
			this.button19.Text = "r-WO";
			this.button19.UseVisualStyleBackColor = true;
			this.button19.Click += new System.EventHandler(button19_Click);
			this.button18.Location = new System.Drawing.Point(336, 96);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(72, 31);
			this.button18.TabIndex = 15;
			this.button18.Text = "r-AI";
			this.button18.UseVisualStyleBackColor = true;
			this.button18.Click += new System.EventHandler(button18_Click);
			this.button17.Location = new System.Drawing.Point(336, 59);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(72, 31);
			this.button17.TabIndex = 14;
			this.button17.Text = "r-AO";
			this.button17.UseVisualStyleBackColor = true;
			this.button17.Click += new System.EventHandler(button17_Click);
			this.button16.Location = new System.Drawing.Point(336, 22);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(72, 31);
			this.button16.TabIndex = 13;
			this.button16.Text = "r-GI";
			this.button16.UseVisualStyleBackColor = true;
			this.button16.Click += new System.EventHandler(button16_Click);
			this.button15.Location = new System.Drawing.Point(262, 96);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(68, 31);
			this.button15.TabIndex = 12;
			this.button15.Text = "r-GO2";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button14.Location = new System.Drawing.Point(262, 59);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(68, 31);
			this.button14.TabIndex = 11;
			this.button14.Text = "r-GO";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.button13.Location = new System.Drawing.Point(262, 22);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(68, 31);
			this.button13.TabIndex = 10;
			this.button13.Text = "r-UI";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button12.Location = new System.Drawing.Point(185, 96);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(71, 31);
			this.button12.TabIndex = 9;
			this.button12.Text = "r-UO";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button11.Location = new System.Drawing.Point(185, 59);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(71, 31);
			this.button11.TabIndex = 8;
			this.button11.Text = "r-SI";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(185, 22);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(71, 31);
			this.button10.TabIndex = 7;
			this.button10.Text = "r-SO";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Location = new System.Drawing.Point(108, 96);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(71, 31);
			this.button9.TabIndex = 6;
			this.button9.Text = "r-RDI";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Location = new System.Drawing.Point(108, 59);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(71, 31);
			this.button8.TabIndex = 5;
			this.button8.Text = "r-RDO";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Location = new System.Drawing.Point(108, 22);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(71, 31);
			this.button7.TabIndex = 4;
			this.button7.Text = "r-SDI";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(6, 96);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(96, 31);
			this.button6.TabIndex = 3;
			this.button6.Text = "r-SDO[1100x]";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(6, 59);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(96, 31);
			this.button5.TabIndex = 2;
			this.button5.Text = "r-SDO[1000x]";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(6, 133);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox3.Size = new System.Drawing.Size(965, 89);
			this.textBox3.TabIndex = 1;
			this.button3.Location = new System.Drawing.Point(6, 22);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(96, 31);
			this.button3.TabIndex = 0;
			this.button3.Text = "r-SDO";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.userControlReadWriteOp1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(5, 6);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(979, 272);
			this.userControlReadWriteOp1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Robot";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 14;
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
			base.Name = "FormFanucRobot";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "发那科机器人";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormFanucRobot_FormClosing);
			base.Load += new System.EventHandler(FormEfort_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
