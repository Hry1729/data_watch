using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace HslControls
{
	[Description("进度条控件，采用标准的线段绘制，支持横向和纵向的两种模式，支持动画效果")]
	public class HslProgressLine : UserControl
	{
		private StringFormat sf = null;

		private Color backColor = Color.LightGray;

		private Color progressColor = Color.Tomato;

		private int max = 100;

		private int m_value = 50;

		private int m_actual = 50;

		private int m_speed = 1;

		private bool useAnimation = false;

		private int m_version = 0;

		private HslProgressStyle m_progressStyle = HslProgressStyle.Horizontal;

		private bool isTextRender = true;

		private Action m_UpdateAction;

		private string textRenderFormat = string.Empty;

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

		[Description("获取或设置进度条的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Browsable(true)]
		public Color ProgressBackColor
		{
			get
			{
				return backColor;
			}
			set
			{
				backColor = value;
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

		[Description("获取或设置进度条的样式")]
		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(typeof(HslProgressStyle), "Horizontal")]
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

		[Description("获取或设置进度条的变化进度")]
		[Category("Appearance")]
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
				Invalidate();
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

		public HslProgressLine()
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
			if ( width < 3 || height < 2)
			{
				return;
			}
			int num = Math.Min(width, height);
			try
			{
				Rectangle rectangle = new Rectangle(0, 0, width - 1, height - 1);
				switch (m_progressStyle)
				{
				case HslProgressStyle.Vertical:
				{
					using (Pen pen3 = new Pen(backColor, width - 2))
					{
						pen3.StartCap = LineCap.Round;
						pen3.EndCap = LineCap.Round;
						g.DrawLine(pen3, width / 2, height - 1 - width / 2, width / 2, width / 2);
					}
					using (Pen pen4 = new Pen(progressColor, width - 2))
					{
						pen4.StartCap = LineCap.Round;
						pen4.EndCap = LineCap.Round;
						g.DrawLine(y2: height - 1 - (int)((long)m_actual * (long)(height - 2 - width) / max) - width / 2, pen: pen4, x1: width / 2, y1: height - 1 - width / 2, x2: width / 2);
					}
					break;
				}
				case HslProgressStyle.Horizontal:
				{
					using (Pen pen5 = new Pen(backColor, height - 2))
					{
						pen5.StartCap = LineCap.Round;
						pen5.EndCap = LineCap.Round;
						g.DrawLine(pen5, height / 2, height / 2, width - 1 - height / 2, height / 2);
					}
					using (Pen pen6 = new Pen(progressColor, height - 2))
					{
						pen6.StartCap = LineCap.Round;
						pen6.EndCap = LineCap.Round;
						g.DrawLine(x2: (int)((long)m_actual * (long)(width - 2 - height) / max) + 1 + height / 2, pen: pen6, x1: height / 2, y1: height / 2, y2: height / 2);
					}
					break;
				}
				case HslProgressStyle.Circular:
				{
					int num2 = num / 10;
					using (Pen pen = new Pen(backColor, num2))
					{
						pen.StartCap = LineCap.Round;
						pen.EndCap = LineCap.Round;
						g.DrawArc(pen, num2, num2, num - 1 - 2 * num2, num - 1 - 2 * num2, -90, 360);
					}
					using (Pen pen2 = new Pen(progressColor, num2))
					{
						pen2.StartCap = LineCap.Round;
						pen2.EndCap = LineCap.Round;
						g.DrawArc(pen2, num2, num2, num - 1 - 2 * num2, num - 1 - 2 * num2, -90, (int)((long)m_actual * 360L / max));
					}
					break;
				}
				}
				rectangle = new Rectangle(0, 0, width - 1, height - 1);
				if (!isTextRender)
				{
					return;
				}
				string s = (string.IsNullOrEmpty(textRenderFormat) ? ((long)m_actual * 100L / max + "%") : string.Format(textRenderFormat, Value, Max));
				using (Brush brush = new SolidBrush(ForeColor))
				{
					if (m_progressStyle == HslProgressStyle.Circular)
					{
						rectangle = new Rectangle(0, 0, num - 1, num - 1);
					}
					g.DrawString(s, Font, brush, rectangle, sf);
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
			base.Name = "HslProgressLine";
			base.Size = new System.Drawing.Size(390, 16);
			base.ResumeLayout(false);
		}
	}
}
