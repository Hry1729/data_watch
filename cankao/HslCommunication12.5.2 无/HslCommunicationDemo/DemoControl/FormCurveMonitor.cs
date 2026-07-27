using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslControls;

namespace HslCommunicationDemo.DemoControl
{
	public class FormCurveMonitor : HslFormContent
	{
		private class LineItem
		{
			public float[] Data = new float[0];

			public ValueLimit ValueLimit = default(ValueLimit);

			public string Address { get; set; } = string.Empty;

			public string Name { get; set; } = string.Empty;

			public string Type { get; set; } = "short";

			public Color Color { get; set; } = Color.Blue;

			public CurveStyle Style { get; set; } = CurveStyle.Curve;

			public float Value { get; set; } = 0f;

			public void SetValue(int dataCountMax, int countMin)
			{
				ValueLimit.SetNewValue(Value);
				SoftBasic.AddArrayData(ref Data, new float[1] { Value }, dataCountMax);
				if (Data.Length < countMin)
				{
					Data = SoftBasic.SpliceArray<float>(GetFloatNaN(countMin - Data.Length), Data);
				}
			}

			private float[] GetFloatNaN(int count)
			{
				float[] array = new float[count];
				for (int i = 0; i < count; i++)
				{
					array[i] = float.NaN;
				}
				return array;
			}

			public string GetCurveName()
			{
				if (string.IsNullOrEmpty(Name))
				{
					return Address;
				}
				return Name;
			}
		}

		private IReadWriteNet readWriteNet;

		private int timeSpan = 1000;

		private Thread thread;

		private bool isQuit = false;

		private DateTime[] times = new DateTime[0];

		private bool threadReadEnable = false;

		private int dataCountMax = 3000;

		private List<LineItem> lineItems = new List<LineItem>();

		private object lockObject = new object();

		private IContainer components = null;

		private TextBox textBox_curve_address;

		private Label label6;

		private TextBox textBox1;

		private Label label1;

		private PropertyGrid propertyGrid1;

		private Label label2;

		private Button button_cancel;

		private UserControlHead userControlHead1;

		private Button button_read;

		private Label label4;

		private ComboBox comboBox_type;

		private Label label5;

		private Label label_color;

		private Label label8;

		private ComboBox comboBox_line_style;

		private HslCurveHistory hslCurveHistory1;

		private Panel panel1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private Panel panel2;

		private TabPage tabPage2;

		private Panel panel3;

		private DataGridView dataGridView1;

		private SplitContainer splitContainer1;

		private Panel panel4;

		private Label label7;

		private Button button_timer_start;

		private Button button_timer_stop;

		private TextBox textBox2;

		private Label label9;

		private Label label10;

		private TextBox textBox_curve_name;

		private Button button_clear_all;

		private DataGridViewTextBoxColumn Column_name;

		private DataGridViewImageColumn Column_color;

		private DataGridViewTextBoxColumn Column_value;

		private DataGridViewTextBoxColumn Column_max;

		private DataGridViewTextBoxColumn Column_min;

		private Label label_time_cost;

		private Label label3;

		public FormCurveMonitor()
		{
			InitializeComponent();
		}

		private void FormCurveMonitor_Load(object sender, EventArgs e)
		{
			propertyGrid1.SelectedObject = hslCurveHistory1;
			comboBox_line_style.DataSource = SoftBasic.GetEnumValues<CurveStyle>();
			comboBox_type.DataSource = new string[9] { "bool", "short", "ushort", "int", "uint", "long", "ulong", "float", "double" };
			comboBox_type.SelectedIndex = 1;
			userControlHead1.SetText1((Program.Language == 1) ? "连接对象:" : "Device:");
		}

		public void SetReadWrite(IReadWriteNet readWrite, string address)
		{
			readWriteNet = readWrite;
			textBox_curve_address.Text = address;
			thread = new Thread(ThreadRead);
			thread.IsBackground = false;
			thread.Start();
			if (readWrite != null)
			{
				userControlHead1.HelpLink = readWrite.ToString();
			}
		}

