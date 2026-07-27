using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class GlassEffectWidthConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (double)value * 2.0 * 0.94;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
