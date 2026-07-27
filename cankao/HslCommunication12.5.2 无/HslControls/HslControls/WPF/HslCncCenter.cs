using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslCncCenter : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty CncDoorStateProperty = DependencyProperty.Register("CncDoorState", typeof(double), typeof(HslCncCenter), new PropertyMetadata(0.0));

		public static readonly DependencyProperty LightRedProperty = DependencyProperty.Register("LightRed", typeof(bool), typeof(HslCncCenter), new PropertyMetadata(false, LightRedPropertyChanged));

		public static readonly DependencyProperty LightYellowProperty = DependencyProperty.Register("LightYellow", typeof(bool), typeof(HslCncCenter), new PropertyMetadata(false, LightYellowPropertyChanged));

		public static readonly DependencyProperty LightGreenProperty = DependencyProperty.Register("LightGreen", typeof(bool), typeof(HslCncCenter), new PropertyMetadata(false, LightGreenPropertyPropertyChanged));

		public double CncDoorState
		{
			get
			{
				return (double)GetValue(CncDoorStateProperty);
			}
			set
			{
				SetValue(CncDoorStateProperty, value);
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

		public HslCncCenter()
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
			if (dependency is HslCncCenter hslCncCenter)
			{
				hslCncCenter.LightRedUpdate();
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
			if (dependency is HslCncCenter hslCncCenter)
			{
				hslCncCenter.LightYellowUpdate();
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
			if (dependency is HslCncCenter hslCncCenter)
			{
				hslCncCenter.LightGreenPropertyUpdate();
			}
		}

		public void SetDoorState(double doorState, double time)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation();
			doubleAnimation.To = doorState;
			doubleAnimation.Duration = TimeSpan.FromMilliseconds(time);
			doubleAnimation.EasingFunction = new CircleEase
			{
				EasingMode = EasingMode.EaseInOut
			};
			BeginAnimation(CncDoorStateProperty, doubleAnimation);
		}

		public void SetDoorState(double doorState)
		{
			SetDoorState(doorState, 1000.0);
		}
	}
}
