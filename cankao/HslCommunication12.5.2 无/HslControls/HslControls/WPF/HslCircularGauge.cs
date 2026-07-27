using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	[TemplatePart(Name = "LayoutRoot", Type = typeof(Grid))]
	[TemplatePart(Name = "Pointer", Type = typeof(Path))]
	[TemplatePart(Name = "RangeIndicatorLight", Type = typeof(Ellipse))]
	[TemplatePart(Name = "PointerCap", Type = typeof(Ellipse))]
	public partial class HslCircularGauge : UserControl, IComponentConnector
	{
		private Grid rootGrid;

		private Path rangeIndicator;

		private Path rangeIndicatorAbove;

		private Path rangeIndicatorOptimal;

		private Path rangeIndicatorBelow;

		private Path pointer;

		private Ellipse pointerCap;

		private Ellipse lightIndicator;

		private bool isInitialValueSet = false;

		private double arcradius1;

		private double arcradius2;

		private int animatingSpeedFactor = 6;

		public static readonly DependencyProperty MinValueProperty;

		public static readonly DependencyProperty MaxValueProperty;

		public static readonly DependencyProperty CurrentValueProperty;

		public static readonly DependencyProperty RadiusProperty;

		public static readonly DependencyProperty DialBorderThicknessProperty;

		public static readonly DependencyProperty PointerCapRadiusProperty;

		public static readonly DependencyProperty PointerLengthProperty;

		public static readonly DependencyProperty ScaleRadiusProperty;

		public static readonly DependencyProperty ScaleStartAngleProperty;

		public static readonly DependencyProperty ScaleSweepAngleProperty;

		public static readonly DependencyProperty MajorDivisionsCountProperty;

		public static readonly DependencyProperty MinorDivisionsCountProperty;

		public static readonly DependencyProperty OptimalRangeEndValueProperty;

		public static readonly DependencyProperty OptimalRangeStartValueProperty;

		public static readonly DependencyProperty ImageSourceProperty;

		public static readonly DependencyProperty ImageOffsetProperty;

		public static readonly DependencyProperty RangeIndicatorLightOffsetProperty;

		public static readonly DependencyProperty ImageSizeProperty;

		public static readonly DependencyProperty RangeIndicatorRadiusProperty;

		public static readonly DependencyProperty RangeIndicatorThicknessProperty;

		public static readonly DependencyProperty ScaleLabelRadiusProperty;

		public static readonly DependencyProperty ScaleLabelSizeProperty;

		public static readonly DependencyProperty ScaleLabelFontSizeProperty;

		public static readonly DependencyProperty ScaleLabelForegroundProperty;

		public static readonly DependencyProperty MajorTickSizeProperty;

		public static readonly DependencyProperty MinorTickSizeProperty;

		public static readonly DependencyProperty MajorTickColorProperty;

		public static readonly DependencyProperty MinorTickColorProperty;

		public static readonly DependencyProperty GaugeBackgroundColorProperty;

		public static readonly DependencyProperty PointerThicknessProperty;

		public static readonly DependencyProperty ResetPointerOnStartUpProperty;

		public static readonly DependencyProperty ScaleValuePrecisionProperty;

		public static readonly DependencyProperty BelowOptimalRangeColorProperty;

		public static readonly DependencyProperty OptimalRangeColorProperty;

		public static readonly DependencyProperty AboveOptimalRangeColorProperty;

		public static readonly DependencyProperty DialTextProperty;

		public static readonly DependencyProperty DialTextColorProperty;

		public static readonly DependencyProperty DialTextFontSizeProperty;

		public static readonly DependencyProperty DialTextOffsetProperty;

		public static readonly DependencyProperty RangeIndicatorLightRadiusProperty;

		public static readonly DependencyProperty AnimatingSpeedFactorProperty;

		public int AnimatingSpeedFactor
		{
			get
			{
				return (int)GetValue(AnimatingSpeedFactorProperty);
			}
			set
			{
				SetValue(AnimatingSpeedFactorProperty, value);
			}
		}

		public double CurrentValue
		{
			get
			{
				return (double)GetValue(CurrentValueProperty);
			}
			set
			{
				SetValue(CurrentValueProperty, value);
			}
		}

		public double MinValue
		{
			get
			{
				return (double)GetValue(MinValueProperty);
			}
			set
			{
				SetValue(MinValueProperty, value);
			}
		}

		public double MaxValue
		{
			get
			{
				return (double)GetValue(MaxValueProperty);
			}
			set
			{
				SetValue(MaxValueProperty, value);
			}
		}

		public double Radius
		{
			get
			{
				return (double)GetValue(RadiusProperty);
			}
			set
			{
				SetValue(RadiusProperty, value);
			}
		}

		public double DialBorderThickness
		{
			get
			{
				return (double)GetValue(DialBorderThicknessProperty);
			}
			set
			{
				SetValue(DialBorderThicknessProperty, value);
			}
		}

		public double PointerCapRadius
		{
			get
			{
				return (double)GetValue(PointerCapRadiusProperty);
			}
			set
			{
				SetValue(PointerCapRadiusProperty, value);
			}
		}

		public double PointerLength
		{
			get
			{
				return (double)GetValue(PointerLengthProperty);
			}
			set
			{
				SetValue(PointerLengthProperty, value);
			}
		}

		public double PointerThickness
		{
			get
			{
				return (double)GetValue(PointerThicknessProperty);
			}
			set
			{
				SetValue(PointerThicknessProperty, value);
			}
		}

		public double ScaleRadius
		{
			get
			{
				return (double)GetValue(ScaleRadiusProperty);
			}
			set
			{
				SetValue(ScaleRadiusProperty, value);
			}
		}

		public double ScaleStartAngle
		{
			get
			{
				return (double)GetValue(ScaleStartAngleProperty);
			}
			set
			{
				SetValue(ScaleStartAngleProperty, value);
			}
		}

		public double ScaleSweepAngle
		{
			get
			{
				return (double)GetValue(ScaleSweepAngleProperty);
			}
			set
			{
				SetValue(ScaleSweepAngleProperty, value);
			}
		}

		public double MajorDivisionsCount
		{
			get
			{
				return (double)GetValue(MajorDivisionsCountProperty);
			}
			set
			{
				SetValue(MajorDivisionsCountProperty, value);
			}
		}

		public double MinorDivisionsCount
		{
			get
			{
				return (double)GetValue(MinorDivisionsCountProperty);
			}
			set
			{
				SetValue(MinorDivisionsCountProperty, value);
			}
		}

		public double OptimalRangeEndValue
		{
			get
			{
				return (double)GetValue(OptimalRangeEndValueProperty);
			}
			set
			{
				SetValue(OptimalRangeEndValueProperty, value);
			}
		}

		public double OptimalRangeStartValue
		{
			get
			{
				return (double)GetValue(OptimalRangeStartValueProperty);
			}
			set
			{
				SetValue(OptimalRangeStartValueProperty, value);
			}
		}

		public ImageSource ImageSource
		{
			get
			{
				return (ImageSource)GetValue(ImageSourceProperty);
			}
			set
			{
				SetValue(ImageSourceProperty, value);
			}
		}

		public double ImageOffset
		{
			get
			{
				return (double)GetValue(ImageOffsetProperty);
			}
			set
			{
				SetValue(ImageOffsetProperty, value);
			}
		}

		public double RangeIndicatorLightOffset
		{
			get
			{
				return (double)GetValue(RangeIndicatorLightOffsetProperty);
			}
			set
			{
				SetValue(RangeIndicatorLightOffsetProperty, value);
			}
		}

		public Size ImageSize
		{
			get
			{
				return (Size)GetValue(ImageSizeProperty);
			}
			set
			{
				SetValue(ImageSizeProperty, value);
			}
		}

		public double RangeIndicatorRadius
		{
			get
			{
				return (double)GetValue(RangeIndicatorRadiusProperty);
			}
			set
			{
				SetValue(RangeIndicatorRadiusProperty, value);
			}
		}

		public double RangeIndicatorThickness
		{
			get
			{
				return (double)GetValue(RangeIndicatorThicknessProperty);
			}
			set
			{
				SetValue(RangeIndicatorThicknessProperty, value);
			}
		}

		public double ScaleLabelRadius
		{
			get
			{
				return (double)GetValue(ScaleLabelRadiusProperty);
			}
			set
			{
				SetValue(ScaleLabelRadiusProperty, value);
			}
		}

		public Size ScaleLabelSize
		{
			get
			{
				return (Size)GetValue(ScaleLabelSizeProperty);
			}
			set
			{
				SetValue(ScaleLabelSizeProperty, value);
			}
		}

		public double ScaleLabelFontSize
		{
			get
			{
				return (double)GetValue(ScaleLabelFontSizeProperty);
			}
			set
			{
				SetValue(ScaleLabelFontSizeProperty, value);
			}
		}

		public Color ScaleLabelForeground
		{
			get
			{
				return (Color)GetValue(ScaleLabelForegroundProperty);
			}
			set
			{
				SetValue(ScaleLabelForegroundProperty, value);
			}
		}

		public Size MajorTickSize
		{
			get
			{
				return (Size)GetValue(MajorTickSizeProperty);
			}
			set
			{
				SetValue(MajorTickSizeProperty, value);
			}
		}

		public Size MinorTickSize
		{
			get
			{
				return (Size)GetValue(MinorTickSizeProperty);
			}
			set
			{
				SetValue(MinorTickSizeProperty, value);
			}
		}

		public Color MajorTickColor
		{
			get
			{
				return (Color)GetValue(MajorTickColorProperty);
			}
			set
			{
				SetValue(MajorTickColorProperty, value);
			}
		}

		public Color MinorTickColor
		{
			get
			{
				return (Color)GetValue(MinorTickColorProperty);
			}
			set
			{
				SetValue(MinorTickColorProperty, value);
			}
		}

		public Color GaugeBackgroundColor
		{
			get
			{
				return (Color)GetValue(GaugeBackgroundColorProperty);
			}
			set
			{
				SetValue(GaugeBackgroundColorProperty, value);
			}
		}

		public bool ResetPointerOnStartUp
		{
			get
			{
				return (bool)GetValue(ResetPointerOnStartUpProperty);
			}
			set
			{
				SetValue(ResetPointerOnStartUpProperty, value);
			}
		}

		public int ScaleValuePrecision
		{
			get
			{
				return (int)GetValue(ScaleValuePrecisionProperty);
			}
			set
			{
				SetValue(ScaleValuePrecisionProperty, value);
			}
		}

		public Color BelowOptimalRangeColor
		{
			get
			{
				return (Color)GetValue(BelowOptimalRangeColorProperty);
			}
			set
			{
				SetValue(BelowOptimalRangeColorProperty, value);
			}
		}

		public Color OptimalRangeColor
		{
			get
			{
				return (Color)GetValue(OptimalRangeColorProperty);
			}
			set
			{
				SetValue(OptimalRangeColorProperty, value);
			}
		}

		public Color AboveOptimalRangeColor
		{
			get
			{
				return (Color)GetValue(AboveOptimalRangeColorProperty);
			}
			set
			{
				SetValue(AboveOptimalRangeColorProperty, value);
			}
		}

		public string DialText
		{
			get
			{
				return (string)GetValue(DialTextProperty);
			}
			set
			{
				SetValue(DialTextProperty, value);
			}
		}

		public Color DialTextColor
		{
			get
			{
				return (Color)GetValue(DialTextColorProperty);
			}
			set
			{
				SetValue(DialTextColorProperty, value);
			}
		}

		public int DialTextFontSize
		{
			get
			{
				return (int)GetValue(DialTextFontSizeProperty);
			}
			set
			{
				SetValue(DialTextFontSizeProperty, value);
			}
		}

		public double DialTextOffset
		{
			get
			{
				return (double)GetValue(DialTextOffsetProperty);
			}
			set
			{
				SetValue(DialTextOffsetProperty, value);
			}
		}

		public double RangeIndicatorLightRadius
		{
			get
			{
				return (double)GetValue(RangeIndicatorLightRadiusProperty);
			}
			set
			{
				SetValue(RangeIndicatorLightRadiusProperty, value);
			}
		}

		public HslCircularGauge()
		{
			InitializeComponent();
		}

		static HslCircularGauge()
		{
			MinValueProperty = DependencyProperty.Register("MinValue", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(0.0, OnMinValuePropertyChanged));
			MaxValueProperty = DependencyProperty.Register("MaxValue", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(100.0, OnMaxValuePropertyChanged));
			CurrentValueProperty = DependencyProperty.Register("CurrentValue", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(double.MinValue, OnCurrentValuePropertyChanged));
			RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(150.0, OnMaxValuePropertyChanged));
			DialBorderThicknessProperty = DependencyProperty.Register("DialBorderThickness", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(16.0, null));
			PointerCapRadiusProperty = DependencyProperty.Register("PointerCapRadius", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(35.0, null));
			PointerLengthProperty = DependencyProperty.Register("PointerLength", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(85.0, null));
			ScaleRadiusProperty = DependencyProperty.Register("ScaleRadius", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(110.0, OnScalePropertyChanged));
			ScaleStartAngleProperty = DependencyProperty.Register("ScaleStartAngle", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(120.0, OnScalePropertyChanged));
			ScaleSweepAngleProperty = DependencyProperty.Register("ScaleSweepAngle", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(300.0, OnScalePropertyChanged));
			MajorDivisionsCountProperty = DependencyProperty.Register("MajorDivisionsCount", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(10.0, OnScalePropertyChanged));
			MinorDivisionsCountProperty = DependencyProperty.Register("MinorDivisionsCount", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(5.0, OnScalePropertyChanged));
			OptimalRangeEndValueProperty = DependencyProperty.Register("OptimalRangeEndValue", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(70.0, OnScalePropertyChanged));
			OptimalRangeStartValueProperty = DependencyProperty.Register("OptimalRangeStartValue", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(30.0, OnScalePropertyChanged));
			ImageSourceProperty = DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(HslCircularGauge), null);
			ImageOffsetProperty = DependencyProperty.Register("ImageOffset", typeof(double), typeof(HslCircularGauge), null);
			RangeIndicatorLightOffsetProperty = DependencyProperty.Register("RangeIndicatorLightOffset", typeof(double), typeof(HslCircularGauge), null);
			ImageSizeProperty = DependencyProperty.Register("ImageSize", typeof(Size), typeof(HslCircularGauge), null);
			RangeIndicatorRadiusProperty = DependencyProperty.Register("RangeIndicatorRadius", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(120.0, OnScalePropertyChanged));
			RangeIndicatorThicknessProperty = DependencyProperty.Register("RangeIndicatorThickness", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(8.0, OnScalePropertyChanged));
			ScaleLabelRadiusProperty = DependencyProperty.Register("ScaleLabelRadius", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(95.0, OnScalePropertyChanged));
			ScaleLabelSizeProperty = DependencyProperty.Register("ScaleLabelSize", typeof(Size), typeof(HslCircularGauge), new PropertyMetadata(new Size(40.0, 20.0), OnScalePropertyChanged));
			ScaleLabelFontSizeProperty = DependencyProperty.Register("ScaleLabelFontSize", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(15.0, OnScalePropertyChanged));
			ScaleLabelForegroundProperty = DependencyProperty.Register("ScaleLabelForeground", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Red, OnScalePropertyChanged));
			MajorTickSizeProperty = DependencyProperty.Register("MajorTickRectSize", typeof(Size), typeof(HslCircularGauge), new PropertyMetadata(new Size(10.0, 3.0), OnScalePropertyChanged));
			MinorTickSizeProperty = DependencyProperty.Register("MinorTickSize", typeof(Size), typeof(HslCircularGauge), new PropertyMetadata(new Size(3.0, 1.0), OnScalePropertyChanged));
			MajorTickColorProperty = DependencyProperty.Register("MajorTickColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Red, OnScalePropertyChanged));
			MinorTickColorProperty = DependencyProperty.Register("MinorTickColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Red, OnScalePropertyChanged));
			GaugeBackgroundColorProperty = DependencyProperty.Register("GaugeBackgroundColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.WhiteSmoke, null));
			PointerThicknessProperty = DependencyProperty.Register("PointerThickness", typeof(double), typeof(HslCircularGauge), new PropertyMetadata(16.0, OnScalePropertyChanged));
			ResetPointerOnStartUpProperty = DependencyProperty.Register("ResetPointerOnStartUp", typeof(bool), typeof(HslCircularGauge), new PropertyMetadata(false, null));
			ScaleValuePrecisionProperty = DependencyProperty.Register("ScaleValuePrecision", typeof(int), typeof(HslCircularGauge), null);
			BelowOptimalRangeColorProperty = DependencyProperty.Register("BelowOptimalRangeColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Yellow, OnScalePropertyChanged));
			OptimalRangeColorProperty = DependencyProperty.Register("OptimalRangeColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Green, OnScalePropertyChanged));
			AboveOptimalRangeColorProperty = DependencyProperty.Register("AboveOptimalRangeColor", typeof(Color), typeof(HslCircularGauge), new PropertyMetadata(Colors.Red, OnScalePropertyChanged));
			DialTextProperty = DependencyProperty.Register("DialText", typeof(string), typeof(HslCircularGauge), null);
			DialTextColorProperty = DependencyProperty.Register("DialTextColor", typeof(Color), typeof(HslCircularGauge), null);
			DialTextFontSizeProperty = DependencyProperty.Register("DialTextFontSize", typeof(int), typeof(HslCircularGauge), null);
			DialTextOffsetProperty = DependencyProperty.Register("DialTextOffset", typeof(double), typeof(HslCircularGauge), null);
			RangeIndicatorLightRadiusProperty = DependencyProperty.Register("RangeIndicatorLightRadius", typeof(double), typeof(HslCircularGauge), null);
			AnimatingSpeedFactorProperty = DependencyProperty.Register("AnimatingSpeedFactor", typeof(int), typeof(HslCircularGauge), new PropertyMetadata(6));
			FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HslCircularGauge), new FrameworkPropertyMetadata(typeof(HslCircularGauge)));
		}

		private static void OnCurrentValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as HslCircularGauge).OnCurrentValueChanged(e);
		}

		private static void OnMinValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			HslCircularGauge hslCircularGauge = d as HslCircularGauge;
			if ((double)e.NewValue < hslCircularGauge.MaxValue)
			{
				hslCircularGauge.RefreshScale();
				hslCircularGauge.OnCurrentValueChanged(new DependencyPropertyChangedEventArgs(CurrentValueProperty, hslCircularGauge.CurrentValue, hslCircularGauge.CurrentValue + 0.1));
			}
		}

		private static void OnMaxValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			HslCircularGauge hslCircularGauge = d as HslCircularGauge;
			if ((double)e.NewValue > hslCircularGauge.MinValue)
			{
				hslCircularGauge.RefreshScale();
				hslCircularGauge.OnCurrentValueChanged(new DependencyPropertyChangedEventArgs(CurrentValueProperty, hslCircularGauge.CurrentValue, hslCircularGauge.CurrentValue + 0.1));
			}
		}

		private static void OnScalePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as HslCircularGauge).RefreshScale();
		}

		public virtual void OnCurrentValueChanged(DependencyPropertyChangedEventArgs e)
		{
			double num = (double)e.NewValue;
			double num2 = (double)e.OldValue;
			if (num > MaxValue)
			{
				num = MaxValue;
			}
			else if (num < MinValue)
			{
				num = MinValue;
			}
			if (num2 > MaxValue)
			{
				num2 = MaxValue;
			}
			else if (num2 < MinValue)
			{
				num2 = MinValue;
			}
			if (pointer != null)
			{
				MovePointer(num, num2);
			}
		}

		private void MovePointer(double newValue, double oldValue)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = ScaleSweepAngle / (MaxValue - MinValue);
			if (oldValue == 0.0 && !isInitialValueSet)
			{
				oldValue = MinValue;
				isInitialValueSet = true;
			}
			num = num3 * (oldValue - MinValue);
			num2 = num3 * (newValue - MinValue);
			AnimatePointer(ScaleStartAngle + num, ScaleStartAngle + num2);
		}

		private double CalculateStartAngle(double oldValue, double newValue)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = ScaleSweepAngle / (MaxValue - MinValue);
			if (oldValue == 0.0 && !isInitialValueSet)
			{
				oldValue = MinValue;
				isInitialValueSet = true;
			}
			num = num3 * (oldValue - MinValue);
			num2 = num3 * (newValue - MinValue);
			double num4 = ScaleStartAngle + num;
			return ScaleStartAngle + num2;
		}

		private void AnimatePointer(double oldcurrentvalueAngle, double newcurrentvalueAngle)
		{
			if (pointer != null)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation();
				double angle = ((RotateTransform)((TransformGroup)pointer.RenderTransform).Children[0]).Angle;
				doubleAnimation.From = angle;
				doubleAnimation.To = newcurrentvalueAngle;
				doubleAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(Math.Abs(angle - newcurrentvalueAngle) * (double)AnimatingSpeedFactor));
				Storyboard storyboard = new Storyboard();
				storyboard.Completed += sb_Completed;
				storyboard.Children.Add(doubleAnimation);
				Storyboard.SetTarget(doubleAnimation, pointer);
				Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(Path.RenderTransform).(TransformGroup.Children)[0].(RotateTransform.Angle)"));
				if (newcurrentvalueAngle != oldcurrentvalueAngle)
				{
					storyboard.Begin();
				}
			}
		}

		private void MovePointer(double angleValue)
		{
			if (pointer != null)
			{
				((pointer.RenderTransform as TransformGroup).Children[0] as RotateTransform).Angle = angleValue;
			}
		}

		private void sb_Completed(object sender, EventArgs e)
		{
			if (CurrentValue > OptimalRangeEndValue)
			{
				lightIndicator.Fill = GetRangeIndicatorGradEffect(AboveOptimalRangeColor);
			}
			else if (CurrentValue <= OptimalRangeEndValue && CurrentValue >= OptimalRangeStartValue)
			{
				lightIndicator.Fill = GetRangeIndicatorGradEffect(OptimalRangeColor);
			}
			else if (CurrentValue < OptimalRangeStartValue)
			{
				lightIndicator.Fill = GetRangeIndicatorGradEffect(BelowOptimalRangeColor);
			}
		}

		private GradientBrush GetRangeIndicatorGradEffect(Color gradientColor)
		{
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
			linearGradientBrush.StartPoint = new Point(0.0, 0.0);
			linearGradientBrush.EndPoint = new Point(1.0, 1.0);
			GradientStop gradientStop = new GradientStop();
			if (gradientColor == Colors.Transparent)
			{
				gradientStop.Color = gradientColor;
			}
			else
			{
				gradientStop.Color = Colors.LightGray;
			}
			gradientStop.Offset = 0.2;
			linearGradientBrush.GradientStops.Add(gradientStop);
			GradientStop gradientStop2 = new GradientStop();
			gradientStop2.Color = gradientColor;
			gradientStop2.Offset = 0.5;
			linearGradientBrush.GradientStops.Add(gradientStop2);
			GradientStop gradientStop3 = new GradientStop();
			gradientStop3.Color = gradientColor;
			gradientStop3.Offset = 0.8;
			linearGradientBrush.GradientStops.Add(gradientStop3);
			return linearGradientBrush;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			rootGrid = GetTemplateChild("LayoutRoot") as Grid;
			pointer = GetTemplateChild("Pointer") as Path;
			pointerCap = GetTemplateChild("PointerCap") as Ellipse;
			lightIndicator = GetTemplateChild("RangeIndicatorLight") as Ellipse;
			DrawScale();
			DrawRangeIndicator();
			Panel.SetZIndex(pointer, 100000);
			Panel.SetZIndex(pointerCap, 100001);
			if (ResetPointerOnStartUp)
			{
				double num = 0.0;
				MovePointer((!(CurrentValue <= MinValue)) ? CalculateStartAngle(MinValue, CurrentValue) : ScaleStartAngle);
			}
		}

		public void RefreshScale()
		{
			rootGrid = GetTemplateChild("LayoutRoot") as Grid;
			if (rootGrid == null)
			{
				return;
			}
			for (int num = rootGrid.Children.Count - 1; num >= 0; num--)
			{
				if (rootGrid.Children[num] is TextBlock textBlock)
				{
					if (textBlock.Text != DialText)
					{
						rootGrid.Children.Remove(textBlock);
					}
				}
				else if (rootGrid.Children[num] is Rectangle element)
				{
					rootGrid.Children.Remove(element);
				}
				else
				{
					Path path = rootGrid.Children[num] as Path;
					if (path == rangeIndicatorAbove || path == rangeIndicatorOptimal || path == rangeIndicatorBelow)
					{
						rootGrid.Children.Remove(path);
					}
				}
			}
			OnApplyTemplate();
		}

		private void DrawScale()
		{
			double num = ScaleSweepAngle / MajorDivisionsCount;
			double num2 = ScaleSweepAngle / MinorDivisionsCount;
			double num3 = Math.Round((MaxValue - MinValue) / MajorDivisionsCount, ScaleValuePrecision);
			double value = MinValue;
			for (double num4 = ScaleStartAngle; num4 <= ScaleStartAngle + ScaleSweepAngle; num4 += num)
			{
				Rectangle rectangle = new Rectangle();
				rectangle.Height = MajorTickSize.Height;
				rectangle.Width = MajorTickSize.Width;
				rectangle.Fill = new SolidColorBrush(MajorTickColor);
				rectangle.RenderTransformOrigin = new Point(0.5, 0.5);
				rectangle.HorizontalAlignment = HorizontalAlignment.Center;
				rectangle.VerticalAlignment = VerticalAlignment.Center;
				TransformGroup transformGroup = new TransformGroup();
				RotateTransform rotateTransform = new RotateTransform();
				double num5 = num4 * Math.PI / 180.0;
				rotateTransform.Angle = num4;
				transformGroup.Children.Add(rotateTransform);
				TranslateTransform translateTransform = new TranslateTransform();
				translateTransform.X = (int)(ScaleRadius * Math.Cos(num5));
				translateTransform.Y = (int)(ScaleRadius * Math.Sin(num5));
				TranslateTransform translateTransform2 = new TranslateTransform();
				translateTransform2.X = (int)(ScaleLabelRadius * Math.Cos(num5));
				translateTransform2.Y = (int)(ScaleLabelRadius * Math.Sin(num5));
				TextBlock textBlock = new TextBlock();
				textBlock.Height = ScaleLabelSize.Height;
				textBlock.Width = ScaleLabelSize.Width;
				textBlock.FontSize = ScaleLabelFontSize;
				textBlock.Foreground = new SolidColorBrush(ScaleLabelForeground);
				textBlock.TextAlignment = TextAlignment.Center;
				textBlock.VerticalAlignment = VerticalAlignment.Center;
				textBlock.HorizontalAlignment = HorizontalAlignment.Center;
				if (!(Math.Round(value, ScaleValuePrecision) <= Math.Round(MaxValue, ScaleValuePrecision)))
				{
					break;
				}
				value = Math.Round(value, ScaleValuePrecision);
				textBlock.Text = value.ToString();
				value += num3;
				transformGroup.Children.Add(translateTransform);
				rectangle.RenderTransform = transformGroup;
				textBlock.RenderTransform = translateTransform2;
				rootGrid.Children.Add(rectangle);
				rootGrid.Children.Add(textBlock);
				double num6 = (num4 + num - num4) / MinorDivisionsCount;
				if (num4 < ScaleStartAngle + ScaleSweepAngle && Math.Round(value, ScaleValuePrecision) <= Math.Round(MaxValue, ScaleValuePrecision))
				{
					for (double num7 = num4 + num6; num7 < num4 + num; num7 += num6)
					{
						Rectangle rectangle2 = new Rectangle();
						rectangle2.Height = MinorTickSize.Height;
						rectangle2.Width = MinorTickSize.Width;
						rectangle2.Fill = new SolidColorBrush(MinorTickColor);
						rectangle2.HorizontalAlignment = HorizontalAlignment.Center;
						rectangle2.VerticalAlignment = VerticalAlignment.Center;
						rectangle2.RenderTransformOrigin = new Point(0.5, 0.5);
						TransformGroup transformGroup2 = new TransformGroup();
						RotateTransform rotateTransform2 = new RotateTransform();
						rotateTransform2.Angle = num7;
						transformGroup2.Children.Add(rotateTransform2);
						TranslateTransform translateTransform3 = new TranslateTransform();
						double num8 = num7 * Math.PI / 180.0;
						translateTransform3.X = (int)(ScaleRadius * Math.Cos(num8));
						translateTransform3.Y = (int)(ScaleRadius * Math.Sin(num8));
						transformGroup2.Children.Add(translateTransform3);
						rectangle2.RenderTransform = transformGroup2;
						rootGrid.Children.Add(rectangle2);
					}
				}
			}
		}

		private Point GetCircumferencePoint(double angle, double radius)
		{
			double num = angle * Math.PI / 180.0;
			return new Point(Radius + radius * Math.Cos(num), Radius + radius * Math.Sin(num));
		}

		private void DrawRangeIndicator()
		{
			double num = MaxValue - MinValue;
			double num2 = ScaleSweepAngle / num;
			double num3 = num2 * (OptimalRangeStartValue - MinValue);
			double num4 = num2 * (OptimalRangeEndValue - MinValue);
			double num5 = ScaleStartAngle + num3;
			double num6 = ScaleStartAngle + num4;
			arcradius1 = RangeIndicatorRadius + RangeIndicatorThickness;
			arcradius2 = RangeIndicatorRadius;
			double num7 = ScaleStartAngle + ScaleSweepAngle;
			rangeIndicatorBelow = DrawSegment(GetCircumferencePoint(ScaleStartAngle, arcradius1), GetCircumferencePoint(ScaleStartAngle, arcradius2), GetCircumferencePoint(num5, arcradius2), GetCircumferencePoint(num5, arcradius1), Math.Abs(num5 - ScaleStartAngle) > 180.0, BelowOptimalRangeColor);
			rangeIndicatorOptimal = DrawSegment(GetCircumferencePoint(num5, arcradius1), GetCircumferencePoint(num5, arcradius2), GetCircumferencePoint(num6, arcradius2), GetCircumferencePoint(num6, arcradius1), Math.Abs(num6 - num5) > 180.0, OptimalRangeColor);
			rangeIndicatorAbove = DrawSegment(GetCircumferencePoint(num6, arcradius1), GetCircumferencePoint(num6, arcradius2), GetCircumferencePoint(num7, arcradius2), GetCircumferencePoint(num7, arcradius1), Math.Abs(num7 - num6) > 180.0, AboveOptimalRangeColor);
		}

		private Path DrawSegment(Point p1, Point p2, Point p3, Point p4, bool reflexangle, Color clr)
		{
			PathSegmentCollection pathSegmentCollection = new PathSegmentCollection();
			pathSegmentCollection.Add(new LineSegment
			{
				Point = p2
			});
			pathSegmentCollection.Add(new ArcSegment
			{
				Size = new Size(arcradius2, arcradius2),
				Point = p3,
				SweepDirection = SweepDirection.Clockwise,
				IsLargeArc = reflexangle
			});
			pathSegmentCollection.Add(new LineSegment
			{
				Point = p4
			});
			pathSegmentCollection.Add(new ArcSegment
			{
				Size = new Size(arcradius1, arcradius1),
				Point = p1,
				SweepDirection = SweepDirection.Counterclockwise,
				IsLargeArc = reflexangle
			});
			Color color = ((!(clr == Colors.Transparent)) ? Colors.White : clr);
			rangeIndicator = new Path
			{
				StrokeLineJoin = PenLineJoin.Round,
				Stroke = new SolidColorBrush(color),
				Fill = new SolidColorBrush(clr),
				Opacity = 0.65,
				StrokeThickness = 0.25,
				Data = new PathGeometry
				{
					Figures = new PathFigureCollection
					{
						new PathFigure
						{
							IsClosed = true,
							StartPoint = p1,
							Segments = pathSegmentCollection
						}
					}
				}
			};
			rangeIndicator.SetValue(Panel.ZIndexProperty, 150);
			rootGrid.Children.Add(rangeIndicator);
			return rangeIndicator;
		}
	}
}
