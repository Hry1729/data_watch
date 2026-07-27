using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls
{
	public class HslMarkText
	{
		public string CurveKey { get; set; }

		public int Index { get; set; }

		public string MarkText { get; set; }

		public Image MarkImage { get; set; }

		public Brush TextBrush { get; set; }

		public Brush CircleBrush { get; set; }

		public int MarkTextOffect { get; set; } = 5;

		public int CenterPointSize { get; set; } = 3;

		public float FontSize { get; set; } = -1f;

		public MarkTextPositionStyle PositionStyle { get; set; } = MarkTextPositionStyle.Auto;

		internal HslCurveItem CurveItem { get; set; }

		public HslMarkText()
		{
			CircleBrush = Brushes.DodgerBlue;
			TextBrush = Brushes.Black;
			MarkTextOffect = 5;
		}

		public static MarkTextPositionStyle CalculateDirectionFromDataIndex(float[] data, int Index)
		{
			float num = ((Index == 0) ? data[Index] : data[Index - 1]);
			float num2 = ((Index == data.Length - 1) ? data[Index] : data[Index + 1]);
			if (num < data[Index] && data[Index] < num2)
			{
				return MarkTextPositionStyle.Left;
			}
			if (num > data[Index] && data[Index] > num2)
			{
				return MarkTextPositionStyle.Right;
			}
			if (num <= data[Index] && data[Index] >= num2)
			{
				return MarkTextPositionStyle.Up;
			}
			if (num >= data[Index] && data[Index] <= num2)
			{
				return MarkTextPositionStyle.Down;
			}
			return MarkTextPositionStyle.Up;
		}

		public static MarkTextPositionStyle CalculateDirectionFromDataIndex(PointF[] data, int Index)
		{
			float num = ((Index == 0) ? data[Index].Y : data[Index - 1].Y);
			float num2 = ((Index == data.Length - 1) ? data[Index].Y : data[Index + 1].Y);
			if (num < data[Index].Y && data[Index].Y < num2)
			{
				return MarkTextPositionStyle.Left;
			}
			if (num > data[Index].Y && data[Index].Y > num2)
			{
				return MarkTextPositionStyle.Right;
			}
			if (num <= data[Index].Y && data[Index].Y >= num2)
			{
				return MarkTextPositionStyle.Up;
			}
			if (num >= data[Index].Y && data[Index].Y <= num2)
			{
				return MarkTextPositionStyle.Down;
			}
			return MarkTextPositionStyle.Up;
		}

		public static Image CreateAddSign(Color color, int size = 6, int penWidth = 2)
		{
			Bitmap bitmap = new Bitmap(size * 2 + 1, size * 2 + 1);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (Pen pen = new Pen(color, penWidth))
			{
				graphics.DrawLine(pen, 0, size, 2 * size, size);
				graphics.DrawLine(pen, size, 0, size, 2 * size);
			}
			return bitmap;
		}

		public static Image CreateTriangleSign(Brush brush, ArrowDirection direction, int size = 6)
		{
			Bitmap bitmap = new Bitmap(size * 2 + 1, size * 2 + 1);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			switch (direction)
			{
			case ArrowDirection.Up:
				graphics.FillPolygon(brush, new Point[4]
				{
					new Point(size, 0),
					new Point(size * 2, size * 2),
					new Point(0, size * 2),
					new Point(size, 0)
				});
				break;
			case ArrowDirection.Down:
				graphics.FillPolygon(brush, new Point[4]
				{
					new Point(0, 0),
					new Point(size * 2, 0),
					new Point(size, size * 2),
					new Point(0, 0)
				});
				break;
			case ArrowDirection.Left:
				graphics.FillPolygon(brush, new Point[4]
				{
					new Point(0, size),
					new Point(size * 2, 0),
					new Point(size * 2, size * 2),
					new Point(0, size)
				});
				break;
			default:
				graphics.FillPolygon(brush, new Point[4]
				{
					new Point(0, 0),
					new Point(size * 2, size),
					new Point(0, size * 2),
					new Point(0, 0)
				});
				break;
			}
			return bitmap;
		}

		public static Image CreateCircleSign(Brush brush, int size = 6)
		{
			Bitmap bitmap = new Bitmap(size * 2 + 1, size * 2 + 1);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.FillEllipse(brush, new Rectangle(0, 0, size * 2, size * 2));
			return bitmap;
		}
	}
}
