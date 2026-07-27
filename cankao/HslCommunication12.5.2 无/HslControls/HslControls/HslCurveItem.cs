using System.Drawing;

namespace HslControls
{
	public class HslCurveItem
	{
		public float[] Data = null;

		public string[] MarkText = null;

		private Brush lineBrush = null;

		public float LineThickness { get; set; }

		public CurveStyle Style { get; set; }

		public Color LineColor { get; set; }

		public int ReferenceAxisIndex { get; set; }

		public bool Visible { get; set; }

		public bool LineRenderVisiable { get; set; }

		public RectangleF TitleRegion { get; set; }

		public string RenderFormat { get; set; } = "{0}";

		public HslCurveItem()
		{
			LineColor = Color.Red;
			LineThickness = 1f;
			ReferenceAxisIndex = 0;
			Visible = true;
			LineRenderVisiable = true;
			TitleRegion = new RectangleF(0f, 0f, 0f, 0f);
			Style = CurveStyle.LineSegment;
		}

		public Brush GetLineBrush()
		{
			if (lineBrush == null)
			{
				lineBrush = new SolidBrush(LineColor);
			}
			return lineBrush;
		}
	}
}
