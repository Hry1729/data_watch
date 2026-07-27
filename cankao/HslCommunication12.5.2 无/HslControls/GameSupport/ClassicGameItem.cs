using System.Drawing;

namespace HslControls.GameSupport
{
	public class ClassicGameItem
	{
		public int ItemCode { get; set; } = 1;

		public Color ItemColor { get; set; }

		public bool HasItem { get; set; } = false;

		public Point Position { get; set; }

		public ClassicGameItem()
		{
			ItemColor = Color.Black;
			Position = new Point(0, 0);
		}
	}
}
