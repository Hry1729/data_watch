using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个工厂厂房的控件，支持设置颜色")]
	public class HslFactory : UserControl
	{
		private StringFormat sf = null;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

		private Color color1 = Color.FromArgb(160, 216, 239);

		private Brush brush1 = new SolidBrush(Color.FromArgb(160, 216, 239));

		private Color color2 = Color.FromArgb(149, 202, 224);

		private Brush brush2 = new SolidBrush(Color.FromArgb(149, 202, 224));

		private Color color3 = Color.FromArgb(248, 248, 248);

		private Brush brush3 = new SolidBrush(Color.FromArgb(248, 248, 248));

		private Color color4 = Color.FromArgb(181, 181, 181);

		private Brush brush4 = new SolidBrush(Color.FromArgb(181, 181, 181));

		private Color color5 = Color.FromArgb(255, 222, 173);

		private Brush brush5 = new SolidBrush(Color.FromArgb(255, 222, 173));

		private Color color6 = Color.FromArgb(233, 201, 142);

		private Brush brush6 = new SolidBrush(Color.FromArgb(233, 201, 142));

		private Color color7 = Color.FromArgb(158, 158, 158);

		private Brush brush7 = new SolidBrush(Color.FromArgb(158, 158, 158));

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
		[Description("获取或设置自定义颜色1")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[160, 216, 239]")]
		public Color Color1
		{
			get
			{
				return color1;
			}
			set
			{
				color1 = value;
				brush1?.Dispose();
				brush1 = new SolidBrush(color1);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色2")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[149, 202, 224]")]
		public Color Color2
		{
			get
			{
				return color2;
			}
			set
			{
				color2 = value;
				brush2?.Dispose();
				brush2 = new SolidBrush(color2);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色3")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[248, 248, 248]")]
		public Color Color3
		{
			get
			{
				return color3;
			}
			set
			{
				color3 = value;
				brush3?.Dispose();
				brush3 = new SolidBrush(color3);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色4")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[181, 181, 181]")]
		public Color Color4
		{
			get
			{
				return color4;
			}
			set
			{
				color4 = value;
				brush4?.Dispose();
				brush4 = new SolidBrush(color4);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色5")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[255, 222, 173]")]
		public Color Color5
		{
			get
			{
				return color5;
			}
			set
			{
				color5 = value;
				brush5?.Dispose();
				brush5 = new SolidBrush(color5);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色6")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[233, 201, 142]")]
		public Color Color6
		{
			get
			{
				return color6;
			}
			set
			{
				color6 = value;
				brush6?.Dispose();
				brush6 = new SolidBrush(color6);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置自定义颜色7")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[158, 158, 158]")]
		public Color Color7
		{
			get
			{
				return color7;
			}
			set
			{
				color7 = value;
				brush7?.Dispose();
				brush7 = new SolidBrush(color7);
				Invalidate();
			}
		}

		public HslFactory()
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
			if(false)
			{
				Graphics graphics = e.Graphics;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				if (hslValvesStyle == HslDirectionStyle.Horizontal)
				{
					PaintMain(graphics, base.Width, base.Height);
				}
				else
				{
					graphics.TranslateTransform(base.Width, 0f);
					graphics.RotateTransform(90f);
					PaintMain(graphics, base.Height, base.Width);
					graphics.ResetTransform();
				}
				base.OnPaint(e);
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			PointF pointF = new PointF(width * 0.2f, 0f);
			PointF pointF2 = new PointF(width * 0.4f, 0f);
			PointF pointF3 = new PointF(width - 1f, height * 0.35f);
			PointF pointF4 = new PointF(width * 0.8f, height * 0.35f);
			PointF pointF5 = new PointF(0f, height * 0.25f);
			PointF pointF6 = new PointF(width * 0.6f, height * 0.6f);
			PointF pointF7 = new PointF(pointF5.X, pointF5.Y + height * 0.03f);
			PointF pointF8 = new PointF(pointF6.X, pointF6.Y + height * 0.03f);
			PointF pointF9 = new PointF(pointF3.X, pointF3.Y + height * 0.03f);
			PointF pointF10 = new PointF(pointF4.X, pointF4.Y + height * 0.03f);
			PointF pointF11 = new PointF(width * 0.03f, pointF7.Y);
			PointF pointF12 = new PointF(pointF6.X + width * 0.03f, pointF3.Y);
			PointF pointF13 = new PointF(pointF6.X + width * 0.03f, height - 1f);
			PointF pointF14 = new PointF(width * 0.03f, height * 0.65f);
			PointF pointF15 = new PointF(width * 0.97f, height * 0.35f);
			PointF pointF16 = new PointF(width * 0.97f, height * 0.75f);
			g.FillPolygon(brush5, new PointF[4] { pointF11, pointF12, pointF13, pointF14 });
			g.FillPolygon(brush6, new PointF[4] { pointF12, pointF13, pointF16, pointF15 });
			g.FillPolygon(brush2, new PointF[4] { pointF, pointF2, pointF3, pointF4 });
			g.FillPolygon(brush1, new PointF[4] { pointF, pointF4, pointF6, pointF5 });
			g.FillPolygon(brush3, new PointF[4] { pointF5, pointF6, pointF8, pointF7 });
			g.FillPolygon(brush4, new PointF[6] { pointF6, pointF4, pointF3, pointF9, pointF10, pointF8 });
			PointF[] points = new PointF[4]
			{
				new PointF(width * 0.12f, height * 0.46f),
				new PointF(width * 0.12f, height * 0.54f),
				new PointF(width * 0.22f, height * 0.6f),
				new PointF(width * 0.22f, height * 0.52f)
			};
			g.FillPolygon(brush1, points);
			AddOffect(points, width * 0.14f, height * 0.08f);
			g.FillPolygon(brush1, points);
			AddOffect(points, width * 0.14f, height * 0.08f);
			g.FillPolygon(brush1, points);
			g.FillPolygon(points: new PointF[4]
			{
				new PointF(width * 0.72f, height * 0.934f),
				new PointF(width * 0.91f, height * 0.8f),
				new PointF(width * 0.91f, height * 0.6f),
				new PointF(width * 0.72f, height * 0.734f)
			}, brush: brush7);
		}

		private void AddOffect(PointF[] points, float x, float y)
		{
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = new PointF(points[i].X + x, points[i].Y + y);
			}
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
			base.Name = "HslFactory";
			base.Size = new System.Drawing.Size(325, 163);
			base.ResumeLayout(false);
		}
	}
}
