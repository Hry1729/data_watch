using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("实时曲线控件，主要用来显示实时的曲线信息，不支持光标移动显示数据信息，支持简单的交互，显示隐藏曲线")]
	public class HslCurve : UserControl
	{
		private const int value_count_max = 4096;

		private int value_Segment = 5;

		private bool value_IsAbscissaStrech = false;

		private int value_StrechDataCountMax = 300;

		private bool value_IsRenderDashLine = true;

		private string textFormat = "HH:mm";

		private int value_IntervalAbscissaText = 100;

		private Random random = null;

		private string value_title = "";

		private ReferenceAxis referenceAxisLeft;

		private ReferenceAxis referenceAxisRight;

		private int leftRight = 50;

		private int upDown = 25;

		private Dictionary<string, HslCurveItem> data_list = null;

		private string[] data_text = null;

		private List<AuxiliaryLine> auxiliary_lines;

		private List<AuxiliaryLable> auxiliary_Labels;

		private List<HslMarkText> hslMarkTexts;

		private Font font_size9 = null;

		private Brush brush_deep = null;

		private Pen pen_normal = null;

		private Pen pen_dash = null;

		private Color color_normal = Color.DeepPink;

		private Color color_deep = Color.DimGray;

		private Color color_dash = Color.Gray;

		private Color color_mark_font = Color.DodgerBlue;

		private Brush brush_mark_font = Brushes.DodgerBlue;

		private StringFormat format_left = null;

		private StringFormat format_right = null;

		private StringFormat format_center = null;

		private bool isRenderRightCoordinate = true;

		private int curveNameWidth = 100;

		private int pointsRadius = 0;

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

		[Category("HslControls")]
		[Description("获取或设置图形的左轴的坐标轴信息")]
		[Browsable(true)]
		[TypeConverter(typeof(ReferenceAxisConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public ReferenceAxis ReferenceAxisLeft => referenceAxisLeft;

		[Category("HslControls")]
		[Description("获取或设置图形的右轴的坐标轴信息")]
		[Browsable(true)]
		[TypeConverter(typeof(ReferenceAxisConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public ReferenceAxis ReferenceAxisRight => referenceAxisRight;

		[Category("HslControls")]
		[Description("获取或设置图形的纵轴分段数")]
		[Browsable(true)]
		[DefaultValue(5)]
		public virtual int ValueSegment
		{
			get
			{
				return value_Segment;
			}
			set
			{
				value_Segment = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置所有的数据是否强制在一个界面里显示")]
		[Browsable(true)]
		[DefaultValue(false)]
		public virtual bool IsAbscissaStrech
		{
			get
			{
				return value_IsAbscissaStrech;
			}
			set
			{
				value_IsAbscissaStrech = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置拉伸模式下的最大数据量")]
		[Browsable(true)]
		[DefaultValue(300)]
		public virtual int StrechDataCountMax
		{
			get
			{
				return value_StrechDataCountMax;
			}
			set
			{
				value_StrechDataCountMax = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置虚线是否进行显示")]
		[Browsable(true)]
		[DefaultValue(true)]
		public virtual bool IsRenderDashLine
		{
			get
			{
				return value_IsRenderDashLine;
			}
			set
			{
				value_IsRenderDashLine = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置坐标轴及相关信息文本的颜色")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "DimGray")]
		public virtual Color ColorLinesAndText
		{
			get
			{
				return color_deep;
			}
			set
			{
				color_deep = value;
				InitializationColor();
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置虚线的颜色")]
		[Browsable(true)]
		[DefaultValue(typeof(Color), "Gray")]
		public virtual Color ColorDashLines
		{
			get
			{
				return color_dash;
			}
			set
			{
				color_dash = value;
				pen_dash?.Dispose();
				pen_dash = new Pen(color_dash);
				pen_dash.DashStyle = DashStyle.Custom;
				pen_dash.DashPattern = new float[2] { 5f, 5f };
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置纵向虚线的分隔情况，单位为多少个数据")]
		[Browsable(true)]
		[DefaultValue(100)]
		public virtual int IntervalAbscissaText
		{
			get
			{
				return value_IntervalAbscissaText;
			}
			set
			{
				value_IntervalAbscissaText = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置实时数据新增时文本相对应于时间的格式化字符串，默认HH:mm")]
		[Browsable(true)]
		[DefaultValue("HH:mm")]
		public virtual string TextAddFormat
		{
			get
			{
				return textFormat;
			}
			set
			{
				textFormat = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置图标的标题信息")]
		[Browsable(true)]
		[DefaultValue("")]
		public virtual string Title
		{
			get
			{
				return value_title;
			}
			set
			{
				value_title = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置是否显示右侧的坐标系信息")]
		[Browsable(true)]
		[DefaultValue(true)]
		public virtual bool IsRenderRightCoordinate
		{
			get
			{
				return isRenderRightCoordinate;
			}
			set
			{
				isRenderRightCoordinate = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置曲线名称的布局宽度，默认为150")]
		[Category("HslControls")]
		[DefaultValue(100)]
		public virtual int CurveNameWidth
		{
			get
			{
				return curveNameWidth;
			}
			set
			{
				if (value > 10)
				{
					curveNameWidth = value;
				}
			}
		}

		[Browsable(true)]
		[Description("获取或设置所有的数据点显示的半径大小，默认是0，不显示数据点")]
		[Category("HslControls")]
		[DefaultValue(0)]
		public virtual int PointsRadius
		{
			get
			{
				return pointsRadius;
			}
			set
			{
				pointsRadius = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置Y轴刻度文本的字体信息")]
		[Category("HslControls")]
		public virtual Font FontCalibration
		{
			get
			{
				return font_size9;
			}
			set
			{
				font_size9 = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置左右两侧的坐标轴的宽度，以像素为单位")]
		[Category("HslControls")]
		[DefaultValue(50)]
		public int LeftRightWidth
		{
			get
			{
				return leftRight;
			}
			set
			{
				leftRight = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置上下两侧的空白的宽度，以像素为单位")]
		[Category("HslControls")]
		[DefaultValue(25)]
		public int UpDownHeight
		{
			get
			{
				return upDown;
			}
			set
			{
				upDown = value;
				Invalidate();
			}
		}

		public HslCurve()
		{
			InitializeComponent();
			random = new Random();
			data_list = new Dictionary<string, HslCurveItem>();
			auxiliary_lines = new List<AuxiliaryLine>();
			hslMarkTexts = new List<HslMarkText>();
			auxiliary_Labels = new List<AuxiliaryLable>();
			format_left = new StringFormat
			{
				LineAlignment = StringAlignment.Center,
				Alignment = StringAlignment.Near
			};
			format_right = new StringFormat
			{
				LineAlignment = StringAlignment.Center,
				Alignment = StringAlignment.Far
			};
			format_center = new StringFormat
			{
				LineAlignment = StringAlignment.Center,
				Alignment = StringAlignment.Center
			};
			font_size9 = new Font("微软雅黑", 9f);
			InitializationColor();
			pen_dash = new Pen(color_deep);
			pen_dash.DashStyle = DashStyle.Custom;
			pen_dash.DashPattern = new float[2] { 5f, 5f };
			referenceAxisLeft = new ReferenceAxis(this);
			referenceAxisRight = new ReferenceAxis(this);
			referenceAxisLeft.Color = Color.DimGray;
			referenceAxisRight.Color = Color.DimGray;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		private void InitializationColor()
		{
			pen_normal?.Dispose();
			brush_deep?.Dispose();
			pen_normal = new Pen(color_deep);
			brush_deep = new SolidBrush(color_deep);
		}

		public void SetCurveText(string[] descriptions)
		{
			data_text = descriptions;
			Invalidate();
		}

		public void SetLeftCurve(string key, float[] data)
		{
			SetLeftCurve(key, data, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
		}

		public void SetLeftCurve(string key, float[] data, Color lineColor)
		{
			SetCurve(key, 0, data, lineColor, 1f, CurveStyle.LineSegment);
		}

		public void SetLeftCurve(string key, float[] data, Color lineColor, CurveStyle style)
		{
			SetCurve(key, 0, data, lineColor, 1f, style);
		}

		public void SetRightCurve(string key, float[] data)
		{
			SetRightCurve(key, data, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
		}

		public void SetRightCurve(string key, float[] data, Color lineColor)
		{
			SetCurve(key, 1, data, lineColor, 1f, CurveStyle.LineSegment);
		}

		public void SetRightCurve(string key, float[] data, Color lineColor, CurveStyle style)
		{
			SetCurve(key, 1, data, lineColor, 1f, style);
		}

		public void SetCurve(string key, int referenceAxis, float[] data, Color lineColor, float thickness, CurveStyle style)
		{
			if (data_list.ContainsKey(key))
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_list[key].Data = data;
				if (data_text != null && data_text.Length != data.Length)
				{
					data_text = new string[data.Length];
				}
			}
			else
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_list.Add(key, new HslCurveItem
				{
					Data = data,
					MarkText = new string[data.Length],
					LineThickness = thickness,
					LineColor = lineColor,
					ReferenceAxisIndex = referenceAxis,
					Style = style
				});
				if (data_text == null || data_text.Length != data.Length)
				{
					data_text = new string[data.Length];
				}
			}
			Invalidate();
		}

		public void RemoveCurve(string key)
		{
			if (data_list.ContainsKey(key))
			{
				data_list.Remove(key);
			}
			if (data_list.Count == 0)
			{
				data_text = new string[0];
			}
			Invalidate();
		}

		public void RemoveAllCurve()
		{
			int count = data_list.Count;
			data_list.Clear();
			if (data_list.Count == 0)
			{
				data_text = new string[0];
			}
			if (count > 0)
			{
				Invalidate();
			}
		}

		public void RemoveAllCurveData()
		{
			int count = data_list.Count;
			foreach (KeyValuePair<string, HslCurveItem> item in data_list)
			{
				item.Value.Data = new float[0];
				item.Value.MarkText = new string[0];
			}
			data_text = new string[0];
			if (count > 0)
			{
				Invalidate();
			}
		}

		public HslCurveItem GetCurveItem(string key)
		{
			if (data_list.ContainsKey(key))
			{
				return data_list[key];
			}
			return null;
		}

		public Bitmap SaveToBitmap()
		{
			return SaveToBitmap(base.Width, base.Height);
		}

		public Bitmap SaveToBitmap(int width, int height)
		{
			Bitmap bitmap = new Bitmap(width, height);
			OnPaint(new PaintEventArgs(Graphics.FromImage(bitmap), new Rectangle(0, 0, width, height)));
			return bitmap;
		}

		private void AddCurveData(string key, float[] values, string[] markTexts, bool isUpdateUI)
		{
			if ((values != null && values.Length < 1) || !data_list.ContainsKey(key))
			{
				return;
			}
			HslCurveItem hslCurveItem = data_list[key];
			if (hslCurveItem.Data != null)
			{
				if (value_IsAbscissaStrech)
				{
					HslHelper.AddArrayData(ref hslCurveItem.Data, values, value_StrechDataCountMax);
					HslHelper.AddArrayData(ref hslCurveItem.MarkText, markTexts, value_StrechDataCountMax);
				}
				else
				{
					HslHelper.AddArrayData(ref hslCurveItem.Data, values, 4096);
					HslHelper.AddArrayData(ref hslCurveItem.MarkText, markTexts, 4096);
				}
				if (isUpdateUI)
				{
					Invalidate();
				}
			}
		}

		private void AddCurveTime(int count)
		{
			AddCurveTime(count, DateTime.Now.ToString(textFormat));
		}

		private void AddCurveTime(int count, string text)
		{
			if (data_text != null)
			{
				string[] array = new string[count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = text;
				}
				if (value_IsAbscissaStrech)
				{
					HslHelper.AddArrayData(ref data_text, array, value_StrechDataCountMax);
				}
				else
				{
					HslHelper.AddArrayData(ref data_text, array, 4096);
				}
			}
		}

		public void AddCurveData(string key, float value)
		{
			AddCurveData(key, new float[1] { value });
		}

		public void AddCurveData(string key, float value, string markText)
		{
			AddCurveData(key, new float[1] { value }, new string[1] { markText });
		}

		public void AddCurveData(string key, float[] values)
		{
			AddCurveData(key, values, null);
		}

		public void AddCurveData(string key, float[] values, string[] markTexts)
		{
			if (markTexts == null)
			{
				markTexts = new string[values.Length];
			}
			AddCurveData(key, values, markTexts, false);
			if (values != null && values.Length != 0)
			{
				AddCurveTime(values.Length);
			}
			Invalidate();
		}

		public void AddCurveData(string[] keys, float[] values)
		{
			AddCurveData(keys, values, null);
		}

		public void AddCurveData(string axisText, string[] keys, float[] values)
		{
			AddCurveData(axisText, keys, values, null);
		}

		public void AddCurveData(string[] keys, float[] values, string[] markTexts)
		{
			if (keys == null)
			{
				throw new ArgumentNullException("keys");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (markTexts == null)
			{
				markTexts = new string[keys.Length];
			}
			if (keys.Length != values.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			if (keys.Length != markTexts.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			for (int i = 0; i < keys.Length; i++)
			{
				AddCurveData(keys[i], new float[1] { values[i] }, new string[1] { markTexts[i] }, false);
			}
			AddCurveTime(1);
			Invalidate();
		}

		public void AddCurveData(string axisText, string[] keys, float[] values, string[] markTexts)
		{
			if (keys == null)
			{
				throw new ArgumentNullException("keys");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (markTexts == null)
			{
				markTexts = new string[keys.Length];
			}
			if (keys.Length != values.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			if (keys.Length != markTexts.Length)
			{
				throw new Exception("两个参数的数组长度不一致。");
			}
			for (int i = 0; i < keys.Length; i++)
			{
				AddCurveData(keys[i], new float[1] { values[i] }, new string[1] { markTexts[i] }, false);
			}
			AddCurveTime(1, axisText);
			Invalidate();
		}

		public void SetCurveVisible(string key, bool visible)
		{
			if (data_list.ContainsKey(key))
			{
				data_list[key].Visible = visible;
				Invalidate();
			}
		}

		public void SetCurveVisible(string[] keys, bool visible)
		{
			foreach (string key in keys)
			{
				if (data_list.ContainsKey(key))
				{
					data_list[key].Visible = visible;
				}
			}
			Invalidate();
		}

		public Dictionary<string, HslCurveItem> GetAllCurve()
		{
			return data_list;
		}

		public AuxiliaryLine AddLeftAuxiliary(float value)
		{
			return AddLeftAuxiliary(value, ColorLinesAndText);
		}

		public AuxiliaryLine AddLeftAuxiliary(float value, Color lineColor)
		{
			return AddLeftAuxiliary(value, lineColor, 1f, true);
		}

		public AuxiliaryLine AddLeftAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine)
		{
			return AddAuxiliary(value, lineColor, lineThickness, isDashLine, true);
		}

		public AuxiliaryLine AddRightAuxiliary(float value)
		{
			return AddRightAuxiliary(value, ColorLinesAndText);
		}

		public AuxiliaryLine AddRightAuxiliary(float value, Color lineColor)
		{
			return AddRightAuxiliary(value, lineColor, 1f, true);
		}

		public AuxiliaryLine AddRightAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine)
		{
			return AddAuxiliary(value, lineColor, lineThickness, isDashLine, false);
		}

		private AuxiliaryLine AddAuxiliary(float value, Color lineColor, float lineThickness, bool isDashLine, bool isLeft)
		{
			AuxiliaryLine auxiliaryLine = new AuxiliaryLine();
			auxiliaryLine.Value = value;
			auxiliaryLine.LineColor = lineColor;
			auxiliaryLine.PenDash = new Pen(lineColor)
			{
				DashStyle = DashStyle.Custom,
				DashPattern = new float[2] { 5f, 5f }
			};
			auxiliaryLine.PenSolid = new Pen(lineColor);
			auxiliaryLine.IsDashStyle = isDashLine;
			auxiliaryLine.IsLeftFrame = isLeft;
			auxiliaryLine.LineThickness = lineThickness;
			auxiliaryLine.LineTextBrush = new SolidBrush(lineColor);
			AuxiliaryLine auxiliaryLine2 = auxiliaryLine;
			auxiliary_lines.Add(auxiliaryLine2);
			Invalidate();
			return auxiliaryLine2;
		}

		public void RemoveAuxiliary(float value)
		{
			int num = 0;
			for (int num2 = auxiliary_lines.Count - 1; num2 >= 0; num2--)
			{
				if (auxiliary_lines[num2].Value == value)
				{
					auxiliary_lines[num2].Dispose();
					auxiliary_lines.RemoveAt(num2);
					num++;
				}
			}
			if (num > 0)
			{
				Invalidate();
			}
		}

		public void RemoveAuxiliary(AuxiliaryLine auxiliary)
		{
			if (auxiliary_lines.Remove(auxiliary))
			{
				Invalidate();
			}
		}

		public void RemoveAllAuxiliary()
		{
			int count = auxiliary_lines.Count;
			auxiliary_lines.Clear();
			if (count > 0)
			{
				Invalidate();
			}
		}

		public void AddAuxiliaryLabel(AuxiliaryLable auxiliaryLable)
		{
			auxiliary_Labels.Add(auxiliaryLable);
		}

		public void RemoveAuxiliaryLable(AuxiliaryLable auxiliaryLable)
		{
			if (auxiliary_Labels.Remove(auxiliaryLable))
			{
				Invalidate();
			}
		}

		public void RemoveAllAuxiliaryLable()
		{
			int count = auxiliary_Labels.Count;
			auxiliary_Labels.Clear();
			if (count > 0)
			{
				Invalidate();
			}
		}

		public void AddMarkText(HslMarkText markText)
		{
			hslMarkTexts.Add(markText);
			if (data_list.Count > 0)
			{
				Invalidate();
			}
		}

		public void RemoveMarkText(HslMarkText markText)
		{
			hslMarkTexts.Remove(markText);
			if (data_list.Count > 0)
			{
				Invalidate();
			}
		}

		public void RemoveAllMarkText()
		{
			hslMarkTexts.Clear();
			if (data_list.Count > 0)
			{
				Invalidate();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool flag = false;
			foreach (KeyValuePair<string, HslCurveItem> item in data_list)
			{
				if (item.Value.TitleRegion.Contains(e.Location))
				{
					flag = true;
					break;
				}
			}
			Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			foreach (KeyValuePair<string, HslCurveItem> item in data_list)
			{
				if (item.Value.TitleRegion.Contains(e.Location))
				{
					item.Value.LineRenderVisiable = !item.Value.LineRenderVisiable;
					Invalidate();
					break;
				}
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if (BackColor != Color.Transparent)
			{
				g.Clear(BackColor);
			}
			int num = height - upDown - upDown;
			if (width < 120 || height < 60)
			{
				return;
			}
			Point[] array = new Point[4]
			{
				new Point(leftRight - 1, upDown - 8),
				new Point(leftRight - 1, height - upDown),
				new Point(width - leftRight, height - upDown),
				new Point(width - leftRight, upDown - 8)
			};
			g.DrawLine(pen_normal, array[1], array[2]);
			g.DrawLine(referenceAxisLeft.GetPen(), array[0], array[1]);
			if (isRenderRightCoordinate)
			{
				g.DrawLine(referenceAxisRight.GetPen(), array[2], array[3]);
			}
			if (!string.IsNullOrEmpty(value_title))
			{
				g.DrawString(value_title, font_size9, brush_deep, new Rectangle(0, 0, width - 1, 20), format_center);
			}
			else if (data_list.Count > 0)
			{
				float num2 = leftRight + 10;
				foreach (KeyValuePair<string, HslCurveItem> item in data_list)
				{
					if (item.Value.Visible)
					{
						Pen pen = (item.Value.LineRenderVisiable ? new Pen(item.Value.LineColor) : new Pen(Color.FromArgb(80, item.Value.LineColor)));
						g.DrawLine(pen, num2, upDown / 2 - 1, num2 + 30f, upDown / 2 - 1);
						g.DrawEllipse(pen, num2 + 8f, upDown / 2 - 8, 14f, 14f);
						pen.Dispose();
						SolidBrush solidBrush = (item.Value.LineRenderVisiable ? new SolidBrush(item.Value.LineColor) : new SolidBrush(Color.FromArgb(80, item.Value.LineColor)));
						g.DrawString(item.Key, Font, solidBrush, new RectangleF(num2 + 35f, upDown / 2 - 9, 120f, 18f), format_left);
						item.Value.TitleRegion = new RectangleF(num2, 2f, 60f, 18f);
						solidBrush.Dispose();
						num2 += (float)curveNameWidth;
					}
				}
			}
			for (int i = 0; i < auxiliary_Labels.Count; i++)
			{
				if (!string.IsNullOrEmpty(auxiliary_Labels[i].Text))
				{
					int num3 = ((auxiliary_Labels[i].LocationX > 1f) ? ((int)auxiliary_Labels[i].LocationX) : ((int)(auxiliary_Labels[i].LocationX * (float)width)));
					int num4 = (int)g.MeasureString(auxiliary_Labels[i].Text, Font).Width + 3;
					g.FillPolygon(points: new Point[6]
					{
						new Point(num3, 11),
						new Point(num3 + 10, 20),
						new Point(num3 + num4 + 10, 20),
						new Point(num3 + num4 + 10, 0),
						new Point(num3 + 10, 0),
						new Point(num3, 11)
					}, brush: auxiliary_Labels[i].TextBack);
					g.DrawString(auxiliary_Labels[i].Text, Font, auxiliary_Labels[i].TextBrush, new Rectangle(num3 + 7, 0, num4 + 3, 20), format_center);
				}
			}
			HslHelper.PaintTriangle(g, referenceAxisLeft.Brush, new Point(leftRight - 1, upDown - 8), 4, GraphDirection.Upward);
			if (isRenderRightCoordinate)
			{
				HslHelper.PaintTriangle(g, referenceAxisRight.Brush, new Point(width - leftRight, upDown - 8), 4, GraphDirection.Upward);
			}
			if (!string.IsNullOrEmpty(referenceAxisLeft.Unit))
			{
				g.DrawString(referenceAxisLeft.Unit, font_size9, referenceAxisLeft.Brush, new RectangleF(0f, 0f, leftRight - 4, upDown - 5), format_right);
			}
			if (isRenderRightCoordinate && !string.IsNullOrEmpty(referenceAxisRight.Unit))
			{
				g.DrawString(referenceAxisRight.Unit, font_size9, referenceAxisRight.Brush, new RectangleF(width - leftRight + 4, 0f, leftRight - 4, upDown - 5), format_left);
			}
			for (int j = 0; j < auxiliary_lines.Count; j++)
			{
				if (auxiliary_lines[j].IsLeftFrame)
				{
					auxiliary_lines[j].PaintValue = HslHelper.ComputePaintLocationY(referenceAxisLeft, num, auxiliary_lines[j].Value) + (float)upDown;
				}
				else
				{
					auxiliary_lines[j].PaintValue = HslHelper.ComputePaintLocationY(referenceAxisRight, num, auxiliary_lines[j].Value) + (float)upDown;
				}
			}
			for (int k = 0; k <= value_Segment; k++)
			{
				float value = (float)((double)k * (double)(referenceAxisLeft.Max - referenceAxisLeft.Min) / (double)value_Segment + (double)referenceAxisLeft.Min);
				float num5 = HslHelper.ComputePaintLocationY(referenceAxisLeft.Max, referenceAxisLeft.Min, num, value) + (float)upDown;
				if (IsNeedPaintDash(num5))
				{
					g.DrawLine(referenceAxisLeft.GetPen(), leftRight - 4, num5, leftRight - 1, num5);
					RectangleF layoutRectangle = new RectangleF(0f, num5 - 9f, leftRight - 4, 20f);
					g.DrawString(HslHelper.GetFormatString(referenceAxisLeft.Format, value), font_size9, referenceAxisLeft.Brush, layoutRectangle, format_right);
					if (isRenderRightCoordinate)
					{
						float value2 = (float)((double)k * (double)(referenceAxisRight.Max - referenceAxisRight.Min) / (double)value_Segment + (double)referenceAxisRight.Min);
						g.DrawLine(referenceAxisRight.GetPen(), width - leftRight + 1, num5, width - leftRight + 4, num5);
						layoutRectangle.Location = new PointF(width - leftRight + 4, num5 - 9f);
						g.DrawString(HslHelper.GetFormatString(referenceAxisRight.Format, value2), font_size9, referenceAxisRight.Brush, layoutRectangle, format_left);
					}
					if (k > 0 && value_IsRenderDashLine)
					{
						g.DrawLine(pen_dash, leftRight, num5, width - leftRight, num5);
					}
				}
			}
			if (value_IsRenderDashLine)
			{
				if (value_IsAbscissaStrech)
				{
					float num6 = (float)(width - leftRight * 2) * 1f / (float)(value_StrechDataCountMax - 1);
					int num7 = CalculateDataCountByOffect(num6);
					for (int l = 0; l < value_StrechDataCountMax; l += num7)
					{
						if (l > 0 && l < value_StrechDataCountMax - 1)
						{
							g.DrawLine(pen_dash, (float)l * num6 + (float)leftRight, upDown, (float)l * num6 + (float)leftRight, height - upDown - 1);
						}
						if (data_text != null && l < data_text.Length && (float)l * num6 + (float)leftRight < (float)(data_text.Length - 1) * num6 + (float)leftRight - 40f)
						{
							g.DrawString(layoutRectangle: new Rectangle((int)((float)l * num6), height - upDown + 1, leftRight * 2, upDown), s: data_text[l], font: font_size9, brush: brush_deep, format: format_center);
						}
					}
					string[] array2 = data_text;
					if (array2 != null && array2.Length > 1)
					{
						if (data_text.Length < value_StrechDataCountMax)
						{
							g.DrawLine(pen_dash, (float)(data_text.Length - 1) * num6 + (float)leftRight, upDown, (float)(data_text.Length - 1) * num6 + (float)leftRight, height - upDown - 1);
						}
						g.DrawString(layoutRectangle: new Rectangle((int)((float)(data_text.Length - 1) * num6 + (float)leftRight) - leftRight, height - upDown + 1, leftRight * 2, upDown), s: data_text[data_text.Length - 1], font: font_size9, brush: brush_deep, format: format_center);
					}
				}
				else
				{
					int num8 = width - 2 * leftRight + 1;
					if (value_IntervalAbscissaText > 0)
					{
						for (int m = leftRight; m < width - leftRight; m += value_IntervalAbscissaText)
						{
							if (m != leftRight)
							{
								g.DrawLine(pen_dash, m, upDown, m, height - upDown - 1);
							}
							if (data_text == null)
							{
								continue;
							}
							int num9 = ((num8 > data_text.Length) ? data_text.Length : num8);
							if (m - leftRight < data_text.Length && num9 - (m - leftRight) > 40)
							{
								if (data_text.Length <= num8)
								{
									g.DrawString(layoutRectangle: new Rectangle(m - leftRight, height - upDown + 1, leftRight * 2, upDown), s: data_text[m - leftRight], font: font_size9, brush: brush_deep, format: format_center);
									continue;
								}
								g.DrawString(layoutRectangle: new Rectangle(m - leftRight, height - upDown + 1, leftRight * 2, upDown), s: data_text[m - leftRight + data_text.Length - num8], font: font_size9, brush: brush_deep, format: format_center);
							}
						}
					}
					string[] array3 = data_text;
					if (array3 != null && array3.Length > 1)
					{
						if (data_text.Length >= num8)
						{
							g.DrawString(layoutRectangle: new Rectangle(width - leftRight - leftRight, height - upDown + 1, leftRight * 2, upDown), s: data_text[data_text.Length - 1], font: font_size9, brush: brush_deep, format: format_center);
						}
						else
						{
							g.DrawLine(pen_dash, data_text.Length + leftRight - 1, upDown, data_text.Length + leftRight - 1, height - upDown - 1);
							g.DrawString(layoutRectangle: new Rectangle(data_text.Length + leftRight - 1 - leftRight, height - upDown + 1, leftRight * 2, upDown), s: data_text[data_text.Length - 1], font: font_size9, brush: brush_deep, format: format_center);
						}
					}
				}
			}
			for (int n = 0; n < auxiliary_lines.Count; n++)
			{
				if (auxiliary_lines[n].IsLeftFrame)
				{
					g.DrawLine(auxiliary_lines[n].GetPen(), leftRight - 4, auxiliary_lines[n].PaintValue, leftRight - 1, auxiliary_lines[n].PaintValue);
					g.DrawString(layoutRectangle: new RectangleF(0f, auxiliary_lines[n].PaintValue - 9f, leftRight - 4, 20f), s: HslHelper.GetFormatString(referenceAxisLeft.Format, auxiliary_lines[n].Value), font: font_size9, brush: auxiliary_lines[n].LineTextBrush, format: format_right);
				}
				else
				{
					g.DrawLine(auxiliary_lines[n].GetPen(), width - leftRight + 1, auxiliary_lines[n].PaintValue, width - leftRight + 4, auxiliary_lines[n].PaintValue);
					g.DrawString(layoutRectangle: new RectangleF(width - leftRight + 4, auxiliary_lines[n].PaintValue - 9f, leftRight - 4, 20f), s: HslHelper.GetFormatString(referenceAxisRight.Format, auxiliary_lines[n].Value), font: font_size9, brush: auxiliary_lines[n].LineTextBrush, format: format_left);
				}
				g.DrawLine(auxiliary_lines[n].GetPen(), leftRight, auxiliary_lines[n].PaintValue, width - leftRight, auxiliary_lines[n].PaintValue);
			}
			if (value_IsAbscissaStrech)
			{
				foreach (HslMarkText hslMarkText in hslMarkTexts)
				{
					foreach (KeyValuePair<string, HslCurveItem> item2 in data_list)
					{
						if (!item2.Value.Visible || !item2.Value.LineRenderVisiable || item2.Key != hslMarkText.CurveKey)
						{
							continue;
						}
						float[] data = item2.Value.Data;
						if (data != null && data.Length > 1)
						{
							float num10 = (float)(width - leftRight * 2) * 1f / (float)(value_StrechDataCountMax - 1);
							if (hslMarkText.Index >= 0 && hslMarkText.Index < item2.Value.Data.Length)
							{
								PointF center = new PointF((float)leftRight + (float)hslMarkText.Index * num10, HslHelper.ComputePaintLocationY((item2.Value.ReferenceAxisIndex == 0) ? referenceAxisLeft.Max : referenceAxisRight.Max, (item2.Value.ReferenceAxisIndex == 0) ? referenceAxisLeft.Min : referenceAxisRight.Min, num, item2.Value.Data[hslMarkText.Index]) + (float)upDown);
								HslCurveHelper.DrawHslMarkTextPoint(markTextPosition: (hslMarkText.PositionStyle == MarkTextPositionStyle.Auto) ? HslMarkText.CalculateDirectionFromDataIndex(item2.Value.Data, hslMarkText.Index) : hslMarkText.PositionStyle, g: g, markText: hslMarkText, center: center, font: Font);
							}
						}
					}
				}
				{
					foreach (HslCurveItem value3 in data_list.Values)
					{
						if (!value3.Visible || !value3.LineRenderVisiable)
						{
							continue;
						}
						float[] data2 = value3.Data;
						if (data2 == null || data2.Length <= 1)
						{
							continue;
						}
						float num11 = (float)(width - leftRight * 2) * 1f / (float)(value_StrechDataCountMax - 1);
						List<PointF> list = new List<PointF>(value3.Data.Length);
						for (int num12 = 0; num12 < value3.Data.Length; num12++)
						{
							if (!float.IsNaN(value3.Data[num12]))
							{
								PointF pointF = default(PointF);
								pointF.X = (float)leftRight + (float)num12 * num11;
								pointF.Y = HslHelper.ComputePaintLocationY((value3.ReferenceAxisIndex == 0) ? referenceAxisLeft.Max : referenceAxisRight.Max, (value3.ReferenceAxisIndex == 0) ? referenceAxisLeft.Min : referenceAxisRight.Min, num, value3.Data[num12]) + (float)upDown;
								list.Add(pointF);
								if (!string.IsNullOrEmpty(value3.MarkText[num12]))
								{
									using (Brush brush = new SolidBrush(value3.LineColor))
									{
										g.FillEllipse(brush, new RectangleF(pointF.X - 3f, pointF.Y - 3f, 6f, 6f));
										HslCurveHelper.DrawTextByPoint(markTextPosition: HslMarkText.CalculateDirectionFromDataIndex(value3.Data, num12), g: g, text: value3.MarkText[num12], center: pointF, font: Font, brush: brush, markTextOffect: 5);
									}
								}
							}
							else
							{
								HslCurveHelper.DrawLineCore(g, value3, list, pointsRadius);
								list.Clear();
							}
						}
						HslCurveHelper.DrawLineCore(g, value3, list, pointsRadius);
					}
					return;
				}
			}
			foreach (HslMarkText hslMarkText2 in hslMarkTexts)
			{
				foreach (KeyValuePair<string, HslCurveItem> item3 in data_list)
				{
					if (item3.Value.Visible && item3.Value.LineRenderVisiable && !(item3.Key != hslMarkText2.CurveKey))
					{
						float[] data3 = item3.Value.Data;
						if (data3 != null && data3.Length > 1 && hslMarkText2.Index >= 0 && hslMarkText2.Index < item3.Value.Data.Length)
						{
							PointF center2 = new PointF(leftRight + hslMarkText2.Index, HslHelper.ComputePaintLocationY((item3.Value.ReferenceAxisIndex == 0) ? referenceAxisLeft.Max : referenceAxisRight.Max, (item3.Value.ReferenceAxisIndex == 0) ? referenceAxisLeft.Min : referenceAxisRight.Min, num, item3.Value.Data[hslMarkText2.Index]) + (float)upDown);
							HslCurveHelper.DrawHslMarkTextPoint(markTextPosition: (hslMarkText2.PositionStyle == MarkTextPositionStyle.Auto) ? HslMarkText.CalculateDirectionFromDataIndex(item3.Value.Data, hslMarkText2.Index) : hslMarkText2.PositionStyle, g: g, markText: hslMarkText2, center: center2, font: Font);
						}
					}
				}
			}
			foreach (HslCurveItem value4 in data_list.Values)
			{
				if (!value4.Visible || !value4.LineRenderVisiable)
				{
					continue;
				}
				float[] data4 = value4.Data;
				if (data4 == null || data4.Length <= 1)
				{
					continue;
				}
				int num13 = width - 2 * leftRight + 1;
				List<PointF> list2 = new List<PointF>(value4.Data.Length);
				if (value4.Data.Length <= num13)
				{
					for (int num14 = 0; num14 < value4.Data.Length; num14++)
					{
						if (!float.IsNaN(value4.Data[num14]))
						{
							PointF pointF2 = default(PointF);
							pointF2.X = leftRight + num14;
							pointF2.Y = HslHelper.ComputePaintLocationY((value4.ReferenceAxisIndex == 0) ? referenceAxisLeft.Max : referenceAxisRight.Max, (value4.ReferenceAxisIndex == 0) ? referenceAxisLeft.Min : referenceAxisRight.Min, num, value4.Data[num14]) + (float)upDown;
							list2.Add(pointF2);
							DrawMarkPoint(g, value4.MarkText[num14], pointF2, value4.LineColor, HslMarkText.CalculateDirectionFromDataIndex(value4.Data, num14));
						}
						else
						{
							HslCurveHelper.DrawLineCore(g, value4, list2, pointsRadius);
							list2.Clear();
						}
					}
				}
				else
				{
					for (int num15 = 0; num15 < num13; num15++)
					{
						int num16 = num15 + value4.Data.Length - num13;
						if (!float.IsNaN(value4.Data[num16]))
						{
							PointF pointF3 = default(PointF);
							pointF3.X = leftRight + num15;
							pointF3.Y = HslHelper.ComputePaintLocationY((value4.ReferenceAxisIndex == 0) ? referenceAxisLeft.Max : referenceAxisRight.Max, (value4.ReferenceAxisIndex == 0) ? referenceAxisLeft.Min : referenceAxisRight.Min, num, value4.Data[num16]) + (float)upDown;
							list2.Add(pointF3);
							DrawMarkPoint(g, value4.MarkText[num16], pointF3, value4.LineColor, HslMarkText.CalculateDirectionFromDataIndex(value4.Data, num16));
						}
						else
						{
							HslCurveHelper.DrawLineCore(g, value4, list2, pointsRadius);
							list2.Clear();
						}
					}
				}
				HslCurveHelper.DrawLineCore(g, value4, list2, pointsRadius);
			}
		}

		private void DrawMarkPoint(Graphics g, string markText, PointF center, Color color, MarkTextPositionStyle markTextPosition)
		{
			if (!string.IsNullOrEmpty(markText))
			{
				using (Brush brush = new SolidBrush(color))
				{
					DrawMarkPoint(g, markText, center, brush, markTextPosition);
				}
			}
		}

		private void DrawMarkPoint(Graphics g, string markText, PointF center, Brush brush, MarkTextPositionStyle markTextPosition)
		{
			if (!string.IsNullOrEmpty(markText))
			{
				g.FillEllipse(brush, new RectangleF(center.X - 3f, center.Y - 3f, 6f, 6f));
				HslCurveHelper.DrawTextByPoint(g, markText, center, Font, brush, markTextPosition, 5);
			}
		}

		private bool IsNeedPaintDash(float paintValue)
		{
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				if (Math.Abs(auxiliary_lines[i].PaintValue - paintValue) < (float)font_size9.Height)
				{
					return false;
				}
			}
			return true;
		}

		private int CalculateDataCountByOffect(float offect)
		{
			if (value_IntervalAbscissaText > 0)
			{
				return value_IntervalAbscissaText;
			}
			if (offect > 40f)
			{
				return 1;
			}
			offect = 40f / offect;
			return (int)Math.Ceiling(offect);
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
			base.Name = "HslCurve";
			base.Size = new System.Drawing.Size(417, 205);
			base.ResumeLayout(false);
		}
	}
}
