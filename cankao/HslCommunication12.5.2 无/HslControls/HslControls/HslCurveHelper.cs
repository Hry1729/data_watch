using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HslControls
{
	public class HslCurveHelper
	{
		public static void DrawTextByPoint(Graphics g, string text, PointF center, Font font, Brush brush, MarkTextPositionStyle markTextPosition, int markTextOffect)
		{
			if (!string.IsNullOrEmpty(text))
			{
				switch (markTextPosition)
				{
				case MarkTextPositionStyle.Left:
					g.DrawString(text, font, brush, new RectangleF(center.X - 100f, center.Y - (float)font.Height - (float)markTextOffect, 100 - markTextOffect, font.Height + markTextOffect), HslHelper.StringFormatRight);
					break;
				case MarkTextPositionStyle.Up:
					g.DrawString(text, font, brush, new RectangleF(center.X - 100f, center.Y - (float)font.Height - (float)markTextOffect, 200f, font.Height + 2), HslHelper.StringFormatCenter);
					break;
				case MarkTextPositionStyle.Right:
					g.DrawString(text, font, brush, new RectangleF(center.X + (float)markTextOffect, center.Y - (float)font.Height - (float)markTextOffect, 100f, font.Height + markTextOffect), HslHelper.StringFormatLeft);
					break;
				case MarkTextPositionStyle.Down:
					g.DrawString(text, font, brush, new RectangleF(center.X - 100f, center.Y + (float)markTextOffect, 200f, font.Height + 2), HslHelper.StringFormatCenter);
					break;
				case MarkTextPositionStyle.Center:
					g.DrawString(text, font, brush, new RectangleF(center.X - 100f, center.Y - (float)font.Height, 200f, font.Height * 2), HslHelper.StringFormatCenter);
					break;
				}
			}
		}

		public static void DrawHslMarkTextPoint(Graphics g, HslMarkText markText, PointF center, Font font, MarkTextPositionStyle markTextPosition)
		{
			if (markText == null)
			{
				return;
			}
			if (markText.MarkImage == null || markTextPosition != MarkTextPositionStyle.Center)
			{
				g.FillEllipse(markText.CircleBrush, new RectangleF(center.X - (float)markText.CenterPointSize, center.Y - (float)markText.CenterPointSize, markText.CenterPointSize * 2, markText.CenterPointSize * 2));
			}
			if (!string.IsNullOrEmpty(markText.MarkText))
			{
				if (markText.FontSize > 0f)
				{
					using (Font font2 = new Font(font.FontFamily, markText.FontSize))
					{
						DrawTextByPoint(g, markText.MarkText, center, font2, markText.TextBrush, markTextPosition, markText.MarkTextOffect);
					}
				}
				else
				{
					DrawTextByPoint(g, markText.MarkText, center, font, markText.TextBrush, markTextPosition, markText.MarkTextOffect);
				}
			}
			if (markText.MarkImage != null)
			{
				switch (markTextPosition)
				{
				case MarkTextPositionStyle.Left:
					g.DrawImage(markText.MarkImage, new PointF(center.X - (float)markText.MarkImage.Width - (float)markText.MarkTextOffect, center.Y - (float)markText.MarkImage.Height - (float)markText.MarkTextOffect));
					break;
				case MarkTextPositionStyle.Up:
					g.DrawImage(markText.MarkImage, new PointF(center.X - (float)(markText.MarkImage.Width / 2), center.Y - (float)markText.MarkImage.Height - (float)markText.MarkTextOffect));
					break;
				case MarkTextPositionStyle.Right:
					g.DrawImage(markText.MarkImage, new PointF(center.X + (float)markText.MarkTextOffect, center.Y - (float)markText.MarkImage.Height - (float)markText.MarkTextOffect));
					break;
				case MarkTextPositionStyle.Down:
					g.DrawImage(markText.MarkImage, new PointF(center.X - (float)(markText.MarkImage.Width / 2), center.Y + (float)markText.MarkTextOffect));
					break;
				case MarkTextPositionStyle.Center:
					g.DrawImage(markText.MarkImage, new PointF(center.X - (float)(markText.MarkImage.Width / 2), center.Y - (float)(markText.MarkImage.Height / 2)));
					break;
				}
			}
		}

		public static void DrawLineCore(Graphics g, HslCurveItem line, List<PointF> listPoints, int pointsRadius, float referenceY = -1f)
		{
			if (listPoints.Count <= 1)
			{
				return;
			}
			using (Pen pen = new Pen(line.LineColor, line.LineThickness))
			{
				if (line.Style == CurveStyle.LineSegment)
				{
					g.DrawLines(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.Curve)
				{
					g.DrawCurve(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.LineDot)
				{
					pen.DashStyle = DashStyle.Dot;
					g.DrawLines(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.CurveDot)
				{
					pen.DashStyle = DashStyle.Dot;
					g.DrawCurve(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.LineDash)
				{
					pen.DashStyle = DashStyle.Dash;
					g.DrawLines(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.CurveDash)
				{
					pen.DashStyle = DashStyle.Dash;
					g.DrawCurve(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.LineLongDath)
				{
					pen.DashStyle = DashStyle.Custom;
					pen.DashPattern = new float[2] { 5f, 5f };
					g.DrawLines(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.CurveLongDath)
				{
					pen.DashStyle = DashStyle.Custom;
					pen.DashPattern = new float[2] { 5f, 5f };
					g.DrawCurve(pen, listPoints.ToArray());
				}
				else if (line.Style == CurveStyle.Section)
				{
					g.DrawCurve(pen, listPoints.ToArray());
					if (listPoints.Count > 0)
					{
						GraphicsPath graphicsPath = new GraphicsPath();
						graphicsPath.AddCurve(listPoints.ToArray());
						graphicsPath.AddLines(new PointF[4]
						{
							listPoints[listPoints.Count - 1],
							new PointF(listPoints[listPoints.Count - 1].X, referenceY),
							new PointF(listPoints[0].X, referenceY),
							new PointF(listPoints[0].X, listPoints[0].Y)
						});
						using (Brush brush = new SolidBrush(Color.FromArgb(64, pen.Color)))
						{
							g.FillPath(brush, graphicsPath);
						}
					}
				}
				else if (line.Style == CurveStyle.StepLine || line.Style == CurveStyle.StepLineWithoutVertical)
				{
					for (int i = 0; i < listPoints.Count - 1; i++)
					{
						PointF pointF = new PointF(listPoints[i + 1].X, listPoints[i].Y);
						g.DrawLine(pen, listPoints[i], pointF);
						if (line.Style == CurveStyle.StepLine)
						{
							g.DrawLine(pen, pointF, listPoints[i + 1]);
						}
					}
				}
			}
			if (pointsRadius <= 0)
			{
				return;
			}
			using (Brush brush2 = new SolidBrush(line.LineColor))
			{
				for (int j = 0; j < listPoints.Count; j++)
				{
					g.FillEllipse(brush2, listPoints[j].X - (float)pointsRadius, listPoints[j].Y - (float)pointsRadius, pointsRadius * 2, pointsRadius * 2);
				}
			}
		}
	}
}
