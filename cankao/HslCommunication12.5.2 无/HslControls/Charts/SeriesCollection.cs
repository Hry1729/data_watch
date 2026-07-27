using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Design;

namespace HslControls.Charts
{
	[Editor(typeof(SeriesCollectionEditor), typeof(UITypeEditor))]
	public class SeriesCollection : Collection<SeriesBase>
	{
		public HslChart Chart { get; internal set; }

		protected override void InsertItem(int index, SeriesBase item)
		{
			base.InsertItem(index, item);
			item.Chart = Chart;
			item.Points.Chart = Chart;
			item.Points.Series = item;
			if (Chart != null)
			{
				Chart.LegendPanel.Controls.Add(item.Legend);
			}
			Chart?.Invalidate();
		}

		protected override void SetItem(int index, SeriesBase item)
		{
			if (Chart != null)
			{
				Chart.LegendPanel.Controls.Remove(base[index].Legend);
			}
			base.SetItem(index, item);
			item.Chart = Chart;
			item.Points.Chart = Chart;
			item.Points.Series = item;
			if (Chart != null)
			{
				Chart.LegendPanel.Controls.Add(item.Legend);
			}
			Chart?.Invalidate();
		}

		protected override void ClearItems()
		{
			if (Chart != null)
			{
				Chart.LegendPanel.Controls.Clear();
			}
			base.ClearItems();
			Chart?.Invalidate();
		}

		protected override void RemoveItem(int index)
		{
			if (Chart != null)
			{
				Chart.LegendPanel.Controls.Remove(base[index].Legend);
			}
			base.RemoveItem(index);
			Chart?.Invalidate();
		}
	}
}
