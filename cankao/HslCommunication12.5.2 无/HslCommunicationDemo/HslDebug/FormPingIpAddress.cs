using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunicationDemo.DemoControl;
using HslControls;

namespace HslCommunicationDemo.HslDebug
{
	public class FormPingIpAddress : HslFormContent
	{
		private List<PingResult> pingResults = new List<PingResult>();

		private Thread[] threads = null;

		private object lockObject = new object();

		private int index = 0;

		private int dealCount = 0;

		private System.Windows.Forms.Timer timer;

		private int latencyGood = 50;

		private int latencyNormal = 150;

		private bool threadRunning = false;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox_ip_start;

		private Label label1;

		private TextBox textBox_ip_stop;

		private Label label2;

		private Panel panel_main;

		private TextBox textBox2;

		private Label label3;

		private PictureBox pictureBox1;

		private Label label4;

		private Panel panel2;

		private TextBox textBox_thread_count;

		private Label label5;

		private Label label7;

		private Label label8;

		private Label label6;

		private Label label11;

		private Label label12;

		private Label label9;

		private Label label10;

		private TextBox textBox3;

		private TextBox textBox1;

		private Label label13;

		private Label label16;

		private Label label15;

		private Label label14;

		public FormPingIpAddress()
		{
			InitializeComponent();
		}

