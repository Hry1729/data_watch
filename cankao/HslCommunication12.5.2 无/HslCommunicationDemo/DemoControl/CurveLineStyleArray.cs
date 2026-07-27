using System.Collections.Generic;
using System.ComponentModel;

namespace HslCommunicationDemo.DemoControl
{
	public class CurveLineStyleArray
	{
		[TypeConverter(typeof(ArrayConverter))]
		public List<CurveLineStyle> Items { get; set; } = new List<CurveLineStyle>();
	}
}
