using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Melsec.Helper;

namespace HslCommunicationDemo.PLC.Melsec
{
	public class McQna3EControl : UserControl
	{
		private Button button6;

		private Button button7;

		private Button button5;

		private Button button4;

		private Button button8;

		private Button button11;

		private Button button10;

		private TextBox textBox_code;

		private GroupBox groupBox2;

		private IReadWriteMc mc;

		private string deviceName = "plc";

		public McQna3EControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button11 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.groupBox2.SuspendLayout();
			base.SuspendLayout();
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.textBox_code);
			this.groupBox2.Controls.Add(this.button11);
			this.groupBox2.Controls.Add(this.button10);
			this.groupBox2.Controls.Add(this.button6);
			this.groupBox2.Controls.Add(this.button7);
			this.groupBox2.Controls.Add(this.button5);
			this.groupBox2.Controls.Add(this.button4);
			this.groupBox2.Controls.Add(this.button8);
			this.groupBox2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox2.Location = new System.Drawing.Point(2, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(725, 225);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "MC Functions";
			this.button11.Location = new System.Drawing.Point(202, 118);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(194, 28);
			this.button11.TabIndex = 32;
			this.button11.Text = "Wait D100 123";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button10.Location = new System.Drawing.Point(6, 118);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(190, 28);
			this.button10.TabIndex = 31;
			this.button10.Text = "Wait M100 True";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button6.Location = new System.Drawing.Point(6, 59);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(92, 28);
			this.button6.TabIndex = 28;
			this.button6.Text = "plc type";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button7.Location = new System.Drawing.Point(6, 25);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(92, 28);
			this.button7.TabIndex = 29;
			this.button7.Text = "remote reset";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button5.Location = new System.Drawing.Point(300, 25);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(94, 28);
			this.button5.TabIndex = 27;
			this.button5.Text = "remote stop";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(202, 25);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(86, 28);
			this.button4.TabIndex = 26;
			this.button4.Text = "remote run";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button8.Location = new System.Drawing.Point(104, 25);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(92, 28);
			this.button8.TabIndex = 30;
			this.button8.Text = "error reset";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(6, 152);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(713, 67);
			this.textBox_code.TabIndex = 33;
			base.Controls.Add(this.groupBox2);
			base.Name = "McQna3EControl";
			base.Size = new System.Drawing.Size(730, 231);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			base.ResumeLayout(false);
		}

		public void SetDevice(IReadWriteMc mc, string address, string deviceName)
		{
			this.mc = mc;
			this.deviceName = deviceName;
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mc.RemoteReset();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("RemoteReset Success");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + deviceName + ".RemoteReset( );    // 远程重置操作";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mc.ErrorStateReset();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("ErrorStateReset Success");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + deviceName + ".ErrorStateReset( );    // 错误状态恢复";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mc.RemoteRun();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Run Success");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + deviceName + ".RemoteRun( );    // 远程启动操作";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mc.RemoteStop();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Stop Success");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + deviceName + ".RemoteStop( );    // 远程停止操作";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = mc.ReadPlcType();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Type:" + operateResult.Content);
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + deviceName + ".ReadPlcType( );    // 读取PLC的规格信息";
		}

		private async void button10_Click(object sender, EventArgs e)
		{
			textBox_code.Text = "OperateResult<TimeSpan> wait = await " + deviceName + ".WaitAsync( \"M100\", true, 100, 30_000 );    // 等待M100为True，读取频率为间隔100ms，等待超时为30秒";
			button10.Enabled = false;
			OperateResult<TimeSpan> wait = await mc.WaitAsync("M100", true, 100, 30000);
			if (wait.IsSuccess)
			{
				DemoUtils.ShowMessage("Wait Success, Takes " + wait.Content.TotalSeconds.ToString("F1") + " Seconds");
			}
			else
			{
				DemoUtils.ShowMessage("Wait Failed:" + wait.Message);
			}
			button10.Enabled = true;
		}

		private async void button11_Click(object sender, EventArgs e)
		{
			textBox_code.Text = "OperateResult<TimeSpan> wait = await " + deviceName + ".WaitAsync( \"D100\", (short)123, 100, 30_000  );    // 等待D100为123，读取频率为间隔100ms，等待超时为30秒";
			button11.Enabled = false;
			OperateResult<TimeSpan> wait = await mc.WaitAsync("D100", (short)123, 100, 30000);
			if (wait.IsSuccess)
			{
				DemoUtils.ShowMessage("Wait Success, Takes " + wait.Content.TotalSeconds.ToString("F1") + " Seconds");
			}
			else
			{
				DemoUtils.ShowMessage("Wait Failed:" + wait.Message);
			}
			button11.Enabled = true;
		}
	}
}
