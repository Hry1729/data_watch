using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslArrow : System.Windows.Controls.UserControl, IComponentConnector
	{
		public static readonly DependencyProperty FactorXProperty = DependencyProperty.Register("FactorX", typeof(float), typeof(HslArrow), new PropertyMetadata(0.75f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty FactorYProperty = DependencyProperty.Register("FactorY", typeof(float), typeof(HslArrow), new PropertyMetadata(0.27f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty UseGradientProperty = DependencyProperty.Register("UseGradient", typeof(bool), typeof(HslArrow), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ArrowBothProperty = DependencyProperty.Register("ArrowBoth", typeof(bool), typeof(HslArrow), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(System.Windows.Media.Color), typeof(HslArrow), new PropertyMetadata(Colors.Gray, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ArrowBackColorProperty = DependencyProperty.Register("ArrowBackColor", typeof(System.Windows.Media.Color), typeof(HslArrow), new PropertyMetadata(Colors.DodgerBlue, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ArrowDirectionProperty = DependencyProperty.Register("ArrowDirection", typeof(ArrowDirection), typeof(HslArrow), new PropertyMetadata(ArrowDirection.Right, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HslArrow), new PropertyMetadata("HslArrow", RenderUpdateByPropertyChanged));

		public float FactorX
		{
			get
			{
				return (float)GetValue(FactorXProperty);
			}
			set
			{
				SetValue(FactorXProperty, value);
			}
		}

		public float FactorY
		{
			get
			{
				return (float)GetValue(FactorYProperty);
			}
			set
			{
				SetValue(FactorYProperty, value);
			}
		}

		public bool UseGradient
		{
			get
			{
				return (bool)GetValue(UseGradientProperty);
			}
			set
			{
				SetValue(UseGradientProperty, value);
			}
		}

		public bool ArrowBoth
		{
			get
			{
				return (bool)GetValue(ArrowBothProperty);
			}
			set
			{
				SetValue(ArrowBothProperty, value);
			}
		}

		public System.Windows.Media.Color EdgeColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(EdgeColorProperty);
			}
			set
			{
				SetValue(EdgeColorProperty, value);
			}
		}

		public System.Windows.Media.Color ArrowBackColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(ArrowBackColorProperty);
			}
			set
			{
				SetValue(ArrowBackColorProperty, value);
			}
		}

		public ArrowDirection ArrowDirection
		{
			get
			{
				return (ArrowDirection)GetValue(ArrowDirectionProperty);
			}
			set
			{
				SetValue(ArrowDirectionProperty, value);
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

		public HslArrow()
		{
			InitializeComponent();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslArrow hslArrow)
			{
				hslArrow.HslArrowUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslArrowUpdate();
		}

		public void HslArrowUpdate()
		{
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			System.Windows.Point[] array = null;
			RectangleF rectangle = default(RectangleF);
			float factorY = FactorY;
			float factorX = FactorX;
			switch (ArrowDirection)
			{
			case ArrowDirection.Right:
				if (!ArrowBoth)
				{
					array = new System.Windows.Point[8]
					{
						new System.Windows.Point(0.0, (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * factorX, num2 - 1),
						new System.Windows.Point((float)num * factorX, (float)num2 * (1f - factorY)),
						new System.Windows.Point(0.0, (float)num2 * (1f - factorY)),
						new System.Windows.Point(0.0, (float)num2 * factorY)
					};
					rectangle = new RectangleF(0f, (float)num2 * factorY, (float)num * (1f - (1f - factorX) / 2f), (float)num2 * (1f - factorY * 2f));
				}
				else
				{
					array = new System.Windows.Point[11]
					{
						new System.Windows.Point((float)num * factorX, (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * factorX, num2 - 1),
						new System.Windows.Point((float)num * factorX, (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * (1f - factorX), 0.0),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, (float)num2 * factorY)
					};
					rectangle = new RectangleF(0f, (float)num2 * factorY, num, (float)num2 * (1f - factorY * 2f));
				}
				break;
			case ArrowDirection.Left:
				if (!ArrowBoth)
				{
					array = new System.Windows.Point[8]
					{
						new System.Windows.Point(0.0, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * (1f - factorX), 0.0),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * factorY),
						new System.Windows.Point(num - 1, (float)num2 * factorY),
						new System.Windows.Point(num - 1, (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * 0.5f)
					};
					rectangle = new RectangleF((1f - factorX) / 2f * (float)num, (float)num2 * factorY, (float)num * (1f - (1f - factorX) / 2f), (float)num2 * (1f - factorY * 2f));
				}
				else
				{
					array = new System.Windows.Point[11]
					{
						new System.Windows.Point(0.0, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * (1f - factorX), 0.0),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, (float)num2 * factorY),
						new System.Windows.Point((float)num * factorX, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * 0.5f),
						new System.Windows.Point((float)num * factorX, num2 - 1),
						new System.Windows.Point((float)num * factorX, (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), (float)num2 * (1f - factorY)),
						new System.Windows.Point((float)num * (1f - factorX), num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * 0.5f)
					};
					rectangle = new RectangleF(0f, (float)num2 * factorY, num, (float)num2 * (1f - factorY * 2f));
				}
				break;
			case ArrowDirection.Up:
				if (!ArrowBoth)
				{
					array = new System.Windows.Point[8]
					{
						new System.Windows.Point((float)num * 0.5f, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), num2 - 1),
						new System.Windows.Point((float)num * factorY, num2 - 1),
						new System.Windows.Point((float)num * factorY, (float)num2 * (1f - factorX)),
						new System.Windows.Point(0.0, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * 0.5f, 0.0)
					};
					rectangle = new RectangleF((float)num * factorY, (float)num2 * (1f - factorX) / 2f, (float)num * (1f - factorY * 2f), (float)num2 * (1f - (1f - factorX) / 2f));
				}
				else
				{
					array = new System.Windows.Point[11]
					{
						new System.Windows.Point((float)num * 0.5f, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * factorX),
						new System.Windows.Point(num - 1, (float)num2 * factorX),
						new System.Windows.Point(num / 2, num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, (float)num2 * (1f - factorX)),
						new System.Windows.Point(0.0, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * 0.5f, 0.0)
					};
					rectangle = new RectangleF((float)num * factorY, 0f, (float)num * (1f - factorY * 2f), num2);
				}
				break;
			case ArrowDirection.Down:
				if (!ArrowBoth)
				{
					array = new System.Windows.Point[8]
					{
						new System.Windows.Point((float)num * (1f - factorY), 0.0),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * factorX),
						new System.Windows.Point(num - 1, (float)num2 * factorX),
						new System.Windows.Point(num / 2, num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, 0.0),
						new System.Windows.Point((float)num * (1f - factorY), 0.0)
					};
					rectangle = new RectangleF((float)num * factorY, 0f, (float)num * (1f - factorY * 2f), (float)num2 * (1f - (1f - factorX) / 2f));
				}
				else
				{
					array = new System.Windows.Point[11]
					{
						new System.Windows.Point((float)num * 0.5f, 0.0),
						new System.Windows.Point(num - 1, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * (1f - factorY), (float)num2 * factorX),
						new System.Windows.Point(num - 1, (float)num2 * factorX),
						new System.Windows.Point(num / 2, num2 - 1),
						new System.Windows.Point(0.0, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, (float)num2 * factorX),
						new System.Windows.Point((float)num * factorY, (float)num2 * (1f - factorX)),
						new System.Windows.Point(0.0, (float)num2 * (1f - factorX)),
						new System.Windows.Point((float)num * 0.5f, 0.0)
					};
					rectangle = new RectangleF((float)num * factorY, 0f, (float)num * (1f - factorY * 2f), num2);
				}
				break;
			}
			if (array == null)
			{
				return;
			}
			if (UseGradient)
			{
				if (ArrowDirection == ArrowDirection.Up || ArrowDirection == ArrowDirection.Down)
				{
					WpfUtils.FillPolygon(brush: new LinearGradientBrush(new GradientStopCollection(new GradientStop[3]
					{
						new GradientStop(ArrowBackColor, 0.0),
						new GradientStop(HslHelper.GetColorLight(ArrowBackColor), 0.5),
						new GradientStop(ArrowBackColor, 1.0)
					}), new System.Windows.Point(0.0, 0.0), new System.Windows.Point(1.0, 0.0)), canvas: canvas1, points: array);
				}
				else
				{
					WpfUtils.FillPolygon(brush: new LinearGradientBrush(new GradientStopCollection(new GradientStop[3]
					{
						new GradientStop(ArrowBackColor, 0.0),
						new GradientStop(HslHelper.GetColorLight(ArrowBackColor), 0.5),
						new GradientStop(ArrowBackColor, 1.0)
					}), new System.Windows.Point(0.0, 0.0), new System.Windows.Point(0.0, 1.0)), canvas: canvas1, points: array);
				}
			}
			else
			{
				WpfUtils.FillPolygon(canvas1, array, ArrowBackColor);
			}
			WpfUtils.DrawLines(canvas1, array, EdgeColor, 1.0);
			WpfUtils.DrawString(canvas1, Text, base.Foreground, rectangle, System.Windows.HorizontalAlignment.Center, VerticalAlignment.Center);
		}
	}
}
