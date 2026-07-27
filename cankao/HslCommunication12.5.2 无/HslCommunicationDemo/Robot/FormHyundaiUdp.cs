using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.LogNet;
using HslCommunication.Robot.Hyundai;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.Robot
{
	public class FormHyundaiUdp : HslFormContent
	{
		private HyundaiUdpNet hyundai;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private GroupBox groupBox1;

		private TextBox textBox_rz;

		private Label label10;

		private TextBox textBox_ry;

		private Label label11;

		private TextBox textBox_rx;

		private Label label12;

		private TextBox textBox_z;

		private Label label9;

		private TextBox textBox_y;

		private Label label8;

		private TextBox textBox_x;

		private Label label7;

		private TextBox textBox6;

		private Label label6;

		private TextBox textBox5;

		private Label label5;

		private TextBox textBox4;

		private Label label4;

		private TextBox textBox3;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private GroupBox groupBox2;

		private TextBox textBox19;

		private GroupBox groupBox3;

		private Button button13;

		private Button button14;

		private TextBox textBox12;

		private Button button11;

		private Button button12;

		private TextBox textBox11;

		private Button button9;

		private Button button10;

		private TextBox textBox10;

		private Button button7;

		private Button button8;

		private TextBox textBox9;

		private Button button5;

		private Button button6;

		private TextBox textBox8;

		private Button button4;

		private Label label13;

		private Label label14;

		private Label label15;

		private Label label16;

		private Label label17;

		private Label label18;

		private Button button3;

		private TextBox textBox7;

		private GroupBox groupBox4;

		private Button button15;

		private TextBox textBox13;

		private TextBox textBox14;

		private TextBox textBox15;

		private TextBox textBox16;

		private TextBox textBox17;

		private Label label19;

		private Label label20;

		private Label label21;

		private Label label22;

		private Label label23;

		private Label label24;

		private TextBox textBox18;

		private Label label27;

		private Label label28;

		private Label label26;

		private Label label25;

		private Label label29;

		public FormHyundaiUdp()
		{
			InitializeComponent();
		}

		private void FormHyundaiUdp_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label3.Text = "Port";
				button1.Text = "Start";
				button2.Text = "Stop";
				groupBox1.Text = "Robot Real Data";
				groupBox3.Text = "Increment Write";
				groupBox4.Text = "Increment Write";
				button15.Text = "Write";
				groupBox2.Text = "Log";
			}
			groupBox1.Enabled = false;
			groupBox2.Enabled = false;
			groupBox3.Enabled = false;
			groupBox4.Enabled = false;
			button2.Enabled = false;
		}

		private void FormHyundaiUdp_FormClosing(object sender, FormClosingEventArgs e)
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
				DemoUtils.ShowMessage("Port input wrong!");
				return;
			}
			try
			{
				hyundai = new HyundaiUdpNet();
				hyundai.OnHyundaiMessageReceive += Hyundai_OnHyundaiMessageReceive;
				hyundai.LogNet = new LogNetSingle("");
				hyundai.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				hyundai.ServerStart(result, false);
				DemoUtils.ShowMessage("Start Success");
				groupBox1.Enabled = true;
				groupBox2.Enabled = true;
				groupBox3.Enabled = true;
				groupBox4.Enabled = true;
				button1.Enabled = false;
				button2.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Start Failed: " + ex.Message);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox19.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			});
		}

		private void Hyundai_OnHyundaiMessageReceive(HyundaiData data)
		{
			Invoke((Action)delegate
			{
				textBox1.Text = data.Command.ToString();
				textBox3.Text = data.CharDummy;
				textBox4.Text = data.State.ToString();
				textBox5.Text = data.Count.ToString();
				textBox6.Text = data.IntDummy.ToString();
				textBox_x.Text = data.Data[0].ToString();
				textBox_y.Text = data.Data[1].ToString();
				textBox_z.Text = data.Data[2].ToString();
				textBox_rx.Text = data.Data[3].ToString();
				textBox_ry.Text = data.Data[4].ToString();
				textBox_rz.Text = data.Data[5].ToString();
			});
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				hyundai.ServerClose();
				button1.Enabled = true;
				button2.Enabled = false;
				groupBox1.Enabled = false;
				groupBox2.Enabled = false;
				groupBox3.Enabled = false;
				groupBox4.Enabled = false;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Close Failed:" + ex.Message);
			}
		}

		private void RenderMoveResult(OperateResult operate, string op)
		{
			if (operate.IsSuccess)
			{
				label29.Text = "Op[" + op + "] Result: True   Time:" + DateTime.Now.ToString("HH:mm:ss.fff");
			}
			else
			{
				DemoUtils.ShowMessage("Op[" + op + "] failed : " + operate.Message);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveX(double.Parse(textBox7.Text)), "X");
		}

		private void button4_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveX(0.0 - double.Parse(textBox7.Text)), "-X");
		}

		private void button6_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveY(double.Parse(textBox8.Text)), "Y");
		}

		private void button5_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveY(0.0 - double.Parse(textBox8.Text)), "-Y");
		}

		private void button8_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveZ(double.Parse(textBox9.Text)), "Z");
		}

		private void button7_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.MoveZ(0.0 - double.Parse(textBox9.Text)), "-Z");
		}

		private void button10_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateX(double.Parse(textBox10.Text)), "Rx");
		}

		private void button9_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateX(0.0 - double.Parse(textBox10.Text)), "Rx");
		}

		private void button12_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateY(double.Parse(textBox11.Text)), "Ry");
		}

		private void button11_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateY(0.0 - double.Parse(textBox11.Text)), "Ry");
		}

		private void button14_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateZ(double.Parse(textBox12.Text)), "Rz");
		}

		private void button13_Click(object sender, EventArgs e)
		{
			RenderMoveResult(hyundai.RotateZ(0.0 - double.Parse(textBox12.Text)), "Rz");
		}

		private void button15_Click(object sender, EventArgs e)
		{
			DemoUtils.WriteResultRender(() => hyundai.WriteIncrementPos(double.Parse(textBox18.Text), double.Parse(textBox17.Text), double.Parse(textBox16.Text), double.Parse(textBox15.Text), double.Parse(textBox14.Text), double.Parse(textBox13.Text)), "X,Y,Z,Rx,Ry,Rz");
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
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.textBox_rz = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_ry = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_rx = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_z = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_y = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_x = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.textBox19 = new System.Windows.Forms.TextBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.label29 = new System.Windows.Forms.Label();
			this.button13 = new System.Windows.Forms.Button();
			this.button14 = new System.Windows.Forms.Button();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.button11 = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.button9 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.button7 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.button5 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.button4 = new System.Windows.Forms.Button();
			this.label13 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label17 = new System.Windows.Forms.Label();
			this.label18 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.label27 = new System.Windows.Forms.Label();
			this.label28 = new System.Windows.Forms.Label();
			this.label26 = new System.Windows.Forms.Label();
			this.label25 = new System.Windows.Forms.Label();
			this.button15 = new System.Windows.Forms.Button();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label19 = new System.Windows.Forms.Label();
			this.label20 = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.label22 = new System.Windows.Forms.Label();
			this.label23 = new System.Windows.Forms.Label();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox18 = new System.Windows.Forms.TextBox();
			this.panel1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox4.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Hyundai";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 3;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(998, 34);
			this.panel1.TabIndex = 4;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(315, 0);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "停止服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(218, 0);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(60, 3);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(141, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "8000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.groupBox1.Controls.Add(this.textBox_rz);
			this.groupBox1.Controls.Add(this.label10);
			this.groupBox1.Controls.Add(this.textBox_ry);
			this.groupBox1.Controls.Add(this.label11);
			this.groupBox1.Controls.Add(this.textBox_rx);
			this.groupBox1.Controls.Add(this.label12);
			this.groupBox1.Controls.Add(this.textBox_z);
			this.groupBox1.Controls.Add(this.label9);
			this.groupBox1.Controls.Add(this.textBox_y);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textBox_x);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox6);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.textBox5);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.textBox1);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Location = new System.Drawing.Point(2, 75);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(413, 228);
			this.groupBox1.TabIndex = 5;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "机器人实时姿态";
			this.textBox_rz.Location = new System.Drawing.Point(265, 169);
			this.textBox_rz.Name = "textBox_rz";
			this.textBox_rz.ReadOnly = true;
			this.textBox_rz.Size = new System.Drawing.Size(133, 23);
			this.textBox_rz.TabIndex = 21;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(235, 172);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(25, 17);
			this.label10.TabIndex = 20;
			this.label10.Text = "Rz:";
			this.textBox_ry.Location = new System.Drawing.Point(265, 141);
			this.textBox_ry.Name = "textBox_ry";
			this.textBox_ry.ReadOnly = true;
			this.textBox_ry.Size = new System.Drawing.Size(133, 23);
			this.textBox_ry.TabIndex = 19;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(235, 144);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(25, 17);
			this.label11.TabIndex = 18;
			this.label11.Text = "Ry:";
			this.textBox_rx.Location = new System.Drawing.Point(265, 112);
			this.textBox_rx.Name = "textBox_rx";
			this.textBox_rx.ReadOnly = true;
			this.textBox_rx.Size = new System.Drawing.Size(133, 23);
			this.textBox_rx.TabIndex = 17;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(235, 115);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(25, 17);
			this.label12.TabIndex = 16;
			this.label12.Text = "Rx:";
			this.textBox_z.Location = new System.Drawing.Point(264, 83);
			this.textBox_z.Name = "textBox_z";
			this.textBox_z.ReadOnly = true;
			this.textBox_z.Size = new System.Drawing.Size(133, 23);
			this.textBox_z.TabIndex = 15;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(234, 86);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(18, 17);
			this.label9.TabIndex = 14;
			this.label9.Text = "Z:";
			this.textBox_y.Location = new System.Drawing.Point(264, 55);
			this.textBox_y.Name = "textBox_y";
			this.textBox_y.ReadOnly = true;
			this.textBox_y.Size = new System.Drawing.Size(133, 23);
			this.textBox_y.TabIndex = 13;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(234, 58);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(18, 17);
			this.label8.TabIndex = 12;
			this.label8.Text = "Y:";
			this.textBox_x.Location = new System.Drawing.Point(264, 26);
			this.textBox_x.Name = "textBox_x";
			this.textBox_x.ReadOnly = true;
			this.textBox_x.Size = new System.Drawing.Size(133, 23);
			this.textBox_x.TabIndex = 11;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(234, 29);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(19, 17);
			this.label7.TabIndex = 10;
			this.label7.Text = "X:";
			this.textBox6.Location = new System.Drawing.Point(99, 141);
			this.textBox6.Name = "textBox6";
			this.textBox6.ReadOnly = true;
			this.textBox6.Size = new System.Drawing.Size(111, 23);
			this.textBox6.TabIndex = 9;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(14, 141);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(70, 17);
			this.label6.TabIndex = 8;
			this.label6.Text = "IntDummy:";
			this.textBox5.Location = new System.Drawing.Point(99, 112);
			this.textBox5.Name = "textBox5";
			this.textBox5.ReadOnly = true;
			this.textBox5.Size = new System.Drawing.Size(111, 23);
			this.textBox5.TabIndex = 7;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(14, 112);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(45, 17);
			this.label5.TabIndex = 6;
			this.label5.Text = "Count:";
			this.textBox4.Location = new System.Drawing.Point(99, 83);
			this.textBox4.Name = "textBox4";
			this.textBox4.ReadOnly = true;
			this.textBox4.Size = new System.Drawing.Size(111, 23);
			this.textBox4.TabIndex = 5;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(14, 86);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(40, 17);
			this.label4.TabIndex = 4;
			this.label4.Text = "State:";
			this.textBox3.Location = new System.Drawing.Point(99, 54);
			this.textBox3.Name = "textBox3";
			this.textBox3.ReadOnly = true;
			this.textBox3.Size = new System.Drawing.Size(111, 23);
			this.textBox3.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(14, 57);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(82, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "CharDummy:";
			this.textBox1.Location = new System.Drawing.Point(99, 26);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(111, 23);
			this.textBox1.TabIndex = 1;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(14, 29);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(71, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Command:";
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.textBox19);
			this.groupBox2.Location = new System.Drawing.Point(2, 303);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(998, 339);
			this.groupBox2.TabIndex = 6;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "日志记录";
			this.textBox19.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox19.Location = new System.Drawing.Point(6, 22);
			this.textBox19.Multiline = true;
			this.textBox19.Name = "textBox19";
			this.textBox19.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox19.Size = new System.Drawing.Size(986, 311);
			this.textBox19.TabIndex = 0;
			this.groupBox3.Controls.Add(this.label29);
			this.groupBox3.Controls.Add(this.button13);
			this.groupBox3.Controls.Add(this.button14);
			this.groupBox3.Controls.Add(this.textBox12);
			this.groupBox3.Controls.Add(this.button11);
			this.groupBox3.Controls.Add(this.button12);
			this.groupBox3.Controls.Add(this.textBox11);
			this.groupBox3.Controls.Add(this.button9);
			this.groupBox3.Controls.Add(this.button10);
			this.groupBox3.Controls.Add(this.textBox10);
			this.groupBox3.Controls.Add(this.button7);
			this.groupBox3.Controls.Add(this.button8);
			this.groupBox3.Controls.Add(this.textBox9);
			this.groupBox3.Controls.Add(this.button5);
			this.groupBox3.Controls.Add(this.button6);
			this.groupBox3.Controls.Add(this.textBox8);
			this.groupBox3.Controls.Add(this.button4);
			this.groupBox3.Controls.Add(this.label13);
			this.groupBox3.Controls.Add(this.label14);
			this.groupBox3.Controls.Add(this.label15);
			this.groupBox3.Controls.Add(this.label16);
			this.groupBox3.Controls.Add(this.label17);
			this.groupBox3.Controls.Add(this.label18);
			this.groupBox3.Controls.Add(this.button3);
			this.groupBox3.Controls.Add(this.textBox7);
			this.groupBox3.Location = new System.Drawing.Point(421, 75);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(271, 228);
			this.groupBox3.TabIndex = 7;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "增量数据写入";
			this.label29.AutoSize = true;
			this.label29.Location = new System.Drawing.Point(6, 203);
			this.label29.Name = "label29";
			this.label29.Size = new System.Drawing.Size(72, 17);
			this.label29.TabIndex = 70;
			this.label29.Text = "Op Result: ";
			this.button13.Location = new System.Drawing.Point(204, 164);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(51, 25);
			this.button13.TabIndex = 69;
			this.button13.Text = "-";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button14.Location = new System.Drawing.Point(147, 164);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(51, 25);
			this.button14.TabIndex = 68;
			this.button14.Text = "+";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.textBox12.Location = new System.Drawing.Point(45, 165);
			this.textBox12.Name = "textBox12";
			this.textBox12.Size = new System.Drawing.Size(96, 23);
			this.textBox12.TabIndex = 67;
			this.textBox12.Text = "0.5";
			this.button11.Location = new System.Drawing.Point(204, 136);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(51, 25);
			this.button11.TabIndex = 66;
			this.button11.Text = "-";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button12.Location = new System.Drawing.Point(147, 136);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(51, 25);
			this.button12.TabIndex = 65;
			this.button12.Text = "+";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.textBox11.Location = new System.Drawing.Point(45, 137);
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(96, 23);
			this.textBox11.TabIndex = 64;
			this.textBox11.Text = "0.5";
			this.button9.Location = new System.Drawing.Point(204, 107);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(51, 25);
			this.button9.TabIndex = 63;
			this.button9.Text = "-";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button10.Location = new System.Drawing.Point(147, 107);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(51, 25);
			this.button10.TabIndex = 62;
			this.button10.Text = "+";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.textBox10.Location = new System.Drawing.Point(45, 108);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(96, 23);
			this.textBox10.TabIndex = 61;
			this.textBox10.Text = "0.5";
			this.button7.Location = new System.Drawing.Point(204, 80);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(51, 25);
			this.button7.TabIndex = 60;
			this.button7.Text = "-";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button8.Location = new System.Drawing.Point(147, 80);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(51, 25);
			this.button8.TabIndex = 59;
			this.button8.Text = "+";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.textBox9.Location = new System.Drawing.Point(45, 81);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(96, 23);
			this.textBox9.TabIndex = 58;
			this.textBox9.Text = "10";
			this.button5.Location = new System.Drawing.Point(204, 51);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(51, 25);
			this.button5.TabIndex = 57;
			this.button5.Text = "-";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button6.Location = new System.Drawing.Point(147, 51);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(51, 25);
			this.button6.TabIndex = 56;
			this.button6.Text = "+";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.textBox8.Location = new System.Drawing.Point(45, 52);
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(96, 23);
			this.textBox8.TabIndex = 55;
			this.textBox8.Text = "10";
			this.button4.Location = new System.Drawing.Point(204, 20);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(51, 25);
			this.button4.TabIndex = 54;
			this.button4.Text = "-";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(6, 168);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(25, 17);
			this.label13.TabIndex = 53;
			this.label13.Text = "Rz:";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(6, 140);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(25, 17);
			this.label14.TabIndex = 52;
			this.label14.Text = "Ry:";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(6, 111);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(25, 17);
			this.label15.TabIndex = 51;
			this.label15.Text = "Rx:";
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(5, 82);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(18, 17);
			this.label16.TabIndex = 50;
			this.label16.Text = "Z:";
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(5, 54);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(18, 17);
			this.label17.TabIndex = 49;
			this.label17.Text = "Y:";
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(5, 25);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(19, 17);
			this.label18.TabIndex = 48;
			this.label18.Text = "X:";
			this.button3.Location = new System.Drawing.Point(147, 20);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(51, 25);
			this.button3.TabIndex = 47;
			this.button3.Text = "+";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox7.Location = new System.Drawing.Point(45, 21);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(96, 23);
			this.textBox7.TabIndex = 46;
			this.textBox7.Text = "10";
			this.groupBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox4.Controls.Add(this.label27);
			this.groupBox4.Controls.Add(this.label28);
			this.groupBox4.Controls.Add(this.label26);
			this.groupBox4.Controls.Add(this.label25);
			this.groupBox4.Controls.Add(this.button15);
			this.groupBox4.Controls.Add(this.textBox13);
			this.groupBox4.Controls.Add(this.textBox14);
			this.groupBox4.Controls.Add(this.textBox15);
			this.groupBox4.Controls.Add(this.textBox16);
			this.groupBox4.Controls.Add(this.textBox17);
			this.groupBox4.Controls.Add(this.label19);
			this.groupBox4.Controls.Add(this.label20);
			this.groupBox4.Controls.Add(this.label21);
			this.groupBox4.Controls.Add(this.label22);
			this.groupBox4.Controls.Add(this.label23);
			this.groupBox4.Controls.Add(this.label24);
			this.groupBox4.Controls.Add(this.textBox18);
			this.groupBox4.Location = new System.Drawing.Point(698, 75);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(302, 228);
			this.groupBox4.TabIndex = 8;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "增量数据写入";
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(146, 103);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(88, 17);
			this.label27.TabIndex = 72;
			this.label27.Text = "X,Y,Z 单位角度";
			this.label28.AutoSize = true;
			this.label28.Location = new System.Drawing.Point(146, 86);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(136, 17);
			this.label28.TabIndex = 71;
			this.label28.Text = "RX,RY,RZ Unit is angle";
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(146, 41);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(88, 17);
			this.label26.TabIndex = 70;
			this.label26.Text = "X,Y,Z 单位毫米";
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(146, 24);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(102, 17);
			this.label25.TabIndex = 69;
			this.label25.Text = "X,Y,Z Unit is mm";
			this.button15.Location = new System.Drawing.Point(162, 164);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(95, 25);
			this.button15.TabIndex = 68;
			this.button15.Text = "写入";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.textBox13.Location = new System.Drawing.Point(45, 165);
			this.textBox13.Name = "textBox13";
			this.textBox13.Size = new System.Drawing.Size(96, 23);
			this.textBox13.TabIndex = 67;
			this.textBox13.Text = "0";
			this.textBox14.Location = new System.Drawing.Point(45, 137);
			this.textBox14.Name = "textBox14";
			this.textBox14.Size = new System.Drawing.Size(96, 23);
			this.textBox14.TabIndex = 64;
			this.textBox14.Text = "0";
			this.textBox15.Location = new System.Drawing.Point(45, 108);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(96, 23);
			this.textBox15.TabIndex = 61;
			this.textBox15.Text = "0";
			this.textBox16.Location = new System.Drawing.Point(45, 81);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(96, 23);
			this.textBox16.TabIndex = 58;
			this.textBox16.Text = "0";
			this.textBox17.Location = new System.Drawing.Point(45, 52);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(96, 23);
			this.textBox17.TabIndex = 55;
			this.textBox17.Text = "0";
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(6, 168);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(25, 17);
			this.label19.TabIndex = 53;
			this.label19.Text = "Rz:";
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(6, 140);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(25, 17);
			this.label20.TabIndex = 52;
			this.label20.Text = "Ry:";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(6, 111);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(25, 17);
			this.label21.TabIndex = 51;
			this.label21.Text = "Rx:";
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(5, 82);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(18, 17);
			this.label22.TabIndex = 50;
			this.label22.Text = "Z:";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(5, 54);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(18, 17);
			this.label23.TabIndex = 49;
			this.label23.Text = "Y:";
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(5, 25);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(19, 17);
			this.label24.TabIndex = 48;
			this.label24.Text = "X:";
			this.textBox18.Location = new System.Drawing.Point(45, 21);
			this.textBox18.Name = "textBox18";
			this.textBox18.Size = new System.Drawing.Size(96, 23);
			this.textBox18.TabIndex = 46;
			this.textBox18.Text = "0";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.groupBox4);
			base.Controls.Add(this.groupBox3);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormHyundaiUdp";
			this.Text = "FormHyundaiUdp";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormHyundaiUdp_FormClosing);
			base.Load += new System.EventHandler(FormHyundaiUdp_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
