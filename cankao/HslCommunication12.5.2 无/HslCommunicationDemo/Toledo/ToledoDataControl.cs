using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Profinet.Toledo;

namespace HslCommunicationDemo.Toledo
{
	public class ToledoDataControl : UserControl
	{
		private IContainer components = null;

		private Label label1;

		private Label label2;

		private Label label3;

		private Label label4;

		private Label label5;

		private Label label6;

		private Label label7;

		private Label label8;

		private TextBox textBox1;

		private Label label9;

		private Label label10;

		private Label label12;

		private TextBox textBox2;

		private Label label11;

		private Label label13;

		private Label label14;

		private Label label15;

		private TextBox textBox3;

		public ToledoDataControl()
		{
			InitializeComponent();
		}

		private void ToledoDataControl_Load(object sender, EventArgs e)
		{
		}

		public void SetToledoData(ToledoStandardData data)
		{
			if (data.Suttle)
			{
				label2.BackColor = Color.Red;
				label1.BackColor = SystemColors.Control;
			}
			else
			{
				label1.BackColor = Color.Red;
				label2.BackColor = SystemColors.Control;
			}
			if (data.Symbol)
			{
				label3.BackColor = Color.Red;
				label4.BackColor = SystemColors.Control;
			}
			else
			{
				label4.BackColor = Color.Red;
				label3.BackColor = SystemColors.Control;
			}
			if (data.BeyondScope)
			{
				label6.BackColor = Color.Red;
				label5.BackColor = SystemColors.Control;
			}
			else
			{
				label5.BackColor = Color.Red;
				label6.BackColor = SystemColors.Control;
			}
			if (data.DynamicState)
			{
				label7.BackColor = Color.Red;
				label8.BackColor = SystemColors.Control;
			}
			else
			{
				label8.BackColor = Color.Red;
				label7.BackColor = SystemColors.Control;
			}
			textBox1.Text = data.Weight.ToString();
			label10.Text = data.Unit;
			textBox2.Text = data.Tare.ToString();
			if (Program.Language == 1)
			{
				label13.Text = (data.DataValid ? "有效" : "无效");
			}
			else
			{
				label13.Text = (data.DataValid ? "Valid" : "Invalid");
			}
			if (data.IsTenExtend)
			{
				if (Program.Language == 1)
				{
					textBox3.Text = ((data.TareType == 0) ? "无皮重" : ((data.TareType == 1) ? "按键去皮" : ((data.TareType == 2) ? "预置皮重" : "皮重内存")));
				}
				else
				{
					textBox3.Text = ((data.TareType == 0) ? "without tare" : ((data.TareType == 1) ? "key peeling" : ((data.TareType == 2) ? "Preset tare" : "Tare memory")));
				}
			}
			else
			{
				textBox3.Text = string.Empty;
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
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.label1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label1.Location = new System.Drawing.Point(17, 36);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(75, 28);
			this.label1.TabIndex = 0;
			this.label1.Text = "毛重";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label2.Location = new System.Drawing.Point(121, 36);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(75, 28);
			this.label2.TabIndex = 1;
			this.label2.Text = "净重";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label3.Location = new System.Drawing.Point(17, 73);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(75, 28);
			this.label3.TabIndex = 2;
			this.label3.Text = "正";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label4.Location = new System.Drawing.Point(121, 73);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(75, 28);
			this.label4.TabIndex = 3;
			this.label4.Text = "负";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label5.Location = new System.Drawing.Point(17, 110);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(75, 28);
			this.label5.TabIndex = 4;
			this.label5.Text = "范围内";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label6.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label6.Location = new System.Drawing.Point(121, 110);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(75, 28);
			this.label6.TabIndex = 5;
			this.label6.Text = "范围外";
			this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label7.Location = new System.Drawing.Point(17, 148);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(75, 28);
			this.label7.TabIndex = 6;
			this.label7.Text = "动态";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label8.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label8.Location = new System.Drawing.Point(121, 148);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(75, 28);
			this.label8.TabIndex = 7;
			this.label8.Text = "稳态";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.textBox1.Location = new System.Drawing.Point(72, 227);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(91, 23);
			this.textBox1.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(13, 230);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(23, 17);
			this.label9.TabIndex = 9;
			this.label9.Text = "值:";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(169, 230);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(32, 17);
			this.label10.TabIndex = 10;
			this.label10.Text = "单位";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(13, 261);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(35, 17);
			this.label12.TabIndex = 12;
			this.label12.Text = "皮重:";
			this.textBox2.Location = new System.Drawing.Point(72, 258);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(91, 23);
			this.textBox2.TabIndex = 11;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(10, 9);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(56, 17);
			this.label11.TabIndex = 13;
			this.label11.Text = "数据值：";
			this.label13.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label13.Location = new System.Drawing.Point(121, 186);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(75, 28);
			this.label13.TabIndex = 14;
			this.label13.Text = "有效";
			this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(22, 192);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(71, 17);
			this.label14.TabIndex = 15;
			this.label14.Text = "数据有效性:";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(13, 293);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(59, 17);
			this.label15.TabIndex = 17;
			this.label15.Text = "皮重类型:";
			this.textBox3.Location = new System.Drawing.Point(72, 290);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(91, 23);
			this.textBox3.TabIndex = 16;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label15);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label14);
			base.Controls.Add(this.label13);
			base.Controls.Add(this.label11);
			base.Controls.Add(this.label12);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.label9);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label8);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ToledoDataControl";
			base.Size = new System.Drawing.Size(216, 329);
			base.Load += new System.EventHandler(ToledoDataControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
