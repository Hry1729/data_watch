using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class BezierSeries : SeriesBase
	{
		private float _thickness = 1.8f;

		[Category("ZChart")]
		[Description("图形宽度。")]
		[DefaultValue(1.8f)]
		public float Thickness
		{
			get
			{
				return _thickness;
			}
			set
			{
				_thickness = value;
			}
		}

		internal override void Draw(Graphics g)
		{
			PointF[] locationArray = GetLocationArray();
			if (locationArray != null && locationArray.Length > 3)
			{
				int num = locationArray.Length % 3;
				PointF[] points = locationArray.Take(3 * (locationArray.Length / 3) + ((num != 0) ? 1 : (-2))).ToArray();
				using (Pen pen = new Pen(base.Color, Thickness))
				{
					g.DrawBeziers(pen, points);
				}
			}
		}

		protected override void PaintLegend(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			PointF[] points = new PointF[4]
			{
				new PointF(0f, (float)base.Legend.Height / 2f),
				new PointF(3f, 0f),
				new PointF(9f, base.Legend.Height),
				new PointF(12f, (float)base.Legend.Height / 2f)
			};
			using (Pen pen = new Pen(base.Color, _thickness))
			{
				e.Graphics.DrawBeziers(pen, points);
			}
		}
	}
}
