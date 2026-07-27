using System;

namespace HslControls.Charts
{
	public class ChartPointEventArgs : EventArgs
	{
		public ChartPoint[] Point { get; private set; }

		public string Content { get; set; }

		public ChartPointEventArgs(ChartPoint[] point = null, string content = null)
		{
			Point = point;
			Content = content;
		}
	}
}
