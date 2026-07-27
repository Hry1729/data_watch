using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.GE;

namespace HslCommunicationDemo.PLC.Ge
{
	public class GeControl : UserControl
	{
		private Button button4;

		private TextBox textBox14;

		private Label label_code;

		private TextBox textBox_code;

		private Button button3;

		private GeSRTPNet ge;

		public GeControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.button4 = new System.Windows.Forms.Button();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.button3 = new System.Windows.Forms.Button();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.button4.Location = new System.Drawing.Point(94, 3);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(102, 28);
			this.button4.TabIndex = 27;
			this.button4.Text = "程序名";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox14.Location = new System.Drawing.Point(3, 37);
			this.textBox14.Multiline = true;
			this.textBox14.Name = "textBox14";
			this.textBox14.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox14.Size = new System.Drawing.Size(856, 139);
			this.textBox14.TabIndex = 26;
			this.button3.Location = new System.Drawing.Point(3, 3);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(85, 28);
			this.button3.TabIndex = 25;
			this.button3.Text = "PLC时间";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 179);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(35, 17);
			this.label_code.TabIndex = 28;
			this.label_code.Text = "代码:";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(56, 180);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(803, 50);
			this.textBox_code.TabIndex = 29;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.button4);
			base.Controls.Add(this.textBox14);
			base.Controls.Add(this.button3);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "GeControl";
			base.Size = new System.Drawing.Size(862, 232);
			base.Load += new System.EventHandler(GeControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		public void SetDevice(GeSRTPNet ge, string address)
		{
			this.ge = ge;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<DateTime> operateResult = ge.ReadPLCTime();
			if (operateResult.IsSuccess)
			{
				textBox14.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<DateTime> read = " + DemoUtils.PlcDeviceName + ".ReadPLCTime( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = ge.ReadProgramName();
			if (operateResult.IsSuccess)
			{
				textBox14.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadProgramName( );";
		}

		private void GeControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label_code.Text = "Code:";
			}
		}
	}
}
