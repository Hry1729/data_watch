using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using DynamicExpresso;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunicationDemo.Control;
using HslCommunicationDemo.Properties;
using HslControls;

namespace HslCommunicationDemo.DemoControl
{
	public class DataTableControl : UserControl
	{
		private class CurveData
		{
			public float[] Data = new float[0];

			public const int DataCount = 4000;

			public static readonly Color[] CurveColors = new Color[13]
			{
				Color.FromArgb(230, 25, 75),
				Color.FromArgb(60, 180, 75),
				Color.FromArgb(67, 99, 216),
				Color.FromArgb(245, 130, 49),
				Color.FromArgb(145, 30, 180),
				Color.FromArgb(255, 225, 25),
				Color.FromArgb(66, 212, 244),
				Color.FromArgb(240, 50, 230),
				Color.FromArgb(70, 153, 144),
				Color.FromArgb(154, 99, 36),
				Color.FromArgb(128, 0, 0),
				Color.FromArgb(128, 128, 0),
				Color.FromArgb(0, 0, 117)
			};

			public static int ColorIndex = 0;

			public int CurveColor { get; set; }

			public string Key { get; }

			public CurveData(string key)
			{
				Key = key;
				if (ColorIndex < CurveColors.Length)
				{
					CurveColor = CurveColors[ColorIndex].ToArgb();
					ColorIndex++;
				}
				else
				{
					CurveColor = -1;
				}
			}

			public void Add(float value)
			{
				SoftBasic.AddArrayData(ref Data, new float[1] { value }, 4000);
			}

			public void Clear()
			{
				Data = new float[0];
			}
		}

		private class CurveDataDict
		{
			private Dictionary<string, CurveData> curveValues = new Dictionary<string, CurveData>();

			private DateTime[] dateTimes = new DateTime[0];

			public void Clear()
			{
				foreach (CurveData value in curveValues.Values)
				{
					value.Clear();
				}
				curveValues.Clear();
			}

			public void RemoveData(string key)
			{
				if (!string.IsNullOrEmpty(key) && curveValues.ContainsKey(key))
				{
					curveValues.Remove(key);
				}
			}

			public void AddData<T>(string key, T value)
			{
				if (string.IsNullOrEmpty(key))
				{
					return;
				}
				if (!curveValues.ContainsKey(key))
				{
					CurveData curveData = new CurveData(key);
					curveData.Data = new float[dateTimes.Length];
					for (int i = 0; i < curveData.Data.Length; i++)
					{
						curveData.Data[i] = float.NaN;
					}
					curveValues.Add(key, curveData);
				}
				CurveData curveData2 = curveValues[key];
				if (curveData2.Data.Length == dateTimes.Length)
				{
					SoftBasic.AddArrayData(ref curveData2.Data, new float[1] { Convert.ToSingle(value) }, 4000);
				}
			}

			public void AddDateTimes()
			{
				SoftBasic.AddArrayData(ref dateTimes, new DateTime[1] { DateTime.Now }, 4000);
				foreach (CurveData value in curveValues.Values)
				{
					while (value.Data.Length < dateTimes.Length)
					{
						SoftBasic.AddArrayData(ref value.Data, new float[1] { float.NaN }, 4000);
					}
				}
			}

			public void SetHistoryCurve(HslCurveHistory hslCurveHistory, DateTime time)
			{
				foreach (CurveData value in curveValues.Values)
				{
					if (value.CurveColor != -1)
					{
						hslCurveHistory.SetLeftCurve(value.Key, value.Data, Color.FromArgb(value.CurveColor));
					}
					else
					{
						hslCurveHistory.SetLeftCurve(value.Key, value.Data);
					}
				}
				hslCurveHistory.SetDateTimes(dateTimes);
				if (time < DateTime.Now.AddSeconds(-5.0))
				{
					hslCurveHistory.ScrollToRight();
				}
				else
				{
					hslCurveHistory.RenderCurveUI();
				}
			}
		}

		private int locationY = -1;

		private bool isMouseDown = false;

		private Point mouseDownPoint = new Point(-1, -1);

		private bool recordData = false;

		private CurveDataDict curveDataDict = new CurveDataDict();

		private IReadWriteNet device;

		private List<string> data_types = new List<string>
		{
			"bool", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double",
			"string"
		};

		private List<string> data_encodings = new List<string> { "ASCII", "UFT16", "UTF8", "GB2312" };

		private Thread threadRead;

		private bool threadEnable = false;

		private int timeSleep = 1000;

		private int readSleep = 0;

		private Interpreter interpreter = new Interpreter();

		private IContainer components = null;

		private DataGridView dataGridView1;

		private TextBox textBox_time;

		private Label label1;

		private Button button1;

		private Button button_out_clip;

		private Button button_from_clip;

		private Button button_out_file;

		private Button button_from_file;

		private Label label_info;

		private Label label2;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem rowDeleteToolStripMenuItem;

		private HslCurveHistory hslCurveHistory1;

		private DataGridViewTextBoxColumn Column_name;

		private DataGridViewTextBoxColumn Column_address;

		private DataGridViewComboBoxColumn Column_type;

		private DataGridViewComboBoxColumn Column_encoding;

		private DataGridViewTextBoxColumn Column_length;

		private DataGridViewTextBoxColumn Column_value;

		private DataGridViewTextBoxColumn Column_unit;

		private DataGridViewTextBoxColumn Column_expression;

		private DataGridViewTextBoxColumn Column_decs;

		private DataGridViewCheckBoxColumn Column_curve;

		private Button button_clear_all;

		private ToolStripMenuItem toClipToolStripMenuItem;

		private ToolStripMenuItem fromClipToolStripMenuItem;

		private ToolStripMenuItem toFileToolStripMenuItem;

		private ToolStripMenuItem fromFileToolStripMenuItem;

		private Label label3;

		private TextBox textBox_sleep_time;

		private ToolStripMenuItem toCsvClipToolStripMenuItem;

		private ToolStripMenuItem fromClipCsvToolStripMenuItem;

