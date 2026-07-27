using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("冷却风扇控件，支持正反方向设置，支持转速设置，颜色设置")]
	public class HslCoolFan : UserControl
	{
		private StringFormat sf = null;

		private Color edgeColor = Color.Gray;

		private Color centerColor = Color.FromArgb(91, 92, 93);

		private Pen edgePen = new Pen(Color.Gray, 1f);

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

		private float moveSpeed = 0.3f;

		private float startAngle = 0f;

		private Timer timer = null;

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
		[Description("获取或设置阀门控件的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[91, 92, 93]")]
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
		[Description("获取或设置传送带流动的速度，0为静止，正数为正向流动，负数为反向流动")]
		[Category("HslControls")]
		[DefaultValue(0.3f)]
		public virtual float MoveSpeed
		{
			get
			{
				return moveSpeed;
			}
			set
			{
				moveSpeed = value;
				Invalidate();
			}
		}

		public HslCoolFan()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			timer = new Timer();
			timer.Interval = 50;
			timer.Tick += Timer_Tick;
			timer.Start();
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
					PaintMain(g, Math.Min(width, height), Math.Min(width, height));
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
			g.TranslateTransform(width / 2f, width / 2f);
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(76, 76, 76),
				Color.FromArgb(245, 245, 245),
				Color.FromArgb(76, 76, 76)
			};
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF((0f - width) / 2f, 0f), new PointF(width / 2f, 0f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
			linearGradientBrush.InterpolationColors = colorBlend;
			g.FillRectangle(linearGradientBrush, (0f - width) / 2f, (0f - width) / 2f, width, width);
			linearGradientBrush.Dispose();
			using (Brush brush = new SolidBrush(Color.FromArgb(127, 127, 127)))
			{
				g.FillEllipse(brush, new RectangleF((0f - width) * 0.46f, (0f - width) * 0.46f, width * 0.92f, width * 0.92f));
			}
			using (Brush brush2 = new SolidBrush(Color.FromArgb(35, 31, 32)))
			{
				g.FillEllipse(brush2, new RectangleF((0f - width) * 0.44f, (0f - width) * 0.44f, width * 0.88f, width * 0.88f));
			}
			g.RotateTransform(startAngle);
			for (int i = 0; i < 6; i++)
			{
				g.RotateTransform(60f);
				using (Brush brush3 = new SolidBrush(Color.FromArgb(77, 77, 77)))
				{
					g.FillRectangle(brush3, new RectangleF(0f, (0f - width) * 0.08f, width * 0.4f, width * 0.16f));
				}
			}
			for (int j = 0; j < 6; j++)
			{
				g.RotateTransform(60f);
				using (GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate))
				{
					graphicsPath.AddLine(0f, (0f - width) * 0.08f, 0f, width * 0.08f);
					graphicsPath.AddBezier(new PointF(0f, width * 0.08f), new PointF(width * 0.15f, width * 0.15f), new PointF(width * 0.25f, width * 0.15f), new PointF(width * 0.4f, width * 0.08f));
					graphicsPath.AddLine(width * 0.4f, width * 0.08f, width * 0.4f, (0f - width) * 0.08f);
					graphicsPath.AddBezier(new PointF(width * 0.4f, (0f - width) * 0.08f), new PointF(width * 0.25f, (0f - width) * 0.01f), new PointF(width * 0.15f, (0f - width) * 0.01f), new PointF(0f, (0f - width) * 0.08f));
					graphicsPath.CloseFigure();
					linearGradientBrush = new LinearGradientBrush(new PointF(0f, width * 0.08f), new PointF(width * 0.4f, (0f - width) * 0.08f), Color.FromArgb(247, 252, 253), Color.WhiteSmoke);
					colorBlend = new ColorBlend();
					colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
					colorBlend.Colors = new Color[3]
					{
						centerColor,
						Color.FromArgb(236, 236, 236),
						centerColor
					};
					linearGradientBrush.InterpolationColors = colorBlend;
					g.FillPath(linearGradientBrush, graphicsPath);
					linearGradientBrush.Dispose();
				}
			}
			g.RotateTransform(0f - startAngle);
			using (Brush brush4 = new SolidBrush(Color.FromArgb(102, 102, 102)))
			{
				g.FillEllipse(brush4, new RectangleF((0f - width) * 0.13f, (0f - width) * 0.13f, width * 0.26f, width * 0.26f));
			}
			using (Brush brush5 = new SolidBrush(Color.FromArgb(229, 229, 229)))
			{
				g.FillEllipse(brush5, new RectangleF((0f - width) * 0.11f, (0f - width) * 0.11f, width * 0.22f, width * 0.22f));
			}
			using (Brush brush6 = new SolidBrush(Color.FromArgb(127, 127, 127)))
			{
				g.FillEllipse(brush6, new RectangleF((0f - width) * 0.05f, (0f - width) * 0.05f, width * 0.1f, width * 0.1f));
			}
			using (Brush brush7 = new SolidBrush(ForeColor))
			{
				g.DrawString(Text, Font, brush7, new RectangleF((0f - width) / 2f, height * 0.38f, width, height * 0.55f), sf);
			}
			g.TranslateTransform((0f - width) / 2f, (0f - width) / 2f);
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (moveSpeed != 0f)
			{
				startAngle = (float)((double)startAngle + (double)(moveSpeed * 180f) / Math.PI / 10.0);
				if (startAngle <= -360f)
				{
					startAngle += 360f;
				}
				else if (startAngle >= 360f)
				{
					startAngle -= 360f;
				}
				Invalidate();
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
			base.Name = "HslCoolFan";
			base.Size = new System.Drawing.Size(200, 200);
			base.ResumeLayout(false);
		}
	}
}
