using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Siemens;
using HslCommunication.Profinet.Siemens.S7PlusHelper;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class SiemensS7PlusControl : UserControl
	{
		private ImageList imageList;

		private Dictionary<uint, S7ObjectNode> s7Nodes = new Dictionary<uint, S7ObjectNode>();

		private Dictionary<uint, S7Object> s7Struct = new Dictionary<uint, S7Object>();

		private SiemensS7Plus siemensTcpNet;

		private Thread threadRead;

		private bool threadEnable = false;

		private int timeSleep = 1000;

		private IContainer components = null;

		private Button button1;

		private TreeView treeView1;

		private DataGridView dataGridView1;

		private Button button2;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column5;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private Label label1;

		private TextBox textBox_time;

		public SiemensS7PlusControl()
		{
			InitializeComponent();
			imageList = new ImageList();
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("Module_648", Resources.Module_648);
			treeView1.ImageList = imageList;
		}

		private void textBox1_TextChanged(object sender, EventArgs e)
		{
		}

		private void SiemensS7PlusControl_Load(object sender, EventArgs e)
		{
		}

		public void SetDevice(SiemensS7Plus siemensTcpNet, string address)
		{
			this.siemensTcpNet = siemensTcpNet;
		}

		private void RenderDbBlock(TreeNodeCollection nodes, List<S7Object> s7Object)
		{
			for (int i = 0; i < s7Object.Count; i++)
			{
				TreeNode treeNode = new TreeNode(s7Object[i].Name);
				treeNode.ImageKey = "Class_489";
				treeNode.SelectedImageKey = "Class_489";
				List<S7Object> subObjects = s7Object[i].SubObjects;
				if (subObjects != null && subObjects.Count > 0)
				{
					RenderDbBlock(treeNode.Nodes, s7Object[i].SubObjects);
				}
				treeNode.Tag = s7Object[i];
				nodes.Add(treeNode);
				if (s7Object[i].RelationId2 != 0 && !s7Nodes.ContainsKey(s7Object[i].RelationId2))
				{
					s7Nodes.Add(s7Object[i].RelationId2, new S7ObjectNode
					{
						S7Object = s7Object[i],
						Node = treeNode
					});
				}
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			OperateResult<List<S7Object>> operateResult = siemensTcpNet.BrowerDB();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
				return;
			}
			treeView1.Nodes.Clear();
			s7Nodes.Clear();
			RenderDbBlock(treeView1.Nodes, operateResult.Content);
			dataGridView1.Columns[0].HeaderText = "Name";
			dataGridView1.Columns[1].HeaderText = "RelationId";
			dataGridView1.Columns[2].HeaderText = "Vid";
			dataGridView1.Columns[3].HeaderText = "ClassId";
			dataGridView1.Columns[4].HeaderText = "Value";
			OperateResult<List<S7Object>> operateResult2 = siemensTcpNet.BrowseTags();
			if (operateResult2.IsSuccess)
			{
				foreach (S7ObjectNode value in s7Nodes.Values)
				{
					value.Node.Nodes.Clear();
				}
				BrowerAllTags(operateResult2.Content);
				DemoUtils.ShowMessage("Finish!");
				treeView1.ExpandAll();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult2.Message);
			}
		}

		public void RenderS7Tags(List<S7Tag> s7Tags)
		{
			int num = s7Tags?.Count ?? 0;
			DemoUtils.DataGridSpecifyRowCount(dataGridView1, num);
			dataGridView1.Columns[0].HeaderText = "Name";
			dataGridView1.Columns[1].HeaderText = "RelationId";
			dataGridView1.Columns[2].HeaderText = "TypeCode";
			dataGridView1.Columns[3].HeaderText = "TypeText";
			dataGridView1.Columns[4].HeaderText = "Value";
			if (num > 0)
			{
				for (int i = 0; i < s7Tags.Count; i++)
				{
					S7Tag s7Tag = s7Tags[i];
					DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
					dataGridViewRow.Cells[0].Value = s7Tag.Name;
					dataGridViewRow.Cells[1].Value = s7Tag.GetLIDText();
					dataGridViewRow.Cells[2].Value = "0x" + s7Tag.TypeCode.ToString("X");
					dataGridViewRow.Cells[3].Value = s7Tag.GetTypeText();
					dataGridViewRow.Cells[4].Value = "";
					dataGridViewRow.Tag = s7Tag;
				}
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = e.Node;
			if (node == null)
			{
				return;
			}
			if (node.Tag is S7Object s7Object)
			{
				if (s7Object.RelationId2 >= 2147483648u)
				{
					RenderS7Tags(s7Object.S7Tags);
					return;
				}
				int num = ((s7Object.SubObjects != null) ? s7Object.SubObjects.Count : 0);
				DemoUtils.DataGridSpecifyRowCount(dataGridView1, num);
				dataGridView1.Columns[0].HeaderText = "Name";
				dataGridView1.Columns[1].HeaderText = "RelationId";
				dataGridView1.Columns[2].HeaderText = "Vid";
				dataGridView1.Columns[3].HeaderText = "ClassId";
				dataGridView1.Columns[4].HeaderText = "Value";
				if (num > 0)
				{
					for (int i = 0; i < s7Object.SubObjects.Count; i++)
					{
						S7Object s7Object2 = s7Object.SubObjects[i];
						DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
						dataGridViewRow.Cells[0].Value = s7Object2.Name;
						dataGridViewRow.Cells[1].Value = "0x" + s7Object2.RelationId.ToString("X");
						dataGridViewRow.Cells[2].Value = "0x" + s7Object2.RelationId2.ToString("X");
						dataGridViewRow.Cells[3].Value = s7Object2.ClassId.ToString();
						dataGridViewRow.Cells[4].Value = "";
					}
				}
			}
			else
			{
				if (!(node.Tag is S7Tag s7Tag) || s7Tag.TypeCode != 17 || s7Tag.ArrayLength >= 0)
				{
					return;
				}
				List<S7Tag> list = new List<S7Tag>();
				foreach (TreeNode node2 in node.Nodes)
				{
					if (node2.Tag is S7Tag item)
					{
						list.Add(item);
					}
				}
				RenderS7Tags(list);
			}
		}

		private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.Tag is S7Tag))
			{
			}
		}

		private void AddS7Tags(TreeNode parent, List<S7Tag> tags)
		{
			if (tags == null || tags.Count == 0)
			{
				return;
			}
			for (int i = 0; i < tags.Count; i++)
			{
				TreeNode treeNode = new TreeNode(tags[i].Name);
				treeNode.Tag = tags[i];
				if (tags[i].ArrayLength >= 0)
				{
					treeNode.ImageKey = "brackets_Square_16xMD";
					treeNode.SelectedImageKey = "brackets_Square_16xMD";
				}
				else if (tags[i].TypeCode == 17)
				{
					treeNode.ImageKey = "Module_648";
					treeNode.SelectedImageKey = "Module_648";
					if (s7Struct.ContainsKey(tags[i].StructID) && s7Struct[tags[i].StructID].S7Tags != null)
					{
						List<S7Tag> list = new List<S7Tag>();
						for (int j = 0; j < s7Struct[tags[i].StructID].S7Tags.Count; j++)
						{
							S7Tag s7Tag = s7Struct[tags[i].StructID].S7Tags[j].Clone();
							s7Tag.LID.InsertRange(0, tags[i].LID);
							list.Add(s7Tag);
						}
						AddS7Tags(treeNode, list);
					}
				}
				else
				{
					treeNode.ImageKey = "Enum_582";
					treeNode.SelectedImageKey = "Enum_582";
				}
				parent.Nodes.Add(treeNode);
			}
		}

		private void BrowerAllTags(List<S7Object> objects)
		{
			for (int i = 0; i < objects.Count; i++)
			{
				if (s7Nodes.ContainsKey(objects[i].RelationId))
				{
					S7Object s7Object = s7Nodes[objects[i].RelationId].S7Object;
					if (objects[i].S7Tags != null)
					{
						s7Object.S7Tags = new List<S7Tag>();
						for (int j = 0; j < objects[i].S7Tags.Count; j++)
						{
							S7Tag s7Tag = objects[i].S7Tags[j].Clone();
							s7Tag.LID.Insert(0, s7Object.RelationId);
							s7Object.S7Tags.Add(s7Tag);
						}
						AddS7Tags(s7Nodes[objects[i].RelationId].Node, s7Object.S7Tags);
					}
				}
				else if (s7Struct.ContainsKey(objects[i].RelationId))
				{
					s7Struct[objects[i].RelationId] = objects[i];
				}
				else
				{
					s7Struct.Add(objects[i].RelationId, objects[i]);
				}
				if (objects[i].SubObjects != null)
				{
					BrowerAllTags(objects[i].SubObjects);
				}
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (siemensTcpNet == null)
			{
				DemoUtils.ShowMessage("ReadWriteNet device is null, can not refresh! ");
			}
			else if (!threadEnable)
			{
				threadEnable = true;
				button2.Text = ((Program.Language == 1) ? "停止刷新" : "Stop");
				button2.BackColor = Color.LimeGreen;
				timeSleep = Convert.ToInt32(textBox_time.Text);
				threadRead = new Thread(ThreadBack);
				threadRead.IsBackground = true;
				threadRead.Start();
			}
			else
			{
				threadEnable = false;
				button2.Text = ((Program.Language == 1) ? "开始刷新" : "Refresh");
				button2.BackColor = SystemColors.Control;
			}
		}

		private void ThreadBack()
		{
			while (threadEnable)
			{
				if (dataGridView1.FirstDisplayedCell == null)
				{
					break;
				}
				int firstRow = dataGridView1.FirstDisplayedCell.RowIndex;
				int rowCount = dataGridView1.DisplayedRowCount(true);
				List<S7Tag> list = new List<S7Tag>();
				for (int i = 0; i < rowCount; i++)
				{
					DataGridViewRow dataGridViewRow = dataGridView1.Rows[i + firstRow];
					if (!dataGridViewRow.IsNewRow && dataGridViewRow.Tag is S7Tag item)
					{
						list.Add(item);
					}
				}
				if (list.Count > 0)
				{
					OperateResult<List<S7Value>> read = siemensTcpNet.ReadTags(list.ToArray());
					try
					{
						Invoke((Action)delegate
						{
							if (read.IsSuccess)
							{
								for (int j = 0; j < rowCount; j++)
								{
									DataGridViewRow dataGridViewRow2 = dataGridView1.Rows[j + firstRow];
									if (!dataGridViewRow2.IsNewRow && j < read.Content.Count)
									{
										if (read.Content[j].Value == null)
										{
											dataGridViewRow2.Cells[4].Value = read.Content[j].Buffer.ToHexString(' ');
										}
										else
										{
											dataGridViewRow2.Cells[4].Value = read.Content[j].Value.ToJsonString();
										}
									}
								}
							}
							else
							{
								for (int k = 0; k < rowCount; k++)
								{
									DataGridViewRow dataGridViewRow3 = dataGridView1.Rows[k + firstRow];
									if (!dataGridViewRow3.IsNewRow)
									{
										dataGridViewRow3.Cells[4].Value = "";
									}
								}
							}
						});
					}
					catch
					{
						break;
					}
				}
				Thread.Sleep(timeSleep);
			}
		}

		private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			DataGridViewRow dataGridViewRow = dataGridView1.Rows[e.RowIndex];
			if (!(dataGridViewRow.Tag is S7Tag s7Tag))
			{
				return;
			}
			OperateResult<List<S7Value>> operateResult = siemensTcpNet.ReadTags(new S7Tag[1] { s7Tag });
			if (operateResult.IsSuccess)
			{
				if (operateResult.Content.Count == 1)
				{
					dataGridViewRow.Cells[4].Value = operateResult.Content[0].Value.ToJsonString();
					return;
				}
				dataGridViewRow.Cells[4].Value = operateResult.Content.Select((S7Value m) => m.Value).ToArray().ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.Message);
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
			this.button1 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.button2 = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_time = new System.Windows.Forms.TextBox();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.button1.Location = new System.Drawing.Point(3, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(95, 29);
			this.button1.TabIndex = 0;
			this.button1.Text = "浏览DB";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(3, 36);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(264, 282);
			this.treeView1.TabIndex = 2;
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.treeView1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(treeView1_MouseDoubleClick);
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(222, 222, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column5, this.Column3, this.Column4);
			this.dataGridView1.Location = new System.Drawing.Point(273, 36);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
			this.dataGridView1.Size = new System.Drawing.Size(656, 282);
			this.dataGridView1.TabIndex = 3;
			this.dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(dataGridView1_CellDoubleClick);
			this.Column1.HeaderText = "Name";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 120;
			this.Column2.HeaderText = "RelationId";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 150;
			this.Column5.HeaderText = "Vid";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column5.Width = 90;
			this.Column3.HeaderText = "ClassId";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column4.HeaderText = "Value";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Width = 180;
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Location = new System.Drawing.Point(826, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(103, 29);
			this.button2.TabIndex = 4;
			this.button2.Text = "点击刷新值";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(666, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 6;
			this.label1.Text = "间隔时间:";
			this.textBox_time.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_time.Location = new System.Drawing.Point(731, 6);
			this.textBox_time.Name = "textBox_time";
			this.textBox_time.Size = new System.Drawing.Size(84, 23);
			this.textBox_time.TabIndex = 5;
			this.textBox_time.Text = "1000";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox_time);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.dataGridView1);
			base.Controls.Add(this.treeView1);
			base.Controls.Add(this.button1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "SiemensS7PlusControl";
			base.Size = new System.Drawing.Size(932, 321);
			base.Load += new System.EventHandler(SiemensS7PlusControl_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
