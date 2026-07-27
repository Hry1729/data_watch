using System;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Panasonic;

namespace HslCommunicationDemo.PLC.Panasonic
{
	public class MewtocolControl : UserControl
	{
		private TextBox textBox1;

		private Label label_code;

		private TextBox textBox_code;

		private Button button5;

		private PanasonicMewtocol mewtocol;

		private PanasonicMewtocolOverTcp mewtocolOverTcp;

		public MewtocolControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button5 = new System.Windows.Forms.Button();
			this.label_code = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(3, 37);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(807, 159);
			this.textBox1.TabIndex = 29;
			this.button5.Location = new System.Drawing.Point(3, 3);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(108, 28);
			this.button5.TabIndex = 28;
			this.button5.Text = "Plc-Type";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(3, 199);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 30;
			this.label_code.Text = "代码：";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(53, 199);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(757, 57);
			this.textBox_code.TabIndex = 31;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.button5);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "MewtocolControl";
			base.Size = new System.Drawing.Size(813, 259);
			base.Load += new System.EventHandler(MewtocolControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			OperateResult<string> operateResult = ((mewtocol == null) ? mewtocolOverTcp.ReadPlcType() : mewtocol.ReadPlcType());
			if (operateResult.IsSuccess)
			{
				textBox1.Text = DateTime.Now.ToString() + Environment.NewLine + operateResult.Content;
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult<string> read = " + DemoUtils.PlcDeviceName + ".ReadPlcType( );";
		}

		public void SetDevice(PanasonicMewtocol mewtocol, string address)
		{
			this.mewtocol = mewtocol;
		}

		public void SetDevice(PanasonicMewtocolOverTcp mewtocolOverTcp, string address)
		{
			this.mewtocolOverTcp = mewtocolOverTcp;
		}

		private void MewtocolControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label_code.Text = "Code:";
			}
		}
	}
}
