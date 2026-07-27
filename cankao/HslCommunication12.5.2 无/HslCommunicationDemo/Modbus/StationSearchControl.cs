using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.ModBus;

namespace HslCommunicationDemo.Modbus
{
	public class StationSearchControl : UserControl
	{
		private IModbus modbus = null;

		private int stationStart = 0;

		private int stationStop = 255;

		private int receiveTimeout = 500;

		private string address = "0";

		private int length = 1;

		private bool isWordRead = true;

		private Thread thread;

		private bool isRunning = false;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox_station_start;

		private TextBox textBox_station_stop;

		private Label label2;

		private CheckBox checkBox_stop_match;

		private TextBox textBox_timeout;

		private Label label3;

		private Button button_start;

		private Button button_stop;

		private Label label4;

		private TextBox textBox_address;

		private TextBox textBox_length;

		private Label label5;

		private Panel panel1;

		private RadioButton radioButton_word;

		private RadioButton radioButton_bool;

		private Label label6;

		private TextBox textBox_log;

		private Label label7;

		private TextBox textBox_match;

		public StationSearchControl()
		{
			InitializeComponent();
		}

		public void SetModbus(IModbus modbus)
		{
			this.modbus = modbus;
		}

		public static string GetTitle()
		{
			return (Program.Language == 1) ? "站号搜索" : "Station Search";
		}

		private void button_start_Click(object sender, EventArgs e)
		{
			try
			{
				stationStart = int.Parse(textBox_station_start.Text);
				stationStop = int.Parse(textBox_station_stop.Text);
				receiveTimeout = int.Parse(textBox_timeout.Text);
				address = textBox_address.Text;
				length = int.Parse(textBox_length.Text);
				isWordRead = radioButton_word.Checked;
				if (stationStart < 0 || stationStart > 255 || stationStop < 0 || stationStop > 255 || stationStart > stationStop)
				{
					if (Program.Language == 1)
					{
						DemoUtils.ShowMessage("站号范围错误，必须在0-255之间，且起始站号小于结束站号！");
					}
					else
					{
						DemoUtils.ShowMessage("The station number range is incorrect. It must be between 0 and 255, and the starting station number is smaller than the ending station number!");
					}
					return;
				}
				isRunning = true;
				thread = new Thread(ThreadSearchStation);
				thread.IsBackground = true;
				thread.Start();
			}
			catch (Exception ex)
			{
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("错误，相关的参数输入异常：" + ex.Message);
				}
				else
				{
					DemoUtils.ShowMessage("Error: Abnormal input of relevant parameters" + ex.Message);
				}
				return;
			}
			textBox_log.Clear();
			textBox_match.Clear();
			button_start.Enabled = false;
			button_stop.Enabled = true;
		}

		private void button_stop_Click(object sender, EventArgs e)
		{
			isRunning = false;
			button_start.Enabled = true;
			button_stop.Enabled = false;
		}

		private string getString1()
		{
			if (Program.Language == 1)
			{
				return "检测站号中断";
			}
			return "Station number check is interrupted";
		}

		private string getString2()
		{
			if (Program.Language == 1)
			{
				return "正在检测站号: ";
			}
			return "Checking Station: ";
		}

		private string getString3()
		{
			if (Program.Language == 1)
			{
				return " 有响应";
			}
			return " Response";
		}

		private string getString4()
		{
			if (Program.Language == 1)
			{
				return " 没有响应";
			}
			return " None response";
		}

		private string getString5()
		{
			if (Program.Language == 1)
			{
				return "检测站号结束";
			}
			return "Station check is over";
		}

