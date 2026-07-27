using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Enthernet;

namespace HslCommunicationDemo
{
	public class FormDisclaimer : Form
	{
		private long countOld = 1L;

		private Dictionary<string, long> loginData = new Dictionary<string, long>();

		private SimpleHybirdLock hybirdLock = new SimpleHybirdLock();

		private IContainer components = null;

		private Label label1;

		private DataGridView dataGridView1;

		private DataGridView dataGridView2;

		private DataGridView dataGridView3;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private DataGridViewTextBoxColumn Column5;

		private DataGridViewTextBoxColumn Column6;

		public FormDisclaimer()
		{
			InitializeComponent();
		}

		private void FormDisclaimer_Load(object sender, EventArgs e)
		{
			label1.BackColor = FormLoad.ThemeColor;
		}

		private void FormDisclaimer_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(GetDataFromServer, null);
		}

		private void AddDict(string address)
		{
			if (!string.IsNullOrEmpty(address))
			{
				if (address.IndexOf(' ') > 0)
				{
					address = address.Substring(0, address.IndexOf(' '));
				}
				hybirdLock.Enter();
				if (loginData.ContainsKey(address))
				{
					loginData[address]++;
				}
				else
				{
					loginData.Add(address, 1L);
				}
				hybirdLock.Leave();
				countOld++;
			}
		}

