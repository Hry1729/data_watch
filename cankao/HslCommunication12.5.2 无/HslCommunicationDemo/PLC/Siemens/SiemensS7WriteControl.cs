using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class SiemensS7WriteControl : UserControl
	{
		private SiemensS7Net siemens;

		private IContainer components = null;

		private Button button7;

		private DataGridView dataGridView1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewComboBoxColumn Column3;

		private DataGridViewTextBoxColumn Column2;

		private Label label_code;

		public SiemensS7WriteControl()
		{
			InitializeComponent();
			dataGridView1.Rows.Add();
			dataGridView1.Rows[0].Cells[0].Value = "M100";
			dataGridView1.Rows[0].Cells[1].Value = "short";
			dataGridView1.Rows[0].Cells[2].Value = "1";
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			label_code.Text = "OperateResult result = " + DemoUtils.PlcDeviceName + ".Write( string[] address, List<byte[]> data );";
		}

		private void SiemensS7WriteControl_Load(object sender, EventArgs e)
		{
			DataGridView1_SizeChanged(dataGridView1, e);
			if (Program.Language == 2)
			{
				button7.Text = "Single Msg Write";
			}
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[0].Width = 150;
			dataGridView1.Columns[1].Width = 100;
			int num = dataGridView1.Width - 150 - 100 - 20 - 40;
			dataGridView1.Columns[2].Width = ((num < 100) ? 100 : num);
		}

		public void SetDevice(SiemensS7Net siemensTcpNet, string address)
		{
			siemens = siemensTcpNet;
		}

		private void button7_Click(object sender, EventArgs e)
		{
			List<string> list = new List<string>();
			List<byte[]> list2 = new List<byte[]>();
			for (int i = 0; i < dataGridView1.RowCount; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (dataGridViewRow.IsNewRow)
				{
					continue;
				}
				list.Add(dataGridViewRow.Cells[0].Value.ToString());
				if (dataGridViewRow.Cells[2].Value != null && dataGridViewRow.Cells[1].Value != null)
				{
					switch (dataGridViewRow.Cells[1].Value.ToString())
					{
					case "byte":
						list2.Add(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<byte>());
						break;
					case "short":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<short>()));
						break;
					case "ushort":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<ushort>()));
						break;
					case "int":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<int>()));
						break;
					case "uint":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<uint>()));
						break;
					case "long":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<long>()));
						break;
					case "ulong":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<ulong>()));
						break;
					case "float":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<float>()));
						break;
					case "double":
						list2.Add(siemens.ByteTransform.TransByte(dataGridViewRow.Cells[2].Value.ToString().ToStringArray<double>()));
						break;
					case "hex":
						list2.Add(dataGridViewRow.Cells[2].Value.ToString().ToHexBytes());
						break;
					}
				}
				else
				{
					list2.Add(null);
				}
			}
			OperateResult operateResult = siemens.Write(list.ToArray(), list2);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Write Success");
			}
			else
			{
				DemoUtils.ShowMessage("Write failed: " + operateResult.Message);
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
			this.button7 = new System.Windows.Forms.Button();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewComboBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label_code = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(697, 3);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(148, 42);
			this.button7.TabIndex = 41;
			this.button7.Text = "一次性写入";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(232, 232, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column3, this.Column2);
			this.dataGridView1.Location = new System.Drawing.Point(3, 0);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(688, 257);
			this.dataGridView1.TabIndex = 42;
			this.Column1.HeaderText = "Address";
			this.Column1.Name = "Column1";
			this.Column1.Width = 150;
			this.Column3.HeaderText = "Type";
			this.Column3.Items.AddRange("byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "hex");
			this.Column3.Name = "Column3";
			this.Column2.HeaderText = "Value";
			this.Column2.Name = "Column2";
			this.Column2.Width = 375;
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_code.Location = new System.Drawing.Point(697, 48);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(148, 200);
			this.label_code.TabIndex = 43;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.button7);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "SiemensS7WriteControl";
			base.Size = new System.Drawing.Size(848, 260);
			base.Load += new System.EventHandler(SiemensS7WriteControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
