using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个用于签名的控件，方便实现用户签名，并对签名内容进行保存")]
	public class HslSignature : UserControl
	{
		private Color boderColor = Color.FromArgb(205, 205, 205);

		private Pen penBorder = new Pen(Color.FromArgb(205, 205, 205));

		private StringFormat sf = null;

		private HslDirectionStyle hslValvesStyle = HslDirectionStyle.Horizontal;

		private List<List<PointF>> totalPoints = new List<List<PointF>>();

		private List<PointF> currPoints = new List<PointF>();

		private bool isEnableSign = true;

		private bool isMouseDown = false;

		private float signWidth = 1f;

		private Color signColor = Color.Black;

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
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
		[Description("获取或设置当前控件的是否允许签名，默认是允许签名的")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool EnableSign
		{
			get
			{
				return isEnableSign;
			}
			set
			{
				isEnableSign = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前签名控件的宽度信息")]
		[Category("HslControls")]
		[DefaultValue(1f)]
		public float SignWidth
		{
			get
			{
				return signWidth;
			}
			set
			{
				signWidth = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前签名的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Black")]
		public Color SignColor
		{
			get
			{
				return signColor;
			}
			set
			{
				signColor = value;
				Invalidate();
			}
		}

		public HslSignature()
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

		private void HslSignature_Load(object sender, EventArgs e)
		{
			base.MouseDown += HslSignature_MouseDown;
			base.MouseUp += HslSignature_MouseUp;
			base.MouseMove += HslSignature_MouseMove;
		}

		private void HslSignature_MouseMove(object sender, MouseEventArgs e)
		{
			if (isMouseDown && isEnableSign)
			{
				currPoints.Add(new PointF(e.X, e.Y));
				Invalidate();
			}
		}

		private void HslSignature_MouseUp(object sender, MouseEventArgs e)
		{
			if (isEnableSign && e.Button == MouseButtons.Left)
			{
				isMouseDown = false;
				currPoints.Add(new PointF(e.X, e.Y));
				totalPoints.Add(currPoints);
				currPoints = new List<PointF>();
				Invalidate();
			}
		}

		private void HslSignature_MouseDown(object sender, MouseEventArgs e)
		{
			if (isEnableSign)
			{
				if (e.Button == MouseButtons.Left)
				{
					isMouseDown = true;
					currPoints.Add(new PointF(e.X, e.Y));
				}
				else if (e.Button == MouseButtons.Right)
				{
					ClearSign();
				}
			}
		}

		public void ClearSign()
		{
			currPoints.Clear();
			totalPoints.Clear();
			Invalidate();
		}

		public Image SaveBitmap()
		{
			Bitmap bitmap = new Bitmap(base.Width, base.Height);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			graphics.Clear(BackColor);
			PaintMain(graphics, bitmap.Width, bitmap.Height);
			graphics.Dispose();
			return bitmap;
		}

		public void SaveBitmap(string fileName)
		{
			SaveBitmap().Save(fileName);
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
			using (Pen pen = new Pen(signColor, signWidth))
			{
				foreach (List<PointF> totalPoint in totalPoints)
				{
					PointF[] array = totalPoint.ToArray();
					if (array.Length > 1)
					{
						g.DrawCurve(pen, array);
					}
				}
				PointF[] array2 = currPoints.ToArray();
				if (array2.Length > 1)
				{
					g.DrawCurve(pen, array2);
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
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.White;
			base.Name = "HslSignature";
			base.Size = new System.Drawing.Size(487, 177);
			base.Load += new System.EventHandler(HslSignature_Load);
			base.ResumeLayout(false);
		}
	}
}
