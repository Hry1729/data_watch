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
	public class HslBadge : UserControl
	{
		private StringFormat format_center = null;

		private string rightText = "v1.0.0";

		private int spaceWidth = 3;

		private int roundCorner = 3;

		private Color leftBackground = Color.FromArgb(79, 79, 79);

		private Color rightBackground = Color.FromArgb(2, 115, 179);

		private Color rightForeground = Color.White;

		private PixelOffsetMode pixelOffsetMode = PixelOffsetMode.None;

		private bool isMouseOver = false;

		private bool useButtonHoverStyle = false;

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
		[Description("获取或设置当前控件的大小是否自动")]
		[Category("HslControls")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[DefaultValue(true)]
		public override bool AutoSize
		{
			get
			{
				return base.AutoSize;
			}
			set
			{
				base.AutoSize = value;
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

		[Description("获取或设置当前徽章的右侧的文本")]
		[Category("HslControls")]
		[DefaultValue("v1.0.0")]
		public virtual string RightText
		{
			get
			{
				return rightText;
			}
			set
			{
				rightText = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前的绘制的文本的左右侧的间隙")]
		[Category("HslControls")]
		[DefaultValue(3)]
		public virtual int SpaceWidth
		{
			get
			{
				return spaceWidth;
			}
			set
			{
				spaceWidth = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前的四个圆角的弧度大小")]
		[Category("HslControls")]
		[DefaultValue(3)]
		public virtual int RoundCorner
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

		[Description("获取或设置左侧说明文本的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[79, 79, 79]")]
		public virtual Color LeftBackground
		{
			get
			{
				return leftBackground;
			}
			set
			{
				leftBackground = value;
				Invalidate();
			}
		}

		[Description("获取或设置右侧说明文本的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[2, 115, 179 ]")]
		public virtual Color RightBackground
		{
			get
			{
				return rightBackground;
			}
			set
			{
				rightBackground = value;
				Invalidate();
			}
		}

		[Description("获取或设置右侧说明文本的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
		public virtual Color RightForeground
		{
			get
			{
				return rightForeground;
			}
			set
			{
				rightForeground = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前绘图的像素偏移情况，设置为HighQuality时，边界不会模式处理，会很清晰")]
		[Category("HslControls")]
		[DefaultValue(typeof(PixelOffsetMode), "None")]
		public virtual PixelOffsetMode PixelOffset
		{
			get
			{
				return pixelOffsetMode;
			}
			set
			{
				pixelOffsetMode = value;
				Invalidate();
			}
		}

		[Description("获取或设置是否启用按钮悬浮样式，当你只使用右半部分的文本时，可以启用按钮的悬浮样式，设置True之后，当鼠标悬浮时，背景色会轻微的变化。")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool ButtonHoverStyle
		{
			get
			{
				return useButtonHoverStyle;
			}
			set
			{
				useButtonHoverStyle = value;
			}
		}

		public HslBadge()
		{
			InitializeComponent();
			format_center = new StringFormat();
			format_center.Alignment = StringAlignment.Center;
			format_center.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			ForeColor = Color.White;
			AutoSize = true;
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			if (useButtonHoverStyle)
			{
				isMouseOver = true;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if (useButtonHoverStyle)
			{
				isMouseOver = false;
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = pixelOffsetMode;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			
			int num = 0;
			int num2 = 0;
			if (!string.IsNullOrEmpty(Text))
			{
				num = num + (int)g.MeasureString(Text, Font).Width + spaceWidth * 2;
			}
			if (!string.IsNullOrEmpty(rightText))
			{
				num2 = num2 + (int)g.MeasureString(rightText, Font).Width + spaceWidth * 2;
			}
			int num3 = num + num2;
			if (num3 < 3)
			{
				num3 = 3;
			}
			if (AutoSize && Dock != DockStyle.Fill && num3 != width)
			{
				base.Width = num3;
			}
			else
			{				
				GraphicsPath graphicsPath = new GraphicsPath();
				if (num > 0)
				{
					graphicsPath = ((num2 <= 0) ? HslHelper.GetRoundRectange(new Rectangle(0, 0, num, height), roundCorner, true, true, true, true) : HslHelper.GetRoundRectange(new Rectangle(0, 0, num + 1, height), roundCorner, true, false, false, true));
					using (LinearGradientBrush brush = new LinearGradientBrush(new PointF(0f, height - 1), new PointF(0f, 0f), leftBackground, HslHelper.GetColorOffset(leftBackground, height - 1)))
					{
						g.FillPath(brush, graphicsPath);
					}
					using (Brush brush2 = new SolidBrush(ForeColor))
					{
						g.DrawString(Text, Font, brush2, new Rectangle(0, 0, num + 1, height - 1), format_center);
					}
				}
				graphicsPath.Dispose();
				if (num2 > 0)
				{
					graphicsPath = ((num <= 0) ? HslHelper.GetRoundRectange(new Rectangle(num, 0, width - num, height), roundCorner, true, true, true, true) : HslHelper.GetRoundRectange(new Rectangle(num, 0, width - num, height), roundCorner, false, true, true, false));
					Color color = ((useButtonHoverStyle && isMouseOver) ? HslHelper.GetColorOffset(rightBackground, 20) : rightBackground);
					using (LinearGradientBrush brush3 = new LinearGradientBrush(new PointF(0f, height - 1), new PointF(0f, 0f), color, HslHelper.GetColorOffset(color, height - 1)))
					{
						g.FillPath(brush3, graphicsPath);
					}
					using (Brush brush4 = new SolidBrush(rightForeground))
					{
						g.DrawString(rightText, Font, brush4, new Rectangle(num, 0, width - num - 1, height - 1), format_center);
					}
				}
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
			base.Name = "HslBadge";
			base.Size = new System.Drawing.Size(117, 23);
			base.ResumeLayout(false);
		}
	}
}
