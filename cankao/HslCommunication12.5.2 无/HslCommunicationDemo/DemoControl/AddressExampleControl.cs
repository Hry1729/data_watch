using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.DemoControl
{
	public class AddressExampleControl : UserControl
	{
		public delegate void OnDataGridViewRowMouseDoubleClickDelegate(DeviceAddressExample addressExample);

		private static Bitmap emptyImage;

		private IContainer components = null;

		private DataGridView dataGridView1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private DataGridViewTextBoxColumn Column5;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem copyAddressToolStripMenuItem;

		public event OnDataGridViewRowMouseDoubleClickDelegate OnDataGridViewRowMouseDoubleClick;

		public AddressExampleControl()
		{
			InitializeComponent();
			if (emptyImage == null)
			{
				emptyImage = new Bitmap(16, 16);
				Graphics graphics = Graphics.FromImage(emptyImage);
				graphics.Clear(Color.Transparent);
				graphics.Dispose();
			}
			copyAddressToolStripMenuItem.Click += CopyAddressToolStripMenuItem_Click;
		}

		private void RequestAddressExampleControl_Load(object sender, EventArgs e)
		{
			DataGridView1_SizeChanged(sender, e);
			dataGridView1.CellMouseDoubleClick += DataGridView1_CellMouseDoubleClick;
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			if (Program.Language == 2)
			{
				dataGridView1.Columns[0].HeaderText = "Address Example";
				dataGridView1.Columns[1].HeaderText = "Address Meaning";
				dataGridView1.Columns[2].HeaderText = "Bit";
				dataGridView1.Columns[3].HeaderText = "Word";
				dataGridView1.Columns[4].HeaderText = "Special instructions";
			}
			else
			{
				copyAddressToolStripMenuItem.Text = "复制地址文本";
			}
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[0].Width = 150;
			dataGridView1.Columns[1].Width = 185;
			dataGridView1.Columns[2].Width = 35;
			dataGridView1.Columns[3].Width = 35;
			if (dataGridView1.Width - 405 - 20 > 30)
			{
				dataGridView1.Columns[4].Width = dataGridView1.Width - 405 - 20;
			}
			else
			{
				dataGridView1.Columns[4].Width = 30;
			}
		}

		private void DataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].Tag is DeviceAddressExample addressExample)
			{
				this.OnDataGridViewRowMouseDoubleClick?.Invoke(addressExample);
			}
		}

		public void SetAddressExample(DeviceAddressExample[] addressExamples)
		{
			if (addressExamples == null)
			{
				addressExamples = new DeviceAddressExample[0];
			}
			DataGridSpecifyRowCount(dataGridView1, addressExamples.Length);
			for (int i = 0; i < addressExamples.Length; i++)
			{
				dataGridView1.Rows[i].Cells[0].Value = addressExamples[i].AddressExample;
				if (!addressExamples[i].IsHeader)
				{
					if (string.IsNullOrEmpty(addressExamples[i].Unit))
					{
						dataGridView1.Rows[i].Cells[1].Value = addressExamples[i].AddressType;
					}
					else
					{
						dataGridView1.Rows[i].Cells[1].Value = addressExamples[i].AddressType + "(" + addressExamples[i].Unit + ")";
					}
					dataGridView1.Rows[i].Cells[2].Value = (addressExamples[i].BitEnable ? "√" : " ");
					dataGridView1.Rows[i].Cells[3].Value = (addressExamples[i].WordEnable ? "√" : " ");
					dataGridView1.Rows[i].Cells[4].Value = addressExamples[i].Mark;
					dataGridView1.Rows[i].Tag = addressExamples[i];
				}
				else
				{
					dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(160, 160, 160);
					dataGridView1.Rows[i].Cells[0].Style.Font = new Font(Font, FontStyle.Bold);
				}
			}
			if (dataGridView1.RowCount > 0 && dataGridView1.SelectedRows.Count > 0)
			{
				dataGridView1.SelectedRows[0].Selected = false;
			}
		}

		public static void DataGridSpecifyRowCount(DataGridView dataGridView1, int row)
		{
			if (dataGridView1.AllowUserToAddRows)
			{
				row++;
			}
			if (dataGridView1.RowCount < row)
			{
				dataGridView1.Rows.Add(row - dataGridView1.RowCount);
			}
			else if (dataGridView1.RowCount > row)
			{
				int num = dataGridView1.RowCount - row;
				for (int i = 0; i < num; i++)
				{
					dataGridView1.Rows.RemoveAt(dataGridView1.Rows.Count - 1);
				}
			}
			if (row > 0)
			{
				dataGridView1.Rows[0].Cells[0].Selected = false;
			}
		}

		private void dataGridView1_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				contextMenuStrip1.Show(dataGridView1, e.Location);
			}
		}

		private void CopyAddressToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0 && dataGridView1.SelectedRows[0].Cells[0].Value != null)
			{
				Clipboard.SetText(dataGridView1.SelectedRows[0].Cells[0].Value.ToString());
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
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.copyAddressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
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
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column3, this.Column4, this.Column5);
			this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataGridView1.Location = new System.Drawing.Point(0, 0);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
			this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(634, 410);
			this.dataGridView1.TabIndex = 0;
			this.dataGridView1.MouseClick += new System.Windows.Forms.MouseEventHandler(dataGridView1_MouseClick);
			this.Column1.HeaderText = "地址类型";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 140;
			this.Column2.HeaderText = "描述信息";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 110;
			this.Column3.HeaderText = "位";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
			this.Column4.HeaderText = "字";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Resizable = System.Windows.Forms.DataGridViewTriState.True;
			this.Column4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
			this.Column5.HeaderText = "备注";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column5.Width = 150;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.copyAddressToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(155, 26);
			this.copyAddressToolStripMenuItem.Name = "copyAddressToolStripMenuItem";
			this.copyAddressToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
			this.copyAddressToolStripMenuItem.Text = "CopyAddress";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.AliceBlue;
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "AddressExampleControl";
			base.Size = new System.Drawing.Size(634, 410);
			base.Load += new System.EventHandler(RequestAddressExampleControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
