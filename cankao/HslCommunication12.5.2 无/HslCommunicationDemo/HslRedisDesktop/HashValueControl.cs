using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunicationDemo;

namespace HslRedisDesktop
{
	public class HashValueControl : UserControl
	{
		private string selectField = string.Empty;

		private string stringKeyName = string.Empty;

		private RedisSettings redisClient = null;

		private IContainer components = null;

		private SplitContainer splitContainer1;

		private Button button5;

		private TextBox textBox1;

		private Button button4;

		private Button button3;

		private Button button2;

		private Button button1;

		private DataGridView dataGridView1;

		private ValueControl valueControl1;

		private Label label3;

		private Label label4;

		private Label label2;

		private Label label1;

		private KeyOperateControl keyOperateControl1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		public HashValueControl()
		{
			InitializeComponent();
		}

		private void ListValueControl_Load(object sender, EventArgs e)
		{
			keyOperateControl1.LoadValue.Click += LoadValue_Click;
			dataGridView1.SelectionChanged += DataGridView1_SelectionChanged;
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
			DataGridView1_SizeChanged(null, null);
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[1].Width = dataGridView1.Width - dataGridView1.Columns[0].Width - 61;
		}

		private void LoadValue_Click(object sender, EventArgs e)
		{
			RefreshKey();
		}

		private void RefreshKey()
		{
			if (redisClient == null)
			{
				DemoUtils.ShowMessage("当前的连接为空，无法读取！");
				return;
			}
			OperateResult<string[]> operateResult = redisClient.Redis.ReadHashKeyAll(stringKeyName);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("读取失败：" + operateResult.Message);
				return;
			}
			int num = 0;
			for (int i = 0; i < operateResult.Content.Length; i++)
			{
				num += Encoding.UTF8.GetBytes(operateResult.Content[i]).Length;
			}
			label2.Text = "大小：" + SoftBasic.GetSizeDescription(num);
			label3.Text = (operateResult.Content.Length / 2).ToString();
			Utils.ControlDataGridViewRow(dataGridView1, operateResult.Content.Length / 2);
			for (int j = 0; j < operateResult.Content.Length / 2; j++)
			{
				dataGridView1.Rows[j].Cells[0].Value = operateResult.Content[j * 2];
				dataGridView1.Rows[j].Cells[1].Value = operateResult.Content[j * 2 + 1];
			}
		}

