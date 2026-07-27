using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslLanternSimple : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty LanternBackgroundProperty = DependencyProperty.Register("LanternBackground", typeof(Color), typeof(HslLanternSimple), new PropertyMetadata(Colors.Orange));

		public static readonly DependencyProperty UseGradientColorProperty = DependencyProperty.Register("UseGradientColor", typeof(bool), typeof(HslLanternSimple), new PropertyMetadata(false, UseGradientColorPropertyChangedCallback));

		public static readonly DependencyProperty CenterBackgroundProperty = DependencyProperty.Register("CenterBackground", typeof(Color), typeof(HslLanternSimple), new PropertyMetadata(Colors.WhiteSmoke));

		public Color LanternBackground
		{
			get
			{
				return (Color)GetValue(LanternBackgroundProperty);
			}
			set
			{
				SetValue(LanternBackgroundProperty, value);
			}
		}

		public bool UseGradientColor
		{
			get
			{
				return (bool)GetValue(UseGradientColorProperty);
			}
			set
			{
				SetValue(UseGradientColorProperty, value);
			}
		}

		public Color CenterBackground
		{
			get
			{
				return (Color)GetValue(CenterBackgroundProperty);
			}
			set
			{
				SetValue(CenterBackgroundProperty, value);
			}
		}

		public HslLanternSimple()
		{
			InitializeComponent();
		}

		public static void UseGradientColorPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslLanternSimple)dependency).ChangeUseGradientColor();
		}

		public void ChangeUseGradientColor()
		{
			if (UseGradientColor)
			{
				ellipse1.Fill = (Brush)FindResource("brush1");
			}
			else
			{
				ellipse1.Fill = (Brush)FindResource("brush2");
			}
		}
	}
}
