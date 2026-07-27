using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HslControls.WPF.Segments
{
	public class SevenSegments : SegmentBase
	{
		protected bool isPropertyCahnged = true;

		protected double startPointThickness;

		protected double vertRoundCoef = 0.0;

		protected double horizRoundCoef = 0.0;

		protected List<GeometryWithSegm> GeometryFigures;

		protected double figureStartPointY;

		protected double VertSegW { get; private set; }

		protected double VertSegPartW { get; private set; }

		protected double VertSegSmallPartH { get; private set; }

		protected double HorizSegH { get; private set; }

		protected double HorizSegSmallPartH { get; private set; }

		protected double HorizSegSmallPartW { get; private set; }

		protected double MidPoint { get; private set; }

		protected double GapW { get; private set; }

		protected double DotDiameter { get; private set; }

		protected double ColonDiameter { get; private set; }

		protected double VirtualWidth { get; private set; }

		protected double VirtualHeight { get; private set; }

		protected double VertSegBotPartW { get; private set; }

		protected PointCollection LeftBottomSegmPoints { get; private set; }

		protected PointCollection LeftTopSegmPoints { get; private set; }

		protected PointCollection TopSegmPoints { get; set; }

		protected PointCollection BottomSegmPoints { get; set; }

		protected PointCollection MiddleSegmPoints { get; set; }

		protected PointCollection RightTopSegmPoints { get; private set; }

		protected PointCollection RightBottomSegmPoints { get; private set; }

		public SevenSegments()
		{
			base.PropertyChanged += OnPropertyChanged;
			vertRoundCoef = 5.5;
			horizRoundCoef = 15.0;
		}

		private void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			SegmentBase segmentBase = (SegmentBase)sender;
			isPropertyCahnged = true;
			segmentBase.InvalidateVisual();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			CalculateMeasures();
			AssignSegments();
			ClearSegmentsSelection();
			SetSegments();
			foreach (GeometryWithSegm entry in GeometryFigures)
			{
				if (base.SegmentsBrush.Any())
				{
					Tuple<int, Brush, Color> tuple = base.SegmentsBrush.SingleOrDefault((Tuple<int, Brush, Color> s) => s.Item1 == entry.SegmentNumber);
					drawingContext.DrawGeometry(pen: new Pen(new SolidColorBrush(tuple?.Item3 ?? base.PenColor), base.PenThickness), brush: (tuple != null) ? tuple.Item2 : base.FillBrush, geometry: entry.Geometry);
				}
				else
				{
					drawingContext.DrawGeometry(pen: new Pen(new SolidColorBrush(entry.IsSelected ? base.SelectedPenColor : base.PenColor), base.PenThickness), brush: entry.IsSelected ? base.SelectedFillBrush : base.FillBrush, geometry: entry.Geometry);
				}
			}
			DrawDot(drawingContext);
			DrawColon(drawingContext);
		}

		public void ClearSegments()
		{
			base.Value = string.Empty;
			base.SelectedSegments = new List<int>();
			base.SegmentsBrush = new List<Tuple<int, Brush, Color>>();
		}

		protected virtual void AssignSegments()
		{
			GeometryFigures = new List<GeometryWithSegm>();
			GeometryFigures.Add(new GeometryWithSegm(LeftBottomSegement(), 4));
			GeometryFigures.Add(new GeometryWithSegm(LeftTopSegement(), 5));
			GeometryFigures.Add(new GeometryWithSegm(RightTopSegement(), 1));
			GeometryFigures.Add(new GeometryWithSegm(RightBottomSegement(), 2));
			GeometryFigures.Add(new GeometryWithSegm(MiddleSegement(), 6));
			GeometryFigures.Add(new GeometryWithSegm(TopSegement(), 0));
			GeometryFigures.Add(new GeometryWithSegm(BottomSegement(), 3));
		}

		protected void SetSegments()
		{
			if (base.SelectedSegments.Any())
			{
				int i;
				for (i = 0; i < base.SelectedSegments.Count; i++)
				{
					GeometryFigures.Single((GeometryWithSegm t) => t.SegmentNumber == base.SelectedSegments[i]).IsSelected = true;
				}
			}
			else
			{
				ValueSegmentsSelection();
			}
		}

		private void CalculateMeasures()
		{
			HorizSegH = base.ActualHeight / base.HorizSegDivider;
			HorizSegSmallPartH = HorizSegH / 4.0;
			VertSegW = base.ActualWidth / base.VertSegDivider;
			VertSegPartW = VertSegW / 3.5;
			VertSegSmallPartH = VertSegW / 3.5;
			VertSegBotPartW = VertSegW / 2.0;
			HorizSegSmallPartW = VertSegW / 4.0;
			MidPoint = base.ActualHeight / 2.0;
			GapW = base.GapWidth;
			DotDiameter = HorizSegH;
			ColonDiameter = HorizSegH;
			VirtualHeight = (base.ShowDot ? (base.ActualHeight - DotDiameter / 1.5) : base.ActualHeight);
			VirtualWidth = (base.ShowDot ? (base.ActualWidth - DotDiameter / 1.5) : base.ActualWidth);
			figureStartPointY = VirtualHeight - (HorizSegSmallPartH + GapW + VertSegSmallPartH);
			startPointThickness = base.PenThickness / 2.0;
		}

		protected virtual void ValueSegmentsSelection()
		{
			if (int.TryParse(base.Value, out var result))
			{
				if (result > 9)
				{
					result = 9;
				}
				if (result < 0)
				{
					result = 0;
				}
				switch (result)
				{
				case 0:
					SelectSegments(5, 0, 1, 2, 3, 4);
					break;
				case 1:
					SelectSegments(1, 2);
					break;
				case 2:
					SelectSegments(0, 1, 6, 4, 3);
					break;
				case 3:
					SelectSegments(0, 1, 6, 2, 3);
					break;
				case 4:
					SelectSegments(5, 1, 6, 2);
					break;
				case 5:
					SelectSegments(5, 0, 6, 2, 3);
					break;
				case 6:
					SelectSegments(5, 0, 6, 2, 4, 3);
					break;
				case 7:
					SelectSegments(5, 0, 1, 2);
					break;
				case 8:
					SelectSegments(5, 0, 1, 6, 4, 2, 3);
					break;
				case 9:
					SelectSegments(5, 0, 1, 6, 2, 3);
					break;
				}
			}
			if (base.Value == "-")
			{
				SelectSegments(6);
			}
		}

		protected void DrawDot(DrawingContext drawingContext)
		{
			if (base.ShowDot)
			{
				PathGeometry pathGeometry = new PathGeometry();
				drawingContext.DrawGeometry(pen: new Pen(new SolidColorBrush(base.OnDot ? base.SelectedPenColor : base.PenColor), base.PenThickness), geometry: CreateEllipseGeometry(new Point(base.ActualWidth - DotDiameter / 2.0, base.ActualHeight - DotDiameter / 2.0), pathGeometry, DotDiameter / 2.0), brush: base.OnDot ? base.SelectedFillBrush : base.FillBrush);
			}
		}

		private void DrawColon(DrawingContext drawingContext)
		{
			if (base.ShowColon)
			{
				PathGeometry pathGeometry = new PathGeometry();
				double num = MiddleSegmPoints[2].Y - GapW - HorizSegH - (HorizSegH + GapW);
				double y = HorizSegH + GapW + num / 2.0 + ColonDiameter / 2.0;
				double num2 = XByAngle(y) + VertSegW;
				double num3 = BottomSegmPoints[2].Y - GapW - (MiddleSegmPoints[0].Y + GapW + HorizSegH);
				double y2 = MiddleSegmPoints[0].Y + GapW + HorizSegH + num3 / 2.0 - ColonDiameter / 2.0;
				double num4 = XByAngle(y2) + VertSegW;
				double x = num2 + (VirtualWidth - num4 - num2) / 2.0;
				double x2 = num4 + (VirtualWidth - num2 - num4) / 2.0;
				Pen pen = new Pen(new SolidColorBrush(base.OnColon ? base.SelectedPenColor : base.PenColor), base.PenThickness);
				pathGeometry = CreateEllipseGeometry(new Point(x, y), pathGeometry, ColonDiameter / 2.0);
				drawingContext.DrawGeometry(base.OnColon ? base.SelectedFillBrush : base.FillBrush, pen, pathGeometry);
				drawingContext.DrawGeometry(geometry: CreateEllipseGeometry(new Point(x2, y2), pathGeometry, ColonDiameter / 2.0), brush: base.OnColon ? base.SelectedFillBrush : base.FillBrush, pen: pen);
			}
		}

		private PathGeometry CreateEllipseGeometry(Point centerPoint, PathGeometry pathGeometry, double diameter)
		{
			EllipseGeometry ellipseGeometry = new EllipseGeometry();
			ellipseGeometry.Center = centerPoint;
			ellipseGeometry.RadiusX = diameter;
			ellipseGeometry.RadiusY = diameter;
			pathGeometry = PathGeometry.CreateFromGeometry(ellipseGeometry);
			pathGeometry.Transform = new SkewTransform(0.0 - base.TiltAngle, 0.0, centerPoint.X, centerPoint.Y);
			return pathGeometry;
		}

		protected void SelectSegments(params int[] segmNumbers)
		{
			int i;
			for (i = 0; i < segmNumbers.Length; i++)
			{
				GeometryFigures.Single((GeometryWithSegm t) => t.SegmentNumber == segmNumbers[i]).IsSelected = true;
			}
		}

		protected void ClearSegmentsSelection()
		{
			GeometryFigures.ForEach(delegate(GeometryWithSegm c)
			{
				c.IsSelected = false;
			});
		}

		protected PathGeometry SegmentPathGeometry(Point startPoint, PolyLineSegment polyLineSegment)
		{
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(polyLineSegment);
			return pathGeometry;
		}

		protected PathGeometry SegmentGeometry(PointCollection assignPoints, PointCollection drawnPoints)
		{
			assignPoints = drawnPoints;
			return SegmentPathGeometry(assignPoints[0], new PolyLineSegment
			{
				Points = assignPoints
			});
		}

		protected double XByAngle(double y)
		{
			double num = figureStartPointY - y;
			return TanAngle() * num;
		}

		protected double TanAngle()
		{
			return Math.Tan(base.TiltAngle * (Math.PI / 180.0));
		}

		private double GapShift()
		{
			return GapW * 0.75;
		}

		protected PointCollection GetLeftTopSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = VirtualHeight / 2.0 - HorizSegH / 2.0;
			double num2 = XByAngle(HorizSegSmallPartH + GapW + VertSegSmallPartH + startPointThickness);
			Point value;
			if (base.RoundedCorners)
			{
				double y = (VirtualHeight - startPointThickness) / vertRoundCoef;
				value = new Point((base.RoundedCorners ? XByAngle(y) : 0.0) + startPointThickness, y);
			}
			else
			{
				value = new Point(num2 + startPointThickness, HorizSegSmallPartH + startPointThickness + GapW + VertSegSmallPartH);
			}
			double num3 = XByAngle(HorizSegSmallPartH + GapShift());
			double num4 = XByAngle(HorizSegH + GapW / 2.0);
			double num5 = XByAngle(num - GapW / 2.0 - startPointThickness);
			double num6 = XByAngle(VirtualHeight / 2.0 - GapW / 2.0 - startPointThickness);
			pointCollection.Add(new Point(num2 + startPointThickness, HorizSegSmallPartH + GapW + VertSegSmallPartH + startPointThickness));
			pointCollection.Add(new Point(num3 + VertSegPartW + startPointThickness, HorizSegSmallPartH + startPointThickness + GapShift()));
			pointCollection.Add(new Point(num4 + VertSegW + startPointThickness, HorizSegH + startPointThickness + GapW / 2.0));
			pointCollection.Add(new Point(num5 + VertSegW + startPointThickness, num - GapW / 2.0));
			pointCollection.Add(new Point(num6 + VertSegBotPartW + startPointThickness, VirtualHeight / 2.0 - GapW / 2.0));
			pointCollection.Add(new Point(num6 + startPointThickness, VirtualHeight / 2.0 - GapW / 2.0));
			pointCollection.Add(value);
			return pointCollection;
		}

		protected PointCollection GetLeftBottomSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double y = VirtualHeight / 2.0 + HorizSegH / 2.0 + GapW / 2.0;
			double y2 = VirtualHeight - (HorizSegH + GapW / 2.0) - startPointThickness;
			double num = XByAngle(VirtualHeight / 2.0 + GapW / 2.0);
			double num2 = XByAngle(y);
			double num3 = XByAngle(y2);
			Point value;
			if (base.RoundedCorners)
			{
				double y3 = VirtualHeight - startPointThickness - VirtualHeight / vertRoundCoef;
				value = new Point((base.RoundedCorners ? XByAngle(y3) : 0.0) + startPointThickness, y3);
			}
			else
			{
				value = new Point(startPointThickness, figureStartPointY - startPointThickness);
			}
			pointCollection.Add(new Point(num + startPointThickness, VirtualHeight / 2.0 + GapW / 2.0));
			pointCollection.Add(new Point(num + VertSegBotPartW + startPointThickness, VirtualHeight / 2.0 + GapW / 2.0));
			pointCollection.Add(new Point(num2 + VertSegW + startPointThickness, y));
			pointCollection.Add(new Point(num3 + VertSegW + startPointThickness, y2));
			pointCollection.Add(new Point(VertSegPartW + startPointThickness, VirtualHeight - startPointThickness - (HorizSegSmallPartH + GapShift())));
			pointCollection.Add(new Point(startPointThickness, figureStartPointY - startPointThickness));
			pointCollection.Add(value);
			return pointCollection;
		}

		protected PointCollection GetRightBottomSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[3].X, VirtualHeight - LeftTopSegmPoints[3].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[4].X, VirtualHeight - LeftTopSegmPoints[4].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[5].X, VirtualHeight - LeftTopSegmPoints[5].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[6].X, VirtualHeight - LeftTopSegmPoints[6].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[0].X, VirtualHeight - LeftTopSegmPoints[0].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[1].X, VirtualHeight - LeftTopSegmPoints[1].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftTopSegmPoints[2].X, VirtualHeight - LeftTopSegmPoints[2].Y));
			return pointCollection;
		}

		protected PointCollection GetRightTopSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[3].X, VirtualHeight - LeftBottomSegmPoints[3].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[4].X, VirtualHeight - LeftBottomSegmPoints[4].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[5].X, VirtualHeight - LeftBottomSegmPoints[5].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[6].X, VirtualHeight - LeftBottomSegmPoints[6].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[0].X, VirtualHeight - LeftBottomSegmPoints[0].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[1].X, VirtualHeight - LeftBottomSegmPoints[1].Y));
			pointCollection.Add(new Point(VirtualWidth - LeftBottomSegmPoints[2].X, VirtualHeight - LeftBottomSegmPoints[2].Y));
			return pointCollection;
		}

		protected PointCollection GetMiddleSegmPoints()
		{
			double num = XByAngle(VirtualHeight / 2.0 + HorizSegH / 2.0) + (VertSegW + GapW);
			double num2 = XByAngle(VirtualHeight / 2.0) + VertSegBotPartW + GapW;
			double num3 = XByAngle(VirtualHeight / 2.0 - HorizSegH / 2.0) + VertSegW + GapW;
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(num, VirtualHeight / 2.0 + HorizSegH / 2.0));
			pointCollection.Add(new Point(num2, VirtualHeight / 2.0));
			pointCollection.Add(new Point(num3, VirtualHeight / 2.0 - HorizSegH / 2.0));
			pointCollection.Add(new Point(VirtualWidth - num, RightTopSegmPoints[6].Y + GapW / 2.0));
			pointCollection.Add(new Point(VirtualWidth - num2, VirtualHeight / 2.0));
			pointCollection.Add(new Point(VirtualWidth - num3, RightBottomSegmPoints[0].Y - GapW / 2.0));
			return pointCollection;
		}

		protected PointCollection GetTopSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = LeftTopSegmPoints[1].X + HorizSegSmallPartW;
			double num2 = RightTopSegmPoints[1].X - HorizSegSmallPartW;
			double num3 = (base.RoundedCorners ? (VirtualWidth / horizRoundCoef) : 0.0);
			pointCollection.Add(new Point(LeftTopSegmPoints[2].X + GapW, HorizSegH + startPointThickness));
			pointCollection.Add(new Point(LeftTopSegmPoints[1].X + GapShift(), HorizSegSmallPartH + startPointThickness));
			pointCollection.Add(new Point(num, startPointThickness));
			pointCollection.Add(new Point(num + num3, startPointThickness));
			pointCollection.Add(new Point(num2 - num3, startPointThickness));
			pointCollection.Add(new Point(num2, startPointThickness));
			pointCollection.Add(new Point(RightTopSegmPoints[1].X - GapShift(), HorizSegSmallPartH + startPointThickness));
			pointCollection.Add(new Point(RightTopSegmPoints[0].X - GapW, HorizSegH + startPointThickness));
			return pointCollection;
		}

		protected PointCollection GetBottomSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = LeftBottomSegmPoints[4].X + HorizSegSmallPartW;
			double num2 = RightBottomSegmPoints[5].X - HorizSegSmallPartW;
			double num3 = (base.RoundedCorners ? (VirtualWidth / horizRoundCoef) : 0.0);
			pointCollection.Add(new Point(num, VirtualHeight - startPointThickness));
			pointCollection.Add(new Point(LeftBottomSegmPoints[4].X + GapShift(), VirtualHeight - HorizSegSmallPartH - startPointThickness));
			pointCollection.Add(new Point(LeftBottomSegmPoints[3].X + GapW, VirtualHeight - HorizSegH - startPointThickness));
			pointCollection.Add(new Point(RightBottomSegmPoints[6].X - GapW, VirtualHeight - HorizSegH - startPointThickness));
			pointCollection.Add(new Point(RightBottomSegmPoints[5].X - GapShift(), VirtualHeight - HorizSegSmallPartH - startPointThickness));
			pointCollection.Add(new Point(num2, VirtualHeight - startPointThickness));
			pointCollection.Add(new Point(num2 - num3, VirtualHeight - startPointThickness));
			pointCollection.Add(new Point(num + num3, VirtualHeight - startPointThickness));
			return pointCollection;
		}

		protected PathGeometry RightTopSegement()
		{
			RightTopSegmPoints = GetRightTopSegmPoints();
			Point startPoint = RightTopSegmPoints[0];
			LineSegment value = new LineSegment(RightTopSegmPoints[0], true);
			LineSegment value2 = new LineSegment(RightTopSegmPoints[1], true);
			LineSegment value3 = new LineSegment(RightTopSegmPoints[4], true);
			LineSegment value4 = new LineSegment(RightTopSegmPoints[5], true);
			LineSegment value5 = new LineSegment(RightTopSegmPoints[6], true);
			PointCollection collection = new PointCollection
			{
				RightTopSegmPoints[1],
				RightTopSegmPoints[2],
				RightTopSegmPoints[3]
			};
			PolyBezierSegment value6 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value6);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value5);
			return pathGeometry;
		}

		protected PathGeometry MiddleSegement()
		{
			MiddleSegmPoints = GetMiddleSegmPoints();
			return SegmentPathGeometry(MiddleSegmPoints[0], new PolyLineSegment
			{
				Points = MiddleSegmPoints
			});
		}

		protected PathGeometry RightBottomSegement()
		{
			RightBottomSegmPoints = GetRightBottomSegmPoints();
			Point startPoint = RightBottomSegmPoints[0];
			LineSegment value = new LineSegment(RightBottomSegmPoints[0], true);
			LineSegment value2 = new LineSegment(RightBottomSegmPoints[1], true);
			LineSegment value3 = new LineSegment(RightBottomSegmPoints[2], true);
			LineSegment value4 = new LineSegment(RightBottomSegmPoints[3], true);
			LineSegment value5 = new LineSegment(RightBottomSegmPoints[6], true);
			PointCollection collection = new PointCollection
			{
				RightBottomSegmPoints[3],
				RightBottomSegmPoints[4],
				RightBottomSegmPoints[5]
			};
			PolyBezierSegment value6 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value6);
			pathFigure.Segments.Add(value5);
			return pathGeometry;
		}

		protected PathGeometry TopSegement()
		{
			TopSegmPoints = GetTopSegmPoints();
			Point startPoint = TopSegmPoints[0];
			LineSegment value = new LineSegment(TopSegmPoints[0], true);
			LineSegment value2 = new LineSegment(TopSegmPoints[1], true);
			LineSegment value3 = new LineSegment(TopSegmPoints[3], true);
			LineSegment value4 = new LineSegment(TopSegmPoints[4], true);
			LineSegment value5 = new LineSegment(TopSegmPoints[6], true);
			LineSegment value6 = new LineSegment(TopSegmPoints[7], true);
			PointCollection collection = new PointCollection
			{
				TopSegmPoints[1],
				TopSegmPoints[2],
				TopSegmPoints[3]
			};
			PolyBezierSegment value7 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PointCollection collection2 = new PointCollection
			{
				TopSegmPoints[4],
				TopSegmPoints[5],
				TopSegmPoints[6]
			};
			PolyBezierSegment value8 = new PolyBezierSegment
			{
				Points = new PointCollection(collection2)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value7);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value8);
			pathFigure.Segments.Add(value5);
			pathFigure.Segments.Add(value6);
			return pathGeometry;
		}

		protected PathGeometry LeftTopSegement()
		{
			LeftTopSegmPoints = GetLeftTopSegmPoints();
			Point startPoint = LeftTopSegmPoints[6];
			LineSegment lineSegment = new LineSegment(LeftTopSegmPoints[6], true);
			LineSegment lineSegment2 = new LineSegment(LeftTopSegmPoints[1], true);
			LineSegment value = new LineSegment(LeftTopSegmPoints[2], true);
			LineSegment value2 = new LineSegment(LeftTopSegmPoints[3], true);
			LineSegment value3 = new LineSegment(LeftTopSegmPoints[4], true);
			LineSegment value4 = new LineSegment(LeftTopSegmPoints[5], true);
			PointCollection collection = new PointCollection
			{
				LeftTopSegmPoints[6],
				LeftTopSegmPoints[0],
				LeftTopSegmPoints[1]
			};
			PolyBezierSegment value5 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value5);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			return pathGeometry;
		}

		protected PathGeometry LeftBottomSegement()
		{
			LeftBottomSegmPoints = GetLeftBottomSegmPoints();
			Point startPoint = LeftBottomSegmPoints[0];
			LineSegment value = new LineSegment(LeftBottomSegmPoints[0], true);
			LineSegment value2 = new LineSegment(LeftBottomSegmPoints[1], true);
			LineSegment value3 = new LineSegment(LeftBottomSegmPoints[2], true);
			LineSegment value4 = new LineSegment(LeftBottomSegmPoints[3], true);
			LineSegment value5 = new LineSegment(LeftBottomSegmPoints[4], true);
			PointCollection collection = new PointCollection
			{
				LeftBottomSegmPoints[4],
				LeftBottomSegmPoints[5],
				LeftBottomSegmPoints[6]
			};
			PolyBezierSegment value6 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value5);
			pathFigure.Segments.Add(value6);
			return pathGeometry;
		}

		protected PathGeometry BottomSegement()
		{
			BottomSegmPoints = GetBottomSegmPoints();
			Point startPoint = BottomSegmPoints[1];
			LineSegment lineSegment = new LineSegment(BottomSegmPoints[0], true);
			LineSegment value = new LineSegment(BottomSegmPoints[1], true);
			LineSegment value2 = new LineSegment(BottomSegmPoints[2], true);
			LineSegment value3 = new LineSegment(BottomSegmPoints[3], true);
			LineSegment value4 = new LineSegment(BottomSegmPoints[4], true);
			LineSegment value5 = new LineSegment(BottomSegmPoints[6], true);
			LineSegment value6 = new LineSegment(BottomSegmPoints[7], true);
			PointCollection collection = new PointCollection
			{
				BottomSegmPoints[4],
				BottomSegmPoints[5],
				BottomSegmPoints[6]
			};
			PolyBezierSegment value7 = new PolyBezierSegment
			{
				Points = new PointCollection(collection)
			};
			PointCollection collection2 = new PointCollection
			{
				BottomSegmPoints[7],
				BottomSegmPoints[0],
				BottomSegmPoints[1]
			};
			PolyBezierSegment value8 = new PolyBezierSegment
			{
				Points = new PointCollection(collection2)
			};
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure();
			pathFigure.StartPoint = startPoint;
			pathFigure.IsClosed = true;
			pathGeometry.Figures.Add(pathFigure);
			pathFigure.Segments.Add(value);
			pathFigure.Segments.Add(value2);
			pathFigure.Segments.Add(value3);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value7);
			pathFigure.Segments.Add(value5);
			pathFigure.Segments.Add(value6);
			pathFigure.Segments.Add(value8);
			return pathGeometry;
		}
	}
}
