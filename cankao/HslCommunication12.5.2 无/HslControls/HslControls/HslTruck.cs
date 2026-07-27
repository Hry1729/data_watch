using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个卡车控件，支持两种样式，支持车体升降操作")]
	public class HslTruck : UserControl
	{
		private HslPipeTurnDirection directionStyle = HslPipeTurnDirection.Right;

		private float widthDesign = 530f;

		private float heightDesign = 325f;

		private StringFormat sf = null;

		private float truckAngle = 0f;

		private int styleMode = 1;

		private Color themeColor = Color.FromArgb(235, 209, 159);

		private Color boderColor = Color.Silver;

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
		public Color EdgeColor
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
		[Description("获取或设置当前的卡车的倾斜角度，合适范围为0-21")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public float TruckAngle
		{
			get
			{
				return truckAngle;
			}
			set
			{
				truckAngle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置分类器控件的中心颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[229, 229, 229]")]
		public Color CenterColor
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
		[DefaultValue(typeof(Color), "Silver")]
		public Color BorderColor
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

		[Browsable(true)]
		[Description("获取或设置卡车的主题颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[235, 209, 159]")]
		public Color ThemeColor
		{
			get
			{
				return themeColor;
			}
			set
			{
				themeColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置卡车的状态，有两个选择，1，2")]
		[Category("HslControls")]
		[DefaultValue(1)]
		public int TruckStyle
		{
			get
			{
				return styleMode;
			}
			set
			{
				styleMode = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置卡车的朝向，仅支持朝右边或是左边")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslPipeTurnDirection), "Right")]
		public HslPipeTurnDirection TurnDirection
		{
			get
			{
				return directionStyle;
			}
			set
			{
				directionStyle = value;
				Invalidate();
			}
		}

		public HslTruck()
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
			if (directionStyle == HslPipeTurnDirection.Left)
			{
				g.ScaleTransform(-1f, 1f);
				g.TranslateTransform(-width, 0f);
			}
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
			if (directionStyle == HslPipeTurnDirection.Left)
			{
				g.TranslateTransform(width, 0f);
				g.ScaleTransform(-1f, 1f);
			}
		}

		private RectangleF TransRectangleF(RectangleF rectangle, float width, float height)
		{
			return new RectangleF(rectangle.X * width / widthDesign, rectangle.Y * height / heightDesign, rectangle.Width * width / widthDesign, rectangle.Height * height / heightDesign);
		}

		private PointF[] TransPoints(PointF[] points, float width, float height)
		{
			PointF[] array = new PointF[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				array[i] = new PointF(points[i].X * width / widthDesign, points[i].Y * height / heightDesign);
			}
			return array;
		}

		private void PaintPolygon(Graphics g, string points, Color color, float width, float height)
		{
			PointF[] pointsFrom = HslHelper.GetPointsFrom(points, widthDesign, heightDesign, width, height);
			using (SolidBrush brush = new SolidBrush(color))
			{
				g.FillPolygon(brush, pointsFrom);
			}
			g.DrawPolygon(borderPen, pointsFrom);
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.35f, 1f };
			colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
			PaintPolygon(g, "244,274 410,274 410,283 244,283", themeColor, width, height);
			PaintPolygon(g, "410,125 485,125 529,188 529,283 410,283 410,125", themeColor, width, height);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(251, 253, 252)))
			{
				g.FillPolygon(brush, HslHelper.GetPointsFrom("430,137 482,137 509,180 509,195 430,195 430,137", widthDesign, heightDesign, width, height));
			}
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(232, 244, 237)))
			{
				g.FillPolygon(brush2, HslHelper.GetPointsFrom("430,137 482,137 454,195 430,195 430,137", widthDesign, heightDesign, width, height));
			}
			using (Pen pen = new Pen(Color.FromArgb(81, 81, 81), 3f))
			{
				g.DrawPolygon(pen, HslHelper.GetPointsFrom("430,137 482,137 509,180 509,195 430,195 430,137", widthDesign, heightDesign, width, height));
			}
			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(81, 81, 81)))
			{
				g.FillEllipse(brush3, TransRectangleF(new Rectangle(511, 200, 6, 6), width, height));
			}
			using (Pen pen2 = new Pen(Color.FromArgb(212, 186, 137), 5f))
			{
				g.DrawLines(pen2, HslHelper.GetPointsFrom("413,209 527,209 527,280 413,280 413,209", widthDesign, heightDesign, width, height));
				g.DrawArc(pen2, TransRectangleF(new RectangleF(430f, 258f, 58f, 52f), width, height), 180f, 180f);
				g.DrawPolygon(pen2, HslHelper.GetPointsFrom($"350,268 {Math.Cos((double)(truckAngle * 2f) * Math.PI / 360.0) * 298.0 + 52.0},{258.0 - Math.Sin((double)(truckAngle * 2f) * Math.PI / 360.0) * 298.0}", widthDesign, heightDesign, width, height));
			}
			PaintPolygon(g, "510,179 516,179 516,194 510,194 510,179", Color.FromArgb(81, 81, 81), width, height);
			PaintPolygon(g, "513,203 515,203 515,194 513,194 513,203", HslHelper.GetColorLight(Color.FromArgb(81, 81, 81)), width, height);
			PaintPolygon(g, "52,260 396,260 396,275 52,275 52,260", Color.FromArgb(81, 81, 81), width, height);
			PaintTair(g, 460f, 293f, width, height);
			PaintTair(g, 102f, 293f, width, height);
			PaintTair(g, 162f, 293f, width, height);
			g.TranslateTransform(52f * width / widthDesign, 259f * height / heightDesign);
			if (truckAngle != 0f)
			{
				g.RotateTransform(0f - truckAngle);
			}
			if (styleMode == 1)
			{
				PaintPolygon(g, "0,-139 348,-139 348,0 0,0 0,-139", Color.FromArgb(233, 233, 233), width, height);
				PaintPolygon(g, "8,-131 340,-131 340,-8 8,-8 8,-131", themeColor, width, height);
				float[] array = new float[15]
				{
					30f, 40f, 50f, 95f, 105f, 115f, 160f, 170f, 180f, 225f,
					235f, 245f, 290f, 300f, 310f
				};
				for (int i = 0; i < array.Length; i++)
				{
					PaintPolygon(g, $"{array[i]},-131 {array[i] + 5f},-131 {array[i] + 5f},-8 {array[i]},-8 {array[i]},-131", HslHelper.GetColorLight(themeColor), width, height);
				}
			}
			else if (styleMode == 2)
			{
				RectangleF rect = TransRectangleF(new RectangleF(-10f, -130f, 20f, 121f), width, height);
				LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(rect.X, rect.Y), new PointF(rect.X, rect.Y + rect.Height), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
				linearGradientBrush.InterpolationColors = colorBlend;
				g.FillEllipse(linearGradientBrush, rect);
				using (Pen pen3 = new Pen(edgeColor, 1f))
				{
					g.DrawEllipse(pen3, rect);
				}
				g.FillPolygon(linearGradientBrush, HslHelper.GetPointsFrom("348,-130 355,-120 355,-10 348,0 348,-130", widthDesign, heightDesign, width, height));
				g.DrawPolygon(borderPen, HslHelper.GetPointsFrom("348,-130 355,-120 355,-10 348,0 348,-130", widthDesign, heightDesign, width, height));
				linearGradientBrush.Dispose();
				rect = TransRectangleF(new RectangleF(0f, -139f, 348f, 140f), width, height);
				linearGradientBrush = new LinearGradientBrush(new PointF(rect.X, rect.Y), new PointF(rect.X, rect.Y + rect.Height), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
				linearGradientBrush.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush, rect);
				g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
				PaintPolygon(g, "0,-139 348,-139 348,-134 0,-134 0,-139", BorderColor, width, height);
				PaintPolygon(g, "0,-75 348,-75 348,-65 0,-65 0,-75", themeColor, width, height);
				PaintPolygon(g, "0,-50 348,-50 348,-40 0,-40 0,-50", themeColor, width, height);
			}
			if (truckAngle != 0f)
			{
				g.RotateTransform(truckAngle);
			}
			g.TranslateTransform(-52f * width / widthDesign, -259f * height / heightDesign);
		}

		private void PaintTair(Graphics g, float x, float y, float width, float height)
		{
			SolidBrush solidBrush = new SolidBrush(Color.FromArgb(55, 55, 55));
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x - 30f, y - 30f, 60f, 60f), width, height));
			RectangleF rectangle = new RectangleF(x - 20f, y - 20f, 40f, 40f);
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.45f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(150, 150, 150),
				Color.FromArgb(220, 220, 220),
				Color.FromArgb(150, 150, 150)
			};
			PointF[] pointsFrom = HslHelper.GetPointsFrom($"{x + 15f},{y - 20f} {x - 15f},{y + 20f}", widthDesign, heightDesign, width, height);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(pointsFrom[0], pointsFrom[1], Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, TransRectangleF(rectangle, width, height));
			linearGradientBrush.Dispose();
			rectangle = new RectangleF(x - 7f, y - 7f, 14f, 14f);
			using (Pen pen = new Pen(solidBrush, 2f))
			{
				g.DrawEllipse(pen, TransRectangleF(rectangle, width, height));
			}
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x - 11f, y - 15f, 8f, 8f), width, height));
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x - 16f, y + 1f, 8f, 8f), width, height));
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x - 3f, y + 10f, 8f, 8f), width, height));
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x + 8f, y + 2f, 8f, 8f), width, height));
			g.FillEllipse(solidBrush, TransRectangleF(new RectangleF(x + 5f, y - 14f, 8f, 8f), width, height));
			solidBrush.Dispose();
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
			base.Name = "HslTruck";
			base.Size = new System.Drawing.Size(329, 185);
			base.ResumeLayout(false);
		}
	}
}
