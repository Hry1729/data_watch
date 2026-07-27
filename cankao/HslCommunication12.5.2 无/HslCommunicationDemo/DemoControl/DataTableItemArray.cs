using System.Collections.Generic;
using System.ComponentModel;

namespace HslCommunicationDemo.DemoControl
{
	public class DataTableItemArray
	{
		[TypeConverter(typeof(ArrayConverter))]
		public List<DataTableItem> Items { get; set; }
	}
}
