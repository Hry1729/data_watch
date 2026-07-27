using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core.Security;
using HslCommunication.LogNet;
using HslCommunicationDemo.Control;
using HslCommunicationDemo.Properties;
using WeifenLuo.WinFormsUI.Docking;

namespace HslCommunicationDemo.DemoControl
{
	public class FormSaveList : DockContent
	{
		private DemoDeviceList deviceList = new DemoDeviceList();

		private ILogNet logNet = null;

		private ImageList imageList;

		private Dictionary<string, int> formIconImageIndex = new Dictionary<string, int>();

		private DockPanel dockPanel1;

		public static Type[] formTypes = Assembly.GetExecutingAssembly().GetTypes();

		private Dictionary<string, HslFormContent> formInstances = new Dictionary<string, HslFormContent>();

		private IContainer components = null;

		private TreeView treeView2;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem deleteDeviceToolStripMenuItem;

		private ToolStripMenuItem renameDeviceToolStripMenuItem;

		private ToolStripMenuItem newWindowsToolStripMenuItem;

		public FormSaveList(DockPanel dockPanel, ImageList imageList, ILogNet logNet, Dictionary<string, int> iconImageIndex)
		{
			InitializeComponent();
			dockPanel1 = dockPanel;
			this.logNet = logNet;
			this.imageList = imageList;
			formIconImageIndex = iconImageIndex;
			treeView2.ImageList = imageList;
			treeView2.MouseClick += TreeView2_MouseClick;
			treeView2.MouseDoubleClick += TreeView2_MouseDoubleClick;
			deleteDeviceToolStripMenuItem.Click += DeleteDeviceToolStripMenuItem_Click;
			renameDeviceToolStripMenuItem.Click += RenameDeviceToolStripMenuItem_Click;
			newWindowsToolStripMenuItem.Click += NewWindowsToolStripMenuItem_Click;
		}

		private void NewWindowsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView2.SelectedNode;
			if (selectedNode != null && selectedNode.Tag != null && selectedNode.Tag is XElement element)
			{
				CreateNewWindows(selectedNode, element);
			}
		}

		private void FormSaveList_Load(object sender, EventArgs e)
		{
			base.CloseButtonVisible = false;
			SetLanguage();
		}

		public void SetLanguage()
		{
			if (Program.Language == 1)
			{
				Text = "保存列表";
				deleteDeviceToolStripMenuItem.Text = "删除设备";
				renameDeviceToolStripMenuItem.Text = "重命名";
				newWindowsToolStripMenuItem.Text = "新窗口";
			}
			else
			{
				Text = "Save List";
				deleteDeviceToolStripMenuItem.Text = "Delete";
				renameDeviceToolStripMenuItem.Text = "Rename";
				newWindowsToolStripMenuItem.Text = "NewWindow";
			}
		}

		public bool LoadDeviceList()
		{
			if (File.Exists(Path.Combine(Application.StartupPath, "devices.xml")))
			{
				deviceList.SetDevices(XElement.Load(Path.Combine(Application.StartupPath, "devices.xml")));
				RefreshSaveDevices();
				return true;
			}
			return false;
		}

