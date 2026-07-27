using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个阀门控件，支持开关两种状态，支持颜色的设置")]
	public class HslValves : UserControl
	{
		private StringFormat sf = null;

		private Color edgeColor = Color.Gray;

		private Pen edgePen = new Pen(Color.Gray, 1f);

		private GraphDirection hslValvesStyle = GraphDirection.Upward;

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
		[Description("获取或设置阀门控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color EdgeColor
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
		[Description("获取或设置阀门控件的方向，可以是向上，也可以向右，向左，向下")]
		[Category("HslControls")]
		[DefaultValue(typeof(GraphDirection), "Upward")]
		public GraphDirection PipeLineStyle
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

		public HslValves()
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
				if (hslValvesStyle == GraphDirection.Upward)
				{
					PaintMain(g, width, height);
				}
				else if (hslValvesStyle == GraphDirection.Rightward)
				{
					g.TranslateTransform(width, 0f);
					g.RotateTransform(90f);
					PaintMain(g, height, width);
					g.ResetTransform();
				}
				else if (hslValvesStyle == GraphDirection.Downward)
				{
					g.RotateTransform(180f);
					g.TranslateTransform(-width, -height);
					PaintMain(g, width, height);
					g.ResetTransform();
				}
				else
				{
					g.RotateTransform(-90f);
					g.TranslateTransform(-height, 0f);
					PaintMain(g, height, width);
					g.ResetTransform();
				}
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			g.TranslateTransform(width / 2f, 0f);
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.25f, 1f };
			colorBlend.Colors = new Color[3]
			{
				edgeColor,
				Color.WhiteSmoke,
				edgeColor
			};
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, height * 0.34f), new PointF(0f, height * 0.93f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.5f - 1f, height * 0.38f, width + 1f, height * 0.55f);
			linearGradientBrush.Dispose();
			linearGradientBrush = new LinearGradientBrush(new PointF(0f, height * 0.25f), new PointF(0f, height * 1.1f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.45f, height * 0.31f, width * 0.18f, height * 0.69f);
			g.FillRectangle(linearGradientBrush, width * 0.27f, height * 0.31f, width * 0.18f, height * 0.69f);
			linearGradientBrush.Dispose();
			colorBlend.Positions = new float[3] { 0f, 0.31f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(144, 162, 167),
				Color.WhiteSmoke,
				Color.FromArgb(144, 162, 167)
			};
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) * 0.07f, 0f), new PointF(width * 0.07f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.05f, height * 0.1f, width * 0.1f, height * 0.13f);
			linearGradientBrush.Dispose();
			colorBlend.Positions = new float[3] { 0f, 0.21f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(190, 190, 190),
				Color.WhiteSmoke,
				Color.FromArgb(170, 175, 175)
			};
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) * 0.1f, 0f), new PointF(width * 0.1f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillEllipse(linearGradientBrush, (0f - width) * 0.1f, height * 0.34f, width * 0.2f, height * 0.08f);
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddLines(new PointF[4]
			{
				new PointF((0f - width) * 0.1f, height * 0.38f),
				new PointF((0f - width) * 0.07f, height * 0.22f),
				new PointF(width * 0.07f, height * 0.22f),
				new PointF(width * 0.1f, height * 0.38f)
			});
			graphicsPath.CloseFigure();
			g.FillPath(linearGradientBrush, graphicsPath);
			linearGradientBrush.Dispose();
			colorBlend.Positions = new float[3] { 0f, 0.78f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(172, 77, 80),
				Color.FromArgb(244, 188, 189),
				Color.FromArgb(172, 77, 80)
			};
			linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) * 0.23f, 0f), new PointF(width * 0.23f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, (0f - width) * 0.23f, 0f, width * 0.46f, height * 0.15f);
			linearGradientBrush.Dispose();
			colorBlend.Positions = new float[3] { 0f, 0.7f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(165, 74, 77),
				Color.FromArgb(244, 188, 189),
				Color.FromArgb(184, 87, 90)
			};
			linearGradientBrush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(width * 0.06f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.TranslateTransform((0f - width) * 0.03f, 0f);
			g.FillRectangle(linearGradientBrush, 0f, 0f, width * 0.06f, height * 0.15f);
			g.TranslateTransform((0f - width) * 0.2f, 0f);
			g.FillRectangle(linearGradientBrush, 0f, 0f, width * 0.06f, height * 0.15f);
			g.TranslateTransform(width * 0.4f, 0f);
			g.FillRectangle(linearGradientBrush, 0f, 0f, width * 0.06f, height * 0.15f);
			g.TranslateTransform((0f - width) * 0.17f, 0f);
			linearGradientBrush.Dispose();
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush, new RectangleF((0f - width) / 2f, height * 0.38f, width, height * 0.55f), sf);
			}
			g.TranslateTransform((0f - width) / 2f, 0f);
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
			base.Name = "HslValves";
			base.Size = new System.Drawing.Size(177, 90);
			base.ResumeLayout(false);
		}
	}
}
