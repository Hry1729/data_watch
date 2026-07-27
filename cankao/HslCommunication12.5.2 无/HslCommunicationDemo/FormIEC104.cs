using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Instrument.IEC;
using HslCommunication.Instrument.IEC.Helper;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormIEC104 : HslFormContent
	{
		private IEC104 iec104 = null;

		private CodeExampleControl codeExampleControl = null;

		private List<IecValueObject<short>> list_标度化遥测值 = new List<IecValueObject<short>>();

		private List<IecValueObject<float>> list_短浮点遥测值 = new List<IecValueObject<float>>();

		private List<IecValueObject<byte>> list_单点遥信 = new List<IecValueObject<byte>>();

		private List<IecValueObject<byte>> list_双点遥信 = new List<IecValueObject<byte>>();

		private List<IecValueObject<short>> list_归一化标度值 = new List<IecValueObject<short>>();

		private List<IecValueObject<uint>> list_比特串 = new List<IecValueObject<uint>>();

		private Timer timer = new Timer();

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox10;

		private Label label13;

		private TextBox textBox_station;

		private Label label21;

		private UserControlHead userControlHead1;

		private Button button3;

		private TextBox textBox1;

		private Label label2;

		private CheckBox checkBox1;

		private Button button5;

		private TextBox textBox4;

		private Button button_u_test;

		private Button button_u_stop;

		private Button button_u_start;

		private GroupBox groupBox1;

		private Label label4;

		private Button button4;

		private Label label5;

		private Button button6;

		private Button button7;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private DataGridView dataGridView1;

		private Panel panel3;

		private RadioButton radioButton5;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private TextBox textBox_write_value;

		private Label label9;

		private TextBox textBox_write_address;

		private Label label8;

		private Label label7;

		private ComboBox comboBox_write_type;

		private Label label6;

		private Button button_write_float;

		private Button button_write_short;

		private Button button_write_bool;

		private ComboBox comboBox_write_reason;

		private Button button8;

		private PipeSelectControl pipeSelectControl1;

		private Label label_code;

		private TextBox textBox_code;

		private Button button_asdu;

		private Panel panel4;

		private Panel panel5;

		private CheckBox checkBox_log_value;

		private RadioButton radioButton6;

		private DataGridViewTextBoxColumn Column8;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column5;

		private DataGridViewTextBoxColumn Column6;

		private DataGridViewTextBoxColumn Column7;

		private DataGridViewTextBoxColumn Column4;

		private Button button9;

		private TextBox textBox2;

		private Label label1;

		public FormIEC104()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
			radioButton1.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton2.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton3.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton4.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton5.CheckedChanged += RadioButton1_CheckedChanged;
			radioButton6.CheckedChanged += RadioButton1_CheckedChanged;
			timer.Tick += Timer_Tick;
		}

		private void RadioButton1_CheckedChanged(object sender, EventArgs e)
		{
			if (radioButton1.Checked)
			{
				RenderDataGridView(dataGridView1, list_单点遥信);
			}
			else if (radioButton2.Checked)
			{
				RenderDataGridView(dataGridView1, list_双点遥信);
			}
			else if (radioButton3.Checked)
			{
				RenderDataGridView(dataGridView1, list_归一化标度值);
			}
			else if (radioButton4.Checked)
			{
				RenderDataGridView(dataGridView1, list_标度化遥测值);
			}
			else if (radioButton5.Checked)
			{
				RenderDataGridView(dataGridView1, list_短浮点遥测值);
			}
			else if (radioButton6.Checked)
			{
				RenderDataGridView(dataGridView1, list_比特串, GetBoolArrayValue);
			}
		}

		public static string GetBoolArrayValue(uint value)
		{
			bool[] array = BitConverter.GetBytes(value).ToBoolArray();
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < array.Length; i++)
			{
				stringBuilder.Append(array[i] ? "1" : "0");
			}
			return stringBuilder.ToString();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox_write_type.SelectedIndex = 0;
			comboBox_write_reason.SelectedIndex = 5;
			Language(Program.Language);
			dataGridView1.RowHeadersWidth = 70;
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "IEC104 Read Demo";
				label21.Text = "Station";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				button5.Text = "Time";
				label13.Text = "Results:";
				label6.Text = "Type:";
				label7.Text = "Reason:";
				label8.Text = "Address:";
				label9.Text = "Value:";
				button_write_bool.Text = "w-bool";
				button_write_short.Text = "w-short";
				button_write_float.Text = "w-float";
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
			if (!int.TryParse(textBox_station.Text, out var result))
			{
				DemoUtils.ShowMessage("Station Input wrong!");
				return;
			}
			iec104?.ConnectClose();
			iec104 = new IEC104();
			iec104.Station = result;
			iec104.OnIEC104MessageReceived += Iec104_IEC104MessageReceived;
			iec104.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(iec104);
				OperateResult operateResult = DeviceConnectPLC(iec104);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					codeExampleControl.SetCodeText("iec104", iec104, "Station");
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

		public static string Example()
		{
			return "iec104.OnIEC104MessageReceived += ( object sender, IEC104MessageEventArgs e ) =>\r\n{\r\n\tif (e.TypeID == IEC104.TypeID.M_ME_TF_1 || e.TypeID == IEC104.TypeID.M_ME_NC_1)\r\n\t{\r\n\t\t// 短浮点遥测值，带不带时标都处理\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 信息对象连续，解析出实际的值\r\n\t\t\tList<IecValueObject<float>> list = IecValueObject<float>.ParseFloatValue( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续，直接显示到文本框\r\n\t\t\tList<IecValueObject<float>> list = IecValueObject<float>.ParseFloatValue( e );\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.M_SP_NA_1 || e.TypeID == IEC104.TypeID.M_SP_TB_1)\r\n\t{\r\n\t\t// 单点遥信，带品质描述，带不带时标都处理\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 信息对象连续\r\n\t\t\tList<IecValueObject<byte>> list = IecValueObject<byte>.ParseYaoXinValue( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续\r\n\t\t\tList<IecValueObject<byte>> list = IecValueObject<byte>.ParseYaoXinValue( e );\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.M_DP_NA_1 || e.TypeID == IEC104.TypeID.M_DP_TB_1)\r\n\t{\r\n\t\t// 双点遥信，带品质描述，带不带时标都处理\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 信息对象连续\r\n\t\t\tList<IecValueObject<byte>> list = IecValueObject<byte>.ParseYaoXinValue( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续\r\n\t\t\tList<IecValueObject<byte>> list = IecValueObject<byte>.ParseYaoXinValue( e );\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.M_ME_ND_1)\r\n\t{\r\n\t\t// 归一化标度值\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 如果当前正在查看单点遥信界面\r\n\t\t\tList<IecValueObject<short>> list = IecValueObject<short>.ParseInt16Value( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续\r\n\t\t\tList<IecValueObject<short>> list = IecValueObject<short>.ParseInt16Value( e );\r\n\t\t\treturn;\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.M_ME_NB_1 || e.TypeID == IEC104.TypeID.M_ME_TE_1)\r\n\t{\r\n\t\t// 标度化遥测值，不带时标\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 如果当前正在查看遥测界面\r\n\t\t\tList<IecValueObject<short>> list = IecValueObject<short>.ParseInt16Value( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续\r\n\t\t\tList<IecValueObject<short>> list = IecValueObject<short>.ParseInt16Value( e );\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.M_BO_NA_1 || e.TypeID == IEC104.TypeID.M_BO_TB_1)\r\n\t{\r\n\t\t// 32比特串\r\n\t\tif (e.IsAddressContinuous)\r\n\t\t{\r\n\t\t\t// 信息对象连续\r\n\t\t\tList<IecValueObject<uint>> list = IecValueObject<uint>.ParseUInt32Value( e );\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\t// 信息对象不连续\r\n\t\t\tList<IecValueObject<uint>> list = IecValueObject<uint>.ParseUInt32Value( e );\r\n\t\t}\r\n\t}\r\n\telse if (e.TypeID == IEC104.TypeID.C_IC_NA_1)\r\n\t{\r\n\t\t// 总召唤\r\n\t\tAppendText( e, string.Empty, e.ASDU );\r\n\t\treturn;\r\n\t}\r\n};";
		}

		private void AppendText(IEC104MessageEventArgs e, string text, byte[] asdu = null)
		{
			if (asdu == null)
			{
				textBox10.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + e.ToString() + ":  " + text + Environment.NewLine);
			}
			else
			{
				textBox10.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + e.ToString() + ":  " + text + "  ASDU: " + asdu.ToHexString(' ') + Environment.NewLine);
			}
		}

		private void AppendText<T>(IEC104MessageEventArgs e, List<IecValueObject<T>> value, Func<IEC104MessageEventArgs, List<IecValueObject<T>>> func)
		{
			StringBuilder stringBuilder = new StringBuilder();
			List<IecValueObject<T>> list = func(e);
			if (value != null)
			{
				value.AddRange(list);
				value.ForEach(delegate(IecValueObject<T> m)
				{
					m.Tag = e;
				});
			}
			if (checkBox_log_value.Checked)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (e.WithTimeInfo())
					{
						stringBuilder.Append($"Address[{list[i].Address}] 品质[{list[i].Quality}] 时标[{list[i].Time}] 值[{list[i].Value}]   ");
					}
					else
					{
						stringBuilder.Append($"Address[{list[i].Address}] 品质[{list[i].Quality}] 值[{list[i].Value}]   ");
					}
				}
			}
			AppendText(e, stringBuilder.ToString());
		}

		public static void RenderDataGridView<T>(DataGridView dataGridView, List<IecValueObject<T>> value, Func<T, string> funcValue = null)
		{
			DemoUtils.DataGridSpecifyRowCount(dataGridView, value.Count);
			for (int i = 0; i < value.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView.Rows[i];
				dataGridViewRow.HeaderCell.Value = (i + 1).ToString();
				if (value[i].Tag is IEC104MessageEventArgs iEC104MessageEventArgs)
				{
					dataGridViewRow.Cells[0].Value = iEC104MessageEventArgs.StationAddress.ToString();
				}
				else
				{
					dataGridViewRow.Cells[0].Value = string.Empty;
				}
				dataGridViewRow.Cells[1].Value = value[i].Address;
				if (funcValue == null)
				{
					dataGridViewRow.Cells[2].Value = value[i].Value.ToString();
				}
				else
				{
					dataGridViewRow.Cells[2].Value = funcValue(value[i].Value);
				}
				dataGridViewRow.Cells[3].Value = ((value[i].Quality & 0x10) >> 4).ToString();
				dataGridViewRow.Cells[4].Value = ((value[i].Quality & 0x20) >> 5).ToString();
				dataGridViewRow.Cells[5].Value = ((value[i].Quality & 0x40) >> 6).ToString();
				dataGridViewRow.Cells[6].Value = ((value[i].Quality & 0x80) >> 7).ToString();
				if (value[i].Time != DateTime.MinValue)
				{
					dataGridViewRow.Cells[7].Value = value[i].Time.ToString("yyyy-MM-dd HH:mm:ss");
				}
				else
				{
					dataGridViewRow.Cells[7].Value = string.Empty;
				}
				dataGridViewRow.Tag = value[i];
			}
		}

		private void ListIni()
		{
			list_标度化遥测值 = new List<IecValueObject<short>>();
			list_短浮点遥测值 = new List<IecValueObject<float>>();
			list_单点遥信 = new List<IecValueObject<byte>>();
			list_双点遥信 = new List<IecValueObject<byte>>();
			list_归一化标度值 = new List<IecValueObject<short>>();
			list_比特串 = new List<IecValueObject<uint>>();
		}

		private void Iec104_IEC104MessageReceived(object sender, IEC104MessageEventArgs e)
		{
			if (checkBox1.Checked)
			{
				return;
			}
			Invoke((Action)delegate
			{
				if (e.TypeID == 36 || e.TypeID == 13)
				{
					AppendText(e, list_短浮点遥测值, IecValueObject<float>.ParseFloatValue);
				}
				else if (e.TypeID == 1 || e.TypeID == 30)
				{
					AppendText(e, list_单点遥信, IecValueObject<byte>.ParseYaoXinValue);
				}
				else if (e.TypeID == 3 || e.TypeID == 31)
				{
					AppendText(e, list_双点遥信, IecValueObject<byte>.ParseYaoXinValue);
				}
				else if (e.TypeID == 21 || e.TypeID == 9 || e.TypeID == 34)
				{
					AppendText(e, list_归一化标度值, IecValueObject<short>.ParseInt16Value);
				}
				else if (e.TypeID == 11 || e.TypeID == 35)
				{
					AppendText(e, list_标度化遥测值, IecValueObject<short>.ParseInt16Value);
				}
				else if (e.TypeID == 7 || e.TypeID == 33)
				{
					AppendText(e, list_比特串, IecValueObject<uint>.ParseUInt32Value);
				}
				else if (e.TypeID == 100)
				{
					if (e.TransmissionReason == 7)
					{
						ListIni();
					}
					else if (e.TransmissionReason == 10)
					{
						RadioButton1_CheckedChanged(null, EventArgs.Empty);
					}
					AppendText(e, string.Empty, e.ASDU);
				}
				else if (e.TypeID == 15)
				{
					AppendText(e, null, IecValueObject<uint>.ParseUInt32Value);
				}
				else
				{
					AppendText(e, string.Empty, e.ASDU);
				}
			});
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
			pipeSelectControl1.ExtraCloseAction(iec104);
			iec104.ConnectClose();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameIMessage(SoftBasic.HexStringToBytes(textBox1.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Failed：" + operateResult.ToMessageShowString());
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_station.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox_station.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button5_Click(object sender, EventArgs e)
		{
			textBox4.Text = IECHelper.GetAbsoluteTimeScale(DateTime.Now, true).ToHexString(' ');
		}

		private void button_u_start_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameUMessage(7);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.SendFrameUMessage( 0x07 );";
		}

		private void button_u_stop_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameUMessage(19);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.SendFrameUMessage( 0x13 );";
		}

		private void button_u_test_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameUMessage(67);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.SendFrameUMessage( 0x43 );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.TotalSubscriptions(20);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.TotalSubscriptions( );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.TotalSubscriptions(1);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.TotalSubscriptions( 0x01 );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.TotalSubscriptions(3);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.TotalSubscriptions( 0x03 );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameIMessage(new byte[10] { 101, 1, 7, 0, 1, 0, 0, 0, 0, 1 });
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.SendFrameIMessage( new byte[] { 0x65, 0x01, 0x07, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x01 } );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void WriteIec(byte[] value)
		{
			byte b = byte.Parse(Regex.Match(comboBox_write_type.SelectedItem.ToString(), "^[0-9]+").Value);
			ushort num = ushort.Parse(Regex.Match(comboBox_write_reason.SelectedItem.ToString(), "^[0-9]+").Value);
			ushort num2 = ushort.Parse(textBox_write_address.Text);
			OperateResult operateResult = iec104.WriteIec(b, num, num2, value);
			textBox_code.Text = $"OperateResult send = iec104.WriteIec( \"{b}\", \"{num}\", \"{num2}\", \"{value.ToHexString()}\".ToHexBytes( ) );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
		}

		private void button_write_bool_Click(object sender, EventArgs e)
		{
			bool flag = false;
			WriteIec((!(textBox_write_value.Text == "1") && (textBox_write_value.Text == "0" || !bool.Parse(textBox_write_value.Text))) ? new byte[1] : new byte[1] { 1 });
		}

		private void button_write_short_Click(object sender, EventArgs e)
		{
			WriteIec(BitConverter.GetBytes(short.Parse(textBox_write_value.Text)));
		}

		private void button_write_float_Click(object sender, EventArgs e)
		{
			WriteIec(BitConverter.GetBytes(float.Parse(textBox_write_value.Text)));
		}

		private void button_apdu_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameIMessage(textBox1.Text.ToHexBytes());
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Send Success!");
			}
			else
			{
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = iec104.SendFrameIMessage( \"" + textBox1.Text + "\".ToHexBytes( ) );";
			codeExampleControl.ReaderReadCode(textBox_code.Text);
		}

		private void button9_Click(object sender, EventArgs e)
		{
			if (timer.Enabled)
			{
				timer.Stop();
				button9.Text = "Start UTest";
			}
			else
			{
				timer.Interval = 1000;
				timer.Start();
				button9.Text = "Stop UTest";
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			OperateResult operateResult = iec104.SendFrameUMessage(67);
			if (!operateResult.IsSuccess)
			{
				button9_Click(button9, e);
				DemoUtils.ShowMessage("Send failed: " + operateResult.Message);
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.panel1 = new System.Windows.Forms.Panel();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.textBox_station = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel4 = new System.Windows.Forms.Panel();
			this.label2 = new System.Windows.Forms.Label();
			this.button_asdu = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button3 = new System.Windows.Forms.Button();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.checkBox_log_value = new System.Windows.Forms.CheckBox();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.panel5 = new System.Windows.Forms.Panel();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton6 = new System.Windows.Forms.RadioButton();
			this.radioButton5 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column8 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button9 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label_code = new System.Windows.Forms.Label();
			this.button8 = new System.Windows.Forms.Button();
			this.comboBox_write_reason = new System.Windows.Forms.ComboBox();
			this.button_write_float = new System.Windows.Forms.Button();
			this.button_write_short = new System.Windows.Forms.Button();
			this.button_write_bool = new System.Windows.Forms.Button();
			this.textBox_write_value = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_write_address = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.comboBox_write_type = new System.Windows.Forms.ComboBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.label5 = new System.Windows.Forms.Label();
			this.button_u_test = new System.Windows.Forms.Button();
			this.button_u_start = new System.Windows.Forms.Button();
			this.button_u_stop = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.panel4.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.panel5.SuspendLayout();
			this.panel3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox_station);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 68);
			this.panel1.TabIndex = 0;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(5, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 8;
			this.pipeSelectControl1.TcpPortText = "2404";
			this.pipeSelectControl1.UdpPortText = "2404";
			this.textBox_station.Location = new System.Drawing.Point(81, 39);
			this.textBox_station.Name = "textBox_station";
			this.textBox_station.Size = new System.Drawing.Size(97, 23);
			this.textBox_station.TabIndex = 7;
			this.textBox_station.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(6, 42);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(68, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "公共地址：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(895, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(798, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Location = new System.Drawing.Point(3, 107);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 535);
			this.panel2.TabIndex = 1;
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Location = new System.Drawing.Point(4, 198);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(988, 332);
			this.tabControl1.TabIndex = 4;
			this.tabPage1.Controls.Add(this.panel4);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(980, 302);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "IEC Log";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.panel4.Controls.Add(this.label2);
			this.panel4.Controls.Add(this.button_asdu);
			this.panel4.Controls.Add(this.button5);
			this.panel4.Controls.Add(this.textBox10);
			this.panel4.Controls.Add(this.textBox4);
			this.panel4.Controls.Add(this.label13);
			this.panel4.Controls.Add(this.textBox1);
			this.panel4.Controls.Add(this.button3);
			this.panel4.Controls.Add(this.checkBox1);
			this.panel4.Controls.Add(this.checkBox_log_value);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(3, 3);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(974, 296);
			this.panel4.TabIndex = 20;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(5, 10);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 11;
			this.label2.Text = "报文：";
			this.button_asdu.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_asdu.Location = new System.Drawing.Point(574, 4);
			this.button_asdu.Name = "button_asdu";
			this.button_asdu.Size = new System.Drawing.Size(67, 28);
			this.button_asdu.TabIndex = 19;
			this.button_asdu.Text = "ASDU";
			this.button_asdu.UseVisualStyleBackColor = true;
			this.button_asdu.Click += new System.EventHandler(button_apdu_Click);
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(915, 3);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(53, 28);
			this.button5.TabIndex = 18;
			this.button5.Text = "时间";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox10.Location = new System.Drawing.Point(62, 36);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox10.Size = new System.Drawing.Size(906, 257);
			this.textBox10.TabIndex = 10;
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(747, 6);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(156, 23);
			this.textBox4.TabIndex = 17;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(8, 39);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 9;
			this.label13.Text = "结果：";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(62, 7);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(506, 23);
			this.textBox1.TabIndex = 12;
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(647, 4);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(81, 28);
			this.button3.TabIndex = 13;
			this.button3.Text = "I帧报文";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(5, 61);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(58, 21);
			this.checkBox1.TabIndex = 15;
			this.checkBox1.Text = "STOP";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox_log_value.AutoSize = true;
			this.checkBox_log_value.Checked = true;
			this.checkBox_log_value.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_log_value.Location = new System.Drawing.Point(5, 85);
			this.checkBox_log_value.Name = "checkBox_log_value";
			this.checkBox_log_value.Size = new System.Drawing.Size(59, 21);
			this.checkBox_log_value.TabIndex = 20;
			this.checkBox_log_value.Text = "Value";
			this.checkBox_log_value.UseVisualStyleBackColor = true;
			this.tabPage2.Controls.Add(this.panel5);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(980, 302);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "DataTable";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.panel5.Controls.Add(this.panel3);
			this.panel5.Controls.Add(this.dataGridView1);
			this.panel5.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel5.Location = new System.Drawing.Point(3, 3);
			this.panel5.Name = "panel5";
			this.panel5.Size = new System.Drawing.Size(974, 296);
			this.panel5.TabIndex = 2;
			this.panel3.Controls.Add(this.radioButton6);
			this.panel3.Controls.Add(this.radioButton5);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton2);
			this.panel3.Controls.Add(this.radioButton1);
			this.panel3.Location = new System.Drawing.Point(3, 3);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(620, 30);
			this.panel3.TabIndex = 1;
			this.radioButton6.AutoSize = true;
			this.radioButton6.Location = new System.Drawing.Point(489, 4);
			this.radioButton6.Name = "radioButton6";
			this.radioButton6.Size = new System.Drawing.Size(88, 21);
			this.radioButton6.TabIndex = 5;
			this.radioButton6.Text = "32位比特串";
			this.radioButton6.UseVisualStyleBackColor = true;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(384, 4);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(86, 21);
			this.radioButton5.TabIndex = 4;
			this.radioButton5.Text = "短浮点遥测";
			this.radioButton5.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Location = new System.Drawing.Point(280, 4);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(86, 21);
			this.radioButton4.TabIndex = 3;
			this.radioButton4.Text = "标度化遥测";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(179, 4);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(86, 21);
			this.radioButton3.TabIndex = 2;
			this.radioButton3.Text = "归一化遥测";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(90, 4);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(74, 21);
			this.radioButton2.TabIndex = 1;
			this.radioButton2.Text = "双点遥信";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(4, 4);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(74, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "单点遥信";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.Azure;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column8, this.Column1, this.Column2, this.Column3, this.Column5, this.Column6, this.Column7, this.Column4);
			this.dataGridView1.Location = new System.Drawing.Point(4, 34);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(967, 259);
			this.dataGridView1.TabIndex = 0;
			this.Column8.HeaderText = "Pub-Add";
			this.Column8.Name = "Column8";
			this.Column8.ReadOnly = true;
			this.Column1.HeaderText = "Address";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 102;
			this.Column2.HeaderText = "Value";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 290;
			this.Column3.HeaderText = "BL";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.ToolTipText = "封锁标志";
			this.Column3.Width = 50;
			this.Column5.HeaderText = "SB";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column5.ToolTipText = "取代标志";
			this.Column5.Width = 50;
			this.Column6.HeaderText = "NT";
			this.Column6.Name = "Column6";
			this.Column6.ReadOnly = true;
			this.Column6.ToolTipText = "刷新标志";
			this.Column6.Width = 50;
			this.Column7.HeaderText = "IV";
			this.Column7.Name = "Column7";
			this.Column7.ReadOnly = true;
			this.Column7.ToolTipText = "有效标志";
			this.Column7.Width = 50;
			this.Column4.HeaderText = "Time";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Width = 180;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button9);
			this.groupBox1.Controls.Add(this.textBox2);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.textBox_code);
			this.groupBox1.Controls.Add(this.label_code);
			this.groupBox1.Controls.Add(this.button8);
			this.groupBox1.Controls.Add(this.comboBox_write_reason);
			this.groupBox1.Controls.Add(this.button_write_float);
			this.groupBox1.Controls.Add(this.button_write_short);
			this.groupBox1.Controls.Add(this.button_write_bool);
			this.groupBox1.Controls.Add(this.textBox_write_value);
			this.groupBox1.Controls.Add(this.label9);
			this.groupBox1.Controls.Add(this.textBox_write_address);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.comboBox_write_type);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.button7);
			this.groupBox1.Controls.Add(this.button6);
			this.groupBox1.Controls.Add(this.button4);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.button_u_test);
			this.groupBox1.Controls.Add(this.button_u_start);
			this.groupBox1.Controls.Add(this.button_u_stop);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Location = new System.Drawing.Point(4, 2);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(988, 190);
			this.groupBox1.TabIndex = 3;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Button Test";
			this.button9.Location = new System.Drawing.Point(490, 89);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(96, 28);
			this.button9.TabIndex = 44;
			this.button9.Text = "Start UTest";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.textBox2.Location = new System.Drawing.Point(428, 92);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(56, 23);
			this.textBox2.TabIndex = 43;
			this.textBox2.Text = "1000";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(353, 95);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 42;
			this.label1.Text = "时间间隔：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(68, 134);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ReadOnly = true;
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(914, 50);
			this.textBox_code.TabIndex = 41;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(4, 138);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(35, 17);
			this.label_code.TabIndex = 40;
			this.label_code.Text = "代码:";
			this.button8.Location = new System.Drawing.Point(66, 53);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(104, 28);
			this.button8.TabIndex = 39;
			this.button8.Text = "积累量总召唤";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.comboBox_write_reason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_write_reason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_write_reason.FormattingEnabled = true;
			this.comboBox_write_reason.Items.AddRange(new object[10] { "1 (周期扫描)", "2 (背景扫描)", "3 (突发)", "4 (初始化)", "5 (请求或被请求)", "6 (激活)", "7 (激活确认)", "8 (停止激活)", "9 (停止激活确认)", "10 (激活终止)" });
			this.comboBox_write_reason.Location = new System.Drawing.Point(678, 47);
			this.comboBox_write_reason.Name = "comboBox_write_reason";
			this.comboBox_write_reason.Size = new System.Drawing.Size(197, 25);
			this.comboBox_write_reason.TabIndex = 38;
			this.button_write_float.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_float.Location = new System.Drawing.Point(890, 75);
			this.button_write_float.Name = "button_write_float";
			this.button_write_float.Size = new System.Drawing.Size(92, 27);
			this.button_write_float.TabIndex = 37;
			this.button_write_float.Text = "float写入";
			this.button_write_float.UseVisualStyleBackColor = true;
			this.button_write_float.Click += new System.EventHandler(button_write_float_Click);
			this.button_write_short.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_short.Location = new System.Drawing.Point(890, 47);
			this.button_write_short.Name = "button_write_short";
			this.button_write_short.Size = new System.Drawing.Size(92, 27);
			this.button_write_short.TabIndex = 36;
			this.button_write_short.Text = "short写入";
			this.button_write_short.UseVisualStyleBackColor = true;
			this.button_write_short.Click += new System.EventHandler(button_write_short_Click);
			this.button_write_bool.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_bool.Location = new System.Drawing.Point(889, 16);
			this.button_write_bool.Name = "button_write_bool";
			this.button_write_bool.Size = new System.Drawing.Size(92, 27);
			this.button_write_bool.TabIndex = 35;
			this.button_write_bool.Text = "bool写入";
			this.button_write_bool.UseVisualStyleBackColor = true;
			this.button_write_bool.Click += new System.EventHandler(button_write_bool_Click);
			this.textBox_write_value.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_write_value.Location = new System.Drawing.Point(678, 104);
			this.textBox_write_value.Name = "textBox_write_value";
			this.textBox_write_value.Size = new System.Drawing.Size(197, 23);
			this.textBox_write_value.TabIndex = 34;
			this.textBox_write_value.Text = "1";
			this.label9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(622, 106);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(23, 17);
			this.label9.TabIndex = 33;
			this.label9.Text = "值:";
			this.textBox_write_address.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_write_address.Location = new System.Drawing.Point(678, 77);
			this.textBox_write_address.Name = "textBox_write_address";
			this.textBox_write_address.Size = new System.Drawing.Size(197, 23);
			this.textBox_write_address.TabIndex = 32;
			this.textBox_write_address.Text = "1";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(622, 80);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(35, 17);
			this.label8.TabIndex = 31;
			this.label8.Text = "地址:";
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(622, 52);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(35, 17);
			this.label7.TabIndex = 29;
			this.label7.Text = "原因:";
			this.comboBox_write_type.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_write_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_write_type.FormattingEnabled = true;
			this.comboBox_write_type.Items.AddRange(new object[7] { "45 (单点遥控)", "46 (双点遥控)", "47 (升降遥控)", "48 (归一化设定值)", "49 (标度化设定值)", "50 (短浮点设定值)", "51 (32比特串)" });
			this.comboBox_write_type.Location = new System.Drawing.Point(678, 17);
			this.comboBox_write_type.Name = "comboBox_write_type";
			this.comboBox_write_type.Size = new System.Drawing.Size(197, 25);
			this.comboBox_write_type.TabIndex = 28;
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(622, 22);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(35, 17);
			this.label6.TabIndex = 27;
			this.label6.Text = "类型:";
			this.button7.Location = new System.Drawing.Point(286, 19);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(104, 28);
			this.button7.TabIndex = 26;
			this.button7.Text = "越限总召唤";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(176, 19);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(104, 28);
			this.button6.TabIndex = 25;
			this.button6.Text = "定时总召唤";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button4.Location = new System.Drawing.Point(66, 19);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(104, 28);
			this.button4.TabIndex = 24;
			this.button4.Text = "响应总召唤";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(4, 25);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(51, 17);
			this.label5.TabIndex = 23;
			this.label5.Text = "I帧命令:";
			this.button_u_test.Location = new System.Drawing.Point(240, 89);
			this.button_u_test.Name = "button_u_test";
			this.button_u_test.Size = new System.Drawing.Size(81, 28);
			this.button_u_test.TabIndex = 21;
			this.button_u_test.Text = "U-Test";
			this.button_u_test.UseVisualStyleBackColor = true;
			this.button_u_test.Click += new System.EventHandler(button_u_test_Click);
			this.button_u_start.Location = new System.Drawing.Point(66, 89);
			this.button_u_start.Name = "button_u_start";
			this.button_u_start.Size = new System.Drawing.Size(81, 28);
			this.button_u_start.TabIndex = 19;
			this.button_u_start.Text = "U-Start";
			this.button_u_start.UseVisualStyleBackColor = true;
			this.button_u_start.Click += new System.EventHandler(button_u_start_Click);
			this.button_u_stop.Location = new System.Drawing.Point(153, 89);
			this.button_u_stop.Name = "button_u_stop";
			this.button_u_stop.Size = new System.Drawing.Size(81, 28);
			this.button_u_stop.TabIndex = 20;
			this.button_u_stop.Text = "U-Stop";
			this.button_u_stop.UseVisualStyleBackColor = true;
			this.button_u_stop.Click += new System.EventHandler(button_u_stop_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(4, 94);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(56, 17);
			this.label4.TabIndex = 22;
			this.label4.Text = "U帧命令:";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "IEC104";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
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
			base.Name = "FormIEC104";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "IEC104访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.panel5.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
