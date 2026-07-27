using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HslCommunication.MQTT;

namespace HslCommunicationDemo.Vip
{
	public class CertificateListControl : UserControl
	{
		private List<CertificateItem> certificateItems = new List<CertificateItem>();

		private MqttSyncClient rpc;

		private IContainer components = null;

		private DataGridView dataGridView1;

		private TextBox textBox1;

		private Button button1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private DataGridViewTextBoxColumn Column5;

		private DataGridViewTextBoxColumn Column6;

		private Label label1;

		private Label label2;

		private Label label3;

		private Label label4;

		private Label label5;

		public CertificateListControl()
		{
			InitializeComponent();
		}

		private void RenderCertificateListHelper(List<CertificateItem> certificates)
		{
			if (certificates == null)
			{
				DemoUtils.DataGridSpecifyRowCount(dataGridView1, 0);
				return;
			}
			DemoUtils.DataGridSpecifyRowCount(dataGridView1, certificates.Count);
			for (int i = 0; i < certificates.Count; i++)
			{
				CertificateItem certificateItem = certificates[i];
				dataGridView1.Rows[i].Cells[0].Value = certificateItem.Certificate.To;
				dataGridView1.Rows[i].Cells[1].Value = certificateItem.Certificate.NotBefore.ToString("yyyy-MM-dd");
				dataGridView1.Rows[i].Cells[2].Value = certificateItem.Certificate.NotAfter.ToString("yyyy-MM-dd");
				dataGridView1.Rows[i].Cells[3].Value = certificateItem.Certificate.CreateTime.ToString("yyyy-MM-dd");
				dataGridView1.Rows[i].Cells[4].Value = certificateItem.Certificate.EffectiveHours;
				dataGridView1.Rows[i].Cells[5].Value = certificateItem.Certificate.UniqueID;
				if (certificateItem.Certificate.NotAfter < DateTime.Now)
				{
					dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Tomato;
				}
				else if ((certificateItem.Certificate.NotAfter - DateTime.Now).TotalDays <= 7.0)
				{
					dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Pink;
				}
				else if ((certificateItem.Certificate.NotAfter - DateTime.Now).TotalDays <= 30.0)
				{
					dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.LightSkyBlue;
				}
				else
				{
					dataGridView1.Rows[i].DefaultCellStyle.BackColor = dataGridView1.RowsDefaultCellStyle.BackColor;
				}
				dataGridView1.Rows[i].Tag = certificateItem;
			}
		}

		private void StatisticsCertificate(List<CertificateItem> certificates)
		{
			if (certificates == null)
			{
				return;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			DateTime now = DateTime.Now;
			foreach (CertificateItem certificate in certificates)
			{
				num++;
				if (certificate.Certificate.NotAfter < now)
				{
					num2++;
					continue;
				}
				if ((certificate.Certificate.NotAfter - now).TotalDays <= 7.0)
				{
					num3++;
				}
				if ((certificate.Certificate.NotAfter - now).TotalDays <= 30.0)
				{
					num4++;
				}
			}
			label1.Text = "总计: " + num;
			label2.Text = "过期: " + num2;
			label3.Text = "7天内过期: " + num3;
			label4.Text = "30天内过期: " + num4;
		}

		public void SetCertificates(MqttSyncClient rpc, List<CertificateItem> certificates)
		{
			this.rpc = rpc;
			certificateItems = certificates;
			RenderCertificateListHelper(certificateItems);
			StatisticsCertificate(certificates);
		}

		private void label1_Click(object sender, EventArgs e)
		{
			RenderCertificateListHelper(certificateItems);
		}

		private void label2_Click(object sender, EventArgs e)
		{
			RenderCertificateListHelper(certificateItems.Where((CertificateItem m) => m.Certificate.NotAfter < DateTime.Now).ToList());
		}

		private void label3_Click(object sender, EventArgs e)
		{
			RenderCertificateListHelper(certificateItems.Where((CertificateItem m) => m.Certificate.NotAfter < DateTime.Now.AddDays(7.0)).ToList());
		}

		private void label4_Click(object sender, EventArgs e)
		{
			RenderCertificateListHelper(certificateItems.Where((CertificateItem m) => m.Certificate.NotAfter < DateTime.Now.AddDays(30.0)).ToList());
		}

		private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex >= 0 && dataGridView1.Rows[e.RowIndex].Tag is CertificateItem item)
			{
				FormHslCertificate formHslCertificate = new FormHslCertificate(rpc, item);
				formHslCertificate.StartPosition = FormStartPosition.CenterParent;
				formHslCertificate.ShowDialog();
			}
		}

