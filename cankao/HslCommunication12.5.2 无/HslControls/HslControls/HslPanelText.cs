using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("Panel控件，左上角带一个文本的标题，该标题没有背景")]
	public class HslPanelText : Panel
	{
		private int textOffect = 20;

		private Color themeColor = Color.DodgerBlue;

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

		public HslPanelText()
		{
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			base.OnPaintBackground(e);
			
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			SizeF sizeF = e.Graphics.MeasureString(Text, Font);
			sizeF = new SizeF(sizeF.Width + 8f, sizeF.Height + 4f);
			using (Brush brush = new SolidBrush(ForeColor))
			{
				e.Graphics.DrawString(Text, Font, brush, new RectangleF(textOffect, 0f, sizeF.Width, sizeF.Height), sf);
			}
			using (Pen pen = new Pen(themeColor))
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
