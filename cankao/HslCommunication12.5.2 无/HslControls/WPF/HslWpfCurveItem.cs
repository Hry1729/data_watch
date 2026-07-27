using System.Windows.Media;

namespace HslControls.WPF
{
	public class HslWpfCurveItem
	{
		public float[] Data = null;

		public string[] MarkText = null;

		public float LineThickness { get; set; }

		public bool IsSmoothCurve { get; set; }

		public Color LineColor { get; set; }

		public bool IsLeftFrame { get; set; }

		public bool Visible { get; set; }

		public bool LineRenderVisiable { get; set; }

		public string RenderFormat { get; set; } = "{0}";

		public HslWpfCurveItem()
		{
			LineThickness = 1f;
			IsLeftFrame = true;
			Visible = true;
			LineRenderVisiable = true;
		}
	}
}
