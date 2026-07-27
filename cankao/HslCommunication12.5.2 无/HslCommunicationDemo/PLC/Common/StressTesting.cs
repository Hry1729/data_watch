using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;
using HslControls;

namespace HslCommunicationDemo.PLC.Common
{
	public class StressTesting : UserControl
	{
		private long currentCount = 0L;

		private int repeatTimes = 1000;

		private int thread_status = 0;

		private int failed = 0;

		private string[] addresses;

		private System.Windows.Forms.Timer testProgressTimer;

		private int read_write_count = 0;

		private double time_cost = 0.0;

		private int check_same_count = 0;

		private int check_same_count_failed = 0;

		private IReadWriteNet readWriteNet;

		private IContainer components = null;

		private GroupBox groupBox_press;

		private TextBox textBox_test_address;

		private Label label_pressure_address;

		private TextBox textBox_repreat_times;

		private Label label_repeat_times;

		private TextBox textBox_thread_count;

		private Label label_thread_count;

		private Button button_test_start;

		private Label label_pressure_progress;

		private CheckBox checkBox_write;

		private CheckBox checkBox_read;

		private HslProgressBar hslProgressBar1;

		private GroupBox groupBox1;

		private Label label2;

		private TextBox textBox_total_count;

		private Label label1;

		private TextBox textBox_total_time;

		private Label label3;

		private TextBox textBox3;

		private CheckBox checkBox_check_same;

		private TextBox textBox_avg_time;

		private Label label4;

		private CheckBox checkBox_continue_when_error;

		private TextBox textBox_check_same;

		private Label label5;

		private TextBox textBox_check_wrong;

		private Label label6;

		private TextBox textBox_failed_count;

		private Label label7;

		private Label label_info;

		private Label label8;

		public StressTesting()
		{
			InitializeComponent();
		}

		private string GetAddressIndex(int index)
		{
			if (addresses == null)
			{
				return string.Empty;
			}
			if (addresses.Length == 0)
			{
				return string.Empty;
			}
			if (index >= addresses.Length)
			{
				return addresses[addresses.Length - 1];
			}
			return addresses[index];
		}

		private void thread_test(object obj)
		{
			string address = string.Empty;
			if (obj is int index)
			{
				address = GetAddressIndex(index);
			}
			int num = repeatTimes;
			short num2 = 0;
			while (num > 0)
			{
				if (checkBox_write.Checked)
				{
					DateTime now = DateTime.Now;
					OperateResult operateResult = readWriteNet.Write(address, num2);
					time_cost += (DateTime.Now - now).TotalMilliseconds;
					if (!operateResult.IsSuccess)
					{
						Interlocked.Increment(ref failed);
					}
					Interlocked.Increment(ref read_write_count);
				}
				if (checkBox_read.Checked)
				{
					DateTime now2 = DateTime.Now;
					OperateResult<short> operateResult2 = readWriteNet.ReadInt16(address);
					time_cost += (DateTime.Now - now2).TotalMilliseconds;
					if (!operateResult2.IsSuccess)
					{
						Interlocked.Increment(ref failed);
					}
					else if (checkBox_check_same.Checked)
					{
						if (operateResult2.Content == num2)
						{
							Interlocked.Increment(ref check_same_count);
						}
						else
						{
							Interlocked.Increment(ref check_same_count_failed);
						}
					}
					Interlocked.Increment(ref read_write_count);
				}
				num--;
				Interlocked.Increment(ref currentCount);
				num2++;
			}
			thread_end();
		}

