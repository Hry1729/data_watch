using System.Drawing;

namespace HslControls
{
	public class HslMarkBackSection : HslMarkSectionBase
	{
		public Color BackColor { get; set; }

		public string MarkText { get; set; }

		public HslMarkBackSection()
		{
			BackColor = Color.FromArgb(52, 52, 52);
		}
	}
}
