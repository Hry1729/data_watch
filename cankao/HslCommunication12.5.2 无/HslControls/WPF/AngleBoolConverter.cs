using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class AngleBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter == null)
			{
				return ((bool)value) ? 35.0 : (-35.0);
			}
			return ((bool)value) ? System.Convert.ToDouble(parameter) : (0.0 - System.Convert.ToDouble(parameter));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}
}
