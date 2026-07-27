using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("加工中心的控件，支持设置门的开关状态，设置信号灯的颜色")]
	public class HslCncCenter : UserControl
	{
		private float setCncDoorState = 0f;

		private float cncDoorState = 0f;

		private Timer timerUpdate = null;

		private bool lightRed = false;

		private bool lightYellow = false;

		private bool lightGreen = false;

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
		[Description("获取或设置cnc门开合的情况，0为完全打开，100为完全闭合")]
		[Category("HslControls")]
		[DefaultValue(0f)]
		public virtual float CncDoorState
		{
			get
			{
				return cncDoorState;
			}
			set
			{
				cncDoorState = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("是否显示红灯的信息")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool LightRed
		{
			get
			{
				return lightRed;
			}
			set
			{
				lightRed = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("是否显示黄灯的信息")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool LightYellow
		{
			get
			{
				return lightYellow;
			}
			set
			{
				lightYellow = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("是否显示绿灯的信息")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public virtual bool LightGreen
		{
			get
			{
				return lightGreen;
			}
			set
			{
				lightGreen = value;
				Invalidate();
			}
		}

		public HslCncCenter()
		{
			InitializeComponent();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		public void SetDoorState(float doorState)
		{
			if (timerUpdate != null)
			{
				timerUpdate.Enabled = false;
				timerUpdate.Dispose();
			}
			timerUpdate = new Timer();
			timerUpdate.Tick += TimerUpdate_Tick;
			timerUpdate.Interval = 50;
			timerUpdate.Start();
			setCncDoorState = doorState;
		}

		private void TimerUpdate_Tick(object sender, EventArgs e)
		{
			if (setCncDoorState > cncDoorState)
			{
				float num = ((setCncDoorState - cncDoorState > 10f) ? 10f : (setCncDoorState - cncDoorState));
				CncDoorState += num;
			}
			else if (setCncDoorState < cncDoorState)
			{
				float num2 = ((cncDoorState - setCncDoorState > 10f) ? 10f : (cncDoorState - setCncDoorState));
				CncDoorState -= num2;
			}
			else
			{
				timerUpdate.Enabled = false;
				timerUpdate.Dispose();
				timerUpdate = null;
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
			if(false)
			{
				PaintMain(g, base.Width, base.Height);
			}
		}

		private PointF[] GetPointsFrom(string points, float width, float height, float dx = 0f, float dy = 0f)
		{
			return HslHelper.GetPointsFrom(points, 999f, 772f, width, height, dx, dy);
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			PointF[] pointsFrom = GetPointsFrom("118,48 118,39 386,39 386,48 118,48", width, height);
			using (Pen pen = new Pen(Color.Black, (width / 300f > 1f) ? (width / 300f) : 1f))
			{
				pointsFrom = GetPointsFrom("52,322 52,250 32,250 32,60 78,60 78,250 58,250 58,322", width, height);
				using (Brush brush = new SolidBrush(Color.FromArgb(76, 76, 76)))
				{
					g.FillPolygon(brush, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				using (Brush brush2 = new SolidBrush(Color.FromArgb(LightRed ? 255 : 32, 255, 0, 0)))
				{
					g.FillPolygon(brush2, GetPointsFrom("35,63 75,63 75,124 35,124 35,63", width, height));
				}
				using (Brush brush3 = new SolidBrush(Color.FromArgb(LightYellow ? 255 : 32, 255, 255, 0)))
				{
					g.FillPolygon(brush3, GetPointsFrom("35,124 75,124 75,185 35,185", width, height));
				}
				using (Brush brush4 = new SolidBrush(Color.FromArgb(LightGreen ? 255 : 32, 0, 255, 0)))
				{
					g.FillPolygon(brush4, GetPointsFrom("35,185 75,185 75,247 35,247", width, height));
				}
				pointsFrom = GetPointsFrom("118,48 118,39 386,39 386,48 118,48", width, height);
				using (Brush brush5 = new SolidBrush(Color.FromArgb(76, 76, 76)))
				{
					g.FillPolygon(brush5, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				pointsFrom = GetPointsFrom("92,488 39,444 39,322 92,322 92,488", width, height);
				using (Brush brush6 = new SolidBrush(Color.FromArgb(178, 178, 178)))
				{
					g.FillPolygon(brush6, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				using (Brush brush7 = new SolidBrush(Color.FromArgb(191, 191, 191)))
				{
					g.FillPolygon(brush7, GetPointsFrom("465,305 465,48 648,48 648,305 465,305", width, height));
				}
				using (Brush brush8 = new SolidBrush(Color.FromArgb(178, 178, 178)))
				{
					g.FillPolygon(brush8, GetPointsFrom("680,391 648,305 648,48 691,48 691,391 680,391", width, height));
				}
				using (Brush brush9 = new SolidBrush(Color.FromArgb(166, 166, 166)))
				{
					g.FillPolygon(brush9, GetPointsFrom("424,391 465,305 465,48 424,48 424,391", width, height));
				}
				using (Brush brush10 = new SolidBrush(Color.FromArgb(204, 204, 204)))
				{
					g.FillPolygon(brush10, GetPointsFrom("80,391 937,391 937,733 80,733 80,391", width, height));
				}
				using (Brush brush11 = new SolidBrush(Color.FromArgb(199, 199, 199)))
				{
					g.FillPolygon(brush11, GetPointsFrom("80,635 937,491 937,733 80,733 80,635", width, height));
				}
				using (Brush brush12 = new SolidBrush(Color.FromArgb(194, 194, 194)))
				{
					g.FillPolygon(brush12, GetPointsFrom("80,648 937,504 937,733 80,733 80,648", width, height));
				}
				using (Brush brush13 = new SolidBrush(Color.FromArgb(189, 189, 189)))
				{
					g.FillPolygon(brush13, GetPointsFrom("80,671 937,527 937,733 80,733 80,671", width, height));
				}
				using (Brush brush14 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush14, GetPointsFrom("92,391 92,231 176,231 176,391 92,391", width, height));
				}
				using (Brush brush15 = new SolidBrush(Color.FromArgb(242, 242, 242)))
				{
					g.FillPolygon(brush15, GetPointsFrom("92,231 103,48 188,48 176,231 92,231", width, height));
				}
				g.DrawLines(pen, GetPointsFrom("92,391 92,231 103,48 188,48 176,231 176,391 92,391", width, height));
				using (Brush brush16 = new SolidBrush(Color.FromArgb(242, 242, 242)))
				{
					g.FillPolygon(brush16, GetPointsFrom("680,391 680,348 937,348 937,391 680,391", width, height));
				}
				using (Brush brush17 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush17, GetPointsFrom("691,348 691,305 937,305 937,348 691,348", width, height));
				}
				using (Brush brush18 = new SolidBrush(Color.FromArgb(217, 217, 217)))
				{
					g.FillPolygon(brush18, GetPointsFrom("691,284 691,305 937,305 937,284 691,284", width, height));
				}
				using (Brush brush19 = new SolidBrush(Color.FromArgb(204, 204, 204)))
				{
					g.FillPolygon(brush19, GetPointsFrom("691,284 691,273 937,273 937,284 691,284", width, height));
				}
				using (Brush brush20 = new SolidBrush(Color.FromArgb(191, 191, 191)))
				{
					g.FillPolygon(brush20, GetPointsFrom("691,273 691,263 937,263 937,273 691,273", width, height));
				}
				g.DrawLines(pen, GetPointsFrom("680,391 680,348 691,348 691,263 937,263 937,391 680,391", width, height));
				using (Pen pen2 = new Pen(Color.FromArgb(127, 127, 127), pen.Width))
				{
					g.DrawLines(pen2, GetPointsFrom("691,348 937,348", width, height));
				}
				pointsFrom = GetPointsFrom("691,263 686,231 948,231 937,263 691,263", width, height);
				using (Brush brush21 = new SolidBrush(Color.FromArgb(153, 153, 153)))
				{
					g.FillPolygon(brush21, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				pointsFrom = GetPointsFrom("691,27 686,231 948,231 937,27 691,27", width, height);
				using (Brush brush22 = new SolidBrush(Color.FromArgb(204, 204, 204)))
				{
					g.FillPolygon(brush22, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				using (Brush brush23 = new SolidBrush(Color.FromArgb(51, 51, 51)))
				{
					g.FillPolygon(brush23, GetPointsFrom("931,39 937,144 788,144 788,39 931,39", width, height));
				}
				using (Brush brush24 = new SolidBrush(Color.FromArgb(127, 0, 0)))
				{
					g.FillPolygon(brush24, GetPointsFrom("744,48 755,48 755,59 744,59 744,48", width, height));
					g.FillPolygon(brush24, GetPointsFrom("744,199 755,199 755,209 744,209 744,199", width, height));
				}
				using (Brush brush25 = new SolidBrush(Color.FromArgb(51, 102, 51)))
				{
					g.FillPolygon(brush25, GetPointsFrom("712,48 724,48 724,59 712,59 712,48", width, height));
					g.FillPolygon(brush25, GetPointsFrom("712,199 724,199 724,209 712,209 712,199", width, height));
				}
				using (Pen pen3 = new Pen(Color.FromArgb(255, 255, 255), pen.Width))
				{
					g.DrawLines(pen3, GetPointsFrom("701,220 701,144 765,144", width, height));
					g.DrawLines(pen3, GetPointsFrom("701,135 701,39 765,39", width, height));
				}
				using (Pen pen4 = new Pen(Color.FromArgb(153, 153, 153), pen.Width))
				{
					g.DrawLines(pen4, GetPointsFrom("701,220 765,220 765,144", width, height));
					g.DrawLines(pen4, GetPointsFrom("701,135 765,135 765,39", width, height));
				}
				using (Brush brush26 = new SolidBrush(Color.FromArgb(178, 178, 178)))
				{
					g.FillPolygon(brush26, GetPointsFrom("933,156 937,220 788,220 788,156 933,156", width, height));
				}
				using (Brush brush27 = new SolidBrush(Color.FromArgb(153, 153, 153)))
				{
					g.FillPolygon(brush27, GetPointsFrom("935,156 937,188 861,188 861,156 935,156", width, height));
					g.FillPolygon(brush27, GetPointsFrom("859,188 861,220 788,220 788,188 859,188", width, height));
				}
				using (Brush brush28 = new SolidBrush(Color.FromArgb(127, 0, 0)))
				{
					g.FillPolygon(brush28, GetPointsFrom("729,112 724,107 724,97 729,92 738,92 744,97 744,107 738,112 729,112", width, height));
				}
				using (Pen pen5 = new Pen(Color.FromArgb(178, 178, 178), pen.Width))
				{
					g.DrawLines(pen5, GetPointsFrom("725,123 712,114 712,101 725,92 742,92 754,101 754,114 742,123 725,123", width, height));
				}
				g.FillPolygon(Brushes.Black, GetPointsFrom("92,744 92,733 925,733 925,744 92,744", width, height));
				using (Pen pen6 = new Pen(Color.Black, pen.Width))
				{
					g.DrawLines(pen6, GetPointsFrom("80,391 937,391 937,733 80,733 80,391", width, height));
				}
				using (Brush brush29 = new SolidBrush(Color.FromArgb(153, 153, 153)))
				{
					g.FillPolygon(brush29, GetPointsFrom("424,391 465,305 465,48 424,284 424,391", width, height));
				}
				pointsFrom = GetPointsFrom("424,48 424,39 691,39 691,48 424,48", width, height);
				using (Brush brush30 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush30, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				using (Brush brush31 = new SolidBrush(Color.FromArgb(76, 76, 76)))
				{
					g.FillPolygon(brush31, GetPointsFrom("257,733 257,712 365,712 365,733 257,733", width, height));
				}
				pointsFrom = GetPointsFrom("252,733 257,707 305,701 316,701 365,707 369,733 354,712 269,712 252,733", width, height);
				using (Brush brush32 = new SolidBrush(Color.FromArgb(229, 76, 0)))
				{
					g.FillPolygon(brush32, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				pointsFrom = GetPointsFrom("252,744 252,733 307,733 307,701 316,701 316,733 369,733 369,744 252,744", width, height);
				using (Brush brush33 = new SolidBrush(Color.FromArgb(255, 102, 0)))
				{
					g.FillPolygon(brush33, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				g.FillPolygon(Brushes.Black, GetPointsFrom("237,535 231,529 237,525 240,529 237,535", width, height));
				g.FillPolygon(Brushes.Black, GetPointsFrom("916,380 910,374 916,369 922,374 916,380", width, height));
				g.DrawLines(pen, GetPointsFrom("237,529 237,712 252,721", width, height));
				pointsFrom = GetPointsFrom("27,444 39,444 39,322 27,322 27,444", width, height);
				using (Brush brush34 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush34, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				pointsFrom = GetPointsFrom("424,391 465,305 648,305 680,391 424,391", width, height);
				using (Brush brush35 = new SolidBrush(Color.FromArgb(153, 153, 153)))
				{
					g.FillPolygon(brush35, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				using (Pen pen7 = new Pen(Color.FromArgb(102, 102, 102), pen.Width))
				{
					g.DrawLines(pen7, GetPointsFrom("477,391 499,305", width, height));
					g.DrawLines(pen7, GetPointsFrom("637,391 616,305", width, height));
				}
				using (Pen pen8 = new Pen(Color.FromArgb(127, 127, 127), pen.Width))
				{
					g.DrawLines(pen8, GetPointsFrom("648,305 648,48", width, height));
				}
				using (Brush brush36 = new SolidBrush(Color.FromArgb(127, 127, 127)))
				{
					g.FillPolygon(brush36, GetPointsFrom("439,301 439,188 488,188 488,301 439,301", width, height));
				}
				using (Brush brush37 = new SolidBrush(Color.FromArgb(145, 145, 145)))
				{
					g.FillPolygon(brush37, GetPointsFrom("441,291 441,195 488,195 488,291 441,291", width, height));
				}
				using (Brush brush38 = new SolidBrush(Color.FromArgb(161, 161, 161)))
				{
					g.FillPolygon(brush38, GetPointsFrom("441,284 441,203 488,203 488,284 441,284", width, height));
				}
				using (Brush brush39 = new SolidBrush(Color.FromArgb(178, 178, 178)))
				{
					g.FillPolygon(brush39, GetPointsFrom("441,277 441,210 488,210 488,277 441,277", width, height));
				}
				using (Brush brush40 = new SolidBrush(Color.FromArgb(196, 196, 196)))
				{
					g.FillPolygon(brush40, GetPointsFrom("441,267 441,220 488,220 488,267 441,267", width, height));
				}
				using (Brush brush41 = new SolidBrush(Color.FromArgb(212, 212, 212)))
				{
					g.FillPolygon(brush41, GetPointsFrom("441,260 441,227 488,227 488,260 441,260", width, height));
				}
				using (Brush brush42 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush42, GetPointsFrom("439,252 439,235 488,235 488,252 439,252", width, height));
				}
				using (Pen pen9 = new Pen(Color.FromArgb(76, 76, 76), pen.Width))
				{
					g.DrawLines(pen9, GetPointsFrom("439,301 439,188 488,188 488,301 439,301", width, height));
				}
				pointsFrom = GetPointsFrom("637,337 637,231 674,231 674,337 637,337", width, height);
				using (Brush brush43 = new SolidBrush(Color.FromArgb(76, 76, 76)))
				{
					g.FillPolygon(brush43, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				pointsFrom = GetPointsFrom("637,193 637,231 674,231 674,193 637,193", width, height);
				using (Brush brush44 = new SolidBrush(Color.FromArgb(127, 127, 127)))
				{
					g.FillPolygon(brush44, pointsFrom);
					g.DrawLines(pen, pointsFrom);
				}
				g.DrawLines(pen, GetPointsFrom("916,374 918,401 899,414 922,422 901,437 923,444 902,457 925,465 906,478 927,486 908,499 931,508 910,521 933,529 914,542 937,550 959,542 939,529 961,521 942,508 963,499 942,488 965,478 946,465 967,457 948,444 969,435 950,424 971,414 952,401 954,343 942,240", width, height));
				using (Pen pen10 = new Pen(Color.FromArgb(127, 127, 127), pen.Width))
				{
					g.DrawLines(pen10, GetPointsFrom("916,386 905,374 916,365 925,374 916,386", width, height));
					g.DrawLines(pen10, GetPointsFrom("237,541 226,529 237,520 246,529 237,541", width, height));
				}
				using (Brush brush45 = new SolidBrush(Color.FromArgb(204, 204, 204)))
				{
					g.FillPolygon(brush45, GetPointsFrom("176,231 188,39 435,39 424,231 176,231", width, height));
				}
				using (Brush brush46 = new SolidBrush(Color.FromArgb(191, 191, 191)))
				{
					g.FillPolygon(brush46, GetPointsFrom("176,391 176,231 424,231 424,391 176,391", width, height));
				}
				g.DrawLines(pen, GetPointsFrom("176,391 176,231 188,39 435,39 424,231 424,391 176,391", width, height));
				using (Brush brush47 = new SolidBrush(Color.FromArgb(191, 191, 191)))
				{
					g.FillPolygon(brush47, GetPointsFrom("176,391 176,231 424,231 424,391 176,391", width, height, cncDoorState * 2.5f));
				}
				using (Brush brush48 = new SolidBrush(Color.FromArgb(204, 204, 204)))
				{
					g.FillPolygon(brush48, GetPointsFrom("176,231 188,39 435,39 424,231 176,231", width, height, cncDoorState * 2.5f));
				}
				using (Brush brush49 = new SolidBrush(Color.FromArgb(229, 229, 229)))
				{
					g.FillPolygon(brush49, GetPointsFrom("291,199 299,71 397,71 391,199 291,199", width, height, cncDoorState * 2.5f));
				}
				using (Brush brush50 = new SolidBrush(Color.FromArgb(153, 153, 153)))
				{
					g.FillPolygon(brush50, GetPointsFrom("297,86 299,71 397,71 397,86 297,86", width, height, cncDoorState * 2.5f));
					g.FillPolygon(brush50, GetPointsFrom("291,199 299,71 310,71 303,199 291,199", width, height, cncDoorState * 2.5f));
				}
				g.DrawLines(pen, GetPointsFrom("291,199 299,71 397,71", width, height, cncDoorState * 2.5f));
				using (Pen pen11 = new Pen(Color.FromArgb(255, 255, 255), pen.Width))
				{
					g.DrawLines(pen11, GetPointsFrom("399,71 391,199 291,199", width, height, cncDoorState * 2.5f));
				}
				g.DrawLines(pen, GetPointsFrom("176,391 176,231 188,39 435,39 424,231 424,391 176,391", width, height, cncDoorState * 2.5f));
				using (Brush brush51 = new SolidBrush(Color.FromArgb(255, 102, 0)))
				{
					g.FillPolygon(brush51, GetPointsFrom("407,365 403,360 403,252 407,246 403,246 397,252 397,360 403,365 407,365", width, height, cncDoorState * 2.5f));
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
			base.Name = "HslCncCenter";
			base.Size = new System.Drawing.Size(352, 215);
			base.ResumeLayout(false);
		}
	}
}
