using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;

namespace HslCommunicationDemo.DemoControl
{
	public class DataExportControl : UserControl
	{
		private IReadWriteNet device;

		private Thread thread;

		private DateTime finishTime = DateTime.Now;

		private List<ReadAddress> readAddresses = new List<ReadAddress>();

		private int threadVersion = 0;

		private FileStream fs;

		private int timeInterval = 1000;

		private bool threadAbort = false;

		private IContainer components = null;

		private Label label1;

		private DataGridView dataGridView1;

		private Label label2;

		private TextBox textBox_timer;

		private Label label3;

		private DateTimePicker dateTimePicker1;

		private Label label4;

		private Button button_select;

		private Label label5;

		private TextBox textBox_log;

		private LinkLabel linkLabel1;

		private Label label_rows;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewComboBoxColumn Column3;

		private DataGridViewTextBoxColumn Column2;

		private Button button_abort;

		private Button button_export_start;

		private TextBox textBox_export_path;

		public DataExportControl()
		{
			InitializeComponent();
			dataGridView1.RowsAdded += DataGridView1_RowsAdded;
			button_abort.Enabled = false;
		}

		private void DataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
		{
			int num = e.RowIndex - 1;
			if (num >= 0 && num < dataGridView1.RowCount)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[num];
				if (dataGridViewRow.Cells[1].Value == null)
				{
					dataGridViewRow.Cells[1].Value = "Byte";
				}
			}
		}

