using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	[DefaultEvent("OnSwitchChanged")]
	public partial class HslSwitch : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty SwitchBackgroundProperty = DependencyProperty.Register("SwitchBackground", typeof(Color), typeof(HslSwitch), new PropertyMetadata(Colors.DimGray));

		public static readonly DependencyProperty SwitchForegroundProperty = DependencyProperty.Register("SwitchForeground", typeof(Color), typeof(HslSwitch), new PropertyMetadata(Color.FromRgb(36, 36, 36)));

		public static readonly DependencyProperty SwitchInfoColorProperty = DependencyProperty.Register("SwitchInfoColor", typeof(Color), typeof(HslSwitch), new PropertyMetadata(Colors.Red));

		public static readonly DependencyProperty SwitchStatusProperty = DependencyProperty.Register("SwitchStatus", typeof(bool), typeof(HslSwitch), new PropertyMetadata(false, SwitchStatusPropertyChangedCallback));

		public static readonly DependencyProperty SwitchTextProperty = DependencyProperty.Register("SwitchText", typeof(string), typeof(HslSwitch), new PropertyMetadata("Off:On", SwitchTextPropertyChangedCallback));

		public static readonly DependencyProperty SwitchAngleProperty = DependencyProperty.Register("SwitchAngle", typeof(double), typeof(HslSwitch), new PropertyMetadata(-35.0));

		public Color SwitchBackground
		{
			get
			{
				return (Color)GetValue(SwitchBackgroundProperty);
			}
			set
			{
				SetValue(SwitchBackgroundProperty, value);
			}
		}

		public Color SwitchForeground
		{
			get
			{
				return (Color)GetValue(SwitchForegroundProperty);
			}
			set
			{
				SetValue(SwitchForegroundProperty, value);
			}
		}

		public Color SwitchInfoColor
		{
			get
			{
				return (Color)GetValue(SwitchInfoColorProperty);
			}
			set
			{
				SetValue(SwitchInfoColorProperty, value);
			}
		}

		public bool SwitchStatus
		{
			get
			{
				return (bool)GetValue(SwitchStatusProperty);
			}
			set
			{
				SetValue(SwitchStatusProperty, value);
				this.OnSwitchChanged?.Invoke(this, value);
			}
		}

		public string SwitchText
		{
			get
			{
				return (string)GetValue(SwitchTextProperty);
			}
			set
			{
				SetValue(SwitchTextProperty, value);
			}
		}

		public double SwitchAngle
		{
			get
			{
				return (double)GetValue(SwitchAngleProperty);
			}
			set
			{
				SetValue(SwitchAngleProperty, value);
			}
		}

		[Category("Action")]
		[Description("点击了按钮开发后触发")]
		public event Action<object, bool> OnSwitchChanged;

		public HslSwitch()
		{
			InitializeComponent();
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			SwitchStatus = !SwitchStatus;
		}

		public static void SwitchStatusPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if ((bool)e.NewValue)
			{
				dependency.SetValue(SwitchInfoColorProperty, Colors.LimeGreen);
				HslSwitch hslSwitch = (HslSwitch)dependency;
				hslSwitch.BeginAnimation(animation: new DoubleAnimation(35.0, TimeSpan.FromMilliseconds(100.0)), dp: SwitchAngleProperty);
				hslSwitch.UpdateText();
			}
			else
			{
				dependency.SetValue(SwitchInfoColorProperty, Colors.Tomato);
				HslSwitch hslSwitch2 = (HslSwitch)dependency;
				hslSwitch2.BeginAnimation(animation: new DoubleAnimation(-35.0, TimeSpan.FromMilliseconds(100.0)), dp: SwitchAngleProperty);
				hslSwitch2.UpdateText();
			}
		}

		public static void SwitchTextPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslSwitch)dependency).UpdateText();
		}

		public void UpdateText()
		{
			if (string.IsNullOrEmpty(SwitchText))
			{
				textBlock1.Text = string.Empty;
				return;
			}
			string[] array = SwitchText.Split(new char[1] { ':' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 1)
			{
				textBlock1.Text = array[0];
			}
			else
			{
				textBlock1.Text = (SwitchStatus ? array[1] : array[0]);
			}
		}

		public void ChangeSwitchStatus(bool status)
		{
			SetValue(SwitchStatusProperty, status);
		}
	}
}
