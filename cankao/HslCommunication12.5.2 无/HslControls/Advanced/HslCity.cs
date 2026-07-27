using System.Drawing;

namespace HslControls.Advanced
{
	public class HslCity
	{
		public string Name { get; set; }

		public int Degree { get; set; }

		public Point Location { get; set; }

		public int ShowLocation { get; set; } = 0;

		public int Size { get; set; }

		public int MarkSize { get; set; } = -1;

		public Brush MarkBrush { get; set; }

		public HslCity()
		{
			Size = 2;
		}
	}
}
