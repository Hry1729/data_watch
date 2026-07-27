using System.Windows.Media;

namespace HslControls.WPF.Segments
{
	public class CharItem : iSegment
	{
		public char Item { get; set; }

		public Brush FillBrush { get; set; }

		public Brush SelectedFillBrush { get; set; }

		public Color PenColor { get; set; }

		public Color SelectedPenColor { get; set; }

		public double PenThickness { get; set; }

		public string Value { get; set; }

		public double GapWidth { get; set; }

		public bool RoundedCorners { get; set; }

		public double TiltAngle { get; set; }

		public bool ShowDot { get; set; }

		public bool OnDot { get; set; }

		public bool ShowColon { get; set; }

		public bool OnColon { get; set; }

		public double VertSegDivider { get; set; }

		public double HorizSegDivider { get; set; }
	}
}
