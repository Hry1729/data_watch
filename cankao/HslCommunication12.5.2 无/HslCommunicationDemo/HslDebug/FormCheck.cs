using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Serial;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.HslDebug
{
	public class FormCheck : HslFormContent
	{
		private IContainer components = null;

		private UserControlHead userControlHead1;

		private GroupBox groupBox1;

		private Label label3;

		private TextBox textBox_crc16;

		private TextBox textBox_crc16_source;

		private Label label2;

		private Label label1;

		private TextBox textBox_crc16_code;

		private Button button_crc16_check;

		private Button button_crc16_calcu;

		private Panel panel1;

		private RadioButton radioButton_crc16_acii;

		private RadioButton radioButton_crc16_hex;

		private TextBox textBox_crc16_fill;

		private Label label4;

		private GroupBox groupBox2;

		private Button button_fcs_check;

		private Button button_fcs_calcu;

		private TextBox textBox_fcs_right;

		private Label label7;

		private TextBox textBox_fcs_left;

		private Label label8;

		private TextBox textBox_fcs;

		private Label label6;

		private TextBox textBox_fcs_source;

		private Label label5;

		private GroupBox groupBox3;

		private Button button_acc_check;

		private Button button_acc_calcu;

		private TextBox textBox_acc_right;

		private Label label9;

		private TextBox textBox_acc_left;

		private Label label10;

		private TextBox textBox_acc;

		private Label label11;

		private TextBox textBox_acc_source;

		private Label label12;

		private GroupBox groupBox4;

		private Button button_lrc_check;

		private Button button_lrc_calcu;

		private TextBox textBox_lrc_right;

		private Label label13;

		private TextBox textBox_lrc_left;

		private Label label14;

		private TextBox textBox_lrc;

		private Label label15;

		private TextBox textBox_lrc_source;

		private Label label16;

		private Panel panel2;

		private RadioButton radioButton_fcs_ascii;

		private RadioButton radioButton_fcs_hex;

		private Panel panel3;

		private RadioButton radioButton_acc_ascii;

		private RadioButton radioButton_acc_hex;

		private Panel panel4;

		private RadioButton radioButton_lrc_ascii;

		private RadioButton radioButton_lrc_hex;

		private TextBox textBox_crc16_ascii;

		private TextBox textBox_fcs_ascii;

		private TextBox textBox_acc_ascii;

		private TextBox textBox_lrc_ascii;

		private Label label_code;

		private TextBox textBox_code;

		public FormCheck()
		{
			InitializeComponent();
		}

		private void FormCheck_Load(object sender, EventArgs e)
		{
		}

		private void button_crc16_calcu_Click(object sender, EventArgs e)
		{
			byte[] array = textBox_crc16_code.Text.ToHexBytes();
			byte[] array2 = textBox_crc16_fill.Text.ToHexBytes();
			byte[] array3 = (radioButton_crc16_hex.Checked ? textBox_crc16_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_crc16_source.Text));
			byte[] array4 = SoftCRC16.CRC16Only(array3, 0, array3.Length, array[0], array[1], array2[0], array2[1]);
			textBox_crc16.Text = array4.ToHexString(' ');
			textBox_crc16_ascii.Text = SoftBasic.GetAsciiStringRender(array4);
			string text = (radioButton_crc16_hex.Checked ? ("\"" + textBox_crc16_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_crc16_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = HslCommunication.Serial.SoftCRC16.CRC16Only( {text}, 0, {array3.Length}, {array[0]}, {array[1]}, {array2[0]}, {array2[1]} );";
		}

		private void button_crc16_check_Click(object sender, EventArgs e)
		{
			DemoUtils.ShowMessage(SoftCRC16.CheckCRC16(radioButton_crc16_hex.Checked ? textBox_crc16_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_crc16_source.Text)) ? "Good!" : "Bad!");
			string obj = (radioButton_crc16_hex.Checked ? ("\"" + textBox_crc16_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_crc16_source.Text + "\" )"));
			textBox_code.Text = "bool check = HslCommunication.Serial.SoftCRC16.CheckCRC16( " + obj + " );";
		}

		private void button_fcs_calcu_Click(object sender, EventArgs e)
		{
			byte[] source = (radioButton_fcs_hex.Checked ? textBox_fcs_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_fcs_source.Text));
			int num = Convert.ToInt32(textBox_fcs_left.Text);
			int num2 = Convert.ToInt32(textBox_fcs_right.Text);
			byte[] array = SoftLRC.CalculateFcs(source, num, num2);
			textBox_fcs.Text = array.ToHexString(' ');
			textBox_fcs_ascii.Text = SoftBasic.GetAsciiStringRender(array);
			string arg = (radioButton_fcs_hex.Checked ? ("\"" + textBox_fcs_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_fcs_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = SoftLRC.CalculateFcs( {arg}, {num}, {num2} );";
		}

		private void button_fcs_check_Click(object sender, EventArgs e)
		{
			byte[] array = (radioButton_fcs_hex.Checked ? textBox_fcs_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_fcs_source.Text));
			int num = Convert.ToInt32(textBox_fcs_left.Text);
			int num2 = Convert.ToInt32(textBox_fcs_right.Text);
			byte[] array2 = SoftLRC.CalculateFcs(array, num, num2);
			DemoUtils.ShowMessage((array[array.Length - num2] == array2[0] && array[array.Length - num2 + 1] == array2[1]) ? "Good!" : "Bad!");
			string arg = (radioButton_fcs_hex.Checked ? ("\"" + textBox_fcs_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_fcs_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = SoftLRC.CalculateFcs( {arg}, {num}, {num2} );";
		}

		private void button_acc_calcu_Click(object sender, EventArgs e)
		{
			byte[] buffer = (radioButton_acc_hex.Checked ? textBox_acc_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_acc_source.Text));
			int num = Convert.ToInt32(textBox_acc_left.Text);
			int num2 = Convert.ToInt32(textBox_acc_right.Text);
			byte[] array = new byte[1] { (byte)SoftLRC.CalculateAcc(buffer, num, num2) };
			textBox_acc.Text = array.ToHexString(' ');
			textBox_acc_ascii.Text = SoftBasic.GetAsciiStringRender(array);
			string arg = (radioButton_acc_hex.Checked ? ("\"" + textBox_acc_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_acc_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = new byte[] {{ (byte)SoftLRC.CalculateAcc( {arg}, {num}, {num2} ) }};";
		}

		private void button_acc_check_Click(object sender, EventArgs e)
		{
			byte[] buffer = (radioButton_acc_hex.Checked ? textBox_acc_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_acc_source.Text));
			int num = Convert.ToInt32(textBox_acc_left.Text);
			int num2 = Convert.ToInt32(textBox_acc_right.Text);
			DemoUtils.ShowMessage(SoftLRC.CalculateAccAndCheck(buffer, num, num2) ? "Good!" : "Bad!");
			string arg = (radioButton_acc_hex.Checked ? ("\"" + textBox_acc_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_acc_source.Text + "\" )"));
			textBox_code.Text = $"bool check = SoftLRC.CalculateAccAndCheck( {arg}, {num}, {num2} );";
		}

		private void button_lrc_calcu_Click(object sender, EventArgs e)
		{
			byte[] value = (radioButton_lrc_hex.Checked ? textBox_lrc_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_lrc_source.Text));
			int num = Convert.ToInt32(textBox_lrc_left.Text);
			int num2 = Convert.ToInt32(textBox_lrc_right.Text);
			byte[] array = new byte[1] { SoftLRC.LRC(value, num, num2) };
			textBox_lrc.Text = array.ToHexString(' ');
			textBox_lrc_ascii.Text = SoftBasic.GetAsciiStringRender(array);
			string arg = (radioButton_lrc_hex.Checked ? ("\"" + textBox_lrc_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_lrc_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = new byte[] {{ SoftLRC.LRC( {arg}, {num}, {num2} ) }};";
		}

		private void button_lrc_check_Click(object sender, EventArgs e)
		{
			byte[] array = (radioButton_lrc_hex.Checked ? textBox_lrc_source.Text.ToHexBytes() : SoftBasic.GetFromAsciiStringRender(textBox_lrc_source.Text));
			int num = Convert.ToInt32(textBox_lrc_left.Text);
			int num2 = Convert.ToInt32(textBox_lrc_right.Text);
			byte[] obj = new byte[1] { SoftLRC.LRC(array, num, num2) };
			DemoUtils.ShowMessage((array[array.Length - num2] == obj[0]) ? "Good!" : "Bad!");
			string arg = (radioButton_lrc_hex.Checked ? ("\"" + textBox_lrc_source.Text + "\".ToHexBytes( )") : ("SoftBasic.GetFromAsciiStringRender( \"" + textBox_lrc_source.Text + "\" )"));
			textBox_code.Text = $"byte[] result = new byte[] {{ SoftLRC.LRC( {arg}, {num}, {num2} ) }};";
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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.textBox_crc16_ascii = new System.Windows.Forms.TextBox();
			this.button_crc16_check = new System.Windows.Forms.Button();
			this.button_crc16_calcu = new System.Windows.Forms.Button();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton_crc16_acii = new System.Windows.Forms.RadioButton();
			this.radioButton_crc16_hex = new System.Windows.Forms.RadioButton();
			this.textBox_crc16_fill = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_crc16_code = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_crc16 = new System.Windows.Forms.TextBox();
			this.textBox_crc16_source = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.textBox_fcs_ascii = new System.Windows.Forms.TextBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.radioButton_fcs_ascii = new System.Windows.Forms.RadioButton();
			this.radioButton_fcs_hex = new System.Windows.Forms.RadioButton();
			this.button_fcs_check = new System.Windows.Forms.Button();
			this.button_fcs_calcu = new System.Windows.Forms.Button();
			this.textBox_fcs_right = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_fcs_left = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_fcs = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_fcs_source = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.textBox_acc_ascii = new System.Windows.Forms.TextBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_acc_ascii = new System.Windows.Forms.RadioButton();
			this.radioButton_acc_hex = new System.Windows.Forms.RadioButton();
			this.button_acc_check = new System.Windows.Forms.Button();
			this.button_acc_calcu = new System.Windows.Forms.Button();
			this.textBox_acc_right = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_acc_left = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_acc = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_acc_source = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.textBox_lrc_ascii = new System.Windows.Forms.TextBox();
			this.panel4 = new System.Windows.Forms.Panel();
			this.radioButton_lrc_ascii = new System.Windows.Forms.RadioButton();
			this.radioButton_lrc_hex = new System.Windows.Forms.RadioButton();
			this.button_lrc_check = new System.Windows.Forms.Button();
			this.button_lrc_calcu = new System.Windows.Forms.Button();
			this.textBox_lrc_right = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox_lrc_left = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox_lrc = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_lrc_source = new System.Windows.Forms.TextBox();
			this.label16 = new System.Windows.Forms.Label();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.groupBox1.SuspendLayout();
			this.panel1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.panel3.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.panel4.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.hslcommunication.cn/";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "各种校验调试";
			this.userControlHead1.Size = new System.Drawing.Size(984, 32);
			this.userControlHead1.TabIndex = 15;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.textBox_crc16_ascii);
			this.groupBox1.Controls.Add(this.button_crc16_check);
			this.groupBox1.Controls.Add(this.button_crc16_calcu);
			this.groupBox1.Controls.Add(this.panel1);
			this.groupBox1.Controls.Add(this.textBox_crc16_fill);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.textBox_crc16_code);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.textBox_crc16);
			this.groupBox1.Controls.Add(this.textBox_crc16_source);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Location = new System.Drawing.Point(4, 35);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(978, 119);
			this.groupBox1.TabIndex = 16;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "CRC16 (常用于 Modbus 协议)";
			this.textBox_crc16_ascii.Location = new System.Drawing.Point(209, 82);
			this.textBox_crc16_ascii.Name = "textBox_crc16_ascii";
			this.textBox_crc16_ascii.Size = new System.Drawing.Size(89, 23);
			this.textBox_crc16_ascii.TabIndex = 11;
			this.button_crc16_check.Location = new System.Drawing.Point(877, 80);
			this.button_crc16_check.Name = "button_crc16_check";
			this.button_crc16_check.Size = new System.Drawing.Size(91, 27);
			this.button_crc16_check.TabIndex = 10;
			this.button_crc16_check.Text = "校验CRC";
			this.button_crc16_check.UseVisualStyleBackColor = true;
			this.button_crc16_check.Click += new System.EventHandler(button_crc16_check_Click);
			this.button_crc16_calcu.Location = new System.Drawing.Point(763, 80);
			this.button_crc16_calcu.Name = "button_crc16_calcu";
			this.button_crc16_calcu.Size = new System.Drawing.Size(108, 27);
			this.button_crc16_calcu.TabIndex = 9;
			this.button_crc16_calcu.Text = "计算CRC";
			this.button_crc16_calcu.UseVisualStyleBackColor = true;
			this.button_crc16_calcu.Click += new System.EventHandler(button_crc16_calcu_Click);
			this.panel1.Controls.Add(this.radioButton_crc16_acii);
			this.panel1.Controls.Add(this.radioButton_crc16_hex);
			this.panel1.Location = new System.Drawing.Point(615, 78);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(142, 34);
			this.panel1.TabIndex = 8;
			this.radioButton_crc16_acii.AutoSize = true;
			this.radioButton_crc16_acii.Location = new System.Drawing.Point(65, 5);
			this.radioButton_crc16_acii.Name = "radioButton_crc16_acii";
			this.radioButton_crc16_acii.Size = new System.Drawing.Size(57, 21);
			this.radioButton_crc16_acii.TabIndex = 1;
			this.radioButton_crc16_acii.Text = "ASCII";
			this.radioButton_crc16_acii.UseVisualStyleBackColor = true;
			this.radioButton_crc16_hex.AutoSize = true;
			this.radioButton_crc16_hex.Checked = true;
			this.radioButton_crc16_hex.Location = new System.Drawing.Point(6, 5);
			this.radioButton_crc16_hex.Name = "radioButton_crc16_hex";
			this.radioButton_crc16_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_crc16_hex.TabIndex = 0;
			this.radioButton_crc16_hex.TabStop = true;
			this.radioButton_crc16_hex.Text = "Hex";
			this.radioButton_crc16_hex.UseVisualStyleBackColor = true;
			this.textBox_crc16_fill.Location = new System.Drawing.Point(537, 82);
			this.textBox_crc16_fill.Name = "textBox_crc16_fill";
			this.textBox_crc16_fill.Size = new System.Drawing.Size(70, 23);
			this.textBox_crc16_fill.TabIndex = 7;
			this.textBox_crc16_fill.Text = "FFFF";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(472, 85);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "填充因子：";
			this.textBox_crc16_code.Location = new System.Drawing.Point(388, 82);
			this.textBox_crc16_code.Name = "textBox_crc16_code";
			this.textBox_crc16_code.Size = new System.Drawing.Size(70, 23);
			this.textBox_crc16_code.TabIndex = 5;
			this.textBox_crc16_code.Text = "A001";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(324, 85);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "多项式：";
			this.textBox_crc16.Location = new System.Drawing.Point(114, 82);
			this.textBox_crc16.Name = "textBox_crc16";
			this.textBox_crc16.Size = new System.Drawing.Size(89, 23);
			this.textBox_crc16.TabIndex = 3;
			this.textBox_crc16_source.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_crc16_source.Location = new System.Drawing.Point(114, 22);
			this.textBox_crc16_source.Multiline = true;
			this.textBox_crc16_source.Name = "textBox_crc16_source";
			this.textBox_crc16_source.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_crc16_source.Size = new System.Drawing.Size(853, 52);
			this.textBox_crc16_source.TabIndex = 2;
			this.textBox_crc16_source.Text = "01 03 00 00 00 01";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 85);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(82, 17);
			this.label2.TabIndex = 1;
			this.label2.Text = "CRC16(ascii):";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(9, 25);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "原始数据：";
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.textBox_fcs_ascii);
			this.groupBox2.Controls.Add(this.panel2);
			this.groupBox2.Controls.Add(this.button_fcs_check);
			this.groupBox2.Controls.Add(this.button_fcs_calcu);
			this.groupBox2.Controls.Add(this.textBox_fcs_right);
			this.groupBox2.Controls.Add(this.label7);
			this.groupBox2.Controls.Add(this.textBox_fcs_left);
			this.groupBox2.Controls.Add(this.label8);
			this.groupBox2.Controls.Add(this.textBox_fcs);
			this.groupBox2.Controls.Add(this.label6);
			this.groupBox2.Controls.Add(this.textBox_fcs_source);
			this.groupBox2.Controls.Add(this.label5);
			this.groupBox2.Location = new System.Drawing.Point(4, 160);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(978, 118);
			this.groupBox2.TabIndex = 17;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "FCS,异或校验（用于 OmronHostlink） ";
			this.textBox_fcs_ascii.Location = new System.Drawing.Point(209, 85);
			this.textBox_fcs_ascii.Name = "textBox_fcs_ascii";
			this.textBox_fcs_ascii.Size = new System.Drawing.Size(89, 23);
			this.textBox_fcs_ascii.TabIndex = 15;
			this.panel2.Controls.Add(this.radioButton_fcs_ascii);
			this.panel2.Controls.Add(this.radioButton_fcs_hex);
			this.panel2.Location = new System.Drawing.Point(615, 81);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(142, 34);
			this.panel2.TabIndex = 14;
			this.radioButton_fcs_ascii.AutoSize = true;
			this.radioButton_fcs_ascii.Checked = true;
			this.radioButton_fcs_ascii.Location = new System.Drawing.Point(65, 5);
			this.radioButton_fcs_ascii.Name = "radioButton_fcs_ascii";
			this.radioButton_fcs_ascii.Size = new System.Drawing.Size(57, 21);
			this.radioButton_fcs_ascii.TabIndex = 1;
			this.radioButton_fcs_ascii.TabStop = true;
			this.radioButton_fcs_ascii.Text = "ASCII";
			this.radioButton_fcs_ascii.UseVisualStyleBackColor = true;
			this.radioButton_fcs_hex.AutoSize = true;
			this.radioButton_fcs_hex.Location = new System.Drawing.Point(6, 5);
			this.radioButton_fcs_hex.Name = "radioButton_fcs_hex";
			this.radioButton_fcs_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_fcs_hex.TabIndex = 0;
			this.radioButton_fcs_hex.Text = "Hex";
			this.radioButton_fcs_hex.UseVisualStyleBackColor = true;
			this.button_fcs_check.Location = new System.Drawing.Point(877, 85);
			this.button_fcs_check.Name = "button_fcs_check";
			this.button_fcs_check.Size = new System.Drawing.Size(91, 27);
			this.button_fcs_check.TabIndex = 13;
			this.button_fcs_check.Text = "校验FCS";
			this.button_fcs_check.UseVisualStyleBackColor = true;
			this.button_fcs_check.Click += new System.EventHandler(button_fcs_check_Click);
			this.button_fcs_calcu.Location = new System.Drawing.Point(763, 85);
			this.button_fcs_calcu.Name = "button_fcs_calcu";
			this.button_fcs_calcu.Size = new System.Drawing.Size(108, 27);
			this.button_fcs_calcu.TabIndex = 12;
			this.button_fcs_calcu.Text = "计算FCS";
			this.button_fcs_calcu.UseVisualStyleBackColor = true;
			this.button_fcs_calcu.Click += new System.EventHandler(button_fcs_calcu_Click);
			this.textBox_fcs_right.Location = new System.Drawing.Point(549, 85);
			this.textBox_fcs_right.Name = "textBox_fcs_right";
			this.textBox_fcs_right.Size = new System.Drawing.Size(60, 23);
			this.textBox_fcs_right.TabIndex = 11;
			this.textBox_fcs_right.Text = "4";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(469, 88);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(80, 17);
			this.label7.TabIndex = 10;
			this.label7.Text = "尾部字节数：";
			this.textBox_fcs_left.Location = new System.Drawing.Point(397, 85);
			this.textBox_fcs_left.Name = "textBox_fcs_left";
			this.textBox_fcs_left.Size = new System.Drawing.Size(57, 23);
			this.textBox_fcs_left.TabIndex = 9;
			this.textBox_fcs_left.Text = "0";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(324, 88);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(80, 17);
			this.label8.TabIndex = 8;
			this.label8.Text = "头部字节数：";
			this.textBox_fcs.Location = new System.Drawing.Point(114, 85);
			this.textBox_fcs.Name = "textBox_fcs";
			this.textBox_fcs.Size = new System.Drawing.Size(89, 23);
			this.textBox_fcs.TabIndex = 6;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(10, 88);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(71, 17);
			this.label6.TabIndex = 5;
			this.label6.Text = "FCS(ASCII):";
			this.textBox_fcs_source.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_fcs_source.Location = new System.Drawing.Point(114, 27);
			this.textBox_fcs_source.Multiline = true;
			this.textBox_fcs_source.Name = "textBox_fcs_source";
			this.textBox_fcs_source.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_fcs_source.Size = new System.Drawing.Size(854, 52);
			this.textBox_fcs_source.TabIndex = 4;
			this.textBox_fcs_source.Text = "@00FA00000000001018200000000017C*\\0D";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(10, 30);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 3;
			this.label5.Text = "原始数据：";
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Controls.Add(this.textBox_acc_ascii);
			this.groupBox3.Controls.Add(this.panel3);
			this.groupBox3.Controls.Add(this.button_acc_check);
			this.groupBox3.Controls.Add(this.button_acc_calcu);
			this.groupBox3.Controls.Add(this.textBox_acc_right);
			this.groupBox3.Controls.Add(this.label9);
			this.groupBox3.Controls.Add(this.textBox_acc_left);
			this.groupBox3.Controls.Add(this.label10);
			this.groupBox3.Controls.Add(this.textBox_acc);
			this.groupBox3.Controls.Add(this.label11);
			this.groupBox3.Controls.Add(this.textBox_acc_source);
			this.groupBox3.Controls.Add(this.label12);
			this.groupBox3.Location = new System.Drawing.Point(4, 284);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(978, 118);
			this.groupBox3.TabIndex = 18;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "ACC,和校验（用于 MelsecFxLinks） ";
			this.textBox_acc_ascii.Location = new System.Drawing.Point(209, 85);
			this.textBox_acc_ascii.Name = "textBox_acc_ascii";
			this.textBox_acc_ascii.Size = new System.Drawing.Size(89, 23);
			this.textBox_acc_ascii.TabIndex = 16;
			this.panel3.Controls.Add(this.radioButton_acc_ascii);
			this.panel3.Controls.Add(this.radioButton_acc_hex);
			this.panel3.Location = new System.Drawing.Point(615, 80);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(142, 34);
			this.panel3.TabIndex = 15;
			this.radioButton_acc_ascii.AutoSize = true;
			this.radioButton_acc_ascii.Checked = true;
			this.radioButton_acc_ascii.Location = new System.Drawing.Point(65, 5);
			this.radioButton_acc_ascii.Name = "radioButton_acc_ascii";
			this.radioButton_acc_ascii.Size = new System.Drawing.Size(57, 21);
			this.radioButton_acc_ascii.TabIndex = 1;
			this.radioButton_acc_ascii.TabStop = true;
			this.radioButton_acc_ascii.Text = "ASCII";
			this.radioButton_acc_ascii.UseVisualStyleBackColor = true;
			this.radioButton_acc_hex.AutoSize = true;
			this.radioButton_acc_hex.Location = new System.Drawing.Point(6, 5);
			this.radioButton_acc_hex.Name = "radioButton_acc_hex";
			this.radioButton_acc_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_acc_hex.TabIndex = 0;
			this.radioButton_acc_hex.Text = "Hex";
			this.radioButton_acc_hex.UseVisualStyleBackColor = true;
			this.button_acc_check.Location = new System.Drawing.Point(877, 83);
			this.button_acc_check.Name = "button_acc_check";
			this.button_acc_check.Size = new System.Drawing.Size(91, 27);
			this.button_acc_check.TabIndex = 13;
			this.button_acc_check.Text = "校验ACC";
			this.button_acc_check.UseVisualStyleBackColor = true;
			this.button_acc_check.Click += new System.EventHandler(button_acc_check_Click);
			this.button_acc_calcu.Location = new System.Drawing.Point(763, 83);
			this.button_acc_calcu.Name = "button_acc_calcu";
			this.button_acc_calcu.Size = new System.Drawing.Size(108, 27);
			this.button_acc_calcu.TabIndex = 12;
			this.button_acc_calcu.Text = "计算ACC";
			this.button_acc_calcu.UseVisualStyleBackColor = true;
			this.button_acc_calcu.Click += new System.EventHandler(button_acc_calcu_Click);
			this.textBox_acc_right.Location = new System.Drawing.Point(549, 85);
			this.textBox_acc_right.Name = "textBox_acc_right";
			this.textBox_acc_right.Size = new System.Drawing.Size(60, 23);
			this.textBox_acc_right.TabIndex = 11;
			this.textBox_acc_right.Text = "2";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(469, 88);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(80, 17);
			this.label9.TabIndex = 10;
			this.label9.Text = "尾部字节数：";
			this.textBox_acc_left.Location = new System.Drawing.Point(397, 85);
			this.textBox_acc_left.Name = "textBox_acc_left";
			this.textBox_acc_left.Size = new System.Drawing.Size(57, 23);
			this.textBox_acc_left.TabIndex = 9;
			this.textBox_acc_left.Text = "1";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(324, 88);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(80, 17);
			this.label10.TabIndex = 8;
			this.label10.Text = "头部字节数：";
			this.textBox_acc.Location = new System.Drawing.Point(114, 85);
			this.textBox_acc.Name = "textBox_acc";
			this.textBox_acc.Size = new System.Drawing.Size(89, 23);
			this.textBox_acc.TabIndex = 6;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(10, 88);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(74, 17);
			this.label11.TabIndex = 5;
			this.label11.Text = "ACC(ASCII):";
			this.textBox_acc_source.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_acc_source.Location = new System.Drawing.Point(114, 27);
			this.textBox_acc_source.Multiline = true;
			this.textBox_acc_source.Name = "textBox_acc_source";
			this.textBox_acc_source.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_acc_source.Size = new System.Drawing.Size(854, 52);
			this.textBox_acc_source.TabIndex = 4;
			this.textBox_acc_source.Text = "\\0500FFWR0D0100012B";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(10, 30);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(68, 17);
			this.label12.TabIndex = 3;
			this.label12.Text = "原始数据：";
			this.groupBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox4.Controls.Add(this.textBox_lrc_ascii);
			this.groupBox4.Controls.Add(this.panel4);
			this.groupBox4.Controls.Add(this.button_lrc_check);
			this.groupBox4.Controls.Add(this.button_lrc_calcu);
			this.groupBox4.Controls.Add(this.textBox_lrc_right);
			this.groupBox4.Controls.Add(this.label13);
			this.groupBox4.Controls.Add(this.textBox_lrc_left);
			this.groupBox4.Controls.Add(this.label14);
			this.groupBox4.Controls.Add(this.textBox_lrc);
			this.groupBox4.Controls.Add(this.label15);
			this.groupBox4.Controls.Add(this.textBox_lrc_source);
			this.groupBox4.Controls.Add(this.label16);
			this.groupBox4.Location = new System.Drawing.Point(4, 408);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(978, 118);
			this.groupBox4.TabIndex = 19;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "LRC（用于 ModbusASCII） ";
			this.textBox_lrc_ascii.Location = new System.Drawing.Point(209, 85);
			this.textBox_lrc_ascii.Name = "textBox_lrc_ascii";
			this.textBox_lrc_ascii.Size = new System.Drawing.Size(89, 23);
			this.textBox_lrc_ascii.TabIndex = 17;
			this.panel4.Controls.Add(this.radioButton_lrc_ascii);
			this.panel4.Controls.Add(this.radioButton_lrc_hex);
			this.panel4.Location = new System.Drawing.Point(616, 81);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(142, 34);
			this.panel4.TabIndex = 16;
			this.radioButton_lrc_ascii.AutoSize = true;
			this.radioButton_lrc_ascii.Location = new System.Drawing.Point(65, 5);
			this.radioButton_lrc_ascii.Name = "radioButton_lrc_ascii";
			this.radioButton_lrc_ascii.Size = new System.Drawing.Size(57, 21);
			this.radioButton_lrc_ascii.TabIndex = 1;
			this.radioButton_lrc_ascii.Text = "ASCII";
			this.radioButton_lrc_ascii.UseVisualStyleBackColor = true;
			this.radioButton_lrc_hex.AutoSize = true;
			this.radioButton_lrc_hex.Checked = true;
			this.radioButton_lrc_hex.Location = new System.Drawing.Point(6, 5);
			this.radioButton_lrc_hex.Name = "radioButton_lrc_hex";
			this.radioButton_lrc_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_lrc_hex.TabIndex = 0;
			this.radioButton_lrc_hex.TabStop = true;
			this.radioButton_lrc_hex.Text = "Hex";
			this.radioButton_lrc_hex.UseVisualStyleBackColor = true;
			this.button_lrc_check.Location = new System.Drawing.Point(878, 83);
			this.button_lrc_check.Name = "button_lrc_check";
			this.button_lrc_check.Size = new System.Drawing.Size(91, 27);
			this.button_lrc_check.TabIndex = 13;
			this.button_lrc_check.Text = "校验LRC";
			this.button_lrc_check.UseVisualStyleBackColor = true;
			this.button_lrc_check.Click += new System.EventHandler(button_lrc_check_Click);
			this.button_lrc_calcu.Location = new System.Drawing.Point(764, 83);
			this.button_lrc_calcu.Name = "button_lrc_calcu";
			this.button_lrc_calcu.Size = new System.Drawing.Size(108, 27);
			this.button_lrc_calcu.TabIndex = 12;
			this.button_lrc_calcu.Text = "计算LRC";
			this.button_lrc_calcu.UseVisualStyleBackColor = true;
			this.button_lrc_calcu.Click += new System.EventHandler(button_lrc_calcu_Click);
			this.textBox_lrc_right.Location = new System.Drawing.Point(549, 85);
			this.textBox_lrc_right.Name = "textBox_lrc_right";
			this.textBox_lrc_right.Size = new System.Drawing.Size(60, 23);
			this.textBox_lrc_right.TabIndex = 11;
			this.textBox_lrc_right.Text = "1";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(469, 88);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(80, 17);
			this.label13.TabIndex = 10;
			this.label13.Text = "尾部字节数：";
			this.textBox_lrc_left.Location = new System.Drawing.Point(397, 85);
			this.textBox_lrc_left.Name = "textBox_lrc_left";
			this.textBox_lrc_left.Size = new System.Drawing.Size(57, 23);
			this.textBox_lrc_left.TabIndex = 9;
			this.textBox_lrc_left.Text = "0";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(324, 88);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(80, 17);
			this.label14.TabIndex = 8;
			this.label14.Text = "头部字节数：";
			this.textBox_lrc.Location = new System.Drawing.Point(114, 85);
			this.textBox_lrc.Name = "textBox_lrc";
			this.textBox_lrc.Size = new System.Drawing.Size(89, 23);
			this.textBox_lrc.TabIndex = 6;
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(10, 88);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(72, 17);
			this.label15.TabIndex = 5;
			this.label15.Text = "LRC(ASCII):";
			this.textBox_lrc_source.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_lrc_source.Location = new System.Drawing.Point(114, 27);
			this.textBox_lrc_source.Multiline = true;
			this.textBox_lrc_source.Name = "textBox_lrc_source";
			this.textBox_lrc_source.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_lrc_source.Size = new System.Drawing.Size(854, 52);
			this.textBox_lrc_source.TabIndex = 4;
			this.textBox_lrc_source.Text = "01 03 00 64 00 01 97";
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(10, 30);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(68, 17);
			this.label16.TabIndex = 3;
			this.label16.Text = "原始数据：";
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(10, 537);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 20;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(60, 532);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(922, 57);
			this.textBox_code.TabIndex = 21;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(984, 598);
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.groupBox4);
			base.Controls.Add(this.groupBox3);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "FormCheck";
			this.Text = "FormCheck";
			base.Load += new System.EventHandler(FormCheck_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
