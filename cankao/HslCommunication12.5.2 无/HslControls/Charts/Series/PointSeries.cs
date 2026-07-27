using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class PointSeries : SeriesBase
	{
		private int _width = 8;

		private int _height = 8;

		[Category("ZChart")]
		[Description("图形宽度。")]
		[DefaultValue(8)]
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
		[DefaultValue(8)]
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

		internal override void Draw(Graphics g)
		{
			if (base.Points.Count <= 0)
			{
				return;
			}
			using (SolidBrush brush = new SolidBrush(base.Color))
			{
				foreach (ChartPoint point in base.Points)
				{
					g.FillEllipse(brush, (float)point.Left - (float)_width / 2f, (float)point.Top - (float)_height / 2f, _width, _height);
				}
			}
		}

		protected override void PaintLegend(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			using (SolidBrush brush = new SolidBrush(base.Color))
			{
				e.Graphics.FillEllipse(brush, 2f, (float)(base.Legend.Height - 8) / 2f, 8f, 8f);
			}
		}
	}
}
