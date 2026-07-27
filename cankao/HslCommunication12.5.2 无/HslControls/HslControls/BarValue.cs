using System.Drawing;

namespace HslControls
{
	public class BarValue
	{
		public double Value { get; set; }

		public string Text { get; set; }

		public string BarValueFormat { get; set; } = "{0}";

		public Color BarColor { get; set; } = Color.DodgerBlue;

		public BarValue(double value, string text, string barValueFormat = "{0}")
		{
			Value = value;
			Text = text;
			BarValueFormat = barValueFormat;
		}

		public BarValue(double value, string text, Color color, string barValueFormat = "{0}")
			: this(value, text, barValueFormat)
		{
			BarColor = color;
		}

		public BarValue()
		{
		}
	}
}
