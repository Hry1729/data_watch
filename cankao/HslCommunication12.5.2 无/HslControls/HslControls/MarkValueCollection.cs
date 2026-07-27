using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace HslControls
{
	public class MarkValueCollection : Collection<MarkValue>
	{
		public Control control { get; internal set; }

		protected override void InsertItem(int index, MarkValue item)
		{
			base.InsertItem(index, item);
			control?.Invalidate();
		}

		protected override void SetItem(int index, MarkValue item)
		{
			base.SetItem(index, item);
			control?.Invalidate();
		}

		protected override void ClearItems()
		{
			base.ClearItems();
			control?.Invalidate();
		}

		protected override void RemoveItem(int index)
		{
			base.RemoveItem(index);
			control?.Invalidate();
		}
	}
}
