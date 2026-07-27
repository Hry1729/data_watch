using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormRedisClient : HslFormContent
	{
		private RedisClient redisClient = new RedisClient("");

		private int status = 1;

		private Random random = new Random();

		private IContainer components = null;

		private Panel panel1;

		private TextBox textBox3;

		private Label label6;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private TextBox textBox7;

		private Label label11;

		private TextBox textBox6;

		private Label label10;

		private Button button_set_key;

		private TextBox textBox4;

		private Label label9;

		private TextBox textBox_write_key;

		private Label label7;

		private Button button5;

		private Button button6;

		private TextBox textBox8;

		private Label label8;

		private TextBox textBox9;

		private Label label12;

		private Button button_get_key;

		private TextBox textBox_get_result;

		private Label label13;

		private TextBox textBox_get_key;

		private Label label14;

		private Button button7;

		private Button button_redis_execute_cmd;

		private Button button10;

		private Button button9;

		private Button button11;

		private Button button12;

		private UserControlHead userControlHead1;

		private Button button_redis_EXISTS;

		private GroupBox groupBox1;

		private Button button_redis_PERSIST;

		private TextBox textBox_redis_cmd;

		private Label label2;

		private Button button_redis_INCR;

		private Button button_redis_DECR;

		private Button button_redis_STRLEN;

		private TextBox textBox_code;

		private Label label4;

		public FormRedisClient()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "Redis网络客户端";
				label1.Text = "Ip地址：";
				label3.Text = "端口号：";
				button1.Text = "连接";
				button2.Text = "断开连接";
				label6.Text = "密码：";
				button5.Text = "启动短连接";
				button6.Text = "压力测试";
				label7.Text = "指令头：";
				label8.Text = "耗时";
				label9.Text = "数据：";
				button_set_key.Text = "写入";
				label10.Text = "次数：";
				label11.Text = "耗时：";
				button_get_key.Text = "读取";
				label12.Text = "接收：";
			}
			else
			{
				Text = "Redis Client Test";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label6.Text = "Password:";
				button5.Text = "Start a short connection";
				button6.Text = "Pressure test";
				label7.Text = "Command:";
				label8.Text = "Take:";
				label9.Text = "Data:";
				label10.Text = "Times:";
				label11.Text = "Take:";
				label12.Text = "Receive:";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			redisClient = new RedisClient(textBox3.Text);
			redisClient.IpAddress = textBox1.Text;
			redisClient.Port = int.Parse(textBox2.Text);
			redisClient.LogNet = base.LogNet;
			OperateResult operateResult = redisClient.ConnectServer();
			if (operateResult.IsSuccess)
			{
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				button5.Enabled = false;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
				textBox_code.Text = "RedisClient redis = new RedisClient( \"" + textBox1.Text + "\", " + textBox2.Text + ", \"" + textBox3.Text + "\" )";
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button5.Enabled = true;
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			redisClient.ConnectClose();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (status == 1)
			{
				redisClient.IpAddress = textBox1.Text;
				redisClient.Port = int.Parse(textBox2.Text);
				button1.Enabled = false;
				button2.Enabled = false;
				panel2.Enabled = true;
				status = 2;
				button5.Text = ((Program.Language == 1) ? "重新选择连接" : "Choose again");
			}
			else
			{
				status = 1;
				button1.Enabled = true;
				panel2.Enabled = false;
				button5.Text = ((Program.Language == 1) ? "启动短连接" : "Start a short connection");
			}
		}

		private void button_set_key_Click(object sender, EventArgs e)
		{
			DateTime now = DateTime.Now;
			int num = int.Parse(textBox6.Text);
			for (int i = 0; i < num; i++)
			{
				OperateResult operateResult = redisClient.WriteKey(textBox_write_key.Text, textBox4.Text);
				if (operateResult.IsSuccess)
				{
					textBox7.Text = (DateTime.Now - now).TotalMilliseconds.ToString("F2");
					if (i >= num - 1)
					{
						DemoUtils.ShowMessage("success");
					}
					textBox_code.Text = "OperateResult write = redis.WriteKey( \"" + textBox_write_key.Text + "\", \"" + textBox4.Text + "\" );";
					continue;
				}
				textBox7.Text = (DateTime.Now - now).TotalMilliseconds.ToString("F2");
				DemoUtils.ShowMessage((Program.Language == 1) ? "写入失败：" : ("Write Failed:" + operateResult.ToMessageShowString()));
				break;
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void button4_Click_1(object sender, EventArgs e)
		{
			DateTime now = DateTime.Now;
			int num = int.Parse(textBox9.Text);
			for (int i = 0; i < num; i++)
			{
				OperateResult<string> operateResult = redisClient.ReadKey(textBox_get_key.Text);
				if (operateResult.IsSuccess)
				{
					textBox_get_result.Text = operateResult.Content;
				}
				else
				{
					textBox_get_result.Text = ((Program.Language == 1) ? ("读取失败：" + operateResult.Message) : ("Read Failed:" + operateResult.Message));
				}
			}
			textBox8.Text = (DateTime.Now - now).TotalMilliseconds.ToString("F2");
			textBox_code.Text = "OperateResult<string> read = redis.ReadKey( \"" + textBox_get_key.Text + "\");";
		}

		private void button_execute_commands_Click(object sender, EventArgs e)
		{
			DateTime now = DateTime.Now;
			int num = int.Parse(textBox9.Text);
			for (int i = 0; i < num; i++)
			{
				OperateResult<string> operateResult = redisClient.ReadCustomer(textBox_redis_cmd.Text);
				if (operateResult.IsSuccess)
				{
					textBox_get_result.Text = operateResult.Content;
				}
				else
				{
					textBox_get_result.Text = ((Program.Language == 1) ? ("读取失败：" + operateResult.Message) : ("Read Failed:" + operateResult.Message));
				}
			}
			textBox8.Text = (DateTime.Now - now).TotalMilliseconds.ToString("F2");
			textBox_code.Text = "OperateResult<string> read = redis.ReadCustomer( \"" + textBox_redis_cmd.Text + "\" );";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = redisClient.ListLeftPush("B", random.Next(1000).ToString());
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("成功");
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
			}
		}

		private void button10_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = redisClient.ListTrim("B", 0L, 2L);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("成功");
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			OperateResult<int> listLength = redisClient.GetListLength(textBox_write_key.Text);
			if (listLength.IsSuccess)
			{
				DemoUtils.ShowMessage("成功：" + listLength.Content);
			}
			else
			{
				DemoUtils.ShowMessage(listLength.Message);
			}
			textBox_code.Text = "OperateResult<int> result = redis.GetListLength( \"" + textBox_write_key.Text + "\" );";
		}

		private void button12_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = redisClient.Publish(textBox_write_key.Text, textBox4.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("success");
			}
			else
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "写入失败：" : ("Write Failed:" + operateResult.ToMessageShowString()));
			}
			textBox_code.Text = "OperateResult write = redis.Publish( \"" + textBox_write_key.Text + "\", \"" + textBox4.Text + "\" );";
		}

		private void button_redis_EXISTS_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = redisClient.ExistsKey(textBox_get_key.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage((operateResult.Content == 1) ? "Exists!" : "None");
			}
			else
			{
				DemoUtils.ShowMessage("request failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<int> exist = redis.ExistsKey( \"" + textBox_get_key.Text + "\" );";
		}

		private void button_redis_PERSIST_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = redisClient.PersistKey(textBox_get_key.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage((operateResult.Content == 1) ? "Persist success!" : "Persist failed");
			}
			else
			{
				DemoUtils.ShowMessage("request failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<int> persist = redis.PersistKey( \"" + textBox_get_key.Text + "\" );";
		}

		private void button_redis_INCR_Click(object sender, EventArgs e)
		{
			OperateResult<long> operateResult = redisClient.IncrementKey(textBox_get_key.Text);
			if (operateResult.IsSuccess)
			{
				textBox_get_result.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("request failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<long> incr = redis.IncrementKey( \"" + textBox_get_key.Text + "\" );";
		}

		private void button_redis_DECR_Click(object sender, EventArgs e)
		{
			OperateResult<long> operateResult = redisClient.DecrementKey(textBox_get_key.Text);
			if (operateResult.IsSuccess)
			{
				textBox_get_result.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("request failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<long> decr = redis.DecrementKey( \"" + textBox_get_key.Text + "\" );";
		}

		private void button_redis_STRLEN_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = redisClient.ReadKeyLength(textBox_get_key.Text);
			if (operateResult.IsSuccess)
			{
				textBox_get_result.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("request failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<int> strLen = redis.ReadKeyLength( \"" + textBox_get_key.Text + "\" );";
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
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.button_redis_STRLEN = new System.Windows.Forms.Button();
			this.button_redis_DECR = new System.Windows.Forms.Button();
			this.button_redis_INCR = new System.Windows.Forms.Button();
			this.textBox_redis_cmd = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button_redis_PERSIST = new System.Windows.Forms.Button();
			this.button_redis_EXISTS = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button_redis_execute_cmd = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button_get_key = new System.Windows.Forms.Button();
			this.textBox_get_result = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox_get_key = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.button_set_key = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_write_key = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button6);
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 67);
			this.panel1.TabIndex = 7;
			this.button6.Location = new System.Drawing.Point(820, 32);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(168, 28);
			this.button6.TabIndex = 9;
			this.button6.Text = "压力测试";
			this.button6.UseVisualStyleBackColor = true;
			this.button5.Location = new System.Drawing.Point(820, 2);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(168, 28);
			this.button5.TabIndex = 8;
			this.button5.Text = "启动短连接";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox3.Location = new System.Drawing.Point(81, 34);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(384, 23);
			this.textBox3.TabIndex = 7;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 37);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(36, 17);
			this.label6.TabIndex = 6;
			this.label6.Text = "pwd:";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(653, 2);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "disconnect";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(546, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "connect";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(418, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(91, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "6379";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(364, 8);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(36, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "port:";
			this.textBox1.Location = new System.Drawing.Point(81, 5);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(257, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(55, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip addr:";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.button_redis_STRLEN);
			this.panel2.Controls.Add(this.button_redis_DECR);
			this.panel2.Controls.Add(this.button_redis_INCR);
			this.panel2.Controls.Add(this.textBox_redis_cmd);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Controls.Add(this.button_redis_PERSIST);
			this.panel2.Controls.Add(this.button_redis_EXISTS);
			this.panel2.Controls.Add(this.button12);
			this.panel2.Controls.Add(this.button11);
			this.panel2.Controls.Add(this.button10);
			this.panel2.Controls.Add(this.button9);
			this.panel2.Controls.Add(this.button_redis_execute_cmd);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.textBox9);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button_get_key);
			this.panel2.Controls.Add(this.textBox_get_result);
			this.panel2.Controls.Add(this.label13);
			this.panel2.Controls.Add(this.textBox_get_key);
			this.panel2.Controls.Add(this.label14);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.button_set_key);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.textBox_write_key);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(3, 106);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 537);
			this.panel2.TabIndex = 13;
			this.button_redis_STRLEN.Location = new System.Drawing.Point(556, 198);
			this.button_redis_STRLEN.Name = "button_redis_STRLEN";
			this.button_redis_STRLEN.Size = new System.Drawing.Size(66, 28);
			this.button_redis_STRLEN.TabIndex = 39;
			this.button_redis_STRLEN.Text = "STRLEN";
			this.button_redis_STRLEN.UseVisualStyleBackColor = true;
			this.button_redis_STRLEN.Click += new System.EventHandler(button_redis_STRLEN_Click);
			this.button_redis_DECR.Location = new System.Drawing.Point(484, 198);
			this.button_redis_DECR.Name = "button_redis_DECR";
			this.button_redis_DECR.Size = new System.Drawing.Size(66, 28);
			this.button_redis_DECR.TabIndex = 38;
			this.button_redis_DECR.Text = "DECR";
			this.button_redis_DECR.UseVisualStyleBackColor = true;
			this.button_redis_DECR.Click += new System.EventHandler(button_redis_DECR_Click);
			this.button_redis_INCR.Location = new System.Drawing.Point(418, 198);
			this.button_redis_INCR.Name = "button_redis_INCR";
			this.button_redis_INCR.Size = new System.Drawing.Size(60, 28);
			this.button_redis_INCR.TabIndex = 37;
			this.button_redis_INCR.Text = "INCR";
			this.button_redis_INCR.UseVisualStyleBackColor = true;
			this.button_redis_INCR.Click += new System.EventHandler(button_redis_INCR_Click);
			this.textBox_redis_cmd.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_redis_cmd.Location = new System.Drawing.Point(62, 456);
			this.textBox_redis_cmd.Name = "textBox_redis_cmd";
			this.textBox_redis_cmd.Size = new System.Drawing.Size(365, 23);
			this.textBox_redis_cmd.TabIndex = 36;
			this.textBox_redis_cmd.Text = "GET A";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(7, 460);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 35;
			this.label2.Text = "Cmds:";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Location = new System.Drawing.Point(10, 189);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(978, 5);
			this.groupBox1.TabIndex = 34;
			this.groupBox1.TabStop = false;
			this.button_redis_PERSIST.Location = new System.Drawing.Point(725, 198);
			this.button_redis_PERSIST.Name = "button_redis_PERSIST";
			this.button_redis_PERSIST.Size = new System.Drawing.Size(91, 28);
			this.button_redis_PERSIST.TabIndex = 33;
			this.button_redis_PERSIST.Text = "PERSIST";
			this.button_redis_PERSIST.UseVisualStyleBackColor = true;
			this.button_redis_PERSIST.Click += new System.EventHandler(button_redis_PERSIST_Click);
			this.button_redis_EXISTS.Location = new System.Drawing.Point(628, 198);
			this.button_redis_EXISTS.Name = "button_redis_EXISTS";
			this.button_redis_EXISTS.Size = new System.Drawing.Size(91, 28);
			this.button_redis_EXISTS.TabIndex = 32;
			this.button_redis_EXISTS.Text = "EXISTS";
			this.button_redis_EXISTS.UseVisualStyleBackColor = true;
			this.button_redis_EXISTS.Click += new System.EventHandler(button_redis_EXISTS_Click);
			this.button12.Location = new System.Drawing.Point(200, 161);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(91, 28);
			this.button12.TabIndex = 31;
			this.button12.Text = "publish key";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button11.Location = new System.Drawing.Point(716, 161);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(91, 28);
			this.button11.TabIndex = 30;
			this.button11.Text = "list length";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(619, 161);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(91, 28);
			this.button10.TabIndex = 29;
			this.button10.Text = "list shrink";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Location = new System.Drawing.Point(522, 161);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(91, 28);
			this.button9.TabIndex = 28;
			this.button9.Text = "list add";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button_redis_execute_cmd.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button_redis_execute_cmd.Location = new System.Drawing.Point(433, 454);
			this.button_redis_execute_cmd.Name = "button_redis_execute_cmd";
			this.button_redis_execute_cmd.Size = new System.Drawing.Size(110, 28);
			this.button_redis_execute_cmd.TabIndex = 27;
			this.button_redis_execute_cmd.Text = "execute cmd";
			this.button_redis_execute_cmd.UseVisualStyleBackColor = true;
			this.button_redis_execute_cmd.Click += new System.EventHandler(button_execute_commands_Click);
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(896, 454);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(91, 28);
			this.button7.TabIndex = 26;
			this.button7.Text = "clear";
			this.button7.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(768, 457);
			this.textBox8.Name = "textBox8";
			this.textBox8.ReadOnly = true;
			this.textBox8.Size = new System.Drawing.Size(88, 23);
			this.textBox8.TabIndex = 25;
			this.textBox8.Text = "0";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(718, 461);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(47, 17);
			this.label8.TabIndex = 24;
			this.label8.Text = "spend:";
			this.textBox9.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox9.Location = new System.Drawing.Point(632, 457);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(80, 23);
			this.textBox9.TabIndex = 23;
			this.textBox9.Text = "1";
			this.label12.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(582, 460);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(51, 17);
			this.label12.TabIndex = 22;
			this.label12.Text = "times：";
			this.button_get_key.Location = new System.Drawing.Point(321, 198);
			this.button_get_key.Name = "button_get_key";
			this.button_get_key.Size = new System.Drawing.Size(91, 28);
			this.button_get_key.TabIndex = 21;
			this.button_get_key.Text = "Get Key";
			this.button_get_key.UseVisualStyleBackColor = true;
			this.button_get_key.Click += new System.EventHandler(button4_Click_1);
			this.textBox_get_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_get_result.Location = new System.Drawing.Point(62, 232);
			this.textBox_get_result.Multiline = true;
			this.textBox_get_result.Name = "textBox_get_result";
			this.textBox_get_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_get_result.Size = new System.Drawing.Size(930, 219);
			this.textBox_get_result.TabIndex = 18;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(8, 234);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(41, 17);
			this.label13.TabIndex = 20;
			this.label13.Text = "text：";
			this.textBox_get_key.Location = new System.Drawing.Point(62, 200);
			this.textBox_get_key.Name = "textBox_get_key";
			this.textBox_get_key.Size = new System.Drawing.Size(253, 23);
			this.textBox_get_key.TabIndex = 19;
			this.textBox_get_key.Text = "A";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(8, 204);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(40, 17);
			this.label14.TabIndex = 17;
			this.label14.Text = "key：";
			this.textBox7.Location = new System.Drawing.Point(435, 164);
			this.textBox7.Name = "textBox7";
			this.textBox7.ReadOnly = true;
			this.textBox7.Size = new System.Drawing.Size(66, 23);
			this.textBox7.TabIndex = 16;
			this.textBox7.Text = "0";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(385, 167);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 15;
			this.label11.Text = "耗时：";
			this.textBox6.Location = new System.Drawing.Point(111, 164);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(76, 23);
			this.textBox6.TabIndex = 14;
			this.textBox6.Text = "1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(64, 167);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(51, 17);
			this.label10.TabIndex = 13;
			this.label10.Text = "times：";
			this.button_set_key.Location = new System.Drawing.Point(328, 4);
			this.button_set_key.Name = "button_set_key";
			this.button_set_key.Size = new System.Drawing.Size(91, 28);
			this.button_set_key.TabIndex = 12;
			this.button_set_key.Text = "Set key";
			this.button_set_key.UseVisualStyleBackColor = true;
			this.button_set_key.Click += new System.EventHandler(button_set_key_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 36);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(930, 123);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(8, 39);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(41, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "text：";
			this.textBox_write_key.Location = new System.Drawing.Point(62, 7);
			this.textBox_write_key.Name = "textBox_write_key";
			this.textBox_write_key.Size = new System.Drawing.Size(260, 23);
			this.textBox_write_key.TabIndex = 9;
			this.textBox_write_key.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(40, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "key：";
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
			this.userControlHead1.TabIndex = 14;
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(8, 488);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(42, 17);
			this.label4.TabIndex = 40;
			this.label4.Text = "Code:";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(62, 484);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(930, 47);
			this.textBox_code.TabIndex = 41;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormRedisClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Redis Client Test";
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
