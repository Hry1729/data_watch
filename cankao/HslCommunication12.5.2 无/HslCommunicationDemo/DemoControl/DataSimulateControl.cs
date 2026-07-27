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
using HslCommunication.Core;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.DemoControl
{
	public class DataSimulateControl : UserControl
	{
		private List<SimulateWrite> writes = new List<SimulateWrite>();

		private Thread threadWrite;

		private IReadWriteNet readWrite;

		private bool threadEnable = true;

		private IContainer components = null;

		private DataGridView dataGridView1;

		private Button button_start;

		private Button button_finish;

		private DataGridViewTextBoxColumn Column_name;

		private DataGridViewTextBoxColumn Column_address;

		private DataGridViewTextBoxColumn Column_time;

		private DataGridViewTextBoxColumn Column_express;

		private DataGridViewTextBoxColumn Column_current;

		private DataGridViewTextBoxColumn Column_mark;

		private Button button_onece;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem rowDeleteToolStripMenuItem;

		private ToolStripMenuItem toXmlClipToolStripMenuItem;

		private ToolStripMenuItem fromClipToolStripMenuItem;

		private ToolStripMenuItem toFileToolStripMenuItem;

		private ToolStripMenuItem fromFileToolStripMenuItem;

		private Label label1;

		private LinkLabel linkLabel1;

		private ToolStripMenuItem toCsvClipToolStripMenuItem;

		private ToolStripMenuItem fromCsvClipCsvToolStripMenuItem;

		private Button button_clear_all;

		public DataSimulateControl()
		{
			InitializeComponent();
			button_start.Click += Button_start_Click;
			button_finish.Click += Button_finish_Click;
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			button_finish.Enabled = false;
			toXmlClipToolStripMenuItem.Click += ToClipToolStripMenuItem_Click;
			fromClipToolStripMenuItem.Click += FromClipToolStripMenuItem_Click;
			toFileToolStripMenuItem.Click += ToFileToolStripMenuItem_Click;
			fromFileToolStripMenuItem.Click += FromFileToolStripMenuItem_Click;
			rowDeleteToolStripMenuItem.Click += RowDeleteToolStripMenuItem_Click;
			toCsvClipToolStripMenuItem.Click += ToCsvClipToolStripMenuItem_Click;
			fromCsvClipCsvToolStripMenuItem.Click += FromCsvClipCsvToolStripMenuItem_Click;
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

		private void FromFileToolStripMenuItem_Click(object sender, EventArgs e)
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
							LoadSimulateTable(XElement.Parse(File.ReadAllText(openFileDialog.FileName, Encoding.UTF8)));
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

		private void ToFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "*XML|*.xml";
				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					XElement xElement = new XElement("SimulateTable");
					GetSimulateTable(xElement);
					xElement.Save(saveFileDialog.FileName);
					DemoUtils.ShowMessage("Save success!");
				}
			}
		}

		private void FromClipToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				LoadSimulateTable(XElement.Parse(Clipboard.GetText()));
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Load failed: " + ex.Message);
			}
		}

		private void ToClipToolStripMenuItem_Click(object sender, EventArgs e)
		{
			XElement xElement = new XElement("SimulateTable");
			GetSimulateTable(xElement);
			Clipboard.SetText(xElement.ToString());
			DemoUtils.ShowMessage("Save success!");
		}

		private void ToCsvClipToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringBuilder stringBuilder = new StringBuilder(Column_name.HeaderText + "\t" + Column_address.HeaderText + "\t" + Column_time.HeaderText + "\t" + Column_express.HeaderText + "\t" + Column_mark.HeaderText + "\r\n");
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[1].Value != null && !dataGridViewRow.IsNewRow)
				{
					SimulateWrite simulateWrite = GetSimulateWrite(dataGridViewRow);
					stringBuilder.Append($"{simulateWrite.Name}\t{simulateWrite.Address}\t{simulateWrite.Time}\t{simulateWrite.Express}\t{simulateWrite.Mark}\r\n");
				}
			}
			Clipboard.SetText(stringBuilder.ToString());
			DemoUtils.ShowMessage("Save success!");
		}

		private void FromCsvClipCsvToolStripMenuItem_Click(object sender, EventArgs e)
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
						SimulateWrite simulateWrite = new SimulateWrite();
						simulateWrite.Name = array2[0];
						simulateWrite.Address = array2[1];
						simulateWrite.Time = int.Parse(array2[2]);
						simulateWrite.Express = array2[3];
						if (array2.Length > 4)
						{
							simulateWrite.Mark = array2[4];
						}
						AddRow(simulateWrite);
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

		private void DataSimulateControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				dataGridView1.Columns[0].HeaderText = "Name";
				dataGridView1.Columns[1].HeaderText = "Address";
				dataGridView1.Columns[2].HeaderText = "Time(ms)";
				dataGridView1.Columns[3].HeaderText = "Expression";
				dataGridView1.Columns[4].HeaderText = "CurrentValue";
				dataGridView1.Columns[5].HeaderText = "Mark";
				button_start.Text = "Start";
				button_finish.Text = "Stop";
				button_onece.Text = "Once";
				label1.Text = "Right-click the to delete, import, export";
				linkLabel1.Text = "Expression Example";
				button_clear_all.Text = "Clear All";
				toXmlClipToolStripMenuItem.Text = "ToXmlClip";
				fromClipToolStripMenuItem.Text = "FromXmlClip";
				toFileToolStripMenuItem.Text = "ToXmlFile";
				fromFileToolStripMenuItem.Text = "FromXmlFile";
				toCsvClipToolStripMenuItem.Text = "ToCsvClip";
				fromCsvClipCsvToolStripMenuItem.Text = "FromCsvClip";
			}
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[0].Width = 120;
			dataGridView1.Columns[1].Width = 120;
			dataGridView1.Columns[2].Width = 100;
			dataGridView1.Columns[3].Width = 280;
			dataGridView1.Columns[4].Width = (dataGridView1.Width - 620 - 62) / 2;
			dataGridView1.Columns[5].Width = (dataGridView1.Width - 620 - 62) / 2;
		}

		public void SetReadWriteNet(IReadWriteNet readwtite)
		{
			readWrite = readwtite;
			DataGridView1_SizeChanged(null, EventArgs.Empty);
		}

		private void Button_start_Click(object sender, EventArgs e)
		{
			if (readWrite == null)
			{
				return;
			}
			List<SimulateWrite> list = new List<SimulateWrite>();
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[1].Value != null && !dataGridViewRow.IsNewRow)
				{
					try
					{
						SimulateWrite simulateWrite = GetSimulateWrite(dataGridViewRow);
						simulateWrite.Script = new Interpreter();
						simulateWrite.Script.Eval(simulateWrite.Express, new Parameter("x", 0), new Parameter("r", HslHelper.HslRandom));
						simulateWrite.Dgvr = dataGridViewRow;
						simulateWrite.ExecuteTime = DateTime.Now.AddDays(-1.0);
						list.Add(simulateWrite);
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage($"Row[{i}] has wrong value: " + ex.Message);
						return;
					}
				}
			}
			threadWrite?.Abort();
			writes = list;
			threadEnable = true;
			threadWrite = new Thread(ThreadWriteMethod);
			threadWrite.IsBackground = true;
			threadWrite.Start();
			button_start.Enabled = false;
			button_finish.Enabled = true;
		}

		private void Button_finish_Click(object sender, EventArgs e)
		{
			threadEnable = false;
			button_start.Enabled = true;
			button_finish.Enabled = false;
			ThreadPool.QueueUserWorkItem(ThreadPoolCheckState, 123);
		}

		public void Close()
		{
			if (threadEnable)
			{
				Button_finish_Click(button_finish, EventArgs.Empty);
			}
		}

		private void ThreadPoolCheckState(object obj)
		{
			Thread.Sleep(200);
			if (threadWrite != null && !threadEnable && threadWrite.ThreadState == ThreadState.Running)
			{
				threadWrite?.Abort();
			}
		}

		private void RunOnce()
		{
		}

		private void ThreadWriteMethod()
		{
			while (threadEnable)
			{
				HslHelper.ThreadSleep(20);
				for (int i = 0; i < writes.Count; i++)
				{
					if (!threadEnable)
					{
						return;
					}
					SimulateWrite simulate = writes[i];
					if (!((DateTime.Now - simulate.ExecuteTime).TotalMilliseconds > (double)simulate.Time))
					{
						continue;
					}
					List<Parameter> list = new List<Parameter>();
					list.Add(new Parameter("x", simulate.X));
					list.Add(new Parameter("r", HslHelper.HslRandom));
					object value = simulate.Script.Eval(simulate.Express, list.ToArray());
					if (value == null)
					{
						continue;
					}
					Type type = value.GetType();
					OperateResult writeResult = null;
					if (type == typeof(bool))
					{
						writeResult = readWrite.Write(simulate.Address, (bool)value);
					}
					else if (type == typeof(bool[]))
					{
						writeResult = readWrite.Write(simulate.Address, (bool[])value);
					}
					else if (type == typeof(short))
					{
						writeResult = readWrite.Write(simulate.Address, (short)value);
					}
					else if (type == typeof(short[]))
					{
						writeResult = readWrite.Write(simulate.Address, (short[])value);
					}
					else if (type == typeof(byte))
					{
						writeResult = readWrite.Write(simulate.Address, new byte[1] { (byte)value });
					}
					else if (type == typeof(byte[]))
					{
						writeResult = readWrite.Write(simulate.Address, (byte[])value);
					}
					else if (type == typeof(int))
					{
						writeResult = readWrite.Write(simulate.Address, (int)value);
					}
					else if (type == typeof(int[]))
					{
						writeResult = readWrite.Write(simulate.Address, (int[])value);
					}
					else if (type == typeof(long))
					{
						writeResult = readWrite.Write(simulate.Address, (long)value);
					}
					else if (type == typeof(long[]))
					{
						writeResult = readWrite.Write(simulate.Address, (long[])value);
					}
					else if (type == typeof(float))
					{
						writeResult = readWrite.Write(simulate.Address, (float)value);
					}
					else if (type == typeof(float[]))
					{
						writeResult = readWrite.Write(simulate.Address, (float[])value);
					}
					else if (type == typeof(double))
					{
						writeResult = readWrite.Write(simulate.Address, (double)value);
					}
					else if (type == typeof(double[]))
					{
						writeResult = readWrite.Write(simulate.Address, (double[])value);
					}
					else
					{
						if (!(type == typeof(string)))
						{
							Invoke((Action)delegate
							{
								DemoUtils.ShowMessage("Write address[" + simulate.Address + "] failed, type[" + type.Name + "] is not supported");
							});
							return;
						}
						writeResult = readWrite.Write(simulate.Address, (string)value);
					}
					if (!threadEnable)
					{
						return;
					}
					try
					{
						Invoke((Action)delegate
						{
							simulate.Dgvr.Cells[4].Value = value;
						});
					}
					catch (InvalidOperationException)
					{
						return;
					}
					if (!writeResult.IsSuccess)
					{
						Invoke((Action)delegate
						{
							DemoUtils.ShowMessage("Write address[" + simulate.Address + "] failed: " + writeResult.Message);
							simulate.Dgvr.Cells[1].Selected = true;
						});
						return;
					}
					simulate.ExecuteTime = DateTime.Now;
					simulate.X++;
				}
			}
		}

		private void button_onece_Click(object sender, EventArgs e)
		{
			if (readWrite == null)
			{
				return;
			}
			List<SimulateWrite> list = new List<SimulateWrite>();
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[1].Value != null && !dataGridViewRow.IsNewRow)
				{
					try
					{
						SimulateWrite simulateWrite = GetSimulateWrite(dataGridViewRow);
						simulateWrite.Script = new Interpreter();
						simulateWrite.Script.Eval(simulateWrite.Express, new Parameter("x", 0), new Parameter("r", HslHelper.HslRandom));
						simulateWrite.Dgvr = dataGridViewRow;
						simulateWrite.ExecuteTime = DateTime.Now.AddDays(-1.0);
						list.Add(simulateWrite);
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage($"Row[{i}] has wrong value: " + ex.Message);
						return;
					}
				}
			}
			DateTime now = DateTime.Now;
			for (int j = 0; j < list.Count; j++)
			{
				SimulateWrite simulateWrite2 = list[j];
				if (!((DateTime.Now - simulateWrite2.ExecuteTime).TotalMilliseconds > (double)simulateWrite2.Time))
				{
					continue;
				}
				List<Parameter> list2 = new List<Parameter>();
				list2.Add(new Parameter("x", simulateWrite2.X));
				list2.Add(new Parameter("r", HslHelper.HslRandom));
				object obj = simulateWrite2.Script.Eval(simulateWrite2.Express, list2.ToArray());
				if (obj == null)
				{
					continue;
				}
				Type type = obj.GetType();
				OperateResult operateResult = null;
				if (type == typeof(bool))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (bool)obj);
				}
				else if (type == typeof(bool[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (bool[])obj);
				}
				else if (type == typeof(short))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (short)obj);
				}
				else if (type == typeof(short[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (short[])obj);
				}
				else if (type == typeof(byte))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, new byte[1] { (byte)obj });
				}
				else if (type == typeof(byte[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (byte[])obj);
				}
				else if (type == typeof(int))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (int)obj);
				}
				else if (type == typeof(int[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (int[])obj);
				}
				else if (type == typeof(long))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (long)obj);
				}
				else if (type == typeof(long[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (long[])obj);
				}
				else if (type == typeof(float))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (float)obj);
				}
				else if (type == typeof(float[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (float[])obj);
				}
				else if (type == typeof(double))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (double)obj);
				}
				else if (type == typeof(double[]))
				{
					operateResult = readWrite.Write(simulateWrite2.Address, (double[])obj);
				}
				else
				{
					if (!(type == typeof(string)))
					{
						DemoUtils.ShowMessage("Write address[" + simulateWrite2.Address + "] failed, type[" + type.Name + "] is not supported");
						return;
					}
					operateResult = readWrite.Write(simulateWrite2.Address, (string)obj);
				}
				try
				{
					simulateWrite2.Dgvr.Cells[4].Value = obj;
				}
				catch (InvalidOperationException)
				{
					return;
				}
				if (operateResult.IsSuccess)
				{
					simulateWrite2.ExecuteTime = DateTime.Now;
					simulateWrite2.X++;
					continue;
				}
				DemoUtils.ShowMessage("Write address[" + simulateWrite2.Address + "] failed: " + operateResult.Message);
				simulateWrite2.Dgvr.Cells[1].Selected = true;
				return;
			}
			DemoUtils.ShowMessage("Write all address success! timecost : " + (DateTime.Now - now).TotalMilliseconds.ToString("F0") + " ms");
		}

		public SimulateWrite GetSimulateWrite(DataGridViewRow dgvr)
		{
			SimulateWrite simulateWrite = new SimulateWrite();
			simulateWrite.Name = dgvr.Cells[0].Value?.ToString();
			simulateWrite.Address = dgvr.Cells[1].Value?.ToString();
			simulateWrite.Time = int.Parse(dgvr.Cells[2].Value?.ToString());
			simulateWrite.Express = dgvr.Cells[3].Value?.ToString();
			simulateWrite.Mark = dgvr.Cells[5].Value?.ToString();
			return simulateWrite;
		}

		public void GetSimulateTable(XElement element)
		{
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[1].Value != null && !dataGridViewRow.IsNewRow)
				{
					element.Add(GetSimulateWrite(dataGridViewRow).ToXmlElement());
				}
			}
		}

		private void AddRow(SimulateWrite item)
		{
			int index = dataGridView1.Rows.Add();
			DataGridViewRow dataGridViewRow = dataGridView1.Rows[index];
			dataGridViewRow.Cells[0].Value = item.Name;
			dataGridViewRow.Cells[1].Value = item.Address;
			dataGridViewRow.Cells[2].Value = item.Time.ToString();
			dataGridViewRow.Cells[3].Value = item.Express;
			dataGridViewRow.Cells[5].Value = item.Mark;
			dataGridViewRow.Tag = item;
		}

		public int LoadSimulateTable(XElement element)
		{
			int num = 0;
			foreach (XElement item in element.Elements("SimulateWrite"))
			{
				SimulateWrite simulateWrite = new SimulateWrite();
				simulateWrite.LoadByXmlElement(item);
				AddRow(simulateWrite);
				num++;
			}
			return num;
		}

		private void linkLabel1_Click(object sender, EventArgs e)
		{
			FormExpressionExample formExpressionExample = new FormExpressionExample();
			formExpressionExample.StartPosition = FormStartPosition.CenterParent;
			formExpressionExample.ShowDialog(this);
			formExpressionExample.Dispose();
		}

		private void dataGridView1_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
		{
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_address = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_time = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_express = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_current = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_mark = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.rowDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toXmlClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toCsvClipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fromCsvClipCsvToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.button_start = new System.Windows.Forms.Button();
			this.button_finish = new System.Windows.Forms.Button();
			this.button_onece = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.button_clear_all = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
			this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_name, this.Column_address, this.Column_time, this.Column_express, this.Column_current, this.Column_mark);
			this.dataGridView1.ContextMenuStrip = this.contextMenuStrip1;
			this.dataGridView1.Location = new System.Drawing.Point(0, 30);
			this.dataGridView1.Name = "dataGridView1";
			dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
			this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(922, 328);
			this.dataGridView1.TabIndex = 1;
			this.dataGridView1.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView1_CellMouseClick);
			this.Column_name.HeaderText = "名称";
			this.Column_name.Name = "Column_name";
			this.Column_name.Width = 140;
			this.Column_address.HeaderText = "设备地址";
			this.Column_address.Name = "Column_address";
			this.Column_address.Width = 120;
			this.Column_time.HeaderText = "间隔时间(ms)";
			this.Column_time.Name = "Column_time";
			this.Column_express.HeaderText = "表达式";
			this.Column_express.Name = "Column_express";
			this.Column_express.Resizable = System.Windows.Forms.DataGridViewTriState.True;
			this.Column_express.Width = 200;
			this.Column_current.HeaderText = "当前值";
			this.Column_current.Name = "Column_current";
			this.Column_current.Width = 150;
			this.Column_mark.HeaderText = "备注";
			this.Column_mark.Name = "Column_mark";
			this.Column_mark.Width = 150;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[7] { this.rowDeleteToolStripMenuItem, this.toXmlClipToolStripMenuItem, this.fromClipToolStripMenuItem, this.toFileToolStripMenuItem, this.fromFileToolStripMenuItem, this.toCsvClipToolStripMenuItem, this.fromCsvClipCsvToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(181, 180);
			this.rowDeleteToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.rowDeleteToolStripMenuItem.Name = "rowDeleteToolStripMenuItem";
			this.rowDeleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.rowDeleteToolStripMenuItem.Text = "RowDelete";
			this.toXmlClipToolStripMenuItem.Name = "toXmlClipToolStripMenuItem";
			this.toXmlClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toXmlClipToolStripMenuItem.Text = "导出到剪切板";
			this.fromClipToolStripMenuItem.Name = "fromClipToolStripMenuItem";
			this.fromClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromClipToolStripMenuItem.Text = "从剪切板导入";
			this.toFileToolStripMenuItem.Name = "toFileToolStripMenuItem";
			this.toFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toFileToolStripMenuItem.Text = "导出到文件";
			this.fromFileToolStripMenuItem.Name = "fromFileToolStripMenuItem";
			this.fromFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromFileToolStripMenuItem.Text = "从文件导入";
			this.toCsvClipToolStripMenuItem.Name = "toCsvClipToolStripMenuItem";
			this.toCsvClipToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.toCsvClipToolStripMenuItem.Text = "导出Csv到剪切板";
			this.fromCsvClipCsvToolStripMenuItem.Name = "fromCsvClipCsvToolStripMenuItem";
			this.fromCsvClipCsvToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.fromCsvClipCsvToolStripMenuItem.Text = "从剪切板导入Csv";
			this.button_start.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_start.Location = new System.Drawing.Point(775, 1);
			this.button_start.Name = "button_start";
			this.button_start.Size = new System.Drawing.Size(69, 28);
			this.button_start.TabIndex = 3;
			this.button_start.Text = "开始";
			this.button_start.UseVisualStyleBackColor = true;
			this.button_finish.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_finish.Location = new System.Drawing.Point(846, 1);
			this.button_finish.Name = "button_finish";
			this.button_finish.Size = new System.Drawing.Size(72, 28);
			this.button_finish.TabIndex = 4;
			this.button_finish.Text = "停止";
			this.button_finish.UseVisualStyleBackColor = true;
			this.button_onece.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_onece.Location = new System.Drawing.Point(702, 1);
			this.button_onece.Name = "button_onece";
			this.button_onece.Size = new System.Drawing.Size(69, 28);
			this.button_onece.TabIndex = 5;
			this.button_onece.Text = "运行一次";
			this.button_onece.UseVisualStyleBackColor = true;
			this.button_onece.Click += new System.EventHandler(button_onece_Click);
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.Color.Gray;
			this.label1.Location = new System.Drawing.Point(4, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(152, 17);
			this.label1.TabIndex = 6;
			this.label1.Text = "右键菜单删除、导入、导出";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(241, 7);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(92, 17);
			this.linkLabel1.TabIndex = 7;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "表达式参考例子";
			this.linkLabel1.Click += new System.EventHandler(linkLabel1_Click);
			this.button_clear_all.Location = new System.Drawing.Point(442, 2);
			this.button_clear_all.Name = "button_clear_all";
			this.button_clear_all.Size = new System.Drawing.Size(83, 27);
			this.button_clear_all.TabIndex = 13;
			this.button_clear_all.Text = "删除全部";
			this.button_clear_all.UseVisualStyleBackColor = true;
			this.button_clear_all.Click += new System.EventHandler(button_clear_all_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.button_clear_all);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.button_onece);
			base.Controls.Add(this.button_finish);
			base.Controls.Add(this.button_start);
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "DataSimulateControl";
			base.Size = new System.Drawing.Size(922, 358);
			base.Load += new System.EventHandler(DataSimulateControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
