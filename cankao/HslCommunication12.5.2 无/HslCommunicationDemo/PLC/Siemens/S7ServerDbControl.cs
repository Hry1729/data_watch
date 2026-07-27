using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class S7ServerDbControl : UserControl
	{
		private SiemensS7Server s7Server = null;

		private List<DbItem> dbItems = new List<DbItem>();

		private IContainer components = null;

		private DataGridView dataGridView1;

		private Label label1;

		private Button button_db_remove;

		private Button button_db_add;

		private TextBox textBox_db;

		private Label label2;

		private TextBox textBox_length;

		private Label label3;

		private TextBox textBox_name;

		private Label label4;

		private TextBox textBox_desc;

		private Label label5;

		private DataGridViewTextBoxColumn Column_name;

		private DataGridViewTextBoxColumn Column_db;

		private DataGridViewTextBoxColumn Column_length;

		private DataGridViewTextBoxColumn Column_des;

		private Label label_row_count;

		private Label label6;

		private Label label7;

		private TextBox textBox_code;

		public S7ServerDbControl()
		{
			InitializeComponent();
			if (Program.Language == 2)
			{
				label1.Text = "Current DB block: (excluding the built-in DB1,DB2, DB3)";
				label6.Text = "Batch add: 100-200";
			}
		}

		public void SetServer(SiemensS7Server s7Server)
		{
			this.s7Server = s7Server;
			if (dataGridView1.RowCount <= 0)
			{
				return;
			}
			for (int i = 0; i < dataGridView1.RowCount; i++)
			{
				if (dataGridView1.Rows[i].Tag is DbItem dbItem)
				{
					if (dbItem.Size > 0)
					{
						s7Server.AddDbBlock(dbItem.DbNumber, dbItem.Size);
					}
					else
					{
						s7Server.AddDbBlock(dbItem.DbNumber);
					}
				}
			}
		}

		private void button_db_add_Click(object sender, EventArgs e)
		{
			if (int.TryParse(textBox_length.Text, out var result))
			{
				int result2;
				if (result <= 0)
				{
					MessageBox.Show("Db Size must be > 0");
				}
				else if (int.TryParse(textBox_db.Text, out result2))
				{
					AddDbBlocks(new DbItem
					{
						DbNumber = result2,
						Size = result,
						Name = textBox_name.Text,
						Note = textBox_desc.Text
					});
					label_row_count.Text = "Rows: " + dataGridView1.RowCount;
				}
				else if (Regex.IsMatch(textBox_db.Text, "^[0-9]+-[0-9]+$"))
				{
					string[] array = textBox_db.Text.Split('-');
					if (int.TryParse(array[0], out var result3) && int.TryParse(array[1], out var result4))
					{
						if (result3 >= result4)
						{
							MessageBox.Show("Db Start must be less than Db End");
							return;
						}
						for (int i = result3; i <= result4; i++)
						{
							AddDbBlocks(new DbItem
							{
								DbNumber = i,
								Size = result,
								Name = textBox_name.Text,
								Note = textBox_desc.Text
							});
						}
						label_row_count.Text = "Rows: " + dataGridView1.RowCount;
					}
					else
					{
						MessageBox.Show("Db Number input wrong");
					}
				}
				else
				{
					MessageBox.Show("Db Number input wrong");
				}
			}
			else
			{
				MessageBox.Show("Db Size input wrong");
			}
		}

		private void AddDbBlocks(DbItem item)
		{
			if (item != null)
			{
				if (string.IsNullOrEmpty(item.Name))
				{
					item.Name = $"DB{item.DbNumber}";
				}
				dbItems.Add(item);
				int num = dataGridView1.Rows.Add();
				SetDgvr(dataGridView1.Rows[num], num, item);
				s7Server.AddDbBlock(item.DbNumber, item.Size);
				textBox_code.Text = $"server.AddDbBlock( {item.DbNumber}, {item.Size} );";
			}
		}

		private void SetDgvr(DataGridViewRow dgvr, int rowIndex, DbItem dbItem)
		{
			dgvr.Cells[0].Value = dbItem.Name;
			dgvr.Cells[1].Value = dbItem.DbNumber.ToString();
			dgvr.Cells[2].Value = dbItem.Size.ToString();
			dgvr.Cells[3].Value = dbItem.Note.ToString();
			dgvr.Tag = dbItem;
		}

		private void button_db_remove_Click(object sender, EventArgs e)
		{
			if (int.TryParse(textBox_db.Text, out var result))
			{
				for (int i = 0; i < dataGridView1.RowCount; i++)
				{
					if (dataGridView1.Rows[i].Tag is DbItem dbItem && dbItem.DbNumber == result)
					{
						dataGridView1.Rows.RemoveAt(i);
						s7Server.RemoveDbBlock(result);
						DemoUtils.ShowMessage($"Remove DB{result} Success");
						label_row_count.Text = "Rows: " + dataGridView1.RowCount;
						textBox_code.Text = $"server.RemoveDbBlock( {result} );";
						return;
					}
				}
				DemoUtils.ShowMessage($"Remove DB{result} failed: not find db");
			}
			else if (Regex.IsMatch(textBox_db.Text, "^[0-9]+-[0-9]+$"))
			{
				string[] array = textBox_db.Text.Split('-');
				if (int.TryParse(array[0], out var result2) && int.TryParse(array[1], out var result3))
				{
					if (result2 >= result3)
					{
						MessageBox.Show("Db Start must be less than Db End");
						return;
					}
					int num = 0;
					for (int j = result2; j <= result3; j++)
					{
						for (int k = 0; k < dataGridView1.RowCount; k++)
						{
							if (dataGridView1.Rows[k].Tag is DbItem dbItem2 && dbItem2.DbNumber == j)
							{
								dataGridView1.Rows.RemoveAt(k);
								s7Server.RemoveDbBlock(j);
								num++;
								break;
							}
						}
					}
					DemoUtils.ShowMessage($"Remove DB{result2}-{result3} Success, Count: {num}");
					label_row_count.Text = "Rows: " + dataGridView1.RowCount;
				}
				else
				{
					MessageBox.Show("Db Number input wrong");
				}
			}
			else
			{
				MessageBox.Show("Db Number input wrong");
			}
		}

		public void SaveToXElement(XElement element)
		{
			for (int i = 0; i < dataGridView1.Rows.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				if (!dataGridViewRow.IsNewRow && dataGridViewRow.Tag is DbItem dbItem)
				{
					element.Add(dbItem.ToXml());
				}
			}
		}

		public int LoadFromXElement(XElement element)
		{
			int num = 0;
			foreach (XElement item in element.Elements("DbItem"))
			{
				DbItem dbItem = new DbItem(item);
				int num2 = dataGridView1.Rows.Add();
				SetDgvr(dataGridView1.Rows[num2], num2, dbItem);
				num++;
			}
			label_row_count.Text = "Rows: " + dataGridView1.RowCount;
			return num;
		}

		private void S7ServerDbControl_SizeChanged(object sender, EventArgs e)
		{
			if (dataGridView1.Width > 450)
			{
				Column_des.Width = dataGridView1.Width - 350 - 43 - 18;
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
			this.Column_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_db = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_length = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_des = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label1 = new System.Windows.Forms.Label();
			this.button_db_remove = new System.Windows.Forms.Button();
			this.button_db_add = new System.Windows.Forms.Button();
			this.textBox_db = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_length = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_name = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_desc = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label_row_count = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_code = new System.Windows.Forms.TextBox();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.AliceBlue;
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_name, this.Column_db, this.Column_length, this.Column_des);
			this.dataGridView1.Location = new System.Drawing.Point(1, 25);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(611, 206);
			this.dataGridView1.TabIndex = 0;
			this.Column_name.HeaderText = "Name";
			this.Column_name.Name = "Column_name";
			this.Column_name.ReadOnly = true;
			this.Column_name.Width = 150;
			this.Column_db.HeaderText = "DB";
			this.Column_db.Name = "Column_db";
			this.Column_db.ReadOnly = true;
			this.Column_length.HeaderText = "Length";
			this.Column_length.Name = "Column_length";
			this.Column_length.ReadOnly = true;
			this.Column_des.HeaderText = "Description";
			this.Column_des.Name = "Column_des";
			this.Column_des.ReadOnly = true;
			this.Column_des.Width = 150;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(290, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "当前的DB块信息: (除系统自带的DB1,DB2, DB3之外)";
			this.button_db_remove.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_db_remove.Location = new System.Drawing.Point(618, 153);
			this.button_db_remove.Name = "button_db_remove";
			this.button_db_remove.Size = new System.Drawing.Size(149, 28);
			this.button_db_remove.TabIndex = 37;
			this.button_db_remove.Text = "Remove";
			this.button_db_remove.UseVisualStyleBackColor = true;
			this.button_db_remove.Click += new System.EventHandler(button_db_remove_Click);
			this.button_db_add.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_db_add.Location = new System.Drawing.Point(618, 121);
			this.button_db_add.Name = "button_db_add";
			this.button_db_add.Size = new System.Drawing.Size(149, 28);
			this.button_db_add.TabIndex = 36;
			this.button_db_add.Text = "Add";
			this.button_db_add.UseVisualStyleBackColor = true;
			this.button_db_add.Click += new System.EventHandler(button_db_add_Click);
			this.textBox_db.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_db.Location = new System.Drawing.Point(696, 2);
			this.textBox_db.Name = "textBox_db";
			this.textBox_db.Size = new System.Drawing.Size(71, 23);
			this.textBox_db.TabIndex = 35;
			this.textBox_db.Text = "10";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(618, 5);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(73, 17);
			this.label2.TabIndex = 34;
			this.label2.Text = "DB block：";
			this.textBox_length.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_length.Location = new System.Drawing.Point(696, 30);
			this.textBox_length.Name = "textBox_length";
			this.textBox_length.Size = new System.Drawing.Size(71, 23);
			this.textBox_length.TabIndex = 39;
			this.textBox_length.Text = "65536";
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(618, 33);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(59, 17);
			this.label3.TabIndex = 38;
			this.label3.Text = "Length：";
			this.textBox_name.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_name.Location = new System.Drawing.Point(696, 59);
			this.textBox_name.Name = "textBox_name";
			this.textBox_name.Size = new System.Drawing.Size(71, 23);
			this.textBox_name.TabIndex = 41;
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(618, 62);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(55, 17);
			this.label4.TabIndex = 40;
			this.label4.Text = "Name：";
			this.textBox_desc.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_desc.Location = new System.Drawing.Point(696, 89);
			this.textBox_desc.Name = "textBox_desc";
			this.textBox_desc.Size = new System.Drawing.Size(71, 23);
			this.textBox_desc.TabIndex = 43;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(618, 92);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(48, 17);
			this.label5.TabIndex = 42;
			this.label5.Text = "Desc：";
			this.label_row_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label_row_count.AutoSize = true;
			this.label_row_count.Location = new System.Drawing.Point(615, 240);
			this.label_row_count.Name = "label_row_count";
			this.label_row_count.Size = new System.Drawing.Size(46, 17);
			this.label_row_count.TabIndex = 44;
			this.label_row_count.Text = "Rows: ";
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label6.ForeColor = System.Drawing.Color.Gray;
			this.label6.Location = new System.Drawing.Point(416, 5);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(196, 17);
			this.label6.TabIndex = 45;
			this.label6.Text = "批量增加：100-200";
			this.label6.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.label7.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(-2, 239);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(46, 17);
			this.label7.TabIndex = 46;
			this.label7.Text = "Code: ";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(50, 235);
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(562, 23);
			this.textBox_code.TabIndex = 47;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.label_row_count);
			base.Controls.Add(this.textBox_desc);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.textBox_name);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox_length);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.button_db_remove);
			base.Controls.Add(this.button_db_add);
			base.Controls.Add(this.textBox_db);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "S7ServerDbControl";
			base.Size = new System.Drawing.Size(770, 262);
			base.SizeChanged += new System.EventHandler(S7ServerDbControl_SizeChanged);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
