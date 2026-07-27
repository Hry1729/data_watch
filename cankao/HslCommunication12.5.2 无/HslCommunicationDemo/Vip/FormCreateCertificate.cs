using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core.Security;
using HslCommunication.MQTT;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.Vip
{
	public class FormCreateCertificate : HslFormContent
	{
		private MqttSyncClient rpc = null;

		private string company = string.Empty;

		private CertificateListControl certificateListControl = null;

		private HslCertificateControl certificateControl = null;

		private IContainer components = null;

		private TreeView treeView1;

		private Label label1;

		private Button button_refresh_cert;

		private Panel panel1;

		private Button button_new;

		private SplitContainer splitContainer1;

		private Panel panel2;

		private Panel panel3;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem deleteToolStripMenuItem;

		public FormCreateCertificate(ImageList imageList, MqttSyncClient rpc, string company)
		{
			this.rpc = rpc;
			this.company = company;
			InitializeComponent();
			certificateListControl = new CertificateListControl();
			certificateListControl.Parent = panel1;
			certificateListControl.Dock = DockStyle.Fill;
			certificateListControl.Visible = true;
			certificateControl = new HslCertificateControl();
			certificateControl.Parent = panel1;
			certificateControl.Dock = DockStyle.Fill;
			certificateControl.SetReadOnly(true);
			certificateControl.Visible = false;
			certificateControl.SetRpcClient(rpc);
			treeView1.ImageList = imageList;
			deleteToolStripMenuItem.Click += DeleteToolStripMenuItem_Click;
		}

		private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is CertificateItem certificateItem && selectedNode.Parent != null && MessageBox.Show("Are you sure to delete [" + certificateItem.Certificate.To + "] certificate?", "Delete Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				string topic = (selectedNode.Parent.Text.Contains("DotNet") ? "DeleteDotNetCertificate" : "DeleteJavaCertificate");
				OperateResult operateResult = rpc.ReadRpc<string>(topic, new
				{
					userName = certificateItem.Certificate.To
				});
				if (operateResult.IsSuccess)
				{
					MessageBox.Show("Delete Success!");
					treeView1.Nodes.Remove(selectedNode);
				}
				else
				{
					MessageBox.Show("Delete Failed: " + operateResult.Message);
				}
			}
		}

		private void FormCreateCertificate_Load(object sender, EventArgs e)
		{
			treeView1.Nodes[0].ImageKey = "CSharpFile_SolutionExplorerNode";
			treeView1.Nodes[0].SelectedImageKey = "CSharpFile_SolutionExplorerNode";
			treeView1.Nodes[1].ImageKey = "java";
			treeView1.Nodes[1].SelectedImageKey = "java";
			if (Program.Language == 2)
			{
				button_refresh_cert.Text = "Refresh";
				button_new.Text = "New";
			}
		}

		private void RefreshCertificate()
		{
			OperateResult<List<byte[]>> read = rpc.ReadRpc<List<byte[]>>("GetAllCertificate", "");
			Invoke((Action)delegate
			{
				if (!read.IsSuccess)
				{
					MessageBox.Show("Read Certificate Failed: " + read.Message);
				}
				else
				{
					treeView1.Nodes[0].Nodes.Clear();
					treeView1.Nodes[1].Nodes.Clear();
					List<CertificateItem> list = new List<CertificateItem>();
					for (int i = 0; i < read.Content.Count; i++)
					{
						try
						{
							HslCertificate hslCertificate = new HslCertificate();
							hslCertificate.LoadFrom(read.Content[i]);
							list.Add(new CertificateItem
							{
								Name = hslCertificate.To + " (" + hslCertificate.NotAfter.ToString("yyyy-MM-dd") + ")",
								Certificate = hslCertificate,
								CertBinary = read.Content[i]
							});
						}
						catch
						{
						}
					}
					list.Sort((CertificateItem a, CertificateItem b) => a.Certificate.To.CompareTo(b.Certificate.To));
					for (int j = 0; j < list.Count; j++)
					{
						try
						{
							HslCertificate certificate = list[j].Certificate;
							TreeNode node = new TreeNode(list[j].Name)
							{
								ImageKey = "Certificate",
								SelectedImageKey = "Certificate",
								Tag = list[j]
							};
							if (certificate.Descriptions.ContainsKey("语言"))
							{
								if (certificate.Descriptions["语言"].Equals("DotNet", StringComparison.OrdinalIgnoreCase))
								{
									treeView1.Nodes[0].Nodes.Add(node);
								}
								else
								{
									treeView1.Nodes[1].Nodes.Add(node);
								}
							}
						}
						catch (Exception ex)
						{
							DemoUtils.ShowMessage($"[{j}] 证书加载失败: " + ex.Message);
						}
					}
					treeView1.ExpandAll();
					treeView1.SelectedNode = null;
				}
			});
		}

		private void FormCreateCertificate_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				RefreshCertificate();
			}, null);
		}

		private void button_refresh_cert_Click(object sender, EventArgs e)
		{
			button_refresh_cert.Enabled = false;
			ThreadPool.QueueUserWorkItem(delegate
			{
				RefreshCertificate();
				Invoke((Action)delegate
				{
					button_refresh_cert.Enabled = true;
				});
			}, null);
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.Tag is CertificateItem cert)
			{
				certificateControl.RenderHslCertificate(cert);
				certificateControl.Visible = true;
				certificateListControl.Visible = false;
			}
			else if (node.Parent == null && node.Text.Contains("DotNet"))
			{
				List<CertificateItem> list = new List<CertificateItem>();
				for (int i = 0; i < node.Nodes.Count; i++)
				{
					if (node.Nodes[i].Tag is CertificateItem item)
					{
						list.Add(item);
					}
				}
				certificateListControl.SetCertificates(rpc, list);
				certificateControl.Visible = false;
				certificateListControl.Visible = true;
			}
			else
			{
				if (node.Parent != null || !node.Text.Contains("Java"))
				{
					return;
				}
				List<CertificateItem> list2 = new List<CertificateItem>();
				for (int j = 0; j < node.Nodes.Count; j++)
				{
					if (node.Nodes[j].Tag is CertificateItem item2)
					{
						list2.Add(item2);
					}
				}
				certificateListControl.SetCertificates(rpc, list2);
				certificateControl.Visible = false;
				certificateListControl.Visible = true;
			}
		}

		private void button_new_Click(object sender, EventArgs e)
		{
			certificateControl.SetCompanyName(company);
			certificateControl.Visible = true;
			certificateListControl.Visible = false;
		}

		private void treeView1_MouseClick(object sender, MouseEventArgs e)
		{
			TreeNode nodeAt = treeView1.GetNodeAt(e.Location);
			if (nodeAt != null && nodeAt.Tag is CertificateItem)
			{
				treeView1.SelectedNode = nodeAt;
				if (e.Button == MouseButtons.Right)
				{
					contextMenuStrip1.Show(treeView1, e.Location);
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
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.TreeNode treeNode = new System.Windows.Forms.TreeNode("DotNet(C#)");
			System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("Java");
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.label1 = new System.Windows.Forms.Label();
			this.button_refresh_cert = new System.Windows.Forms.Button();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button_new = new System.Windows.Forms.Button();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panel2 = new System.Windows.Forms.Panel();
			this.panel3 = new System.Windows.Forms.Panel();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(5, 30);
			this.treeView1.Name = "treeView1";
			treeNode.Name = "节点0";
			treeNode.Text = "DotNet(C#)";
			treeNode2.Name = "节点1";
			treeNode2.Text = "Java";
			this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[2] { treeNode, treeNode2 });
			this.treeView1.Size = new System.Drawing.Size(232, 572);
			this.treeView1.TabIndex = 0;
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.treeView1.MouseClick += new System.Windows.Forms.MouseEventHandler(treeView1_MouseClick);
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "证书列表:";
			this.button_refresh_cert.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_refresh_cert.Location = new System.Drawing.Point(176, 1);
			this.button_refresh_cert.Name = "button_refresh_cert";
			this.button_refresh_cert.Size = new System.Drawing.Size(62, 26);
			this.button_refresh_cert.TabIndex = 3;
			this.button_refresh_cert.Text = "刷新";
			this.button_refresh_cert.UseVisualStyleBackColor = true;
			this.button_refresh_cert.Click += new System.EventHandler(button_refresh_cert_Click);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Location = new System.Drawing.Point(3, 30);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(721, 572);
			this.panel1.TabIndex = 26;
			this.button_new.Location = new System.Drawing.Point(3, 1);
			this.button_new.Name = "button_new";
			this.button_new.Size = new System.Drawing.Size(60, 26);
			this.button_new.TabIndex = 27;
			this.button_new.Text = "新建";
			this.button_new.UseVisualStyleBackColor = true;
			this.button_new.Click += new System.EventHandler(button_new_Click);
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.panel2);
			this.splitContainer1.Panel2.Controls.Add(this.panel3);
			this.splitContainer1.Size = new System.Drawing.Size(971, 605);
			this.splitContainer1.SplitterDistance = 240;
			this.splitContainer1.TabIndex = 28;
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.treeView1);
			this.panel2.Controls.Add(this.button_refresh_cert);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(240, 605);
			this.panel2.TabIndex = 0;
			this.panel3.Controls.Add(this.button_new);
			this.panel3.Controls.Add(this.panel1);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel3.Location = new System.Drawing.Point(0, 0);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(727, 605);
			this.panel3.TabIndex = 1;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.deleteToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(125, 26);
			this.deleteToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
			this.deleteToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.deleteToolStripMenuItem.Text = "删除证书";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(971, 605);
			base.Controls.Add(this.splitContainer1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormCreateCertificate";
			this.Text = "VipCertificate";
			base.Load += new System.EventHandler(FormCreateCertificate_Load);
			base.Shown += new System.EventHandler(FormCreateCertificate_Shown);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
