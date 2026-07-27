using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个水平的柱状图控件，支持显示柱状图信息，方便的设置数据源，然后显示出来，支持渐变色")]
	public class HslHorizonBarChart : UserControl
	{
		private int value_max_left = -1;

		private int value_min_left = 0;

		private int value_Segment = 5;

		private List<BarValue> barValues = new List<BarValue>();

		private Brush brush_deep = null;

		private Pen pen_normal = null;

		private Pen pen_dash = null;

		private bool useGradient = false;

		private Color color_deep = Color.DimGray;

		private Color color_dash = Color.LightGray;

		private bool value_IsRenderDashLine = true;

		private bool isShowBarValue = true;

		private string value_title = "";

		private float barPercentWidth = 0.8f;

		private bool isAoordinateRoundInt = false;

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
		[Description("获取或设置当前控件的文本")]
		[Category("HslControls")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Bindable(true)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				base.Text = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Black")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color ForeColor
		{
			get
			{
				return base.ForeColor;
			}
			set
			{
				base.ForeColor = value;
			}
		}

		[Category("HslControls")]
		[Description("获取或设置坐标轴及相关信息文本的颜色")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "DimGray")]
		public virtual Color ColorLinesAndText
		{
			get
			{
				return color_deep;
			}
			set
			{
				color_deep = value;
				InitializationColor();
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置本条形图控件是否使用渐进色")]
		[Browsable(true)]
		[DefaultValue(false)]
		public virtual bool UseGradient
		{
			get
			{
				return useGradient;
			}
			set
			{
				useGradient = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置虚线的颜色")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "LightGray")]
		public virtual Color ColorDashLines
		{
			get
			{
				return color_dash;
			}
			set
			{
				color_dash = value;
				pen_dash?.Dispose();
				pen_dash = new Pen(color_dash);
				pen_dash.DashStyle = DashStyle.Custom;
				pen_dash.DashPattern = new float[2] { 5f, 5f };
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置虚线是否进行显示")]
		[Browsable(true)]
		[DefaultValue(true)]
		public virtual bool IsRenderDashLine
		{
			get
			{
				return value_IsRenderDashLine;
			}
			set
			{
				value_IsRenderDashLine = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置图形的纵轴分段数")]
		[Browsable(true)]
		[DefaultValue(5)]
		public virtual int ValueSegment
		{
			get
			{
				return value_Segment;
			}
			set
			{
				value_Segment = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置图形的左纵坐标的最大值，该值必须大于最小值，该值为负数，最大值即为自动适配。")]
		[Browsable(true)]
		[DefaultValue(-1)]
		public virtual int ValueMaxLeft
		{
			get
			{
				return value_max_left;
			}
			set
			{
				value_max_left = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置图形的左纵坐标的最小值，该值必须小于最大值")]
		[Browsable(true)]
		[DefaultValue(0)]
		public virtual int ValueMinLeft
		{
			get
			{
				return value_min_left;
			}
			set
			{
				if (value < value_max_left)
				{
					value_min_left = value;
					Invalidate();
				}
			}
		}

		[Category("HslControls")]
		[Description("获取或设置图标的标题信息")]
		[Browsable(true)]
		[DefaultValue("")]
		public virtual string Title
		{
			get
			{
				return value_title;
			}
			set
			{
				value_title = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置是否显示柱状图的值文本")]
		[Browsable(true)]
		[DefaultValue(true)]
		public virtual bool IsShowBarValue
		{
			get
			{
				return isShowBarValue;
			}
			set
			{
				isShowBarValue = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置柱状图占平均宽度的百分比，默认0.8，即80%")]
		[Browsable(true)]
		[DefaultValue(0.8f)]
		public virtual float BarPercentWidth
		{
			get
			{
				return barPercentWidth;
			}
			set
			{
				if (value > 0f && value <= 1f)
				{
					barPercentWidth = value;
					Invalidate();
				}
			}
		}

		[Category("HslControls")]
		[Description("获取或设置纵轴是否强制使用整型。")]
		[Browsable(true)]
		[DefaultValue(false)]
		public virtual bool IsAoordinateRoundInt
		{
			get
			{
				return isAoordinateRoundInt;
			}
			set
			{
				isAoordinateRoundInt = value;
				Invalidate();
			}
		}

		public HslHorizonBarChart()
		{
			InitializeComponent();
			pen_dash = new Pen(color_dash);
			pen_dash.DashStyle = DashStyle.Custom;
			pen_dash.DashPattern = new float[2] { 5f, 5f };
			InitializationColor();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			ForeColor = Color.Black;
			if (GetService(typeof(IDesignerHost)) != null || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
			{
				barValues.Add(new BarValue
				{
					Text = "1",
					Value = 5.0
				});
				barValues.Add(new BarValue
				{
					Text = "2",
					Value = 4.0
				});
				barValues.Add(new BarValue
				{
					Text = "3",
					Value = 3.0
				});
				barValues.Add(new BarValue
				{
					Text = "4",
					Value = 2.0
				});
				barValues.Add(new BarValue
				{
					Text = "5",
					Value = 1.0
				});
			}
		}

		private void InitializationColor()
		{
			pen_normal?.Dispose();
			brush_deep?.Dispose();
			pen_normal = new Pen(color_deep);
			brush_deep = new SolidBrush(color_deep);
		}

		public void SetDataSource(List<BarValue> data)
		{
			barValues = data;
			Invalidate();
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
			List<BarValue> list = barValues;
			int num = ((list != null && list.Count > 0) ? HslHelper.CalculateMaxSectionFrom(barValues.Select((BarValue m) => m.Value).ToArray()) : 5);
			if (value_max_left > 0)
			{
				num = value_max_left;
			}
			int num2 = 30;
			List<BarValue> list2 = barValues;
			if (list2 != null && list2.Count > 0)
			{
				num2 = (int)barValues.Select((BarValue m) => g.MeasureString(m.Text, Font).Width).Max() + 6;
			}
			if (num2 < 30)
			{
				num2 = 30;
			}
			int num3 = 20;
			int num4 = 25;
			int num5 = 25;
			Point[] array = new Point[3]
			{
				new Point(num2, num4 - 8),
				new Point(num2, height - num5),
				new Point(width - num3, height - num5)
			};
			g.DrawLine(pen_normal, array[0], array[1]);
			g.DrawLine(pen_normal, array[1], array[2]);
			HslHelper.PaintTriangle(g, brush_deep, array[0], 4, GraphDirection.Upward);
			HslHelper.PaintTriangle(g, brush_deep, array[2], 4, GraphDirection.Rightward);
			for (int i = 0; i <= value_Segment; i++)
			{
				float num6 = (float)((double)i * (double)(num - value_min_left) / (double)value_Segment + (double)value_min_left);
				if (isAoordinateRoundInt)
				{
					num6 = (float)Math.Round(num6, 0);
				}
				float num7 = HslHelper.ComputePaintLocationX(num, value_min_left, width - num2 - num3, num6) + (float)num2;
				g.DrawLine(pen_normal, num7, height - num5, num7, height - num5 + 4);
				RectangleF layoutRectangle = new RectangleF(num7 - 100f, height - num5, 200f, num5);
				g.DrawString(num6.ToString(), Font, brush_deep, layoutRectangle, HslHelper.StringFormatCenter);
				if (i > 0 && value_IsRenderDashLine)
				{
					g.DrawLine(pen_dash, num7, num4, num7, height - num5);
				}
			}
			if (barValues == null || barValues.Count == 0)
			{
				return;
			}
			float num8 = (float)(height - num4 - 1 - num5) * 1f / (float)barValues.Count;
			for (int j = 0; j < barValues.Count; j++)
			{
				if (num8 * barPercentWidth < 1f)
				{
					float num9 = 1f;
				}
				RectangleF rectangleF = new RectangleF(width: HslHelper.ComputePaintLocationX(num, value_min_left, width - num2 - num3, (float)barValues[j].Value) + (float)num2 - (float)num2, x: num2, y: (float)j * num8 + (1f - barPercentWidth) / 2f * num8 + (float)num4, height: num8 * barPercentWidth);
				Color barColor = barValues[j].BarColor;
				if (useGradient)
				{
					if (rectangleF.Width > 0f)
					{
						using (LinearGradientBrush brush = new LinearGradientBrush(new PointF(rectangleF.X, rectangleF.Y), new PointF(rectangleF.X + rectangleF.Width, rectangleF.Y), HslHelper.GetColorLight(barColor), barColor))
						{
							g.FillRectangle(brush, rectangleF);
						}
					}
				}
				else
				{
					using (Brush brush2 = new SolidBrush(barColor))
					{
						g.FillRectangle(brush2, rectangleF);
					}
				}
				if (!string.IsNullOrEmpty(barValues[j].Text))
				{
					RectangleF layoutRectangle2 = new RectangleF(0f, (float)j * num8 + (float)num4, num2 - 3, num8);
					g.DrawString(barValues[j].Text, Font, brush_deep, layoutRectangle2, HslHelper.StringFormatRight);
				}
				if (!isShowBarValue)
				{
					continue;
				}
				string s = string.Format(barValues[j].BarValueFormat, barValues[j].Value);
				int num11 = Convert.ToInt32(g.MeasureString(s, Font).Width + 6f);
				if (rectangleF.Width >= (float)((width - num2 - num3) / 2) || rectangleF.Width >= (float)num11)
				{
					using (Brush brush3 = new SolidBrush(ForeColor))
					{
						rectangleF.Width -= 3f;
						g.DrawString(s, Font, brush3, rectangleF, HslHelper.StringFormatRight);
					}
				}
				else
				{
					using (Brush brush4 = new SolidBrush(barColor))
					{
						RectangleF layoutRectangle3 = new RectangleF(rectangleF.X + rectangleF.Width + 3f, rectangleF.Y, (float)(width - num2) - rectangleF.Width, rectangleF.Height);
						g.DrawString(s, Font, brush4, layoutRectangle3, HslHelper.StringFormatLeft);
					}
				}
			}
			if (!string.IsNullOrEmpty(value_title))
			{
				g.DrawString(value_title, Font, brush_deep, new Rectangle(0, 0, width - 1, num4), HslHelper.StringFormatCenter);
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
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.Transparent;
			base.Name = "HslHorizonBarChart";
			base.Size = new System.Drawing.Size(456, 179);
			base.ResumeLayout(false);
		}
	}
}
