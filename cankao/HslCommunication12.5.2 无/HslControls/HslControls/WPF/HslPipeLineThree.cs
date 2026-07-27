using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslPipeLineThree : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslPipeLineThree), new PropertyMetadata(Colors.DimGray));

		public static readonly DependencyProperty CenterColorProperty = DependencyProperty.Register("CenterColor", typeof(Color), typeof(HslPipeLineThree), new PropertyMetadata(Colors.LightGray));

		public static readonly DependencyProperty PipeLineActive1Property = DependencyProperty.Register("PipeLineActive1", typeof(bool), typeof(HslPipeLineThree), new PropertyMetadata(false));

		public static readonly DependencyProperty PipeLineActive2Property = DependencyProperty.Register("PipeLineActive2", typeof(bool), typeof(HslPipeLineThree), new PropertyMetadata(false));

		public static readonly DependencyProperty PipeLineActive3Property = DependencyProperty.Register("PipeLineActive3", typeof(bool), typeof(HslPipeLineThree), new PropertyMetadata(false));

		public static readonly DependencyProperty MoveSpeed1Property = DependencyProperty.Register("MoveSpeed1", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0, MoveSpeed1PropertyChangedCallback));

		private DoubleAnimation offect1DoubleAnimation = null;

		public static readonly DependencyProperty MoveSpeed2Property = DependencyProperty.Register("MoveSpeed2", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0, MoveSpeed2PropertyChangedCallback));

		private DoubleAnimation offect2DoubleAnimation = null;

		public static readonly DependencyProperty MoveSpeed3Property = DependencyProperty.Register("MoveSpeed3", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0, MoveSpeed3PropertyChangedCallback));

		private DoubleAnimation offect3DoubleAnimation = null;

		public static readonly DependencyProperty LineOffect1Property = DependencyProperty.Register("LineOffect1", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0));

		public static readonly DependencyProperty LineOffect2Property = DependencyProperty.Register("LineOffect2", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0));

		public static readonly DependencyProperty LineOffect3Property = DependencyProperty.Register("LineOffect3", typeof(double), typeof(HslPipeLineThree), new PropertyMetadata(0.0));

		public static readonly DependencyProperty PipeLineWidthProperty = DependencyProperty.Register("PipeLineWidth", typeof(int), typeof(HslPipeLineThree), new PropertyMetadata(2));

		public static readonly DependencyProperty ActiveLineCenterColorProperty = DependencyProperty.Register("ActiveLineCenterColor", typeof(Color), typeof(HslPipeLineThree), new PropertyMetadata(Colors.DodgerBlue));

		public static readonly DependencyProperty PipeWidthProperty = DependencyProperty.Register("PipeWidth", typeof(int), typeof(HslPipeLineThree), new PropertyMetadata(30, PipeWidthPropertyChangedCallback));

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

		public Color CenterColor
		{
			get
			{
				return (Color)GetValue(CenterColorProperty);
			}
			set
			{
				SetValue(CenterColorProperty, value);
			}
		}

		public bool PipeLineActive1
		{
			get
			{
				return (bool)GetValue(PipeLineActive1Property);
			}
			set
			{
				SetValue(PipeLineActive1Property, value);
			}
		}

		public bool PipeLineActive2
		{
			get
			{
				return (bool)GetValue(PipeLineActive2Property);
			}
			set
			{
				SetValue(PipeLineActive2Property, value);
			}
		}

		public bool PipeLineActive3
		{
			get
			{
				return (bool)GetValue(PipeLineActive3Property);
			}
			set
			{
				SetValue(PipeLineActive3Property, value);
			}
		}

		public double MoveSpeed1
		{
			get
			{
				return (double)GetValue(MoveSpeed1Property);
			}
			set
			{
				SetValue(MoveSpeed1Property, value);
			}
		}

		public double MoveSpeed2
		{
			get
			{
				return (double)GetValue(MoveSpeed2Property);
			}
			set
			{
				SetValue(MoveSpeed2Property, value);
			}
		}

		public double MoveSpeed3
		{
			get
			{
				return (double)GetValue(MoveSpeed3Property);
			}
			set
			{
				SetValue(MoveSpeed3Property, value);
			}
		}

		public double LineOffect1
		{
			get
			{
				return (double)GetValue(LineOffect1Property);
			}
			set
			{
				SetValue(LineOffect1Property, value);
			}
		}

		public double LineOffect2
		{
			get
			{
				return (double)GetValue(LineOffect2Property);
			}
			set
			{
				SetValue(LineOffect2Property, value);
			}
		}

		public double LineOffect3
		{
			get
			{
				return (double)GetValue(LineOffect3Property);
			}
			set
			{
				SetValue(LineOffect3Property, value);
			}
		}

		public int PipeLineWidth
		{
			get
			{
				return (int)GetValue(PipeLineWidthProperty);
			}
			set
			{
				SetValue(PipeLineWidthProperty, value);
			}
		}

		public Color ActiveLineCenterColor
		{
			get
			{
				return (Color)GetValue(ActiveLineCenterColorProperty);
			}
			set
			{
				SetValue(ActiveLineCenterColorProperty, value);
			}
		}

		public int PipeWidth
		{
			get
			{
				return (int)GetValue(PipeWidthProperty);
			}
			set
			{
				SetValue(PipeWidthProperty, value);
			}
		}

		public HslPipeLineThree()
		{
			InitializeComponent();
			offect1DoubleAnimation = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(1000.0));
			offect1DoubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(LineOffect1Property, offect1DoubleAnimation);
			offect2DoubleAnimation = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(1000.0));
			offect2DoubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(LineOffect2Property, offect2DoubleAnimation);
			offect3DoubleAnimation = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(1000.0));
			offect3DoubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(LineOffect3Property, offect3DoubleAnimation);
		}

		public static void MoveSpeed1PropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLineThree)dependency).UpdateMoveSpeed1();
		}

		public void UpdateMoveSpeed1()
		{
			if (MoveSpeed1 > 0.0)
			{
				offect1DoubleAnimation.From = 0.0;
				offect1DoubleAnimation.To = 10.0;
				offect1DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / MoveSpeed1);
				BeginAnimation(LineOffect1Property, offect1DoubleAnimation);
			}
			else if (MoveSpeed1 < 0.0)
			{
				offect1DoubleAnimation.From = 0.0;
				offect1DoubleAnimation.To = -10.0;
				offect1DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / Math.Abs(MoveSpeed1));
				BeginAnimation(LineOffect1Property, offect1DoubleAnimation);
			}
			else
			{
				offect1DoubleAnimation.From = 0.0;
				offect1DoubleAnimation.To = 0.0;
				BeginAnimation(LineOffect1Property, offect1DoubleAnimation);
			}
		}

		public static void MoveSpeed2PropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLineThree)dependency).UpdateMoveSpeed2();
		}

		public void UpdateMoveSpeed2()
		{
			if (MoveSpeed2 > 0.0)
			{
				offect2DoubleAnimation.From = 0.0;
				offect2DoubleAnimation.To = 10.0;
				offect2DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / MoveSpeed2);
				BeginAnimation(LineOffect2Property, offect2DoubleAnimation);
			}
			else if (MoveSpeed2 < 0.0)
			{
				offect2DoubleAnimation.From = 0.0;
				offect2DoubleAnimation.To = -10.0;
				offect2DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / Math.Abs(MoveSpeed2));
				BeginAnimation(LineOffect2Property, offect2DoubleAnimation);
			}
			else
			{
				offect2DoubleAnimation.From = 0.0;
				offect2DoubleAnimation.To = 0.0;
				BeginAnimation(LineOffect2Property, offect2DoubleAnimation);
			}
		}

		public static void MoveSpeed3PropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLineThree)dependency).UpdateMoveSpeed3();
		}

		public void UpdateMoveSpeed3()
		{
			if (MoveSpeed3 > 0.0)
			{
				offect3DoubleAnimation.From = 0.0;
				offect3DoubleAnimation.To = 10.0;
				offect3DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / MoveSpeed3);
				BeginAnimation(LineOffect3Property, offect3DoubleAnimation);
			}
			else if (MoveSpeed3 < 0.0)
			{
				offect3DoubleAnimation.From = 0.0;
				offect3DoubleAnimation.To = -10.0;
				offect3DoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / Math.Abs(MoveSpeed3));
				BeginAnimation(LineOffect3Property, offect3DoubleAnimation);
			}
			else
			{
				offect3DoubleAnimation.From = 0.0;
				offect3DoubleAnimation.To = 0.0;
				BeginAnimation(LineOffect3Property, offect3DoubleAnimation);
			}
		}

		public static void PipeWidthPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLineThree)dependency).PipeWidthUpdate();
		}

		public void PipeWidthUpdate()
		{
			UpdatePath();
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			UpdatePath();
			base.OnRenderSizeChanged(sizeInfo);
		}

		public void UpdatePath()
		{
			polygon1.Points = new PointCollection(new Point[6]
			{
				new Point(base.ActualWidth / 2.0 - (double)PipeWidth / 2.0, PipeWidth),
				new Point(base.ActualWidth / 2.0, (double)PipeWidth / 2.0),
				new Point(base.ActualWidth / 2.0 + (double)PipeWidth / 2.0, PipeWidth),
				new Point(base.ActualWidth / 2.0 + (double)PipeWidth / 2.0, base.ActualHeight),
				new Point(base.ActualWidth / 2.0 - (double)PipeWidth / 2.0, base.ActualHeight),
				new Point(base.ActualWidth / 2.0 - (double)PipeWidth / 2.0, PipeWidth)
			});
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(0.0, (double)PipeWidth / 2.0), false, false);
				streamGeometryContext.LineTo(new Point(base.ActualWidth, (double)PipeWidth / 2.0), true, false);
			}
			path1.Data = streamGeometry;
			StreamGeometry streamGeometry2 = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
			{
				streamGeometryContext2.BeginFigure(new Point(0.0, (double)PipeWidth / 2.0), false, false);
				streamGeometryContext2.LineTo(new Point(base.ActualWidth / 2.0 - (double)PipeWidth / 2.0, (double)PipeWidth / 2.0), true, false);
				streamGeometryContext2.ArcTo(new Point(base.ActualWidth / 2.0, PipeWidth), new Size((double)PipeWidth / 2.0, (double)PipeWidth / 2.0), 0.0, false, SweepDirection.Clockwise, true, false);
				streamGeometryContext2.LineTo(new Point(base.ActualWidth / 2.0, base.ActualHeight), true, false);
			}
			path2.Data = streamGeometry2;
			StreamGeometry streamGeometry3 = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext3 = streamGeometry3.Open())
			{
				streamGeometryContext3.BeginFigure(new Point(base.ActualWidth, (double)PipeWidth / 2.0), false, false);
				streamGeometryContext3.LineTo(new Point(base.ActualWidth / 2.0 + (double)PipeWidth / 2.0, (double)PipeWidth / 2.0), true, false);
				streamGeometryContext3.ArcTo(new Point(base.ActualWidth / 2.0, PipeWidth), new Size((double)PipeWidth / 2.0, (double)PipeWidth / 2.0), 0.0, false, SweepDirection.Counterclockwise, true, false);
				streamGeometryContext3.LineTo(new Point(base.ActualWidth / 2.0, base.ActualHeight), true, false);
			}
			path3.Data = streamGeometry3;
		}
	}
}
