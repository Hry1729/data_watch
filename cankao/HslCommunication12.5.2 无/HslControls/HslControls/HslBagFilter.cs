using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个袋式除尘器控件，支持设置颜色")]
	public class HslBagFilter : UserControl
	{
		private float widthDesign = 636f;

		private float heightDesign = 562f;

		private StringFormat sf = null;

		private Color boderColor = Color.FromArgb(150, 150, 150);

		private Pen borderPen = new Pen(Color.FromArgb(150, 150, 150));

		private Color edgeColor = Color.FromArgb(176, 176, 176);

		private Color centerColor = Color.FromArgb(229, 229, 229);

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
		[Description("获取或设置分类器控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[176, 176, 176]")]
		public virtual Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置分类器控件的中心颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[229, 229, 229]")]
		public virtual Color CenterColor
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
		[Description("获取或设置分类器控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[150, 150, 150]")]
		public virtual Color BorderColor
		{
			get
			{
				return boderColor;
			}
			set
			{
				boderColor = value;
				borderPen.Dispose();
				borderPen = new Pen(boderColor);
				Invalidate();
			}
		}

		public HslBagFilter()
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
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if (width > 15 && height > 15)
			{
				if ((float)width > widthDesign / heightDesign * (float)height)
				{
					PaintMain(g, widthDesign / heightDesign * (float)height, height);
				}
				else
				{
					PaintMain(g, width, heightDesign / widthDesign * (float)width);
				}
			}
		}

		private void PaintRectangle(Graphics g, RectangleF rectangle, ColorBlend colorBlend)
		{
			rectangle = new RectangleF(rectangle.X * (float)base.Width / widthDesign, rectangle.Y * (float)base.Height / heightDesign, rectangle.Width * (float)base.Width / widthDesign, rectangle.Height * (float)base.Height / heightDesign);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(rectangle.X, 0f), new PointF(rectangle.X + rectangle.Width, 0f), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, rectangle);
			g.DrawRectangle(borderPen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
			linearGradientBrush.Dispose();
		}

		private void PaintPolygon(Graphics g, PointF[] points, ColorBlend colorBlend)
		{
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = new PointF(points[i].X * (float)base.Width / widthDesign, points[i].Y * (float)base.Height / heightDesign);
			}
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(points[0].X, points[0].Y), new PointF(points[1].X, points[0].Y), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillPolygon(linearGradientBrush, points);
			g.DrawPolygon(borderPen, points);
			linearGradientBrush.Dispose();
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.45f, 1f };
			colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
			PaintRectangle(g, new RectangleF(0f, 0f, 50f, 7f), colorBlend);
			PaintRectangle(g, new RectangleF(8f, 7f, 6f, 30f), colorBlend);
			PaintRectangle(g, new RectangleF(38f, 7f, 6f, 30f), colorBlend);
			PaintRectangle(g, new RectangleF(48f, 86f, 504f, 224f), colorBlend);
			PaintRectangle(g, new RectangleF(48f, 310f, 504f, 18f), colorBlend);
			for (int i = 0; i < 10; i++)
			{
				PaintRectangle(g, new RectangleF(92 + 47 * i, 87f, 6f, 222f), colorBlend);
			}
			PaintRectangle(g, new RectangleF(48f, 328f, 18f, 233f), colorBlend);
			PaintRectangle(g, new RectangleF(292f, 328f, 18f, 233f), colorBlend);
			PaintRectangle(g, new RectangleF(537f, 328f, 16f, 233f), colorBlend);
			PaintPolygon(g, new PointF[6]
			{
				new PointF(0f, 35f),
				new PointF(49f, 35f),
				new PointF(49f, 281f),
				new PointF(17f, 281f),
				new PointF(0f, 254f),
				new PointF(0f, 35f)
			}, colorBlend);
			PaintPolygon(g, new PointF[5]
			{
				new PointF(551f, 55f),
				new PointF(635f, 140f),
				new PointF(635f, 226f),
				new PointF(551f, 309f),
				new PointF(551f, 55f)
			}, colorBlend);
			PaintPolygon(g, new PointF[5]
			{
				new PointF(64f, 328f),
				new PointF(292f, 328f),
				new PointF(191f, 502f),
				new PointF(162f, 502f),
				new PointF(64f, 328f)
			}, colorBlend);
			PaintPolygon(g, new PointF[5]
			{
				new PointF(308f, 328f),
				new PointF(536f, 328f),
				new PointF(435f, 502f),
				new PointF(405f, 502f),
				new PointF(308f, 328f)
			}, colorBlend);
			PaintPolygon(g, new PointF[4]
			{
				new PointF(64f, 328f),
				new PointF(97f, 328f),
				new PointF(64f, 361f),
				new PointF(64f, 328f)
			}, colorBlend);
			PaintPolygon(g, new PointF[4]
			{
				new PointF(263f, 328f),
				new PointF(292f, 328f),
				new PointF(292f, 361f),
				new PointF(263f, 328f)
			}, colorBlend);
			PaintPolygon(g, new PointF[4]
			{
				new PointF(308f, 328f),
				new PointF(339f, 328f),
				new PointF(308f, 361f),
				new PointF(308f, 328f)
			}, colorBlend);
			PaintPolygon(g, new PointF[4]
			{
				new PointF(505f, 328f),
				new PointF(537f, 328f),
				new PointF(537f, 361f),
				new PointF(505f, 328f)
			}, colorBlend);
			colorBlend.Colors = new Color[3] { boderColor, centerColor, boderColor };
			PaintRectangle(g, new RectangleF(49f, 55f, 502f, 31f), colorBlend);
			PaintPolygon(g, new PointF[5]
			{
				new PointF(126f, 439f),
				new PointF(228f, 439f),
				new PointF(220f, 452f),
				new PointF(134f, 452f),
				new PointF(126f, 439f)
			}, colorBlend);
			PaintPolygon(g, new PointF[5]
			{
				new PointF(369f, 439f),
				new PointF(472f, 439f),
				new PointF(464f, 452f),
				new PointF(377f, 452f),
				new PointF(369f, 439f)
			}, colorBlend);
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
			base.Name = "HslBagFilter";
			base.Size = new System.Drawing.Size(349, 335);
			base.ResumeLayout(false);
		}
	}
}
