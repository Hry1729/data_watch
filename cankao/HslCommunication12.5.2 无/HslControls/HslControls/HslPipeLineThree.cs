using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("管道流动控件，支持横竖模式，支持显示流动动画，多个控件组合使用出更高级的效果")]
	public class HslPipeLineThree : UserControl
	{
		private ArrowDirection arrowDirection = ArrowDirection.Down;

		private Color centerColor = Color.LightGray;

		private Color edgeColor = Color.DimGray;

		private Pen edgePen = new Pen(Color.DimGray, 1f);

		private bool isPipeLineActive1 = false;

		private bool isPipeLineActive2 = false;

		private bool isPipeLineActive3 = false;

		private int pipeLineWidth = 3;

		private Color colorPipeLineCenter = Color.DodgerBlue;

		private float moveSpeed1 = 0f;

		private float moveSpeed2 = 0f;

		private float moveSpeed3 = 0f;

		private float startOffect1 = 0f;

		private float startOffect2 = 0f;

		private float startOffect3 = 0f;

		private Timer timer = null;

		private int pipeWidth = 30;

		private float antLength = 5f;

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
		[Description("获取或设置管道控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				edgePen.Dispose();
				edgePen = new Pen(value, 1f);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道控件的中心颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color LineCenterColor
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
		[Description("获取或设置管道控件是否是横向的还是纵向的")]
		[Category("HslControls")]
		[DefaultValue(typeof(ArrowDirection), "Down")]
		public ArrowDirection PipeLineStyle
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

		[Browsable(true)]
		[Description("获取或设置管道线是否激活液体显示")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool PipeLineActive1
		{
			get
			{
				return isPipeLineActive1;
			}
			set
			{
				isPipeLineActive1 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线是否激活液体显示")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool PipeLineActive2
		{
			get
			{
				return isPipeLineActive2;
			}
			set
			{
				isPipeLineActive2 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线是否激活液体显示")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool PipeLineActive3
		{
			get
			{
				return isPipeLineActive3;
			}
			set
			{
				isPipeLineActive3 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线液体流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public float MoveSpeed1
		{
			get
			{
				return moveSpeed1;
			}
			set
			{
				moveSpeed1 = value;
				startOffect1 = 0f;
				startOffect2 = 0f;
				startOffect3 = 0f;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线液体流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public float MoveSpeed2
		{
			get
			{
				return moveSpeed2;
			}
			set
			{
				moveSpeed2 = value;
				startOffect1 = 0f;
				startOffect2 = 0f;
				startOffect3 = 0f;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线液体流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public float MoveSpeed3
		{
			get
			{
				return moveSpeed3;
			}
			set
			{
				moveSpeed3 = value;
				startOffect1 = 0f;
				startOffect2 = 0f;
				startOffect3 = 0f;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置液体控制的定时器刷新的间隔，默认是50")]
		[Category("HslControls")]
		[DefaultValue(50)]
		public int TimerInterval
		{
			get
			{
				return timer.Interval;
			}
			set
			{
				timer.Interval = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置中间管道线的宽度信息，默认为3")]
		[Category("HslControls")]
		[DefaultValue(3)]
		public int PipeLineWidth
		{
			get
			{
				return pipeLineWidth;
			}
			set
			{
				pipeLineWidth = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置流动状态时管道控件的中心颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public Color ActiveLineCenterColor
		{
			get
			{
				return colorPipeLineCenter;
			}
			set
			{
				colorPipeLineCenter = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道的宽度，默认为30")]
		[Category("HslControls")]
		[DefaultValue(30)]
		public int PipeWidth
		{
			get
			{
				return pipeWidth;
			}
			set
			{
				pipeWidth = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置蚂蚁线的长度信息")]
		[Category("HslControls")]
		[DefaultValue(5f)]
		public float AntLength
		{
			get
			{
				return antLength;
			}
			set
			{
				antLength = value;
				Invalidate();
			}
		}

		public HslPipeLineThree()
			: this(true)
		{
		}

		public HslPipeLineThree(bool timerEnable)
		{
			InitializeComponent();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			if (timerEnable)
			{
				timer = new Timer();
				timer.Interval = 50;
				timer.Tick += Timer_Tick;
				timer.Start();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				if (arrowDirection == ArrowDirection.Down)
				{
					PaintMain(g, width, height);
				}
				else if (arrowDirection == ArrowDirection.Right)
				{
					g.TranslateTransform(0f, height);
					g.RotateTransform(-90f);
					PaintMain(g, height, width);
					g.RotateTransform(90f);
					g.TranslateTransform(0f, -height);
				}
				else if (arrowDirection == ArrowDirection.Left)
				{
					g.TranslateTransform(width, 0f);
					g.RotateTransform(90f);
					PaintMain(g, height, width);
					g.RotateTransform(-90f);
					g.TranslateTransform(-width, 0f);
				}
				else
				{
					g.RotateTransform(-180f);
					g.TranslateTransform(-width, -height);
					PaintMain(g, width, height);
					g.TranslateTransform(width, height);
					g.RotateTransform(180f);
				}
			}
		}

		private void PaintMain(Graphics g, int width, int height)
		{
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, pipeWidth - 1), edgeColor, centerColor);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, new Rectangle(0, 0, width, pipeWidth - 1));
			g.DrawLine(edgePen, 0, 0, width, 0);
			g.DrawLine(edgePen, 0, pipeWidth - 1, width, pipeWidth - 1);
			linearGradientBrush.Dispose();
			linearGradientBrush = new LinearGradientBrush(new Point((width - pipeWidth) / 2, 0), new Point((width + pipeWidth) / 2, 0), edgeColor, centerColor);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillPolygon(linearGradientBrush, new PointF[5]
			{
				new PointF(width / 2, pipeWidth / 2),
				new PointF((width - pipeWidth) / 2, pipeWidth),
				new PointF((width - pipeWidth) / 2, height),
				new PointF((width + pipeWidth) / 2 - 1, height),
				new PointF((width + pipeWidth) / 2 - 1, pipeWidth)
			});
			linearGradientBrush.Dispose();
			g.DrawLine(edgePen, (width - pipeWidth) / 2, pipeWidth, (width - pipeWidth) / 2, height);
			g.DrawLine(edgePen, (width + pipeWidth) / 2 - 1, height, (width + pipeWidth) / 2 - 1, pipeWidth);
			GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate);
			GraphicsPath graphicsPath2 = new GraphicsPath(FillMode.Alternate);
			GraphicsPath graphicsPath3 = new GraphicsPath(FillMode.Alternate);
			if (isPipeLineActive1)
			{
				graphicsPath.AddLine(0, pipeWidth / 2, width, pipeWidth / 2);
			}
			if (isPipeLineActive2)
			{
				graphicsPath2.AddLine(0, pipeWidth / 2, (width - pipeWidth) / 2, pipeWidth / 2);
				graphicsPath2.AddArc(new Rectangle(width / 2 - pipeWidth, pipeWidth / 2, pipeWidth, pipeWidth), 270f, 90f);
				graphicsPath2.AddLine(width / 2, pipeWidth, width / 2, height);
			}
			if (isPipeLineActive3)
			{
				graphicsPath3.AddLine(width / 2, height, width / 2, pipeWidth);
				graphicsPath3.AddArc(new Rectangle(width / 2, pipeWidth / 2, pipeWidth, pipeWidth), 180f, 90f);
				graphicsPath3.AddLine((width + pipeWidth) / 2, pipeWidth / 2, width, pipeWidth / 2);
			}
			using (Pen pen = new Pen(colorPipeLineCenter, pipeLineWidth))
			{
				pen.DashStyle = DashStyle.Custom;
				pen.DashPattern = new float[2] { antLength, 5f };
				pen.DashOffset = startOffect1;
				g.DrawPath(pen, graphicsPath);
				pen.DashOffset = startOffect2;
				g.DrawPath(pen, graphicsPath2);
				pen.DashOffset = startOffect3;
				g.DrawPath(pen, graphicsPath3);
			}
			graphicsPath.Dispose();
			graphicsPath2.Dispose();
			graphicsPath3.Dispose();
		}

		private void PaintRectangleBorder(Graphics g, Brush brush, Pen pen, Rectangle rectangle, bool left, bool right, bool up, bool down)
		{
			g.FillRectangle(brush, rectangle);
			if (left)
			{
				g.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.X, rectangle.Y + rectangle.Height - 1);
			}
			if (up)
			{
				g.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y);
			}
			if (right)
			{
				g.DrawLine(pen, rectangle.X + rectangle.Width - 1, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
			}
			if (down)
			{
				g.DrawLine(pen, rectangle.X, rectangle.Y + rectangle.Height - 1, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
			}
		}

		private void PaintRectangleBorderUpDown(Graphics g, Brush brush, Pen pen, Rectangle rectangle)
		{
			PaintRectangleBorder(g, brush, pen, rectangle, false, false, true, true);
		}

		public void TimerExecute()
		{
			if ((isPipeLineActive1 || isPipeLineActive2 || isPipeLineActive3) && (moveSpeed1 != 0f || moveSpeed2 != 0f || moveSpeed3 != 0f))
			{
				startOffect1 -= moveSpeed1;
				if (startOffect1 <= 0f - antLength * 2f || startOffect1 >= antLength * 2f)
				{
					startOffect1 = 0f;
				}
				startOffect2 -= moveSpeed2;
				if (startOffect2 <= 0f - antLength * 2f || startOffect2 >= antLength * 2f)
				{
					startOffect2 = 0f;
				}
				startOffect3 -= moveSpeed3;
				if (startOffect3 <= 0f - antLength * 2f || startOffect3 >= antLength * 2f)
				{
					startOffect3 = 0f;
				}
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if ((isPipeLineActive1 || isPipeLineActive2 || isPipeLineActive3) && (moveSpeed1 != 0f || moveSpeed2 != 0f || moveSpeed3 != 0f))
			{
				TimerExecute();
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
			base.Name = "HslPipeLineThree";
			base.Size = new System.Drawing.Size(135, 83);
			base.ResumeLayout(false);
		}
	}
}
