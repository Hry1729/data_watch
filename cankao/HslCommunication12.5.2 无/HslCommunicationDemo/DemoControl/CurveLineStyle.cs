using System.ComponentModel;
using System.Drawing;
using HslControls;

namespace HslCommunicationDemo.DemoControl
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class CurveLineStyle
	{
		[ReadOnly(true)]
		[Description("曲线的关键字")]
		public string Key { get; set; } = string.Empty;

		[Browsable(true)]
		[Description("线条的宽度，默认1.0f")]
		[DefaultValue(1f)]
		public float Width { get; set; } = 1f;

		[Browsable(true)]
		[Description("曲线的颜色")]
		public Color Color { get; set; } = Color.Red;

		[Browsable(true)]
		[Description("曲线的样式")]
		public CurveStyle Style { get; set; } = CurveStyle.Section;

		[Browsable(true)]
		[Description("曲线显示数据格式化的字符串信息，例如摄氏度，{0} ℃")]
		[DefaultValue("{0}")]
		public string Format { get; set; }

		public override string ToString()
		{
			return "LineStyle[" + Key + "]";
		}
	}
}
