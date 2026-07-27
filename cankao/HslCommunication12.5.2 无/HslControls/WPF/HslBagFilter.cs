using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslBagFilter : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslBagFilter), new PropertyMetadata(Color.FromRgb(176, 176, 176)));

		public static readonly DependencyProperty CenterColorProperty = DependencyProperty.Register("CenterColor", typeof(Color), typeof(HslBagFilter), new PropertyMetadata(Color.FromRgb(229, 229, 229)));

		public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(Color), typeof(HslBagFilter), new PropertyMetadata(Color.FromRgb(150, 150, 150)));

		public Color EdgeColor
		{
			get
			{
				return (Color)GetValue(EdgeColorProperty);
			}
			set
			{
				SetValue(EdgeColorProperty, value);
			}
		}

		public Color CenterColor
		{
			get
			{
				return (Color)GetValue(CenterColorProperty);
			}
			set
			{
				SetValue(CenterColorProperty, value);
			}
		}

		public Color BorderColor
		{
			get
			{
				return (Color)GetValue(BorderColorProperty);
			}
			set
			{
				SetValue(BorderColorProperty, value);
			}
		}

		public HslBagFilter()
		{
			InitializeComponent();
		}
	}
}
