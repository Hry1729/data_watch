using System;
using System.Globalization;
using System.Windows.Data;

namespace HslControls.WPF
{
	[ValueConversion(typeof(double), typeof(string))]
	internal class Double2StringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return "-";
			}
			double num = (double)value;
			if (parameter == null || string.IsNullOrEmpty(parameter.ToString()))
			{
				return (num == double.NaN) ? "-" : $"{num:F1}";
			}
			return (num == double.NaN) ? "-" : string.Format("{0:" + parameter.ToString() + "}", num);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
