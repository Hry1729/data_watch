using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("旧的仪表盘控件，外观像一把扇子，支持设置范围，指针颜色，报警区域")]
	public class HslGaugeChart : UserControl
	{
		private Color color_gauge_border = Color.DimGray;

		private Pen pen_gauge_border = new Pen(Color.DimGray);

		private Brush pen_gauge_mark = new SolidBrush(Color.DimGray);

		private Brush foreBrush = new SolidBrush(Color.DodgerBlue);

		private Color color_gauge_pointer = Color.Tomato;

		private Brush brush_gauge_pointer = null;

		private double value_start = 0.0;

		private double value_max = 100.0;

		private double value_current = 0.0;

		private double value_alarm_max = 80.0;

		private double value_alarm_min = 20.0;

		private Pen pen_gauge_alarm = null;

		private int m_version = 0;

		private double value_paint = 0.0;

		private object hybirdLock;

		private Action m_UpdateAction;

		private int segment_count = 10;

		private StringFormat sf = null;

		private string value_unit_text = string.Empty;

		private bool text_under_pointer = true;

		private bool isBigSemiCircle = false;

		private string valueFormat = "{0}";

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
		[Description("获取或设置仪表盘的背景色")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color GaugeBorder
		{
			get
			{
				return color_gauge_border;
			}
			set
			{
				pen_gauge_border?.Dispose();
				pen_gauge_border = new Pen(value);
				color_gauge_border = value;
				pen_gauge_mark.Dispose();
				pen_gauge_mark = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置仪表盘指针的颜色")]
		[DefaultValue(typeof(Color), "Tomato")]
		public Color PointerColor
		{
			get
			{
				return color_gauge_pointer;
			}
			set
			{
				brush_gauge_pointer?.Dispose();
				brush_gauge_pointer = new SolidBrush(value);
				color_gauge_pointer = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的起始值，默认为0")]
		[DefaultValue(0.0)]
		public double ValueStart
		{
			get
			{
				if (value_max <= value_start)
				{
					return value_start + 1.0;
				}
				return value_start;
			}
			set
			{
				value_start = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的最大值，默认为100")]
		[DefaultValue(100.0)]
		public double ValueMax
		{
			get
			{
				if (value_max <= value_start)
				{
					return value_start + 1.0;
				}
				return value_max;
			}
			set
			{
				value_max = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的当前值，默认为0")]
		[DefaultValue(0.0)]
		public double Value
		{
			get
			{
				return value_current;
			}
			set
			{
				if (ValueStart <= value && value <= ValueMax && value != value_current)
				{
					value_current = value;
					ThreadPool.QueueUserWorkItem(state: Interlocked.Increment(ref m_version), callBack: ThreadPoolUpdateProgress);
				}
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置当前的值显示的格式化，例如可以只显示1位小数点，2位小数点")]
		[DefaultValue("{0}")]
		public string ValueFormat
		{
			get
			{
				return valueFormat;
			}
			set
			{
				valueFormat = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的上限报警值，设置为超过最大值则无上限报警，默认为80")]
		[DefaultValue(80.0)]
		public double ValueAlarmMax
		{
			get
			{
				return value_alarm_max;
			}
			set
			{
				if (ValueStart <= value)
				{
					value_alarm_max = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的下限报警值，设置为小于最小值则无下限报警，默认为20")]
		[DefaultValue(20.0)]
		public double ValueAlarmMin
		{
			get
			{
				return value_alarm_min;
			}
			set
			{
				if (value <= ValueMax)
				{
					value_alarm_min = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置仪表盘的分割段数，最小为2，最大1000")]
		[DefaultValue(10)]
		public int SegmentCount
		{
			get
			{
				return segment_count;
			}
			set
			{
				if (value > 1 && value < 1000)
				{
					segment_count = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置仪表盘的单位描述文本")]
		[DefaultValue("")]
		public string UnitText
		{
			get
			{
				return value_unit_text;
			}
			set
			{
				value_unit_text = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置文本是否是指针的下面")]
		[DefaultValue(true)]
		public bool IsTextUnderPointer
		{
			get
			{
				return text_under_pointer;
			}
			set
			{
				text_under_pointer = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("通常情况，仪表盘不会大于半个圆，除非本属性设置为 True")]
		[DefaultValue(false)]
		public bool IsBigSemiCircle
		{
			get
			{
				return isBigSemiCircle;
			}
			set
			{
				isBigSemiCircle = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置控件的前景色")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
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
				Invalidate();
			}
		}

		public HslGaugeChart()
		{
			InitializeComponent();
			brush_gauge_pointer = new SolidBrush(color_gauge_pointer);
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			pen_gauge_alarm = new Pen(Color.OrangeRed, 3f);
			pen_gauge_alarm.DashStyle = DashStyle.Custom;
			pen_gauge_alarm.DashPattern = new float[2] { 5f, 1f };
			hybirdLock = new object();
			m_UpdateAction = base.Invalidate;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			ForeColor = Color.DodgerBlue;
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
			CenterPoint centerPoint = GetCenterPoint(width, height);
			if (centerPoint == null)
			{
				return;
			}
			Point point = centerPoint.Point;
			int radius = centerPoint.Radius;
			float num = Convert.ToSingle(centerPoint.Angle);
			Rectangle rect = new Rectangle(-radius, -radius, 2 * radius, 2 * radius);
			Rectangle rect2 = new Rectangle(-radius - 5, -radius - 5, 2 * radius + 10, 2 * radius + 10);
			Rectangle rect3 = new Rectangle(-radius / 3, -radius / 3, 2 * radius / 3, 2 * radius / 3);
			g.TranslateTransform(point.X, point.Y);
			g.DrawArc(pen_gauge_border, rect3, 0f - num, num * 2f - 180f);
			g.DrawArc(pen_gauge_border, rect, num - 180f, 180f - num * 2f);
			g.DrawLine(pen_gauge_border, (int)((double)(-(radius / 3)) * Math.Cos((double)(num / 180f) * Math.PI)), -(int)((double)(radius / 3) * Math.Sin((double)(num / 180f) * Math.PI)), -(int)((double)(radius - 10 - Font.Height) * Math.Cos((double)(num / 180f) * Math.PI)), -(int)((double)(radius - 10 - Font.Height) * Math.Sin((double)(num / 180f) * Math.PI)));
			g.DrawLine(pen_gauge_border, (int)((double)(radius - 10 - Font.Height) * Math.Cos((double)(num / 180f) * Math.PI)), -(int)((double)(radius - 10 - Font.Height) * Math.Sin((double)(num / 180f) * Math.PI)), (int)((double)(radius / 3) * Math.Cos((double)(num / 180f) * Math.PI)), -(int)((double)(radius / 3) * Math.Sin((double)(num / 180f) * Math.PI)));
			g.RotateTransform(num - 90f);
			int num2 = segment_count;
			for (int i = 0; i <= num2; i++)
			{
				g.FillRectangle(rect: new Rectangle(-2, -radius, 3, 7), brush: pen_gauge_mark);
				g.DrawString(layoutRectangle: new Rectangle(-100, -radius + 7, 200, Font.Height + 3), s: string.Format(arg0: ValueStart + (ValueMax - ValueStart) * (double)i / (double)num2, format: valueFormat), font: Font, brush: foreBrush, format: sf);
				g.RotateTransform((180f - 2f * num) / (float)num2 / 2f);
				if (i != num2)
				{
					g.DrawLine(pen_gauge_border, 0, -radius, 0, -radius + 3);
				}
				g.RotateTransform((180f - 2f * num) / (float)num2 / 2f);
			}
			g.RotateTransform((0f - (180f - 2f * num)) / (float)num2);
			g.RotateTransform(num - 90f);
			Rectangle rectangle = new Rectangle(-200, -(radius * 2 / 3 - 3), 400, Font.Height + 3);
			if (IsTextUnderPointer)
			{
				g.DrawString(string.Format(valueFormat, Value), Font, foreBrush, rectangle, sf);
				rectangle.Offset(0, Font.Height);
				if (!string.IsNullOrEmpty(UnitText))
				{
					g.DrawString(UnitText, Font, foreBrush, rectangle, sf);
				}
			}
			g.RotateTransform(num - 90f);
			g.RotateTransform((float)((value_paint - ValueStart) / (ValueMax - ValueStart) * (double)(180f - 2f * num)));
			g.FillEllipse(rect: new Rectangle(-5, -5, 10, 10), brush: brush_gauge_pointer);
			g.FillPolygon(points: new Point[5]
			{
				new Point(5, 0),
				new Point(2, -radius + 40),
				new Point(0, -radius + 20),
				new Point(-2, -radius + 40),
				new Point(-5, 0)
			}, brush: brush_gauge_pointer);
			g.RotateTransform((float)((0.0 - (value_paint - ValueStart)) / (ValueMax - ValueStart) * (double)(180f - 2f * num)));
			g.RotateTransform(90f - num);
			if (value_alarm_min > ValueStart && value_alarm_min <= ValueMax)
			{
				g.DrawArc(pen_gauge_alarm, rect2, num - 180f, (float)((ValueAlarmMin - ValueStart) / (ValueMax - ValueStart) * (double)(180f - 2f * num)));
			}
			if (value_alarm_max >= ValueStart && value_alarm_max < ValueMax)
			{
				float num3 = (float)((value_alarm_max - ValueStart) / (ValueMax - ValueStart) * (double)(180f - 2f * num));
				g.DrawArc(pen_gauge_alarm, rect2, -180f + num + num3, 180f - 2f * num - num3);
			}
			if (!IsTextUnderPointer)
			{
				g.DrawString(Value.ToString(), Font, foreBrush, rectangle, sf);
				rectangle.Offset(0, Font.Height);
				if (!string.IsNullOrEmpty(UnitText))
				{
					g.DrawString(UnitText, Font, foreBrush, rectangle, sf);
				}
			}
			g.ResetTransform();
		}

		private CenterPoint GetCenterPoint(int width, int height)
		{
			CenterPoint centerPoint = new CenterPoint();
			if (height <= 35)
			{
				return null;
			}
			if (width <= 20)
			{
				return null;
			}
			if (!IsBigSemiCircle)
			{
				centerPoint.Radius = height - 30;
				if ((double)(width - 40) / 2.0 > (double)centerPoint.Radius)
				{
					centerPoint.Angle = Math.Acos(1.0) * 180.0 / Math.PI;
				}
				else
				{
					centerPoint.Angle = Math.Acos((double)(width - 40) / 2.0 / (double)(height - 30)) * 180.0 / Math.PI;
				}
				centerPoint.Point = new Point(width / 2, height - 10);
				return centerPoint;
			}
			centerPoint.Radius = (width - 40) / 2;
			if (height - 30 < centerPoint.Radius)
			{
				centerPoint.Radius = height - 30;
				centerPoint.Angle = Math.Acos(1.0) * 180.0 / Math.PI;
				centerPoint.Point = new Point(width / 2, height - 10);
				return centerPoint;
			}
			int num = height - 30 - centerPoint.Radius;
			if (num > centerPoint.Radius)
			{
				num = centerPoint.Radius;
			}
			centerPoint.Angle = (0.0 - Math.Asin((double)num * 1.0 / (double)centerPoint.Radius)) * 180.0 / Math.PI;
			centerPoint.Point = new Point(width / 2, centerPoint.Radius + 20);
			return centerPoint;
		}

		private void ThreadPoolUpdateProgress(object obj)
		{
			try
			{
				int num = (int)obj;
				if (value_paint == value_current)
				{
					return;
				}
				double num2 = Math.Abs(value_paint - value_current) / 10.0;
				if (num2 == 0.0)
				{
					num2 = 1.0;
				}
				while (value_paint != value_current)
				{
					Thread.Sleep(17);
					if (num != m_version)
					{
						break;
					}
					lock (hybirdLock)
					{
						double num3 = 0.0;
						if (value_paint > value_current)
						{
							double num4 = value_paint - value_current;
							if (num4 > num2)
							{
								num4 = num2;
							}
							num3 = value_paint - num4;
						}
						else
						{
							double num5 = value_current - value_paint;
							if (num5 > num2)
							{
								num5 = num2;
							}
							num3 = value_paint + num5;
						}
						value_paint = num3;
					}
					if (num == m_version)
					{
						if (base.IsHandleCreated)
						{
							Invoke(m_UpdateAction);
						}
						continue;
					}
					break;
				}
			}
			catch (Exception)
			{
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
			base.Name = "HslGaugeChart";
			base.ResumeLayout(false);
		}
	}
}
