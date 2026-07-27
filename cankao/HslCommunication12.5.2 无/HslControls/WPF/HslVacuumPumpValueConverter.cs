using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class HslVacuumPumpValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return System.Convert.ToDouble(value) * -1.0 - 3.0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}
}
