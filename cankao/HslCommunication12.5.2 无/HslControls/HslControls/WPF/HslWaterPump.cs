using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HslControls.WPF
{
	public partial class HslWaterPump : UserControl, IComponentConnector
	{
		public enum MotorColor
		{
			Gray,
			Green
		}

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(bool), typeof(HslWaterPump), new PropertyMetadata(false, ValuePropertyChanged));

		public static DependencyProperty MotorColorsProperty = DependencyProperty.Register("MotorColors", typeof(MotorColor), typeof(HslWaterPump), new FrameworkPropertyMetadata(MotorColor.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

		private ImageSource imageSource;

		[Category("HMI")]
		public bool Value
		{
			get
			{
				return (bool)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		[Category("HMI")]
		[Browsable(false)]
		public MotorColor MotorColors
		{
			get
			{
				return (MotorColor)GetValue(MotorColorsProperty);
			}
			set
			{
				SetValue(MotorColorsProperty, value);
			}
		}

		public HslWaterPump()
		{
			InitializeComponent();
		}

		private static void ValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			HslWaterPump hslWaterPump = (HslWaterPump)d;
			if ((bool)e.NewValue)
			{
				hslWaterPump.MotorColors = MotorColor.Green;
			}
			else
			{
				hslWaterPump.MotorColors = MotorColor.Gray;
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			double actualWidth = base.ActualWidth;
			double actualHeight = base.ActualHeight;
			switch (MotorColors)
			{
			case MotorColor.Gray:
				imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/RSCorPumpOff.png"));
				drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
				break;
			case MotorColor.Green:
				imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/RSCorPumpOn.png"));
				drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
				break;
			default:
				imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/RSCorPumpOff.png"));
				drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
				break;
			}
		}
	}
}
