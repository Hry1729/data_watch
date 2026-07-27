using System;
using System.Drawing;

namespace HslControls.Advanced
{
	public class HslFigureBase : IHslFigure
	{
		private static Random random = new Random();

		public string FigureId { get; set; }

		public string FigureType { get; set; } = "HslFigureBase";

		public string Name { get; set; } = "HslFigureBase";

		public PointF Location { get; set; }

		public float Width { get; set; }

		public float Height { get; set; }

		public bool Selected { get; set; }

		public HslFigureBase()
		{
			FigureId = GetFigureUniqueId();
		}

		public virtual int GetMiddleDatum()
		{
			return (int)(Height / 2f);
		}

		public virtual bool IsMouseIn(PointF cursor)
		{
			return false;
		}

		public virtual void DrawCadFigure(Graphics g, Color themeColor, Color backColor, Font font, float scale)
		{
		}

		private static string GetFigureUniqueId()
		{
			return Guid.NewGuid().ToString("N") + random.Next(1000, 10000);
		}
	}
}
