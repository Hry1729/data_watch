using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HslControls.WPF
{
	public class ImageOffsetConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			double y = (double)value;
			TranslateTransform translateTransform = new TranslateTransform();
			translateTransform.Y = y;
			return translateTransform;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
