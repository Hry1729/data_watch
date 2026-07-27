using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HslControls.WPF.Segments
{
	public class SixteenSegments : SevenSegments
	{
		protected double DiagSegW;

		protected PointCollection LeftTopHorizSegmPoints { get; private set; }

		protected PointCollection RightTopHorizSegmPoints { get; private set; }

		protected PointCollection LeftBottomHorizSegmPoints { get; private set; }

		protected PointCollection RightBottomHorizSegmPoints { get; private set; }

		protected PointCollection LeftMiddleSegmPoints { get; private set; }

		protected PointCollection RightMiddleSegmPoints { get; private set; }

		protected PointCollection TopVerticalSegmPoints { get; private set; }

		protected PointCollection BottomVerticalSegmPoints { get; private set; }

		protected PointCollection BottomLeftDiagSegmPoints { get; private set; }

		protected PointCollection TopLeftDiagSegmPoints { get; private set; }

		protected PointCollection TopRightDiagSegmPoints { get; private set; }

		protected PointCollection BottomRightDiagSegmPoints { get; private set; }

		static SixteenSegments()
		{
			SegmentBase.VertSegDividerProperty.OverrideMetadata(typeof(SixteenSegments), new FrameworkPropertyMetadata(SegmentBase.defVertDividerSixteen));
			SegmentBase.HorizSegDividerProperty.OverrideMetadata(typeof(SixteenSegments), new FrameworkPropertyMetadata(SegmentBase.defHorizDividerSixteen));
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
		}

		protected override void ValueSegmentsSelection()
		{
			switch ((base.Value.ToCharArray().Count() > 0) ? base.Value[0] : ' ')
			{
			case '0':
				SelectSegments(0, 1, 2, 3, 4, 5, 6, 7);
				break;
			case '1':
				SelectSegments(10, 2, 3);
				break;
			case '2':
				SelectSegments(0, 1, 2, 11, 12, 6, 5, 4);
				break;
			case '3':
				SelectSegments(0, 1, 2, 3, 4, 5, 11, 12);
				break;
			case '4':
				SelectSegments(7, 11, 12, 2, 3);
				break;
			case '5':
				SelectSegments(0, 1, 12, 11, 7, 4, 5, 3);
				break;
			case '6':
				SelectSegments(15, 16, 5, 6, 7, 8, 4, 17, 18, 2);
				break;
			case '7':
				SelectSegments(0, 1, 2, 3);
				break;
			case '8':
				SelectSegments(0, 1, 2, 3, 4, 5, 6, 7, 11, 12);
				break;
			case '9':
				SelectSegments(0, 1, 2, 3, 4, 5, 7, 11, 12);
				break;
			case 'A':
				SelectSegments(0, 1, 2, 3, 6, 7, 11, 12);
				break;
			case 'B':
				SelectSegments(0, 1, 2, 3, 4, 5, 9, 14, 12);
				break;
			case 'C':
				SelectSegments(0, 1, 4, 5, 6, 7);
				break;
			case 'D':
				SelectSegments(0, 1, 2, 3, 4, 5, 9, 14);
				break;
			case 'E':
				SelectSegments(0, 1, 4, 5, 6, 7, 11, 12);
				break;
			case 'F':
				SelectSegments(0, 1, 6, 7, 11, 12);
				break;
			case 'G':
				SelectSegments(0, 1, 3, 4, 5, 6, 7, 12);
				break;
			case 'H':
				SelectSegments(2, 3, 6, 7, 11, 12);
				break;
			case 'I':
				SelectSegments(0, 1, 4, 5, 9, 14);
				break;
			case 'J':
				SelectSegments(2, 3, 4, 5, 6);
				break;
			case 'K':
				SelectSegments(6, 7, 11, 10, 15);
				break;
			case 'L':
				SelectSegments(4, 5, 6, 7);
				break;
			case 'M':
				SelectSegments(2, 3, 6, 7, 8, 10);
				break;
			case 'N':
				SelectSegments(2, 3, 6, 7, 8, 15);
				break;
			case 'O':
				SelectSegments(0, 1, 2, 3, 4, 5, 6, 7);
				break;
			case 'P':
				SelectSegments(6, 7, 0, 1, 2, 11, 12);
				break;
			case 'Q':
				SelectSegments(0, 1, 2, 3, 4, 5, 6, 7, 15);
				break;
			case 'R':
				SelectSegments(6, 7, 0, 1, 2, 11, 12, 15);
				break;
			case 'S':
				SelectSegments(0, 1, 12, 11, 7, 4, 5, 3);
				break;
			case 'T':
				SelectSegments(0, 1, 9, 14);
				break;
			case 'U':
				SelectSegments(2, 3, 4, 5, 6, 7);
				break;
			case 'V':
				SelectSegments(6, 7, 13, 10);
				break;
			case 'W':
				SelectSegments(6, 7, 2, 3, 13, 15);
				break;
			case 'X':
				SelectSegments(8, 15, 13, 10);
				break;
			case 'Y':
				SelectSegments(7, 11, 12, 14, 2);
				break;
			case 'Z':
				SelectSegments(0, 1, 10, 13, 4, 5);
				break;
			case '-':
				SelectSegments(11, 12);
				break;
			}
		}

		protected override void AssignSegments()
		{
			GeometryFigures = new List<GeometryWithSegm>();
			DiagSegW = base.VertSegBotPartW;
			GeometryFigures.Add(new GeometryWithSegm(LeftBottomSegement(), 6));
			GeometryFigures.Add(new GeometryWithSegm(LeftTopSegement(), 7));
			GeometryFigures.Add(new GeometryWithSegm(RightTopSegement(), 2));
			GeometryFigures.Add(new GeometryWithSegm(RightBottomSegement(), 3));
			base.TopSegmPoints = GetTopSegmPoints();
			base.BottomSegmPoints = GetBottomSegmPoints();
			GeometryFigures.Add(new GeometryWithSegm(LeftTopHorizSegement(), 0));
			GeometryFigures.Add(new GeometryWithSegm(RightTopHorizSegement(), 1));
			GeometryFigures.Add(new GeometryWithSegm(LeftBottomHorizSegement(), 5));
			GeometryFigures.Add(new GeometryWithSegm(RightBottomHorizSegement(), 4));
			base.MiddleSegmPoints = GetMiddleSegmPoints();
			GeometryFigures.Add(new GeometryWithSegm(LeftMiddleSegement(), 11));
			GeometryFigures.Add(new GeometryWithSegm(RightMiddleSegement(), 12));
			GeometryFigures.Add(new GeometryWithSegm(TopVerticalSegment(), 9));
			GeometryFigures.Add(new GeometryWithSegm(BottomVerticalSegment(), 14));
			GeometryFigures.Add(new GeometryWithSegm(BottomLeftDiagSegment(), 13));
			GeometryFigures.Add(new GeometryWithSegm(TopLeftDiagSegment(), 8));
			GeometryFigures.Add(new GeometryWithSegm(TopRightDiagSegment(), 10));
			GeometryFigures.Add(new GeometryWithSegm(BottomRightDiagSegment(), 15));
		}

		protected PointCollection GetLeftTopHorizSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = XByAngle(0.0) + base.VertSegW + base.GapW;
			double num2 = base.VirtualWidth - (XByAngle(base.VirtualHeight) + base.VertSegW + base.GapW) - num;
			double num3 = base.TopSegmPoints[7].X - base.TopSegmPoints[0].X;
			pointCollection.Add(base.TopSegmPoints[0]);
			pointCollection.Add(base.TopSegmPoints[1]);
			pointCollection.Add(base.TopSegmPoints[2]);
			pointCollection.Add(base.TopSegmPoints[3]);
			pointCollection.Add(new Point(num + (num2 / 2.0 - base.GapW / 2.0), base.TopSegmPoints[2].Y));
			pointCollection.Add(new Point(base.TopSegmPoints[0].X + (num3 / 2.0 - base.GapW / 2.0), base.TopSegmPoints[7].Y));
			return pointCollection;
		}

		protected PointCollection GetRightTopHorizSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = XByAngle(0.0) + base.VertSegW + base.GapW;
			double num2 = base.VirtualWidth - (XByAngle(base.VirtualHeight) + base.VertSegW + base.GapW) - num;
			double num3 = base.TopSegmPoints[7].X - base.TopSegmPoints[0].X;
			pointCollection.Add(new Point(base.TopSegmPoints[0].X + (num3 / 2.0 + base.GapW / 2.0), base.TopSegmPoints[7].Y));
			pointCollection.Add(new Point(num + (num2 / 2.0 + base.GapW / 2.0), base.TopSegmPoints[2].Y));
			pointCollection.Add(base.TopSegmPoints[4]);
			pointCollection.Add(base.TopSegmPoints[5]);
			pointCollection.Add(base.TopSegmPoints[6]);
			pointCollection.Add(base.TopSegmPoints[7]);
			return pointCollection;
		}

		protected PointCollection GetLeftBottomHorizSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(base.BottomSegmPoints[0]);
			pointCollection.Add(base.BottomSegmPoints[1]);
			pointCollection.Add(base.BottomSegmPoints[2]);
			pointCollection.Add(new Point(base.VirtualWidth - RightTopHorizSegmPoints[0].X, base.BottomSegmPoints[2].Y));
			pointCollection.Add(new Point(base.VirtualWidth - RightTopHorizSegmPoints[1].X, base.BottomSegmPoints[0].Y));
			pointCollection.Add(base.BottomSegmPoints[7]);
			return pointCollection;
		}

		protected PointCollection GetRightBottomHorizSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(base.VirtualWidth - LeftTopHorizSegmPoints[4].X, base.BottomSegmPoints[0].Y));
			pointCollection.Add(new Point(base.VirtualWidth - LeftTopHorizSegmPoints[5].X, base.BottomSegmPoints[2].Y));
			pointCollection.Add(base.BottomSegmPoints[3]);
			pointCollection.Add(base.BottomSegmPoints[4]);
			pointCollection.Add(base.BottomSegmPoints[5]);
			pointCollection.Add(base.BottomSegmPoints[6]);
			return pointCollection;
		}

		protected PointCollection GetLeftMiddleSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = base.MiddleSegmPoints[3].X - base.MiddleSegmPoints[2].X;
			double num2 = base.MiddleSegmPoints[5].X - base.MiddleSegmPoints[0].X;
			pointCollection.Add(base.MiddleSegmPoints[0]);
			pointCollection.Add(base.MiddleSegmPoints[1]);
			pointCollection.Add(base.MiddleSegmPoints[2]);
			pointCollection.Add(new Point(base.MiddleSegmPoints[2].X + (num / 2.0 - base.GapW / 2.0), base.MiddleSegmPoints[2].Y));
			pointCollection.Add(new Point(base.MiddleSegmPoints[0].X + (num2 / 2.0 - base.GapW / 2.0), base.MiddleSegmPoints[5].Y));
			return pointCollection;
		}

		protected PointCollection GetRightMiddleSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = base.MiddleSegmPoints[3].X - base.MiddleSegmPoints[2].X;
			double num2 = base.MiddleSegmPoints[5].X - base.MiddleSegmPoints[0].X;
			pointCollection.Add(new Point(base.MiddleSegmPoints[0].X + (num2 / 2.0 + base.GapW / 2.0), base.MiddleSegmPoints[5].Y));
			pointCollection.Add(new Point(base.MiddleSegmPoints[2].X + (num / 2.0 + base.GapW / 2.0), base.MiddleSegmPoints[2].Y));
			pointCollection.Add(base.MiddleSegmPoints[3]);
			pointCollection.Add(base.MiddleSegmPoints[4]);
			pointCollection.Add(base.MiddleSegmPoints[5]);
			return pointCollection;
		}

		protected PointCollection GetTopVerticalSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = base.RightTopSegmPoints[0].X - base.LeftTopSegmPoints[2].X;
			double y = base.MiddleSegmPoints[2].Y - base.GapW - base.HorizSegH;
			double num2 = base.VertSegW / 2.5;
			double num3 = XByAngle(y) + base.VertSegW;
			pointCollection.Add(new Point(base.LeftTopSegmPoints[2].X + (num / 2.0 - num2), base.HorizSegH + base.GapW));
			pointCollection.Add(new Point(base.LeftTopSegmPoints[2].X + (num / 2.0 + num2), base.HorizSegH + base.GapW));
			pointCollection.Add(new Point(num3 + (num / 2.0 + num2), y));
			pointCollection.Add(new Point(base.LeftTopSegmPoints[3].X + (num / 2.0 + num2 / 2.0), base.MiddleSegmPoints[2].Y - base.GapW));
			pointCollection.Add(new Point(base.LeftTopSegmPoints[3].X + (num / 2.0 - num2 / 2.0), base.MiddleSegmPoints[2].Y - base.GapW));
			pointCollection.Add(new Point(num3 + (num / 2.0 - num2), y));
			return pointCollection;
		}

		protected PointCollection GetBottomVerticalSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = base.RightBottomSegmPoints[0].X - base.LeftBottomSegmPoints[2].X;
			double y = base.MiddleSegmPoints[0].Y + base.GapW + base.HorizSegH;
			double num2 = XByAngle(y) + base.VertSegW;
			double num3 = base.VertSegW / 2.5;
			pointCollection.Add(new Point(num2 + (num / 2.0 - num3), y));
			pointCollection.Add(new Point(base.LeftBottomSegmPoints[2].X + (num / 2.0 - num3 / 2.0), base.MiddleSegmPoints[0].Y + base.GapW));
			pointCollection.Add(new Point(base.LeftBottomSegmPoints[2].X + (num / 2.0 + num3 / 2.0), base.MiddleSegmPoints[0].Y + base.GapW));
			pointCollection.Add(new Point(num2 + (num / 2.0 + num3), y));
			pointCollection.Add(new Point(base.LeftBottomSegmPoints[3].X + (num / 2.0 + num3), base.BottomSegmPoints[2].Y - base.GapW));
			pointCollection.Add(new Point(base.LeftBottomSegmPoints[3].X + (num / 2.0 - num3), base.BottomSegmPoints[2].Y - base.GapW));
			return pointCollection;
		}

		protected PointCollection GetBottomLeftDiagSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double num = base.BottomSegmPoints[2].Y - base.GapW;
			double num2 = XByAngle(num) + base.VertSegW;
			double y = num - base.HorizSegH / 2.0;
			double num3 = XByAngle(y) + base.VertSegW;
			pointCollection.Add(new Point(BottomVerticalSegmPoints[1].X - base.GapW - DiagSegW, BottomVerticalSegmPoints[1].Y));
			pointCollection.Add(new Point(BottomVerticalSegmPoints[1].X - base.GapW, BottomVerticalSegmPoints[1].Y));
			pointCollection.Add(new Point(BottomVerticalSegmPoints[0].X - base.GapW, BottomVerticalSegmPoints[0].Y));
			pointCollection.Add(new Point(num2 + base.GapW + DiagSegW, num));
			pointCollection.Add(new Point(num2 + base.GapW, num));
			pointCollection.Add(new Point(num3 + base.GapW, y));
			return pointCollection;
		}

		protected PointCollection GetTopLeftDiagSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			double y = base.HorizSegH + base.GapW + base.HorizSegH / 2.0;
			double num = XByAngle(y) + base.VertSegW;
			double y2 = base.HorizSegH + base.GapW;
			double num2 = XByAngle(y2) + base.VertSegW;
			pointCollection.Add(new Point(num + base.GapW, y));
			pointCollection.Add(new Point(num2 + base.GapW, y2));
			pointCollection.Add(new Point(num2 + base.GapW + DiagSegW, y2));
			pointCollection.Add(new Point(TopVerticalSegmPoints[5].X - base.GapW, TopVerticalSegmPoints[5].Y));
			pointCollection.Add(new Point(TopVerticalSegmPoints[4].X - base.GapW, TopVerticalSegmPoints[4].Y));
			pointCollection.Add(new Point(TopVerticalSegmPoints[4].X - base.GapW - DiagSegW, TopVerticalSegmPoints[4].Y));
			return pointCollection;
		}

		protected PointCollection GetTopRightDiagSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[3].X, base.VirtualHeight - BottomLeftDiagSegmPoints[3].Y));
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[4].X, base.VirtualHeight - BottomLeftDiagSegmPoints[4].Y));
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[5].X, base.VirtualHeight - BottomLeftDiagSegmPoints[5].Y));
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[0].X, base.VirtualHeight - BottomLeftDiagSegmPoints[0].Y));
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[1].X, base.VirtualHeight - BottomLeftDiagSegmPoints[1].Y));
			pointCollection.Add(new Point(base.VirtualWidth - BottomLeftDiagSegmPoints[2].X, base.VirtualHeight - BottomLeftDiagSegmPoints[2].Y));
			return pointCollection;
		}

		protected PointCollection GetBottomRightDiagSegmPoints()
		{
			PointCollection pointCollection = new PointCollection();
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[3].X, base.VirtualHeight - TopLeftDiagSegmPoints[3].Y));
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[4].X, base.VirtualHeight - TopLeftDiagSegmPoints[4].Y));
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[5].X, base.VirtualHeight - TopLeftDiagSegmPoints[5].Y));
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[0].X, base.VirtualHeight - TopLeftDiagSegmPoints[0].Y));
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[1].X, base.VirtualHeight - TopLeftDiagSegmPoints[1].Y));
			pointCollection.Add(new Point(base.VirtualWidth - TopLeftDiagSegmPoints[2].X, base.VirtualHeight - TopLeftDiagSegmPoints[2].Y));
			return pointCollection;
		}

		protected PathGeometry LeftTopHorizSegement()
		{
			LeftTopHorizSegmPoints = GetLeftTopHorizSegmPoints();
			Point startPoint = LeftTopHorizSegmPoints[0];
			LineSegment value = new LineSegment(LeftTopHorizSegmPoints[0], true);
			LineSegment value2 = new LineSegment(LeftTopHorizSegmPoints[1], true);
			LineSegment value3 = new LineSegment(LeftTopHorizSegmPoints[3], true);
			LineSegment value4 = new LineSegment(LeftTopHorizSegmPoints[4], true);
			LineSegment value5 = new LineSegment(LeftTopHorizSegmPoints[5], true);
			PointCollection collection = new PointCollection
			{
				LeftTopHorizSegmPoints[1],
				LeftTopHorizSegmPoints[2],
				LeftTopHorizSegmPoints[3]
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

		protected PathGeometry RightTopHorizSegement()
		{
			RightTopHorizSegmPoints = GetRightTopHorizSegmPoints();
			Point startPoint = RightTopHorizSegmPoints[0];
			LineSegment value = new LineSegment(RightTopHorizSegmPoints[0], true);
			LineSegment value2 = new LineSegment(RightTopHorizSegmPoints[1], true);
			LineSegment value3 = new LineSegment(RightTopHorizSegmPoints[2], true);
			LineSegment lineSegment = new LineSegment(RightTopHorizSegmPoints[3], true);
			LineSegment value4 = new LineSegment(RightTopHorizSegmPoints[4], true);
			LineSegment value5 = new LineSegment(RightTopHorizSegmPoints[5], true);
			PointCollection collection = new PointCollection
			{
				RightTopHorizSegmPoints[2],
				RightTopHorizSegmPoints[3],
				RightTopHorizSegmPoints[4]
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
			pathFigure.Segments.Add(value6);
			pathFigure.Segments.Add(value4);
			pathFigure.Segments.Add(value5);
			return pathGeometry;
		}

		protected PathGeometry LeftBottomHorizSegement()
		{
			LeftBottomHorizSegmPoints = GetLeftBottomHorizSegmPoints();
			Point startPoint = LeftBottomHorizSegmPoints[1];
			LineSegment lineSegment = new LineSegment(LeftBottomHorizSegmPoints[0], true);
			LineSegment value = new LineSegment(LeftBottomHorizSegmPoints[1], true);
			LineSegment value2 = new LineSegment(LeftBottomHorizSegmPoints[2], true);
			LineSegment value3 = new LineSegment(LeftBottomHorizSegmPoints[3], true);
			LineSegment value4 = new LineSegment(LeftBottomHorizSegmPoints[4], true);
			LineSegment value5 = new LineSegment(LeftBottomHorizSegmPoints[5], true);
			PointCollection collection = new PointCollection
			{
				LeftBottomHorizSegmPoints[5],
				LeftBottomHorizSegmPoints[0],
				LeftBottomHorizSegmPoints[1]
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

		protected PathGeometry RightBottomHorizSegement()
		{
			RightBottomHorizSegmPoints = GetRightBottomHorizSegmPoints();
			Point startPoint = RightBottomHorizSegmPoints[0];
			LineSegment value = new LineSegment(RightBottomHorizSegmPoints[0], true);
			LineSegment value2 = new LineSegment(RightBottomHorizSegmPoints[1], true);
			LineSegment value3 = new LineSegment(RightBottomHorizSegmPoints[2], true);
			LineSegment value4 = new LineSegment(RightBottomHorizSegmPoints[3], true);
			LineSegment lineSegment = new LineSegment(RightBottomHorizSegmPoints[4], true);
			LineSegment value5 = new LineSegment(RightBottomHorizSegmPoints[5], true);
			PointCollection collection = new PointCollection
			{
				RightBottomHorizSegmPoints[3],
				RightBottomHorizSegmPoints[4],
				RightBottomHorizSegmPoints[5]
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

		protected PathGeometry LeftMiddleSegement()
		{
			LeftMiddleSegmPoints = GetLeftMiddleSegmPoints();
			return SegmentPathGeometry(LeftMiddleSegmPoints[0], new PolyLineSegment
			{
				Points = LeftMiddleSegmPoints
			});
		}

		protected PathGeometry RightMiddleSegement()
		{
			RightMiddleSegmPoints = GetRightMiddleSegmPoints();
			return SegmentPathGeometry(RightMiddleSegmPoints[0], new PolyLineSegment
			{
				Points = RightMiddleSegmPoints
			});
		}

		protected PathGeometry TopVerticalSegment()
		{
			TopVerticalSegmPoints = GetTopVerticalSegmPoints();
			return SegmentPathGeometry(TopVerticalSegmPoints[0], new PolyLineSegment
			{
				Points = TopVerticalSegmPoints
			});
		}

		protected PathGeometry BottomVerticalSegment()
		{
			BottomVerticalSegmPoints = GetBottomVerticalSegmPoints();
			return SegmentPathGeometry(BottomVerticalSegmPoints[0], new PolyLineSegment
			{
				Points = BottomVerticalSegmPoints
			});
		}

		protected PathGeometry BottomLeftDiagSegment()
		{
			BottomLeftDiagSegmPoints = GetBottomLeftDiagSegmPoints();
			return SegmentPathGeometry(BottomLeftDiagSegmPoints[0], new PolyLineSegment
			{
				Points = BottomLeftDiagSegmPoints
			});
		}

		protected PathGeometry TopLeftDiagSegment()
		{
			TopLeftDiagSegmPoints = GetTopLeftDiagSegmPoints();
			return SegmentPathGeometry(TopLeftDiagSegmPoints[0], new PolyLineSegment
			{
				Points = TopLeftDiagSegmPoints
			});
		}

		protected PathGeometry TopRightDiagSegment()
		{
			TopRightDiagSegmPoints = GetTopRightDiagSegmPoints();
			return SegmentPathGeometry(TopRightDiagSegmPoints[0], new PolyLineSegment
			{
				Points = TopRightDiagSegmPoints
			});
		}

		protected PathGeometry BottomRightDiagSegment()
		{
			BottomRightDiagSegmPoints = GetBottomRightDiagSegmPoints();
			return SegmentPathGeometry(BottomRightDiagSegmPoints[0], new PolyLineSegment
			{
				Points = BottomRightDiagSegmPoints
			});
		}
	}
}
