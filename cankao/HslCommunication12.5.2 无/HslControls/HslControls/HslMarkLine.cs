using System.Drawing;

namespace HslControls
{
	public class HslMarkLine
	{
		public Brush TextBrush { get; set; }

		public Brush CircleBrush { get; set; }

		public Pen LinePen { get; set; }

		public PointF[] Points { get; set; }

		public string[] Marks { get; set; }

		public bool IsLineClosed { get; set; }

		public bool IsLeftFrame { get; set; }

		public HslMarkLine()
		{
			TextBrush = Brushes.DodgerBlue;
			CircleBrush = Brushes.DodgerBlue;
			LinePen = Pens.DodgerBlue;
			IsLeftFrame = true;
		}
	}
}
