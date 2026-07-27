using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Media;

namespace HslControls
{
	public class HslHelper
	{
		public static StringFormat StringFormatCenter { get; set; }

		public static StringFormat StringFormatLeft { get; set; }

		public static StringFormat StringFormatRight { get; set; }

		public static StringFormat StringFormatDefault { get; set; }

		public static StringFormat StringFormatTopCenter { get; set; }

		static HslHelper()
		{
			StringFormatDefault = new StringFormat();
			StringFormatCenter = new StringFormat();
			StringFormatCenter.Alignment = StringAlignment.Center;
			StringFormatCenter.LineAlignment = StringAlignment.Center;
			StringFormatLeft = new StringFormat();
			StringFormatLeft.LineAlignment = StringAlignment.Center;
			StringFormatLeft.Alignment = StringAlignment.Near;
			StringFormatRight = new StringFormat();
			StringFormatRight.LineAlignment = StringAlignment.Center;
			StringFormatRight.Alignment = StringAlignment.Far;
			StringFormatTopCenter = new StringFormat();
			StringFormatTopCenter.Alignment = StringAlignment.Center;
			StringFormatTopCenter.LineAlignment = StringAlignment.Near;
		}

		public static int Middle(int min, int value, int max)
		{
			if (value > max)
			{
				return max;
			}
			if (value < min)
			{
				return min;
			}
			return value;
		}

		public static Point[] GetRhombusFromRectangle(Rectangle rect)
		{
			return new Point[5]
			{
				new Point(rect.X, rect.Y + rect.Height / 2),
				new Point(rect.X + rect.Width / 2, rect.Y + rect.Height - 1),
				new Point(rect.X + rect.Width - 1, rect.Y + rect.Height / 2),
				new Point(rect.X + rect.Width / 2, rect.Y),
				new Point(rect.X, rect.Y + rect.Height / 2)
			};
		}

		public static float ComputePaintLocationY(int max, int min, int height, int value)
		{
			if ((float)(max - min) == 0f)
			{
				return height;
			}
			return (float)height - (float)(value - min) * 1f / (float)(max - min) * (float)height;
		}

		public static float ComputePaintLocationY(float max, float min, float height, float value)
		{
			if (max - min == 0f)
			{
				return height;
			}
			float num = max - min;
			if (num == 0f)
			{
				num = 1f;
			}
			return height - (value - min) / num * height;
		}

		public static float ComputePaintLocationX(float max, float min, float width, float value)
		{
			if (max - min == 0f)
			{
				return width;
			}
			float num = max - min;
			if (num == 0f)
			{
				num = 1f;
			}
			return (value - min) / num * width;
		}

		public static float ComputeValueFromPaintLocationY(float max, float min, float height, float paint)
		{
			if (max - min == 0f)
			{
				return max;
			}
			float num = max - min;
			if (num == 0f)
			{
				num = 1f;
			}
			return (height - paint) * num / height + min;
		}

		public static float ComputePaintLocationY(ReferenceAxis referenceAxis, float height, float value)
		{
			return ComputePaintLocationY(referenceAxis.Max, referenceAxis.Min, height, value);
		}

		public static void PaintCoordinateDivide(Graphics g, System.Drawing.Pen penLine, System.Drawing.Pen penDash, Font font, System.Drawing.Brush brush, StringFormat sf, int degree, int max, int min, int width, int height, int left = 60, int right = 8, int up = 8, int down = 8)
		{
			for (int i = 0; i <= degree; i++)
			{
				int value = (max - min) * i / degree + min;
				int num = (int)ComputePaintLocationY(max, min, height - up - down, value) + up + 1;
				g.DrawLine(penLine, left - 1, num, left - 4, num);
				if (i != 0)
				{
					g.DrawLine(penDash, left, num, width - right, num);
				}
				g.DrawString(value.ToString(), font, brush, new Rectangle(-5, num - font.Height / 2, left, font.Height), sf);
			}
		}

