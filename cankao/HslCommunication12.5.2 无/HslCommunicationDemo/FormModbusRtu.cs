using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.ModBus;
using HslCommunication.Serial;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Modbus;

namespace HslCommunicationDemo
{
	public class FormModbusRtu : HslFormContent
	{
		private ModbusRtu busRtuClient = null;

		private ModbusControl control;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private StationSearchControl stationSearchControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label21;

		private CheckBox checkBox1;

		private CheckBox checkBox3;

		private ComboBox comboBox2;

		private UserControlHead userControlHead1;

		private CheckBox checkBox2;

		private CheckBox checkBox_crc16;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private CheckBox checkBox_station_check;

		private PipeSelectControl pipeSelectControl1;

		private TextBox textBox_BroadcastStation;

		private Label label_BroadcastStation;

		private TextBox textBox_batch_length;

		private Label label_batch_length;

		private Label label1;

		private CheckBox checkBox_DisableFunctionCode06;

		private Label label2;

		public FormModbusRtu()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
			checkBox_DisableFunctionCode06.CheckedChanged += CheckBox_DisableFunctionCode06_CheckedChanged;
		}

		private void CheckBox_DisableFunctionCode06_CheckedChanged(object sender, EventArgs e)
		{
			if (busRtuClient != null)
			{
				busRtuClient.DisableFunctionCode06 = checkBox_DisableFunctionCode06.Checked;
			}
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox2.SelectedIndex = 2;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			checkBox3.CheckedChanged += CheckBox3_CheckedChanged;
			Language(Program.Language);
			control = new ModbusControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(control);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetModbusAddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			stationSearchControl = new StationSearchControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(stationSearchControl, false, StationSearchControl.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Modbus Rtu Read Demo";
				label21.Text = "station";
				checkBox1.Text = "address from 0";
				checkBox3.Text = "string reverse";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				checkBox2.Text = "IsClearCacheBeforeRead";
				label_BroadcastStation.Text = "BroadcastStat:";
				label_batch_length.Text = "BatchLen:";
			}
			else
			{
				checkBox_DisableFunctionCode06.Text = "禁用功能码06";
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (busRtuClient != null)
			{
				busRtuClient.IsStringReverse = checkBox3.Checked;
			}
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (busRtuClient != null)
			{
				switch (comboBox2.SelectedIndex)
				{
				case 0:
					busRtuClient.DataFormat = DataFormat.ABCD;
					break;
				case 1:
					busRtuClient.DataFormat = DataFormat.BADC;
					break;
				case 2:
					busRtuClient.DataFormat = DataFormat.CDAB;
					break;
				case 3:
					busRtuClient.DataFormat = DataFormat.DCBA;
					break;
				}
			}
		}

		private void FormModbusRtu_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!byte.TryParse(textBox15.Text, out var result))
			{
				DemoUtils.ShowMessage("Station input wrong！");
				return;
			}
			busRtuClient?.Close();
			busRtuClient = new ModbusRtu(result);
			busRtuClient.AddressStartWithZero = checkBox1.Checked;
			busRtuClient.IsClearCacheBeforeRead = checkBox2.Checked;
			busRtuClient.LogNet = base.LogNet;
			busRtuClient.Crc16CheckEnable = checkBox_crc16.Checked;
			busRtuClient.StationCheckMatch = checkBox_station_check.Checked;
			busRtuClient.DisableFunctionCode06 = checkBox_DisableFunctionCode06.Checked;
			if (!string.IsNullOrEmpty(textBox_BroadcastStation.Text))
			{
				busRtuClient.BroadcastStation = int.Parse(textBox_BroadcastStation.Text);
			}
			if (!string.IsNullOrEmpty(textBox_batch_length.Text))
			{
				busRtuClient.WordReadBatchLength = int.Parse(textBox_batch_length.Text);
			}
			ComboBox2_SelectedIndexChanged(null, new EventArgs());
			busRtuClient.IsStringReverse = checkBox3.Checked;
			try
			{
				pipeSelectControl1.IniPipe(busRtuClient);
				OperateResult operateResult = DeviceConnectPLC(busRtuClient);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(busRtuClient, "100");
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(busRtuClient, "100", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => busRtuClient.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => busRtuClient.ReadFromCoreServer(SoftCRC16.CRC16(m), true, false), "No CRC", "with no crc16, example: 01 03 00 00 00 01");
					control.SetDevice(busRtuClient, "100");
					stationSearchControl.SetModbus(busRtuClient);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.ModbusDeviceName);
					List<string> list = new List<string>();
					list.Add("Station");
					list.Add("IsStringReverse");
					list.Add("DataFormat");
					list.Add("IsClearCacheBeforeRead");
					if (!busRtuClient.Crc16CheckEnable)
					{
						list.Add("Crc16CheckEnable");
					}
					if (!busRtuClient.StationCheckMatch)
					{
						list.Add("StationCheckMatch");
					}
					if (busRtuClient.BroadcastStation != -1)
					{
						list.Add("BroadcastStation");
					}
					if (!busRtuClient.AddressStartWithZero)
					{
						list.Add("AddressStartWithZero");
					}
					if (busRtuClient.WordReadBatchLength != 120)
					{
						list.Add("WordReadBatchLength");
					}
					if (busRtuClient.DisableFunctionCode06)
					{
						list.Add("DisableFunctionCode06");
					}
					codeExampleControl.SetCodeText(DemoUtils.ModbusDeviceName, busRtuClient, list.ToArray());
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
				}
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
			userControlReadWriteDevice1.SetEnable(false);
			pipeSelectControl1.ExtraCloseAction(busRtuClient);
			busRtuClient.Close();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox15.Text);
			element.SetAttributeValue(DemoDeviceList.XmlAddressStartWithZero, checkBox1.Checked);
			element.SetAttributeValue(DemoDeviceList.XmlDataFormat, comboBox2.SelectedIndex);
			element.SetAttributeValue(DemoDeviceList.XmlStringReverse, checkBox3.Checked);
			element.SetAttributeValue("CRC16", checkBox_crc16.Checked);
			element.SetAttributeValue("StationCheckMacth", checkBox_station_check.Checked);
			element.SetAttributeValue("BroadcastStation", textBox_BroadcastStation.Text);
			element.SetAttributeValue("DisableFunctionCode06", checkBox_DisableFunctionCode06.Checked);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			checkBox1.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlAddressStartWithZero).Value);
			comboBox2.SelectedIndex = int.Parse(element.Attribute(DemoDeviceList.XmlDataFormat).Value);
			checkBox3.Checked = bool.Parse(element.Attribute(DemoDeviceList.XmlStringReverse).Value);
			checkBox_crc16.Checked = HslFormContent.GetXmlValue(element, "CRC16", checkBox_crc16.Checked, bool.Parse);
			checkBox_station_check.Checked = HslFormContent.GetXmlValue(element, "StationCheckMacth", checkBox_station_check.Checked, bool.Parse);
			checkBox_DisableFunctionCode06.Checked = HslFormContent.GetXmlValue(element, "DisableFunctionCode06", false, bool.Parse);
			textBox_BroadcastStation.Text = HslFormContent.GetXmlValue(element, "BroadcastStation", "", (string m) => m);
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
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
			this.textBox_batch_length = new System.Windows.Forms.TextBox();
			this.label_batch_length = new System.Windows.Forms.Label();
			this.textBox_BroadcastStation = new System.Windows.Forms.TextBox();
			this.label_BroadcastStation = new System.Windows.Forms.Label();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.checkBox_station_check = new System.Windows.Forms.CheckBox();
			this.checkBox_crc16 = new System.Windows.Forms.CheckBox();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox_DisableFunctionCode06 = new System.Windows.Forms.CheckBox();
			this.label2 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.checkBox_DisableFunctionCode06);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.textBox_batch_length);
			this.panel1.Controls.Add(this.label_batch_length);
			this.panel1.Controls.Add(this.textBox_BroadcastStation);
			this.panel1.Controls.Add(this.label_BroadcastStation);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.checkBox_station_check);
			this.panel1.Controls.Add(this.checkBox_crc16);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.comboBox2);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.checkBox3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 82);
			this.panel1.TabIndex = 0;
			this.textBox_batch_length.Location = new System.Drawing.Point(519, 33);
			this.textBox_batch_length.Name = "textBox_batch_length";
			this.textBox_batch_length.Size = new System.Drawing.Size(37, 23);
			this.textBox_batch_length.TabIndex = 39;
			this.label_batch_length.AutoSize = true;
			this.label_batch_length.Location = new System.Drawing.Point(434, 36);
			this.label_batch_length.Name = "label_batch_length";
			this.label_batch_length.Size = new System.Drawing.Size(71, 17);
			this.label_batch_length.TabIndex = 38;
			this.label_batch_length.Text = "字分批长度:";
			this.textBox_BroadcastStation.Location = new System.Drawing.Point(384, 33);
			this.textBox_BroadcastStation.Name = "textBox_BroadcastStation";
			this.textBox_BroadcastStation.Size = new System.Drawing.Size(33, 23);
			this.textBox_BroadcastStation.TabIndex = 37;
			this.label_BroadcastStation.AutoSize = true;
			this.label_BroadcastStation.Location = new System.Drawing.Point(310, 36);
			this.label_BroadcastStation.Name = "label_BroadcastStation";
			this.label_BroadcastStation.Size = new System.Drawing.Size(59, 17);
			this.label_BroadcastStation.TabIndex = 36;
			this.label_BroadcastStation.Text = "广播站号:";
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(-1, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(796, 28);
			this.pipeSelectControl1.TabIndex = 34;
			this.pipeSelectControl1.TcpPortText = "502";
			this.pipeSelectControl1.UdpPortText = "502";
			this.checkBox_station_check.AutoSize = true;
			this.checkBox_station_check.Checked = true;
			this.checkBox_station_check.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_station_check.Location = new System.Drawing.Point(589, 58);
			this.checkBox_station_check.Name = "checkBox_station_check";
			this.checkBox_station_check.Size = new System.Drawing.Size(106, 21);
			this.checkBox_station_check.TabIndex = 33;
			this.checkBox_station_check.Text = "Station Check";
			this.checkBox_station_check.UseVisualStyleBackColor = true;
			this.checkBox_crc16.AutoSize = true;
			this.checkBox_crc16.Checked = true;
			this.checkBox_crc16.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_crc16.Location = new System.Drawing.Point(148, 58);
			this.checkBox_crc16.Name = "checkBox_crc16";
			this.checkBox_crc16.Size = new System.Drawing.Size(99, 21);
			this.checkBox_crc16.TabIndex = 32;
			this.checkBox_crc16.Text = "Crc16 Check";
			this.checkBox_crc16.UseVisualStyleBackColor = true;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(427, 58);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(135, 21);
			this.checkBox2.TabIndex = 31;
			this.checkBox2.Text = "读取前是否清空缓存";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox2.Location = new System.Drawing.Point(207, 33);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(75, 25);
			this.comboBox2.TabIndex = 28;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(6, 58);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(106, 21);
			this.checkBox1.TabIndex = 9;
			this.checkBox1.Text = "首地址从0开始";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox15.Location = new System.Drawing.Point(51, 33);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(42, 23);
			this.textBox15.TabIndex = 7;
			this.textBox15.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(3, 36);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(899, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(805, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(308, 58);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(87, 21);
			this.checkBox3.TabIndex = 26;
			this.checkBox3.Text = "字符串颠倒";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 119);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 522);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(995, 520);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Modbus Rtu";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(113, 36);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(88, 17);
			this.label1.TabIndex = 40;
			this.label1.Text = "DataFormat：";
			this.checkBox_DisableFunctionCode06.AutoSize = true;
			this.checkBox_DisableFunctionCode06.Location = new System.Drawing.Point(733, 58);
			this.checkBox_DisableFunctionCode06.Name = "checkBox_DisableFunctionCode06";
			this.checkBox_DisableFunctionCode06.Size = new System.Drawing.Size(163, 21);
			this.checkBox_DisableFunctionCode06.TabIndex = 42;
			this.checkBox_DisableFunctionCode06.Text = "DisableFunctionCode06";
			this.checkBox_DisableFunctionCode06.UseVisualStyleBackColor = true;
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Gray;
			this.label2.Location = new System.Drawing.Point(571, 36);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(184, 17);
			this.label2.TabIndex = 43;
			this.label2.Text = "(如果不清楚，不用管，空着就行)";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormModbusRtu";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Modbus Rtu访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormModbusRtu_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
