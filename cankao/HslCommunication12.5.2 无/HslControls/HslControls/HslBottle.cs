using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个类似瓶子，罐子的控件，支持液位显示，支持设置渐变的颜色及文本标题")]
	public class HslBottle : UserControl
	{
		private Color foreColorTop = Color.FromArgb(243, 245, 139);

		private Color foreColorEdge = Color.FromArgb(194, 190, 77);

		private Color foreColorCenter = Color.FromArgb(226, 221, 98);

		private Color backColorTop = Color.FromArgb(151, 232, 244);

		private Color backColorEdge = Color.FromArgb(142, 196, 216);

		private Color backColorCenter = Color.FromArgb(240, 240, 240);

		private double value = 50.0;

		private bool isOpen = false;

		private string bottleTag = "";

		private StringFormat sf;

		private string headTag = "原料1";

		private float dockHeight = 30f;

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
		[Description("获取或设置瓶子的液位值。")]
		[DefaultValue(typeof(double), "60")]
		[Category("HslControls")]
		public virtual double Value
		{
			get
			{
				return value;
			}
			set
			{
				if (value >= 0.0 && value <= 100.0)
				{
					this.value = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置瓶子是否处于打开的状态。")]
		[DefaultValue(typeof(bool), "false")]
		[Category("HslControls")]
		public virtual bool IsOpen
		{
			get
			{
				return isOpen;
			}
			set
			{
				isOpen = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置瓶子的标签信息，用于绘制在瓶子上的信息。")]
		[DefaultValue(typeof(string), "")]
		[Category("HslControls")]
		public virtual string BottleTag
		{
			get
			{
				return bottleTag;
			}
			set
			{
				bottleTag = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置瓶子的备注信息，用于绘制在瓶子顶部的信息。")]
		[DefaultValue(typeof(string), "原料1")]
		[Category("HslControls")]
		public virtual string HeadTag
		{
			get
			{
				return headTag;
			}
			set
			{
				headTag = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(30f)]
		[Description("获取或设置控件底座的高度")]
		[Category("HslControls")]
		public virtual float DockHeight
		{
			get
			{
				return dockHeight;
			}
			set
			{
				dockHeight = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[194, 190, 77]")]
		[Description("获取或设置前景色的边缘颜色内容")]
		[Category("HslControls")]
		public virtual Color ForeColorEdge
		{
			get
			{
				return foreColorEdge;
			}
			set
			{
				foreColorEdge = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[226, 221, 98]")]
		[Description("获取或设置前景色的中心颜色内容")]
		[Category("HslControls")]
		public virtual Color ForeColorCenter
		{
			get
			{
				return foreColorCenter;
			}
			set
			{
				foreColorCenter = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[243, 245, 139]")]
		[Description("获取或设置前景色的顶部颜色内容")]
		[Category("HslControls")]
		public virtual Color ForeColorTop
		{
			get
			{
				return foreColorTop;
			}
			set
			{
				foreColorTop = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[142, 196, 216]")]
		[Description("获取或设置背景色的边缘颜色内容")]
		[Category("HslControls")]
		public virtual Color BackColorEdge
		{
			get
			{
				return backColorEdge;
			}
			set
			{
				backColorEdge = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[240, 240, 240]")]
		[Description("获取或设置背景色的中心颜色内容")]
		[Category("HslControls")]
		public virtual Color BackColorCenter
		{
			get
			{
				return backColorCenter;
			}
			set
			{
				backColorCenter = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[DefaultValue(typeof(Color), "[151, 232, 244]")]
		[Description("获取或设置背景色的顶部颜色内容")]
		[Category("HslControls")]
		public virtual Color BackColorTop
		{
			get
			{
				return backColorTop;
			}
			set
			{
				backColorTop = value;
				Invalidate();
			}
		}

		public HslBottle()
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
			if ( width < 15 || base.Height < 15)
			{
				return;
			}
			int num = ((dockHeight < 1f) ? ((int)(dockHeight * (float)height)) : ((int)dockHeight));
			int num2 = 20;
			float num3 = (float)width / 2f;
			float num4 = (float)(height - num) - (float)(height - num - num2) * Convert.ToSingle(value) / 100f;
			int num5 = width / 50 + 3;
			if (string.IsNullOrEmpty(headTag))
			{
				num2 = num5;
			}
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddPolygon(new PointF[6]
			{
				new PointF(0f, num2),
				new PointF(0f, height - num),
				new PointF(num3, height - 1),
				new PointF(width - 1, height - num),
				new PointF(width - 1, num2),
				new PointF(0f, num2)
			});
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Point(0, num2), new Point(width - 1, num2), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3] { backColorEdge, backColorCenter, backColorEdge };
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillPath(linearGradientBrush, graphicsPath);
			using (Brush brush = new SolidBrush(backColorTop))
			{
				g.FillEllipse(brush, 1, num2 - num5, width - 2, num5 * 2);
			}
			graphicsPath.Reset();
			graphicsPath.AddPolygon(new PointF[6]
			{
				new PointF(0f, num4),
				new PointF(0f, height - num),
				new PointF(num3, height - 1),
				new PointF(width - 1, height - num),
				new PointF(width - 1, num4),
				new PointF(0f, num4)
			});
			colorBlend.Colors = new Color[3] { foreColorEdge, foreColorCenter, foreColorEdge };
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillPath(linearGradientBrush, graphicsPath);
			linearGradientBrush.Dispose();
			using (Brush brush2 = new SolidBrush(foreColorTop))
			{
				g.FillEllipse(brush2, 1f, num4 - (float)num5, width - 2, num5 * 2);
			}
			graphicsPath.Reset();
			graphicsPath.AddPolygon(new PointF[3]
			{
				new PointF(0f, height - num),
				new PointF(num3, height - 1),
				new PointF(width - 1, height - num)
			});
			graphicsPath.AddArc(0, height - num - num5, width, num5 * 2, 0f, 180f);
			using (Brush brush3 = new SolidBrush(foreColorEdge))
			{
				g.FillPath(brush3, graphicsPath);
			}
			graphicsPath.Reset();
			graphicsPath.AddLines(new PointF[4]
			{
				new PointF((float)width / 4f, height - num / 2),
				new PointF((float)width / 4f, height - 1),
				new PointF((float)width * 3f / 4f, height - 1),
				new PointF((float)width * 3f / 4f, height - num / 2)
			});
			graphicsPath.AddArc((float)width / 4f, height - num / 2 - num5 - 1, (float)width / 2f, num5 * 2, 0f, 180f);
			g.FillPath(Brushes.DimGray, graphicsPath);
			using (Brush brush4 = new SolidBrush(ForeColor))
			{
				if (!string.IsNullOrEmpty(bottleTag))
				{
					g.DrawString(bottleTag, Font, brush4, new Rectangle(-10, 26, width + 20, 20), sf);
				}
				if (!string.IsNullOrEmpty(headTag))
				{
					g.DrawString(headTag, Font, brush4, new Rectangle(-10, 0, width + 20, 20), sf);
				}
			}
			graphicsPath.Dispose();
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
			base.Name = "HslBottle";
			base.ResumeLayout(false);
		}
	}
}