		private void CertificateListControl_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				dataGridView1.Columns[0].HeaderText = "To";
				dataGridView1.Columns[1].HeaderText = "Not Before";
				dataGridView1.Columns[2].HeaderText = "Not After";
				dataGridView1.Columns[3].HeaderText = "Create Time";
				dataGridView1.Columns[4].HeaderText = "Effective Hours";
				dataGridView1.Columns[5].HeaderText = "Unique ID";
				label5.Text = "Double click to view";
				button1.Text = "Search";
			}
		}

		private bool SearchCertificate(CertificateItem item, string keyword)
		{
			if (string.IsNullOrEmpty(keyword))
			{
				return false;
			}
			return item.Certificate.To.Contains(keyword) || item.Certificate.From.Contains(keyword) || item.Certificate.UniqueID.Contains(keyword);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			RenderCertificateListHelper(certificateItems.Where((CertificateItem m) => SearchCertificate(m, textBox1.Text)).ToList());
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
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(223, 223, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column3, this.Column4, this.Column5, this.Column6);
			this.dataGridView1.Location = new System.Drawing.Point(3, 3);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(795, 408);
			this.dataGridView1.TabIndex = 0;
			this.dataGridView1.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView1_CellMouseDoubleClick);
			this.Column1.HeaderText = "持有者";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 150;
			this.Column2.HeaderText = "开始日期";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column3.HeaderText = "结束日期";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column4.HeaderText = "生成日期";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column5.HeaderText = "有效小时数";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column6.HeaderText = "唯一编号";
			this.Column6.Name = "Column6";
			this.Column6.ReadOnly = true;
			this.Column6.Width = 150;
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(575, 416);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(139, 23);
			this.textBox1.TabIndex = 1;
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(720, 414);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(78, 27);
			this.button1.TabIndex = 2;
			this.button1.Text = "查找";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label1.AutoSize = true;
			this.label1.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label1.Location = new System.Drawing.Point(3, 419);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(35, 17);
			this.label1.TabIndex = 3;
			this.label1.Text = "总计:";
			this.label1.Click += new System.EventHandler(label1_Click);
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label2.Location = new System.Drawing.Point(74, 419);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 17);
			this.label2.TabIndex = 4;
			this.label2.Text = "过期:";
			this.label2.Click += new System.EventHandler(label2_Click);
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label3.AutoSize = true;
			this.label3.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label3.Location = new System.Drawing.Point(154, 419);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(66, 17);
			this.label3.TabIndex = 5;
			this.label3.Text = "7天内过期:";
			this.label3.Click += new System.EventHandler(label3_Click);
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label4.Location = new System.Drawing.Point(269, 419);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(73, 17);
			this.label4.TabIndex = 6;
			this.label4.Text = "30天内过期:";
			this.label4.Click += new System.EventHandler(label4_Click);
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label5.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label5.ForeColor = System.Drawing.Color.Gray;
			this.label5.Location = new System.Drawing.Point(427, 419);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(142, 20);
			this.label5.TabIndex = 7;
			this.label5.Text = "(双击查看证书)";
			this.label5.TextAlign = System.Drawing.ContentAlignment.TopRight;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label5);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.dataGridView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "CertificateListControl";
			base.Size = new System.Drawing.Size(801, 444);
			base.Load += new System.EventHandler(CertificateListControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
