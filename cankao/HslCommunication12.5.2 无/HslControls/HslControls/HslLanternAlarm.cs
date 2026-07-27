using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls
{
	[Description("报警灯控件，支持闪烁，颜色设置")]
	public class HslLanternAlarm : UserControl
	{
		private Color bottomColor = Color.DimGray;

		private Brush bottomBrush = new SolidBrush(Color.DimGray);

		private Color lightColor = Color.Tomato;

		private Brush lightBrush = new SolidBrush(Color.Tomato);

		private Color alarmColor = Color.Tomato;

		private Color normalColor = Color.LightGray;

		private bool isAlarm = false;

		private bool alarmStatus = false;

		private Timer timer = new Timer();

		private StringFormat sf = null;

		private float buttonHeight = 0.1f;

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
		[Description("获取或设置底座的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "DimGray")]
		public Color BottomColor
		{
			get
			{
				return bottomColor;
			}
			set
			{
				bottomColor = value;
				bottomBrush.Dispose();
				bottomBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置报警灯的颜色，可以设置为任意的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		public Color LightColor
		{
			get
			{
				return lightColor;
			}
			set
			{
				lightColor = value;
				lightBrush.Dispose();
				lightBrush = new SolidBrush(value);
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置报警状态下的颜色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "Tomato")]
		public Color AlarmColor
		{
			get
			{
				return alarmColor;
			}
			set
			{
				alarmColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("获取或设置正常状态的背景色")]
		[Category("HslControls")]
		[DefaultValue(typeof(Color), "LightGray")]
		public Color NormalColor
		{
			get
			{
				return normalColor;
			}
			set
			{
				normalColor = value;
				Invalidate();
			}
		}

		[Browsable(true)]
		[Description("一旦设置本属性，启动信号灯闪烁，信号灯在正常色和报警色之间进行闪烁")]
		[Category("HslControls")]
		[DefaultValue(false)]
		public bool IsAlarm
		{
			get
			{
				return isAlarm;
			}
			set
			{
				isAlarm = value;
				if (isAlarm)
				{
					timer.Start();
					return;
				}
				timer.Stop();
				LightColor = NormalColor;
			}
		}

		[Browsable(true)]
		[Description("获取或设置报警灯闪烁的频率，单位是毫秒，默认为500")]
		[Category("HslControls")]
		[DefaultValue(500)]
		public int AlarmFlushTime
		{
			get
			{
				return timer.Interval;
			}
			set
			{
				timer.Interval = value;
			}
		}

		[Browsable(true)]
		[Description("获取或设置当前报警灯控件底部的高度信息，如果小于0，则表示百分比，如果大于1，则表示绝对像素单位")]
		[Category("HslControls")]
		[DefaultValue(0.1f)]
		public float ButtonHeight
		{
			get
			{
				return buttonHeight;
			}
			set
			{
				buttonHeight = value;
				Invalidate();
			}
		}

		public HslLanternAlarm()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			timer.Interval = 500;
			timer.Tick += Timer_Tick;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (isAlarm)
			{
				if (alarmStatus)
				{
					LightColor = AlarmColor;
				}
				else
				{
					LightColor = NormalColor;
				}
				alarmStatus = !alarmStatus;
			}
			else
			{
				LightColor = NormalColor;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			PaintHslControls(e.Graphics, base.Width, base.Height);
			base.OnPaint(e);
		}

		public void PaintHslControls(Graphics g, int width, int height)
		{
			if(false)
			{
				int num = ((buttonHeight >= 1f) ? ((int)buttonHeight) : ((int)(buttonHeight * (float)height)));
				int num2 = width * 4 / 10;
				if (height - num2 - 2 * num > 0)
				{
					g.FillRectangle(lightBrush, width / 10, num2, width * 8 / 10, height - num2 - 2 * num);
				}
				g.FillPie(lightBrush, width / 10, 1, width * 8 / 10, num2 * 2, 180, 180);
				g.FillRectangle(bottomBrush, new Rectangle(0, height - 1 - num, width, num));
				g.FillRectangle(bottomBrush, new Rectangle(width / 20, height - 1 - num * 2, width - width / 10, num));
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
			base.Name = "HslLanternAlarm";
			base.Size = new System.Drawing.Size(99, 129);
			base.ResumeLayout(false);
		}
	}
}