		private void ThreadSearchStation()
		{
			int station = stationStart;
			BinaryCommunication binaryCommunication = modbus as BinaryCommunication;
			if (binaryCommunication == null)
			{
				Invoke((Action)delegate
				{
					if (Program.Language == 1)
					{
						textBox_log.AppendText("设备类型不正确，无法转为 BinaryCommunication 类，准备结束。" + Environment.NewLine);
					}
					else
					{
						textBox_log.AppendText("The device type is incorrect and cannot be converted to the BinaryCommunication class. Prepare to end." + Environment.NewLine);
					}
					button_stop_Click(null, EventArgs.Empty);
				});
				return;
			}
			byte stationOld = modbus.Station;
			int receiveTimeOutOld = binaryCommunication.ReceiveTimeOut;
			binaryCommunication.ReceiveTimeOut = receiveTimeout;
			if (binaryCommunication.CommunicationPipe is PipeTcpNet pipeTcpNet)
			{
				pipeTcpNet.CloseOnRecvTimeOutTick = 10000;
			}
			do
			{
				if (!isRunning)
				{
					textBox_log.AppendText(getString1() + Environment.NewLine);
					if (binaryCommunication.CommunicationPipe is PipeTcpNet pipeTcpNet2)
					{
						pipeTcpNet2.CloseOnRecvTimeOutTick = 1;
					}
					binaryCommunication.ReceiveTimeOut = receiveTimeOutOld;
					modbus.Station = stationOld;
					return;
				}
				Invoke((Action)delegate
				{
					textBox_log.AppendText(getString2() + station + " ....");
				});
				modbus.Station = (byte)station;
				if (isWordRead)
				{
					OperateResult<byte[]> read = modbus.Read(address, (ushort)length);
					if (read.IsSuccess)
					{
						Invoke((Action)delegate
						{
							textBox_log.AppendText(getString3() + Environment.NewLine);
							if (Program.Language == 1)
							{
								textBox_match.AppendText($"站号:{station} 有响应数据：" + read.Content.ToHexString(' ') + Environment.NewLine);
							}
							else
							{
								textBox_match.AppendText($"Station:{station} Response data：" + read.Content.ToHexString(' ') + Environment.NewLine);
							}
							if (checkBox_stop_match.Checked)
							{
								textBox_log.AppendText(getString5() + Environment.NewLine);
								button_stop_Click(null, EventArgs.Empty);
								if (binaryCommunication.CommunicationPipe is PipeTcpNet pipeTcpNet3)
								{
									pipeTcpNet3.CloseOnRecvTimeOutTick = 1;
								}
								binaryCommunication.ReceiveTimeOut = receiveTimeOutOld;
								modbus.Station = stationOld;
							}
						});
						HslHelper.ThreadSleep(200);
						if (checkBox_stop_match.Checked)
						{
							return;
						}
					}
					else
					{
						Invoke((Action)delegate
						{
							textBox_log.AppendText(getString4() + Environment.NewLine);
						});
					}
				}
				else
				{
					OperateResult<bool[]> read2 = modbus.ReadBool(address, (ushort)length);
					if (read2.IsSuccess)
					{
						Invoke((Action)delegate
						{
							textBox_log.AppendText(getString3() + Environment.NewLine);
							if (Program.Language == 1)
							{
								textBox_match.AppendText($"站号:{station} 有响应数据：" + read2.Content.ToArrayString() + Environment.NewLine);
							}
							else
							{
								textBox_match.AppendText($"Station:{station} Response data：" + read2.Content.ToArrayString() + Environment.NewLine);
							}
							if (checkBox_stop_match.Checked)
							{
								textBox_log.AppendText(getString5() + Environment.NewLine);
								button_stop_Click(null, EventArgs.Empty);
								if (binaryCommunication.CommunicationPipe is PipeTcpNet pipeTcpNet4)
								{
									pipeTcpNet4.CloseOnRecvTimeOutTick = 1;
								}
								binaryCommunication.ReceiveTimeOut = receiveTimeOutOld;
								modbus.Station = stationOld;
							}
						});
						HslHelper.ThreadSleep(200);
						if (checkBox_stop_match.Checked)
						{
							return;
						}
					}
					else
					{
						Invoke((Action)delegate
						{
							textBox_log.AppendText(getString4() + Environment.NewLine);
						});
					}
				}
				station++;
			}
			while (station <= stationStop);
			Invoke((Action)delegate
			{
				textBox_log.AppendText(getString5() + Environment.NewLine);
				button_stop_Click(null, EventArgs.Empty);
				if (binaryCommunication.CommunicationPipe is PipeTcpNet pipeTcpNet5)
				{
					pipeTcpNet5.CloseOnRecvTimeOutTick = 1;
				}
				binaryCommunication.ReceiveTimeOut = receiveTimeOutOld;
				modbus.Station = stationOld;
			});
		}