		private void GetDataFromServer(object obj)
		{
			OperateResult<NetHandle, string> operateResult = new NetSimplifyClient("118.24.36.220", 18467).ReadCustomerFromServer(1000, SoftBasic.FrameworkVersion.ToString());
			if (!operateResult.IsSuccess)
			{
				return;
			}
			MemoryStream memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(operateResult.Content2));
			StreamReader streamReader = new StreamReader(memoryStream, Encoding.Unicode);
			hybirdLock.Enter();
			loginData.Clear();
			while (true)
			{
				string text = streamReader.ReadLine();
				if (text == null)
				{
					break;
				}
				string s = streamReader.ReadLine();
				loginData.Add(text, long.Parse(s));
			}
			streamReader.Close();
			hybirdLock.Leave();
			memoryStream.Close();
			List<dataMy> list = new List<dataMy>();
			hybirdLock.Enter();
			foreach (KeyValuePair<string, long> loginDatum in loginData)
			{
				list.Add(new dataMy(loginDatum.Key, loginDatum.Value));
			}
			hybirdLock.Leave();
			list.Sort();
			list.Reverse();
			List<dataMy> shengfen = new List<dataMy>();
			List<dataMy> others = new List<dataMy>();
			for (int i = 0; i < list.Count; i++)
			{
				string tmp = string.Empty;
				if (list[i].Key.IndexOf('省') > 0)
				{
					tmp = list[i].Key.Substring(0, list[i].Key.IndexOf('省') + 1);
				}
				else if (list[i].Key.Contains("北京市"))
				{
					tmp = "北京市";
				}
				else if (list[i].Key.Contains("上海市"))
				{
					tmp = "上海市";
				}
				else if (list[i].Key.Contains("天津市"))
				{
					tmp = "天津市";
				}
				else if (list[i].Key.Contains("重庆市"))
				{
					tmp = "重庆市";
				}
				else
				{
					if (list[i].Key.IndexOf('区') <= 0)
					{
						tmp = list[i].Key;
						others.Add(new dataMy(tmp, list[i].Value));
						continue;
					}
					tmp = list[i].Key.Substring(0, list[i].Key.IndexOf('区') + 1);
				}
				if (!string.IsNullOrEmpty(tmp))
				{
					dataMy dataMy2 = shengfen.Find((dataMy m) => m.Key == tmp);
					if (dataMy2 == null)
					{
						shengfen.Add(new dataMy(tmp, list[i].Value));
					}
					else
					{
						dataMy2.Value += list[i].Value;
					}
				}
			}
			Invoke((Action)delegate
			{
				shengfen.Sort();
				shengfen.Reverse();
				RenderDataTable(dataGridView1, shengfen);
				others.Sort();
				others.Reverse();
				RenderDataTable(dataGridView3, others);
			});
		}

		private long RenderDataTable(DataGridView dataGridView, List<dataMy> datas)
		{
			long num = 0L;
			while (dataGridView.RowCount < datas.Count)
			{
				dataGridView.Rows.Add();
			}
			while (dataGridView.RowCount > datas.Count)
			{
				dataGridView.Rows.RemoveAt(0);
			}
			for (int i = 0; i < datas.Count; i++)
			{
				dataGridView.Rows[i].Cells[0].Value = datas[i].Key;
				dataGridView.Rows[i].Cells[1].Value = datas[i].Value.ToString();
				num += datas[i].Value;
			}
			return num;
		}

		private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
		{
			int rowIndex = e.RowIndex;
			if (rowIndex < 0)
			{
				return;
			}
			string value = dataGridView1.Rows[rowIndex].Cells[0].Value.ToString();
			List<dataMy> list = new List<dataMy>();
			hybirdLock.Enter();
			foreach (KeyValuePair<string, long> loginDatum in loginData)
			{
				if (loginDatum.Key.Contains(value))
				{
					list.Add(new dataMy(loginDatum.Key, loginDatum.Value));
				}
			}
			hybirdLock.Leave();
			list.Sort();
			list.Reverse();
			RenderDataTable(dataGridView2, list);
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
			this.label1 = new System.Windows.Forms.Label();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.dataGridView2 = new System.Windows.Forms.DataGridView();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.dataGridView3 = new System.Windows.Forms.DataGridView();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.dataGridView2).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.dataGridView3).BeginInit();
			base.SuspendLayout();
			this.label1.Dock = System.Windows.Forms.DockStyle.Top;
			this.label1.Location = new System.Drawing.Point(0, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(907, 50);
			this.label1.TabIndex = 0;
			this.label1.Text = "本程序在开启时，会进行版本更新检测，服务器会搜集客户端的ip地址信息，仅用于统计分析各个城市的Demo使用情况，仅仅公开统计信息，如果您不同意该声明，请立即关闭程序，并不再运行，一旦使用则视为同意上述的声明，谢谢合作。";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2);
			this.dataGridView1.Location = new System.Drawing.Point(32, 68);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(263, 432);
			this.dataGridView1.TabIndex = 1;
			this.dataGridView1.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(dataGridView1_CellClick);
			this.Column1.HeaderText = "省份(点击详细)";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 120;
			this.Column2.HeaderText = "访问";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 75;
			this.dataGridView2.AllowUserToAddRows = false;
			this.dataGridView2.AllowUserToDeleteRows = false;
			this.dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView2.Columns.AddRange(this.Column3, this.Column4);
			this.dataGridView2.Location = new System.Drawing.Point(299, 68);
			this.dataGridView2.Name = "dataGridView2";
			this.dataGridView2.ReadOnly = true;
			this.dataGridView2.RowTemplate.Height = 23;
			this.dataGridView2.Size = new System.Drawing.Size(287, 432);
			this.dataGridView2.TabIndex = 2;
			this.Column3.HeaderText = "城市";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.Width = 150;
			this.Column4.HeaderText = "数量";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Width = 75;
			this.dataGridView3.AllowUserToAddRows = false;
			this.dataGridView3.AllowUserToDeleteRows = false;
			this.dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView3.Columns.AddRange(this.Column5, this.Column6);
			this.dataGridView3.Location = new System.Drawing.Point(659, 68);
			this.dataGridView3.Name = "dataGridView3";
			this.dataGridView3.ReadOnly = true;
			this.dataGridView3.RowTemplate.Height = 23;
			this.dataGridView3.Size = new System.Drawing.Size(236, 432);
			this.dataGridView3.TabIndex = 3;
			this.Column5.HeaderText = "其他区域";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column6.HeaderText = "数量";
			this.Column6.Name = "Column6";
			this.Column6.ReadOnly = true;
			this.Column6.Width = 75;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(907, 512);
			base.Controls.Add(this.dataGridView3);
			base.Controls.Add(this.dataGridView2);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormDisclaimer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "免责声明";
			base.Load += new System.EventHandler(FormDisclaimer_Load);
			base.Shown += new System.EventHandler(FormDisclaimer_Shown);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			((System.ComponentModel.ISupportInitialize)this.dataGridView2).EndInit();
			((System.ComponentModel.ISupportInitialize)this.dataGridView3).EndInit();
			base.ResumeLayout(false);
		}
	}
}
