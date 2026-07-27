using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace HslControls.Charts
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class Axis
	{
		private double minValBuf;

		private double maxValBuf;

		private double dataStep;

		private float locationStep;

		private float maxLabelWidth;

		private float maxLabelHeight;

		private HslChart _chart;

		private AxisType _type;

		private double _maxValueLimit = 1.0;

		private double _minValueLimit = 0.0;

		private List<AxisLabel> _labels = new List<AxisLabel>();

		private bool _isCustomLabels = false;

		private bool _visible = true;

		private bool _labelVisible = true;

		private Color _color = Color.FromArgb(51, 51, 51);

		private Brush _brush;

		private AxisPosition _position = AxisPosition.LeftBottom;

		private string _title;

		private bool _isReverse = false;

		private bool _autoAdapt = true;

		private bool _zoomEnabled = true;

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
		public AxisType Type
		{
			get
			{
				return _type;
			}
			internal set
			{
				_type = value;
			}
		}

		[Category("HslChart")]
		[Description("最大值极限。")]
		[DefaultValue(1.0)]
		public double MaxValueLimit
		{
			get
			{
				return _maxValueLimit;
			}
			set
			{
				_maxValueLimit = value;
				MaxValue = value;
			}
		}

		[Category("HslChart")]
		[Description("最小值极限。")]
		[DefaultValue(0.0)]
		public double MinValueLimit
		{
			get
			{
				return _minValueLimit;
			}
			set
			{
				_minValueLimit = value;
				MinValue = value;
			}
		}

		[Category("HslChart")]
		[Description("标签。")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public List<AxisLabel> Labels
		{
			get
			{
				return _labels;
			}
			private set
			{
				_labels = value;
			}
		}

		[Category("HslChart")]
		[Description("是否使用自定义标签。")]
		[DefaultValue(false)]
		public bool IsCustomLabels
		{
			get
			{
				return _isCustomLabels;
			}
			set
			{
				_isCustomLabels = value;
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

		[Category("HslChart")]
		[Description("显示/隐藏标签，隐藏的标签不占用布局空间。")]
		[DefaultValue(true)]
		public bool LabelVisible
		{
			get
			{
				return _labelVisible;
			}
			set
			{
				_labelVisible = value;
			}
		}

		[Category("HslChart")]
		[Description("颜色。")]
		[DefaultValue(true)]
		public Color Color
		{
			get
			{
				return _color;
			}
			set
			{
				_color = value;
				_brush = new SolidBrush(value);
			}
		}

		[Category("HslChart")]
		[Description("位置。")]
		[DefaultValue(AxisPosition.LeftBottom)]
		public AxisPosition Position
		{
			get
			{
				return _position;
			}
			set
			{
				_position = value;
			}
		}

		[Category("HslChart")]
		[Description("标题。")]
		[DefaultValue(null)]
		public string Title
		{
			get
			{
				return _title;
			}
			set
			{
				_title = value;
			}
		}

		[Category("HslChart")]
		[Description("是否反转方向。")]
		[DefaultValue(false)]
		public bool IsReverse
		{
			get
			{
				return _isReverse;
			}
			set
			{
				_isReverse = value;
			}
		}

		[Category("HslChart")]
		[Description("是否让数据范围自适应。")]
		[DefaultValue(true)]
		public bool AutoAdapt
		{
			get
			{
				return _autoAdapt;
			}
			set
			{
				_autoAdapt = value;
			}
		}

		[Category("HslChart")]
		[Description("缩放使能。")]
		[DefaultValue(true)]
		public bool ZoomEnabled
		{
			get
			{
				return _zoomEnabled;
			}
			set
			{
				_zoomEnabled = value;
			}
		}

		[Browsable(false)]
		public float Width { get; private set; }

		[Browsable(false)]
		public float Height { get; private set; }

		[Browsable(false)]
		public double MaxValue { get; private set; } = 1.0;

		[Browsable(false)]
		public double MinValue { get; private set; } = 0.0;

		[Browsable(false)]
		public double Range
		{
			get
			{
				double num = MaxValue - MinValue;
				if (num <= 0.0)
				{
					return 1.0;
				}
				return num;
			}
		}

		[Browsable(false)]
		public ZoomType ZoomState { get; private set; } = ZoomType.None;

		public Axis()
		{
			_labels = new List<AxisLabel>();
			_brush = new SolidBrush(_color);
			maxLabelWidth = 0f;
			maxLabelHeight = 0f;
		}

		internal void ResetRange()
		{
			MinValue = (minValBuf = _minValueLimit);
			MaxValue = (maxValBuf = _maxValueLimit);
		}

		internal void UpdateRange(SeriesBase series, AxisType axisType, int separatorSum)
		{
			if (_autoAdapt && !_isCustomLabels)
			{
				double? num = ((axisType == AxisType.X) ? series.MinValueX : series.MinValueY);
				double? num2 = ((axisType == AxisType.X) ? series.MaxValueX : series.MaxValueY);
				bool flag = false;
				if (num.HasValue && minValBuf > num.Value)
				{
					minValBuf = num.Value;
					flag = true;
				}
				if (num2.HasValue && maxValBuf < num2.Value)
				{
					maxValBuf = num2.Value;
					flag = true;
				}
				if (flag)
				{
					UpdateRange(minValBuf, maxValBuf, separatorSum);
				}
			}
		}

		internal void UpdateZoom(double times)
		{
			if (_zoomEnabled)
			{
				if (times > 0.0)
				{
					ZoomState = ZoomType.Enlarge;
				}
				else if (times < 0.0)
				{
					ZoomState = ZoomType.Reduce;
				}
				else
				{
					ZoomState = ZoomType.None;
				}
				if (times != 0.0)
				{
					double range = Range;
					MinValue += range * times;
					MaxValue -= range * times;
				}
			}
		}

		internal void UpdateLabel(int separatorSum)
		{
			if (!_isCustomLabels)
			{
				_labels.Clear();
				dataStep = Range / (double)separatorSum;
				for (int i = 0; i <= separatorSum; i++)
				{
					double value = dataStep * (double)i + MinValue;
					_labels.Add(new AxisLabel(value));
				}
			}
		}

		internal void UpdateSize1(Graphics g, Font font)
		{
			if (_type == AxisType.X)
			{
				Height = CountAxisXHeight(g, font);
			}
			else if (_type == AxisType.Y)
			{
				Width = CountAxisYWidth(g, font);
			}
		}

		internal void UpdateSize2(Rectangle border, int separatorSum)
		{
			if (_type == AxisType.X)
			{
				Width = border.Width;
				locationStep = Width / (float)separatorSum;
			}
			else if (_type == AxisType.Y)
			{
				Height = border.Height;
				locationStep = Height / (float)separatorSum;
			}
		}

		internal void UpdateOffset(int separatorSum, int offset)
		{
			if (_isCustomLabels)
			{
				return;
			}
			double num = (double)(int)((float)offset / locationStep) * dataStep;
			if (num != 0.0)
			{
				_labels.Clear();
				for (int i = 0; i <= separatorSum; i++)
				{
					double num2 = dataStep * (double)i + MinValue;
					double value = (_isReverse ? (num2 + num) : (num2 - num));
					_labels.Add(new AxisLabel(value));
				}
			}
		}

		internal void DrawAxisX(Graphics g, Font font, Rectangle border, int offset, ref float top, ref float bottom)
		{
			if (!_visible)
			{
				return;
			}
			StringFormat stringFormat = null;
			StringFormat stringFormat2 = null;
			float x = (float)border.Left + (float)border.Width / 2f;
			float num = 0f;
			float num2 = (_isCustomLabels ? ((float)(border.Left + offset)) : ((float)border.Left + (float)offset % locationStep));
			float num3 = 0f;
			if (_position == AxisPosition.RightTop)
			{
				stringFormat = AxisFormat.TitleFormatTopAxisX;
				stringFormat2 = AxisFormat.LabelFormatTopAxisX;
				num = top;
				num3 = top + Height - 3f;
				top += Height;
			}
			else
			{
				stringFormat = AxisFormat.TitleFormatBottomAxisX;
				stringFormat2 = AxisFormat.LabelFormatBottomAxisX;
				num = (float)border.Bottom + bottom + Height;
				num3 = (float)border.Bottom + bottom + 3f;
				bottom += Height;
			}
			if (_labelVisible)
			{
				int count = _labels.Count;
				if (count > 0)
				{
					RectangleF rectangleF = default(RectangleF);
					for (int i = 0; i < count; i++)
					{
						float num4 = 0f;
						if (_isCustomLabels)
						{
							double num5 = (_isReverse ? _labels[count - i - 1].Value : _labels[i].Value);
							num4 = Convert.ToSingle((double)Width / Range * (num5 - MinValue));
							if (_isReverse)
							{
								num4 = Width - num4;
							}
						}
						else
						{
							num4 = (float)i * locationStep;
						}
						num4 += num2;
						if (num4 >= (float)border.Left && num4 <= (float)border.Right)
						{
							PointF pointF = new PointF(num4, num3);
							if (!rectangleF.Contains(pointF))
							{
								g.DrawString(_isReverse ? _labels[count - i - 1].Content : _labels[i].Content, font, _brush, pointF, stringFormat2);
								rectangleF = new RectangleF(num4, num3, maxLabelWidth + 21f, maxLabelHeight);
							}
						}
					}
				}
			}
			if (!string.IsNullOrEmpty(_title))
			{
				g.DrawString(_title, font, _brush, x, num, stringFormat);
			}
		}

		internal void DrawAxisY(Graphics g, Font font, Rectangle border, int offset, ref float left, ref float right)
		{
			if (!_visible)
			{
				return;
			}
			StringFormat stringFormat = null;
			StringFormat stringFormat2 = null;
			float num = 0f;
			float y = border.Top + border.Height / 2;
			float num2 = 0f;
			float num3 = (_isCustomLabels ? ((float)(border.Top + offset)) : ((float)border.Top + (float)offset % locationStep));
			if (_position == AxisPosition.RightTop)
			{
				stringFormat = AxisFormat.TitleFormatRightAxisY;
				stringFormat2 = AxisFormat.LabelFormatRightAxisY;
				num = (float)border.Right + right + Width;
				num2 = (float)border.Right + right + 3f;
				right += Width;
			}
			else
			{
				stringFormat = AxisFormat.TitleFormatLeftAxisY;
				stringFormat2 = AxisFormat.LabelFormatLeftAxisY;
				num = left;
				num2 = left + Width - 3f;
				left += Width;
			}
			if (_labelVisible)
			{
				int count = _labels.Count;
				if (count > 0)
				{
					RectangleF rectangleF = default(RectangleF);
					for (int i = 0; i < count; i++)
					{
						float num4 = 0f;
						if (_isCustomLabels)
						{
							double num5 = (_isReverse ? _labels[i].Value : _labels[count - i - 1].Value);
							num4 = Convert.ToSingle((double)Height / Range * (num5 - MinValue));
							if (!_isReverse)
							{
								num4 = Height - num4;
							}
						}
						else
						{
							num4 = (float)i * locationStep;
						}
						num4 += num3;
						if (num4 >= (float)border.Top && num4 <= (float)border.Bottom)
						{
							PointF pointF = new PointF(num2, num4);
							if (!rectangleF.Contains(pointF))
							{
								g.DrawString(_isReverse ? _labels[i].Content : _labels[count - i - 1].Content, font, _brush, pointF, stringFormat2);
								rectangleF = new RectangleF(num2, num4, maxLabelWidth, maxLabelHeight + 6f);
							}
						}
					}
				}
			}
			if (!string.IsNullOrEmpty(_title))
			{
				Helper.DrawString(g, _title, font, _brush, new PointF(num, y), stringFormat, -90f);
			}
		}

		private void UpdateRange(double min, double max, int separatorSum)
		{
			double num = max - min;
			double num2 = Math.Pow(10.0, Math.Floor(Math.Log(num / (double)separatorSum) / Math.Log(10.0)));
			double num3 = num / (double)separatorSum / num2;
			double num4 = ((num3 > 5.0) ? (10.0 * num2) : ((num3 > 2.0) ? (5.0 * num2) : ((!(num3 > 1.0)) ? num2 : (2.0 * num2))));
			MinValue = Math.Floor(min / num4) * num4;
			MaxValue = Math.Ceiling(max / num4) * num4;
		}

		private float CountAxisYWidth(Graphics g, Font font)
		{
			if (!_visible)
			{
				return 0f;
			}
			float num = 0f;
			if (!string.IsNullOrEmpty(_title))
			{
				num = Helper.ConvertSize(g.MeasureString(_title, font), -90f).Width + 3f;
			}
			if (!_labelVisible || _labels.Count < 1)
			{
				return num;
			}
			maxLabelHeight = 0f;
			maxLabelWidth = 0f;
			for (int i = 0; i < _labels.Count; i++)
			{
				SizeF sizeF = g.MeasureString(_labels[i].Content, font);
				maxLabelHeight = ((sizeF.Height > maxLabelHeight) ? sizeF.Height : maxLabelHeight);
				maxLabelWidth = ((sizeF.Width > maxLabelWidth) ? sizeF.Width : maxLabelWidth);
			}
			return num + maxLabelWidth + 3f;
		}

		private float CountAxisXHeight(Graphics g, Font font)
		{
			if (!_visible)
			{
				return 0f;
			}
			float num = (string.IsNullOrEmpty(_title) ? 0f : (g.MeasureString(_title, font).Height + 3f));
			if (!_labelVisible || _labels.Count < 1)
			{
				return num;
			}
			maxLabelHeight = 0f;
			maxLabelWidth = 0f;
			for (int i = 0; i < _labels.Count; i++)
			{
				SizeF sizeF = g.MeasureString(_labels[i].Content, font);
				maxLabelHeight = ((sizeF.Height > maxLabelHeight) ? sizeF.Height : maxLabelHeight);
				maxLabelWidth = ((sizeF.Width > maxLabelWidth) ? sizeF.Width : maxLabelWidth);
			}
			return num + maxLabelHeight + 3f;
		}

		public void SetLabels(string[] labels, double startValue = 0.0, double valueStep = 0.1)
		{
			if (labels != null)
			{
				_isCustomLabels = true;
				_labels.Clear();
				for (int i = 0; i < labels.Length; i++)
				{
					_labels.Add(new AxisLabel(startValue + (double)i * valueStep)
					{
						Content = labels[i]
					});
				}
			}
			_chart?.Invalidate();
		}

		public void SetLabels(string[] labels, double[] values)
		{
			if (labels != null && values != null)
			{
				_isCustomLabels = true;
				_labels.Clear();
				for (int i = 0; i < labels.Length; i++)
				{
					_labels.Add(new AxisLabel(values[i])
					{
						Content = labels[i]
					});
				}
			}
			_chart?.Invalidate();
		}

		public void SetLabels(DateTime[] dateTimes, string format = "yyyy-MM-dd HH:mm:ss", double startValue = 0.0, double valueStep = 0.1)
		{
			if (dateTimes != null)
			{
				_isCustomLabels = true;
				_labels.Clear();
				for (int i = 0; i < dateTimes.Length; i++)
				{
					_labels.Add(new AxisLabel(startValue + (double)i * valueStep)
					{
						Content = dateTimes[i].ToString(format)
					});
				}
			}
			_chart?.Invalidate();
		}

		public void SetLabels(DateTime[] dateTimes, double[] values, string format = "yyyy-MM-dd HH:mm:ss")
		{
			if (dateTimes != null && values != null)
			{
				_isCustomLabels = true;
				_labels.Clear();
				for (int i = 0; i < dateTimes.Length; i++)
				{
					_labels.Add(new AxisLabel(values[i])
					{
						Content = dateTimes[i].ToString(format)
					});
				}
			}
			_chart?.Invalidate();
		}

		public string GetLabel(double value)
		{
			if (_isCustomLabels)
			{
				IEnumerable<AxisLabel> enumerable = _labels.Where((AxisLabel lab) => lab.Value == value);
				if (enumerable != null && enumerable.Count() > 0)
				{
					return enumerable.Last().Content;
				}
				return string.Empty;
			}
			return value.ToString();
		}
	}
}
