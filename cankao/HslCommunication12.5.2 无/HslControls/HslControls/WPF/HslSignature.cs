using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public partial class HslSignature : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty SignWidthProperty = DependencyProperty.Register("SignWidth", typeof(float), typeof(HslSignature), new PropertyMetadata(1f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty SignColorProperty = DependencyProperty.Register("SignColor", typeof(System.Windows.Media.Color), typeof(HslSignature), new PropertyMetadata(Colors.Black, RenderUpdateByPropertyChanged));

		private System.Windows.Media.Color boderColor = System.Windows.Media.Color.FromRgb(205, 205, 205);

		private List<List<System.Windows.Point>> totalPoints = new List<List<System.Windows.Point>>();

		private List<System.Windows.Point> currPoints = new List<System.Windows.Point>();

		private bool isEnableSign = true;

		private bool isMouseDown = false;

		public float SignWidth
		{
			get
			{
				return (float)GetValue(SignWidthProperty);
			}
			set
			{
				SetValue(SignWidthProperty, value);
			}
		}

		public System.Windows.Media.Color SignColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(SignColorProperty);
			}
			set
			{
				SetValue(SignColorProperty, value);
			}
		}

		public HslSignature()
		{
			InitializeComponent();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslSignature hslSignature)
			{
				hslSignature.BarChartUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			BarChartUpdate();
		}

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);
			if (isEnableSign)
			{
				if (e.LeftButton == MouseButtonState.Pressed)
				{
					isMouseDown = true;
					currPoints.Add(e.GetPosition(this));
				}
				else if (e.RightButton == MouseButtonState.Pressed)
				{
					ClearSign();
				}
			}
		}

		protected override void OnMouseUp(MouseButtonEventArgs e)
		{
			base.OnMouseUp(e);
			if (isEnableSign && e.LeftButton == MouseButtonState.Released)
			{
				isMouseDown = false;
				currPoints.Add(e.GetPosition(this));
				totalPoints.Add(currPoints);
				currPoints = new List<System.Windows.Point>();
				BarChartUpdate();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (isMouseDown && isEnableSign)
			{
				if (e.LeftButton == MouseButtonState.Released)
				{
					isMouseDown = false;
					currPoints.Add(e.GetPosition(this));
					totalPoints.Add(currPoints);
					currPoints = new List<System.Windows.Point>();
					BarChartUpdate();
				}
				else
				{
					currPoints.Add(e.GetPosition(this));
					BarChartUpdate();
				}
			}
		}

		public void ClearSign()
		{
			currPoints.Clear();
			totalPoints.Clear();
			BarChartUpdate();
		}

		public void BarChartUpdate()
		{
			if (!Authorization.iashdiadasbdnajsdhjaf())
			{
				return;
			}
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			foreach (List<System.Windows.Point> totalPoint in totalPoints)
			{
				System.Windows.Point[] array = totalPoint.ToArray();
				if (array.Length > 1)
				{
					Polyline polyline = new Polyline();
					polyline.Points = new PointCollection(array);
					polyline.Stroke = new SolidColorBrush(SignColor);
					polyline.StrokeThickness = SignWidth;
					canvas1.Children.Add(polyline);
				}
			}
			System.Windows.Point[] array2 = currPoints.ToArray();
			if (array2.Length > 1)
			{
				Polyline polyline2 = new Polyline();
				polyline2.Points = new PointCollection(array2);
				polyline2.Stroke = new SolidColorBrush(SignColor);
				polyline2.StrokeThickness = SignWidth;
				canvas1.Children.Add(polyline2);
			}
		}

		public Bitmap SaveBitmap()
		{
			Bitmap bitmap = new Bitmap((int)base.ActualWidth, (int)base.ActualHeight);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			graphics.Clear(System.Drawing.Color.White);
			using (System.Drawing.Pen pen = new System.Drawing.Pen(WpfUtils.TransDrawingColor(SignColor), SignWidth))
			{
				foreach (List<System.Windows.Point> totalPoint in totalPoints)
				{
					PointF[] array = WpfUtils.TransMediaPoints(totalPoint);
					if (array.Length > 1)
					{
						graphics.DrawLines(pen, array);
					}
				}
				PointF[] array2 = WpfUtils.TransMediaPoints(currPoints);
				if (array2.Length > 1)
				{
					graphics.DrawLines(pen, array2);
				}
			}
			graphics.Dispose();
			return bitmap;
		}
	}
}
