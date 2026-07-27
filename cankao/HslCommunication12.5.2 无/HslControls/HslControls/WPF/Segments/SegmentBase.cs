using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HslControls.WPF.Segments
{
	[DesignTimeVisible(false)]
	public class SegmentBase : UserControl, iSegment
	{
		protected static double defVertDividerSixteen;

		protected static double defHorizDividerSixteen;

		public static DependencyProperty PenColorProperty;

		public static DependencyProperty SelectedPenColorProperty;

		public static DependencyProperty FillBrushProperty;

		public static DependencyProperty PenThicknessProperty;

		public static DependencyProperty SelectedFillBrushProperty;

		public static DependencyProperty ValueProperty;

		public static DependencyProperty GapWidthProperty;

		public static DependencyProperty ShowDotProperty;

		public static DependencyProperty OnDotProperty;

		public static DependencyProperty ShowColonProperty;

		public static DependencyProperty OnColonProperty;

		public static DependencyProperty TiltAngleProperty;

		public static DependencyProperty RoundedCornersProperty;

		public static DependencyProperty SelectedSegmentsProperty;

		public static DependencyProperty SegmentsBrushProperty;

		public static DependencyProperty VertSegDividerProperty;

		public static DependencyProperty HorizSegDividerProperty;

		public List<int> SelectedSegments
		{
			get
			{
				return (List<int>)GetValue(SelectedSegmentsProperty);
			}
			set
			{
				SetValue(SelectedSegmentsProperty, value);
			}
		}

		public List<Tuple<int, Brush, Color>> SegmentsBrush
		{
			get
			{
				return (List<Tuple<int, Brush, Color>>)GetValue(SegmentsBrushProperty);
			}
			set
			{
				SetValue(SegmentsBrushProperty, value);
			}
		}

		public Brush FillBrush
		{
			get
			{
				return (Brush)GetValue(FillBrushProperty);
			}
			set
			{
				SetValue(FillBrushProperty, value);
			}
		}

		public Brush SelectedFillBrush
		{
			get
			{
				return (Brush)GetValue(SelectedFillBrushProperty);
			}
			set
			{
				SetValue(SelectedFillBrushProperty, value);
			}
		}

		public Color PenColor
		{
			get
			{
				return (Color)GetValue(PenColorProperty);
			}
			set
			{
				SetValue(PenColorProperty, value);
			}
		}

		public Color SelectedPenColor
		{
			get
			{
				return (Color)GetValue(SelectedPenColorProperty);
			}
			set
			{
				SetValue(SelectedPenColorProperty, value);
			}
		}

		public double PenThickness
		{
			get
			{
				return (double)GetValue(PenThicknessProperty);
			}
			set
			{
				SetValue(PenThicknessProperty, value);
			}
		}

		public string Value
		{
			get
			{
				return (string)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		public double GapWidth
		{
			get
			{
				return (double)GetValue(GapWidthProperty);
			}
			set
			{
				SetValue(GapWidthProperty, value);
			}
		}

		public bool RoundedCorners
		{
			get
			{
				return (bool)GetValue(RoundedCornersProperty);
			}
			set
			{
				SetValue(RoundedCornersProperty, value);
			}
		}

		public double TiltAngle
		{
			get
			{
				return (double)GetValue(TiltAngleProperty);
			}
			set
			{
				SetValue(TiltAngleProperty, value);
			}
		}

		public bool ShowDot
		{
			get
			{
				return (bool)GetValue(ShowDotProperty);
			}
			set
			{
				SetValue(ShowDotProperty, value);
			}
		}

		public bool OnDot
		{
			get
			{
				return (bool)GetValue(OnDotProperty);
			}
			set
			{
				SetValue(OnDotProperty, value);
			}
		}

		public bool ShowColon
		{
			get
			{
				return (bool)GetValue(ShowColonProperty);
			}
			set
			{
				SetValue(ShowColonProperty, value);
			}
		}

		public bool OnColon
		{
			get
			{
				return (bool)GetValue(OnColonProperty);
			}
			set
			{
				SetValue(OnColonProperty, value);
			}
		}

		public double VertSegDivider
		{
			get
			{
				return (double)GetValue(VertSegDividerProperty);
			}
			set
			{
				SetValue(VertSegDividerProperty, value);
			}
		}

		public double HorizSegDivider
		{
			get
			{
				return (double)GetValue(HorizSegDividerProperty);
			}
			set
			{
				SetValue(HorizSegDividerProperty, value);
			}
		}

		protected event PropertyChangedCallback PropertyChanged = delegate
		{
		};

		static SegmentBase()
		{
			defVertDividerSixteen = 7.5;
			defHorizDividerSixteen = 11.5;
			PenThicknessProperty = DependencyProperty.Register("PenThickness", typeof(double), typeof(SegmentBase), new PropertyMetadata(1.0, VisualChanged));
			PenColorProperty = DependencyProperty.Register("PenColor", typeof(Color), typeof(SegmentBase), new PropertyMetadata(Color.FromRgb(234, 234, 234), VisualChanged));
			SelectedPenColorProperty = DependencyProperty.Register("SelectedPenColor", typeof(Color), typeof(SegmentBase), new PropertyMetadata(Colors.Black, VisualChanged));
			FillBrushProperty = DependencyProperty.Register("FillBrush", typeof(Brush), typeof(SegmentBase), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(248, 248, 248)), VisualChanged));
			SelectedFillBrushProperty = DependencyProperty.Register("SelectedFillBrush", typeof(Brush), typeof(SegmentBase), new PropertyMetadata(new SolidColorBrush(Colors.Green), VisualChanged));
			TiltAngleProperty = DependencyProperty.Register("TiltAngle", typeof(double), typeof(SegmentBase), new PropertyMetadata(10.0, VisualChanged));
			GapWidthProperty = DependencyProperty.Register("GapWidth", typeof(double), typeof(SegmentBase), new PropertyMetadata(3.0, VisualChanged));
			RoundedCornersProperty = DependencyProperty.Register("RoundedCorners", typeof(bool), typeof(SegmentBase), new PropertyMetadata(false, VisualChanged));
			ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(SegmentBase), new PropertyMetadata("", VisualChanged));
			ShowDotProperty = DependencyProperty.Register("ShowDot", typeof(bool), typeof(SegmentBase), new PropertyMetadata(false, VisualChanged));
			OnDotProperty = DependencyProperty.Register("OnDot", typeof(bool), typeof(SegmentBase), new PropertyMetadata(false, VisualChanged));
			ShowColonProperty = DependencyProperty.Register("ShowColon", typeof(bool), typeof(SegmentBase), new PropertyMetadata(false, VisualChanged));
			OnColonProperty = DependencyProperty.Register("OnColon", typeof(bool), typeof(SegmentBase), new PropertyMetadata(false, VisualChanged));
			SelectedSegmentsProperty = DependencyProperty.Register("SelectedSegments", typeof(List<int>), typeof(SegmentBase), new PropertyMetadata(new List<int>(), VisualChanged));
			SegmentsBrushProperty = DependencyProperty.Register("SegmentsBrush", typeof(List<Tuple<int, Brush, Color>>), typeof(SegmentBase), new PropertyMetadata(new List<Tuple<int, Brush, Color>>(), VisualChanged));
			VertSegDividerProperty = DependencyProperty.Register("VertSegDivider", typeof(double), typeof(SegmentBase), new PropertyMetadata(5.0, VisualChanged));
			HorizSegDividerProperty = DependencyProperty.Register("HorizSegDivider", typeof(double), typeof(SegmentBase), new PropertyMetadata(9.0, VisualChanged));
		}

		private static void VisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			((SegmentBase)sender).PropertyChanged(sender, e);
		}
	}
}
