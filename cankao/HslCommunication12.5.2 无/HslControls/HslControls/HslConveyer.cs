using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("传送带控件，支持正反方向设置，支持转速设置，颜色设置")]
	public class HslConveyer : UserControl
	{
		private StringFormat sf = null;

		private float moveSpeed = 0.3f;

		private float startAngle = 0f;

		private float startOffect = 0f;

		private Timer timer = null;

		private bool isConveyerActive = false;

		private float circularRadius = 20f;

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
		[Description("获取或设置传送带流动的速度，0为静止，正数为正向流动，负数为反向流动，一般情况值范围为 -9 ~ 9")]
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
		[Description("获取或设置管道线是否激活液体显示")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool ConveyerActive
		{
			get
			{
				return isConveyerActive;
			}
			set
			{
				isConveyerActive = value;
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

		public HslConveyer()
			: this(true)
		{
		}

		public HslConveyer(bool timerEnable)
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
			if (timerEnable)
			{
				timer = new Timer();
				timer.Interval = 50;
				timer.Tick += Timer_Tick;
				timer.Start();
			}
		}

		public void TimerExecute()
		{
			if (moveSpeed == 0f)
			{
				return;
			}
			if (isConveyerActive)
			{
				startOffect -= moveSpeed;
				if (startOffect <= -10f || startOffect >= 10f)
				{
					startOffect = 0f;
				}
			}
			for (startAngle = (float)((double)startAngle + (double)(moveSpeed * 180f) / Math.PI / 10.0); startAngle < -360f; startAngle += 360f)
			{
			}
			while (startAngle >= 360f)
			{
				startAngle -= 360f;
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (moveSpeed != 0f)
			{
				TimerExecute();
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
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
				else if (conveyerStyle == HslConveyerStyle.Downslope)
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
				else
				{
					g.TranslateTransform(width, 0f);
					g.RotateTransform(90f);
					PaintMain(g, height, width, num, num2);
					g.ResetTransform();
				}
			}
		}

		private void PaintMain(Graphics g, float width, float height, float margin, float offect)
		{
			float num = (height - margin * 2f - offect * 2f) / 2f;
			float num2 = startAngle;
			Pen pen = new Pen(ForeColor, 1f);
			Pen pen2 = new Pen(ForeColor, (num > 100f) ? 7 : ((num > 20f) ? 5 : ((!(num > 5f)) ? 1 : 3)));
			pen2.StartCap = LineCap.Round;
			pen2.EndCap = LineCap.Round;
			Brush brush = new SolidBrush(ForeColor);
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddArc(margin, margin, num * 2f + offect * 2f, num * 2f + offect * 2f, 90f, 180f);
			graphicsPath.AddLine(margin + offect + num, margin, width - margin - offect - num - 1f, margin);
			graphicsPath.AddArc(width - margin - num * 2f - offect * 2f, margin, num * 2f + offect * 2f, num * 2f + offect * 2f, -90f, 180f);
			graphicsPath.CloseFigure();
			g.DrawPath(pen, graphicsPath);
			g.DrawEllipse(pen, margin + offect, margin + offect, num * 2f, num * 2f);
			g.DrawEllipse(pen, width - margin - num * 2f - offect, margin + offect, num * 2f, num * 2f);
			float num3 = (float)Math.Sqrt(2f * offect * num + offect * offect);
			float num4 = (float)(Math.Asin(num / (num + offect)) * 180.0 / Math.PI);
			graphicsPath.Reset();
			graphicsPath.AddArc(margin, margin, num * 2f + offect * 2f, num * 2f + offect * 2f, 0f - num4, num4 * 2f);
			graphicsPath.AddLine(margin + offect + num + num3, height - margin - offect, width - margin - offect - num - 1f - num3, height - margin - offect);
			graphicsPath.AddArc(width - margin - num * 2f - offect * 2f, margin, num * 2f + offect * 2f, num * 2f + offect * 2f, 180f - num4, num4 * 2f);
			graphicsPath.CloseFigure();
			g.DrawPath(pen, graphicsPath);
			g.TranslateTransform(margin + offect + num, margin + offect + num);
			g.RotateTransform(num2);
			g.DrawLine(pen2, (0f - num) / 2f, 0f, num / 2f, 0f);
			g.DrawLine(pen2, 0f, (0f - num) / 2f, 0f, num / 2f);
			g.RotateTransform(0f - num2);
			g.TranslateTransform(0f - margin - offect - num, 0f - margin - offect - num);
			g.TranslateTransform(width - margin - num - offect, margin + offect + num);
			g.RotateTransform(num2);
			g.DrawLine(pen2, (0f - num) / 2f, 0f, num / 2f, 0f);
			g.DrawLine(pen2, 0f, (0f - num) / 2f, 0f, num / 2f);
			g.RotateTransform(0f - num2);
			g.TranslateTransform(0f - width + margin + num + offect, 0f - margin - offect - num);
			if (isConveyerActive)
			{
				using (Pen pen3 = new Pen(ForeColor, 5f))
				{
					pen3.DashStyle = DashStyle.Custom;
					pen3.DashPattern = new float[2] { 5f, 5f };
					pen3.DashOffset = startOffect;
					graphicsPath.Reset();
					graphicsPath.AddArc(margin + offect / 2f, margin + offect / 2f, num * 2f + offect, num * 2f + offect, 90f, 180f);
					graphicsPath.AddLine(margin + offect + num + 1f, margin + offect / 2f, width - margin - offect - num - 2f, margin + offect / 2f);
					graphicsPath.AddArc(width - margin - num * 2f - offect * 2f + offect / 2f, margin + offect / 2f, num * 2f + offect, num * 2f + offect, -90f, 180f);
					graphicsPath.CloseFigure();
					g.DrawPath(pen3, graphicsPath);
				}
			}
			g.DrawString(Text, Font, brush, new RectangleF(0f, 0f, width, height), sf);
			graphicsPath.Dispose();
			pen.Dispose();
			pen2.Dispose();
			brush.Dispose();
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
			base.Name = "HslConveyer";
			base.Size = new System.Drawing.Size(461, 61);
			base.ResumeLayout(false);
		}
	}
}
