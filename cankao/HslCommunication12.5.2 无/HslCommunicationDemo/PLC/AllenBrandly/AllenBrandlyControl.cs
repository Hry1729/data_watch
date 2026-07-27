using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.AllenBradley;

namespace HslCommunicationDemo.PLC.AllenBrandly
{
	public class AllenBrandlyControl : UserControl
	{
		private IReadWriteCip cipNet;

		private IContainer components = null;

		private GroupBox groupBox1;

		private Button button11;

		private Button button10;

		private Button button9;

		private Button button8;

		private Button button7;

		private Button button6;

		private TextBox textBox_date_render;

		private Label label8;

		private TextBox textBox_date_address;

		private Label label2;

		private GroupBox groupBox5;

		private Button button12;

		private Button button5;

		private TextBox textBox7;

		private Label label7;

		private TextBox textBox5;

		private Label label6;

		private TextBox textBox4;

		private Label label5;

		private Button button4;

		private TextBox textBox3;

		private Label label4;

		private TextBox textBox_code;

		private Label label_code;

		private Label label_read_length;

		private Label label_type_info;

		public AllenBrandlyControl()
		{
			InitializeComponent();
		}

		public void SetDevice(IReadWriteCip cipNet, string address)
		{
			this.cipNet = cipNet;
			textBox_date_address.Text = address;
			textBox3.Text = address;
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = cipNet.ReadDate(textBox_date_address.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				textBox_date_render.Text = operateResult.Content.ToString();
			}
			textBox_code.Text = "OperateResult<DateTime> read = " + DemoUtils.PlcDeviceName + ".ReadDate( \"" + textBox_date_address.Text + "\" );";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = cipNet.WriteDate(textBox_date_address.Text, DateTime.Parse(textBox_date_render.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				DemoUtils.ShowMessage("Write Success");
			}
			textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".WriteDate( \"" + textBox_date_address.Text + "\", DateTime.Parse( \"" + textBox_date_render.Text + "\" ) );";
		}

		private void button10_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = cipNet.WriteTimeAndDate(textBox_date_address.Text, DateTime.Parse(textBox_date_render.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				DemoUtils.ShowMessage("Write Success");
			}
			textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".WriteTimeAndDate( \"" + textBox_date_address.Text + "\", DateTime.Parse( \"" + textBox_date_render.Text + "\" ) );";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult<TimeSpan> operateResult = cipNet.ReadTime(textBox_date_address.Text);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				textBox_date_render.Text = operateResult.Content.ToString();
			}
			textBox_code.Text = "OperateResult<TimeSpan> read = " + DemoUtils.PlcDeviceName + ".ReadTime( \"" + textBox_date_address.Text + "\" );";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = cipNet.WriteTime(textBox_date_address.Text, TimeSpan.Parse(textBox_date_render.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				DemoUtils.ShowMessage("Write Success");
			}
			textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".WriteTime( \"" + textBox_date_address.Text + "\", TimeSpan.Parse( \"" + textBox_date_render.Text + "\" ) );";
		}

		private void button11_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = cipNet.WriteTimeOfDate(textBox_date_address.Text, TimeSpan.Parse(textBox_date_render.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				DemoUtils.ShowMessage("Write Success");
			}
			textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".WriteTimeOfDate( \"" + textBox_date_address.Text + "\", TimeSpan.Parse( \"" + textBox_date_render.Text + "\" ) );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			try
			{
				DemoUtils.WriteResultRender(cipNet.WriteTag(textBox3.Text, Convert.ToUInt16(textBox4.Text, 16), textBox5.Text.ToHexBytes(), int.Parse(textBox7.Text)), textBox3.Text);
				textBox_code.Text = "OperateResult write = " + DemoUtils.PlcDeviceName + ".WriteTag( \"" + textBox3.Text + "\"," + $" 0x{Convert.ToUInt16(textBox4.Text, 16):X}," + " \"" + textBox5.Text + "\".ToHexBytes( )," + $" {int.Parse(textBox7.Text)} );";
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("write failed：" + ex.Message);
			}
		}

		private string getTypeDescription(ushort typeCode)
		{
			switch (typeCode)
			{
			case 193:
				return "(bool)";
			case 194:
				return "(byte)";
			case 195:
				return "(short)";
			case 196:
				return "(int)";
			case 197:
				return "(long)";
			case 198:
				return "[USINT]";
			case 199:
				return "(ushort)";
			case 200:
				return "(uint)";
			case 201:
				return "(ulong)";
			case 202:
				return "(float)";
			case 203:
				return "(double)";
			case 204:
				return "[Struct]";
			case 208:
				return "[String]";
			case 209:
				return "[Str-8]";
			case 210:
				return "[Str-16]";
			case 211:
				return "[Str-32]";
			case 212:
				return "[Str-64]";
			default:
				return "";
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<ushort, byte[]> operateResult = cipNet.ReadTag(textBox3.Text, ushort.Parse(textBox7.Text));
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read failed! " + operateResult.ToMessageShowString());
			}
			else
			{
				textBox4.Text = operateResult.Content1.ToString("X");
				textBox5.Text = operateResult.Content2.ToHexString(' ');
				label_type_info.Text = getTypeDescription(operateResult.Content1);
				label_read_length.Text = "Bytes: " + operateResult.Content2.Length;
			}
			textBox_code.Text = "OperateResult<ushort, byte[]> read = " + DemoUtils.PlcDeviceName + ".ReadTag( \"" + textBox3.Text + "\", " + textBox7.Text + " ) );";
		}

		private void button12_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = cipNet.ReadPlcType();
			if (operateResult.IsSuccess)
			{
				textBox5.Text = operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read failed! " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadPlcType( );";
		}

		private void AllenBrandlyControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				groupBox1.Text = "Time Tag read/write";
				label8.Text = "Data:";
				label2.Text = "Address:";
				groupBox5.Text = "Tag type/data Read Write";
				label4.Text = "Address:";
				label5.Text = "Type:";
				label6.Text = "Data:";
				label7.Text = "Len:";
				button4.Text = "Write";
				button5.Text = "Read";
				button12.Text = "r-plc-type";
				label_code.Text = "Code:";
			}
		}

