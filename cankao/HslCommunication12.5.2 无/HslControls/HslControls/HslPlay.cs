using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[DefaultEvent("OnPlayChanged")]
	[DefaultBindingProperty("Text")]
	[DefaultProperty("Text")]
	[Description("一个按钮控件，样式是播放停止")]
	public class HslPlay : UserControl
	{
		private Color textForeColor = Color.OrangeRed;

		private Brush textForeBrush = new SolidBrush(Color.OrangeRed);

		private bool played = false;

		private Color activeColor = Color.Green;

		private Brush activeBrush = new SolidBrush(Color.Green);

		private Color borderColor = Color.DodgerBlue;

		private StringFormat sf = null;

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
		[Description("获取或设置当前控件的文本的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "OrangeRed")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override Color ForeColor
		{
			get
			{
				return textForeColor;
			}
			set
			{
				textForeColor = value;
				textForeBrush.Dispose();
				textForeBrush = new SolidBrush(value);
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
				base.Text = value;
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Green")]
		[Description("当点击了开始播放的时候的前景色")]
		public Color ActiveColor
		{
			get
			{
				return activeColor;
			}
			set
			{
				activeColor = value;
				activeBrush.Dispose();
				activeBrush = new SolidBrush(activeColor);
				Invalidate();
			}
		}

		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DodgerBlue")]
		[Description("获取或设置外层圈圈的颜色")]
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

		[Category("HslControls")]
		[DefaultValue(false)]
		[Description("获取或设置是否处于播放中的状态")]
		public bool Played
		{
			get
			{
				return played;
			}
			set
			{
				if (value != played)
				{
					played = value;
					Invalidate();
					this.OnPlayChanged?.Invoke(this, played);
				}
			}
		}

		[Category("Action")]
		[Description("点击了按钮开发后触发")]
		public event Action<object, bool> OnPlayChanged;

		public HslPlay()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				Played = !Played;
			}
			base.OnMouseClick(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(false)
			{
				e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
				e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				PaintHslControls(e.Graphics, base.Width, base.Height);
				base.OnPaint(e);
			}
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if ( width >= 3 && height >= 3)
			{
				int num = Math.Min(width, height);
				int num2 = ((num / 15 == 0) ? 1 : (num / 15));
				using (Pen pen = new Pen(borderColor, num2))
				{
					g.DrawEllipse(pen, num2, num2, num - 2 * num2, num - 2 * num2);
				}
				if (!played)
				{
					g.FillPolygon(points: new Point[4]
					{
						new Point(num * 32 / 100, num * 27 / 100),
						new Point(num * 32 / 100, num * 73 / 100),
						new Point(num * 78 / 100, num * 50 / 100),
						new Point(num * 32 / 100, num * 27 / 100)
					}, brush: textForeBrush);
				}
				else
				{
					g.FillRectangle(activeBrush, num * 32 / 100, num * 27 / 100, num * 12 / 100, num * 46 / 100);
					g.FillRectangle(activeBrush, num * 56 / 100, num * 27 / 100, num * 12 / 100, num * 46 / 100);
				}
				if (height - num > 0 && !string.IsNullOrEmpty(Text))
				{
					g.DrawString(Text, Font, textForeBrush, new Rectangle(0, num, num, height - num), sf);
				}
			}
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
			this.Cursor = System.Windows.Forms.Cursors.Hand;
			base.Name = "HslPlay";
			base.Size = new System.Drawing.Size(97, 139);
			base.ResumeLayout(false);
		}
	}
}
