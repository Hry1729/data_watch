using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("Panel控件，继承自系统控件，左上角带一个文本的标题，可设置背景颜色")]
	public class HslPanelTextBack : Panel
	{
		private int textOffect = 20;

		private Color borderColor = Color.DodgerBlue;

		private Color panelBackColor = Color.Lavender;

		private Color panelTextBackColor = Color.Red;

		private StringFormat sf = null;

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
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前控件的主题色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Lavender")]
		public Color PanelBackColor
		{
			get
			{
				return panelBackColor;
			}
			set
			{
				panelBackColor = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前控件的主题色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Red")]
		public Color PanelTextBackColor
		{
			get
			{
				return panelTextBackColor;
			}
			set
			{
				panelTextBackColor = value;
				Invalidate();
			}
		}

		[Description("获取或设置控件顶部文本的高度")]
		[Category("HslControls")]
		[DefaultValue(30)]
		public int TextOffect
		{
			get
			{
				return textOffect;
			}
			set
			{
				textOffect = value;
				Invalidate();
			}
		}

		public HslPanelTextBack()
		{
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			SizeF sizeF = e.Graphics.MeasureString(Text, Font);
			sizeF = new SizeF(sizeF.Width + 8f, sizeF.Height + 4f);
			using (SolidBrush brush = new SolidBrush(panelBackColor))
			{
				e.Graphics.FillRectangle(brush, new RectangleF(0f, sizeF.Height / 2f, base.Width - 1, (float)base.Height - sizeF.Height / 2f));
			}
			using (SolidBrush brush2 = new SolidBrush(panelTextBackColor))
			{
				e.Graphics.FillRectangle(brush2, new RectangleF(textOffect, 0f, sizeF.Width, sizeF.Height));
			}
			using (Brush brush3 = new SolidBrush(ForeColor))
			{
				e.Graphics.DrawString(Text, Font, brush3, new RectangleF(textOffect, 0f, sizeF.Width, sizeF.Height), sf);
			}
			using (Pen pen = new Pen(borderColor))
			{
				PointF[] points = new PointF[6]
				{
					new PointF(textOffect, (int)(sizeF.Height / 2f)),
					new PointF(0f, (int)(sizeF.Height / 2f)),
					new PointF(0f, base.Height - 1),
					new PointF(base.Width - 1, base.Height - 1),
					new PointF(base.Width - 1, (int)(sizeF.Height / 2f)),
					new PointF((float)textOffect + sizeF.Width, (int)(sizeF.Height / 2f))
				};
				e.Graphics.DrawLines(pen, points);
			}
		}
	}
}
