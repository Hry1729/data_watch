using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("数字键盘控件，支持数字，小数点，支持掩码操作，可用来触摸输入一些数据信息")]
	public class HslDigitalInput : UserControl
	{
		private StringBuilder sb = new StringBuilder("0");

		private Color buttonColor = Color.Lavender;

		private Color textForeColor = Color.DimGray;

		private bool enableSpot = true;

		private bool enableNegative = true;

		private char digitalMask = '\0';

		private IContainer components = null;

		private SplitContainer splitContainer1;

		private Panel panel1;

		private HslLedDisplay hslLedDisplay1;

		private TableLayoutPanel tableLayoutPanel1;

		private HslButton hslButton15;

		private HslButton hslButton14;

		private HslButton hslButton13;

		private HslButton hslButton12;

		private HslButton hslButton11;

		private HslButton hslButton10;

		private HslButton hslButton9;

		private HslButton hslButton8;

		private HslButton hslButton7;

		private HslButton hslButton6;

		private HslButton hslButton5;

		private HslButton hslButton4;

		private HslButton hslButton3;

		private HslButton hslButton2;

		private HslButton hslButton1;

		public override Font Font
		{
			get
			{
				return base.Font;
			}
			set
			{
				base.Font = value;
				hslButton1.Font = value;
				hslButton2.Font = value;
				hslButton3.Font = value;
				hslButton4.Font = value;
				hslButton5.Font = value;
				hslButton6.Font = value;
				hslButton7.Font = value;
				hslButton8.Font = value;
				hslButton9.Font = value;
				hslButton10.Font = value;
				hslButton11.Font = value;
				hslButton12.Font = value;
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Action<string> OnOk { get; set; }

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Func<string, bool> InputCheck { get; set; }

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[46, 46, 46]")]
		public Color LedBackColor
		{
			get
			{
				return hslLedDisplay1.BackColor;
			}
			set
			{
				hslLedDisplay1.BackColor = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue(8)]
		[Description("获取或设置数码管显示的位数")]
		public int DisplayNumber
		{
			get
			{
				return hslLedDisplay1.DisplayNumber;
			}
			set
			{
				hslLedDisplay1.DisplayNumber = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue(6)]
		[Description("获取或设置数码管显示的大小")]
		public int LedNumberSize
		{
			get
			{
				return hslLedDisplay1.LedNumberSize;
			}
			set
			{
				hslLedDisplay1.LedNumberSize = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue("0")]
		[Description("获取或设置数码管显示的内容")]
		public string DisplayText
		{
			get
			{
				return sb.ToString();
			}
			set
			{
				sb = new StringBuilder(value);
				hslLedDisplay1.DisplayText = GetMaskTest(value);
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[62, 62, 62]")]
		[Description("获取或设置数码管数字的背景色")]
		public Color DisplayBackColor
		{
			get
			{
				return hslLedDisplay1.DisplayBackColor;
			}
			set
			{
				hslLedDisplay1.DisplayBackColor = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		[Description("获取或设置数码管数字的前景色")]
		public Color LedForeColor
		{
			get
			{
				return hslLedDisplay1.ForeColor;
			}
			set
			{
				hslLedDisplay1.ForeColor = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue("")]
		[Description("获取或设置数码管数字的单位文本")]
		public string UnitText
		{
			get
			{
				return hslLedDisplay1.UnitText;
			}
			set
			{
				hslLedDisplay1.UnitText = value;
			}
		}

		[Category("HslControls")]
		[DefaultValue('\0')]
		[Description("当需要设置密码的时候，设置为掩码，就不显示真实的数据信息")]
		public char MaskChar
		{
			get
			{
				return digitalMask;
			}
			set
			{
				digitalMask = value;
				DisplayText = sb.ToString();
			}
		}

		[Category("HslControls")]
		[DefaultValue(10)]
		[Description("获取或设置数码管两端的空闲长度")]
		public int LeftRightOffect
		{
			get
			{
				return hslLedDisplay1.LeftRightOffect;
			}
			set
			{
				hslLedDisplay1.LeftRightOffect = value;
			}
		}

		public bool EnableSpot
		{
			get
			{
				return enableSpot;
			}
			set
			{
				enableSpot = value;
				hslButton12.Enabled = value;
			}
		}

		public bool EnableNegative
		{
			get
			{
				return enableNegative;
			}
			set
			{
				enableNegative = value;
				hslButton10.Enabled = value;
			}
		}

		[Description("获取或设置所有按钮的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Lavender")]
		public Color ButtonColor
		{
			get
			{
				return buttonColor;
			}
			set
			{
				buttonColor = value;
				hslButton1.OriginalColor = value;
				hslButton2.OriginalColor = value;
				hslButton3.OriginalColor = value;
				hslButton4.OriginalColor = value;
				hslButton5.OriginalColor = value;
				hslButton6.OriginalColor = value;
				hslButton7.OriginalColor = value;
				hslButton8.OriginalColor = value;
				hslButton9.OriginalColor = value;
				hslButton10.OriginalColor = value;
				hslButton11.OriginalColor = value;
				hslButton12.OriginalColor = value;
				hslButton13.OriginalColor = value;
				hslButton14.OriginalColor = value;
				hslButton15.OriginalColor = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前控件的文本的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color ForeColor
		{
			get
			{
				return textForeColor;
			}
			set
			{
				textForeColor = value;
				hslButton1.ForeColor = value;
				hslButton2.ForeColor = value;
				hslButton3.ForeColor = value;
				hslButton4.ForeColor = value;
				hslButton5.ForeColor = value;
				hslButton6.ForeColor = value;
				hslButton7.ForeColor = value;
				hslButton8.ForeColor = value;
				hslButton9.ForeColor = value;
				hslButton10.ForeColor = value;
				hslButton11.ForeColor = value;
				hslButton12.ForeColor = value;
				hslButton13.ForeColor = value;
				hslButton14.ForeColor = value;
				hslButton15.ForeColor = value;
				Invalidate();
			}
		}

		public HslDigitalInput()
		{
			InitializeComponent();
			hslButton1.Click += HslButton1_Click;
			hslButton2.Click += HslButton2_Click;
			hslButton3.Click += HslButton3_Click;
			hslButton4.Click += HslButton4_Click;
			hslButton5.Click += HslButton5_Click;
			hslButton6.Click += HslButton6_Click;
			hslButton7.Click += HslButton7_Click;
			hslButton8.Click += HslButton8_Click;
			hslButton9.Click += HslButton9_Click;
			hslButton10.Click += HslButton10_Click;
			hslButton11.Click += HslButton11_Click;
			hslButton12.Click += HslButton12_Click;
			hslButton13.Click += HslButton13_Click;
			hslButton14.Click += HslButton14_Click;
			hslButton15.Click += HslButton15_Click;
			tableLayoutPanel1.Click += TableLayoutPanel1_Click;
		}

		private void TableLayoutPanel1_Click(object sender, EventArgs e)
		{
			OnClick(e);
		}

		private void HslButton15_Click(object sender, EventArgs e)
		{
			if (sb.Length > 0)
			{
				sb.Remove(sb.Length - 1, 1);
			}
			if (sb.Length == 0)
			{
				sb.Append("0");
			}
			DisplayText = sb.ToString();
		}

		private void HslButton14_Click(object sender, EventArgs e)
		{
			sb = new StringBuilder();
			sb.Append("0");
			DisplayText = sb.ToString();
		}

		private void HslButton13_Click(object sender, EventArgs e)
		{
			if (sb.ToString().EndsWith("."))
			{
				sb.Remove(sb.Length - 1, 1);
			}
			if (InputCheck != null && !InputCheck(sb.ToString()))
			{
				MessageBox.Show("Input Wrong, Please input again!");
			}
			else
			{
				OnOk?.Invoke(sb.ToString());
			}
		}

		private void HslButton12_Click(object sender, EventArgs e)
		{
			if (sb.ToString().IndexOf('.') < 0)
			{
				sb.Append(".");
				DisplayText = sb.ToString();
			}
		}

		private void HslButton11_Click(object sender, EventArgs e)
		{
			if (sb.ToString().IndexOf('.') >= 0 || (!sb.ToString().StartsWith("0") && !sb.ToString().StartsWith("-0")))
			{
				sb.Append("0");
				DisplayText = sb.ToString();
			}
		}

		private void HslButton10_Click(object sender, EventArgs e)
		{
			if (sb.ToString().StartsWith("-"))
			{
				sb.Remove(0, 1);
			}
			else
			{
				sb.Insert(0, "-");
			}
			DisplayText = sb.ToString();
		}

		private void LedAddText(string number)
		{
			if (sb.ToString().IndexOf(".") <= 0)
			{
				if (sb.ToString().StartsWith("0"))
				{
					sb.Remove(0, 1);
				}
				if (sb.ToString().StartsWith("-0"))
				{
					sb.Remove(1, 1);
				}
			}
			sb.Append(number);
			DisplayText = sb.ToString();
		}

		private void HslButton9_Click(object sender, EventArgs e)
		{
			LedAddText("9");
		}

		private void HslButton8_Click(object sender, EventArgs e)
		{
			LedAddText("8");
		}

		private void HslButton7_Click(object sender, EventArgs e)
		{
			LedAddText("7");
		}

		private void HslButton6_Click(object sender, EventArgs e)
		{
			LedAddText("6");
		}

		private void HslButton5_Click(object sender, EventArgs e)
		{
			LedAddText("5");
		}

		private void HslButton4_Click(object sender, EventArgs e)
		{
			LedAddText("4");
		}

		private void HslButton3_Click(object sender, EventArgs e)
		{
			LedAddText("3");
		}

		private void HslButton2_Click(object sender, EventArgs e)
		{
			LedAddText("2");
		}

		private void HslButton1_Click(object sender, EventArgs e)
		{
			LedAddText("1");
		}

		private string GetMaskTest(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			if (digitalMask > '\0')
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < value.Length; i++)
				{
					if (char.IsDigit(value[i]))
					{
						stringBuilder.Append(digitalMask);
					}
					else
					{
						stringBuilder.Append(value[i]);
					}
				}
				return stringBuilder.ToString();
			}
			return value;
		}

		private void HslDigitalInput_Load(object sender, EventArgs e)
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
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panel1 = new System.Windows.Forms.Panel();
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.hslLedDisplay1 = new HslControls.HslLedDisplay();
			this.hslButton15 = new HslControls.HslButton();
			this.hslButton14 = new HslControls.HslButton();
			this.hslButton13 = new HslControls.HslButton();
			this.hslButton12 = new HslControls.HslButton();
			this.hslButton11 = new HslControls.HslButton();
			this.hslButton10 = new HslControls.HslButton();
			this.hslButton9 = new HslControls.HslButton();
			this.hslButton8 = new HslControls.HslButton();
			this.hslButton7 = new HslControls.HslButton();
			this.hslButton6 = new HslControls.HslButton();
			this.hslButton5 = new HslControls.HslButton();
			this.hslButton4 = new HslControls.HslButton();
			this.hslButton3 = new HslControls.HslButton();
			this.hslButton2 = new HslControls.HslButton();
			this.hslButton1 = new HslControls.HslButton();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.panel1.SuspendLayout();
			this.tableLayoutPanel1.SuspendLayout();
			base.SuspendLayout();
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			this.splitContainer1.Panel1.Controls.Add(this.panel1);
			this.splitContainer1.Panel2.Controls.Add(this.tableLayoutPanel1);
			this.splitContainer1.Size = new System.Drawing.Size(286, 305);
			this.splitContainer1.SplitterDistance = 56;
			this.splitContainer1.TabIndex = 0;
			this.panel1.Controls.Add(this.hslLedDisplay1);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(286, 56);
			this.panel1.TabIndex = 0;
			this.tableLayoutPanel1.ColumnCount = 3;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333f));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334f));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334f));
			this.tableLayoutPanel1.Controls.Add(this.hslButton15, 0, 4);
			this.tableLayoutPanel1.Controls.Add(this.hslButton14, 0, 4);
			this.tableLayoutPanel1.Controls.Add(this.hslButton13, 0, 4);
			this.tableLayoutPanel1.Controls.Add(this.hslButton12, 2, 3);
			this.tableLayoutPanel1.Controls.Add(this.hslButton11, 1, 3);
			this.tableLayoutPanel1.Controls.Add(this.hslButton10, 0, 3);
			this.tableLayoutPanel1.Controls.Add(this.hslButton9, 2, 2);
			this.tableLayoutPanel1.Controls.Add(this.hslButton8, 1, 2);
			this.tableLayoutPanel1.Controls.Add(this.hslButton7, 0, 2);
			this.tableLayoutPanel1.Controls.Add(this.hslButton6, 2, 1);
			this.tableLayoutPanel1.Controls.Add(this.hslButton5, 1, 1);
			this.tableLayoutPanel1.Controls.Add(this.hslButton4, 0, 1);
			this.tableLayoutPanel1.Controls.Add(this.hslButton3, 2, 0);
			this.tableLayoutPanel1.Controls.Add(this.hslButton2, 1, 0);
			this.tableLayoutPanel1.Controls.Add(this.hslButton1, 0, 0);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 5;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20f));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20f));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20f));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20f));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20f));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20f));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(286, 245);
			this.tableLayoutPanel1.TabIndex = 0;
			this.hslLedDisplay1.BackColor = System.Drawing.Color.FromArgb(46, 46, 46);
			this.hslLedDisplay1.DisplayBackColor = System.Drawing.Color.FromArgb(62, 62, 62);
			this.hslLedDisplay1.DisplayNumber = 8;
			this.hslLedDisplay1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslLedDisplay1.LedNumberSize = 6;
			this.hslLedDisplay1.Location = new System.Drawing.Point(0, 0);
			this.hslLedDisplay1.Name = "hslLedDisplay1";
			this.hslLedDisplay1.Size = new System.Drawing.Size(286, 56);
			this.hslLedDisplay1.TabIndex = 0;
			this.hslLedDisplay1.DisplayText = "0";
			this.hslButton15.CustomerInformation = null;
			this.hslButton15.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton15.Location = new System.Drawing.Point(3, 199);
			this.hslButton15.Name = "hslButton15";
			this.hslButton15.Size = new System.Drawing.Size(89, 43);
			this.hslButton15.TabIndex = 14;
			this.hslButton15.Text = "Back";
			this.hslButton14.CustomerInformation = null;
			this.hslButton14.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton14.Location = new System.Drawing.Point(98, 199);
			this.hslButton14.Name = "hslButton14";
			this.hslButton14.Size = new System.Drawing.Size(89, 43);
			this.hslButton14.TabIndex = 13;
			this.hslButton14.Text = "Clear";
			this.hslButton13.CustomerInformation = null;
			this.hslButton13.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton13.Location = new System.Drawing.Point(193, 199);
			this.hslButton13.Name = "hslButton13";
			this.hslButton13.Size = new System.Drawing.Size(90, 43);
			this.hslButton13.TabIndex = 12;
			this.hslButton13.Text = "OK";
			this.hslButton12.CustomerInformation = null;
			this.hslButton12.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton12.Location = new System.Drawing.Point(193, 150);
			this.hslButton12.Name = "hslButton12";
			this.hslButton12.Size = new System.Drawing.Size(90, 43);
			this.hslButton12.TabIndex = 11;
			this.hslButton12.Text = ".";
			this.hslButton11.CustomerInformation = null;
			this.hslButton11.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton11.Location = new System.Drawing.Point(98, 150);
			this.hslButton11.Name = "hslButton11";
			this.hslButton11.Size = new System.Drawing.Size(89, 43);
			this.hslButton11.TabIndex = 10;
			this.hslButton11.Text = "0";
			this.hslButton10.CustomerInformation = null;
			this.hslButton10.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton10.Location = new System.Drawing.Point(3, 150);
			this.hslButton10.Name = "hslButton10";
			this.hslButton10.Size = new System.Drawing.Size(89, 43);
			this.hslButton10.TabIndex = 9;
			this.hslButton10.Text = "-";
			this.hslButton9.CustomerInformation = null;
			this.hslButton9.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton9.Location = new System.Drawing.Point(193, 101);
			this.hslButton9.Name = "hslButton9";
			this.hslButton9.Size = new System.Drawing.Size(90, 43);
			this.hslButton9.TabIndex = 8;
			this.hslButton9.Text = "9";
			this.hslButton8.CustomerInformation = null;
			this.hslButton8.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton8.Location = new System.Drawing.Point(98, 101);
			this.hslButton8.Name = "hslButton8";
			this.hslButton8.Size = new System.Drawing.Size(89, 43);
			this.hslButton8.TabIndex = 7;
			this.hslButton8.Text = "8";
			this.hslButton7.CustomerInformation = null;
			this.hslButton7.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton7.Location = new System.Drawing.Point(3, 101);
			this.hslButton7.Name = "hslButton7";
			this.hslButton7.Size = new System.Drawing.Size(89, 43);
			this.hslButton7.TabIndex = 6;
			this.hslButton7.Text = "7";
			this.hslButton6.CustomerInformation = null;
			this.hslButton6.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton6.Location = new System.Drawing.Point(193, 52);
			this.hslButton6.Name = "hslButton6";
			this.hslButton6.Size = new System.Drawing.Size(90, 43);
			this.hslButton6.TabIndex = 5;
			this.hslButton6.Text = "6";
			this.hslButton5.CustomerInformation = null;
			this.hslButton5.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton5.Location = new System.Drawing.Point(98, 52);
			this.hslButton5.Name = "hslButton5";
			this.hslButton5.Size = new System.Drawing.Size(89, 43);
			this.hslButton5.TabIndex = 4;
			this.hslButton5.Text = "5";
			this.hslButton4.CustomerInformation = null;
			this.hslButton4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton4.Location = new System.Drawing.Point(3, 52);
			this.hslButton4.Name = "hslButton4";
			this.hslButton4.Size = new System.Drawing.Size(89, 43);
			this.hslButton4.TabIndex = 3;
			this.hslButton4.Text = "4";
			this.hslButton3.CustomerInformation = null;
			this.hslButton3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton3.Location = new System.Drawing.Point(193, 3);
			this.hslButton3.Name = "hslButton3";
			this.hslButton3.Size = new System.Drawing.Size(90, 43);
			this.hslButton3.TabIndex = 2;
			this.hslButton3.Text = "3";
			this.hslButton2.CustomerInformation = null;
			this.hslButton2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton2.Location = new System.Drawing.Point(98, 3);
			this.hslButton2.Name = "hslButton2";
			this.hslButton2.Size = new System.Drawing.Size(89, 43);
			this.hslButton2.TabIndex = 1;
			this.hslButton2.Text = "2";
			this.hslButton1.CustomerInformation = null;
			this.hslButton1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslButton1.Location = new System.Drawing.Point(3, 3);
			this.hslButton1.Name = "hslButton1";
			this.hslButton1.Size = new System.Drawing.Size(89, 43);
			this.hslButton1.TabIndex = 0;
			this.hslButton1.Text = "1";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.Transparent;
			base.Controls.Add(this.splitContainer1);
			base.Name = "HslDigitalInput";
			base.Size = new System.Drawing.Size(286, 305);
			base.Load += new System.EventHandler(HslDigitalInput_Load);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.tableLayoutPanel1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
