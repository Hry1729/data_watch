using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HslControls.WPF
{
	public class RangeIndicatorLightPositionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			double y = (double)value;
			TransformGroup transformGroup = new TransformGroup();
			RotateTransform value2 = new RotateTransform();
			TranslateTransform translateTransform = new TranslateTransform();
			translateTransform.Y = y;
			transformGroup.Children.Add(value2);
			transformGroup.Children.Add(translateTransform);
			return transformGroup;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
