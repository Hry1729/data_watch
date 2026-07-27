using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslControls.Forms
{
	public class FormHslDigitalInput : Form
	{
		private IContainer components = null;

		private HslDigitalInput hslDigitalInput1;

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Action<string> OnOk
		{
			get
			{
				return hslDigitalInput1.OnOk;
			}
			set
			{
				hslDigitalInput1.OnOk = value;
			}
		}

		public HslDigitalInput DigitalInput => hslDigitalInput1;

		public FormHslDigitalInput(Action<string> onOk = null)
		{
			InitializeComponent();
			OnOk = onOk;
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
			this.hslDigitalInput1 = new HslControls.HslDigitalInput();
			base.SuspendLayout();
			this.hslDigitalInput1.BackColor = System.Drawing.Color.Linen;
			this.hslDigitalInput1.ButtonColor = System.Drawing.Color.Beige;
			this.hslDigitalInput1.DisplayBackColor = System.Drawing.Color.FromArgb(210, 225, 213);
			this.hslDigitalInput1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslDigitalInput1.EnableNegative = true;
			this.hslDigitalInput1.EnableSpot = true;
			this.hslDigitalInput1.Font = new System.Drawing.Font("微软雅黑", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.hslDigitalInput1.ForeColor = System.Drawing.Color.Black;
			this.hslDigitalInput1.LedBackColor = System.Drawing.Color.FromArgb(210, 225, 213);
			this.hslDigitalInput1.LedForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslDigitalInput1.Location = new System.Drawing.Point(0, 0);
			this.hslDigitalInput1.Name = "hslDigitalInput1";
			this.hslDigitalInput1.Size = new System.Drawing.Size(276, 308);
			this.hslDigitalInput1.TabIndex = 0;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(276, 308);
			base.Controls.Add(this.hslDigitalInput1);
			this.Font = new System.Drawing.Font("微软雅黑", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(292, 347);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(292, 347);
			base.Name = "FormHslDigitalInput";
			base.ShowIcon = false;
			base.ShowInTaskbar = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "数字键盘";
			base.ResumeLayout(false);
		}
	}
}
