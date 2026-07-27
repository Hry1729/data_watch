using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("新的仪表盘控件，外观像汽车的仪表盘，支持设置范围，单位，指针颜色")]
	public class HslGauge : UserControl
	{
		private Color color_gauge_border = Color.DimGray;

		private Brush pen_gauge_mark = new SolidBrush(Color.DimGray);

		private Brush foreBrush = new SolidBrush(Color.DodgerBlue);

		private Color color_gauge_pointer = Color.Tomato;

		private Brush brush_gauge_pointer = null;

		private float value_start = 0f;

		private float value_max = 100f;

		private float value_current = 0f;

		private float value_alarm_max = 80f;

		private float value_alarm_min = 20f;

		private Color color_gauge_alarm = Color.OrangeRed;

		private Pen pen_gauge_alarm = null;

		private Brush brush_gauge_alarm = null;

		private int m_version = 0;

		private float value_paint = 0f;

		private object hybirdLock;

		private Action m_UpdateAction;

		private int segment_count = 10;

		private StringFormat sf = null;

		private string value_unit_text = string.Empty;

		private bool text_under_pointer = true;

		private bool isBigSemiCircle = false;

		private int pointerSize = 5;

		private string customerCalibration = string.Empty;

		private bool isTextInfoRenderBlow = false;

		private string valueFormat = "{0}";

		private bool isAlarmRenderLine = true;

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
		[DefaultValue(0f)]
		public float ValueStart
		{
			get
			{
				return value_start;
			}
			set
			{
				if (value_max <= value_start)
				{
					value_start = value_max - 1f;
				}
				else
				{
					value_start = value;
				}
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的最大值，默认为100")]
		[DefaultValue(100f)]
		public float ValueMax
		{
			get
			{
				return value_max;
			}
			set
			{
				if (value_max <= value_start)
				{
					value_max = value_start + 1f;
				}
				else
				{
					value_max = value;
				}
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置数值的当前值，默认为0")]
		[DefaultValue(0f)]
		public float Value
		{
			get
			{
				return value_current;
			}
			set
			{
				if (value != value_current)
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
		[DefaultValue(80f)]
		public float ValueAlarmMax
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
		[DefaultValue(20f)]
		public float ValueAlarmMin
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

		[Category("HslControls")]
		[Description("获取或设置报警区段的颜色")]
		[DefaultValue(typeof(Color), "OrangeRed")]
		public Color GaugeAlarmColor
		{
			get
			{
				return color_gauge_alarm;
			}
			set
			{
				color_gauge_alarm = value;
				pen_gauge_alarm?.Dispose();
				pen_gauge_alarm = new Pen(color_gauge_alarm, 3f);
				pen_gauge_alarm.DashStyle = DashStyle.Custom;
				pen_gauge_alarm.DashPattern = new float[2] { 5f, 1f };
				brush_gauge_alarm?.Dispose();
				brush_gauge_alarm = new SolidBrush(color_gauge_alarm);
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置指针的大小值")]
		[DefaultValue(5)]
		public int PointerSize
		{
			get
			{
				return pointerSize;
			}
			set
			{
				pointerSize = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置完全自定义的刻度表示数据")]
		[DefaultValue("")]
		public string CustomerCalibration
		{
			get
			{
				return customerCalibration;
			}
			set
			{
				customerCalibration = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置单位文本信息是否显示在控件的下方")]
		[DefaultValue(false)]
		public bool IsTextInfoRenderBlow
		{
			get
			{
				return isTextInfoRenderBlow;
			}
			set
			{
				isTextInfoRenderBlow = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("当前的报警是否是线表示，还是区域表示")]
		[DefaultValue(true)]
		public bool IsAlarmRenderLine
		{
			get
			{
				return isAlarmRenderLine;
			}
			set
			{
				isAlarmRenderLine = value;
				Invalidate();
			}
		}

		public HslGauge()
		{
			InitializeComponent();
			brush_gauge_pointer = new SolidBrush(color_gauge_pointer);
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			pen_gauge_alarm = new Pen(color_gauge_alarm, 3f);
			pen_gauge_alarm.DashStyle = DashStyle.Custom;
			pen_gauge_alarm.DashPattern = new float[2] { 5f, 1f };
			brush_gauge_alarm = new SolidBrush(color_gauge_alarm);
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
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if ( ValueMax < ValueStart)
			{
				return;
			}
			float num = value_paint;
			if (num > ValueMax)
			{
				num = ValueMax;
			}
			if (num < ValueStart)
			{
				num = ValueStart;
			}
			CenterPoint centerPoint = GetCenterPoint(width, height);
			if (centerPoint == null)
			{
				return;
			}
			Point point = centerPoint.Point;
			int radius = centerPoint.Radius;
			float num2 = Convert.ToSingle(centerPoint.Angle);
			Rectangle rect = new Rectangle(-radius, -radius, 2 * radius, 2 * radius);
			Rectangle rect2 = new Rectangle(-radius - 5, -radius - 5, 2 * radius + 10, 2 * radius + 10);
			g.TranslateTransform(point.X, point.Y);
			if (value_alarm_min > ValueStart && value_alarm_min <= ValueMax)
			{
				if (isAlarmRenderLine)
				{
					g.DrawArc(pen_gauge_alarm, rect2, num2 - 180f, (ValueAlarmMin - ValueStart) / (ValueMax - ValueStart) * (180f - 2f * num2));
				}
				else
				{
					g.FillPie(brush_gauge_alarm, rect, num2 - 180f, (ValueAlarmMin - ValueStart) / (ValueMax - ValueStart) * (180f - 2f * num2));
				}
			}
			if (value_alarm_max >= ValueStart && value_alarm_max < ValueMax)
			{
				float num3 = (value_alarm_max - ValueStart) / (ValueMax - ValueStart) * (180f - 2f * num2);
				if (isAlarmRenderLine)
				{
					g.DrawArc(pen_gauge_alarm, rect2, -180f + num2 + num3, 180f - 2f * num2 - num3);
				}
				else
				{
					g.FillPie(brush_gauge_alarm, rect, -180f + num2 + num3, 180f - 2f * num2 - num3);
				}
			}
			g.RotateTransform(num2 - 90f);
			int num4 = segment_count;
			for (int i = 0; i <= num4; i++)
			{
				int num5 = ((radius > 200) ? 3 : 2);
				int num6 = ((radius > 100) ? 12 : 8);
				g.FillPolygon(pen_gauge_mark, new PointF[6]
				{
					new PointF(-num5, -radius),
					new PointF(-num5, -radius + num6),
					new PointF(0f, -radius + num6 + num6 / 2 - 1),
					new PointF(num5, -radius + num6),
					new PointF(num5, -radius),
					new PointF(-num5, -radius)
				});
				for (int j = 0; j < 10; j++)
				{
					using (Pen pen = new Pen(color_gauge_border, 1f))
					{
						g.RotateTransform((180f - 2f * num2) / (float)num4 / 10f);
						if (i != num4)
						{
							switch (j)
							{
							case 4:
								pen.Width = 2f;
								g.DrawLine(pen, 0, -radius, 0, -radius + 6);
								pen.Width = 1f;
								break;
							default:
								g.DrawLine(pen, 0, -radius, 0, -radius + 4);
								break;
							case 9:
								break;
							}
						}
					}
				}
			}
			g.RotateTransform((0f - (180f - 2f * num2)) / (float)num4);
			g.RotateTransform(num2 - 90f);
			string[] array = null;
			if (!string.IsNullOrEmpty(customerCalibration))
			{
				array = customerCalibration.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			}
			for (int k = 0; k <= num4; k++)
			{
				double num7 = ValueStart + (ValueMax - ValueStart) * (float)k / (float)num4;
				double num8 = (double)(180f - num2) - (num7 - (double)ValueStart) / (double)(ValueMax - ValueStart) * (double)(180f - 2f * num2);
				if (!double.IsNaN(num8))
				{
					PointF pointF = new PointF((float)(radius - 12 - Font.Height) * (float)Math.Cos(num8 * Math.PI / 180.0), (float)(-(radius - 12 - Font.Height)) * (float)Math.Sin(num8 * Math.PI / 180.0));
					if (array != null && k < array.Length)
					{
						g.DrawString(array[k], Font, foreBrush, new RectangleF(pointF.X - 100f, pointF.Y - 40f, 200f, 80f), sf);
					}
					else
					{
						g.DrawString(string.Format(ValueFormat, num7), Font, foreBrush, new RectangleF(pointF.X - 100f, pointF.Y - 40f, 200f, 80f), sf);
					}
				}
			}
			Rectangle rectangle = new Rectangle(-200, -(radius * 3 / 5 - 3), 400, Font.Height + 3);
			if (isTextInfoRenderBlow)
			{
				rectangle.Y = radius / 5 + 3;
			}
			if (IsTextUnderPointer)
			{
				if (width > 200)
				{
					g.DrawString(string.Format(ValueFormat, Value), Font, foreBrush, rectangle, sf);
				}
				rectangle.Offset(0, Font.Height);
				if (!string.IsNullOrEmpty(UnitText))
				{
					g.DrawString(UnitText, Font, foreBrush, rectangle, sf);
				}
			}
			g.RotateTransform(num2 - 90f);
			g.RotateTransform((num - ValueStart) / (ValueMax - ValueStart) * (180f - 2f * num2));
			g.FillEllipse(rect: new Rectangle(-pointerSize, -pointerSize, pointerSize * 2, pointerSize * 2), brush: brush_gauge_pointer);
			g.FillPolygon(points: new Point[5]
			{
				new Point(pointerSize, 0),
				new Point((pointerSize / 2 < 1) ? 1 : (pointerSize / 2), -radius + 20),
				new Point(0, -radius),
				new Point(-((pointerSize / 2 < 1) ? 1 : (pointerSize / 2)), -radius + 20),
				new Point(-pointerSize, 0)
			}, brush: brush_gauge_pointer);
			g.RotateTransform((0f - (num - ValueStart)) / (ValueMax - ValueStart) * (180f - 2f * num2));
			g.RotateTransform(90f - num2);
			if (!IsTextUnderPointer)
			{
				if (width > 200)
				{
					g.DrawString(string.Format(ValueFormat, Value), Font, foreBrush, rectangle, sf);
				}
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
				float num2 = Math.Abs(value_paint - value_current) / 10f;
				if (num2 == 0f)
				{
					num2 = 1f;
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
						float num3 = 0f;
						if (value_paint > value_current)
						{
							float num4 = value_paint - value_current;
							if (num4 > num2)
							{
								num4 = num2;
							}
							num3 = value_paint - num4;
						}
						else
						{
							float num5 = value_current - value_paint;
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
			base.Name = "HslGauge";
			base.ResumeLayout(false);
		}
	}
}
