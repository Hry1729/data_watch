using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo.DemoControl;
using HslControls;

namespace HslCommunicationDemo.Redis
{
	public class FormRedisCopy : HslFormContent
	{
		private RedisClient client1 = null;

		private RedisClient client2 = null;

		private System.Threading.Timer timer = null;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private HslPanelTextBack hslPanelTextBack1;

		private TextBox textBox3;

		private Label label3;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private HslPanelTextBack hslPanelTextBack2;

		private TextBox textBox4;

		private Label label4;

		private TextBox textBox5;

		private Label label5;

		private TextBox textBox6;

		private Label label6;

		private HslPanelHead hslPanelHead1;

		private HslPanelHead hslPanelHead2;

		private Button button3;

		private Button button2;

		private TextBox textBox8;

		private Label label8;

		private Button button5;

		private Label label7;

		private TextBox textBox7;

		private Button button4;

		private TextBox textBox9;

		private Label label9;

		private TextBox textBox10;

		private Button button7;

		private Button button6;

		private HslProgress hslProgress1;

		private Button button8;

		public FormRedisCopy()
		{
			InitializeComponent();
		}

		private void FormRedisCopy_Load(object sender, EventArgs e)
		{
			hslPanelHead1.Enabled = false;
			button3.Enabled = false;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			if (!int.TryParse(textBox5.Text, out var result2))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			if (!int.TryParse(textBox8.Text, out var result3))
			{
				DemoUtils.ShowMessage("DB Block input wrong");
				return;
			}
			if (!int.TryParse(textBox9.Text, out var result4))
			{
				DemoUtils.ShowMessage("DB Block input wrong");
				return;
			}
			client1?.ConnectClose();
			client2?.ConnectClose();
			client1 = new RedisClient(textBox1.Text, result, textBox3.Text);
			client2 = new RedisClient(textBox6.Text, result2, textBox4.Text);
			OperateResult operateResult = client1.ConnectServer();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + " " + operateResult.Message);
				return;
			}
			OperateResult operateResult2 = client2.ConnectServer();
			if (!operateResult2.IsSuccess)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + " " + operateResult2.Message);
				return;
			}
			if (!client1.SelectDB(result3).IsSuccess)
			{
				DemoUtils.ShowMessage("client1 DB Block select wrong");
				return;
			}
			if (!client2.SelectDB(result4).IsSuccess)
			{
				DemoUtils.ShowMessage("client2 DB Block select wrong");
				return;
			}
			button2.Enabled = false;
			button3.Enabled = true;
			hslPanelTextBack1.Enabled = false;
			hslPanelTextBack2.Enabled = false;
			hslPanelHead1.Enabled = true;
			DemoUtils.ShowMessage("Success");
		}

		private void button3_Click(object sender, EventArgs e)
		{
			client1?.ConnectClose();
			client2?.ConnectClose();
			hslPanelTextBack1.Enabled = true;
			hslPanelTextBack2.Enabled = true;
			button2.Enabled = true;
			button3.Enabled = false;
			hslPanelHead1.Enabled = false;
			timer?.Dispose();
			button5.Text = "定时同步";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage("Clear sure?", "check", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				OperateResult operateResult = client1.FlushDB();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("Flush Success!");
				}
				else
				{
					DemoUtils.ShowMessage("Flush Failed!" + operateResult.Message);
				}
			}
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (DemoUtils.ShowMessage("Clear sure?", "check", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				OperateResult operateResult = client2.FlushDB();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("Flush Success!");
				}
				else
				{
					DemoUtils.ShowMessage("Flush Failed!" + operateResult.Message);
				}
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(RedisCopy, null);
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (button5.Text == "定时同步")
			{
				timer = new System.Threading.Timer(RedisCopy, null, 1000, int.Parse(textBox7.Text));
				button5.Text = "关闭同步";
			}
			else
			{
				timer?.Dispose();
				button5.Text = "定时同步";
			}
		}

		private void MsgShow(string msg, int progress = -1)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<string, int>(MsgShow), msg, progress);
				return;
			}
			textBox10.AppendText(DateTime.Now.ToString() + " " + msg + Environment.NewLine);
			if (progress > 0)
			{
				hslProgress1.Value = progress;
			}
		}

		private void RedisCopy(object obj)
		{
			if (client1 == null || client2 == null)
			{
				return;
			}
			OperateResult<long> operateResult = client1.DBSize();
			if (!operateResult.IsSuccess)
			{
				MsgShow("Size Get Failed");
				return;
			}
			if (operateResult.Content == 0)
			{
				MsgShow("No Key Value");
				return;
			}
			MsgShow("Getting all key Name");
			OperateResult<string[]> operateResult2 = client1.ReadAllKeys("*");
			if (!operateResult2.IsSuccess)
			{
				MsgShow("Key Names Get Failed");
				return;
			}
			hslProgress1.Max = operateResult2.Content.Length;
			hslProgress1.Value = 0;
			MsgShow("Begin Copy key value to redis2");
			for (int i = 0; i < operateResult2.Content.Length; i++)
			{
				OperateResult<string> operateResult3 = client1.ReadKeyType(operateResult2.Content[i]);
				if (!operateResult3.IsSuccess)
				{
					MsgShow("Key[" + operateResult2.Content[i] + "] Get type Failed");
					return;
				}
				MsgShow("Copy Key[" + operateResult2.Content[i] + "] Type[" + operateResult3.Content + "] ...", i + 1);
				if (operateResult3.Content == "string")
				{
					OperateResult<string> operateResult4 = client1.ReadKey(operateResult2.Content[i]);
					if (!operateResult4.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					if (!client2.WriteKey(operateResult2.Content[i], operateResult4.Content).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Copy Failed");
						return;
					}
				}
				else if (operateResult3.Content == "hash")
				{
					OperateResult<string[]> operateResult5 = client1.ReadHashKeys(operateResult2.Content[i]);
					if (!operateResult5.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					OperateResult<string[]> operateResult6 = client1.ReadHashValues(operateResult2.Content[i]);
					if (!operateResult6.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					if (!client2.WriteHashKey(operateResult2.Content[i], operateResult5.Content, operateResult6.Content).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Copy Failed");
						return;
					}
				}
				else if (operateResult3.Content == "list")
				{
					OperateResult<string[]> operateResult7 = client1.ListRange(operateResult2.Content[i], 0L, -1L);
					if (!operateResult7.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					OperateResult<int> listLength = client2.GetListLength(operateResult2.Content[i]);
					if (!listLength.IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Get Length Failed");
						return;
					}
					if (listLength.Content != operateResult7.Content.Length)
					{
						if (!client2.DeleteKey(operateResult2.Content[i]).IsSuccess)
						{
							MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Delete Failed");
							return;
						}
						if (!client2.ListRightPush(operateResult2.Content[i], operateResult7.Content).IsSuccess)
						{
							MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Copy Failed");
							return;
						}
						continue;
					}
					for (int j = 0; j < operateResult7.Content.Length; j++)
					{
						if (!client2.ListSet(operateResult2.Content[i], j, operateResult7.Content[j]).IsSuccess)
						{
							MsgShow($"Redis2 Key[{operateResult2.Content[i]}] Index[{j}] Copy Failed");
							return;
						}
					}
				}
				else if (operateResult3.Content == "set")
				{
					OperateResult<string[]> operateResult8 = client1.SetMembers(operateResult2.Content[i]);
					if (!operateResult8.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					if (!client2.DeleteKey(operateResult2.Content[i]).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Delete Failed");
						return;
					}
					if (!client2.SetAdd(operateResult2.Content[i], operateResult8.Content).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Write Failed");
						return;
					}
				}
				else if (operateResult3.Content == "zset")
				{
					OperateResult<string[]> operateResult9 = client1.ZSetRange(operateResult2.Content[i], 0, -1, true);
					if (!operateResult9.IsSuccess)
					{
						MsgShow("Redis1 Key[" + operateResult2.Content[i] + "] Read Failed");
						return;
					}
					if (!client2.DeleteKey(operateResult2.Content[i]).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Delete Failed");
						return;
					}
					string[] array = new string[operateResult9.Content.Length / 2];
					double[] array2 = new double[operateResult9.Content.Length / 2];
					for (int k = 0; k < array.Length; k++)
					{
						array[i] = operateResult9.Content[i * 2];
						array2[i] = double.Parse(operateResult9.Content[i * 2 + 1]);
					}
					if (!client2.ZSetAdd(operateResult2.Content[i], array, array2).IsSuccess)
					{
						MsgShow("Redis2 Key[" + operateResult2.Content[i] + "] Write Failed");
						return;
					}
				}
			}
			MsgShow("Copy Finish");
		}

		private void button8_Click(object sender, EventArgs e)
		{
			textBox10.Clear();
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
			this.hslPanelTextBack1 = new HslControls.HslPanelTextBack();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.hslPanelTextBack2 = new HslControls.HslPanelTextBack();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.hslPanelHead1 = new HslControls.HslPanelHead();
			this.button8 = new System.Windows.Forms.Button();
			this.hslProgress1 = new HslControls.HslProgress();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.button4 = new System.Windows.Forms.Button();
			this.hslPanelHead2 = new HslControls.HslPanelHead();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.hslPanelTextBack1.SuspendLayout();
			this.hslPanelTextBack2.SuspendLayout();
			this.hslPanelHead1.SuspendLayout();
			this.hslPanelHead2.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7697782.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Redis";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 15;
			this.hslPanelTextBack1.Controls.Add(this.textBox8);
			this.hslPanelTextBack1.Controls.Add(this.textBox3);
			this.hslPanelTextBack1.Controls.Add(this.label8);
			this.hslPanelTextBack1.Controls.Add(this.label3);
			this.hslPanelTextBack1.Controls.Add(this.textBox2);
			this.hslPanelTextBack1.Controls.Add(this.label2);
			this.hslPanelTextBack1.Controls.Add(this.textBox1);
			this.hslPanelTextBack1.Controls.Add(this.label1);
			this.hslPanelTextBack1.Location = new System.Drawing.Point(3, 35);
			this.hslPanelTextBack1.Name = "hslPanelTextBack1";
			this.hslPanelTextBack1.PanelBackColor = System.Drawing.SystemColors.Control;
			this.hslPanelTextBack1.PanelTextBackColor = System.Drawing.Color.FromArgb(224, 224, 224);
			this.hslPanelTextBack1.Size = new System.Drawing.Size(318, 157);
			this.hslPanelTextBack1.TabIndex = 16;
			this.hslPanelTextBack1.Text = "原Redis";
			this.hslPanelTextBack1.TextOffect = 20;
			this.textBox8.Location = new System.Drawing.Point(113, 121);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(145, 23);
			this.textBox8.TabIndex = 26;
			this.textBox8.Text = "0";
			this.textBox3.Location = new System.Drawing.Point(113, 91);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(145, 23);
			this.textBox3.TabIndex = 5;
			this.label8.AutoSize = true;
			this.label8.BackColor = System.Drawing.Color.Transparent;
			this.label8.Location = new System.Drawing.Point(24, 124);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(59, 17);
			this.label8.TabIndex = 25;
			this.label8.Text = "dbBlock:";
			this.label3.AutoSize = true;
			this.label3.BackColor = System.Drawing.Color.Transparent;
			this.label3.Location = new System.Drawing.Point(24, 94);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(67, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "Password:";
			this.textBox2.Location = new System.Drawing.Point(113, 61);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(145, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "6379";
			this.label2.AutoSize = true;
			this.label2.BackColor = System.Drawing.Color.Transparent;
			this.label2.Location = new System.Drawing.Point(24, 64);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Port:";
			this.textBox1.Location = new System.Drawing.Point(113, 31);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(145, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.BackColor = System.Drawing.Color.Transparent;
			this.label1.Location = new System.Drawing.Point(24, 34);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(75, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip Address:";
			this.hslPanelTextBack2.Controls.Add(this.textBox9);
			this.hslPanelTextBack2.Controls.Add(this.label9);
			this.hslPanelTextBack2.Controls.Add(this.textBox4);
			this.hslPanelTextBack2.Controls.Add(this.label4);
			this.hslPanelTextBack2.Controls.Add(this.textBox5);
			this.hslPanelTextBack2.Controls.Add(this.label5);
			this.hslPanelTextBack2.Controls.Add(this.textBox6);
			this.hslPanelTextBack2.Controls.Add(this.label6);
			this.hslPanelTextBack2.Location = new System.Drawing.Point(327, 35);
			this.hslPanelTextBack2.Name = "hslPanelTextBack2";
			this.hslPanelTextBack2.PanelBackColor = System.Drawing.SystemColors.Control;
			this.hslPanelTextBack2.PanelTextBackColor = System.Drawing.Color.FromArgb(224, 224, 224);
			this.hslPanelTextBack2.Size = new System.Drawing.Size(320, 157);
			this.hslPanelTextBack2.TabIndex = 17;
			this.hslPanelTextBack2.Text = "新Redis";
			this.hslPanelTextBack2.TextOffect = 20;
			this.textBox9.Location = new System.Drawing.Point(118, 121);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(145, 23);
			this.textBox9.TabIndex = 28;
			this.textBox9.Text = "0";
			this.label9.AutoSize = true;
			this.label9.BackColor = System.Drawing.Color.Transparent;
			this.label9.Location = new System.Drawing.Point(29, 124);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(59, 17);
			this.label9.TabIndex = 27;
			this.label9.Text = "dbBlock:";
			this.textBox4.Location = new System.Drawing.Point(118, 91);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(145, 23);
			this.textBox4.TabIndex = 11;
			this.label4.AutoSize = true;
			this.label4.BackColor = System.Drawing.Color.Transparent;
			this.label4.Location = new System.Drawing.Point(29, 94);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(67, 17);
			this.label4.TabIndex = 10;
			this.label4.Text = "Password:";
			this.textBox5.Location = new System.Drawing.Point(118, 61);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(145, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "6379";
			this.label5.AutoSize = true;
			this.label5.BackColor = System.Drawing.Color.Transparent;
			this.label5.Location = new System.Drawing.Point(29, 64);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(35, 17);
			this.label5.TabIndex = 8;
			this.label5.Text = "Port:";
			this.textBox6.Location = new System.Drawing.Point(118, 31);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(145, 23);
			this.textBox6.TabIndex = 7;
			this.label6.AutoSize = true;
			this.label6.BackColor = System.Drawing.Color.Transparent;
			this.label6.Location = new System.Drawing.Point(29, 34);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(75, 17);
			this.label6.TabIndex = 6;
			this.label6.Text = "Ip Address:";
			this.hslPanelHead1.Controls.Add(this.button8);
			this.hslPanelHead1.Controls.Add(this.hslProgress1);
			this.hslPanelHead1.Controls.Add(this.textBox10);
			this.hslPanelHead1.Controls.Add(this.button7);
			this.hslPanelHead1.Controls.Add(this.button6);
			this.hslPanelHead1.Controls.Add(this.button5);
			this.hslPanelHead1.Controls.Add(this.label7);
			this.hslPanelHead1.Controls.Add(this.textBox7);
			this.hslPanelHead1.Controls.Add(this.button4);
			this.hslPanelHead1.Location = new System.Drawing.Point(4, 246);
			this.hslPanelHead1.Name = "hslPanelHead1";
			this.hslPanelHead1.Size = new System.Drawing.Size(996, 395);
			this.hslPanelHead1.TabIndex = 19;
			this.hslPanelHead1.Text = "操作区域";
			this.hslPanelHead1.ThemeColor = System.Drawing.Color.LightSkyBlue;
			this.button8.Location = new System.Drawing.Point(885, 83);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(75, 25);
			this.button8.TabIndex = 29;
			this.button8.Text = "Clear";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.hslProgress1.BackColor = System.Drawing.Color.AliceBlue;
			this.hslProgress1.Location = new System.Drawing.Point(15, 113);
			this.hslProgress1.Name = "hslProgress1";
			this.hslProgress1.ProgressColor = System.Drawing.Color.LightSkyBlue;
			this.hslProgress1.ProgressStyle = HslControls.HslProgressStyle.Horizontal;
			this.hslProgress1.Size = new System.Drawing.Size(945, 22);
			this.hslProgress1.TabIndex = 28;
			this.hslProgress1.TextRenderFormat = "{0} / {1}";
			this.hslProgress1.Value = 0;
			this.textBox10.Location = new System.Drawing.Point(15, 141);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox10.Size = new System.Drawing.Size(945, 246);
			this.textBox10.TabIndex = 27;
			this.button7.Location = new System.Drawing.Point(805, 41);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(155, 37);
			this.button7.TabIndex = 26;
			this.button7.Text = "清空Redis2";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(15, 41);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(155, 37);
			this.button6.TabIndex = 25;
			this.button6.Text = "清空Redis1";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(492, 73);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(155, 37);
			this.button5.TabIndex = 23;
			this.button5.Text = "定时同步";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.label7.AutoSize = true;
			this.label7.BackColor = System.Drawing.Color.Transparent;
			this.label7.Location = new System.Drawing.Point(473, 41);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(41, 17);
			this.label7.TabIndex = 22;
			this.label7.Text = "Timer";
			this.textBox7.Location = new System.Drawing.Point(535, 38);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(145, 23);
			this.textBox7.TabIndex = 21;
			this.textBox7.Text = "1000";
			this.button4.Location = new System.Drawing.Point(315, 73);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(155, 37);
			this.button4.TabIndex = 20;
			this.button4.Text = "立即同步";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.hslPanelHead2.Controls.Add(this.button3);
			this.hslPanelHead2.Controls.Add(this.button2);
			this.hslPanelHead2.Controls.Add(this.hslPanelTextBack1);
			this.hslPanelHead2.Controls.Add(this.hslPanelTextBack2);
			this.hslPanelHead2.Location = new System.Drawing.Point(4, 37);
			this.hslPanelHead2.Name = "hslPanelHead2";
			this.hslPanelHead2.Size = new System.Drawing.Size(996, 205);
			this.hslPanelHead2.TabIndex = 20;
			this.hslPanelHead2.Text = "参数设置区域";
			this.hslPanelHead2.ThemeColor = System.Drawing.Color.LightSkyBlue;
			this.button3.Location = new System.Drawing.Point(668, 114);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(155, 37);
			this.button3.TabIndex = 20;
			this.button3.Text = "断开连接";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button2.Location = new System.Drawing.Point(668, 59);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(155, 37);
			this.button2.TabIndex = 19;
			this.button2.Text = "连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			this.BackColor = System.Drawing.SystemColors.Control;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.hslPanelHead2);
			base.Controls.Add(this.hslPanelHead1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormRedisCopy";
			this.Text = "Redis Copy";
			base.Load += new System.EventHandler(FormRedisCopy_Load);
			this.hslPanelTextBack1.ResumeLayout(false);
			this.hslPanelTextBack1.PerformLayout();
			this.hslPanelTextBack2.ResumeLayout(false);
			this.hslPanelTextBack2.PerformLayout();
			this.hslPanelHead1.ResumeLayout(false);
			this.hslPanelHead1.PerformLayout();
			this.hslPanelHead2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
