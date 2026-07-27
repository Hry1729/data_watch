using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HslControls.WPF
{
	public class SizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			double num = 0.0;
			Size size = (Size)value;
			if (parameter.ToString() == "Height")
			{
				num = size.Height;
			}
			else if (parameter.ToString() == "Width")
			{
				num = size.Width;
			}
			return num;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
