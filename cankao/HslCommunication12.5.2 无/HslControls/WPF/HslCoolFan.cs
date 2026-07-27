using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslCoolFan : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(HslCoolFan), new PropertyMetadata(0.0));

		private DoubleAnimation doubleAnimation;

		public static readonly DependencyProperty MoveSpeedProperty = DependencyProperty.Register("MoveSpeed", typeof(double), typeof(HslCoolFan), new PropertyMetadata(0.3, MoveSpeedDependencyPropertyChanged));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslCoolFan), new PropertyMetadata(Color.FromRgb(76, 76, 76)));

		public static readonly DependencyProperty CenterColorProperty = DependencyProperty.Register("CenterColor", typeof(Color), typeof(HslCoolFan), new PropertyMetadata(Color.FromRgb(91, 92, 95)));

		public double StartAngle
		{
			get
			{
				return (double)GetValue(StartAngleProperty);
			}
			set
			{
				SetValue(StartAngleProperty, value);
			}
		}

		public double MoveSpeed
		{
			get
			{
				return (double)GetValue(MoveSpeedProperty);
			}
			set
			{
				SetValue(MoveSpeedProperty, value);
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

		public HslCoolFan()
		{
			InitializeComponent();
			doubleAnimation = new DoubleAnimation(0.0, 360.0, TimeSpan.FromSeconds(3.33333));
			doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(StartAngleProperty, doubleAnimation);
		}

		public void UpdateMoveSpeed()
		{
			if(false)
			{
				if (MoveSpeed > 0.0)
				{
					doubleAnimation = new DoubleAnimation(0.0, 360.0, TimeSpan.FromSeconds(1.0 / MoveSpeed));
					doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
					BeginAnimation(StartAngleProperty, doubleAnimation);
				}
				else if (MoveSpeed < 0.0)
				{
					doubleAnimation = new DoubleAnimation(0.0, -360.0, TimeSpan.FromSeconds(1.0 / Math.Abs(MoveSpeed)));
					doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
					BeginAnimation(StartAngleProperty, doubleAnimation);
				}
				else
				{
					BeginAnimation(StartAngleProperty, null);
				}
			}
		}

		public static void MoveSpeedDependencyPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslCoolFan hslCoolFan)
			{
				hslCoolFan.UpdateMoveSpeed();
			}
		}
	}
}
