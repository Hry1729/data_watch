using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public partial class HslBarChart : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty ColorLinesAndTextProperty = DependencyProperty.Register("ColorLinesAndText", typeof(Color), typeof(HslBarChart), new PropertyMetadata(Colors.DimGray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty UseAnimationProperty = DependencyProperty.Register("UseAnimation", typeof(bool), typeof(HslBarChart), new PropertyMetadata(false));

		public static readonly DependencyProperty AnimationTimeProperty = DependencyProperty.Register("AnimationTime", typeof(int), typeof(HslBarChart), new PropertyMetadata(300));

		public static readonly DependencyProperty UseGradientProperty = DependencyProperty.Register("UseGradient", typeof(bool), typeof(HslBarChart), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty BarBackColorProperty = DependencyProperty.Register("BarBackColor", typeof(Color), typeof(HslBarChart), new PropertyMetadata(Colors.DodgerBlue, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ColorDashLinesProperty = DependencyProperty.Register("ColorDashLines", typeof(Color), typeof(HslBarChart), new PropertyMetadata(Colors.LightGray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsRenderDashLineProperty = DependencyProperty.Register("IsRenderDashLine", typeof(bool), typeof(HslBarChart), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueSegmentProperty = DependencyProperty.Register("ValueSegment", typeof(int), typeof(HslBarChart), new PropertyMetadata(5, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMaxLeftProperty = DependencyProperty.Register("ValueMaxLeft", typeof(int), typeof(HslBarChart), new PropertyMetadata(-1, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMinLeftProperty = DependencyProperty.Register("ValueMinLeft", typeof(int), typeof(HslBarChart), new PropertyMetadata(0, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(HslBarChart), new PropertyMetadata(string.Empty, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsShowBarValueProperty = DependencyProperty.Register("IsShowBarValue", typeof(bool), typeof(HslBarChart), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ShowBarValueFormatProperty = DependencyProperty.Register("ShowBarValueFormat", typeof(string), typeof(HslBarChart), new PropertyMetadata("{0}", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty BarPercentWidthProperty = DependencyProperty.Register("BarPercentWidth", typeof(float), typeof(HslBarChart), new PropertyMetadata(0.8f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsAoordinateRoundIntProperty = DependencyProperty.Register("IsAoordinateRoundInt", typeof(bool), typeof(HslBarChart), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		private Dictionary<string, double[]> dict_datas;

		private string[] data_texts = null;

		private Color[] data_colors = null;

		private List<AuxiliaryLine> auxiliary_lines;

		public Color ColorLinesAndText
		{
			get
			{
				return (Color)GetValue(ColorLinesAndTextProperty);
			}
			set
			{
				SetValue(ColorLinesAndTextProperty, value);
			}
		}

		public bool UseAnimation
		{
			get
			{
				return (bool)GetValue(UseAnimationProperty);
			}
			set
			{
				SetValue(UseAnimationProperty, value);
			}
		}

		public int AnimationTime
		{
			get
			{
				return (int)GetValue(AnimationTimeProperty);
			}
			set
			{
				SetValue(AnimationTimeProperty, value);
			}
		}

		public bool UseGradient
		{
			get
			{
				return (bool)GetValue(UseGradientProperty);
			}
			set
			{
				SetValue(UseGradientProperty, value);
			}
		}

		public Color BarBackColor
		{
			get
			{
				return (Color)GetValue(BarBackColorProperty);
			}
			set
			{
				SetValue(BarBackColorProperty, value);
			}
		}

		public Color ColorDashLines
		{
			get
			{
				return (Color)GetValue(ColorDashLinesProperty);
			}
			set
			{
				SetValue(ColorDashLinesProperty, value);
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

		public int ValueMaxLeft
		{
			get
			{
				return (int)GetValue(ValueMaxLeftProperty);
			}
			set
			{
				SetValue(ValueMaxLeftProperty, value);
			}
		}

		public int ValueMinLeft
		{
			get
			{
				return (int)GetValue(ValueMinLeftProperty);
			}
			set
			{
				if (value < ValueMaxLeft)
				{
					SetValue(ValueMinLeftProperty, value);
				}
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

		public bool IsShowBarValue
		{
			get
			{
				return (bool)GetValue(IsShowBarValueProperty);
			}
			set
			{
				SetValue(IsShowBarValueProperty, value);
			}
		}

		public string ShowBarValueFormat
		{
			get
			{
				return (string)GetValue(ShowBarValueFormatProperty);
			}
			set
			{
				SetValue(ShowBarValueFormatProperty, value);
			}
		}

		public float BarPercentWidth
		{
			get
			{
				return (float)GetValue(BarPercentWidthProperty);
			}
			set
			{
				SetValue(BarPercentWidthProperty, value);
			}
		}

		public bool IsAoordinateRoundInt
		{
			get
			{
				return (bool)GetValue(IsAoordinateRoundIntProperty);
			}
			set
			{
				SetValue(IsAoordinateRoundIntProperty, value);
			}
		}

		public HslBarChart()
		{
			InitializeComponent();
			auxiliary_lines = new List<AuxiliaryLine>();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			BarChartUpdate();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslBarChart hslBarChart)
			{
				hslBarChart.BarChartUpdate();
			}
		}

		public void AddLeftAuxiliary(float value)
		{
			AddLeftAuxiliary(value, ColorLinesAndText);
		}

		public void AddLeftAuxiliary(float value, Color lineColor)
		{
			AddLeftAuxiliary(value, lineColor, 1f, true);
		}

		public void AddLeftAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine)
		{
			AddAuxiliary(value, lineColor, lineThickness, isDashLine, true);
		}

		public void AddRightAuxiliary(float value)
		{
			AddRightAuxiliary(value, ColorLinesAndText);
		}

		public void AddRightAuxiliary(float value, Color lineColor)
		{
			AddRightAuxiliary(value, lineColor, 1f, true);
		}

		public void AddRightAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine)
		{
			AddAuxiliary(value, lineColor, lineThickness, isDashLine, false);
		}

		private void AddAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine, bool isLeft)
		{
			auxiliary_lines.Add(new AuxiliaryLine
			{
				Value = value,
				LineColor = WpfUtils.TransDrawingColor(lineColor),
				IsDashStyle = isDashLine,
				IsLeftFrame = isLeft,
				LineThickness = lineThickness
			});
			BarChartUpdate();
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
				BarChartUpdate();
			}
		}

		public void RemoveAllAuxiliary()
		{
			int count = auxiliary_lines.Count;
			auxiliary_lines.Clear();
			if (count > 0)
			{
				BarChartUpdate();
			}
		}

		public void BarChartUpdate()
		{
			if (!Authorization.iashdiadasbdnajsdhjaf())
			{
				return;
			}
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			Dictionary<string, double[]> dictionary = dict_datas;
			int num3 = ((dictionary != null && dictionary.Count > 0) ? HslHelper.CalculateMaxSectionFrom(dict_datas) : 5);
			if (ValueMaxLeft > 0)
			{
				num3 = ValueMaxLeft;
			}
			int num4 = (int)new FormattedText(num3.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(base.FontFamily, base.FontStyle, base.FontWeight, base.FontStretch), base.FontSize, Brushes.Black).Width + 3;
			if (num4 < 50)
			{
				num4 = 50;
			}
			int num5 = 10;
			int num6 = 25;
			int num7 = 25;
			WpfUtils.DrawLines(canvas1, new Point[3]
			{
				new Point(num4, num6 - 8),
				new Point(num4, num2 - num7),
				new Point(num - num5, num2 - num7)
			}, ColorLinesAndText, 1.0);
			WpfUtils.FillTriangle(canvas1, new Point(num4, num6 - 8), ColorLinesAndText, 4, GraphDirection.Upward);
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				auxiliary_lines[i].PaintValue = HslHelper.ComputePaintLocationY(num3, ValueMinLeft, num2 - num6 - num7, auxiliary_lines[i].Value) + (float)num6;
			}
			for (int j = 0; j <= ValueSegment; j++)
			{
				float num8 = (float)((double)j * (double)(num3 - ValueMinLeft) / (double)ValueSegment + (double)ValueMinLeft);
				if (IsAoordinateRoundInt)
				{
					num8 = (float)Math.Round(num8, 0);
				}
				float num9 = HslHelper.ComputePaintLocationY(num3, ValueMinLeft, num2 - num6 - num7, num8) + (float)num6;
				if (IsNeedPaintDash(num9))
				{
					Line element = new Line
					{
						StrokeThickness = 1.0,
						Stroke = new SolidColorBrush(ColorLinesAndText),
						X1 = num4 - 4,
						Y1 = num9,
						X2 = num4 - 1,
						Y2 = num9
					};
					canvas1.Children.Add(element);
					Label label = new Label();
					label.Width = num4 - 4;
					label.Height = 40.0;
					label.Foreground = new SolidColorBrush(ColorLinesAndText);
					label.HorizontalContentAlignment = HorizontalAlignment.Right;
					label.VerticalContentAlignment = VerticalAlignment.Center;
					label.Content = num8.ToString();
					canvas1.Children.Add(label);
					Canvas.SetLeft(label, 0.0);
					Canvas.SetTop(label, num9 - 19f);
					if (j > 0 && IsRenderDashLine)
					{
						Line line = new Line();
						line.StrokeThickness = 1.0;
						line.Stroke = new SolidColorBrush(ColorDashLines);
						line.StrokeDashArray = new DoubleCollection(new double[2] { 5.0, 5.0 });
						line.X1 = num4;
						line.Y1 = num9;
						line.X2 = num - num5;
						line.Y2 = num9;
						canvas1.Children.Add(line);
					}
				}
			}
			for (int k = 0; k < auxiliary_lines.Count; k++)
			{
				Line line2 = new Line
				{
					StrokeThickness = 1.0,
					Stroke = new SolidColorBrush(WpfUtils.TransMediaColor(auxiliary_lines[k].LineColor)),
					X1 = num4 - 4,
					Y1 = auxiliary_lines[k].PaintValue,
					X2 = num4 - 1,
					Y2 = auxiliary_lines[k].PaintValue
				};
				Label label2 = new Label();
				label2.Width = num4 - 4;
				label2.Height = 40.0;
				label2.Foreground = new SolidColorBrush(WpfUtils.TransMediaColor(auxiliary_lines[k].LineColor));
				label2.HorizontalContentAlignment = HorizontalAlignment.Right;
				label2.VerticalContentAlignment = VerticalAlignment.Center;
				label2.Content = auxiliary_lines[k].Value.ToString();
				canvas1.Children.Add(label2);
				Canvas.SetLeft(label2, 0.0);
				Canvas.SetTop(label2, auxiliary_lines[k].PaintValue - 19f);
				Line line3 = new Line
				{
					StrokeThickness = 1.0,
					Stroke = new SolidColorBrush(WpfUtils.TransMediaColor(auxiliary_lines[k].LineColor)),
					X1 = num4,
					Y1 = auxiliary_lines[k].PaintValue,
					X2 = num - num5,
					Y2 = auxiliary_lines[k].PaintValue
				};
				if (auxiliary_lines[k].IsDashStyle)
				{
					line3.StrokeDashArray = new DoubleCollection(new double[2] { 5.0, 5.0 });
				}
				canvas1.Children.Add(line3);
			}
			if (dict_datas == null || dict_datas.Count == 0)
			{
				return;
			}
			int num10 = dict_datas.Values.Select((double[] m) => m.Length).Max();
			float num11 = (float)(num - num4 - 1 - num5) * 1f / (float)num10;
			for (int l = 0; l < num10; l++)
			{
				int num12 = 0;
				foreach (KeyValuePair<string, double[]> dict_data in dict_datas)
				{
					double num13 = HslHelper.ComputePaintLocationY(num3, ValueMinLeft, num2 - num6 - num7, ValueMinLeft) + (float)num6;
					double num14 = HslHelper.ComputePaintLocationY(num3, ValueMinLeft, num2 - num6 - num7, (float)dict_data.Value[l]) + (float)num6;
					double num15 = 0.0;
					double num16 = (double)(num2 - num7) - num14;
					float num17 = num11 * BarPercentWidth;
					num17 = ((dict_datas.Count == 1) ? num17 : (num17 / (float)dict_datas.Count - (float)(3 * dict_datas.Count) + 3f));
					if (num17 < 1f)
					{
						num17 = 1f;
					}
					Rectangle rectangle = new Rectangle();
					rectangle.Width = num17;
					rectangle.Height = (UseAnimation ? num15 : num16);
					canvas1.Children.Add(rectangle);
					Canvas.SetLeft(rectangle, (float)l * num11 + (1f - BarPercentWidth) / 2f * num11 + (float)num4 + (float)num12 * (num11 * BarPercentWidth / (float)dict_datas.Count));
					Canvas.SetTop(rectangle, UseAnimation ? num13 : num14);
					if (UseAnimation)
					{
						DoubleAnimation doubleAnimation = new DoubleAnimation();
						doubleAnimation.From = num13;
						doubleAnimation.To = num14;
						doubleAnimation.Duration = TimeSpan.FromMilliseconds(AnimationTime);
						rectangle.BeginAnimation(Canvas.TopProperty, doubleAnimation);
						DoubleAnimation doubleAnimation2 = new DoubleAnimation();
						doubleAnimation2.From = num15;
						doubleAnimation2.To = num16;
						doubleAnimation2.Duration = TimeSpan.FromMilliseconds(AnimationTime);
						rectangle.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation2);
					}
					Color color = BarBackColor;
					if (data_colors != null)
					{
						if (dict_datas.Count == 1)
						{
							if (l < data_colors.Length)
							{
								color = data_colors[l];
							}
						}
						else if (num12 < data_colors.Length)
						{
							color = data_colors[num12];
						}
					}
					if (UseGradient)
					{
						rectangle.Fill = new LinearGradientBrush(HslHelper.GetColorLight(color), color, new Point(0.0, 1.0), new Point(0.0, 0.0));
					}
					else
					{
						rectangle.Fill = new SolidColorBrush(color);
					}
					if (IsShowBarValue)
					{
						Label label3 = new Label();
						label3.Width = rectangle.Width + 100.0;
						label3.Height = 35.0;
						label3.Foreground = new SolidColorBrush(ColorLinesAndText);
						label3.HorizontalContentAlignment = HorizontalAlignment.Center;
						label3.VerticalContentAlignment = VerticalAlignment.Bottom;
						label3.Content = string.Format(ShowBarValueFormat, dict_data.Value[l]);
						canvas1.Children.Add(label3);
						Canvas.SetLeft(label3, Canvas.GetLeft(rectangle) - 50.0);
						Canvas.SetTop(label3, UseAnimation ? (num13 - 35.0) : (num14 - 35.0));
						if (UseAnimation)
						{
							DoubleAnimation doubleAnimation3 = new DoubleAnimation();
							doubleAnimation3.From = num13 - 35.0;
							doubleAnimation3.To = num14 - 35.0;
							doubleAnimation3.Duration = TimeSpan.FromMilliseconds(AnimationTime);
							label3.BeginAnimation(Canvas.TopProperty, doubleAnimation3);
						}
					}
					num12++;
				}
				if (data_texts != null && l < data_texts.Length)
				{
					Label label4 = new Label();
					label4.Width = num11 + 100f;
					label4.Height = num7 + 1;
					label4.Foreground = new SolidColorBrush(ColorLinesAndText);
					label4.HorizontalContentAlignment = HorizontalAlignment.Center;
					label4.VerticalContentAlignment = VerticalAlignment.Top;
					label4.Content = data_texts[l];
					canvas1.Children.Add(label4);
					Canvas.SetLeft(label4, (float)l * num11 + (float)num4 - 50f);
					Canvas.SetTop(label4, num2 - num7 + 1);
				}
			}
			if (!string.IsNullOrEmpty(Title))
			{
				return;
			}
			int num18 = 0;
			int num19 = num4 + 100 * num18 + 10;
			foreach (KeyValuePair<string, double[]> dict_data2 in dict_datas)
			{
				if (!string.IsNullOrEmpty(dict_data2.Key))
				{
					Color color2 = BarBackColor;
					if (data_colors != null && num18 < data_colors.Length)
					{
						color2 = data_colors[num18];
					}
					WpfUtils.FillRectangle(canvas1, num19, 4.0, 20.0, num6 - 12, color2);
					num19 += 25;
					num19 += (int)WpfUtils.DrawString(canvas1, dict_data2.Key, color2, num19, -2.0, 100.0, num6 - 2, HorizontalAlignment.Left, VerticalAlignment.Center).Width + 10;
					num18++;
				}
			}
		}

		private bool IsNeedPaintDash(float paintValue)
		{
			if (dict_datas == null)
			{
				return true;
			}
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				if ((double)Math.Abs(auxiliary_lines[i].PaintValue - paintValue) < base.FontSize)
				{
					return false;
				}
			}
			return true;
		}

		public void SetDataSource(int[] data)
		{
			dict_datas = new Dictionary<string, double[]> { 
			{
				"",
				HslHelper.TranlateArrayToDouble(data)
			} };
			data_texts = null;
			data_colors = null;
			BarChartUpdate();
		}

		public void SetDataSource(double[] data)
		{
			dict_datas = new Dictionary<string, double[]> { { "", data } };
			data_texts = null;
			data_colors = null;
			BarChartUpdate();
		}

		public void SetDataSource(int[] data, string[] texts)
		{
			dict_datas = new Dictionary<string, double[]> { 
			{
				"",
				HslHelper.TranlateArrayToDouble(data)
			} };
			data_texts = texts;
			data_colors = null;
			BarChartUpdate();
		}

		public void SetDataSource(double[] data, string[] texts)
		{
			dict_datas = new Dictionary<string, double[]> { { "", data } };
			data_texts = texts;
			data_colors = null;
			BarChartUpdate();
		}

		public void SetDataSource(int[] data, string[] texts, Color[] colors)
		{
			dict_datas = new Dictionary<string, double[]> { 
			{
				"",
				HslHelper.TranlateArrayToDouble(data)
			} };
			data_texts = texts;
			data_colors = colors;
			BarChartUpdate();
		}

		public void SetDataSource(double[] data, string[] texts, Color[] colors)
		{
			dict_datas = new Dictionary<string, double[]> { { "", data } };
			data_texts = texts;
			data_colors = colors;
			BarChartUpdate();
		}

		public void SetDataSource(Dictionary<string, double[]> datas, string[] texts, Color[] colors)
		{
			dict_datas = datas;
			data_texts = texts;
			data_colors = colors;
			BarChartUpdate();
		}
	}
}
