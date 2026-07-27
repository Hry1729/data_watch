using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Markup;
using HslControls.WPF.Segments;

namespace HslControls.WPF
{
	public partial class SixteenSegmentsStack : SegmentsStackBase, IComponentConnector
	{
		private ObservableCollection<CharItem> ValueChars;

		public SixteenSegmentsStack()
		{
			InitializeComponent();
			base.VertSegDivider = SegmentBase.defVertDividerSixteen;
			base.HorizSegDivider = SegmentBase.defHorizDividerSixteen;
		}

		public override void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			ValueChars = GetCharsArray();
			SegmentsArray.ItemsSource = ValueChars;
		}
	}
}
