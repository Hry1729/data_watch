using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("管道流动控件，支持横竖模式，支持显示流动动画，多个控件组合使用出更高级的效果")]
	public class HslPipeLine : UserControl
	{
		private HslDirectionStyle hslPipeLineStyle = HslDirectionStyle.Horizontal;

		private Color centerColor = Color.LightGray;

		private Color edgeColor = Color.DimGray;

		private Pen edgePen = new Pen(Color.DimGray, 1f);

		private HslPipeTurnDirection hslPipeTurnLeft = HslPipeTurnDirection.None;

		private HslPipeTurnDirection hslPipeTurnRight = HslPipeTurnDirection.None;

		private bool isPipeLineActive = false;

		private int pipeLineWidth = 3;

		private Color colorPipeLineCenter = Color.DodgerBlue;

		private float moveSpeed = 0f;

		private float startOffect = 0f;

		private Timer timer = null;

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
				HslPipeLine_SizeChanged(this, new EventArgs());
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
				HslPipeLine_SizeChanged(this, new EventArgs());
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道控件左侧或是上方的端点朝向")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslPipeTurnDirection), "None")]
		public HslPipeTurnDirection PipeTurnLeft
		{
			get
			{
				return hslPipeTurnLeft;
			}
			set
			{
				hslPipeTurnLeft = value;
				HslPipeLine_SizeChanged(null, null);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道控件左侧或是上方的端点朝向")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslPipeTurnDirection), "None")]
		public HslPipeTurnDirection PipeTurnRight
		{
			get
			{
				return hslPipeTurnRight;
			}
			set
			{
				hslPipeTurnRight = value;
				HslPipeLine_SizeChanged(null, null);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道控件是否是横向的还是纵向的")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslDirectionStyle), "Horizontal")]
		public HslDirectionStyle PipeLineStyle
		{
			get
			{
				return hslPipeLineStyle;
			}
			set
			{
				hslPipeLineStyle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线是否激活液体显示")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool PipeLineActive
		{
			get
			{
				return isPipeLineActive;
			}
			set
			{
				isPipeLineActive = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置管道线液体流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0f)]
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

		public HslPipeLine()
			: this(true)
		{
		}

		public HslPipeLine(bool timerEnable)
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

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			base.SizeChanged += HslPipeLine_SizeChanged;
			BackgroundImage = getBackgroundImg(base.Width, base.Height);
		}

		private void HslPipeLine_SizeChanged(object sender, EventArgs e)
		{
			BackgroundImage?.Dispose();
			BackgroundImage = getBackgroundImg(base.Width, base.Height);
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
			if(false)
			{
				PaintMain(g, base.Width, base.Height);
			}
		}

		private Bitmap getBackgroundImg(int width, int height)
		{
			Bitmap bitmap = new Bitmap(width, height);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
			if (hslPipeLineStyle == HslDirectionStyle.Horizontal)
			{
				LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, base.Height - 1), edgeColor, centerColor);
				linearGradientBrush.InterpolationColors = colorBlend;
				if (hslPipeTurnLeft == HslPipeTurnDirection.Up)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(0, -base.Height - 1, 2 * base.Height, 2 * base.Height), 90f, 90f);
				}
				else if (hslPipeTurnLeft == HslPipeTurnDirection.Down)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(0, 0, 2 * base.Height, 2 * base.Height), 180f, 90f);
				}
				else
				{
					PaintRectangleBorderUpDown(graphics, linearGradientBrush, edgePen, new Rectangle(0, 0, base.Height, base.Height));
				}
				if (hslPipeTurnRight == HslPipeTurnDirection.Up)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(base.Width - 1 - base.Height * 2, -base.Height - 1, 2 * base.Height, 2 * base.Height), 0f, 90f);
				}
				else if (hslPipeTurnRight == HslPipeTurnDirection.Down)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(base.Width - 1 - base.Height * 2, 0, 2 * base.Height, 2 * base.Height), 270f, 90f);
				}
				else
				{
					PaintRectangleBorderUpDown(graphics, linearGradientBrush, edgePen, new Rectangle(base.Width - base.Height, 0, base.Height, base.Height));
				}
				if (base.Width - base.Height * 2 >= 0)
				{
					PaintRectangleBorderUpDown(graphics, linearGradientBrush, edgePen, new Rectangle(base.Height - 1, 0, base.Width - 2 * base.Height + 2, base.Height));
				}
				linearGradientBrush.Dispose();
			}
			else
			{
				LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new Point(0, 0), new Point(base.Width - 1, 0), edgeColor, centerColor);
				linearGradientBrush2.InterpolationColors = colorBlend;
				if (hslPipeTurnLeft == HslPipeTurnDirection.Left)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(-base.Width - 1, 0, 2 * base.Width, 2 * base.Width), 270f, 90f);
				}
				else if (hslPipeTurnLeft == HslPipeTurnDirection.Right)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(0, 0, 2 * base.Width, 2 * base.Width), 180f, 90f);
				}
				else
				{
					PaintRectangleBorderLeftRight(graphics, linearGradientBrush2, edgePen, new Rectangle(0, 0, base.Width, base.Width));
				}
				if (hslPipeTurnRight == HslPipeTurnDirection.Left)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(-base.Width - 1, base.Height - base.Width * 2, 2 * base.Width, 2 * base.Width), 0f, 90f);
				}
				else if (hslPipeTurnRight == HslPipeTurnDirection.Right)
				{
					PaintEllipse(graphics, colorBlend, new Rectangle(0, base.Height - base.Width * 2, 2 * base.Width, 2 * base.Width), 90f, 90f);
				}
				else
				{
					PaintRectangleBorderLeftRight(graphics, linearGradientBrush2, edgePen, new Rectangle(0, base.Height - base.Width, base.Width, base.Width));
				}
				if (base.Height - base.Width * 2 >= 0)
				{
					PaintRectangleBorderLeftRight(graphics, linearGradientBrush2, edgePen, new Rectangle(0, base.Width - 1, base.Width, base.Height - base.Width * 2 + 2));
				}
				linearGradientBrush2.Dispose();
			}
			return bitmap;
		}

		private void PaintMain(Graphics g, int width, int height)
		{
			GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate);
			if (hslPipeLineStyle == HslDirectionStyle.Horizontal)
			{
				if (isPipeLineActive)
				{
					if (base.Width < base.Height)
					{
						graphicsPath.AddLine(0, base.Height / 2, base.Height, base.Height / 2);
					}
					else
					{
						if (hslPipeTurnLeft == HslPipeTurnDirection.Up)
						{
							graphicsPath.AddArc(new Rectangle(base.Height / 2, -base.Height / 2 - 1, base.Height, base.Height), 180f, -90f);
						}
						else if (hslPipeTurnLeft == HslPipeTurnDirection.Down)
						{
							graphicsPath.AddArc(new Rectangle(base.Height / 2, base.Height / 2, base.Height, base.Height), 180f, 90f);
						}
						else
						{
							graphicsPath.AddLine(0, base.Height / 2, base.Height, base.Height / 2);
						}
						if (base.Width - base.Height * 2 >= 0)
						{
							graphicsPath.AddLine(base.Height, base.Height / 2, base.Width - base.Height - 1, base.Height / 2);
						}
						if (hslPipeTurnRight == HslPipeTurnDirection.Up)
						{
							graphicsPath.AddArc(new Rectangle(base.Width - 1 - base.Height * 3 / 2, -base.Height / 2 - 1, base.Height, base.Height), 90f, -90f);
						}
						else if (hslPipeTurnRight == HslPipeTurnDirection.Down)
						{
							graphicsPath.AddArc(new Rectangle(base.Width - 1 - base.Height * 3 / 2, base.Height / 2, base.Height, base.Height), 270f, 90f);
						}
						else
						{
							graphicsPath.AddLine(base.Width - base.Height, base.Height / 2, base.Width - 1, base.Height / 2);
						}
					}
				}
			}
			else if (isPipeLineActive)
			{
				if (base.Width > base.Height)
				{
					graphicsPath.AddLine(0, base.Height / 2, base.Height, base.Height / 2);
				}
				else
				{
					if (hslPipeTurnLeft == HslPipeTurnDirection.Left)
					{
						graphicsPath.AddArc(new Rectangle(-base.Width / 2, base.Width / 2 - 1, base.Width, base.Width), 270f, 90f);
					}
					else if (hslPipeTurnLeft == HslPipeTurnDirection.Right)
					{
						graphicsPath.AddArc(new Rectangle(base.Width / 2, base.Width / 2 - 1, base.Width, base.Width), 270f, -90f);
					}
					else
					{
						graphicsPath.AddLine(base.Width / 2, 0, base.Width / 2, base.Width);
					}
					if (base.Height - base.Width * 2 >= 0)
					{
						graphicsPath.AddLine(base.Width / 2, base.Width, base.Width / 2, base.Height - base.Width - 1);
					}
					if (hslPipeTurnRight == HslPipeTurnDirection.Left)
					{
						graphicsPath.AddArc(new Rectangle(-base.Width / 2, base.Height - 1 - base.Width * 3 / 2, base.Width, base.Width), 0f, 90f);
					}
					else if (hslPipeTurnRight == HslPipeTurnDirection.Right)
					{
						graphicsPath.AddArc(new Rectangle(base.Width / 2, base.Height - 1 - base.Width * 3 / 2, base.Width, base.Width), -180f, -90f);
					}
					else
					{
						graphicsPath.AddLine(base.Width / 2, base.Height - base.Width, base.Width / 2, base.Height - 1);
					}
				}
			}
			using (Pen pen = new Pen(colorPipeLineCenter, pipeLineWidth))
			{
				pen.DashStyle = DashStyle.Custom;
				pen.DashPattern = new float[2] { antLength, antLength };
				pen.DashOffset = startOffect;
				g.DrawPath(pen, graphicsPath);
			}
			graphicsPath.Dispose();
		}

		private void PaintEllipse(Graphics g, ColorBlend colorBlend, Rectangle rect, float startAngle, float sweepAngle)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(rect);
			PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterPoint = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
			pathGradientBrush.InterpolationColors = colorBlend;
			g.FillPie(pathGradientBrush, rect, startAngle, sweepAngle);
			g.DrawArc(edgePen, rect, startAngle, sweepAngle);
			pathGradientBrush.Dispose();
			graphicsPath.Dispose();
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

		private void PaintRectangleBorderLeftRight(Graphics g, Brush brush, Pen pen, Rectangle rectangle)
		{
			PaintRectangleBorder(g, brush, pen, rectangle, true, true, false, false);
		}

		private void PaintRectangleBorderUpDown(Graphics g, Brush brush, Pen pen, Rectangle rectangle)
		{
			PaintRectangleBorder(g, brush, pen, rectangle, false, false, true, true);
		}

		private void PaintRectangleBorder(Graphics g, Brush brush, Pen pen, Rectangle rectangle)
		{
			PaintRectangleBorder(g, brush, pen, rectangle, true, true, true, true);
		}

		public void TimerExecute()
		{
			if (isPipeLineActive && moveSpeed != 0f)
			{
				startOffect -= moveSpeed;
				if (startOffect <= 0f - antLength * 2f || startOffect >= antLength * 2f)
				{
					startOffect = 0f;
				}
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (isPipeLineActive && moveSpeed != 0f)
			{
				TimerExecute();
				Invalidate();
			}
		}

		private void Timer_Tick2(object status)
		{
			if (!isPipeLineActive)
			{
				return;
			}
			startOffect -= moveSpeed;
			if (startOffect <= -10f || startOffect >= 10f)
			{
				startOffect = 0f;
			}
			Bitmap bitmap = new Bitmap(base.Width, base.Height);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintMain(graphics, base.Width, base.Height);
			graphics.Dispose();
			if (base.InvokeRequired)
			{
				Invoke((Action<Bitmap>)delegate
				{
					BackgroundImage?.Dispose();
					BackgroundImage = bitmap;
				}, bitmap);
			}
			else
			{
				BackgroundImage?.Dispose();
				BackgroundImage = bitmap;
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
			base.Name = "HslPipeLine";
			base.Size = new System.Drawing.Size(335, 21);
			base.ResumeLayout(false);
		}
	}
}
