using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[DefaultBindingProperty("Text")]
	[DefaultProperty("Text")]
	[Description("一个鼓风机控件，支持设置不同的方向，支持两个方向的旋转")]
	public class HslBlower : UserControl
	{
		private StringFormat sf = null;

		private Color color_1 = Color.FromArgb(199, 205, 211);

		private Brush brush_1 = new SolidBrush(Color.FromArgb(199, 205, 211));

		private Color color_2 = Color.FromArgb(135, 144, 156);

		private Brush brush_2 = new SolidBrush(Color.FromArgb(135, 144, 156));

		private Color color_3 = Color.FromArgb(208, 213, 220);

		private Brush brush_3 = new SolidBrush(Color.FromArgb(208, 213, 220));

		private Pen pen_1 = new Pen(Color.FromArgb(92, 100, 111), 3f);

		private Color color_4 = Color.FromArgb(153, 160, 169);

		private Brush brush_4 = new SolidBrush(Color.FromArgb(153, 160, 169));

		private Color color_5 = Color.FromArgb(92, 100, 111);

		private Brush brush_5 = new SolidBrush(Color.FromArgb(92, 100, 111));

		private Color color_6 = Color.FromArgb(108, 114, 121);

		private Color color_7 = Color.FromArgb(158, 165, 173);

		private Brush brush_7 = new SolidBrush(Color.FromArgb(158, 165, 173));

		private Color color_8 = Color.FromArgb(176, 182, 189);

		private Brush brush_8 = new SolidBrush(Color.FromArgb(176, 182, 189));

		private GraphDirection direction = GraphDirection.Rightward;

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
		[Description("获取或设置风机的出口方向")]
		[Category("HslControls")]
		[DefaultValue(GraphDirection.Rightward)]
		public virtual GraphDirection ExportDirection
		{
			get
			{
				return direction;
			}
			set
			{
				direction = value;
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
		[Description("获取或设置颜色1")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[199, 205, 211]")]
		public virtual Color Color1
		{
			get
			{
				return color_1;
			}
			set
			{
				color_1 = value;
				brush_1.Dispose();
				brush_1 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色2")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[135, 144, 156]")]
		public virtual Color Color2
		{
			get
			{
				return color_2;
			}
			set
			{
				color_2 = value;
				brush_2.Dispose();
				brush_2 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色3")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[208, 213, 220]")]
		public virtual Color Color3
		{
			get
			{
				return color_3;
			}
			set
			{
				color_3 = value;
				brush_3.Dispose();
				brush_3 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色4")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[153, 160, 169]")]
		public virtual Color Color4
		{
			get
			{
				return color_4;
			}
			set
			{
				color_4 = value;
				brush_4.Dispose();
				brush_4 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色5")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[92, 100, 111]")]
		public virtual Color Color5
		{
			get
			{
				return color_5;
			}
			set
			{
				color_5 = value;
				brush_5.Dispose();
				brush_5 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色6")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[108, 114, 121]")]
		public virtual Color Color6
		{
			get
			{
				return color_6;
			}
			set
			{
				color_6 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色7")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[158, 165, 173]")]
		public virtual Color Color7
		{
			get
			{
				return color_7;
			}
			set
			{
				color_7 = value;
				brush_7.Dispose();
				brush_7 = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置颜色8")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "[176, 182, 189]")]
		public virtual Color Color8
		{
			get
			{
				return color_8;
			}
			set
			{
				color_8 = value;
				brush_8.Dispose();
				brush_8 = new SolidBrush(value);
				Invalidate();
			}
		}

		public HslBlower()
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
			float num = Math.Min(width - 1, height - 1);
			g.TranslateTransform(num / 2f, num / 2f);
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Positions = new float[3] { 0f, 0.65f, 1f };
			colorBlend.Colors = new Color[3]
			{
				color_6,
				Color.WhiteSmoke,
				color_6
			};
			if (direction == GraphDirection.Leftward)
			{
				LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new PointF(0f, (0f - num) * 0.06f), new PointF(0f, (0f - num) * 0.35f), color_1, color_6);
				linearGradientBrush.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush, (0f - num) * 0.49f, (0f - num) * 0.35f + 1f, num * 0.49f, num * 0.29f);
				linearGradientBrush.Dispose();
				g.FillRectangle(brush_2, (0f - num) * 0.5f - 1f, (0f - num) * 0.35f - 2f, num * 0.01f + 1f, num * 0.29f + 5f);
			}
			else if (direction == GraphDirection.Rightward)
			{
				LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush(new PointF(0f, (0f - num) * 0.06f), new PointF(0f, (0f - num) * 0.35f), color_1, color_6);
				linearGradientBrush2.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush2, 0f, (0f - num) * 0.35f + 1f, num * 0.49f, num * 0.29f);
				linearGradientBrush2.Dispose();
				g.FillRectangle(brush_2, num * 0.49f - 1f, (0f - num) * 0.35f - 2f, num * 0.01f + 2f, num * 0.29f + 5f);
			}
			else if (direction == GraphDirection.Upward)
			{
				LinearGradientBrush linearGradientBrush3 = new LinearGradientBrush(new PointF(num * 0.06f, 0f), new PointF(num * 0.35f, 0f), color_1, color_6);
				linearGradientBrush3.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush3, num * 0.06f, (0f - num) * 0.49f, num * 0.29f, num * 0.49f);
				linearGradientBrush3.Dispose();
				g.FillRectangle(brush_2, num * 0.06f - 2f, (0f - num) * 0.5f - 1f, num * 0.29f + 5f, num * 0.01f + 2f);
			}
			else if (direction == GraphDirection.Downward)
			{
				LinearGradientBrush linearGradientBrush4 = new LinearGradientBrush(new PointF(num * 0.06f, 0f), new PointF(num * 0.35f, 0f), color_1, color_6);
				linearGradientBrush4.InterpolationColors = colorBlend;
				g.FillRectangle(linearGradientBrush4, num * 0.06f, 0f, num * 0.29f, num * 0.49f);
				linearGradientBrush4.Dispose();
				g.FillRectangle(brush_2, num * 0.06f - 2f, num * 0.49f - 1f, num * 0.29f + 5f, num * 0.01f + 2f);
			}
			if (direction != GraphDirection.Downward)
			{
				g.FillRectangle(brush_7, (0f - num) * 0.25f, 0f, num * 0.5f, num * 0.49f - 1f);
				g.FillRectangle(brush_8, (0f - num) * 0.23f, 0f, num * 0.46f, num * 0.49f - 1f);
				g.FillRectangle(brush_7, (0f - num) * 0.3f, num * 0.47f, num * 0.6f, num * 0.02f + 1f);
			}
			g.FillEllipse(brush_1, (0f - num) * 0.35f, (0f - num) * 0.35f, num * 0.7f, num * 0.7f);
			g.FillEllipse(brush_2, (0f - num) * 0.28f, (0f - num) * 0.28f, num * 0.56f, num * 0.56f);
			g.FillEllipse(brush_3, (0f - num) * 0.21f, (0f - num) * 0.21f, num * 0.42f, num * 0.42f);
			g.RotateTransform(startAngle);
			if (num < 50f)
			{
				pen_1.Width = 1f;
			}
			else if (num < 100f)
			{
				pen_1.Width = 2f;
			}
			else
			{
				pen_1.Width = 3f;
			}
			for (int i = 0; i < 4; i++)
			{
				g.DrawLine(pen_1, (0f - num) * 0.2f, 0f, num * 0.2f, 0f);
				g.RotateTransform(45f);
			}
			g.RotateTransform(-180f);
			g.RotateTransform(0f - startAngle);
			g.FillEllipse(brush_3, (0f - num) * 0.09f, (0f - num) * 0.09f, num * 0.18f, num * 0.18f);
			g.FillEllipse(brush_4, (0f - num) * 0.08f, (0f - num) * 0.08f, num * 0.16f, num * 0.16f);
			g.FillEllipse(brush_3, (0f - num) * 0.02f, (0f - num) * 0.02f, num * 0.04f, num * 0.04f);
			g.FillEllipse(brush_5, (0f - num) * 0.01f, (0f - num) * 0.01f, num * 0.02f, num * 0.02f);
			if ((float)height - num > (float)Font.Height && !string.IsNullOrEmpty(Text))
			{
				using (Brush brush = new SolidBrush(ForeColor))
				{
					g.DrawString(Text, Font, brush, new RectangleF((0f - num) / 2f, num / 2f + 1f, num, (float)base.Height - num), sf);
				}
			}
			g.TranslateTransform((0f - num) / 2f, (0f - num) / 2f);
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
			base.Name = "HslBlower";
			base.Size = new System.Drawing.Size(160, 220);
			base.ResumeLayout(false);
		}
	}
}
