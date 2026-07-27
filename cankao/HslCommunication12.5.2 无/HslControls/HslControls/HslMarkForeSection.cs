using System.Collections.Generic;
using System.Drawing;

namespace HslControls
{
	public class HslMarkForeSection : HslMarkSectionBase
	{
		public float StartHeight { get; set; }

		public float Height { get; set; }

		public bool IsRenderTimeText { get; set; }

		public Pen LinePen { get; set; }

		public Brush FontBrush { get; set; }

		public string MarkText { get; set; }

		public Dictionary<string, string> CursorTexts { get; set; }

		public HslMarkForeSection()
		{
			LinePen = Pens.Cyan;
			FontBrush = Brushes.Yellow;
			IsRenderTimeText = true;
			CursorTexts = new Dictionary<string, string>();
		}
	}
}
