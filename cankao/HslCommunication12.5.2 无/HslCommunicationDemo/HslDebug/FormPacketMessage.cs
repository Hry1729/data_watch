using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.HslDebug
{
	public class FormPacketMessage : Form
	{
		private List<PacketMessageItem> packetMessages;

		private IContainer components = null;

		private DataGridView dataGridView1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private Button button1;

		public List<PacketMessageItem> PacketMessages { get; set; }

		public FormPacketMessage(List<PacketMessageItem> list)
		{
			InitializeComponent();
			packetMessages = list;
		}

		private void FormPacketMessage_Load(object sender, EventArgs e)
		{
			DataGridView1_SizeChanged(sender, e);
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			if (Program.Language == 2)
			{
				dataGridView1.Columns[0].HeaderText = "Name";
				dataGridView1.Columns[1].HeaderText = "Message Content";
				dataGridView1.Columns[2].HeaderText = "Decs";
			}
			List<PacketMessageItem> list = packetMessages;
			if (list != null && list.Count > 0)
			{
				SetAddressExample(packetMessages);
			}
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[0].Width = 150;
			dataGridView1.Columns[1].Width = dataGridView1.Width - 350 - 20 - 40;
			dataGridView1.Columns[2].Width = 200;
		}

		public void SetAddressExample(List<PacketMessageItem> list)
		{
			if (list == null)
			{
				list = new List<PacketMessageItem>();
			}
			AddressExampleControl.DataGridSpecifyRowCount(dataGridView1, list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				dataGridView1.Rows[i].Cells[0].Value = list[i].Name;
				dataGridView1.Rows[i].Cells[1].Value = list[i].Message;
				dataGridView1.Rows[i].Cells[2].Value = list[i].Description;
			}
		}

		private PacketMessageItem GetDataTableItem(DataGridViewRow dgvr)
		{
			PacketMessageItem packetMessageItem = new PacketMessageItem();
			packetMessageItem.Name = ((dgvr.Cells[0].Value != null) ? dgvr.Cells[0].Value.ToString() : string.Empty);
			packetMessageItem.Message = ((dgvr.Cells[1].Value != null) ? dgvr.Cells[1].Value.ToString() : string.Empty);
			packetMessageItem.Description = ((dgvr.Cells[2].Value != null) ? dgvr.Cells[2].Value.ToString() : string.Empty);
			return packetMessageItem;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			List<PacketMessageItem> list = new List<PacketMessageItem>();
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.Cells[1].Value != null && !dataGridViewRow.IsNewRow)
				{
					list.Add(GetDataTableItem(dataGridViewRow));
				}
			}
			PacketMessages = list;
			base.DialogResult = DialogResult.OK;
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
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.button1 = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column3);
			this.dataGridView1.Location = new System.Drawing.Point(0, 0);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(914, 430);
			this.dataGridView1.TabIndex = 0;
			this.Column1.HeaderText = "名称";
			this.Column1.Name = "Column1";
			this.Column1.Width = 150;
			this.Column2.HeaderText = "报文内容";
			this.Column2.Name = "Column2";
			this.Column2.Width = 500;
			this.Column3.HeaderText = "注释";
			this.Column3.Name = "Column3";
			this.Column3.Width = 200;
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button1.Location = new System.Drawing.Point(391, 436);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(137, 38);
			this.button1.TabIndex = 1;
			this.button1.Text = "保存";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(914, 481);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormPacketMessage";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "数据包管理窗体";
			base.Load += new System.EventHandler(FormPacketMessage_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
