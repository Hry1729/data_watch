using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslControls
{
	[TypeConverter(typeof(ReferenceAxisConverter))]
	public class ReferenceAxis
	{
		private float _max;

		private float _min;

		private string _unit;

		private string _format;

		private Color _color;

		private UserControl _control;

		private Pen _pen;

		private bool useLogarithms;

		[Category("HslControls")]
		[Description("获取或设置当前的Y轴的最大值")]
		[Browsable(true)]
		[DefaultValue(100f)]
		public float Max
		{
			get
			{
				return _max;
			}
			set
			{
				_max = value;
				_control?.Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置当前的Y轴的最小值")]
		[Browsable(true)]
		[DefaultValue(0f)]
		public float Min
		{
			get
			{
				return _min;
			}
			set
			{
				_min = value;
				_control?.Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置当前的Y轴的单位值")]
		[Browsable(true)]
		[DefaultValue("")]
		public string Unit
		{
			get
			{
				return _unit;
			}
			set
			{
				_unit = value;
				_control?.Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("是否使用对数的表示方式，在开始对数的模式下，无法表示小于等于0的数值，对数以10为底数")]
		[Browsable(true)]
		[DefaultValue(false)]
		public bool UseLogarithms
		{
			get
			{
				return useLogarithms;
			}
			set
			{
				useLogarithms = value;
				_control?.Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置当前的坐标系的颜色信息")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color Color
		{
			get
			{
				return _color;
			}
			set
			{
				_color = value;
				Brush?.Dispose();
				Brush = new SolidBrush(_color);
				_pen?.Dispose();
				_pen = new Pen(_color, 1f);
				_control?.Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置当前坐标轴数字的格式化信息，默认为 {0}, 直接转字符串")]
		[Browsable(true)]
		[DefaultValue("{0}")]
		public string Format
		{
			get
			{
				return _format;
			}
			set
			{
				_format = value;
				_control?.Invalidate();
			}
		}

		[Browsable(false)]
		public Brush Brush { get; set; }

		public ReferenceAxis()
		{
			Max = 100f;
			Min = 0f;
			Color = Color.LightGray;
			Format = "{0}";
		}

		public ReferenceAxis(UserControl userControl)
			: this()
		{
			_control = userControl;
		}

		public ReferenceAxis(float max, float min, bool useLog)
		{
			Max = max;
			Min = min;
			useLogarithms = useLog;
		}

		public Pen GetPen()
		{
			if (_pen != null)
			{
				return _pen;
			}
			_pen = new Pen(Color, 1f);
			return _pen;
		}

		public bool IsUseLogarithms()
		{
			return useLogarithms && _max > 0f && _min > 0f;
		}

		public float GetCalculateMax()
		{
			if (IsUseLogarithms())
			{
				return (float)Math.Log10(Max);
			}
			return Max;
		}

		public float GetCalculateMin()
		{
			if (IsUseLogarithms())
			{
				return (float)Math.Log10(Min);
			}
			return Min;
		}

		public void SetMaxValue(float value)
		{
			_max = value;
		}

		public void SetMinValue(float value)
		{
			_min = value;
		}
	}
}
