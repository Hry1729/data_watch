using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class AdditionValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter == null)
			{
				return value;
			}
			return System.Convert.ToDouble(value) + System.Convert.ToDouble(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}
}