		private void groupBox5_Enter(object sender, EventArgs e)
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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.textBox_date_render = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_date_address = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.label_read_length = new System.Windows.Forms.Label();
			this.button12 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label_code = new System.Windows.Forms.Label();
			this.label_type_info = new System.Windows.Forms.Label();
			this.groupBox1.SuspendLayout();
			this.groupBox5.SuspendLayout();
			base.SuspendLayout();
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.groupBox1.Controls.Add(this.button11);
			this.groupBox1.Controls.Add(this.button10);
			this.groupBox1.Controls.Add(this.button9);
			this.groupBox1.Controls.Add(this.button8);
			this.groupBox1.Controls.Add(this.button7);
			this.groupBox1.Controls.Add(this.button6);
			this.groupBox1.Controls.Add(this.textBox_date_render);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textBox_date_address);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(448, 217);
			this.groupBox1.TabIndex = 7;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "时间格式";
			this.button11.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button11.Location = new System.Drawing.Point(279, 116);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(162, 28);
			this.button11.TabIndex = 21;
			this.button11.Text = "w-DateOfTime";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button10.Location = new System.Drawing.Point(279, 52);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(162, 28);
			this.button10.TabIndex = 20;
			this.button10.Text = "w-DateAndTime";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button9.Location = new System.Drawing.Point(363, 84);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(78, 28);
			this.button9.TabIndex = 19;
			this.button9.Text = "w-Time";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(279, 84);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(78, 28);
			this.button8.TabIndex = 18;
			this.button8.Text = "r-Time";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(363, 21);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(78, 28);
			this.button7.TabIndex = 17;
			this.button7.Text = "w-Date";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button6.Location = new System.Drawing.Point(279, 21);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(78, 28);
			this.button6.TabIndex = 16;
			this.button6.Text = "r-Date";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.textBox_date_render.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_date_render.Location = new System.Drawing.Point(64, 57);
			this.textBox_date_render.Multiline = true;
			this.textBox_date_render.Name = "textBox_date_render";
			this.textBox_date_render.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_date_render.Size = new System.Drawing.Size(209, 154);
			this.textBox_date_render.TabIndex = 15;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(10, 60);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(44, 17);
			this.label8.TabIndex = 14;
			this.label8.Text = "数据：";
			this.textBox_date_address.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_date_address.Location = new System.Drawing.Point(64, 24);
			this.textBox_date_address.Name = "textBox_date_address";
			this.textBox_date_address.Size = new System.Drawing.Size(209, 23);
			this.textBox_date_address.TabIndex = 9;
			this.textBox_date_address.Text = "A1";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(10, 27);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 8;
			this.label2.Text = "地址：";
			this.groupBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox5.Controls.Add(this.label_type_info);
			this.groupBox5.Controls.Add(this.label_read_length);
			this.groupBox5.Controls.Add(this.button12);
			this.groupBox5.Controls.Add(this.button5);
			this.groupBox5.Controls.Add(this.textBox7);
			this.groupBox5.Controls.Add(this.label7);
			this.groupBox5.Controls.Add(this.textBox5);
			this.groupBox5.Controls.Add(this.label6);
			this.groupBox5.Controls.Add(this.textBox4);
			this.groupBox5.Controls.Add(this.label5);
			this.groupBox5.Controls.Add(this.button4);
			this.groupBox5.Controls.Add(this.textBox3);
			this.groupBox5.Controls.Add(this.label4);
			this.groupBox5.Location = new System.Drawing.Point(457, 3);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(474, 217);
			this.groupBox5.TabIndex = 8;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "类型及原始数据读写测试";
			this.groupBox5.Enter += new System.EventHandler(groupBox5_Enter);
			this.label_read_length.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_read_length.AutoSize = true;
			this.label_read_length.ForeColor = System.Drawing.Color.Gray;
			this.label_read_length.Location = new System.Drawing.Point(402, 189);
			this.label_read_length.Name = "label_read_length";
			this.label_read_length.Size = new System.Drawing.Size(46, 17);
			this.label_read_length.TabIndex = 27;
			this.label_read_length.Text = "Bytes: ";
			this.button12.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button12.Location = new System.Drawing.Point(294, 184);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(102, 28);
			this.button12.TabIndex = 26;
			this.button12.Text = "PLC 型号";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button5.Location = new System.Drawing.Point(208, 184);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(80, 28);
			this.button5.TabIndex = 16;
			this.button5.Text = "读取";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox7.Location = new System.Drawing.Point(64, 186);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(64, 23);
			this.textBox7.TabIndex = 15;
			this.textBox7.Text = "1";
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(10, 189);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 14;
			this.label7.Text = "长度：";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(64, 56);
			this.textBox5.Multiline = true;
			this.textBox5.Name = "textBox5";
			this.textBox5.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox5.Size = new System.Drawing.Size(400, 124);
			this.textBox5.TabIndex = 13;
			this.textBox5.Text = "64 00";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(10, 59);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 12;
			this.label6.Text = "数据：";
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(349, 27);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(57, 23);
			this.textBox4.TabIndex = 11;
			this.textBox4.Text = "C1";
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(295, 30);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(44, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "类型：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button4.Location = new System.Drawing.Point(132, 184);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(73, 28);
			this.button4.TabIndex = 9;
			this.button4.Text = "写入";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(64, 27);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(224, 23);
			this.textBox3.TabIndex = 7;
			this.textBox3.Text = "A1";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(10, 30);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "地址：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(67, 221);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(864, 40);
			this.textBox_code.TabIndex = 9;
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 224);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 15;
			this.label_code.Text = "代码：";
			this.label_type_info.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_type_info.AutoSize = true;
			this.label_type_info.Location = new System.Drawing.Point(412, 31);
			this.label_type_info.Name = "label_type_info";
			this.label_type_info.Size = new System.Drawing.Size(0, 17);
			this.label_type_info.TabIndex = 28;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.groupBox5);
			base.Controls.Add(this.groupBox1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "AllenBrandlyControl";
			base.Size = new System.Drawing.Size(934, 262);
			base.Load += new System.EventHandler(AllenBrandlyControl_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
