using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslIndicator : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty IndicatorColorProperty = DependencyProperty.Register("IndicatorColor", typeof(Color), typeof(HslIndicator), new PropertyMetadata(Colors.Green));

		public static readonly DependencyProperty IndicatorStylesProperty = DependencyProperty.Register("IndicatorStyles", typeof(IndicatorStyles), typeof(HslIndicator), new PropertyMetadata(IndicatorStyles.Ellipse));

		public static readonly DependencyProperty FrameColorProperty = DependencyProperty.Register("FrameColor", typeof(Color), typeof(HslIndicator), new PropertyMetadata(Colors.Gray));

		public Color IndicatorColor
		{
			get
			{
				return (Color)GetValue(IndicatorColorProperty);
			}
			set
			{
				SetValue(IndicatorColorProperty, value);
			}
		}

		public IndicatorStyles IndicatorStyles
		{
			get
			{
				return (IndicatorStyles)GetValue(IndicatorStylesProperty);
			}
			set
			{
				SetValue(IndicatorStylesProperty, value);
			}
		}

		public Color FrameColor
		{
			get
			{
				return (Color)GetValue(FrameColorProperty);
			}
			set
			{
				SetValue(FrameColorProperty, value);
			}
		}

		public HslIndicator()
		{
			InitializeComponent();
		}
	}
}
