using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HslControls.WPF.Segments
{
	[DesignTimeVisible(false)]
	internal class ArrangedPanel : Panel
	{
		protected override Size ArrangeOverride(Size finalSize)
		{
			double num = 0.0;
			double y = 0.0;
			double num2 = finalSize.Width / (double)base.InternalChildren.Count;
			double height = finalSize.Height;
			foreach (UIElement internalChild in base.InternalChildren)
			{
				internalChild.Arrange(new Rect(new Point(num, y), new Size(num2, height)));
				num += num2;
			}
			return finalSize;
		}
	}
}
