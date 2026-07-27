using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("文本组合控件，支持一个文本，数据，单位的同时显示操作")]
	public class HslLabelCombo : UserControl
	{
		private StringFormat format_center = null;

		private StringFormat format_left = null;

		private string textInfo = "Infomation:";

		private string textValue = "0.00";

		private float textValueWidth = 0.3f;

		private string textUnit = "Kg";

		private float locationInfo = 0.02f;

		private float locationValue = 0.6f;

		private float locationUnit = 0.91f;

		private Color textValueBorderColor = Color.LightGray;

		private Color backcolorValue = Color.Wheat;

		private ArrowDirection arrowDirection = ArrowDirection.Left;

		private Color arrowColor = Color.Green;

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

		[Description("获取或设置当前的绘制的左侧的文本信息，通常是描述数据的内容")]
		[Category("HslControls")]
		[DefaultValue("Infomation:")]
		public virtual string TextInfo
		{
			get
			{
				return textInfo;
			}
			set
			{
				textInfo = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前的中间的值文本，通常是数值信息")]
		[Category("HslControls")]
		[DefaultValue("0.00")]
		public virtual string TextValue
		{
			get
			{
				return textValue;
			}
			set
			{
				textValue = value;
				Invalidate();
			}
		}

		[Description("获取或设置右侧的文本信息，通常用来表示单位信息")]
		[Category("HslControls")]
		[DefaultValue("Kg")]
		public virtual string TextUnit
		{
			get
			{
				return textUnit;
			}
			set
			{
				textUnit = value;
				Invalidate();
			}
		}

		[Description("获取或设置左侧的文本的位置，如果小于1，表示总宽度的百分比，如果大于1表示绝对的像素位置")]
		[Category("HslControls")]
		[DefaultValue(0.02f)]
		public virtual float LocationInfo
		{
			get
			{
				return locationInfo;
			}
			set
			{
				locationInfo = value;
				Invalidate();
			}
		}

		[Description("获取或设置中间的文本框的位置，如果小于1，表示总宽度的百分比，如果大于1表示绝对的像素位置")]
		[Category("HslControls")]
		[DefaultValue(0.6f)]
		public virtual float LocationValue
		{
			get
			{
				return locationValue;
			}
			set
			{
				locationValue = value;
				Invalidate();
			}
		}

		[Description("获取或设置右侧的文本的位置，如果小于1，表示总宽度的百分比，如果大于1表示绝对的像素位置")]
		[Category("HslControls")]
		[DefaultValue(0.91f)]
		public virtual float LocationUnit
		{
			get
			{
				return locationUnit;
			}
			set
			{
				locationUnit = value;
				Invalidate();
			}
		}

		[Description("获取或设置中间文本框的宽度信息，如果小于1，表示总宽度的百分比，如果大于1表示绝对的像素位置")]
		[Category("HslControls")]
		[DefaultValue(0.3f)]
		public virtual float TextValueWidth
		{
			get
			{
				return textValueWidth;
			}
			set
			{
				textValueWidth = value;
				Invalidate();
			}
		}

		[Description("获取或设置中间文本框的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Wheat")]
		public virtual Color TextValueBackcolor
		{
			get
			{
				return backcolorValue;
			}
			set
			{
				if (backcolorValue != value)
				{
					backcolorValue = value;
					Invalidate();
				}
			}
		}

		[Description("获取或设置箭头的放向信息，只有向上或是向下会显示箭头，否则不显示")]
		[Category("HslControls")]
		[DefaultValue(typeof(ArrowDirection), "Left")]
		public virtual ArrowDirection ArrowDirection
		{
			get
			{
				return arrowDirection;
			}
			set
			{
				if (arrowDirection != value)
				{
					arrowDirection = value;
					Invalidate();
				}
			}
		}

		[Description("获取或设置箭头的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Green")]
		public virtual Color ArrowColor
		{
			get
			{
				return arrowColor;
			}
			set
			{
				if (arrowColor != value)
				{
					arrowColor = value;
					Invalidate();
				}
			}
		}

		[Description("获取或设置中间文本框的的边框颜色信息")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public virtual Color TextValueBorderColor
		{
			get
			{
				return textValueBorderColor;
			}
			set
			{
				if (textValueBorderColor != value)
				{
					textValueBorderColor = value;
					Invalidate();
				}
			}
		}

		[Browsable(false)]
		public Func<string, Color> TextValueColorTransFun { get; set; }

		public HslLabelCombo()
		{
			InitializeComponent();
			format_center = new StringFormat();
			format_center.Alignment = StringAlignment.Center;
			format_center.LineAlignment = StringAlignment.Center;
			format_left = new StringFormat();
			format_left.Alignment = StringAlignment.Near;
			format_left.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			PaintHslControls(e.Graphics, base.Width, base.Height);
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
			SolidBrush solidBrush = new SolidBrush(ForeColor);
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			RectangleF layoutRectangle = new RectangleF((locationInfo < 1f) ? (locationInfo * (float)width) : locationInfo, 0f, width, height - 1);
			g.DrawString(textInfo, Font, solidBrush, layoutRectangle, format_left);
			RectangleF rectangleF = new RectangleF((locationValue < 1f) ? (locationValue * (float)width) : locationValue, 1f, (textValueWidth < 1f) ? (textValueWidth * (float)width) : textValueWidth, height - 3);
			Color color = ((TextValueColorTransFun == null) ? backcolorValue : TextValueColorTransFun(textValue));
			using (LinearGradientBrush brush = new LinearGradientBrush(new PointF(0f, height - 1), new PointF(0f, 1f), color, HslHelper.GetColorOffset(color, (height - 1) * 2 / 3)))
			{
				g.FillRectangle(brush, rectangleF);
			}
			using (Pen pen = new Pen(textValueBorderColor))
			{
				g.DrawRectangles(pen, new RectangleF[1] { rectangleF });
			}
			g.DrawString(textValue, Font, solidBrush, rectangleF, format_center);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float num = 6f;
			float num2 = rectangleF.X + rectangleF.Width - num - 1f;
			if (arrowDirection == ArrowDirection.Up)
			{
				PointF[] points = new PointF[5]
				{
					new PointF(num2, (float)height - num - 2f),
					new PointF(num2, num),
					new PointF(num2 - 5f, num + 5f),
					new PointF(num2, num),
					new PointF(num2 + 5f, num + 5f)
				};
				using (Pen pen2 = new Pen(arrowColor))
				{
					g.DrawLines(pen2, points);
				}
			}
			else if (arrowDirection == ArrowDirection.Down)
			{
				PointF[] points2 = new PointF[5]
				{
					new PointF(num2, num),
					new PointF(num2, (float)height - num - 2f),
					new PointF(num2 - 5f, (float)height - num - 2f - 5f),
					new PointF(num2, (float)height - num - 2f),
					new PointF(num2 + 5f, (float)height - num - 2f - 5f)
				};
				using (Pen pen3 = new Pen(arrowColor))
				{
					g.DrawLines(pen3, points2);
				}
			}
			RectangleF layoutRectangle2 = new RectangleF((locationUnit < 1f) ? (locationUnit * (float)width) : locationUnit, 0f, width, height - 1);
			g.DrawString(textUnit, Font, solidBrush, layoutRectangle2, format_left);
			solidBrush.Dispose();
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
			base.Name = "HslLabelCombo";
			base.Size = new System.Drawing.Size(304, 33);
			base.ResumeLayout(false);
		}
	}
}
