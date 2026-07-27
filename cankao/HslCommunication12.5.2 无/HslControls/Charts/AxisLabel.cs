using System.ComponentModel;

namespace HslControls.Charts
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class AxisLabel
	{
		private string _content;

		private double _value;

		public string Content
		{
			get
			{
				return _content;
			}
			set
			{
				_content = value;
			}
		}

		public double Value
		{
			get
			{
				return _value;
			}
			set
			{
				_value = value;
			}
		}

		public AxisLabel(double value)
		{
			_value = value;
			_content = _value.ToString();
		}

		public AxisLabel()
			: this(0.0)
		{
		}
	}
}
