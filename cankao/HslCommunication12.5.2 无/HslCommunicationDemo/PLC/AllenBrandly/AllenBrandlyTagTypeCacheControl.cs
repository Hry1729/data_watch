using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication.Profinet.AllenBradley;

namespace HslCommunicationDemo.PLC.AllenBrandly
{
	public class AllenBrandlyTagTypeCacheControl : UserControl
	{
		private AllenBradleyNet ab = null;

		private IContainer components = null;

		private DataGridView dataGridView1;

		private Label label1;

		private Button button1;

		private DataGridViewTextBoxColumn Column_id;

		private DataGridViewTextBoxColumn Column_TagName;

		private DataGridViewTextBoxColumn Column_TypeCode;

		private DataGridViewTextBoxColumn Column_Decs;

		public AllenBrandlyTagTypeCacheControl()
		{
			InitializeComponent();
		}

		public void SetDevice(AllenBradleyNet allenBradley)
		{
			ab = allenBradley;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (ab == null)
			{
				return;
			}
			Dictionary<string, int> tagValueAndDataBits = ab.TagValueAndDataBits;
			if (tagValueAndDataBits == null)
			{
				DemoUtils.DataGridSpecifyRowCount(dataGridView1, 0);
				return;
			}
			DemoUtils.DataGridSpecifyRowCount(dataGridView1, tagValueAndDataBits.Count);
			int num = 0;
			foreach (KeyValuePair<string, int> item in tagValueAndDataBits)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[num];
				dataGridViewRow.Cells[0].Value = num + 1;
				dataGridViewRow.Cells[1].Value = item.Key;
				dataGridViewRow.Cells[2].Value = item.Value;
				num++;
			}
		}

		private void AllenBrandlyTagTypeCacheControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				label1.Text = "Cached tagName and data byte length when writing a bool value to the address i=xxx[2]";
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column_id = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_TagName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_TypeCode = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_Decs = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label1 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_id, this.Column_TagName, this.Column_TypeCode, this.Column_Decs);
			this.dataGridView1.Location = new System.Drawing.Point(3, 27);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(819, 232);
			this.dataGridView1.TabIndex = 0;
			this.Column_id.HeaderText = "Id";
			this.Column_id.Name = "Column_id";
			this.Column_id.ReadOnly = true;
			this.Column_TagName.HeaderText = "TagName";
			this.Column_TagName.Name = "Column_TagName";
			this.Column_TagName.ReadOnly = true;
			this.Column_TagName.Width = 300;
			this.Column_TypeCode.HeaderText = "DataBits";
			this.Column_TypeCode.Name = "Column_TypeCode";
			this.Column_TypeCode.ReadOnly = true;
			this.Column_TypeCode.Width = 150;
			this.Column_Decs.HeaderText = "Decscription";
			this.Column_Decs.Name = "Column_Decs";
			this.Column_Decs.ReadOnly = true;
			this.Column_Decs.Width = 200;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(448, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "地址使用 i=xxx[2] 写入bool值时，缓存的标签名称，以及关联的数据字节长度信息";
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(746, 0);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(75, 25);
			this.button1.TabIndex = 2;
			this.button1.Text = "Refresh";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.button1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "AllenBrandlyTagTypeCacheControl";
			base.Size = new System.Drawing.Size(825, 262);
			base.Load += new System.EventHandler(AllenBrandlyTagTypeCacheControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
