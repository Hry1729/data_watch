using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[DefaultEvent("OnSwitchChanged")]
	[Description("一个开关控件，可以在开关两种状态之间进行选择")]
	public class HslSwitch : UserControl
	{
		private Color textForeColor = Color.Black;

		private Brush textForeBrush = new SolidBrush(Color.Black);

		private Color color_switch_background = Color.DimGray;

		private Brush brush_switch_background = new SolidBrush(Color.DimGray);

		private Pen pen_switch_background = new Pen(Color.DimGray, 2f);

		private bool switch_status = false;

		private Color color_switch_foreground = Color.FromArgb(36, 36, 36);

		private Brush brush_switch_foreground = new SolidBrush(Color.FromArgb(36, 36, 36));

		private StringFormat sf = null;

		private string description = "Off;On";

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Transparent")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color BackColor
		{
			get
			{
				return base.BackColor;
			}
			set
			{
				base.BackColor = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的控件是否只读，只读的话就不允许操作")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool ReadOnly { get; set; } = false;

		[Browsable(true)]
		[Description("获取或设置开关按钮的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color SwitchBackground
		{
			get
			{
				return color_switch_background;
			}
			set
			{
				color_switch_background = value;
				brush_switch_background?.Dispose();
				pen_switch_background?.Dispose();
				brush_switch_background = new SolidBrush(color_switch_background);
				pen_switch_background = new Pen(color_switch_background, 2f);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置开关按钮的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[36, 36, 36]")]
		public Color SwitchForeground
		{
			get
			{
				return color_switch_foreground;
			}
			set
			{
				color_switch_foreground = value;
				brush_switch_foreground = new SolidBrush(color_switch_foreground);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置开关按钮的开合状态")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool SwitchStatus
		{
			get
			{
				return switch_status;
			}
			set
			{
				if (value != switch_status)
				{
					switch_status = value;
					Invalidate();
					this.OnSwitchChanged?.Invoke(this, switch_status);
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置两种开关状态的文本描述，例如：Off;On")]
		[Category("HslControls")]
		[DefaultValue("Off;On")]
		public string SwitchStatusDescription
		{
			get
			{
				return description;
			}
			set
			{
				description = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前控件的文本")]
		[Category("HslControls")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Bindable(true)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				base.Text = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前控件的文本的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Black")]
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
				textForeBrush.Dispose();
				textForeBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Category("Action")]
		[Description("点击了按钮开发后触发")]
		public event Action<object, bool> OnSwitchChanged;

		public HslSwitch()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		private Point GetCenterPoint(int width, int height)
		{
			if (height > width)
			{
				return new Point(width / 2, width / 2);
			}
			return new Point(height / 2, height / 2);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(false)
			{
				e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
				e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				PaintHslControls(e.Graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			string[] array = description.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			string text = string.Empty;
			string text2 = string.Empty;
			if (array.Length != 0)
			{
				text = array[0];
			}
			if (array.Length > 1)
			{
				text2 = array[1];
			}
			Point centerPoint = GetCenterPoint(width, height);
			g.TranslateTransform(centerPoint.X, centerPoint.Y);
			int num = 45 * (centerPoint.X * 2 - 30) / 130;
			if (num >= 5)
			{
				Rectangle rect = new Rectangle(-num - 4, -num - 4, 2 * num + 8, 2 * num + 8);
				Rectangle rect2 = new Rectangle(-num, -num, 2 * num, 2 * num);
				g.DrawEllipse(pen_switch_background, rect);
				g.FillEllipse(brush_switch_background, rect2);
				float angle = -36f;
				if (switch_status)
				{
					angle = 36f;
				}
				g.RotateTransform(angle);
				int num2 = 20 * (centerPoint.X * 2 - 30) / 130;
				g.FillRectangle(rect: new Rectangle(-centerPoint.X / 8, -num - num2, centerPoint.X / 4, num * 2 + num2 * 2), brush: brush_switch_foreground);
				g.FillEllipse(rect: new Rectangle(-centerPoint.X / 16, -num - 10, centerPoint.X / 8, centerPoint.X * 3 / 8), brush: switch_status ? Brushes.LimeGreen : Brushes.Tomato);
				g.DrawString(layoutRectangle: new Rectangle(-50, -num - num2 - 15, 100, 15), s: switch_status ? text2 : text, font: Font, brush: switch_status ? Brushes.LimeGreen : Brushes.Tomato, format: sf);
				g.ResetTransform();
				if (height - Math.Min(width, height) > 0 && !string.IsNullOrEmpty(Text))
				{
					g.DrawString(Text, Font, textForeBrush, new Rectangle(0, Math.Min(width, height) - 15, Math.Min(width, height), height - Math.Min(width, height) + 15), sf);
				}
			}
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && !ReadOnly)
			{
				SwitchStatus = !SwitchStatus;
			}
			base.OnClick(e);
		}

		public void ChangeSwitchStatus(bool status)
		{
			switch_status = status;
			Invalidate();
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
			base.SuspendLayout();
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.Transparent;
			this.Cursor = System.Windows.Forms.Cursors.Hand;
			base.Name = "HslSwitch";
			base.ResumeLayout(false);
		}
	}
}
