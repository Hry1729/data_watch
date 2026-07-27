using System.Drawing;

namespace HslControls.Advanced
{
	public interface IHslFigure
	{
		string FigureId { get; set; }

		string FigureType { get; set; }

		string Name { get; set; }

		PointF Location { get; set; }

		float Width { get; set; }

		float Height { get; set; }

		bool Selected { get; set; }

		bool IsMouseIn(PointF cursor);

		int GetMiddleDatum();

		void DrawCadFigure(Graphics g, Color themeColor, Color backColor, Font font, float scale);
	}
}