		private void FormPingIpAddress_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
				Text = "Ping IP Test Tool";
				label1.Text = "Start IP:";
				label2.Text = "End IP:";
				label4.Text = "Thread Count:";
				button1.Text = "Start Test";
				button2.Text = "Stop Test";
				label5.Text = "Good:";
				label8.Text = "Normal:";
				label10.Text = "Poor:";
				label12.Text = "Bad:";
			}
			else
			{
				label1.Text = "起始IP地址：";
				label2.Text = "结束IP地址：";
				label4.Text = "使用线程数：";
				button1.Text = "开始测试";
				button2.Text = "结束测试";
			}
		}

		public async Task<NetworkStatusResult> AnalyzeNetworkStatusAsync(IPAddress ipAddress, int pingCount = 10)
		{
			NetworkStatusResult result = new NetworkStatusResult
			{
				Target = ipAddress.ToString()
			};
			for (int i = 0; i < pingCount; i++)
			{
				using (Ping ping = new Ping())
				{
					PingReply reply = await ping.SendPingAsync(ipAddress, 1000);
					result.PingReplies.Add(reply);
					if (reply.Status == IPStatus.Success)
					{
						result.TotalSuccess++;
						result.TotalRoundtripTime += reply.RoundtripTime;
					}
					await Task.Delay(100);
				}
			}
			result.PacketLossRate = 1.0 - (double)result.TotalSuccess / (double)pingCount;
			if (result.TotalSuccess > 0)
			{
				result.AverageRoundtripTime = result.TotalRoundtripTime / result.TotalSuccess;
			}
			result.Status = EvaluateNetworkStatus(result.AverageRoundtripTime, result.PacketLossRate);
			if (result.TotalSuccess == 0)
			{
				result.Status = NetworkStatus.Bad;
			}
			return result;
		}

		private NetworkStatus EvaluateNetworkStatus(long averageLatency, double packetLossRate)
		{
			if (averageLatency < latencyGood && packetLossRate < 0.05)
			{
				return NetworkStatus.Good;
			}
			if (averageLatency < latencyNormal && packetLossRate < 0.15)
			{
				return NetworkStatus.Normal;
			}
			return NetworkStatus.Poor;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			int[] array = (from s in textBox_ip_start.Text.Split('.')
				select int.Parse(s)).ToArray();
			int[] array2 = (from s in textBox_ip_stop.Text.Split('.')
				select int.Parse(s)).ToArray();
			if (array.Length != 4 || array2.Length != 4)
			{
				MessageBox.Show("IP地址格式不正确，请输入正确的IP地址格式！");
				return;
			}
			if (array[0] != array2[0] || array[1] != array2[1])
			{
				MessageBox.Show("只支持同一网段的IP地址范围，请输入正确的IP地址范围！");
				return;
			}
			List<PingResult> list = new List<PingResult>();
			for (int i = array[2]; i <= array2[2]; i++)
			{
				for (int j = array[3]; j <= array2[3]; j++)
				{
					string text = $"{array[0]}.{array[1]}.{i}.{j}";
					try
					{
						IPAddress iPAddress = IPAddress.Parse(text);
						list.Add(new PingResult
						{
							IpAddress = text
						});
					}
					catch (Exception ex)
					{
						MessageBox.Show("解析IP地址 " + text + " 时发生错误: " + ex.Message);
					}
				}
			}
			int result = 10;
			if (!int.TryParse(textBox_thread_count.Text, out result) || result <= 0 || result > 10000)
			{
				MessageBox.Show("线程数必须是正整数，请输入正确的线程数！");
				return;
			}
			latencyGood = int.Parse(textBox1.Text);
			latencyNormal = int.Parse(textBox3.Text);
			pingResults = list;
			threadRunning = true;
			index = 0;
			dealCount = 0;
			threads = new Thread[result];
			for (int k = 0; k < result; k++)
			{
				threads[k] = new Thread(ThreadPing);
				threads[k].IsBackground = true;
				threads[k].Start(k);
			}
			button1.Enabled = false;
			button2.Enabled = true;
			timer = new System.Windows.Forms.Timer();
			timer.Interval = 250;
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			threadRunning = false;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			pictureBox1.Invalidate();
		}

		private int getDealIndex()
		{
			lock (lockObject)
			{
				if (index < pingResults.Count)
				{
					int result = index;
					index++;
					return result;
				}
				return -1;
			}
		}

		private async void ThreadPing(object obj)
		{
			int id = (int)obj;
			while (true)
			{
				int index = getDealIndex();
				if (index < 0)
				{
					Invoke((Action)delegate
					{
						textBox2.AppendText(DateTime.Now.ToString(DemoUtils.DateTimeFormate) + $" Thread[{id}] finish" + Environment.NewLine);
						if (Interlocked.Increment(ref dealCount) >= threads.Length)
						{
							textBox2.AppendText(DateTime.Now.ToString(DemoUtils.DateTimeFormate) + " All task finish" + Environment.NewLine);
							timer?.Stop();
							pictureBox1.Invalidate();
							button1.Enabled = true;
							button2.Enabled = false;
						}
					});
					return;
				}
				if (!threadRunning)
				{
					break;
				}
				PingResult pingResult = pingResults[index];
				_ = DateTime.Now;
				pingResult.NetworkStatus = await AnalyzeNetworkStatusAsync(IPAddress.Parse(pingResult.IpAddress), 4);
				Invoke((Action)delegate
				{
					textBox2.AppendText(DateTime.Now.ToString(DemoUtils.DateTimeFormate) + $" Thread[{id}] deal ip: {pingResult.IpAddress}  Avg-Time: {pingResult.NetworkStatus.AverageRoundtripTime}ms LossRate:{pingResult.NetworkStatus.PacketLossRate * 100.0:F2}%" + Environment.NewLine);
				});
			}
			Invoke((Action)delegate
			{
				textBox2.AppendText(DateTime.Now.ToString(DemoUtils.DateTimeFormate) + $" Thread[{id}] stop" + Environment.NewLine);
			});
		}

		private void pictureBox1_Paint(object sender, PaintEventArgs e)
		{
			int num = 50;
			int num2 = 20;
			int num3 = 0;
			int num4 = 0;
			int count = pingResults.Count;
			for (int i = 0; i < count; i++)
			{
				PingResult pingResult = pingResults[i];
				Color color;
				if (pingResult.NetworkStatus == null)
				{
					color = Color.WhiteSmoke;
				}
				else
				{
					switch (pingResult.NetworkStatus.Status)
					{
					case NetworkStatus.Good:
						color = Color.LimeGreen;
						break;
					case NetworkStatus.Normal:
						color = Color.Yellow;
						break;
					case NetworkStatus.Poor:
						color = Color.Pink;
						break;
					case NetworkStatus.Bad:
						color = Color.Tomato;
						break;
					default:
						color = Color.WhiteSmoke;
						break;
					}
				}
				e.Graphics.FillRectangle(new SolidBrush(color), num3, num4, num, num2);
				if (pingResult.NetworkStatus == null)
				{
					e.Graphics.DrawRectangle(Pens.LightGray, num3, num4, num - 1, num2 - 1);
				}
				else
				{
					e.Graphics.DrawRectangle(Pens.Gray, num3, num4, num - 1, num2 - 1);
				}
				e.Graphics.DrawString(Regex.Match(pingResult.IpAddress, "[0-9]+\\.[0-9]+$").Value, Font, Brushes.Black, new RectangleF(num3, num4, num, num2), HslHelper.StringFormatCenter);
				num3 += num + 2;
				if (num3 >= pictureBox1.Width - num - 2)
				{
					num3 = 0;
					num4 += num2 + 2;
				}
			}
		}

		private void panel2_Resize(object sender, EventArgs e)
		{
			pictureBox1.Invalidate();
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip_start.Text);
			element.SetAttributeValue("IpAddress2", textBox_ip_stop.Text);
			element.SetAttributeValue("ThreadCount", textBox_thread_count.Text);
			element.SetAttributeValue("RoundtripTime1", textBox1.Text);
			element.SetAttributeValue("RoundtripTime2", textBox3.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip_start.Text = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlIpAddress, "192.168.0.1", (string m) => m);
			textBox_ip_stop.Text = HslFormContent.GetXmlValue(element, "IpAddress2", "192.168.0.255", (string m) => m);
			textBox_thread_count.Text = HslFormContent.GetXmlValue(element, "ThreadCount", "10", (string m) => m);
			textBox1.Text = HslFormContent.GetXmlValue(element, "RoundtripTime1", "50", (string m) => m);
			textBox3.Text = HslFormContent.GetXmlValue(element, "RoundtripTime2", "150", (string m) => m);
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
			this.label16 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_thread_count = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_ip_stop = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_ip_start = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel_main = new System.Windows.Forms.Panel();
			this.panel2 = new System.Windows.Forms.Panel();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel_main.SuspendLayout();
			this.panel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "IpAddress PING";
			this.userControlHead1.Size = new System.Drawing.Size(1005, 32);
			this.userControlHead1.TabIndex = 22;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label16);
			this.panel1.Controls.Add(this.label15);
			this.panel1.Controls.Add(this.label14);
			this.panel1.Controls.Add(this.label13);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label11);
			this.panel1.Controls.Add(this.label12);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.label10);
			this.panel1.Controls.Add(this.label7);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.textBox_thread_count);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox_ip_stop);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_ip_start);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(998, 60);
			this.panel1.TabIndex = 23;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(570, 35);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(17, 17);
			this.label16.TabIndex = 23;
			this.label16.Text = "<";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(405, 35);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(17, 17);
			this.label15.TabIndex = 22;
			this.label15.Text = "<";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(494, 35);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(17, 17);
			this.label14.TabIndex = 21;
			this.label14.Text = "<";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(330, 35);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(17, 17);
			this.label13.TabIndex = 20;
			this.label13.Text = "<";
			this.textBox3.Location = new System.Drawing.Point(516, 32);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(48, 23);
			this.textBox3.TabIndex = 19;
			this.textBox3.Text = "150";
			this.textBox1.Location = new System.Drawing.Point(351, 32);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(48, 23);
			this.textBox1.TabIndex = 18;
			this.textBox1.Text = "50";
			this.label11.BackColor = System.Drawing.Color.Tomato;
			this.label11.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label11.Location = new System.Drawing.Point(731, 35);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(17, 17);
			this.label11.TabIndex = 17;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(678, 35);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(47, 17);
			this.label12.TabIndex = 16;
			this.label12.Text = "不可达:";
			this.label9.BackColor = System.Drawing.Color.Pink;
			this.label9.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label9.Location = new System.Drawing.Point(634, 35);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(17, 17);
			this.label9.TabIndex = 15;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(593, 35);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(35, 17);
			this.label10.TabIndex = 14;
			this.label10.Text = "较差:";
			this.label7.BackColor = System.Drawing.Color.Yellow;
			this.label7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label7.Location = new System.Drawing.Point(471, 35);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(17, 17);
			this.label7.TabIndex = 13;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(427, 35);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(35, 17);
			this.label8.TabIndex = 12;
			this.label8.Text = "一般:";
			this.label6.BackColor = System.Drawing.Color.LightGreen;
			this.label6.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label6.Location = new System.Drawing.Point(307, 35);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(17, 17);
			this.label6.TabIndex = 11;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(261, 35);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(35, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "良好:";
			this.textBox_thread_count.Location = new System.Drawing.Point(106, 32);
			this.textBox_thread_count.Name = "textBox_thread_count";
			this.textBox_thread_count.Size = new System.Drawing.Size(135, 23);
			this.textBox_thread_count.TabIndex = 9;
			this.textBox_thread_count.Text = "10";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(8, 35);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(92, 17);
			this.label4.TabIndex = 8;
			this.label4.Text = "使用线程数量：";
			this.textBox_ip_stop.Location = new System.Drawing.Point(330, 5);
			this.textBox_ip_stop.Name = "textBox_ip_stop";
			this.textBox_ip_stop.Size = new System.Drawing.Size(163, 23);
			this.textBox_ip_stop.TabIndex = 7;
			this.textBox_ip_stop.Text = "192.168.0.255";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(260, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(46, 17);
			this.label2.TabIndex = 6;
			this.label2.Text = "终止IP:";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(633, 2);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "结束测试";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(536, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "开始测试";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_ip_start.Location = new System.Drawing.Point(78, 5);
			this.textBox_ip_start.Name = "textBox_ip_start";
			this.textBox_ip_start.Size = new System.Drawing.Size(163, 23);
			this.textBox_ip_start.TabIndex = 1;
			this.textBox_ip_start.Text = "192.168.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(46, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "起始IP:";
			this.panel_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel_main.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel_main.Controls.Add(this.panel2);
			this.panel_main.Controls.Add(this.textBox2);
			this.panel_main.Controls.Add(this.label3);
			this.panel_main.Location = new System.Drawing.Point(3, 98);
			this.panel_main.Name = "panel_main";
			this.panel_main.Size = new System.Drawing.Size(998, 536);
			this.panel_main.TabIndex = 24;
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.AutoScroll = true;
			this.panel2.BackColor = System.Drawing.Color.White;
			this.panel2.Controls.Add(this.pictureBox1);
			this.panel2.Location = new System.Drawing.Point(0, 22);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(996, 294);
			this.panel2.TabIndex = 3;
			this.panel2.Resize += new System.EventHandler(panel2_Resize);
			this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(995, 291);
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(pictureBox1_Paint);
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox2.Location = new System.Drawing.Point(3, 322);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox2.Size = new System.Drawing.Size(986, 209);
			this.textBox2.TabIndex = 2;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(4, 2);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(35, 17);
			this.label3.TabIndex = 1;
			this.label3.Text = "状态:";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1005, 636);
			base.Controls.Add(this.panel_main);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormPingIpAddress";
			this.Text = "PING Test";
			base.Load += new System.EventHandler(FormPingIpAddress_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel_main.ResumeLayout(false);
			this.panel_main.PerformLayout();
			this.panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
