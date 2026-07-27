using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HslControls.Charts
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public abstract class SeriesBase
	{
		private HslChart _chart;

		private string _title = "Series";

		private ChartPointCollection _points = new ChartPointCollection();

		private int _scalesXAt = 0;

		private int _scalesYAt = 0;

		private Color _color = Color.FromArgb(232, 17, 35);

		private bool _visible = true;

		private Label _legend;

		private bool _legendVisible = true;

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

		[Category("HslChart")]
		[Description("标题。")]
		[DefaultValue("Series")]
		public string Title
		{
			get
			{
				return _title;
			}
			set
			{
				_title = value;
				_legend.Text = _title;
			}
		}

		[Category("HslChart")]
		[Description("数据点。")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public ChartPointCollection Points
		{
			get
			{
				return _points;
			}
			private set
			{
				_points = value;
			}
		}

		[Browsable(false)]
		public double? MaxValueX
		{
			get
			{
				if (_points == null || _points.Count < 1)
				{
					return null;
				}
				return _points.Select((ChartPoint cp) => cp.X).Max();
			}
		}

		[Browsable(false)]
		public double? MinValueX
		{
			get
			{
				if (_points == null || _points.Count < 1)
				{
					return null;
				}
				return _points.Select((ChartPoint cp) => cp.X).Min();
			}
		}

		[Browsable(false)]
		public double? MaxValueY
		{
			get
			{
				if (_points == null || _points.Count < 1)
				{
					return null;
				}
				return _points.Select((ChartPoint cp) => cp.Y).Max();
			}
		}

		[Browsable(false)]
		public double? MinValueY
		{
			get
			{
				if (_points == null || _points.Count < 1)
				{
					return null;
				}
				return _points.Select((ChartPoint cp) => cp.Y).Min();
			}
		}

		[Category("HslChart")]
		[Description("附着的X轴序号。")]
		[DefaultValue(0)]
		public int ScalesXAt
		{
			get
			{
				return _scalesXAt;
			}
			set
			{
				if (value < 0)
				{
					_scalesXAt = 0;
				}
				else
				{
					_scalesXAt = value;
				}
			}
		}

		[Browsable(false)]
		public Axis ParentAxisX
		{
			get
			{
				if (_chart != null && _scalesXAt < _chart.AxisX.Count)
				{
					return _chart.AxisX[_scalesXAt];
				}
				return null;
			}
		}

		[Category("HslChart")]
		[Description("附着的Y轴序号。")]
		[DefaultValue(0)]
		public int ScalesYAt
		{
			get
			{
				return _scalesYAt;
			}
			set
			{
				if (value < 0)
				{
					_scalesYAt = 0;
				}
				else
				{
					_scalesYAt = value;
				}
			}
		}

		[Browsable(false)]
		public Axis ParentAxisY
		{
			get
			{
				if (_chart != null && _scalesYAt < _chart.AxisY.Count)
				{
					return _chart.AxisY[_scalesYAt];
				}
				return null;
			}
		}

		[Category("HslChart")]
		[Description("颜色。")]
		public Color Color
		{
			get
			{
				return _color;
			}
			set
			{
				_color = value;
			}
		}

		[Category("HslChart")]
		[Description("显示/隐藏。")]
		[DefaultValue(true)]
		public bool Visible
		{
			get
			{
				return _visible;
			}
			set
			{
				_visible = value;
			}
		}

		[Browsable(false)]
		internal Label Legend
		{
			get
			{
				return _legend;
			}
			set
			{
				_legend = value;
			}
		}

		[Category("HslChart")]
		[Description("显示/隐藏图例。")]
		[DefaultValue(true)]
		public bool LegendVisible
		{
			get
			{
				return _legendVisible;
			}
			set
			{
				_legendVisible = value;
				_legend.Visible = _legendVisible;
			}
		}

		public SeriesBase()
		{
			_color = GetColorByIndex(HslChart.ColorIndex);
			HslChart.ColorIndex++;
			_legend = new Label
			{
				AutoSize = true,
				Padding = new Padding(12, 0, 0, 0),
				Text = Title
			};
			_legend.Paint += PaintLegend;
		}

		public static Color GetColorByIndex(int index)
		{
			return HslChart.Colors[index - HslChart.Colors.Count * (index / HslChart.Colors.Count)];
		}

		public float CountLeft(double xValue)
		{
			double num = _chart.Border.Width;
			double num2 = num / ParentAxisX.Range;
			double num3 = (xValue - ParentAxisX.MinValue) * num2;
			if (ParentAxisX.IsReverse)
			{
				num3 = num - num3;
			}
			return Convert.ToSingle(num3 + (double)_chart.Border.Left);
		}

		public float CountTop(double yValue)
		{
			double num = _chart.Border.Height;
			double num2 = num / ParentAxisY.Range;
			double num3 = (yValue - ParentAxisY.MinValue) * num2;
			if (!ParentAxisY.IsReverse)
			{
				num3 = num - num3;
			}
			return Convert.ToSingle(num3 + (double)_chart.Border.Top);
		}

		public PointF[] GetLocationArray()
		{
			if (_points != null || _points.Count > 0)
			{
				PointF[] array = new PointF[_points.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = new PointF(_points[i].Left, _points[i].Top);
				}
				return array;
			}
			return null;
		}

		public void SetValuesY(double[] values, double startValue = 0.0, double valueStep = 0.1)
		{
			_points.Clear();
			if (values != null && values.Length != 0)
			{
				_chart.BeginUpdate();
				for (int i = 0; i < values.Length; i++)
				{
					_points.Add(new ChartPoint((double)i * valueStep + startValue, values[i]));
				}
				_chart.EndUpdate();
			}
		}

		public void SetValuesX(double[] values, double startValue = 0.0, double valueStep = 0.1)
		{
			_points.Clear();
			if (values != null && values.Length != 0)
			{
				_chart.BeginUpdate();
				for (int i = 0; i < values.Length; i++)
				{
					_points.Add(new ChartPoint(values[i], (double)i * valueStep + startValue));
				}
				_chart.EndUpdate();
			}
		}

		public ChartPoint[] SelectAllByX(int x, float range = 0f)
		{
			return _points.Where((ChartPoint cp) => (float)cp.Left >= (float)x - range && (float)cp.Left <= (float)x + range)?.ToArray();
		}

		public ChartPoint SelectByX(int x, float range = 0f)
		{
			ChartPoint[] array = SelectAllByX(x, range);
			if (array == null)
			{
				return null;
			}
			int num = int.MaxValue;
			ChartPoint result = null;
			ChartPoint[] array2 = array;
			foreach (ChartPoint chartPoint in array2)
			{
				int num2 = Math.Abs(x - chartPoint.Left);
				if (num2 < num)
				{
					result = chartPoint;
					num = num2;
				}
			}
			return result;
		}

		public ChartPoint[] SelectAllByY(int y, float range = 0f)
		{
			return _points.Where((ChartPoint cp) => (float)cp.Top >= (float)y - range && (float)cp.Top <= (float)y + range)?.ToArray();
		}

		public ChartPoint SelectByY(int y, float range = 0f)
		{
			ChartPoint[] array = SelectAllByY(y, range);
			if (array == null)
			{
				return null;
			}
			int num = int.MaxValue;
			ChartPoint result = null;
			ChartPoint[] array2 = array;
			foreach (ChartPoint chartPoint in array2)
			{
				int num2 = Math.Abs(y - chartPoint.Top);
				if (num2 < num)
				{
					result = chartPoint;
					num = num2;
				}
			}
			return result;
		}

		public ChartPoint[] SelectAll(int x, int y, float range = 0f)
		{
			return _points.Where((ChartPoint cp) => (float)cp.Left >= (float)x - range && (float)cp.Left <= (float)x + range && (float)cp.Top >= (float)y - range && (float)cp.Top <= (float)y + range)?.ToArray();
		}

		public ChartPoint Select(int x, int y, float range = 0f)
		{
			ChartPoint[] array = SelectAll(x, y, range);
			if (array == null)
			{
				return null;
			}
			int num = int.MaxValue;
			ChartPoint result = null;
			ChartPoint[] array2 = array;
			foreach (ChartPoint chartPoint in array2)
			{
				int num2 = Math.Abs(x - chartPoint.Left) + Math.Abs(y - chartPoint.Top);
				if (num2 < num)
				{
					result = chartPoint;
					num = num2;
				}
			}
			return result;
		}

		internal virtual void Draw(Graphics g)
		{
		}

		protected virtual void PaintLegend(object sender, PaintEventArgs e)
		{
		}
	}
}