		public void AddDeviceList(XElement element, HslFormContent hslForm)
		{
			deviceList.AddDevice(element);
			RefreshSaveDevices();
			string xmlValue = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlGuid, string.Empty, (string m) => m);
			if (!string.IsNullOrEmpty(xmlValue) && hslForm != null)
			{
				if (formInstances.ContainsKey(xmlValue))
				{
					formInstances[xmlValue] = hslForm;
				}
				else
				{
					formInstances.Add(xmlValue, hslForm);
				}
			}
			File.WriteAllText(Path.Combine(Application.StartupPath, "devices.xml"), deviceList.GetDevices.ToString());
		}

		public void RemoveDeviceForm(string guid)
		{
			if (formInstances.ContainsKey(guid))
			{
				formInstances.Remove(guid);
			}
		}

		public void RefreshSaveDevices()
		{
			treeView2.Nodes.Clear();
			foreach (XElement item in deviceList.GetDevices.Elements())
			{
				AddTreeNode(key: item.Attribute(DemoDeviceList.XmlName).Value, treeView: treeView2, parent: null, element: item);
			}
			treeView2.ExpandAll();
		}

		private void RenameDeviceToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView2.SelectedNode;
			if (selectedNode == null || selectedNode.Tag == null || !(selectedNode.Tag is XElement xElement))
			{
				return;
			}
			string text = xElement.Attribute(DemoDeviceList.XmlName)?.Value;
			using (FormInputNewValue formInputNewValue = new FormInputNewValue(text))
			{
				if (formInputNewValue.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(formInputNewValue.InputValue))
				{
					deviceList.RenameDevice(xElement, formInputNewValue.InputValue);
					if (text.Contains(":") || formInputNewValue.InputValue.Contains(":"))
					{
						RefreshSaveDevices();
					}
					else
					{
						selectedNode.Text = formInputNewValue.InputValue;
					}
					File.WriteAllText(Path.Combine(Application.StartupPath, "devices.xml"), deviceList.GetDevices.ToString());
				}
			}
		}

		private string GetPath(TreeNode node)
		{
			if (node == null)
			{
				return string.Empty;
			}
			if (node.Parent == null)
			{
				return node.Text;
			}
			return GetPath(node.Parent) + ":" + node.Text;
		}

		private void DeleteDeviceToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (treeView2.SelectedNode == null)
			{
				return;
			}
			if (treeView2.SelectedNode.Tag == null)
			{
				if (treeView2.SelectedNode.ImageKey == "Class_489")
				{
					string text = GetPath(treeView2.SelectedNode) + ":";
					if (MessageBox.Show((Program.Language == 1) ? ("确认删除[" + text + "]下所有设备?") : ("Delete all device under path [" + text + "]?"), "Check", MessageBoxButtons.YesNo) == DialogResult.Yes)
					{
						deviceList.DeleteDevice(text);
						treeView2.SelectedNode.Remove();
						File.WriteAllText(Path.Combine(Application.StartupPath, "devices.xml"), deviceList.GetDevices.ToString());
						DemoUtils.ShowMessage("Delete Success!");
					}
				}
			}
			else if (treeView2.SelectedNode.Tag is XElement device && MessageBox.Show((Program.Language == 1) ? "删除确认" : "Delete Check", "Check", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				deviceList.DeleteDevice(device);
				RefreshSaveDevices();
				File.WriteAllText(Path.Combine(Application.StartupPath, "devices.xml"), deviceList.GetDevices.ToString());
				DemoUtils.ShowMessage("Delete Success!");
			}
		}

		private void TreeView2_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				treeView2.SelectedNode = treeView2.GetNodeAt(e.Location);
				contextMenuStrip1.Show(treeView2, e.Location);
			}
		}

		private void TreeView2_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			TreeNode selectedNode = treeView2.SelectedNode;
			if (selectedNode == null || selectedNode.Tag == null || !(selectedNode.Tag is XElement element))
			{
				return;
			}
			string xmlValue = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlGuid, string.Empty, (string m) => m);
			if (!string.IsNullOrEmpty(xmlValue))
			{
				try
				{
					if (formInstances.ContainsKey(xmlValue))
					{
						HslFormContent hslFormContent = formInstances[xmlValue];
						if (hslFormContent != null && !hslFormContent.IsDisposed)
						{
							hslFormContent.Show(dockPanel1);
							hslFormContent.Activate();
							return;
						}
					}
				}
				catch
				{
				}
			}
			CreateNewWindows(selectedNode, element);
		}

		private void CreateNewWindows(TreeNode treeNode, XElement element)
		{
			string value = element.Attribute(DemoDeviceList.XmlType).Value;
			HslFormContent hslFormContent = null;
			Type[] array = formTypes;
			foreach (Type type in array)
			{
				if (type.Name == value)
				{
					hslFormContent = (HslFormContent)type.GetConstructors()[0].Invoke(null);
					hslFormContent.LogNet = logNet;
					break;
				}
			}
			if (hslFormContent == null)
			{
				return;
			}
			string xmlValue = HslFormContent.GetXmlValue(element, DemoDeviceList.XmlGuid, string.Empty, (string m) => m);
			if (!string.IsNullOrEmpty(xmlValue))
			{
				if (formInstances.ContainsKey(xmlValue))
				{
					formInstances[xmlValue] = hslFormContent;
				}
				else
				{
					formInstances.Add(xmlValue, hslFormContent);
				}
			}
			if (treeNode.ImageIndex >= 0)
			{
				hslFormContent.Icon = Icon.FromHandle(((Bitmap)imageList.Images[treeNode.ImageIndex]).GetHicon());
				hslFormContent.SetProtocolImage((Bitmap)imageList.Images[treeNode.ImageIndex]);
			}
			else
			{
				hslFormContent.Icon = Icon.FromHandle(Resources.Method_636.GetHicon());
				hslFormContent.SetProtocolImage(Resources.Method_636);
			}
			if (element.Attribute(DemoDeviceList.XmlEncrypt) != null)
			{
				FormInputPassword formInputPassword = new FormInputPassword();
				if (formInputPassword.ShowDialog() == DialogResult.OK)
				{
					AesCryptography aesCryptography = new AesCryptography(formInputPassword.Password.PadRight(32, '0'));
					try
					{
						string text = aesCryptography.Decrypt(element.Attribute(DemoDeviceList.XmlEncrypt).Value);
						hslFormContent.Show(dockPanel1);
						hslFormContent.LoadXmlParameter(XElement.Parse(text));
						hslFormContent.SetXml(element);
						hslFormContent.Password = formInputPassword.Password;
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage(string.Concat(str2: ex.Message, str0: (Program.Language == 1) ? "解密失败，无法加载当前的配置信息" : "Decryption failed and the current configuration information could not be loaded", str1: Environment.NewLine));
					}
				}
			}
			else
			{
				hslFormContent.Show(dockPanel1);
				hslFormContent.LoadXmlParameter(element);
			}
		}

		private void AddTreeNode(TreeView treeView, TreeNode parent, XElement element, string key)
		{
			int num = key.IndexOf(':');
			if (num <= 0)
			{
				TreeNode treeNode = new TreeNode(key);
				treeNode.Tag = element;
				string value = element.Attribute(DemoDeviceList.XmlType).Value;
				if (formIconImageIndex.ContainsKey(value))
				{
					treeNode.ImageIndex = formIconImageIndex[value];
					treeNode.SelectedImageIndex = formIconImageIndex[value];
				}
				else
				{
					treeNode.ImageIndex = 0;
					treeNode.SelectedImageIndex = 0;
				}
				if (parent == null)
				{
					treeView.Nodes.Add(treeNode);
				}
				else
				{
					parent.Nodes.Add(treeNode);
				}
				return;
			}
			TreeNode treeNode2 = null;
			if (parent == null)
			{
				for (int i = 0; i < treeView.Nodes.Count; i++)
				{
					if (treeView.Nodes[i].Text == key.Substring(0, num))
					{
						treeNode2 = treeView.Nodes[i];
						break;
					}
				}
			}
			else
			{
				for (int j = 0; j < parent.Nodes.Count; j++)
				{
					if (parent.Nodes[j].Text == key.Substring(0, num))
					{
						treeNode2 = parent.Nodes[j];
						break;
					}
				}
			}
			if (treeNode2 == null)
			{
				treeNode2 = new TreeNode(key.Substring(0, num));
				treeNode2.ImageKey = "Class_489";
				treeNode2.SelectedImageKey = "Class_489";
				AddTreeNode(treeView, treeNode2, element, key.Substring(num + 1));
				if (parent == null)
				{
					treeView.Nodes.Add(treeNode2);
				}
				else
				{
					parent.Nodes.Add(treeNode2);
				}
			}
			else
			{
				AddTreeNode(treeView, treeNode2, element, key.Substring(num + 1));
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
			this.treeView2 = new System.Windows.Forms.TreeView();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.deleteDeviceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.renameDeviceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.newWindowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.treeView2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.treeView2.Location = new System.Drawing.Point(0, 0);
			this.treeView2.Name = "treeView2";
			this.treeView2.Size = new System.Drawing.Size(221, 561);
			this.treeView2.TabIndex = 1;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.newWindowsToolStripMenuItem, this.deleteDeviceToolStripMenuItem, this.renameDeviceToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(162, 70);
			this.deleteDeviceToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.deleteDeviceToolStripMenuItem.Name = "deleteDeviceToolStripMenuItem";
			this.deleteDeviceToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
			this.deleteDeviceToolStripMenuItem.Text = "DeleteDevice";
			this.renameDeviceToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.settings;
			this.renameDeviceToolStripMenuItem.Name = "renameDeviceToolStripMenuItem";
			this.renameDeviceToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
			this.renameDeviceToolStripMenuItem.Text = "RenameDevice";
			this.newWindowsToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.view_16xLG;
			this.newWindowsToolStripMenuItem.Name = "newWindowsToolStripMenuItem";
			this.newWindowsToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
			this.newWindowsToolStripMenuItem.Text = "NewWindows";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(221, 561);
			base.Controls.Add(this.treeView2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormSaveList";
			this.Text = "Save List";
			base.Load += new System.EventHandler(FormSaveList_Load);
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
