using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HslControls.Charts.Series
{
	public class LineSeries : SeriesBase
	{
		private float _thickness = 1.8f;

		private float _curveTension = 0f;

		private bool _fillGraph = true;

		private bool _pointVisible = false;

		private bool _valueVisible = false;

		private Font _font = new Font("微软雅黑", 10f);

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

		[Category("ZChart")]
		[Description("曲线张力。")]
		[DefaultValue(0f)]
		public float CurveTension
		{
			get
			{
				return _curveTension;
			}
			set
			{
				_curveTension = value;
			}
		}

		[Category("ZChart")]
		[Description("是否填充图形。")]
		[DefaultValue(true)]
		public bool FillGraph
		{
			get
			{
				return _fillGraph;
			}
			set
			{
				_fillGraph = value;
			}
		}

		[Category("ZChart")]
		[Description("显示/隐藏点状图形。")]
		[DefaultValue(false)]
		public bool PointVisible
		{
			get
			{
				return _pointVisible;
			}
			set
			{
				_pointVisible = value;
			}
		}

		[Category("ZChart")]
		[Description("显示/隐藏当前值。")]
		[DefaultValue(false)]
		public bool ValueVisible
		{
			get
			{
				return _valueVisible;
			}
			set
			{
				_valueVisible = value;
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
			PointF[] locationArray = GetLocationArray();
			if (locationArray != null && locationArray.Length > 1)
			{
				using (Pen pen = new Pen(base.Color, Thickness))
				{
					if (CurveTension > 0f)
					{
						g.DrawCurve(pen, locationArray, CurveTension);
					}
					else
					{
						g.DrawLines(pen, locationArray);
					}
				}
				if (FillGraph && base.ParentAxisY != null)
				{
					int num = (int)Math.Round(CountTop(base.ParentAxisY.IsReverse ? base.ParentAxisY.MaxValue : base.ParentAxisY.MinValue));
					PointF[] array = new PointF[locationArray.Length + 2];
					locationArray.CopyTo(array, 0);
					array[array.Length - 1] = new PointF(locationArray[0].X, num);
					array[array.Length - 2] = new PointF(locationArray[locationArray.Length - 1].X, num);
					using (SolidBrush brush = new SolidBrush(Color.FromArgb(50, base.Color)))
					{
						g.FillPolygon(brush, array);
					}
				}
			}
			if (base.Points.Count <= 0 || (!_pointVisible && !_valueVisible))
			{
				return;
			}
			using (SolidBrush brush2 = new SolidBrush(base.Color))
			{
				if (_pointVisible)
				{
					float num2 = 4f * _thickness;
					foreach (ChartPoint point in base.Points)
					{
						g.FillEllipse(brush2, (int)Math.Round((float)point.Left - 2f * _thickness), (int)Math.Round((float)point.Top - 2f * _thickness), num2, num2);
					}
				}
				if (_valueVisible)
				{
					ChartPoint chartPoint = base.Points.Last();
					string text = chartPoint.Y.ToString();
					if (!string.IsNullOrEmpty(text))
					{
						g.DrawString(text, _font, brush2, new Point(chartPoint.Left, chartPoint.Top), SeriesFormat.LabelFormat);
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
