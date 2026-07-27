using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Robot.ABB;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.Robot
{
	public class FormABBWebApi : HslFormContent
	{
		private ABBWebApiClient webApiClient;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button_open;

		private TextBox textBox4;

		private Label label4;

		private TextBox textBox3;

		private Label label3;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private TextBox textBox7;

		private Label label9;

		private Panel panel3;

		private TextBox textBox6;

		private WebBrowser webBrowser1;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private ComboBox comboBox1;

		private Button button2;

		private Label label6;

		private TextBox textBox5;

		private Label label5;

		private Button button6;

		private Button button5;

		private Button button4;

		private Button button3;

		private Button button7;

		private Button button12;

		private Button button10;

		private Button button11;

		private Button button9;

		private Button button8;

		private Button button13;

		private Button button14;

		private Button button15;

		private Button button16;

		private Button button17;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private Label label7;

		private Button button18;

		private TextBox textBox_user_value_name;

		private Label label8;

		private TextBox textBox_url_read;

		private TextBox textBox_web_text;

		private Label label_time_cost;

		private Label label_11;

		private Button button_close;

		private Label label10;

		private TextBox textBox_mechunit;

		private Label label11;

		private TextBox textBox_io_network;

		private Label label12;

		private TextBox textBox_io_unit;

		private Label label13;

		private TextBox textBox_io_signal;

		private Button button_io_signal;

		private Label label14;

		public FormABBWebApi()
		{
			InitializeComponent();
		}

		private void FormABBWebApi_FormClosing(object sender, FormClosingEventArgs e)
		{
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			try
			{
				webApiClient = new ABBWebApiClient(textBox1.Text, int.Parse(textBox2.Text), textBox3.Text, textBox4.Text);
				panel2.Enabled = true;
				button_close.Enabled = true;
				button_open.Enabled = false;
				codeExampleControl.SetCodeText(webApiClient);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Input Data is wrong! please int again!" + Environment.NewLine + ex.Message);
			}
		}

		private void button_close_Click(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button_close.Enabled = false;
			button_open.Enabled = true;
		}

		private async void Button2_Click(object sender, EventArgs e)
		{
			if (comboBox1.SelectedIndex == 0)
			{
				OperateResult<string> read = await webApiClient.ReadStringAsync(textBox5.Text);
				if (read.IsSuccess)
				{
					textBox6.Text = read.Content;
					webBrowser1.DocumentText = read.Content;
				}
				else
				{
					DemoUtils.ShowMessage("Read Failed:" + read.Message);
				}
			}
			else
			{
				OperateResult write = await webApiClient.WriteAsync(textBox5.Text, textBox7.Text);
				if (write.IsSuccess)
				{
					DemoUtils.ShowMessage("Write Success");
				}
				else
				{
					DemoUtils.ShowMessage("Read Failed:" + write.Message);
				}
			}
		}

		private void FormABBWebApi_Load(object sender, EventArgs e)
		{
			button_close.Enabled = false;
			panel2.Enabled = false;
			comboBox1.SelectedIndex = 0;
			radioButton1.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton2.CheckedChanged += RadioButton2_CheckedChanged;
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void RadioButton2_CheckedChanged(object sender, EventArgs e)
		{
			if (radioButton2.Checked)
			{
				textBox_web_text.Visible = false;
				webBrowser1.Visible = true;
			}
		}

		private void RadioButton1_CheckedChanged(object sender, EventArgs e)
		{
			if (radioButton1.Checked)
			{
				textBox_web_text.Visible = true;
				webBrowser1.Visible = false;
			}
		}

		private async Task RenderWebUrl(string url, bool get = true)
		{
			textBox_url_read.Text = url;
			DateTime start = DateTime.Now;
			OperateResult<string> read_web = await webApiClient.ReadStringAsync(textBox_url_read.Text);
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (read_web.IsSuccess)
			{
				textBox_web_text.Text = read_web.Content;
				webBrowser1.DocumentText = read_web.Content;
			}
		}

		private async void Button3_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetErrorStateAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/motionsystem/errorstate");
		}

		private async void Button4_Click(object sender, EventArgs e)
		{
			string unit = (string.IsNullOrEmpty(textBox_mechunit.Text) ? "ROB_1" : textBox_mechunit.Text);
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetJointTargetAsync(unit);
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/motionsystem/mechunits/" + unit + "/jointtarget");
		}

		private async void Button5_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetSpeedRatioAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/panel/speedratio");
		}

		private async void Button6_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetOperationModeAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/panel/opmode");
		}

		private async void Button7_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetCtrlStateAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/panel/ctrlstate");
		}

		private async void Button8_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetIOInAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/devices/D652_10");
		}

		private async void Button9_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetIOOutAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/devices/D652_10");
		}

		private async void Button11_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetIO2InAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/devices/BK5250");
		}

		private async void Button10_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetIO2OutAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/devices/BK5250");
		}

		private async void button_io_signal_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetAnIOSignalAsync(textBox_io_network.Text, textBox_io_unit.Text, textBox_io_signal.Text);
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/signals/" + textBox_io_network.Text + "/" + textBox_io_unit.Text + "/" + textBox_io_signal.Text);
		}

		private async void Button12_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetLogAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/elog/0?lang=zh&amp;resource=title");
		}

		private async void button13_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetSystemAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/system");
		}

		private async void button14_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetRobotTargetAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/motionsystem/mechunits/ROB_1/robtarget");
		}

		private async void button15_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetServoEnableAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/iosystem/signals/Local/DRV_1/DRV1K1");
		}

		private async void button16_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetRapidExecutionAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/rapid/execution");
		}

		private async void button17_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<string> read = await webApiClient.GetRapidTasksAsync();
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content;
			await RenderWebUrl("url=/rw/rapid/tasks");
		}

		private async void button18_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			OperateResult<double[]> read = webApiClient.GetUserValue(textBox_user_value_name.Text);
			label_time_cost.Text = DemoUtils.GetTimeCost(start);
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.Message);
				return;
			}
			textBox6.Text = read.Content.ToJsonString();
			await RenderWebUrl(textBox_user_value_name.Text.StartsWith("url=", StringComparison.OrdinalIgnoreCase) ? textBox_user_value_name.Text : ("url=/rw/rapid/symbol/data/RAPID/T_ROB1/user/" + textBox_user_value_name.Text));
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox4.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox4.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button_close = new System.Windows.Forms.Button();
			this.button_open = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label14 = new System.Windows.Forms.Label();
			this.button_io_signal = new System.Windows.Forms.Button();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox_io_signal = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_io_unit = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_io_network = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_mechunit = new System.Windows.Forms.TextBox();
			this.label_time_cost = new System.Windows.Forms.Label();
			this.label_11 = new System.Windows.Forms.Label();
			this.textBox_url_read = new System.Windows.Forms.TextBox();
			this.button18 = new System.Windows.Forms.Button();
			this.textBox_user_value_name = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.button17 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.button14 = new System.Windows.Forms.Button();
			this.button13 = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.panel3 = new System.Windows.Forms.Panel();
			this.textBox_web_text = new System.Windows.Forms.TextBox();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.webBrowser1 = new System.Windows.Forms.WebBrowser();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button2 = new System.Windows.Forms.Button();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "web api";
			this.userControlHead1.Size = new System.Drawing.Size(1006, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 30;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button_close);
			this.panel1.Controls.Add(this.button_open);
			this.panel1.Controls.Add(this.textBox4);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1000, 39);
			this.panel1.TabIndex = 31;
			this.button_close.Location = new System.Drawing.Point(791, 5);
			this.button_close.Name = "button_close";
			this.button_close.Size = new System.Drawing.Size(99, 25);
			this.button_close.TabIndex = 18;
			this.button_close.Text = "close";
			this.button_close.UseVisualStyleBackColor = true;
			this.button_close.Click += new System.EventHandler(button_close_Click);
			this.button_open.Location = new System.Drawing.Point(686, 5);
			this.button_open.Name = "button_open";
			this.button_open.Size = new System.Drawing.Size(99, 25);
			this.button_open.TabIndex = 17;
			this.button_open.Text = "open";
			this.button_open.UseVisualStyleBackColor = true;
			this.button_open.Click += new System.EventHandler(Button1_Click);
			this.textBox4.Location = new System.Drawing.Point(568, 6);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(97, 23);
			this.textBox4.TabIndex = 16;
			this.textBox4.Text = "robotics";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(497, 9);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(65, 17);
			this.label4.TabIndex = 15;
			this.label4.Text = "password";
			this.textBox3.Location = new System.Drawing.Point(382, 6);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(100, 23);
			this.textBox3.TabIndex = 14;
			this.textBox3.Text = "Default User";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(336, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(43, 17);
			this.label3.TabIndex = 13;
			this.label3.Text = "Name";
			this.textBox2.Location = new System.Drawing.Point(284, 6);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(44, 23);
			this.textBox2.TabIndex = 12;
			this.textBox2.Text = "80";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(246, 9);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(32, 17);
			this.label2.TabIndex = 11;
			this.label2.Text = "Port";
			this.textBox1.Location = new System.Drawing.Point(82, 6);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(156, 23);
			this.textBox1.TabIndex = 10;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(75, 17);
			this.label1.TabIndex = 9;
			this.label1.Text = "Ip Address:";
			this.panel2.Controls.Add(this.label14);
			this.panel2.Controls.Add(this.button_io_signal);
			this.panel2.Controls.Add(this.label13);
			this.panel2.Controls.Add(this.textBox_io_signal);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.textBox_io_unit);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox_io_network);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.textBox_mechunit);
			this.panel2.Controls.Add(this.label_time_cost);
			this.panel2.Controls.Add(this.label_11);
			this.panel2.Controls.Add(this.textBox_url_read);
			this.panel2.Controls.Add(this.button18);
			this.panel2.Controls.Add(this.textBox_user_value_name);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.button17);
			this.panel2.Controls.Add(this.button16);
			this.panel2.Controls.Add(this.button15);
			this.panel2.Controls.Add(this.button14);
			this.panel2.Controls.Add(this.button13);
			this.panel2.Controls.Add(this.button12);
			this.panel2.Controls.Add(this.button10);
			this.panel2.Controls.Add(this.button11);
			this.panel2.Controls.Add(this.button9);
			this.panel2.Controls.Add(this.button8);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.button6);
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.button2);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(986, 520);
			this.panel2.TabIndex = 32;
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label14.AutoSize = true;
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(236, 501);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(486, 17);
			this.label14.TabIndex = 51;
			this.label14.Text = "Set url example:  url=/rw/rapid/symbol/data/RAPID/T_ROB1/user/reg1?action=set";
			this.button_io_signal.Location = new System.Drawing.Point(684, 69);
			this.button_io_signal.Name = "button_io_signal";
			this.button_io_signal.Size = new System.Drawing.Size(102, 27);
			this.button_io_signal.TabIndex = 50;
			this.button_io_signal.Text = "IO Signal ";
			this.button_io_signal.UseVisualStyleBackColor = true;
			this.button_io_signal.Click += new System.EventHandler(button_io_signal_Click);
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(553, 73);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(45, 17);
			this.label13.TabIndex = 49;
			this.label13.Text = "signal:";
			this.textBox_io_signal.Location = new System.Drawing.Point(606, 70);
			this.textBox_io_signal.Name = "textBox_io_signal";
			this.textBox_io_signal.Size = new System.Drawing.Size(69, 23);
			this.textBox_io_signal.TabIndex = 48;
			this.textBox_io_signal.Text = "DRV1K1";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(443, 73);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(32, 17);
			this.label12.TabIndex = 47;
			this.label12.Text = "unit:";
			this.textBox_io_unit.Location = new System.Drawing.Point(480, 70);
			this.textBox_io_unit.Name = "textBox_io_unit";
			this.textBox_io_unit.Size = new System.Drawing.Size(69, 23);
			this.textBox_io_unit.TabIndex = 46;
			this.textBox_io_unit.Text = "DRV_1";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(301, 74);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(58, 17);
			this.label11.TabIndex = 45;
			this.label11.Text = "network:";
			this.textBox_io_network.Location = new System.Drawing.Point(368, 70);
			this.textBox_io_network.Name = "textBox_io_network";
			this.textBox_io_network.Size = new System.Drawing.Size(69, 23);
			this.textBox_io_network.TabIndex = 44;
			this.textBox_io_network.Text = "Local";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(7, 74);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(63, 17);
			this.label10.TabIndex = 43;
			this.label10.Text = "mechunit:";
			this.textBox_mechunit.Location = new System.Drawing.Point(77, 70);
			this.textBox_mechunit.Name = "textBox_mechunit";
			this.textBox_mechunit.Size = new System.Drawing.Size(101, 23);
			this.textBox_mechunit.TabIndex = 42;
			this.textBox_mechunit.Text = "ROB_1";
			this.label_time_cost.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_time_cost.AutoSize = true;
			this.label_time_cost.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label_time_cost.Location = new System.Drawing.Point(154, 501);
			this.label_time_cost.Name = "label_time_cost";
			this.label_time_cost.Size = new System.Drawing.Size(36, 17);
			this.label_time_cost.TabIndex = 41;
			this.label_time_cost.Text = "0 ms";
			this.label_11.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_11.AutoSize = true;
			this.label_11.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label_11.Location = new System.Drawing.Point(74, 501);
			this.label_11.Name = "label_11";
			this.label_11.Size = new System.Drawing.Size(74, 17);
			this.label_11.TabIndex = 40;
			this.label_11.Text = "Cost-Time: ";
			this.textBox_url_read.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_url_read.Location = new System.Drawing.Point(76, 165);
			this.textBox_url_read.Name = "textBox_url_read";
			this.textBox_url_read.ReadOnly = true;
			this.textBox_url_read.Size = new System.Drawing.Size(906, 23);
			this.textBox_url_read.TabIndex = 39;
			this.button18.Location = new System.Drawing.Point(912, 130);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(59, 27);
			this.button18.TabIndex = 38;
			this.button18.Text = "Read";
			this.button18.UseVisualStyleBackColor = true;
			this.button18.Click += new System.EventHandler(button18_Click);
			this.textBox_user_value_name.Location = new System.Drawing.Point(779, 132);
			this.textBox_user_value_name.Name = "textBox_user_value_name";
			this.textBox_user_value_name.Size = new System.Drawing.Size(130, 23);
			this.textBox_user_value_name.TabIndex = 37;
			this.textBox_user_value_name.Text = "nCurProgIndex";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(736, 136);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(43, 17);
			this.label8.TabIndex = 36;
			this.label8.Text = "name:";
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label7.AutoSize = true;
			this.label7.ForeColor = System.Drawing.Color.Blue;
			this.label7.Location = new System.Drawing.Point(892, 10);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(85, 17);
			this.label7.TabIndex = 35;
			this.label7.Text = "startwith url=";
			this.button17.Location = new System.Drawing.Point(566, 130);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(119, 27);
			this.button17.TabIndex = 34;
			this.button17.Text = "RapidTasks";
			this.button17.UseVisualStyleBackColor = true;
			this.button17.Click += new System.EventHandler(button17_Click);
			this.button16.Location = new System.Drawing.Point(441, 130);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(119, 27);
			this.button16.TabIndex = 33;
			this.button16.Text = "RapidExecution";
			this.button16.UseVisualStyleBackColor = true;
			this.button16.Click += new System.EventHandler(button16_Click);
			this.button15.Location = new System.Drawing.Point(739, 98);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(102, 27);
			this.button15.TabIndex = 32;
			this.button15.Text = "servo enable";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button14.Location = new System.Drawing.Point(631, 98);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(102, 27);
			this.button14.TabIndex = 31;
			this.button14.Text = "robtarget";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.button13.Location = new System.Drawing.Point(523, 98);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(102, 27);
			this.button13.TabIndex = 30;
			this.button13.Text = "System";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button12.Location = new System.Drawing.Point(370, 130);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(67, 27);
			this.button12.TabIndex = 29;
			this.button12.Text = "log";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(Button12_Click);
			this.button10.Location = new System.Drawing.Point(297, 130);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(67, 27);
			this.button10.TabIndex = 28;
			this.button10.Text = "io2 out";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(Button10_Click);
			this.button11.Location = new System.Drawing.Point(223, 130);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(68, 27);
			this.button11.TabIndex = 27;
			this.button11.Text = "io2 in";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(Button11_Click);
			this.button9.Location = new System.Drawing.Point(150, 130);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(67, 27);
			this.button9.TabIndex = 26;
			this.button9.Text = "io out";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(Button9_Click);
			this.button8.Location = new System.Drawing.Point(76, 130);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(68, 27);
			this.button8.TabIndex = 25;
			this.button8.Text = "io in";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(Button8_Click);
			this.button7.Location = new System.Drawing.Point(415, 98);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(102, 27);
			this.button7.TabIndex = 24;
			this.button7.Text = "CtrlState";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(Button7_Click);
			this.button6.Location = new System.Drawing.Point(292, 98);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(117, 27);
			this.button6.TabIndex = 23;
			this.button6.Text = "OperationMode";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(Button6_Click);
			this.button5.Location = new System.Drawing.Point(184, 98);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(102, 27);
			this.button5.TabIndex = 22;
			this.button5.Text = "SpeedRatio";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(Button5_Click);
			this.button4.Location = new System.Drawing.Point(184, 68);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(102, 27);
			this.button4.TabIndex = 21;
			this.button4.Text = "Joints";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(Button4_Click);
			this.button3.Location = new System.Drawing.Point(76, 98);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(102, 27);
			this.button3.TabIndex = 20;
			this.button3.Text = "ErrorState";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(Button3_Click);
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox7.Location = new System.Drawing.Point(99, 38);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(678, 23);
			this.textBox7.TabIndex = 19;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(3, 40);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(81, 17);
			this.label9.TabIndex = 18;
			this.label9.Text = "post content";
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel3.Controls.Add(this.textBox_web_text);
			this.panel3.Controls.Add(this.textBox6);
			this.panel3.Controls.Add(this.webBrowser1);
			this.panel3.Location = new System.Drawing.Point(76, 192);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(907, 308);
			this.panel3.TabIndex = 17;
			this.textBox_web_text.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_web_text.Location = new System.Drawing.Point(430, 0);
			this.textBox_web_text.Multiline = true;
			this.textBox_web_text.Name = "textBox_web_text";
			this.textBox_web_text.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_web_text.Size = new System.Drawing.Size(472, 303);
			this.textBox_web_text.TabIndex = 15;
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox6.Location = new System.Drawing.Point(0, 0);
			this.textBox6.Multiline = true;
			this.textBox6.Name = "textBox6";
			this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox6.Size = new System.Drawing.Size(424, 307);
			this.textBox6.TabIndex = 5;
			this.webBrowser1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.webBrowser1.Location = new System.Drawing.Point(430, 0);
			this.webBrowser1.MinimumSize = new System.Drawing.Size(20, 20);
			this.webBrowser1.Name = "webBrowser1";
			this.webBrowser1.Size = new System.Drawing.Size(472, 303);
			this.webBrowser1.TabIndex = 14;
			this.radioButton2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(876, 39);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(84, 21);
			this.radioButton2.TabIndex = 16;
			this.radioButton2.Text = "web page";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(809, 39);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(47, 21);
			this.radioButton1.TabIndex = 15;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "text";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[2] { "GET", "POST" });
			this.comboBox1.Location = new System.Drawing.Point(696, 7);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(81, 25);
			this.comboBox1.TabIndex = 13;
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Location = new System.Drawing.Point(799, 7);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(84, 27);
			this.button2.TabIndex = 9;
			this.button2.Text = "read";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(Button2_Click);
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(7, 192);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(53, 51);
			this.label6.TabIndex = 4;
			this.label6.Text = "Content\r\nAnd\r\nHtml";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(76, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(614, 23);
			this.textBox5.TabIndex = 3;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(7, 10);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(26, 17);
			this.label5.TabIndex = 2;
			this.label5.Text = "url:";
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(3, 76);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1000, 556);
			this.tabControl1.TabIndex = 33;
			this.tabPage1.Controls.Add(this.panel2);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(992, 526);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Basic Function";
			this.tabPage1.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1006, 634);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormABBWebApi";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormABBWebApi";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormABBWebApi_FormClosing);
			base.Load += new System.EventHandler(FormABBWebApi_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
