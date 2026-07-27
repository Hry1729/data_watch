using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个真空泵控件，支持两个方向的旋转操作，支持颜色设置")]
	public class HslVacuumPump : UserControl
	{
		private StringFormat sf = null;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Vertical;

		private int entrance = 1;

		private int export = 4;

		private Color color1 = Color.FromArgb(0, 151, 0);

		private Brush brush1 = new SolidBrush(Color.FromArgb(0, 151, 0));

		private Color color2 = Color.FromArgb(76, 76, 76);

		private Brush brush2 = new SolidBrush(Color.FromArgb(76, 76, 76));

		private Color color3 = Color.FromArgb(255, 255, 255);

		private Brush brush3 = new SolidBrush(Color.FromArgb(255, 255, 255));

		private Color color4 = Color.FromArgb(151, 151, 151);

		private Brush brush4 = new SolidBrush(Color.FromArgb(151, 151, 151));

		private float moveSpeed = 0.3f;

		private float startAngle = -47f;

		private Timer timer = null;

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
		[Description("获取或设置泵控件是否是横向的还是纵向的")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslDirectionStyle), "Vertical")]
		public HslDirectionStyle PumpStyle
		{
			get
			{
				return hslValvesStyle;
			}
			set
			{
				hslValvesStyle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置传送带流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0.3f)]
		public float MoveSpeed
		{
			get
			{
				return moveSpeed;
			}
			set
			{
				moveSpeed = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色1")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[0, 151, 0]")]
		public Color Color1
		{
			get
			{
				return color1;
			}
			set
			{
				color1 = value;
				brush1.Dispose();
				brush1 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色2")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[76, 76, 76]")]
		public Color Color2
		{
			get
			{
				return color2;
			}
			set
			{
				color2 = value;
				brush2.Dispose();
				brush2 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色3")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[255, 255, 255]")]
		public Color Color3
		{
			get
			{
				return color3;
			}
			set
			{
				color3 = value;
				brush3.Dispose();
				brush3 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色4")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[151, 151, 151]")]
		public Color Color4
		{
			get
			{
				return color4;
			}
			set
			{
				color4 = value;
				brush4.Dispose();
				brush4 = new SolidBrush(value);
				Invalidate();
			}
		}

		public HslVacuumPump()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			timer = new Timer();
			timer.Interval = 50;
			timer.Tick += Timer_Tick;
			timer.Start();
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
			float num = 0f;
			float num2 = 0f;
			if (hslValvesStyle == HslDirectionStyle.Vertical)
			{
				if (height >= width * 112 / 80)
				{
					num = width - 1;
					num2 = width * 112 / 80 - 1;
				}
				else
				{
					num2 = height - 1;
					num = height * 80 / 112 - 1;
				}
				PaintMain(g, num, num2);
				return;
			}
			if (width >= height * 112 / 80)
			{
				num2 = height - 1;
				num = height * 112 / 80 - 1;
			}
			else
			{
				num = width - 1;
				num2 = width * 80 / 112 - 1;
			}
			g.TranslateTransform(num, 0f);
			g.RotateTransform(90f);
			PaintMain(g, num2, num);
			g.ResetTransform();
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.51f, 1f };
			colorBlend.Colors = new Color[3]
			{
				color1,
				Color.WhiteSmoke,
				color1
			};
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(width, 0f), Color.Wheat, Color.White);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, new RectangleF(0f, 0f, width, width));
			g.FillEllipse(linearGradientBrush, new RectangleF(0f, height - width, width, width));
			if (height - width > 0f)
			{
				g.FillRectangle(linearGradientBrush, new RectangleF(0f, width / 2f, width, height - width));
			}
			g.FillEllipse(brush2, new RectangleF(width * 0.15f, width * 0.15f, width * 0.7f, width * 0.7f));
			g.FillEllipse(brush2, new RectangleF(width * 0.15f, height - width + width * 0.15f, width * 0.7f, width * 0.7f));
			if (height - width > 0f)
			{
				g.FillRectangle(brush2, new RectangleF(0f, width / 2f, width, height - width));
			}
			g.TranslateTransform(width / 2f, width / 2f);
			g.RotateTransform(startAngle);
			PointF[] points = new PointF[20]
			{
				new PointF(0f, (0f - width) * 0.08f),
				new PointF((0f - width) * 0.1f, (0f - width) * 0.12f),
				new PointF((0f - width) * 0.2f, (0f - width) * 0.13f),
				new PointF((0f - width) * 0.25f, (0f - width) * 0.11f),
				new PointF((0f - width) * 0.28f, (0f - width) * 0.07f),
				new PointF((0f - width) * 0.3f, 0f),
				new PointF((0f - width) * 0.28f, width * 0.07f),
				new PointF((0f - width) * 0.25f, width * 0.11f),
				new PointF((0f - width) * 0.2f, width * 0.13f),
				new PointF((0f - width) * 0.1f, width * 0.12f),
				new PointF(0f, width * 0.08f),
				new PointF(width * 0.1f, width * 0.12f),
				new PointF(width * 0.2f, width * 0.13f),
				new PointF(width * 0.25f, width * 0.11f),
				new PointF(width * 0.28f, width * 0.07f),
				new PointF(width * 0.3f, 0f),
				new PointF(width * 0.28f, (0f - width) * 0.07f),
				new PointF(width * 0.25f, (0f - width) * 0.11f),
				new PointF(width * 0.2f, (0f - width) * 0.13f),
				new PointF(width * 0.1f, (0f - width) * 0.12f)
			};
			linearGradientBrush.Dispose();
			colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3] { color4, color3, color4 };
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) * 0.3f, 0f), new PointF(width * 0.3f, 0f), Color.Wheat, Color.White);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillClosedCurve(linearGradientBrush, points);
			g.FillEllipse(Brushes.Black, new RectangleF((0f - width) * 0.04f, (0f - width) * 0.04f, width * 0.08f, width * 0.08f));
			g.RotateTransform(0f - startAngle);
			g.TranslateTransform((0f - width) / 2f, (0f - width) / 2f);
			g.TranslateTransform(width / 2f, height - width / 2f);
			g.RotateTransform(startAngle * -1f - 83f);
			g.FillClosedCurve(linearGradientBrush, points);
			g.FillEllipse(Brushes.Black, new RectangleF((0f - width) * 0.04f, (0f - width) * 0.04f, width * 0.08f, width * 0.08f));
			g.RotateTransform(0f - (startAngle * -1f - 83f));
			g.TranslateTransform((0f - width) / 2f, 0f - height + width / 2f);
			linearGradientBrush.Dispose();
		}

		private PointF[] GetPointsFrom(string points, float width, float height, float dx = 0f, float dy = 0f)
		{
			return HslHelper.GetPointsFrom(points, 80.7f, 112.5f, width, height, dx, dy);
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (moveSpeed != 0f)
			{
				startAngle = (float)((double)startAngle + (double)(moveSpeed * 180f) / Math.PI / 10.0);
				if (startAngle <= -360f)
				{
					startAngle += 360f;
				}
				else if (startAngle >= 360f)
				{
					startAngle -= 360f;
				}
				Invalidate();
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
			base.Name = "HslVacuumPump";
			base.Size = new System.Drawing.Size(109, 151);
			base.ResumeLayout(false);
		}
	}
}
