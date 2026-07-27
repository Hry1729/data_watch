using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Robot.YAMAHA;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.Robot
{
	public class FormYamahaRCX : HslFormContent
	{
		private YamahaRCX yamahaRCX;

		private CodeExampleControl codeExampleControl;

		private JogMoveCmd jogEnabled = new JogMoveCmd
		{
			JogIndex = -1
		};

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button1;

		private Panel panel2;

		private Button button2;

		private TextBox textBox_result;

		private Button button3;

		private Button button4;

		private Button button5;

		private Button button6;

		private Button button7;

		private Button button8;

		private Button button9;

		private Button button_execute;

		private TextBox textBox_read_command;

		private Label label3;

		private Button button20;

		private Button button21;

		private Button button18;

		private Button button19;

		private Button button16;

		private Button button17;

		private Button button14;

		private Button button15;

		private Button button12;

		private Button button13;

		private Button button11;

		private Button button10;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private Panel panel3;

		private Label label4;

		private DataGridView dataGridView1;

		private PipeSelectControl pipeSelectControl1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private Panel panel4;

		private Button button24;

		private Button button23;

		private Button button22;

		private Button button25;

		private Button button26;

		private Button button27;

		private Label label1;

		private CheckBox checkBox_keepmove;

		public FormYamahaRCX()
		{
			InitializeComponent();
		}

		private void FormYamahaRCX_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private async void Button1_Click(object sender, EventArgs e)
		{
			try
			{
				yamahaRCX = new YamahaRCX();
				try
				{
					pipeSelectControl1.IniPipe(yamahaRCX);
				}
				catch (Exception ex)
				{
					SoftBasic.ShowExceptionMessage(ex);
					return;
				}
				yamahaRCX.LogNet = base.LogNet;
				button1.Enabled = false;
				OperateResult connect = await yamahaRCX.ConnectServerAsync();
				if (connect.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button1.Enabled = false;
					button2.Enabled = true;
					panel2.Enabled = true;
					codeExampleControl.SetCodeText("yamahaRCX", yamahaRCX);
				}
				else
				{
					button1.Enabled = true;
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.Message);
				}
			}
			catch (Exception ex2)
			{
				SoftBasic.ShowExceptionMessage(ex2);
			}
		}

		private void FormABBWebApi_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
			AddCommandInfo("自动模式", "@AUTO", "@AUTO", "");
			AddCommandInfo("编程模式", "@PROGRAM", "", "");
			AddCommandInfo("手动模式", "", "@MANUAL", "");
			AddCommandInfo("获取脉冲坐标值", "@? WHERE", "@? WHERE", "");
			AddCommandInfo("获取毫米坐标值", "@? WHRXY", "@? WHRXY", "");
			AddCommandInfo("程序停止", "@STOP", "@STOP", "");
			AddCommandInfo("JOG X 正方向", "@JOG X+", "@JOGXY 1+", "");
			AddCommandInfo("JOG X 负方向", "@JOG X-", "@JOGXY 1-", "");
			AddCommandInfo("JOG Y 正方向", "@JOG Y+", "@JOGXY 2+", "");
			AddCommandInfo("JOG Y 负方向", "@JOG Y-", "@JOGXY 2-", "");
			AddCommandInfo("JOG R 正方向", "@JOG R+", "@JOGXY 4+", "");
			AddCommandInfo("JOG R 负方向", "@JOG R-", "@JOGXY 4-", "");
			AddCommandInfo("JOG Z 正方向", "@JOG Z+", "@JOGXY 3+", "");
			AddCommandInfo("JOG Z 负方向", "@JOG Z-", "@JOGXY 3-", "");
			AddCommandInfo("改变机器人速度", "@MSPEED #", "@MSPEED #", "例如 @MSPEED 50, 默认机器人编号1, #代表数字");
			AddCommandInfo("改变机器人加速度", "@ACCEL #", "@ACCEL #", "#代表数字");
			AddCommandInfo("改变机器人减速度", "@DECEL #", "@DECEL #", "#代表数字");
			AddCommandInfo("设置左手系", "@ARMTYP 1,1", "@ARMSEL 2", "");
			AddCommandInfo("设置右手系", "@ARMTYP 0,0", "@ARMSEL 1", "");
			AddCommandInfo("设置单位为毫米", "@UNIT 1", "", "");
			AddCommandInfo("设置单位为脉冲", "@UNIT 0", "", "");
			AddCommandInfo("得到当前的手系", "@? ARM", "@? ARMCND", "");
			AddCommandInfo("等待机械臂停稳", "@WAIT ARM", "", "");
			AddCommandInfo("是否已经回原点", "@? ABSRST", "@? ORIGIN", "");
			AddCommandInfo("伺服是否上电", "@? SERVO", "@? SERVO", "");
			AddCommandInfo("是否急停状态", "@? EMG", "@? EMG", "");
			AddCommandInfo("当前模式", "@? MOD", "@? MODE", "");
			AddCommandInfo("伺服上电", "@SERVO ON", "@MOTOR ON", "");
			AddCommandInfo("伺服断电", "@SERVO OFF", "@MOTOR OFF", "");
			AddCommandInfo("伺服自由", "@SERVO FREE", "@MOTOR FREE", "");
			AddCommandInfo("回原点", "@ABSRST", "@ORIGIN", "");
			AddCommandInfo("机器人开始停止", "@RUN", "@RUN", "");
			AddCommandInfo("机器人运行停止", "@STOP", "@STOP", "");
			AddCommandInfo("移动到指定位置", "@MOVE P,X Y R Z 0 0", "@MOVE P,X Y R Z 0 0", "");
			AddCommandInfo("写入点位到控制器", "@P#=X Y R Z 0 0", "@P#=X Y R Z 0 0", "#代表数字");
			AddCommandInfo("清除内部报警状态", "", "@ALMRST", "");
			AddCommandInfo("示教点位", "@TEACH #", "@TCHXY #", "#代表数字");
			AddCommandInfo("获取任务运行状态", "", "@? TSKMON", "R:执行中，U:暂时休止, S:停止状态");
			AddCommandInfo("读机器人输入", "@?DI2()", "@?DI2()", "");
			AddCommandInfo("读机器人输出", "@?DO2()", "@?DO2()", "");
			AddCommandInfo("写机器人输出", "@DO2()=#", "@DO2()=#", "#代表数字");
			dataGridView1.Rows[0].Cells[0].Selected = false;
		}

		private void AddCommandInfo(string name, string cmdCX240, string cmdCX340, string des)
		{
			int index = dataGridView1.Rows.Add();
			DataGridViewRow dataGridViewRow = dataGridView1.Rows[index];
			dataGridViewRow.Cells[0].Value = name;
			dataGridViewRow.Cells[1].Value = cmdCX240;
			dataGridViewRow.Cells[2].Value = cmdCX340;
			dataGridViewRow.Cells[3].Value = des;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			yamahaRCX?.ConnectClose();
			button2.Enabled = false;
			panel2.Enabled = false;
			button1.Enabled = true;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yamahaRCX.Reset();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Reset success");
			}
			else
			{
				DemoUtils.ShowMessage("Reset Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult reset = yamahaRCX.Reset( );");
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yamahaRCX.Run();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Run success");
			}
			else
			{
				DemoUtils.ShowMessage("Run Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult run = yamahaRCX.Run( );");
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = yamahaRCX.Stop();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Stop success");
			}
			else
			{
				DemoUtils.ShowMessage("Stop Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult stop = yamahaRCX.Stop( );");
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = yamahaRCX.ReadMotorStatus();
			if (operateResult.IsSuccess)
			{
				if (operateResult.Content == 0)
				{
					textBox_result.Text = "0 -> 马达电源关闭;";
				}
				else if (operateResult.Content == 1)
				{
					textBox_result.Text = "1 -> 马达电源开启;";
				}
				else if (operateResult.Content == 2)
				{
					textBox_result.Text = "2 -> 马达电源开启＋所有机器人伺服开启";
				}
				else
				{
					textBox_result.Text = operateResult.Content + " -> 未知的状态";
				}
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult<int> motor = yamahaRCX.ReadMotorStatus( );");
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = yamahaRCX.ReadModeStatus();
			if (operateResult.IsSuccess)
			{
				textBox_result.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult<int> mode = yamahaRCX.ReadModeStatus( );");
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult<float[]> operateResult = yamahaRCX.ReadJoints();
			if (operateResult.IsSuccess)
			{
				textBox_result.Text = SoftBasic.ArrayFormat(operateResult);
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult<float[]> joints = yamahaRCX.ReadJoints( );");
		}

		private void button9_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = yamahaRCX.ReadEmergencyStatus();
			if (operateResult.IsSuccess)
			{
				if (operateResult.Content == 0)
				{
					textBox_result.Text = "0 -> 正常状态;";
				}
				else if (operateResult.Content == 1)
				{
					textBox_result.Text = "1 -> 紧急停止状态;";
				}
				else
				{
					textBox_result.Text = operateResult.Content + " -> 未知的状态";
				}
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult<int> emergency = yamahaRCX.ReadEmergencyStatus( );");
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button_execute_Click(object sender, EventArgs e)
		{
			OperateResult<string[]> operateResult = yamahaRCX.ReadCommand(textBox_read_command.Text);
			if (operateResult.IsSuccess)
			{
				textBox_result.Lines = operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode("OperateResult<string[]> read = yamahaRCX.ReadCommand( \"" + textBox_read_command.Text + "\" );");
		}

		private void RenderJogResult(OperateResult result, string jog)
		{
			if (result.IsSuccess)
			{
				textBox_result.AppendText(DateTime.Now.ToString() + " " + jog + " Success" + Environment.NewLine);
			}
			else
			{
				DemoUtils.ShowMessage(jog + " failed: " + result.Message);
			}
		}

		private void StartJogXY(JogMoveCmd cmd)
		{
			textBox_result.Text = DateTime.Now.ToString() + " Start Jog" + Environment.NewLine;
			ThreadPool.QueueUserWorkItem(((Action<object>)delegate(object m)
			{
				JogMoveCmd jogMoveCmd = m as JogMoveCmd;
				yamahaRCX.SendJogXY(jogMoveCmd.JogIndex);
				while (jogMoveCmd.MoveEnable)
				{
					HslHelper.ThreadSleep(100);
					yamahaRCX.CommunicationPipe.Send(new byte[1] { 22 });
				}
				OperateResult<byte[]> finish = yamahaRCX.ReadFromCoreServer(new byte[0], true, true);
				Invoke((Action)delegate
				{
					if (finish.IsSuccess)
					{
						textBox_result.AppendText("Finish Jog: " + Encoding.UTF8.GetString(finish.Content));
					}
					else
					{
						DemoUtils.ShowMessage("Operate failed: " + finish.Message);
					}
				});
			}).Invoke, cmd);
			codeExampleControl.ReaderReadCode($"\r\nyamahaRCX.SendJogXY( {cmd.JogIndex} );\r\nwhile ([MoveEnable])\r\n{{\r\n\tHslCommunication.Core.HslHelper.ThreadSleep( 100 );\r\n\tyamahaRCX.CommunicationPipe.Send( new byte[] {{ 0x16 }} );\r\n}}\r\nOperateResult<byte[]> finish = yamahaRCX.ReadFromCoreServer( new byte[0], hasResponseData: true, usePackAndUnpack: true );");
		}

		private void button10_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-1), "JOG 1-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -1 );");
			}
		}

		private void button10_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -1;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button10_MouseUp(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.MoveEnable = false;
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(1), "JOG 1+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 1 );");
			}
		}

		private void button11_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 1;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button13_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-2), "JOG 2-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -2 );");
			}
		}

		private void button13_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -2;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button12_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(2), "JOG 2+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 2 );");
			}
		}

		private void button12_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 2;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button15_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-3), "JOG 3-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -3 );");
			}
		}

		private void button15_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -3;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button14_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(3), "JOG 3+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 3 );");
			}
		}

		private void button14_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 3;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button17_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-4), "JOG 4-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -4 );");
			}
		}

		private void button17_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -4;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button16_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(4), "JOG 4+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 4 );");
			}
		}

		private void button16_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 4;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button19_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-5), "JOG 5-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -5 );");
			}
		}

		private void button19_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -5;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button18_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(5), "JOG 5+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 5 );");
			}
		}

		private void button18_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 5;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button21_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(-6), "JOG 6-");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( -6 );");
			}
		}

		private void button21_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = -6;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		private void button20_Click(object sender, EventArgs e)
		{
			if (!checkBox_keepmove.Checked)
			{
				RenderJogResult(yamahaRCX.JogXY(6), "JOG 6+");
				codeExampleControl.ReaderReadCode("OperateResult result = yamahaRCX.JogXY( 6 );");
			}
		}

		private void button20_MouseDown(object sender, MouseEventArgs e)
		{
			if (checkBox_keepmove.Checked && e.Button == MouseButtons.Left)
			{
				jogEnabled.JogIndex = 6;
				jogEnabled.MoveEnable = true;
				StartJogXY(jogEnabled);
			}
		}

		public void ReadDI(int index)
		{
			OperateResult<bool[]> operateResult = yamahaRCX.ReadDI(index);
			if (operateResult.IsSuccess)
			{
				textBox_result.Text = operateResult.Content.ToArrayString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode($"OperateResult<bool[]> read = yamahaRCX.ReadDI( {index} )");
		}

		private void button22_Click(object sender, EventArgs e)
		{
			ReadDI(0);
		}

		private void button23_Click(object sender, EventArgs e)
		{
			ReadDI(1);
		}

		private void button24_Click(object sender, EventArgs e)
		{
			ReadDI(2);
		}

		public void ReadDO(int index)
		{
			OperateResult<bool[]> operateResult = yamahaRCX.ReadDO(index);
			if (operateResult.IsSuccess)
			{
				textBox_result.Text = operateResult.Content.ToArrayString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Faield:" + operateResult.Message);
			}
			codeExampleControl.ReaderReadCode($"OperateResult<bool[]> read = yamahaRCX.ReadDO( {index} )");
		}

		private void button27_Click(object sender, EventArgs e)
		{
			ReadDO(0);
		}

		private void button26_Click(object sender, EventArgs e)
		{
			ReadDO(1);
		}

		private void button25_Click(object sender, EventArgs e)
		{
			ReadDO(2);
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1 = new System.Windows.Forms.Panel();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label4 = new System.Windows.Forms.Label();
			this.panel3 = new System.Windows.Forms.Panel();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBox_keepmove = new System.Windows.Forms.CheckBox();
			this.button25 = new System.Windows.Forms.Button();
			this.button26 = new System.Windows.Forms.Button();
			this.button27 = new System.Windows.Forms.Button();
			this.button24 = new System.Windows.Forms.Button();
			this.button23 = new System.Windows.Forms.Button();
			this.button22 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button20 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button21 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button18 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button19 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button17 = new System.Windows.Forms.Button();
			this.button13 = new System.Windows.Forms.Button();
			this.button14 = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel4 = new System.Windows.Forms.Panel();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.textBox_result = new System.Windows.Forms.TextBox();
			this.button_execute = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_read_command = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.panel4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "TCP";
			this.userControlHead1.Size = new System.Drawing.Size(986, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 30;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(982, 45);
			this.panel1.TabIndex = 31;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(3, 6);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(785, 31);
			this.pipeSelectControl1.TabIndex = 35;
			this.pipeSelectControl1.TcpPortText = "23";
			this.pipeSelectControl1.UdpPortText = "23";
			this.button2.Location = new System.Drawing.Point(890, 7);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(83, 27);
			this.button2.TabIndex = 18;
			this.button2.Text = "Close";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(795, 7);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(87, 27);
			this.button1.TabIndex = 17;
			this.button1.Text = "Connect";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Controls.Add(this.textBox_result);
			this.panel2.Controls.Add(this.button_execute);
			this.panel2.Controls.Add(this.label3);
			this.panel2.Controls.Add(this.textBox_read_command);
			this.panel2.Location = new System.Drawing.Point(2, 85);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(982, 572);
			this.panel2.TabIndex = 32;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(3, 112);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(46, 17);
			this.label4.TabIndex = 44;
			this.label4.Text = "Result:";
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.label1);
			this.panel3.Controls.Add(this.checkBox_keepmove);
			this.panel3.Controls.Add(this.button25);
			this.panel3.Controls.Add(this.button26);
			this.panel3.Controls.Add(this.button27);
			this.panel3.Controls.Add(this.button24);
			this.panel3.Controls.Add(this.button23);
			this.panel3.Controls.Add(this.button22);
			this.panel3.Controls.Add(this.button10);
			this.panel3.Controls.Add(this.button3);
			this.panel3.Controls.Add(this.button20);
			this.panel3.Controls.Add(this.button4);
			this.panel3.Controls.Add(this.button5);
			this.panel3.Controls.Add(this.button6);
			this.panel3.Controls.Add(this.button21);
			this.panel3.Controls.Add(this.button7);
			this.panel3.Controls.Add(this.button18);
			this.panel3.Controls.Add(this.button8);
			this.panel3.Controls.Add(this.button19);
			this.panel3.Controls.Add(this.button9);
			this.panel3.Controls.Add(this.button16);
			this.panel3.Controls.Add(this.button11);
			this.panel3.Controls.Add(this.button17);
			this.panel3.Controls.Add(this.button13);
			this.panel3.Controls.Add(this.button14);
			this.panel3.Controls.Add(this.button12);
			this.panel3.Controls.Add(this.button15);
			this.panel3.Location = new System.Drawing.Point(599, 3);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(378, 253);
			this.panel3.TabIndex = 43;
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
			this.label1.Location = new System.Drawing.Point(2, 218);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(172, 17);
			this.label1.TabIndex = 49;
			this.label1.Text = "(按下开始动作，松开结束动作)";
			this.checkBox_keepmove.AutoSize = true;
			this.checkBox_keepmove.Location = new System.Drawing.Point(5, 195);
			this.checkBox_keepmove.Name = "checkBox_keepmove";
			this.checkBox_keepmove.Size = new System.Drawing.Size(99, 21);
			this.checkBox_keepmove.TabIndex = 48;
			this.checkBox_keepmove.Text = "连续动作模式";
			this.checkBox_keepmove.UseVisualStyleBackColor = true;
			this.button25.Location = new System.Drawing.Point(291, 195);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(81, 26);
			this.button25.TabIndex = 47;
			this.button25.Text = "DO_2";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.button26.Location = new System.Drawing.Point(291, 163);
			this.button26.Name = "button26";
			this.button26.Size = new System.Drawing.Size(81, 26);
			this.button26.TabIndex = 46;
			this.button26.Text = "DO_1";
			this.button26.UseVisualStyleBackColor = true;
			this.button26.Click += new System.EventHandler(button26_Click);
			this.button27.Location = new System.Drawing.Point(291, 131);
			this.button27.Name = "button27";
			this.button27.Size = new System.Drawing.Size(81, 26);
			this.button27.TabIndex = 45;
			this.button27.Text = "DO_0";
			this.button27.UseVisualStyleBackColor = true;
			this.button27.Click += new System.EventHandler(button27_Click);
			this.button24.Location = new System.Drawing.Point(202, 195);
			this.button24.Name = "button24";
			this.button24.Size = new System.Drawing.Size(81, 26);
			this.button24.TabIndex = 44;
			this.button24.Text = "DI_2";
			this.button24.UseVisualStyleBackColor = true;
			this.button24.Click += new System.EventHandler(button24_Click);
			this.button23.Location = new System.Drawing.Point(202, 163);
			this.button23.Name = "button23";
			this.button23.Size = new System.Drawing.Size(81, 26);
			this.button23.TabIndex = 43;
			this.button23.Text = "DI_1";
			this.button23.UseVisualStyleBackColor = true;
			this.button23.Click += new System.EventHandler(button23_Click);
			this.button22.Location = new System.Drawing.Point(202, 131);
			this.button22.Name = "button22";
			this.button22.Size = new System.Drawing.Size(81, 26);
			this.button22.TabIndex = 42;
			this.button22.Text = "DI_0";
			this.button22.UseVisualStyleBackColor = true;
			this.button22.Click += new System.EventHandler(button22_Click);
			this.button10.Location = new System.Drawing.Point(3, 3);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(87, 26);
			this.button10.TabIndex = 30;
			this.button10.Text = "JOG 1-";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button10.MouseDown += new System.Windows.Forms.MouseEventHandler(button10_MouseDown);
			this.button10.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button3.Location = new System.Drawing.Point(199, 3);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(81, 26);
			this.button3.TabIndex = 18;
			this.button3.Text = "RESET";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button20.Location = new System.Drawing.Point(96, 163);
			this.button20.Name = "button20";
			this.button20.Size = new System.Drawing.Size(87, 26);
			this.button20.TabIndex = 41;
			this.button20.Text = "JOG 6+";
			this.button20.UseVisualStyleBackColor = true;
			this.button20.Click += new System.EventHandler(button20_Click);
			this.button20.MouseDown += new System.Windows.Forms.MouseEventHandler(button20_MouseDown);
			this.button20.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button4.Location = new System.Drawing.Point(291, 3);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(81, 26);
			this.button4.TabIndex = 19;
			this.button4.Text = "RUN";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button5.Location = new System.Drawing.Point(200, 67);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(81, 26);
			this.button5.TabIndex = 20;
			this.button5.Text = "STOP";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button6.Location = new System.Drawing.Point(200, 35);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(81, 26);
			this.button6.TabIndex = 21;
			this.button6.Text = "MOTOR";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button21.Location = new System.Drawing.Point(3, 163);
			this.button21.Name = "button21";
			this.button21.Size = new System.Drawing.Size(87, 26);
			this.button21.TabIndex = 40;
			this.button21.Text = "JOG 6-";
			this.button21.UseVisualStyleBackColor = true;
			this.button21.Click += new System.EventHandler(button21_Click);
			this.button21.MouseDown += new System.Windows.Forms.MouseEventHandler(button21_MouseDown);
			this.button21.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button7.Location = new System.Drawing.Point(291, 35);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(81, 26);
			this.button7.TabIndex = 22;
			this.button7.Text = "MODE";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button18.Location = new System.Drawing.Point(96, 131);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(87, 26);
			this.button18.TabIndex = 39;
			this.button18.Text = "JOG 5+";
			this.button18.UseVisualStyleBackColor = true;
			this.button18.Click += new System.EventHandler(button18_Click);
			this.button18.MouseDown += new System.Windows.Forms.MouseEventHandler(button18_MouseDown);
			this.button18.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button8.Location = new System.Drawing.Point(200, 99);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(81, 26);
			this.button8.TabIndex = 23;
			this.button8.Text = "JOINTS";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button19.Location = new System.Drawing.Point(3, 131);
			this.button19.Name = "button19";
			this.button19.Size = new System.Drawing.Size(87, 26);
			this.button19.TabIndex = 38;
			this.button19.Text = "JOG 5-";
			this.button19.UseVisualStyleBackColor = true;
			this.button19.Click += new System.EventHandler(button19_Click);
			this.button19.MouseDown += new System.Windows.Forms.MouseEventHandler(button19_MouseDown);
			this.button19.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button9.Location = new System.Drawing.Point(291, 99);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(81, 26);
			this.button9.TabIndex = 24;
			this.button9.Text = "Emergency";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button16.Location = new System.Drawing.Point(96, 99);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(87, 26);
			this.button16.TabIndex = 37;
			this.button16.Text = "JOG 4+";
			this.button16.UseVisualStyleBackColor = true;
			this.button16.Click += new System.EventHandler(button16_Click);
			this.button16.MouseDown += new System.Windows.Forms.MouseEventHandler(button16_MouseDown);
			this.button16.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button11.Location = new System.Drawing.Point(96, 3);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(87, 26);
			this.button11.TabIndex = 31;
			this.button11.Text = "JOG 1+";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button11.MouseDown += new System.Windows.Forms.MouseEventHandler(button11_MouseDown);
			this.button11.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button17.Location = new System.Drawing.Point(3, 99);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(87, 26);
			this.button17.TabIndex = 36;
			this.button17.Text = "JOG 4-";
			this.button17.UseVisualStyleBackColor = true;
			this.button17.Click += new System.EventHandler(button17_Click);
			this.button17.MouseDown += new System.Windows.Forms.MouseEventHandler(button17_MouseDown);
			this.button17.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button13.Location = new System.Drawing.Point(3, 35);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(87, 26);
			this.button13.TabIndex = 32;
			this.button13.Text = "JOG 2-";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button13.MouseDown += new System.Windows.Forms.MouseEventHandler(button13_MouseDown);
			this.button13.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button14.Location = new System.Drawing.Point(96, 67);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(87, 26);
			this.button14.TabIndex = 35;
			this.button14.Text = "JOG 3+";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.button14.MouseDown += new System.Windows.Forms.MouseEventHandler(button14_MouseDown);
			this.button14.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button12.Location = new System.Drawing.Point(96, 35);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(87, 26);
			this.button12.TabIndex = 33;
			this.button12.Text = "JOG 2+";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button12.MouseDown += new System.Windows.Forms.MouseEventHandler(button12_MouseDown);
			this.button12.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.button15.Location = new System.Drawing.Point(3, 67);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(87, 26);
			this.button15.TabIndex = 34;
			this.button15.Text = "JOG 3-";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button15.MouseDown += new System.Windows.Forms.MouseEventHandler(button15_MouseDown);
			this.button15.MouseUp += new System.Windows.Forms.MouseEventHandler(button10_MouseUp);
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(3, 262);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(976, 305);
			this.tabControl1.TabIndex = 42;
			this.tabPage1.Controls.Add(this.panel4);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(968, 275);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Command List";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.panel4.Controls.Add(this.dataGridView1);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(3, 3);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(962, 269);
			this.panel4.TabIndex = 33;
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column3, this.Column4);
			this.dataGridView1.Location = new System.Drawing.Point(3, 3);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(956, 263);
			this.dataGridView1.TabIndex = 32;
			this.Column1.HeaderText = "功能名称";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 150;
			this.Column2.HeaderText = "YAMAHA CX240";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 240;
			this.Column3.HeaderText = "YAMAHA CX340";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.Width = 240;
			this.Column4.HeaderText = "备注";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Width = 300;
			this.textBox_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_result.Location = new System.Drawing.Point(81, 108);
			this.textBox_result.Multiline = true;
			this.textBox_result.Name = "textBox_result";
			this.textBox_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_result.Size = new System.Drawing.Size(512, 148);
			this.textBox_result.TabIndex = 5;
			this.button_execute.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_execute.Location = new System.Drawing.Point(504, 6);
			this.button_execute.Name = "button_execute";
			this.button_execute.Size = new System.Drawing.Size(92, 26);
			this.button_execute.TabIndex = 29;
			this.button_execute.Text = "Command";
			this.button_execute.UseVisualStyleBackColor = true;
			this.button_execute.Click += new System.EventHandler(button_execute_Click);
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(4, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(71, 17);
			this.label3.TabIndex = 25;
			this.label3.Text = "Command:";
			this.textBox_read_command.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_read_command.Location = new System.Drawing.Point(81, 9);
			this.textBox_read_command.Multiline = true;
			this.textBox_read_command.Name = "textBox_read_command";
			this.textBox_read_command.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_read_command.Size = new System.Drawing.Size(417, 93);
			this.textBox_read_command.TabIndex = 26;
			this.textBox_read_command.Text = "@？WHERE";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(986, 660);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormYamahaRCX";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormYamahaRCX";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormYamahaRCX_FormClosing);
			base.Load += new System.EventHandler(FormABBWebApi_Load);
			this.panel1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
