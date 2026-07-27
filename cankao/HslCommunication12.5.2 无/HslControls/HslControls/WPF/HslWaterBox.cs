using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public partial class HslWaterBox : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HslWaterBox), new FrameworkPropertyMetadata("HslWaterBox", FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty WaterValueProperty = DependencyProperty.Register("WaterValue", typeof(double), typeof(HslWaterBox), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty EdgeWidthProperty = DependencyProperty.Register("EdgeWidth", typeof(double), typeof(HslWaterBox), new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty WaterColorProperty = DependencyProperty.Register("WaterColor", typeof(Color), typeof(HslWaterBox), new FrameworkPropertyMetadata(Color.FromRgb(207, 235, 246), FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslWaterBox), new FrameworkPropertyMetadata(Color.FromRgb(253, 233, 208), FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(Color), typeof(HslWaterBox), new FrameworkPropertyMetadata(Color.FromRgb(234, 205, 152), FrameworkPropertyMetadataOptions.AffectsRender));

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

		public double WaterValue
		{
			get
			{
				return (double)GetValue(WaterValueProperty);
			}
			set
			{
				SetValue(WaterValueProperty, value);
			}
		}

		public double EdgeWidth
		{
			get
			{
				return (double)GetValue(EdgeWidthProperty);
			}
			set
			{
				SetValue(EdgeWidthProperty, value);
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

		public HslWaterBox()
		{
			InitializeComponent();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			UpdateHslWaterBox();
		}

		public void SetWaterValue(double value, double time = 300.0)
		{
			BeginAnimation(animation: new DoubleAnimation(value, TimeSpan.FromMilliseconds(time)), dp: WaterValueProperty);
		}

		public void UpdateHslWaterBox()
		{
			if (Authorization.iashdiadasbdnajsdhjaf())
			{
				canvas1.Children.Clear();
				double num = ((WaterValue < 0.0) ? 0.0 : ((WaterValue > 100.0) ? 100.0 : WaterValue));
				if (!(base.ActualHeight < EdgeWidth))
				{
					Rectangle rectangle = new Rectangle();
					rectangle.Width = base.ActualWidth;
					rectangle.Height = num / 100.0 * (base.ActualHeight - EdgeWidth);
					canvas1.Children.Add(rectangle);
					rectangle.Fill = new SolidColorBrush(WaterColor);
					Canvas.SetTop(rectangle, base.ActualHeight - rectangle.Height - EdgeWidth);
					Polygon polygon = new Polygon();
					polygon.Points = new PointCollection(new Point[9]
					{
						new Point(0.0, 0.0),
						new Point(0.0, base.ActualHeight),
						new Point(base.ActualWidth, base.ActualHeight),
						new Point(base.ActualWidth, 0.0),
						new Point(base.ActualWidth - EdgeWidth, 0.0),
						new Point(base.ActualWidth - EdgeWidth, base.ActualHeight - EdgeWidth),
						new Point(EdgeWidth, base.ActualHeight - EdgeWidth),
						new Point(EdgeWidth, 0.0),
						new Point(0.0, 0.0)
					});
					polygon.StrokeThickness = 1.0;
					polygon.Stroke = new SolidColorBrush(BorderColor);
					polygon.Fill = new SolidColorBrush(EdgeColor);
					canvas1.Children.Add(polygon);
					Label label = new Label();
					label.Width = base.ActualWidth;
					label.Height = base.ActualHeight - EdgeWidth;
					label.HorizontalContentAlignment = HorizontalAlignment.Center;
					label.VerticalContentAlignment = VerticalAlignment.Center;
					label.Content = Text;
					canvas1.Children.Add(label);
				}
			}
		}
	}
}
