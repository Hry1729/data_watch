using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslPipeLine : UserControl, IComponentConnector
	{
		private DoubleAnimation offectDoubleAnimation = null;

		public static readonly DependencyProperty LeftDirectionProperty = DependencyProperty.Register("LeftDirection", typeof(HslPipeTurnDirection), typeof(HslPipeLine), new PropertyMetadata(HslPipeTurnDirection.Left, LeftDirectionPropertyChangedCallback));

		public static readonly DependencyProperty RightDirectionProperty = DependencyProperty.Register("RightDirection", typeof(HslPipeTurnDirection), typeof(HslPipeLine), new PropertyMetadata(HslPipeTurnDirection.Right, RightDirectionPropertyChangedCallback));

		public static readonly DependencyProperty PipeLineActiveProperty = DependencyProperty.Register("PipeLineActive", typeof(bool), typeof(HslPipeLine), new PropertyMetadata(false));

		public static readonly DependencyProperty LineOffectProperty = DependencyProperty.Register("LineOffect", typeof(double), typeof(HslPipeLine), new PropertyMetadata(0.0));

		public static readonly DependencyProperty MoveSpeedProperty = DependencyProperty.Register("MoveSpeed", typeof(double), typeof(HslPipeLine), new PropertyMetadata(0.3, MoveSpeedPropertyChangedCallback));

		private Storyboard storyboard = new Storyboard();

		public static readonly DependencyProperty CenterColorProperty = DependencyProperty.Register("CenterColor", typeof(Color), typeof(HslPipeLine), new PropertyMetadata(Colors.LightGray));

		public static readonly DependencyProperty PipeLineWidthProperty = DependencyProperty.Register("PipeLineWidth", typeof(int), typeof(HslPipeLine), new PropertyMetadata(2));

		public static readonly DependencyProperty ActiveLineCenterColorProperty = DependencyProperty.Register("ActiveLineCenterColor", typeof(Color), typeof(HslPipeLine), new PropertyMetadata(Colors.DodgerBlue));

		public static readonly DependencyProperty EdgeColorProperty = DependencyProperty.Register("EdgeColor", typeof(Color), typeof(HslPipeLine), new PropertyMetadata(Colors.DimGray));

		public HslPipeTurnDirection LeftDirection
		{
			get
			{
				return (HslPipeTurnDirection)GetValue(LeftDirectionProperty);
			}
			set
			{
				SetValue(LeftDirectionProperty, value);
			}
		}

		public HslPipeTurnDirection RightDirection
		{
			get
			{
				return (HslPipeTurnDirection)GetValue(RightDirectionProperty);
			}
			set
			{
				SetValue(RightDirectionProperty, value);
			}
		}

		public bool PipeLineActive
		{
			get
			{
				return (bool)GetValue(PipeLineActiveProperty);
			}
			set
			{
				SetValue(PipeLineActiveProperty, value);
			}
		}

		public double LineOffect
		{
			get
			{
				return (double)GetValue(LineOffectProperty);
			}
			set
			{
				SetValue(LineOffectProperty, value);
			}
		}

		public double MoveSpeed
		{
			get
			{
				return (double)GetValue(MoveSpeedProperty);
			}
			set
			{
				SetValue(MoveSpeedProperty, value);
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

		public HslPipeLine()
		{
			InitializeComponent();
			offectDoubleAnimation = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(1000.0));
			offectDoubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
			BeginAnimation(LineOffectProperty, offectDoubleAnimation);
		}

		public static void LeftDirectionPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLine)dependency).UpdateLeftDirectionBinding();
		}

		public void UpdateLeftDirectionBinding()
		{
			BindingOperations.ClearBinding(ellipe1, Canvas.TopProperty);
			if (LeftDirection == HslPipeTurnDirection.Left)
			{
				canvas1.Visibility = Visibility.Visible;
				Binding binding = new Binding();
				binding.Source = grid1;
				binding.Path = new PropertyPath("ActualHeight");
				binding.Converter = new MultiplesValueConverter();
				binding.ConverterParameter = 0;
				ellipe1.SetBinding(Canvas.TopProperty, binding);
			}
			else if (LeftDirection == HslPipeTurnDirection.Right)
			{
				canvas1.Visibility = Visibility.Visible;
				Binding binding2 = new Binding();
				binding2.Source = grid1;
				binding2.Path = new PropertyPath("ActualHeight");
				binding2.Converter = new MultiplesValueConverter();
				binding2.ConverterParameter = -1;
				ellipe1.SetBinding(Canvas.TopProperty, binding2);
			}
			else
			{
				canvas1.Visibility = Visibility.Collapsed;
			}
			UpdatePathData();
		}

		public static void RightDirectionPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLine)dependency).UpdateRightDirectionBinding();
		}

		public void UpdateRightDirectionBinding()
		{
			BindingOperations.ClearBinding(ellipe2, Canvas.TopProperty);
			if (RightDirection == HslPipeTurnDirection.Left)
			{
				canvas2.Visibility = Visibility.Visible;
				Binding binding = new Binding();
				binding.Source = grid1;
				binding.Path = new PropertyPath("ActualHeight");
				binding.Converter = new MultiplesValueConverter();
				binding.ConverterParameter = 0;
				ellipe2.SetBinding(Canvas.TopProperty, binding);
			}
			else if (RightDirection == HslPipeTurnDirection.Right)
			{
				canvas2.Visibility = Visibility.Visible;
				Binding binding2 = new Binding();
				binding2.Source = grid1;
				binding2.Path = new PropertyPath("ActualHeight");
				binding2.Converter = new MultiplesValueConverter();
				binding2.ConverterParameter = -1;
				ellipe2.SetBinding(Canvas.TopProperty, binding2);
			}
			else
			{
				canvas2.Visibility = Visibility.Collapsed;
			}
			UpdatePathData();
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			UpdatePathData();
			base.OnRenderSizeChanged(sizeInfo);
		}

		public void UpdatePathData()
		{
			Console.WriteLine("Size Changed");
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				if (LeftDirection == HslPipeTurnDirection.Left)
				{
					streamGeometryContext.BeginFigure(new Point(base.ActualHeight / 2.0, base.ActualHeight), false, false);
					streamGeometryContext.ArcTo(new Point(base.ActualHeight, base.ActualHeight / 2.0), new Size(base.ActualHeight / 2.0, base.ActualHeight / 2.0), 0.0, false, SweepDirection.Clockwise, true, false);
				}
				else if (LeftDirection == HslPipeTurnDirection.Right)
				{
					streamGeometryContext.BeginFigure(new Point(base.ActualHeight / 2.0, 0.0), false, false);
					streamGeometryContext.ArcTo(new Point(base.ActualHeight, base.ActualHeight / 2.0), new Size(base.ActualHeight / 2.0, base.ActualHeight / 2.0), 0.0, false, SweepDirection.Counterclockwise, true, false);
				}
				else
				{
					streamGeometryContext.BeginFigure(new Point(0.0, base.ActualHeight / 2.0), false, false);
					streamGeometryContext.LineTo(new Point(base.ActualHeight, base.ActualHeight / 2.0), true, false);
				}
				streamGeometryContext.LineTo(new Point(base.ActualWidth - base.ActualHeight, base.ActualHeight / 2.0), true, false);
				if (RightDirection == HslPipeTurnDirection.Left)
				{
					streamGeometryContext.ArcTo(new Point(base.ActualWidth - base.ActualHeight / 2.0, base.ActualHeight), new Size(base.ActualHeight / 2.0, base.ActualHeight / 2.0), 0.0, false, SweepDirection.Clockwise, true, false);
				}
				else if (RightDirection == HslPipeTurnDirection.Right)
				{
					streamGeometryContext.ArcTo(new Point(base.ActualWidth - base.ActualHeight / 2.0, 0.0), new Size(base.ActualHeight / 2.0, base.ActualHeight / 2.0), 0.0, false, SweepDirection.Counterclockwise, true, false);
				}
				else
				{
					streamGeometryContext.LineTo(new Point(base.ActualWidth, base.ActualHeight / 2.0), true, false);
				}
			}
			path1.Data = streamGeometry;
		}

		public static void MoveSpeedPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslPipeLine)dependency).UpdateMoveSpeed();
		}

		public void UpdateMoveSpeed()
		{
			if (MoveSpeed > 0.0)
			{
				offectDoubleAnimation.From = 0.0;
				offectDoubleAnimation.To = 10.0;
				offectDoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / MoveSpeed);
				BeginAnimation(LineOffectProperty, offectDoubleAnimation);
			}
			else if (MoveSpeed < 0.0)
			{
				offectDoubleAnimation.From = 0.0;
				offectDoubleAnimation.To = -10.0;
				offectDoubleAnimation.Duration = TimeSpan.FromMilliseconds(300.0 / Math.Abs(MoveSpeed));
				BeginAnimation(LineOffectProperty, offectDoubleAnimation);
			}
			else
			{
				offectDoubleAnimation.From = 0.0;
				offectDoubleAnimation.To = 0.0;
				BeginAnimation(LineOffectProperty, offectDoubleAnimation);
			}
		}
	}
}
