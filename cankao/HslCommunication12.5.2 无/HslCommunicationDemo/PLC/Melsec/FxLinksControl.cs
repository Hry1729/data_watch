using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Melsec.Helper;

namespace HslCommunicationDemo.PLC.Melsec
{
	public class FxLinksControl : UserControl
	{
		private Button button5;

		private Button button4;

		private Label label_code;

		private TextBox textBox_code;

		private Button button3;

		private IReadWriteFxLinks melsecFxLinks;

		public FxLinksControl()
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
			this.button5.Location = new System.Drawing.Point(183, 7);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(108, 30);
			this.button5.TabIndex = 28;
			this.button5.Text = "读取PLC型号";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(95, 7);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(82, 30);
			this.button4.TabIndex = 27;
			this.button4.Text = "停止PLC";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(7, 7);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(82, 30);
			this.button3.TabIndex = 26;
			this.button3.Text = "启动PLC";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(4, 163);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 29;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(69, 160);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(961, 78);
			this.textBox_code.TabIndex = 30;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.button5);
			base.Controls.Add(this.button4);
			base.Controls.Add(this.button3);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FxLinksControl";
			base.Size = new System.Drawing.Size(1033, 241);
			base.Load += new System.EventHandler(FxLinksControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		private void FxLinksControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				button3.Text = "Start PLC";
				button4.Text = "Stop PLC";
				button5.Text = "Read PlcType";
				label_code.Text = "Code:";
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = melsecFxLinks.StartPLC();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Start Failed：" + operateResult.Message);
			}
			else
			{
				DemoUtils.ShowMessage("Start Success");
			}
			textBox_code.Text = "OperateResult result = " + DemoUtils.PlcDeviceName + ".StartPLC( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = melsecFxLinks.StopPLC();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Stop Failed：" + operateResult.Message);
			}
			else
			{
				DemoUtils.ShowMessage("Stop Success");
			}
			textBox_code.Text = "OperateResult result = " + DemoUtils.PlcDeviceName + ".StopPLC( );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = melsecFxLinks.ReadPlcType();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("PLC Type:" + operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Read PLC Type failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadPlcType( );";
		}

		public void SetDevice(IReadWriteFxLinks melsecFxLinks, string address)
		{
			this.melsecFxLinks = melsecFxLinks;
		}
	}
}