		private void button_timer_start_Click(object sender, EventArgs e)
		{
			try
			{
				timeSpan = int.Parse(textBox1.Text);
				dataCountMax = int.Parse(textBox2.Text);
				threadReadEnable = true;
				button_timer_start.Enabled = false;
				button_timer_stop.Enabled = true;
			}
			catch (Exception)
			{
				DemoUtils.ShowMessage("时间间隔必须是数字！");
			}
		}

		private void button_timer_stop_Click(object sender, EventArgs e)
		{
			threadReadEnable = false;
			button_timer_start.Enabled = true;
			button_timer_stop.Enabled = false;
		}

		private void ThreadRead()
		{
			do
			{
				IL_059a:
				Thread.Sleep(timeSpan);
				if (isQuit)
				{
					break;
				}
				if (readWriteNet != null && threadReadEnable)
				{
					LineItem[] lineItems = null;
					lock (lockObject)
					{
						lineItems = this.lineItems.ToArray();
					}
					DateTime start = DateTime.Now;
					for (int i = 0; i < lineItems.Length; i++)
					{
						if (isQuit)
						{
							return;
						}
						LineItem lineItem = lineItems[i];
						if (string.IsNullOrEmpty(lineItem.Address) || string.IsNullOrEmpty(lineItem.Type))
						{
							continue;
						}
						if (lineItem.Type == "bool")
						{
							OperateResult<bool> operateResult = readWriteNet.ReadBool(lineItem.Address);
							if (!operateResult.IsSuccess)
							{
								DemoUtils.ShowMessage("Read [" + lineItem.Address + "] Failed: " + operateResult.Message);
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = (operateResult.Content ? 100f : 0f);
							}
						}
						else if (lineItem.Type == "short")
						{
							OperateResult<short> operateResult2 = readWriteNet.ReadInt16(lineItem.Address);
							if (!operateResult2.IsSuccess)
							{
								DemoUtils.ShowMessage("Read [" + lineItem.Address + "] Failed: " + operateResult2.Message);
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult2.Content;
							}
						}
						else if (lineItem.Type == "ushort")
						{
							OperateResult<ushort> operateResult3 = readWriteNet.ReadUInt16(lineItem.Address);
							if (!operateResult3.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = (int)operateResult3.Content;
							}
						}
						else if (lineItem.Type == "int")
						{
							OperateResult<int> operateResult4 = readWriteNet.ReadInt32(lineItem.Address);
							if (!operateResult4.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult4.Content;
							}
						}
						else if (lineItem.Type == "uint")
						{
							OperateResult<uint> operateResult5 = readWriteNet.ReadUInt32(lineItem.Address);
							if (!operateResult5.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult5.Content;
							}
						}
						else if (lineItem.Type == "long")
						{
							OperateResult<long> operateResult6 = readWriteNet.ReadInt64(lineItem.Address);
							if (!operateResult6.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult6.Content;
							}
						}
						else if (lineItem.Type == "ulong")
						{
							OperateResult<ulong> operateResult7 = readWriteNet.ReadUInt64(lineItem.Address);
							if (!operateResult7.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult7.Content;
							}
						}
						else if (lineItem.Type == "float")
						{
							OperateResult<float> operateResult8 = readWriteNet.ReadFloat(lineItem.Address);
							if (!operateResult8.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = operateResult8.Content;
							}
						}
						else if (lineItem.Type == "double")
						{
							OperateResult<double> operateResult9 = readWriteNet.ReadDouble(lineItem.Address);
							if (!operateResult9.IsSuccess)
							{
								DemoUtils.ShowMessage("Read Failed");
								return;
							}
							if (!isQuit)
							{
								lineItem.Value = (float)operateResult9.Content;
							}
						}
					}
					try
					{
						if (lineItems.Length != 0)
						{
							if (isQuit)
							{
								continue;
							}
							Invoke((Action)delegate
							{
								SoftBasic.AddArrayData(ref times, new DateTime[1] { DateTime.Now }, dataCountMax);
								hslCurveHistory1.SetDateTimes(times);
								foreach (LineItem lineItem2 in lineItems)
								{
									lineItem2.SetValue(dataCountMax, times.Length);
									hslCurveHistory1.SetLeftCurve(lineItem2.GetCurveName(), lineItem2.Data, lineItem2.Color, lineItem2.Style);
								}
								hslCurveHistory1.ScrollToRight();
								hslCurveHistory1.RenderCurveUI();
								RenderLineItems(lineItems);
								label_time_cost.Text = $"{(DateTime.Now - start).TotalMilliseconds:F0} ms";
							});
							continue;
						}
					}
					catch (Exception)
					{
						continue;
					}
				}
				goto IL_059a;
			}
			while (!isQuit);
		}

