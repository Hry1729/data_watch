using System.Drawing;

namespace HslControls
{
	public class HslMarkImage
	{
		public int Index { get; set; }

		public float OffsetY { get; set; }

		public Image MarkImage { get; set; }

		public ContentAlignment ReferencePoint { get; set; }

		public bool ScaleEnable { get; set; }

		public HslMarkImage()
		{
			ReferencePoint = ContentAlignment.TopLeft;
			ScaleEnable = true;
		}
	}
}
