using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.DemoControl
{
	public class BorderPanel : Panel
	{
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if (base.Width > 2 && base.Height > 2)
			{
				e.Graphics.DrawRectangle(Pens.LightGray, new Rectangle(0, 0, base.Width - 1, base.Height - 1));
			}
		}
	}
}
