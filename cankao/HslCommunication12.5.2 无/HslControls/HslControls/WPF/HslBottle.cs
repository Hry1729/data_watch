using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslBottle : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty BackColorEdgeProperty = DependencyProperty.Register("BackColorEdge", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(142, 196, 216)));

		public static readonly DependencyProperty BackColorTopProperty = DependencyProperty.Register("BackColorTop", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(151, 232, 244)));

		public static readonly DependencyProperty BackColorCenterProperty = DependencyProperty.Register("BackColorCenter", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(240, 240, 240)));

		public static readonly DependencyProperty ForeColorEdgeProperty = DependencyProperty.Register("ForeColorEdge", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(194, 190, 77)));

		public static readonly DependencyProperty ForeColorTopProperty = DependencyProperty.Register("ForeColorTop", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(243, 245, 139)));

		public static readonly DependencyProperty ForeColorCenterProperty = DependencyProperty.Register("ForeColorCenter", typeof(Color), typeof(HslBottle), new PropertyMetadata(Color.FromRgb(226, 221, 98)));

		public static readonly DependencyProperty HeadTagProperty = DependencyProperty.Register("HeadTag", typeof(string), typeof(HslBottle), new PropertyMetadata("原料1"));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(HslBottle), new PropertyMetadata(50.0));

		public static readonly DependencyProperty BottleTagProperty = DependencyProperty.Register("BottleTag", typeof(string), typeof(HslBottle), new PropertyMetadata("1#"));

		public static readonly DependencyProperty UseAnimationProperty = DependencyProperty.Register("UseAnimation", typeof(bool), typeof(HslBottle), new PropertyMetadata(true));

		public Color BackColorEdge
		{
			get
			{
				return (Color)GetValue(BackColorEdgeProperty);
			}
			set
			{
				SetValue(BackColorEdgeProperty, value);
			}
		}

		public Color BackColorTop
		{
			get
			{
				return (Color)GetValue(BackColorTopProperty);
			}
			set
			{
				SetValue(BackColorTopProperty, value);
			}
		}

		public Color BackColorCenter
		{
			get
			{
				return (Color)GetValue(BackColorCenterProperty);
			}
			set
			{
				SetValue(BackColorCenterProperty, value);
			}
		}

		public Color ForeColorEdge
		{
			get
			{
				return (Color)GetValue(ForeColorEdgeProperty);
			}
			set
			{
				SetValue(ForeColorEdgeProperty, value);
			}
		}

		public Color ForeColorTop
		{
			get
			{
				return (Color)GetValue(ForeColorTopProperty);
			}
			set
			{
				SetValue(ForeColorTopProperty, value);
			}
		}

		public Color ForeColorCenter
		{
			get
			{
				return (Color)GetValue(ForeColorCenterProperty);
			}
			set
			{
				SetValue(ForeColorCenterProperty, value);
			}
		}

		public string HeadTag
		{
			get
			{
				return (string)GetValue(HeadTagProperty);
			}
			set
			{
				SetValue(HeadTagProperty, value);
			}
		}

		public double Value
		{
			get
			{
				return (double)GetValue(ValueProperty);
			}
			set
			{
				if (value < 0.0)
				{
					value = 0.0;
				}
				if (value > 100.0)
				{
					value = 100.0;
				}
				if (UseAnimation)
				{
					BeginAnimation(animation: new DoubleAnimation(value, TimeSpan.FromMilliseconds(200.0)), dp: ValueProperty);
					return;
				}
				SetValue(ValueProperty, value);
			}
		}

		public string BottleTag
		{
			get
			{
				return (string)GetValue(BottleTagProperty);
			}
			set
			{
				SetValue(BottleTagProperty, value);
			}
		}

		public bool UseAnimation
		{
			get
			{
				return (bool)GetValue(UseAnimationProperty);
			}
			set
			{
				SetValue(UseAnimationProperty, value);
			}
		}

		public HslBottle()
		{
			InitializeComponent();
		}
	}
}
