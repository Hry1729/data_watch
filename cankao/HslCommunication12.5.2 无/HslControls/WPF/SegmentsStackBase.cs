using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HslControls.WPF.Segments;

namespace HslControls.WPF
{
	[DesignTimeVisible(false)]
	public class SegmentsStackBase : SegmentBase
	{
		public static DependencyProperty ElementsCountProperty;

		public int ElementsCount
		{
			get
			{
				return (int)GetValue(ElementsCountProperty);
			}
			set
			{
				SetValue(ElementsCountProperty, value);
			}
		}

		static SegmentsStackBase()
		{
			ElementsCountProperty = DependencyProperty.Register("ElementsCount", typeof(int), typeof(SegmentsStackBase), new PropertyMetadata(1, CountChanged));
		}

		public SegmentsStackBase()
		{
			base.PropertyChanged += OnPropertyChanged;
		}

		public virtual void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
		}

		private static void CountChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			((SegmentsStackBase)sender).OnPropertyChanged(sender, e);
		}

		public ObservableCollection<CharItem> GetCharsArray()
		{
			char[] array = base.Value.ToCharArray();
			int num = array.Where((char c) => c == '.').Count();
			int num2 = array.Where((char c) => c == ':').Count();
			int num3 = array.Count() - num;
			ObservableCollection<CharItem> observableCollection = new ObservableCollection<CharItem>();
			int num4 = 0;
			if (array.Count() > 0)
			{
				for (int i = 0; i < ElementsCount; i++)
				{
					CharItem charItem = new CharItem();
					charItem.ShowDot = base.ShowDot;
					charItem.ShowColon = base.ShowColon;
					charItem.FillBrush = base.FillBrush;
					charItem.SelectedFillBrush = base.SelectedFillBrush;
					charItem.PenColor = base.PenColor;
					charItem.SelectedPenColor = base.SelectedPenColor;
					charItem.PenThickness = base.PenThickness;
					charItem.GapWidth = base.GapWidth;
					charItem.RoundedCorners = base.RoundedCorners;
					charItem.TiltAngle = base.TiltAngle;
					charItem.VertSegDivider = base.VertSegDivider;
					charItem.HorizSegDivider = base.HorizSegDivider;
					observableCollection.Add(charItem);
					if (i < ElementsCount - num3)
					{
						continue;
					}
					if (num4 <= array.Count() - 1)
					{
						if (array[num4] != '.' && array[num4] != ':')
						{
							observableCollection[i].Item = array[num4];
						}
						if (array[num4] == ':')
						{
							observableCollection[i].OnColon = true;
						}
						if (array[num4] == '.')
						{
							observableCollection[i - 1].OnDot = true;
							observableCollection[i].Item = array[num4 + 1];
							num4++;
						}
					}
					num4++;
				}
				if (ElementsCount >= num3)
				{
					if (array[array.Count() - 1] == '.')
					{
						observableCollection.Last().OnDot = true;
					}
				}
				else if (array[num4] == '.')
				{
					observableCollection[ElementsCount - 1].OnDot = true;
				}
			}
			return observableCollection;
		}
	}
}
