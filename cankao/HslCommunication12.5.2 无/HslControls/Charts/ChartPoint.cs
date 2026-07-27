using System;
using System.ComponentModel;
using System.Drawing;

namespace HslControls.Charts
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class ChartPoint
	{
		private HslChart _chart;

		private SeriesBase _series;

		private double _x;

		private double _y;

		private string _label;

		[Browsable(false)]
		public HslChart Chart
		{
			get
			{
				return _chart;
			}
			internal set
			{
				_chart = value;
			}
		}

		[Browsable(false)]
		public SeriesBase Series
		{
			get
			{
				return _series;
			}
			internal set
			{
				_series = value;
			}
		}

		[Browsable(false)]
		public Axis ParentAxisX
		{
			get
			{
				if (_series != null)
				{
					return _series.ParentAxisX;
				}
				return null;
			}
		}

		[Browsable(false)]
		public Axis ParentAxisY
		{
			get
			{
				if (_series != null)
				{
					return _series.ParentAxisY;
				}
				return null;
			}
		}

		public double X
		{
			get
			{
				return _x;
			}
			set
			{
				if (_x != value)
				{
					_x = value;
					_chart?.Invalidate();
					OnValueChanged(new EventArgs());
				}
			}
		}

		public double Y
		{
			get
			{
				return _y;
			}
			set
			{
				if (_y != value)
				{
					_y = value;
					_chart?.Invalidate();
					OnValueChanged(new EventArgs());
				}
			}
		}

		public string Label
		{
			get
			{
				return _label;
			}
			set
			{
				if (_label != value)
				{
					_label = value;
					_chart?.Invalidate();
					OnLabelChanged(new EventArgs());
				}
			}
		}

		[Browsable(false)]
		public int Left
		{
			get
			{
				int num = ((_chart != null) ? _chart.OffsetX : 0);
				return (int)(Series.CountLeft(X) + (float)num);
			}
		}

		[Browsable(false)]
		public int Top
		{
			get
			{
				int num = ((_chart != null) ? _chart.OffsetY : 0);
				return (int)(Series.CountTop(Y) + (float)num);
			}
		}

		[Browsable(false)]
		public Point Location => new Point(Left, Top);

		public event EventHandler ValueChanged;

		public event EventHandler LabelChanged;

		public ChartPoint(double x, double y)
		{
			_x = x;
			_y = y;
		}

		public ChartPoint()
			: this(0.0, 0.0)
		{
		}

		protected void OnValueChanged(EventArgs e)
		{
			this.ValueChanged?.Invoke(this, e);
		}

		protected void OnLabelChanged(EventArgs e)
		{
			this.LabelChanged?.Invoke(this, e);
		}

		public string GetLabelFromAxisX()
		{
			if (ParentAxisX != null)
			{
				return ParentAxisX.GetLabel(_x);
			}
			return null;
		}

		public string GetLabelFromAxisY()
		{
			if (ParentAxisY != null)
			{
				return ParentAxisY.GetLabel(_y);
			}
			return null;
		}
	}
}
