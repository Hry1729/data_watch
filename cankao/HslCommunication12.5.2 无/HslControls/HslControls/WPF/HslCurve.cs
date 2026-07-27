using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslCurve : UserControl, IComponentConnector
	{
		private Dictionary<string, HslCurveItem> data_list = null;

		private string[] data_text = null;

		public static readonly DependencyProperty ValueMaxLeftProperty = DependencyProperty.Register("ValueMaxLeft", typeof(float), typeof(HslCurve), new PropertyMetadata(100f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMinLeftProperty = DependencyProperty.Register("ValueMinLeft", typeof(float), typeof(HslCurve), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMaxRightProperty = DependencyProperty.Register("ValueMaxRight", typeof(float), typeof(HslCurve), new PropertyMetadata(100f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMinRightProperty = DependencyProperty.Register("ValueMinRight", typeof(float), typeof(HslCurve), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueSegmentProperty = DependencyProperty.Register("ValueSegment", typeof(int), typeof(HslCurve), new PropertyMetadata(5, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsAbscissaStrechProperty = DependencyProperty.Register("IsAbscissaStrech", typeof(bool), typeof(HslCurve), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty StrechDataCountMaxProperty = DependencyProperty.Register("StrechDataCountMax", typeof(int), typeof(HslCurve), new PropertyMetadata(300, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsRenderDashLineProperty = DependencyProperty.Register("IsRenderDashLine", typeof(bool), typeof(HslCurve), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ColorLinesAndTextProperty = DependencyProperty.Register("ColorLinesAndText", typeof(System.Windows.Media.Color), typeof(HslCurve), new PropertyMetadata(Colors.DimGray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ColorDashLinesProperty = DependencyProperty.Register("ColorDashLines", typeof(System.Windows.Media.Color), typeof(HslCurve), new PropertyMetadata(Colors.Gray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IntervalAbscissaTextProperty = DependencyProperty.Register("IntervalAbscissaText", typeof(int), typeof(HslCurve), new PropertyMetadata(100, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextAddFormatProperty = DependencyProperty.Register("TextAddFormat", typeof(string), typeof(HslCurve), new PropertyMetadata("HH:mm", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(HslCurve), new PropertyMetadata(string.Empty, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsRenderRightCoordinateProperty = DependencyProperty.Register("IsRenderRightCoordinate", typeof(bool), typeof(HslCurve), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty CurveNameWidthProperty = DependencyProperty.Register("CurveNameWidth", typeof(int), typeof(HslCurve), new PropertyMetadata(100, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty PointsRadiusProperty = DependencyProperty.Register("PointsRadius", typeof(int), typeof(HslCurve), new PropertyMetadata(0, RenderUpdateByPropertyChanged));

		private List<AuxiliaryLine> auxiliary_lines;

		private List<WpfAuxiliaryLable> auxiliary_Labels;

		private List<HslWpfMarkText> hslMarkTexts;

		private const int value_count_max = 4096;

		private Random random = null;

		public float ValueMaxLeft
		{
			get
			{
				return (float)GetValue(ValueMaxLeftProperty);
			}
			set
			{
				SetValue(ValueMaxLeftProperty, value);
			}
		}

		public float ValueMinLeft
		{
			get
			{
				return (float)GetValue(ValueMinLeftProperty);
			}
			set
			{
				SetValue(ValueMinLeftProperty, value);
			}
		}

		public float ValueMaxRight
		{
			get
			{
				return (float)GetValue(ValueMaxRightProperty);
			}
			set
			{
				SetValue(ValueMaxRightProperty, value);
			}
		}

		public float ValueMinRight
		{
			get
			{
				return (float)GetValue(ValueMinRightProperty);
			}
			set
			{
				SetValue(ValueMinRightProperty, value);
			}
		}

		public int ValueSegment
		{
			get
			{
				return (int)GetValue(ValueSegmentProperty);
			}
			set
			{
				SetValue(ValueSegmentProperty, value);
			}
		}

		public bool IsAbscissaStrech
		{
			get
			{
				return (bool)GetValue(IsAbscissaStrechProperty);
			}
			set
			{
				SetValue(IsAbscissaStrechProperty, value);
			}
		}

		public int StrechDataCountMax
		{
			get
			{
				return (int)GetValue(StrechDataCountMaxProperty);
			}
			set
			{
				SetValue(StrechDataCountMaxProperty, value);
			}
		}

		public bool IsRenderDashLine
		{
			get
			{
				return (bool)GetValue(IsRenderDashLineProperty);
			}
			set
			{
				SetValue(IsRenderDashLineProperty, value);
			}
		}

		public System.Windows.Media.Color ColorLinesAndText
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(ColorLinesAndTextProperty);
			}
			set
			{
				SetValue(ColorLinesAndTextProperty, value);
			}
		}

		public System.Windows.Media.Color ColorDashLines
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(ColorDashLinesProperty);
			}
			set
			{
				SetValue(ColorDashLinesProperty, value);
			}
		}

		public int IntervalAbscissaText
		{
			get
			{
				return (int)GetValue(IntervalAbscissaTextProperty);
			}
			set
			{
				SetValue(IntervalAbscissaTextProperty, value);
			}
		}

		public string TextAddFormat
		{
			get
			{
				return (string)GetValue(TextAddFormatProperty);
			}
			set
			{
				SetValue(TextAddFormatProperty, value);
			}
		}

		public string Title
		{
			get
			{
				return (string)GetValue(TitleProperty);
			}
			set
			{
				SetValue(TitleProperty, value);
			}
		}

		public bool IsRenderRightCoordinate
		{
			get
			{
				return (bool)GetValue(IsRenderRightCoordinateProperty);
			}
			set
			{
				SetValue(IsRenderRightCoordinateProperty, value);
			}
		}

		public int CurveNameWidth
		{
			get
			{
				return (int)GetValue(CurveNameWidthProperty);
			}
			set
			{
				SetValue(CurveNameWidthProperty, value);
			}
		}

		public int PointsRadius
		{
			get
			{
				return (int)GetValue(PointsRadiusProperty);
			}
			set
			{
				SetValue(PointsRadiusProperty, value);
			}
		}

		public HslCurve()
		{
			InitializeComponent();
			random = new Random();
			data_list = new Dictionary<string, HslCurveItem>();
			auxiliary_lines = new List<AuxiliaryLine>();
			hslMarkTexts = new List<HslWpfMarkText>();
			auxiliary_Labels = new List<WpfAuxiliaryLable>();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslCurveUpdate();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslCurve hslCurve)
			{
				hslCurve.HslCurveUpdate();
			}
		}

		public void SetCurveText(string[] descriptions)
		{
			data_text = descriptions;
			HslCurveUpdate();
		}

		public void SetLeftCurve(string key, float[] data)
		{
			SetLeftCurve(key, data, System.Windows.Media.Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
		}

		public void SetLeftCurve(string key, float[] data, System.Windows.Media.Color lineColor)
		{
			SetCurve(key, true, data, lineColor, 1f, CurveStyle.LineSegment);
		}

		public void SetLeftCurve(string key, float[] data, System.Windows.Media.Color lineColor, CurveStyle style)
		{
			SetCurve(key, true, data, lineColor, 1f, style);
		}

		public void SetRightCurve(string key, float[] data)
		{
			SetRightCurve(key, data, System.Windows.Media.Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
		}

		public void SetRightCurve(string key, float[] data, System.Windows.Media.Color lineColor)
		{
			SetCurve(key, false, data, lineColor, 1f, CurveStyle.LineSegment);
		}

		public void SetRightCurve(string key, float[] data, System.Windows.Media.Color lineColor, CurveStyle style)
		{
			SetCurve(key, false, data, lineColor, 1f, style);
		}

		public void SetCurve(string key, bool isLeft, float[] data, System.Windows.Media.Color lineColor, float thickness, CurveStyle style)
		{
			if (data_list.ContainsKey(key))
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_list[key].Data = data;
			}
			else
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_list.Add(key, new HslCurveItem
				{
					Data = data,
					MarkText = new string[data.Length],
					LineThickness = thickness,
					LineColor = WpfUtils.TransDrawingColor(lineColor),
					ReferenceAxisIndex = ((!isLeft) ? 1 : 0),
					Style = style
				});
				if (data_text == null)
				{
					data_text = new string[data.Length];
				}
			}
			HslCurveUpdate();
		}

		public void RemoveCurve(string key)
		{
			if (data_list.ContainsKey(key))
			{
				data_list.Remove(key);
			}
			if (data_list.Count == 0)
			{
				data_text = new string[0];
			}
			HslCurveUpdate();
		}

		public void RemoveAllCurve()
		{
			int count = data_list.Count;
			data_list.Clear();
			if (data_list.Count == 0)
			{
				data_text = new string[0];
			}
			if (count > 0)
			{
				HslCurveUpdate();
			}
		}

		public void RemoveAllCurveData()
		{
			int count = data_list.Count;
			foreach (KeyValuePair<string, HslCurveItem> item in data_list)
			{
				item.Value.Data = new float[0];
				item.Value.MarkText = new string[0];
			}
			data_text = new string[0];
			if (count > 0)
			{
				HslCurveUpdate();
			}
		}

		public HslCurveItem GetCurveItem(string key)
		{
			if (data_list.ContainsKey(key))
			{
				return data_list[key];
			}
			return null;
		}

		private void AddCurveData(string key, float[] values, string[] markTexts, bool isUpdateUI)
		{
			if ((values != null && values.Length < 1) || !data_list.ContainsKey(key))
			{
				return;
			}
			HslCurveItem hslCurveItem = data_list[key];
			if (hslCurveItem.Data != null)
			{
				if (IsAbscissaStrech)
				{
					HslHelper.AddArrayData(ref hslCurveItem.Data, values, StrechDataCountMax);
					HslHelper.AddArrayData(ref hslCurveItem.MarkText, markTexts, StrechDataCountMax);
				}
				else
				{
					HslHelper.AddArrayData(ref hslCurveItem.Data, values, 4096);
					HslHelper.AddArrayData(ref hslCurveItem.MarkText, markTexts, 4096);
				}
				if (isUpdateUI)
				{
					HslCurveUpdate();
				}
			}
		}

		private void AddCurveTime(int count)
		{
			AddCurveTime(count, DateTime.Now.ToString(TextAddFormat));
		}

		private void AddCurveTime(int count, string text)
		{
			if (data_text != null)
			{
				string[] array = new string[count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = text;
				}
				if (IsAbscissaStrech)
				{
					HslHelper.AddArrayData(ref data_text, array, StrechDataCountMax);
				}
				else
				{
					HslHelper.AddArrayData(ref data_text, array, 4096);
				}
			}
		}

		public void AddCurveData(string key, float value)
		{
			AddCurveData(key, new float[1] { value });
		}

		public void AddCurveData(string key, float value, string markText)
		{
			AddCurveData(key, new float[1] { value }, new string[1] { markText });
		}

		public void AddCurveData(string key, float[] values)
		{
			AddCurveData(key, values, null);
		}

		public void AddCurveData(string key, float[] values, string[] markTexts)
		{
			if (markTexts == null)
			{
				markTexts = new string[values.Length];
			}
			AddCurveData(key, values, markTexts, false);
			if (values != null && values.Length != 0)
			{
				AddCurveTime(values.Length);
			}
			HslCurveUpdate();
		}

		public void AddCurveData(string[] keys, float[] values)
		{
			AddCurveData(keys, values, null);
		}

		public void AddCurveData(string axisText, string[] keys, float[] values)
		{
			AddCurveData(axisText, keys, values, null);
		}

		public void AddCurveData(string[] keys, float[] values, string[] markTexts)
		{
			if (keys == null)
			{
				throw new ArgumentNullException("keys");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (markTexts == null)
			{
				markTexts = new string[keys.Length];
			}
			if (keys.Length != values.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			if (keys.Length != markTexts.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			for (int i = 0; i < keys.Length; i++)
			{
				AddCurveData(keys[i], new float[1] { values[i] }, new string[1] { markTexts[i] }, false);
			}
			AddCurveTime(1);
			HslCurveUpdate();
		}

		public void AddCurveData(string axisText, string[] keys, float[] values, string[] markTexts)
		{
			if (keys == null)
			{
				throw new ArgumentNullException("keys");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (markTexts == null)
			{
				markTexts = new string[keys.Length];
			}
			if (keys.Length != values.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			if (keys.Length != markTexts.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			for (int i = 0; i < keys.Length; i++)
			{
				AddCurveData(keys[i], new float[1] { values[i] }, new string[1] { markTexts[i] }, false);
			}
			AddCurveTime(1, axisText);
			HslCurveUpdate();
		}

		public void SetCurveVisible(string key, bool visible)
		{
			if (data_list.ContainsKey(key))
			{
				data_list[key].Visible = visible;
				HslCurveUpdate();
			}
		}

		public void SetCurveVisible(string[] keys, bool visible)
		{
			foreach (string key in keys)
			{
				if (data_list.ContainsKey(key))
				{
					data_list[key].Visible = visible;
				}
			}
			HslCurveUpdate();
		}

		public void AddLeftAuxiliary(float value)
		{
			AddLeftAuxiliary(value, ColorLinesAndText);
		}

		public void AddLeftAuxiliary(float value, System.Windows.Media.Color lineColor)
		{
			AddLeftAuxiliary(value, lineColor, 1f, true);
		}

		public void AddLeftAuxiliary(float value, System.Windows.Media.Color lineColor, float lineThickness, bool isDashLine)
		{
			AddAuxiliary(value, lineColor, lineThickness, isDashLine, true);
		}

		public void AddRightAuxiliary(float value)
		{
			AddRightAuxiliary(value, ColorLinesAndText);
		}

		public void AddRightAuxiliary(float value, System.Windows.Media.Color lineColor)
		{
			AddRightAuxiliary(value, lineColor, 1f, true);
		}

		public void AddRightAuxiliary(float value, System.Windows.Media.Color lineColor, float lineThickness, bool isDashLine)
		{
			AddAuxiliary(value, lineColor, lineThickness, isDashLine, false);
		}

		private void AddAuxiliary(float value, System.Windows.Media.Color lineColor, float lineThickness, bool isDashLine, bool isLeft)
		{
			auxiliary_lines.Add(new AuxiliaryLine
			{
				Value = value,
				LineColor = WpfUtils.TransDrawingColor(lineColor),
				IsDashStyle = isDashLine,
				IsLeftFrame = isLeft,
				LineThickness = lineThickness
			});
			HslCurveUpdate();
		}

		public void RemoveAuxiliary(float value)
		{
			int num = 0;
			for (int num2 = auxiliary_lines.Count - 1; num2 >= 0; num2--)
			{
				if (auxiliary_lines[num2].Value == value)
				{
					auxiliary_lines[num2].Dispose();
					auxiliary_lines.RemoveAt(num2);
					num++;
				}
			}
			if (num > 0)
			{
				HslCurveUpdate();
			}
		}

		public void RemoveAllAuxiliary()
		{
			int count = auxiliary_lines.Count;
			auxiliary_lines.Clear();
			if (count > 0)
			{
				HslCurveUpdate();
			}
		}

		public void AddAuxiliaryLabel(WpfAuxiliaryLable auxiliaryLable)
		{
			auxiliary_Labels.Add(auxiliaryLable);
			HslCurveUpdate();
		}

		public void RemoveAuxiliaryLable(WpfAuxiliaryLable auxiliaryLable)
		{
			if (auxiliary_Labels.Remove(auxiliaryLable))
			{
				HslCurveUpdate();
			}
		}

		public void RemoveAllAuxiliaryLable()
		{
			int count = auxiliary_Labels.Count;
			auxiliary_Labels.Clear();
			if (count > 0)
			{
				HslCurveUpdate();
			}
		}

		public void AddMarkText(HslWpfMarkText markText)
		{
			hslMarkTexts.Add(markText);
			if (data_list.Count > 0)
			{
				HslCurveUpdate();
			}
		}

		public void RemoveMarkText(HslWpfMarkText markText)
		{
			hslMarkTexts.Remove(markText);
			if (data_list.Count > 0)
			{
				HslCurveUpdate();
			}
		}

		public void RemoveAllMarkText()
		{
			hslMarkTexts.Clear();
			if (data_list.Count > 0)
			{
				HslCurveUpdate();
			}
		}

		private bool IsNeedPaintDash(float paintValue)
		{
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				if ((double)Math.Abs(auxiliary_lines[i].PaintValue - paintValue) < base.FontSize + 3.0)
				{
					return false;
				}
			}
			return true;
		}

		private int CalculateDataCountByOffect(float offect)
		{
			if (IntervalAbscissaText > 0)
			{
				return IntervalAbscissaText;
			}
			if (offect > 40f)
			{
				return 1;
			}
			offect = 40f / offect;
			return (int)Math.Ceiling(offect);
		}

		private void HslCurveUpdate()
		{
			if (!Authorization.iashdiadasbdnajsdhjaf())
			{
				return;
			}
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth + 1;
			int num2 = (int)base.ActualHeight + 1;
			if (num < 120 || num2 < 60)
			{
				return;
			}
			int num3 = 50;
			int num4 = 50;
			int num5 = 25;
			int num6 = 25;
			System.Windows.Point[] array = new System.Windows.Point[4]
			{
				new System.Windows.Point(num3 - 1, num5 - 8),
				new System.Windows.Point(num3 - 1, num2 - num6),
				new System.Windows.Point(num - num4, num2 - num6),
				new System.Windows.Point(num - num4, num5 - 8)
			};
			WpfUtils.DrawLine(canvas1, array[0], array[1], ColorLinesAndText, 1.0);
			WpfUtils.DrawLine(canvas1, array[1], array[2], ColorLinesAndText, 1.0);
			if (IsRenderRightCoordinate)
			{
				WpfUtils.DrawLine(canvas1, array[2], array[3], ColorLinesAndText, 1.0);
			}
			if (string.IsNullOrEmpty(Title) && data_list.Count > 0)
			{
				float num7 = num3 + 10;
				foreach (KeyValuePair<string, HslCurveItem> item in data_list)
				{
					if (item.Value.Visible)
					{
						System.Windows.Media.Color color = (item.Value.LineRenderVisiable ? WpfUtils.TransMediaColor(item.Value.LineColor) : System.Windows.Media.Color.FromArgb(80, item.Value.LineColor.R, item.Value.LineColor.G, item.Value.LineColor.B));
						WpfUtils.DrawLine(canvas1, num7, 11.0, num7 + 30f, 11.0, color, 1.0);
						WpfUtils.DrawEllipse(canvas1, num7 + 8f, 4.0, 14.0, 14.0, color, 1.0);
						WpfUtils.DrawString(canvas1, item.Key, color, num7 + 35f, -10.0, 120.0, 40.0, HorizontalAlignment.Left, VerticalAlignment.Center);
						num7 += (float)CurveNameWidth;
					}
				}
			}
			for (int i = 0; i < auxiliary_Labels.Count; i++)
			{
				if (!string.IsNullOrEmpty(auxiliary_Labels[i].Text))
				{
					int num8 = ((auxiliary_Labels[i].LocationX > 1f) ? ((int)auxiliary_Labels[i].LocationX) : ((int)(auxiliary_Labels[i].LocationX * (float)num)));
					WpfUtils.FillPolygon(points: new System.Windows.Point[4]
					{
						new System.Windows.Point(num8, 11.0),
						new System.Windows.Point(num8 + 10, 20.0),
						new System.Windows.Point(num8 + 10, 0.0),
						new System.Windows.Point(num8, 11.0)
					}, canvas: canvas1, color: auxiliary_Labels[i].TextBack);
					Label label = new Label();
					label.Height = 20.0;
					label.Foreground = new SolidColorBrush(auxiliary_Labels[i].TextBrush);
					label.Background = new SolidColorBrush(auxiliary_Labels[i].TextBack);
					label.HorizontalContentAlignment = HorizontalAlignment.Left;
					label.VerticalContentAlignment = VerticalAlignment.Center;
					label.Content = auxiliary_Labels[i].Text;
					label.Padding = new Thickness(0.0, 0.0, 4.0, 0.0);
					canvas1.Children.Add(label);
					label.Style = null;
					Canvas.SetLeft(label, num8 + 10);
					Canvas.SetTop(label, 0.0);
				}
			}
			WpfUtils.FillTriangle(canvas1, new System.Windows.Point(num3 - 1, num5 - 8), ColorLinesAndText, 4, GraphDirection.Upward);
			if (IsRenderRightCoordinate)
			{
				WpfUtils.FillTriangle(canvas1, new System.Windows.Point(num - num4, num5 - 8), ColorLinesAndText, 4, GraphDirection.Upward);
			}
			for (int j = 0; j < auxiliary_lines.Count; j++)
			{
				if (auxiliary_lines[j].IsLeftFrame)
				{
					auxiliary_lines[j].PaintValue = HslHelper.ComputePaintLocationY(ValueMaxLeft, ValueMinLeft, num2 - num5 - num6, auxiliary_lines[j].Value) + (float)num5;
				}
				else
				{
					auxiliary_lines[j].PaintValue = HslHelper.ComputePaintLocationY(ValueMaxRight, ValueMinRight, num2 - num5 - num6, auxiliary_lines[j].Value) + (float)num5;
				}
			}
			for (int k = 0; k <= ValueSegment; k++)
			{
				float value = (float)((double)k * (double)(ValueMaxLeft - ValueMinLeft) / (double)ValueSegment + (double)ValueMinLeft);
				float num9 = HslHelper.ComputePaintLocationY(ValueMaxLeft, ValueMinLeft, num2 - num5 - num6, value) + (float)num5;
				if (IsNeedPaintDash(num9))
				{
					WpfUtils.DrawLine(canvas1, num3 - 4, num9, num3 - 1, num9, ColorLinesAndText, 1.0);
					RectangleF rectangle = new RectangleF(0f, num9 - 20f, num3 - 4, 40f);
					WpfUtils.DrawString(canvas1, value.ToString(), ColorLinesAndText, rectangle, HorizontalAlignment.Right, VerticalAlignment.Center);
					if (IsRenderRightCoordinate)
					{
						float num10 = (float)k * (ValueMaxRight - ValueMinRight) / (float)ValueSegment + ValueMinRight;
						WpfUtils.DrawLine(canvas1, num - num4 + 1, num9, num - num4 + 4, num9, ColorLinesAndText, 1.0);
						rectangle.Location = new PointF(num - num4 + 4, num9 - 20f);
						WpfUtils.DrawString(canvas1, num10.ToString(), ColorLinesAndText, rectangle, HorizontalAlignment.Left, VerticalAlignment.Center);
					}
					if (k > 0 && IsRenderDashLine)
					{
						WpfUtils.DrawLine(canvas1, num3, num9, num - num4, num9, ColorDashLines, 1.0, new double[2] { 5.0, 5.0 });
					}
				}
			}
			if (IsRenderDashLine)
			{
				if (IsAbscissaStrech)
				{
					float num11 = (float)(num - num3 - num4) * 1f / (float)(StrechDataCountMax - 1);
					int num12 = CalculateDataCountByOffect(num11);
					for (int l = 0; l < StrechDataCountMax; l += num12)
					{
						if (l > 0 && l < StrechDataCountMax - 1)
						{
							WpfUtils.DrawLine(canvas1, (float)l * num11 + (float)num3, num5, (float)l * num11 + (float)num3, num2 - num6 - 1, ColorDashLines, 1.0, new double[2] { 5.0, 5.0 });
						}
						if (data_text != null && l < data_text.Length && (float)l * num11 + (float)num3 < (float)(data_text.Length - 1) * num11 + (float)num3 - 40f)
						{
							WpfUtils.DrawString(rectangle: new Rectangle((int)((float)l * num11) + num3 - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[l], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
						}
					}
					string[] array2 = data_text;
					if (array2 != null && array2.Length > 1)
					{
						if (data_text.Length < StrechDataCountMax)
						{
							WpfUtils.DrawLine(canvas1, (float)(data_text.Length - 1) * num11 + (float)num3, num5, (float)(data_text.Length - 1) * num11 + (float)num3, num2 - num6 - 1, ColorDashLines, 1.0, new double[2] { 5.0, 5.0 });
						}
						WpfUtils.DrawString(rectangle: new Rectangle((int)((float)(data_text.Length - 1) * num11 + (float)num3) - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[data_text.Length - 1], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
					}
				}
				else
				{
					int num13 = num - num3 - num4 + 1;
					for (int m = num3; m < num - num4; m += IntervalAbscissaText)
					{
						if (m != num3)
						{
							WpfUtils.DrawLine(canvas1, m, num5, m, num2 - num6 - 1, ColorDashLines, 1.0, new double[2] { 5.0, 5.0 });
						}
						if (data_text == null)
						{
							continue;
						}
						int num14 = ((num13 > data_text.Length) ? data_text.Length : num13);
						if (m - num3 < data_text.Length && num14 - (m - num3) > 40)
						{
							if (data_text.Length <= num13)
							{
								WpfUtils.DrawString(rectangle: new Rectangle(m - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[m - num3], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
								continue;
							}
							WpfUtils.DrawString(rectangle: new Rectangle(m - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[m - num3 + data_text.Length - num13], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
						}
					}
					string[] array3 = data_text;
					if (array3 != null && array3.Length > 1)
					{
						if (data_text.Length >= num13)
						{
							WpfUtils.DrawString(rectangle: new Rectangle(num - num4 - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[data_text.Length - 1], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
						}
						else
						{
							WpfUtils.DrawLine(canvas1, data_text.Length + num3 - 1, num5, data_text.Length + num3 - 1, num2 - num6 - 1, ColorDashLines, 1.0, new double[2] { 5.0, 5.0 });
							WpfUtils.DrawString(rectangle: new Rectangle(data_text.Length + num3 - 1 - 100, num2 - num6 + 1, 200, num6), canvas: canvas1, text: data_text[data_text.Length - 1], color: ColorLinesAndText, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Top);
						}
					}
				}
			}
			for (int n = 0; n < auxiliary_lines.Count; n++)
			{
				if (auxiliary_lines[n].IsLeftFrame)
				{
					WpfUtils.DrawLine(canvas1, num3 - 4, auxiliary_lines[n].PaintValue, num3 - 1, auxiliary_lines[n].PaintValue, ColorDashLines, 1.0);
					WpfUtils.DrawString(rectangle: new RectangleF(0f, auxiliary_lines[n].PaintValue - 20f, num3 - 4, 40f), canvas: canvas1, text: auxiliary_lines[n].Value.ToString(), color: ColorLinesAndText, horizontal: HorizontalAlignment.Right, vertical: VerticalAlignment.Center);
				}
				else
				{
					WpfUtils.DrawLine(canvas1, num - num4 + 1, auxiliary_lines[n].PaintValue, num - num4 + 4, auxiliary_lines[n].PaintValue, ColorDashLines, 1.0);
					WpfUtils.DrawString(rectangle: new RectangleF(num - num4 + 4, auxiliary_lines[n].PaintValue - 20f, num4 - 4, 40f), canvas: canvas1, text: auxiliary_lines[n].Value.ToString(), color: ColorLinesAndText, horizontal: HorizontalAlignment.Left, vertical: VerticalAlignment.Center);
				}
				if (auxiliary_lines[n].IsDashStyle)
				{
					WpfUtils.DrawLine(canvas1, num3, auxiliary_lines[n].PaintValue, num - num4, auxiliary_lines[n].PaintValue, WpfUtils.TransMediaColor(auxiliary_lines[n].LineColor), auxiliary_lines[n].LineThickness, new double[2]
					{
						(double)auxiliary_lines[n].LineThickness * 5.0,
						(double)auxiliary_lines[n].LineThickness * 5.0
					});
				}
				else
				{
					WpfUtils.DrawLine(canvas1, num3, auxiliary_lines[n].PaintValue, num - num4, auxiliary_lines[n].PaintValue, WpfUtils.TransMediaColor(auxiliary_lines[n].LineColor), auxiliary_lines[n].LineThickness);
				}
			}
			if (IsAbscissaStrech)
			{
				foreach (HslWpfMarkText hslMarkText in hslMarkTexts)
				{
					foreach (KeyValuePair<string, HslCurveItem> item2 in data_list)
					{
						if (!item2.Value.Visible || !item2.Value.LineRenderVisiable || item2.Key != hslMarkText.CurveKey)
						{
							continue;
						}
						float[] data = item2.Value.Data;
						if (data == null || data.Length <= 1)
						{
							continue;
						}
						float num15 = (float)(num - num3 - num4) * 1f / (float)(StrechDataCountMax - 1);
						if (hslMarkText.Index >= 0 && hslMarkText.Index < item2.Value.Data.Length)
						{
							System.Windows.Point point = new System.Windows.Point((float)num3 + (float)hslMarkText.Index * num15, HslHelper.ComputePaintLocationY((item2.Value.ReferenceAxisIndex == 0) ? ValueMaxLeft : ValueMaxRight, (item2.Value.ReferenceAxisIndex == 0) ? ValueMinLeft : ValueMinRight, num2 - num5 - num6, item2.Value.Data[hslMarkText.Index]) + (float)num5);
							WpfUtils.FillEllipse(canvas1, point.X - 3.0, point.Y - 3.0, 6.0, 6.0, hslMarkText.CircleColor);
							switch ((hslMarkText.PositionStyle == MarkTextPositionStyle.Auto) ? HslMarkText.CalculateDirectionFromDataIndex(item2.Value.Data, hslMarkText.Index) : hslMarkText.PositionStyle)
							{
							case MarkTextPositionStyle.Left:
								WpfUtils.DrawString(canvas1, hslMarkText.MarkText, hslMarkText.TextColor, point.X - 100.0, point.Y - 20.0, 100 - HslWpfMarkText.MarkTextOffect, 40.0, HorizontalAlignment.Right, VerticalAlignment.Center);
								break;
							case MarkTextPositionStyle.Up:
								WpfUtils.DrawString(canvas1, hslMarkText.MarkText, hslMarkText.TextColor, point.X - 100.0, point.Y - 40.0 - (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Bottom);
								break;
							case MarkTextPositionStyle.Right:
								WpfUtils.DrawString(canvas1, hslMarkText.MarkText, hslMarkText.TextColor, point.X + (double)HslWpfMarkText.MarkTextOffect, point.Y - 20.0, 100.0, 40.0, HorizontalAlignment.Left, VerticalAlignment.Center);
								break;
							case MarkTextPositionStyle.Down:
								WpfUtils.DrawString(canvas1, hslMarkText.MarkText, hslMarkText.TextColor, point.X - 100.0, point.Y + (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Top);
								break;
							}
						}
					}
				}
				{
					foreach (HslCurveItem value2 in data_list.Values)
					{
						if (!value2.Visible || !value2.LineRenderVisiable)
						{
							continue;
						}
						float[] data2 = value2.Data;
						if (data2 == null || data2.Length <= 1)
						{
							continue;
						}
						float num16 = (float)(num - num3 - num4) * 1f / (float)(StrechDataCountMax - 1);
						System.Windows.Point[] array4 = new System.Windows.Point[value2.Data.Length];
						for (int num17 = 0; num17 < value2.Data.Length; num17++)
						{
							array4[num17].X = (float)num3 + (float)num17 * num16;
							array4[num17].Y = HslHelper.ComputePaintLocationY((value2.ReferenceAxisIndex == 0) ? ValueMaxLeft : ValueMaxRight, (value2.ReferenceAxisIndex == 0) ? ValueMinLeft : ValueMinRight, num2 - num5 - num6, value2.Data[num17]) + (float)num5;
							if (!string.IsNullOrEmpty(value2.MarkText[num17]))
							{
								WpfUtils.FillEllipse(canvas1, array4[num17].X - 3.0, array4[num17].Y - 3.0, 6.0, 6.0, WpfUtils.TransMediaColor(value2.LineColor));
								switch (HslMarkText.CalculateDirectionFromDataIndex(value2.Data, num17))
								{
								case MarkTextPositionStyle.Left:
									WpfUtils.DrawString(canvas1, value2.MarkText[num17], WpfUtils.TransMediaColor(value2.LineColor), array4[num17].X - 100.0, array4[num17].Y - 20.0, 100 - HslWpfMarkText.MarkTextOffect, 40.0, HorizontalAlignment.Right, VerticalAlignment.Center);
									break;
								case MarkTextPositionStyle.Up:
									WpfUtils.DrawString(canvas1, value2.MarkText[num17], WpfUtils.TransMediaColor(value2.LineColor), array4[num17].X - 100.0, array4[num17].Y - 40.0 - (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Bottom);
									break;
								case MarkTextPositionStyle.Right:
									WpfUtils.DrawString(canvas1, value2.MarkText[num17], WpfUtils.TransMediaColor(value2.LineColor), array4[num17].X + (double)HslWpfMarkText.MarkTextOffect, array4[num17].Y - 20.0, 100.0, 40.0, HorizontalAlignment.Left, VerticalAlignment.Center);
									break;
								case MarkTextPositionStyle.Down:
									WpfUtils.DrawString(canvas1, value2.MarkText[num17], WpfUtils.TransMediaColor(value2.LineColor), array4[num17].X - 100.0, array4[num17].Y + (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Top);
									break;
								}
							}
						}
						WpfUtils.DrawLines(canvas1, array4, value2, PointsRadius);
					}
					return;
				}
			}
			foreach (HslWpfMarkText hslMarkText2 in hslMarkTexts)
			{
				foreach (KeyValuePair<string, HslCurveItem> item3 in data_list)
				{
					if (!item3.Value.Visible || !item3.Value.LineRenderVisiable || item3.Key != hslMarkText2.CurveKey)
					{
						continue;
					}
					float[] data3 = item3.Value.Data;
					if (data3 != null && data3.Length > 1 && hslMarkText2.Index >= 0 && hslMarkText2.Index < item3.Value.Data.Length)
					{
						System.Windows.Point point2 = new System.Windows.Point(num3 + hslMarkText2.Index, HslHelper.ComputePaintLocationY((item3.Value.ReferenceAxisIndex == 0) ? ValueMaxLeft : ValueMaxRight, (item3.Value.ReferenceAxisIndex == 0) ? ValueMinLeft : ValueMinRight, num2 - num5 - num6, item3.Value.Data[hslMarkText2.Index]) + (float)num5);
						WpfUtils.FillEllipse(canvas1, point2.X - 3.0, point2.Y - 3.0, 6.0, 6.0, hslMarkText2.CircleColor);
						switch ((hslMarkText2.PositionStyle == MarkTextPositionStyle.Auto) ? HslMarkText.CalculateDirectionFromDataIndex(item3.Value.Data, hslMarkText2.Index) : hslMarkText2.PositionStyle)
						{
						case MarkTextPositionStyle.Left:
							WpfUtils.DrawString(canvas1, hslMarkText2.MarkText, hslMarkText2.TextColor, point2.X - 100.0, point2.Y - 20.0, 100 - HslWpfMarkText.MarkTextOffect, 40.0, HorizontalAlignment.Right, VerticalAlignment.Center);
							break;
						case MarkTextPositionStyle.Up:
							WpfUtils.DrawString(canvas1, hslMarkText2.MarkText, hslMarkText2.TextColor, point2.X - 100.0, point2.Y - 40.0 - (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Bottom);
							break;
						case MarkTextPositionStyle.Right:
							WpfUtils.DrawString(canvas1, hslMarkText2.MarkText, hslMarkText2.TextColor, point2.X + (double)HslWpfMarkText.MarkTextOffect, point2.Y - 20.0, 100.0, 40.0, HorizontalAlignment.Left, VerticalAlignment.Center);
							break;
						case MarkTextPositionStyle.Down:
							WpfUtils.DrawString(canvas1, hslMarkText2.MarkText, hslMarkText2.TextColor, point2.X - 100.0, point2.Y + (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Top);
							break;
						}
					}
				}
			}
			foreach (HslCurveItem value3 in data_list.Values)
			{
				if (!value3.Visible || !value3.LineRenderVisiable)
				{
					continue;
				}
				float[] data4 = value3.Data;
				if (data4 == null || data4.Length <= 1)
				{
					continue;
				}
				int num18 = num - num3 - num4 + 1;
				List<System.Windows.Point> list = new List<System.Windows.Point>(value3.Data.Length);
				if (value3.Data.Length <= num18)
				{
					for (int num19 = 0; num19 < value3.Data.Length; num19++)
					{
						if (!float.IsNaN(value3.Data[num19]))
						{
							System.Windows.Point point3 = default(System.Windows.Point);
							point3.X = num3 + num19;
							point3.Y = HslHelper.ComputePaintLocationY((value3.ReferenceAxisIndex == 0) ? ValueMaxLeft : ValueMaxRight, (value3.ReferenceAxisIndex == 0) ? ValueMinLeft : ValueMinRight, num2 - num5 - num6, value3.Data[num19]) + (float)num5;
							list.Add(point3);
							DrawMarkPoint(value3.MarkText[num19], point3, WpfUtils.TransMediaColor(value3.LineColor), HslMarkText.CalculateDirectionFromDataIndex(value3.Data, num19));
						}
						else
						{
							WpfUtils.DrawLines(canvas1, list.ToArray(), value3, PointsRadius);
							list.Clear();
						}
					}
				}
				else
				{
					for (int num20 = 0; num20 < num18; num20++)
					{
						int num21 = num20 + value3.Data.Length - num18;
						if (!float.IsNaN(value3.Data[num20]))
						{
							System.Windows.Point point4 = default(System.Windows.Point);
							point4.X = num3 + num20;
							point4.Y = HslHelper.ComputePaintLocationY((value3.ReferenceAxisIndex == 0) ? ValueMaxLeft : ValueMaxRight, (value3.ReferenceAxisIndex == 0) ? ValueMinLeft : ValueMinRight, num2 - num5 - num6, value3.Data[num21]) + (float)num5;
							list.Add(point4);
							DrawMarkPoint(value3.MarkText[num21], point4, WpfUtils.TransMediaColor(value3.LineColor), HslMarkText.CalculateDirectionFromDataIndex(value3.Data, num21));
						}
						else
						{
							WpfUtils.DrawLines(canvas1, list.ToArray(), value3, PointsRadius);
							list.Clear();
						}
					}
				}
				WpfUtils.DrawLines(canvas1, list.ToArray(), value3, PointsRadius);
			}
		}

		private void DrawMarkPoint(string markText, System.Windows.Point center, System.Windows.Media.Color color, MarkTextPositionStyle markTextPosition)
		{
			if (!string.IsNullOrEmpty(markText))
			{
				WpfUtils.FillEllipse(canvas1, center.X - 3.0, center.Y - 3.0, 6.0, 6.0, color);
				switch (markTextPosition)
				{
				case MarkTextPositionStyle.Left:
					WpfUtils.DrawString(canvas1, markText, color, center.X - 100.0, center.Y - 20.0, 100 - HslWpfMarkText.MarkTextOffect, 40.0, HorizontalAlignment.Right, VerticalAlignment.Center);
					break;
				case MarkTextPositionStyle.Up:
					WpfUtils.DrawString(canvas1, markText, color, center.X - 100.0, center.Y - 40.0 - (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Bottom);
					break;
				case MarkTextPositionStyle.Right:
					WpfUtils.DrawString(canvas1, markText, color, center.X + (double)HslWpfMarkText.MarkTextOffect, center.Y - 20.0, 100.0, 40.0, HorizontalAlignment.Left, VerticalAlignment.Center);
					break;
				case MarkTextPositionStyle.Down:
					WpfUtils.DrawString(canvas1, markText, color, center.X - 100.0, center.Y + (double)HslWpfMarkText.MarkTextOffect, 200.0, 40.0, HorizontalAlignment.Center, VerticalAlignment.Top);
					break;
				}
			}
		}
	}
}
