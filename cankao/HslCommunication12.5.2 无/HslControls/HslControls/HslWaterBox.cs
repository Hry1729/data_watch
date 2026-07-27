using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个水箱控件，可以设置水箱的高度，设置颜色信息")]
	public class HslWaterBox : UserControl
	{
		private float edgeWidth = 10f;

		private Color edgeColor = Color.FromArgb(253, 233, 208);

		private Brush brushEdge = new SolidBrush(Color.FromArgb(253, 233, 208));

		private Color boderColor = Color.FromArgb(234, 205, 152);

		private Pen penBorder = new Pen(Color.FromArgb(234, 205, 152));

		private Color waterColor = Color.FromArgb(207, 235, 246);

		private Brush brushWater = new SolidBrush(Color.FromArgb(207, 235, 246));

		private float waterValue = 96f;

		private StringFormat sf = null;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

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
		[Description("获取或设置水池的高度信息，值为百分比的内容。")]
		[Category("HslControls")]
		[DefaultValue(96f)]
		public float Value
		{
			get
			{
				return waterValue;
			}
			set
			{
				waterValue = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置水池边框的颜色。")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[234, 205, 152]")]
		public Color BorderColor
		{
			get
			{
				return boderColor;
			}
			set
			{
				boderColor = value;
				penBorder?.Dispose();
				penBorder = new Pen(boderColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置水池边界的颜色。")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[253, 233, 208]")]
		public Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				brushEdge?.Dispose();
				brushEdge = new SolidBrush(edgeColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置水池的颜色。")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[207,235, 246]")]
		public Color WaterColor
		{
			get
			{
				return waterColor;
			}
			set
			{
				waterColor = value;
				brushWater?.Dispose();
				brushWater = new SolidBrush(waterColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置水池边界的宽度。")]
		[Category("HslControls")]
		[DefaultValue(10f)]
		public float EdgeWidth
		{
			get
			{
				return edgeWidth;
			}
			set
			{
				edgeWidth = value;
				if (edgeWidth < 0f)
				{
					edgeWidth = 0f;
				}
				Invalidate();
			}
		}

		public HslWaterBox()
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
			if (height < edgeWidth + 2f)
			{
				return;
			}
			g.TranslateTransform(width / 2f, 0f);
			float num = waterValue;
			if (num > 100f)
			{
				num = 100f;
			}
			if (num < 0f)
			{
				num = 0f;
			}
			float num2 = (height - edgeWidth - 1f) * num / 100f;
			g.FillRectangle(rect: new RectangleF((0f - width) / 2f, height - num2 - edgeWidth - 1f, width, num2), brush: brushWater);
			g.TranslateTransform((0f - width) / 2f, 0f);
			PointF[] points = new PointF[9]
			{
				new PointF(0f, 0f),
				new PointF(edgeWidth, 0f),
				new PointF(edgeWidth, height - edgeWidth - 1f),
				new PointF(width - edgeWidth, height - edgeWidth - 1f),
				new PointF(width - edgeWidth, 0f),
				new PointF(width - 1f, 0f),
				new PointF(width - 1f, height - 1f),
				new PointF(0f, height - 1f),
				new PointF(0f, 0f)
			};
			g.FillPolygon(brushEdge, points);
			g.DrawPolygon(penBorder, points);
			if (string.IsNullOrEmpty(Text))
			{
				return;
			}
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, new RectangleF(0f, 0f, width, height), sf);
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
			base.Name = "HslWaterBox";
			base.Size = new System.Drawing.Size(134, 119);
			base.ResumeLayout(false);
		}
	}
}
