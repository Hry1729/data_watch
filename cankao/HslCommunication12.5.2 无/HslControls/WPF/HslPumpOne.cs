using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public partial class HslPumpOne : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty EntranceProperty = DependencyProperty.Register("Entrance", typeof(int), typeof(HslPumpOne), new PropertyMetadata(1, EntranceDependencyPropertyChanged));

		public static readonly DependencyProperty ExportProperty = DependencyProperty.Register("Export", typeof(int), typeof(HslPumpOne), new PropertyMetadata(4, ExportDependencyPropertyChanged));

		public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(HslPumpOne), new PropertyMetadata(0.0));

		private DoubleAnimation doubleAnimation;

		public static readonly DependencyProperty MoveSpeedProperty = DependencyProperty.Register("MoveSpeed", typeof(double), typeof(HslPumpOne), new PropertyMetadata(0.3, MoveSpeedDependencyPropertyChanged));

		public static readonly DependencyProperty Color1Property = DependencyProperty.Register("Color1", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(199, 205, 211)));

		public static readonly DependencyProperty Color2Property = DependencyProperty.Register("Color2", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(157, 164, 173)));

		public static readonly DependencyProperty Color3Property = DependencyProperty.Register("Color3", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(195, 200, 207)));

		public static readonly DependencyProperty Color4Property = DependencyProperty.Register("Color4", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(204, 208, 214)));

		public static readonly DependencyProperty Color5Property = DependencyProperty.Register("Color5", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(208, 213, 220)));

		public static readonly DependencyProperty Color6Property = DependencyProperty.Register("Color6", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(153, 160, 169)));

		public static readonly DependencyProperty Color7Property = DependencyProperty.Register("Color7", typeof(Color), typeof(HslPumpOne), new PropertyMetadata(Color.FromRgb(92, 100, 111)));

		public int Entrance
		{
			get
			{
				return (int)GetValue(EntranceProperty);
			}
			set
			{
				SetValue(EntranceProperty, value);
			}
		}

		public int Export
		{
			get
			{
				return (int)GetValue(ExportProperty);
			}
			set
			{
				SetValue(ExportProperty, value);
			}
		}

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

		public Color Color1
		{
			get
			{
				return (Color)GetValue(Color1Property);
			}
			set
			{
				SetValue(Color1Property, value);
			}
		}

		public Color Color2
		{
			get
			{
				return (Color)GetValue(Color2Property);
			}
			set
			{
				SetValue(Color2Property, value);
			}
		}

		public Color Color3
		{
			get
			{
				return (Color)GetValue(Color3Property);
			}
			set
			{
				SetValue(Color3Property, value);
			}
		}

		public Color Color4
		{
			get
			{
				return (Color)GetValue(Color4Property);
			}
			set
			{
				SetValue(Color4Property, value);
			}
		}

		public Color Color5
		{
			get
			{
				return (Color)GetValue(Color5Property);
			}
			set
			{
				SetValue(Color5Property, value);
			}
		}

		public Color Color6
		{
			get
			{
				return (Color)GetValue(Color6Property);
			}
			set
			{
				SetValue(Color6Property, value);
			}
		}

		public Color Color7
		{
			get
			{
				return (Color)GetValue(Color7Property);
			}
			set
			{
				SetValue(Color7Property, value);
			}
		}

		public HslPumpOne()
		{
			InitializeComponent();
			doubleAnimation = new DoubleAnimation(0.0, 360.0, TimeSpan.FromSeconds(3.3333333));
			doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(StartAngleProperty, doubleAnimation);
		}

		private void UpdatePipe(Rectangle rectangle1, Rectangle rectangle2, int dir)
		{
			if (dir > 0 && dir < 7)
			{
				switch (dir)
				{
				case 1:
					rectangle1.RenderTransform = null;
					rectangle2.RenderTransform = null;
					break;
				case 2:
					rectangle1.RenderTransform = new TranslateTransform(0.0, -35.0);
					rectangle2.RenderTransform = new TranslateTransform(0.0, -35.0);
					break;
				case 3:
				{
					TransformGroup transformGroup2 = new TransformGroup();
					transformGroup2.Children.Add(new RotateTransform(90.0));
					transformGroup2.Children.Add(new TranslateTransform(45.0, -55.0));
					rectangle1.RenderTransform = transformGroup2;
					transformGroup2 = new TransformGroup();
					transformGroup2.Children.Add(new RotateTransform(90.0));
					transformGroup2.Children.Add(new TranslateTransform(48.0, -52.0));
					rectangle2.RenderTransform = transformGroup2;
					break;
				}
				case 4:
				{
					TransformGroup transformGroup = new TransformGroup();
					transformGroup.Children.Add(new RotateTransform(90.0));
					transformGroup.Children.Add(new TranslateTransform(80.0, -55.0));
					transformGroup.Value.Translate(80.0, -55.0);
					rectangle1.RenderTransform = transformGroup;
					transformGroup = new TransformGroup();
					transformGroup.Children.Add(new RotateTransform(90.0));
					transformGroup.Children.Add(new TranslateTransform(83.0, -52.0));
					rectangle2.RenderTransform = transformGroup;
					break;
				}
				case 5:
					rectangle1.RenderTransform = new TranslateTransform(50.0, -35.0);
					rectangle2.RenderTransform = new TranslateTransform(97.0, -35.0);
					break;
				case 6:
					rectangle1.RenderTransform = new TranslateTransform(50.0, 0.0);
					rectangle2.RenderTransform = new TranslateTransform(97.0, 0.0);
					break;
				}
			}
		}

		public void UpdateEntrance()
		{
			UpdatePipe(rect1, rect2, Entrance);
		}

		public static void EntranceDependencyPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslPumpOne hslPumpOne)
			{
				hslPumpOne.UpdateEntrance();
			}
		}

		public void UpdateExport()
		{
			UpdatePipe(rect3, rect4, Export);
		}

		public static void ExportDependencyPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslPumpOne hslPumpOne)
			{
				hslPumpOne.UpdateExport();
			}
		}

		public void UpdateMoveSpeed()
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

		public static void MoveSpeedDependencyPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslPumpOne hslPumpOne)
			{
				hslPumpOne.UpdateMoveSpeed();
			}
		}
	}
}
