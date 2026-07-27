using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslTitle : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty LeftRightEdgeColorProperty = DependencyProperty.Register("LeftRightEdgeColor", typeof(Color), typeof(HslTitle), new PropertyMetadata(Colors.DimGray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslTitle), new PropertyMetadata(Colors.LightGray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty WidthPercentProperty = DependencyProperty.Register("WidthPercent", typeof(float), typeof(HslTitle), new PropertyMetadata(0.5f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HslTitle), new PropertyMetadata("HslTitle", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextRightProperty = DependencyProperty.Register("TextRight", typeof(string), typeof(HslTitle), new PropertyMetadata("HslControls", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextRightFontSizeProperty = DependencyProperty.Register("TextRightFontSize", typeof(double), typeof(HslTitle), new PropertyMetadata(12.0, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextLeftProperty = DependencyProperty.Register("TextLeft", typeof(string), typeof(HslTitle), new PropertyMetadata("00:00:00", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextLeftFontSizeProperty = DependencyProperty.Register("TextLeftFontSize", typeof(double), typeof(HslTitle), new PropertyMetadata(12.0, RenderUpdateByPropertyChanged));

		public Color LeftRightEdgeColor
		{
			get
			{
				return (Color)GetValue(LeftRightEdgeColorProperty);
			}
			set
			{
				SetValue(LeftRightEdgeColorProperty, value);
			}
		}

		public Color EdgeColor
		{
			get
			{
				return (Color)GetValue(EdgeColorProperty);
			}
			set
			{
				SetValue(EdgeColorProperty, value);
			}
		}

		public float WidthPercent
		{
			get
			{
				return (float)GetValue(WidthPercentProperty);
			}
			set
			{
				SetValue(WidthPercentProperty, value);
			}
		}

		public string Text
		{
			get
			{
				return (string)GetValue(TextProperty);
			}
			set
			{
				SetValue(TextProperty, value);
			}
		}

		public string TextRight
		{
			get
			{
				return (string)GetValue(TextRightProperty);
			}
			set
			{
				SetValue(TextRightProperty, value);
			}
		}

		public double TextRightFontSize
		{
			get
			{
				return (double)GetValue(TextRightFontSizeProperty);
			}
			set
			{
				SetValue(TextRightFontSizeProperty, value);
			}
		}

		public string TextLeft
		{
			get
			{
				return (string)GetValue(TextLeftProperty);
			}
			set
			{
				SetValue(TextLeftProperty, value);
			}
		}

		public double TextLeftFontSize
		{
			get
			{
				return (double)GetValue(TextLeftFontSizeProperty);
			}
			set
			{
				SetValue(TextLeftFontSizeProperty, value);
			}
		}

		public HslTitle()
		{
			InitializeComponent();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslTitle hslTitle)
			{
				hslTitle.HslTitleUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslTitleUpdate();
		}

		public void HslTitleUpdate()
		{
			if(false)
			{
				canvas1.Children.Clear();
				int num = (int)base.ActualWidth;
				int num2 = (int)base.ActualHeight;
				canvas1.Background = new LinearGradientBrush(new GradientStopCollection
				{
					new GradientStop(LeftRightEdgeColor, 0.0),
					new GradientStop(HslHelper.GetColorLight(LeftRightEdgeColor), 0.5),
					new GradientStop(LeftRightEdgeColor, 1.0)
				}, new Point(0.0, 0.0), new Point(0.0, 1.0));
				Brush brush = new LinearGradientBrush(new GradientStopCollection
				{
					new GradientStop(EdgeColor, 0.0),
					new GradientStop(HslHelper.GetColorLight(EdgeColor), 0.5),
					new GradientStop(EdgeColor, 1.0)
				}, new Point(0.0, 0.0), new Point(0.0, 1.0));
				if ((float)num * 0.1f < (float)num2)
				{
					WpfUtils.FillPolygon(canvas1, new Point[4]
					{
						new Point((float)num * (0.5f - WidthPercent / 2f), 0.0),
						new Point((float)num * (0.5f + WidthPercent / 2f), 0.0),
						new Point((float)num * (0.4f + WidthPercent / 2f), num2),
						new Point((float)num * (0.6f - WidthPercent / 2f), num2)
					}, brush);
				}
				else
				{
					WpfUtils.FillPolygon(canvas1, new Point[4]
					{
						new Point((float)num * (0.5f - WidthPercent / 2f), 0.0),
						new Point((float)num * (0.5f + WidthPercent / 2f), 0.0),
						new Point((float)num * (0.5f + WidthPercent / 2f) - (float)num2, num2),
						new Point((float)num * (0.5f - WidthPercent / 2f) + (float)num2, num2)
					}, brush);
				}
				WpfUtils.DrawString(canvas1, Text, base.Foreground, 0.0, 0.0, num, num2, HorizontalAlignment.Center, VerticalAlignment.Center);
				brush = new LinearGradientBrush(new GradientStopCollection
				{
					new GradientStop(HslHelper.GetColorLight(Colors.Yellow), 0.0),
					new GradientStop(Colors.Yellow, 0.5),
					new GradientStop(HslHelper.GetColorLight(Colors.Yellow), 1.0)
				}, new Point(0.0, 0.0), new Point(0.0, 1.0));
				if (!string.IsNullOrEmpty(TextRight))
				{
					WpfUtils.DrawString(canvas1, TextRight, brush, TextRightFontSize, (float)num * (0.5f + WidthPercent / 2f), 0.0, (float)num * (0.5f - WidthPercent / 2f), num2, HorizontalAlignment.Center, VerticalAlignment.Center);
				}
				brush = new LinearGradientBrush(new GradientStopCollection
				{
					new GradientStop(HslHelper.GetColorLight(Colors.Cyan), 0.0),
					new GradientStop(Colors.Cyan, 0.5),
					new GradientStop(HslHelper.GetColorLight(Colors.Cyan), 1.0)
				}, new Point(0.0, 0.0), new Point(0.0, 1.0));
				if (!string.IsNullOrEmpty(TextLeft))
				{
					WpfUtils.DrawString(canvas1, TextLeft, brush, TextLeftFontSize, 0.0, 0.0, (float)num * (0.5f - WidthPercent / 2f), num2, HorizontalAlignment.Center, VerticalAlignment.Center);
				}
			}
		}
	}
}
