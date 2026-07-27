using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace HslControls.WPF
{
	public partial class HslBoiler : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty FireVisuableProperty = DependencyProperty.Register("FireVisuable", typeof(Visibility), typeof(HslBoiler), new PropertyMetadata(Visibility.Visible));

		public Visibility FireVisuable
		{
			get
			{
				return (Visibility)GetValue(FireVisuableProperty);
			}
			set
			{
				SetValue(FireVisuableProperty, value);
			}
		}

		public HslBoiler()
		{
			InitializeComponent();
		}
	}
}
