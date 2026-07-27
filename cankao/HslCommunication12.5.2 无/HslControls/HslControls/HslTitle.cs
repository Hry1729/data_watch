using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个标题看板控件，用来显示重点的标题信息，两侧可以设置其他的数据信息")]
	public class HslTitle : UserControl
	{
		private int leftRightCenterColor = 30;

		private Color leftTextColor = Color.Cyan;

		private Color rightTextColor = Color.Yellow;

		private StringFormat sf = null;

		private Color edgeColor = Color.LightGray;

		private Color leftRightEdgeColor = Color.DimGray;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

		private float widthPercent = 0.5f;

		private Font fontRight = null;

		private Font fontLeft = null;

		private string textRight = "HslControl";

		private string textLeft = "00:00:00";

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
		[Description("获取或设置中间信息的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置两侧的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color LeftRightEdgeColor
		{
			get
			{
				return leftRightEdgeColor;
			}
			set
			{
				leftRightEdgeColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置两侧的中间颜色的系数，范围 0-100，0表示等同于边缘色，100表示白色")]
		[Category("HslControls")]
		[DefaultValue(30)]
		public int LeftRightCenterColorScale
		{
			get
			{
				return leftRightCenterColor;
			}
			set
			{
				leftRightCenterColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置标题控件是否是横向的还是纵向的")]
		[Category("HslControls")]
		[DefaultValue(typeof(HslDirectionStyle), "Horizontal")]
		public HslDirectionStyle TitleStyle
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

		[Browsable(true)]
		[Description("获取或设置右侧文本的字体信息")]
		[Category("HslControls")]
		public Font FontRight
		{
			get
			{
				return fontRight;
			}
			set
			{
				fontRight = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左侧文本的字体信息")]
		[Category("HslControls")]
		public Font FontLeft
		{
			get
			{
				return fontLeft;
			}
			set
			{
				fontLeft = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左侧文本的颜色信息")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Cyan")]
		public Color LeftTextColor
		{
			get
			{
				return leftTextColor;
			}
			set
			{
				leftTextColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置右侧文本的颜色信息")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Yellow")]
		public Color RightTextColor
		{
			get
			{
				return rightTextColor;
			}
			set
			{
				rightTextColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置右侧文本的信息")]
		[Category("HslControls")]
		[DefaultValue("HslControl")]
		public string TextRight
		{
			get
			{
				return textRight;
			}
			set
			{
				textRight = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左侧文本的信息")]
		[Category("HslControls")]
		[DefaultValue("00:00:00")]
		public string TextLeft
		{
			get
			{
				return textLeft;
			}
			set
			{
				textLeft = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置中间文本的宽度百分比，0.5表示50%")]
		[Category("HslControls")]
		[DefaultValue(0.5f)]
		public float WidthPercent
		{
			get
			{
				return widthPercent;
			}
			set
			{
				widthPercent = value;
				Invalidate();
			}
		}

		public HslTitle()
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
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
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
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3]
			{
				leftRightEdgeColor,
				HslHelper.GetColorLight(leftRightEdgeColor, leftRightCenterColor),
				leftRightEdgeColor
			};
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, height), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, 0f, 0f, width - 1f, height - 1f);
			colorBlend.Colors = new Color[3]
			{
				edgeColor,
				HslHelper.GetColorLight(edgeColor),
				edgeColor
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			if (width * 0.1f < height)
			{
				g.FillPolygon(linearGradientBrush, new PointF[4]
				{
					new PointF(width * (0.5f - widthPercent / 2f), 0f),
					new PointF(width * (0.5f + widthPercent / 2f), 0f),
					new PointF(width * (0.4f + widthPercent / 2f), height - 1f),
					new PointF(width * (0.6f - widthPercent / 2f), height - 1f)
				});
			}
			else
			{
				g.FillPolygon(linearGradientBrush, new PointF[4]
				{
					new PointF(width * (0.5f - widthPercent / 2f), 0f),
					new PointF(width * (0.5f + widthPercent / 2f), 0f),
					new PointF(width * (0.5f + widthPercent / 2f) - height, height - 1f),
					new PointF(width * (0.5f - widthPercent / 2f) + height, height - 1f)
				});
			}
			linearGradientBrush.Dispose();
			linearGradientBrush = new LinearGradientBrush(new PointF(0f, height * 0.5f - (float)Font.Height), new PointF(0f, height * 0.5f + (float)Font.Height), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			colorBlend.Colors = new Color[3]
			{
				HslHelper.GetColorLight(rightTextColor, -30),
				rightTextColor,
				HslHelper.GetColorLight(rightTextColor, -30)
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, new RectangleF(0f, 0f, width - 1f, height - 1f), sf);
			}
			if (!string.IsNullOrEmpty(textRight))
			{
				g.DrawString(textRight, fontRight ?? Font, linearGradientBrush, new RectangleF(width * (0.5f + widthPercent / 2f), 0f, width * (0.5f - widthPercent / 2f), height - 1f), sf);
			}
			colorBlend.Colors = new Color[3]
			{
				HslHelper.GetColorLight(leftTextColor, -30),
				leftTextColor,
				HslHelper.GetColorLight(leftTextColor, -30)
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			if (!string.IsNullOrEmpty(textLeft))
			{
				g.DrawString(textLeft, fontLeft ?? Font, linearGradientBrush, new RectangleF(0f, 0f, width * (0.5f - widthPercent / 2f), height - 1f), sf);
			}
			linearGradientBrush.Dispose();
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
			base.Name = "HslTitle";
			base.Size = new System.Drawing.Size(771, 65);
			base.ResumeLayout(false);
		}
	}
}
