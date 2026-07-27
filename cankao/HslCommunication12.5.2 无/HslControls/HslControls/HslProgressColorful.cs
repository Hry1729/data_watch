using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("进度条控件，支持横向，竖向两种方向，支持颜色渐变色")]
	public class HslProgressColorful : UserControl
	{
		private StringFormat sf = null;

		private Color colorBorder = Color.Silver;

		private Color colorCenter = Color.WhiteSmoke;

		private Color colorTmp = Color.Tomato;

		private Brush backBrush = new SolidBrush(Color.DimGray);

		private int max = 100;

		private int m_value = 50;

		private int m_actual = 50;

		private int m_speed = 1;

		private bool useAnimation = false;

		private int m_version = 0;

		private HslProgressStyle m_progressStyle = HslProgressStyle.Vertical;

		private bool isTextRender = true;

		private Action m_UpdateAction;

		private IContainer components = null;

		[Description("获取或设置进度条的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Browsable(true)]
		public override Color BackColor
		{
			get
			{
				return base.BackColor;
			}
			set
			{
				base.BackColor = value;
				backBrush?.Dispose();
				backBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Description("获取或设置进度条的前景色")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "Tomato")]
		public Color ProgressColor
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

		[Description("获取或设置进度条的最大值，默认为100")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(100)]
		public int Max
		{
			get
			{
				return max;
			}
			set
			{
				if (value > 1)
				{
					max = value;
				}
				if (m_value > max)
				{
					m_value = max;
				}
				Invalidate();
			}
		}

		[Description("获取或设置当前进度条的值")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(50)]
		public int Value
		{
			get
			{
				return m_value;
			}
			set
			{
				if (value >= 0 && value <= max && value != m_value)
				{
					m_value = value;
					if (UseAnimation)
					{
						ThreadPool.QueueUserWorkItem(state: Interlocked.Increment(ref m_version), callBack: ThreadPoolUpdateProgress);
						return;
					}
					m_actual = value;
					Invalidate();
				}
			}
		}

		[Description("获取或设置是否显示进度文本")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(true)]
		public bool IsTextRender
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

		[Description("获取或设置进度条的边框颜色")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "Silver")]
		public Color BorderColor
		{
			get
			{
				return colorBorder;
			}
			set
			{
				colorBorder = value;
				Invalidate();
			}
		}

		[Description("获取或设置进度条中间的过渡色")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "WhiteSmoke")]
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

		[Description("获取或设置进度条的变化进度")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(1)]
		public int ValueChangeSpeed
		{
			get
			{
				return m_speed;
			}
			set
			{
				if (value >= 1)
				{
					m_speed = value;
				}
			}
		}

		[Description("获取或设置进度条变化的时候是否采用动画效果")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(false)]
		public bool UseAnimation
		{
			get
			{
				return useAnimation;
			}
			set
			{
				useAnimation = value;
			}
		}

		[Description("获取或设置进度条的样式")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(typeof(HslProgressStyle), "Vertical")]
		public HslProgressStyle ProgressStyle
		{
			get
			{
				return m_progressStyle;
			}
			set
			{
				m_progressStyle = value;
				Invalidate();
			}
		}

		public HslProgressColorful()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			m_UpdateAction = UpdateRender;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			if(false)
			{
				PaintHslControls(graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			try
			{
				g.FillRectangle(rect: new Rectangle(0, 0, width - 1, height - 1), brush: backBrush);
				ColorBlend colorBlend = new ColorBlend();
				colorBlend.Positions = new float[3] { 0f, 0.8f, 1f };
				colorBlend.Colors = new Color[3] { colorBorder, colorCenter, colorBorder };
				switch (m_progressStyle)
				{
				case HslProgressStyle.Vertical:
				{
					LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new PointF(0f, 0f), new PointF(width - 1, 0f), colorBorder, colorCenter);
					linearGradientBrush2.InterpolationColors = colorBlend;
					g.FillEllipse(linearGradientBrush2, 0, 0, width - 1, width - 1);
					g.FillRectangle(linearGradientBrush2, 0f, (float)width / 2f, width - 1, height - width);
					colorBlend.Colors = new Color[3]
					{
						colorTmp,
						HslHelper.GetColorLightFive(colorTmp),
						colorTmp
					};
					linearGradientBrush2.InterpolationColors = colorBlend;
					float num2 = (int)((long)m_actual * (long)(height - width) / max);
					g.FillRectangle(linearGradientBrush2, 0f, (float)height - num2 - 1f - (float)(width / 2), width - 1, num2);
					g.FillEllipse(linearGradientBrush2, 0f, (float)height - num2 - (float)width, width - 1, width - 1);
					g.FillEllipse(linearGradientBrush2, 0, height - width, width - 1, width - 1);
					linearGradientBrush2.Dispose();
					break;
				}
				case HslProgressStyle.Horizontal:
				{
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, height - 1), colorBorder, colorCenter);
					linearGradientBrush.InterpolationColors = colorBlend;
					g.FillEllipse(linearGradientBrush, width - height, 0, height - 1, height - 1);
					g.FillRectangle(linearGradientBrush, height / 2, 0, width - height - 1, height - 1);
					colorBlend.Colors = new Color[3]
					{
						colorTmp,
						HslHelper.GetColorLightFive(colorTmp),
						colorTmp
					};
					linearGradientBrush.InterpolationColors = colorBlend;
					float num = (int)((long)m_actual * (long)(width - height) / max);
					g.FillRectangle(linearGradientBrush, height / 2, 0f, num, height - 1);
					g.FillEllipse(linearGradientBrush, num, 0f, height - 1, height - 1);
					g.FillEllipse(linearGradientBrush, 0, 0, height - 1, height - 1);
					linearGradientBrush.Dispose();
					break;
				}
				}
				Rectangle rectangle = new Rectangle(0, 0, width - 1, height - 1);
				if (!isTextRender)
				{
					return;
				}
				string s = (long)m_actual * 100L / max + "%";
				using (Brush brush = new SolidBrush(ForeColor))
				{
					if (m_progressStyle != HslProgressStyle.Circular)
					{
						g.DrawString(s, Font, brush, rectangle, sf);
					}
					else
					{
						g.DrawString("Not supported", Font, brush, rectangle, sf);
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void ThreadPoolUpdateProgress(object obj)
		{
			try
			{
				int num = (int)obj;
				if (m_speed < 1)
				{
					m_speed = 1;
				}
				while (m_actual != m_value)
				{
					Thread.Sleep(17);
					if (num != m_version)
					{
						break;
					}
					int num2 = 0;
					if (m_actual > m_value)
					{
						int num3 = m_actual - m_value;
						if (num3 > m_speed)
						{
							num3 = m_speed;
						}
						num2 = m_actual - num3;
					}
					else
					{
						int num4 = m_value - m_actual;
						if (num4 > m_speed)
						{
							num4 = m_speed;
						}
						num2 = m_actual + num4;
					}
					m_actual = num2;
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

		private void UpdateRender()
		{
			Invalidate();
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
			base.Name = "HslProgressColorful";
			base.Size = new System.Drawing.Size(421, 17);
			base.ResumeLayout(false);
		}
	}
}
