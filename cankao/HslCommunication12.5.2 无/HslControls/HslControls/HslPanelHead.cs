using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("Panel控件，继承自系统控件，控件上部分携带一个渐变色的文本区域")]
	public class HslPanelHead : Panel
	{
		private int headHeight = 30;

		private Color themeColor = Color.DodgerBlue;

		private StringFormat sf = null;

		private bool isShowBorder = true;

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

		[Description("获取或设置当前控件的主题色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		public Color ThemeColor
		{
			get
			{
				return themeColor;
			}
			set
			{
				themeColor = value;
				Invalidate();
			}
		}

		[Description("获取或设置控件顶部文本的高度")]
		[Category("HslControls")]
		[DefaultValue(30)]
		public int HeadHeight
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

		[Description("获取或设置控件是否显示边框")]
		[Category("HslControls")]
		[DefaultValue(true)]
		public bool ShowBorder
		{
			get
			{
				return isShowBorder;
			}
			set
			{
				isShowBorder = value;
				Invalidate();
			}
		}

		public HslPanelHead()
		{
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			base.OnPaintBackground(e);
			
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			using (LinearGradientBrush brush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, headHeight), HslHelper.GetColorLight(themeColor), themeColor))
			{
				e.Graphics.FillRectangle(brush, 0, 0, base.Width, headHeight);
			}
			using (Brush brush2 = new SolidBrush(ForeColor))
			{
				e.Graphics.DrawString(Text, Font, brush2, new Rectangle(0, 0, base.Width, headHeight), sf);
			}
			if (!isShowBorder)
			{
				return;
			}
			using (Pen pen = new Pen(themeColor))
			{
				e.Graphics.DrawRectangle(pen, 0, 0, base.Width - 1, base.Height - 1);
				e.Graphics.DrawLine(pen, 0, headHeight, base.Width - 1, headHeight);
			}
		}
	}
}
