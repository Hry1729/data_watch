using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个圆形的信号灯，支持设置颜色，是否渐变")]
	public class HslLanternSimple : UserControl
	{
		private Color colorBackground = Color.LimeGreen;

		private Brush brushBackground = new SolidBrush(Color.LimeGreen);

		private StringFormat sf = null;

		private Color centerColor = Color.White;

		private bool isUseGradientColor = false;

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
		[Description("获取或设置信号灯的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LimeGreen")]
		public Color LanternBackground
		{
			get
			{
				return colorBackground;
			}
			set
			{
				colorBackground = value;
				brushBackground?.Dispose();
				brushBackground = new SolidBrush(colorBackground);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置中心点的颜色，当且仅当UseGradientColor属性为True时生效")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
		public Color CenterBackground
		{
			get
			{
				return centerColor;
			}
			set
			{
				centerColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的灯信号是否启用渐变色的画刷")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool UseGradientColor
		{
			get
			{
				return isUseGradientColor;
			}
			set
			{
				isUseGradientColor = value;
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

		public HslLanternSimple()
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

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			int num = Math.Min(width, height) - 1;
			Point point = new Point(num / 2, num / 2);
			g.TranslateTransform(point.X, point.Y);
			float num2 = (float)(point.X * 17) / 20f;
			float num3 = (float)point.X / 20f;
			if (num2 < 2f)
			{
				return;
			}
			RectangleF rect = new RectangleF(0f - num2 - 2f * num3, 0f - num2 - 2f * num3, 2f * num2 + 4f * num3, 2f * num2 + 4f * num3);
			RectangleF rect2 = new RectangleF(0f - num2, 0f - num2, 2f * num2, 2f * num2);
			using (Pen pen = new Pen(colorBackground, num3))
			{
				g.DrawEllipse(pen, rect);
			}
			if (!UseGradientColor)
			{
				g.FillEllipse(brushBackground, rect2);
			}
			else
			{
				GraphicsPath graphicsPath = new GraphicsPath();
				graphicsPath.AddEllipse(0f - num2, 0f - num2, 2f * num2, 2f * num2);
				PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
				pathGradientBrush.CenterPoint = new Point(0, 0);
				ColorBlend colorBlend = new ColorBlend();
				colorBlend.Positions = new float[2] { 0f, 1f };
				colorBlend.Colors = new Color[2] { colorBackground, centerColor };
				pathGradientBrush.InterpolationColors = colorBlend;
				g.FillEllipse(pathGradientBrush, rect2);
				using (Pen pen2 = new Pen(colorBackground))
				{
					g.DrawEllipse(pen2, 0f - num2, 0f - num2, 2f * num2, 2f * num2);
				}
				pathGradientBrush.Dispose();
				graphicsPath.Dispose();
			}
			g.TranslateTransform(-point.X, -point.Y);
			if (height - num <= 0 || string.IsNullOrEmpty(Text))
			{
				return;
			}
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, new Rectangle(0, num, num, height - num), sf);
			}
		}

		public void PerformClick()
		{
			OnClick(new EventArgs());
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
			base.Name = "HslLanternSimple";
			base.Size = new System.Drawing.Size(78, 94);
			base.ResumeLayout(false);
		}
	}
}
