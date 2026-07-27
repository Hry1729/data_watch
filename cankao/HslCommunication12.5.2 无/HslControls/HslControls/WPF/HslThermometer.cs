using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslThermometer : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty ValueStartProperty = DependencyProperty.Register("ValueStart", typeof(float), typeof(HslThermometer), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueMaxProperty = DependencyProperty.Register("ValueMax", typeof(float), typeof(HslThermometer), new PropertyMetadata(100f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(float), typeof(HslThermometer), new PropertyMetadata(0f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty SegmentCountProperty = DependencyProperty.Register("SegmentCount", typeof(int), typeof(HslThermometer), new PropertyMetadata(8, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TemperatureColorProperty = DependencyProperty.Register("TemperatureColor", typeof(System.Windows.Media.Color), typeof(HslThermometer), new PropertyMetadata(Colors.Tomato, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TemperatureBackColorProperty = DependencyProperty.Register("TemperatureBackColor", typeof(System.Windows.Media.Color), typeof(HslThermometer), new PropertyMetadata(Colors.Silver, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty IsRenderTextProperty = DependencyProperty.Register("IsRenderText", typeof(bool), typeof(HslThermometer), new PropertyMetadata(false, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty LeftUnitTextProperty = DependencyProperty.Register("LeftUnitText", typeof(string), typeof(HslThermometer), new PropertyMetadata("℃", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty RightUnitTextProperty = DependencyProperty.Register("RightUnitText", typeof(string), typeof(HslThermometer), new PropertyMetadata("°F", RenderUpdateByPropertyChanged));

		private System.Windows.Media.Color colorCenter = Colors.WhiteSmoke;

		private System.Windows.Media.Color colorBorder = Colors.Silver;

		public float ValueStart
		{
			get
			{
				return (float)GetValue(ValueStartProperty);
			}
			set
			{
				SetValue(ValueStartProperty, value);
			}
		}

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

		public int SegmentCount
		{
			get
			{
				return (int)GetValue(SegmentCountProperty);
			}
			set
			{
				SetValue(SegmentCountProperty, value);
			}
		}

		public System.Windows.Media.Color TemperatureColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(TemperatureColorProperty);
			}
			set
			{
				SetValue(TemperatureColorProperty, value);
			}
		}

		public System.Windows.Media.Color TemperatureBackColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(TemperatureBackColorProperty);
			}
			set
			{
				SetValue(TemperatureBackColorProperty, value);
			}
		}

		public bool IsRenderText
		{
			get
			{
				return (bool)GetValue(IsRenderTextProperty);
			}
			set
			{
				SetValue(IsRenderTextProperty, value);
			}
		}

		public string LeftUnitText
		{
			get
			{
				return (string)GetValue(LeftUnitTextProperty);
			}
			set
			{
				SetValue(LeftUnitTextProperty, value);
			}
		}

		public string RightUnitText
		{
			get
			{
				return (string)GetValue(RightUnitTextProperty);
			}
			set
			{
				SetValue(RightUnitTextProperty, value);
			}
		}

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Func<float, float> RightDataTransfer { get; set; }

		public HslThermometer()
		{
			RightDataTransfer = (float m) => m * 1.8f + 32f;
			InitializeComponent();
		}

		public void SetValue(float value, double time = 300.0)
		{
			BeginAnimation(animation: new SingleAnimation(value, TimeSpan.FromMilliseconds(time)), dp: ValueProperty);
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslThermometer hslThermometer)
			{
				hslThermometer.HslThermometerUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslThermometerUpdate();
		}

		public void HslThermometerUpdate()
		{
			if (!Authorization.iashdiadasbdnajsdhjaf())
			{
				return;
			}
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			if (num < 20 || num2 < 50)
			{
				return;
			}
			float num3 = (float)num * 0.1f;
			float num4 = (float)num * 0.2f;
			float num5 = num3 + 40f;
			float num6 = (float)num2 - num5 - 20f - num4 * 2f;
			LinearGradientBrush brush = new LinearGradientBrush(new GradientStopCollection(new List<GradientStop>
			{
				new GradientStop(colorCenter, 0.0),
				new GradientStop(colorBorder, 0.30000001192092896),
				new GradientStop(colorCenter, 0.800000011920929),
				new GradientStop(colorBorder, 1.0)
			}), new System.Windows.Point(0.0, 0.0), new System.Windows.Point(1.0, 0.0));
			WpfUtils.FillEllipse(canvas1, (float)num / 2f - num3, num5 - num3, num3 * 2f, num3 * 2f, TemperatureBackColor);
			WpfUtils.FillEllipse(canvas1, (float)num / 2f - num4, num5 + num6, num4 * 2f, num4 * 2f, TemperatureBackColor);
			WpfUtils.FillRectangle(canvas1, (float)num / 2f - num3, num5, num3 * 2f, num6 + num4, TemperatureBackColor);
			for (int i = 0; i <= SegmentCount; i++)
			{
				float num7 = (float)i * (ValueMax - ValueStart) / (float)SegmentCount + ValueStart;
				float num8 = num6 - 10f - (num7 - ValueStart) / (ValueMax - ValueStart) * (num6 - 10f) + num5;
				if (i > 0)
				{
					for (int j = 1; j < 10; j++)
					{
						float num9 = (num6 - 10f) / (float)SegmentCount / 10f * (float)j;
						if (j == 5)
						{
							WpfUtils.DrawLine(canvas1, (float)num / 2f - num3 - (float)num * 0.14f, num8 + num9, (float)num / 2f - num3 - 4f, num8 + num9, base.Foreground, 1.0);
							WpfUtils.DrawLine(canvas1, (float)num / 2f + num3 + (float)num * 0.14f, num8 + num9, (float)num / 2f + num3 + 4f, num8 + num9, base.Foreground, 1.0);
						}
						else
						{
							WpfUtils.DrawLine(canvas1, (float)num / 2f - num3 - (float)num * 0.1f, num8 + num9, (float)num / 2f - num3 - 4f, num8 + num9, base.Foreground, 1.0);
							WpfUtils.DrawLine(canvas1, (float)num / 2f + num3 + (float)num * 0.1f, num8 + num9, (float)num / 2f + num3 + 4f, num8 + num9, base.Foreground, 1.0);
						}
					}
				}
				WpfUtils.DrawLine(canvas1, (float)num / 2f - num3 - (float)num * 0.3f, num8, (float)num / 2f - num3 - 4f, num8, base.Foreground, 1.0);
				WpfUtils.DrawLine(canvas1, (float)num / 2f + num3 + 4f, num8, (float)num / 2f + num3 + (float)num * 0.3f, num8, base.Foreground, 1.0);
				WpfUtils.DrawString(canvas1, RightUnitText, base.Foreground, new RectangleF(5f, 0f, num - 10, num5 - num3), HorizontalAlignment.Right, VerticalAlignment.Center);
				WpfUtils.DrawString(canvas1, num7.ToString(), base.Foreground, new RectangleF(0f, num8 - 100f, (float)num / 2f - num3 - (float)num * 0.13f, 100f), HorizontalAlignment.Right, VerticalAlignment.Bottom);
				WpfUtils.DrawString(canvas1, LeftUnitText, base.Foreground, new RectangleF(5f, 0f, num - 10, num5 - num3), HorizontalAlignment.Left, VerticalAlignment.Center);
				if (RightDataTransfer != null)
				{
					WpfUtils.DrawString(canvas1, Math.Round(RightDataTransfer(num7)).ToString(), base.Foreground, new RectangleF((float)num / 2f + num3 + (float)num * 0.1f + 5f, num8 - 100f, (float)num * 0.4f, 100f), HorizontalAlignment.Left, VerticalAlignment.Bottom);
				}
			}
			WpfUtils.FillEllipse(canvas1, (float)num / 2f - num3 * 0.7f, num5 - num3 * 0.7f, num3 * 1.4f, num3 * 1.4f, brush);
			WpfUtils.FillRectangle(canvas1, (float)num / 2f - num3 * 0.7f, num5, num3 * 1.4f, num6 + num4, brush);
			brush = new LinearGradientBrush();
			brush.GradientStops = new GradientStopCollection(new List<GradientStop>
			{
				new GradientStop(TemperatureColor, 0.0),
				new GradientStop(TemperatureColor, 0.30000001192092896),
				new GradientStop(colorCenter, 0.800000011920929),
				new GradientStop(TemperatureColor, 1.0)
			});
			brush.StartPoint = new System.Windows.Point(0.0, 0.0);
			brush.EndPoint = new System.Windows.Point(1.0, 0.0);
			float num10 = num6 - 10f - (Value - ValueStart) / (ValueMax - ValueStart) * (num6 - 10f) + num5;
			if (num10 < num5 - 5f)
			{
				num10 = num5 - 5f;
			}
			if (num10 > num5 + num6 + 5f)
			{
				num10 = num5 + num6 + 5f;
			}
			WpfUtils.FillRectangle(canvas1, (float)num / 2f - num3 * 0.7f, num10, num3 * 1.4f, num6 + num4 + num5 - num10, brush);
			WpfUtils.FillEllipse(canvas1, (float)num / 2f - num3 * 0.7f, num10 - 1f, num3 * 1.4f, 3.0, TemperatureColor);
			RectangleF rectangle = new RectangleF((float)num / 2f - num4 * 0.85f, num5 + num6 + num4 * 0.15f, num4 * 1.7f, num4 * 1.7f);
			RadialGradientBrush radialGradientBrush = new RadialGradientBrush(colorCenter, TemperatureColor);
			radialGradientBrush.RadiusX = 0.5;
			radialGradientBrush.RadiusY = 0.5;
			radialGradientBrush.GradientOrigin = new System.Windows.Point(0.7, 0.3);
			WpfUtils.FillEllipse(canvas1, rectangle, radialGradientBrush);
			if (IsRenderText)
			{
				WpfUtils.DrawString(canvas1, Value.ToString(), base.Foreground, (float)num / 2f - num4, num5 + num6, num4 * 2f, num4 * 2f, HorizontalAlignment.Center, VerticalAlignment.Center);
			}
		}
	}
}
