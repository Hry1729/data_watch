using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace HslControls.WPF
{
	public partial class HslMoveText : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty MoveSpeedProperty = DependencyProperty.Register("MoveSpeed", typeof(double), typeof(HslMoveText), new PropertyMetadata(1.0));

		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HslMoveText), new PropertyMetadata("HslMoveText"));

		public double MoveSpeed
		{
			get
			{
				return (double)GetValue(MoveSpeedProperty);
			}
			set
			{
				SetValue(MoveSpeedProperty, value);
			}
		}

		public string Text
		{
			get
			{
				return (string)GetValue(TextProperty);
			}
			set
			{
				SetValue(TextProperty, value);
			}
		}

		public HslMoveText()
		{
			InitializeComponent();
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			DoubleAnimation_Completed(null, new EventArgs());
		}

		private async void DoubleAnimation_Completed(object sender, EventArgs e)
		{
			await Task.Delay(50);
			if (MoveSpeed != 0.0)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation(canvas1.ActualWidth, 0.0 - label1.ActualWidth, TimeSpan.FromSeconds((canvas1.ActualWidth + label1.ActualWidth) / 50.0 / MoveSpeed));
				doubleAnimation.Completed += DoubleAnimation_Completed;
				label1.BeginAnimation(Canvas.LeftProperty, doubleAnimation);
			}
		}
	}
}
