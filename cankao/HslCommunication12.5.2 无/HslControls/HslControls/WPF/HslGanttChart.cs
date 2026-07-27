using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslGanttChart : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty LeftRightOffectProperty = DependencyProperty.Register("LeftRightOffect", typeof(int), typeof(HslGanttChart), new PropertyMetadata(20, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TimeFormateProperty = DependencyProperty.Register("TimeFormate", typeof(string), typeof(HslGanttChart), new PropertyMetadata("{0}H", RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TimeCountProperty = DependencyProperty.Register("TimeCount", typeof(int), typeof(HslGanttChart), new PropertyMetadata(86400, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TimeStartProperty = DependencyProperty.Register("TimeStart", typeof(int), typeof(HslGanttChart), new PropertyMetadata(0, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TimeMaxProperty = DependencyProperty.Register("TimeMax", typeof(int), typeof(HslGanttChart), new PropertyMetadata(24, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty TimeSegmentProperty = DependencyProperty.Register("TimeSegment", typeof(int), typeof(HslGanttChart), new PropertyMetadata(6, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty GanttBackColorProperty = DependencyProperty.Register("GanttBackColor", typeof(System.Windows.Media.Color), typeof(HslGanttChart), new PropertyMetadata(Colors.LightGray, RenderUpdateByPropertyChanged));

		private int[] data = new int[0];

		private System.Windows.Media.Color[] colors = new System.Windows.Media.Color[0];

		public int LeftRightOffect
		{
			get
			{
				return (int)GetValue(LeftRightOffectProperty);
			}
			set
			{
				SetValue(LeftRightOffectProperty, value);
			}
		}

		public string TimeFormate
		{
			get
			{
				return (string)GetValue(TimeFormateProperty);
			}
			set
			{
				SetValue(TimeFormateProperty, value);
			}
		}

		public int TimeCount
		{
			get
			{
				return (int)GetValue(TimeCountProperty);
			}
			set
			{
				SetValue(TimeCountProperty, value);
			}
		}

		public int TimeStart
		{
			get
			{
				return (int)GetValue(TimeStartProperty);
			}
			set
			{
				SetValue(TimeStartProperty, value);
			}
		}

		public int TimeMax
		{
			get
			{
				return (int)GetValue(TimeMaxProperty);
			}
			set
			{
				SetValue(TimeMaxProperty, value);
			}
		}

		public int TimeSegment
		{
			get
			{
				return (int)GetValue(TimeSegmentProperty);
			}
			set
			{
				SetValue(TimeSegmentProperty, value);
			}
		}

		public System.Windows.Media.Color GanttBackColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(GanttBackColorProperty);
			}
			set
			{
				SetValue(GanttBackColorProperty, value);
			}
		}

		public HslGanttChart()
		{
			InitializeComponent();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslGanttChart hslGanttChart)
			{
				hslGanttChart.HslGanttChartUpdate();
			}
		}

		public void SetGanttChart(int[] data, System.Windows.Media.Color[] colors)
		{
			if (data != null && colors != null)
			{
				this.data = data;
				this.colors = colors;
				HslGanttChartUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslGanttChartUpdate();
		}

		public void HslGanttChartUpdate()
		{
			if (!Authorization.iashdiadasbdnajsdhjaf())
			{
				return;
			}
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			int leftRightOffect = LeftRightOffect;
			int num3 = (int)(base.FontSize + 10.0);
			for (int i = 0; i <= TimeSegment; i++)
			{
				int num4 = (TimeMax - TimeStart) * i / TimeSegment + TimeStart;
				WpfUtils.DrawString(rectangle: new RectangleF((int)(float)((num - 2 * leftRightOffect) * i / TimeSegment + leftRightOffect) - 50, 0f, 100f, num3), canvas: canvas1, text: string.Format(TimeFormate, num4), brush: base.Foreground, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Center);
			}
			RectangleF rectangle2 = new RectangleF(leftRightOffect, num3, num - 2 * leftRightOffect, num2 - num3);
			WpfUtils.FillRectangle(canvas1, rectangle2, GanttBackColor);
			int num5 = 0;
			for (int j = 0; j < data.Length; j++)
			{
				float num6 = num5 * (num - 2 * leftRightOffect) / TimeCount + leftRightOffect;
				float num7 = data[j] * (num - 2 * leftRightOffect) / TimeCount + 1;
				if (num7 > rectangle2.Width - num6 + (float)leftRightOffect)
				{
					num7 = rectangle2.Width - num6 + (float)leftRightOffect;
				}
				if (j < colors.Length)
				{
					WpfUtils.FillRectangle(canvas1, new RectangleF(num6, num3, num7, rectangle2.Height), colors[j]);
				}
				num5 += data[j];
				if (num5 > TimeCount)
				{
					break;
				}
			}
		}
	}
}
