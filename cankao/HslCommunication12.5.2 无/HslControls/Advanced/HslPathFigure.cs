using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HslControls.Advanced
{
	public class HslPathFigure : HslFigureBase
	{
		private GraphicsPath[] pathes;

		public Brush FigureBackBrush { get; set; }

		public Brush FigureSelectdBrush { get; set; }

		public Pen PenBorder { get; set; }

		public HslPathFigure(params Point[][] points)
		{
			FigureBackBrush = new SolidBrush(Color.Lavender);
			FigureSelectdBrush = new SolidBrush(Color.LightSkyBlue);
			PenBorder = new Pen(Color.DimGray, 1f);
			pathes = new GraphicsPath[points.Length];
			for (int i = 0; i < pathes.Length; i++)
			{
				pathes[i] = new GraphicsPath(FillMode.Alternate);
				for (int j = 0; j < points[i].Length - 1; j++)
				{
					pathes[i].AddLine(points[i][j], points[i][j + 1]);
				}
				pathes[i].CloseFigure();
			}
		}

		public override bool IsMouseIn(PointF cursor)
		{
			for (int i = 0; i < pathes.Length; i++)
			{
				if (pathes[i].IsVisible(cursor))
				{
					return true;
				}
			}
			return false;
		}

		public override void DrawCadFigure(Graphics g, Color themeColor, Color backColor, Font font, float scale)
		{
			base.DrawCadFigure(g, themeColor, backColor, font, scale);
			if (base.Selected)
			{
				for (int i = 0; i < pathes.Length; i++)
				{
					if (FigureSelectdBrush != null)
					{
						g.FillPath(FigureSelectdBrush, pathes[i]);
					}
				}
			}
			else
			{
				for (int j = 0; j < pathes.Length; j++)
				{
					if (FigureBackBrush != null)
					{
						g.FillPath(FigureBackBrush, pathes[j]);
					}
				}
			}
			for (int k = 0; k < pathes.Length; k++)
			{
				if (PenBorder != null && (double)Math.Abs(PenBorder.Width - 1f / scale) > 0.2)
				{
					Color color = PenBorder.Color;
					PenBorder.Dispose();
					PenBorder = new Pen(color, 1f / scale);
				}
				if (PenBorder != null)
				{
					g.DrawPath(PenBorder, pathes[k]);
				}
			}
		}
	}
}
