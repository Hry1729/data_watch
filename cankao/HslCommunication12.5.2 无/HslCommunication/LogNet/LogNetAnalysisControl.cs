using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication.BasicFramework;

namespace HslCommunication.LogNet
{
	/// <summary>
	/// 一个用于日志分析的控件
	/// </summary>
	public class LogNetAnalysisControl : UserControl
	{
		private class PaintItem
		{
			public DateTime Start { get; set; }

			public DateTime End { get; set; }

			public int Count { get; set; }
		}

		private string m_LogSource = string.Empty;

		private Button selectButton = null;

		private List<DateTime> listPaint = new List<DateTime>();

		private List<PaintItem> listRender = new List<PaintItem>();

		private StringFormat stringFormat = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		/// <summary> 
		/// 必需的设计器变量。
		/// </summary>
		private IContainer components = null;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label1;

		private TextBox textBox3;

		private Button userButton_Debug;

		private Button userButton_Info;

		private Button userButton_Warn;

		private Button userButton_Error;

		private Button userButton_Fatal;

		private Button userButton_All;

		private Label label2;

		private Button userButton_source;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private PictureBox pictureBox1;

		private CheckBox checkBox1;

		private TextBox textBox4;

		private CheckBox checkBox2;

		private bool IsMouseEnter { get; set; }

		private PaintItem ClickSelected { get; set; }

		private Point pointMove { get; set; }

		/// <summary>
		/// 实例化一个控件信息
		/// </summary>
		public LogNetAnalysisControl()
		{
			InitializeComponent();
		}

		private void LogNetAnalysisControl_Load(object sender, EventArgs e)
		{
			userButton_Debug.Text = StringResources.Language.LogNetDebug ?? "";
			userButton_Info.Text = StringResources.Language.LogNetInfo ?? "";
			userButton_Warn.Text = StringResources.Language.LogNetWarn ?? "";
			userButton_Error.Text = StringResources.Language.LogNetError ?? "";
			userButton_Fatal.Text = StringResources.Language.LogNetFatal ?? "";
			userButton_All.Text = StringResources.Language.LogNetAll ?? "";
			label2.Text = StringResources.Language.LogNetTimeSelect;
			checkBox1.Text = StringResources.Language.LogNetUseExpress;
			tabPage1.Text = StringResources.Language.LogNetDataView;
			tabPage2.Text = StringResources.Language.LogNetDistributedView;
			userButton_source.Text = StringResources.Language.LogNetSource;
			checkBox2.Text = StringResources.Language.LogNetCharZero;
		}

		/// <summary>
		/// 设置日志的数据源
		/// </summary>
		/// <param name="logSource">直接从日志文件中读到的数据或是来自网络的数据</param>
		public void SetLogNetSource(string logSource)
		{
			m_LogSource = logSource;
			SetLogNetSourceView();
		}

		private void SetLogNetSourceView()
		{
			if (!string.IsNullOrEmpty(m_LogSource))
			{
				AnalysisLogSource(DateTime.MinValue, DateTime.MaxValue, StringResources.Language.LogNetAll);
				selectButton = userButton_All;
			}
		}

		/// <summary>
		/// 从现有的日志中筛选数据
		/// </summary>
		/// <param name="degree">等级</param>
		private void FilterLogSource(string degree)
		{
			if (!string.IsNullOrEmpty(m_LogSource))
			{
				DateTime result = DateTime.MinValue;
				DateTime result2 = DateTime.MaxValue;
				if (!DateTime.TryParse(textBox2.Text, out result))
				{
					MessageBox.Show(StringResources.Language.LogNetStartTimeWrong);
				}
				else if (!DateTime.TryParse(textBox3.Text, out result2))
				{
					MessageBox.Show(StringResources.Language.LogNetFinishTimeWrong);
				}
				else
				{
					AnalysisLogSource(result, result2, degree);
				}
			}
		}

