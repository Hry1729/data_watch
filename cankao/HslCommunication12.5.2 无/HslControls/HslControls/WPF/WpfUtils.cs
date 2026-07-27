using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public class WpfUtils
	{
		public static System.Windows.Media.Color TransMediaColor(System.Drawing.Color color)
		{
			return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		public static PointF[] TransMediaPoints(System.Windows.Point[] points)
		{
			PointF[] array = new PointF[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				array[i] = new PointF((float)points[i].X, (float)points[i].Y);
			}
			return array;
		}

		public static System.Windows.Point[] TransMediaPoints(System.Drawing.Point[] points)
		{
			System.Windows.Point[] array = new System.Windows.Point[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				array[i] = new System.Windows.Point((float)points[i].X, (float)points[i].Y);
			}
			return array;
		}

		public static PointF[] TransMediaPoints(List<System.Windows.Point> points)
		{
			return TransMediaPoints(points.ToArray());
		}

		public static System.Drawing.Color TransDrawingColor(System.Windows.Media.Color color)
		{
			return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		public static Line DrawLine(Canvas canvas, System.Windows.Point point1, System.Windows.Point point2, System.Windows.Media.Color color, double thickness)
		{
			return DrawLine(canvas, point1.X, point1.Y, point2.X, point2.Y, color, thickness);
		}

		public static Line DrawLine(Canvas canvas, System.Windows.Point point1, System.Windows.Point point2, System.Windows.Media.Color color, double thickness, double[] dashArray)
		{
			return DrawLine(canvas, point1.X, point1.Y, point2.X, point2.Y, color, thickness, dashArray);
		}

		public static Line DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, System.Windows.Media.Color color, double thickness)
		{
			return DrawLine(canvas, x1, y1, x2, y2, new SolidColorBrush(color), thickness);
		}

		public static Line DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, System.Windows.Media.Brush brush, double thickness)
		{
			Line line = new Line();
			line.X1 = x1;
			line.Y1 = y1;
			line.X2 = x2;
			line.Y2 = y2;
			line.StrokeThickness = thickness;
			line.Stroke = brush;
			canvas.Children.Add(line);
			return line;
		}

		public static Line DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, System.Windows.Media.Color color, double thickness, double[] dashArray)
		{
			Line line = new Line();
			line.X1 = x1;
			line.Y1 = y1;
			line.X2 = x2;
			line.Y2 = y2;
			line.StrokeDashArray = new DoubleCollection(dashArray);
			line.StrokeThickness = thickness;
			line.Stroke = new SolidColorBrush(color);
			canvas.Children.Add(line);
			return line;
		}

		public static Ellipse DrawEllipse(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Color color, double thickness)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = width;
			ellipse.Height = height;
			canvas.Children.Add(ellipse);
			Canvas.SetLeft(ellipse, x);
			Canvas.SetTop(ellipse, y);
			ellipse.StrokeThickness = thickness;
			ellipse.Stroke = new SolidColorBrush(color);
			return ellipse;
		}

		public static Ellipse DrawEllipse(Canvas canvas, RectangleF rectangle, System.Windows.Media.Color color, double thickness)
		{
			return DrawEllipse(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, thickness);
		}

		public static System.Windows.Shapes.Rectangle FillRectangle(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Color color)
		{
			return FillRectangle(canvas, x, y, width, height, new SolidColorBrush(color));
		}

		public static System.Windows.Shapes.Rectangle FillRectangle(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Brush brush)
		{
			if (width >= 0.0 && height >= 0.0)
			{
				System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle();
				rectangle.Width = width;
				rectangle.Height = height;
				canvas.Children.Add(rectangle);
				Canvas.SetLeft(rectangle, x);
				Canvas.SetTop(rectangle, y);
				rectangle.Fill = brush;
				return rectangle;
			}
			return null;
		}

		public static System.Windows.Shapes.Rectangle FillRectangle(Canvas canvas, RectangleF rectangle, System.Windows.Media.Color color)
		{
			return FillRectangle(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);
		}

		public static System.Windows.Shapes.Rectangle FillRectangle(Canvas canvas, RectangleF rectangle, System.Windows.Media.Brush brush)
		{
			return FillRectangle(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, brush);
		}

		public static System.Windows.Shapes.Rectangle DrawRectangle(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Color color, float penWidth)
		{
			if (width >= 0.0 && height >= 0.0)
			{
				System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle();
				rectangle.Width = width;
				rectangle.Height = height;
				rectangle.StrokeThickness = penWidth;
				rectangle.Stroke = new SolidColorBrush(color);
				canvas.Children.Add(rectangle);
				Canvas.SetLeft(rectangle, x);
				Canvas.SetTop(rectangle, y);
				return rectangle;
			}
			return null;
		}

		public static System.Windows.Shapes.Rectangle DrawRectangle(Canvas canvas, RectangleF rectangle, System.Windows.Media.Color color, float penWidth)
		{
			return DrawRectangle(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, penWidth);
		}

		public static Ellipse FillEllipse(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Color color)
		{
			return FillEllipse(canvas, x, y, width, height, new SolidColorBrush(color));
		}

		public static Ellipse FillEllipse(Canvas canvas, double x, double y, double width, double height, System.Windows.Media.Brush brush)
		{
			Ellipse ellipse = new Ellipse();
			ellipse.Width = width;
			ellipse.Height = height;
			canvas.Children.Add(ellipse);
			Canvas.SetLeft(ellipse, x);
			Canvas.SetTop(ellipse, y);
			ellipse.Fill = brush;
			return ellipse;
		}

		public static Ellipse FillEllipse(Canvas canvas, RectangleF rectangle, System.Windows.Media.Color color)
		{
			return FillEllipse(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);
		}

		public static Ellipse FillEllipse(Canvas canvas, RectangleF rectangle, System.Windows.Media.Brush brush)
		{
			return FillEllipse(canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, brush);
		}

		public static Polyline DrawLines(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Color color, double thickness, double[] dashArray = null)
		{
			Polyline polyline = new Polyline();
			polyline.Points = new PointCollection(points);
			polyline.StrokeThickness = thickness;
			polyline.Stroke = new SolidColorBrush(color);
			if (dashArray != null)
			{
				polyline.StrokeDashArray = new DoubleCollection(dashArray);
			}
			canvas.Children.Add(polyline);
			return polyline;
		}

		public static Path DrawBezier(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Color color, double thickness, double[] dashArray = null)
		{
			Path path = new Path();
			path.StrokeThickness = thickness;
			path.Stroke = new SolidColorBrush(color);
			if (dashArray != null)
			{
				path.StrokeDashArray = new DoubleCollection(dashArray);
			}
			PathGeometry pathGeometry = new PathGeometry();
			PolyBezierSegment polyBezierSegment = new PolyBezierSegment();
			polyBezierSegment.Points = new PointCollection(points);
			pathGeometry.Figures.Add(new PathFigure(points[0], new PathSegment[1] { polyBezierSegment }, false));
			path.Data = pathGeometry;
			canvas.Children.Add(path);
			return path;
		}

		public static void DrawLines(Canvas canvas, System.Windows.Point[] points, HslCurveItem line, int pointsRadius)
		{
			if (points.Length < 2)
			{
				return;
			}
			if (line.Style == CurveStyle.LineSegment || line.Style == CurveStyle.Curve)
			{
				DrawLines(canvas, points, TransMediaColor(line.LineColor), line.LineThickness);
			}
			else if (line.Style == CurveStyle.LineDot || line.Style == CurveStyle.CurveDot)
			{
				DrawLines(canvas, points, TransMediaColor(line.LineColor), line.LineThickness, new double[2] { 1.0, 1.0 });
			}
			else if (line.Style == CurveStyle.CurveDash || line.Style == CurveStyle.LineDash)
			{
				DrawLines(canvas, points, TransMediaColor(line.LineColor), line.LineThickness, new double[2] { 2.0, 2.0 });
			}
			else if (line.Style == CurveStyle.CurveLongDath || line.Style == CurveStyle.LineLongDath)
			{
				DrawLines(canvas, points, TransMediaColor(line.LineColor), line.LineThickness, new double[2] { 5.0, 5.0 });
			}
			else if (line.Style == CurveStyle.StepLine)
			{
				PointCollection pointCollection = new PointCollection();
				for (int i = 0; i < points.Length - 1; i++)
				{
					System.Windows.Point value = new System.Windows.Point(points[i + 1].X, points[i].Y);
					pointCollection.Add(points[i]);
					pointCollection.Add(value);
				}
				pointCollection.Add(points[points.Length - 1]);
				DrawLines(canvas, pointCollection.ToArray(), TransMediaColor(line.LineColor), line.LineThickness);
			}
			else
			{
				for (int j = 0; j < points.Length - 1; j++)
				{
					System.Windows.Point point = new System.Windows.Point(points[j + 1].X, points[j].Y);
					Line line2 = new Line();
					line2.X1 = points[j].X;
					line2.Y1 = points[j].Y;
					line2.X2 = point.X;
					line2.Y2 = point.Y;
					line2.StrokeThickness = line.LineThickness;
					line2.Stroke = new SolidColorBrush(TransMediaColor(line.LineColor));
					canvas.Children.Add(line2);
				}
			}
			if (pointsRadius > 0 && pointsRadius < 20)
			{
				for (int k = 0; k < points.Length; k++)
				{
					FillEllipse(canvas, points[k].X - (double)pointsRadius, points[k].Y - (double)pointsRadius, pointsRadius * 2, pointsRadius * 2, TransMediaColor(line.LineColor));
				}
			}
		}

		public static Path DrawCurve(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Color color, double thickness)
		{
			Path path = new Path();
			path.StrokeThickness = thickness;
			path.Stroke = new SolidColorBrush(color);
			canvas.Children.Add(path);
			return path;
		}

		public static Polygon FillPolygon(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Color color)
		{
			return FillPolygon(canvas, points, new SolidColorBrush(color));
		}

		public static Polygon FillPolygon(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Brush brush)
		{
			Polygon polygon = new Polygon();
			polygon.Points = new PointCollection(points);
			polygon.Fill = brush;
			canvas.Children.Add(polygon);
			return polygon;
		}

		public static Polygon DrawPolygon(Canvas canvas, System.Windows.Point[] points, System.Windows.Media.Color color, float penWidth)
		{
			Polygon polygon = new Polygon();
			polygon.Points = new PointCollection(points);
			polygon.StrokeThickness = penWidth;
			polygon.Stroke = new SolidColorBrush(color);
			canvas.Children.Add(polygon);
			return polygon;
		}

		public static Polygon FillTriangle(Canvas canvas, System.Windows.Point point, System.Windows.Media.Color color, int size, GraphDirection direction)
		{
			System.Windows.Point[] array = new System.Windows.Point[4];
			switch (direction)
			{
			case GraphDirection.Leftward:
				array[0] = new System.Windows.Point(point.X, point.Y - (double)size);
				array[1] = new System.Windows.Point(point.X, point.Y + (double)size);
				array[2] = new System.Windows.Point(point.X - (double)(2 * size), point.Y);
				break;
			case GraphDirection.Rightward:
				array[0] = new System.Windows.Point(point.X, point.Y - (double)size);
				array[1] = new System.Windows.Point(point.X, point.Y + (double)size);
				array[2] = new System.Windows.Point(point.X + (double)(2 * size), point.Y);
				break;
			case GraphDirection.Upward:
				array[0] = new System.Windows.Point(point.X - (double)size, point.Y);
				array[1] = new System.Windows.Point(point.X + (double)size, point.Y);
				array[2] = new System.Windows.Point(point.X, point.Y - (double)(2 * size));
				break;
			default:
				array[0] = new System.Windows.Point(point.X - (double)size, point.Y);
				array[1] = new System.Windows.Point(point.X + (double)size, point.Y);
				array[2] = new System.Windows.Point(point.X, point.Y + (double)(2 * size));
				break;
			}
			array[3] = array[0];
			return FillPolygon(canvas, array, color);
		}

		public static Label DrawString(Canvas canvas, string text, System.Windows.Media.Color color, RectangleF rectangle, HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			return DrawString(canvas, text, color, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, horizontal, vertical);
		}

		public static Label DrawString(Canvas canvas, string text, System.Windows.Media.Brush brush, RectangleF rectangle, HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			return DrawString(canvas, text, brush, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, horizontal, vertical);
		}

		public static Label DrawString(Canvas canvas, string text, System.Windows.Media.Color color, double x, double y, double width, double height, HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			return DrawString(canvas, text, new SolidColorBrush(color), x, y, width, height, horizontal, vertical);
		}

		public static Label DrawString(Canvas canvas, string text, System.Windows.Media.Brush foreground, double x, double y, double width, double height, HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			Label label = new Label();
			label.Width = width;
			label.Height = height;
			label.Foreground = foreground;
			label.HorizontalContentAlignment = horizontal;
			label.VerticalContentAlignment = vertical;
			label.Content = text;
			canvas.Children.Add(label);
			label.Style = null;
			Canvas.SetLeft(label, x);
			Canvas.SetTop(label, y);
			return label;
		}

		public static void DrawString(Canvas canvas, string text, System.Windows.Media.Brush foreground, double fontSize, double x, double y, double width, double height, HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			Label label = new Label();
			label.Width = width;
			label.Height = height;
			label.FontSize = fontSize;
			label.Foreground = foreground;
			label.HorizontalContentAlignment = horizontal;
			label.VerticalContentAlignment = vertical;
			label.Content = text;
			canvas.Children.Add(label);
			Canvas.SetLeft(label, x);
			Canvas.SetTop(label, y);
		}
	}
}
