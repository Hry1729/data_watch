using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("一个表格控件")]
	public class HslTable : UserControl
	{
		public delegate void DrawCellTextDelegate(Graphics g, int rowIndex, int colIndex, RectangleF rectangle, string value, StringFormat sf);

		private StringFormat sf = null;

		private Color borderColor = Color.LightGray;

		private Pen borderPen = new Pen(Color.LightGray);

		private Brush textBrush = new SolidBrush(Color.Gray);

		private Color headerColor = Color.DimGray;

		private Brush headerBrush = new SolidBrush(Color.DimGray);

		private Color topTextColor = Color.DarkSlateGray;

		private Brush topTextBrush = new SolidBrush(Color.DarkSlateGray);

		private float headTextSize = 18f;

		private float headHeight = 0.25f;

		private int rowsTotalCount = 5;

		private float[] columnWidth = new float[6] { 0f, 0.3f, 0.45f, 0.55f, 0.8f, 1f };

		private string[] columnHeader = new string[5] { "规格", "生产数量", "工艺", "完成度", "特别说明" };

		private string topHeaderText = "今日生产计划";

		private string assistHeaderText = "9月2日";

		private List<string[]> tableValues = new List<string[]>();

		private bool isShowColumnHeader = true;

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Transparent")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color BackColor
		{
			get
			{
				return base.BackColor;
			}
			set
			{
				base.BackColor = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前控件的文本")]
		[Category("HslControls")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Bindable(true)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				base.Text = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件的前景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color ForeColor
		{
			get
			{
				return base.ForeColor;
			}
			set
			{
				base.ForeColor = value;
				textBrush?.Dispose();
				textBrush = new SolidBrush(value);
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件的表格边框的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				borderPen?.Dispose();
				borderPen = new Pen(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的标题的文本颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color HeaderColor
		{
			get
			{
				return headerColor;
			}
			set
			{
				headerColor = value;
				headerBrush?.Dispose();
				headerBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前的最大标题的文本颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DarkSlateGray")]
		public Color TopHeaderColor
		{
			get
			{
				return topTextColor;
			}
			set
			{
				topTextColor = value;
				topTextBrush?.Dispose();
				topTextBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前表格的数据行的行数，不包括顶部最大的标题行")]
		[Category("HslControls")]
		[DefaultValue(5)]
		public int RowsTotalCount
		{
			get
			{
				return rowsTotalCount;
			}
			set
			{
				if (rowsTotalCount > 0)
				{
					rowsTotalCount = value;
					Invalidate();
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前最大标题的文本信息，默认为今日生产计划")]
		[Category("HslControls")]
		[DefaultValue("今日生产计划")]
		public string TopHeaderText
		{
			get
			{
				return topHeaderText;
			}
			set
			{
				topHeaderText = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前辅助标题的文本信息，默认为9月2日")]
		[Category("HslControls")]
		[DefaultValue("9月2日")]
		public string AssistHeaderText
		{
			get
			{
				return assistHeaderText;
			}
			set
			{
				assistHeaderText = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前最大标题的高度，如果小于1就是百分比，如果大于1就是绝对值")]
		[Category("HslControls")]
		[DefaultValue(0.25f)]
		public float TopHeaderHeight
		{
			get
			{
				return headHeight;
			}
			set
			{
				headHeight = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("列的宽度信息设置，数组的长度决定了列的数量")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public float[] ColumnWidth
		{
			get
			{
				return columnWidth;
			}
			set
			{
				columnWidth = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("列的标题信息设置，数组长度应该刚好是列的数组，如果少于，多出来的列则为空，如果大于，多出来的列则不显示")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public string[] ColumnHeader
		{
			get
			{
				return columnHeader;
			}
			set
			{
				columnHeader = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置顶部标题的字体大小，默认18")]
		[Category("HslControls")]
		[DefaultValue(28f)]
		public float TopHeaderTextSize
		{
			get
			{
				return headTextSize;
			}
			set
			{
				headTextSize = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置是否显示列标题的文本信息")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool IsShowColumnHeader
		{
			get
			{
				return isShowColumnHeader;
			}
			set
			{
				isShowColumnHeader = value;
				Invalidate();
			}
		}

		public event DrawCellTextDelegate OnDrawCellTextEvent;

		public HslTable()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			if (GetService(typeof(IDesignerHost)) != null || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
			{
				tableValues.Add(new string[5] { "φ31-61-2.5", "1000", "A类型", "800 nm", "" });
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			if (base.Width >= 20 && base.Height >= 20)
			{
				PaintHslControls(e.Graphics, base.Width, base.Height);
			}
		}

		public void PaintHslControls(Graphics g, int width, float height)
		{
			float num = ((headHeight < 1f) ? (height * headHeight) : headHeight);
			float num2 = ((rowsTotalCount == 0) ? (height - num) : ((height - num) / (float)rowsTotalCount));
			if (headHeight > 0f)
			{
				RectangleF layoutRectangle = new RectangleF(0f, 0f, width, num);
				using (Font font = new Font(Font.FontFamily, headTextSize))
				{
					g.DrawString(topHeaderText, font, topTextBrush, layoutRectangle, sf);
					SizeF sizeF = g.MeasureString(topHeaderText, font);
					g.DrawString(assistHeaderText, Font, topTextBrush, new PointF((float)width * 0.5f + sizeF.Width / 2f, layoutRectangle.Height / 2f + (float)font.Height / 2f - (float)Font.Height - 1f));
				}
			}
			if (isShowColumnHeader)
			{
				for (int i = 0; i < columnHeader.Length; i++)
				{
					if (i + 1 < columnWidth.Length)
					{
						RectangleF layoutRectangle2 = new RectangleF((float)width * columnWidth[i], num, (float)width * (columnWidth[i + 1] - columnWidth[i]), num2);
						if (!string.IsNullOrEmpty(columnHeader[i]))
						{
							g.DrawString(columnHeader[i], Font, headerBrush, layoutRectangle2, sf);
						}
					}
				}
			}
			g.DrawLine(borderPen, 0, 0, width - 1, 0);
			if (headHeight > 0f)
			{
				g.DrawLine(borderPen, 0, (int)num, width - 1, (int)num);
			}
			for (int j = 1; j < rowsTotalCount; j++)
			{
				g.DrawLine(borderPen, 0, (int)((float)j * num2 + num), width - 1, (int)((float)j * num2 + num));
			}
			g.DrawLine(borderPen, 0f, height - 1f, width - 1, height - 1f);
			g.DrawLine(borderPen, 0f, 0f, 0f, height - 1f);
			g.DrawLine(borderPen, width - 1, 0f, width - 1, height - 1f);
			for (int k = 0; k < columnWidth.Length - 1; k++)
			{
				g.DrawLine(borderPen, (int)((float)width * columnWidth[k]), (int)num, (int)((float)width * columnWidth[k]), height - 1f);
			}
			for (int l = 0; l < tableValues.Count; l++)
			{
				for (int m = 0; m < columnHeader.Length; m++)
				{
					if (m + 1 >= columnWidth.Length)
					{
						continue;
					}
					RectangleF rectangle = new RectangleF((float)width * columnWidth[m], num2 * (float)(l + (isShowColumnHeader ? 1 : 0)) + num, (float)width * (columnWidth[m + 1] - columnWidth[m]) - 1f, num2 - 1f);
					if (m < tableValues[l].Length)
					{
						if (this.OnDrawCellTextEvent == null)
						{
							DrawCellText(g, l, m, rectangle, tableValues[l][m], sf);
						}
						else
						{
							this.OnDrawCellTextEvent(g, l, m, rectangle, tableValues[l][m], sf);
						}
					}
				}
			}
		}

		public virtual void DrawCellText(Graphics g, int rowIndex, int colIndex, RectangleF rectangle, string value, StringFormat sf)
		{
			g.DrawString(value, Font, textBrush, rectangle, sf);
		}

		public void AddRowDown(string[] values)
		{
			AddRowDown(new List<string[]> { values });
		}

		public void AddRowDown(List<string[]> values)
		{
			tableValues.AddRange(values);
			if (isShowColumnHeader)
			{
				while (tableValues.Count >= rowsTotalCount)
				{
					tableValues.RemoveAt(0);
				}
			}
			else
			{
				while (tableValues.Count > rowsTotalCount)
				{
					tableValues.RemoveAt(0);
				}
			}
			Invalidate();
		}

		public void AddRowTop(string[] values)
		{
			AddRowTop(new List<string[]> { values });
		}

		public void AddRowTop(List<string[]> values)
		{
			if (values != null)
			{
				for (int i = 0; i < values.Count; i++)
				{
					tableValues.Insert(0, values[i]);
				}
			}
			if (isShowColumnHeader)
			{
				while (tableValues.Count >= rowsTotalCount)
				{
					tableValues.RemoveAt(tableValues.Count - 1);
				}
			}
			else
			{
				while (tableValues.Count > rowsTotalCount)
				{
					tableValues.RemoveAt(tableValues.Count - 1);
				}
			}
			Invalidate();
		}

		public void SetTableValue(List<string[]> values)
		{
			tableValues = values;
			Invalidate();
		}

		public void SetTableValue(int rowIndex, int colIndex, string value, bool updateUI = true)
		{
			if (rowIndex < tableValues.Count && colIndex < tableValues[rowIndex].Length)
			{
				tableValues[rowIndex][colIndex] = value;
				if (updateUI)
				{
					Invalidate();
				}
			}
		}

		public string GetTableValue(int rowIndex, int colIndex)
		{
			if (rowIndex >= tableValues.Count)
			{
				return null;
			}
			if (colIndex >= tableValues[rowIndex].Length)
			{
				return null;
			}
			return tableValues[rowIndex][colIndex];
		}

		public List<string[]> GetTableValue()
		{
			return tableValues;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			base.SuspendLayout();
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.Transparent;
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.ForeColor = System.Drawing.Color.Gray;
			base.Name = "HslTable";
			base.Size = new System.Drawing.Size(444, 206);
			base.ResumeLayout(false);
		}
	}
}