		/// <summary>
		/// 底层的数据分析筛选
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="degree"></param>
		private void AnalysisLogSource(DateTime start, DateTime end, string degree)
		{
			if (string.IsNullOrEmpty(m_LogSource))
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			List<Match> list = new List<Match>(Regex.Matches(m_LogSource, "\u0002\\[[^\u0002]+").OfType<Match>());
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			List<DateTime> list2 = new List<DateTime>();
			for (int i = 0; i < list.Count; i++)
			{
				Match match = list[i];
				string text = match.Value.Substring(2, 5);
				DateTime dateTime = Convert.ToDateTime(match.Value.Substring(match.Value.IndexOf('2'), 19));
				if (start == DateTime.MinValue)
				{
					if (i == 0)
					{
						textBox2.Text = match.Value.Substring(match.Value.IndexOf('2'), 19);
					}
					if (i == list.Count - 1)
					{
						textBox3.Text = match.Value.Substring(match.Value.IndexOf('2'), 19);
					}
				}
				if (start <= dateTime && dateTime <= end && (!checkBox1.Checked || Regex.IsMatch(match.Value, textBox4.Text)))
				{
					if (text.StartsWith(StringResources.Language.LogNetDebug))
					{
						num++;
					}
					else if (text.StartsWith(StringResources.Language.LogNetInfo))
					{
						num2++;
					}
					else if (text.StartsWith(StringResources.Language.LogNetWarn))
					{
						num3++;
					}
					else if (text.StartsWith(StringResources.Language.LogNetError))
					{
						num4++;
					}
					else if (text.StartsWith(StringResources.Language.LogNetFatal))
					{
						num5++;
					}
					num6++;
					if (degree == StringResources.Language.LogNetAll || text.StartsWith(degree))
					{
						stringBuilder.Append(match.Value.Substring(1));
						list2.Add(dateTime);
					}
				}
			}
			userButton_Debug.Text = $"{StringResources.Language.LogNetDebug} ({num})";
			userButton_Info.Text = $"{StringResources.Language.LogNetInfo} ({num2})";
			userButton_Warn.Text = $"{StringResources.Language.LogNetWarn} ({num3})";
			userButton_Error.Text = $"{StringResources.Language.LogNetError} ({num4})";
			userButton_Fatal.Text = $"{StringResources.Language.LogNetFatal} ({num5})";
			userButton_All.Text = $"{StringResources.Language.LogNetAll} ({num6})";
			if (checkBox2.Checked)
			{
				stringBuilder = stringBuilder.Replace("\0", "\\0");
			}
			textBox1.Text = stringBuilder.ToString();
			listPaint = list2;
			if (pictureBox1.Width > 10)
			{
				pictureBox1.Image = PaintData(pictureBox1.Width, pictureBox1.Height);
			}
		}

		private void UserButtonSetSelected(Button userButton)
		{
			if (selectButton != userButton)
			{
				selectButton = userButton;
			}
		}

