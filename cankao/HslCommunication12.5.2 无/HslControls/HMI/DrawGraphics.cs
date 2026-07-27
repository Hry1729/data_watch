using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HslControls.HMI
{
	public class DrawGraphics
	{
		public void SetRect(Graphics g, Rectangle rec_width, int m_OuterBorderLength, Color m_OuterBorderDarkColor, Color m_OuterBorderLightColor, int m_MiddleBorderLength, Color m_MiddleBorderColor, int m_InnerBorderLength, Color m_InnerBorderDarkColor, Color m_InnerBorderLightColor, int m_BorderExteriorLength, Color m_BorderExteriorColor, DAS_BorderGradientStyle m_BorderGradientType, int m_BorderGradientAngle, float m_BorderGradientRate, float m_BorderGradientLightPos1, float m_BorderGradientLightPos2, float m_BorderLightIntermediateBrightness)
		{
			if ((m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength) * 2 >= rec_width.Width || (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength) * 2 >= rec_width.Height)
			{
				return;
			}
			Rectangle rect = new Rectangle(rec_width.Left + m_BorderExteriorLength, rec_width.Top + m_BorderExteriorLength, rec_width.Width - 2 * m_BorderExteriorLength, rec_width.Height - 2 * m_BorderExteriorLength);
			Rectangle rect2 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength));
			Rectangle rect3 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength));
			Rectangle rect4 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength));
			switch (m_BorderGradientType)
			{
			case DAS_BorderGradientStyle.BGS_Flat:
			{
				PointF[] array = new PointF[5];
				array[0].X = rect.Left;
				array[0].Y = rect.Top;
				array[1].X = rect.Left;
				array[1].Y = rect.Top + rect.Height;
				if (rect.Width < rect.Height)
				{
					array[2].X = (float)rect.Left + (float)rect.Width / 2f;
					array[2].Y = (float)(rect.Top + rect.Height) - (float)rect.Width / 2f;
					array[3].X = (float)rect.Left + (float)rect.Width / 2f;
					array[3].Y = (float)rect.Top + (float)rect.Width / 2f;
				}
				else
				{
					array[2].X = (float)rect.Left + (float)rect.Height / 2f;
					array[2].Y = (float)rect.Top + (float)rect.Height / 2f;
					array[3].X = (float)(rect.Left + rect.Width) - (float)rect.Height / 2f;
					array[3].Y = (float)rect.Top + (float)rect.Height / 2f;
				}
				array[4].X = rect.Left + rect.Width;
				array[4].Y = rect.Top;
				GraphicsPath graphicsPath3 = new GraphicsPath();
				graphicsPath3.AddPolygon(array);
				if (m_OuterBorderLength > 0)
				{
					Region region8 = new Region(rect);
					region8.Exclude(rect2);
					SolidBrush solidBrush4 = new SolidBrush(m_OuterBorderLightColor);
					Region region9 = region8.Clone();
					region9.Intersect(graphicsPath3);
					g.FillRegion(solidBrush4, region9);
					solidBrush4.Dispose();
					region9.Dispose();
					SolidBrush solidBrush5 = new SolidBrush(m_OuterBorderDarkColor);
					Region region10 = region8.Clone();
					region10.Exclude(graphicsPath3);
					g.FillRegion(solidBrush5, region10);
					solidBrush5.Dispose();
					region10.Dispose();
					region8.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region11 = new Region(rect2);
					region11.Exclude(rect3);
					SolidBrush solidBrush6 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush6, region11);
					Pen pen4 = new Pen(solidBrush6);
					g.DrawRectangle(pen4, rect2);
					g.DrawRectangle(pen4, rect3);
					pen4.Dispose();
					solidBrush6.Dispose();
					region11.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					Region region12 = new Region(rect3);
					region12.Exclude(rect4);
					SolidBrush solidBrush7 = new SolidBrush(m_InnerBorderDarkColor);
					Region region13 = region12.Clone();
					region13.Intersect(graphicsPath3);
					g.FillRegion(solidBrush7, region13);
					solidBrush7.Dispose();
					region13.Dispose();
					SolidBrush solidBrush8 = new SolidBrush(m_InnerBorderLightColor);
					Region region14 = region12.Clone();
					region14.Exclude(graphicsPath3);
					g.FillRegion(solidBrush8, region14);
					solidBrush8.Dispose();
					region14.Dispose();
					region12.Dispose();
				}
				graphicsPath3.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Path:
			{
				Blend blend4 = new Blend();
				bool flag2 = false;
				if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag2 = true;
					blend4.Factors = new float[3];
					blend4.Positions = new float[3];
					blend4.Factors[0] = 0f;
					blend4.Positions[0] = 0f;
					blend4.Factors[1] = m_BorderGradientRate;
					blend4.Positions[1] = 1f - m_BorderGradientRate;
					blend4.Factors[2] = 1f;
					blend4.Positions[2] = 1f;
				}
				if (m_OuterBorderLength > 0)
				{
					double num2 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0;
					GraphicsPath graphicsPath4 = new GraphicsPath();
					graphicsPath4.AddPolygon(new Point[4]
					{
						new Point(rect.Left - 1, rect.Top - 1),
						new Point(rect.Left + rect.Width + 1, rect.Top - 1),
						new Point(rect.Left + rect.Width + 1, rect.Top + rect.Height + 1),
						new Point(rect.Left - 1, rect.Top + rect.Height + 1)
					});
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath4);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					PathGradientBrush pathGradientBrush4 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect.Left + (float)rect.Width / 2f;
					focusScales.Y = (float)rect.Top + (float)rect.Height / 2f;
					PointF centerPoint = focusScales;
					float num4;
					float num3;
					if (Math.Abs(Math.Sin(num2)) < 1E-10)
					{
						num3 = 0f;
						num4 = (float)(Math.Cos(num2) * (double)(rect.Width - m_OuterBorderLength) / 2.0);
					}
					else if (Math.Abs(Math.Cos(num2)) >= 1E-10)
					{
						num4 = (float)((double)(rect.Height - m_OuterBorderLength) / (2.0 * Math.Tan(num2)));
						num3 = (float)(Math.Tan(num2) * (double)(rect.Width - m_OuterBorderLength) / 2.0);
						num4 = ((Math.Cos(num2) >= 0.0) ? Math.Abs(num4) : (0f - Math.Abs(num4)));
						num3 = ((Math.Sin(num2) >= 0.0) ? Math.Abs(num3) : (0f - Math.Abs(num3)));
						if (num4 < 0f - (float)(rect.Width - m_OuterBorderLength) / 2f)
						{
							num4 = 0f - (float)(rect.Width - m_OuterBorderLength) / 2f;
						}
						else if (num4 > (float)(rect.Width - m_OuterBorderLength) / 2f)
						{
							num4 = (float)(rect.Width - m_OuterBorderLength) / 2f;
						}
						if (num3 < 0f - (float)(rect.Height - m_OuterBorderLength) / 2f)
						{
							num3 = 0f - (float)(rect.Height - m_OuterBorderLength) / 2f;
						}
						else if (num3 > (float)(rect.Height - m_OuterBorderLength) / 2f)
						{
							num3 = (float)(rect.Height - m_OuterBorderLength) / 2f;
						}
					}
					else
					{
						num4 = 0f;
						num3 = (float)(Math.Sin(num2) * (double)(rect.Height - m_OuterBorderLength) / 2.0);
					}
					centerPoint.X += num4;
					centerPoint.Y += num3;
					if (flag2)
					{
						pathGradientBrush4.Blend = blend4;
					}
					pathGradientBrush4.CenterPoint = centerPoint;
					pathGradientBrush4.CenterColor = m_OuterBorderLightColor;
					Region region15 = new Region(rect);
					region15.Exclude(rect2);
					g.FillRegion(pathGradientBrush4, region15);
					pathGradientBrush4.Dispose();
					region15.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region16 = new Region(rect2);
					region16.Exclude(rect3);
					SolidBrush solidBrush9 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush9, region16);
					Pen pen5 = new Pen(solidBrush9);
					g.DrawRectangle(pen5, rect2);
					g.DrawRectangle(pen5, rect3);
					pen5.Dispose();
					solidBrush9.Dispose();
					region16.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					double num5 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0 + 3.14159265358979;
					GraphicsPath graphicsPath5 = new GraphicsPath();
					graphicsPath5.AddPolygon(new Point[4]
					{
						new Point(rect3.Left, rect3.Top),
						new Point(rect3.Left + rect3.Width, rect3.Top),
						new Point(rect3.Left + rect3.Width, rect3.Top + rect3.Height),
						new Point(rect3.Left, rect3.Top + rect3.Height)
					});
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath5);
					pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderDarkColor };
					PathGradientBrush pathGradientBrush5 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect3.Left + (float)rect3.Width / 2f;
					focusScales.Y = (float)rect3.Top + (float)rect3.Height / 2f;
					PointF centerPoint2 = focusScales;
					float num7;
					float num6;
					if (Math.Abs(Math.Sin(num5)) < 1E-10)
					{
						num6 = 0f;
						num7 = (float)(Math.Cos(num5) * (double)(rect3.Width - m_InnerBorderLength) / 2.0);
					}
					else if (Math.Abs(Math.Cos(num5)) >= 1E-10)
					{
						num7 = (float)((double)(rect3.Height - m_InnerBorderLength) / (2.0 * Math.Tan(num5)));
						num6 = (float)(Math.Tan(num5) * (double)(rect3.Width - m_InnerBorderLength) / 2.0);
						num7 = ((Math.Cos(num5) >= 0.0) ? Math.Abs(num7) : (0f - Math.Abs(num7)));
						num6 = ((Math.Sin(num5) >= 0.0) ? Math.Abs(num6) : (0f - Math.Abs(num6)));
						if (num7 < 0f - (float)(rect3.Width - m_InnerBorderLength) / 2f)
						{
							num7 = 0f - (float)(rect3.Width - m_InnerBorderLength) / 2f;
						}
						else if (num7 > (float)(rect3.Width - m_InnerBorderLength) / 2f)
						{
							num7 = (float)(rect3.Width - m_InnerBorderLength) / 2f;
						}
						if (num6 < 0f - (float)(rect3.Height - m_InnerBorderLength) / 2f)
						{
							num6 = 0f - (float)(rect3.Height - m_InnerBorderLength) / 2f;
						}
						else if (num6 > (float)(rect3.Height - m_InnerBorderLength) / 2f)
						{
							num6 = (float)(rect3.Height - m_InnerBorderLength) / 2f;
						}
					}
					else
					{
						num7 = 0f;
						num6 = (float)(Math.Sin(num5) * (double)(rect3.Height - m_InnerBorderLength) / 2.0);
					}
					centerPoint2.X += num7;
					centerPoint2.Y += num6;
					if (flag2)
					{
						pathGradientBrush5.Blend = blend4;
					}
					pathGradientBrush5.CenterPoint = centerPoint2;
					pathGradientBrush5.CenterColor = m_InnerBorderLightColor;
					Region region17 = new Region(rect3);
					region17.Exclude(rect4);
					g.FillRegion(pathGradientBrush5, region17);
					pathGradientBrush5.Dispose();
					region17.Dispose();
					if (m_BorderExteriorLength > 0)
					{
						SolidBrush solidBrush3 = new SolidBrush(m_BorderExteriorColor);
						Region region7 = new Region(rec_width);
						region7.Exclude(rect);
						g.FillRegion(solidBrush3, region7);
						Pen pen3 = new Pen(solidBrush3, m_BorderExteriorLength);
						g.DrawRectangle(pen3, rec_width);
						g.DrawRectangle(pen3, rect);
						pen3.Dispose();
						solidBrush3.Dispose();
						region7.Dispose();
					}
				}
				else if (m_BorderExteriorLength > 0)
				{
					SolidBrush solidBrush3 = new SolidBrush(m_BorderExteriorColor);
					Region region7 = new Region(rec_width);
					region7.Exclude(rect);
					g.FillRegion(solidBrush3, region7);
					Pen pen3 = new Pen(solidBrush3, m_BorderExteriorLength);
					g.DrawRectangle(pen3, rec_width);
					g.DrawRectangle(pen3, rect);
					pen3.Dispose();
					solidBrush3.Dispose();
					region7.Dispose();
				}
				return;
			}
			default:
				if (m_BorderExteriorLength > 0)
				{
					SolidBrush solidBrush3 = new SolidBrush(m_BorderExteriorColor);
					Region region7 = new Region(rec_width);
					region7.Exclude(rect);
					g.FillRegion(solidBrush3, region7);
					Pen pen3 = new Pen(solidBrush3, m_BorderExteriorLength);
					g.DrawRectangle(pen3, rec_width);
					g.DrawRectangle(pen3, rect);
					pen3.Dispose();
					solidBrush3.Dispose();
					region7.Dispose();
				}
				return;
			case DAS_BorderGradientStyle.BGS_Linear:
			case DAS_BorderGradientStyle.BGS_Linear2:
			{
				Blend blend3 = new Blend();
				bool flag = false;
				float num = 0f;
				if (m_BorderLightIntermediateBrightness >= 0f && (double)m_BorderLightIntermediateBrightness <= 1.0)
				{
					num = m_BorderLightIntermediateBrightness;
				}
				if (m_BorderGradientLightPos1 > 0f && (double)m_BorderGradientLightPos1 < 1.0)
				{
					flag = true;
					if (m_BorderGradientLightPos2 <= 0f || (double)m_BorderGradientLightPos2 > 1.0)
					{
						blend3.Factors = new float[5];
						blend3.Positions = new float[5];
						blend3.Factors[0] = 0f;
						blend3.Positions[0] = 0f;
						blend3.Factors[1] = m_BorderGradientRate;
						blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
						blend3.Factors[2] = 1f;
						blend3.Positions[2] = m_BorderGradientLightPos1;
						blend3.Factors[3] = m_BorderGradientRate;
						blend3.Positions[3] = (1f - m_BorderGradientLightPos1) * m_BorderGradientRate + m_BorderGradientLightPos1;
						blend3.Factors[4] = 0f;
						blend3.Positions[4] = 1f;
					}
					else
					{
						blend3.Factors = new float[9];
						blend3.Positions = new float[9];
						if (m_BorderGradientLightPos1 <= m_BorderGradientLightPos2)
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos1;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos1 + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos2;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos2 + (1f - m_BorderGradientLightPos2) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
							if (m_BorderGradientLightPos1 == m_BorderGradientLightPos2)
							{
								blend3.Factors[3] = 1f;
								blend3.Factors[4] = 1f;
								blend3.Factors[5] = 1f;
							}
							if (m_BorderGradientLightPos2 == 1f)
							{
								blend3.Factors[7] = 1f;
								blend3.Factors[8] = 1f;
							}
						}
						else
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos2 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos2;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos2 + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos1;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos1 + (1f - m_BorderGradientLightPos1) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
						}
					}
				}
				else if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag = true;
					blend3.Factors = new float[3];
					blend3.Positions = new float[3];
					blend3.Factors[0] = 0f;
					blend3.Positions[0] = 0f;
					blend3.Factors[1] = m_BorderGradientRate;
					blend3.Positions[1] = 1f - m_BorderGradientRate;
					blend3.Factors[2] = 1f;
					blend3.Positions[2] = 1f;
				}
				if (m_OuterBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(rect.Left - 1, rect.Top - 1, rect.Width + 2, rect.Height + 2), m_OuterBorderDarkColor, m_OuterBorderLightColor, m_BorderGradientAngle);
					if (flag)
					{
						linearGradientBrush.Blend = blend3;
					}
					Region region4 = new Region(rect);
					region4.Exclude(rect2);
					g.FillRegion(linearGradientBrush, region4);
					linearGradientBrush.Dispose();
					region4.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region5 = new Region(rect2);
					region5.Exclude(rect3);
					SolidBrush solidBrush2 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush2, region5);
					Pen pen2 = new Pen(solidBrush2);
					g.DrawRectangle(pen2, rect2);
					g.DrawRectangle(pen2, rect3);
					pen2.Dispose();
					solidBrush2.Dispose();
					region5.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush2 = ((m_BorderGradientType != DAS_BorderGradientStyle.BGS_Linear) ? new LinearGradientBrush(rect3, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderGradientAngle + 180) : new LinearGradientBrush(rect3, m_InnerBorderLightColor, m_InnerBorderDarkColor, m_BorderGradientAngle));
					if (flag)
					{
						linearGradientBrush2.Blend = blend3;
					}
					Region region6 = new Region(rect3);
					region6.Exclude(rect4);
					g.FillRegion(linearGradientBrush2, region6);
					linearGradientBrush2.Dispose();
					region6.Dispose();
				}
				break;
			}
			case DAS_BorderGradientStyle.BGS_Ring:
			{
				Blend blend = new Blend();
				blend.Factors = new float[3];
				blend.Positions = new float[3];
				Blend blend2 = blend;
				blend2.Factors[0] = 0f;
				blend2.Positions[0] = 0f;
				blend2.Factors[2] = 1f;
				blend2.Positions[2] = 1f;
				if (m_OuterBorderLength > 0)
				{
					blend2.Factors[1] = m_BorderGradientRate;
					blend2.Positions[1] = 1f - m_BorderGradientRate;
					GraphicsPath graphicsPath = new GraphicsPath();
					graphicsPath.AddPolygon(new Point[4]
					{
						new Point(rect.Left, rect.Top),
						new Point(rect.Left + rect.Width, rect.Top),
						new Point(rect.Left + rect.Width, rect.Top + rect.Height),
						new Point(rect.Left, rect.Top + rect.Height)
					});
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					pathGradientBrush.CenterColor = m_OuterBorderLightColor;
					PathGradientBrush pathGradientBrush2 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect2.Width / (float)rect.Width;
					focusScales.Y = (float)rect2.Height / (float)rect.Height;
					pathGradientBrush2.FocusScales = focusScales;
					Region region = new Region(rect);
					region.Exclude(rect2);
					g.FillRegion(pathGradientBrush2, region);
					pathGradientBrush2.Dispose();
					region.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region2 = new Region(rect2);
					region2.Exclude(rect3);
					SolidBrush solidBrush = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush, region2);
					Pen pen = new Pen(solidBrush);
					g.DrawRectangle(pen, rect2);
					g.DrawRectangle(pen, rect3);
					pen.Dispose();
					solidBrush.Dispose();
					region2.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					blend2.Factors[1] = 1f - m_BorderGradientRate;
					blend2.Positions[1] = m_BorderGradientRate;
					GraphicsPath graphicsPath2 = new GraphicsPath();
					graphicsPath2.AddPolygon(new Point[4]
					{
						new Point(rect3.Left, rect3.Top),
						new Point(rect3.Left + rect3.Width, rect3.Top),
						new Point(rect3.Left + rect3.Width, rect3.Top + rect3.Height),
						new Point(rect3.Left, rect3.Top + rect3.Height)
					});
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath2);
					pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderLightColor };
					pathGradientBrush.Blend = blend2;
					pathGradientBrush.CenterColor = m_InnerBorderDarkColor;
					PathGradientBrush pathGradientBrush3 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect4.Width / (float)rect3.Width;
					focusScales.Y = (float)rect4.Height / (float)rect3.Height;
					pathGradientBrush3.FocusScales = focusScales;
					Region region3 = new Region(rect3);
					region3.Exclude(rect4);
					g.FillRegion(pathGradientBrush3, region3);
					pathGradientBrush3.Dispose();
					region3.Dispose();
				}
				break;
			}
			}
			if (m_BorderExteriorLength > 0)
			{
				SolidBrush solidBrush3 = new SolidBrush(m_BorderExteriorColor);
				Region region7 = new Region(rec_width);
				region7.Exclude(rect);
				g.FillRegion(solidBrush3, region7);
				Pen pen3 = new Pen(solidBrush3, m_BorderExteriorLength);
				g.DrawRectangle(pen3, rec_width);
				g.DrawRectangle(pen3, rect);
				pen3.Dispose();
				solidBrush3.Dispose();
				region7.Dispose();
			}
		}

		public void SetCircle(Graphics graphics_0, Rectangle rec, int int_0, Color color_0, Color color_1, int int_1, Color color_2, int int_2, Color color_3, Color color_4, int int_3, Color color_5, DAS_BorderGradientStyle das_BorderGradientStyle_0, long long_0, float float_0, float float_1, float float_2, float float_3)
		{
			Rectangle rect = new Rectangle(rec.Left + int_3, rec.Top + int_3, rec.Width - 2 * int_3, rec.Height - 2 * int_3);
			Rectangle rect2 = new Rectangle(rec.Left + int_3 + int_0, rec.Top + int_3 + int_0, rec.Width - 2 * (int_3 + int_0), rec.Height - 2 * (int_3 + int_0));
			Rectangle rect3 = new Rectangle(rec.Left + int_3 + int_0 + int_1, rec.Top + int_3 + int_0 + int_1, rec.Width - 2 * (int_3 + int_0 + int_1), rec.Height - 2 * (int_3 + int_0 + int_1));
			Rectangle rect4 = new Rectangle(rec.Left + int_3 + int_0 + int_1 + int_2, rec.Top + int_3 + int_0 + int_1 + int_2, rec.Width - 2 * (int_3 + int_0 + int_1 + int_2), rec.Height - 2 * (int_3 + int_0 + int_1 + int_2));
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(rect);
			GraphicsPath graphicsPath2 = new GraphicsPath();
			graphicsPath2.AddEllipse(rect2);
			GraphicsPath graphicsPath3 = new GraphicsPath();
			graphicsPath3.AddEllipse(rect3);
			GraphicsPath graphicsPath4 = new GraphicsPath();
			graphicsPath4.AddEllipse(rect4);
			switch (das_BorderGradientStyle_0)
			{
			case DAS_BorderGradientStyle.BGS_Flat:
			{
				PointF[] array = new PointF[5];
				array[0].X = rect.Left;
				array[0].Y = rect.Top;
				array[1].X = rect.Left;
				array[1].Y = rect.Top + rect.Height;
				if (rect.Width < rect.Height)
				{
					array[2].X = (float)rect.Left + (float)rect.Width / 2f;
					array[2].Y = (float)(rect.Top + rect.Height) - (float)rect.Width / 2f;
					array[3].X = (float)rect.Left + (float)rect.Width / 2f;
					array[3].Y = (float)rect.Top + (float)rect.Width / 2f;
				}
				else
				{
					array[2].X = (float)rect.Left + (float)rect.Height / 2f;
					array[2].Y = (float)rect.Top + (float)rect.Height / 2f;
					array[3].X = (float)(rect.Left + rect.Width) - (float)rect.Height / 2f;
					array[3].Y = (float)rect.Top + (float)rect.Height / 2f;
				}
				array[4].X = rect.Left + rect.Width;
				array[4].Y = rect.Top;
				GraphicsPath graphicsPath5 = new GraphicsPath();
				graphicsPath5.AddPolygon(array);
				if (int_0 > 0)
				{
					Region region6 = new Region(graphicsPath);
					region6.Exclude(graphicsPath2);
					SolidBrush solidBrush2 = new SolidBrush(color_1);
					Region region7 = region6.Clone();
					region7.Intersect(graphicsPath5);
					graphics_0.FillRegion(solidBrush2, region7);
					graphics_0.SetClip(graphicsPath5);
					Pen pen4 = new Pen(solidBrush2);
					graphics_0.DrawEllipse(pen4, rect);
					graphics_0.DrawEllipse(pen4, rect3);
					pen4.Dispose();
					graphics_0.ResetClip();
					solidBrush2.Dispose();
					region7.Dispose();
					SolidBrush solidBrush3 = new SolidBrush(color_0);
					Region region8 = region6.Clone();
					region8.Exclude(graphicsPath5);
					graphics_0.FillRegion(solidBrush3, region8);
					graphics_0.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen5 = new Pen(solidBrush3);
					graphics_0.DrawEllipse(pen5, rect);
					graphics_0.DrawEllipse(pen5, rect3);
					pen5.Dispose();
					graphics_0.ResetClip();
					solidBrush3.Dispose();
					region8.Dispose();
					region6.Dispose();
				}
				if (int_1 > 0)
				{
					Region region9 = new Region(graphicsPath2);
					region9.Exclude(graphicsPath3);
					SolidBrush solidBrush4 = new SolidBrush(color_2);
					graphics_0.FillRegion(solidBrush4, region9);
					Pen pen6 = new Pen(solidBrush4);
					graphics_0.DrawPath(pen6, graphicsPath2);
					graphics_0.DrawPath(pen6, graphicsPath3);
					pen6.Dispose();
					solidBrush4.Dispose();
					region9.Dispose();
				}
				if (int_2 > 0)
				{
					Region region10 = new Region(graphicsPath3);
					region10.Exclude(graphicsPath4);
					SolidBrush solidBrush5 = new SolidBrush(color_3);
					Region region11 = region10.Clone();
					region11.Intersect(graphicsPath5);
					graphics_0.FillRegion(solidBrush5, region11);
					graphics_0.SetClip(graphicsPath5);
					Pen pen7 = new Pen(solidBrush5);
					graphics_0.DrawEllipse(pen7, rect3);
					graphics_0.DrawEllipse(pen7, rect4);
					pen7.Dispose();
					graphics_0.ResetClip();
					solidBrush5.Dispose();
					region11.Dispose();
					SolidBrush solidBrush6 = new SolidBrush(color_4);
					Region region12 = region10.Clone();
					region12.Exclude(graphicsPath5);
					graphics_0.FillRegion(solidBrush6, region12);
					graphics_0.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen8 = new Pen(solidBrush6);
					graphics_0.DrawEllipse(pen8, rect3);
					graphics_0.DrawEllipse(pen8, rect4);
					pen8.Dispose();
					graphics_0.ResetClip();
					solidBrush6.Dispose();
					region12.Dispose();
					region10.Dispose();
				}
				graphicsPath5.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Path:
			{
				Blend blend4 = new Blend();
				bool flag2 = false;
				if ((double)float_0 < 0.49999999999 || (double)float_0 > 0.5000000000001)
				{
					flag2 = true;
					blend4.Factors = new float[3];
					blend4.Positions = new float[3];
					blend4.Factors[0] = 0f;
					blend4.Positions[0] = 0f;
					blend4.Factors[1] = float_0;
					blend4.Positions[1] = 1f - float_0;
					blend4.Factors[2] = 1f;
					blend4.Positions[2] = 1f;
				}
				if (int_0 > 0)
				{
					double num2 = 3.14159265358979 * (double)long_0 / 180.0;
					GraphicsPath graphicsPath6 = new GraphicsPath();
					graphicsPath6.AddEllipse(rect);
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath6);
					pathGradientBrush.SurroundColors = new Color[1] { color_0 };
					PathGradientBrush pathGradientBrush4 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect.Left + (float)rect.Width / 2f;
					focusScales.Y = (float)rect.Top + (float)rect.Height / 2f;
					PointF centerPoint = focusScales;
					float num3 = (float)((double)((float)rect.Width - (float)int_0 * float_1) * Math.Cos(num2) / 2.0);
					float num4 = (float)((double)((float)rect.Height - (float)int_0 * float_1) * Math.Sin(num2) / 2.0);
					centerPoint.X += num3;
					centerPoint.Y += num4;
					if (flag2)
					{
						pathGradientBrush4.Blend = blend4;
					}
					pathGradientBrush4.CenterPoint = centerPoint;
					pathGradientBrush4.CenterColor = color_1;
					Region region18 = new Region(graphicsPath);
					Region region19 = new Region(graphicsPath2);
					region18.Exclude(region19);
					graphics_0.FillRegion(pathGradientBrush4, region18);
					Pen pen12 = new Pen(color_0);
					graphics_0.DrawPath(pen12, graphicsPath);
					pen12.Dispose();
					pathGradientBrush4.Dispose();
					region18.Dispose();
					region19.Dispose();
				}
				if (int_1 > 0)
				{
					Region region20 = new Region(graphicsPath2);
					region20.Exclude(graphicsPath3);
					SolidBrush solidBrush8 = new SolidBrush(color_2);
					graphics_0.FillRegion(solidBrush8, region20);
					Pen pen13 = new Pen(solidBrush8);
					graphics_0.DrawPath(pen13, graphicsPath2);
					graphics_0.DrawPath(pen13, graphicsPath3);
					pen13.Dispose();
					solidBrush8.Dispose();
					region20.Dispose();
				}
				if (int_2 > 0)
				{
					double num5 = 3.14159265358979 * (double)long_0 / 180.0 + 3.14159265358979;
					GraphicsPath graphicsPath7 = new GraphicsPath();
					graphicsPath7.AddEllipse(rect3);
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath7);
					pathGradientBrush.SurroundColors = new Color[1] { color_3 };
					PathGradientBrush pathGradientBrush5 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect3.Left + (float)rect3.Width / 2f;
					focusScales.Y = (float)rect3.Top + (float)rect3.Height / 2f;
					PointF centerPoint2 = focusScales;
					float num6 = (float)((double)((float)rect3.Width - (float)int_2 * (1f - float_1)) * Math.Cos(num5) / 2.0);
					float num7 = (float)((double)((float)rect3.Height - (float)int_2 * (1f - float_1)) * Math.Sin(num5) / 2.0);
					centerPoint2.X += num6;
					centerPoint2.Y += num7;
					if (flag2)
					{
						pathGradientBrush5.Blend = blend4;
					}
					pathGradientBrush5.CenterPoint = centerPoint2;
					pathGradientBrush5.CenterColor = color_4;
					Region region21 = new Region(graphicsPath3);
					Region region22 = new Region(graphicsPath4);
					region21.Exclude(region22);
					graphics_0.FillRegion(pathGradientBrush5, region21);
					Pen pen14 = new Pen(color_3);
					graphics_0.DrawPath(pen14, graphicsPath3);
					graphics_0.DrawPath(pen14, graphicsPath4);
					pen14.Dispose();
					pathGradientBrush5.Dispose();
					region21.Dispose();
					region22.Dispose();
				}
				break;
			}
			case DAS_BorderGradientStyle.BGS_Linear:
			case DAS_BorderGradientStyle.BGS_Linear2:
			{
				Blend blend3 = new Blend();
				bool flag = false;
				float num = 0f;
				if (float_3 >= 0f && (double)float_3 <= 1.0)
				{
					num = float_3;
				}
				if (float_1 > 0f && (double)float_1 < 1.0)
				{
					flag = true;
					if (float_2 <= 0f || (double)float_2 > 1.0)
					{
						blend3.Factors = new float[5];
						blend3.Positions = new float[5];
						blend3.Factors[0] = 0f;
						blend3.Positions[0] = 0f;
						blend3.Factors[1] = float_0;
						blend3.Positions[1] = float_1 * (1f - float_0);
						blend3.Factors[2] = 1f;
						blend3.Positions[2] = float_1;
						blend3.Factors[3] = float_0;
						blend3.Positions[3] = (1f - float_1) * float_0 + float_1;
						blend3.Factors[4] = 0f;
						blend3.Positions[4] = 1f;
					}
					else
					{
						blend3.Factors = new float[9];
						blend3.Positions = new float[9];
						if (float_1 <= float_2)
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = float_0;
							blend3.Positions[1] = float_1 * (1f - float_0);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = float_1;
							blend3.Factors[3] = (1f - num) * float_0 + num;
							blend3.Positions[3] = float_1 + (float_2 - float_1) / 2f * float_0;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (float_1 + float_2) / 2f;
							blend3.Factors[5] = (1f - num) * float_0 + num;
							blend3.Positions[5] = (float_1 + float_2) / 2f + (float_2 - float_1) / 2f * (1f - float_0);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = float_2;
							blend3.Factors[7] = float_0;
							blend3.Positions[7] = float_2 + (1f - float_2) * float_0;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
							if (float_1 == float_2)
							{
								blend3.Factors[3] = 1f;
								blend3.Factors[4] = 1f;
								blend3.Factors[5] = 1f;
							}
							if (float_2 == 1f)
							{
								blend3.Factors[7] = 1f;
								blend3.Factors[8] = 1f;
							}
						}
						else
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = float_0;
							blend3.Positions[1] = float_2 * (1f - float_0);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = float_2;
							blend3.Factors[3] = (1f - num) * float_0 + num;
							blend3.Positions[3] = float_2 + (float_1 - float_2) / 2f * float_0;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (float_1 + float_2) / 2f;
							blend3.Factors[5] = (1f - num) * float_0 + num;
							blend3.Positions[5] = (float_1 + float_2) / 2f + (float_1 - float_2) / 2f * (1f - float_0);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = float_1;
							blend3.Factors[7] = float_0;
							blend3.Positions[7] = float_1 + (1f - float_1) * float_0;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
						}
					}
				}
				else if ((double)float_0 < 0.49999999999 || (double)float_0 > 0.5000000000001)
				{
					flag = true;
					blend3.Factors = new float[3];
					blend3.Positions = new float[3];
					blend3.Factors[0] = 0f;
					blend3.Positions[0] = 0f;
					blend3.Factors[1] = float_0;
					blend3.Positions[1] = 1f - float_0;
					blend3.Factors[2] = 1f;
					blend3.Positions[2] = 1f;
				}
				if (int_0 > 0)
				{
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(rect.Left - 1, rect.Top - 1, rect.Width + 2, rect.Height + 2), color_0, color_1, long_0);
					if (flag)
					{
						linearGradientBrush.Blend = blend3;
					}
					Region region13 = new Region(graphicsPath);
					Region region14 = new Region(graphicsPath2);
					region13.Exclude(region14);
					graphics_0.FillRegion(linearGradientBrush, region13);
					Pen pen9 = new Pen(linearGradientBrush);
					graphics_0.DrawPath(pen9, graphicsPath);
					graphics_0.DrawPath(pen9, graphicsPath2);
					pen9.Dispose();
					linearGradientBrush.Dispose();
					region13.Dispose();
					region14.Dispose();
				}
				if (int_1 > 0)
				{
					Region region15 = new Region(graphicsPath2);
					region15.Exclude(graphicsPath3);
					SolidBrush solidBrush7 = new SolidBrush(color_2);
					graphics_0.FillRegion(solidBrush7, region15);
					Pen pen10 = new Pen(solidBrush7);
					graphics_0.DrawPath(pen10, graphicsPath2);
					graphics_0.DrawPath(pen10, graphicsPath3);
					pen10.Dispose();
					solidBrush7.Dispose();
					region15.Dispose();
				}
				if (int_2 > 0)
				{
					LinearGradientBrush linearGradientBrush2 = ((das_BorderGradientStyle_0 != DAS_BorderGradientStyle.BGS_Linear) ? new LinearGradientBrush(rect3, color_3, color_4, long_0 + 180) : new LinearGradientBrush(rect3, color_4, color_3, long_0));
					if (flag)
					{
						linearGradientBrush2.Blend = blend3;
					}
					Region region16 = new Region(graphicsPath3);
					Region region17 = new Region(graphicsPath4);
					region16.Exclude(region17);
					graphics_0.FillRegion(linearGradientBrush2, region16);
					Pen pen11 = new Pen(linearGradientBrush2);
					graphics_0.DrawPath(pen11, graphicsPath3);
					graphics_0.DrawPath(pen11, graphicsPath4);
					pen11.Dispose();
					linearGradientBrush2.Dispose();
					region16.Dispose();
					region17.Dispose();
				}
				break;
			}
			case DAS_BorderGradientStyle.BGS_Ring:
			{
				Blend blend = new Blend();
				blend.Factors = new float[3];
				blend.Positions = new float[3];
				Blend blend2 = blend;
				blend2.Factors[0] = 0f;
				blend2.Positions[0] = 0f;
				blend2.Factors[2] = 1f;
				blend2.Positions[2] = 1f;
				if (int_0 > 0)
				{
					blend2.Factors[1] = float_0;
					blend2.Positions[1] = 1f - float_0;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
					pathGradientBrush.SurroundColors = new Color[1] { color_0 };
					pathGradientBrush.CenterColor = color_1;
					PathGradientBrush pathGradientBrush2 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect2.Width / (float)rect.Width;
					focusScales.Y = (float)rect2.Height / (float)rect.Height;
					pathGradientBrush2.FocusScales = focusScales;
					Region region = new Region(graphicsPath);
					Region region2 = new Region(graphicsPath2);
					region.Exclude(region2);
					graphics_0.FillRegion(pathGradientBrush2, region);
					Pen pen = new Pen(color_0);
					graphics_0.DrawPath(pen, graphicsPath);
					pen.Dispose();
					pathGradientBrush2.Dispose();
					region.Dispose();
					region2.Dispose();
				}
				if (int_1 > 0)
				{
					Region region3 = new Region(graphicsPath2);
					region3.Exclude(graphicsPath3);
					SolidBrush solidBrush = new SolidBrush(color_2);
					graphics_0.FillRegion(solidBrush, region3);
					Pen pen2 = new Pen(solidBrush);
					graphics_0.DrawPath(pen2, graphicsPath2);
					graphics_0.DrawPath(pen2, graphicsPath3);
					pen2.Dispose();
					solidBrush.Dispose();
					region3.Dispose();
				}
				if (int_2 > 0)
				{
					blend2.Factors[1] = 1f - float_0;
					blend2.Positions[1] = float_0;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath3);
					pathGradientBrush.SurroundColors = new Color[1] { color_4 };
					pathGradientBrush.Blend = blend2;
					pathGradientBrush.CenterColor = color_3;
					PathGradientBrush pathGradientBrush3 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rect4.Width / (float)rect3.Width;
					focusScales.Y = (float)rect4.Height / (float)rect3.Height;
					pathGradientBrush3.FocusScales = focusScales;
					Region region4 = new Region(graphicsPath3);
					Region region5 = new Region(graphicsPath4);
					region4.Exclude(region5);
					graphics_0.FillRegion(pathGradientBrush3, region4);
					Pen pen3 = new Pen(color_3);
					graphics_0.DrawPath(pen3, graphicsPath4);
					pen3.Dispose();
					pathGradientBrush3.Dispose();
					region4.Dispose();
					region5.Dispose();
				}
				break;
			}
			}
			if (int_3 > 0)
			{
				SolidBrush solidBrush9 = new SolidBrush(color_5);
				GraphicsPath graphicsPath8 = new GraphicsPath();
				graphicsPath8.AddEllipse(rec);
				Region region23 = new Region(graphicsPath8);
				region23.Exclude(graphicsPath);
				graphics_0.FillRegion(solidBrush9, region23);
				Pen pen15 = new Pen(solidBrush9);
				graphics_0.DrawPath(pen15, graphicsPath8);
				graphics_0.DrawPath(pen15, graphicsPath);
				pen15.Dispose();
				solidBrush9.Dispose();
				region23.Dispose();
				graphicsPath8.Dispose();
			}
			graphicsPath4.Dispose();
			graphicsPath3.Dispose();
			graphicsPath.Dispose();
		}

		public void SetRoundRect2(Graphics g, Rectangle rec_width, int m_RoundRadius, int m_OuterBorderLength, Color m_OuterBorderDarkColor, Color m_OuterBorderLightColor, int m_MiddleBorderLength, Color m_MiddleBorderColor, int m_InnerBorderLength, Color m_InnerBorderDarkColor, Color m_InnerBorderLightColor, int m_BorderExteriorLength, Color m_BorderExteriorColor, DAS_BorderGradientStyle m_BorderGradientType, long m_BorderGradientAngle, float m_BorderGradientRate, float m_BorderGradientLightPos1, float m_BorderGradientLightPos2, float m_BorderLightIntermediateBrightness)
		{
			Rectangle rec = new Rectangle(rec_width.Left + m_BorderExteriorLength, rec_width.Top + m_BorderExteriorLength, rec_width.Width - 2 * m_BorderExteriorLength, rec_width.Height - 2 * m_BorderExteriorLength);
			Rectangle rec2 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength));
			Rectangle rectangle = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength));
			Rectangle rec3 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength));
			int roundRadius = m_RoundRadius - m_BorderExteriorLength;
			GraphicsPath graphicsPath = new GraphicsPath();
			SetRoundRect(graphicsPath, rec, roundRadius);
			roundRadius = m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength;
			GraphicsPath graphicsPath2 = new GraphicsPath();
			SetRoundRect(graphicsPath2, rec2, roundRadius);
			roundRadius = m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength - m_MiddleBorderLength;
			GraphicsPath graphicsPath3 = new GraphicsPath();
			SetRoundRect(graphicsPath3, rectangle, roundRadius);
			roundRadius = m_RoundRadius - m_BorderExteriorLength - m_OuterBorderLength - m_MiddleBorderLength - m_InnerBorderLength;
			GraphicsPath graphicsPath4 = new GraphicsPath();
			SetRoundRect(graphicsPath4, rec3, roundRadius);
			switch (m_BorderGradientType)
			{
			case DAS_BorderGradientStyle.BGS_Flat:
			{
				PointF[] array = new PointF[5];
				array[0].X = rec.Left;
				array[0].Y = rec.Top;
				array[1].X = rec.Left;
				array[1].Y = rec.Top + rec.Height;
				if (rec.Width < rec.Height)
				{
					array[2].X = (float)rec.Left + (float)rec.Width / 2f;
					array[2].Y = (float)(rec.Top + rec.Height) - (float)rec.Width / 2f;
					array[3].X = (float)rec.Left + (float)rec.Width / 2f;
					array[3].Y = (float)rec.Top + (float)rec.Width / 2f;
				}
				else
				{
					array[2].X = (float)rec.Left + (float)rec.Height / 2f;
					array[2].Y = (float)rec.Top + (float)rec.Height / 2f;
					array[3].X = (float)(rec.Left + rec.Width) - (float)rec.Height / 2f;
					array[3].Y = (float)rec.Top + (float)rec.Height / 2f;
				}
				array[4].X = rec.Left + rec.Width;
				array[4].Y = rec.Top;
				GraphicsPath graphicsPath5 = new GraphicsPath();
				graphicsPath5.AddPolygon(array);
				if (m_OuterBorderLength > 0)
				{
					Region region6 = new Region(graphicsPath);
					region6.Exclude(graphicsPath2);
					SolidBrush solidBrush2 = new SolidBrush(m_OuterBorderLightColor);
					Region region7 = region6.Clone();
					region7.Intersect(graphicsPath5);
					g.FillRegion(solidBrush2, region7);
					g.SetClip(graphicsPath5);
					Pen pen4 = new Pen(solidBrush2);
					g.DrawPath(pen4, graphicsPath);
					g.DrawPath(pen4, graphicsPath2);
					pen4.Dispose();
					g.ResetClip();
					solidBrush2.Dispose();
					region7.Dispose();
					SolidBrush solidBrush3 = new SolidBrush(m_OuterBorderDarkColor);
					Region region8 = region6.Clone();
					region8.Exclude(graphicsPath5);
					g.FillRegion(solidBrush3, region8);
					g.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen5 = new Pen(solidBrush3);
					g.DrawPath(pen5, graphicsPath);
					g.DrawPath(pen5, graphicsPath2);
					pen5.Dispose();
					g.ResetClip();
					solidBrush3.Dispose();
					region8.Dispose();
					region6.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region9 = new Region(graphicsPath2);
					region9.Exclude(graphicsPath3);
					SolidBrush solidBrush4 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush4, region9);
					Pen pen6 = new Pen(solidBrush4);
					g.DrawPath(pen6, graphicsPath2);
					g.DrawPath(pen6, graphicsPath3);
					pen6.Dispose();
					solidBrush4.Dispose();
					region9.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					Region region10 = new Region(graphicsPath3);
					region10.Exclude(graphicsPath4);
					SolidBrush solidBrush5 = new SolidBrush(m_InnerBorderDarkColor);
					Region region11 = region10.Clone();
					region11.Intersect(graphicsPath5);
					g.FillRegion(solidBrush5, region11);
					g.SetClip(graphicsPath5);
					Pen pen7 = new Pen(solidBrush5);
					g.DrawPath(pen7, graphicsPath3);
					g.DrawPath(pen7, graphicsPath4);
					pen7.Dispose();
					g.ResetClip();
					solidBrush5.Dispose();
					region11.Dispose();
					SolidBrush solidBrush6 = new SolidBrush(m_InnerBorderLightColor);
					Region region12 = region10.Clone();
					region12.Exclude(graphicsPath5);
					g.FillRegion(solidBrush6, region12);
					g.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen8 = new Pen(solidBrush6);
					g.DrawPath(pen8, graphicsPath3);
					g.DrawPath(pen8, graphicsPath4);
					pen8.Dispose();
					g.ResetClip();
					solidBrush6.Dispose();
					region12.Dispose();
					region10.Dispose();
				}
				graphicsPath5.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Path:
			{
				Blend blend4 = new Blend();
				bool flag2 = false;
				if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag2 = true;
					blend4.Factors = new float[3];
					blend4.Positions = new float[3];
					blend4.Factors[0] = 0f;
					blend4.Positions[0] = 0f;
					blend4.Factors[1] = m_BorderGradientRate;
					blend4.Positions[1] = 1f - m_BorderGradientRate;
					blend4.Factors[2] = 1f;
					blend4.Positions[2] = 1f;
				}
				PathGradientBrush pathGradientBrush;
				PointF focusScales;
				if (m_OuterBorderLength > 0)
				{
					double num2 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0;
					GraphicsPath graphicsPath6 = new GraphicsPath();
					graphicsPath6.AddPolygon(new Point[4]
					{
						new Point(rec.Left - 1, rec.Top - 1),
						new Point(rec.Left + rec.Width + 1, rec.Top - 1),
						new Point(rec.Left + rec.Width + 1, rec.Top + rec.Height + 1),
						new Point(rec.Left - 1, rec.Top + rec.Height + 1)
					});
					pathGradientBrush = new PathGradientBrush(graphicsPath6);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					PathGradientBrush pathGradientBrush4 = pathGradientBrush;
					focusScales = default(PointF);
					focusScales.X = (float)rec.Left + (float)rec.Width / 2f;
					focusScales.Y = (float)rec.Top + (float)rec.Height / 2f;
					PointF centerPoint = focusScales;
					float num4;
					float num3;
					if (Math.Abs(Math.Sin(num2)) < 1E-10)
					{
						num3 = 0f;
						num4 = (float)(Math.Cos(num2) * (double)(rec.Width - m_OuterBorderLength) / 2.0);
					}
					else if (Math.Abs(Math.Cos(num2)) >= 1E-10)
					{
						num4 = (float)((double)(rec.Height - m_OuterBorderLength) / (2.0 * Math.Tan(num2)));
						num3 = (float)(Math.Tan(num2) * (double)(rec.Width - m_OuterBorderLength) / 2.0);
						num4 = ((Math.Cos(num2) >= 0.0) ? Math.Abs(num4) : (0f - Math.Abs(num4)));
						num3 = ((Math.Sin(num2) >= 0.0) ? Math.Abs(num3) : (0f - Math.Abs(num3)));
						if (num4 < 0f - (float)(rec.Width - m_OuterBorderLength) / 2f)
						{
							num4 = 0f - (float)(rec.Width - m_OuterBorderLength) / 2f;
						}
						else if (num4 > (float)(rec.Width - m_OuterBorderLength) / 2f)
						{
							num4 = (float)(rec.Width - m_OuterBorderLength) / 2f;
						}
						if (num3 < 0f - (float)(rec.Height - m_OuterBorderLength) / 2f)
						{
							num3 = 0f - (float)(rec.Height - m_OuterBorderLength) / 2f;
						}
						else if (num3 > (float)(rec.Height - m_OuterBorderLength) / 2f)
						{
							num3 = (float)(rec.Height - m_OuterBorderLength) / 2f;
						}
					}
					else
					{
						num4 = 0f;
						num3 = (float)(Math.Sin(num2) * (double)(rec.Height - m_OuterBorderLength) / 2.0);
					}
					centerPoint.X += num4;
					centerPoint.Y += num3;
					if (flag2)
					{
						pathGradientBrush4.Blend = blend4;
					}
					pathGradientBrush4.CenterPoint = centerPoint;
					pathGradientBrush4.CenterColor = m_OuterBorderLightColor;
					Region region18 = new Region(graphicsPath);
					Region region19 = new Region(graphicsPath2);
					region18.Exclude(region19);
					g.FillRegion(pathGradientBrush4, region18);
					Pen pen12 = new Pen(m_OuterBorderDarkColor);
					g.DrawPath(pen12, graphicsPath);
					g.DrawPath(pen12, graphicsPath2);
					pen12.Dispose();
					pathGradientBrush4.Dispose();
					region18.Dispose();
					region19.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region20 = new Region(graphicsPath2);
					region20.Exclude(graphicsPath3);
					SolidBrush solidBrush8 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush8, region20);
					Pen pen13 = new Pen(solidBrush8);
					g.DrawPath(pen13, graphicsPath2);
					g.DrawPath(pen13, graphicsPath3);
					pen13.Dispose();
					solidBrush8.Dispose();
					region20.Dispose();
				}
				if (m_InnerBorderLength <= 0)
				{
					break;
				}
				double num5 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0 + 3.14159265358979;
				GraphicsPath graphicsPath7 = new GraphicsPath();
				graphicsPath7.AddPolygon(new Point[4]
				{
					new Point(rectangle.Left, rectangle.Top),
					new Point(rectangle.Left + rectangle.Width, rectangle.Top),
					new Point(rectangle.Left + rectangle.Width, rectangle.Top + rectangle.Height),
					new Point(rectangle.Left, rectangle.Top + rectangle.Height)
				});
				pathGradientBrush = new PathGradientBrush(graphicsPath7);
				pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderDarkColor };
				PathGradientBrush pathGradientBrush5 = pathGradientBrush;
				focusScales = default(PointF);
				focusScales.X = (float)rectangle.Left + (float)rectangle.Width / 2f;
				focusScales.Y = (float)rectangle.Top + (float)rectangle.Height / 2f;
				PointF centerPoint2 = focusScales;
				float num7;
				float num6;
				if (Math.Abs(Math.Sin(num5)) < 1E-10)
				{
					num6 = 0f;
					num7 = (float)(Math.Cos(num5) * (double)(rectangle.Width - m_InnerBorderLength) / 2.0);
				}
				else if (Math.Abs(Math.Cos(num5)) >= 1E-10)
				{
					num7 = (float)((double)(rectangle.Height - m_InnerBorderLength) / (2.0 * Math.Tan(num5)));
					num6 = (float)(Math.Tan(num5) * (double)(rectangle.Width - m_InnerBorderLength) / 2.0);
					num7 = ((Math.Cos(num5) >= 0.0) ? Math.Abs(num7) : (0f - Math.Abs(num7)));
					num6 = ((Math.Sin(num5) >= 0.0) ? Math.Abs(num6) : (0f - Math.Abs(num6)));
					if (num7 < 0f - (float)(rectangle.Width - m_InnerBorderLength) / 2f)
					{
						num7 = 0f - (float)(rectangle.Width - m_InnerBorderLength) / 2f;
					}
					else if (num7 > (float)(rectangle.Width - m_InnerBorderLength) / 2f)
					{
						num7 = (float)(rectangle.Width - m_InnerBorderLength) / 2f;
					}
					if (num6 < 0f - (float)(rectangle.Height - m_InnerBorderLength) / 2f)
					{
						num6 = 0f - (float)(rectangle.Height - m_InnerBorderLength) / 2f;
					}
					else if (num6 > (float)(rectangle.Height - m_InnerBorderLength) / 2f)
					{
						num6 = (float)(rectangle.Height - m_InnerBorderLength) / 2f;
					}
				}
				else
				{
					num7 = 0f;
					num6 = (float)(Math.Sin(num5) * (double)(rectangle.Height - m_InnerBorderLength) / 2.0);
				}
				centerPoint2.X += num7;
				centerPoint2.Y += num6;
				if (flag2)
				{
					pathGradientBrush5.Blend = blend4;
				}
				pathGradientBrush5.CenterPoint = centerPoint2;
				pathGradientBrush5.CenterColor = m_InnerBorderLightColor;
				Region region21 = new Region(graphicsPath3);
				Region region22 = new Region(graphicsPath4);
				region21.Exclude(region22);
				g.FillRegion(pathGradientBrush5, region21);
				Pen pen14 = new Pen(m_InnerBorderDarkColor);
				g.DrawPath(pen14, graphicsPath3);
				g.DrawPath(pen14, graphicsPath4);
				pen14.Dispose();
				pathGradientBrush5.Dispose();
				region21.Dispose();
				region22.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Linear:
			case DAS_BorderGradientStyle.BGS_Linear2:
			{
				Blend blend3 = new Blend();
				bool flag = false;
				float num = 0f;
				if (m_BorderLightIntermediateBrightness >= 0f && (double)m_BorderLightIntermediateBrightness <= 1.0)
				{
					num = m_BorderLightIntermediateBrightness;
				}
				if (m_BorderGradientLightPos1 > 0f && (double)m_BorderGradientLightPos1 < 1.0)
				{
					flag = true;
					if (m_BorderGradientLightPos2 <= 0f || (double)m_BorderGradientLightPos2 > 1.0)
					{
						blend3.Factors = new float[5];
						blend3.Positions = new float[5];
						blend3.Factors[0] = 0f;
						blend3.Positions[0] = 0f;
						blend3.Factors[1] = m_BorderGradientRate;
						blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
						blend3.Factors[2] = 1f;
						blend3.Positions[2] = m_BorderGradientLightPos1;
						blend3.Factors[3] = m_BorderGradientRate;
						blend3.Positions[3] = (1f - m_BorderGradientLightPos1) * m_BorderGradientRate + m_BorderGradientLightPos1;
						blend3.Factors[4] = 0f;
						blend3.Positions[4] = 1f;
					}
					else
					{
						blend3.Factors = new float[9];
						blend3.Positions = new float[9];
						if (m_BorderGradientLightPos1 <= m_BorderGradientLightPos2)
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos1;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos1 + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos2;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos2 + (1f - m_BorderGradientLightPos2) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
							if (m_BorderGradientLightPos1 == m_BorderGradientLightPos2)
							{
								blend3.Factors[3] = 1f;
								blend3.Factors[4] = 1f;
								blend3.Factors[5] = 1f;
							}
							if (m_BorderGradientLightPos2 == 1f)
							{
								blend3.Factors[7] = 1f;
								blend3.Factors[8] = 1f;
							}
						}
						else
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos2 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos2;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos2 + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos1;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos1 + (1f - m_BorderGradientLightPos1) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
						}
					}
				}
				else if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag = true;
					blend3.Factors = new float[3];
					blend3.Positions = new float[3];
					blend3.Factors[0] = 0f;
					blend3.Positions[0] = 0f;
					blend3.Factors[1] = m_BorderGradientRate;
					blend3.Positions[1] = 1f - m_BorderGradientRate;
					blend3.Factors[2] = 1f;
					blend3.Positions[2] = 1f;
				}
				if (m_OuterBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(rec.Left - 1, rec.Top - 1, rec.Width + 2, rec.Height + 2), m_OuterBorderDarkColor, m_OuterBorderLightColor, m_BorderGradientAngle);
					if (flag)
					{
						linearGradientBrush.Blend = blend3;
					}
					Region region13 = new Region(graphicsPath);
					Region region14 = new Region(graphicsPath2);
					region13.Exclude(region14);
					g.FillRegion(linearGradientBrush, region13);
					Pen pen9 = new Pen(linearGradientBrush);
					g.DrawPath(pen9, graphicsPath);
					g.DrawPath(pen9, graphicsPath2);
					pen9.Dispose();
					linearGradientBrush.Dispose();
					region13.Dispose();
					region14.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region15 = new Region(graphicsPath2);
					region15.Exclude(graphicsPath3);
					SolidBrush solidBrush7 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush7, region15);
					Pen pen10 = new Pen(solidBrush7);
					g.DrawPath(pen10, graphicsPath2);
					g.DrawPath(pen10, graphicsPath3);
					pen10.Dispose();
					solidBrush7.Dispose();
					region15.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush2 = ((m_BorderGradientType != DAS_BorderGradientStyle.BGS_Linear) ? new LinearGradientBrush(rectangle, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderGradientAngle + 180) : new LinearGradientBrush(rectangle, m_InnerBorderLightColor, m_InnerBorderDarkColor, m_BorderGradientAngle));
					if (flag)
					{
						linearGradientBrush2.Blend = blend3;
					}
					Region region16 = new Region(graphicsPath3);
					Region region17 = new Region(graphicsPath4);
					region16.Exclude(region17);
					g.FillRegion(linearGradientBrush2, region16);
					Pen pen11 = new Pen(linearGradientBrush2);
					g.DrawPath(pen11, graphicsPath3);
					g.DrawPath(pen11, graphicsPath4);
					pen11.Dispose();
					linearGradientBrush2.Dispose();
					region16.Dispose();
					region17.Dispose();
				}
				break;
			}
			case DAS_BorderGradientStyle.BGS_Ring:
			{
				Blend blend = new Blend();
				blend.Factors = new float[3];
				blend.Positions = new float[3];
				Blend blend2 = blend;
				blend2.Factors[0] = 0f;
				blend2.Positions[0] = 0f;
				blend2.Factors[2] = 1f;
				blend2.Positions[2] = 1f;
				if (m_OuterBorderLength > 0)
				{
					blend2.Factors[1] = m_BorderGradientRate;
					blend2.Positions[1] = 1f - m_BorderGradientRate;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					pathGradientBrush.CenterColor = m_OuterBorderLightColor;
					PathGradientBrush pathGradientBrush2 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rec2.Width / (float)rec.Width;
					focusScales.Y = (float)rec2.Height / (float)rec.Height;
					pathGradientBrush2.FocusScales = focusScales;
					Region region = new Region(graphicsPath);
					Region region2 = new Region(graphicsPath2);
					region.Exclude(region2);
					g.FillRegion(pathGradientBrush2, region);
					Pen pen = new Pen(m_OuterBorderDarkColor);
					g.DrawPath(pen, graphicsPath);
					pen.Dispose();
					pathGradientBrush2.Dispose();
					region.Dispose();
					region2.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region3 = new Region(graphicsPath2);
					region3.Exclude(graphicsPath3);
					SolidBrush solidBrush = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush, region3);
					Pen pen2 = new Pen(solidBrush);
					g.DrawPath(pen2, graphicsPath2);
					g.DrawPath(pen2, graphicsPath3);
					pen2.Dispose();
					solidBrush.Dispose();
					region3.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					blend2.Factors[1] = 1f - m_BorderGradientRate;
					blend2.Positions[1] = m_BorderGradientRate;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath3);
					pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderLightColor };
					pathGradientBrush.Blend = blend2;
					pathGradientBrush.CenterColor = m_InnerBorderDarkColor;
					PathGradientBrush pathGradientBrush3 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rec3.Width / (float)rectangle.Width;
					focusScales.Y = (float)rec3.Height / (float)rectangle.Height;
					pathGradientBrush3.FocusScales = focusScales;
					Region region4 = new Region(graphicsPath3);
					Region region5 = new Region(graphicsPath4);
					region4.Exclude(region5);
					g.FillRegion(pathGradientBrush3, region4);
					Pen pen3 = new Pen(m_InnerBorderDarkColor);
					g.DrawPath(pen3, graphicsPath4);
					pen3.Dispose();
					pathGradientBrush3.Dispose();
					region4.Dispose();
					region5.Dispose();
				}
				break;
			}
			}
			if (m_BorderExteriorLength > 0)
			{
				SolidBrush solidBrush9 = new SolidBrush(m_BorderExteriorColor);
				GraphicsPath graphicsPath8 = new GraphicsPath();
				SetRoundRect(graphicsPath8, rec_width, m_RoundRadius);
				Region region23 = new Region(graphicsPath8);
				region23.Exclude(graphicsPath);
				g.FillRegion(solidBrush9, region23);
				Pen pen15 = new Pen(solidBrush9);
				g.DrawPath(pen15, graphicsPath8);
				g.DrawPath(pen15, graphicsPath);
				pen15.Dispose();
				solidBrush9.Dispose();
				region23.Dispose();
				graphicsPath8.Dispose();
			}
			graphicsPath4.Dispose();
			graphicsPath3.Dispose();
			graphicsPath.Dispose();
		}

		public void SetValue(Graphics g, Rectangle rectangle_0, DAS_ShapeStyle m_Shape, GraphicsPath graphicsPath_0, Color m_OffColor, Color m_OffGradientColor, DAS_BkGradientStyle m_GradientType, int m_GradientAngle, float m_GradientRate, int m_RoundRadius, int m_ArrowWidth, float m_ShinePosition)
		{
			Blend blend2;
			PathGradientBrush pathGradientBrush;
			if (m_GradientType != DAS_BkGradientStyle.BKGS_Polygon && m_GradientType != DAS_BkGradientStyle.BKGS_Sphere)
			{
				switch (m_GradientType)
				{
				case DAS_BkGradientStyle.BKGS_Shine:
				{
					blend2 = new Blend();
					blend2.Factors = new float[3];
					blend2.Positions = new float[3];
					Blend blend3 = blend2;
					blend3.Factors[0] = 0f;
					blend3.Positions[0] = 0f;
					blend3.Factors[1] = m_GradientRate;
					blend3.Positions[1] = 1f - m_GradientRate;
					blend3.Factors[2] = 1f;
					blend3.Positions[2] = 1f;
					double num = 3.14159265358979 * (double)m_GradientAngle / 180.0;
					GraphicsPath graphicsPath = new GraphicsPath();
					Rectangle rectangle = new Rectangle(rectangle_0.Left - 1, rectangle_0.Top - 1, rectangle_0.Width + 2, rectangle_0.Height + 2);
					switch (m_Shape)
					{
					case DAS_ShapeStyle.SS_Rect:
						graphicsPath.AddRectangle(rectangle);
						break;
					case DAS_ShapeStyle.SS_RoundRect:
						SetRoundRect(graphicsPath, rectangle, m_RoundRadius);
						break;
					default:
						graphicsPath = (GraphicsPath)graphicsPath_0.Clone();
						break;
					case DAS_ShapeStyle.SS_Circle:
						graphicsPath.AddEllipse(rectangle);
						break;
					}
					pathGradientBrush = new PathGradientBrush(graphicsPath);
					pathGradientBrush.SurroundColors = new Color[1] { m_OffColor };
					PathGradientBrush pathGradientBrush2 = pathGradientBrush;
					PointF pointF = default(PointF);
					pointF.X = (float)rectangle_0.Left + (float)rectangle_0.Width / 2f;
					pointF.Y = (float)rectangle_0.Top + (float)rectangle_0.Height / 2f;
					PointF centerPoint = pointF;
					float num3;
					float num2;
					if (Math.Abs(Math.Sin(num)) < 1E-10)
					{
						num2 = 0f;
						num3 = (float)(Math.Cos(num) * (double)rectangle_0.Width / 2.0);
					}
					else if (Math.Abs(Math.Cos(num)) >= 1E-10)
					{
						num3 = (float)((double)rectangle_0.Height / (2.0 * Math.Tan(num)));
						num2 = (float)(Math.Tan(num) * (double)rectangle_0.Width / 2.0);
						num3 = ((Math.Cos(num) >= 0.0) ? Math.Abs(num3) : (0f - Math.Abs(num3)));
						num2 = ((Math.Sin(num) >= 0.0) ? Math.Abs(num2) : (0f - Math.Abs(num2)));
						if (num3 < 0f - (float)rectangle_0.Width / 2f)
						{
							num3 = 0f - (float)rectangle_0.Width / 2f;
						}
						else if (num3 > (float)rectangle_0.Width / 2f)
						{
							num3 = (float)rectangle_0.Width / 2f;
						}
						if (num2 < 0f - (float)rectangle_0.Height / 2f)
						{
							num2 = 0f - (float)rectangle_0.Height / 2f;
						}
						else if (num2 > (float)rectangle_0.Height / 2f)
						{
							num2 = (float)rectangle_0.Height / 2f;
						}
					}
					else
					{
						num3 = 0f;
						num2 = (float)(Math.Sin(num) * (double)rectangle_0.Height / 2.0);
					}
					if (m_Shape == DAS_ShapeStyle.SS_Circle)
					{
						num3 = (float)((double)rectangle_0.Width * Math.Cos(num) / 2.0);
						num2 = (float)((double)rectangle_0.Height * Math.Sin(num) / 2.0);
					}
					centerPoint.X += num3 * m_ShinePosition;
					centerPoint.Y += num2 * m_ShinePosition;
					pathGradientBrush2.Blend = blend3;
					pathGradientBrush2.CenterPoint = centerPoint;
					pathGradientBrush2.CenterColor = m_OffGradientColor;
					if (graphicsPath_0 != null)
					{
						g.FillPath(pathGradientBrush2, graphicsPath_0);
					}
					else if (m_Shape != DAS_ShapeStyle.SS_Circle)
					{
						g.FillRectangle(pathGradientBrush2, rectangle_0);
					}
					else
					{
						g.FillEllipse(pathGradientBrush2, rectangle);
					}
					pathGradientBrush2.Dispose();
					return;
				}
				default:
					return;
				case DAS_BkGradientStyle.BKGS_Linear:
				case DAS_BkGradientStyle.BKGS_Linear2:
				{
					Blend blend = new Blend();
					if (m_GradientType != 0)
					{
						blend.Factors = new float[5];
						blend.Positions = new float[5];
						blend.Factors[0] = 0f;
						blend.Positions[0] = 0f;
						blend.Factors[1] = m_GradientRate;
						blend.Positions[1] = 0.5f * (1f - m_GradientRate);
						blend.Factors[2] = 1f;
						blend.Positions[2] = 0.5f;
						blend.Factors[3] = m_GradientRate;
						blend.Positions[3] = 0.5f + 0.5f * m_GradientRate;
						blend.Factors[4] = 0f;
						blend.Positions[4] = 1f;
					}
					else
					{
						blend.Factors = new float[3];
						blend.Positions = new float[3];
						blend.Factors[0] = 0f;
						blend.Positions[0] = 0f;
						blend.Factors[1] = m_GradientRate;
						blend.Positions[1] = 0.5f * (1f - m_GradientRate);
						blend.Factors[2] = 1f;
						blend.Positions[2] = 1f;
					}
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(rectangle_0.Left - 1, rectangle_0.Top - 1, rectangle_0.Width + 2, rectangle_0.Height + 2), m_OffColor, m_OffGradientColor, m_GradientAngle)
					{
						Blend = blend
					};
					if (graphicsPath_0 != null)
					{
						g.FillPath(linearGradientBrush, graphicsPath_0);
					}
					else
					{
						g.FillRectangle(linearGradientBrush, rectangle_0);
					}
					linearGradientBrush.Dispose();
					return;
				}
				case DAS_BkGradientStyle.BKGS_Sphere2:
					break;
				}
			}
			blend2 = new Blend();
			blend2.Factors = new float[3];
			blend2.Positions = new float[3];
			Blend blend4 = blend2;
			blend4.Factors[0] = 0f;
			blend4.Positions[0] = 0f;
			blend4.Factors[2] = 1f;
			blend4.Positions[2] = 1f;
			blend4.Factors[1] = m_GradientRate;
			blend4.Positions[1] = 1f - m_GradientRate;
			GraphicsPath graphicsPath2 = new GraphicsPath();
			if (m_GradientType != DAS_BkGradientStyle.BKGS_Polygon)
			{
				graphicsPath2.AddEllipse((m_Shape == DAS_ShapeStyle.SS_Circle) ? new RectangleF(rectangle_0.Left - 1, rectangle_0.Top - 1, rectangle_0.Width + 2, rectangle_0.Height + 2) : new RectangleF((float)rectangle_0.Left - 0.415f * (float)rectangle_0.Width / 2f, (float)rectangle_0.Top - 0.415f * (float)rectangle_0.Height / 2f, 1.415f * (float)rectangle_0.Width, 1.415f * (float)rectangle_0.Height));
			}
			else
			{
				graphicsPath2.AddPolygon(new Point[4]
				{
					new Point(rectangle_0.Left, rectangle_0.Top),
					new Point(rectangle_0.Left + rectangle_0.Width, rectangle_0.Top),
					new Point(rectangle_0.Left + rectangle_0.Width, rectangle_0.Top + rectangle_0.Height),
					new Point(rectangle_0.Left, rectangle_0.Top + rectangle_0.Height)
				});
			}
			pathGradientBrush = new PathGradientBrush(graphicsPath2);
			pathGradientBrush.SurroundColors = new Color[1] { m_OffColor };
			pathGradientBrush.Blend = blend4;
			pathGradientBrush.CenterColor = m_OffGradientColor;
			PathGradientBrush pathGradientBrush3 = pathGradientBrush;
			if (m_GradientType == DAS_BkGradientStyle.BKGS_Sphere2)
			{
				PointF focusScales = default(PointF);
				if (rectangle_0.Width <= rectangle_0.Height)
				{
					focusScales.X = 0f;
					focusScales.Y = (float)(rectangle_0.Height - rectangle_0.Width) / (float)rectangle_0.Height;
				}
				else
				{
					focusScales.X = (float)(rectangle_0.Width - rectangle_0.Height) / (float)rectangle_0.Width;
					focusScales.Y = 0f;
				}
				pathGradientBrush3.FocusScales = focusScales;
			}
			if (graphicsPath_0 != null)
			{
				g.FillPath(pathGradientBrush3, graphicsPath_0);
			}
			else
			{
				g.FillRectangle(pathGradientBrush3, rectangle_0);
			}
			pathGradientBrush3.Dispose();
		}

		public void SetRoundRect(GraphicsPath graphicsPath_0, Rectangle rec, int m_RoundRadius)
		{
			int num = m_RoundRadius;
			int left = rec.Left;
			int top = rec.Top;
			int num2 = rec.Width - 1;
			int num3 = rec.Height - 1;
			if (2 * num > num2)
			{
				num = num2 / 2;
			}
			if (2 * num > num3)
			{
				num = num3 / 2;
			}
			if (num <= 0)
			{
				graphicsPath_0.AddLine(left, top, left + num2, top);
				graphicsPath_0.AddLine(left + num2, top, left + num2, top + num3);
				graphicsPath_0.AddLine(left + num2, top + num3, left, top + num3);
				graphicsPath_0.AddLine(left, top + num3, left, top);
			}
			else
			{
				graphicsPath_0.AddLine(left + num, top, left + num2 - num, top);
				graphicsPath_0.AddArc(left + num2 - 2 * num, top, 2 * num, 2 * num, 270f, 90f);
				graphicsPath_0.AddLine(left + num2, top + num, left + num2, top + num3 - num);
				graphicsPath_0.AddArc(left + num2 - 2 * num, top + num3 - 2 * num, 2 * num, 2 * num, 0f, 90f);
				graphicsPath_0.AddLine(left + num2 - num, top + num3, left + num, top + num3);
				graphicsPath_0.AddArc(left, top + num3 - 2 * num, 2 * num, 2 * num, 90f, 90f);
				graphicsPath_0.AddLine(left, top + num3 - num, left, top + num);
				graphicsPath_0.AddArc(left, top, 2 * num, 2 * num, 180f, 90f);
			}
			graphicsPath_0.CloseFigure();
		}

		public void SetArrow(Graphics g, Rectangle rec_width, DAS_ShapeStyle m_Shape, int m_RoundRadius, int m_ArrowWidth, int m_OuterBorderLength, Color m_OuterBorderDarkColor, Color m_OuterBorderLightColor, int m_MiddleBorderLength, Color m_MiddleBorderColor, int m_InnerBorderLength, Color m_InnerBorderDarkColor, Color m_InnerBorderLightColor, int m_BorderExteriorLength, Color m_BorderExteriorColor, DAS_BorderGradientStyle m_BorderGradientType, long m_BorderGradientAngle, float m_BorderGradientRate, float m_BorderGradientLightPos1, float m_BorderGradientLightPos2, float m_BorderLightIntermediateBrightness)
		{
			switch (m_Shape)
			{
			case DAS_ShapeStyle.SS_Rect:
				SetRect(g, rec_width, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, (int)m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
				return;
			case DAS_ShapeStyle.SS_RoundRect:
				SetRoundRect2(g, rec_width, m_RoundRadius, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
				return;
			case DAS_ShapeStyle.SS_Circle:
				SetCircle(g, rec_width, m_OuterBorderLength, m_OuterBorderDarkColor, m_OuterBorderLightColor, m_MiddleBorderLength, m_MiddleBorderColor, m_InnerBorderLength, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderExteriorLength, m_BorderExteriorColor, m_BorderGradientType, m_BorderGradientAngle, m_BorderGradientRate, m_BorderGradientLightPos1, m_BorderGradientLightPos2, m_BorderLightIntermediateBrightness);
				return;
			}
			Rectangle rectangle = new Rectangle(rec_width.Left + m_BorderExteriorLength, rec_width.Top + m_BorderExteriorLength, rec_width.Width - 2 * m_BorderExteriorLength, rec_width.Height - 2 * m_BorderExteriorLength);
			Rectangle rectangle2 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength));
			Rectangle rect = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength));
			Rectangle rectangle3 = new Rectangle(rec_width.Left + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Top + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, rec_width.Width - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength), rec_width.Height - 2 * (m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength));
			int int_ = m_ArrowWidth + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength;
			GraphicsPath graphicsPath = new GraphicsPath();
			method_6(graphicsPath, rec_width, m_Shape, int_, m_BorderExteriorLength);
			GraphicsPath graphicsPath2 = new GraphicsPath();
			method_6(graphicsPath2, rec_width, m_Shape, int_, m_BorderExteriorLength + m_OuterBorderLength);
			GraphicsPath graphicsPath3 = new GraphicsPath();
			method_6(graphicsPath3, rec_width, m_Shape, int_, m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength);
			GraphicsPath graphicsPath4 = new GraphicsPath();
			method_6(graphicsPath4, rec_width, m_Shape, int_, m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength);
			switch (m_BorderGradientType)
			{
			case DAS_BorderGradientStyle.BGS_Flat:
			{
				PointF[] array = new PointF[5];
				array[0].X = rectangle.Left;
				array[0].Y = rectangle.Top;
				array[1].X = rectangle.Left;
				array[1].Y = rectangle.Top + rectangle.Height;
				if (rectangle.Width < rectangle.Height)
				{
					array[2].X = (float)rectangle.Left + (float)rectangle.Width / 2f;
					array[2].Y = (float)(rectangle.Top + rectangle.Height) - (float)rectangle.Width / 2f;
					array[3].X = (float)rectangle.Left + (float)rectangle.Width / 2f;
					array[3].Y = (float)rectangle.Top + (float)rectangle.Width / 2f;
				}
				else
				{
					array[2].X = (float)rectangle.Left + (float)rectangle.Height / 2f;
					array[2].Y = (float)rectangle.Top + (float)rectangle.Height / 2f;
					array[3].X = (float)(rectangle.Left + rectangle.Width) - (float)rectangle.Height / 2f;
					array[3].Y = (float)rectangle.Top + (float)rectangle.Height / 2f;
				}
				array[4].X = rectangle.Left + rectangle.Width;
				array[4].Y = rectangle.Top;
				GraphicsPath graphicsPath5 = new GraphicsPath();
				graphicsPath5.AddPolygon(array);
				if (m_OuterBorderLength > 0)
				{
					Region region6 = new Region(graphicsPath);
					region6.Exclude(graphicsPath2);
					SolidBrush solidBrush2 = new SolidBrush(m_OuterBorderLightColor);
					Region region7 = region6.Clone();
					region7.Intersect(graphicsPath5);
					g.FillRegion(solidBrush2, region7);
					g.SetClip(graphicsPath5);
					Pen pen4 = new Pen(solidBrush2);
					g.DrawPath(pen4, graphicsPath);
					g.DrawPath(pen4, graphicsPath2);
					pen4.Dispose();
					g.ResetClip();
					solidBrush2.Dispose();
					region7.Dispose();
					SolidBrush solidBrush3 = new SolidBrush(m_OuterBorderDarkColor);
					Region region8 = region6.Clone();
					region8.Exclude(graphicsPath5);
					g.FillRegion(solidBrush3, region8);
					g.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen5 = new Pen(solidBrush3);
					g.DrawPath(pen5, graphicsPath);
					g.DrawPath(pen5, graphicsPath2);
					pen5.Dispose();
					g.ResetClip();
					solidBrush3.Dispose();
					region8.Dispose();
					region6.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region9 = new Region(graphicsPath2);
					region9.Exclude(graphicsPath3);
					SolidBrush solidBrush4 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush4, region9);
					Pen pen6 = new Pen(solidBrush4);
					g.DrawPath(pen6, graphicsPath2);
					g.DrawPath(pen6, graphicsPath3);
					pen6.Dispose();
					solidBrush4.Dispose();
					region9.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					Region region10 = new Region(graphicsPath3);
					region10.Exclude(graphicsPath4);
					SolidBrush solidBrush5 = new SolidBrush(m_InnerBorderDarkColor);
					Region region11 = region10.Clone();
					region11.Intersect(graphicsPath5);
					g.FillRegion(solidBrush5, region11);
					g.SetClip(graphicsPath5);
					Pen pen7 = new Pen(solidBrush5);
					g.DrawPath(pen7, graphicsPath3);
					g.DrawPath(pen7, graphicsPath4);
					pen7.Dispose();
					g.ResetClip();
					solidBrush5.Dispose();
					region11.Dispose();
					SolidBrush solidBrush6 = new SolidBrush(m_InnerBorderLightColor);
					Region region12 = region10.Clone();
					region12.Exclude(graphicsPath5);
					g.FillRegion(solidBrush6, region12);
					g.SetClip(graphicsPath5, CombineMode.Exclude);
					Pen pen8 = new Pen(solidBrush6);
					g.DrawPath(pen8, graphicsPath3);
					g.DrawPath(pen8, graphicsPath4);
					pen8.Dispose();
					g.ResetClip();
					solidBrush6.Dispose();
					region12.Dispose();
					region10.Dispose();
				}
				graphicsPath5.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Path:
			{
				Blend blend4 = new Blend();
				bool flag2 = false;
				if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag2 = true;
					blend4.Factors = new float[3];
					blend4.Positions = new float[3];
					blend4.Factors[0] = 0f;
					blend4.Positions[0] = 0f;
					blend4.Factors[1] = m_BorderGradientRate;
					blend4.Positions[1] = 1f - m_BorderGradientRate;
					blend4.Factors[2] = 1f;
					blend4.Positions[2] = 1f;
				}
				PathGradientBrush pathGradientBrush;
				PointF focusScales;
				if (m_OuterBorderLength > 0)
				{
					double num2 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0;
					GraphicsPath graphicsPath6 = new GraphicsPath();
					graphicsPath6.AddPolygon(new Point[4]
					{
						new Point(rectangle.Left - 1, rectangle.Top - 1),
						new Point(rectangle.Left + rectangle.Width + 1, rectangle.Top - 1),
						new Point(rectangle.Left + rectangle.Width + 1, rectangle.Top + rectangle.Height + 1),
						new Point(rectangle.Left - 1, rectangle.Top + rectangle.Height + 1)
					});
					pathGradientBrush = new PathGradientBrush(graphicsPath6);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					PathGradientBrush pathGradientBrush4 = pathGradientBrush;
					focusScales = default(PointF);
					focusScales.X = (float)rectangle.Left + (float)rectangle.Width / 2f;
					focusScales.Y = (float)rectangle.Top + (float)rectangle.Height / 2f;
					PointF centerPoint = focusScales;
					float num4;
					float num3;
					if (Math.Abs(Math.Sin(num2)) < 1E-10)
					{
						num3 = 0f;
						num4 = (float)(Math.Cos(num2) * (double)(rectangle.Width - m_OuterBorderLength) / 2.0);
					}
					else if (Math.Abs(Math.Cos(num2)) >= 1E-10)
					{
						num4 = (float)((double)(rectangle.Height - m_OuterBorderLength) / (2.0 * Math.Tan(num2)));
						num3 = (float)(Math.Tan(num2) * (double)(rectangle.Width - m_OuterBorderLength) / 2.0);
						num4 = ((Math.Cos(num2) >= 0.0) ? Math.Abs(num4) : (0f - Math.Abs(num4)));
						num3 = ((Math.Sin(num2) >= 0.0) ? Math.Abs(num3) : (0f - Math.Abs(num3)));
						if (num4 < 0f - (float)(rectangle.Width - m_OuterBorderLength) / 2f)
						{
							num4 = 0f - (float)(rectangle.Width - m_OuterBorderLength) / 2f;
						}
						else if (num4 > (float)(rectangle.Width - m_OuterBorderLength) / 2f)
						{
							num4 = (float)(rectangle.Width - m_OuterBorderLength) / 2f;
						}
						if (num3 < 0f - (float)(rectangle.Height - m_OuterBorderLength) / 2f)
						{
							num3 = 0f - (float)(rectangle.Height - m_OuterBorderLength) / 2f;
						}
						else if (num3 > (float)(rectangle.Height - m_OuterBorderLength) / 2f)
						{
							num3 = (float)(rectangle.Height - m_OuterBorderLength) / 2f;
						}
					}
					else
					{
						num4 = 0f;
						num3 = (float)(Math.Sin(num2) * (double)(rectangle.Height - m_OuterBorderLength) / 2.0);
					}
					centerPoint.X += num4 * m_BorderGradientLightPos1;
					centerPoint.Y += num3 * m_BorderGradientLightPos1;
					if (flag2)
					{
						pathGradientBrush4.Blend = blend4;
					}
					pathGradientBrush4.CenterPoint = centerPoint;
					pathGradientBrush4.CenterColor = m_OuterBorderLightColor;
					Region region18 = new Region(graphicsPath);
					Region region19 = new Region(graphicsPath2);
					region18.Exclude(region19);
					g.FillRegion(pathGradientBrush4, region18);
					Pen pen12 = new Pen(m_OuterBorderDarkColor);
					g.DrawPath(pen12, graphicsPath);
					g.DrawPath(pen12, graphicsPath2);
					pen12.Dispose();
					pathGradientBrush4.Dispose();
					region18.Dispose();
					region19.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region20 = new Region(graphicsPath2);
					region20.Exclude(graphicsPath3);
					SolidBrush solidBrush8 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush8, region20);
					Pen pen13 = new Pen(solidBrush8);
					g.DrawPath(pen13, graphicsPath2);
					g.DrawPath(pen13, graphicsPath3);
					pen13.Dispose();
					solidBrush8.Dispose();
					region20.Dispose();
				}
				if (m_InnerBorderLength <= 0)
				{
					break;
				}
				double num5 = 3.14159265358979 * (double)m_BorderGradientAngle / 180.0 + 3.14159265358979;
				GraphicsPath graphicsPath7 = new GraphicsPath();
				graphicsPath7.AddPolygon(new Point[4]
				{
					new Point(rect.Left, rect.Top),
					new Point(rect.Left + rect.Width, rect.Top),
					new Point(rect.Left + rect.Width, rect.Top + rect.Height),
					new Point(rect.Left, rect.Top + rect.Height)
				});
				pathGradientBrush = new PathGradientBrush(graphicsPath7);
				pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderDarkColor };
				PathGradientBrush pathGradientBrush5 = pathGradientBrush;
				focusScales = default(PointF);
				focusScales.X = (float)rect.Left + (float)rect.Width / 2f;
				focusScales.Y = (float)rect.Top + (float)rect.Height / 2f;
				PointF centerPoint2 = focusScales;
				float num7;
				float num6;
				if (Math.Abs(Math.Sin(num5)) < 1E-10)
				{
					num6 = 0f;
					num7 = (float)(Math.Cos(num5) * (double)(rect.Width - m_InnerBorderLength) / 2.0);
				}
				else if (Math.Abs(Math.Cos(num5)) >= 1E-10)
				{
					num7 = (float)((double)(rect.Height - m_InnerBorderLength) / (2.0 * Math.Tan(num5)));
					num6 = (float)(Math.Tan(num5) * (double)(rect.Width - m_InnerBorderLength) / 2.0);
					num7 = ((Math.Cos(num5) >= 0.0) ? Math.Abs(num7) : (0f - Math.Abs(num7)));
					num6 = ((Math.Sin(num5) >= 0.0) ? Math.Abs(num6) : (0f - Math.Abs(num6)));
					if (num7 < 0f - (float)(rect.Width - m_InnerBorderLength) / 2f)
					{
						num7 = 0f - (float)(rect.Width - m_InnerBorderLength) / 2f;
					}
					else if (num7 > (float)(rect.Width - m_InnerBorderLength) / 2f)
					{
						num7 = (float)(rect.Width - m_InnerBorderLength) / 2f;
					}
					if (num6 < 0f - (float)(rect.Height - m_InnerBorderLength) / 2f)
					{
						num6 = 0f - (float)(rect.Height - m_InnerBorderLength) / 2f;
					}
					else if (num6 > (float)(rect.Height - m_InnerBorderLength) / 2f)
					{
						num6 = (float)(rect.Height - m_InnerBorderLength) / 2f;
					}
				}
				else
				{
					num7 = 0f;
					num6 = (float)(Math.Sin(num5) * (double)(rect.Height - m_InnerBorderLength) / 2.0);
				}
				centerPoint2.X += num7 * m_BorderGradientLightPos1;
				centerPoint2.Y += num6 * m_BorderGradientLightPos1;
				if (flag2)
				{
					pathGradientBrush5.Blend = blend4;
				}
				pathGradientBrush5.CenterPoint = centerPoint2;
				pathGradientBrush5.CenterColor = m_InnerBorderLightColor;
				Region region21 = new Region(graphicsPath3);
				Region region22 = new Region(graphicsPath4);
				region21.Exclude(region22);
				g.FillRegion(pathGradientBrush5, region21);
				Pen pen14 = new Pen(m_InnerBorderDarkColor);
				g.DrawPath(pen14, graphicsPath3);
				g.DrawPath(pen14, graphicsPath4);
				pen14.Dispose();
				pathGradientBrush5.Dispose();
				region21.Dispose();
				region22.Dispose();
				break;
			}
			case DAS_BorderGradientStyle.BGS_Linear:
			case DAS_BorderGradientStyle.BGS_Linear2:
			{
				Blend blend3 = new Blend();
				bool flag = false;
				float num = 0f;
				if (m_BorderLightIntermediateBrightness >= 0f && (double)m_BorderLightIntermediateBrightness <= 1.0)
				{
					num = m_BorderLightIntermediateBrightness;
				}
				if (m_BorderGradientLightPos1 > 0f && (double)m_BorderGradientLightPos1 < 1.0)
				{
					flag = true;
					if (m_BorderGradientLightPos2 <= 0f || (double)m_BorderGradientLightPos2 > 1.0)
					{
						blend3.Factors = new float[5];
						blend3.Positions = new float[5];
						blend3.Factors[0] = 0f;
						blend3.Positions[0] = 0f;
						blend3.Factors[1] = m_BorderGradientRate;
						blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
						blend3.Factors[2] = 1f;
						blend3.Positions[2] = m_BorderGradientLightPos1;
						blend3.Factors[3] = m_BorderGradientRate;
						blend3.Positions[3] = (1f - m_BorderGradientLightPos1) * m_BorderGradientRate + m_BorderGradientLightPos1;
						blend3.Factors[4] = 0f;
						blend3.Positions[4] = 1f;
					}
					else
					{
						blend3.Factors = new float[9];
						blend3.Positions = new float[9];
						if (m_BorderGradientLightPos1 <= m_BorderGradientLightPos2)
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos1 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos1;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos1 + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos2 - m_BorderGradientLightPos1) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos2;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos2 + (1f - m_BorderGradientLightPos2) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
							if (m_BorderGradientLightPos1 == m_BorderGradientLightPos2)
							{
								blend3.Factors[3] = 1f;
								blend3.Factors[4] = 1f;
								blend3.Factors[5] = 1f;
							}
							if (m_BorderGradientLightPos2 == 1f)
							{
								blend3.Factors[7] = 1f;
								blend3.Factors[8] = 1f;
							}
						}
						else
						{
							blend3.Factors[0] = 0f;
							blend3.Positions[0] = 0f;
							blend3.Factors[1] = m_BorderGradientRate;
							blend3.Positions[1] = m_BorderGradientLightPos2 * (1f - m_BorderGradientRate);
							blend3.Factors[2] = 1f;
							blend3.Positions[2] = m_BorderGradientLightPos2;
							blend3.Factors[3] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[3] = m_BorderGradientLightPos2 + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * m_BorderGradientRate;
							blend3.Factors[4] = num;
							blend3.Positions[4] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f;
							blend3.Factors[5] = (1f - num) * m_BorderGradientRate + num;
							blend3.Positions[5] = (m_BorderGradientLightPos1 + m_BorderGradientLightPos2) / 2f + (m_BorderGradientLightPos1 - m_BorderGradientLightPos2) / 2f * (1f - m_BorderGradientRate);
							blend3.Factors[6] = 1f;
							blend3.Positions[6] = m_BorderGradientLightPos1;
							blend3.Factors[7] = m_BorderGradientRate;
							blend3.Positions[7] = m_BorderGradientLightPos1 + (1f - m_BorderGradientLightPos1) * m_BorderGradientRate;
							blend3.Factors[8] = 0f;
							blend3.Positions[8] = 1f;
						}
					}
				}
				else if ((double)m_BorderGradientRate < 0.49999999999 || (double)m_BorderGradientRate > 0.5000000000001)
				{
					flag = true;
					blend3.Factors = new float[3];
					blend3.Positions = new float[3];
					blend3.Factors[0] = 0f;
					blend3.Positions[0] = 0f;
					blend3.Factors[1] = m_BorderGradientRate;
					blend3.Positions[1] = 1f - m_BorderGradientRate;
					blend3.Factors[2] = 1f;
					blend3.Positions[2] = 1f;
				}
				if (m_OuterBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new Rectangle(rectangle.Left - 1, rectangle.Top - 1, rectangle.Width + 2, rectangle.Height + 2), m_OuterBorderDarkColor, m_OuterBorderLightColor, m_BorderGradientAngle);
					if (flag)
					{
						linearGradientBrush.Blend = blend3;
					}
					Region region13 = new Region(graphicsPath);
					Region region14 = new Region(graphicsPath2);
					region13.Exclude(region14);
					g.FillRegion(linearGradientBrush, region13);
					Pen pen9 = new Pen(linearGradientBrush);
					g.DrawPath(pen9, graphicsPath);
					g.DrawPath(pen9, graphicsPath2);
					pen9.Dispose();
					linearGradientBrush.Dispose();
					region13.Dispose();
					region14.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region15 = new Region(graphicsPath2);
					region15.Exclude(graphicsPath3);
					SolidBrush solidBrush7 = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush7, region15);
					Pen pen10 = new Pen(solidBrush7);
					g.DrawPath(pen10, graphicsPath2);
					g.DrawPath(pen10, graphicsPath3);
					pen10.Dispose();
					solidBrush7.Dispose();
					region15.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					LinearGradientBrush linearGradientBrush2 = ((m_BorderGradientType != DAS_BorderGradientStyle.BGS_Linear) ? new LinearGradientBrush(rect, m_InnerBorderDarkColor, m_InnerBorderLightColor, m_BorderGradientAngle + 180) : new LinearGradientBrush(rect, m_InnerBorderLightColor, m_InnerBorderDarkColor, m_BorderGradientAngle));
					if (flag)
					{
						linearGradientBrush2.Blend = blend3;
					}
					Region region16 = new Region(graphicsPath3);
					Region region17 = new Region(graphicsPath4);
					region16.Exclude(region17);
					g.FillRegion(linearGradientBrush2, region16);
					Pen pen11 = new Pen(linearGradientBrush2);
					g.DrawPath(pen11, graphicsPath3);
					g.DrawPath(pen11, graphicsPath4);
					pen11.Dispose();
					linearGradientBrush2.Dispose();
					region16.Dispose();
					region17.Dispose();
				}
				break;
			}
			case DAS_BorderGradientStyle.BGS_Ring:
			{
				Blend blend = new Blend();
				blend.Factors = new float[3];
				blend.Positions = new float[3];
				Blend blend2 = blend;
				blend2.Factors[0] = 0f;
				blend2.Positions[0] = 0f;
				blend2.Factors[2] = 1f;
				blend2.Positions[2] = 1f;
				if (m_OuterBorderLength > 0)
				{
					blend2.Factors[1] = m_BorderGradientRate;
					blend2.Positions[1] = 1f - m_BorderGradientRate;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
					pathGradientBrush.SurroundColors = new Color[1] { m_OuterBorderDarkColor };
					pathGradientBrush.CenterColor = m_OuterBorderLightColor;
					PathGradientBrush pathGradientBrush2 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rectangle2.Width / (float)rectangle.Width;
					focusScales.Y = (float)rectangle2.Height / (float)rectangle.Height;
					pathGradientBrush2.FocusScales = focusScales;
					Region region = new Region(graphicsPath);
					Region region2 = new Region(graphicsPath2);
					region.Exclude(region2);
					g.FillRegion(pathGradientBrush2, region);
					Pen pen = new Pen(m_OuterBorderDarkColor);
					g.DrawPath(pen, graphicsPath);
					pen.Dispose();
					pathGradientBrush2.Dispose();
					region.Dispose();
					region2.Dispose();
				}
				if (m_MiddleBorderLength > 0)
				{
					Region region3 = new Region(graphicsPath2);
					region3.Exclude(graphicsPath3);
					SolidBrush solidBrush = new SolidBrush(m_MiddleBorderColor);
					g.FillRegion(solidBrush, region3);
					Pen pen2 = new Pen(solidBrush);
					g.DrawPath(pen2, graphicsPath2);
					g.DrawPath(pen2, graphicsPath3);
					pen2.Dispose();
					solidBrush.Dispose();
					region3.Dispose();
				}
				if (m_InnerBorderLength > 0)
				{
					blend2.Factors[1] = 1f - m_BorderGradientRate;
					blend2.Positions[1] = m_BorderGradientRate;
					PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath3);
					pathGradientBrush.SurroundColors = new Color[1] { m_InnerBorderLightColor };
					pathGradientBrush.Blend = blend2;
					pathGradientBrush.CenterColor = m_InnerBorderDarkColor;
					PathGradientBrush pathGradientBrush3 = pathGradientBrush;
					PointF focusScales = default(PointF);
					focusScales.X = (float)rectangle3.Width / (float)rect.Width;
					focusScales.Y = (float)rectangle3.Height / (float)rect.Height;
					pathGradientBrush3.FocusScales = focusScales;
					Region region4 = new Region(graphicsPath3);
					Region region5 = new Region(graphicsPath4);
					region4.Exclude(region5);
					g.FillRegion(pathGradientBrush3, region4);
					Pen pen3 = new Pen(m_InnerBorderDarkColor);
					g.DrawPath(pen3, graphicsPath4);
					pen3.Dispose();
					pathGradientBrush3.Dispose();
					region4.Dispose();
					region5.Dispose();
				}
				break;
			}
			}
			if (m_BorderExteriorLength > 0)
			{
				SolidBrush solidBrush9 = new SolidBrush(m_BorderExteriorColor);
				GraphicsPath graphicsPath8 = new GraphicsPath();
				method_6(graphicsPath8, rec_width, m_Shape, m_ArrowWidth + m_BorderExteriorLength + m_OuterBorderLength + m_MiddleBorderLength + m_InnerBorderLength, 0);
				Region region23 = new Region(graphicsPath8);
				region23.Exclude(graphicsPath);
				g.FillRegion(solidBrush9, region23);
				Pen pen15 = new Pen(solidBrush9);
				g.DrawPath(pen15, graphicsPath8);
				g.DrawPath(pen15, graphicsPath);
				pen15.Dispose();
				solidBrush9.Dispose();
				region23.Dispose();
				graphicsPath8.Dispose();
			}
			graphicsPath4.Dispose();
			graphicsPath3.Dispose();
			graphicsPath.Dispose();
		}

		internal void method_6(GraphicsPath graphicsPath_0, Rectangle rectangle_0, DAS_ShapeStyle das_ShapeStyle_0, int int_0, int int_1)
		{
			int num = int_0;
			int left = rectangle_0.Left;
			int top = rectangle_0.Top;
			int num2 = rectangle_0.Width - 1;
			int num3 = rectangle_0.Height - 1;
			float num4 = 0f;
			float num5 = 0f;
			if (num > num2 - 2)
			{
				num = num2 - 2;
			}
			if (num > num3 - 2)
			{
				num = num3 - 2;
			}
			switch (das_ShapeStyle_0)
			{
			case DAS_ShapeStyle.SS_Left_Triangle:
				num4 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Width * rectangle_0.Width) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) / ((double)rectangle_0.Height / 2.0));
				num5 = (float)((double)(int_1 * rectangle_0.Width) / (Math.Sqrt((double)(rectangle_0.Width * rectangle_0.Width) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) - (double)rectangle_0.Height / 2.0));
				graphicsPath_0.AddLine((float)left + num4, top + num3 / 2, left + num2 - int_1, (float)top + num5);
				graphicsPath_0.AddLine(left + num2 - int_1, (float)top + num5, left + num2 - int_1, (float)(top + num3) - num5);
				graphicsPath_0.AddLine(left + num2 - int_1, (float)(top + num3) - num5, (float)left + num4, top + num3 / 2);
				break;
			case DAS_ShapeStyle.SS_Right_Triangle:
				num4 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Width * rectangle_0.Width) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) / ((double)rectangle_0.Height / 2.0));
				num5 = (float)((double)(int_1 * rectangle_0.Width) / (Math.Sqrt((double)(rectangle_0.Width * rectangle_0.Width) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) - (double)rectangle_0.Height / 2.0));
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + num3 / 2, left + int_1, (float)top + num5);
				graphicsPath_0.AddLine(left + int_1, (float)top + num5, left + int_1, (float)(top + num3) - num5);
				graphicsPath_0.AddLine(left + int_1, (float)(top + num3) - num5, (float)(left + num2) - num4, top + num3 / 2);
				break;
			case DAS_ShapeStyle.SS_Top_Triangle:
				num5 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Width / 2.0));
				num4 = (float)((double)(int_1 * rectangle_0.Height) / (Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) - (double)rectangle_0.Width / 2.0));
				graphicsPath_0.AddLine(left + num2 / 2, (float)top + num5, (float)(left + num2) - num4, top + num3 - int_1);
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + num3 - int_1, (float)left + num4, top + num3 - int_1);
				graphicsPath_0.AddLine((float)left + num4, top + num3 - int_1, left + num2 / 2, (float)top + num5);
				break;
			case DAS_ShapeStyle.SS_Bottom_Triangle:
				num5 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Width / 2.0));
				num4 = (float)((double)(int_1 * rectangle_0.Height) / (Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) - (double)rectangle_0.Width / 2.0));
				graphicsPath_0.AddLine(left + num2 / 2, (float)(top + num3) - num5, (float)(left + num2) - num4, top + int_1);
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + int_1, (float)left + num4, top + int_1);
				graphicsPath_0.AddLine((float)left + num4, top + int_1, left + num2 / 2, (float)(top + num3) - num5);
				break;
			case DAS_ShapeStyle.SS_Diamond:
				num4 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height + rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Height / 2.0));
				num5 = (float)((double)int_1 * Math.Sqrt((double)(rectangle_0.Height * rectangle_0.Height + rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Width / 2.0));
				graphicsPath_0.AddLine(left + num2 / 2, (float)top + num5, (float)(left + num2) - num4, top + num3 / 2);
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + num3 / 2, left + num2 / 2, (float)(top + num3) - num5);
				graphicsPath_0.AddLine(left + num2 / 2, (float)(top + num3) - num5, (float)left + num4, top + num3 / 2);
				graphicsPath_0.AddLine((float)left + num4, top + num3 / 2, left + num2 / 2, (float)top + num5);
				break;
			case DAS_ShapeStyle.SS_Left_Arrow:
				num4 = (float)((double)int_1 * Math.Sqrt((double)(num * num) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) / ((double)rectangle_0.Height / 2.0));
				num5 = (float)((double)(int_1 * num) / (Math.Sqrt((double)(num * num) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) - (double)rectangle_0.Height / 2.0));
				graphicsPath_0.AddLine((float)left + num4, top + num3 / 2, left + num - int_1, (float)top + num5);
				graphicsPath_0.AddLine(left + num - int_1, (float)top + num5, left + num - int_1, top + (num3 - num) / 2 + int_1);
				graphicsPath_0.AddLine(left + num - int_1, top + (num3 - num) / 2 + int_1, left + num2 - int_1, top + (num3 - num) / 2 + int_1);
				graphicsPath_0.AddLine(left + num2 - int_1, top + (num3 - num) / 2 + int_1, left + num2 - int_1, top + (num3 + num) / 2 - int_1);
				graphicsPath_0.AddLine(left + num2 - int_1, top + (num3 + num) / 2 - int_1, left + num - int_1, top + (num3 + num) / 2 - int_1);
				graphicsPath_0.AddLine(left + num - int_1, top + (num3 + num) / 2 - int_1, left + num - int_1, (float)(top + num3) - num5);
				graphicsPath_0.AddLine(left + num - int_1, (float)(top + num3) - num5, (float)left + num4, top + num3 / 2);
				break;
			case DAS_ShapeStyle.SS_Right_Arrow:
				num4 = (float)((double)int_1 * Math.Sqrt((double)(num * num) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) / ((double)rectangle_0.Height / 2.0));
				num5 = (float)((double)(int_1 * num) / (Math.Sqrt((double)(num * num) + (double)(rectangle_0.Height * rectangle_0.Height) / 4.0) - (double)rectangle_0.Height / 2.0));
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + num3 / 2, left + num2 - num + int_1, (float)top + num5);
				graphicsPath_0.AddLine(left + num2 - num + int_1, (float)top + num5, left + num2 - num + int_1, top + (num3 - num) / 2 + int_1);
				graphicsPath_0.AddLine(left + num2 - num + int_1, top + (num3 - num) / 2 + int_1, left + int_1, top + (num3 - num) / 2 + int_1);
				graphicsPath_0.AddLine(left + int_1, top + (num3 - num) / 2 + int_1, left + int_1, top + (num3 + num) / 2 - int_1);
				graphicsPath_0.AddLine(left + int_1, top + (num3 + num) / 2 - int_1, left + num2 - num + int_1, top + (num3 + num) / 2 - int_1);
				graphicsPath_0.AddLine(left + num2 - num + int_1, top + (num3 + num) / 2 - int_1, left + num2 - num + int_1, (float)(top + num3) - num5);
				graphicsPath_0.AddLine(left + num2 - num + int_1, (float)(top + num3) - num5, (float)(left + num2) - num4, top + num3 / 2);
				break;
			case DAS_ShapeStyle.SS_Top_Arrow:
				num5 = (float)((double)int_1 * Math.Sqrt((double)(num * num) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Width / 2.0));
				num4 = (float)((double)(int_1 * num) / (Math.Sqrt((double)(num * num) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) - (double)rectangle_0.Width / 2.0));
				graphicsPath_0.AddLine(left + num2 / 2, (float)top + num5, (float)left + num4, top + num - int_1);
				graphicsPath_0.AddLine((float)left + num4, top + num - int_1, left + (num2 - num) / 2 + int_1, top + num - int_1);
				graphicsPath_0.AddLine(left + (num2 - num) / 2 + int_1, top + num - int_1, left + (num2 - num) / 2 + int_1, top + num3 - int_1);
				graphicsPath_0.AddLine(left + (num2 - num) / 2 + int_1, top + num3 - int_1, left + (num2 + num) / 2 - int_1, top + num3 - int_1);
				graphicsPath_0.AddLine(left + (num2 + num) / 2 - int_1, top + num3 - int_1, left + (num2 + num) / 2 - int_1, top + num - int_1);
				graphicsPath_0.AddLine(left + (num2 + num) / 2 - int_1, top + num - int_1, (float)(left + num2) - num4, top + num - int_1);
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + num - int_1, left + num2 / 2, (float)top + num5);
				break;
			case DAS_ShapeStyle.SS_Bottom_Arrow:
				num5 = (float)((double)int_1 * Math.Sqrt((double)(num * num) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) / ((double)rectangle_0.Width / 2.0));
				num4 = (float)((double)(int_1 * num) / (Math.Sqrt((double)(num * num) + (double)(rectangle_0.Width * rectangle_0.Width) / 4.0) - (double)rectangle_0.Width / 2.0));
				graphicsPath_0.AddLine(left + num2 / 2, (float)(top + num3) - num5, (float)left + num4, top + (num3 - num) + int_1);
				graphicsPath_0.AddLine((float)left + num4, top + (num3 - num) + int_1, left + (num2 - num) / 2 + int_1, top + (num3 - num) + int_1);
				graphicsPath_0.AddLine(left + (num2 - num) / 2 + int_1, top + (num3 - num) + int_1, left + (num2 - num) / 2 + int_1, top + int_1);
				graphicsPath_0.AddLine(left + (num2 - num) / 2 + int_1, top + int_1, left + (num2 + num) / 2 - int_1, top + int_1);
				graphicsPath_0.AddLine(left + (num2 + num) / 2 - int_1, top + int_1, left + (num2 + num) / 2 - int_1, top + (num3 - num) + int_1);
				graphicsPath_0.AddLine(left + (num2 + num) / 2 - int_1, top + (num3 - num) + int_1, (float)(left + num2) - num4, top + (num3 - num) + int_1);
				graphicsPath_0.AddLine((float)(left + num2) - num4, top + (num3 - num) + int_1, left + num2 / 2, (float)(top + num3) - num5);
				break;
			}
			graphicsPath_0.CloseFigure();
		}

		internal void method_7(Graphics graphics_0, Rectangle rectangle_0, GraphicsPath graphicsPath_0, Color color_0, int int_0, float float_0)
		{
			graphics_0.TranslateTransform(int_0, int_0);
			PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath_0);
			Blend blend = new Blend();
			blend.Factors = new float[3];
			blend.Positions = new float[3];
			Blend blend2 = blend;
			blend2.Factors[0] = 0f;
			blend2.Positions[0] = 0f;
			blend2.Factors[2] = 1f;
			blend2.Positions[2] = 1f;
			blend2.Factors[1] = float_0;
			blend2.Positions[1] = 1f - float_0;
			pathGradientBrush.SurroundColors = new Color[1] { Color.Transparent };
			pathGradientBrush.Blend = blend2;
			pathGradientBrush.CenterColor = color_0;
			PointF focusScales = default(PointF);
			focusScales.X = (float)(rectangle_0.Width - 2 * int_0) / (float)rectangle_0.Width;
			focusScales.Y = (float)(rectangle_0.Height - 2 * int_0) / (float)rectangle_0.Height;
			pathGradientBrush.FocusScales = focusScales;
			graphics_0.FillPath(pathGradientBrush, graphicsPath_0);
			pathGradientBrush.Dispose();
			graphics_0.ResetTransform();
		}
	}
}
