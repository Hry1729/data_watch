using System.Collections.ObjectModel;

namespace HslControls.Charts
{
	public class AxisCollection : Collection<Axis>
	{
		public HslChart Chart { get; internal set; }

		public AxisType Type { get; internal set; }

		protected override void InsertItem(int index, Axis item)
		{
			base.InsertItem(index, item);
			item.Chart = Chart;
			item.Type = Type;
			Chart?.Invalidate();
		}

		protected override void SetItem(int index, Axis item)
		{
			base.SetItem(index, item);
			item.Chart = Chart;
			item.Type = Type;
			Chart?.Invalidate();
		}

		protected override void ClearItems()
		{
			base.ClearItems();
			Chart?.Invalidate();
		}

		protected override void RemoveItem(int index)
		{
			base.RemoveItem(index);
			Chart?.Invalidate();
		}
	}
}