		private void StationSearchControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label1.Text = "Station Start:";
				label2.Text = "Station Stop:";
				label3.Text = "ReceiveTimeout:";
				checkBox_stop_match.Text = "Stop if match?";
				button_start.Text = "Start";
				button_stop.Text = "Stop";
				label4.Text = "Read Address:";
				label5.Text = "Read Length:";
				label6.Text = "Log:";
				label7.Text = "Match Staion:";
				radioButton_bool.Text = "Bool Read";
				radioButton_word.Text = "Word Read";
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_station_start = new System.Windows.Forms.TextBox();
			this.textBox_station_stop = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.checkBox_stop_match = new System.Windows.Forms.CheckBox();
			this.textBox_timeout = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.button_start = new System.Windows.Forms.Button();
			this.button_stop = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_address = new System.Windows.Forms.TextBox();
			this.textBox_length = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton_word = new System.Windows.Forms.RadioButton();
			this.radioButton_bool = new System.Windows.Forms.RadioButton();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_log = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_match = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(5, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(83, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "起始搜索站号:";
			this.textBox_station_start.Location = new System.Drawing.Point(102, 7);
			this.textBox_station_start.Name = "textBox_station_start";
			this.textBox_station_start.Size = new System.Drawing.Size(58, 23);
			this.textBox_station_start.TabIndex = 1;
			this.textBox_station_start.Text = "0";
			this.textBox_station_stop.Location = new System.Drawing.Point(271, 7);
			this.textBox_station_stop.Name = "textBox_station_stop";
			this.textBox_station_stop.Size = new System.Drawing.Size(62, 23);
			this.textBox_station_stop.TabIndex = 3;
			this.textBox_station_stop.Text = "255";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(174, 10);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(83, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "终止搜索站号:";
			this.checkBox_stop_match.AutoSize = true;
			this.checkBox_stop_match.Location = new System.Drawing.Point(495, 9);
			this.checkBox_stop_match.Name = "checkBox_stop_match";
			this.checkBox_stop_match.Size = new System.Drawing.Size(111, 21);
			this.checkBox_stop_match.TabIndex = 4;
			this.checkBox_stop_match.Text = "匹配即停止搜索";
			this.checkBox_stop_match.UseVisualStyleBackColor = true;
			this.textBox_timeout.Location = new System.Drawing.Point(429, 7);
			this.textBox_timeout.Name = "textBox_timeout";
			this.textBox_timeout.Size = new System.Drawing.Size(53, 23);
			this.textBox_timeout.TabIndex = 6;
			this.textBox_timeout.Text = "500";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(341, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(59, 17);
			this.label3.TabIndex = 5;
			this.label3.Text = "超时时间:";
			this.button_start.Location = new System.Drawing.Point(623, 4);
			this.button_start.Name = "button_start";
			this.button_start.Size = new System.Drawing.Size(81, 29);
			this.button_start.TabIndex = 7;
			this.button_start.Text = "开始";
			this.button_start.UseVisualStyleBackColor = true;
			this.button_start.Click += new System.EventHandler(button_start_Click);
			this.button_stop.Enabled = false;
			this.button_stop.Location = new System.Drawing.Point(710, 4);
			this.button_stop.Name = "button_stop";
			this.button_stop.Size = new System.Drawing.Size(81, 29);
			this.button_stop.TabIndex = 8;
			this.button_stop.Text = "停止";
			this.button_stop.UseVisualStyleBackColor = true;
			this.button_stop.Click += new System.EventHandler(button_stop_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(5, 41);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(59, 17);
			this.label4.TabIndex = 9;
			this.label4.Text = "读取地址:";
			this.textBox_address.Location = new System.Drawing.Point(102, 38);
			this.textBox_address.Name = "textBox_address";
			this.textBox_address.Size = new System.Drawing.Size(97, 23);
			this.textBox_address.TabIndex = 10;
			this.textBox_address.Text = "0";
			this.textBox_length.Location = new System.Drawing.Point(293, 38);
			this.textBox_length.Name = "textBox_length";
			this.textBox_length.Size = new System.Drawing.Size(82, 23);
			this.textBox_length.TabIndex = 12;
			this.textBox_length.Text = "1";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(208, 41);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(59, 17);
			this.label5.TabIndex = 11;
			this.label5.Text = "读取长度:";
			this.panel1.Controls.Add(this.radioButton_word);
			this.panel1.Controls.Add(this.radioButton_bool);
			this.panel1.Location = new System.Drawing.Point(388, 35);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(209, 32);
			this.panel1.TabIndex = 13;
			this.radioButton_word.AutoSize = true;
			this.radioButton_word.Checked = true;
			this.radioButton_word.Location = new System.Drawing.Point(101, 4);
			this.radioButton_word.Name = "radioButton_word";
			this.radioButton_word.Size = new System.Drawing.Size(80, 21);
			this.radioButton_word.TabIndex = 1;
			this.radioButton_word.TabStop = true;
			this.radioButton_word.Text = "word读取";
			this.radioButton_word.UseVisualStyleBackColor = true;
			this.radioButton_bool.AutoSize = true;
			this.radioButton_bool.Location = new System.Drawing.Point(3, 4);
			this.radioButton_bool.Name = "radioButton_bool";
			this.radioButton_bool.Size = new System.Drawing.Size(77, 21);
			this.radioButton_bool.TabIndex = 0;
			this.radioButton_bool.TabStop = true;
			this.radioButton_bool.Text = "bool读取";
			this.radioButton_bool.UseVisualStyleBackColor = true;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(5, 68);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(59, 17);
			this.label6.TabIndex = 14;
			this.label6.Text = "实时日志:";
			this.textBox_log.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox_log.Location = new System.Drawing.Point(8, 88);
			this.textBox_log.Multiline = true;
			this.textBox_log.Name = "textBox_log";
			this.textBox_log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_log.Size = new System.Drawing.Size(381, 187);
			this.textBox_log.TabIndex = 15;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(395, 70);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(59, 17);
			this.label7.TabIndex = 16;
			this.label7.Text = "匹配站号:";
			this.textBox_match.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_match.Location = new System.Drawing.Point(395, 88);
			this.textBox_match.Multiline = true;
			this.textBox_match.Name = "textBox_match";
			this.textBox_match.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_match.Size = new System.Drawing.Size(413, 187);
			this.textBox_match.TabIndex = 17;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.textBox_match);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.textBox_log);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.textBox_length);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.textBox_address);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button_stop);
			base.Controls.Add(this.button_start);
			base.Controls.Add(this.textBox_timeout);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.checkBox_stop_match);
			base.Controls.Add(this.textBox_station_stop);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_station_start);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "StationSearchControl";
			base.Size = new System.Drawing.Size(811, 278);
			base.Load += new System.EventHandler(StationSearchControl_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
