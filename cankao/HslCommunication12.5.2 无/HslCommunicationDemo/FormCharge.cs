using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunicationDemo.Properties;
using HslControls;

namespace HslCommunicationDemo
{
	public class FormCharge : HslFormContent
	{
		private Timer timer1s;

		private IContainer components = null;

		private TableLayoutPanel tableLayoutPanel1;

		private Panel panel5;

		private Label label8;

		private Label label9;

		private Panel panel3;

		private Label label4;

		private Panel panel1;

		private Label label3;

		private Label label2;

		private Label label1;

		private HslPanel hslPanel3;

		private HslPanel hslPanel2;

		private HslPanel hslPanel1;

		private Label label7;

		private TableLayoutPanel tableLayoutPanel2;

		private Panel panel7;

		private Panel panel6;

		private Panel panel4;

		private Panel panel2;

		private TableLayoutPanel tableLayoutPanel3;

		private Panel panel9;

		private Panel panel8;

		private Label label5;

		private PictureBox pictureBox2;

		private PictureBox pictureBox1;

		private Label label16;

		private Label label12;

		private Label label15;

		private Label label10;

		private Label label14;

		private Label label6;

		private PictureBox pictureBox3;

		private Label label17;

		private Label label11;

		public FormCharge()
		{
			InitializeComponent();
		}

		private void FormCharge_Load(object sender, EventArgs e)
		{
			if (!Program.IsActive)
			{
				timer1s = new Timer();
				timer1s.Tick += Timer1s_Tick;
				timer1s.Interval = 500;
				timer1s.Start();
			}
			if (Program.Language == 2)
			{
				Text = "Charge";
				label17.Text = "The copyright of this component belongs to Hangzhou Hu Gong Iot Technology Co., Ltd., commercial use please contact authorization, thank you for China's industrial software industry, automation industry support. Any misappropriated software, cracking software, commercial use without formal contract authorization is considered infringement, will be held accountable according to law, commercial use includes direct integrated sales and internal use of economic benefits.";
				label7.Text = "One-time payment, permanent authorization\r\nC#+Java+Python all code\r\nSupport subsequent updates\r\nSupport windows+linux\r\nConsulting QQ:200962190\r\nEmail: hsl200909@163.com";
				label9.Text = "Enterprise commercial + source code";
				label8.Text = "Please contact for a quote";
				label3.Text = "Can only call C# dll\r\nContinuous operation for 20 years\r\nUp to 100 PLCs per project\r\nSoftware restart and re-time\r\nScan the QR code below to pay and add vip group\r\nOnly for testing environment and academic research";
				label1.Text = "Personal non-commercial";
				label2.Text = "39 $";
			}
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
		}

		private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
		{
		}

