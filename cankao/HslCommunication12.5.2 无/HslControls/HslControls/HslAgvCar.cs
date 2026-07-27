using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("Agv 小车控件")]
	public class HslAgvCar : UserControl
	{
		private Color borderColor = Color.Gray;

		private Pen borderPen = new Pen(Color.Gray, 1f);

		private SolidBrush carBrush = new SolidBrush(Color.Tomato);

		private SolidBrush darkBlack = new SolidBrush(Color.FromArgb(52, 52, 52));

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置当前AGV小车的颜色信息")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		public virtual Color AgvColor
		{
			get
			{
				return carBrush.Color;
			}
			set
			{
				carBrush.Color = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的轮胎的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "52, 52, 52")]
		public virtual Color TireColor
		{
			get
			{
				return darkBlack.Color;
			}
			set
			{
				darkBlack.Color = value;
				Invalidate();
			}
		}

		public HslAgvCar()
		{
			InitializeComponent();
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
				PaintHslControls(graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				PaintMain(g, width, height);
			}
		}

		private void PaintMain(Graphics g, int width, int height)
		{
			int num = (int)((float)height * 0.08f);
			if (width < 50 && height < 50)
			{
				num = (int)((float)height * 0.12f);
			}
			int num2 = (int)((float)width * 0.04f);
			int num3 = (int)((float)width * 0.1f);
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddArc(new Rectangle(width - num3 - num3 + 1, num, num3 + num3 - 2, num3 + num3 - 2), -90f, 90f);
			graphicsPath.AddLine(new Point(width - 1, num + num3), new Point(width - 1, height - num - num3));
			graphicsPath.AddArc(new Rectangle(width - num3 - num3 + 1, height - num - num3 - num3, num3 + num3 - 2, num3 + num3), 0f, 90f);
			Rectangle rect = new Rectangle(num2, num, width - num2 - num3, height - num - num);
			using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, height - num), new PointF(0f, num), carBrush.Color, HslHelper.GetColorOffset(carBrush.Color, height - num)))
			{
				ColorBlend colorBlend = new ColorBlend();
				colorBlend.Positions = new float[4] { 0f, 0.1f, 0.9f, 1f };
				colorBlend.Colors = new Color[4]
				{
					HslHelper.GetColorLight(carBrush.Color),
					carBrush.Color,
					carBrush.Color,
					HslHelper.GetColorLight(carBrush.Color)
				};
				linearGradientBrush.InterpolationColors = colorBlend;
				g.FillPath(linearGradientBrush, graphicsPath);
				g.DrawPath(borderPen, graphicsPath);
				g.FillRectangle(linearGradientBrush, rect);
				g.DrawRectangle(borderPen, rect);
				using (Pen pen = new Pen(HslHelper.GetColorLight(carBrush.Color, -15)))
				{
					g.DrawLine(pen, new Point(width - num3, num + 1), new Point(width - num3, height - num - 2));
				}
			}
			int num4 = (int)((float)width * 0.12f);
			int num5 = (int)((float)width * 0.28f);
			Rectangle rect2 = new Rectangle(rect.X + rect.Width / 2 - num5 - num4 / 2, 0, num4, num * 2);
			g.FillRectangle(darkBlack, rect2);
			rect2.X = rect.X + rect.Width / 2 + num5 - num4 / 2;
			g.FillRectangle(darkBlack, rect2);
			rect2.X = rect.X + rect.Width / 2 - num5 - num4 / 2;
			rect2.Y = height - rect2.Height;
			g.FillRectangle(darkBlack, rect2);
			rect2.X = rect.X + rect.Width / 2 + num5 - num4 / 2;
			g.FillRectangle(darkBlack, rect2);
			Point point = new Point(rect.X + rect.Width / 2, height / 2);
			g.TranslateTransform(point.X, point.Y);
			int num6 = (int)((float)height * 0.36f);
			g.FillEllipse(Brushes.LightGray, new Rectangle(-num6, -num6, num6 + num6, num6 + num6));
			g.DrawEllipse(Pens.Gray, new Rectangle(-num6, -num6, num6 + num6, num6 + num6));
			num6 = (int)((float)height * 0.33f);
			for (int i = 0; i < 6; i++)
			{
				g.FillPie(darkBlack, new Rectangle(-num6, -num6, num6 + num6, num6 + num6), -115 + 60 * i, 50f);
			}
			num6 = (int)((float)height * 0.24f);
			g.FillEllipse(Brushes.LightGray, new Rectangle(-num6, -num6, num6 + num6, num6 + num6));
			num6 = (int)((float)height * 0.04f);
			for (int j = 0; j < 4; j++)
			{
				g.FillEllipse(darkBlack, new Rectangle((int)((float)height * 0.16f) - num6, -num6, num6 * 2, num6 * 2));
				g.RotateTransform(90f);
			}
			num6 = (int)((float)height * 0.06f);
			g.FillEllipse(carBrush, new Rectangle(-num6, -num6, num6 + num6, num6 + num6));
			g.TranslateTransform(-point.X, -point.Y);
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
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "HslAgvCar";
			base.Size = new System.Drawing.Size(58, 65);
			base.ResumeLayout(false);
		}
	}
}