		private DateTime mouseMoveTime { get; set; } = DateTime.Now.AddMinutes(-1.0);

		public DataTableControl()
		{
			InitializeComponent();
			button1.Click += Button1_Click;
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			dataGridView1.CellContentClick += DataGridView1_CellContentClick;
			hslCurveHistory1.Paint += DataGridView1_Paint;
			hslCurveHistory1.MouseDown += DataGridView1_MouseDown;
			hslCurveHistory1.OnScrollChanged += HslCurveHistory1_OnScrollChanged;
			hslCurveHistory1.MouseMove += HslCurveHistory1_MouseMove;
			base.MouseMove += DataTableControl_MouseMove;
			base.MouseDown += DataTableControl_MouseDown;
			base.MouseUp += DataTableControl_MouseUp;
			Column_type.DataSource = data_types.ToArray();
			Column_encoding.DataSource = data_encodings.ToArray();
			toClipToolStripMenuItem.Click += button_out_clip_Click;
			fromClipToolStripMenuItem.Click += button_from_clip_Click;
			toFileToolStripMenuItem.Click += button_out_file_Click;
			fromFileToolStripMenuItem.Click += button_from_file_Click;
			rowDeleteToolStripMenuItem.Click += RowDeleteToolStripMenuItem_Click;
			toCsvClipToolStripMenuItem.Click += ToCsvClipToolStripMenuItem_Click;
			fromClipCsvToolStripMenuItem.Click += FromClipCsvToolStripMenuItem_Click;
		}