		private void DataGridView1_SelectionChanged(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0)
			{
				if (dataGridView1.SelectedRows[0].Cells[1].Value != null)
				{
					string value = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
					selectField = dataGridView1.SelectedRows[0].Cells[0].Value.ToString();
					valueControl1.SetValue(value);
				}
			}
			else
			{
				selectField = string.Empty;
			}
		}

		public void SetNewKey(RedisSettings redisClient, string key)
		{
			if (key != stringKeyName)
			{
				valueControl1.SetValue(string.Empty);
			}
			this.redisClient = redisClient;
			stringKeyName = key;
			keyOperateControl1.SetRedisClient(redisClient.Redis, key);
			RefreshKey();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0)
			{
				using (HashValueEditForm hashValueEditForm = new HashValueEditForm(redisClient, stringKeyName, selectField, valueControl1.KeySourceValue()))
				{
					hashValueEditForm.ShowDialog();
					return;
				}
			}
			DemoUtils.ShowMessage("请先选择对应的域数据，然后再点击编辑");
		}

		private void button1_Click(object sender, EventArgs e)
		{
			using (HashValueEditForm hashValueEditForm = new HashValueEditForm(redisClient, stringKeyName, string.Empty, string.Empty))
			{
				hashValueEditForm.ShowDialog();
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0)
			{
				if (DemoUtils.ShowMessage("确认是否真的删除Key:" + stringKeyName + " Field:" + selectField + " 的数据信息？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
				{
					OperateResult operateResult = redisClient.Redis.DeleteHashKey(stringKeyName, selectField);
					if (operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("删除 Field 成功！");
					}
					else
					{
						DemoUtils.ShowMessage("删除 Field 失败！" + operateResult.Message);
					}
				}
			}
			else
			{
				DemoUtils.ShowMessage("请先选择对应的域数据，然后再点击编辑");
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.SelectedRows[0];
				string field = dataGridViewRow.Cells[0].Value.ToString();
				OperateResult<string> operateResult = redisClient.Redis.ReadHashKey(stringKeyName, field);
				if (operateResult.IsSuccess)
				{
					dataGridViewRow.Cells[1].Value = operateResult.Content;
					valueControl1.SetValue(operateResult.Content);
				}
				else
				{
					DemoUtils.ShowMessage("数据读取失败！" + operateResult.Message);
				}
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			DemoUtils.ShowMessage("当前版本暂时不支持！");
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
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.valueControl1 = new HslRedisDesktop.ValueControl();
			this.keyOperateControl1 = new HslRedisDesktop.KeyOperateControl();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer1.Location = new System.Drawing.Point(3, 37);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			this.splitContainer1.Panel1.Controls.Add(this.label3);
			this.splitContainer1.Panel1.Controls.Add(this.label4);
			this.splitContainer1.Panel1.Controls.Add(this.label2);
			this.splitContainer1.Panel1.Controls.Add(this.label1);
			this.splitContainer1.Panel1.Controls.Add(this.button5);
			this.splitContainer1.Panel1.Controls.Add(this.textBox1);
			this.splitContainer1.Panel1.Controls.Add(this.button4);
			this.splitContainer1.Panel1.Controls.Add(this.button3);
			this.splitContainer1.Panel1.Controls.Add(this.button2);
			this.splitContainer1.Panel1.Controls.Add(this.button1);
			this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
			this.splitContainer1.Panel2.Controls.Add(this.valueControl1);
			this.splitContainer1.Size = new System.Drawing.Size(841, 573);
			this.splitContainer1.SplitterDistance = 259;
			this.splitContainer1.TabIndex = 1;
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.Color.Silver;
			this.label3.Location = new System.Drawing.Point(272, 6);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(15, 17);
			this.label3.TabIndex = 10;
			this.label3.Text = "0";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(205, 6);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 9;
			this.label4.Text = "数组长度：";
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Silver;
			this.label2.Location = new System.Drawing.Point(59, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(71, 17);
			this.label2.TabIndex = 8;
			this.label2.Text = "大小：0 KB";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 7;
			this.label1.Text = "总大小：";
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(738, 231);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(100, 25);
			this.button5.TabIndex = 6;
			this.button5.Text = "定时刷新域";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(738, 202);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(100, 23);
			this.textBox1.TabIndex = 5;
			this.textBox1.Text = "1000";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(738, 119);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(100, 25);
			this.button4.TabIndex = 4;
			this.button4.Text = "刷新域";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(738, 88);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(100, 25);
			this.button3.TabIndex = 3;
			this.button3.Text = "删除域";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Location = new System.Drawing.Point(738, 57);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(100, 25);
			this.button2.TabIndex = 2;
			this.button2.Text = "编辑域";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(738, 26);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(100, 25);
			this.button1.TabIndex = 1;
			this.button1.Text = "插入域";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(192, 192, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2);
			this.dataGridView1.Location = new System.Drawing.Point(3, 26);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(729, 230);
			this.dataGridView1.TabIndex = 0;
			this.Column1.HeaderText = "Field";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 200;
			this.Column2.HeaderText = "Value";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 400;
			this.valueControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.valueControl1.BackColor = System.Drawing.Color.White;
			this.valueControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.valueControl1.Location = new System.Drawing.Point(3, 3);
			this.valueControl1.Name = "valueControl1";
			this.valueControl1.Size = new System.Drawing.Size(835, 307);
			this.valueControl1.TabIndex = 0;
			this.keyOperateControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.keyOperateControl1.BackColor = System.Drawing.Color.White;
			this.keyOperateControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.keyOperateControl1.KeyType = "Hash:";
			this.keyOperateControl1.Location = new System.Drawing.Point(0, 3);
			this.keyOperateControl1.Name = "keyOperateControl1";
			this.keyOperateControl1.Size = new System.Drawing.Size(847, 28);
			this.keyOperateControl1.TabIndex = 2;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.keyOperateControl1);
			base.Controls.Add(this.splitContainer1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "HashValueControl";
			base.Size = new System.Drawing.Size(847, 610);
			base.Load += new System.EventHandler(ListValueControl_Load);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel1.PerformLayout();
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