		private async void FormCurveMonitor_FormClosing(object sender, FormClosingEventArgs e)
		{
			isQuit = true;
			await Task.Delay(200);
		}

		private void label_color_Click(object sender, EventArgs e)
		{
			using (ColorDialog colorDialog = new ColorDialog())
			{
				if (colorDialog.ShowDialog() == DialogResult.OK)
				{
					label_color.BackColor = colorDialog.Color;
				}
			}
		}

		private void button_read_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_curve_address.Text))
			{
				DemoUtils.ShowMessage("地址不能为空！");
				return;
			}
			LineItem lineItem = new LineItem();
			lineItem.Address = textBox_curve_address.Text;
			if (string.IsNullOrEmpty(textBox_curve_name.Text))
			{
				lineItem.Name = lineItem.Address;
			}
			else
			{
				lineItem.Name = textBox_curve_name.Text;
			}
			lineItem.Type = comboBox_type.Text;
			lineItem.Color = label_color.BackColor;
			lineItem.Style = (CurveStyle)comboBox_line_style.SelectedItem;
			lock (lockObject)
			{
				if (lineItems.Any((LineItem m) => m.GetCurveName() == lineItem.GetCurveName()))
				{
					DemoUtils.ShowMessage("当前地址已经存在，请修改后重新添加！");
					return;
				}
				lineItems.Add(lineItem);
			}
			if (tabControl1.SelectedTab != tabPage2)
			{
				tabControl1.SelectedTab = tabPage2;
			}
			RenderLineItems(lineItems.ToArray());
		}

		private Image GetImageColor(Color color)
		{
			Bitmap bitmap = new Bitmap(16, 16);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(color);
			}
			return bitmap;
		}

		private void RenderLineItems(LineItem[] lineItems)
		{
			DemoUtils.DataGridSpecifyRowCount(dataGridView1, (lineItems != null) ? lineItems.Length : 0);
			if (lineItems == null || lineItems.Length == 0)
			{
				return;
			}
			for (int i = 0; i < lineItems.Length; i++)
			{
				LineItem lineItem = lineItems[i];
				if (dataGridView1.Rows[i].Tag is LineItem lineItem2)
				{
					if (lineItem2 == lineItem)
					{
						dataGridView1.Rows[i].Cells[2].Value = lineItem.Value;
						dataGridView1.Rows[i].Cells[3].Value = lineItem.ValueLimit.MaxValue;
						dataGridView1.Rows[i].Cells[4].Value = lineItem.ValueLimit.MinValue;
						continue;
					}
					dataGridView1.Rows[i].Cells[0].Value = lineItem.GetCurveName();
					dataGridView1.Rows[i].Cells[1].Value = GetImageColor(lineItem.Color);
					dataGridView1.Rows[i].Cells[2].Value = lineItem.Value;
					dataGridView1.Rows[i].Cells[3].Value = lineItem.ValueLimit.MaxValue;
					dataGridView1.Rows[i].Cells[4].Value = lineItem.ValueLimit.MinValue;
					dataGridView1.Rows[i].Tag = lineItem;
				}
				else
				{
					dataGridView1.Rows[i].Cells[0].Value = lineItem.GetCurveName();
					dataGridView1.Rows[i].Cells[1].Value = GetImageColor(lineItem.Color);
					dataGridView1.Rows[i].Cells[2].Value = lineItem.Value;
					dataGridView1.Rows[i].Cells[3].Value = lineItem.ValueLimit.MaxValue;
					dataGridView1.Rows[i].Cells[4].Value = lineItem.ValueLimit.MinValue;
					dataGridView1.Rows[i].Tag = lineItem;
				}
			}
		}

		private void button_cancel_Click(object sender, EventArgs e)
		{
			string name = textBox_curve_address.Text;
			if (!string.IsNullOrEmpty(textBox_curve_name.Text))
			{
				name = textBox_curve_name.Text;
			}
			lock (lockObject)
			{
				lineItems.RemoveAll((LineItem m) => m.GetCurveName() == name);
			}
			hslCurveHistory1.RemoveCurve(name);
			hslCurveHistory1.RenderCurveUI();
			RenderLineItems(lineItems.ToArray());
		}

		private void button_clear_all_Click(object sender, EventArgs e)
		{
			lock (lockObject)
			{
				lineItems.Clear();
				times = new DateTime[0];
			}
			hslCurveHistory1.RemoveAllCurve();
			RenderLineItems(lineItems.ToArray());
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.DemoControl.FormCurveMonitor));
			this.textBox_curve_address = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
			this.label2 = new System.Windows.Forms.Label();
			this.button_cancel = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.button_read = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.comboBox_type = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label_color = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.comboBox_line_style = new System.Windows.Forms.ComboBox();
			this.hslCurveHistory1 = new HslControls.HslCurveHistory();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label3 = new System.Windows.Forms.Label();
			this.label_time_cost = new System.Windows.Forms.Label();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.panel3 = new System.Windows.Forms.Panel();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_color = new System.Windows.Forms.DataGridViewImageColumn();
			this.Column_value = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_max = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_min = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panel4 = new System.Windows.Forms.Panel();
			this.label7 = new System.Windows.Forms.Label();
			this.button_timer_start = new System.Windows.Forms.Button();
			this.button_timer_stop = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_curve_name = new System.Windows.Forms.TextBox();
			this.button_clear_all = new System.Windows.Forms.Button();
			this.panel1.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.panel3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.panel4.SuspendLayout();
			base.SuspendLayout();
			this.textBox_curve_address.Location = new System.Drawing.Point(55, 64);
			this.textBox_curve_address.Name = "textBox_curve_address";
			this.textBox_curve_address.Size = new System.Drawing.Size(180, 23);
			this.textBox_curve_address.TabIndex = 5;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(5, 67);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 4;
			this.label6.Text = "地址：";
			this.textBox1.Location = new System.Drawing.Point(151, 37);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(67, 23);
			this.textBox1.TabIndex = 7;
			this.textBox1.Text = "1000";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(60, 40);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(81, 17);
			this.label1.TabIndex = 6;
			this.label1.Text = "时间间隔(ms)";
			this.propertyGrid1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.propertyGrid1.BackColor = System.Drawing.Color.White;
			this.propertyGrid1.Location = new System.Drawing.Point(3, 4);
			this.propertyGrid1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.propertyGrid1.Name = "propertyGrid1";
			this.propertyGrid1.Size = new System.Drawing.Size(300, 397);
			this.propertyGrid1.TabIndex = 27;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(4, 4);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(63, 17);
			this.label2.TabIndex = 28;
			this.label2.Text = "循环耗时: ";
			this.button_cancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_cancel.Location = new System.Drawing.Point(867, 63);
			this.button_cancel.Name = "button_cancel";
			this.button_cancel.Size = new System.Drawing.Size(80, 28);
			this.button_cancel.TabIndex = 31;
			this.button_cancel.Text = "移除";
			this.button_cancel.UseVisualStyleBackColor = true;
			this.button_cancel.Click += new System.EventHandler(button_cancel_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7469679.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "";
			this.userControlHead1.SaveDeviceVisiable = false;
			this.userControlHead1.Size = new System.Drawing.Size(952, 32);
			this.userControlHead1.TabIndex = 33;
			this.button_read.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read.Location = new System.Drawing.Point(780, 63);
			this.button_read.Name = "button_read";
			this.button_read.Size = new System.Drawing.Size(81, 28);
			this.button_read.TabIndex = 34;
			this.button_read.Text = "增加";
			this.button_read.UseVisualStyleBackColor = true;
			this.button_read.Click += new System.EventHandler(button_read_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(403, 67);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 35;
			this.label4.Text = "类型：";
			this.comboBox_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_type.FormattingEnabled = true;
			this.comboBox_type.Location = new System.Drawing.Point(454, 63);
			this.comboBox_type.Name = "comboBox_type";
			this.comboBox_type.Size = new System.Drawing.Size(74, 25);
			this.comboBox_type.TabIndex = 36;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(535, 67);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(35, 17);
			this.label5.TabIndex = 37;
			this.label5.Text = "颜色:";
			this.label_color.BackColor = System.Drawing.Color.Blue;
			this.label_color.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label_color.Location = new System.Drawing.Point(583, 67);
			this.label_color.Name = "label_color";
			this.label_color.Size = new System.Drawing.Size(18, 17);
			this.label_color.TabIndex = 38;
			this.label_color.Click += new System.EventHandler(label_color_Click);
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(611, 67);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(35, 17);
			this.label8.TabIndex = 39;
			this.label8.Text = "样式:";
			this.comboBox_line_style.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_line_style.FormattingEnabled = true;
			this.comboBox_line_style.Location = new System.Drawing.Point(653, 64);
			this.comboBox_line_style.Name = "comboBox_line_style";
			this.comboBox_line_style.Size = new System.Drawing.Size(119, 25);
			this.comboBox_line_style.TabIndex = 40;
			this.hslCurveHistory1.BackColor = System.Drawing.Color.White;
			this.hslCurveHistory1.CoordinateColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.CurveRanges = null;
			this.hslCurveHistory1.DashCoordinateColor = System.Drawing.Color.LightGray;
			this.hslCurveHistory1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.hslCurveHistory1.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.HoverBackColor = System.Drawing.Color.FromArgb(224, 224, 224);
			this.hslCurveHistory1.Location = new System.Drawing.Point(0, 0);
			this.hslCurveHistory1.MarkBorderColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.MarkLineColor = System.Drawing.Color.FromArgb(192, 0, 0);
			this.hslCurveHistory1.MarkTextColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.MoveLineColor = System.Drawing.Color.Red;
			this.hslCurveHistory1.Name = "hslCurveHistory1";
			this.hslCurveHistory1.ReferenceAxisLeft.Color = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.ReferenceAxisLeft.Unit = null;
			this.hslCurveHistory1.ReferenceAxisRight.Color = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.ReferenceAxisRight.Unit = null;
			this.hslCurveHistory1.Size = new System.Drawing.Size(631, 441);
			this.hslCurveHistory1.TabIndex = 46;
			this.hslCurveHistory1.Text = "hslCurveHistory1";
			this.panel1.BackColor = System.Drawing.Color.LightGray;
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label_time_cost);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panel1.Location = new System.Drawing.Point(0, 543);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(952, 24);
			this.panel1.TabIndex = 47;
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.Color.DimGray;
			this.label3.Location = new System.Drawing.Point(183, 4);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(763, 17);
			this.label3.TabIndex = 30;
			this.label3.Text = "说明: 可以先启动定时器，也可以先添加完所有的曲线信息，再启动定时器，最大个数是曲线点位上线，超过该上限，会自动移除最早的数据。 ";
			this.label_time_cost.AutoSize = true;
			this.label_time_cost.Location = new System.Drawing.Point(89, 4);
			this.label_time_cost.Name = "label_time_cost";
			this.label_time_cost.Size = new System.Drawing.Size(38, 17);
			this.label_time_cost.TabIndex = 29;
			this.label_time_cost.Text = "-  ms";
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(317, 441);
			this.tabControl1.TabIndex = 48;
			this.tabPage1.Controls.Add(this.panel2);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(309, 411);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "曲线属性";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.panel2.Controls.Add(this.propertyGrid1);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(303, 405);
			this.panel2.TabIndex = 0;
			this.tabPage2.Controls.Add(this.panel3);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(309, 411);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "曲线信息";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.panel3.Controls.Add(this.dataGridView1);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel3.Location = new System.Drawing.Point(3, 3);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(303, 405);
			this.panel3.TabIndex = 0;
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_name, this.Column_color, this.Column_value, this.Column_max, this.Column_min);
			this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataGridView1.Location = new System.Drawing.Point(0, 0);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(303, 405);
			this.dataGridView1.TabIndex = 0;
			this.Column_name.HeaderText = "名称";
			this.Column_name.Name = "Column_name";
			this.Column_name.ReadOnly = true;
			this.Column_name.Width = 110;
			this.Column_color.HeaderText = "";
			this.Column_color.Name = "Column_color";
			this.Column_color.ReadOnly = true;
			this.Column_color.Resizable = System.Windows.Forms.DataGridViewTriState.True;
			this.Column_color.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
			this.Column_color.Width = 28;
			this.Column_value.HeaderText = "值";
			this.Column_value.Name = "Column_value";
			this.Column_value.ReadOnly = true;
			this.Column_max.HeaderText = "最大值";
			this.Column_max.Name = "Column_max";
			this.Column_max.ReadOnly = true;
			this.Column_min.HeaderText = "最小值";
			this.Column_min.Name = "Column_min";
			this.Column_min.ReadOnly = true;
			this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer1.Location = new System.Drawing.Point(0, 97);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.panel4);
			this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
			this.splitContainer1.Size = new System.Drawing.Size(952, 441);
			this.splitContainer1.SplitterDistance = 631;
			this.splitContainer1.TabIndex = 49;
			this.panel4.Controls.Add(this.hslCurveHistory1);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(0, 0);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(631, 441);
			this.panel4.TabIndex = 0;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(4, 40);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(56, 17);
			this.label7.TabIndex = 50;
			this.label7.Text = "定时器：";
			this.button_timer_start.Location = new System.Drawing.Point(375, 34);
			this.button_timer_start.Name = "button_timer_start";
			this.button_timer_start.Size = new System.Drawing.Size(91, 28);
			this.button_timer_start.TabIndex = 51;
			this.button_timer_start.Text = "启动";
			this.button_timer_start.UseVisualStyleBackColor = true;
			this.button_timer_start.Click += new System.EventHandler(button_timer_start_Click);
			this.button_timer_stop.Enabled = false;
			this.button_timer_stop.Location = new System.Drawing.Point(472, 34);
			this.button_timer_stop.Name = "button_timer_stop";
			this.button_timer_stop.Size = new System.Drawing.Size(91, 28);
			this.button_timer_stop.TabIndex = 52;
			this.button_timer_stop.Text = "停止";
			this.button_timer_stop.UseVisualStyleBackColor = true;
			this.button_timer_stop.Click += new System.EventHandler(button_timer_stop_Click);
			this.textBox2.Location = new System.Drawing.Point(296, 37);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(67, 23);
			this.textBox2.TabIndex = 54;
			this.textBox2.Text = "3000";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(224, 40);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(59, 17);
			this.label9.TabIndex = 53;
			this.label9.Text = "最大个数:";
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(239, 67);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(56, 17);
			this.label10.TabIndex = 55;
			this.label10.Text = "曲线名：";
			this.textBox_curve_name.Location = new System.Drawing.Point(296, 64);
			this.textBox_curve_name.Name = "textBox_curve_name";
			this.textBox_curve_name.Size = new System.Drawing.Size(101, 23);
			this.textBox_curve_name.TabIndex = 56;
			this.button_clear_all.Location = new System.Drawing.Point(569, 34);
			this.button_clear_all.Name = "button_clear_all";
			this.button_clear_all.Size = new System.Drawing.Size(110, 28);
			this.button_clear_all.TabIndex = 57;
			this.button_clear_all.Text = "清空所有曲线";
			this.button_clear_all.UseVisualStyleBackColor = true;
			this.button_clear_all.Click += new System.EventHandler(button_clear_all_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(952, 567);
			base.Controls.Add(this.button_clear_all);
			base.Controls.Add(this.textBox_curve_name);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label9);
			base.Controls.Add(this.button_timer_stop);
			base.Controls.Add(this.button_timer_start);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.splitContainer1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.comboBox_line_style);
			base.Controls.Add(this.label8);
			base.Controls.Add(this.label_color);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.comboBox_type);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button_read);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.button_cancel);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox_curve_address);
			base.Controls.Add(this.label6);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.MinimizeBox = false;
			base.Name = "FormCurveMonitor";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "实时曲线";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormCurveMonitor_FormClosing);
			base.Load += new System.EventHandler(FormCurveMonitor_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
