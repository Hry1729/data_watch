using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace HslControls
{
	[Description("历史曲线控件，支持光标移动，显示x,y轴数据信息，支持多条曲线，支持左右两个坐标轴")]
	[ToolboxBitmap(typeof(HslCurveHistory), "Resources.HslCurveHistory.bmp")]
	public class HslCurveHistory : UserControl
	{
		public delegate void CurveDoubleClick(HslCurveHistory hslCurve, int index, DateTime dateTime);

		public delegate void CurveCustomerDoubleClick(HslCurveHistory hslCurve, int index, string customer);

		public delegate void CurveMouseMove(HslCurveHistory hslCurve, int x, int y);

		public delegate void CurveRangeSelect(HslCurveHistory hslCurve, HslMarkForeSection markForeSection);

		public delegate void CurveScollScaleChanged(HslCurveHistory hslCurve, int scrollX, float scale, int offsetPaintScrollX);

		private int paintCount = 0;

		private List<AuxiliaryLable> auxiliary_Labels;

		private List<HslMarkLine> hslMarkLines;

		private List<HslMarkText> hslMarkTexts;

		private List<HslMarkImage> hslMarkImages;

		private Dictionary<string, HslCurveItem> data_dicts = null;

		private List<HslCurveItem> data_lists = null;

		private List<DateTime> data_times = null;

		private List<string> data_customer = null;

		private List<AuxiliaryLine> auxiliary_lines;

		private float data_ScaleX_Render = 1f;

		private float[] scale_x_options = new float[10] { 0.0625f, 0.125f, 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f, 32f };

		private int scale_x_index = 4;

		private float data_ScaleY_Render = 1f;

		private float[] scale_y_options = new float[6] { 1f, 2f, 4f, 8f, 16f, 32f };

		private int scale_y_index = 0;

		private int data_count = 0;

		private string data_time_formate = "yyyy-MM-dd HH:mm:ss";

		private string mouse_hover_time_formate = "HH:mm:ss";

		private Point mouse_scroll_location = new Point(-1, -1);

		private bool is_mouse_click_scroll = false;

		private bool is_mouse_on_picture = false;

		private Point mouse_location = new Point(-1, -1);

		private int m_RowBetweenStart = -1;

		private int m_RowBetweenEnd = -1;

		private int m_RowBetweenStartHeight = -1;

		private int m_RowBetweenHeight = -1;

		private bool m_IsMouseLeftDown = false;

		private bool m_IsMouseMiddleDown = false;

		private Point mouse_right_location = new Point(-1, -1);

		private List<HslMarkBackSection> markBackSections = null;

		private List<HslMarkForeSection> markForeSections = null;

		private List<HslMarkForeSection> markForeSectionsTmp = null;

		private List<HslMarkForeSection> markForeActiveSections = null;

		private Color backColor = Color.White;

		private Random random = null;

		private Color coordinateColor = Color.LightGray;

		private Brush coordinateBrush = new SolidBrush(Color.LightGray);

		private Pen coordinatePen = new Pen(Color.LightGray);

		private Color coordinateDashColor = Color.FromArgb(72, 72, 72);

		private Pen coordinateDashPen = null;

		private Color markLineColor = Color.Cyan;

		private Pen markLinePen = new Pen(Color.Cyan);

		private Color markTextColor = Color.Yellow;

		private Brush markTextBrush = new SolidBrush(Color.Yellow);

		private Color moveLineColor = Color.White;

		private Pen moveLinePen = new Pen(Color.White);

		private int data_tip_width = -1;

		private int curveNameWidth = 150;

		private int value_IntervalAbscissaText = 200;

		private Brush mouseHoverBackBrush = new SolidBrush(Color.FromArgb(220, Color.FromArgb(52, 52, 52)));

		private Color hoverBackColor = Color.FromArgb(52, 52, 52);

		private Color markBorderColor = Color.HotPink;

		private Pen markBorderPen = new Pen(Color.HotPink);

		private ReferenceAxis referenceAxisLeft;

		private ReferenceAxis referenceAxisRight;

		private bool isOtherAxisHide = false;

		private int leftWidth = 50;

		private int rightWidth = 50;

		private int mouseHoverTimeWidth = 100;

		private int topHeadHeight = 30;

		private int buttomHeight = 20;

		private int scrollHeight = 15;

		private int scrollX = 0;

		private int scrollMaxX = 0;

		private int scrollWidth = 10;

		private int offsetPaintScrollX = 0;

		private int offsetPaintScrollY = 0;

		private Rectangle scrollRectage = new Rectangle(0, 0, 0, 0);

		private Rectangle scrollRectageBack = new Rectangle(0, 0, 0, 0);

		private Color scrollColor = Color.DimGray;

		private ScaleMode scaleMode = ScaleMode.OnlyX;

		private bool renderInvertedTriangle = true;

		private bool renderScaleInfo = true;

		private int rightRemainWidth = 200;

		private bool markLineVisible = true;

		private int value_Segment = 5;

		private int pointsRadius = -1;

		private bool isShowTextInfomation = true;

		private bool isRederRightCoordinate = true;

		private bool isAllowSelectSection = true;

		private bool isRenderTimeData = false;

		private bool isMouseFreeze = false;

		private bool isAoordinateRoundInt = false;

		private HslCurveHistory syncHslCurveHistory;

		private ReferenceAxisCollection referenceAxes;

		private bool isRenderYTip = true;

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "46, 46, 46")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color BackColor
		{
			get
			{
				return base.BackColor;
			}
			set
			{
				backColor = value;
				base.BackColor = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("HslControls")]
		[Bindable(true)]
		[DefaultValue(typeof(Color), "Yellow")]
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
			}
		}

		[Browsable(true)]
		[Description("获取或设置曲线控件是否显示右侧的坐标轴")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool RenderRightCoordinate
		{
			get
			{
				return isRederRightCoordinate;
			}
			set
			{
				isRederRightCoordinate = value;
				Invalidate();
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
				isShowTextInfomation = true;
				base.Text = value;
				scrollX = 0;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("或者或设置所有的实线坐标轴的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public virtual Color CoordinateColor
		{
			get
			{
				return coordinateColor;
			}
			set
			{
				coordinateColor = value;
				coordinatePen.Dispose();
				coordinatePen = new Pen(coordinateColor);
				coordinateBrush.Dispose();
				coordinateBrush = new SolidBrush(coordinateColor);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("或者或设置所有的虚线坐标轴的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "72, 72, 72")]
		public virtual Color DashCoordinateColor
		{
			get
			{
				return coordinateDashColor;
			}
			set
			{
				coordinateDashColor = value;
				coordinateDashPen.Dispose();
				coordinateDashPen = new Pen(coordinateDashColor);
				coordinateDashPen.DashPattern = new float[2] { 5f, 5f };
				coordinateDashPen.DashStyle = DashStyle.Custom;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("或者或设置所有的区间标记的线条颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Cyan")]
		public virtual Color MarkLineColor
		{
			get
			{
				return markLineColor;
			}
			set
			{
				markLineColor = value;
				markLinePen.Dispose();
				markLinePen = new Pen(markLineColor);
			}
		}

		[Browsable(true)]
		[Description("或者或设置所有的区间标记的文本颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Cyan")]
		public virtual Color MarkTextColor
		{
			get
			{
				return markTextColor;
			}
			set
			{
				markTextColor = value;
				markTextBrush.Dispose();
				markTextBrush = new SolidBrush(markTextColor);
			}
		}

		[Browsable(true)]
		[Description("或者或设置光标移动时显示信息的边框颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "HotPink")]
		public virtual Color MarkBorderColor
		{
			get
			{
				return markBorderColor;
			}
			set
			{
				markBorderColor = value;
				markBorderPen?.Dispose();
				markBorderPen = new Pen(markBorderColor);
			}
		}

		[Browsable(true)]
		[Description("获取或设置鼠标移动的时候，显示的提示信息的背景颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "52, 52, 52")]
		public Color HoverBackColor
		{
			get
			{
				return hoverBackColor;
			}
			set
			{
				hoverBackColor = value;
				mouseHoverBackBrush?.Dispose();
				mouseHoverBackBrush = new SolidBrush(Color.FromArgb(220, hoverBackColor));
			}
		}

		[Browsable(true)]
		[Description("或者或设置鼠标移动过程中的提示线的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "White")]
		public virtual Color MoveLineColor
		{
			get
			{
				return moveLineColor;
			}
			set
			{
				moveLineColor = value;
				moveLinePen.Dispose();
				moveLinePen = new Pen(moveLineColor);
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前滚动条的颜色信息，默认为 Color.DimGray 颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public virtual Color ScrollColor
		{
			get
			{
				return scrollColor;
			}
			set
			{
				scrollColor = value;
				HslInvalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置鼠标移动过程中提示信息的宽度，如果是小于0表示自动宽度，如果是0表示不显示提示信息，如果大于0就表示绝对像素宽度")]
		[Category("HslControls")]
		[DefaultValue(-1)]
		public virtual int DataTipWidth
		{
			get
			{
				return data_tip_width;
			}
			set
			{
				data_tip_width = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置曲线名称的布局宽度，默认为150")]
		[Category("HslControls")]
		[DefaultValue(150)]
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
		[Description("获取或设置当曲线点击隐藏时，曲线标题的透明度信息，默认70")]
		[Category("HslControls")]
		[DefaultValue(70)]
		public virtual int CurveNameHideAlpha { get; set; } = 70;

		[Category("HslControls")]
		[Description("获取或设置纵向虚线的分隔情况，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(200)]
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
		[Description("获取或设置左坐标轴的宽度信息，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(50)]
		public virtual int LeftWidth
		{
			get
			{
				return leftWidth;
			}
			set
			{
				leftWidth = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置右坐标轴的宽度信息，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(50)]
		public virtual int RightWidth
		{
			get
			{
				return rightWidth;
			}
			set
			{
				rightWidth = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置顶部曲线名称部分的高度信息，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(30)]
		public virtual int TopCurveNameHeight
		{
			get
			{
				return topHeadHeight;
			}
			set
			{
				topHeadHeight = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置底部部分的高度信息，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(20)]
		public virtual int ButtomHeight
		{
			get
			{
				return buttomHeight;
			}
			set
			{
				buttomHeight = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置底部滚动条部分的高度信息，单位为多少个像素点")]
		[Browsable(true)]
		[DefaultValue(15)]
		public virtual int ScrollHeight
		{
			get
			{
				return scrollHeight;
			}
			set
			{
				scrollHeight = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置曲线图里所有的时间的格式，默认是 yyyy-MM-dd HH:mm:ss")]
		[Browsable(true)]
		[DefaultValue("yyyy-MM-dd HH:mm:ss")]
		public virtual string DateTimeFormate
		{
			get
			{
				return data_time_formate;
			}
			set
			{
				data_time_formate = value;
				HslInvalidate();
			}
		}

		[Category("HslControls")]
		[Description("获取或设置曲线图里光标移动时下方显示的时间格式，默认是HH:mm:ss")]
		[Browsable(true)]
		[DefaultValue("HH:mm:ss")]
		public virtual string HoverDateTimeFormate
		{
			get
			{
				return mouse_hover_time_formate;
			}
			set
			{
				mouse_hover_time_formate = value;
			}
		}

		[Category("HslControls")]
		[Description("获取或设置曲线图里光标移动时下方显示的矩形宽度信息，默认100")]
		[Browsable(true)]
		[DefaultValue(100)]
		public virtual int HoverDateTimeWidth
		{
			get
			{
				return mouseHoverTimeWidth;
			}
			set
			{
				mouseHoverTimeWidth = value;
			}
		}

		[Category("HslControls")]
		[Description("获取或设置曲线是否禁止鼠标选择区间的功能。")]
		[Browsable(true)]
		[DefaultValue(true)]
		public virtual bool IsAllowSelectSection
		{
			get
			{
				return isAllowSelectSection;
			}
			set
			{
				isAllowSelectSection = value;
			}
		}

		[Category("HslControls")]
		[Description("获取或设置纵轴是否强制使用整型。")]
		[Browsable(true)]
		[DefaultValue(false)]
		public virtual bool IsAoordinateRoundInt
		{
			get
			{
				return isAoordinateRoundInt;
			}
			set
			{
				isAoordinateRoundInt = value;
				HslInvalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置所有的数据点显示的半径大小，默认是-1，不显示数据点")]
		[Category("HslControls")]
		[DefaultValue(-1)]
		public virtual int PointsRadius
		{
			get
			{
				return pointsRadius;
			}
			set
			{
				pointsRadius = value;
				HslInvalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置鼠标移动的时候，是否显示鼠标的标记线，这个标记线跟随着光标移动")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool MarkLineVisible
		{
			get
			{
				return markLineVisible;
			}
			set
			{
				markLineVisible = value;
				HslInvalidate();
			}
		}

		[Description("获取或设置其他的坐标轴信息")]
		[Category("HslControls")]
		[TypeConverter(typeof(CollectionConverter))]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public virtual ReferenceAxisCollection ReferenceAxis
		{
			get
			{
				return referenceAxes;
			}
			set
			{
				referenceAxes = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置是否显示倒三角的信息，默认为True，如果显示不限制倒三角，则顶部会连线")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool RenderInvertedTriangle
		{
			get
			{
				return renderInvertedTriangle;
			}
			set
			{
				renderInvertedTriangle = value;
				HslInvalidate();
			}
		}

		[Description("获取或设置是否显示缩放倍率信息，默认为True，如果想要不显示右上角的倍率信息，则设置为False")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public virtual bool RenderScaleInfo
		{
			get
			{
				return renderScaleInfo;
			}
			set
			{
				renderScaleInfo = value;
				HslInvalidate();
			}
		}

		[Description("获取或设置当前曲线的缩放模式，默认只有X轴，还可以选择同时缩放，或是先缩放X轴后缩放Y轴")]
		[Category("HslControls")]
		[DefaultValue(typeof(ScaleMode), "OnlyX")]
		public virtual ScaleMode ScaleMode
		{
			get
			{
				return scaleMode;
			}
			set
			{
				scaleMode = value;
			}
		}

		[Description("获取或设置当前的曲线控件右侧的预留的空白宽度信息，像素为单位，默认200")]
		[Category("HslControls")]
		[DefaultValue(200)]
		public int RightRemainWidth
		{
			get
			{
				return rightRemainWidth;
			}
			set
			{
				rightRemainWidth = value;
				RenderCurveUI();
			}
		}

		[Description("获取或设置当前的曲线控件的额外的轴是否隐藏，额外的轴主要指在属性 ReferenceAxis 配置的第三轴，第四轴，等等")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool IsOtherAxisHide
		{
			get
			{
				return isOtherAxisHide;
			}
			set
			{
				isOtherAxisHide = value;
				Invalidate();
			}
		}

		[Description("获取或设置光标移动时，是否在每个Y轴上显示光标位置对应的值（这个值反计算时存在一定的微小误差），默认为 true，显示")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool IsRenderYTip
		{
			get
			{
				return isRenderYTip;
			}
			set
			{
				isRenderYTip = value;
				Invalidate();
			}
		}

		[Browsable(false)]
		public HslCurveRange[] CurveRanges { get; set; }

		[Category("Mouse")]
		[Description("当鼠标在曲线上双击时触发，由此获取到点击的数据的索引位置，时间坐标")]
		public event CurveDoubleClick onCurveDoubleClick;

		[Category("Mouse")]
		[Description("当鼠标在曲线上双击时触发，由此获取到点击的数据的索引位置，用户自定义的坐标")]
		public event CurveCustomerDoubleClick onCurveCustomerDoubleClick;

		[Category("Mouse")]
		[Description("当鼠标在曲线上双击时触发，由此获取到鼠标的移动位置")]
		public event CurveMouseMove onCurveMouseMove;

		[Category("Mouse")]
		[Description("当鼠标在曲线上选择了一个区间，由此出发了一个选择事件，注意，包含两侧的端点。")]
		public event CurveRangeSelect onCurveRangeSelect;

		public event CurveScollScaleChanged OnScaleChanged;

		public event CurveScollScaleChanged OnScrollChanged;

		public HslCurveHistory()
		{
			auxiliary_lines = new List<AuxiliaryLine>();
			data_dicts = new Dictionary<string, HslCurveItem>();
			data_lists = new List<HslCurveItem>();
			markBackSections = new List<HslMarkBackSection>();
			markForeSections = new List<HslMarkForeSection>();
			markForeSectionsTmp = new List<HslMarkForeSection>();
			markForeActiveSections = new List<HslMarkForeSection>();
			hslMarkTexts = new List<HslMarkText>();
			hslMarkImages = new List<HslMarkImage>();
			hslMarkLines = new List<HslMarkLine>();
			auxiliary_Labels = new List<AuxiliaryLable>();
			data_times = new List<DateTime>(0);
			data_customer = new List<string>();
			coordinateDashPen = new Pen(Color.FromArgb(72, 72, 72));
			coordinateDashPen.DashPattern = new float[2] { 5f, 5f };
			coordinateDashPen.DashStyle = DashStyle.Custom;
			random = new Random();
			referenceAxes = new ReferenceAxisCollection(this);
			referenceAxisLeft = new ReferenceAxis(this);
			referenceAxisRight = new ReferenceAxis(this);
			InitializeComponent();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			base.BackColor = Color.FromArgb(46, 46, 46);
			base.ForeColor = Color.Yellow;
		}

		private void HslInvalidate()
		{
			Invalidate();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			base.MouseMove += PictureBox3_MouseMove;
			base.MouseLeave += PictureBox3_MouseLeave;
			base.MouseEnter += PictureBox3_MouseEnter;
			base.MouseDown += PictureBox3_MouseDown;
			base.MouseUp += PictureBox3_MouseUp;
			base.Paint += PictureBox3_Paint;
			base.MouseDoubleClick += PictureBox3_MouseDoubleClick;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnKeyDown(e);
				return;
			}
			if (e.KeyCode == Keys.Left)
			{
				scrollX -= 5;
				if (scrollX < 0)
				{
					scrollX = 0;
				}
				HslInvalidate();
			}
			else if (e.KeyCode == Keys.Right)
			{
				scrollX += 5;
				if (scrollX > scrollMaxX)
				{
					scrollX = scrollMaxX;
				}
				HslInvalidate();
			}
			base.OnKeyDown(e);
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnMouseWheel(e);
				return;
			}
			if (ScaleMode != 0)
			{
				if (mouse_location.X < 0)
				{
					return;
				}
				int actualLeftWidth = GetActualLeftWidth();
				int num = mouse_location.X + offsetPaintScrollX;
				int num2 = mouse_location.Y - 20 + offsetPaintScrollY;
				if (scale_x_index >= scale_x_options.Length)
				{
					scale_x_index = scale_x_options.Length - 1;
				}
				if (scale_y_index >= scale_y_options.Length)
				{
					scale_y_index = scale_y_options.Length - 1;
				}
				if (e.Delta == 120)
				{
					if (scale_x_index < scale_x_options.Length - 1)
					{
						scale_x_index++;
						data_ScaleX_Render = scale_x_options[scale_x_index];
						if (ScaleMode == ScaleMode.Both && data_ScaleX_Render > 0.95f)
						{
							data_ScaleY_Render = data_ScaleX_Render;
							offsetPaintScrollY = EnsureOffsetPaintScrollY(Convert.ToInt32((double)num2 * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index - 1]) - (mouse_location.Y - 20));
						}
						int num3 = CalculatePaintWidthAndScrollMax(base.Width - actualLeftWidth - rightWidth);
						if (num3 - (base.Width - actualLeftWidth - rightWidth) == 0)
						{
							SetScrollXNewValue(0);
							offsetPaintScrollX = 0;
						}
						else
						{
							SetScrollXNewValue(Convert.ToInt32(((double)num * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index - 1] - (double)mouse_location.X) * 1.0 * (double)scrollMaxX * 1.0 / (double)(num3 - (base.Width - actualLeftWidth - rightWidth))));
							offsetPaintScrollX = Convert.ToInt32((double)num * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index - 1]) - mouse_location.X;
							offsetPaintScrollX = HslHelper.Middle(0, offsetPaintScrollX, num3 - (base.Width - actualLeftWidth - rightWidth));
						}
						HslInvalidate();
					}
					else if (ScaleMode == ScaleMode.XThenY && scale_y_index < scale_y_options.Length - 1)
					{
						scale_y_index++;
						data_ScaleY_Render = scale_y_options[scale_y_index];
						offsetPaintScrollY = EnsureOffsetPaintScrollY(Convert.ToInt32((double)num2 * 1.0 * (double)scale_y_options[scale_y_index] / (double)scale_y_options[scale_y_index - 1]) - (mouse_location.Y - 20));
						HslInvalidate();
					}
				}
				else if (e.Delta == -120)
				{
					if (ScaleMode == ScaleMode.XThenY && scale_y_index > 0)
					{
						scale_y_index--;
						data_ScaleY_Render = scale_y_options[scale_y_index];
						offsetPaintScrollY = EnsureOffsetPaintScrollY(Convert.ToInt32((double)num2 * 1.0 * (double)scale_y_options[scale_y_index] / (double)scale_y_options[scale_y_index + 1]) - (mouse_location.Y - 20));
						HslInvalidate();
					}
					else if (scale_x_index > 0)
					{
						scale_x_index--;
						data_ScaleX_Render = scale_x_options[scale_x_index];
						if (ScaleMode == ScaleMode.Both && data_ScaleX_Render > 0.95f)
						{
							data_ScaleY_Render = data_ScaleX_Render;
							offsetPaintScrollY = EnsureOffsetPaintScrollY(Convert.ToInt32((double)num2 * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index + 1]) - (mouse_location.Y - 20));
						}
						int num4 = CalculatePaintWidthAndScrollMax(base.Width - actualLeftWidth - rightWidth);
						if (num4 - (base.Width - actualLeftWidth - rightWidth) <= 0)
						{
							SetScrollXNewValue(0);
							offsetPaintScrollX = 0;
						}
						else
						{
							SetScrollXNewValue(Convert.ToInt32(((double)num * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index + 1] - (double)mouse_location.X) * 1.0 * (double)scrollMaxX * 1.0 / (double)(num4 - (base.Width - actualLeftWidth - rightWidth))));
							offsetPaintScrollX = Convert.ToInt32((double)num * 1.0 * (double)scale_x_options[scale_x_index] / (double)scale_x_options[scale_x_index + 1]) - mouse_location.X;
							offsetPaintScrollX = HslHelper.Middle(0, offsetPaintScrollX, num4 - (base.Width - actualLeftWidth - rightWidth));
						}
						HslInvalidate();
					}
				}
				this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
			}
			base.OnMouseWheel(e);
		}

		private int EnsureOffsetPaintScrollY(int offsetPaintScrollY)
		{
			offsetPaintScrollY = HslHelper.Middle(0, offsetPaintScrollY, (int)((float)(base.Height - topHeadHeight - buttomHeight - 40) * data_ScaleY_Render) - (base.Height - topHeadHeight - buttomHeight - 40));
			return offsetPaintScrollY;
		}

		public void SetScrollPosition(ScrollEventArgs e)
		{
			SetScrollXNewValue(e.NewValue);
			Refresh();
			this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
		}

		public void SetSyncHslCurveHistory(HslCurveHistory hslCurveHistory)
		{
			syncHslCurveHistory = hslCurveHistory;
			if (hslCurveHistory != null)
			{
				hslCurveHistory.OnScaleChanged += delegate(HslCurveHistory curve, int i, float j, int k)
				{
					data_ScaleX_Render = j;
					SetScrollXNewValue(i);
					offsetPaintScrollX = k;
					Refresh();
				};
				hslCurveHistory.onCurveMouseMove += delegate(HslCurveHistory curve, int x, int y)
				{
					SetCurveMousePosition(x, y);
				};
			}
		}

		public void ScrollToRight()
		{
			isShowTextInfomation = false;
			int actualLeftWidth = GetActualLeftWidth();
			CalculatePaintWidthAndScrollMax(base.Width - actualLeftWidth - rightWidth);
			SetScrollXNewValue(scrollMaxX);
			HslInvalidate();
			this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
		}

		public void ScrollToIndex(int index)
		{
			int actualLeftWidth = GetActualLeftWidth();
			CalculatePaintWidthAndScrollMax(base.Width - actualLeftWidth - rightWidth);
			int curvePaintActualWidth = GetCurvePaintActualWidth();
			int num = CalculatePaintWidthAndScrollMax(curvePaintActualWidth);
			if (num > curvePaintActualWidth)
			{
				offsetPaintScrollX = (int)((float)index * data_ScaleX_Render);
				SetScrollXNewValue(Convert.ToInt32((float)((long)offsetPaintScrollX * (long)scrollMaxX) * 1f / (float)(num - curvePaintActualWidth)));
				offsetPaintScrollX = (int)((float)index * data_ScaleX_Render);
				HslInvalidate();
				this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
			}
		}

		public void ScrollToDateTime(DateTime dateTime)
		{
			int num = FindIndexByDateTime(dateTime);
			if (num >= 0)
			{
				ScrollToIndex(num);
			}
		}

		public void RenderCurveUI(int start, int end)
		{
			if (start < end && end >= 0)
			{
				int curvePaintActualWidth = GetCurvePaintActualWidth();
				if (end >= data_count)
				{
					end = data_count - 1;
				}
				if (start < 0)
				{
					start = 0;
				}
				data_ScaleX_Render = Convert.ToSingle((double)curvePaintActualWidth * 1.0 / (double)(end - start));
				ScrollToIndex(start);
			}
		}

		public void RenderCurveUI(DateTime start, DateTime end)
		{
			int num = FindIndexByDateTime(start);
			if (num < 0)
			{
				num = 0;
			}
			RenderCurveUI(num, FindIndexByDateTime(end, num));
		}

		private int FindIndexByDateTime(DateTime dateTime, int startIndex = 0)
		{
			if (data_times == null)
			{
				return -1;
			}
			if (data_times.Count == 0)
			{
				return -1;
			}
			for (int i = startIndex; i < data_times.Count; i += 100)
			{
				int num = i + 100;
				if (num >= data_times.Count)
				{
					num = data_times.Count - 1;
				}
				if (dateTime < data_times[i])
				{
					return i;
				}
				if (!(dateTime >= data_times[i]) || !(dateTime < data_times[num]))
				{
					continue;
				}
				for (int j = i; j < num - 1; j++)
				{
					if (dateTime >= data_times[j] && dateTime < data_times[j + 1])
					{
						return j;
					}
				}
				return -1;
			}
			return -1;
		}

		private int GetAcutalSegment(float m_data_ScaleY_Render)
		{
			return value_Segment * ((m_data_ScaleY_Render < 3.95f) ? 1 : ((int)(m_data_ScaleY_Render / 4f) * 2));
		}

		private void DrawCoordinate(Graphics g, bool isLeft, float dx, float dy, ReferenceAxis referenceAxis, bool needPaintDash)
		{
			StringFormat format = (isLeft ? HslHelper.StringFormatRight : HslHelper.StringFormatLeft);
			float calculateMax = referenceAxis.GetCalculateMax();
			float calculateMin = referenceAxis.GetCalculateMin();
			string unit = referenceAxis.Unit;
			Brush brush = referenceAxis.Brush;
			Pen pen = new Pen(referenceAxis.Brush);
			g.TranslateTransform(dx, dy);
			float num = (isShowTextInfomation ? 1f : data_ScaleY_Render);
			float num2 = base.Height - topHeadHeight - buttomHeight;
			float num3 = (isLeft ? leftWidth : rightWidth);
			int num4 = ((!isShowTextInfomation) ? offsetPaintScrollY : 0);
			if (isLeft)
			{
				g.DrawLine(pen, num3 - 1f, 5f, num3 - 1f, num2 - 15f);
			}
			else
			{
				g.DrawLine(pen, 0f, 5f, 0f, num2 - 15f);
			}
			if (RenderInvertedTriangle)
			{
				if (isLeft)
				{
					HslHelper.PaintTriangle(g, brush, new PointF(num3 - 1f, 10f), 5, GraphDirection.Upward);
				}
				else
				{
					HslHelper.PaintTriangle(g, brush, new PointF(0f, 10f), 5, GraphDirection.Upward);
				}
			}
			g.DrawString(unit, Font, brush, new RectangleF(0f, -15f, num3 - 5f, 30f), HslHelper.StringFormatRight);
			g.TranslateTransform(0f, -num4 + 20);
			int acutalSegment = GetAcutalSegment(num);
			for (int i = 0; i <= acutalSegment; i++)
			{
				float num5 = (float)((double)i * (double)(calculateMax - calculateMin) / (double)acutalSegment + (double)calculateMin);
				if (IsAoordinateRoundInt)
				{
					num5 = (float)Math.Round(num5, 0);
				}
				float num6 = HslHelper.ComputePaintLocationY(calculateMax, calculateMin, (num2 - 40f) * num, num5);
				if (referenceAxis.IsUseLogarithms())
				{
					num5 = (float)Math.Pow(10.0, num5);
				}
				if ((needPaintDash || IsNeedPaintDash(num6)) && !(num6 - (float)num4 + 20f < 0f) && !(num6 - (float)num4 + 20f > num2 - 20f))
				{
					if (isLeft)
					{
						g.DrawLine(pen, num3 - 5f, num6, num3 - 2f, num6);
						g.DrawString(layoutRectangle: new RectangleF(0f, num6 - 9f, num3 - 4f, 20f), s: HslHelper.GetFormatString(referenceAxis.Format, num5), font: Font, brush: brush, format: format);
					}
					else
					{
						g.DrawLine(pen, 1f, num6, 5f, num6);
						g.DrawString(layoutRectangle: new RectangleF(5f, num6 - 9f, num3 - 3f, 20f), s: HslHelper.GetFormatString(referenceAxis.Format, num5), font: Font, brush: brush, format: format);
					}
				}
			}
			if (!needPaintDash)
			{
				for (int j = 0; j < auxiliary_lines.Count; j++)
				{
					if (!auxiliary_lines[j].IsLeftFrame)
					{
						continue;
					}
					float num7 = HslHelper.ComputePaintLocationY(calculateMax, calculateMin, (num2 - 40f) * num, referenceAxis.IsUseLogarithms() ? ((float)Math.Log10(auxiliary_lines[j].Value)) : auxiliary_lines[j].Value);
					if (!(num7 - (float)num4 + 20f < 0f) && !(num7 - (float)num4 + 20f > num2 - 20f))
					{
						if (isLeft)
						{
							g.DrawLine(pen, num3 - 5f, num7, num3 - 2f, num7);
							g.DrawString(layoutRectangle: new RectangleF(0f, num7 - 9f, num3 - 4f, 20f), s: HslHelper.GetFormatString(referenceAxis.Format, auxiliary_lines[j].Value), font: Font, brush: brush, format: format);
						}
						else
						{
							g.DrawLine(pen, 1f, num7, 5f, num7);
							g.DrawString(layoutRectangle: new RectangleF(6f, num7 - 9f, num3 - 3f, 20f), s: HslHelper.GetFormatString(referenceAxis.Format, auxiliary_lines[j].Value), font: Font, brush: brush, format: format);
						}
					}
				}
			}
			if (isRenderYTip && is_mouse_on_picture && !isShowTextInfomation && markLineVisible)
			{
				RectangleF rectangleF = new RectangleF(1f, mouse_location.Y - 9 + num4 - 20, num3 - 3f, 20f);
				g.FillRectangle(mouseHoverBackBrush, rectangleF);
				g.DrawRectangles(markBorderPen, new RectangleF[1] { rectangleF });
				rectangleF = ((!isLeft) ? new RectangleF(5f, mouse_location.Y - 9 + num4 - 20, num3 - 6f, 20f) : new RectangleF(0f, mouse_location.Y - 9 + num4 - 20, num3 - 4f, 20f));
				float num8 = HslHelper.ComputeValueFromPaintLocationY(calculateMax, calculateMin, (num2 - 40f) * num, mouse_location.Y + num4 - 20);
				if (referenceAxis.IsUseLogarithms())
				{
					num8 = (float)Math.Pow(10.0, num8);
				}
				g.DrawString((referenceAxis.Format == "{0}") ? num8.ToString("F2") : HslHelper.GetFormatString(referenceAxis.Format, num8), Font, brush, rectangleF, format);
			}
			g.TranslateTransform(0f, num4 - 20);
			g.TranslateTransform(0f - dx, 0f - dy);
			pen.Dispose();
		}

		private void PaintFromString(Graphics g, string text)
		{
			int actualLeftWidth = GetActualLeftWidth();
			int num = base.Width - actualLeftWidth - rightWidth;
			int num2 = base.Height - topHeadHeight - buttomHeight;
			Font font = new Font(Font.FontFamily, 18f);
			if (text != null && text.Length > 400)
			{
				font = new Font(Font.FontFamily, 12f);
			}
			int num3 = num2 - 40;
			g.DrawLine(coordinatePen, 0, num2 - 20, num - 1, num2 - 20);
			for (int i = 1; i <= value_Segment; i++)
			{
				float num4 = HslHelper.ComputePaintLocationY(value: (float)((double)i * (double)(referenceAxisLeft.GetCalculateMax() - referenceAxisLeft.GetCalculateMin()) / (double)value_Segment + (double)referenceAxisLeft.GetCalculateMin()), max: referenceAxisLeft.GetCalculateMax(), min: referenceAxisLeft.GetCalculateMin(), height: num3) + 20f;
				if (IsNeedPaintDash(num4))
				{
					g.DrawLine(coordinateDashPen, 0f, num4, num - 1, num4);
				}
			}
			for (int j = 0; j < auxiliary_lines.Count; j++)
			{
				if (!(auxiliary_lines[j].PaintValue - (float)offsetPaintScrollY + 20f < 0f) && !(auxiliary_lines[j].PaintValue - (float)offsetPaintScrollY + 20f > (float)(num2 - 20)))
				{
					g.DrawLine(auxiliary_lines[j].GetPen(), 0f, auxiliary_lines[j].PaintValue, num - 1, auxiliary_lines[j].PaintValue);
				}
			}
			for (int k = value_IntervalAbscissaText; k < num; k += value_IntervalAbscissaText)
			{
				g.DrawLine(coordinateDashPen, k, num2 - 20, k, 0);
			}
			Rectangle rectangle = new Rectangle(0, 0, num, num2);
			using (Brush brush = new SolidBrush(ForeColor))
			{
				g.DrawString(text, font, brush, rectangle, HslHelper.StringFormatCenter);
			}
			font.Dispose();
		}

		private bool IsNeedPaintDash(float paintValue)
		{
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				if (Math.Abs(auxiliary_lines[i].PaintValue - paintValue) < (float)Font.Height)
				{
					return false;
				}
			}
			return true;
		}

		private int GetActualLeftWidth()
		{
			if (isOtherAxisHide)
			{
				return leftWidth;
			}
			return leftWidth + leftWidth * referenceAxes.Count;
		}

		private int GetCurvePaintActualWidth()
		{
			return base.Width - GetActualLeftWidth() - rightWidth;
		}

		private int GetCurvePaintActualWidth(int width)
		{
			return width - GetActualLeftWidth() - rightWidth;
		}

		private int GetCurvePaintActualHeight(int height)
		{
			return height - topHeadHeight - buttomHeight;
		}

		private bool IsPaintxInRenderRegion(int x, int width, int offset = 10)
		{
			if (data_ScaleX_Render >= 1f)
			{
				return (float)x >= (float)offsetPaintScrollX - (float)offset * data_ScaleX_Render && (float)x <= (float)(offsetPaintScrollX + width) + (float)offset * data_ScaleX_Render;
			}
			return x >= offsetPaintScrollX - offset && x <= offsetPaintScrollX + width + offset;
		}

		private bool IsRectangleInRenderRegion(Rectangle rectangle, int width)
		{
			if (IsPaintxInRenderRegion(rectangle.X, width) || IsPaintxInRenderRegion(rectangle.X + rectangle.Width, width))
			{
				return true;
			}
			if (rectangle.X <= offsetPaintScrollX && rectangle.X + rectangle.Width >= offsetPaintScrollX + width)
			{
				return true;
			}
			return false;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				if (data_ScaleX_Render < 0.1f)
				{
					g.SmoothingMode = SmoothingMode.HighSpeed;
				}
				else
				{
					g.SmoothingMode = SmoothingMode.AntiAlias;
				}
				g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				PaintMain(g, width, height);
			}
		}

		public void PaintMain(Graphics g, int width, int height)
		{			
			if (width < 10)
			{
				width = 10;
			}
			if (height < 10)
			{
				height = 10;
			}
			DateTime now = DateTime.Now;
			paintCount++;
			int actualLeftWidth = GetActualLeftWidth();
			int curvePaintActualWidth = GetCurvePaintActualWidth(width);
			int curvePaintActualHeight = GetCurvePaintActualHeight(height);
			g.TranslateTransform(actualLeftWidth, topHeadHeight);
			if (isShowTextInfomation)
			{
				CalculateAuxiliaryPaintY(20);
				PaintFromString(g, Text);
			}
			else
			{
				g.SetClip(new Rectangle(0, 0, curvePaintActualWidth, height - topHeadHeight));
				GetRenderCurveMain(g, curvePaintActualWidth, curvePaintActualHeight);
				g.ResetClip();
			}
			g.TranslateTransform(-actualLeftWidth, -topHeadHeight);
			if (isShowTextInfomation)
			{
				CalculateAuxiliaryPaintY(0);
			}
			DrawCoordinate(g, true, (!isOtherAxisHide) ? (leftWidth * referenceAxes.Count) : 0, topHeadHeight, referenceAxisLeft, false);
			if (isRederRightCoordinate)
			{
				DrawCoordinate(g, false, width - rightWidth, topHeadHeight, referenceAxisRight, false);
			}
			if (!isOtherAxisHide)
			{
				for (int i = 0; i < referenceAxes.Count; i++)
				{
					DrawCoordinate(g, true, leftWidth * i, topHeadHeight, referenceAxes[i], true);
				}
			}
			if (!RenderInvertedTriangle)
			{
				g.DrawLine(coordinatePen, leftWidth + leftWidth * referenceAxes.Count - 1, topHeadHeight + 5, width - rightWidth, topHeadHeight + 5);
			}
			PaintHeadText(g, actualLeftWidth);
			if (ReferenceAxis.Count > 0)
			{
				g.DrawString(layoutRectangle: new Rectangle(GetActualLeftWidth() - leftWidth, base.Height - scrollHeight - 5, leftWidth, scrollHeight), s: isOtherAxisHide ? "<<" : ">>", font: Font, brush: coordinateBrush, format: HslHelper.StringFormatCenter);
			}
		}

		private void PaintHeadText(Graphics g, int actualLeftWidth)
		{
			if (topHeadHeight <= 0)
			{
				return;
			}
			float num = actualLeftWidth + 1;
			float num2 = 11f;
			foreach (KeyValuePair<string, HslCurveItem> data_dict in data_dicts)
			{
				if (data_dict.Value.Visible && data_dict.Value.Style != CurveStyle.PointMark)
				{
					Pen pen = (data_dict.Value.LineRenderVisiable ? new Pen(data_dict.Value.LineColor) : new Pen(Color.FromArgb(CurveNameHideAlpha, data_dict.Value.LineColor)));
					g.DrawLine(pen, num, num2, num + 30f, num2);
					g.DrawEllipse(pen, num + 8f, num2 - 7f, 14f, 14f);
					pen.Dispose();
					SolidBrush solidBrush = (data_dict.Value.LineRenderVisiable ? new SolidBrush(data_dict.Value.LineColor) : new SolidBrush(Color.FromArgb(CurveNameHideAlpha, data_dict.Value.LineColor)));
					g.DrawString(data_dict.Key, Font, solidBrush, new RectangleF(num + 35f, num2 - 10f, curveNameWidth - 35, 20f), HslHelper.StringFormatLeft);
					data_dict.Value.TitleRegion = new RectangleF(num, num2 - 10f, curveNameWidth, 20f);
					solidBrush.Dispose();
					num += (float)curveNameWidth;
					if (num >= (float)(base.Width - rightWidth - 30))
					{
						num = actualLeftWidth + 1;
						num2 += 22f;
					}
				}
			}
			for (int i = 0; i < auxiliary_Labels.Count; i++)
			{
				if (!string.IsNullOrEmpty(auxiliary_Labels[i].Text))
				{
					int num3 = ((auxiliary_Labels[i].LocationX > 1f) ? ((int)auxiliary_Labels[i].LocationX) : ((int)(auxiliary_Labels[i].LocationX * (float)base.Width)));
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
					g.DrawString(auxiliary_Labels[i].Text, Font, auxiliary_Labels[i].TextBrush, new Rectangle(num3 + 7, 0, num4 + 3, 20), HslHelper.StringFormatCenter);
				}
			}
			if (!renderScaleInfo)
			{
				return;
			}
			using (Brush brush = new SolidBrush(scrollColor))
			{
				g.DrawString($"Scale:X{data_ScaleX_Render} Y{data_ScaleY_Render}", Font, brush, new RectangleF(base.Width - 205, 0f, 200f, Font.Height + 2), HslHelper.StringFormatRight);
			}
		}

		private string GetScale()
		{
			if (data_ScaleX_Render > 0.8f)
			{
				return data_ScaleX_Render.ToString("F0");
			}
			if (data_ScaleX_Render > 0.3f)
			{
				return data_ScaleX_Render.ToString("F1");
			}
			return data_ScaleX_Render.ToString();
		}

		public void RenderCurveUI(bool stretchToFull = false)
		{
			isShowTextInfomation = false;
			if (stretchToFull)
			{
				if (data_count > 0)
				{
					RenderCurveUI(0, data_count - 1);
				}
			}
			else
			{
				HslInvalidate();
			}
		}

		private int CalculatePaintWidthAndScrollMax(int width)
		{
			int num = Math.Max(Convert.ToInt32((float)data_count * data_ScaleX_Render) + rightRemainWidth, width);
			scrollWidth = (int)((long)width * (long)width / num);
			if (scrollWidth < 10)
			{
				scrollWidth = 10;
			}
			scrollMaxX = width - scrollWidth;
			return num;
		}

		private void SetScrollXNewValue(int scrollValue)
		{
			int curvePaintActualWidth = GetCurvePaintActualWidth();
			int num = CalculatePaintWidthAndScrollMax(curvePaintActualWidth);
			if (scrollValue > scrollMaxX)
			{
				scrollValue = scrollMaxX;
			}
			if (scrollValue < 0)
			{
				scrollValue = 0;
			}
			if (scrollX != scrollValue)
			{
				scrollX = scrollValue;
			}
			if (num > curvePaintActualWidth && scrollMaxX > 0)
			{
				offsetPaintScrollX = Convert.ToInt32((double)((long)(num - curvePaintActualWidth) * (long)scrollX) * 1.0 / (double)scrollMaxX);
			}
			else
			{
				offsetPaintScrollX = 0;
			}
		}

		private ReferenceAxis GetReferenceAxisByIndex(int index)
		{
			switch (index)
			{
			case 0:
				return new ReferenceAxis(referenceAxisLeft.Max, referenceAxisLeft.Min, referenceAxisLeft.UseLogarithms);
			case 1:
				return new ReferenceAxis(referenceAxisRight.Max, referenceAxisRight.Min, referenceAxisRight.UseLogarithms);
			default:
				if (index - 2 < referenceAxes.Count)
				{
					return referenceAxes[index - 2];
				}
				return null;
			}
		}

		private float ComputePaintLocationY(ReferenceAxis reference, int m_height, float value)
		{
			return HslHelper.ComputePaintLocationY(reference.GetCalculateMax(), reference.GetCalculateMin(), (float)(m_height - 40) * data_ScaleY_Render, reference.IsUseLogarithms() ? ((float)Math.Log10(value)) : value);
		}

		private void GetRenderCurveMain(Graphics g, int width, int height)
		{
			int num = CalculatePaintWidthAndScrollMax(width);
			if (num > width)
			{
				g.TranslateTransform(-offsetPaintScrollX, 0f);
			}
			for (int i = 0; i < markBackSections.Count; i++)
			{
				Rectangle rectangle = new Rectangle(Convert.ToInt32((float)markBackSections[i].StartIndex * data_ScaleX_Render), 0, Convert.ToInt32((float)(markBackSections[i].EndIndex - markBackSections[i].StartIndex) * data_ScaleX_Render), height - 20);
				if (IsRectangleInRenderRegion(rectangle, width))
				{
					using (Brush brush = new SolidBrush(markBackSections[i].BackColor))
					{
						g.FillRectangle(brush, rectangle);
					}
					g.DrawRectangle(Pens.DimGray, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
					string text = markBackSections[i].MarkText ?? string.Empty;
					if (markBackSections[i].StartIndex < data_times.Count && markBackSections[i].EndIndex < data_times.Count)
					{
						text = text + " (" + (data_times[markBackSections[i].EndIndex] - data_times[markBackSections[i].StartIndex]).TotalMinutes.ToString("F1") + " 分钟)";
					}
					g.DrawString(text, Font, Brushes.DimGray, new RectangleF(rectangle.X, 3f, rectangle.Width, height - 20), HslHelper.StringFormatTopCenter);
				}
			}
			g.TranslateTransform(0f, -offsetPaintScrollY + 20);
			foreach (HslMarkImage hslMarkImage in hslMarkImages)
			{
				if (hslMarkImage.MarkImage != null)
				{
					Point point = default(Point);
					point.X = Convert.ToInt32((double)hslMarkImage.Index * 1.0 * (double)data_ScaleX_Render);
					point.Y = Convert.ToInt32((hslMarkImage.OffsetY < 1f) ? (hslMarkImage.OffsetY * (float)(height - 40) * data_ScaleY_Render) : (hslMarkImage.OffsetY * data_ScaleY_Render));
					int num2 = (hslMarkImage.ScaleEnable ? Convert.ToInt32((float)hslMarkImage.MarkImage.Width * data_ScaleX_Render) : hslMarkImage.MarkImage.Width);
					int num3 = (hslMarkImage.ScaleEnable ? Convert.ToInt32((float)hslMarkImage.MarkImage.Height * data_ScaleY_Render) : hslMarkImage.MarkImage.Height);
					Rectangle rectangle2 = default(Rectangle);
					if (hslMarkImage.ReferencePoint == ContentAlignment.TopLeft)
					{
						rectangle2 = new Rectangle(point.X, point.Y, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.TopCenter)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width / 2, point.Y, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.TopRight)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width, point.Y, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.MiddleLeft)
					{
						rectangle2 = new Rectangle(point.X, point.Y - hslMarkImage.MarkImage.Height / 2, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.MiddleCenter)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width / 2, point.Y - hslMarkImage.MarkImage.Height / 2, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.MiddleRight)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width, point.Y - hslMarkImage.MarkImage.Height / 2, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.BottomLeft)
					{
						rectangle2 = new Rectangle(point.X, point.Y - hslMarkImage.MarkImage.Height, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.BottomCenter)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width / 2, point.Y - hslMarkImage.MarkImage.Height, num2, num3);
					}
					else if (hslMarkImage.ReferencePoint == ContentAlignment.BottomRight)
					{
						rectangle2 = new Rectangle(point.X - hslMarkImage.MarkImage.Width, point.Y - hslMarkImage.MarkImage.Height, num2, num3);
					}
					if (IsRectangleInRenderRegion(rectangle2, width))
					{
						g.DrawImage(hslMarkImage.MarkImage, rectangle2);
					}
				}
			}
			g.TranslateTransform(0f, offsetPaintScrollY - 20);
			if (num > width)
			{
				g.TranslateTransform(offsetPaintScrollX, 0f);
			}
			g.DrawLine(coordinatePen, 0, height - 20, width, height - 20);
			g.TranslateTransform(0f, -offsetPaintScrollY + 20);
			for (int j = 0; j < auxiliary_lines.Count; j++)
			{
				if (auxiliary_lines[j].IsLeftFrame)
				{
					auxiliary_lines[j].PaintValue = ComputePaintLocationY(referenceAxisLeft, height, auxiliary_lines[j].Value);
				}
				else
				{
					auxiliary_lines[j].PaintValue = ComputePaintLocationY(referenceAxisRight, height, auxiliary_lines[j].Value);
				}
			}
			int acutalSegment = GetAcutalSegment(isShowTextInfomation ? 1f : data_ScaleY_Render);
			int num4 = ((!isShowTextInfomation) ? offsetPaintScrollY : 0);
			for (int k = 1; k <= acutalSegment; k++)
			{
				float num5 = (float)((double)k * (double)(referenceAxisLeft.GetCalculateMax() - referenceAxisLeft.GetCalculateMin()) / (double)acutalSegment + (double)referenceAxisLeft.GetCalculateMin());
				if (IsAoordinateRoundInt)
				{
					num5 = (float)Math.Round(num5, 0);
				}
				if (referenceAxisLeft.IsUseLogarithms())
				{
					num5 = (float)Math.Pow(10.0, num5);
				}
				float num6 = ComputePaintLocationY(referenceAxisLeft, height, num5);
				if (IsNeedPaintDash(num6) && !(num6 - (float)num4 + 20f < 0f) && !(num6 - (float)num4 + 20f > (float)(height - 20)))
				{
					g.DrawLine(coordinateDashPen, 0f, num6, width, num6);
				}
			}
			for (int l = 0; l < auxiliary_lines.Count; l++)
			{
				if (!(auxiliary_lines[l].PaintValue - (float)offsetPaintScrollY + 20f < 0f) && !(auxiliary_lines[l].PaintValue - (float)offsetPaintScrollY + 20f > (float)(height - 20)))
				{
					g.DrawLine(auxiliary_lines[l].GetPen(), 0f, auxiliary_lines[l].PaintValue, width, auxiliary_lines[l].PaintValue);
				}
			}
			g.TranslateTransform(0f, offsetPaintScrollY - 20);
			if (num > width)
			{
				g.TranslateTransform(-offsetPaintScrollX, 0f);
			}
			for (int m = value_IntervalAbscissaText; m < num; m += value_IntervalAbscissaText)
			{
				if (num > width && (m < offsetPaintScrollX - 10 || m > offsetPaintScrollX + width + 10))
				{
					continue;
				}
				int num7 = Convert.ToInt32((float)m / data_ScaleX_Render);
				g.DrawLine(coordinateDashPen, m, height - 18, m, (!RenderInvertedTriangle) ? 5 : 0);
				Rectangle rectangle3 = new Rectangle(m - 100, height - 18, 200, 17);
				if (buttomHeight > 20)
				{
					rectangle3.Height += buttomHeight - 20;
				}
				if (isRenderTimeData)
				{
					if (num7 < data_times.Count)
					{
						g.DrawString(data_times[num7].ToString(data_time_formate, CultureInfo.InvariantCulture), Font, coordinateBrush, rectangle3, HslHelper.StringFormatCenter);
					}
				}
				else if (num7 < data_customer.Count)
				{
					g.DrawString(data_customer[num7], Font, coordinateBrush, rectangle3, HslHelper.StringFormatCenter);
				}
			}
			g.TranslateTransform(0f, -offsetPaintScrollY + 20);
			foreach (HslMarkText hslMarkText in hslMarkTexts)
			{
				if (hslMarkText.CurveItem == null)
				{
					foreach (KeyValuePair<string, HslCurveItem> data_dict in data_dicts)
					{
						if (!data_dict.Value.Visible || !data_dict.Value.LineRenderVisiable || data_dict.Key != hslMarkText.CurveKey)
						{
							continue;
						}
						hslMarkText.CurveItem = data_dict.Value;
						break;
					}
				}
				if (hslMarkText.CurveItem == null)
				{
					continue;
				}
				HslCurveItem curveItem = hslMarkText.CurveItem;
				float[] data = curveItem.Data;
				if (data == null || data.Length <= 1 || hslMarkText.Index < 0 || hslMarkText.Index >= curveItem.Data.Length || float.IsNaN(curveItem.Data[hslMarkText.Index]))
				{
					continue;
				}
				PointF center = new PointF((float)hslMarkText.Index * data_ScaleX_Render, ComputePaintLocationY(GetReferenceAxisByIndex(curveItem.ReferenceAxisIndex), height, curveItem.Data[hslMarkText.Index]));
				MarkTextPositionStyle markTextPosition = ((hslMarkText.PositionStyle == MarkTextPositionStyle.Auto) ? HslMarkText.CalculateDirectionFromDataIndex(curveItem.Data, hslMarkText.Index) : hslMarkText.PositionStyle);
				if (IsPaintxInRenderRegion((int)center.X, width))
				{
					if ((int)center.X <= offsetPaintScrollX + 10)
					{
						markTextPosition = MarkTextPositionStyle.Right;
					}
					HslCurveHelper.DrawHslMarkTextPoint(g, hslMarkText, center, Font, markTextPosition);
				}
			}
			foreach (HslMarkLine hslMarkLine in hslMarkLines)
			{
				PointF[] points = hslMarkLine.Points;
				if (points == null || points.Length <= 1)
				{
					continue;
				}
				PointF[] array = new PointF[hslMarkLine.Points.Length];
				for (int n = 0; n < hslMarkLine.Points.Length; n++)
				{
					array[n].X = hslMarkLine.Points[n].X * data_ScaleX_Render;
					array[n].Y = ComputePaintLocationY(hslMarkLine.IsLeftFrame ? referenceAxisLeft : referenceAxisRight, height, hslMarkLine.Points[n].Y);
					MarkTextPositionStyle markTextPosition2 = HslMarkText.CalculateDirectionFromDataIndex(hslMarkLine.Points, n);
					g.FillEllipse(hslMarkLine.CircleBrush, new RectangleF(array[n].X - 3f, array[n].Y - 3f, 6f, 6f));
					if (hslMarkLine.Marks != null)
					{
						HslCurveHelper.DrawTextByPoint(g, hslMarkLine.Marks[n], array[n], Font, hslMarkLine.TextBrush, markTextPosition2, 5);
					}
				}
				if (hslMarkLine.IsLineClosed)
				{
					g.DrawLines(hslMarkLine.LinePen, array);
					g.DrawLine(hslMarkLine.LinePen, array[0], array[array.Length - 1]);
				}
				else
				{
					g.DrawLines(hslMarkLine.LinePen, array);
				}
			}
			int num8 = 0;
			if (num > width)
			{
				if (offsetPaintScrollX > 20)
				{
					num8 = Convert.ToInt32((double)offsetPaintScrollX * 1.0 / (double)data_ScaleX_Render);
				}
				num8 = ((num8 > 10) ? (num8 - 10) : 0);
			}
			HslCurveRange[] curveRanges = CurveRanges;
			if (curveRanges != null && curveRanges.Length != 0)
			{
				for (int num9 = 0; num9 < CurveRanges.Length; num9++)
				{
					HslCurveRange hslCurveRange = CurveRanges[num9];
					GraphicsPath graphicsPath = new GraphicsPath();
					List<PointF> list = new List<PointF>(Convert.ToInt32((float)width / data_ScaleX_Render) + 10);
					List<PointF> list2 = new List<PointF>(Convert.ToInt32((float)width / data_ScaleX_Render) + 10);
					for (int num10 = num8; num10 < hslCurveRange.Upper.Length; num10++)
					{
						PointF item = default(PointF);
						item.X = (float)num10 * data_ScaleX_Render;
						item.Y = ComputePaintLocationY(GetReferenceAxisByIndex(hslCurveRange.ReferenceAxisIndex), height, hslCurveRange.Upper[num10]);
						PointF item2 = default(PointF);
						if (hslCurveRange.Lower != null)
						{
							item2.X = (float)num10 * data_ScaleX_Render;
							item2.Y = ComputePaintLocationY(GetReferenceAxisByIndex(hslCurveRange.ReferenceAxisIndex), height, hslCurveRange.Lower[num10]);
						}
						else
						{
							item2.X = (float)num10 * data_ScaleX_Render;
							item2.Y = ComputePaintLocationY(GetReferenceAxisByIndex(hslCurveRange.ReferenceAxisIndex), height, GetReferenceAxisByIndex(hslCurveRange.ReferenceAxisIndex).Min);
						}
						if (num <= width)
						{
							list.Add(item);
							list2.Add(item2);
						}
						else if (item.X >= (float)offsetPaintScrollX - 10f * data_ScaleX_Render)
						{
							if (!(item.X < (float)(offsetPaintScrollX + width) + 10f * data_ScaleX_Render))
							{
								break;
							}
							list.Add(item);
							list2.Add(item2);
						}
					}
					list2.Reverse();
					if (hslCurveRange.Style == CurveStyle.LineSegment)
					{
						graphicsPath.AddLines(list.ToArray());
					}
					else
					{
						graphicsPath.AddCurve(list.ToArray());
					}
					graphicsPath.AddLine(list[list.Count - 1], list2[0]);
					if (hslCurveRange.Style == CurveStyle.LineSegment)
					{
						graphicsPath.AddLines(list2.ToArray());
					}
					else
					{
						graphicsPath.AddCurve(list2.ToArray());
					}
					graphicsPath.AddLine(list2[list2.Count - 1], list[0]);
					using (Brush brush2 = new SolidBrush(Color.FromArgb(48, hslCurveRange.LineColor)))
					{
						g.FillPath(brush2, graphicsPath);
					}
					using (Pen pen = new Pen(hslCurveRange.LineColor))
					{
						g.DrawPath(pen, graphicsPath);
					}
				}
			}
			foreach (HslCurveItem value in data_dicts.Values)
			{
				if (!value.Visible || !value.LineRenderVisiable)
				{
					continue;
				}
				float[] data2 = value.Data;
				if (data2 == null || data2.Length <= 1)
				{
					continue;
				}
				List<PointF> list3 = new List<PointF>(Convert.ToInt32((float)((width < 0) ? 10 : width) / data_ScaleX_Render) + 10);
				for (int num11 = num8; num11 < value.Data.Length; num11++)
				{
					if (!float.IsNaN(value.Data[num11]))
					{
						PointF item3 = default(PointF);
						item3.X = (float)num11 * data_ScaleX_Render;
						item3.Y = ComputePaintLocationY(GetReferenceAxisByIndex(value.ReferenceAxisIndex), height, value.Data[num11]);
						if (num <= width)
						{
							list3.Add(item3);
						}
						else if (item3.X >= (float)offsetPaintScrollX - 10f * data_ScaleX_Render)
						{
							if (!(item3.X < (float)(offsetPaintScrollX + width) + 10f * data_ScaleX_Render))
							{
								break;
							}
							list3.Add(item3);
						}
					}
					else
					{
						HslCurveHelper.DrawLineCore(g, value, list3, GetPointsRadius(), (float)(height - 40) * data_ScaleY_Render);
						list3.Clear();
					}
				}
				HslCurveHelper.DrawLineCore(g, value, list3, GetPointsRadius(), (float)(height - 40) * data_ScaleY_Render);
			}
			for (int num12 = 0; num12 < markForeSections.Count; num12++)
			{
				DrawMarkForeSection(g, markForeSections[num12], Font, true);
			}
			g.TranslateTransform(0f, offsetPaintScrollY - 20);
			if (num > width)
			{
				g.TranslateTransform(offsetPaintScrollX, 0f);
			}
			g.SmoothingMode = SmoothingMode.None;
			if (buttomHeight <= 0)
			{
				scrollRectage = new Rectangle(-1, -1, 1, 1);
				scrollRectageBack = new Rectangle(0, 0, 0, 0);
			}
			else
			{
				using (Brush brush3 = new SolidBrush(backColor))
				{
					g.FillRectangle(brush3, 0, base.Height - topHeadHeight - scrollHeight - 5, width, scrollHeight + 3);
				}
				if (num <= width)
				{
					scrollRectage = new Rectangle(-1, -1, 1, 1);
					scrollRectageBack = new Rectangle(0, 0, 0, 0);
					scrollX = 0;
					this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
				}
				else
				{
					scrollRectage = new Rectangle(scrollX, base.Height - topHeadHeight - scrollHeight - 3, scrollWidth, scrollHeight);
					scrollRectageBack = new Rectangle(0, base.Height - topHeadHeight - scrollHeight - 3, width, scrollHeight);
					using (Brush brush4 = new SolidBrush(Color.FromArgb(80, scrollColor)))
					{
						g.FillRectangle(brush4, scrollRectageBack);
					}
					using (Brush brush5 = new SolidBrush(scrollColor))
					{
						g.FillRectangle(brush5, scrollRectage);
					}
				}
			}
			g.SmoothingMode = SmoothingMode.AntiAlias;
		}

		protected override void OnDockChanged(EventArgs e)
		{
			base.OnDockChanged(e);
			OnAutoSizeChanged(e);
		}

		private int GetPointsRadius()
		{
			if (data_ScaleX_Render >= 15f && pointsRadius == -1)
			{
				return 2;
			}
			return pointsRadius;
		}

		private void CalculateAuxiliaryPaintY(int offset)
		{
			for (int i = 0; i < auxiliary_lines.Count; i++)
			{
				if (auxiliary_lines[i].IsLeftFrame)
				{
					auxiliary_lines[i].PaintValue = HslHelper.ComputePaintLocationY(referenceAxisLeft.GetCalculateMax(), referenceAxisLeft.GetCalculateMin(), base.Height - 40 - topHeadHeight - buttomHeight, referenceAxisLeft.IsUseLogarithms() ? ((float)Math.Log10(auxiliary_lines[i].Value)) : auxiliary_lines[i].Value) + (float)offset;
				}
				else
				{
					auxiliary_lines[i].PaintValue = HslHelper.ComputePaintLocationY(referenceAxisRight.GetCalculateMax(), referenceAxisRight.GetCalculateMin(), base.Height - 40 - topHeadHeight - buttomHeight, referenceAxisRight.IsUseLogarithms() ? ((float)Math.Log10(auxiliary_lines[i].Value)) : auxiliary_lines[i].Value) + (float)offset;
				}
			}
		}

		public AuxiliaryLine AddLeftAuxiliary(float value)
		{
			return AddLeftAuxiliary(value, coordinateColor);
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
			return AddRightAuxiliary(value, coordinateColor);
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

		public void AddMarkLine(HslMarkLine markLine)
		{
			hslMarkLines.Add(markLine);
		}

		public void RemoveMarkLine(HslMarkLine markLine)
		{
			hslMarkLines.Remove(markLine);
		}

		public void RemoveAllMarkLine()
		{
			hslMarkLines.Clear();
		}

		public void AddMarkText(HslMarkText markText)
		{
			hslMarkTexts.Add(markText);
		}

		public void RemoveMarkText(HslMarkText markText)
		{
			hslMarkTexts.Remove(markText);
		}

		public void RemoveAllMarkText()
		{
			hslMarkTexts.Clear();
		}

		public void AddMarkCurvePeakPoint(string curveKey)
		{
			if (!data_dicts.ContainsKey(curveKey))
			{
				return;
			}
			float[] data = data_dicts[curveKey].Data;
			for (int i = 0; i < data.Length; i++)
			{
				if (i == 0)
				{
					if (data[i] > data[i + 1])
					{
						AddMarkText(new HslMarkText
						{
							CurveKey = curveKey,
							Index = i,
							MarkText = string.Format(data_dicts[curveKey].RenderFormat, data[i]),
							TextBrush = data_dicts[curveKey].GetLineBrush()
						});
					}
				}
				else if (i == data.Length - 1)
				{
					if (data[i] > data[i - 1])
					{
						AddMarkText(new HslMarkText
						{
							CurveKey = curveKey,
							Index = i,
							MarkText = string.Format(data_dicts[curveKey].RenderFormat, data[i]),
							TextBrush = data_dicts[curveKey].GetLineBrush()
						});
					}
				}
				else
				{
					if (!(data[i] > data[i - 1]) || !(data[i] >= data[i + 1]))
					{
						continue;
					}
					for (int j = i + 1; j < data.Length; j++)
					{
						if (data[i] != data[j])
						{
							if (data[i] > data[j])
							{
								AddMarkText(new HslMarkText
								{
									CurveKey = curveKey,
									Index = i,
									MarkText = string.Format(data_dicts[curveKey].RenderFormat, data[i]),
									TextBrush = data_dicts[curveKey].GetLineBrush()
								});
								i = j;
							}
							else
							{
								i = j;
							}
							break;
						}
					}
				}
			}
		}

		public void AddMarkImage(HslMarkImage markImage)
		{
			hslMarkImages.Add(markImage);
		}

		public void RemoveMarkImage(HslMarkImage markImage)
		{
			hslMarkImages.Remove(markImage);
		}

		public void RemoveAllMarkImages()
		{
			hslMarkImages.Clear();
		}

		private void CalculateCurveDataMax()
		{
			data_count = 0;
			for (int i = 0; i < data_lists.Count; i++)
			{
				if (data_count < data_lists[i].Data.Length)
				{
					data_count = data_lists[i].Data.Length;
				}
			}
		}

		public void SetDateTimes(DateTime[] times)
		{
			data_times = new List<DateTime>(times);
			isRenderTimeData = true;
		}

		public void SetDateCustomer(string[] customers)
		{
			data_customer = new List<string>(customers);
			isRenderTimeData = false;
		}

		public void SetScaleByXAxis(float scale)
		{
			data_ScaleX_Render = scale;
			SetScrollXNewValue(scrollX);
			this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
		}

		public void SetScaleXOptions(float[] scales, int index)
		{
			scale_x_options = scales;
			if (index >= 0 && index < scale_x_options.Length)
			{
				scale_x_index = index;
				data_ScaleX_Render = scale_x_options[index];
			}
		}

		public void SetScaleYOptions(float[] scales, int index)
		{
			scale_y_options = scales;
			if (index >= 0 && index < scale_y_options.Length)
			{
				scale_y_index = index;
				data_ScaleY_Render = scale_y_options[index];
			}
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

		public void SetLeftCurve(string key, float[] data, Color lineColor, CurveStyle style, string renderFormat)
		{
			SetCurve(key, 0, data, lineColor, 1f, style, renderFormat);
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

		public void SetRightCurve(string key, float[] data, Color lineColor, CurveStyle style, string renderFormat)
		{
			SetCurve(key, 1, data, lineColor, 1f, style, renderFormat);
		}

		public void SetCurve(string key, int referenceAxis, float[] data, Color lineColor, float thickness, CurveStyle style)
		{
			SetCurve(key, referenceAxis, data, lineColor, thickness, style, "{0}");
		}

		public void SetCurve(string key, int referenceAxis, float[] data, Color lineColor, float thickness, CurveStyle style, string renderFormat)
		{
			if (data_dicts.ContainsKey(key))
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_dicts[key].Data = data;
			}
			else
			{
				if (data == null)
				{
					data = new float[0];
				}
				data_dicts.Add(key, new HslCurveItem
				{
					Data = data,
					LineThickness = thickness,
					LineColor = lineColor,
					ReferenceAxisIndex = referenceAxis,
					Style = style,
					RenderFormat = renderFormat
				});
				data_lists.Add(data_dicts[key]);
			}
			CalculateCurveDataMax();
		}

		public void SetCurve(string key, HslCurveItem curveItem)
		{
			data_dicts.Add(key, curveItem);
			CalculateCurveDataMax();
		}

		public void SetCurveLineColor(string key, Color lineColor)
		{
			if (data_dicts.ContainsKey(key))
			{
				data_dicts[key].LineColor = lineColor;
			}
		}

		public void SetCurveLineThickness(string key, float thickness)
		{
			if (data_dicts.ContainsKey(key))
			{
				data_dicts[key].LineThickness = thickness;
			}
		}

		public void SetCurveLineCurveStyle(string key, CurveStyle style)
		{
			if (data_dicts.ContainsKey(key))
			{
				data_dicts[key].Style = style;
			}
		}

		public void SetCurveLineRenderFormat(string key, string renderFormat)
		{
			if (data_dicts.ContainsKey(key))
			{
				data_dicts[key].RenderFormat = renderFormat;
			}
		}

		public void RemoveCurve(string key)
		{
			if (data_dicts.ContainsKey(key))
			{
				data_lists.Remove(data_dicts[key]);
				data_dicts.Remove(key);
			}
			if (data_dicts.Count == 0)
			{
				data_times = new List<DateTime>(0);
				data_customer = new List<string>();
			}
			CalculateCurveDataMax();
		}

		public Dictionary<string, HslCurveItem> GetAllCurve()
		{
			return data_dicts;
		}

		public void RemoveAllCurve()
		{
			data_dicts.Clear();
			data_lists.Clear();
			markBackSections.Clear();
			markForeSections.Clear();
			markForeSectionsTmp.Clear();
			markForeActiveSections.Clear();
			hslMarkTexts.Clear();
			auxiliary_Labels.Clear();
			hslMarkLines.Clear();
			if (data_dicts.Count == 0)
			{
				data_times = new List<DateTime>(0);
			}
			CalculateCurveDataMax();
			SetScrollXNewValue(0);
			this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
		}

		public void SetCurveVisible(string key, bool visible, bool lineRenderVisiable = true)
		{
			if (data_dicts.ContainsKey(key))
			{
				HslCurveItem hslCurveItem = data_dicts[key];
				hslCurveItem.Visible = visible;
				hslCurveItem.LineRenderVisiable = lineRenderVisiable;
			}
		}

		public void SetCurveVisible(string[] keys, bool visible, bool lineRenderVisiable = true)
		{
			foreach (string key in keys)
			{
				if (data_dicts.ContainsKey(key))
				{
					HslCurveItem hslCurveItem = data_dicts[key];
					hslCurveItem.Visible = visible;
					hslCurveItem.LineRenderVisiable = lineRenderVisiable;
				}
			}
		}

		private void PictureBox3_MouseEnter(object sender, EventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnMouseEnter(e);
			}
			else
			{
				is_mouse_on_picture = true;
			}
		}

		private void PictureBox3_MouseLeave(object sender, EventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnMouseLeave(e);
				return;
			}
			if (!isMouseFreeze)
			{
				is_mouse_on_picture = false;
				this.onCurveMouseMove?.Invoke(this, -1, -1);
			}
			HslInvalidate();
		}

		private void PictureBox3_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnMouseDoubleClick(e);
			}
			else
			{
				if (isShowTextInfomation || !IsPointInDataRegion(e.Location))
				{
					return;
				}
				int actualLeftWidth = GetActualLeftWidth();
				if (e.X - actualLeftWidth + offsetPaintScrollX < 0)
				{
					return;
				}
				mouse_location = new Point(e.Location.X - actualLeftWidth, e.Location.Y - topHeadHeight);
				isMouseFreeze = true;
				int num = Convert.ToInt32((float)(e.X - actualLeftWidth + offsetPaintScrollX) / data_ScaleX_Render);
				if (isRenderTimeData)
				{
					if (num >= 0 && num < data_times.Count)
					{
						this.onCurveDoubleClick?.Invoke(this, num, data_times[num]);
					}
				}
				else if (num >= 0 && num < data_customer.Count)
				{
					this.onCurveCustomerDoubleClick?.Invoke(this, num, data_customer[num]);
				}
			}
		}

		private void PictureBox3_MouseUp(object sender, MouseEventArgs e)
		{
			if (is_mouse_click_scroll)
			{
				is_mouse_click_scroll = false;
				HslInvalidate();
			}
			else if (e.Button == MouseButtons.Left && isAllowSelectSection)
			{
				HslMarkForeSection hslMarkForeSection = new HslMarkForeSection
				{
					StartIndex = m_RowBetweenStart,
					EndIndex = m_RowBetweenEnd,
					Height = m_RowBetweenHeight,
					StartHeight = m_RowBetweenStartHeight,
					LinePen = markLinePen,
					FontBrush = markTextBrush
				};
				markForeSectionsTmp.Add(hslMarkForeSection);
				m_IsMouseLeftDown = false;
				HslInvalidate();
				this.onCurveRangeSelect?.Invoke(this, hslMarkForeSection);
				m_RowBetweenStart = -1;
				m_RowBetweenEnd = -1;
				m_RowBetweenHeight = -1;
				m_RowBetweenStartHeight = -1;
			}
			else if (e.Button == MouseButtons.Middle)
			{
				if (syncHslCurveHistory != null)
				{
					syncHslCurveHistory.OnMouseUp(e);
					return;
				}
				m_IsMouseMiddleDown = false;
				mouse_right_location = new Point(-1, -1);
			}
		}

		private void PictureBox3_MouseDown(object sender, MouseEventArgs e)
		{
			int actualLeftWidth = GetActualLeftWidth();
			foreach (KeyValuePair<string, HslCurveItem> data_dict in data_dicts)
			{
				if (data_dict.Value.TitleRegion.Contains(e.Location))
				{
					data_dict.Value.LineRenderVisiable = !data_dict.Value.LineRenderVisiable;
					HslInvalidate();
					return;
				}
			}
			if (ReferenceAxis.Count > 0 && new Rectangle(GetActualLeftWidth() - leftWidth, base.Height - scrollHeight - 5, leftWidth, scrollHeight).Contains(e.Location))
			{
				isOtherAxisHide = !isOtherAxisHide;
				HslInvalidate();
			}
			else if (scrollRectage.X >= 0 && scrollRectage.Contains(new Point(e.Location.X - actualLeftWidth, e.Location.Y - topHeadHeight)) && e.Button == MouseButtons.Left)
			{
				is_mouse_click_scroll = true;
				mouse_scroll_location = e.Location;
			}
			else if (scrollRectageBack.Width > 0 && scrollRectageBack.Contains(new Point(e.Location.X - actualLeftWidth, e.Location.Y - topHeadHeight)) && e.Button == MouseButtons.Left)
			{
				is_mouse_click_scroll = true;
				mouse_scroll_location = e.Location;
				SetScrollXNewValue(e.Location.X - actualLeftWidth);
				this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
				Refresh();
			}
			else if (e.Button == MouseButtons.Right)
			{
				isMouseFreeze = false;
				markForeSectionsTmp.Clear();
				m_RowBetweenStart = -1;
				m_RowBetweenEnd = -1;
				m_RowBetweenHeight = -1;
				m_RowBetweenStartHeight = -1;
				HslInvalidate();
			}
			else if (e.Button == MouseButtons.Middle)
			{
				if (syncHslCurveHistory != null)
				{
					syncHslCurveHistory.OnMouseDown(e);
					return;
				}
				m_IsMouseMiddleDown = true;
				mouse_right_location = e.Location;
			}
			else if (isAllowSelectSection && e != null && e.X - actualLeftWidth + offsetPaintScrollX >= 0 && IsPointInDataRegion(e.Location))
			{
				m_IsMouseLeftDown = true;
				m_RowBetweenStart = Convert.ToInt32((float)(e.X - actualLeftWidth + offsetPaintScrollX) / data_ScaleX_Render);
				m_RowBetweenStartHeight = e.Y - topHeadHeight;
				m_RowBetweenHeight = e.Y - topHeadHeight;
			}
		}

		private void PictureBox3_MouseMove(object sender, MouseEventArgs e)
		{
			if (syncHslCurveHistory != null)
			{
				syncHslCurveHistory.OnMouseMove(e);
				return;
			}
			bool flag = false;
			foreach (KeyValuePair<string, HslCurveItem> data_dict in data_dicts)
			{
				if (data_dict.Value.TitleRegion.Contains(e.Location))
				{
					flag = true;
					break;
				}
			}
			if (ReferenceAxis.Count > 0 && new Rectangle(GetActualLeftWidth() - leftWidth, base.Height - scrollHeight - 5, leftWidth, scrollHeight).Contains(e.Location))
			{
				flag = true;
			}
			Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
			if (is_mouse_click_scroll)
			{
				int num = e.Location.X - mouse_scroll_location.X;
				mouse_scroll_location = new Point(e.Location.X, e.Location.Y);
				SetScrollXNewValue(scrollX + num);
				Refresh();
				this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
				this.OnScrollChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
				return;
			}
			if (IsPointInDataRegion(e.Location))
			{
				int actualLeftWidth = GetActualLeftWidth();
				int curvePaintActualWidth = GetCurvePaintActualWidth();
				is_mouse_on_picture = true;
				if (is_mouse_on_picture && !isShowTextInfomation && !isMouseFreeze)
				{
					mouse_location = new Point(e.Location.X - actualLeftWidth, e.Location.Y - topHeadHeight);
					this.onCurveMouseMove?.Invoke(this, mouse_location.X, mouse_location.Y);
					Refresh();
				}
				if (!m_IsMouseMiddleDown)
				{
					return;
				}
				int num2 = e.Location.X - mouse_right_location.X;
				int num3 = ((data_ScaleY_Render > 1.1f) ? (e.Location.Y - mouse_right_location.Y) : 0);
				mouse_right_location = e.Location;
				int num4 = CalculatePaintWidthAndScrollMax(curvePaintActualWidth);
				if (num4 > curvePaintActualWidth)
				{
					int num5 = offsetPaintScrollX - num2;
					SetScrollXNewValue(Convert.ToInt32((float)((long)(offsetPaintScrollX - num2) * (long)scrollMaxX) * 1f / (float)(num4 - curvePaintActualWidth)));
					offsetPaintScrollX = num5;
					if (offsetPaintScrollX >= num4 - curvePaintActualWidth)
					{
						offsetPaintScrollX = num4 - curvePaintActualWidth;
					}
					if (offsetPaintScrollX < 0)
					{
						offsetPaintScrollX = 0;
					}
					offsetPaintScrollY = EnsureOffsetPaintScrollY(offsetPaintScrollY - num3);
					Refresh();
					this.OnScaleChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
					this.OnScrollChanged?.Invoke(this, scrollX, data_ScaleX_Render, offsetPaintScrollX);
				}
				return;
			}
			if (!isMouseFreeze)
			{
				mouse_location = new Point(-2);
				if (is_mouse_on_picture)
				{
					this.onCurveMouseMove?.Invoke(this, -1, -1);
				}
				is_mouse_on_picture = false;
			}
			HslInvalidate();
		}

		public void SetCurveMousePosition(int x, int y)
		{
			if (x < 0 || y < 0)
			{
				is_mouse_on_picture = false;
			}
			else
			{
				is_mouse_on_picture = true;
			}
			mouse_location.X = x;
			mouse_location.Y = y;
			Refresh();
		}

		private bool IsPointInDataRegion(Point point)
		{
			int actualLeftWidth = GetActualLeftWidth();
			return point.X >= actualLeftWidth && point.X <= base.Width - rightWidth && point.Y >= topHeadHeight && point.Y <= base.Height - buttomHeight;
		}

		private void PictureBox3_Paint(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			int actualLeftWidth = GetActualLeftWidth();
			graphics.TranslateTransform(actualLeftWidth, topHeadHeight);
			graphics.Clip = new Region(new Rectangle(0, 0, base.Width - actualLeftWidth - rightWidth + 1, base.Height - topHeadHeight - scrollHeight + 1));
			Font font = new Font(Font.FontFamily, 12f);
			int num = (int)((float)(mouse_location.X + offsetPaintScrollX) / data_ScaleX_Render);
			graphics.TranslateTransform(-offsetPaintScrollX, 0f);
			for (int i = 0; i < markForeSectionsTmp.Count; i++)
			{
				DrawMarkForeSection(graphics, markForeSectionsTmp[i], font);
			}
			if (m_IsMouseLeftDown && m_RowBetweenStart != -1 && num < data_count)
			{
				m_RowBetweenEnd = Convert.ToInt32((float)(mouse_location.X + offsetPaintScrollX) / data_ScaleX_Render);
				m_RowBetweenHeight = mouse_location.Y;
			}
			DrawMarkForeSection(graphics, new HslMarkForeSection
			{
				StartIndex = m_RowBetweenStart,
				EndIndex = m_RowBetweenEnd,
				Height = m_RowBetweenHeight,
				StartHeight = m_RowBetweenStartHeight,
				LinePen = markLinePen,
				FontBrush = markTextBrush
			}, font);
			for (int j = 0; j < markForeActiveSections.Count; j++)
			{
				if (IsPointInDataRegion(new Point(mouse_location.X + actualLeftWidth, mouse_location.Y + topHeadHeight)) && is_mouse_on_picture && num >= markForeActiveSections[j].StartIndex && num <= markForeActiveSections[j].EndIndex)
				{
					DrawMarkForeSection(graphics, markForeActiveSections[j], font);
				}
			}
			graphics.TranslateTransform(offsetPaintScrollX, 0f);
			if (is_mouse_on_picture && !isShowTextInfomation && markLineVisible)
			{
				graphics.DrawLine(moveLinePen, mouse_location.X, 15, mouse_location.X, base.Height - topHeadHeight - buttomHeight - 18);
				if (isRenderYTip)
				{
					graphics.DrawLine(moveLinePen, 0, mouse_location.Y, base.Width - rightWidth, mouse_location.Y);
				}
				if (num >= data_count || num < 0)
				{
					return;
				}
				int num2 = mouse_location.X;
				int num3 = data_tip_width;
				int num4 = 0;
				foreach (KeyValuePair<string, HslCurveItem> data_dict in data_dicts)
				{
					if (data_dict.Value.LineRenderVisiable && data_dict.Value.Style != CurveStyle.PointMark)
					{
						num4++;
					}
					if (data_tip_width < 0)
					{
						int num5 = (int)graphics.MeasureString(data_dict.Key, font).Width;
						if (num < data_dict.Value.Data.Length)
						{
							num5 += (int)graphics.MeasureString(string.Format(data_dict.Value.RenderFormat, data_dict.Value.Data[num]), font).Width;
						}
						num3 = Math.Max(num3, num5 + 25);
					}
				}
				if (num2 + num3 + 20 > base.Width - rightWidth - actualLeftWidth)
				{
					num2 = mouse_location.X - num3 - 10;
				}
				int num6 = mouse_location.Y - num4 * 20 - 20;
				if (num6 < 5)
				{
					num6 = 5;
				}
				Rectangle rectangle = new Rectangle(num2 + 5, num6 - 5, num3, num4 * 20 + 10);
				if (num3 > 0)
				{
					using (GraphicsPath path = HslHelper.GetRoundRectange(rectangle, 4, true, true, true, true))
					{
						graphics.FillPath(mouseHoverBackBrush, path);
						graphics.DrawPath(markBorderPen, path);
					}
					foreach (KeyValuePair<string, HslCurveItem> data_dict2 in data_dicts)
					{
						if (data_dict2.Value.LineRenderVisiable && data_dict2.Value.Style != CurveStyle.PointMark)
						{
							Rectangle rectangle2 = new Rectangle(rectangle.X + 3, num6, num3 - 6, 20);
							graphics.DrawString(data_dict2.Key, font, markTextBrush, rectangle2, HslHelper.StringFormatLeft);
							if (num < data_dict2.Value.Data.Length)
							{
								graphics.DrawString(string.Format(data_dict2.Value.RenderFormat, data_dict2.Value.Data[num]), font, markTextBrush, rectangle2, HslHelper.StringFormatRight);
							}
							num6 += 20;
						}
					}
				}
				num6 = mouse_location.Y + 25;
				for (int k = 0; k < markForeActiveSections.Count; k++)
				{
					if (num < markForeActiveSections[k].StartIndex || num > markForeActiveSections[k].EndIndex)
					{
						continue;
					}
					float num7 = 0f;
					foreach (KeyValuePair<string, string> cursorText in markForeActiveSections[k].CursorTexts)
					{
						num7 += graphics.MeasureString(cursorText.Key + " : " + cursorText.Value, Font, 244).Height + 8f;
					}
					rectangle = new Rectangle(num2 + 5, num6 - 5, 250, (int)num7 + 10);
					if (num2 < mouse_location.X)
					{
						rectangle.X = mouse_location.X - 250 - 10;
					}
					graphics.FillRectangle(mouseHoverBackBrush, rectangle);
					graphics.DrawRectangle(markBorderPen, rectangle);
					foreach (KeyValuePair<string, string> cursorText2 in markForeActiveSections[k].CursorTexts)
					{
						num7 = graphics.MeasureString(cursorText2.Key + " : " + cursorText2.Value, Font, 244).Height;
						graphics.DrawString(layoutRectangle: new Rectangle(rectangle.X + 3, num6, 244, 200), s: cursorText2.Key + " : " + cursorText2.Value, font: font, brush: Brushes.Yellow, format: HslHelper.StringFormatDefault);
						num6 += (int)num7 + 8;
					}
					break;
				}
				Rectangle rectangle3 = new Rectangle(mouse_location.X - mouseHoverTimeWidth / 2, base.Height - topHeadHeight - buttomHeight - 18, mouseHoverTimeWidth, 17);
				if (rectangle3.X < 0)
				{
					rectangle3.X = 0;
				}
				if (rectangle3.X > base.Width - actualLeftWidth - rightWidth - mouseHoverTimeWidth - 1)
				{
					rectangle3.X = base.Width - actualLeftWidth - rightWidth - mouseHoverTimeWidth - 1;
				}
				if (buttomHeight > 20)
				{
					rectangle3.Height += buttomHeight - 20;
				}
				if (isRenderTimeData)
				{
					if (num < data_times.Count)
					{
						graphics.FillRectangle(mouseHoverBackBrush, rectangle3);
						graphics.DrawRectangle(markBorderPen, rectangle3);
						graphics.DrawString(data_times[num].ToString(mouse_hover_time_formate, CultureInfo.InvariantCulture), Font, markTextBrush, rectangle3, HslHelper.StringFormatCenter);
					}
				}
				else if (num < data_customer.Count)
				{
					graphics.FillRectangle(mouseHoverBackBrush, rectangle3);
					graphics.DrawRectangle(markBorderPen, rectangle3);
					graphics.DrawString(data_customer[num], Font, markTextBrush, rectangle3, HslHelper.StringFormatCenter);
				}
			}
			font.Dispose();
			graphics.TranslateTransform(-actualLeftWidth, -topHeadHeight);
		}

		public void AddMarkBackSection(HslMarkBackSection markBackSection)
		{
			markBackSections.Add(markBackSection);
		}

		public void RemoveMarkBackSection(HslMarkBackSection markBackSection)
		{
			markBackSections.Remove(markBackSection);
		}

		public void RemoveAllMarkBackSection()
		{
			markBackSections.Clear();
		}

		public void AddMarkForeSection(HslMarkForeSection markForeSection)
		{
			markForeSections.Add(markForeSection);
		}

		public void RemoveMarkForeSection(HslMarkForeSection markForeSection)
		{
			markForeSections.Remove(markForeSection);
		}

		public void RemoveAllMarkForeSection()
		{
			markForeSections.Clear();
		}

		public void RemoveAllMarkMouseSection()
		{
			markForeSectionsTmp.Clear();
			m_RowBetweenStart = -1;
			m_RowBetweenEnd = -1;
			m_RowBetweenHeight = -1;
			m_RowBetweenStartHeight = -1;
			HslInvalidate();
		}

		public void AddMarkActiveSection(HslMarkForeSection markActiveSection)
		{
			markForeActiveSections.Add(markActiveSection);
		}

		private void DrawMarkForeSection(Graphics g, HslMarkForeSection markForeSection, Font font, bool paintMain = false)
		{
			if (markForeSection == null)
			{
				return;
			}
			float num = 0f;
			num = ((!paintMain) ? ((markForeSection.Height > 1f) ? markForeSection.Height : ((float)(base.Height - topHeadHeight - buttomHeight - 40) * markForeSection.Height + 20f)) : ((markForeSection.Height > 1f) ? markForeSection.Height : ((float)(base.Height - topHeadHeight - buttomHeight - 40) * data_ScaleY_Render * markForeSection.Height)));
			float num2 = 0f;
			num2 = ((!paintMain) ? ((markForeSection.StartHeight > 1f) ? markForeSection.StartHeight : ((float)(base.Height - topHeadHeight - buttomHeight - 40) * markForeSection.StartHeight + 20f)) : ((markForeSection.StartHeight > 1f) ? markForeSection.StartHeight : ((float)(base.Height - topHeadHeight - buttomHeight - 40) * data_ScaleY_Render * markForeSection.StartHeight)));
			if (markForeSection.StartIndex == -1 || markForeSection.EndIndex == -1 || markForeSection.EndIndex <= markForeSection.StartIndex || !(num2 < num))
			{
				return;
			}
			int num3 = Convert.ToInt32((float)markForeSection.StartIndex * data_ScaleX_Render);
			int num4 = Convert.ToInt32((float)markForeSection.EndIndex * data_ScaleX_Render);
			if (markForeSection.StartIndex < data_count && markForeSection.EndIndex < data_count && markForeSection.StartIndex < data_times.Count && markForeSection.EndIndex < data_times.Count)
			{
				g.DrawLine(markForeSection.LinePen, new PointF(num3, num2), new PointF(num3, num + 30f));
				g.DrawLine(markForeSection.LinePen, new PointF(num4, num2), new PointF(num4, num + 30f));
				int num5 = (markForeSection.IsRenderTimeText ? 20 : 0);
				int num6 = ((num4 - num3 > 100) ? num5 : 110);
				int num7 = num4 - num3;
				g.DrawLine(markForeSection.LinePen, new PointF(num3 - num6, num), new PointF(num4 + num5, num));
				g.DrawLines(markForeSection.LinePen, new PointF[3]
				{
					new PointF(num3 + 20, num + 10f),
					new PointF(num3, num),
					new PointF(num3 + 20, num - 10f)
				});
				g.DrawLines(markForeSection.LinePen, new PointF[3]
				{
					new PointF(num4 - 20, num - 10f),
					new PointF(num4, num),
					new PointF(num4 - 20, num + 10f)
				});
				TimeSpan timeSpan = data_times[markForeSection.EndIndex] - data_times[markForeSection.StartIndex];
				string empty = string.Empty;
				empty = ((timeSpan.TotalMinutes > 2.0) ? (timeSpan.TotalMinutes.ToString("F1") + " 分钟") : ((!(timeSpan.TotalSeconds > 1.0)) ? ((int)timeSpan.TotalMilliseconds + " 毫秒") : (timeSpan.TotalSeconds.ToString("F1") + " 秒")));
				if (num4 - num3 <= 100)
				{
					g.DrawString(empty, font, markForeSection.FontBrush, new PointF(num3 - 100, num - 17f));
				}
				else
				{
					g.DrawString(empty, font, markForeSection.FontBrush, new RectangleF(num3, num - (float)font.Height - 2f, num4 - num3, font.Height), HslHelper.StringFormatCenter);
				}
				if (!string.IsNullOrEmpty(markForeSection.MarkText))
				{
					g.DrawString(markForeSection.MarkText, font, markForeSection.FontBrush, new RectangleF(num3, num + 3f, num4 - num3, font.Height), HslHelper.StringFormatCenter);
				}
				if (markForeSection.IsRenderTimeText)
				{
					g.DrawString("开始 " + data_times[markForeSection.StartIndex].ToString(mouse_hover_time_formate), font, markForeSection.FontBrush, new PointF(num4 + 5, num - 17f));
					g.DrawString("结束 " + data_times[markForeSection.EndIndex].ToString(mouse_hover_time_formate), font, markForeSection.FontBrush, new PointF(num4 + 5, num + 2f));
				}
			}
		}

		public Bitmap SaveToBitmap(bool isCurrentRegion = false)
		{
			int actualLeftWidth = GetActualLeftWidth();
			if (isShowTextInfomation)
			{
				Bitmap bitmap = new Bitmap(base.Width, base.Height);
				Graphics graphics = Graphics.FromImage(bitmap);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				graphics.Clear(backColor);
				PaintMain(graphics, base.Width, base.Height);
				graphics.TranslateTransform(actualLeftWidth, topHeadHeight);
				PaintFromString(graphics, Text);
				graphics.TranslateTransform(-actualLeftWidth, -topHeadHeight);
				graphics.Dispose();
				return bitmap;
			}
			int num = (int)((float)data_count * data_ScaleX_Render) + rightRemainWidth;
			if (num < base.Width - actualLeftWidth - rightWidth)
			{
				num = base.Width - actualLeftWidth - rightWidth;
			}
			int num2 = base.Height - topHeadHeight - buttomHeight;
			Bitmap bitmap2 = new Bitmap(isCurrentRegion ? base.Width : (num + actualLeftWidth + rightWidth), base.Height);
			Graphics graphics2 = Graphics.FromImage(bitmap2);
			graphics2.SmoothingMode = SmoothingMode.AntiAlias;
			graphics2.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			graphics2.Clear(backColor);
			PaintMain(graphics2, bitmap2.Width, bitmap2.Height);
			if (isCurrentRegion)
			{
				PictureBox3_Paint(this, new PaintEventArgs(graphics2, new Rectangle(0, 0, bitmap2.Width, bitmap2.Height)));
			}
			graphics2.Dispose();
			return bitmap2;
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
			this.BackColor = System.Drawing.Color.FromArgb(46, 46, 46);
			base.Name = "HslCurveHistory";
			base.Size = new System.Drawing.Size(852, 478);
			base.ResumeLayout(false);
		}
	}
}
