using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个温度计控件，支持两种信息显示，也可以用来做纵向的进度条")]
	public class HslThermometer : UserControl
	{
		private StringFormat sf = null;

		private float value_start = -20f;

		private float value_max = 60f;

		private float value_current = 0f;

		private int m_version = 0;

		private float value_paint = 0f;

		private object hybirdLock;

		private Action m_UpdateAction;

		private bool isRenderText = false;

		private int segment_count = 8;

		private Color colorBorder = Color.Silver;

		private Color colorCenter = Color.WhiteSmoke;

		private Color colorTmp = Color.Tomato;

		private Color colorTmpBack = Color.LightGray;

		private Brush brushback = new SolidBrush(Color.LightGray);

		private string leftUnitText = "℃";

		private string rightUnitText = "°F";

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
		[Description("获取或设置数值的起始值，默认为0")]
		[DefaultValue(-20f)]
		public float ValueStart
		{
			get
			{
				if (value_max <= value_start)
				{
					return value_start + 1f;
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
		[DefaultValue(60f)]
		public float ValueMax
		{
			get
			{
				if (value_max <= value_start)
				{
					return value_start + 1f;
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
		[Description("获取或设置温度计的分割段数，最小为2，最大100")]
		[DefaultValue(8)]
		public int SegmentCount
		{
			get
			{
				return segment_count;
			}
			set
			{
				if (value > 1 && value < 100)
				{
					segment_count = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置温度计的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		public Color TemperatureColor
		{
			get
			{
				return colorTmp;
			}
			set
			{
				colorTmp = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置温度计的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Silver")]
		public Color TemperatureBackColor
		{
			get
			{
				return colorTmpBack;
			}
			set
			{
				colorTmpBack = value;
				brushback.Dispose();
				brushback = new SolidBrush(colorTmpBack);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置是否显示温度计的数值")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool IsRenderText
		{
			get
			{
				return isRenderText;
			}
			set
			{
				isRenderText = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左侧的单位显示文本，默认是℃")]
		[Category("HslControls")]
		[DefaultValue("℃")]
		public string LeftUnitText
		{
			get
			{
				return leftUnitText;
			}
			set
			{
				leftUnitText = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左侧的单位显示文本，默认是°F")]
		[Category("HslControls")]
		[DefaultValue("°F")]
		public string RightUnitText
		{
			get
			{
				return rightUnitText;
			}
			set
			{
				rightUnitText = value;
				Invalidate();
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Func<float, float> RightDataTransfer { get; set; }

		public HslThermometer()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			RightDataTransfer = (float m) => m * 1.8f + 32f;
			hybirdLock = new object();
			m_UpdateAction = base.Invalidate;
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
			Brush brush = new SolidBrush(ForeColor);
			Pen pen = new Pen(ForeColor, 1f);
			float num = (float)width * 0.1f;
			float num2 = (float)width * 0.2f;
			float num3 = num + 40f;
			float num4 = (float)height - num3 - 20f - num2 * 2f;
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[4] { 0f, 0.3f, 0.8f, 1f };
			colorBlend.Colors = new Color[4] { colorCenter, colorBorder, colorCenter, colorBorder };
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF((float)width / 2f - num * 0.7f, 0f), new PointF((float)width / 2f + num * 0.7f, 0f), colorBorder, colorCenter);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(brushback, (float)width / 2f - num, num3 - num, num * 2f, num * 2f);
			g.FillEllipse(brushback, (float)width / 2f - num2, num3 + num4, num2 * 2f, num2 * 2f);
			g.FillRectangle(brushback, (float)width / 2f - num, num3, num * 2f, num4 + num2);
			for (int i = 0; i <= segment_count; i++)
			{
				float num5 = (float)i * (value_max - value_start) / (float)segment_count + value_start;
				float num6 = num4 - 10f - (num5 - value_start) / (value_max - value_start) * (num4 - 10f) + num3;
				if (i > 0)
				{
					for (int j = 1; j < 10; j++)
					{
						float num7 = (num4 - 10f) / (float)segment_count / 10f * (float)j;
						if (j == 5)
						{
							g.DrawLine(pen, (float)width / 2f - num - (float)width * 0.14f, num6 + num7, (float)width / 2f - num - 4f, num6 + num7);
							g.DrawLine(pen, (float)width / 2f + num + (float)width * 0.14f, num6 + num7, (float)width / 2f + num + 4f, num6 + num7);
						}
						else
						{
							g.DrawLine(pen, (float)width / 2f - num - (float)width * 0.1f, num6 + num7, (float)width / 2f - num - 4f, num6 + num7);
							g.DrawLine(pen, (float)width / 2f + num + (float)width * 0.1f, num6 + num7, (float)width / 2f + num + 4f, num6 + num7);
						}
					}
				}
				g.DrawLine(pen, (float)width / 2f - num - (float)width * 0.3f, num6, (float)width / 2f - num - 4f, num6);
				g.DrawLine(pen, (float)width / 2f + num + 4f, num6, (float)width / 2f + num + (float)width * 0.3f, num6);
				sf.Alignment = StringAlignment.Far;
				g.DrawString(rightUnitText, Font, brush, new RectangleF(5f, 0f, width - 10, num3 - num), sf);
				g.DrawString(num5.ToString(), Font, brush, new RectangleF(0f, num6 - (float)Font.Height - 1f, (float)width / 2f - num - (float)width * 0.14f, Font.Height + 1), sf);
				sf.Alignment = StringAlignment.Near;
				g.DrawString(leftUnitText, Font, brush, new RectangleF(5f, 0f, width - 10, num3 - num), sf);
				if (RightDataTransfer != null)
				{
					g.DrawString(Math.Round(RightDataTransfer(num5)).ToString(), Font, brush, new RectangleF((float)width / 2f + num + (float)width * 0.1f + 5f, num6 - (float)Font.Height - 1f, (float)width * 0.4f, Font.Height + 1), sf);
				}
			}
			g.FillEllipse(linearGradientBrush, (float)width / 2f - num * 0.7f, num3 - num * 0.7f, num * 1.4f, num * 1.4f);
			g.FillRectangle(linearGradientBrush, (float)width / 2f - num * 0.7f, num3, num * 1.4f, num4 + num2);
			colorBlend.Colors = new Color[4] { colorTmp, colorTmp, colorCenter, colorTmp };
			linearGradientBrush.InterpolationColors = colorBlend;
			float num8 = num4 - 10f - (value_paint - value_start) / (value_max - value_start) * (num4 - 10f) + num3;
			if (num8 < num3 - 5f)
			{
				num8 = num3 - 5f;
			}
			if (num8 > num3 + num4 + 5f)
			{
				num8 = num3 + num4 + 5f;
			}
			g.FillRectangle(linearGradientBrush, (float)width / 2f - num * 0.7f, num8, num * 1.4f, num4 + num2 + num3 - num8);
			using (Brush brush2 = new SolidBrush(colorTmp))
			{
				g.FillEllipse(brush2, (float)width / 2f - num * 0.7f, num8 - 1f, num * 1.4f, 3f);
			}
			RectangleF rect = new RectangleF((float)width / 2f - num2 * 0.85f, num3 + num4 + num2 * 0.15f, num2 * 1.7f, num2 * 1.7f);
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(rect);
			PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterPoint = new PointF((float)width / 2f + num2 * 0.2f, num3 + num4 + num2 * 0.7f);
			ColorBlend colorBlend2 = new ColorBlend();
			colorBlend2.Positions = new float[2] { 0f, 1f };
			colorBlend2.Colors = new Color[2] { colorTmp, colorCenter };
			pathGradientBrush.InterpolationColors = colorBlend2;
			g.FillEllipse(pathGradientBrush, rect);
			using (Pen pen2 = new Pen(colorTmp))
			{
				g.DrawEllipse(pen2, rect);
			}
			pathGradientBrush.Dispose();
			graphicsPath.Dispose();
			if (isRenderText)
			{
				sf.Alignment = StringAlignment.Center;
				g.DrawString(value_current.ToString(), Font, brush, new RectangleF((float)width / 2f - num2, num3 + num4, num2 * 2f, num2 * 2f), sf);
			}
			linearGradientBrush.Dispose();
			brush.Dispose();
			pen.Dispose();
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
			base.Name = "HslThermometer";
			base.Size = new System.Drawing.Size(140, 201);
			base.ResumeLayout(false);
		}
	}
}
