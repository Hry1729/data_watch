using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace HslControls.HMI
{
	[ToolboxItem(true)]
	public class HMILedSingle : UserControl
	{
		private int m_BorderExteriorLength;

		private Color m_BorderExteriorColor = Color.Blue;

		private int m_OuterBorderLength = 5;

		private int m_InnerBorderLength = 5;

		private Color m_OuterBorderDarkColor = Color.DimGray;

		private Color m_OuterBorderLightColor = Color.White;

		private Color m_InnerBorderDarkColor = Color.DimGray;

		private Color m_InnerBorderLightColor = Color.White;

		private int m_RoundRadius = 30;

		private int m_MiddleBorderLength;

		private Color m_MiddleBorderColor = Color.Gray;

		private DAS_BorderGradientStyle m_BorderGradientType = DAS_BorderGradientStyle.BGS_Path;

		private int m_BorderGradientAngle = 225;

		private float m_BorderGradientRate = 0.5f;

		private float m_BorderGradientLightPos1 = 1f;

		private float m_BorderGradientLightPos2 = -1f;

		private float m_BorderLightIntermediateBrightness;

		private DAS_ShapeStyle m_Shape = DAS_ShapeStyle.SS_RoundRect;

		private bool m_Value = true;

		private DAS_TextPosStyle m_TextPosition;

		private bool m_TextVisible;

		private float m_GradientRate = 0.6f;

		private int m_GradientAngle = 225;

		private DAS_BkGradientStyle m_GradientType = DAS_BkGradientStyle.BKGS_Shine;

		private float m_ShinePosition = 0.5f;

		private Color m_OnColor = Color.Lime;

		private Color m_OffColor = Color.FromArgb(0, 64, 0);

		private Color m_OnGradientColor = Color.White;

		private Color m_OffGradientColor = Color.Gray;

		private int m_ArrowWidth = 20;

		private bool m_IndicatorAutoSize = true;

		private int m_IndicatorWidth = 50;

		private int m_IndicatorHeight = 50;

		private IContainer components = null;

		[Browsable(true)]
		[Description("获取或设置控件的背景色")]
		[Category("General")]
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

		[Category("General")]
		[DefaultValue("20")]
		[Description("Specify the shape width  of the indicator")]
		public int ArrowWidth
		{
			get
			{
				return m_ArrowWidth;
			}
			set
			{
				if (value >= 0)
				{
					m_ArrowWidth = value;
					Invalidate();
				}
			}
		}

		[Category("Border")]
		[DefaultValue("Color.Blue")]
		[Description("Get/Set exterior line color of control")]
		public Color BorderExteriorColor
		{
			get
			{
				return m_BorderExteriorColor;
			}
			set
			{
				m_BorderExteriorColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("0")]
		[Description("Get/Set exterior line width outside of the outer border of control")]
		public int BorderExteriorLength
		{
			get
			{
				return m_BorderExteriorLength;
			}
			set
			{
				if (value >= 0)
				{
					m_BorderExteriorLength = value;
					Invalidate();
				}
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("225")]
		[Description("Get/Set gradient angle of the border of control")]
		public int BorderGradientAngle
		{
			get
			{
				return m_BorderGradientAngle;
			}
			set
			{
				m_BorderGradientAngle = value;
				Invalidate();
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("1.0")]
		[Description("Get/Set border gradient light position 1 of control")]
		public float BorderGradientLightPos1
		{
			get
			{
				return m_BorderGradientLightPos1;
			}
			set
			{
				if (value <= 1f || (double)value >= 0.0)
				{
					m_BorderGradientLightPos1 = value;
					Invalidate();
				}
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("-1.0")]
		[Description("Get/Set border gradient light position 2 of control")]
		public float BorderGradientLightPos2
		{
			get
			{
				return m_BorderGradientLightPos2;
			}
			set
			{
				if (value <= 1f || (double)value >= -1.0)
				{
					m_BorderGradientLightPos2 = value;
					Invalidate();
				}
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("0.5")]
		[Description("Get/Set gradient rate of the border of control")]
		public float BorderGradientRate
		{
			get
			{
				return m_BorderGradientRate;
			}
			set
			{
				if (value <= 1f || (double)value >= 0.0)
				{
					m_BorderGradientRate = value;
					Invalidate();
				}
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("BGS_Path")]
		[Description("Get/Set which border gradient style is used to draw the border of control")]
		public DAS_BorderGradientStyle BorderGradientType
		{
			get
			{
				return m_BorderGradientType;
			}
			set
			{
				m_BorderGradientType = value;
				Invalidate();
			}
		}

		[Category("Border Gradient")]
		[DefaultValue("0.0")]
		[Description("Get/Set intermediate brightness between border lights of control")]
		public float BorderLightIntermediateBrightness
		{
			get
			{
				return m_BorderLightIntermediateBrightness;
			}
			set
			{
				if (value <= 1f || (double)value >= 0.0)
				{
					m_BorderLightIntermediateBrightness = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("225")]
		[Description("Get/Set gradient angle")]
		public int GradientAngle
		{
			get
			{
				return m_GradientAngle;
			}
			set
			{
				m_GradientAngle = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("0.6")]
		[Description("Get/Set gradient rate of the indicator")]
		public float GradientRate
		{
			get
			{
				return m_GradientRate;
			}
			set
			{
				if (value <= 1f || (double)value >= 0.0)
				{
					m_GradientRate = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("BKGS_Shine")]
		[Description("Get/Set gradient Type")]
		public DAS_BkGradientStyle GradientType
		{
			get
			{
				return m_GradientType;
			}
			set
			{
				m_GradientType = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("true")]
		[Description("Specify whether the indicator size can be automatically resize to the control size when the text is invisible")]
		public bool IndicatorAutoSize
		{
			get
			{
				return m_IndicatorAutoSize;
			}
			set
			{
				m_IndicatorAutoSize = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("50")]
		[Description("Set/Get the indicator height ")]
		public int IndicatorHeight
		{
			get
			{
				return m_IndicatorHeight;
			}
			set
			{
				if (value >= 0)
				{
					m_IndicatorHeight = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("50")]
		[Description("Set/Get the indicator width ")]
		public int IndicatorWidth
		{
			get
			{
				return m_IndicatorWidth;
			}
			set
			{
				if (value >= 0)
				{
					m_IndicatorWidth = value;
					Invalidate();
				}
			}
		}

		[Category("Border")]
		[DefaultValue("Color.DimGray")]
		[Description("Get/Set inner border dark color of control")]
		public Color InnerBorderDarkColor
		{
			get
			{
				return m_InnerBorderDarkColor;
			}
			set
			{
				m_InnerBorderDarkColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("5")]
		[Description("Get/Set inner border length of control")]
		public int InnerBorderLength
		{
			get
			{
				return m_InnerBorderLength;
			}
			set
			{
				if (value >= 0)
				{
					m_InnerBorderLength = value;
					Invalidate();
				}
			}
		}

		[Category("Border")]
		[DefaultValue("Color.White")]
		[Description("Get/Set Inner border light color of control")]
		public Color InnerBorderLightColor
		{
			get
			{
				return m_InnerBorderLightColor;
			}
			set
			{
				m_InnerBorderLightColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("Color.Gray")]
		[Description("Get/Set Middle Border color of control")]
		public Color MiddleBorderColor
		{
			get
			{
				return m_MiddleBorderColor;
			}
			set
			{
				m_MiddleBorderColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("0")]
		[Description("Get/Set Middle border length of control")]
		public int MiddleBorderLength
		{
			get
			{
				return m_MiddleBorderLength;
			}
			set
			{
				if (value >= 0)
				{
					m_MiddleBorderLength = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("Color.FromArgb(0, 64, 0)")]
		[Description("Specify the color of indicator when Value=false")]
		public Color OffColor
		{
			get
			{
				return m_OffColor;
			}
			set
			{
				m_OffColor = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("Color.Gray")]
		[Description("Specify the Gradient Color of indicator when Value=false")]
		public Color OffGradientColor
		{
			get
			{
				return m_OffGradientColor;
			}
			set
			{
				m_OffGradientColor = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("Color.Lime")]
		[Description("Specify the Gradient color of indicator when Value=true")]
		public Color OnColor
		{
			get
			{
				return m_OnColor;
			}
			set
			{
				m_OnColor = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("Color.White")]
		[Description("Specify the Gradient Color of indicator when Value=true")]
		public Color OnGradientColor
		{
			get
			{
				return m_OnGradientColor;
			}
			set
			{
				m_OnGradientColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("Color.DimGray")]
		[Description("Get/Set outer border dark color of control")]
		public Color OuterBorderDarkColor
		{
			get
			{
				return m_OuterBorderDarkColor;
			}
			set
			{
				m_OuterBorderDarkColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("5")]
		[Description("Get/Set outer border length of control")]
		public int OuterBorderLength
		{
			get
			{
				return m_OuterBorderLength;
			}
			set
			{
				if (value >= 0)
				{
					m_OuterBorderLength = value;
					Invalidate();
				}
			}
		}

		[Category("Border")]
		[DefaultValue("Color.White")]
		[Description("Get/Set outer border light color of control")]
		public Color OuterBorderLightColor
		{
			get
			{
				return m_OuterBorderLightColor;
			}
			set
			{
				m_OuterBorderLightColor = value;
				Invalidate();
			}
		}

		[Category("Border")]
		[DefaultValue("30")]
		[Description("Get/Set Round Radius of Round Rectangle Border of control")]
		public int RoundRadius
		{
			get
			{
				return m_RoundRadius;
			}
			set
			{
				if (value >= 0)
				{
					m_RoundRadius = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("SS_RoundRect")]
		[Description("Get/Set the indicator shape")]
		public DAS_ShapeStyle Shape
		{
			get
			{
				return m_Shape;
			}
			set
			{
				m_Shape = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("0.5")]
		[Description("Get/Set the shine position of the background gradient rendering")]
		public float ShinePosition
		{
			get
			{
				return m_ShinePosition;
			}
			set
			{
				if (value <= 1f || (double)value >= 0.0)
				{
					m_ShinePosition = value;
					Invalidate();
				}
			}
		}

		[Category("General")]
		[DefaultValue("DAS_TextPosStyle.TPS_Left")]
		[Description("Get/Set the text position")]
		public DAS_TextPosStyle TextPosition
		{
			get
			{
				return m_TextPosition;
			}
			set
			{
				m_TextPosition = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("false")]
		[Description("Get/Set the visibility of the text ")]
		public bool TextVisible
		{
			get
			{
				return m_TextVisible;
			}
			set
			{
				m_TextVisible = value;
				Invalidate();
			}
		}

		[Category("General")]
		[DefaultValue("true")]
		[Description("Get/Set the value of the indicator")]
		public bool Value
		{
			get
			{
				return m_Value;
			}
			set
			{
				m_Value = value;
				Invalidate();
			}
		}

		public HMILedSingle()
		{
			InitializeComponent();
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			try
			{
				if (base.ClientRectangle.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength) > 0 && base.ClientRectangle.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength) > 0)
				{
					DrawGraphics drawGraphics = new DrawGraphics();
					int left = base.ClientRectangle.Left;
					int top = base.ClientRectangle.Top;
					Rectangle clientRectangle = base.ClientRectangle;
					Rectangle clientRectangle2 = base.ClientRectangle;
					Rectangle rectangle = new Rectangle(left, top, clientRectangle.Width - 1, clientRectangle2.Height - 1);
					e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
					e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
					SolidBrush solidBrush = new SolidBrush(BackColor);
					Pen pen = new Pen(solidBrush);
					e.Graphics.DrawRectangle(pen, base.ClientRectangle);
					pen.Dispose();
					solidBrush.Dispose();
					StringFormat stringFormat = new StringFormat
					{
						Alignment = StringAlignment.Center,
						LineAlignment = StringAlignment.Center
					};
					if ((m_TextVisible && Text != string.Empty) || !m_IndicatorAutoSize)
					{
						int indicatorWidth = m_IndicatorWidth;
						int indicatorHeight = m_IndicatorHeight;
						if (indicatorWidth > rectangle.Width)
						{
							indicatorWidth = rectangle.Width;
						}
						if (indicatorHeight > rectangle.Height)
						{
							indicatorHeight = rectangle.Height;
						}
						rectangle.Width = indicatorWidth;
						rectangle.Height = indicatorHeight;
						if (m_TextPosition == DAS_TextPosStyle.TPS_Left)
						{
							rectangle.X = base.ClientRectangle.X + base.ClientRectangle.Width - 1 - indicatorWidth;
							rectangle.Y = base.ClientRectangle.Y + (base.ClientRectangle.Height - 1 - indicatorHeight) / 2;
							stringFormat.Alignment = StringAlignment.Near;
						}
						else if (m_TextPosition == DAS_TextPosStyle.TPS_Right)
						{
							rectangle.X = base.ClientRectangle.X;
							rectangle.Y = base.ClientRectangle.Y + (base.ClientRectangle.Height - 1 - indicatorHeight) / 2;
							stringFormat.Alignment = StringAlignment.Far;
						}
						else if (m_TextPosition == DAS_TextPosStyle.TPS_Top)
						{
							rectangle.X = base.ClientRectangle.X + (base.ClientRectangle.Width - 1 - indicatorWidth) / 2;
							rectangle.Y = base.ClientRectangle.Y + (base.ClientRectangle.Height - 1 - indicatorHeight);
							stringFormat.LineAlignment = StringAlignment.Near;
						}
						else if (m_TextPosition != DAS_TextPosStyle.TPS_Bottom)
						{
							rectangle.X = base.ClientRectangle.X + (base.ClientRectangle.Width - 1 - indicatorWidth) / 2;
							rectangle.Y = base.ClientRectangle.Y + (base.ClientRectangle.Height - 1 - indicatorHeight) / 2;
						}
						else
						{
							rectangle.X = base.ClientRectangle.X + (base.ClientRectangle.Width - 1 - indicatorWidth) / 2;
							rectangle.Y = base.ClientRectangle.Y;
							stringFormat.LineAlignment = StringAlignment.Far;
						}
					}
					Rectangle rectangle2 = new Rectangle(rectangle.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength - 1, rectangle.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength - 1, rectangle.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength - 1), rectangle.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength - 1));
					GraphicsPath graphicsPath = null;
					if (m_Shape != 0 && m_Shape != DAS_ShapeStyle.SS_RoundRect && m_Shape != DAS_ShapeStyle.SS_Circle)
					{
						graphicsPath = new GraphicsPath();
						drawGraphics.method_6(graphicsPath, rectangle, m_Shape, m_ArrowWidth + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength);
					}
					if (!m_Value)
					{
						drawGraphics.SetValue(e.Graphics, rectangle2, m_Shape, graphicsPath, m_OffColor, m_OffGradientColor, m_GradientType, m_GradientAngle, m_GradientRate, m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength - m_MiddleBorderLength - m_InnerBorderLength, m_ArrowWidth, m_ShinePosition);
					}
					else
					{
						drawGraphics.SetValue(e.Graphics, rectangle2, m_Shape, graphicsPath, m_OnColor, m_OnGradientColor, m_GradientType, m_GradientAngle, m_GradientRate, m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength - m_MiddleBorderLength - m_InnerBorderLength, m_ArrowWidth, m_ShinePosition);
					}
					graphicsPath?.Dispose();
					if (m_Shape == DAS_ShapeStyle.SS_Rect)
					{
						drawGraphics.SetRect(e.Graphics, rectangle, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
					}
					else if (m_Shape == DAS_ShapeStyle.SS_RoundRect)
					{
						Region region = new Region(base.ClientRectangle);
						GraphicsPath graphicsPath2 = new GraphicsPath();
						drawGraphics.SetRoundRect(graphicsPath2, rectangle2, m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength - m_MiddleBorderLength - m_InnerBorderLength);
						region.Exclude(graphicsPath2);
						SolidBrush solidBrush2 = new SolidBrush(BackColor);
						e.Graphics.FillRegion(solidBrush2, region);
						solidBrush2.Dispose();
						graphicsPath2.Dispose();
						region.Dispose();
						drawGraphics.SetRoundRect2(e.Graphics, rectangle, m_RoundRadius, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
					}
					else if (m_Shape != DAS_ShapeStyle.SS_Circle)
					{
						drawGraphics.SetArrow(e.Graphics, rectangle, m_Shape, m_RoundRadius, m_ArrowWidth, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
					}
					else
					{
						Region region2 = new Region(base.ClientRectangle);
						GraphicsPath graphicsPath3 = new GraphicsPath();
						graphicsPath3.AddEllipse(rectangle2);
						region2.Exclude(graphicsPath3);
						SolidBrush solidBrush3 = new SolidBrush(BackColor);
						e.Graphics.FillRegion(solidBrush3, region2);
						solidBrush3.Dispose();
						graphicsPath3.Dispose();
						region2.Dispose();
						drawGraphics.SetCircle(e.Graphics, rectangle, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
					}
					if (m_TextVisible && Text != string.Empty)
					{
						SolidBrush solidBrush4 = new SolidBrush(ForeColor);
						e.Graphics.DrawString(Text, Font, solidBrush4, base.ClientRectangle, stringFormat);
						solidBrush4.Dispose();
					}
				}
				base.OnPaint(e);
			}
			catch (ApplicationException ex)
			{
				MessageBox.Show(ex.ToString());
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
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.Transparent;
			base.Name = "HMILedSingleTwo";
			base.ResumeLayout(false);
		}
	}
}
