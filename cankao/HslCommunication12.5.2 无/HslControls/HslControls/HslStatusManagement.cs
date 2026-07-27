using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个状态集显示控件，例如用来显示10×20仓库库位信息，还可以设置每个单元的提示消息")]
	public class HslStatusManagement : UserControl
	{
		public delegate void StatusManagementClickDelegate(HslStatusManagement hslStatus, int row, int col);

		private StringFormat sf = null;

		private int row = 2;

		private int col = 10;

		private float offect = 4f;

		private Color[,] arrayColor;

		private string[,] descriptions;

		private string[,] marks;

		private Color borderColor = Color.DimGray;

		private Pen borderPen = new Pen(Color.DimGray);

		private bool isRenderBorder = true;

		private HslRenderStyle renderStyle = HslRenderStyle.Rectangle;

		private Color defaultColor = Color.LightGray;

		private Point activePoint = new Point(-1, -1);

		private bool render = true;

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
		[Description("获取或设置状态间距信息")]
		[Category("HslControls")]
		[DefaultValue(4f)]
		public float SpacingOffect
		{
			get
			{
				return offect;
			}
			set
			{
				offect = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置状态集的行数信息")]
		[Category("HslControls")]
		[DefaultValue(2)]
		public int RowCount
		{
			get
			{
				return row;
			}
			set
			{
				row = value;
				ArrayInni();
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置状态集的列数信息")]
		[Category("HslControls")]
		[DefaultValue(10)]
		public int ColCount
		{
			get
			{
				return col;
			}
			set
			{
				col = value;
				ArrayInni();
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置是否显示边框信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool IsRenderBorder
		{
			get
			{
				return isRenderBorder;
			}
			set
			{
				isRenderBorder = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置状态集的显示样式，支持矩形，椭圆，菱形")]
		[Category("HslControls")]
		[DefaultValue(HslRenderStyle.Rectangle)]
		public HslRenderStyle RenderStyle
		{
			get
			{
				return renderStyle;
			}
			set
			{
				renderStyle = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置所有状态集合体的默认背景色，设置本数据将会强制所有的颜色恢复初始化")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color GeneralColor
		{
			get
			{
				return defaultColor;
			}
			set
			{
				defaultColor = value;
				ArrayInni();
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置所有状态集合体的边框颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				borderPen.Dispose();
				borderPen = new Pen(borderColor);
				Invalidate();
			}
		}

		public event StatusManagementClickDelegate OnStatusClick;

		public HslStatusManagement()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			ArrayInni();
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (base.Width >= 10)
			{
				int num = e.Location.X * col / base.Width;
				int num2 = e.Location.Y * row / base.Height;
				if (num >= 0 && num2 >= 0)
				{
					this.OnStatusClick?.Invoke(this, num2, num);
				}
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				PaintMain(g, width, height);
			}
		}

		private void PaintMain(Graphics g, float width, float height)
		{
			float num = (width - (float)col * offect) / (float)col;
			float num2 = (height - (float)row * offect) / (float)row;
			if (renderStyle == HslRenderStyle.Rectangle)
			{
				g.SmoothingMode = SmoothingMode.None;
			}
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					Rectangle rectangle = new Rectangle((int)(offect / 2f + (num + offect) * (float)j), (int)(offect / 2f + (num2 + offect) * (float)i), (int)num, (int)num2);
					using (Brush brush = new SolidBrush(arrayColor[i, j]))
					{
						if (renderStyle == HslRenderStyle.Rectangle)
						{
							g.FillRectangle(brush, rectangle);
							if (isRenderBorder)
							{
								g.DrawRectangle(borderPen, rectangle);
							}
						}
						else if (renderStyle == HslRenderStyle.Ellipse)
						{
							g.FillEllipse(brush, rectangle);
							if (isRenderBorder)
							{
								g.DrawEllipse(borderPen, rectangle);
							}
						}
						else
						{
							g.FillPolygon(brush, HslHelper.GetRhombusFromRectangle(rectangle));
							if (isRenderBorder)
							{
								g.DrawPolygon(borderPen, HslHelper.GetRhombusFromRectangle(rectangle));
							}
						}
					}
					if (!string.IsNullOrEmpty(marks[i, j]))
					{
						rectangle.Offset(1, 1);
						using (Brush brush2 = new SolidBrush(ForeColor))
						{
							g.DrawString(marks[i, j], Font, brush2, rectangle, HslHelper.StringFormatCenter);
						}
					}
				}
			}
		}

		public void StopRendering()
		{
			render = false;
		}

		public void ContinueRendering()
		{
			render = true;
			Invalidate();
		}

		public void SetColorByLocation(int rowIndex, int colIndex, Color color)
		{
			arrayColor[rowIndex, colIndex] = color;
			if (render)
			{
				Invalidate();
			}
		}

		public void ResetColorByLocation(int rowIndex, int colIndex)
		{
			arrayColor[rowIndex, colIndex] = defaultColor;
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorByLocation(Point point, Color color)
		{
			SetColorByLocation(point.X, point.Y, color);
		}

		public void ResetColorByLocation(Point point)
		{
			ResetColorByLocation(point.X, point.Y);
		}

		public void SetColorByLocation(Point[] points, Color color)
		{
			if (points != null)
			{
				for (int i = 0; i < points.Length; i++)
				{
					arrayColor[points[i].X, points[i].Y] = color;
				}
				if (render)
				{
					Invalidate();
				}
			}
		}

		public void SetColorByLocation(Point[] points, Color[] color)
		{
			if (points == null || color == null)
			{
				return;
			}
			for (int i = 0; i < points.Length; i++)
			{
				if (i < color.Length)
				{
					arrayColor[points[i].X, points[i].Y] = color[i];
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void ResetColorByLocation(Point[] points)
		{
			if (points != null)
			{
				for (int i = 0; i < points.Length; i++)
				{
					arrayColor[points[i].X, points[i].Y] = defaultColor;
				}
				if (render)
				{
					Invalidate();
				}
			}
		}

		public void SetColorByRow(int rowIndex, Color color)
		{
			for (int i = 0; i < col; i++)
			{
				arrayColor[rowIndex, i] = color;
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorByRow(int rowIndex, Color[] colors)
		{
			for (int i = 0; i < col; i++)
			{
				if (i < colors.Length)
				{
					arrayColor[rowIndex, i] = colors[i];
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void ResetColorByRow(int rowIndex)
		{
			for (int i = 0; i < col; i++)
			{
				arrayColor[rowIndex, i] = defaultColor;
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorByCol(int colIndex, Color color)
		{
			for (int i = 0; i < row; i++)
			{
				arrayColor[i, colIndex] = color;
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorByCol(int colIndex, Color[] colors)
		{
			for (int i = 0; i < row; i++)
			{
				if (i < colors.Length)
				{
					arrayColor[i, colIndex] = colors[i];
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void ResetColorByCol(int colIndex)
		{
			for (int i = 0; i < row; i++)
			{
				arrayColor[i, colIndex] = defaultColor;
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorAll(Color color)
		{
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					arrayColor[i, j] = color;
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void SetColorAll(Color[] colors)
		{
			int num = 0;
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					if (num >= colors.Length)
					{
						break;
					}
					arrayColor[i, j] = colors[num];
					num++;
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public void ResetColorAll()
		{
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					arrayColor[i, j] = defaultColor;
				}
			}
			if (render)
			{
				Invalidate();
			}
		}

		public Color GetColorFromPosition(int rowIndex, int colIndex)
		{
			return arrayColor[rowIndex, colIndex];
		}

		public void SetMark(int rowIndex, int colIndex, string mark)
		{
			marks[rowIndex, colIndex] = mark;
			if (render)
			{
				Invalidate();
			}
		}

		public void SetDescription(int rowIndex, int colIndex, string description)
		{
			descriptions[rowIndex, colIndex] = description;
		}

		public void SetAllDescription(string description)
		{
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					descriptions[i, j] = description;
				}
			}
		}

		public void ClearAllDescription()
		{
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					descriptions[i, j] = string.Empty;
				}
			}
		}

		public void SetAllDescription(string[] descs)
		{
			int num = 0;
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					if (num >= descs.Length)
					{
						break;
					}
					descriptions[i, j] = descs[num];
					num++;
				}
			}
		}

		private void ArrayInni()
		{
			arrayColor = new Color[row, col];
			descriptions = new string[row, col];
			marks = new string[row, col];
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					arrayColor[i, j] = defaultColor;
					descriptions[i, j] = string.Empty;
					marks[i, j] = string.Empty;
				}
			}
		}

		private void HslStatusManagement_Load(object sender, EventArgs e)
		{
			base.MouseMove += HslStatusManagement_MouseMove;
			base.MouseLeave += HslStatusManagement_MouseLeave;
		}

		private void HslStatusManagement_MouseLeave(object sender, EventArgs e)
		{
			activePoint = new Point(-1, -1);
			Refresh();
		}

		private void HslStatusManagement_MouseMove(object sender, MouseEventArgs e)
		{
			activePoint = new Point(e.X, e.Y);
			Refresh();
		}

		private void HslStatusManagement_Paint(object sender, PaintEventArgs e)
		{
			if (activePoint.X < 0 || activePoint.Y < 0 || base.Width < 10 || base.Height < 10)
			{
				return;
			}
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			float num = (float)base.Width * 1f / (float)col;
			float num2 = (float)base.Height * 1f / (float)row;
			int num3 = (int)((float)activePoint.Y / num2);
			int num4 = (int)((float)activePoint.X / num);
			if (!((float)activePoint.X < (float)num4 * num + offect / 2f) && !((float)activePoint.X > (float)(num4 + 1) * num - offect / 2f) && !((float)activePoint.Y < (float)num3 * num2 + offect / 2f) && !((float)activePoint.Y > (float)(num3 + 1) * num2 - offect / 2f) && num3 < row && num4 < col && !string.IsNullOrEmpty(descriptions[num3, num4]))
			{
				float num5 = graphics.MeasureString(descriptions[num3, num4], Font).Width;
				RectangleF rectangleF = new RectangleF(activePoint.X + 10, activePoint.Y - 10, num5 + 20f, Font.Height + 10);
				if ((float)activePoint.X > (float)base.Width - num5 - 20f)
				{
					rectangleF.X = (float)activePoint.X - num5 - 20f;
				}
				if (activePoint.Y > base.Height - Font.Height - 10)
				{
					activePoint.Y = activePoint.Y - Font.Height - 10;
				}
				graphics.FillRectangle(Brushes.DimGray, rectangleF);
				graphics.DrawString(descriptions[num3, num4], Font, Brushes.Yellow, rectangleF, sf);
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
			base.Name = "HslStatusManagement";
			base.Size = new System.Drawing.Size(375, 124);
			base.Load += new System.EventHandler(HslStatusManagement_Load);
			base.Paint += new System.Windows.Forms.PaintEventHandler(HslStatusManagement_Paint);
			base.ResumeLayout(false);
		}
	}
}
