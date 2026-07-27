using System.ComponentModel;
using System.Drawing;

namespace HslControls
{
	public class MarkValue
	{
		[Description("获取或设置当前的值数据")]
		public float Value { get; set; }

		[Description("获取或设置当前的颜色信息")]
		public Color Color { get; set; }

		[Description("获取或设置当前的刻度宽度信息")]
		public float LineWidth { get; set; }

		public MarkValue()
		{
			Value = 0f;
			Color = Color.DodgerBlue;
			LineWidth = 1f;
		}
	}
}
