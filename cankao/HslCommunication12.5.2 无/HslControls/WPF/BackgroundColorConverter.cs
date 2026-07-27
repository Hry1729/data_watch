using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HslControls.WPF
{
	public class BackgroundColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Color color = (Color)value;
			RadialGradientBrush radialGradientBrush = new RadialGradientBrush();
			GradientStop gradientStop = new GradientStop();
			gradientStop.Offset = 0.982;
			gradientStop.Color = color;
			GradientStop gradientStop2 = new GradientStop();
			gradientStop2.Color = Color.FromArgb(byte.MaxValue, 175, 178, 176);
			radialGradientBrush.GradientStops.Add(gradientStop);
			radialGradientBrush.GradientStops.Add(gradientStop2);
			return radialGradientBrush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
