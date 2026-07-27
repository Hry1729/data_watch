using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Siemens.Helper;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class SiemensPPIControl : UserControl
	{
		private Button button5;

		private Button button4;

		private Label label_code;

		private TextBox textBox_code;

		private Button button3;

		private ISiemensPPI siemensPPI;

		public SiemensPPIControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(192, 9);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(82, 28);
			this.button5.TabIndex = 20;
			this.button5.Text = "PLC Type";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(91, 9);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(82, 28);
			this.button4.TabIndex = 22;
			this.button4.Text = "Stop";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(3, 9);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(82, 28);
			this.button3.TabIndex = 21;
			this.button3.Text = "Start";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 121);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 23;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(53, 118);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(699, 54);
			this.textBox_code.TabIndex = 24;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.button5);
			base.Controls.Add(this.button4);
			base.Controls.Add(this.button3);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "SiemensPPIControl";
			base.Size = new System.Drawing.Size(755, 175);
			base.Load += new System.EventHandler(SiemensPPIControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		public void SetDevice(ISiemensPPI siemensPPI, string address)
		{
			this.siemensPPI = siemensPPI;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = siemensPPI.Start();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Start Success!");
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
			}
			textBox_code.Text = "OperateResult start = " + DemoUtils.PlcDeviceName + ".Start( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = siemensPPI.Stop();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Stop Success!");
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
			}
			textBox_code.Text = "OperateResult stop = " + DemoUtils.PlcDeviceName + ".Stop( );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = siemensPPI.ReadPlcType();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Plc type: " + operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage(operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadPlcType( );";
		}

		private void SiemensPPIControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label_code.Text = "Code:";
			}
		}
	}
}
