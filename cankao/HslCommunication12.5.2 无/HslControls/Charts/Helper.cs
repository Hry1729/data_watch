using System.Drawing;
using System.Drawing.Drawing2D;

namespace HslControls.Charts
{
	public class Helper
	{
		public static SizeF ConvertSize(SizeF size, float angle)
		{
			Matrix matrix = new Matrix();
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

		public static void DrawString(Graphics g, string s, Font font, Brush brush, PointF point, StringFormat format, float angle)
		{
			Matrix transform = g.Transform;
			Matrix transform2 = g.Transform;
			transform2.RotateAt(angle, point);
			g.Transform = transform2;
			g.DrawString(s, font, brush, point, format);
			g.Transform = transform;
		}
	}
}
