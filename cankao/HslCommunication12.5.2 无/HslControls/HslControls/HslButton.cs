using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[DefaultEvent("Click")]
	[DefaultBindingProperty("Text")]
	[DefaultProperty("Text")]
	[Description("按钮控件，带点小圆角，当点击按钮的时候，文本会偏移一点点")]
	public class HslButton : UserControl
	{
		private Color textForeColor = Color.DimGray;

		private Brush textForeBrush = new SolidBrush(Color.DimGray);

		private int roundCorner = 4;

		private bool selected = false;

		private Color backColor = Color.Lavender;

		private Color enablecolor = Color.FromArgb(190, 190, 190);

		private Color activeColor = Color.AliceBlue;

		private Color selectColor = Color.DodgerBlue;

		private bool borderVisiable = true;

		private bool isMouseOn = false;

		private bool isLeftMouseDown = false;

		private StringFormat sf = null;

		private bool useGradient = false;

		private bool roundTopLeft = true;

		private bool roundTopRight = true;

		private bool roundButtomLeft = true;

		private bool roundButtomRight = true;

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
		[Description("获取或设置当前控件的文本的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color ForeColor
		{
			get
			{
				return textForeColor;
			}
			set
			{
				textForeColor = value;
				textForeBrush.Dispose();
				textForeBrush = new SolidBrush(value);
				Invalidate();
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

		[Category("HslControls")]
		[DefaultValue(4)]
		[Description("按钮框的圆角属性")]
		public virtual int CornerRadius
		{
			get
			{
				return roundCorner;
			}
			set
			{
				roundCorner = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(false)]
		[Description("指示按钮的选中状态")]
		public virtual bool Selected
		{
			get
			{
				return selected;
			}
			set
			{
				selected = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Lavender")]
		[Description("按钮的背景色")]
		public virtual Color OriginalColor
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

		[Category("HslControls")]
		[Description("按钮的活动色")]
		[DefaultValue(typeof(Color), "0xBEBEBE")]
		public virtual Color EnableColor
		{
			get
			{
				return enablecolor;
			}
			set
			{
				enablecolor = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "AliceBlue")]
		[Description("按钮的活动色")]
		public virtual Color ActiveColor
		{
			get
			{
				return activeColor;
			}
			set
			{
				activeColor = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		[Description("按钮选中时候的颜色")]
		public virtual Color SelectColor
		{
			get
			{
				return selectColor;
			}
			set
			{
				selectColor = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Browsable(true)]
		[DefaultValue(true)]
		[Description("指示按钮是否存在边框")]
		public virtual bool BorderVisiable
		{
			get
			{
				return borderVisiable;
			}
			set
			{
				borderVisiable = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置按钮控件是否使用渐进色")]
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
		[Description("获取或设置按钮左上角是否圆角处理")]
		[DefaultValue(true)]
		public virtual bool RoundTopLeft
		{
			get
			{
				return roundTopLeft;
			}
			set
			{
				roundTopLeft = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置按钮右上角是否圆角处理")]
		[DefaultValue(true)]
		public virtual bool RoundTopRight
		{
			get
			{
				return roundTopRight;
			}
			set
			{
				roundTopRight = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置按钮左下角是否圆角处理")]
		[DefaultValue(true)]
		public virtual bool RoundButtomLeft
		{
			get
			{
				return roundButtomLeft;
			}
			set
			{
				roundButtomLeft = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置按钮右下角是否圆角处理")]
		[DefaultValue(true)]
		public virtual bool RoundButtomRight
		{
			get
			{
				return roundButtomRight;
			}
			set
			{
				roundButtomRight = value;
				Invalidate();
			}
		}

		[Browsable(false)]
		public virtual string CustomerInformation { get; set; }

		public HslButton()
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
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if ( width < 3 || height < 3)
			{
				return;
			}
			GraphicsPath roundRectange = HslHelper.GetRoundRectange(new Rectangle(0, 0, width, height), roundCorner, roundTopLeft, roundTopRight, roundButtomRight, roundButtomLeft);
			Brush brush = null;
			Brush brush2 = null;
			Rectangle rectangle = new Rectangle(base.ClientRectangle.X, base.ClientRectangle.Y, base.ClientRectangle.Width, base.ClientRectangle.Height);
			if (base.Enabled)
			{
				brush = new SolidBrush(ForeColor);
				if (!useGradient)
				{
					brush2 = (Selected ? new SolidBrush(selectColor) : ((!isMouseOn) ? new SolidBrush(backColor) : new SolidBrush(ActiveColor)));
				}
				else
				{
					ColorBlend colorBlend = new ColorBlend();
					colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
					if (Selected)
					{
						colorBlend.Colors = new Color[3]
						{
							Color.DodgerBlue,
							HslHelper.GetColorLight(Color.DodgerBlue),
							Color.DodgerBlue
						};
					}
					else if (isMouseOn)
					{
						colorBlend.Colors = new Color[3]
						{
							ActiveColor,
							HslHelper.GetColorLight(ActiveColor),
							ActiveColor
						};
					}
					else
					{
						colorBlend.Colors = new Color[3]
						{
							backColor,
							HslHelper.GetColorLight(backColor),
							backColor
						};
					}
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, height - 1), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
					linearGradientBrush.InterpolationColors = colorBlend;
					brush2 = linearGradientBrush;
				}
				if (isLeftMouseDown)
				{
					rectangle.Offset(1, 1);
				}
			}
			else
			{
				brush = new SolidBrush(Color.Gray);
				brush2 = new SolidBrush(EnableColor);
			}
			g.FillPath(brush2, roundRectange);
			Pen pen = new Pen(Color.FromArgb(170, 170, 170));
			if (BorderVisiable)
			{
				g.DrawPath(pen, roundRectange);
			}
			g.DrawString(Text, Font, brush, rectangle, sf);
			brush.Dispose();
			brush2.Dispose();
			pen.Dispose();
			roundRectange.Dispose();
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			isMouseOn = true;
			Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			isMouseOn = false;
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button == MouseButtons.Left)
			{
				isLeftMouseDown = true;
				Invalidate();
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (e.Button == MouseButtons.Left)
			{
				isLeftMouseDown = false;
				Invalidate();
			}
		}

		public void PerformClick()
		{
			OnClick(new EventArgs());
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
			base.Name = "HslButton";
			base.Size = new System.Drawing.Size(114, 46);
			base.ResumeLayout(false);
		}
	}
}
