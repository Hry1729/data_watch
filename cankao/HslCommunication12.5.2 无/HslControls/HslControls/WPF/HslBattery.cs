using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslBattery : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty BatteryBackColorProperty = DependencyProperty.Register("BatteryBackEdgeColor", typeof(Color), typeof(HslBattery), new PropertyMetadata(Color.FromRgb(142, 196, 216), RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(float), typeof(HslBattery), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsTextRenderProperty = DependencyProperty.Register("IsTextRender", typeof(bool), typeof(HslBattery), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMaxProperty = DependencyProperty.Register("ValueMax", typeof(float), typeof(HslBattery), new PropertyMetadata(100f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMinProperty = DependencyProperty.Register("ValueMin", typeof(float), typeof(HslBattery), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register("StringFormat", typeof(string), typeof(HslBattery), new PropertyMetadata("{0} %", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Color1Property = DependencyProperty.Register("Color1", typeof(Color), typeof(HslBattery), new PropertyMetadata(Colors.Green, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Color2Property = DependencyProperty.Register("Color2", typeof(Color), typeof(HslBattery), new PropertyMetadata(Colors.LimeGreen, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Color3Property = DependencyProperty.Register("Color3", typeof(Color), typeof(HslBattery), new PropertyMetadata(Colors.Orange, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Color4Property = DependencyProperty.Register("Color4", typeof(Color), typeof(HslBattery), new PropertyMetadata(Colors.Tomato, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Color5Property = DependencyProperty.Register("Color5", typeof(Color), typeof(HslBattery), new PropertyMetadata(Colors.Red, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Separatrix1Property = DependencyProperty.Register("Separatrix1", typeof(float), typeof(HslBattery), new PropertyMetadata(0.85f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Separatrix2Property = DependencyProperty.Register("Separatrix2", typeof(float), typeof(HslBattery), new PropertyMetadata(0.6f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Separatrix3Property = DependencyProperty.Register("Separatrix3", typeof(float), typeof(HslBattery), new PropertyMetadata(0.4f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty Separatrix4Property = DependencyProperty.Register("Separatrix4", typeof(float), typeof(HslBattery), new PropertyMetadata(0.15f, RenderUpdateByPropertyChanged));

		[Category("HslControls")]
		public Color BatteryBackColor
		{
			get
			{
				return (Color)GetValue(BatteryBackColorProperty);
			}
			set
			{
				SetValue(BatteryBackColorProperty, value);
			}
		}

		[Category("HslControls")]
		public float Value
		{
			get
			{
				return (float)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		[Category("HslControls")]
		public bool IsTextRender
		{
			get
			{
				return (bool)GetValue(IsTextRenderProperty);
			}
			set
			{
				SetValue(IsTextRenderProperty, value);
			}
		}

		[Category("HslControls")]
		public float ValueMax
		{
			get
			{
				return (float)GetValue(ValueMaxProperty);
			}
			set
			{
				SetValue(ValueMaxProperty, value);
			}
		}

		[Category("HslControls")]
		public float ValueMin
		{
			get
			{
				return (float)GetValue(ValueMinProperty);
			}
			set
			{
				SetValue(ValueMinProperty, value);
			}
		}

		[Category("HslControls")]
		public string StringFormat
		{
			get
			{
				return (string)GetValue(StringFormatProperty);
			}
			set
			{
				SetValue(StringFormatProperty, value);
			}
		}

		[Category("HslControls")]
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

		[Category("HslControls")]
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

		[Category("HslControls")]
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

		[Category("HslControls")]
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

		[Category("HslControls")]
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

		[Category("HslControls")]
		public float Separatrix1
		{
			get
			{
				return (float)GetValue(Separatrix1Property);
			}
			set
			{
				SetValue(Separatrix1Property, value);
			}
		}

		[Category("HslControls")]
		public float Separatrix2
		{
			get
			{
				return (float)GetValue(Separatrix2Property);
			}
			set
			{
				SetValue(Separatrix2Property, value);
			}
		}

		[Category("HslControls")]
		public float Separatrix3
		{
			get
			{
				return (float)GetValue(Separatrix3Property);
			}
			set
			{
				SetValue(Separatrix3Property, value);
			}
		}

		[Category("HslControls")]
		public float Separatrix4
		{
			get
			{
				return (float)GetValue(Separatrix4Property);
			}
			set
			{
				SetValue(Separatrix4Property, value);
			}
		}

		public HslBattery()
		{
			InitializeComponent();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslBattery hslBattery)
			{
				hslBattery.HslBatteryUpdate();
			}
		}

		public void SetValueByAnimation(float value, float milliseconds)
		{
			BeginAnimation(animation: new SingleAnimation(Value, value, TimeSpan.FromMilliseconds(milliseconds)), dp: ValueProperty);
		}

		private Color getColorFromValue(float value)
		{
			if (value > (ValueMax - ValueMin) * Separatrix1 + ValueMin)
			{
				return Color1;
			}
			if (value > (ValueMax - ValueMin) * Separatrix2 + ValueMin)
			{
				return Color2;
			}
			if (value > (ValueMax - ValueMin) * Separatrix3 + ValueMin)
			{
				return Color3;
			}
			if (value > (ValueMax - ValueMin) * Separatrix4 + ValueMin)
			{
				return Color4;
			}
			return Color5;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslBatteryUpdate();
		}

		public void HslBatteryUpdate()
		{
			if (Authorization.iashdiadasbdnajsdhjaf())
			{
				canvas1.Children.Clear();
				int num = (int)base.ActualWidth;
				int num2 = (int)base.ActualHeight;
				LinearGradientBrush brush = new LinearGradientBrush(new GradientStopCollection(new GradientStop[3]
				{
					new GradientStop(BatteryBackColor, 0.0),
					new GradientStop(Colors.WhiteSmoke, 0.75),
					new GradientStop(BatteryBackColor, 1.0)
				}), new Point(0.0, 0.0), new Point(1.0, 0.0));
				WpfUtils.FillEllipse(canvas1, 0.0, (float)num2 * 0.93f, num, (float)num2 * 0.06f, brush);
				WpfUtils.FillRectangle(canvas1, 0.0, (float)num2 * 0.1f, num, (float)num2 * 0.86f, brush);
				WpfUtils.FillEllipse(canvas1, 0.0, (float)num2 * 0.07f, num, (float)num2 * 0.06f, BatteryBackColor);
				WpfUtils.FillEllipse(canvas1, (float)num * 0.35f, (float)num2 * 0.085f, (float)num * 0.3f, (float)num2 * 0.02f, brush);
				WpfUtils.FillRectangle(canvas1, (float)num * 0.35f, (float)num2 * 0.03f, (float)num * 0.3f, (float)num2 * 0.065f, brush);
				WpfUtils.FillEllipse(canvas1, (float)num * 0.35f, (float)num2 * 0.02f, (float)num * 0.3f, (float)num2 * 0.02f, BatteryBackColor);
				Color colorFromValue = getColorFromValue(Value);
				LinearGradientBrush brush2 = new LinearGradientBrush(new GradientStopCollection(new GradientStop[3]
				{
					new GradientStop(colorFromValue, 0.0),
					new GradientStop(Colors.WhiteSmoke, 0.75),
					new GradientStop(colorFromValue, 1.0)
				}), new Point(0.0, 0.0), new Point(1.0, 0.0));
				float num3 = (float)num2 * 0.86f - (Value - ValueMin) / (ValueMax - ValueMin) * (float)num2 * 0.86f + (float)num2 * 0.1f;
				if (num3 < (float)num2 * 0.1f)
				{
					num3 = (float)num2 * 0.1f;
				}
				if (num3 > (float)num2 * 0.96f)
				{
					num3 = (float)num2 * 0.96f;
				}
				WpfUtils.FillEllipse(canvas1, 0.0, (float)num2 * 0.93f, num, (float)num2 * 0.06f, brush2);
				WpfUtils.FillRectangle(canvas1, 0.0, num3, num, (float)num2 * 0.86f - (num3 - (float)num2 * 0.1f), brush2);
				WpfUtils.FillEllipse(canvas1, 0.0, num3 - (float)num2 * 0.03f, num, (float)num2 * 0.06f, colorFromValue);
				if (IsTextRender)
				{
					WpfUtils.DrawString(canvas1, string.Format(StringFormat, Value), base.Foreground, 0.0, base.Height * 0.10000000149011612, base.Width, base.Height * 0.8999999761581421, HorizontalAlignment.Center, VerticalAlignment.Center);
				}
			}
		}
	}
}