		private void thread_end()
		{
			if (Interlocked.Decrement(ref thread_status) == 0)
			{
				Invoke((Action)delegate
				{
					button_test_start.Enabled = true;
					label_info.Text = ((Program.Language == 1) ? "测试完成!" : "Test Finish!");
				});
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_thread_count.Text, out thread_status))
			{
				DemoUtils.ShowMessage("thread_count input wrong!");
				return;
			}
			if (!int.TryParse(textBox_repreat_times.Text, out repeatTimes))
			{
				DemoUtils.ShowMessage("repreat_times input wrong!");
				return;
			}
			addresses = textBox_test_address.Text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			failed = 0;
			check_same_count_failed = 0;
			read_write_count = 0;
			currentCount = 0L;
			time_cost = 0.0;
			check_same_count = 0;
			label_info.Visible = true;
			label_info.Text = ((Program.Language == 1) ? "测试开始..." : "Test Start...");
			hslProgressBar1.Value = 0;
			for (int i = 0; i < thread_status; i++)
			{
				Thread thread = new Thread(thread_test);
				thread.IsBackground = true;
				thread.Start(i);
			}
			button_test_start.Enabled = false;
			testProgressTimer = new System.Windows.Forms.Timer();
			testProgressTimer.Interval = 1000;
			testProgressTimer.Tick += TestProgressTimer_Tick;
			testProgressTimer.Start();
		}

		private void TestProgressTimer_Tick(object sender, EventArgs e)
		{
			if (currentCount >= repeatTimes * thread_status)
			{
				hslProgressBar1.Value = 100;
				testProgressTimer?.Stop();
				testProgressTimer?.Dispose();
			}
			else
			{
				double num = (double)(currentCount * 100) / ((double)(repeatTimes * thread_status) * 1.0);
				hslProgressBar1.Value = (int)num;
			}
			textBox_total_count.Text = read_write_count.ToString();
			textBox_total_time.Text = (time_cost / 1000.0).ToString("F1") + " s";
			if (read_write_count != 0)
			{
				textBox_avg_time.Text = (time_cost / (double)read_write_count).ToString("F0") + " ms";
			}
			textBox_check_same.Text = check_same_count.ToString();
			textBox_failed_count.Text = failed.ToString();
			textBox_check_wrong.Text = check_same_count_failed.ToString();
		}

		public void SetReadWriteNet(IReadWriteNet readWriteNet, string address)
		{
			this.readWriteNet = readWriteNet;
			textBox_test_address.Text = address;
			if (this.readWriteNet != null)
			{
				button_test_start.Enabled = true;
			}
		}

