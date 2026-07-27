using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("时钟控件，支持时针，分针，秒针显示，支持按秒跳格或是连续运行，支持显示附加文本显示")]
	public class HslClock : UserControl
	{
		private StringFormat sf = null;

		private Timer time1s = null;

		private Brush foreBrush = new SolidBrush(Color.Chocolate);

		private Color clockBackColor = Color.White;

		private Brush brushClockBack = new SolidBrush(Color.White);

		private Color hourColor = Color.Chocolate;

		private Pen penHour = new Pen(Color.Chocolate, 2f);

		private Color minuteColor = Color.Coral;

		private Pen penMinute = new Pen(Color.Coral, 2f);

		private Color secondColor = Color.Green;

		private Pen penSecond = new Pen(Color.Green, 2f);

		private bool isSecondStep = false;

		private float needleWidth = 2f;

		private int secondCurrent = -1;

		private bool isShowCalendar = false;

		private bool isShowWeek = true;

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
		[Category("HslControls")]
		[Description("获取或设置控件的字体信息")]
		public override Font Font
		{
			get
			{
				return base.Font;
			}
			set
			{
				base.Font = value;
				BackgroundImage = getBackGround();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[DefaultValue(ImageLayout.None)]
		[Description("用于组件的背景图像布局")]
		public override ImageLayout BackgroundImageLayout
		{
			get
			{
				return base.BackgroundImageLayout;
			}
			set
			{
				base.BackgroundImageLayout = value;
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置时钟指针的颜色")]
		[DefaultValue(typeof(Color), "Chocolate")]
		public virtual Color HourColor
		{
			get
			{
				return hourColor;
			}
			set
			{
				hourColor = value;
				penHour.Dispose();
				penHour = new Pen(hourColor, needleWidth);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置分钟指针的颜色")]
		[DefaultValue(typeof(Color), "Coral")]
		public virtual Color MinuteColor
		{
			get
			{
				return minuteColor;
			}
			set
			{
				minuteColor = value;
				penMinute.Dispose();
				penMinute = new Pen(minuteColor, needleWidth);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置秒钟指针的颜色")]
		[DefaultValue(typeof(Color), "Green")]
		public virtual Color SecondColor
		{
			get
			{
				return secondColor;
			}
			set
			{
				secondColor = value;
				penSecond.Dispose();
				penSecond = new Pen(secondColor, needleWidth);
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
		[Description("获取或设置秒针是否按照每秒前进还是连续变化")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool IsSecondStep
		{
			get
			{
				return isSecondStep;
			}
			set
			{
				isSecondStep = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置时钟的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
		public virtual Color ClockBackColor
		{
			get
			{
				return clockBackColor;
			}
			set
			{
				clockBackColor = value;
				brushClockBack.Dispose();
				brushClockBack = new SolidBrush(value);
				BackgroundImage = getBackGround();
			}
		}

		[Browsable(true)]
		[Description("获取或设置文本的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Chocolate")]
		public override Color ForeColor
		{
			get
			{
				return base.ForeColor;
			}
			set
			{
				base.ForeColor = value;
				foreBrush.Dispose();
				foreBrush = new SolidBrush(value);
				BackgroundImage = getBackGround();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件是否显示日期信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool IsShowCalendar
		{
			get
			{
				return isShowCalendar;
			}
			set
			{
				isShowCalendar = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件是否显示星期的信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool IsShowWeek
		{
			get
			{
				return isShowWeek;
			}
			set
			{
				isShowWeek = value;
				Invalidate();
			}
		}

		public HslClock()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			time1s = new Timer();
			time1s.Interval = 50;
			time1s.Tick += Time1s_Tick;
			BackgroundImageLayout = ImageLayout.None;
			ForeColor = Color.Chocolate;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			BackgroundImage = getBackGround();
			time1s.Start();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(false)
			{
				Graphics graphics = e.Graphics;
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				PaintHslControls(graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				DateTime now = DateTime.Now;
				int num = 10;
				int num2 = Math.Min(width, height);
				float num3 = (float)(num2 - num * 2) / 2f;
				float num4 = (float)now.Hour + (float)now.Minute / 60f;
				float num5 = (float)now.Minute + (float)now.Second / 60f;
				float num6 = (float)now.Second + (float)now.Millisecond / 1000f;
				if (isSecondStep)
				{
					num6 = now.Second;
				}
				float num7 = num4 * 30f + 270f;
				float num8 = num5 * 6f + 270f;
				float angle = num6 * 6f + 270f;
				g.TranslateTransform(num2 / 2, num2 / 2);
				if (num2 > 130 && isShowWeek)
				{
					g.DrawString(now.DayOfWeek.ToString(), Font, foreBrush, new RectangleF(-100f, num3 / 4f, 200f, Font.Height), sf);
				}
				g.DrawString(Text, Font, foreBrush, new RectangleF(-100f, (0f - num3) / 3f, 200f, Font.Height), sf);
				if (num2 > 180 && isShowCalendar)
				{
					RectangleF rectangleF = new RectangleF(num3 / 2f - 30f, -Font.Height / 2 - 2, 40f, Font.Height + 4);
					g.FillRectangle(Brushes.LimeGreen, rectangleF);
					g.DrawString(now.Day.ToString(), Font, Brushes.White, rectangleF, sf);
				}
				g.RotateTransform(num7, MatrixOrder.Prepend);
				g.DrawLine(penHour, new Point(4, 0), new Point(9, 0));
				g.DrawClosedCurve(penHour, new PointF[6]
				{
					new PointF(12f, 2f),
					new PointF(10f, 0f),
					new PointF(12f, -2f),
					new PointF(num3 / 2f, -2f),
					new PointF(num3 / 2f + 6f, 0f),
					new PointF(num3 / 2f, 2f)
				});
				g.RotateTransform(0f - num7);
				g.RotateTransform(num8, MatrixOrder.Prepend);
				g.DrawLine(penMinute, new Point(4, 0), new Point(9, 0));
				g.DrawClosedCurve(penMinute, new PointF[6]
				{
					new PointF(14f, 2f),
					new PointF(10f, 0f),
					new PointF(14f, -2f),
					new PointF(num3 - 17f, -2f),
					new PointF(num3 - 10f, 0f),
					new PointF(num3 - 17f, 2f)
				});
				g.RotateTransform(0f - num8);
				g.RotateTransform(angle, MatrixOrder.Prepend);
				g.DrawLine(penSecond, new PointF(-13f, 0f), new PointF(num3 - 6f, 0f));
				g.ResetTransform();
			}
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			BackgroundImage = getBackGround();
			base.OnSizeChanged(e);
		}

		private void Time1s_Tick(object sender, EventArgs e)
		{
			if (isSecondStep)
			{
				if (DateTime.Now.Second != secondCurrent)
				{
					secondCurrent = DateTime.Now.Second;
					Invalidate();
				}
			}
			else
			{
				Invalidate();
			}
		}

		private Bitmap getBackGround()
		{
			int num = Math.Min(base.Width, base.Height);
			Bitmap bitmap = new Bitmap(num, num);
			Graphics graphics = Graphics.FromImage(bitmap);
			Pen pen = new Pen(Color.Chocolate, 2f);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			int num2 = 10;
			graphics.TranslateTransform((float)bitmap.Width / 2f, (float)bitmap.Height / 2f);
			float num3 = (float)(bitmap.Width - num2 * 2) / 2f;
			graphics.FillEllipse(Brushes.DarkGray, new RectangleF(0f - num3, 0f - num3, 2f * num3, 2f * num3));
			graphics.FillEllipse(brushClockBack, new RectangleF(0f - num3 + 2f, 0f - num3 + 2f, 2f * num3 - 4f, 2f * num3 - 4f));
			graphics.DrawEllipse(Pens.Black, new RectangleF(-4f, -4f, 8f, 8f));
			for (int i = 0; i < 60; i++)
			{
				graphics.RotateTransform(6f);
				graphics.DrawLine(Pens.DarkGray, new PointF(num3 - 3f, 0f), new PointF(num3 - 1f, 0f));
			}
			for (int j = 0; j < 12; j++)
			{
				graphics.RotateTransform(30f);
				graphics.DrawLine(pen, new PointF(num3 - 6f, 0f), new PointF(num3 - 1f, 0f));
			}
			for (int k = 0; k < 12; k++)
			{
				graphics.DrawString(layoutRectangle: new RectangleF((float)((double)(num3 - 5f - ((num > 100) ? ((float)Font.Height) : 7f)) * Math.Cos((double)(30 * k - 60) / 360.0 * 2.0 * Math.PI)) - 30f, (float)((double)(num3 - 5f - ((num > 100) ? ((float)Font.Height) : 7f)) * Math.Sin((double)(30 * k - 60) / 360.0 * 2.0 * Math.PI)) - (float)(Font.Height / 2), 60f, Font.Height), s: (k + 1).ToString(), font: Font, brush: foreBrush, format: sf);
			}
			pen.Dispose();
			graphics.ResetTransform();
			graphics.Dispose();
			return bitmap;
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
			base.Name = "HslClock";
			base.Size = new System.Drawing.Size(182, 179);
			base.ResumeLayout(false);
		}
	}
}
