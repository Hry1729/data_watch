using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslHandValve : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty OpenEnableProperty = DependencyProperty.Register("OpenEnable", typeof(bool), typeof(HslHandValve), new PropertyMetadata(true));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslHandValve), new PropertyMetadata(Color.FromArgb(byte.MaxValue, 91, 92, 95)));

		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HslHandValve), new PropertyMetadata(""));

		public bool OpenEnable
		{
			get
			{
				return (bool)GetValue(OpenEnableProperty);
			}
			set
			{
				SetValue(OpenEnableProperty, value);
			}
		}

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

		public string Text
		{
			get
			{
				return (string)GetValue(TextProperty);
			}
			set
			{
				SetValue(TextProperty, value);
			}
		}

		public HslHandValve()
		{
			InitializeComponent();
		}
	}
}
