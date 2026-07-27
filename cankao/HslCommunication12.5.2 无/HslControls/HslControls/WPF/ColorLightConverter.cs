using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HslControls.WPF
{
	public class ColorLightConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Color color = (Color)value;
			int num = 40;
			if (parameter != null)
			{
				num = System.Convert.ToInt32(parameter);
			}
			return Color.FromRgb((byte)(color.R + (255 - color.R) * num / 100), (byte)(color.G + (255 - color.G) * num / 100), (byte)(color.B + (255 - color.B) * num / 100));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}
}
