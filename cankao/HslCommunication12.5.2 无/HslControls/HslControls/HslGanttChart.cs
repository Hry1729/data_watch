using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个简单的甘特图控件，可以用来表示一天或是一个月里面各个环节占用的时间信息")]
	public class HslGanttChart : UserControl
	{
		private int[] data = new int[0];

		private Color[] colors = new Color[0];

		private StringFormat sf = null;

		private int timeStart = 0;

		private int timeMax = 24;

		private int timeCount = 86400;

		private int timeSegment = 6;

		private string timeFormate = "{0}H";

		private Color ganttBackColor = Color.LightGray;

		private Brush ganttBrush = new SolidBrush(Color.LightGray);

		private int leftRightOffect = 20;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

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
		[Description("获取或设置甘特图的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color GanttBackColor
		{
			get
			{
				return ganttBackColor;
			}
			set
			{
				ganttBackColor = value;
				ganttBrush.Dispose();
				ganttBrush = new SolidBrush(ganttBackColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置甘特图的时间的分段信息")]
		[Category("HslControls")]
		[DefaultValue(6)]
		public int TimeSegment
		{
			get
			{
				return timeSegment;
			}
			set
			{
				timeSegment = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置时间的起始信息，通常一天的起始为0，一个月的起始为1，年份的起始为自然年，比如2018")]
		[Category("HslControls")]
		[DefaultValue(0)]
		public int TimeStart
		{
			get
			{
				return timeStart;
			}
			set
			{
				timeStart = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置时间的结束信息，通常一天的结束为24，一个月的结束为29,30,31，年份的结束为自然年，比如2019")]
		[Category("HslControls")]
		[DefaultValue(24)]
		public int TimeMax
		{
			get
			{
				return timeMax;
			}
			set
			{
				timeMax = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置时间的总数信息，比如是按照天计算的甘特图，设置了1440，就是按照分钟计数，如果是86400，就是按照秒计数，这个传入的值密切相关")]
		[Category("HslControls")]
		[DefaultValue(86400)]
		public int TimeCount
		{
			get
			{
				return timeCount;
			}
			set
			{
				timeCount = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置时间的显示格式，可以设置为显示小时，分钟，天等等信息")]
		[Category("HslControls")]
		[DefaultValue("{0}H")]
		public string TimeFormate
		{
			get
			{
				return timeFormate;
			}
			set
			{
				timeFormate = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置两侧的距离宽度，当你的数字范围特别长的时候就有用，默认为20")]
		[Category("HslControls")]
		[DefaultValue(20)]
		public int LeftRightOffect
		{
			get
			{
				return leftRightOffect;
			}
			set
			{
				leftRightOffect = value;
				Invalidate();
			}
		}

		public HslGanttChart()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
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
			if(false)
			{
				if (hslValvesStyle == HslDirectionStyle.Horizontal)
				{
					PaintMain(g, width, height);
					return;
				}
				g.TranslateTransform(width, 0f);
				g.RotateTransform(90f);
				PaintMain(g, height, width);
				g.ResetTransform();
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			int num = leftRightOffect;
			int num2 = Font.Height + 2;
			Brush brush = new SolidBrush(ForeColor);
			for (int i = 0; i <= timeSegment; i++)
			{
				int num3 = (timeMax - timeStart) * i / timeSegment + timeStart;
				g.DrawString(layoutRectangle: new Rectangle((int)((width - (float)(2 * num)) * (float)i / (float)timeSegment + (float)num) - 50, 0, 100, num2), s: string.Format(timeFormate, num3), font: Font, brush: brush, format: sf);
			}
			g.SmoothingMode = SmoothingMode.None;
			RectangleF rect = new RectangleF(num, num2, width - (float)(2 * num), height - (float)num2);
			g.FillRectangle(ganttBrush, rect);
			int num4 = 0;
			for (int j = 0; j < data.Length; j++)
			{
				float num5 = (float)num4 * (width - (float)(2 * num)) / (float)timeCount + (float)num;
				float num6 = (float)data[j] * (width - (float)(2 * num)) / (float)timeCount;
				if (num6 > rect.Width - num5 + (float)num)
				{
					num6 = rect.Width - num5 + (float)num;
				}
				if (j < colors.Length)
				{
					using (Brush brush2 = new SolidBrush(colors[j]))
					{
						g.FillRectangle(brush2, new RectangleF(num5, num2, num6, rect.Height));
					}
				}
				num4 += data[j];
				if (num4 > timeCount)
				{
					break;
				}
			}
			brush.Dispose();
		}

		public void SetGanttChart(int[] data, Color[] colors)
		{
			if (data != null && colors != null)
			{
				this.data = data;
				this.colors = colors;
				Invalidate();
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
			base.Name = "HslGanttChart";
			base.Size = new System.Drawing.Size(333, 65);
			base.ResumeLayout(false);
		}
	}
}
