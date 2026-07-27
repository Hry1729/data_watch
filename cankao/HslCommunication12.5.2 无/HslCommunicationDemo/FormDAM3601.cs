using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Instrument.Temperature;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormDAM3601 : HslFormContent
	{
		private DAM3601 dAM3601 = null;

		private List<TextBox> allTextBox = new List<TextBox>();

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private GroupBox groupBox1;

		private Button button_read_bool;

		private TextBox textBox_station;

		private Label label21;

		private CheckBox checkBox_startwith0;

		private CheckBox checkBox_stringreverse;

		private ComboBox comboBox_dataformat;

		private UserControlHead userControlHead1;

		private PipeSelectControl pipeSelectControl1;

		public FormDAM3601()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox_dataformat.SelectedIndex = 2;
			comboBox_dataformat.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			checkBox_stringreverse.CheckedChanged += CheckBox3_CheckedChanged;
			Language(Program.Language);
			int num = 1;
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					Label label = new Label();
					label.Text = "温度" + num;
					label.AutoSize = true;
					TextBox textBox = new TextBox();
					textBox.Width = 50;
					label.Parent = groupBox1;
					label.Location = new Point(j * 115 + 5, i * 25 + 72);
					textBox.Parent = groupBox1;
					textBox.Location = new Point(j * 115 + 60, i * 25 + 70);
					allTextBox.Add(textBox);
					num++;
				}
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Modbus Rtu Read Demo";
				label21.Text = "station";
				checkBox_startwith0.Text = "address from 0";
				checkBox_stringreverse.Text = "string reverse";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				button_read_bool.Text = "r-coil";
				groupBox1.Text = "read test";
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (dAM3601 != null)
			{
				dAM3601.IsStringReverse = checkBox_stringreverse.Checked;
			}
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (dAM3601 != null)
			{
				switch (comboBox_dataformat.SelectedIndex)
				{
				case 0:
					dAM3601.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					dAM3601.DataFormat = DataFormat.BADC;
					break;
				case 2:
					dAM3601.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					dAM3601.DataFormat = DataFormat.DCBA;
					break;
				}
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
			if (!byte.TryParse(textBox_station.Text, out var result))
			{
				DemoUtils.ShowMessage("站号输入不正确！");
				return;
			}
			dAM3601?.Close();
			dAM3601 = new DAM3601(result);
			dAM3601.AddressStartWithZero = checkBox_startwith0.Checked;
			dAM3601.LogNet = base.LogNet;
			ComboBox2_SelectedIndexChanged(null, new EventArgs());
			dAM3601.IsStringReverse = checkBox_stringreverse.Checked;
			try
			{
				pipeSelectControl1.IniPipe(dAM3601);
				OperateResult operateResult = DeviceConnectPLC(dAM3601);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					return;
				}
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
			pipeSelectControl1.ExtraCloseAction(dAM3601);
			dAM3601.Close();
		}

		private void button_read_bool_Click(object sender, EventArgs e)
		{
			OperateResult<float[]> operateResult = dAM3601.ReadAllTemperature();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("读取失败！原因：" + operateResult.Message);
				return;
			}
			for (int i = 0; i < allTextBox.Count; i++)
			{
				allTextBox[i].Text = operateResult.Content[i].ToString();
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
			element.SetAttributeValue(DemoDeviceList.XmlAddressStartWithZero, checkBox_startwith0.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox_dataformat.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox_stringreverse.Checked);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			checkBox_startwith0.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlAddressStartWithZero).Value);
			comboBox_dataformat.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox_stringreverse.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
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
			this.comboBox_dataformat = new System.Windows.Forms.ComboBox();
			this.checkBox_startwith0 = new System.Windows.Forms.CheckBox();
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.checkBox_stringreverse = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button_read_bool = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.comboBox_dataformat);
			this.panel1.Controls.Add(this.checkBox_startwith0);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.checkBox_stringreverse);
			this.panel1.Location = new System.Drawing.Point(3, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 65);
			this.panel1.TabIndex = 0;
			this.comboBox_dataformat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_dataformat.FormattingEnabled = true;
			this.comboBox_dataformat.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox_dataformat.Location = new System.Drawing.Point(241, 35);
			this.comboBox_dataformat.Name = "comboBox_dataformat";
			this.comboBox_dataformat.Size = new System.Drawing.Size(111, 25);
			this.comboBox_dataformat.TabIndex = 28;
			this.checkBox_startwith0.AutoSize = true;
			this.checkBox_startwith0.Checked = true;
			this.checkBox_startwith0.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_startwith0.Location = new System.Drawing.Point(110, 37);
			this.checkBox_startwith0.Name = "checkBox_startwith0";
			this.checkBox_startwith0.Size = new System.Drawing.Size(106, 21);
			this.checkBox_startwith0.TabIndex = 9;
			this.checkBox_startwith0.Text = "首地址从0开始";
			this.checkBox_startwith0.UseVisualStyleBackColor = true;
			this.textBox_station.Location = new System.Drawing.Point(52, 35);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(37, 23);
			this.textBox_station.TabIndex = 7;
			this.textBox_station.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(8, 38);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(894, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(797, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.checkBox_stringreverse.AutoSize = true;
			this.checkBox_stringreverse.Location = new System.Drawing.Point(386, 37);
			this.checkBox_stringreverse.Name = "checkBox_stringreverse";
			this.checkBox_stringreverse.Size = new System.Drawing.Size(87, 21);
			this.checkBox_stringreverse.TabIndex = 26;
			this.checkBox_stringreverse.Text = "字符串颠倒";
			this.checkBox_stringreverse.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(3, 101);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 541);
			this.panel2.TabIndex = 1;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button_read_bool);
			this.groupBox1.Location = new System.Drawing.Point(7, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(981, 528);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.button_read_bool.Location = new System.Drawing.Point(23, 31);
			this.button_read_bool.Name = "button_read_bool";
			this.button_read_bool.Size = new System.Drawing.Size(127, 28);
			this.button_read_bool.TabIndex = 6;
			this.button_read_bool.Text = "温度数据读取";
			this.button_read_bool.UseVisualStyleBackColor = true;
			this.button_read_bool.Click += new System.EventHandler(button_read_bool_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Modbus rtu";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(1, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 29;
			this.pipeSelectControl1.TcpPortText = "502";
			this.pipeSelectControl1.UdpPortText = "502";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormDAM3601";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Modbus Rtu访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
