using System.Drawing;

namespace HslControls.Charts
{
	public class AxisFormat
	{
		public const int TitleAngleAxisY = -90;

		public static StringFormat TitleFormatRightAxisY { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Far
		};

		public static StringFormat TitleFormatLeftAxisY { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Near
		};

		public static StringFormat LabelFormatLeftAxisY { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center
		};

		public static StringFormat LabelFormatRightAxisY { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center
		};

		public static StringFormat TitleFormatTopAxisX { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Near
		};

		public static StringFormat TitleFormatBottomAxisX { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Far
		};

		public static StringFormat LabelFormatTopAxisX { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Far
		};

		public static StringFormat LabelFormatBottomAxisX { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Near
		};
	}
}
