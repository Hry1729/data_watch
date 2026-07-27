using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("电机控件，支持颜色的设置，文本信息的设置")]
	public class HslMotor : UserControl
	{
		private StringFormat sf = null;

		private Color colorEdge = Color.FromArgb(89, 91, 96);

		private Color colorCenter = Color.FromArgb(200, 200, 200);

		private Pen penEdge = new Pen(Color.Gray);

		private Color borderColor = Color.Gray;

		private ArrowDirection arrowDirection = ArrowDirection.Right;

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
		[Category("HslControls")]
		[Description("获取或设置电机的主题色")]
		[DefaultValue(typeof(Color), "[89, 91, 96]")]
		public Color EdgeColor
		{
			get
			{
				return colorEdge;
			}
			set
			{
				colorEdge = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置电机的比较淡的中间色")]
		[DefaultValue(typeof(Color), "[200, 200, 200]")]
		public Color CenterColor
		{
			get
			{
				return colorCenter;
			}
			set
			{
				colorCenter = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置电机的边框的颜色")]
		[DefaultValue(typeof(Color), "Gray")]
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				penEdge?.Dispose();
				penEdge = new Pen(borderColor, 1f);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置点击的方向，默认是向右的方向")]
		[DefaultValue(ArrowDirection.Right)]
		public ArrowDirection Direction
		{
			get
			{
				return arrowDirection;
			}
			set
			{
				arrowDirection = value;
				Invalidate();
			}
		}

		public HslMotor()
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
				PaintHslControls(graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			RectangleF rectangleF = default(RectangleF);
			if (arrowDirection == ArrowDirection.Right)
			{
				rectangleF = new RectangleF((float)width * 0.21f, 0f, (float)width * 0.24f, (float)height * 0.22f);
				PaintMain(g, width, height);
			}
			else if (arrowDirection == ArrowDirection.Left)
			{
				rectangleF = new RectangleF((float)width * 0.55f, 0f, (float)width * 0.24f, (float)height * 0.22f);
				Matrix matrix = new Matrix();
				matrix.Scale(-1f, 1f);
				g.MultiplyTransform(matrix);
				g.TranslateTransform(-width, 0f);
				PaintMain(g, width, height);
				g.ResetTransform();
			}
			else if (arrowDirection == ArrowDirection.Up)
			{
				rectangleF = new RectangleF(0f, (float)height * 0.55f, (float)width * 0.22f, (float)height * 0.24f);
				g.RotateTransform(-90f);
				g.TranslateTransform(-height, 0f);
				PaintMain(g, height, width);
				g.ResetTransform();
			}
			else
			{
				rectangleF = new RectangleF((float)width * 0.78f, (float)height * 0.21f, (float)width * 0.22f, (float)height * 0.24f);
				g.TranslateTransform(width, 0f);
				g.RotateTransform(90f);
				PaintMain(g, height, width);
				g.ResetTransform();
			}
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, rectangleF, sf);
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.25f, 1f };
			colorBlend.Colors = new Color[3] { colorEdge, colorCenter, colorEdge };
			using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, height * 0.28f), new PointF(0f, height * 0.92f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush, new RectangleF(width * 0.2f, height * 0.28f, width * 0.41f, height * 0.64f));
			}
			using (LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new PointF(0f, height * 0.25f), new PointF(0f, height * 0.95f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush2.InterpolationColors = colorBlend;
				g.FillEllipse(linearGradientBrush2, new RectangleF(0f, height * 0.25f, width * 0.2f, height * 0.7f));
				g.FillRectangle(linearGradientBrush2, new RectangleF(width * 0.1f, height * 0.25f, width * 0.11f, height * 0.7f));
				g.FillRectangle(linearGradientBrush2, new RectangleF(width * 0.6f, height * 0.25f, width * 0.25f, height * 0.7f));
				g.DrawLine(penEdge, width * 0.21f, height * 0.25f, width * 0.21f, height * 0.95f);
				g.DrawLine(penEdge, width * 0.6f, height * 0.25f, width * 0.6f, height * 0.95f);
			}
			using (LinearGradientBrush linearGradientBrush3 = new LinearGradientBrush(new PointF(0f, height * 0.52f), new PointF(0f, height * 0.68f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush3.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush3, new RectangleF(width * 0.9f, height * 0.52f, width * 0.1f + 1f, height * 0.16f));
			}
			using (LinearGradientBrush linearGradientBrush4 = new LinearGradientBrush(new PointF(0f, height * 0.46f), new PointF(0f, height * 0.74f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush4.InterpolationColors = colorBlend;
				g.FillPolygon(linearGradientBrush4, new PointF[5]
				{
					new PointF(width * 0.84f, height * 0.46f),
					new PointF(width * 0.9f, height * 0.52f),
					new PointF(width * 0.9f, height * 0.68f),
					new PointF(width * 0.84f, height * 0.74f),
					new PointF(width * 0.84f, height * 0.46f)
				});
			}
			using (LinearGradientBrush linearGradientBrush5 = new LinearGradientBrush(new PointF(0f, height * 0.2f), new PointF(0f, height * 1f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush5.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush5, new RectangleF(width * 0.82f, height * 0.2f, width * 0.04f, height * 0.8f));
				g.DrawRectangle(penEdge, width * 0.82f, height * 0.2f, width * 0.04f, height * 0.8f);
			}
			for (int i = 0; i < 12; i++)
			{
				float num = height * 0.64f / 12f;
				float num2 = height * 0.02f;
				if (num2 < 3f)
				{
					num2 = 3f;
				}
				using (LinearGradientBrush linearGradientBrush6 = new LinearGradientBrush(new PointF(0f, height * 0.3f + num * (float)i), new PointF(0f, height * 0.3f + num * (float)i + num2), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
				{
					linearGradientBrush6.InterpolationColors = colorBlend;
					g.FillRectangle(linearGradientBrush6, new RectangleF(width * 0.21f, height * 0.3f + num * (float)i, width * 0.39f, num2));
				}
			}
			using (Brush brush = new SolidBrush(HslHelper.GetColorLight(colorEdge)))
			{
				PointF[] points = new PointF[5]
				{
					new PointF(width * 0.21f, height * 0.22f),
					new PointF(width * 0.23f, height * 0.28f),
					new PointF(width * 0.43f, height * 0.28f),
					new PointF(width * 0.45f, height * 0.22f),
					new PointF(width * 0.21f, height * 0.22f)
				};
				g.FillPolygon(brush, points);
				ColorBlend colorBlend2 = new ColorBlend();
				colorBlend2.Positions = new float[5] { 0f, 0.1f, 0.9f, 0.95f, 1f };
				colorBlend2.Colors = new Color[5]
				{
					colorEdge,
					HslHelper.GetColorLight(colorEdge),
					HslHelper.GetColorLight(colorEdge),
					colorCenter,
					colorEdge
				};
				using (LinearGradientBrush linearGradientBrush7 = new LinearGradientBrush(new PointF(width * 0.21f, 0f), new PointF(width * 0.45f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
				{
					linearGradientBrush7.InterpolationColors = colorBlend2;
					g.FillRectangle(linearGradientBrush7, new RectangleF(width * 0.21f, 0f, width * 0.24f, height * 0.22f));
				}
				g.DrawRectangle(penEdge, width * 0.21f, 0f, width * 0.24f, height * 0.22f);
				g.DrawPolygon(penEdge, points);
			}
			using (LinearGradientBrush linearGradientBrush8 = new LinearGradientBrush(new PointF(width * 0.65f, 0f), new PointF(width * 0.75f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke))
			{
				linearGradientBrush8.InterpolationColors = colorBlend;
				g.FillEllipse(linearGradientBrush8, new RectangleF(width * 0.65f, height * 0.6f, width * 0.1f, width * 0.1f));
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
			base.Name = "HslMotor";
			base.Size = new System.Drawing.Size(275, 134);
			base.ResumeLayout(false);
		}
	}
}