		public static void PaintTriangle(Graphics g, System.Drawing.Brush brush, Point point, int size, GraphDirection direction)
		{
			Point[] array = new Point[4];
			switch (direction)
			{
			case GraphDirection.Leftward:
				array[0] = new Point(point.X, point.Y - size);
				array[1] = new Point(point.X, point.Y + size);
				array[2] = new Point(point.X - 2 * size, point.Y);
				break;
			case GraphDirection.Rightward:
				array[0] = new Point(point.X, point.Y - size);
				array[1] = new Point(point.X, point.Y + size);
				array[2] = new Point(point.X + 2 * size, point.Y);
				break;
			case GraphDirection.Upward:
				array[0] = new Point(point.X - size, point.Y);
				array[1] = new Point(point.X + size, point.Y);
				array[2] = new Point(point.X, point.Y - 2 * size);
				break;
			default:
				array[0] = new Point(point.X - size, point.Y);
				array[1] = new Point(point.X + size, point.Y);
				array[2] = new Point(point.X, point.Y + 2 * size);
				break;
			}
			array[3] = array[0];
			g.FillPolygon(brush, array);
		}

		public static void DrawLeftRight(Graphics g, System.Drawing.Pen pen, Rectangle rectangle, GraphDirection direction)
		{
			switch (direction)
			{
			case GraphDirection.Leftward:
				g.DrawLines(pen, new Point[3]
				{
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y),
					new Point(rectangle.X, rectangle.Y + rectangle.Height / 2),
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height)
				});
				g.DrawLines(pen, new Point[3]
				{
					new Point(rectangle.X + rectangle.Width, rectangle.Y),
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2),
					new Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height)
				});
				break;
			case GraphDirection.Rightward:
				g.DrawLines(pen, new Point[3]
				{
					new Point(rectangle.X, rectangle.Y),
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2),
					new Point(rectangle.X, rectangle.Y + rectangle.Height)
				});
				g.DrawLines(pen, new Point[3]
				{
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y),
					new Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height / 2),
					new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height)
				});
				break;
			}
		}

		public static void PaintTriangle(Graphics g, System.Drawing.Brush brush, PointF point, int size, GraphDirection direction)
		{
			PointF[] array = new PointF[4];
			switch (direction)
			{
			case GraphDirection.Leftward:
				array[0] = new PointF(point.X, point.Y - (float)size);
				array[1] = new PointF(point.X, point.Y + (float)size);
				array[2] = new PointF(point.X - (float)(2 * size), point.Y);
				break;
			case GraphDirection.Rightward:
				array[0] = new PointF(point.X, point.Y - (float)size);
				array[1] = new PointF(point.X, point.Y + (float)size);
				array[2] = new PointF(point.X + (float)(2 * size), point.Y);
				break;
			case GraphDirection.Upward:
				array[0] = new PointF(point.X - (float)size, point.Y);
				array[1] = new PointF(point.X + (float)size, point.Y);
				array[2] = new PointF(point.X, point.Y - (float)(2 * size));
				break;
			default:
				array[0] = new PointF(point.X - (float)size, point.Y);
				array[1] = new PointF(point.X + (float)size, point.Y);
				array[2] = new PointF(point.X, point.Y + (float)(2 * size));
				break;
			}
			array[3] = array[0];
			g.FillPolygon(brush, array);
		}

		public static void AddArrayData<T>(ref T[] array, T[] data, int max)
		{
			if (data == null || data.Length == 0)
			{
				return;
			}
			if (array.Length == max)
			{
				Array.Copy(array, data.Length, array, 0, array.Length - data.Length);
				Array.Copy(data, 0, array, array.Length - data.Length, data.Length);
			}
			else if (array.Length + data.Length > max)
			{
				T[] array2 = new T[max];
				for (int i = 0; i < max - data.Length; i++)
				{
					array2[i] = array[i + (array.Length - max + data.Length)];
				}
				for (int j = 0; j < data.Length; j++)
				{
					array2[array2.Length - data.Length + j] = data[j];
				}
				array = array2;
			}
			else
			{
				T[] array3 = new T[array.Length + data.Length];
				for (int k = 0; k < array.Length; k++)
				{
					array3[k] = array[k];
				}
				for (int l = 0; l < data.Length; l++)
				{
					array3[array3.Length - data.Length + l] = data[l];
				}
				array = array3;
			}
		}

		public static SizeF ConvertSize(SizeF size, float angle)
		{
			System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix();
			matrix.Rotate(angle);
			PointF[] array = new PointF[4];
			array[0].X = (0f - size.Width) / 2f;
			array[0].Y = (0f - size.Height) / 2f;
			array[1].X = (0f - size.Width) / 2f;
			array[1].Y = size.Height / 2f;
			array[2].X = size.Width / 2f;
			array[2].Y = size.Height / 2f;
			array[3].X = size.Width / 2f;
			array[3].Y = (0f - size.Height) / 2f;
			matrix.TransformPoints(array);
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			PointF[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				PointF pointF = array2[i];
				if (pointF.X < num)
				{
					num = pointF.X;
				}
				if (pointF.X > num2)
				{
					num2 = pointF.X;
				}
				if (pointF.Y < num3)
				{
					num3 = pointF.Y;
				}
				if (pointF.Y > num4)
				{
					num4 = pointF.Y;
				}
			}
			return new SizeF(num2 - num, num4 - num3);
		}

		public static void DrawString(Graphics g, string s, Font font, System.Drawing.Brush brush, PointF point, StringFormat format, float angle)
		{
			System.Drawing.Drawing2D.Matrix transform = g.Transform;
			System.Drawing.Drawing2D.Matrix transform2 = g.Transform;
			transform2.RotateAt(angle, point);
			g.Transform = transform2;
			g.DrawString(s, font, brush, point, format);
			g.Transform = transform;
		}

		private static int GetPow(int digit)
		{
			int num = 1;
			for (int i = 0; i < digit; i++)
			{
				num *= 10;
			}
			return num;
		}

		public static double[] TranlateArrayToDouble(int[] values)
		{
			if (values == null)
			{
				return null;
			}
			double[] array = new double[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				array[i] = values[i];
			}
			return array;
		}

		public static double[] TranlateArrayToDouble(float[] values)
		{
			double[] array = new double[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				array[i] = values[i];
			}
			return array;
		}

		public static int CalculateMaxSectionFrom(double[] values)
		{
			double num = values.Max();
			if (num <= 5.0)
			{
				return 5;
			}
			if (num <= 9.0)
			{
				return 10;
			}
			int num2 = Convert.ToInt32(Math.Ceiling(num));
			int digit = num2.ToString().Length - 2;
			int num3 = int.Parse(num2.ToString().Substring(0, 2));
			if (num3 < 11)
			{
				return 12 * GetPow(digit);
			}
			if (num3 < 13)
			{
				return 14 * GetPow(digit);
			}
			if (num3 < 15)
			{
				return 16 * GetPow(digit);
			}
			if (num3 < 17)
			{
				return 18 * GetPow(digit);
			}
			if (num3 < 19)
			{
				return 20 * GetPow(digit);
			}
			if (num3 < 21)
			{
				return 22 * GetPow(digit);
			}
			if (num3 < 23)
			{
				return 24 * GetPow(digit);
			}
			if (num3 < 25)
			{
				return 26 * GetPow(digit);
			}
			if (num3 < 27)
			{
				return 28 * GetPow(digit);
			}
			if (num3 < 29)
			{
				return 30 * GetPow(digit);
			}
			if (num3 < 33)
			{
				return 34 * GetPow(digit);
			}
			if (num3 < 40)
			{
				return 40 * GetPow(digit);
			}
			if (num3 < 50)
			{
				return 50 * GetPow(digit);
			}
			if (num3 < 60)
			{
				return 60 * GetPow(digit);
			}
			if (num3 < 80)
			{
				return 80 * GetPow(digit);
			}
			if (num3 < 95)
			{
				return 100 * GetPow(digit);
			}
			return 100 * GetPow(digit);
		}

		public static int CalculateMaxSectionFrom(Dictionary<string, double[]> values)
		{
			return CalculateMaxSectionFrom(values.Select((KeyValuePair<string, double[]> m) => m.Value.Max()).ToArray());
		}

		public static System.Drawing.Color GetColorLight(System.Drawing.Color color)
		{
			return GetColorLight(color, 40);
		}

		public static System.Drawing.Color GetColorDeep(System.Drawing.Color color)
		{
			return GetColorLight(color, -40);
		}

		public static System.Drawing.Color GetColorLight(System.Drawing.Color color, int scale)
		{
			if (scale > 0)
			{
				return System.Drawing.Color.FromArgb(color.R + (255 - color.R) * scale / 100, color.G + (255 - color.G) * scale / 100, color.B + (255 - color.B) * scale / 100);
			}
			return System.Drawing.Color.FromArgb(color.R + color.R * scale / 100, color.G + color.G * scale / 100, color.B + color.B * scale / 100);
		}

		public static System.Drawing.Color GetColorOffset(System.Drawing.Color color, int offset)
		{
			int num = color.R + offset;
			if (num < 0)
			{
				num = 0;
			}
			if (num > 255)
			{
				num = 255;
			}
			int num2 = color.G + offset;
			if (num2 < 0)
			{
				num2 = 0;
			}
			if (num2 > 255)
			{
				num2 = 255;
			}
			int num3 = color.B + offset;
			if (num3 < 0)
			{
				num3 = 0;
			}
			if (num3 > 255)
			{
				num3 = 255;
			}
			return System.Drawing.Color.FromArgb(num, num2, num3);
		}

		public static System.Drawing.Color GetColorLightFive(System.Drawing.Color color)
		{
			return GetColorLight(color, 50);
		}

		public static System.Windows.Media.Color GetColorLight(System.Windows.Media.Color color)
		{
			return System.Windows.Media.Color.FromRgb((byte)(color.R + (255 - color.R) * 40 / 100), (byte)(color.G + (255 - color.G) * 40 / 100), (byte)(color.B + (255 - color.B) * 40 / 100));
		}

		public static System.Windows.Media.Color GetColorLightFive(System.Windows.Media.Color color)
		{
			return System.Windows.Media.Color.FromRgb((byte)(color.R + (255 - color.R) * 50 / 100), (byte)(color.G + (255 - color.G) * 50 / 100), (byte)(color.B + (255 - color.B) * 50 / 100));
		}

		public static PointF[] GetPointsFrom(string points, float soureWidth, float sourceHeight, float width, float height, float dx = 0f, float dy = 0f)
		{
			string[] array = points.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			PointF[] array2 = new PointF[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				int num = array[i].IndexOf(',');
				float num2 = Convert.ToSingle(array[i].Substring(0, num));
				array2[i] = new PointF(y: height * (Convert.ToSingle(array[i].Substring(num + 1)) + dy) / sourceHeight, x: width * (num2 + dx) / soureWidth);
			}
			return array2;
		}

		public static GraphicsPath GetRoundRectange(Rectangle rectangle, int radius, bool topLeft, bool topRight, bool buttomRight, bool buttomLeft)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			Point pt = new Point(rectangle.X + (topLeft ? radius : 0), rectangle.Y);
			Point pt2 = new Point(rectangle.X + rectangle.Width - 1 - (topRight ? radius : 0), rectangle.Y);
			graphicsPath.AddLine(pt, pt2);
			if (topRight && radius > 0)
			{
				graphicsPath.AddArc(rectangle.X + rectangle.Width - radius * 2 - 1, rectangle.Y, radius * 2, radius * 2, 270f, 90f);
			}
			Point pt3 = new Point(rectangle.X + rectangle.Width - 1, rectangle.Y + (topRight ? radius : 0));
			Point pt4 = new Point(rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1 - (buttomRight ? radius : 0));
			graphicsPath.AddLine(pt3, pt4);
			if (buttomRight && radius > 0)
			{
				graphicsPath.AddArc(rectangle.X + rectangle.Width - radius * 2 - 1, rectangle.Y + rectangle.Height - radius * 2 - 1, radius * 2, radius * 2, 0f, 90f);
			}
			Point pt5 = new Point(rectangle.X + rectangle.Width - 1 - (buttomRight ? radius : 0), rectangle.Y + rectangle.Height - 1);
			Point pt6 = new Point(rectangle.X + (buttomLeft ? radius : 0), rectangle.Y + rectangle.Height - 1);
			graphicsPath.AddLine(pt5, pt6);
			if (buttomLeft && radius > 0)
			{
				graphicsPath.AddArc(rectangle.X, rectangle.Y + rectangle.Height - radius * 2 - 1, radius * 2, radius * 2, 90f, 90f);
			}
			Point pt7 = new Point(rectangle.X, rectangle.Y + rectangle.Height - 1 - (buttomLeft ? radius : 0));
			Point pt8 = new Point(rectangle.X, rectangle.Y + (topLeft ? radius : 0));
			graphicsPath.AddLine(pt7, pt8);
			if (topLeft && radius > 0)
			{
				graphicsPath.AddArc(rectangle.X, rectangle.Y, radius * 2, radius * 2, 180f, 90f);
			}
			return graphicsPath;
		}

		private static string FloatMatchEvaluator(Match m)
		{
			if (m.Value.Length == 5)
			{
				return m.Value.Substring(0, 3);
			}
			return m.Value.Substring(0, 2);
		}

		private static string RemoveEndWithZero(string value)
		{
			return Regex.Replace(value, "\\.[0]+$", "");
		}

		public static float GetReferenceAxisMaxValue(float max)
		{
			if (max >= 0f)
			{
				int floatPow = GetFloatPow(max);
				float num = (float)((double)max * Math.Pow(10.0, -floatPow));
				if (num == 0f)
				{
					return 1f;
				}
				if (num < 1.1f)
				{
					return Convert.ToSingle(1.2000000476837158 * Math.Pow(10.0, floatPow));
				}
				if (num < 1.3f)
				{
					return Convert.ToSingle(1.399999976158142 * Math.Pow(10.0, floatPow));
				}
				if (num < 1.5f)
				{
					return Convert.ToSingle(1.600000023841858 * Math.Pow(10.0, floatPow));
				}
				if (num < 1.7f)
				{
					return Convert.ToSingle(1.7999999523162842 * Math.Pow(10.0, floatPow));
				}
				if (num < 1.9f)
				{
					return Convert.ToSingle(2.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 2.2f)
				{
					return Convert.ToSingle(2.4000000953674316 * Math.Pow(10.0, floatPow));
				}
				if (num < 2.4f)
				{
					return Convert.ToSingle(2.5999999046325684 * Math.Pow(10.0, floatPow));
				}
				if (num < 2.6f)
				{
					return Convert.ToSingle(2.799999952316284 * Math.Pow(10.0, floatPow));
				}
				if (num < 2.8f)
				{
					return Convert.ToSingle(3.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 3.3f)
				{
					return Convert.ToSingle(3.5 * Math.Pow(10.0, floatPow));
				}
				if (num < 3.8f)
				{
					return Convert.ToSingle(4.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 4.5f)
				{
					return Convert.ToSingle(5.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 6.5f)
				{
					return Convert.ToSingle(6.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 7.5f)
				{
					return Convert.ToSingle(8.0 * Math.Pow(10.0, floatPow));
				}
				if (num < 9.5f)
				{
					return Convert.ToSingle(10.0 * Math.Pow(10.0, floatPow));
				}
				return Convert.ToSingle(12.0 * Math.Pow(10.0, floatPow));
			}
			int floatPow2 = GetFloatPow(0f - max);
			float num2 = (float)((double)max * Math.Pow(10.0, -floatPow2));
			if (num2 < -8.5f)
			{
				return Convert.ToSingle(-8.0 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -6.5f)
			{
				return Convert.ToSingle(-6.0 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -4.5f)
			{
				return Convert.ToSingle(-4.0 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -3.5f)
			{
				return Convert.ToSingle(-3.0 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -2.2f)
			{
				return Convert.ToSingle(-2.0 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -1.9f)
			{
				return Convert.ToSingle(-1.7999999523162842 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -1.7f)
			{
				return Convert.ToSingle(-1.600000023841858 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -1.5f)
			{
				return Convert.ToSingle(-1.399999976158142 * Math.Pow(10.0, floatPow2));
			}
			if (num2 < -1.3f)
			{
				return Convert.ToSingle(-1.2000000476837158 * Math.Pow(10.0, floatPow2));
			}
			return Convert.ToSingle(-1.0 * Math.Pow(10.0, floatPow2));
		}

		private static int GetFloatPow(float value)
		{
			if (value >= 1f && value < 10f)
			{
				return 0;
			}
			if (value >= 10f)
			{
				return GetFloatPow(value / 10f) + 1;
			}
			if (value > 0f && value < 1f)
			{
				return GetFloatPow(value * 10f) - 1;
			}
			return 0;
		}

		public static string GetFormatString(string format, float value, bool remainZero = false)
		{
			try
			{
				string text = string.Format(format, value);
				if (remainZero)
				{
					return RemoveEndWithZero(text);
				}
				return RemoveEndWithZero(Regex.Replace(text, "[Ee][+-][0]+", FloatMatchEvaluator));
			}
			catch
			{
				return "Wrong";
			}
		}

		public static void ShowExceptionMessage(Exception ex)
		{
			MessageBox.Show(GetExceptionMessage(ex));
		}

		public static void ShowExceptionMessage(string extraMsg, Exception ex)
		{
			MessageBox.Show(GetExceptionMessage(extraMsg, ex));
		}

		public static string GetExceptionMessage(Exception ex)
		{
			return "Message" + ex.Message + Environment.NewLine + "StackTrace" + ex.StackTrace + Environment.NewLine + "TargetSite" + ex.TargetSite;
		}

		public static string GetExceptionMessage(string extraMsg, Exception ex)
		{
			if (string.IsNullOrEmpty(extraMsg))
			{
				return GetExceptionMessage(ex);
			}
			return extraMsg + Environment.NewLine + GetExceptionMessage(ex);
		}
	}
}