		private void userButton_Debug_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_Debug);
			FilterLogSource(StringResources.Language.LogNetDebug);
		}

		private void userButton_Info_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_Info);
			FilterLogSource(StringResources.Language.LogNetInfo);
		}

		private void userButton_Warn_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_Warn);
			FilterLogSource(StringResources.Language.LogNetWarn);
		}

		private void userButton_Error_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_Error);
			FilterLogSource(StringResources.Language.LogNetError);
		}

		private void userButton_Fatal_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_Fatal);
			FilterLogSource(StringResources.Language.LogNetFatal);
		}

		private void userButton_All_Click(object sender, EventArgs e)
		{
			UserButtonSetSelected(userButton_All);
			FilterLogSource(StringResources.Language.LogNetAll);
		}

		private void userButton_source_Click(object sender, EventArgs e)
		{
			SetLogNetSourceView();
		}

		private Bitmap PaintData(int width, int height)
		{
			if (width < 200)
			{
				width = 200;
			}
			if (height < 100)
			{
				height = 100;
			}
			Bitmap bitmap = new Bitmap(width, height);
			Graphics graphics = Graphics.FromImage(bitmap);
			Font font = new Font("宋体", 12f);
			StringFormat stringFormat = new StringFormat
			{
				Alignment = StringAlignment.Far,
				LineAlignment = StringAlignment.Center
			};
			Pen pen = new Pen(Color.LightGray, 1f);
			pen.DashStyle = DashStyle.Custom;
			pen.DashPattern = new float[2] { 5f, 5f };
			graphics.Clear(Color.White);
			if (listPaint.Count <= 5)
			{
				using (StringFormat format = new StringFormat
				{
					Alignment = StringAlignment.Center,
					LineAlignment = StringAlignment.Center
				})
				{
					graphics.DrawString(StringResources.Language.LogNetDataTooLittle, font, Brushes.DeepSkyBlue, new Rectangle(0, 0, width, height), format);
				}
			}
			else
			{
				int num = (width - 60) / 6;
				TimeSpan timeSpan = listPaint.Max() - listPaint.Min();
				DateTime dateTime = listPaint.Min();
				double num2 = timeSpan.TotalSeconds / (double)num;
				int[] array = new int[num];
				for (int i = 0; i < listPaint.Count; i++)
				{
					int num3 = (int)((listPaint[i] - dateTime).TotalSeconds / num2);
					if (num3 < 0)
					{
						num3 = 0;
					}
					if (num3 == num)
					{
						num3--;
					}
					array[num3]++;
				}
				int num4 = array.Max();
				int min = 0;
				PaintItem[] array2 = new PaintItem[num];
				for (int j = 0; j < array.Length; j++)
				{
					PaintItem paintItem = new PaintItem();
					paintItem.Count = array[j];
					paintItem.Start = listPaint[0].AddSeconds((double)j * num2);
					if (j == array.Length - 1)
					{
						paintItem.End = listPaint[listPaint.Count - 1];
					}
					else
					{
						paintItem.End = listPaint[0].AddSeconds((double)(j + 1) * num2);
					}
					array2[j] = paintItem;
				}
				listRender = new List<PaintItem>(array2);
				int num5 = 50;
				int num6 = 10;
				int num7 = 20;
				int num8 = 30;
				graphics.DrawLine(Pens.DimGray, num5, num7 - 10, num5, height - num8);
				graphics.DrawLine(Pens.DimGray, num5, height - num8 + 1, width - num6, height - num8 + 1);
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				SoftPainting.PaintTriangle(graphics, Brushes.DimGray, new Point(num5, num7 - 10), 5, GraphDirection.Upward);
				graphics.SmoothingMode = SmoothingMode.None;
				int degree = 8;
				if (height >= 500)
				{
					degree = ((height < 700) ? ((num4 >= 25 || num4 <= 1) ? 16 : num4) : ((num4 >= 40 || num4 <= 1) ? 24 : num4));
				}
				else if (num4 < 15 && num4 > 1)
				{
					degree = num4;
				}
				SoftPainting.PaintCoordinateDivide(graphics, Pens.DimGray, pen, font, Brushes.DimGray, stringFormat, degree, num4, min, width, height, num5, num6, num7, num8);
				stringFormat.Alignment = StringAlignment.Center;
				graphics.DrawString("Totle: " + listPaint.Count, font, Brushes.DodgerBlue, new RectangleF(num5, 0f, width - num5 - num6, num7), stringFormat);
				int num9 = num5 + 2;
				for (int k = 0; k < array2.Length; k++)
				{
					float num10 = SoftPainting.ComputePaintLocationY(num4, min, height - num7 - num8, array2[k].Count) + (float)num7;
					RectangleF rect = new RectangleF(num9, num10, 5f, (float)(height - num8) - num10);
					if (rect.Height <= 0f && array2[k].Count > 0)
					{
						rect = new RectangleF(num9, height - num8 - 1, 5f, 1f);
					}
					graphics.FillRectangle(Brushes.Tomato, rect);
					num9 += 6;
				}
				graphics.DrawLine(Pens.DimGray, num9, num7 - 10, num9, height - num8);
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				SoftPainting.PaintTriangle(graphics, Brushes.DimGray, new Point(num9, num7 - 10), 5, GraphDirection.Upward);
				graphics.SmoothingMode = SmoothingMode.None;
			}
			stringFormat.Dispose();
			font.Dispose();
			pen.Dispose();
			graphics.Dispose();
			return bitmap;
		}

		private void pictureBox1_Paint(object sender, PaintEventArgs e)
		{
			if (IsMouseEnter && ClickSelected != null && pictureBox1.Width > 100)
			{
				string s = ClickSelected.Start.ToString("yyyy-MM-dd HH:mm:ss") + "  -  " + ClickSelected.End.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + "Count:" + ClickSelected.Count;
				e.Graphics.DrawString(s, Font, Brushes.DimGray, new Rectangle(50, pictureBox1.Height - 27, pictureBox1.Width - 60, 30), stringFormat);
				e.Graphics.DrawLine(Pens.DeepPink, pointMove.X, 15, pointMove.X, pictureBox1.Height - 30);
			}
		}

		private void pictureBox1_MouseEnter(object sender, EventArgs e)
		{
			IsMouseEnter = true;
		}

		private void pictureBox1_MouseLeave(object sender, EventArgs e)
		{
			IsMouseEnter = false;
			pictureBox1.Refresh();
		}

		private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
		{
			if (IsMouseEnter && e.Y > 20 && e.Y < pictureBox1.Height - 30 && e.X > 51 && e.X < pictureBox1.Width - 10 && (e.X - 52) % 6 != 5)
			{
				int num = (e.X - 52) / 6;
				if (num < listRender.Count)
				{
					pointMove = e.Location;
					ClickSelected = listRender[num];
					pictureBox1.Refresh();
				}
			}
		}

		private void pictureBox1_SizeChanged(object sender, EventArgs e)
		{
			if (pictureBox1.Width > 10)
			{
				pictureBox1.Image = PaintData(pictureBox1.Width, pictureBox1.Height);
			}
		}

		private void pictureBox1_DoubleClick(object sender, EventArgs e)
		{
			if (IsMouseEnter && pointMove.Y > 20 && pointMove.Y < pictureBox1.Height - 30 && pointMove.X > 51 && pointMove.X < pictureBox1.Width - 10 && selectButton != null && (ClickSelected.End - ClickSelected.Start).TotalSeconds > 3.0)
			{
				textBox2.Text = ClickSelected.Start.ToString("yyyy-MM-dd HH:mm:ss");
				textBox3.Text = ClickSelected.End.ToString("yyyy-MM-dd HH:mm:ss");
				AnalysisLogSource(ClickSelected.Start, ClickSelected.End, selectButton.Text.Substring(0, 2));
			}
		}

		/// <summary> 
		/// 清理所有正在使用的资源。
		/// </summary>
		/// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		/// <summary> 
		/// 设计器支持所需的方法 - 不要修改
		/// 使用代码编辑器修改此方法的内容。
		/// </summary>
		private void InitializeComponent()
		{
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.userButton_source = new System.Windows.Forms.Button();
			this.userButton_All = new System.Windows.Forms.Button();
			this.userButton_Fatal = new System.Windows.Forms.Button();
			this.userButton_Error = new System.Windows.Forms.Button();
			this.userButton_Warn = new System.Windows.Forms.Button();
			this.userButton_Info = new System.Windows.Forms.Button();
			this.userButton_Debug = new System.Windows.Forms.Button();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.textBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBox1.Font = new System.Drawing.Font("宋体", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(3, 3);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(728, 439);
			this.textBox1.TabIndex = 0;
			this.textBox2.Font = new System.Drawing.Font("宋体", 10.5f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox2.Location = new System.Drawing.Point(92, 4);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(156, 23);
			this.textBox2.TabIndex = 2;
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(264, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(28, 17);
			this.label1.TabIndex = 3;
			this.label1.Text = "----";
			this.textBox3.Font = new System.Drawing.Font("宋体", 10.5f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox3.Location = new System.Drawing.Point(304, 4);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(156, 23);
			this.textBox3.TabIndex = 4;
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.Location = new System.Drawing.Point(3, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 12;
			this.label2.Text = "时间选择：";
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.tabControl1.Location = new System.Drawing.Point(6, 34);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(742, 475);
			this.tabControl1.TabIndex = 15;
			this.tabPage1.Controls.Add(this.textBox1);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(734, 445);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "数据视图";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.tabPage2.Controls.Add(this.pictureBox1);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(734, 440);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "分布视图";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.pictureBox1.Location = new System.Drawing.Point(6, 11);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(708, 402);
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.SizeChanged += new System.EventHandler(pictureBox1_SizeChanged);
			this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(pictureBox1_Paint);
			this.pictureBox1.DoubleClick += new System.EventHandler(pictureBox1_DoubleClick);
			this.pictureBox1.MouseEnter += new System.EventHandler(pictureBox1_MouseEnter);
			this.pictureBox1.MouseLeave += new System.EventHandler(pictureBox1_MouseLeave);
			this.pictureBox1.MouseMove += new System.Windows.Forms.MouseEventHandler(pictureBox1_MouseMove);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.checkBox1.Location = new System.Drawing.Point(514, 5);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(111, 21);
			this.checkBox1.TabIndex = 16;
			this.checkBox1.Text = "使用正则表达式";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Font = new System.Drawing.Font("宋体", 10.5f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox4.Location = new System.Drawing.Point(514, 27);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(227, 23);
			this.textBox4.TabIndex = 17;
			this.userButton_source.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.userButton_source.BackColor = System.Drawing.Color.Transparent;
			this.userButton_source.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_source.Location = new System.Drawing.Point(650, 513);
			this.userButton_source.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_source.Name = "userButton_source";
			this.userButton_source.Size = new System.Drawing.Size(98, 25);
			this.userButton_source.TabIndex = 13;
			this.userButton_source.Text = "源日志";
			this.userButton_source.UseVisualStyleBackColor = false;
			this.userButton_source.Click += new System.EventHandler(userButton_source_Click);
			this.userButton_All.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_All.BackColor = System.Drawing.Color.Transparent;
			this.userButton_All.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_All.Location = new System.Drawing.Point(525, 513);
			this.userButton_All.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_All.Name = "userButton_All";
			this.userButton_All.Size = new System.Drawing.Size(98, 25);
			this.userButton_All.TabIndex = 11;
			this.userButton_All.Text = "全部";
			this.userButton_All.UseVisualStyleBackColor = false;
			this.userButton_All.Click += new System.EventHandler(userButton_All_Click);
			this.userButton_Fatal.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_Fatal.BackColor = System.Drawing.Color.Transparent;
			this.userButton_Fatal.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_Fatal.Location = new System.Drawing.Point(422, 513);
			this.userButton_Fatal.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_Fatal.Name = "userButton_Fatal";
			this.userButton_Fatal.Size = new System.Drawing.Size(98, 25);
			this.userButton_Fatal.TabIndex = 10;
			this.userButton_Fatal.Text = "致命";
			this.userButton_Fatal.UseVisualStyleBackColor = false;
			this.userButton_Fatal.Click += new System.EventHandler(userButton_Fatal_Click);
			this.userButton_Error.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_Error.BackColor = System.Drawing.Color.Transparent;
			this.userButton_Error.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_Error.Location = new System.Drawing.Point(318, 513);
			this.userButton_Error.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_Error.Name = "userButton_Error";
			this.userButton_Error.Size = new System.Drawing.Size(98, 25);
			this.userButton_Error.TabIndex = 9;
			this.userButton_Error.Text = "错误";
			this.userButton_Error.UseVisualStyleBackColor = false;
			this.userButton_Error.Click += new System.EventHandler(userButton_Error_Click);
			this.userButton_Warn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_Warn.BackColor = System.Drawing.Color.Transparent;
			this.userButton_Warn.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_Warn.Location = new System.Drawing.Point(214, 513);
			this.userButton_Warn.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_Warn.Name = "userButton_Warn";
			this.userButton_Warn.Size = new System.Drawing.Size(98, 25);
			this.userButton_Warn.TabIndex = 8;
			this.userButton_Warn.Text = "警告";
			this.userButton_Warn.UseVisualStyleBackColor = false;
			this.userButton_Warn.Click += new System.EventHandler(userButton_Warn_Click);
			this.userButton_Info.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_Info.BackColor = System.Drawing.Color.Transparent;
			this.userButton_Info.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_Info.Location = new System.Drawing.Point(110, 513);
			this.userButton_Info.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_Info.Name = "userButton_Info";
			this.userButton_Info.Size = new System.Drawing.Size(98, 25);
			this.userButton_Info.TabIndex = 7;
			this.userButton_Info.Text = "信息";
			this.userButton_Info.UseVisualStyleBackColor = false;
			this.userButton_Info.Click += new System.EventHandler(userButton_Info_Click);
			this.userButton_Debug.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.userButton_Debug.BackColor = System.Drawing.Color.Transparent;
			this.userButton_Debug.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton_Debug.Location = new System.Drawing.Point(6, 513);
			this.userButton_Debug.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton_Debug.Name = "userButton_Debug";
			this.userButton_Debug.Size = new System.Drawing.Size(98, 25);
			this.userButton_Debug.TabIndex = 6;
			this.userButton_Debug.Text = "调试";
			this.userButton_Debug.UseVisualStyleBackColor = false;
			this.userButton_Debug.Click += new System.EventHandler(userButton_Debug_Click);
			this.checkBox2.AutoSize = true;
			this.checkBox2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.checkBox2.Location = new System.Drawing.Point(669, 4);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(67, 21);
			this.checkBox2.TabIndex = 18;
			this.checkBox2.Text = "\\0 转义";
			this.checkBox2.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.checkBox2);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.checkBox1);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.userButton_source);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.userButton_All);
			base.Controls.Add(this.userButton_Fatal);
			base.Controls.Add(this.userButton_Error);
			base.Controls.Add(this.userButton_Warn);
			base.Controls.Add(this.userButton_Info);
			base.Controls.Add(this.userButton_Debug);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox2);
			base.Name = "LogNetAnalysisControl";
			base.Size = new System.Drawing.Size(752, 542);
			base.Load += new System.EventHandler(LogNetAnalysisControl_Load);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
