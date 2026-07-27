using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class ColumnSeries : SeriesBase
	{
		private int _width = 24;

		[Category("ZChart")]
		[Description("图形宽度。")]
		[DefaultValue(24)]
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

		protected override void PaintLegend(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			using (SolidBrush brush = new SolidBrush(base.Color))
			{
				e.Graphics.FillRectangle(brush, 2f, ((float)base.Legend.Height - 16f) / 2f, 12f, 16f);
			}
		}

		internal static void Draw(Graphics g, Rectangle border, int sum, ColumnSeries[] series)
		{
			int num = series.Count();
			float num2 = 0f;
			for (int i = 0; i < num; i++)
			{
				num2 += (float)series[i].Width;
			}
			if (!(num2 > 0f))
			{
				return;
			}
			num2 += (float)(num - 1) * 3f;
			float num3 = (float)border.Width / (float)sum;
			float num4 = (float)border.Left + num3 - num2 / 2f;
			for (int j = 0; j < num; j++)
			{
				if (!series[j].Visible)
				{
					continue;
				}
				PointF[] locationArray = series[j].GetLocationArray();
				if (locationArray != null && locationArray.Length != 0)
				{
					using (SolidBrush brush = new SolidBrush(series[j].Color))
					{
						for (int k = 0; k < locationArray.Length; k++)
						{
							g.FillRectangle(brush, new RectangleF(num4 + num3 * (float)k, locationArray[k].Y, series[j].Width, (float)border.Bottom - locationArray[k].Y - 1f));
						}
					}
				}
				num4 += (float)series[j].Width + 3f;
			}
		}
	}
}
