using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class StandardLineSeries : SeriesBase
	{
		private float _thickness = 1f;

		private bool _horizontatLineVisible = true;

		private bool _verticalLineVisible = true;

		[Category("ZChart")]
		[Description("图形宽度。")]
		[DefaultValue(1f)]
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

		[Category("ZChart")]
		[Description("显示/隐藏横向基准线。")]
		[DefaultValue(true)]
		public bool HorizontatLineVisible
		{
			get
			{
				return _horizontatLineVisible;
			}
			set
			{
				_horizontatLineVisible = value;
			}
		}

		[Category("ZChart")]
		[Description("显示/隐藏纵向基准线。")]
		[DefaultValue(true)]
		public bool VerticalLineVisible
		{
			get
			{
				return _verticalLineVisible;
			}
			set
			{
				_verticalLineVisible = value;
			}
		}

		internal override void Draw(Graphics g)
		{
			PointF[] locationArray = GetLocationArray();
			if (locationArray == null || locationArray.Length == 0)
			{
				return;
			}
			using (Pen pen = new Pen(base.Color, Thickness))
			{
				if (_horizontatLineVisible && base.ParentAxisX != null)
				{
					float x = CountLeft(base.ParentAxisX.MinValue);
					float x2 = CountLeft(base.ParentAxisX.MaxValue);
					PointF[] array = locationArray;
					for (int i = 0; i < array.Length; i++)
					{
						PointF pointF = array[i];
						g.DrawLine(pen, x, pointF.Y, x2, pointF.Y);
					}
				}
				if (_verticalLineVisible && base.ParentAxisY != null)
				{
					float y = CountTop(base.ParentAxisY.MinValue);
					float y2 = CountTop(base.ParentAxisY.MaxValue);
					PointF[] array2 = locationArray;
					for (int j = 0; j < array2.Length; j++)
					{
						PointF pointF2 = array2[j];
						g.DrawLine(pen, pointF2.X, y, pointF2.X, y2);
					}
				}
			}
		}

		protected override void PaintLegend(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			float num = ((float)base.Legend.Height - _thickness) / 2f;
			using (Pen pen = new Pen(base.Color, _thickness))
			{
				e.Graphics.DrawLine(pen, 0f, num, 12f, num);
			}
		}
	}
}
