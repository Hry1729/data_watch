using System.Collections.ObjectModel;

namespace HslControls.Charts
{
	public class ChartPointCollection : Collection<ChartPoint>
	{
		public HslChart Chart { get; internal set; }

		public SeriesBase Series { get; internal set; }

		protected override void InsertItem(int index, ChartPoint item)
		{
			base.InsertItem(index, item);
			item.Chart = Chart;
			item.Series = Series;
			Chart?.Invalidate();
		}

		protected override void SetItem(int index, ChartPoint item)
		{
			base.SetItem(index, item);
			item.Chart = Chart;
			item.Series = Series;
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
