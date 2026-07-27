using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.MQTT;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo.DemoControl
{
	public class FormDeviceSupport : Form
	{
		private string formName = string.Empty;

		private IContainer components = null;

		private Label label1;

		private DataGridView dataGridView1;

		private Label label2;

		private Label label3;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label4;

		private Button button1;

		private TextBox textBox3;

		private Label label5;

		private DataGridViewTextBoxColumn Model;

		private DataGridViewTextBoxColumn ColumnName;

		private DataGridViewTextBoxColumn QQ;

		private DataGridViewTextBoxColumn ColumnTime;

		public FormDeviceSupport(string formName)
		{
			InitializeComponent();
			this.formName = formName;
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox1.Text))
			{
				DemoUtils.ShowMessage("Model can't be null!");
				return;
			}
			button1.Enabled = false;
			OperateResult<List<DeviceSupportList>> read = await new MqttSyncClient(new MqttConnectionOptions
			{
				IpAddress = "118.24.36.220",
				Port = 1883,
				UseRSAProvider = true
			}).ReadRpcAsync<List<DeviceSupportList>>("SupportList/UploadSupport", new
			{
				token = string.Empty,
				unique = formName,
				model = textBox1.Text,
				qq = textBox2.Text,
				name = textBox3.Text
			});
			if (read.IsSuccess)
			{
				DemoUtils.ShowMessage("Upload data success");
				ThreadPool.QueueUserWorkItem(ThreadPoolCheckVersion, null);
			}
			else
			{
				DemoUtils.ShowMessage("Request Server failed: " + read.Message);
			}
			button1.Enabled = true;
		}

		private void FormDeviceSupport_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(ThreadPoolCheckVersion, null);
		}

		private async void ThreadPoolCheckVersion(object obj)
		{
			Thread.Sleep(100);
			OperateResult<string> read = await new MqttSyncClient(new MqttConnectionOptions
			{
				IpAddress = "118.24.36.220",
				Port = 1883,
				UseRSAProvider = true
			}).ReadRpcAsync<string>("SupportList/GetDeviceSupport", new
			{
				token = string.Empty,
				unique = formName
			});
			if (read.IsSuccess)
			{
				if (!string.IsNullOrEmpty(read.Content))
				{
					List<DeviceSupportList> devices = JArray.Parse(read.Content).ToObject<List<DeviceSupportList>>();
					Invoke(new Action<List<DeviceSupportList>>(RenderDevice), devices);
				}
			}
			else
			{
				DemoUtils.ShowMessage("Request Server failed: " + read.Message);
			}
		}

		private void RenderDevice(List<DeviceSupportList> devices)
		{
			if (devices == null)
			{
				DataGridSpecifyRowCount(dataGridView1, 0);
				return;
			}
			DataGridSpecifyRowCount(dataGridView1, devices.Count);
			for (int i = 0; i < devices.Count; i++)
			{
				dataGridView1.Rows[i].Cells[0].Value = devices[i].Model;
				dataGridView1.Rows[i].Cells[1].Value = devices[i].Name;
				dataGridView1.Rows[i].Cells[2].Value = devices[i].QQ;
				dataGridView1.Rows[i].Cells[3].Value = devices[i].Time.ToString("yyyy-MM-dd");
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

		private void FormDeviceSupport_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
				dataGridView1.Columns[0].HeaderText = "系列及型号";
				dataGridView1.Columns[1].HeaderText = "上传者";
				dataGridView1.Columns[2].HeaderText = "联系方式（QQ）";
				dataGridView1.Columns[3].HeaderText = "上传时间";
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
			this.label1 = new System.Windows.Forms.Label();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Model = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.QQ = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.ColumnTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(116, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "支持的设备列表信息";
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(210, 210, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Model, this.ColumnName, this.QQ, this.ColumnTime);
			this.dataGridView1.Location = new System.Drawing.Point(15, 39);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(711, 457);
			this.dataGridView1.TabIndex = 1;
			this.Model.HeaderText = "Model";
			this.Model.Name = "Model";
			this.Model.ReadOnly = true;
			this.Model.Width = 260;
			this.ColumnName.HeaderText = "Name";
			this.ColumnName.Name = "ColumnName";
			this.ColumnName.ReadOnly = true;
			this.ColumnName.Width = 160;
			this.QQ.HeaderText = "Contact: QQ";
			this.QQ.Name = "QQ";
			this.QQ.ReadOnly = true;
			this.QQ.Width = 150;
			this.ColumnTime.HeaderText = "Date";
			this.ColumnTime.Name = "ColumnTime";
			this.ColumnTime.ReadOnly = true;
			this.ColumnTime.Width = 120;
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(733, 9);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(104, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "上传新的设备信息";
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(733, 34);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 3;
			this.label3.Text = "型号：";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(736, 54);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(216, 23);
			this.textBox1.TabIndex = 4;
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox2.Location = new System.Drawing.Point(736, 159);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(216, 23);
			this.textBox2.TabIndex = 6;
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(733, 134);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(96, 17);
			this.label4.TabIndex = 5;
			this.label4.Text = "QQ(联系方式)：";
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(787, 225);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(107, 35);
			this.button1.TabIndex = 7;
			this.button1.Text = "上传";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(736, 105);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(216, 23);
			this.textBox3.TabIndex = 9;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(733, 84);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(80, 17);
			this.label5.TabIndex = 8;
			this.label5.Text = "上传人昵称：";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(958, 510);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormDeviceSupport";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormDeviceSupport";
			base.Load += new System.EventHandler(FormDeviceSupport_Load);
			base.Shown += new System.EventHandler(FormDeviceSupport_Shown);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
