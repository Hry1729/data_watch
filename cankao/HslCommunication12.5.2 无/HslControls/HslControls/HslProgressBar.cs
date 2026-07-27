using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	public class HslProgressBar : ProgressBar
	{
		private ProgressStyle progressStyle = ProgressStyle.None;

		private Color progressColor = Color.Transparent;

		private int inflateWidth = 3;

		private bool useBackColor = false;

		private bool useSmallSquares = false;

		[Browsable(true)]
		[Description("获取或设置显示的文本样式")]
		[Category("HslControls")]
		[DefaultValue(typeof(ProgressStyle), "None")]
		public ProgressStyle ProgressStyle
		{
			get
			{
				return progressStyle;
			}
			set
			{
				progressStyle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件缩进的宽度信息")]
		[Category("HslControls")]
		[DefaultValue(3)]
		public int InflateWidth
		{
			get
			{
				return inflateWidth;
			}
			set
			{
				inflateWidth = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置进度条的前景色，如果为透明，则使用系统的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Transparent")]
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

		[Browsable(true)]
		[Description("获取或设置是否使用BackColor颜色来绘制背景色")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool UseBackColor
		{
			get
			{
				return useBackColor;
			}
			set
			{
				useBackColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置是否使用小方块来表示进度条的显示效果，当 ProgressColor 不为 Color.Transparent 时有效")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool UseSmallSquares
		{
			get
			{
				return useSmallSquares;
			}
			set
			{
				useSmallSquares = value;
				Invalidate();
			}
		}

		[Browsable(false)]
		public Func<int, int, string> CustmerTextTranslate { get; set; }

		public HslProgressBar()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		private string SizeToString(long size, long sizeBase = -1L)
		{
			if (sizeBase < 0)
			{
				sizeBase = size;
			}
			if (sizeBase < 1024)
			{
				return size + " B";
			}
			if (sizeBase < 1048576)
			{
				return ((float)size / 1024f).ToString("F1") + " K";
			}
			return ((float)size / 1048576f).ToString("F2") + " M";
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Rectangle clientRectangle = base.ClientRectangle;
			Graphics graphics = e.Graphics;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.ClientRectangle.Width, base.ClientRectangle.Height);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			Rectangle rectangle = new Rectangle(0, 0, width, height);
			if (useBackColor)
			{
				using (Brush brush = new SolidBrush(BackColor))
				{
					g.FillRectangle(brush, rectangle);
					using (Pen pen = new Pen(HslHelper.GetColorLight(BackColor, 30)))
					{
						g.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
					}
				}
			}
			else
			{
				ProgressBarRenderer.DrawHorizontalBar(g, rectangle);
			}
			rectangle.Inflate(-InflateWidth, -InflateWidth);
			if (base.Value > 0)
			{
				Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y, (int)Math.Round((double)base.Value * (double)rectangle.Width / (double)base.Maximum), rectangle.Height);
				if (progressColor == Color.Transparent)
				{
					ProgressBarRenderer.DrawHorizontalChunks(g, rectangle2);
				}
				else
				{
					using (Brush brush2 = new SolidBrush(progressColor))
					{
						if (UseSmallSquares)
						{
							int i;
							for (i = 0; i + 8 <= rectangle2.Width; i += 10)
							{
								g.FillRectangle(brush2, new Rectangle(rectangle2.X + i, rectangle2.Y, 8, rectangle2.Height));
							}
							int num = rectangle2.Width - i;
							if (num > 0)
							{
								g.FillRectangle(brush2, new Rectangle(rectangle2.X + i, rectangle2.Y, num, rectangle2.Height));
							}
						}
						else
						{
							g.FillRectangle(brush2, rectangle2);
						}
					}
				}
			}
			using (Brush brush3 = new SolidBrush(ForeColor))
			{
				if (progressStyle == ProgressStyle.None)
				{
					return;
				}
				if (progressStyle == ProgressStyle.Number)
				{
					g.DrawString($"{base.Value}/{base.Maximum}", Font, brush3, rectangle, HslHelper.StringFormatCenter);
				}
				else if (progressStyle == ProgressStyle.Size)
				{
					g.DrawString(SizeToString(base.Value, base.Maximum) + "/" + SizeToString(base.Maximum, -1L), Font, brush3, rectangle, HslHelper.StringFormatCenter);
				}
				else if (progressStyle == ProgressStyle.Percent)
				{
					g.DrawString((int)((long)base.Value * 100L / base.Maximum) + "%", Font, brush3, rectangle, HslHelper.StringFormatCenter);
				}
				else if (progressStyle == ProgressStyle.Customer)
				{
					string s = ((CustmerTextTranslate == null) ? string.Empty : CustmerTextTranslate(base.Value, base.Maximum));
					if (!string.IsNullOrEmpty(s))
					{
						g.DrawString(s, Font, brush3, rectangle, HslHelper.StringFormatCenter);
					}
				}
			}
		}
	}
}
