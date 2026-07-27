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
	public class SetValueControl : UserControl
	{
		private string stringKeyName = string.Empty;

		private RedisSettings redisClient = null;

		private IContainer components = null;

		private SplitContainer splitContainer1;

		private Button button3;

		private Button button1;

		private DataGridView dataGridView1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private ValueControl valueControl1;

		private Label label3;

		private Label label4;

		private Label label2;

		private Label label1;

		private KeyOperateControl keyOperateControl1;

		public SetValueControl()
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
			dataGridView1.Columns[1].Width = dataGridView1.Width - 161;
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
			OperateResult<string[]> operateResult = redisClient.Redis.SetMembers(stringKeyName);
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
			label3.Text = operateResult.Content.Length.ToString();
			Utils.ControlDataGridViewRow(dataGridView1, operateResult.Content.Length);
			for (int j = 0; j < operateResult.Content.Length; j++)
			{
				dataGridView1.Rows[j].Cells[0].Value = j;
				dataGridView1.Rows[j].Cells[1].Value = operateResult.Content[j];
			}
		}

		private void DataGridView1_SelectionChanged(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0 && dataGridView1.SelectedRows[0].Cells[1].Value != null)
			{
				string value = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
				valueControl1.SetValue(value);
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

		private void button1_Click(object sender, EventArgs e)
		{
			using (SetValueInsertForm setValueInsertForm = new SetValueInsertForm(redisClient, stringKeyName))
			{
				setValueInsertForm.ShowDialog();
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (dataGridView1.SelectedRows.Count > 0 && dataGridView1.SelectedRows[0].Cells[1].Value != null && DemoUtils.ShowMessage("确认是否真的删除Key:" + stringKeyName + " 元素:" + dataGridView1.SelectedRows[0].Cells[1].Value.ToString() + " 的数据信息？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				OperateResult operateResult = redisClient.Redis.SetRemove(stringKeyName, dataGridView1.SelectedRows[0].Cells[1].Value.ToString());
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("删除集合元素成功！");
				}
				else
				{
					DemoUtils.ShowMessage("删除集合元素失败！" + operateResult.Message);
				}
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
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
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
			this.splitContainer1.Panel1.Controls.Add(this.button3);
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
			this.label4.Text = "总计个数：";
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
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(738, 57);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(100, 25);
			this.button3.TabIndex = 3;
			this.button3.Text = "删除元素";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(738, 26);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(100, 25);
			this.button1.TabIndex = 1;
			this.button1.Text = "插入元素";
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
			this.Column1.HeaderText = "Index";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column2.HeaderText = "Value";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 500;
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
			this.keyOperateControl1.KeyType = "Set:";
			this.keyOperateControl1.Location = new System.Drawing.Point(0, 3);
			this.keyOperateControl1.Name = "keyOperateControl1";
			this.keyOperateControl1.Size = new System.Drawing.Size(847, 28);
			this.keyOperateControl1.TabIndex = 2;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.keyOperateControl1);
			base.Controls.Add(this.splitContainer1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "SetValueControl";
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
