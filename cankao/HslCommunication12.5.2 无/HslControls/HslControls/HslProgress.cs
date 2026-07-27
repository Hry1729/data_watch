using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("进度条控件，支持横向，竖向，圆形三种样式")]
	public class HslProgress : UserControl
	{
		private StringFormat sf = null;

		private Brush backBrush = new SolidBrush(Color.DimGray);

		private Color progressColor = Color.Tomato;

		private Brush progressBrush = new SolidBrush(Color.Tomato);

		private Color borderColor = Color.DimGray;

		private Pen borderPen = new Pen(Color.DimGray, 1f);

		private int max = 100;

		private int m_value = 50;

		private int m_actual = 50;

		private int m_speed = 1;

		private bool useAnimation = false;

		private int m_version = 0;

		private HslProgressStyle m_progressStyle = HslProgressStyle.Vertical;

		private bool isTextRender = true;

		private string textRenderFormat = string.Empty;

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
				return progressColor;
			}
			set
			{
				progressColor = value;
				progressBrush?.Dispose();
				progressBrush = new SolidBrush(value);
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
		[DefaultValue(typeof(Color), "DimGray")]
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				borderPen?.Dispose();
				borderPen = new Pen(value);
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

		[Description("获取或设置自定义的格式化文本信息")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue("")]
		public string TextRenderFormat
		{
			get
			{
				return textRenderFormat;
			}
			set
			{
				textRenderFormat = value;
				Invalidate();
			}
		}

		public HslProgress()
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
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			try
			{
				g.FillRectangle(rect: new Rectangle(0, 0, width - 1, height - 1), brush: backBrush);
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				switch (m_progressStyle)
				{
				case HslProgressStyle.Vertical:
				{
					int num = (int)((long)m_actual * (long)(height - 2) / max);
					g.FillRectangle(rect: new Rectangle(0, height - 1 - num, width - 1, num), brush: progressBrush);
					break;
				}
				case HslProgressStyle.Horizontal:
					g.FillRectangle(rect: new Rectangle(0, 0, (int)((long)m_actual * (long)(width - 2) / max) + 1, height - 1), brush: progressBrush);
					break;
				}
				Rectangle rectangle = new Rectangle(0, 0, width - 1, height - 1);
				if (isTextRender)
				{
					string s = (string.IsNullOrEmpty(textRenderFormat) ? ((long)m_actual * 100L / max + "%") : string.Format(textRenderFormat, Value, Max));
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
				if (m_progressStyle != HslProgressStyle.Circular)
				{
					g.DrawRectangle(borderPen, rectangle);
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
			this.BackColor = System.Drawing.Color.DimGray;
			base.Name = "HslProgress";
			base.Size = new System.Drawing.Size(120, 97);
			base.ResumeLayout(false);
		}
	}
}