		private void RowDeleteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0)
			{
				StringBuilder stringBuilder = new StringBuilder();
				List<DataGridViewRow> list = new List<DataGridViewRow>();
				for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
				{
					stringBuilder.Append($"\"{dataGridView1.SelectedRows[i].Cells[1].Value}\"");
					stringBuilder.Append(",");
					list.Add(dataGridView1.SelectedRows[i]);
				}
				string text = "是否删除设备地址为: " + stringBuilder.ToString() + " 所有的行，共计 " + list.Count + " 行";
				if (Program.Language == 2)
				{
					text = "Delete Address: " + stringBuilder.ToString() + " all lines，total: " + list.Count + " line";
				}
				if (MessageBox.Show(text, "Delete Check", MessageBoxButtons.YesNo) != DialogResult.Yes)
				{
					return;
				}
				for (int j = 0; j < list.Count; j++)
				{
					if (!list[j].IsNewRow)
					{
						dataGridView1.Rows.Remove(list[j]);
					}
				}
			}
			else
			{
				if (dataGridView1.SelectedCells.Count <= 0)
				{
					return;
				}
				DataGridViewCell dataGridViewCell = dataGridView1.SelectedCells[0];
				if (!dataGridView1.Rows[dataGridViewCell.RowIndex].IsNewRow)
				{
					string text2 = $"是否删除设备地址为: {dataGridView1.Rows[dataGridViewCell.RowIndex].Cells[1].Value} 的行";
					if (Program.Language == 2)
					{
						text2 = $"Delete Address: {dataGridView1.Rows[dataGridViewCell.RowIndex].Cells[1].Value} line?";
					}
					if (MessageBox.Show(text2, "Delete Check", MessageBoxButtons.YesNo) == DialogResult.Yes)
					{
						dataGridView1.Rows.Remove(dataGridView1.Rows[dataGridViewCell.RowIndex]);
					}
				}
			}
		}

		private void HslCurveHistory1_MouseMove(object sender, MouseEventArgs e)
		{
			if (mouseMoveTime >= DateTime.Now.AddSeconds(5.0))
			{
				mouseMoveTime = DateTime.Now;
			}
		}

		private void HslCurveHistory1_OnScrollChanged(HslCurveHistory hslCurve, int scrollX, float scale, int offsetPaintScrollX)
		{
			mouseMoveTime = DateTime.Now;
		}

		private void DataGridView1_MouseDown(object sender, MouseEventArgs e)
		{
			if (new Rectangle(hslCurveHistory1.Width - 52, hslCurveHistory1.Height - 23, 50, 20).Contains(e.Location))
			{
				using (FormPropertyModify formPropertyModify = new FormPropertyModify())
				{
					formPropertyModify.SetObject(hslCurveHistory1);
					formPropertyModify.ShowDialog();
					return;
				}
			}
			if (!new Rectangle(1, hslCurveHistory1.Height - 23, 48, 20).Contains(e.Location))
			{
				return;
			}
			List<CurveLineStyle> list = new List<CurveLineStyle>();
			foreach (KeyValuePair<string, HslCurveItem> item in hslCurveHistory1.GetAllCurve())
			{
				CurveLineStyle curveLineStyle = new CurveLineStyle();
				curveLineStyle.Key = item.Key;
				curveLineStyle.Color = item.Value.LineColor;
				curveLineStyle.Width = item.Value.LineThickness;
				curveLineStyle.Style = item.Value.Style;
				curveLineStyle.Format = item.Value.RenderFormat;
				list.Add(curveLineStyle);
			}
			using (FormPropertyModify formPropertyModify2 = new FormPropertyModify())
			{
				formPropertyModify2.SetObject(new CurveLineStyleArray
				{
					Items = list
				});
				if (formPropertyModify2.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				int num = 0;
				foreach (KeyValuePair<string, HslCurveItem> item2 in hslCurveHistory1.GetAllCurve())
				{
					if (num >= list.Count)
					{
						break;
					}
					item2.Value.LineColor = list[num].Color;
					item2.Value.LineThickness = list[num].Width;
					item2.Value.Style = list[num].Style;
					item2.Value.RenderFormat = list[num].Format;
					num++;
				}
				hslCurveHistory1.RenderCurveUI();
			}
		}

		private void DataGridView1_Paint(object sender, PaintEventArgs e)
		{
			Rectangle rectangle = new Rectangle(hslCurveHistory1.Width - 50, hslCurveHistory1.Height - 23, 48, 20);
			e.Graphics.DrawRectangle(Pens.Gray, rectangle);
			e.Graphics.DrawString((Program.Language == 1) ? "属性" : "Setting", Font, Brushes.Gray, rectangle, HslControls.HslHelper.StringFormatCenter);
			Rectangle rectangle2 = new Rectangle(1, hslCurveHistory1.Height - 23, 45, 20);
			e.Graphics.DrawRectangle(Pens.Gray, rectangle2);
			e.Graphics.DrawString((Program.Language == 1) ? "样式" : "Style", Font, Brushes.Gray, rectangle2, HslControls.HslHelper.StringFormatCenter);
		}

		private void SetControlLocation(int offset)
		{
			dataGridView1.Width = base.Width - 1;
			dataGridView1.Height += offset;
			hslCurveHistory1.Location = new Point(0, hslCurveHistory1.Location.Y + offset);
			hslCurveHistory1.Height -= offset;
			hslCurveHistory1.Width = base.Width - 1;
			hslCurveHistory1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			locationY = dataGridView1.Height + 33;
		}

		private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.ColumnIndex != 9 || !(dataGridView1.Rows[e.RowIndex].Cells[9] is DataGridViewCheckBoxCell dataGridViewCheckBoxCell))
			{
				return;
			}
			bool flag = dataGridViewCheckBoxCell.Tag == null || !(bool)dataGridViewCheckBoxCell.Tag;
			dataGridViewCheckBoxCell.Tag = flag;
			dataGridViewCheckBoxCell.Value = flag;
			if (flag)
			{
				if (!hslCurveHistory1.Visible)
				{
					dataGridView1.Width = base.Width - 1;
					dataGridView1.Height = (base.Height - 33) / 2 - 1;
					hslCurveHistory1.Height = (base.Height - 33) / 2 - 1;
					hslCurveHistory1.Width = base.Width - 1;
					hslCurveHistory1.Location = new Point(0, (base.Height - 33) / 2 + 33);
					hslCurveHistory1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
					hslCurveHistory1.Visible = true;
					locationY = (base.Height - 33) / 2 + 32;
				}
				if (!recordData)
				{
					recordData = true;
					CurveData.ColorIndex = 0;
					curveDataDict.Clear();
				}
				return;
			}
			bool flag2 = false;
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				if (dataGridView1.Rows[i].IsNewRow || !(dataGridView1.Rows[i].Cells[9] is DataGridViewCheckBoxCell dataGridViewCheckBoxCell2) || dataGridViewCheckBoxCell2 == null)
				{
					continue;
				}
				if (dataGridViewCheckBoxCell2.Value != null)
				{
					if ((bool)dataGridViewCheckBoxCell2.Value)
					{
						flag2 = true;
						break;
					}
				}
				else if (dataGridViewCheckBoxCell2.Tag is bool flag3 && true && flag3)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				recordData = false;
				dataGridView1.Width = base.Width - 1;
				dataGridView1.Height = base.Height - 33;
				hslCurveHistory1.Visible = false;
				hslCurveHistory1.RemoveAllCurve();
			}
		}

		private void DataTableControl_MouseMove(object sender, MouseEventArgs e)
		{
			if (!hslCurveHistory1.Visible)
			{
				Cursor = Cursors.Default;
				return;
			}
			if (isMouseDown)
			{
				if (e.Location.Y < 60)
				{
					isMouseDown = false;
					return;
				}
				if (e.Location.Y > base.Height - 50)
				{
					isMouseDown = false;
					return;
				}
				SetControlLocation(e.Location.Y - mouseDownPoint.Y);
				mouseDownPoint = e.Location;
				return;
			}
			locationY = dataGridView1.Height + 33;
			if (e.Location.Y >= 58 && e.Location.Y <= base.Height - 48)
			{
				if (e.Location.Y >= locationY - 1 && e.Location.Y <= locationY + 1)
				{
					Cursor = Cursors.HSplit;
				}
				else
				{
					Cursor = Cursors.Default;
				}
			}
		}

		private void DataTableControl_MouseDown(object sender, MouseEventArgs e)
		{
			if (Cursor == Cursors.HSplit)
			{
				isMouseDown = true;
				mouseDownPoint = e.Location;
			}
		}

		private void DataTableControl_MouseUp(object sender, MouseEventArgs e)
		{
			isMouseDown = false;
		}

		public void SetReadWriteNet(IReadWriteNet device)
		{
			this.device = device;
		}

		private void DataTableControl_Load(object sender, EventArgs e)
		{
			DataGridView1_SizeChanged(null, e);
			button1.Text = ((Program.Language == 1) ? "开始刷新" : "Refresh");
			if (Program.Language == 2)
			{
				Column_name.HeaderText = "Name";
				Column_address.HeaderText = "Address";
				Column_type.HeaderText = "Type";
				Column_encoding.HeaderText = "Coding";
				Column_length.HeaderText = "Len";
				Column_value.HeaderText = "Value";
				Column_unit.HeaderText = "Unit";
				Column_decs.HeaderText = "Description";
				Column_expression.HeaderText = "Express";
				Column_curve.HeaderText = "Curv";
				toClipToolStripMenuItem.Text = "ToXmlClip";
				fromClipToolStripMenuItem.Text = "FromXmlClip";
				toFileToolStripMenuItem.Text = "ToXmlFile";
				fromFileToolStripMenuItem.Text = "FromXmlFile";
				toCsvClipToolStripMenuItem.Text = "ToCsvClip";
				fromClipCsvToolStripMenuItem.Text = "FromCsvClip";
				label1.Text = "Cycle Interval:";
				label2.Text = "Double click to write";
				label3.Text = "EverySleep:";
				button_clear_all.Text = "EmptyAll";
			}
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[0].Width = 120 + ((dataGridView1.Width >= 1200) ? (dataGridView1.Width / 15) : 0);
			dataGridView1.Columns[1].Width = 120 + ((dataGridView1.Width >= 1200) ? (dataGridView1.Width / 15) : 0);
			dataGridView1.Columns[2].Width = 80;
			dataGridView1.Columns[3].Width = 80;
			dataGridView1.Columns[4].Width = 60;
			dataGridView1.Columns[5].Width = 150 + ((dataGridView1.Width >= 1200) ? (dataGridView1.Width / 10) : 0);
			dataGridView1.Columns[6].Width = 60;
			dataGridView1.Columns[7].Width = 100;
			dataGridView1.Columns[9].Width = 40;
			int num = 0;
			for (int i = 0; i < 8; i++)
			{
				num += dataGridView1.Columns[i].Width;
			}
			dataGridView1.Columns[8].Width = dataGridView1.Width - (num + 40) - 20 - 40;
		}

		private DataTableItem GetDataTableItem(DataGridViewRow dgvr)
		{
			DataTableItem dataTableItem = new DataTableItem();
			dataTableItem.Name = ((dgvr.Cells[0].Value != null) ? dgvr.Cells[0].Value.ToString() : string.Empty);
			dataTableItem.Address = ((dgvr.Cells[1].Value != null) ? dgvr.Cells[1].Value.ToString() : string.Empty);
			dataTableItem.DataTypeCode = ((dgvr.Cells[2].Value != null) ? dgvr.Cells[2].Value.ToString() : string.Empty);
			if (dataTableItem.DataTypeCode == "string")
			{
				if (dgvr.Cells[3].Value != null)
				{
					dataTableItem.StringEncoding = SoftBasic.GetEnumFromString<StringEncoding>(dgvr.Cells[3].Value.ToString());
				}
				else
				{
					dataTableItem.StringEncoding = StringEncoding.ASCII;
				}
			}
			string value = ((dgvr.Cells[4].Value != null) ? dgvr.Cells[4].Value.ToString() : string.Empty);
			dataTableItem.Length = (string.IsNullOrEmpty(value) ? (-1) : Convert.ToInt32(value));
			dataTableItem.Unit = ((dgvr.Cells[6].Value != null) ? dgvr.Cells[6].Value.ToString() : string.Empty);
			dataTableItem.Expression = ((dgvr.Cells[7].Value != null) ? dgvr.Cells[7].Value.ToString() : string.Empty);
			dataTableItem.Description = ((dgvr.Cells[8].Value != null) ? dgvr.Cells[8].Value.ToString() : string.Empty);
			return dataTableItem;
		}

		public void GetDataTable(XElement element)
		{
			element.SetAttributeValue("Interval", textBox_time.Text);
			if (!string.IsNullOrWhiteSpace(textBox_sleep_time.Text))
			{
				element.SetAttributeValue("SleepTime", textBox_sleep_time.Text);
			}
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[2].Value != null && !dataGridViewRow.IsNewRow)
				{
					element.Add(GetDataTableItem(dataGridViewRow).ToXmlElement());
				}
			}
		}

		private void AddRow(DataTableItem dataTableItem)
		{
			int num = dataGridView1.Rows.Add();
			DataGridViewRow dataGridViewRow = dataGridView1.Rows[num];
			dataGridViewRow.Cells[0].Value = dataTableItem.Name;
			dataGridViewRow.Cells[1].Value = dataTableItem.Address;
			if (!string.IsNullOrEmpty(dataTableItem.DataTypeCode))
			{
				if (data_types.Contains(dataTableItem.DataTypeCode))
				{
					dataGridViewRow.Cells[2].Value = dataTableItem.DataTypeCode;
				}
				else
				{
					DemoUtils.ShowMessage($"Row[{num}] Name[{dataTableItem.Name}] Address[{dataTableItem.Address}] type: {dataTableItem.DataTypeCode} is not supported");
				}
				if (dataTableItem.DataTypeCode == "string")
				{
					dataGridViewRow.Cells[3].Value = dataTableItem.StringEncoding.ToString();
				}
			}
			if (dataTableItem.Length >= 0)
			{
				dataGridViewRow.Cells[4].Value = dataTableItem.Length.ToString();
			}
			dataGridViewRow.Cells[6].Value = dataTableItem.Unit;
			dataGridViewRow.Cells[7].Value = dataTableItem.Expression;
			dataGridViewRow.Cells[8].Value = dataTableItem.Description;
			dataGridViewRow.Tag = dataTableItem;
		}

		public int LoadDataTable(XElement element)
		{
			XAttribute xAttribute = element.Attribute("Interval");
			if (xAttribute != null)
			{
				textBox_time.Text = xAttribute.Value;
			}
			XAttribute xAttribute2 = element.Attribute("SleepTime");
			if (xAttribute2 != null)
			{
				textBox_sleep_time.Text = xAttribute2.Value;
			}
			int num = 0;
			foreach (XElement item in element.Elements("DataTableItem"))
			{
				DataTableItem dataTableItem = new DataTableItem();
				dataTableItem.LoadByXmlElement(item);
				AddRow(dataTableItem);
				num++;
			}
			return num;
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			if (device == null)
			{
				DemoUtils.ShowMessage("ReadWriteNet device is null, can not refresh! ");
			}
			else if (!threadEnable)
			{
				threadEnable = true;
				button1.Text = ((Program.Language == 1) ? "停止刷新" : "Stop");
				button1.BackColor = Color.LimeGreen;
				timeSleep = Convert.ToInt32(textBox_time.Text);
				if (!string.IsNullOrWhiteSpace(textBox_sleep_time.Text))
				{
					readSleep = Convert.ToInt32(textBox_sleep_time.Text);
				}
				else
				{
					readSleep = 0;
				}
				threadRead = new Thread(ThreadBack);
				threadRead.IsBackground = true;
				threadRead.Start();
			}
			else
			{
				threadEnable = false;
				button1.Text = ((Program.Language == 1) ? "开始刷新" : "Refresh");
				button1.BackColor = SystemColors.Control;
			}
		}

		public void Close()
		{
			if (threadEnable)
			{
				Button1_Click(button1, EventArgs.Empty);
			}
		}

		private void ReadByType<T>(DataGridViewRow dgvr, DataTableItem dataTableItem, Func<string, OperateResult<T>> read1, Func<string, ushort, OperateResult<T[]>> read2)
		{
			if (dataTableItem.Length <= 0)
			{
				OperateResult<T> read3 = read1(dataTableItem.Address);
				Invoke((Action)delegate
				{
					if (read3.IsSuccess)
					{
						if (string.IsNullOrEmpty(dataTableItem.Expression))
						{
							dgvr.Cells[5].Value = read3.Content.ToString();
						}
						else
						{
							dgvr.Cells[5].Value = interpreter.Eval(dataTableItem.Expression, new Parameter("x", read3.Content));
						}
						string text = dataTableItem.Name;
						if (string.IsNullOrEmpty(text))
						{
							text = dataTableItem.Address;
						}
						if (recordData && !string.IsNullOrEmpty(text) && (dgvr.Cells[9] as DataGridViewCheckBoxCell).Tag is bool flag)
						{
							if (flag)
							{
								if (string.IsNullOrEmpty(dataTableItem.Expression))
								{
									curveDataDict.AddData(text, read3.Content);
								}
								else
								{
									curveDataDict.AddData(text, interpreter.Eval(dataTableItem.Expression, new Parameter("x", read3.Content)));
								}
							}
							else
							{
								curveDataDict.RemoveData(text);
								hslCurveHistory1.RemoveCurve(text);
							}
						}
					}
					else
					{
						dgvr.Cells[5].Value = string.Empty;
					}
				});
				return;
			}
			OperateResult<T[]> read4 = read2(dataTableItem.Address, (ushort)dataTableItem.Length);
			Invoke((Action)delegate
			{
				if (read4.IsSuccess)
				{
					if (string.IsNullOrEmpty(dataTableItem.Expression))
					{
						dgvr.Cells[5].Value = read4.Content.ToArrayString();
					}
					else
					{
						object[] array = new object[read4.Content.Length];
						for (int i = 0; i < read4.Content.Length; i++)
						{
							array[i] = interpreter.Eval(dataTableItem.Expression, new Parameter("x", read4.Content[i]));
						}
						dgvr.Cells[5].Value = array.ToArrayString();
					}
				}
				else
				{
					dgvr.Cells[5].Value = string.Empty;
				}
			});
		}

		private void ThreadBack()
		{
			while (threadEnable && dataGridView1.FirstDisplayedCell != null)
			{
				int rowIndex = dataGridView1.FirstDisplayedCell.RowIndex;
				int num = dataGridView1.DisplayedRowCount(true);
				for (int i = 0; i < num; i++)
				{
					DataGridViewRow dgvr = dataGridView1.Rows[i + rowIndex];
					if (dgvr.IsNewRow)
					{
						continue;
					}
					DataTableItem dataTableItem = GetDataTableItem(dgvr);
					if (dataTableItem.DataTypeCode == "bool")
					{
						ReadByType(dgvr, dataTableItem, device.ReadBool, device.ReadBool);
					}
					else if (dataTableItem.DataTypeCode == "short")
					{
						ReadByType(dgvr, dataTableItem, device.ReadInt16, device.ReadInt16);
					}
					else if (dataTableItem.DataTypeCode == "ushort")
					{
						ReadByType(dgvr, dataTableItem, device.ReadUInt16, device.ReadUInt16);
					}
					else if (dataTableItem.DataTypeCode == "int")
					{
						ReadByType(dgvr, dataTableItem, device.ReadInt32, device.ReadInt32);
					}
					else if (dataTableItem.DataTypeCode == "uint")
					{
						ReadByType(dgvr, dataTableItem, device.ReadUInt32, device.ReadUInt32);
					}
					else if (dataTableItem.DataTypeCode == "long")
					{
						ReadByType(dgvr, dataTableItem, device.ReadInt64, device.ReadInt64);
					}
					else if (dataTableItem.DataTypeCode == "ulong")
					{
						ReadByType(dgvr, dataTableItem, device.ReadUInt64, device.ReadUInt64);
					}
					else if (dataTableItem.DataTypeCode == "float")
					{
						ReadByType(dgvr, dataTableItem, device.ReadFloat, device.ReadFloat);
					}
					else if (dataTableItem.DataTypeCode == "double")
					{
						ReadByType(dgvr, dataTableItem, device.ReadDouble, device.ReadDouble);
					}
					else if (dataTableItem.DataTypeCode == "string")
					{
						int num2 = ((dataTableItem.Length >= 0) ? dataTableItem.Length : 0);
						Encoding encoding = ((dataTableItem.StringEncoding == StringEncoding.ASCII) ? Encoding.ASCII : ((dataTableItem.StringEncoding == StringEncoding.ANSI) ? Encoding.Default : ((dataTableItem.StringEncoding == StringEncoding.UTF16) ? Encoding.Unicode : ((dataTableItem.StringEncoding == StringEncoding.UTF16Big) ? Encoding.BigEndianUnicode : ((dataTableItem.StringEncoding == StringEncoding.UTF8) ? Encoding.UTF8 : ((dataTableItem.StringEncoding == StringEncoding.GB2312) ? Encoding.GetEncoding("gb2312") : Encoding.ASCII))))));
						OperateResult<string> read = device.ReadString(dataTableItem.Address, (ushort)num2, encoding);
						Invoke((Action)delegate
						{
							if (read.IsSuccess)
							{
								dgvr.Cells[5].Value = read.Content;
							}
							else
							{
								dgvr.Cells[5].Value = string.Empty;
							}
						});
					}
					else if (dataTableItem.DataTypeCode == "byte")
					{
						if (dataTableItem.Length <= 0)
						{
							OperateResult<byte[]> read2 = device.Read(dataTableItem.Address, 1);
							Invoke((Action)delegate
							{
								if (read2.IsSuccess)
								{
									dgvr.Cells[5].Value = read2.Content[0].ToString();
								}
								else
								{
									dgvr.Cells[5].Value = string.Empty;
								}
							});
						}
						else
						{
							OperateResult<byte[]> read3 = device.Read(dataTableItem.Address, (ushort)dataTableItem.Length);
							Invoke((Action)delegate
							{
								if (read3.IsSuccess)
								{
									dgvr.Cells[5].Value = read3.Content.ToHexString(' ');
								}
								else
								{
									dgvr.Cells[5].Value = string.Empty;
								}
							});
						}
					}
					if (readSleep > 0)
					{
						HslCommunication.Core.HslHelper.ThreadSleep(readSleep);
					}
				}
				Invoke((Action)delegate
				{
					if (recordData)
					{
						curveDataDict.AddDateTimes();
						curveDataDict.SetHistoryCurve(hslCurveHistory1, mouseMoveTime);
					}
				});
				Thread.Sleep(timeSleep);
			}
		}

		private void button_out_clip_Click(object sender, EventArgs e)
		{
			XElement xElement = new XElement("DataTable");
			GetDataTable(xElement);
			Clipboard.SetText(xElement.ToString());
			DemoUtils.ShowMessage("Save success!");
		}

		private void button_from_clip_Click(object sender, EventArgs e)
		{
			try
			{
				LoadDataTable(XElement.Parse(Clipboard.GetText()));
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Load failed: " + ex.Message);
			}
		}

		private void button_out_file_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "*XML|*.xml";
				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					XElement xElement = new XElement("DataTable");
					GetDataTable(xElement);
					xElement.Save(saveFileDialog.FileName);
					DemoUtils.ShowMessage("Save success!");
				}
			}
		}

		private void ToCsvClipToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringBuilder stringBuilder = new StringBuilder(Column_name.HeaderText + "\t" + Column_address.HeaderText + "\t" + Column_type.HeaderText + "\t" + Column_encoding.HeaderText + "\t" + Column_length.HeaderText + "\t" + Column_unit.HeaderText + "\t" + Column_expression.HeaderText + "\t" + Column_decs.HeaderText + "\r\n");
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[2].Value != null && !dataGridViewRow.IsNewRow)
				{
					DataTableItem dataTableItem = GetDataTableItem(dataGridViewRow);
					stringBuilder.Append($"{dataTableItem.Name}\t{dataTableItem.Address}\t{dataTableItem.DataTypeCode}\t{dataTableItem.StringEncoding}\t" + $"{dataTableItem.Length}\t{dataTableItem.Unit}\t{dataTableItem.Expression}\t{dataTableItem.Description}\r\n");
				}
			}
			Clipboard.SetText(stringBuilder.ToString());
			DemoUtils.ShowMessage("Save success!");
		}

		private void FromClipCsvToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				string[] array = Clipboard.GetText().Split(new string[3] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length < 2)
				{
					return;
				}
				int num = 0;
				for (int i = 1; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[1] { '\t' }, StringSplitOptions.None);
					if (array2.Length >= 4)
					{
						DataTableItem dataTableItem = new DataTableItem();
						dataTableItem.Name = array2[0];
						dataTableItem.Address = array2[1];
						dataTableItem.DataTypeCode = array2[2];
						dataTableItem.StringEncoding = ((!string.IsNullOrEmpty(array2[3])) ? SoftBasic.GetEnumFromString<StringEncoding>(array2[3]) : StringEncoding.ASCII);
						if (array2.Length > 4)
						{
							dataTableItem.Length = (string.IsNullOrEmpty(array2[4]) ? (-1) : Convert.ToInt32(array2[4]));
						}
						if (array2.Length > 5)
						{
							dataTableItem.Unit = array2[5];
						}
						if (array2.Length > 6)
						{
							dataTableItem.Expression = array2[6];
						}
						if (array2.Length > 7)
						{
							dataTableItem.Description = array2[7];
						}
						AddRow(dataTableItem);
						num++;
					}
				}
				DemoUtils.ShowMessage($"Import {num} rows success!");
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Load failed: " + ex.Message);
			}
		}

		private void button_from_file_Click(object sender, EventArgs e)
		{
			try
			{
				using (OpenFileDialog openFileDialog = new OpenFileDialog())
				{
					openFileDialog.Filter = "*XML|*.xml";
					if (openFileDialog.ShowDialog() == DialogResult.OK)
					{
						try
						{
							LoadDataTable(XElement.Parse(File.ReadAllText(openFileDialog.FileName, Encoding.UTF8)));
							return;
						}
						catch (Exception ex)
						{
							DemoUtils.ShowMessage("Load failed: " + ex.Message);
							return;
						}
					}
				}
			}
			catch (Exception ex2)
			{
				DemoUtils.ShowMessage("Load failed: " + ex2.Message);
			}
		}

		private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.ColumnIndex != 5 || e.RowIndex < 0)
			{
				return;
			}
			DataTableItem dataTableItem = GetDataTableItem(dataGridView1.Rows[e.RowIndex]);
			if (dataTableItem == null || string.IsNullOrEmpty(dataTableItem.Address))
			{
				return;
			}
			using (FormInputNewValue formInputNewValue = new FormInputNewValue(dataGridView1.Rows[e.RowIndex].Cells[5].Value?.ToString()))
			{
				if (formInputNewValue.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				OperateResult operateResult = OperateResult.CreateSuccessResult();
				try
				{
					string text = formInputNewValue.InputValue;
					if (dataTableItem.DataTypeCode == "bool")
					{
						if (dataTableItem.Length < 0)
						{
							if (text == "1")
							{
								text = "True";
							}
							if (text == "0")
							{
								text = "False";
							}
							operateResult = device.Write(dataTableItem.Address, bool.Parse(text));
						}
						else
						{
							operateResult = device.Write(dataTableItem.Address, text.ToStringArray<bool>());
						}
					}
					else if (dataTableItem.DataTypeCode == "short")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<short>()) : device.Write(dataTableItem.Address, short.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "ushort")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<ushort>()) : device.Write(dataTableItem.Address, ushort.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "int")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<int>()) : device.Write(dataTableItem.Address, int.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "uint")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<uint>()) : device.Write(dataTableItem.Address, int.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "long")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<long>()) : device.Write(dataTableItem.Address, long.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "float")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<float>()) : device.Write(dataTableItem.Address, float.Parse(text)));
					}
					else if (dataTableItem.DataTypeCode == "double")
					{
						operateResult = ((dataTableItem.Length >= 0) ? device.Write(dataTableItem.Address, text.ToStringArray<double>()) : device.Write(dataTableItem.Address, double.Parse(text)));
					}
					else
					{
						if (!(dataTableItem.DataTypeCode == "string"))
						{
							DemoUtils.ShowMessage("Not supported data type: " + dataTableItem.DataTypeCode);
							return;
						}
						int num = ((dataTableItem.Length >= 0) ? dataTableItem.Length : 0);
						Encoding encoding = ((dataTableItem.StringEncoding == StringEncoding.ASCII) ? Encoding.ASCII : ((dataTableItem.StringEncoding == StringEncoding.ANSI) ? Encoding.Default : ((dataTableItem.StringEncoding == StringEncoding.UTF16) ? Encoding.Unicode : ((dataTableItem.StringEncoding == StringEncoding.UTF16Big) ? Encoding.BigEndianUnicode : ((dataTableItem.StringEncoding == StringEncoding.UTF8) ? Encoding.UTF8 : ((dataTableItem.StringEncoding == StringEncoding.GB2312) ? Encoding.GetEncoding("gb2312") : Encoding.ASCII))))));
						OperateResult<string> operateResult2 = device.ReadString(dataTableItem.Address, (ushort)num, encoding);
						operateResult = device.Write(dataTableItem.Address, text, encoding);
					}
					if (operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("Write Success!");
					}
					else
					{
						DemoUtils.ShowMessage("Write Failed: " + operateResult.Message);
					}
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("trans string to type " + dataTableItem.DataTypeCode + "failed: " + ex.Message);
				}
			}
		}

		private void button_clear_all_Click(object sender, EventArgs e)
		{
			DemoUtils.ClearDataGridView(dataGridView1);
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
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_address = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_type = new System.Windows.Forms.DataGridViewComboBoxColumn();
			this.Column_encoding = new System.Windows.Forms.DataGridViewComboBoxColumn();
			this.Column_length = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_value = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_unit = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_expression = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_decs = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_curve = new System.Windows.Forms.DataGridViewCheckBoxColumn();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.rowDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.textBox_time = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button_out_clip = new System.Windows.Forms.Button();
			this.button_from_clip = new System.Windows.Forms.Button();
			this.button_out_file = new System.Windows.Forms.Button();
			this.button_from_file = new System.Windows.Forms.Button();
			this.label_info = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.hslCurveHistory1 = new HslControls.HslCurveHistory();
			this.button_clear_all = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_sleep_time = new System.Windows.Forms.TextBox();
			this.toCsvClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromClipCsvToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_name, this.Column_address, this.Column_type, this.Column_encoding, this.Column_length, this.Column_value, this.Column_unit, this.Column_expression, this.Column_decs, this.Column_curve);
			this.dataGridView1.ContextMenuStrip = this.contextMenuStrip1;
			this.dataGridView1.Location = new System.Drawing.Point(0, 30);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(1095, 387);
			this.dataGridView1.TabIndex = 0;
			this.dataGridView1.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView1_CellMouseDoubleClick);
			this.Column_name.HeaderText = "数据名";
			this.Column_name.Name = "Column_name";
			this.Column_address.HeaderText = "设备地址";
			this.Column_address.Name = "Column_address";
			this.Column_type.HeaderText = "数据类型";
			this.Column_type.Items.AddRange("bool", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "string");
			this.Column_type.MaxDropDownItems = 12;
			this.Column_type.Name = "Column_type";
			this.Column_encoding.HeaderText = "字符编码";
			this.Column_encoding.Items.AddRange("ASCII", "UFT16", "UTF8", "GB2312");
			this.Column_encoding.Name = "Column_encoding";
			this.Column_length.HeaderText = "长度";
			this.Column_length.Name = "Column_length";
			this.Column_length.ToolTipText = "数组长度，大于0表示这是一个数组，并且指示长度信息";
			this.Column_value.HeaderText = "值";
			this.Column_value.Name = "Column_value";
			this.Column_unit.HeaderText = "单位";
			this.Column_unit.Name = "Column_unit";
			this.Column_expression.HeaderText = "表达式";
			this.Column_expression.Name = "Column_expression";
			this.Column_decs.HeaderText = "注释";
			this.Column_decs.Name = "Column_decs";
			this.Column_curve.HeaderText = "曲线";
			this.Column_curve.Name = "Column_curve";
			this.Column_curve.Width = 40;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[7] { this.rowDeleteToolStripMenuItem, this.toClipToolStripMenuItem, this.fromClipToolStripMenuItem, this.toFileToolStripMenuItem, this.fromFileToolStripMenuItem, this.toCsvClipToolStripMenuItem, this.fromClipCsvToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(181, 180);
			this.rowDeleteToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.rowDeleteToolStripMenuItem.Name = "rowDeleteToolStripMenuItem";
			this.rowDeleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.rowDeleteToolStripMenuItem.Text = "RowDelete";
			this.toClipToolStripMenuItem.Name = "toClipToolStripMenuItem";
			this.toClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toClipToolStripMenuItem.Text = "导出到剪切板";
			this.fromClipToolStripMenuItem.Name = "fromClipToolStripMenuItem";
			this.fromClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromClipToolStripMenuItem.Text = "从剪切板导入";
			this.toFileToolStripMenuItem.Name = "toFileToolStripMenuItem";
			this.toFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toFileToolStripMenuItem.Text = "导出到文件";
			this.fromFileToolStripMenuItem.Name = "fromFileToolStripMenuItem";
			this.fromFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromFileToolStripMenuItem.Text = "从文件导入";
			this.textBox_time.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_time.Location = new System.Drawing.Point(816, 2);
			this.textBox_time.Name = "textBox_time";
			this.textBox_time.Size = new System.Drawing.Size(51, 23);
			this.textBox_time.TabIndex = 1;
			this.textBox_time.Text = "1000";
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(724, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(83, 17);
			this.label1.TabIndex = 2;
			this.label1.Text = "线程循环时间:";
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(994, 0);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(101, 27);
			this.button1.TabIndex = 3;
			this.button1.Text = "开始刷新";
			this.button1.UseVisualStyleBackColor = true;
			this.button_out_clip.Location = new System.Drawing.Point(0, 0);
			this.button_out_clip.Name = "button_out_clip";
			this.button_out_clip.Size = new System.Drawing.Size(66, 27);
			this.button_out_clip.TabIndex = 4;
			this.button_out_clip.Text = "ToClip";
			this.button_out_clip.UseVisualStyleBackColor = true;
			this.button_out_clip.Click += new System.EventHandler(button_out_clip_Click);
			this.button_from_clip.Location = new System.Drawing.Point(69, 0);
			this.button_from_clip.Name = "button_from_clip";
			this.button_from_clip.Size = new System.Drawing.Size(76, 27);
			this.button_from_clip.TabIndex = 5;
			this.button_from_clip.Text = "FromClip";
			this.button_from_clip.UseVisualStyleBackColor = true;
			this.button_from_clip.Click += new System.EventHandler(button_from_clip_Click);
			this.button_out_file.Location = new System.Drawing.Point(149, 0);
			this.button_out_file.Name = "button_out_file";
			this.button_out_file.Size = new System.Drawing.Size(54, 27);
			this.button_out_file.TabIndex = 6;
			this.button_out_file.Text = "ToFile";
			this.button_out_file.UseVisualStyleBackColor = true;
			this.button_out_file.Click += new System.EventHandler(button_out_file_Click);
			this.button_from_file.Location = new System.Drawing.Point(205, 0);
			this.button_from_file.Name = "button_from_file";
			this.button_from_file.Size = new System.Drawing.Size(71, 27);
			this.button_from_file.TabIndex = 7;
			this.button_from_file.Text = "FromFile";
			this.button_from_file.UseVisualStyleBackColor = true;
			this.button_from_file.Click += new System.EventHandler(button_from_file_Click);
			this.label_info.AutoSize = true;
			this.label_info.ForeColor = System.Drawing.Color.Gray;
			this.label_info.Location = new System.Drawing.Point(447, 6);
			this.label_info.Name = "label_info";
			this.label_info.Size = new System.Drawing.Size(0, 17);
			this.label_info.TabIndex = 8;
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Gray;
			this.label2.Location = new System.Drawing.Point(279, 5);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(218, 17);
			this.label2.TabIndex = 9;
			this.label2.Text = "双击\"值\"单元格可写入,表达式例子: x+1";
			this.hslCurveHistory1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslCurveHistory1.BackColor = System.Drawing.Color.White;
			this.hslCurveHistory1.CoordinateColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.CurveRanges = null;
			this.hslCurveHistory1.DashCoordinateColor = System.Drawing.Color.Silver;
			this.hslCurveHistory1.ForeColor = System.Drawing.Color.Blue;
			this.hslCurveHistory1.HoverBackColor = System.Drawing.Color.WhiteSmoke;
			this.hslCurveHistory1.Location = new System.Drawing.Point(3, 228);
			this.hslCurveHistory1.MarkLineColor = System.Drawing.Color.FromArgb(255, 128, 128);
			this.hslCurveHistory1.MarkTextColor = System.Drawing.Color.DarkGoldenrod;
			this.hslCurveHistory1.MoveLineColor = System.Drawing.Color.Red;
			this.hslCurveHistory1.Name = "hslCurveHistory1";
			this.hslCurveHistory1.ReferenceAxisLeft.Color = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.ReferenceAxisLeft.Unit = null;
			this.hslCurveHistory1.ReferenceAxisRight.Color = System.Drawing.Color.FromArgb(64, 64, 64);
			this.hslCurveHistory1.ReferenceAxisRight.Unit = null;
			this.hslCurveHistory1.ScrollColor = System.Drawing.Color.Gray;
			this.hslCurveHistory1.Size = new System.Drawing.Size(1089, 186);
			this.hslCurveHistory1.TabIndex = 11;
			this.hslCurveHistory1.Text = "hslCurveHistory1";
			this.hslCurveHistory1.Visible = false;
			this.button_clear_all.Location = new System.Drawing.Point(505, 1);
			this.button_clear_all.Name = "button_clear_all";
			this.button_clear_all.Size = new System.Drawing.Size(83, 27);
			this.button_clear_all.TabIndex = 12;
			this.button_clear_all.Text = "删除全部";
			this.button_clear_all.UseVisualStyleBackColor = true;
			this.button_clear_all.Click += new System.EventHandler(button_clear_all_Click);
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(871, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(59, 17);
			this.label3.TabIndex = 14;
			this.label3.Text = "请求间歇:";
			this.textBox_sleep_time.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_sleep_time.Location = new System.Drawing.Point(950, 3);
			this.textBox_sleep_time.Name = "textBox_sleep_time";
			this.textBox_sleep_time.Size = new System.Drawing.Size(38, 23);
			this.textBox_sleep_time.TabIndex = 13;
			this.textBox_sleep_time.Text = "0";
			this.toCsvClipToolStripMenuItem.Name = "toCsvClipToolStripMenuItem";
			this.toCsvClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toCsvClipToolStripMenuItem.Text = "导出Csv到剪切板";
			this.fromClipCsvToolStripMenuItem.Name = "fromClipCsvToolStripMenuItem";
			this.fromClipCsvToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromClipCsvToolStripMenuItem.Text = "从剪切板导入Csv";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.textBox_sleep_time);
			base.Controls.Add(this.button_clear_all);
			base.Controls.Add(this.hslCurveHistory1);
			base.Controls.Add(this.label_info);
			base.Controls.Add(this.button_from_file);
			base.Controls.Add(this.button_out_file);
			base.Controls.Add(this.button_from_clip);
			base.Controls.Add(this.button_out_clip);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox_time);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "DataTableControl";
			base.Size = new System.Drawing.Size(1095, 417);
			base.Load += new System.EventHandler(DataTableControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
