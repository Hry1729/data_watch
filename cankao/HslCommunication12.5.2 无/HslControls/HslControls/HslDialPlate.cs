using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个新的仪表盘控件，方便的用来显示流量计等情况")]
	public class HslDialPlate : UserControl
	{
		private StringFormat sf = null;

		private Color boderColor = Color.FromArgb(0, 0, 0);

		private Color boderColor2 = Color.FromArgb(228, 229, 229);

		private Color smallScaleColor = Color.Gray;

		private Pen borderPen = new Pen(Color.FromArgb(0, 0, 0));

		private Color centerColor = Color.Gray;

		private Color dialPlateBackgroundColor = Color.White;

		private bool isRenderSegmentText = true;

		private int segment = 8;

		private float maxValue = 100f;

		private float minValue = 0f;

		private float value = 0f;

		private string unitText = "";

		private MarkValueCollection markValues;

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
		[Description("获取或设置仪表控件的中心背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		public virtual Color CenterColor
		{
			get
			{
				return centerColor;
			}
			set
			{
				centerColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置分类器控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "0, 0, 0")]
		public virtual Color BorderColor
		{
			get
			{
				return boderColor;
			}
			set
			{
				boderColor = value;
				borderPen.Dispose();
				borderPen = new Pen(boderColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件内边缘的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "228, 229, 229")]
		public virtual Color BoderColor2
		{
			get
			{
				return boderColor2;
			}
			set
			{
				boderColor2 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置小刻度的颜色信息，如果设置为透明色，则不显示")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		public Color SmallScaleColor
		{
			get
			{
				return smallScaleColor;
			}
			set
			{
				smallScaleColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前仪表的主背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
		public virtual Color DialPlateBackgroundColor
		{
			get
			{
				return dialPlateBackgroundColor;
			}
			set
			{
				dialPlateBackgroundColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前仪表是否显示文本信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool IsRenderSegmentText
		{
			get
			{
				return isRenderSegmentText;
			}
			set
			{
				isRenderSegmentText = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前仪表的分割数量")]
		[Category("HslControls")]
		[DefaultValue(8)]
		public virtual int Segment
		{
			get
			{
				return segment;
			}
			set
			{
				if (segment > 0)
				{
					segment = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前仪表控件的最小值")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float MinValue
		{
			get
			{
				return minValue;
			}
			set
			{
				minValue = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前仪表控件的最大值")]
		[Category("HslControls")]
		[DefaultValue(100f)]
		public virtual float MaxValue
		{
			get
			{
				return maxValue;
			}
			set
			{
				maxValue = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置仪表控件的当前值")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float Value
		{
			get
			{
				return value;
			}
			set
			{
				this.value = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置显示的单位文本信息")]
		[Category("HslControls")]
		[DefaultValue("")]
		public virtual string UnitText
		{
			get
			{
				return unitText;
			}
			set
			{
				unitText = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置当前辅助标记的集合。")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public virtual MarkValueCollection MarkValues => markValues;

		public HslDialPlate()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			markValues = new MarkValueCollection();
			markValues.control = this;
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
			if ( base.Width > 15 && base.Height > 15)
			{
				PaintMain(g, Math.Min(width, height), Math.Min(width, height));
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			if (maxValue <= minValue)
			{
				return;
			}
			g.TranslateTransform(width / 2f, width / 2f);
			Font font = new Font(Font.FontFamily, width / 30f);
			Brush brush = new SolidBrush(ForeColor);
			using (Brush brush2 = new SolidBrush(dialPlateBackgroundColor))
			{
				g.FillEllipse(brush2, new RectangleF((0f - width) / 2f + 1f, (0f - width) / 2f + 1f, width - 3f, width - 3f));
			}
			borderPen.Width = width / 14f;
			borderPen.Color = BoderColor2;
			g.DrawEllipse(borderPen, new RectangleF((0f - width) / 2f + borderPen.Width / 2f, (0f - width) / 2f + borderPen.Width / 2f, width - borderPen.Width - 1f, width - borderPen.Width - 1f));
			borderPen.Color = ForeColor;
			borderPen.Width = 1f;
			if (segment > 0)
			{
				float num = 360f / (float)segment;
				if (isRenderSegmentText)
				{
					for (int i = 0; i < segment; i++)
					{
						float currentValue = (maxValue - minValue) * (float)i / (float)segment + minValue;
						if (markValues.FirstOrDefault((MarkValue m) => m.Value == currentValue) == null)
						{
							float num2 = width * 0.36f - (float)font.Height;
							float num3 = 360f / (float)segment * (float)i;
							g.RotateTransform(num3);
							g.DrawLine(borderPen, 0f, width * 0.365f, 0f, width * 0.4f);
							if (smallScaleColor != Color.Transparent)
							{
								borderPen.Color = smallScaleColor;
								for (int j = 0; j < 10; j++)
								{
									g.RotateTransform(num / 10f);
									if (j < 9)
									{
										g.DrawLine(borderPen, 0f, (j == 4) ? (width * 0.378f) : (width * 0.39f), 0f, width * 0.4f);
									}
								}
								borderPen.Color = ForeColor;
								g.RotateTransform(0f - num);
							}
							g.RotateTransform(0f - num3);
							g.DrawString(layoutRectangle: new RectangleF((float)((double)num2 * Math.Cos((double)(num3 + 90f) / 360.0 * 2.0 * Math.PI)) - 100f, (float)((double)num2 * Math.Sin((double)(num3 + 90f) / 360.0 * 2.0 * Math.PI)) - (float)(font.Height / 2), 200f, font.Height), s: currentValue.ToString(), font: font, brush: brush, format: sf);
						}
						else
						{
							if (!(smallScaleColor != Color.Transparent))
							{
								continue;
							}
							float num4 = width * 0.36f - (float)font.Height;
							float num5 = 360f / (float)segment * (float)i;
							g.RotateTransform(num5);
							borderPen.Color = smallScaleColor;
							for (int k = 0; k < 10; k++)
							{
								g.RotateTransform(num / 10f);
								if (k < 9)
								{
									g.DrawLine(borderPen, 0f, (k == 4) ? (width * 0.378f) : (width * 0.39f), 0f, width * 0.4f);
								}
							}
							borderPen.Color = ForeColor;
							g.RotateTransform(0f - num);
							g.RotateTransform(0f - num5);
						}
					}
					for (int l = 0; l < markValues.Count; l++)
					{
						num = (markValues[l].Value - minValue) / (maxValue - minValue) * 360f;
						g.RotateTransform(num);
						using (Pen pen = new Pen(markValues[l].Color, markValues[l].LineWidth))
						{
							g.DrawLine(pen, 0f, width * 0.365f, 0f, width * 0.4f);
						}
						g.RotateTransform(0f - num);
						float num6 = width * 0.36f - (float)font.Height;
						RectangleF layoutRectangle2 = new RectangleF((float)((double)num6 * Math.Cos((double)(num + 90f) / 360.0 * 2.0 * Math.PI)) - 100f, (float)((double)num6 * Math.Sin((double)(num + 90f) / 360.0 * 2.0 * Math.PI)) - (float)(font.Height / 2), 200f, font.Height);
						using (Brush brush3 = new SolidBrush(markValues[l].Color))
						{
							g.DrawString(markValues[l].Value.ToString(), font, brush3, layoutRectangle2, sf);
						}
					}
				}
				else
				{
					for (int n = 0; n < segment; n++)
					{
						g.DrawLine(borderPen, 0f, width * 0.33f, 0f, width * 0.4f);
						g.RotateTransform(num);
					}
				}
			}
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(new RectangleF((0f - width) / 2f + 1f, (0f - width) / 2f + 1f, width - 3f, width - 3f));
			GraphicsPath graphicsPath2 = new GraphicsPath();
			graphicsPath2.AddEllipse(new RectangleF((0f - width) / 2f + 1f - width * 0.2f, (0f - width) / 2f + 1f - width * 0.2f, width - 3f, width - 3f));
			Region region = new Region(graphicsPath);
			region.Intersect(graphicsPath2);
			using (Brush brush4 = new SolidBrush(Color.FromArgb(60, centerColor)))
			{
				g.FillRegion(brush4, region);
			}
			region.Dispose();
			graphicsPath2.Dispose();
			graphicsPath.Dispose();
			borderPen.Width = width / 20f;
			borderPen.Color = boderColor;
			g.DrawEllipse(borderPen, new RectangleF((0f - width) / 2f + borderPen.Width / 2f, (0f - width) / 2f + borderPen.Width / 2f, width - borderPen.Width - 1f, width - borderPen.Width - 1f));
			float num7 = (value - minValue) / (maxValue - minValue) * 360f;
			g.RotateTransform(num7);
			using (Brush brush5 = new SolidBrush(boderColor))
			{
				g.FillEllipse(brush5, new RectangleF((0f - width) * 0.04f, (0f - width) * 0.04f, width * 0.08f, width * 0.08f));
				for (int num8 = 0; num8 < 2; num8++)
				{
					GraphicsPath graphicsPath3 = new GraphicsPath();
					graphicsPath3.AddLine(0f, width * 0.41f, width * 0.02f, 0f);
					graphicsPath3.AddBezier(new PointF(width * 0.02f, 0f), new PointF(width * 0.03f, (0f - width) * 0.1f), new PointF(width * 0.04f, (0f - width) * 0.11f), new PointF(width * 0.05f, (0f - width) * 0.13f));
					graphicsPath3.AddBezier(new PointF(width * 0.05f, (0f - width) * 0.13f), new PointF(width * 0.06f, (0f - width) * 0.15f), new PointF(width * 0.06f, (0f - width) * 0.15f), new PointF(width * 0.04f, (0f - width) * 0.15f));
					graphicsPath3.AddLine(new PointF(width * 0.04f, (0f - width) * 0.15f), new PointF(-1f, (0f - width) * 0.15f));
					graphicsPath3.CloseFigure();
					g.FillPath(brush5, graphicsPath3);
					g.ScaleTransform(-1f, 1f);
				}
			}
			g.RotateTransform(0f - num7);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(width * 0.022f, width * 0.022f), new PointF((0f - width) * 0.022f, (0f - width) * 0.022f), Color.FromArgb(161, 141, 108), Color.FromArgb(255, 232, 190));
			linearGradientBrush.InterpolationColors = new ColorBlend
			{
				Colors = new Color[3]
				{
					Color.FromArgb(161, 141, 108),
					Color.FromArgb(255, 232, 190),
					Color.FromArgb(161, 141, 108)
				},
				Positions = new float[3] { 0f, 0.3f, 1f }
			};
			g.FillEllipse(linearGradientBrush, new RectangleF((0f - width) * 0.022f, (0f - width) * 0.022f, width * 0.044f, width * 0.044f));
			linearGradientBrush.Dispose();
			if (!string.IsNullOrEmpty(unitText))
			{
				g.DrawString(unitText, font, brush, new RectangleF(width * 0.12f - 100f, width * (width / 5000f + 0.01f), 200f, Font.Height), sf);
			}
			font.Dispose();
			brush.Dispose();
			g.TranslateTransform((0f - width) / 2f, (0f - width) / 2f);
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
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "HslDialPlate";
			base.Size = new System.Drawing.Size(241, 218);
			base.ResumeLayout(false);
		}
	}
}
