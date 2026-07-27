using System.Drawing;

namespace HslControls.Charts
{
	public class SeriesFormat
	{
		public static StringFormat LabelFormat { get; set; } = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Far
		};
	}
}
