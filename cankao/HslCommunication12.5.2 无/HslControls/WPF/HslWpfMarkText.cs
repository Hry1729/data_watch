using System.Windows.Media;

namespace HslControls.WPF
{
	public class HslWpfMarkText
	{
		public static readonly int MarkTextOffect = 5;

		public string CurveKey { get; set; }

		public int Index { get; set; }

		public string MarkText { get; set; }

		public Color TextColor { get; set; }

		public Color CircleColor { get; set; }

		public MarkTextPositionStyle PositionStyle { get; set; } = MarkTextPositionStyle.Auto;

		public HslWpfMarkText()
		{
			CircleColor = Colors.DodgerBlue;
			TextColor = Colors.Black;
		}

		public static MarkTextPositionStyle CalculateDirectionFromDataIndex(float[] data, int Index)
		{
			float num = ((Index == 0) ? data[Index] : data[Index - 1]);
			float num2 = ((Index == data.Length - 1) ? data[Index] : data[Index + 1]);
			if (num < data[Index] && data[Index] < num2)
			{
				return MarkTextPositionStyle.Left;
			}
			if (num > data[Index] && data[Index] > num2)
			{
				return MarkTextPositionStyle.Right;
			}
			if (num <= data[Index] && data[Index] >= num2)
			{
				return MarkTextPositionStyle.Up;
			}
			if (num >= data[Index] && data[Index] <= num2)
			{
				return MarkTextPositionStyle.Down;
			}
			return MarkTextPositionStyle.Up;
		}
	}
}
