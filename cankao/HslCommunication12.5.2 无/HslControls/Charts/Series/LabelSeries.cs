using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class LabelSeries : SeriesBase
	{
		private int _width = 12;

		private int _height = 12;

		private Font _font = new Font("微软雅黑", 10f);

		[Category("ZChart")]
		[Description("图形宽度。")]
		[DefaultValue(12)]
		public int Width
		{
			get
			{
				return _width;
			}
			set
			{
				_width = value;
			}
		}

		[Category("ZChart")]
		[Description("图形高度。")]
		[DefaultValue(12)]
		public int Height
		{
			get
			{
				return _height;
			}
			set
			{
				_height = value;
			}
		}

		[Category("ZChart")]
		[Description("字体。")]
		public Font Font
		{
			get
			{
				return _font;
			}
			set
			{
				_font = value;
			}
		}

		internal override void Draw(Graphics g)
		{
			if (base.Points.Count <= 0)
			{
				return;
			}
			using (SolidBrush brush = new SolidBrush(base.Color))
			{
				foreach (ChartPoint point2 in base.Points)
				{
					int left = point2.Left;
					int top = point2.Top;
					g.FillPolygon(brush, new Point[3]
					{
						new Point(left, top),
						new Point(left - _width / 2, top - _height),
						new Point(left + _width / 2, top - _height)
					});
					if (!string.IsNullOrEmpty(point2.Label))
					{
						g.DrawString(point: new Point(left, top - _height), s: point2.Label, font: _font, brush: brush, format: SeriesFormat.LabelFormat);
					}
				}
			}
		}

		protected override void PaintLegend(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			float num = ((float)base.Legend.Height - 12f) / 2f;
			PointF[] points = new PointF[3]
			{
				new PointF(0f, num),
				new PointF(12f, num),
				new PointF(6f, num + 12f)
			};
			using (SolidBrush brush = new SolidBrush(base.Color))
			{
				e.Graphics.FillPolygon(brush, points);
			}
		}
	}
}
