using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.Robot.YASKAWA;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormYRCHighEthernet : HslFormContent
	{
		private YRCHighEthernet yrc = null;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Label label1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private TextBox textBox7;

		private Label label9;

		private TextBox textBox_command;

		private Label label10;

		private TextBox textBox4;

		private Label label7;

		private Label label22;

		private Label label21;

		private UserControlHead userControlHead1;

		private Button button4;

		private Button button3;

		private Button button18;

		private Label label5;

		private Button button17;

		private Button button16;

		private Button button15;

		private Button button13;

		private Label label4;

		private Button button12;

		private Button button11;

		private Button button10;

		private Button button9;

		private Button button8;

		private TextBox textBox5;

		private Label label2;

		private Button button7;

		private Button button6;

		private Button button5;

		private Label label11;

		private ComboBox comboBox1;

		private Button button22;

		private Button button21;

		private Label label12;

		private TextBox textBox_dataHandle;

		private Label label15;

		private TextBox textBox_dataAttribute;

		private Label label14;

		private TextBox textBox_dataAddress;

		private Label label13;

		private Label label16;

		private TextBox textBox_dataPart;

		private Button button14;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private Button button23;

		private Button button24;

		private Button button25;

		private Button button19;

		private TextBox textBox6;

		private Label label8;

		private TextBox textBox3;

		private Label label6;

		private Button button27;

		private Button button26;

		private Button button20;

		private Button button28;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private Button button29;

		private Button button30;

		private Button button31;

		private Label label17;

		private Button button33;

		private Button button32;

		private TextBox textBox_code;

		private Label label_code;

		private Label label18;

		private TextBox textBox_code2;

		private Button button34;

		private Button button35;

		private Panel panel3;

		private RadioButton radioButton5;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		public FormYRCHighEthernet()
		{
			InitializeComponent();
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			comboBox1.DataSource = new string[12]
			{
				"基坐标", "机器坐标", "用户1", "用户2", "用户3", "用户4", "用户5", "用户6", "用户7", "用户8",
				"用户9", "用户10"
			};
			comboBox1.SelectedIndex = 0;
			Language(Program.Language);
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "YASKAWA Robot Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label21.Text = "Address:";
				label7.Text = "result:";
				label10.Text = "Address:";
				label9.Text = "Value:";
				tabPage1.Text = "Single Data Read test";
				tabPage2.Text = "Single Data Write test";
				label22.Text = "Parameter name of robot";
				label_code.Text = "Code:";
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
				DemoUtils.ShowMessage("端口输入格式不正确！");
				return;
			}
			yrc = new YRCHighEthernet(textBox1.Text, result);
			yrc.LogNet = new LogNetSingle("");
			try
			{
				DemoUtils.ShowMessage("打开UDP成功！");
				button2.Enabled = true;
				button1.Enabled = false;
				panel2.Enabled = true;
				codeExampleControl.SetCodeText("robot", yrc);
				textBox_code.Text = $"YRCHighEthernet yrc = new YRCHighEthernet( \"{textBox1.Text}\", {result} );";
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox4.AppendText(e.HslMessage.ToString());
			});
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
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

		private void button14_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = DemoUtils.ParseInt(textBox_command.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Command input wrong: " + operateResult.Message);
				return;
			}
			OperateResult<int> operateResult2 = DemoUtils.ParseInt(textBox_dataAddress.Text);
			if (!operateResult2.IsSuccess)
			{
				DemoUtils.ShowMessage("Data Address input wrong: " + operateResult2.Message);
				return;
			}
			OperateResult<int> operateResult3 = DemoUtils.ParseInt(textBox_dataAttribute.Text);
			if (!operateResult3.IsSuccess)
			{
				DemoUtils.ShowMessage("单元编号输入错误:" + operateResult3.Message);
				return;
			}
			OperateResult<int> operateResult4 = DemoUtils.ParseInt(textBox_dataHandle.Text);
			if (!operateResult4.IsSuccess)
			{
				DemoUtils.ShowMessage("处理请求输入错误:" + operateResult3.Message);
				return;
			}
			byte[] dataPart = (string.IsNullOrEmpty(textBox_dataPart.Text) ? new byte[0] : textBox_dataPart.Text.ToHexBytes());
			OperateResult<byte[]> operateResult5 = yrc.ReadCommand((ushort)operateResult.Content, (ushort)operateResult2.Content, (byte)operateResult3.Content, (byte)operateResult4.Content, dataPart);
			if (!operateResult5.IsSuccess)
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult5.Message);
				return;
			}
			if (radioButton1.Checked)
			{
				textBox7.Text = operateResult5.Content.ToHexString(' ');
			}
			else if (radioButton2.Checked)
			{
				textBox7.Text = SoftBasic.GetAsciiStringRender(operateResult5.Content);
			}
			else if (radioButton3.Checked)
			{
				short[] array = new short[operateResult5.Content.Length / 2];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = BitConverter.ToInt16(operateResult5.Content, i * 2);
				}
				textBox7.Text = array.ToArrayString();
			}
			else if (radioButton4.Checked)
			{
				int[] array2 = new int[operateResult5.Content.Length / 4];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = BitConverter.ToInt32(operateResult5.Content, j * 4);
				}
				textBox7.Text = array2.ToArrayString();
			}
			else if (radioButton5.Checked)
			{
				float[] array3 = new float[operateResult5.Content.Length / 4];
				for (int k = 0; k < array3.Length; k++)
				{
					array3[k] = BitConverter.ToSingle(operateResult5.Content, k * 4);
				}
				textBox7.Text = array3.ToArrayString();
			}
			else
			{
				textBox7.Text = "Unknown data type!";
			}
			textBox_code2.Text = $"OperateResult<byte[]> read = yrc.ReadCommand( {operateResult.Content}, {operateResult2.Content}, {operateResult3.Content}, {operateResult4.Content}, \"{textBox_dataPart.Text}\".ToHexBytes( ) );";
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<YRCAlarmItem[]> operateResult = yrc.ReadAlarms();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<YRCAlarmItem[]> read = yrc.ReadAlarms( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult<string[]> operateResult = yrc.ReadPose();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string[]> read = yrc.ReadPose( );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<YRCRobotData> operateResult = yrc.ReadPOSC(comboBox1.SelectedIndex, true);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = $"OperateResult<YRCRobotData> read = yrc.ReadPOSC( {comboBox1.SelectedIndex}, true );";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult<bool[]> operateResult = yrc.ReadStats();
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + "[ 0] 单步              " + operateResult.Content[0] + Environment.NewLine + "[ 1] 1循环             " + operateResult.Content[1] + Environment.NewLine + "[ 2] 自动连续          " + operateResult.Content[2] + Environment.NewLine + "[ 3] 运行中            " + operateResult.Content[3] + Environment.NewLine + "[ 4] 运转中            " + operateResult.Content[4] + Environment.NewLine + "[ 5] 示教              " + operateResult.Content[5] + Environment.NewLine + "[ 6] 在线              " + operateResult.Content[6] + Environment.NewLine + "[ 7] 命令模式          " + operateResult.Content[7] + Environment.NewLine + "[ 9] 示教编程器HOLD中   " + operateResult.Content[9] + Environment.NewLine + "[10] 外部HOLD中        " + operateResult.Content[10] + Environment.NewLine + "[11] 命令HOLD中        " + operateResult.Content[11] + Environment.NewLine + "[12] 发生警报          " + operateResult.Content[12] + Environment.NewLine + "[13] 发生错误          " + operateResult.Content[13] + Environment.NewLine + "[14] 伺服ON            " + operateResult.Content[14] + Environment.NewLine;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<bool[]> read = yrc.ReadStats( );";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult<string[]> operateResult = yrc.ReadJSeq(1);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + "[0]程序名: " + operateResult.Content[0] + Environment.NewLine + "[1]行编号: " + operateResult.Content[1] + Environment.NewLine + "[2]步编号: " + operateResult.Content[2] + Environment.NewLine + "[3]速度超出值: " + operateResult.Content[3] + Environment.NewLine;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string[]> read = yrc.ReadJSeq( );";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadByteVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<byte> read = yrc.ReadByteVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadIntegerVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<short> read = yrc.ReadIntegerVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button10_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadDoubleIntegerVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<int> read = yrc.ReadDoubleIntegerVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button11_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadRealVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<float> read = yrc.ReadRealVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button12_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadStringVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<string> read = yrc.ReadStringVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button34_Click(object sender, EventArgs e)
		{
			OperateResult<float[]> operateResult = yrc.ReadPositionVariable(ushort.Parse(textBox5.Text));
			if (operateResult.IsSuccess)
			{
				try
				{
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.AppendLine("数据类型: " + operateResult.Content[0] + "   0:脉冲型  16:基座坐标值  17:机器人坐标值  18: 用户坐标值  19: 工具坐标值");
					stringBuilder.AppendLine("形式: " + operateResult.Content[1]);
					stringBuilder.AppendLine("工具编号" + operateResult.Content[2]);
					stringBuilder.AppendLine("用户坐标编号:" + operateResult.Content[3]);
					stringBuilder.AppendLine("扩张形态:" + operateResult.Content[4]);
					stringBuilder.Append("坐标数据:" + operateResult.Content.RemoveBegin(5).ToArrayString());
					DemoUtils.ReadResultRender(OperateResult.CreateSuccessResult(stringBuilder.ToString()), textBox5.Text, textBox4);
				}
				catch
				{
					DemoUtils.ReadResultRender(operateResult, textBox5.Text, textBox4);
				}
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<float[]> read = yrc.ReadPositionVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button35_Click(object sender, EventArgs e)
		{
			DemoUtils.ReadResultRender(yrc.ReadBasePositionVariable(ushort.Parse(textBox5.Text)), textBox5.Text, textBox4);
			textBox_code.Text = "OperateResult<float[]> read = yrc.ReadBasePositionVariable( ushort.Parse( \"" + textBox5.Text + "\" ) )";
		}

		private void button19_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(yrc.WriteStringVariable(ushort.Parse(textBox3.Text), textBox6.Text), textBox3.Text);
			textBox_code.Text = "OperateResult write = yrc.WriteStringVariable( ushort.Parse( \"" + textBox3.Text + "\" ), \"" + textBox6.Text + "\" )";
		}

		private void button20_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(yrc.WriteIntegerVariable(ushort.Parse(textBox3.Text), short.Parse(textBox6.Text)), textBox3.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Write Failed: " + ex.Message);
			}
			textBox_code.Text = "OperateResult write = yrc.WriteIntegerVariable( ushort.Parse( \"" + textBox3.Text + "\" ), short.Parse( \"" + textBox6.Text + "\" ) )";
		}

		private void button26_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(yrc.WriteDoubleIntegerVariable(ushort.Parse(textBox3.Text), int.Parse(textBox6.Text)), textBox3.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Write Failed: " + ex.Message);
			}
			textBox_code.Text = "OperateResult write = yrc.WriteDoubleIntegerVariable( ushort.Parse( \"" + textBox3.Text + "\" ), int.Parse( \"" + textBox6.Text + "\" ) )";
		}

		private void button27_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(yrc.WriteRealVariable(ushort.Parse(textBox3.Text), float.Parse(textBox6.Text)), textBox3.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Write Failed: " + ex.Message);
			}
			textBox_code.Text = "OperateResult write = yrc.WriteRealVariable( ushort.Parse( \"" + textBox3.Text + "\" ), float.Parse( \"" + textBox6.Text + "\" ) )";
		}

		private void button28_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(yrc.WriteByteVariable(ushort.Parse(textBox3.Text), byte.Parse(textBox6.Text)), textBox3.Text);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Write Failed: " + ex.Message);
			}
			textBox_code.Text = "OperateResult write = yrc.WriteByteVariable( ushort.Parse( \"" + textBox3.Text + "\" ), byte.Parse( \"" + textBox6.Text + "\" ) )";
		}

		private void button13_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Hold(true);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("HOLD ON Success");
			}
			else
			{
				DemoUtils.ShowMessage("HOLD ON Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Hold( true );";
		}

		private void button15_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Hold(false);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("HOLD OFF Success");
			}
			else
			{
				DemoUtils.ShowMessage("HOLD OFF Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Hold( false );";
		}

		private void button16_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Reset();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("RESET Success");
			}
			else
			{
				DemoUtils.ShowMessage("RESET Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Reset( );";
		}

		private void button17_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Cancel();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Cancel Success");
			}
			else
			{
				DemoUtils.ShowMessage("Cancel Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Cancel( );";
		}

		private void button21_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Svon(true);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SVON ON Success");
			}
			else
			{
				DemoUtils.ShowMessage("SVON ON Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Svon( true );";
		}

		private void button22_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Svon(false);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SVON OFF Success");
			}
			else
			{
				DemoUtils.ShowMessage("SVON OFF Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Svon( false );";
		}

		private void button18_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yrc.Start();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Start Success");
			}
			else
			{
				DemoUtils.ShowMessage("Start Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult op = yrc.Start( );";
		}

		private void button23_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = yrc.ReadManagementTime(1);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<DateTime> read = yrc.ReadManagementTime( 1 );";
		}

		private void button24_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = yrc.ReadManagementTime(10);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<DateTime> read = yrc.ReadManagementTime( 10 );";
		}

		private void button25_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = yrc.ReadManagementTime(210);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<DateTime> read = yrc.ReadManagementTime( 210 );";
		}

		private void button32_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = yrc.ReadManagementTime(110);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<DateTime> read = yrc.ReadManagementTime( 110 );";
		}

		private void button31_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = yrc.ReadManagementTimeSpan(1);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = yrc.ReadManagementTimeSpan( 1 );";
		}

		private void button30_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = yrc.ReadManagementTimeSpan(10);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = yrc.ReadManagementTimeSpan( 10 );";
		}

		private void button29_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = yrc.ReadManagementTimeSpan(210);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = yrc.ReadManagementTimeSpan( 210 );";
		}

		private void button33_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = yrc.ReadManagementTimeSpan(110);
			if (operateResult.IsSuccess)
			{
				textBox4.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = yrc.ReadManagementTimeSpan( 110 );";
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
			this.label22 = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.button35 = new System.Windows.Forms.Button();
			this.button34 = new System.Windows.Forms.Button();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label_code = new System.Windows.Forms.Label();
			this.button33 = new System.Windows.Forms.Button();
			this.button32 = new System.Windows.Forms.Button();
			this.button29 = new System.Windows.Forms.Button();
			this.button30 = new System.Windows.Forms.Button();
			this.button31 = new System.Windows.Forms.Button();
			this.label17 = new System.Windows.Forms.Label();
			this.button28 = new System.Windows.Forms.Button();
			this.button27 = new System.Windows.Forms.Button();
			this.label7 = new System.Windows.Forms.Label();
			this.button26 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.button20 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button19 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.button5 = new System.Windows.Forms.Button();
			this.label8 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.button7 = new System.Windows.Forms.Button();
			this.label6 = new System.Windows.Forms.Label();
			this.button25 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.button24 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button23 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button22 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button21 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button12 = new System.Windows.Forms.Button();
			this.button18 = new System.Windows.Forms.Button();
			this.label5 = new System.Windows.Forms.Label();
			this.button13 = new System.Windows.Forms.Button();
			this.button17 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.label11 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.textBox_code2 = new System.Windows.Forms.TextBox();
			this.label18 = new System.Windows.Forms.Label();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.textBox_command = new System.Windows.Forms.TextBox();
			this.button14 = new System.Windows.Forms.Button();
			this.textBox_dataPart = new System.Windows.Forms.TextBox();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_dataHandle = new System.Windows.Forms.TextBox();
			this.textBox_dataAddress = new System.Windows.Forms.TextBox();
			this.textBox_dataAttribute = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.radioButton5 = new System.Windows.Forms.RadioButton();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label22);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(967, 42);
			this.panel1.TabIndex = 0;
			this.label22.Location = new System.Drawing.Point(651, 0);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(341, 45);
			this.label22.TabIndex = 7;
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(581, 0);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(80, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "自定义示例：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(468, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(368, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(272, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(83, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "10040";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(218, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 9);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(150, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "192.168.0.100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Location = new System.Drawing.Point(3, 80);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(967, 503);
			this.panel2.TabIndex = 1;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(965, 501);
			this.tabControl1.TabIndex = 2;
			this.tabPage1.Controls.Add(this.button35);
			this.tabPage1.Controls.Add(this.button34);
			this.tabPage1.Controls.Add(this.textBox_code);
			this.tabPage1.Controls.Add(this.label_code);
			this.tabPage1.Controls.Add(this.button33);
			this.tabPage1.Controls.Add(this.button32);
			this.tabPage1.Controls.Add(this.button29);
			this.tabPage1.Controls.Add(this.button30);
			this.tabPage1.Controls.Add(this.button31);
			this.tabPage1.Controls.Add(this.label17);
			this.tabPage1.Controls.Add(this.button28);
			this.tabPage1.Controls.Add(this.button27);
			this.tabPage1.Controls.Add(this.label7);
			this.tabPage1.Controls.Add(this.button26);
			this.tabPage1.Controls.Add(this.textBox4);
			this.tabPage1.Controls.Add(this.button20);
			this.tabPage1.Controls.Add(this.button3);
			this.tabPage1.Controls.Add(this.button19);
			this.tabPage1.Controls.Add(this.button4);
			this.tabPage1.Controls.Add(this.textBox6);
			this.tabPage1.Controls.Add(this.button5);
			this.tabPage1.Controls.Add(this.label8);
			this.tabPage1.Controls.Add(this.button6);
			this.tabPage1.Controls.Add(this.textBox3);
			this.tabPage1.Controls.Add(this.button7);
			this.tabPage1.Controls.Add(this.label6);
			this.tabPage1.Controls.Add(this.button25);
			this.tabPage1.Controls.Add(this.textBox5);
			this.tabPage1.Controls.Add(this.button24);
			this.tabPage1.Controls.Add(this.button8);
			this.tabPage1.Controls.Add(this.button23);
			this.tabPage1.Controls.Add(this.button9);
			this.tabPage1.Controls.Add(this.button22);
			this.tabPage1.Controls.Add(this.button10);
			this.tabPage1.Controls.Add(this.button21);
			this.tabPage1.Controls.Add(this.button11);
			this.tabPage1.Controls.Add(this.comboBox1);
			this.tabPage1.Controls.Add(this.button12);
			this.tabPage1.Controls.Add(this.button18);
			this.tabPage1.Controls.Add(this.label5);
			this.tabPage1.Controls.Add(this.button13);
			this.tabPage1.Controls.Add(this.button17);
			this.tabPage1.Controls.Add(this.button15);
			this.tabPage1.Controls.Add(this.button16);
			this.tabPage1.Controls.Add(this.label11);
			this.tabPage1.Controls.Add(this.label2);
			this.tabPage1.Controls.Add(this.label4);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(957, 471);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "单数据读取测试";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.button35.Location = new System.Drawing.Point(742, 38);
			this.button35.Name = "button35";
			this.button35.Size = new System.Drawing.Size(95, 28);
			this.button35.TabIndex = 65;
			this.button35.Text = "基座位置型";
			this.button35.UseVisualStyleBackColor = true;
			this.button35.Click += new System.EventHandler(button35_Click);
			this.button34.Location = new System.Drawing.Point(641, 38);
			this.button34.Name = "button34";
			this.button34.Size = new System.Drawing.Size(95, 28);
			this.button34.TabIndex = 64;
			this.button34.Text = "位置型变量";
			this.button34.UseVisualStyleBackColor = true;
			this.button34.Click += new System.EventHandler(button34_Click);
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(73, 414);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(881, 52);
			this.textBox_code.TabIndex = 63;
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(6, 418);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 62;
			this.label_code.Text = "代码：";
			this.button33.Location = new System.Drawing.Point(453, 134);
			this.button33.Name = "button33";
			this.button33.Size = new System.Drawing.Size(95, 28);
			this.button33.TabIndex = 61;
			this.button33.Text = "再线时间";
			this.button33.UseVisualStyleBackColor = true;
			this.button33.Click += new System.EventHandler(button33_Click);
			this.button32.Location = new System.Drawing.Point(453, 101);
			this.button32.Name = "button32";
			this.button32.Size = new System.Drawing.Size(95, 28);
			this.button32.TabIndex = 60;
			this.button32.Text = "再线时间";
			this.button32.UseVisualStyleBackColor = true;
			this.button32.Click += new System.EventHandler(button32_Click);
			this.button29.Location = new System.Drawing.Point(351, 134);
			this.button29.Name = "button29";
			this.button29.Size = new System.Drawing.Size(95, 28);
			this.button29.TabIndex = 59;
			this.button29.Text = "动作时间";
			this.button29.UseVisualStyleBackColor = true;
			this.button29.Click += new System.EventHandler(button29_Click);
			this.button30.Location = new System.Drawing.Point(212, 134);
			this.button30.Name = "button30";
			this.button30.Size = new System.Drawing.Size(133, 28);
			this.button30.TabIndex = 58;
			this.button30.Text = "伺服电源接通时间";
			this.button30.UseVisualStyleBackColor = true;
			this.button30.Click += new System.EventHandler(button30_Click);
			this.button31.Location = new System.Drawing.Point(73, 134);
			this.button31.Name = "button31";
			this.button31.Size = new System.Drawing.Size(133, 28);
			this.button31.TabIndex = 57;
			this.button31.Text = "控制电源接通时间";
			this.button31.UseVisualStyleBackColor = true;
			this.button31.Click += new System.EventHandler(button31_Click);
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(4, 139);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(68, 17);
			this.label17.TabIndex = 56;
			this.label17.Text = "持续时间：";
			this.button28.Location = new System.Drawing.Point(682, 167);
			this.button28.Name = "button28";
			this.button28.Size = new System.Drawing.Size(82, 28);
			this.button28.TabIndex = 55;
			this.button28.Text = "字节写入";
			this.button28.UseVisualStyleBackColor = true;
			this.button28.Click += new System.EventHandler(button28_Click);
			this.button27.Location = new System.Drawing.Point(607, 167);
			this.button27.Name = "button27";
			this.button27.Size = new System.Drawing.Size(69, 28);
			this.button27.TabIndex = 54;
			this.button27.Text = "实数写入";
			this.button27.UseVisualStyleBackColor = true;
			this.button27.Click += new System.EventHandler(button27_Click);
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 205);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.button26.Location = new System.Drawing.Point(527, 167);
			this.button26.Name = "button26";
			this.button26.Size = new System.Drawing.Size(76, 28);
			this.button26.TabIndex = 53;
			this.button26.Text = "双整型写入";
			this.button26.UseVisualStyleBackColor = true;
			this.button26.Click += new System.EventHandler(button26_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(73, 202);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(881, 206);
			this.textBox4.TabIndex = 5;
			this.button20.Location = new System.Drawing.Point(451, 167);
			this.button20.Name = "button20";
			this.button20.Size = new System.Drawing.Size(73, 28);
			this.button20.TabIndex = 52;
			this.button20.Text = "整型写入";
			this.button20.UseVisualStyleBackColor = true;
			this.button20.Click += new System.EventHandler(button20_Click);
			this.button3.Location = new System.Drawing.Point(74, 7);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(102, 28);
			this.button3.TabIndex = 18;
			this.button3.Text = "报警信息读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button19.Location = new System.Drawing.Point(362, 167);
			this.button19.Name = "button19";
			this.button19.Size = new System.Drawing.Size(86, 28);
			this.button19.TabIndex = 51;
			this.button19.Text = "字符串写入";
			this.button19.UseVisualStyleBackColor = true;
			this.button19.Click += new System.EventHandler(button19_Click);
			this.button4.Location = new System.Drawing.Point(182, 7);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 19;
			this.button4.Text = "关节坐标读取";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox6.Location = new System.Drawing.Point(200, 170);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(156, 23);
			this.textBox6.TabIndex = 50;
			this.button5.Location = new System.Drawing.Point(539, 6);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(101, 28);
			this.button5.TabIndex = 20;
			this.button5.Text = "姿态坐标读取";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(141, 173);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(56, 17);
			this.label8.TabIndex = 49;
			this.label8.Text = "变量值：";
			this.button6.Location = new System.Drawing.Point(279, 7);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(79, 28);
			this.button6.TabIndex = 21;
			this.button6.Text = "状态读取";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.textBox3.Location = new System.Drawing.Point(74, 170);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(61, 23);
			this.textBox3.TabIndex = 48;
			this.textBox3.Text = "000";
			this.button7.Location = new System.Drawing.Point(364, 7);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(83, 28);
			this.button7.TabIndex = 22;
			this.button7.Text = "程序名读取";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(6, 173);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 47;
			this.label6.Text = "变量地址：";
			this.button25.Location = new System.Drawing.Point(352, 101);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(95, 28);
			this.button25.TabIndex = 46;
			this.button25.Text = "动作时间";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.textBox5.Location = new System.Drawing.Point(75, 41);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(85, 23);
			this.textBox5.TabIndex = 24;
			this.textBox5.Text = "000";
			this.button24.Location = new System.Drawing.Point(213, 101);
			this.button24.Name = "button24";
			this.button24.Size = new System.Drawing.Size(133, 28);
			this.button24.TabIndex = 45;
			this.button24.Text = "伺服电源接通时间";
			this.button24.UseVisualStyleBackColor = true;
			this.button24.Click += new System.EventHandler(button24_Click);
			this.button8.Location = new System.Drawing.Point(166, 38);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(82, 28);
			this.button8.TabIndex = 25;
			this.button8.Text = "字节读取";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button23.Location = new System.Drawing.Point(74, 101);
			this.button23.Name = "button23";
			this.button23.Size = new System.Drawing.Size(133, 28);
			this.button23.TabIndex = 44;
			this.button23.Text = "控制电源接通时间";
			this.button23.UseVisualStyleBackColor = true;
			this.button23.Click += new System.EventHandler(button23_Click);
			this.button9.Location = new System.Drawing.Point(254, 38);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(82, 28);
			this.button9.TabIndex = 26;
			this.button9.Text = "整型读取";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button22.Location = new System.Drawing.Point(486, 69);
			this.button22.Name = "button22";
			this.button22.Size = new System.Drawing.Size(80, 28);
			this.button22.TabIndex = 43;
			this.button22.Text = "Svon OFF";
			this.button22.UseVisualStyleBackColor = true;
			this.button22.Click += new System.EventHandler(button22_Click);
			this.button10.Location = new System.Drawing.Point(342, 38);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(91, 28);
			this.button10.TabIndex = 27;
			this.button10.Text = "双整型读取";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button21.Location = new System.Drawing.Point(400, 69);
			this.button21.Name = "button21";
			this.button21.Size = new System.Drawing.Size(80, 28);
			this.button21.TabIndex = 42;
			this.button21.Text = "Svon ON";
			this.button21.UseVisualStyleBackColor = true;
			this.button21.Click += new System.EventHandler(button21_Click);
			this.button11.Location = new System.Drawing.Point(439, 38);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(95, 28);
			this.button11.TabIndex = 28;
			this.button11.Text = "实数读取";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(453, 8);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(82, 25);
			this.comboBox1.TabIndex = 41;
			this.button12.Location = new System.Drawing.Point(540, 38);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(95, 28);
			this.button12.TabIndex = 29;
			this.button12.Text = "字符串读取";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button18.Location = new System.Drawing.Point(572, 69);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(68, 28);
			this.button18.TabIndex = 37;
			this.button18.Text = "Start";
			this.button18.UseVisualStyleBackColor = true;
			this.button18.Click += new System.EventHandler(button18_Click);
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(5, 106);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 35;
			this.label5.Text = "触发时间：";
			this.button13.Location = new System.Drawing.Point(74, 69);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(86, 28);
			this.button13.TabIndex = 31;
			this.button13.Text = "Hold ON";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button17.Location = new System.Drawing.Point(329, 69);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(65, 28);
			this.button17.TabIndex = 34;
			this.button17.Text = "Cancel";
			this.button17.UseVisualStyleBackColor = true;
			this.button17.Click += new System.EventHandler(button17_Click);
			this.button15.Location = new System.Drawing.Point(166, 69);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(86, 28);
			this.button15.TabIndex = 32;
			this.button15.Text = "Hold OFF";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button16.Location = new System.Drawing.Point(258, 69);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(65, 28);
			this.button16.TabIndex = 33;
			this.button16.Text = "Reset";
			this.button16.UseVisualStyleBackColor = true;
			this.button16.Click += new System.EventHandler(button16_Click);
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(5, 13);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(68, 17);
			this.label11.TabIndex = 40;
			this.label11.Text = "状态读取：";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(5, 44);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 23;
			this.label2.Text = "变量地址：";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(5, 74);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 30;
			this.label4.Text = "控制操作：";
			this.tabPage2.Controls.Add(this.panel3);
			this.tabPage2.Controls.Add(this.textBox_code2);
			this.tabPage2.Controls.Add(this.label18);
			this.tabPage2.Controls.Add(this.textBox_command);
			this.tabPage2.Controls.Add(this.button14);
			this.tabPage2.Controls.Add(this.textBox_dataPart);
			this.tabPage2.Controls.Add(this.textBox7);
			this.tabPage2.Controls.Add(this.label12);
			this.tabPage2.Controls.Add(this.textBox_dataHandle);
			this.tabPage2.Controls.Add(this.textBox_dataAddress);
			this.tabPage2.Controls.Add(this.textBox_dataAttribute);
			this.tabPage2.Controls.Add(this.label10);
			this.tabPage2.Controls.Add(this.label9);
			this.tabPage2.Controls.Add(this.label13);
			this.tabPage2.Controls.Add(this.label15);
			this.tabPage2.Controls.Add(this.label14);
			this.tabPage2.Controls.Add(this.label16);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(957, 471);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "详细命令测试";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.textBox_code2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code2.Location = new System.Drawing.Point(98, 417);
			this.textBox_code2.Multiline = true;
			this.textBox_code2.Name = "textBox_code2";
			this.textBox_code2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code2.Size = new System.Drawing.Size(853, 48);
			this.textBox_code2.TabIndex = 19;
			this.label18.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(6, 420);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(44, 17);
			this.label18.TabIndex = 18;
			this.label18.Text = "代码：";
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(57, 5);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(57, 21);
			this.radioButton2.TabIndex = 17;
			this.radioButton2.Text = "ASCII";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(3, 5);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(48, 21);
			this.radioButton1.TabIndex = 16;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "Hex";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.textBox_command.Location = new System.Drawing.Point(98, 5);
			this.textBox_command.Name = "textBox_command";
			this.textBox_command.Size = new System.Drawing.Size(151, 23);
			this.textBox_command.TabIndex = 3;
			this.textBox_command.Text = "0x89";
			this.button14.Location = new System.Drawing.Point(257, 41);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(75, 67);
			this.button14.TabIndex = 15;
			this.button14.Text = "Read";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.textBox_dataPart.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_dataPart.Location = new System.Drawing.Point(98, 114);
			this.textBox_dataPart.Multiline = true;
			this.textBox_dataPart.Name = "textBox_dataPart";
			this.textBox_dataPart.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_dataPart.Size = new System.Drawing.Size(853, 108);
			this.textBox_dataPart.TabIndex = 14;
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox7.Location = new System.Drawing.Point(98, 255);
			this.textBox7.Multiline = true;
			this.textBox7.Name = "textBox7";
			this.textBox7.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox7.Size = new System.Drawing.Size(853, 156);
			this.textBox7.TabIndex = 5;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(265, 9);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(46, 17);
			this.label12.TabIndex = 6;
			this.label12.Text = "16进制";
			this.textBox_dataHandle.Location = new System.Drawing.Point(98, 86);
			this.textBox_dataHandle.Name = "textBox_dataHandle";
			this.textBox_dataHandle.Size = new System.Drawing.Size(151, 23);
			this.textBox_dataHandle.TabIndex = 12;
			this.textBox_dataHandle.Text = "0x01";
			this.textBox_dataAddress.Location = new System.Drawing.Point(98, 32);
			this.textBox_dataAddress.Name = "textBox_dataAddress";
			this.textBox_dataAddress.Size = new System.Drawing.Size(151, 23);
			this.textBox_dataAddress.TabIndex = 8;
			this.textBox_dataAddress.Text = "11";
			this.textBox_dataAttribute.Location = new System.Drawing.Point(98, 59);
			this.textBox_dataAttribute.Name = "textBox_dataAttribute";
			this.textBox_dataAttribute.Size = new System.Drawing.Size(151, 23);
			this.textBox_dataAttribute.TabIndex = 10;
			this.textBox_dataAttribute.Text = "0";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(6, 8);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(68, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "命令编号：";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(6, 230);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 4;
			this.label9.Text = "值：";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(6, 35);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(92, 17);
			this.label13.TabIndex = 7;
			this.label13.Text = "数据队列编号：";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(6, 89);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(76, 17);
			this.label15.TabIndex = 11;
			this.label15.Text = "处理(请求)：";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(6, 62);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(68, 17);
			this.label14.TabIndex = 9;
			this.label14.Text = "单元编号：";
			this.label16.Location = new System.Drawing.Point(6, 117);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(92, 68);
			this.label16.TabIndex = 13;
			this.label16.Text = "附加数据：\r\n(16进制字符串)";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "YASKAWA - Ethernet 高速服务器功能";
			this.userControlHead1.Size = new System.Drawing.Size(974, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel3.Controls.Add(this.radioButton5);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton1);
			this.panel3.Controls.Add(this.radioButton2);
			this.panel3.Location = new System.Drawing.Point(96, 223);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(367, 29);
			this.panel3.TabIndex = 20;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(121, 5);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(55, 21);
			this.radioButton3.TabIndex = 18;
			this.radioButton3.Text = "Int16";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Location = new System.Drawing.Point(191, 5);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(55, 21);
			this.radioButton4.TabIndex = 19;
			this.radioButton4.Text = "Int32";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(257, 5);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(54, 21);
			this.radioButton5.TabIndex = 20;
			this.radioButton5.Text = "Float";
			this.radioButton5.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(974, 585);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormYRCHighEthernet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "安川机器人访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
