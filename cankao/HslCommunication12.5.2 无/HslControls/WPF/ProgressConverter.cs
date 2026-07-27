using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class ProgressConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter == null)
			{
				return 100.0 - (double)value;
			}
			return 100.0 - (double)value + System.Convert.ToDouble(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return 100.0 - (double)value;
		}
	}
}
