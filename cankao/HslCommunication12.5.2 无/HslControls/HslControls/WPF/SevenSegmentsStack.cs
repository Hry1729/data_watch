using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Markup;
using HslControls.WPF.Segments;

namespace HslControls.WPF
{
	public partial class SevenSegmentsStack : SegmentsStackBase, IComponentConnector
	{
		private ObservableCollection<CharItem> ValueChars;

		public SevenSegmentsStack()
		{
			InitializeComponent();
		}

		public override void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			ValueChars = GetCharsArray();
			SegmentsArray.ItemsSource = ValueChars;
		}
	}
}