		private void DataExportControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label1.Text = "Address list for export(Read(address, length)):";
				label2.Text = "Interval(ms): ";
				label3.Text = "Stop Time: ";
				label4.Text = "Save to file auto: ";
				button_select.Text = "select";
				button_export_start.Text = "Start";
				label5.Text = "Read logs: ";
				button_abort.Text = "Abort";
			}
		}

		private void button_select_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "数据文件(*.hsldata)|*.hsldata";
				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox_export_path.Text = saveFileDialog.FileName;
				}
			}
		}

		public void SetReadWriteNet(IReadWriteNet device)
		{
			this.device = device;
		}

		private void AppendLog(string text)
		{
			if (textBox_log.InvokeRequired)
			{
				textBox_log.Invoke(new Action<string>(AppendLog), text);
				return;
			}
			try
			{
				textBox_log.AppendText($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}");
			}
			catch
			{
			}
		}

		private void button_start_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_export_path.Text))
			{
				DemoUtils.ShowMessage("Save path can not be null");
				return;
			}
			try
			{
				timeInterval = int.Parse(textBox_timer.Text);
				if (timeInterval < 0)
				{
					throw new Exception("Time Interval can not below 0");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Time Interval get failed: " + ex.Message);
				return;
			}
			finishTime = dateTimePicker1.Value;
			readAddresses.Clear();
			for (int i = 0; i < dataGridView1.RowCount; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.IsNewRow)
				{
					continue;
				}
				ReadAddress readAddress = new ReadAddress();
				try
				{
					readAddress.Address = dataGridViewRow.Cells[0].Value.ToString();
					if (string.IsNullOrEmpty(readAddress.Address))
					{
						throw new Exception("Address null");
					}
					readAddress.IsByte = dataGridViewRow.Cells[1].Value.ToString() == "Byte";
					readAddress.Length = ushort.Parse(dataGridViewRow.Cells[2].Value.ToString());
					dataGridViewRow.DefaultCellStyle.BackColor = dataGridView1.RowsDefaultCellStyle.BackColor;
					readAddresses.Add(readAddress);
				}
				catch (Exception ex2)
				{
					dataGridViewRow.DefaultCellStyle.BackColor = Color.Tomato;
					DemoUtils.ShowMessage($"Wrong in row[{i}]: " + ex2.Message);
					return;
				}
			}
			if (readAddresses.Count == 0)
			{
				AppendLog("No Address to read");
				return;
			}
			try
			{
				fs = new FileStream(textBox_export_path.Text, FileMode.Create);
				byte[] array = new byte[100];
				array[8] = 1;
				Encoding.ASCII.GetBytes("HSLDemo").CopyTo(array, 0);
				BitConverter.GetBytes(DateTime.Now.Ticks).CopyTo(array, 10);
				BitConverter.GetBytes(timeInterval).CopyTo(array, 26);
				fs.Write(array, 0, array.Length);
			}
			catch (Exception ex3)
			{
				AppendLog("File[" + textBox_export_path.Text + "] stream create failed: " + ex3.Message);
				return;
			}
			threadAbort = false;
			thread = new Thread(ReadFromPlcAndSave);
			thread.IsBackground = true;
			threadVersion++;
			thread.Start(threadVersion);
			button_export_start.Enabled = false;
			button_abort.Enabled = true;
		}

		private void ReadFromPlcAndSave(object obj)
		{
			int num = (int)obj;
			long writeTick = 0L;
			DateTime dateTime = DateTime.Now.AddMilliseconds(-timeInterval);
			do
			{
				bool flag = true;
				do
				{
					Thread.Sleep(20);
					if (num != threadVersion)
					{
						AppendLog("Thread abort!");
						break;
					}
					if ((DateTime.Now - dateTime).TotalMilliseconds >= (double)timeInterval)
					{
						dateTime = DateTime.Now;
						break;
					}
				}
				while (!threadAbort);
				if (threadAbort)
				{
					break;
				}
				fs.Write(BitConverter.GetBytes(readAddresses.Count), 0, 4);
				DateTime now = DateTime.Now;
				int num2 = 0;
				for (int i = 0; i < readAddresses.Count; i++)
				{
					if (num != threadVersion)
					{
						AppendLog("Thread abort!");
						break;
					}
					byte[] bytes = Encoding.UTF8.GetBytes(readAddresses[i].Address);
					fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
					fs.Write(bytes, 0, bytes.Length);
					fs.Write(BitConverter.GetBytes((int)readAddresses[i].Length), 0, 4);
					if (readAddresses[i].IsByte)
					{
						OperateResult<byte[]> operateResult = device.Read(readAddresses[i].Address, readAddresses[i].Length);
						if (!operateResult.IsSuccess)
						{
							AppendLog("Read [" + readAddresses[i].Address + "] failed: " + operateResult.Message);
							fs.WriteByte(byte.MaxValue);
							continue;
						}
						num2++;
						fs.WriteByte(0);
						fs.Write(BitConverter.GetBytes(operateResult.Content.Length), 0, 4);
						fs.Write(operateResult.Content, 0, operateResult.Content.Length);
						continue;
					}
					OperateResult<bool[]> operateResult2 = device.ReadBool(readAddresses[i].Address, readAddresses[i].Length);
					if (!operateResult2.IsSuccess)
					{
						AppendLog("Read Bool[" + readAddresses[i].Address + "] failed: " + operateResult2.Message);
						fs.WriteByte(byte.MaxValue);
						continue;
					}
					num2++;
					fs.WriteByte(1);
					fs.Write(BitConverter.GetBytes(operateResult2.Content.Length), 0, 4);
					byte[] array = operateResult2.Content.ToByteArray();
					fs.Write(array, 0, array.Length);
				}
				writeTick++;
				Invoke((Action)delegate
				{
					label_rows.Text = "Rows: " + writeTick;
				});
				AppendLog($"Read success [{num2}] times, spend: " + (DateTime.Now - now).TotalMilliseconds.ToString("F0"));
			}
			while (!(DateTime.Now > finishTime) && !threadAbort);
			fs.Position = 18L;
			fs.Write(BitConverter.GetBytes(DateTime.Now.Ticks), 0, 8);
			fs.Position = 30L;
			fs.Write(BitConverter.GetBytes(writeTick), 0, 8);
			fs.Flush();
			fs.Dispose();
			AppendLog("Thread finish!");
			Invoke((Action)delegate
			{
				button_export_start.Enabled = true;
			});
		}

		private void DataExportControl_SizeChanged(object sender, EventArgs e)
		{
			if (base.Width > 700)
			{
				textBox_export_path.Size = new Size(base.Width - 597, textBox_export_path.Size.Height);
			}
		}

		private void button_abort_Click(object sender, EventArgs e)
		{
			threadAbort = true;
			button_abort.Enabled = false;
			button_export_start.Enabled = true;
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.label1 = new System.Windows.Forms.Label();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewComboBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_timer = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
			this.label4 = new System.Windows.Forms.Label();
			this.button_select = new System.Windows.Forms.Button();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_log = new System.Windows.Forms.TextBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label_rows = new System.Windows.Forms.Label();
			this.button_abort = new System.Windows.Forms.Button();
			this.button_export_start = new System.Windows.Forms.Button();
			this.textBox_export_path = new System.Windows.Forms.TextBox();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 4);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(279, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "等待数据导出的地址列表(Read(address, length)):";
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(222, 222, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column3, this.Column2);
			this.dataGridView1.Location = new System.Drawing.Point(3, 24);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(370, 298);
			this.dataGridView1.TabIndex = 1;
			this.Column1.HeaderText = "Address";
			this.Column1.Name = "Column1";
			this.Column1.Width = 167;
			this.Column3.HeaderText = "Type";
			this.Column3.Items.AddRange("Byte", "Bool");
			this.Column3.Name = "Column3";
			this.Column3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
			this.Column3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
			this.Column3.Width = 70;
			this.Column2.HeaderText = "Length";
			this.Column2.Name = "Column2";
			this.Column2.Width = 70;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(379, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(88, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "时间间隔(ms): ";
			this.textBox_timer.Location = new System.Drawing.Point(481, 3);
			this.textBox_timer.Name = "textBox_timer";
			this.textBox_timer.Size = new System.Drawing.Size(100, 23);
			this.textBox_timer.TabIndex = 3;
			this.textBox_timer.Text = "1000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(592, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(63, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "截止时间: ";
			this.dateTimePicker1.CustomFormat = "yyyy-MM-dd HH:mm:ss";
			this.dateTimePicker1.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
			this.dateTimePicker1.Location = new System.Drawing.Point(675, 3);
			this.dateTimePicker1.Name = "dateTimePicker1";
			this.dateTimePicker1.Size = new System.Drawing.Size(175, 23);
			this.dateTimePicker1.TabIndex = 5;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(379, 35);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(123, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "自动存储到以下路径: ";
			this.button_select.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_select.Location = new System.Drawing.Point(794, 30);
			this.button_select.Name = "button_select";
			this.button_select.Size = new System.Drawing.Size(68, 27);
			this.button_select.TabIndex = 8;
			this.button_select.Text = "选择";
			this.button_select.UseVisualStyleBackColor = true;
			this.button_select.Click += new System.EventHandler(button_select_Click);
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(379, 71);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(63, 17);
			this.label5.TabIndex = 9;
			this.label5.Text = "读取日志: ";
			this.textBox_log.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_log.Location = new System.Drawing.Point(382, 100);
			this.textBox_log.Multiline = true;
			this.textBox_log.Name = "textBox_log";
			this.textBox_log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_log.Size = new System.Drawing.Size(480, 222);
			this.textBox_log.TabIndex = 10;
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(827, 71);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(35, 17);
			this.linkLabel1.TabIndex = 12;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "Help";
			this.label_rows.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_rows.AutoSize = true;
			this.label_rows.Location = new System.Drawing.Point(717, 71);
			this.label_rows.Name = "label_rows";
			this.label_rows.Size = new System.Drawing.Size(53, 17);
			this.label_rows.TabIndex = 13;
			this.label_rows.Text = "Rows: 0";
			this.button_abort.Location = new System.Drawing.Point(572, 64);
			this.button_abort.Name = "button_abort";
			this.button_abort.Size = new System.Drawing.Size(118, 30);
			this.button_abort.TabIndex = 14;
			this.button_abort.Text = "终止";
			this.button_abort.UseVisualStyleBackColor = true;
			this.button_abort.Click += new System.EventHandler(button_abort_Click);
			this.button_export_start.Location = new System.Drawing.Point(458, 64);
			this.button_export_start.Name = "button_export_start";
			this.button_export_start.Size = new System.Drawing.Size(108, 29);
			this.button_export_start.TabIndex = 15;
			this.button_export_start.Text = "开始";
			this.button_export_start.UseVisualStyleBackColor = true;
			this.button_export_start.Click += new System.EventHandler(button_start_Click);
			this.textBox_export_path.Location = new System.Drawing.Point(519, 32);
			this.textBox_export_path.Name = "textBox_export_path";
			this.textBox_export_path.Size = new System.Drawing.Size(269, 23);
			this.textBox_export_path.TabIndex = 16;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.button_export_start);
			base.Controls.Add(this.button_abort);
			base.Controls.Add(this.label_rows);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.textBox_log);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.button_select);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.dateTimePicker1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox_timer);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox_export_path);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "DataExportControl";
			base.Size = new System.Drawing.Size(866, 325);
			base.Load += new System.EventHandler(DataExportControl_Load);
			base.SizeChanged += new System.EventHandler(DataExportControl_SizeChanged);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
