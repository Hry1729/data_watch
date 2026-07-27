using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls
{
	[Description("Panel控件，从系统继承而来，支持背景的渐变色颜色")]
	public class HslPanel : Panel
	{
		private Color borderColor = Color.Gray;

		private Color edgeColor = Color.DimGray;

		private Color centerColor = Color.Gray;

		private Pen boderPen = new Pen(Color.Gray, 1f);

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

		[Description("获取或设置当前panel的边框颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				boderPen?.Dispose();
				boderPen = new Pen(borderColor, 1f);
				Invalidate();
			}
		}

		[Description("获取或设置当前panel的渐变色的边缘颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color EdgeColor
		{
			get
			{
				return edgeColor;
			}
			set
			{
				edgeColor = value;
				Invalidate();
			}
		}

		[Description("获取或设置当前panel的渐变色的中间颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Gray")]
		public Color CenterColor
		{
			get
			{
				return centerColor;
			}
			set
			{
				centerColor = value;
				Invalidate();
			}
		}

		public HslPanel()
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
			if(false)
			{
				Graphics graphics = e.Graphics;
				ColorBlend colorBlend = new ColorBlend();
				colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
				colorBlend.Colors = new Color[3] { edgeColor, centerColor, edgeColor };
				using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, base.Height - 1), centerColor, borderColor))
				{
					linearGradientBrush.InterpolationColors = colorBlend;
					graphics.FillRectangle(linearGradientBrush, e.ClipRectangle);
				}
				graphics.DrawRectangle(boderPen, new Rectangle(0, 0, base.Width - 1, base.Height - 1));
			}
		}
	}
}
