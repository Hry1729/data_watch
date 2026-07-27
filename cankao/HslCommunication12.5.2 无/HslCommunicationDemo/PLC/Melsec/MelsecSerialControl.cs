using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Melsec.Helper;

namespace HslCommunicationDemo.PLC.Melsec
{
	public class MelsecSerialControl : UserControl
	{
		private Button button_active_plc;

		private Label label_code;

		private TextBox textBox_code;

		private GroupBox groupBox2;

		private IMelsecFxSerial melsec;

		public MelsecSerialControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button_active_plc = new System.Windows.Forms.Button();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.groupBox2.SuspendLayout();
			base.SuspendLayout();
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button_active_plc);
			this.groupBox2.Location = new System.Drawing.Point(3, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(933, 184);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Serial Function";
			this.button_active_plc.Location = new System.Drawing.Point(18, 25);
			this.button_active_plc.Name = "button_active_plc";
			this.button_active_plc.Size = new System.Drawing.Size(90, 28);
			this.button_active_plc.TabIndex = 24;
			this.button_active_plc.Text = "激活PLC";
			this.button_active_plc.UseVisualStyleBackColor = true;
			this.button_active_plc.Click += new System.EventHandler(button_active_plc_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 196);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(35, 17);
			this.label_code.TabIndex = 2;
			this.label_code.Text = "代码:";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(54, 193);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(882, 47);
			this.textBox_code.TabIndex = 3;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.groupBox2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "MelsecSerialControl";
			base.Size = new System.Drawing.Size(939, 243);
			base.Load += new System.EventHandler(MelsecSerialControl_Load);
			this.groupBox2.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		private void button_active_plc_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = melsec.ActivePlc();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Active Successful");
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.ToMessageShowString());
			}
			textBox_code.Text = "OperateResult result = " + DemoUtils.PlcDeviceName + ".ActivePlc( );";
		}

		public void SetDevice(IMelsecFxSerial melsecFxSerial, string address)
		{
			melsec = melsecFxSerial;
		}

		private void MelsecSerialControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label_code.Text = "Code:";
			}
		}
	}
}
