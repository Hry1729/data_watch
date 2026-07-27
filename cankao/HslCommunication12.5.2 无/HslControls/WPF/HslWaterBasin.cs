using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslWaterBasin : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty WaterSpeedProperty = DependencyProperty.Register("WaterSpeed", typeof(double), typeof(HslWaterBasin), new PropertyMetadata(5000.0));

		public static readonly DependencyProperty BubblesVisibilityProperty = DependencyProperty.Register("BubblesVisibility", typeof(Visibility), typeof(HslWaterBasin), new PropertyMetadata(Visibility.Visible));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(HslWaterBasin), new PropertyMetadata(80.0, ValuePropertyChangedCallback));

		public static readonly DependencyProperty WaterColorProperty = DependencyProperty.Register("WaterColor", typeof(Color), typeof(HslWaterBasin), new PropertyMetadata(Color.FromRgb(115, 135, 140)));

		private DoubleAnimation animation1 = null;

		private DoubleAnimation animation2 = null;

		private DoubleAnimation animation3 = null;

		private DoubleAnimation animation4 = null;

		private DoubleAnimation animation5 = null;

		private DoubleAnimation animation6 = null;

		private DoubleAnimation animation7 = null;

		private DoubleAnimation animation8 = null;

		private DoubleAnimation animation9 = null;

		private DoubleAnimation animation10 = null;

		private DoubleAnimation animation11 = null;

		private DoubleAnimation animation12 = null;

		private DoubleAnimation animation13 = null;

		public double WaterSpeed
		{
			get
			{
				return (double)GetValue(WaterSpeedProperty);
			}
			set
			{
				SetValue(WaterSpeedProperty, value);
			}
		}

		public Visibility BubblesVisibility
		{
			get
			{
				return (Visibility)GetValue(BubblesVisibilityProperty);
			}
			set
			{
				SetValue(BubblesVisibilityProperty, value);
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
				BeginAnimation(animation: new DoubleAnimation(value, TimeSpan.FromMilliseconds(200.0)), dp: ValueProperty);
			}
		}

		public Color WaterColor
		{
			get
			{
				return (Color)GetValue(WaterColorProperty);
			}
			set
			{
				SetValue(WaterColorProperty, value);
			}
		}

		public HslWaterBasin()
		{
			InitializeComponent();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			AnimationIni();
		}

		public void AnimationIni()
		{
			if(false)
			{
				animation1 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation1.RepeatBehavior = RepeatBehavior.Forever;
				animation1.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.2);
				path1.BeginAnimation(Canvas.TopProperty, animation1);
				animation2 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation2.RepeatBehavior = RepeatBehavior.Forever;
				animation2.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.8);
				path2.BeginAnimation(Canvas.TopProperty, animation2);
				animation3 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation3.RepeatBehavior = RepeatBehavior.Forever;
				animation3.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.1);
				path3.BeginAnimation(Canvas.TopProperty, animation3);
				animation4 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation4.RepeatBehavior = RepeatBehavior.Forever;
				animation4.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.7);
				path4.BeginAnimation(Canvas.TopProperty, animation4);
				animation5 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation5.RepeatBehavior = RepeatBehavior.Forever;
				animation5.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.4);
				path5.BeginAnimation(Canvas.TopProperty, animation5);
				animation6 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation6.RepeatBehavior = RepeatBehavior.Forever;
				animation6.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.8);
				path6.BeginAnimation(Canvas.TopProperty, animation6);
				animation7 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation7.RepeatBehavior = RepeatBehavior.Forever;
				animation7.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.3);
				path7.BeginAnimation(Canvas.TopProperty, animation7);
				animation8 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation8.RepeatBehavior = RepeatBehavior.Forever;
				animation8.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.9);
				path8.BeginAnimation(Canvas.TopProperty, animation8);
				animation9 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation9.RepeatBehavior = RepeatBehavior.Forever;
				animation9.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.95);
				path9.BeginAnimation(Canvas.TopProperty, animation9);
				animation10 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation10.RepeatBehavior = RepeatBehavior.Forever;
				animation10.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.25);
				path10.BeginAnimation(Canvas.TopProperty, animation10);
				animation11 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation11.RepeatBehavior = RepeatBehavior.Forever;
				animation11.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.75);
				path11.BeginAnimation(Canvas.TopProperty, animation11);
				animation12 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation12.RepeatBehavior = RepeatBehavior.Forever;
				animation12.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.35);
				path12.BeginAnimation(Canvas.TopProperty, animation12);
				animation13 = new DoubleAnimation(45.0, (100.0 - Value) / 2.0, TimeSpan.FromMilliseconds(WaterSpeed * Value / 100.0));
				animation13.RepeatBehavior = RepeatBehavior.Forever;
				animation13.BeginTime = TimeSpan.FromMilliseconds(WaterSpeed * 0.65);
				path13.BeginAnimation(Canvas.TopProperty, animation13);
			}
		}

		public static void ValuePropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslWaterBasin)dependency).ValueUpdate();
		}

		public void ValueUpdate()
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(95.0, 50.0), true, true);
				streamGeometryContext.LineTo(new Point(5.0, 50.0), true, false);
				streamGeometryContext.LineTo(new Point(5.0, (100.0 - Value) / 2.0), true, false);
				streamGeometryContext.LineTo(new Point(95.0, (100.0 - Value) / 2.0), true, false);
			}
			pathWater.Data = streamGeometry;
			Canvas.SetTop(path_out, (100.0 - Value) / 2.0 - path_out.Height);
			Canvas.SetTop(Group_Air_Bubbles, (100.0 - Value) / 2.0 - Group_Air_Bubbles.Height);
			AnimationIni();
		}
	}
}
