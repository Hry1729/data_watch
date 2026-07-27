using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个箭头控件，支持设置不同的方向，颜色，文本信息")]
	public class HslArrow : UserControl
	{
		private StringFormat sf = null;

		private Color edgeColor = Color.Gray;

		private Pen edgePen = new Pen(Color.Gray, 1f);

		private Color arrowBackColor = Color.DodgerBlue;

		private Brush brushArrow = new SolidBrush(Color.DodgerBlue);

		private ArrowDirection arrowDirection = ArrowDirection.Right;

		private float factorX = 0.75f;

		private float factorY = 0.27f;

		private bool useGradient = true;

		private bool arrowBoth = false;

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
		[DefaultValue(typeof(Color), "White")]
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

		[Browsable(true)]
		[Description("获取或设置箭头控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public virtual Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				edgePen = new Pen(value, 1f);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置箭头控件的方向")]
		[Category("HslControls")]
		[DefaultValue(typeof(ArrowDirection), "Right")]
		public virtual ArrowDirection ArrowDirection
		{
			get
			{
				return arrowDirection;
			}
			set
			{
				arrowDirection = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置箭头是否应用于两头，默认只有一个箭头")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool ArrowBoth
		{
			get
			{
				return arrowBoth;
			}
			set
			{
				arrowBoth = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置箭头背景的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public virtual Color ArrowBackColor
		{
			get
			{
				return arrowBackColor;
			}
			set
			{
				arrowBackColor = value;
				brushArrow?.Dispose();
				brushArrow = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置横向宽度的比例值，范围0-1")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public virtual float FactorX
		{
			get
			{
				return factorX;
			}
			set
			{
				if (value >= 0f && value <= 1f)
				{
					factorX = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置纵向宽度的比例值，范围0-0.5")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public virtual float FactorY
		{
			get
			{
				return factorY;
			}
			set
			{
				if (value >= 0f && value <= 0.5f)
				{
					factorY = value;
					Invalidate();
				}
			}
		}

		[Category("HslControls")]
		[Description("获取或设置本箭头控件是否使用渐进色")]
		[Browsable(true)]
		[DefaultValue(true)]
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

		public HslArrow()
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

		public void PaintHslControls(Graphics g, float width, float height)
		{
			PointF[] array = null;
			RectangleF layoutRectangle = default(RectangleF);
			switch (arrowDirection)
			{
			case ArrowDirection.Right:
				if (!arrowBoth)
				{
					array = new PointF[8]
					{
						new PointF(0f, height * factorY),
						new PointF(width * factorX, height * factorY),
						new PointF(width * factorX, 0f),
						new PointF(width - 1f, height * 0.5f),
						new PointF(width * factorX, height - 1f),
						new PointF(width * factorX, height * (1f - factorY)),
						new PointF(0f, height * (1f - factorY)),
						new PointF(0f, height * factorY)
					};
					layoutRectangle = new RectangleF(0f, height * factorY, width * (1f - (1f - factorX) / 2f), height * (1f - factorY * 2f));
				}
				else
				{
					array = new PointF[11]
					{
						new PointF(width * factorX, height * factorY),
						new PointF(width * factorX, 0f),
						new PointF(width - 1f, height * 0.5f),
						new PointF(width * factorX, height - 1f),
						new PointF(width * factorX, height * (1f - factorY)),
						new PointF(width * (1f - factorX), height * (1f - factorY)),
						new PointF(width * (1f - factorX), height - 1f),
						new PointF(0f, height * 0.5f),
						new PointF(width * (1f - factorX), 0f),
						new PointF(width * (1f - factorX), height * factorY),
						new PointF(width * factorX, height * factorY)
					};
					layoutRectangle = new RectangleF(0f, height * factorY, width - 1f, height * (1f - factorY * 2f));
				}
				break;
			case ArrowDirection.Left:
				if (!arrowBoth)
				{
					array = new PointF[8]
					{
						new PointF(0f, height * 0.5f),
						new PointF(width * (1f - factorX), 0f),
						new PointF(width * (1f - factorX), height * factorY),
						new PointF(width - 1f, height * factorY),
						new PointF(width - 1f, height * (1f - factorY)),
						new PointF(width * (1f - factorX), height * (1f - factorY)),
						new PointF(width * (1f - factorX), height - 1f),
						new PointF(0f, height * 0.5f)
					};
					layoutRectangle = new RectangleF((1f - factorX) / 2f * width, height * factorY, width * (1f - (1f - factorX) / 2f), height * (1f - factorY * 2f));
				}
				else
				{
					array = new PointF[11]
					{
						new PointF(0f, height * 0.5f),
						new PointF(width * (1f - factorX), 0f),
						new PointF(width * (1f - factorX), height * factorY),
						new PointF(width * factorX, height * factorY),
						new PointF(width * factorX, 0f),
						new PointF(width - 1f, height * 0.5f),
						new PointF(width * factorX, height - 1f),
						new PointF(width * factorX, height * (1f - factorY)),
						new PointF(width * (1f - factorX), height * (1f - factorY)),
						new PointF(width * (1f - factorX), height - 1f),
						new PointF(0f, height * 0.5f)
					};
					layoutRectangle = new RectangleF(0f, height * factorY, width - 1f, height * (1f - factorY * 2f));
				}
				break;
			case ArrowDirection.Up:
				if (!arrowBoth)
				{
					array = new PointF[8]
					{
						new PointF(width * 0.5f, 0f),
						new PointF(width - 1f, height * (1f - factorX)),
						new PointF(width * (1f - factorY), height * (1f - factorX)),
						new PointF(width * (1f - factorY), height - 1f),
						new PointF(width * factorY, height - 1f),
						new PointF(width * factorY, height * (1f - factorX)),
						new PointF(0f, height * (1f - factorX)),
						new PointF(width * 0.5f, 0f)
					};
					layoutRectangle = new RectangleF(width * factorY, height * (1f - factorX) / 2f, width * (1f - factorY * 2f), height * (1f - (1f - factorX) / 2f));
				}
				else
				{
					array = new PointF[11]
					{
						new PointF(width * 0.5f, 0f),
						new PointF(width - 1f, height * (1f - factorX)),
						new PointF(width * (1f - factorY), height * (1f - factorX)),
						new PointF(width * (1f - factorY), height * factorX),
						new PointF(width - 1f, height * factorX),
						new PointF(width / 2f, height - 1f),
						new PointF(0f, height * factorX),
						new PointF(width * factorY, height * factorX),
						new PointF(width * factorY, height * (1f - factorX)),
						new PointF(0f, height * (1f - factorX)),
						new PointF(width * 0.5f, 0f)
					};
					layoutRectangle = new RectangleF(width * factorY, 0f, width * (1f - factorY * 2f), height - 1f);
				}
				break;
			case ArrowDirection.Down:
				if (!arrowBoth)
				{
					array = new PointF[8]
					{
						new PointF(width * (1f - factorY), 0f),
						new PointF(width * (1f - factorY), height * factorX),
						new PointF(width - 1f, height * factorX),
						new PointF(width / 2f, height - 1f),
						new PointF(0f, height * factorX),
						new PointF(width * factorY, height * factorX),
						new PointF(width * factorY, 0f),
						new PointF(width * (1f - factorY), 0f)
					};
					layoutRectangle = new RectangleF(width * factorY, 0f, width * (1f - factorY * 2f), height * (1f - (1f - factorX) / 2f));
				}
				else
				{
					array = new PointF[11]
					{
						new PointF(width * 0.5f, 0f),
						new PointF(width - 1f, height * (1f - factorX)),
						new PointF(width * (1f - factorY), height * (1f - factorX)),
						new PointF(width * (1f - factorY), height * factorX),
						new PointF(width - 1f, height * factorX),
						new PointF(width / 2f, height - 1f),
						new PointF(0f, height * factorX),
						new PointF(width * factorY, height * factorX),
						new PointF(width * factorY, height * (1f - factorX)),
						new PointF(0f, height * (1f - factorX)),
						new PointF(width * 0.5f, 0f)
					};
					layoutRectangle = new RectangleF(width * factorY, 0f, width * (1f - factorY * 2f), height - 1f);
				}
				break;
			}
			if (array == null)
			{
				return;
			}
			if (useGradient)
			{
				ColorBlend colorBlend = new ColorBlend();
				colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
				colorBlend.Colors = new Color[3]
				{
					arrowBackColor,
					HslHelper.GetColorLight(arrowBackColor),
					arrowBackColor
				};
				if (arrowDirection == ArrowDirection.Up || arrowDirection == ArrowDirection.Down)
				{
					using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(width - 1f, 0f), arrowBackColor, arrowBackColor))
					{
						linearGradientBrush.InterpolationColors = colorBlend;
						g.FillPolygon(linearGradientBrush, array);
					}
				}
				else
				{
					using (LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, height - 1f), arrowBackColor, arrowBackColor))
					{
						linearGradientBrush2.InterpolationColors = colorBlend;
						g.FillPolygon(linearGradientBrush2, array);
					}
				}
			}
			else
			{
				g.FillPolygon(brushArrow, array);
			}
			g.DrawPolygon(edgePen, array);
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, layoutRectangle, sf);
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
			this.ForeColor = System.Drawing.Color.White;
			base.Name = "HslArrow";
			base.Size = new System.Drawing.Size(176, 49);
			base.ResumeLayout(false);
		}
	}
}
