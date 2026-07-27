using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[DefaultBindingProperty("Text")]
	[DefaultProperty("Text")]
	public class HslClassifier : UserControl
	{
		private StringFormat sf = null;

		private Color boderColor = Color.DimGray;

		private Pen borderPen = new Pen(Color.DimGray);

		private Color edgeColor = Color.Gray;

		private Color centerColor = Color.WhiteSmoke;

		private Color textBackColor = Color.FromArgb(46, 46, 46);

		private Brush textBackBrush = new SolidBrush(Color.FromArgb(46, 46, 46));

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
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Cyan")]
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
		[Description("获取或设置分类器控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		public virtual Color EdgeColor
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
		[Description("获取或设置分类器控件的中心颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "WhiteSmoke")]
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
		[DefaultValue(typeof(Color), "DimGray")]
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
		[Description("获取或设置分类器控件文本的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[46, 46, 46]")]
		public virtual Color TextBackColor
		{
			get
			{
				return textBackColor;
			}
			set
			{
				textBackColor = value;
				textBackBrush.Dispose();
				textBackBrush = new SolidBrush(textBackColor);
				Invalidate();
			}
		}

		public HslClassifier()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			ForeColor = Color.Cyan;
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
				PaintMain(g, base.Width, base.Height);
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Point(0, 20), new Point(base.Width - 1, 20), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.45f, 1f };
			colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, width * 0.02f, -1f, width * 0.96f, height * 0.2f);
			g.DrawRectangle(borderPen, width * 0.02f, 0f, width * 0.96f, height * 0.2f);
			g.FillRectangle(linearGradientBrush, 0f, height * 0.02f, width, height * 0.52f);
			PointF[] points = new PointF[4]
			{
				new PointF(0f, height * 0.54f),
				new PointF(width - 1f, height * 0.54f),
				new PointF(width * 0.65f, height * 0.8f),
				new PointF(width * 0.35f, height * 0.8f)
			};
			g.FillPolygon(linearGradientBrush, points);
			g.DrawPolygon(borderPen, points);
			linearGradientBrush.Dispose();
			g.DrawRectangle(borderPen, 0f, height * 0.02f, width - 1f, height * 0.52f);
			g.DrawRectangle(borderPen, width * 0.35f, height * 0.8f, width * 0.3f, height * 0.17f);
			linearGradientBrush = new LinearGradientBrush(new PointF(width * 0.35f, 20f), new PointF((float)base.Width * 0.65f, 20f), Color.FromArgb(142, 196, 216), Color.FromArgb(240, 240, 240));
			colorBlend.Positions = new float[3]
			{
				0f,
				1f / 3f,
				1f
			};
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, width * 0.35f, height * 0.94f - 1f, width * 0.3f, height * 0.06f);
			g.DrawEllipse(borderPen, width * 0.35f, height * 0.94f - 1f, width * 0.3f, height * 0.06f);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, width * 0.35f, height * 0.8f, width * 0.3f, height * 0.17f);
			linearGradientBrush.Dispose();
			if (!string.IsNullOrEmpty(Text))
			{
				float num = g.MeasureString(Text, Font, (int)(width * 0.5f)).Height + 10f;
				RectangleF rectangleF = new RectangleF(width * 0.25f, height * 0.26f - num / 2f, width * 0.5f, num);
				g.FillRectangle(textBackBrush, rectangleF);
				g.DrawRectangle(borderPen, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
				using (SolidBrush brush = new SolidBrush(ForeColor))
				{
					g.DrawString(Text, Font, brush, rectangleF, sf);
				}
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
			base.Name = "HslClassifier";
			base.Size = new System.Drawing.Size(161, 159);
			base.ResumeLayout(false);
		}
	}
}
