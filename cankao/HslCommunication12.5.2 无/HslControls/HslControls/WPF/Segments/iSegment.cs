using System.Windows.Media;

namespace HslControls.WPF.Segments
{
	internal interface iSegment
	{
		Brush FillBrush { get; set; }

		Brush SelectedFillBrush { get; set; }

		Color PenColor { get; set; }

		Color SelectedPenColor { get; set; }

		double PenThickness { get; set; }

		string Value { get; set; }

		double GapWidth { get; set; }

		bool RoundedCorners { get; set; }

		double TiltAngle { get; set; }

		bool ShowDot { get; set; }

		bool ShowColon { get; set; }

		bool OnDot { get; set; }

		bool OnColon { get; set; }

		double VertSegDivider { get; set; }

		double HorizSegDivider { get; set; }
	}
}
