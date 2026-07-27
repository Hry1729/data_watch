using System.Windows.Media;

namespace HslControls.WPF
{
	public class WpfAuxiliaryLable
	{
		public string Text { get; set; }

		public Color TextBrush { get; set; }

		public Color TextBack { get; set; }

		public float LocationX { get; set; }

		public WpfAuxiliaryLable()
		{
			TextBrush = Colors.Black;
			TextBack = Colors.Transparent;
			LocationX = 0.5f;
		}
	}
}
