using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Keyence;

namespace HslCommunicationDemo.PLC.Keyence
{
	public class NanoControl : UserControl
	{
		private Button button4;

		private Button button3;

		private TextBox textBox10;

		private Label label13;

		private TextBox textBox6;

		private Label label_code;

		private TextBox textBox_code;

		private Button button_clear_error;

		private Button button_plc_mode;

		private Label label1;

		private KeyenceNanoSerial keyence;

		private KeyenceNanoSerialOverTcp keyence1;

		public NanoControl()
		{
			InitializeComponent();
			if (Program.Language == 2)
			{
				label1.Text = "Address:";
				label13.Text = "Result:";
				button4.Text = "Annotation";
				label_code.Text = "Code:";
			}
		}

		private void InitializeComponent()
		{
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.button_clear_error = new System.Windows.Forms.Button();
			this.button_plc_mode = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.textBox6.Location = new System.Drawing.Point(57, 5);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(217, 23);
			this.textBox6.TabIndex = 18;
			this.textBox6.Text = "D100";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 17;
			this.label1.Text = "地址：";
			this.textBox10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox10.Location = new System.Drawing.Point(57, 35);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox10.Size = new System.Drawing.Size(722, 127);
			this.textBox10.TabIndex = 16;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(3, 37);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 15;
			this.label13.Text = "结果：";
			this.button4.Location = new System.Drawing.Point(280, 2);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(72, 28);
			this.button4.TabIndex = 14;
			this.button4.Text = "注释";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(358, 2);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(72, 28);
			this.button3.TabIndex = 13;
			this.button3.Text = "plc-type";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 168);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 19;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(57, 166);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(722, 63);
			this.textBox_code.TabIndex = 20;
			this.button_clear_error.Location = new System.Drawing.Point(436, 2);
			this.button_clear_error.Name = "button_clear_error";
			this.button_clear_error.Size = new System.Drawing.Size(82, 28);
			this.button_clear_error.TabIndex = 21;
			this.button_clear_error.Text = "错误清除";
			this.button_clear_error.UseVisualStyleBackColor = true;
			this.button_clear_error.Click += new System.EventHandler(button_clear_error_Click);
			this.button_plc_mode.Location = new System.Drawing.Point(524, 2);
			this.button_plc_mode.Name = "button_plc_mode";
			this.button_plc_mode.Size = new System.Drawing.Size(76, 28);
			this.button_plc_mode.TabIndex = 22;
			this.button_plc_mode.Text = "plc-mode";
			this.button_plc_mode.UseVisualStyleBackColor = true;
			this.button_plc_mode.Click += new System.EventHandler(button_plc_mode_Click);
			base.Controls.Add(this.button_plc_mode);
			base.Controls.Add(this.button_clear_error);
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox6);
			base.Controls.Add(this.button3);
			base.Controls.Add(this.button4);
			base.Controls.Add(this.textBox10);
			base.Controls.Add(this.label13);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "NanoControl";
			base.Size = new System.Drawing.Size(782, 232);
			base.Load += new System.EventHandler(NanoControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		public void SetDevice(KeyenceNanoSerial keyence, string address)
		{
			this.keyence = keyence;
		}

		public void SetDevice(KeyenceNanoSerialOverTcp keyence, string address)
		{
			keyence1 = keyence;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OperateResult<KeyencePLCS> operateResult = ((keyence != null) ? keyence.ReadPlcType() : keyence1.ReadPlcType());
			if (operateResult.IsSuccess)
			{
				textBox10.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<KeyencePLCS> read = " + DemoUtils.PlcDeviceName + ".ReadPlcType( );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = ((keyence != null) ? keyence.ReadAddressAnnotation(textBox6.Text) : keyence1.ReadAddressAnnotation(textBox6.Text));
			if (operateResult.IsSuccess)
			{
				textBox10.Text = operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadAddressAnnotation( \"" + textBox6.Text + "\" );";
		}

		private void NanoControl_Load(object sender, EventArgs e)
		{
		}

		private void button_clear_error_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = ((keyence != null) ? keyence.ClearError() : keyence1.ClearError());
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("ClearError success");
			}
			else
			{
				DemoUtils.ShowMessage("ClearError failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + DemoUtils.PlcDeviceName + ".ClearError( );";
		}

		private void button_plc_mode_Click(object sender, EventArgs e)
		{
			OperateResult<int> operateResult = ((keyence != null) ? keyence.ReadPlcMode() : keyence1.ReadPlcMode());
			if (operateResult.IsSuccess)
			{
				textBox10.Text = "Mode: " + operateResult.Content + "   // 如果是0，代表 PROG模式或者梯形图未登录，如果为1，代表RUN模式";
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed:" + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult<int> read = " + DemoUtils.PlcDeviceName + ".ReadPlcMode( );";
		}
	}
}
