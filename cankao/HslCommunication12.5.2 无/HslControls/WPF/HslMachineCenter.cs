using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslMachineCenter : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty DoorOpenStatusProperty = DependencyProperty.Register("DoorOpenStatus", typeof(bool), typeof(HslMachineCenter), new PropertyMetadata(false));

		public static readonly DependencyProperty LightRedProperty = DependencyProperty.Register("LightRed", typeof(bool), typeof(HslMachineCenter), new PropertyMetadata(false, LightRedPropertyChanged));

		public static readonly DependencyProperty LightYellowProperty = DependencyProperty.Register("LightYellow", typeof(bool), typeof(HslMachineCenter), new PropertyMetadata(false, LightYellowPropertyChanged));

		public static readonly DependencyProperty LightGreenProperty = DependencyProperty.Register("LightGreen", typeof(bool), typeof(HslMachineCenter), new PropertyMetadata(false, LightGreenPropertyPropertyChanged));

		public bool DoorOpenStatus
		{
			get
			{
				return (bool)GetValue(DoorOpenStatusProperty);
			}
			set
			{
				SetValue(DoorOpenStatusProperty, value);
			}
		}

		public bool LightRed
		{
			get
			{
				return (bool)GetValue(LightRedProperty);
			}
			set
			{
				SetValue(LightRedProperty, value);
			}
		}

		public bool LightYellow
		{
			get
			{
				return (bool)GetValue(LightYellowProperty);
			}
			set
			{
				SetValue(LightYellowProperty, value);
			}
		}

		public bool LightGreen
		{
			get
			{
				return (bool)GetValue(LightGreenProperty);
			}
			set
			{
				SetValue(LightGreenProperty, value);
			}
		}

		public HslMachineCenter()
		{
			InitializeComponent();
		}

		public void LightRedUpdate()
		{
			if (LightRed)
			{
				light1.Fill = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, 0, 0));
			}
			else
			{
				light1.Fill = new SolidColorBrush(Color.FromArgb(32, byte.MaxValue, 0, 0));
			}
		}

		public static void LightRedPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslMachineCenter hslMachineCenter)
			{
				hslMachineCenter.LightRedUpdate();
			}
		}

		public void LightYellowUpdate()
		{
			if (LightYellow)
			{
				light2.Fill = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0));
			}
			else
			{
				light2.Fill = new SolidColorBrush(Color.FromArgb(32, byte.MaxValue, byte.MaxValue, 0));
			}
		}

		public static void LightYellowPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslMachineCenter hslMachineCenter)
			{
				hslMachineCenter.LightYellowUpdate();
			}
		}

		public void LightGreenPropertyUpdate()
		{
			if (LightGreen)
			{
				light3.Fill = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, byte.MaxValue, 0));
			}
			else
			{
				light3.Fill = new SolidColorBrush(Color.FromArgb(32, 0, byte.MaxValue, 0));
			}
		}

		public static void LightGreenPropertyPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslMachineCenter hslMachineCenter)
			{
				hslMachineCenter.LightGreenPropertyUpdate();
			}
		}
	}
}
