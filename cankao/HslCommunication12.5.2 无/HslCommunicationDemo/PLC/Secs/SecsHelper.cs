using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication.Secs.Types;

namespace HslCommunicationDemo.PLC.Secs
{
	public class SecsHelper
	{
		public static void EditSecsItemToolStripMenuItem_Click(TreeView treeView1, Action<FormSecsGem.SecsTreeItem> RenderSelectedSecs, bool server)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is FormSecsGem.SecsTreeItem treeItem)
			{
				FormServerSecsItem formServerSecsItem = new FormServerSecsItem(treeItem, server);
				if (formServerSecsItem.ShowDialog() == DialogResult.OK)
				{
					selectedNode.Text = formServerSecsItem.SecsTreeItem.GetTreeNodeText();
					selectedNode.Tag = formServerSecsItem.SecsTreeItem;
					RenderSelectedSecs(formServerSecsItem.SecsTreeItem);
				}
			}
		}

		public static void DeleteSecsItemToolStripMenuItem_Click(TreeView treeView1)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is FormSecsGem.SecsTreeItem secsTreeItem)
			{
				string message = "确认删除 " + secsTreeItem.GetTreeNodeText() + " 的功能？";
				if (Program.Language == 2)
				{
					message = "Confirm delete {secsTreeItem. GetTreeNodeText ()} ?";
				}
				if (DemoUtils.ShowMessage(message, (Program.Language == 2) ? "Delete Check" : "删除确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
				{
					selectedNode.Remove();
				}
			}
		}

		private static TreeNode FindNodeS(TreeView treeView1, FormSecsGem.SecsTreeItem secsTreeItem)
		{
			for (int i = 0; i < treeView1.Nodes.Count; i++)
			{
				TreeNode treeNode = treeView1.Nodes[i];
				int num = Convert.ToInt32(treeNode.Text.Substring(1));
				if (num == secsTreeItem.S)
				{
					return treeNode;
				}
				if (num > secsTreeItem.S)
				{
					TreeNode treeNode2 = new TreeNode("S" + secsTreeItem.S);
					treeView1.Nodes.Insert(i, treeNode2);
					return treeNode2;
				}
			}
			TreeNode treeNode3 = new TreeNode("S" + secsTreeItem.S);
			treeView1.Nodes.Add(treeNode3);
			return treeNode3;
		}

		private static void InsertNodeF(TreeNode parent, FormSecsGem.SecsTreeItem secsTreeItem)
		{
			for (int i = 0; i < parent.Nodes.Count; i++)
			{
				TreeNode treeNode = parent.Nodes[i];
				Match match = Regex.Match(treeNode.Text, "F[0-9]+");
				if (match.Success)
				{
					int num = Convert.ToInt32(match.Value.Substring(1));
					if (num == secsTreeItem.F)
					{
						treeNode.Text = secsTreeItem.GetTreeNodeText();
						treeNode.Tag = secsTreeItem;
						return;
					}
					if (num > secsTreeItem.F)
					{
						TreeNode treeNode2 = new TreeNode(secsTreeItem.GetTreeNodeText());
						treeNode2.Tag = secsTreeItem;
						parent.Nodes.Insert(i, treeNode2);
						return;
					}
				}
			}
			TreeNode treeNode3 = new TreeNode(secsTreeItem.GetTreeNodeText());
			treeNode3.Tag = secsTreeItem;
			parent.Nodes.Add(treeNode3);
		}

		public static void AddNewSecsItemToolStripMenuItem_Click(TreeView treeView1, Action<FormSecsGem.SecsTreeItem> RenderSelectedSecs, bool server)
		{
			FormSecsGem.SecsTreeItem treeItem = new FormSecsGem.SecsTreeItem(1, 1, false, SecsValue.EmptySecsValue(), "Test");
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is FormSecsGem.SecsTreeItem secsTreeItem)
			{
				treeItem = secsTreeItem;
			}
			FormServerSecsItem formServerSecsItem = new FormServerSecsItem(treeItem, server);
			if (formServerSecsItem.ShowDialog() == DialogResult.OK)
			{
				TreeNode treeNode = FindNodeS(treeView1, formServerSecsItem.SecsTreeItem);
				if (treeNode == null)
				{
					DemoUtils.ShowMessage("failed, node is null");
					return;
				}
				InsertNodeF(treeNode, formServerSecsItem.SecsTreeItem);
				RenderSelectedSecs(formServerSecsItem.SecsTreeItem);
			}
		}
	}
}
