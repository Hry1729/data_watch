using System.Drawing;

namespace HslControls
{
	public class HslCurveRange
	{
		public float[] Upper = null;

		public float[] Lower = null;

		public float LineThickness { get; set; }

		public CurveStyle Style { get; set; }

		public Color LineColor { get; set; }

		public int ReferenceAxisIndex { get; set; }

		public HslCurveRange()
		{
			LineColor = Color.Red;
			LineThickness = 1f;
			ReferenceAxisIndex = 0;
			Style = CurveStyle.LineSegment;
		}
	}
}
