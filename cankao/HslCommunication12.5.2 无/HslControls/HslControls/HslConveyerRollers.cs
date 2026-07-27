using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("传送带滚筒控件，支持正反方向转动设置，支持转速设置，颜色设置")]
	public class HslConveyerRollers : UserControl
	{
		private StringFormat sf = null;

		private float moveSpeed = 0.3f;

		private float startAngle = 0f;

		private float startOffect = 0f;

		private Timer timer = null;

		private float circularRadius = 20f;

		private float density = 1f;

		private float upPercent = 0f;

		private float rightPercent = 0f;

		private HslConveyerStyle conveyerStyle = HslConveyerStyle.Horizontal;

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
		[DefaultValue(typeof(Color), "[142, 196, 216]")]
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
		[Description("获取或设置两侧的圆圈的半径，当且仅当ConveyerStyle属性不为Horizontal枚举时成立")]
		[Category("HslControls")]
		[DefaultValue(HslConveyerStyle.Horizontal)]
		public virtual float CircularRadius
		{
			get
			{
				return circularRadius;
			}
			set
			{
				if (!(value > (float)(base.Width / 2)) && !(value > (float)(base.Height / 2)))
				{
					circularRadius = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置传送带的样式，水平，还是上坡，还是下坡")]
		[Category("HslControls")]
		[DefaultValue(HslConveyerStyle.Horizontal)]
		public virtual HslConveyerStyle ConveyerStyle
		{
			get
			{
				return conveyerStyle;
			}
			set
			{
				conveyerStyle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置传送带的滚筒密度，默认是1，完全分布，大于1则表示更密，小于1表示更稀疏")]
		[Category("HslControls")]
		[DefaultValue(1f)]
		public virtual float DistributeDensity
		{
			get
			{
				return density;
			}
			set
			{
				density = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置上边的偏移量信息，用于3D的传送带显示")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float UpPercent
		{
			get
			{
				return upPercent;
			}
			set
			{
				upPercent = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置右边的偏移量信息，用于3D的传送带显示")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float RightPercent
		{
			get
			{
				return rightPercent;
			}
			set
			{
				rightPercent = value;
				Invalidate();
			}
		}

		public HslConveyerRollers()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			base.ForeColor = Color.FromArgb(142, 196, 216);
			timer = new Timer();
			timer.Interval = 50;
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (moveSpeed != 0f)
			{
				for (startAngle = (float)((double)startAngle + (double)(moveSpeed * 180f) / Math.PI / 10.0); startAngle < -360f; startAngle += 360f)
				{
				}
				while (startAngle > 360f)
				{
					startAngle -= 360f;
				}
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(false)
			{
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				PaintHslControls(e.Graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				int num = 5;
				int num2 = 5;
				if (conveyerStyle == HslConveyerStyle.Horizontal)
				{
					PaintMain(g, width, height, num, num2);
				}
				else if (conveyerStyle == HslConveyerStyle.Upslope)
				{
					PointF pointF = new PointF((float)(num + num2) + circularRadius, (float)(height - 1 - num - num2) - circularRadius);
					PointF pointF2 = new PointF((float)(width - num - num2 - 1) - circularRadius, (float)(num + num2) + circularRadius);
					float num3 = (float)Math.Sqrt(Math.Pow(pointF2.Y - pointF.Y, 2.0) + Math.Pow(pointF2.X - pointF.X, 2.0));
					float num4 = (float)(Math.Acos((pointF2.X - pointF.X) / num3) * 180.0 / Math.PI);
					g.TranslateTransform(pointF.X, pointF.Y);
					g.RotateTransform(0f - num4);
					g.TranslateTransform((float)(-num - num2) - circularRadius, (float)(-num - num2) - circularRadius);
					PaintMain(g, num3 + (float)(2 * num) + (float)(2 * num2) + 2f * circularRadius, (float)(2 * num + 2 * num2) + 2f * circularRadius, num, num2);
					g.ResetTransform();
				}
				else
				{
					PointF pointF3 = new PointF((float)(num + num2) + circularRadius, (float)(num + num2) + circularRadius);
					PointF pointF4 = new PointF((float)(width - num - num2 - 1) - circularRadius, (float)(height - 1 - num - num2) - circularRadius);
					float num5 = (float)Math.Sqrt(Math.Pow(pointF4.Y - pointF3.Y, 2.0) + Math.Pow(pointF4.X - pointF3.X, 2.0));
					float angle = (float)(Math.Acos((pointF4.X - pointF3.X) / num5) * 180.0 / Math.PI);
					g.TranslateTransform(pointF3.X, pointF3.Y);
					g.RotateTransform(angle);
					g.TranslateTransform((float)(-num - num2) - circularRadius, (float)(-num - num2) - circularRadius);
					PaintMain(g, num5 + (float)(2 * num) + (float)(2 * num2) + 2f * circularRadius, (float)(2 * num + 2 * num2) + 2f * circularRadius, num, num2);
					g.ResetTransform();
				}
			}
		}

		private void PaintMain(Graphics g, float width, float height, float margin, float offect)
		{
			g.TranslateTransform(0f, height * upPercent);
			float num = height * upPercent;
			float num2 = width * rightPercent;
			height *= 1f - upPercent;
			width *= 1f - rightPercent;
			float num3 = (height - margin * 2f - offect * 2f) / 2f;
			Pen pen = new Pen(ForeColor, 1f);
			Pen pen2 = new Pen(ForeColor, (num3 > 100f) ? 7 : ((num3 > 20f) ? 5 : ((!(num3 > 6f)) ? 1 : 3)));
			pen2.StartCap = LineCap.Round;
			pen2.EndCap = LineCap.Round;
			Brush brush = new SolidBrush(ForeColor);
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddArc(margin, margin, num3 * 2f + offect * 2f, num3 * 2f + offect * 2f, 90f, 180f);
			graphicsPath.AddLine(margin + offect + num3, margin, width - margin - offect - num3 - 1f, margin);
			graphicsPath.AddArc(width - margin - num3 * 2f - offect * 2f, margin, num3 * 2f + offect * 2f, num3 * 2f + offect * 2f, -90f, 180f);
			graphicsPath.CloseFigure();
			g.DrawPath(pen, graphicsPath);
			g.DrawEllipse(pen, margin + offect, margin + offect, num3 * 2f, num3 * 2f);
			g.DrawEllipse(pen, width - margin - num3 * 2f - offect, margin + offect, num3 * 2f, num3 * 2f);
			g.TranslateTransform(margin + offect + num3, margin + offect + num3);
			g.RotateTransform(startAngle);
			g.DrawLine(pen2, (0f - num3) / 2f, 0f, num3 / 2f, 0f);
			g.DrawLine(pen2, 0f, (0f - num3) / 2f, 0f, num3 / 2f);
			g.RotateTransform(0f - startAngle);
			g.TranslateTransform(0f - margin - offect - num3, 0f - margin - offect - num3);
			g.TranslateTransform(width - margin - num3 - offect, margin + offect + num3);
			g.RotateTransform(startAngle);
			g.DrawLine(pen2, (0f - num3) / 2f, 0f, num3 / 2f, 0f);
			g.DrawLine(pen2, 0f, (0f - num3) / 2f, 0f, num3 / 2f);
			g.RotateTransform(0f - startAngle);
			g.TranslateTransform(0f - width + margin + num3 + offect, 0f - margin - offect - num3);
			float num4 = width - margin * 2f - num3 * 4f - offect * 2f;
			int num5 = (int)(num4 * density / num3 / 2f);
			float num6 = num4 / (float)num5;
			for (int i = 0; i < num5; i++)
			{
				g.TranslateTransform(margin + offect + num3 * 2f + num6 * (float)i + num6 / 2f, margin + offect + num3);
				g.RotateTransform(startAngle);
				g.DrawLine(pen2, (0f - num3) / 2f, 0f, num3 / 2f, 0f);
				g.DrawLine(pen2, 0f, (0f - num3) / 2f, 0f, num3 / 2f);
				g.RotateTransform(0f - startAngle);
				g.DrawEllipse(pen, 0f - num3, 0f - num3, num3 * 2f, num3 * 2f);
				g.TranslateTransform(0f - margin - offect - num3 * 2f - num6 * (float)i - num6 / 2f, 0f - margin - offect - num3);
			}
			if (num > 0f && num2 > 0f)
			{
				g.DrawLines(pen, new PointF[4]
				{
					new PointF(margin + offect + num3, margin),
					new PointF(margin + offect + num3 + num2, 0f - num + margin),
					new PointF(width - margin - num3 - offect + num2, 0f - num + margin),
					new PointF(width - margin - num3 - offect, margin)
				});
				g.DrawLine(pen, width - margin - num3 - offect, height - margin, width - margin - num3 - offect + num2, height - margin - num);
				g.DrawArc(pen, width - margin - num3 - offect + num2 - num3 - offect, 0f - num + margin, num3 * 2f + offect * 2f, num3 * 2f + offect * 2f, -90f, 180f);
			}
			graphicsPath.Dispose();
			pen.Dispose();
			pen2.Dispose();
			brush.Dispose();
			g.TranslateTransform(0f, (0f - height) * upPercent);
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
			base.Name = "HslConveyerRollers";
			base.Size = new System.Drawing.Size(451, 90);
			base.ResumeLayout(false);
		}
	}
}
