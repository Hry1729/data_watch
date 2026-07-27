using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace HslControls
{
	[Description("饼图控件，支持设置颜色，是否显示百分比功能")]
	public class HslPieChart : UserControl
	{
		private Color percentColor = Color.DodgerBlue;

		private Brush percentBrush = new SolidBrush(Color.DodgerBlue);

		private Color borderColor = Color.DodgerBlue;

		private Pen borderPen = new Pen(Color.DodgerBlue, 1f);

		private HslPieItem[] pieItems = new HslPieItem[0];

		private Random random = null;

		private int margin = 50;

		private bool m_IsRenderPercent = false;

		private bool m_IsRenderSmall = true;

		private string percenFormat = "{0:F2}%";

		private float startAngle = 0f;

		private RotateDirection rotateDirection = RotateDirection.AntiClockWise;

		private IContainer components = null;

		[Browsable(true)]
		[Category("HslControls")]
		[DefaultValue(false)]
		[Description("获取或设置是否显示百分比占用")]
		public bool IsRenderPercent
		{
			get
			{
				return m_IsRenderPercent;
			}
			set
			{
				m_IsRenderPercent = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置是否显示占比很小的文本信息")]
		[DefaultValue(true)]
		public bool IsRenderSmall
		{
			get
			{
				return m_IsRenderSmall;
			}
			set
			{
				m_IsRenderSmall = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置文本距离，单位为像素，默认50")]
		[DefaultValue(50)]
		public int TextMargin
		{
			get
			{
				return margin;
			}
			set
			{
				margin = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置文百分比文字的颜色信息")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public Color PercentColor
		{
			get
			{
				return percentColor;
			}
			set
			{
				percentColor = value;
				percentBrush.Dispose();
				percentBrush = new SolidBrush(percentColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置边框的颜色信息")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
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
				borderPen = new Pen(borderColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置文百分比文字的格式化信息")]
		[DefaultValue("{0:F2}%")]
		public string PercentFormat
		{
			get
			{
				return percenFormat;
			}
			set
			{
				percenFormat = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置初始的角度信息，默认为0，可以设置 0 - 360 范围")]
		[DefaultValue("0")]
		public float StartAngle
		{
			get
			{
				return startAngle;
			}
			set
			{
				for (startAngle = value; startAngle < 0f; startAngle += 360f)
				{
				}
				while (startAngle > 360f)
				{
					startAngle -= 360f;
				}
				Invalidate();
			}
		}

		[Browsable(true)]
		[Category("HslControls")]
		[Description("获取或设置当前饼图的旋转方向，选择 ClockWise 为顺时针，AntiClockWise 为逆时针")]
		[DefaultValue(typeof(RotateDirection), "AntiClockWise")]
		public RotateDirection RotateDirection
		{
			get
			{
				return rotateDirection;
			}
			set
			{
				rotateDirection = value;
				Invalidate();
			}
		}

		public HslPieChart()
		{
			InitializeComponent();
			random = new Random();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			pieItems = new HslPieItem[0];
			if (GetService(typeof(IDesignerHost)) != null || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
			{
				pieItems = new HslPieItem[5]
				{
					new HslPieItem
					{
						Name = "数据一",
						Value = 30,
						PieColor = Color.DodgerBlue
					},
					new HslPieItem
					{
						Name = "数据二",
						Value = 10,
						PieColor = Color.Purple
					},
					new HslPieItem
					{
						Name = "数据三",
						Value = 16,
						PieColor = Color.Tomato
					},
					new HslPieItem
					{
						Name = "数据四",
						Value = 15,
						PieColor = Color.Orange
					},
					new HslPieItem
					{
						PieColor = Color.Wheat,
						Name = "数据五",
						Value = 12
					}
				};
			}
		}

		private Point GetCenterPoint(int width, int height, out int squareWidth)
		{
			if (width > height)
			{
				squareWidth = height / 2 - margin - 8;
				return new Point(width / 2 - 1, height / 2 - 1);
			}
			squareWidth = width / 2 - margin - 8;
			return new Point(width / 2 - 1, height / 2 - 1);
		}

		private Color GetRandomColor()
		{
			int num = random.Next(230);
			int num2 = random.Next(230);
			return Color.FromArgb(num, num2, (num + num2 > 350) ? random.Next(100) : (random.Next(100) + 100));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				PaintMain(g, width, height);
			}
		}

		private void PaintMain(Graphics g, int width, int height)
		{
			int squareWidth;
			Point centerPoint = GetCenterPoint(width, height, out squareWidth);
			Rectangle rectangle = new Rectangle(centerPoint.X - squareWidth, centerPoint.Y - squareWidth, squareWidth + squareWidth, squareWidth + squareWidth);
			if (squareWidth > 0 && pieItems.Length != 0)
			{
				g.FillEllipse(Brushes.AliceBlue, rectangle);
				g.DrawEllipse(borderPen, rectangle);
				Rectangle rect = new Rectangle(rectangle.X - centerPoint.X, rectangle.Y - centerPoint.Y, rectangle.Width, rectangle.Height);
				g.TranslateTransform(centerPoint.X, centerPoint.Y);
				g.RotateTransform(90f);
				g.DrawLine(Pens.DimGray, 0, 0, squareWidth, 0);
				int num = pieItems.Sum((HslPieItem item) => item.Value);
				float num2 = 0f - startAngle;
				float num3 = num2 - 90f;
				for (int i = 0; i < pieItems.Length; i++)
				{
					Pen pen = new Pen(pieItems[i].LineColor, 1f);
					SolidBrush solidBrush = new SolidBrush(pieItems[i].PieColor);
					SolidBrush solidBrush2 = new SolidBrush(pieItems[i].FontColor);
					float num4 = g.MeasureString(pieItems[i].Name, Font).Width + 5f;
					float num5 = 0f;
					num5 = ((num != 0) ? Convert.ToSingle((double)pieItems[i].Value * 1.0 / (double)num * 360.0) : ((float)(360 / pieItems.Length)));
					float num6 = ((rotateDirection != RotateDirection.AntiClockWise) ? 1 : (-1));
					g.FillPie(solidBrush, rect, startAngle, num6 * num5);
					g.RotateTransform(startAngle);
					g.RotateTransform(num6 * num5 / 2f);
					if (num5 < 2f && !IsRenderSmall)
					{
						num2 += (0f - num6) * num5;
					}
					else
					{
						num2 += (0f - num6) * num5 / 2f;
						float num7 = 0f;
						num7 = ((rotateDirection != RotateDirection.AntiClockWise) ? ((num2 + 360f < 0f) ? (num2 + 360f + 360f) : (num2 + 360f)) : ((num2 < 0f) ? (num2 + 360f) : num2));
						int num8 = 15;
						if (num7 < 45f || num7 > 315f)
						{
							num8 = 20;
						}
						if (num7 > 135f && num7 < 225f)
						{
							num8 = 20;
						}
						g.DrawLine(pen, squareWidth * 2 / 3, 0, squareWidth + num8, 0);
						g.TranslateTransform(squareWidth + num8, 0f);
						if (Math.Abs(num2 - num3) < 5f)
						{
						}
						num3 = num2;
						if (num7 < 180f)
						{
							g.RotateTransform(num7 - 90f);
							g.DrawLine(pen, 0f, 0f, num4, 0f);
							g.DrawString(pieItems[i].Name, Font, solidBrush2, new Point(0, -Font.Height));
							if (IsRenderPercent)
							{
								g.DrawString(string.Format(percenFormat, num5 * 100f / 360f), Font, percentBrush, new Point(0, 1));
							}
							g.RotateTransform(90f - num7);
						}
						else
						{
							g.RotateTransform(num7 - 270f);
							g.DrawLine(pen, 0f, 0f, num4, 0f);
							g.TranslateTransform(num4 - 3f, 0f);
							g.RotateTransform(180f);
							g.DrawString(pieItems[i].Name, Font, solidBrush2, new Point(0, -Font.Height));
							if (IsRenderPercent)
							{
								g.DrawString(string.Format(percenFormat, num5 * 100f / 360f), Font, percentBrush, new Point(0, 1));
							}
							g.RotateTransform(-180f);
							g.TranslateTransform(0f - num4 + 3f, 0f);
							g.RotateTransform(270f - num7);
						}
						g.TranslateTransform(-squareWidth - num8, 0f);
						g.RotateTransform(num6 * num5 / 2f);
						num2 += (0f - num6) * num5 / 2f;
					}
					g.RotateTransform(0f - startAngle);
					solidBrush.Dispose();
					pen.Dispose();
					solidBrush2.Dispose();
				}
				g.ResetTransform();
				return;
			}
			g.FillEllipse(Brushes.AliceBlue, rectangle);
			g.DrawEllipse(borderPen, rectangle);
			using (SolidBrush brush = new SolidBrush(ForeColor))
			{
				g.DrawString("空", Font, brush, rectangle, HslHelper.StringFormatCenter);
			}
		}

		public void SetDataSource(HslPieItem[] source)
		{
			if (source != null)
			{
				pieItems = source;
				Invalidate();
			}
		}

		public void SetDataSource(string[] names, int[] values)
		{
			if (names == null)
			{
				throw new ArgumentNullException("names");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (names.Length != values.Length)
			{
				throw new Exception("两个数组的长度不一致！");
			}
			pieItems = new HslPieItem[names.Length];
			for (int i = 0; i < names.Length; i++)
			{
				pieItems[i] = new HslPieItem
				{
					Name = names[i],
					Value = values[i],
					PieColor = GetRandomColor()
				};
			}
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
			base.Name = "HslPieChart";
			base.ResumeLayout(false);
		}
	}
}