		private void StressTesting_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
				groupBox_press.Text = "线程设置";
				label_thread_count.Text = "线程数量:";
				label_repeat_times.Text = "重复次数:";
				label_pressure_address.Text = "地址:";
				label3.Text = "如果每个线程地址不一样：D100;D200;D300";
				checkBox_check_same.Text = "检查写入值及读取值是否一致?";
				checkBox_continue_when_error.Text = "在读写失败时是否继续?";
				groupBox1.Text = "测试结果";
				label_pressure_progress.Text = "测试进度:";
				label1.Text = "读写总次数:";
				label2.Text = "总耗时(秒):";
				label4.Text = "平均耗时(ms):";
				label5.Text = "读写校验正确:";
				label7.Text = "失败次数:";
				label6.Text = "读写校验错误:";
				button_test_start.Text = "开始";
			}
			else
			{
				label8.Text = "Warning: test when it is confirmed to be safe!!";
			}
			label_info.Visible = false;
		}

		private void StressTesting_Resize(object sender, EventArgs e)
		{
			if (base.Width > 100)
			{
				groupBox1.Size = new Size(base.Width - 413, groupBox_press.Size.Height);
				hslProgressBar1.Width = groupBox1.Width - 110;
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
			this.groupBox_press = new System.Windows.Forms.GroupBox();
			this.checkBox_continue_when_error = new System.Windows.Forms.CheckBox();
			this.checkBox_check_same = new System.Windows.Forms.CheckBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.checkBox_write = new System.Windows.Forms.CheckBox();
			this.checkBox_read = new System.Windows.Forms.CheckBox();
			this.button_test_start = new System.Windows.Forms.Button();
			this.textBox_test_address = new System.Windows.Forms.TextBox();
			this.label_pressure_address = new System.Windows.Forms.Label();
			this.textBox_repreat_times = new System.Windows.Forms.TextBox();
			this.label_repeat_times = new System.Windows.Forms.Label();
			this.textBox_thread_count = new System.Windows.Forms.TextBox();
			this.label_thread_count = new System.Windows.Forms.Label();
			this.label_pressure_progress = new System.Windows.Forms.Label();
			this.hslProgressBar1 = new HslControls.HslProgressBar();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label_info = new System.Windows.Forms.Label();
			this.textBox_check_wrong = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_failed_count = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_check_same = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_avg_time = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_total_time = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_total_count = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.groupBox_press.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.groupBox_press.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.groupBox_press.Controls.Add(this.checkBox_continue_when_error);
			this.groupBox_press.Controls.Add(this.checkBox_check_same);
			this.groupBox_press.Controls.Add(this.label3);
			this.groupBox_press.Controls.Add(this.textBox3);
			this.groupBox_press.Controls.Add(this.checkBox_write);
			this.groupBox_press.Controls.Add(this.checkBox_read);
			this.groupBox_press.Controls.Add(this.button_test_start);
			this.groupBox_press.Controls.Add(this.textBox_test_address);
			this.groupBox_press.Controls.Add(this.label_pressure_address);
			this.groupBox_press.Controls.Add(this.textBox_repreat_times);
			this.groupBox_press.Controls.Add(this.label_repeat_times);
			this.groupBox_press.Controls.Add(this.textBox_thread_count);
			this.groupBox_press.Controls.Add(this.label_thread_count);
			this.groupBox_press.Location = new System.Drawing.Point(3, 3);
			this.groupBox_press.Name = "groupBox_press";
			this.groupBox_press.Size = new System.Drawing.Size(401, 280);
			this.groupBox_press.TabIndex = 0;
			this.groupBox_press.TabStop = false;
			this.groupBox_press.Text = "Thread Settings";
			this.checkBox_continue_when_error.AutoSize = true;
			this.checkBox_continue_when_error.Checked = true;
			this.checkBox_continue_when_error.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_continue_when_error.Location = new System.Drawing.Point(9, 179);
			this.checkBox_continue_when_error.Name = "checkBox_continue_when_error";
			this.checkBox_continue_when_error.Size = new System.Drawing.Size(199, 21);
			this.checkBox_continue_when_error.TabIndex = 14;
			this.checkBox_continue_when_error.Text = "Continued read/write if it fails";
			this.checkBox_continue_when_error.UseVisualStyleBackColor = true;
			this.checkBox_check_same.AutoSize = true;
			this.checkBox_check_same.Checked = true;
			this.checkBox_check_same.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_check_same.Location = new System.Drawing.Point(109, 157);
			this.checkBox_check_same.Name = "checkBox_check_same";
			this.checkBox_check_same.Size = new System.Drawing.Size(218, 21);
			this.checkBox_check_same.TabIndex = 13;
			this.checkBox_check_same.Text = "Check same with the write value?";
			this.checkBox_check_same.UseVisualStyleBackColor = true;
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.Color.Gray;
			this.label3.Location = new System.Drawing.Point(108, 106);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(282, 17);
			this.label3.TabIndex = 12;
			this.label3.Text = "every thread use diff address: D100;D200;D300";
			this.textBox3.Location = new System.Drawing.Point(109, 128);
			this.textBox3.Name = "textBox3";
			this.textBox3.ReadOnly = true;
			this.textBox3.Size = new System.Drawing.Size(286, 23);
			this.textBox3.TabIndex = 11;
			this.textBox3.Text = "0-32767";
			this.checkBox_write.AutoSize = true;
			this.checkBox_write.Checked = true;
			this.checkBox_write.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_write.Location = new System.Drawing.Point(9, 130);
			this.checkBox_write.Name = "checkBox_write";
			this.checkBox_write.Size = new System.Drawing.Size(92, 21);
			this.checkBox_write.TabIndex = 10;
			this.checkBox_write.Text = "Write short";
			this.checkBox_write.UseVisualStyleBackColor = true;
			this.checkBox_read.AutoSize = true;
			this.checkBox_read.Checked = true;
			this.checkBox_read.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_read.Location = new System.Drawing.Point(9, 155);
			this.checkBox_read.Name = "checkBox_read";
			this.checkBox_read.Size = new System.Drawing.Size(91, 21);
			this.checkBox_read.TabIndex = 9;
			this.checkBox_read.Text = "Read short";
			this.checkBox_read.UseVisualStyleBackColor = true;
			this.button_test_start.Enabled = false;
			this.button_test_start.Location = new System.Drawing.Point(121, 203);
			this.button_test_start.Name = "button_test_start";
			this.button_test_start.Size = new System.Drawing.Size(147, 34);
			this.button_test_start.TabIndex = 8;
			this.button_test_start.Text = "Start";
			this.button_test_start.UseVisualStyleBackColor = true;
			this.button_test_start.Click += new System.EventHandler(button1_Click);
			this.textBox_test_address.Location = new System.Drawing.Point(105, 50);
			this.textBox_test_address.Multiline = true;
			this.textBox_test_address.Name = "textBox_test_address";
			this.textBox_test_address.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_test_address.Size = new System.Drawing.Size(290, 56);
			this.textBox_test_address.TabIndex = 5;
			this.textBox_test_address.Text = "D100";
			this.label_pressure_address.AutoSize = true;
			this.label_pressure_address.Location = new System.Drawing.Point(6, 53);
			this.label_pressure_address.Name = "label_pressure_address";
			this.label_pressure_address.Size = new System.Drawing.Size(59, 17);
			this.label_pressure_address.TabIndex = 4;
			this.label_pressure_address.Text = "Address:";
			this.textBox_repreat_times.Location = new System.Drawing.Point(303, 22);
			this.textBox_repreat_times.Name = "textBox_repreat_times";
			this.textBox_repreat_times.Size = new System.Drawing.Size(92, 23);
			this.textBox_repreat_times.TabIndex = 3;
			this.textBox_repreat_times.Text = "1000";
			this.label_repeat_times.AutoSize = true;
			this.label_repeat_times.Location = new System.Drawing.Point(195, 25);
			this.label_repeat_times.Name = "label_repeat_times";
			this.label_repeat_times.Size = new System.Drawing.Size(87, 17);
			this.label_repeat_times.TabIndex = 2;
			this.label_repeat_times.Text = "Repeat times:";
			this.textBox_thread_count.Location = new System.Drawing.Point(105, 22);
			this.textBox_thread_count.Name = "textBox_thread_count";
			this.textBox_thread_count.Size = new System.Drawing.Size(73, 23);
			this.textBox_thread_count.TabIndex = 1;
			this.textBox_thread_count.Text = "1";
			this.label_thread_count.AutoSize = true;
			this.label_thread_count.Location = new System.Drawing.Point(6, 25);
			this.label_thread_count.Name = "label_thread_count";
			this.label_thread_count.Size = new System.Drawing.Size(90, 17);
			this.label_thread_count.TabIndex = 0;
			this.label_thread_count.Text = "Thread Count:";
			this.label_pressure_progress.AutoSize = true;
			this.label_pressure_progress.Location = new System.Drawing.Point(6, 22);
			this.label_pressure_progress.Name = "label_pressure_progress";
			this.label_pressure_progress.Size = new System.Drawing.Size(63, 17);
			this.label_pressure_progress.TabIndex = 6;
			this.label_pressure_progress.Text = "Progress:";
			this.hslProgressBar1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslProgressBar1.CustmerTextTranslate = null;
			this.hslProgressBar1.InflateWidth = 2;
			this.hslProgressBar1.Location = new System.Drawing.Point(105, 19);
			this.hslProgressBar1.Name = "hslProgressBar1";
			this.hslProgressBar1.ProgressStyle = HslControls.ProgressStyle.Percent;
			this.hslProgressBar1.Size = new System.Drawing.Size(419, 23);
			this.hslProgressBar1.TabIndex = 11;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.label_info);
			this.groupBox1.Controls.Add(this.textBox_check_wrong);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.textBox_failed_count);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox_check_same);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.textBox_avg_time);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.textBox_total_time);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.textBox_total_count);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.hslProgressBar1);
			this.groupBox1.Controls.Add(this.label_pressure_progress);
			this.groupBox1.Location = new System.Drawing.Point(410, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(530, 280);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Test Result";
			this.label_info.AutoSize = true;
			this.label_info.Font = new System.Drawing.Font("微软雅黑", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label_info.ForeColor = System.Drawing.Color.Teal;
			this.label_info.Location = new System.Drawing.Point(6, 174);
			this.label_info.Name = "label_info";
			this.label_info.Size = new System.Drawing.Size(107, 27);
			this.label_info.TabIndex = 24;
			this.label_info.Text = "测试开始...";
			this.textBox_check_wrong.Location = new System.Drawing.Point(306, 118);
			this.textBox_check_wrong.Name = "textBox_check_wrong";
			this.textBox_check_wrong.ReadOnly = true;
			this.textBox_check_wrong.Size = new System.Drawing.Size(86, 23);
			this.textBox_check_wrong.TabIndex = 23;
			this.textBox_check_wrong.Text = "0";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(209, 121);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(90, 17);
			this.label6.TabIndex = 22;
			this.label6.Text = "Check Wrong:";
			this.textBox_failed_count.Location = new System.Drawing.Point(105, 118);
			this.textBox_failed_count.Name = "textBox_failed_count";
			this.textBox_failed_count.ReadOnly = true;
			this.textBox_failed_count.Size = new System.Drawing.Size(84, 23);
			this.textBox_failed_count.TabIndex = 21;
			this.textBox_failed_count.Text = "0";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 124);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(83, 17);
			this.label7.TabIndex = 20;
			this.label7.Text = "Failed Count:";
			this.textBox_check_same.Location = new System.Drawing.Point(306, 86);
			this.textBox_check_same.Name = "textBox_check_same";
			this.textBox_check_same.ReadOnly = true;
			this.textBox_check_same.Size = new System.Drawing.Size(86, 23);
			this.textBox_check_same.TabIndex = 19;
			this.textBox_check_same.Text = "0";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(209, 89);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(82, 17);
			this.label5.TabIndex = 18;
			this.label5.Text = "Check Same:";
			this.textBox_avg_time.Location = new System.Drawing.Point(105, 86);
			this.textBox_avg_time.Name = "textBox_avg_time";
			this.textBox_avg_time.ReadOnly = true;
			this.textBox_avg_time.Size = new System.Drawing.Size(84, 23);
			this.textBox_avg_time.TabIndex = 17;
			this.textBox_avg_time.Text = "0";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 89);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(88, 17);
			this.label4.TabIndex = 16;
			this.label4.Text = "R/W-Avg(ms):";
			this.textBox_total_time.Location = new System.Drawing.Point(306, 50);
			this.textBox_total_time.Name = "textBox_total_time";
			this.textBox_total_time.ReadOnly = true;
			this.textBox_total_time.Size = new System.Drawing.Size(86, 23);
			this.textBox_total_time.TabIndex = 15;
			this.textBox_total_time.Text = "0";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(209, 53);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(91, 17);
			this.label2.TabIndex = 14;
			this.label2.Text = "Timer Total(s):";
			this.textBox_total_count.Location = new System.Drawing.Point(105, 50);
			this.textBox_total_count.Name = "textBox_total_count";
			this.textBox_total_count.ReadOnly = true;
			this.textBox_total_count.Size = new System.Drawing.Size(84, 23);
			this.textBox_total_count.TabIndex = 13;
			this.textBox_total_count.Text = "0";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 56);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(74, 17);
			this.label1.TabIndex = 12;
			this.label1.Text = "R/W Count:";
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.Red;
			this.label8.Location = new System.Drawing.Point(9, 152);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(260, 17);
			this.label8.TabIndex = 25;
			this.label8.Text = "警告：请在确认安全的情况下发起测试！！！！";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.groupBox_press);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "StressTesting";
			base.Size = new System.Drawing.Size(946, 286);
			base.Load += new System.EventHandler(StressTesting_Load);
			base.Resize += new System.EventHandler(StressTesting_Resize);
			this.groupBox_press.ResumeLayout(false);
			this.groupBox_press.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