		private void label1_Click(object sender, EventArgs e)
		{
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
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.panel5 = new System.Windows.Forms.Panel();
			this.hslPanel3 = new HslControls.HslPanel();
			this.label7 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.panel3 = new System.Windows.Forms.Panel();
			this.hslPanel2 = new HslControls.HslPanel();
			this.label4 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.hslPanel1 = new HslControls.HslPanel();
			this.label1 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
			this.panel7 = new System.Windows.Forms.Panel();
			this.label16 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.panel6 = new System.Windows.Forms.Panel();
			this.label15 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.panel4 = new System.Windows.Forms.Panel();
			this.label14 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label11 = new System.Windows.Forms.Label();
			this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
			this.pictureBox3 = new System.Windows.Forms.PictureBox();
			this.panel9 = new System.Windows.Forms.Panel();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			this.panel8 = new System.Windows.Forms.Panel();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label17 = new System.Windows.Forms.Label();
			this.tableLayoutPanel1.SuspendLayout();
			this.panel5.SuspendLayout();
			this.hslPanel3.SuspendLayout();
			this.panel3.SuspendLayout();
			this.hslPanel2.SuspendLayout();
			this.panel1.SuspendLayout();
			this.hslPanel1.SuspendLayout();
			this.tableLayoutPanel2.SuspendLayout();
			this.panel7.SuspendLayout();
			this.panel6.SuspendLayout();
			this.panel4.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tableLayoutPanel3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox3).BeginInit();
			this.panel9.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
			this.panel8.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.tableLayoutPanel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tableLayoutPanel1.ColumnCount = 3;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.1688f));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.46495f));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.36625f));
			this.tableLayoutPanel1.Controls.Add(this.panel5, 2, 0);
			this.tableLayoutPanel1.Controls.Add(this.panel3, 1, 0);
			this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 0);
			this.tableLayoutPanel1.Location = new System.Drawing.Point(10, 69);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 1;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 300f));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(821, 257);
			this.tableLayoutPanel1.TabIndex = 1;
			this.tableLayoutPanel1.Paint += new System.Windows.Forms.PaintEventHandler(tableLayoutPanel1_Paint);
			this.panel5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel5.Controls.Add(this.hslPanel3);
			this.panel5.Location = new System.Drawing.Point(549, 3);
			this.panel5.Name = "panel5";
			this.panel5.Size = new System.Drawing.Size(269, 294);
			this.panel5.TabIndex = 2;
			this.hslPanel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslPanel3.CenterColor = System.Drawing.Color.Snow;
			this.hslPanel3.Controls.Add(this.label7);
			this.hslPanel3.Controls.Add(this.label9);
			this.hslPanel3.Controls.Add(this.label8);
			this.hslPanel3.EdgeColor = System.Drawing.Color.MistyRose;
			this.hslPanel3.Location = new System.Drawing.Point(4, 8);
			this.hslPanel3.Name = "hslPanel3";
			this.hslPanel3.Size = new System.Drawing.Size(262, 242);
			this.hslPanel3.TabIndex = 1;
			this.hslPanel3.Text = "hslPanel3";
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label7.BackColor = System.Drawing.Color.Transparent;
			this.label7.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label7.ForeColor = System.Drawing.Color.Red;
			this.label7.Location = new System.Drawing.Point(0, 82);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(262, 158);
			this.label7.TabIndex = 6;
			this.label7.Text = "一次付费，永久授权\r\nC#+Java+Python所有代码\r\n支持后续更新\r\n支持windows+linux\r\n咨询QQ:200962190\r\n咨询微信：xyz200962190\r\nEmail: hsltechnology@163.com";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label9.BackColor = System.Drawing.Color.Transparent;
			this.label9.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label9.ForeColor = System.Drawing.Color.Black;
			this.label9.Location = new System.Drawing.Point(0, 20);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(262, 36);
			this.label9.TabIndex = 0;
			this.label9.Text = "企业商用+源码";
			this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label8.BackColor = System.Drawing.Color.Transparent;
			this.label8.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label8.ForeColor = System.Drawing.Color.Blue;
			this.label8.Location = new System.Drawing.Point(3, 50);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(262, 36);
			this.label8.TabIndex = 1;
			this.label8.Text = "请联系报价";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.hslPanel2);
			this.panel3.Location = new System.Drawing.Point(275, 3);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(268, 294);
			this.panel3.TabIndex = 1;
			this.hslPanel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslPanel2.CenterColor = System.Drawing.Color.Azure;
			this.hslPanel2.Controls.Add(this.label4);
			this.hslPanel2.EdgeColor = System.Drawing.Color.LightCyan;
			this.hslPanel2.Location = new System.Drawing.Point(3, 8);
			this.hslPanel2.Name = "hslPanel2";
			this.hslPanel2.Size = new System.Drawing.Size(262, 242);
			this.hslPanel2.TabIndex = 1;
			this.hslPanel2.Text = "hslPanel2";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label4.BackColor = System.Drawing.Color.Transparent;
			this.label4.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label4.ForeColor = System.Drawing.Color.Red;
			this.label4.Location = new System.Drawing.Point(3, 42);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(262, 172);
			this.label4.TabIndex = 2;
			this.label4.Text = "QQ群信息\r\n群1: 592132877\r\n群2: 948305931\r\n群3: 767856490\r\n群4: 818409889\r\n群5: 474160094\r\n普通vip群: 838185568 (赞助240元)";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.Controls.Add(this.hslPanel1);
			this.panel1.Location = new System.Drawing.Point(3, 3);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(266, 294);
			this.panel1.TabIndex = 0;
			this.hslPanel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslPanel1.CenterColor = System.Drawing.Color.WhiteSmoke;
			this.hslPanel1.Controls.Add(this.label1);
			this.hslPanel1.Controls.Add(this.label3);
			this.hslPanel1.Controls.Add(this.label2);
			this.hslPanel1.EdgeColor = System.Drawing.Color.FromArgb(224, 224, 224);
			this.hslPanel1.Location = new System.Drawing.Point(3, 7);
			this.hslPanel1.Name = "hslPanel1";
			this.hslPanel1.Size = new System.Drawing.Size(260, 241);
			this.hslPanel1.TabIndex = 0;
			this.hslPanel1.Text = "hslPanel1";
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label1.BackColor = System.Drawing.Color.Transparent;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.ForeColor = System.Drawing.Color.Black;
			this.label1.Location = new System.Drawing.Point(0, 21);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(260, 36);
			this.label1.TabIndex = 0;
			this.label1.Text = "个人使用";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label1.Click += new System.EventHandler(label1_Click);
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label3.BackColor = System.Drawing.Color.Transparent;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.ForeColor = System.Drawing.Color.Red;
			this.label3.Location = new System.Drawing.Point(3, 100);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(260, 141);
			this.label3.TabIndex = 2;
			this.label3.Text = "调用C#的dll或java的jar包\r\n连续运行10年\r\n软件重启重新计时\r\n扫描下方二维码付款然后加vip群\r\n高级API限制使用24小时\r\n注释带[商业授权]字样的";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label2.BackColor = System.Drawing.Color.Transparent;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.ForeColor = System.Drawing.Color.Blue;
			this.label2.Location = new System.Drawing.Point(0, 65);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(260, 36);
			this.label2.TabIndex = 1;
			this.label2.Text = "240 ￥";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.tableLayoutPanel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tableLayoutPanel2.ColumnCount = 4;
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120f));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120f));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120f));
			this.tableLayoutPanel2.Controls.Add(this.panel7, 3, 0);
			this.tableLayoutPanel2.Controls.Add(this.panel6, 2, 0);
			this.tableLayoutPanel2.Controls.Add(this.panel4, 1, 0);
			this.tableLayoutPanel2.Controls.Add(this.panel2, 0, 0);
			this.tableLayoutPanel2.Location = new System.Drawing.Point(12, 330);
			this.tableLayoutPanel2.Name = "tableLayoutPanel2";
			this.tableLayoutPanel2.RowCount = 1;
			this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			this.tableLayoutPanel2.Size = new System.Drawing.Size(822, 268);
			this.tableLayoutPanel2.TabIndex = 8;
			this.panel7.Controls.Add(this.label16);
			this.panel7.Controls.Add(this.label12);
			this.panel7.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel7.Location = new System.Drawing.Point(705, 3);
			this.panel7.Name = "panel7";
			this.panel7.Size = new System.Drawing.Size(114, 262);
			this.panel7.TabIndex = 3;
			this.label16.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label16.BackColor = System.Drawing.Color.Transparent;
			this.label16.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label16.ForeColor = System.Drawing.Color.Red;
			this.label16.Location = new System.Drawing.Point(5, 38);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(106, 218);
			this.label16.TabIndex = 5;
			this.label16.Text = "ab-plc\r\nsiemens-s7\r\nsiemens-fw\r\nmelsec-3e\r\nmelsec-1e\r\nmodbus-tcp\r\nomron-finsTcp";
			this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label12.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label12.Location = new System.Drawing.Point(3, 5);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(91, 33);
			this.label12.TabIndex = 1;
			this.label12.Text = "Python 版本";
			this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.panel6.Controls.Add(this.label15);
			this.panel6.Controls.Add(this.label10);
			this.panel6.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel6.Location = new System.Drawing.Point(585, 3);
			this.panel6.Name = "panel6";
			this.panel6.Size = new System.Drawing.Size(114, 262);
			this.panel6.TabIndex = 2;
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label15.BackColor = System.Drawing.Color.Transparent;
			this.label15.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label15.ForeColor = System.Drawing.Color.Red;
			this.label15.Location = new System.Drawing.Point(6, 38);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(108, 218);
			this.label15.TabIndex = 4;
			this.label15.Text = "ab-plc\r\nsiemens-s7\r\nsiemens-fw\r\nmelsec-3e\r\nmelsec-1e\r\nmodbus-tcp\r\nomron-finsTcp";
			this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label10.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label10.Location = new System.Drawing.Point(3, 8);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(111, 30);
			this.label10.TabIndex = 1;
			this.label10.Text = "Java 版本";
			this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.panel4.Controls.Add(this.label14);
			this.panel4.Controls.Add(this.label6);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(465, 3);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(114, 262);
			this.panel4.TabIndex = 1;
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label14.BackColor = System.Drawing.Color.Transparent;
			this.label14.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label14.ForeColor = System.Drawing.Color.Red;
			this.label14.Location = new System.Drawing.Point(3, 38);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(108, 218);
			this.label14.TabIndex = 3;
			this.label14.Text = "winform\r\nwpf\r\nunity\r\nasp.net\r\n.net core\r\nwindows\r\nlinux\r\nXamarin\r\nandriod\r\nuwp\r\nraspberry pi";
			this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label6.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label6.Location = new System.Drawing.Point(3, 8);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(108, 30);
			this.label6.TabIndex = 0;
			this.label6.Text = "C# 版本";
			this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.tableLayoutPanel3);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(456, 262);
			this.panel2.TabIndex = 0;
			this.label11.AutoSize = true;
			this.label11.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label11.ForeColor = System.Drawing.Color.DarkOrchid;
			this.label11.Location = new System.Drawing.Point(7, 27);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(261, 17);
			this.label11.TabIndex = 10;
			this.label11.Text = "本软件已经申请著作权：软著登字第5219522号";
			this.tableLayoutPanel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tableLayoutPanel3.ColumnCount = 3;
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333f));
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333f));
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333f));
			this.tableLayoutPanel3.Controls.Add(this.pictureBox3, 0, 0);
			this.tableLayoutPanel3.Controls.Add(this.panel9, 1, 0);
			this.tableLayoutPanel3.Controls.Add(this.panel8, 0, 0);
			this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 52);
			this.tableLayoutPanel3.Name = "tableLayoutPanel3";
			this.tableLayoutPanel3.RowCount = 1;
			this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			this.tableLayoutPanel3.Size = new System.Drawing.Size(450, 204);
			this.tableLayoutPanel3.TabIndex = 1;
			this.pictureBox3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox3.Image = HslCommunicationDemo.Properties.Resources.weipay;
			this.pictureBox3.Location = new System.Drawing.Point(153, 3);
			this.pictureBox3.Name = "pictureBox3";
			this.pictureBox3.Size = new System.Drawing.Size(144, 198);
			this.pictureBox3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox3.TabIndex = 2;
			this.pictureBox3.TabStop = false;
			this.panel9.Controls.Add(this.pictureBox2);
			this.panel9.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel9.Location = new System.Drawing.Point(303, 3);
			this.panel9.Name = "panel9";
			this.panel9.Size = new System.Drawing.Size(144, 198);
			this.panel9.TabIndex = 1;
			this.pictureBox2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox2.Image = HslCommunicationDemo.Properties.Resources.copyright;
			this.pictureBox2.Location = new System.Drawing.Point(0, 0);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(144, 198);
			this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox2.TabIndex = 1;
			this.pictureBox2.TabStop = false;
			this.panel8.Controls.Add(this.pictureBox1);
			this.panel8.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel8.Location = new System.Drawing.Point(3, 3);
			this.panel8.Name = "panel8";
			this.panel8.Size = new System.Drawing.Size(144, 198);
			this.panel8.TabIndex = 0;
			this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.alipay;
			this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(144, 198);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.label5.AutoSize = true;
			this.label5.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label5.Location = new System.Drawing.Point(7, 5);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(260, 17);
			this.label5.TabIndex = 0;
			this.label5.Text = "个人非商用付款方式（企业商用请联系公司）：";
			this.label17.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label17.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 134);
			this.label17.Location = new System.Drawing.Point(7, 0);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(827, 69);
			this.label17.TabIndex = 9;
			this.label17.Text = "本组件的版权归杭州胡工物联科技有限公司所有，商业使用请联系授权，感谢对中国工业软件行业，自动化行业的支持\r\n任何盗用软件，破解软件，未经正式合同授权而商业使用均视为侵权，将依法追究责任，商用用途包含直接集成销售及企业内部使用产生经济效益。";
			this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			this.BackColor = System.Drawing.Color.AliceBlue;
			base.ClientSize = new System.Drawing.Size(845, 600);
			base.Controls.Add(this.label17);
			base.Controls.Add(this.tableLayoutPanel2);
			base.Controls.Add(this.tableLayoutPanel1);
			base.Name = "FormCharge";
			this.Text = "关于授权";
			base.Load += new System.EventHandler(FormCharge_Load);
			this.tableLayoutPanel1.ResumeLayout(false);
			this.panel5.ResumeLayout(false);
			this.hslPanel3.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			this.hslPanel2.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.hslPanel1.ResumeLayout(false);
			this.tableLayoutPanel2.ResumeLayout(false);
			this.panel7.ResumeLayout(false);
			this.panel6.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.tableLayoutPanel3.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox3).EndInit();
			this.panel9.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
			this.panel8.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
