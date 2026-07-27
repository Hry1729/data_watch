using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslArcGauge : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty GaugeTitleProperty = DependencyProperty.Register("GaugeTitle", typeof(string), typeof(HslArcGauge), new PropertyMetadata(string.Empty));

		public static readonly DependencyProperty GaugeUnitProperty = DependencyProperty.Register("GaugeUnit", typeof(string), typeof(HslArcGauge), new PropertyMetadata(string.Empty));

		public static readonly DependencyProperty MaxProperty = DependencyProperty.Register("Max", typeof(double), typeof(HslArcGauge), new PropertyMetadata(100.0, Rerender));

		public static readonly DependencyProperty MinProperty = DependencyProperty.Register("Min", typeof(double), typeof(HslArcGauge), new PropertyMetadata(0.0, Rerender));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(HslArcGauge), new PropertyMetadata(0.0, Rerender));

		public static readonly DependencyProperty SettingValueProperty = DependencyProperty.Register("SettingValue", typeof(double), typeof(HslArcGauge), new PropertyMetadata(0.0, Rerender));

		public static readonly DependencyProperty GaugeColorProperty = DependencyProperty.Register("GaugeColor", typeof(Brush), typeof(HslArcGauge), new PropertyMetadata(Brushes.Gray));

		public static readonly DependencyProperty SettingGaugeColorProperty = DependencyProperty.Register("SettingGaugeColor", typeof(Brush), typeof(HslArcGauge), new PropertyMetadata(Brushes.Red));

		public static readonly DependencyProperty ArcGaugeFormatProperty = DependencyProperty.Register("ArcGaugeFormat", typeof(string), typeof(HslArcGauge), new PropertyMetadata(string.Empty, RerenderBinding));

		private readonly double FULLRANGE_ANGLE = 270.0;

		private readonly double START_ANGLE_OFFSET = 90.0;

		[Category("HslControls")]
		public string GaugeTitle
		{
			get
			{
				return (string)GetValue(GaugeTitleProperty);
			}
			set
			{
				SetValue(GaugeTitleProperty, value);
			}
		}

		[Category("HslControls")]
		public string GaugeUnit
		{
			get
			{
				return (string)GetValue(GaugeUnitProperty);
			}
			set
			{
				SetValue(GaugeUnitProperty, value);
			}
		}

		[Category("HslControls")]
		public double Max
		{
			get
			{
				return (double)GetValue(MaxProperty);
			}
			set
			{
				SetValue(MaxProperty, value);
			}
		}

		[Category("HslControls")]
		public double Min
		{
			get
			{
				return (double)GetValue(MinProperty);
			}
			set
			{
				SetValue(MinProperty, value);
			}
		}

		[Category("HslControls")]
		public double Value
		{
			get
			{
				return (double)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		[Category("HslControls")]
		public double SettingValue
		{
			get
			{
				return (double)GetValue(SettingValueProperty);
			}
			set
			{
				SetValue(SettingValueProperty, value);
			}
		}

		[Category("HslControls")]
		public Brush GaugeColor
		{
			get
			{
				return (Brush)GetValue(GaugeColorProperty);
			}
			set
			{
				SetValue(GaugeColorProperty, value);
			}
		}

		[Category("HslControls")]
		public Brush SettingGaugeColor
		{
			get
			{
				return (Brush)GetValue(SettingGaugeColorProperty);
			}
			set
			{
				SetValue(SettingGaugeColorProperty, value);
			}
		}

		[Category("HslControls")]
		public string ArcGaugeFormat
		{
			get
			{
				return (string)GetValue(ArcGaugeFormatProperty);
			}
			set
			{
				SetValue(ArcGaugeFormatProperty, value);
			}
		}

		public void ReBindGaugeValue()
		{
			if (!string.IsNullOrEmpty(ArcGaugeFormat))
			{
				Binding binding = new Binding();
				binding.Path = new PropertyPath("Value");
				binding.Converter = (Double2StringConverter)base.Resources["Double2Str"];
				binding.ConverterParameter = ArcGaugeFormat;
				textBlockValue.SetBinding(TextBlock.TextProperty, binding);
				Binding binding2 = new Binding();
				binding2.Path = new PropertyPath("SettingValue");
				binding2.Converter = (Double2StringConverter)base.Resources["Double2Str"];
				binding2.ConverterParameter = ArcGaugeFormat;
				textBlockSettingValue.SetBinding(TextBlock.TextProperty, binding2);
			}
		}

		private static void RerenderBinding(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as HslArcGauge).ReBindGaugeValue();
		}

		private static void Rerender(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as HslArcGauge).RenderGauge();
		}

		public HslArcGauge()
		{
			InitializeComponent();
			RenderGauge();
		}

		private void RenderGauge()
		{
			Point point;
			if (Value >= Max)
			{
				point = new Point(320.0, 160.0);
				arcGauge.IsLargeArc = true;
			}
			else if (Value <= Min)
			{
				point = new Point(160.0, 320.0);
				arcGauge.IsLargeArc = false;
			}
			else
			{
				double num = FULLRANGE_ANGLE / (Max - Min) * (Value - Min);
				double num2 = Math.PI / 180.0 * (num + START_ANGLE_OFFSET);
				point = new Point(160.0 * Math.Cos(num2) + 160.0, 160.0 * Math.Sin(num2) + 160.0);
				arcGauge.IsLargeArc = num > 180.0;
			}
			arcGauge.Point = point;
			if (SettingValue >= Max)
			{
				RotateTransform rotateTransform = new RotateTransform();
				rotateTransform.Angle = -90.0;
				ptSetup.RenderTransform = rotateTransform;
				return;
			}
			if (SettingValue <= Min)
			{
				RotateTransform rotateTransform2 = new RotateTransform();
				rotateTransform2.Angle = 0.0;
				ptSetup.RenderTransform = rotateTransform2;
				return;
			}
			double num3 = FULLRANGE_ANGLE / (Max - Min) * (SettingValue - Min);
			if (num3 <= 180.0)
			{
				RotateTransform rotateTransform3 = new RotateTransform();
				rotateTransform3.Angle = num3;
				ptSetup.RenderTransform = rotateTransform3;
			}
			else
			{
				RotateTransform rotateTransform4 = new RotateTransform();
				rotateTransform4.Angle = -360.0 + num3;
				ptSetup.RenderTransform = rotateTransform4;
			}
		}
	}
}
