using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HslControls.WPF
{
	public partial class HslMotorTwo : UserControl, IComponentConnector
	{
		public enum MotorColor
		{
			Gray,
			Green,
			Red,
			Yellow
		}

		public static DependencyProperty MotorColorsProperty = DependencyProperty.Register("MotorColors", typeof(MotorColor), typeof(HslMotorTwo), new FrameworkPropertyMetadata(MotorColor.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(bool), typeof(HslMotorTwo), new PropertyMetadata(false));

		public static readonly DependencyProperty PLCAddressValueProperty = DependencyProperty.Register("PLCAddressValue", typeof(string), typeof(HslMotorTwo), new FrameworkPropertyMetadata("0"));

		private ImageSource imageSource;

		[Category("HMI")]
		public MotorColor MotorColors
		{
			get
			{
				return (MotorColor)GetValue(MotorColorsProperty);
			}
			set
			{
				SetValue(MotorColorsProperty, value);
			}
		}

		public HslMotorTwo()
		{
			InitializeComponent();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (Authorization.iashdiadasbdnajsdhjaf())
			{
				double actualWidth = base.ActualWidth;
				double actualHeight = base.ActualHeight;
				switch (MotorColors)
				{
				case MotorColor.Gray:
					imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/MotorGray.png"));
					drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
					break;
				case MotorColor.Green:
					imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/MotorGreen.png"));
					drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
					break;
				case MotorColor.Red:
					imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/MotorRed.png"));
					drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
					break;
				case MotorColor.Yellow:
					imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/MotorYellow.png"));
					drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
					break;
				default:
					imageSource = new BitmapImage(new Uri("pack://application:,,,/HslControls;component/Properties/../images/MotorGray.png"));
					drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, actualWidth, actualHeight));
					break;
				}
			}
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			if (!((LicenseManager.UsageMode == LicenseUsageMode.Designtime) | (Process.GetCurrentProcess().ProcessName == "devenv")))
			{
			}
		}
	}
}
