using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个电池控件，类似进度条，可以设置颜色，设置进度条的格式化操作")]
	public class HslBattery : UserControl
	{
		private StringFormat sf = null;

		private Color batteryBackEdgeColor = Color.FromArgb(142, 196, 216);

		private Brush batteryBackBrush = new SolidBrush(Color.FromArgb(142, 196, 216));

		private Pen batteryBackPen = new Pen(Color.FromArgb(142, 196, 216));

		private Color batteryForeEdgeColor = Color.Gray;

		private Pen edgePen = new Pen(Color.Gray, 1f);

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

		private Color color_1 = Color.Green;

		private Color color_2 = Color.LimeGreen;

		private Color color_3 = Color.Orange;

		private Color color_4 = Color.Tomato;

		private Color color_5 = Color.Red;

		private float value_max = 100f;

		private float value_min = 0f;

		private float value_now = 0f;

		private float value_paint = 0f;

		private float value_color1 = 0.85f;

		private float value_color2 = 0.6f;

		private float value_color3 = 0.4f;

		private float value_color4 = 0.15f;

		private string stringFormat = "{0} %";

		private bool isTextRender = false;

		private bool isUseAnimation = true;

		private Action m_UpdateAction;

		private int m_version = 0;

		private object hybirdLock;

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
		[Description("获取或设置电池的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[142, 196, 216]")]
		public virtual Color BatteryBackColor
		{
			get
			{
				return batteryBackEdgeColor;
			}
			set
			{
				batteryBackEdgeColor = value;
				batteryBackBrush.Dispose();
				batteryBackBrush = new SolidBrush(value);
				batteryBackPen.Dispose();
				batteryBackPen = new Pen(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的数值")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float Value
		{
			get
			{
				return value_now;
			}
			set
			{
				if (value != value_now)
				{
					value_now = value;
					if (isUseAnimation)
					{
						ThreadPool.QueueUserWorkItem(state: Interlocked.Increment(ref m_version), callBack: ThreadPoolUpdateProgress);
						return;
					}
					value_paint = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前数值的最大值")]
		[Category("HslControls")]
		[DefaultValue(100f)]
		public virtual float ValueMax
		{
			get
			{
				return value_max;
			}
			set
			{
				value_max = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前数值的最小值")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float ValueMin
		{
			get
			{
				return value_min;
			}
			set
			{
				value_min = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池控件是否是横向的还是纵向的")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslDirectionStyle), "Horizontal")]
		public virtual HslDirectionStyle BatteryStyle
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

		[Description("获取或设置是否显示文本提示信息的文本")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(false)]
		public virtual bool IsTextRender
		{
			get
			{
				return isTextRender;
			}
			set
			{
				isTextRender = value;
				Invalidate();
			}
		}

		[Description("获取或设置显示的文本的信息内容")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue("{0} %")]
		public virtual string StringFormat
		{
			get
			{
				return stringFormat;
			}
			set
			{
				stringFormat = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第一区间的前景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Green")]
		public virtual Color Color1
		{
			get
			{
				return color_1;
			}
			set
			{
				color_1 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第二区间的前景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LimeGreen")]
		public virtual Color Color2
		{
			get
			{
				return color_2;
			}
			set
			{
				color_2 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第三区间的前景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Orange")]
		public virtual Color Color3
		{
			get
			{
				return color_3;
			}
			set
			{
				color_3 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第四区间的前景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		public virtual Color Color4
		{
			get
			{
				return color_4;
			}
			set
			{
				color_4 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第五区间的前景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Red")]
		public virtual Color Color5
		{
			get
			{
				return color_5;
			}
			set
			{
				color_5 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第一区间和第二区间的分割值的百分比，默认0.85")]
		[Category("HslControls")]
		[DefaultValue(0.85f)]
		public virtual float Separatrix1
		{
			get
			{
				return value_color1;
			}
			set
			{
				value_color1 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第二区间和第三区间的分割值的百分比，默认0.60")]
		[Category("HslControls")]
		[DefaultValue(0.6f)]
		public virtual float Separatrix2
		{
			get
			{
				return value_color2;
			}
			set
			{
				value_color2 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第三区间和第四区间的分割值的百分比，默认0.40")]
		[Category("HslControls")]
		[DefaultValue(0.4f)]
		public virtual float Separatrix3
		{
			get
			{
				return value_color3;
			}
			set
			{
				value_color3 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置电池的第四区间和第五区间的分割值的百分比，默认0.15")]
		[Category("HslControls")]
		[DefaultValue(0.15f)]
		public virtual float Separatrix4
		{
			get
			{
				return value_color4;
			}
			set
			{
				value_color4 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的控件是否启用动画信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool UseAnimation
		{
			get
			{
				return isUseAnimation;
			}
			set
			{
				isUseAnimation = value;
			}
		}

		public HslBattery()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			hybirdLock = new object();
			m_UpdateAction = base.Invalidate;
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
			if (hslValvesStyle == HslDirectionStyle.Horizontal)
			{
				rectangleF = new RectangleF(0f, (float)height * 0.1f, width, (float)height * 0.9f);
				PaintMain(g, width, height);
			}
			else
			{
				rectangleF = new RectangleF(0f, 0f, (float)width * 0.9f, height);
				g.TranslateTransform(width, 0f);
				g.RotateTransform(90f);
				PaintMain(g, height, width);
				g.ResetTransform();
			}
			if (!isTextRender)
			{
				return;
			}
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(string.Format(stringFormat, value_now), Font, brush, rectangleF, sf);
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			g.TranslateTransform(width / 2f, 0f);
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.75f, 1f };
			colorBlend.Colors = new Color[3]
			{
				batteryBackEdgeColor,
				Color.WhiteSmoke,
				batteryBackEdgeColor
			};
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) / 2f, 0f), new PointF(width / 2f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, (0f - width) * 0.5f - 1f, height * 0.93f, width + 1f, height * 0.06f);
			g.DrawEllipse(batteryBackPen, (0f - width) * 0.5f - 1f, height * 0.93f, width + 1f, height * 0.06f);
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.5f - 1f, height * 0.1f, width + 1f, height * 0.86f);
			g.FillEllipse(batteryBackBrush, (0f - width) * 0.5f - 1f, height * 0.07f, width + 1f, height * 0.06f);
			linearGradientBrush.Dispose();
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) * 0.15f, 0f), new PointF(width * 0.15f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, (0f - width) * 0.15f, height * 0.085f, width * 0.3f, height * 0.02f);
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.15f, height * 0.03f, width * 0.3f, height * 0.065f);
			g.FillEllipse(batteryBackBrush, (0f - width) * 0.15f, height * 0.02f, width * 0.3f, height * 0.02f);
			linearGradientBrush.Dispose();
			Color colorFromValue = getColorFromValue(value_now);
			colorBlend.Colors = new Color[3]
			{
				colorFromValue,
				Color.WhiteSmoke,
				colorFromValue
			};
			float num = height * 0.86f - (value_paint - value_min) / (value_max - value_min) * height * 0.86f + height * 0.1f;
			if (num < height * 0.1f)
			{
				num = height * 0.1f;
			}
			if (num > height * 0.96f)
			{
				num = height * 0.96f;
			}
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) / 2f, 0f), new PointF(width / 2f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, (0f - width) * 0.5f - 1f, height * 0.93f, width + 1f, height * 0.06f);
			using (Pen pen = new Pen(colorFromValue))
			{
				g.DrawEllipse(pen, (0f - width) * 0.5f - 1f, height * 0.93f, width + 1f, height * 0.06f);
			}
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.5f - 1f, num, width + 1f, height * 0.86f - (num - height * 0.1f));
			using (Brush brush = new SolidBrush(colorFromValue))
			{
				g.FillEllipse(brush, (0f - width) * 0.5f - 1f, num - height * 0.03f, width + 1f, height * 0.06f);
			}
			linearGradientBrush.Dispose();
			g.TranslateTransform((0f - width) / 2f, 0f);
		}

		private Color getColorFromValue(float value)
		{
			if (value > (value_max - value_min) * value_color1 + value_min)
			{
				return color_1;
			}
			if (value > (value_max - value_min) * value_color2 + value_min)
			{
				return color_2;
			}
			if (value > (value_max - value_min) * value_color3 + value_min)
			{
				return color_3;
			}
			if (value > (value_max - value_min) * value_color4 + value_min)
			{
				return color_4;
			}
			return color_5;
		}

		private void ThreadPoolUpdateProgress(object obj)
		{
			try
			{
				int num = (int)obj;
				if (value_paint == value_now)
				{
					return;
				}
				float num2 = Math.Abs(value_paint - value_now) / 10f;
				if (num2 == 0f)
				{
					num2 = 1f;
				}
				while (value_paint != value_now)
				{
					Thread.Sleep(17);
					if (num != m_version)
					{
						break;
					}
					lock (hybirdLock)
					{
						float num3 = 0f;
						if (value_paint > value_now)
						{
							float num4 = value_paint - value_now;
							if (num4 > num2)
							{
								num4 = num2;
							}
							num3 = value_paint - num4;
						}
						else
						{
							float num5 = value_now - value_paint;
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
			base.Name = "HslBattery";
			base.Size = new System.Drawing.Size(93, 171);
			base.ResumeLayout(false);
		}
	}
}
