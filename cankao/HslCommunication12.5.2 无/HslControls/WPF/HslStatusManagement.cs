using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HslControls.WPF
{
	public partial class HslStatusManagement : UserControl, IComponentConnector
	{
		public static readonly DependencyProperty SpacingOffectProperty = DependencyProperty.Register("SpacingOffect", typeof(float), typeof(HslStatusManagement), new PropertyMetadata(4f, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty RowCountProperty = DependencyProperty.Register("RowCount", typeof(int), typeof(HslStatusManagement), new PropertyMetadata(2, RenderUpdateArrayByPropertyChanged));

		public static readonly DependencyProperty ColCountProperty = DependencyProperty.Register("ColCount", typeof(int), typeof(HslStatusManagement), new PropertyMetadata(10, RenderUpdateArrayByPropertyChanged));

		public static readonly DependencyProperty IsRenderBorderProperty = DependencyProperty.Register("IsRenderBorder", typeof(bool), typeof(HslStatusManagement), new PropertyMetadata(true, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty RenderStyleProperty = DependencyProperty.Register("RenderStyle", typeof(HslRenderStyle), typeof(HslStatusManagement), new PropertyMetadata(HslRenderStyle.Rectangle, RenderUpdateByPropertyChanged));

		public static readonly DependencyProperty GeneralColorProperty = DependencyProperty.Register("GeneralColor", typeof(System.Windows.Media.Color), typeof(HslStatusManagement), new PropertyMetadata(Colors.LightGray, RenderUpdateArrayByPropertyChanged));

		public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(System.Windows.Media.Color), typeof(HslStatusManagement), new PropertyMetadata(Colors.DimGray, RenderUpdateByPropertyChanged));

		private System.Windows.Media.Color[,] arrayColor;

		private string[,] descriptions;

		public float SpacingOffect
		{
			get
			{
				return (float)GetValue(SpacingOffectProperty);
			}
			set
			{
				SetValue(SpacingOffectProperty, value);
			}
		}

		public int RowCount
		{
			get
			{
				return (int)GetValue(RowCountProperty);
			}
			set
			{
				SetValue(RowCountProperty, value);
			}
		}

		public int ColCount
		{
			get
			{
				return (int)GetValue(ColCountProperty);
			}
			set
			{
				SetValue(ColCountProperty, value);
			}
		}

		public bool IsRenderBorder
		{
			get
			{
				return (bool)GetValue(IsRenderBorderProperty);
			}
			set
			{
				SetValue(IsRenderBorderProperty, value);
			}
		}

		public HslRenderStyle RenderStyle
		{
			get
			{
				return (HslRenderStyle)GetValue(RenderStyleProperty);
			}
			set
			{
				SetValue(RenderStyleProperty, value);
			}
		}

		public System.Windows.Media.Color GeneralColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(GeneralColorProperty);
			}
			set
			{
				SetValue(GeneralColorProperty, value);
			}
		}

		public System.Windows.Media.Color BorderColor
		{
			get
			{
				return (System.Windows.Media.Color)GetValue(BorderColorProperty);
			}
			set
			{
				SetValue(BorderColorProperty, value);
			}
		}

		public HslStatusManagement()
		{
			InitializeComponent();
			ArrayInni();
		}

		public static void RenderUpdateByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslStatusManagement hslStatusManagement)
			{
				hslStatusManagement.HslStatusManagementUpdate();
			}
		}

		public static void RenderUpdateArrayByPropertyChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs e)
		{
			if (dependency is HslStatusManagement hslStatusManagement)
			{
				hslStatusManagement.ArrayInni();
				hslStatusManagement.HslStatusManagementUpdate();
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			HslStatusManagementUpdate();
		}

		public void HslStatusManagementUpdate()
		{
			canvas1.Children.Clear();
			int num = (int)base.ActualWidth;
			int num2 = (int)base.ActualHeight;
			if (num == 0 || num2 == 0)
			{
				return;
			}
			float num3 = ((float)num - (float)ColCount * SpacingOffect) / (float)ColCount;
			float num4 = ((float)num2 - (float)RowCount * SpacingOffect) / (float)RowCount;
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle((int)(SpacingOffect / 2f + (num3 + SpacingOffect) * (float)j), (int)(SpacingOffect / 2f + (num4 + SpacingOffect) * (float)i), (int)num3, (int)num4);
					if (RenderStyle == HslRenderStyle.Rectangle)
					{
						System.Windows.Shapes.Rectangle rectangle2 = WpfUtils.FillRectangle(canvas1, rectangle, arrayColor[i, j]);
						if (!string.IsNullOrEmpty(descriptions[i, j]) && rectangle2 != null)
						{
							rectangle2.ToolTip = descriptions[i, j];
						}
						if (IsRenderBorder)
						{
							WpfUtils.DrawRectangle(canvas1, rectangle, BorderColor, 1f);
						}
					}
					else if (RenderStyle == HslRenderStyle.Ellipse)
					{
						Ellipse ellipse = WpfUtils.FillEllipse(canvas1, rectangle, arrayColor[i, j]);
						if (!string.IsNullOrEmpty(descriptions[i, j]) && ellipse != null)
						{
							ellipse.ToolTip = descriptions[i, j];
						}
						if (IsRenderBorder)
						{
							WpfUtils.DrawEllipse(canvas1, rectangle, BorderColor, 1.0);
						}
					}
					else
					{
						Polygon polygon = WpfUtils.FillPolygon(canvas1, WpfUtils.TransMediaPoints(HslHelper.GetRhombusFromRectangle(rectangle)), arrayColor[i, j]);
						if (!string.IsNullOrEmpty(descriptions[i, j]) && polygon != null)
						{
							polygon.ToolTip = descriptions[i, j];
						}
						if (IsRenderBorder)
						{
							WpfUtils.DrawPolygon(canvas1, WpfUtils.TransMediaPoints(HslHelper.GetRhombusFromRectangle(rectangle)), BorderColor, 1f);
						}
					}
				}
			}
		}

		public void SetColorByLocation(int rowIndex, int colIndex, System.Windows.Media.Color color)
		{
			arrayColor[rowIndex, colIndex] = color;
			HslStatusManagementUpdate();
		}

		public void ResetColorByLocation(int rowIndex, int colIndex)
		{
			arrayColor[rowIndex, colIndex] = GeneralColor;
			HslStatusManagementUpdate();
		}

		public void SetColorByLocation(System.Windows.Point point, System.Windows.Media.Color color)
		{
			SetColorByLocation((int)point.X, (int)point.Y, color);
		}

		public void ResetColorByLocation(System.Windows.Point point)
		{
			ResetColorByLocation((int)point.X, (int)point.Y);
		}

		public void SetColorByLocation(System.Windows.Point[] points, System.Windows.Media.Color color)
		{
			if (points != null)
			{
				for (int i = 0; i < points.Length; i++)
				{
					arrayColor[(int)points[i].X, (int)points[i].Y] = color;
				}
				HslStatusManagementUpdate();
			}
		}

		public void ResetColorByLocation(System.Windows.Point[] points)
		{
			if (points != null)
			{
				for (int i = 0; i < points.Length; i++)
				{
					arrayColor[(int)points[i].X, (int)points[i].Y] = GeneralColor;
				}
				HslStatusManagementUpdate();
			}
		}

		public void SetColorByRow(int rowIndex, System.Windows.Media.Color color)
		{
			for (int i = 0; i < ColCount; i++)
			{
				arrayColor[rowIndex, i] = color;
			}
			HslStatusManagementUpdate();
		}

		public void SetColorByRow(int rowIndex, System.Windows.Media.Color[] colors)
		{
			for (int i = 0; i < ColCount; i++)
			{
				if (i < colors.Length)
				{
					arrayColor[rowIndex, i] = colors[i];
				}
			}
			HslStatusManagementUpdate();
		}

		public void ResetColorByRow(int rowIndex)
		{
			for (int i = 0; i < ColCount; i++)
			{
				arrayColor[rowIndex, i] = GeneralColor;
			}
			HslStatusManagementUpdate();
		}

		public void SetColorByCol(int colIndex, System.Windows.Media.Color color)
		{
			for (int i = 0; i < RowCount; i++)
			{
				arrayColor[i, colIndex] = color;
			}
			HslStatusManagementUpdate();
		}

		public void SetColorByCol(int colIndex, System.Windows.Media.Color[] colors)
		{
			for (int i = 0; i < RowCount; i++)
			{
				if (i < colors.Length)
				{
					arrayColor[i, colIndex] = colors[i];
				}
			}
			HslStatusManagementUpdate();
		}

		public void ResetColorByCol(int colIndex)
		{
			for (int i = 0; i < RowCount; i++)
			{
				arrayColor[i, colIndex] = GeneralColor;
			}
			HslStatusManagementUpdate();
		}

		public void SetColorAll(System.Windows.Media.Color color)
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					arrayColor[i, j] = color;
				}
			}
			HslStatusManagementUpdate();
		}

		public void SetColorAll(System.Windows.Media.Color[] colors)
		{
			int num = 0;
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					if (num >= colors.Length)
					{
						break;
					}
					arrayColor[i, j] = colors[num];
					num++;
				}
			}
			HslStatusManagementUpdate();
		}

		public void ResetColorAll()
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					arrayColor[i, j] = GeneralColor;
				}
			}
			HslStatusManagementUpdate();
		}

		public System.Windows.Media.Color GetColorFromPosition(int rowIndex, int colIndex)
		{
			return arrayColor[rowIndex, colIndex];
		}

		public void SetDescription(int rowIndex, int colIndex, string description)
		{
			descriptions[rowIndex, colIndex] = description;
			HslStatusManagementUpdate();
		}

		public void SetAllDescription(string description)
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					descriptions[i, j] = description;
				}
			}
			HslStatusManagementUpdate();
		}

		public void ClearAllDescription()
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					descriptions[i, j] = string.Empty;
				}
			}
			HslStatusManagementUpdate();
		}

		public void SetAllDescription(string[] descs)
		{
			int num = 0;
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					if (num >= descs.Length)
					{
						break;
					}
					descriptions[i, j] = descs[num];
					num++;
				}
			}
			HslStatusManagementUpdate();
		}

		public void ArrayInni()
		{
			arrayColor = new System.Windows.Media.Color[RowCount, ColCount];
			descriptions = new string[RowCount, ColCount];
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColCount; j++)
				{
					arrayColor[i, j] = GeneralColor;
					descriptions[i, j] = string.Empty;
				}
			}
		}
	}
}
