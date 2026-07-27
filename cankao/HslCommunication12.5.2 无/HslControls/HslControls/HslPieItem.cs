using System.Drawing;

namespace HslControls
{
	public class HslPieItem
	{
		public string Name { get; set; }

		public int Value { get; set; }

		public Color PieColor { get; set; }

		public Color FontColor { get; set; }

		public Color LineColor { get; set; }

		public HslPieItem()
		{
			PieColor = Color.DodgerBlue;
			LineColor = Color.DimGray;
			FontColor = Color.DimGray;
		}
	}
}
