using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls.Advanced
{
	public class NoBoundaryControl : UserControl
	{
		private Color graphiccolor = Color.FromArgb(168, 168, 168);

		private float[] scales = new float[17]
		{
			0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.1f,
			1.3f, 1.5f, 2f, 2.5f, 3f, 4f, 5f
		};

		private int scaleIndex = 8;

		private double offsetX = 0.0;

		private double offsetY = 0.0;

		private Point MouseCurrent = new Point(0, 0);

		private bool zoomEnable = true;

		private bool translationEnable = true;

		private bool isLeftMouseDown = false;

		private Point leftMouseDownPoint = default(Point);

		private int mouseDelta = 0;

		private Rectangle boundary = new Rectangle(0, 0, -100, -100);

		private IContainer components = null;

		[DefaultValue(typeof(Color), "168,168,168")]
		[Description("用来显示画布的颜色")]
		[Category("HslControls")]
		public Color GraphicColor
		{
			get
			{
				return graphiccolor;
			}
			set
			{
				graphiccolor = value;
				Invalidate();
			}
		}

		[DefaultValue(true)]
		[Description("获取或设置当前的控件是否允许缩放功能")]
		[Category("HslControls")]
		public bool ZoomEnable
		{
			get
			{
				return zoomEnable;
			}
			set
			{
				zoomEnable = value;
			}
		}

		[DefaultValue(true)]
		[Description("获取或设置当前的控件是否允许平移功能")]
		[Category("HslControls")]
		public bool TranslationEnable
		{
			get
			{
				return translationEnable;
			}
			set
			{
				translationEnable = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
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

		[DefaultValue(0.0)]
		[Description("获取或设置当前的控件的X轴的偏移信息")]
		[Category("HslControls")]
		public int OffsetX
		{
			get
			{
				return (int)offsetX;
			}
			set
			{
				offsetX = value;
				Invalidate();
			}
		}

		[DefaultValue(0.0)]
		[Description("获取或设置当前的控件的Y轴的偏移信息")]
		[Category("HslControls")]
		public int OffsetY
		{
			get
			{
				return (int)offsetY;
			}
			set
			{
				offsetY = value;
				Invalidate();
			}
		}

		public NoBoundaryControl()
		{
			InitializeComponent();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (zoomEnable)
			{
				double num = 0.0;
				double num2 = 0.0;
				float num3 = scales[scaleIndex];
				if (e.Delta == 120)
				{
					if (scaleIndex >= scales.Length - 1)
					{
						return;
					}
					num = (float)e.Location.X / num3 - (float)e.Location.X / scales[scaleIndex + 1];
					num2 = (float)e.Location.Y / num3 - (float)e.Location.Y / scales[scaleIndex + 1];
					scaleIndex++;
				}
				else if (e.Delta == -120)
				{
					if (scaleIndex < 1)
					{
						return;
					}
					num = (float)e.Location.X / num3 - (float)e.Location.X / scales[scaleIndex - 1];
					num2 = (float)e.Location.Y / num3 - (float)e.Location.Y / scales[scaleIndex - 1];
					scaleIndex--;
				}
				mouseDelta = e.Delta;
				offsetX += num;
				offsetY += num2;
				OffsetModInteger();
				base.OnMouseWheel(e);
				Invalidate();
			}
			else
			{
				base.OnMouseWheel(e);
			}
		}

		private void OffsetModInteger()
		{
			offsetX = Math.Round(offsetX, 0);
			offsetY = Math.Round(offsetY, 0);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			isLeftMouseDown = false;
			Cursor = Cursors.Arrow;
			base.OnMouseLeave(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			MouseCurrent = e.Location;
			if (translationEnable && isLeftMouseDown)
			{
				offsetX -= (float)(e.Location.X - leftMouseDownPoint.X) / scales[scaleIndex];
				offsetY -= (float)(e.Location.Y - leftMouseDownPoint.Y) / scales[scaleIndex];
				leftMouseDownPoint = e.Location;
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isLeftMouseDown = false;
				Cursor = Cursors.Arrow;
				OffsetModInteger();
			}
			base.OnMouseUp(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isLeftMouseDown = true;
				Cursor = Cursors.Hand;
				leftMouseDownPoint = e.Location;
			}
			base.OnMouseDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(false)
			{
				e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
				e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				Graphics graphics = e.Graphics;
				graphics.Clear(graphiccolor);
				GraphicsTransform(graphics);
				base.OnPaint(e);
				graphics.ResetTransform();
				graphics.DrawString($"X:{offsetX:F0} Y:{offsetY:F0} Scale:{scales[scaleIndex]}", Font, Brushes.DimGray, new Point(5, base.Height - Font.Height - 2));
				Point point = ControlToActualPoint(MouseCurrent);
				graphics.DrawString($"CurrentX:{point.X} CurrentY:{point.Y}", Font, Brushes.DimGray, new Point(400, base.Height - Font.Height - 2));
			}
		}

		public Point ActualToControlPoint(Point point)
		{
			Point result = default(Point);
			result.X = (int)(((double)point.X - offsetX) * (double)scales[scaleIndex]);
			result.Y = (int)(((double)point.Y - offsetY) * (double)scales[scaleIndex]);
			return result;
		}

		public Point ControlToActualPoint(Point point)
		{
			Point result = default(Point);
			result.X = (int)Math.Round(offsetX + (double)((float)point.X / scales[scaleIndex]));
			result.Y = (int)Math.Round(offsetY + (double)((float)point.Y / scales[scaleIndex]));
			return result;
		}

		public void GraphicsTransform(Graphics g)
		{
			g.ResetTransform();
			g.ScaleTransform(scales[scaleIndex], scales[scaleIndex]);
			g.TranslateTransform((float)(0.0 - offsetX), (float)(0.0 - offsetY));
		}

		public void ResetGraphicsTransform(Graphics g)
		{
			g.ResetTransform();
		}

		public void SetScales(float[] scales, int index)
		{
			if (scales != null)
			{
				this.scales = scales;
				if (index >= 0 && index < scales.Length)
				{
					scaleIndex = index;
				}
			}
		}

		public float GetScales()
		{
			return scales[scaleIndex];
		}

		public void DrawImage(Graphics g, Point location, Bitmap image, bool noScale = true, float angle = 0f)
		{
			g.TranslateTransform(location.X, location.Y);
			g.RotateTransform(angle);
			if (image != null)
			{
				if (noScale)
				{
					int num = Convert.ToInt32((float)image.Width / GetScales());
					int num2 = Convert.ToInt32((float)image.Height / GetScales());
					g.DrawImage(image, new RectangleF(-num / 2, -num2 / 2, num, num2));
				}
				else
				{
					g.DrawImage(image, new RectangleF(-image.Width / 2, -image.Height / 2, image.Width, image.Height));
				}
			}
			g.RotateTransform(0f - angle);
			g.TranslateTransform(-location.X, -location.Y);
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
			this.ForeColor = System.Drawing.Color.DimGray;
			base.Name = "NoBoundaryControl";
			base.Size = new System.Drawing.Size(946, 594);
			base.ResumeLayout(false);
		}
	}
}
