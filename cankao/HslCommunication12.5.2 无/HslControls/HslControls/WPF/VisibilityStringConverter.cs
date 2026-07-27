using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class VisibilityStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return Visibility.Hidden;
			}
			if (string.IsNullOrEmpty(value.ToString()))
			{
				return Visibility.Hidden;
			}
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}
}
