using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class VisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter == null)
			{
				if (System.Convert.ToBoolean(value))
				{
					return Visibility.Visible;
				}
				return Visibility.Hidden;
			}
			if (System.Convert.ToBoolean(value))
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
