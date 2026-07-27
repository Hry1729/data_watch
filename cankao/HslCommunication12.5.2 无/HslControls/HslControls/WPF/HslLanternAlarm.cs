using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;

namespace HslControls.WPF
{
	public partial class HslLanternAlarm : System.Windows.Controls.UserControl, IComponentConnector
	{
		public static readonly DependencyProperty LightColorProperty = DependencyProperty.Register("LightColor", typeof(Color), typeof(HslLanternAlarm), new PropertyMetadata(Colors.Tomato));

		public static readonly DependencyProperty BottomColorProperty = DependencyProperty.Register("BottomColor", typeof(Color), typeof(HslLanternAlarm), new PropertyMetadata(Colors.DimGray));

		public static readonly DependencyProperty AlarmColorProperty = DependencyProperty.Register("AlarmColor", typeof(Color), typeof(HslLanternAlarm), new PropertyMetadata(Colors.Tomato));

		public static readonly DependencyProperty NormalColorProperty = DependencyProperty.Register("NormalColor", typeof(Color), typeof(HslLanternAlarm), new PropertyMetadata(Colors.LightGray));

		public static readonly DependencyProperty IsAlarmProperty = DependencyProperty.Register("IsAlarm", typeof(bool), typeof(HslLanternAlarm), new PropertyMetadata(false, IsAlarmPropertyChangedCallback));

		public static readonly DependencyProperty AlarmFlushTimeProperty = DependencyProperty.Register("AlarmFlushTime", typeof(int), typeof(HslLanternAlarm), new PropertyMetadata(500, AlarmFlushTimemPropertyChangedCallback));

		private bool alarmStatus = false;

		private Timer timer = null;

		public Color LightColor
		{
			get
			{
				return (Color)GetValue(LightColorProperty);
			}
			set
			{
				SetValue(LightColorProperty, value);
			}
		}

		public Color BottomColor
		{
			get
			{
				return (Color)GetValue(BottomColorProperty);
			}
			set
			{
				SetValue(BottomColorProperty, value);
			}
		}

		public Color AlarmColor
		{
			get
			{
				return (Color)GetValue(AlarmColorProperty);
			}
			set
			{
				SetValue(AlarmColorProperty, value);
			}
		}

		public Color NormalColor
		{
			get
			{
				return (Color)GetValue(NormalColorProperty);
			}
			set
			{
				SetValue(NormalColorProperty, value);
			}
		}

		public bool IsAlarm
		{
			get
			{
				return (bool)GetValue(IsAlarmProperty);
			}
			set
			{
				SetValue(IsAlarmProperty, value);
			}
		}

		public int AlarmFlushTime
		{
			get
			{
				return (int)GetValue(AlarmFlushTimeProperty);
			}
			set
			{
				SetValue(AlarmFlushTimeProperty, value);
			}
		}

		public HslLanternAlarm()
		{
			InitializeComponent();
			timer = new Timer();
			timer.Interval = AlarmFlushTime;
			timer.Tick += Timer_Tick;
		}

		public static void IsAlarmPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslLanternAlarm)dependency).UpdateTimer();
		}

		public void UpdateTimer()
		{
			if (IsAlarm)
			{
				timer.Start();
			}
			else
			{
				timer.Stop();
			}
		}

		public static void AlarmFlushTimemPropertyChangedCallback(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			((HslLanternAlarm)dependency).UpdateAlarmFlushTime();
		}

		public void UpdateAlarmFlushTime()
		{
			timer.Interval = AlarmFlushTime;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (IsAlarm)
			{
				if (alarmStatus)
				{
					LightColor = AlarmColor;
				}
				else
				{
					LightColor = NormalColor;
				}
				alarmStatus = !alarmStatus;
			}
			else
			{
				LightColor = NormalColor;
			}
		}
	}
}
