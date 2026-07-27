using System.Collections.ObjectModel;

namespace HslControls
{
	public class ReferenceAxisCollection : Collection<ReferenceAxis>
	{
		private HslCurveHistory hslCurveHistory;

		public ReferenceAxisCollection(HslCurveHistory hslCurveHistory)
		{
			this.hslCurveHistory = hslCurveHistory;
		}

		protected override void InsertItem(int index, ReferenceAxis item)
		{
			base.InsertItem(index, item);
			hslCurveHistory?.Invalidate();
		}

		protected override void SetItem(int index, ReferenceAxis item)
		{
			base.SetItem(index, item);
			hslCurveHistory?.Invalidate();
		}

		protected override void ClearItems()
		{
			base.ClearItems();
			hslCurveHistory?.Invalidate();
		}

		protected override void RemoveItem(int index)
		{
			base.RemoveItem(index);
			hslCurveHistory?.Invalidate();
		}
	}
}
