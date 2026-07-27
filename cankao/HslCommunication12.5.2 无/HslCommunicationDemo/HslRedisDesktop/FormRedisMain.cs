using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo;
using HslCommunicationDemo.Properties;
using HslCommunicationDemo.Redis;
using Newtonsoft.Json.Linq;

namespace HslRedisDesktop
{
	public class FormRedisMain : HslFormContent
	{
		private List<RedisSettings> redisSettings = new List<RedisSettings>();

		private UserControl userControl = null;

		private string SettingsPath = Path.Combine(Application.StartupPath, "redisConnect.txt");

		private IContainer components = null;

		private StatusStrip statusStrip1;

		private ToolStripStatusLabel toolStripStatusLabel1;

		private SplitContainer splitContainer1;

		private TreeView treeView1;

		private Label label1;

		private ContextMenuStrip contextMenuStrip_db;

		private ToolStripMenuItem 刷新数据ToolStripMenuItem;

		private ToolStripMenuItem 清除所有KeyToolStripMenuItem;

		private ToolStripMenuItem 新增KeyToolStripMenuItem;

		private ToolStripMenuItem 过滤KeyToolStripMenuItem;

		private ContextMenuStrip contextMenuStrip_redis;

		private ToolStripMenuItem 服务器状态ToolStripMenuItem;

		private ToolStripMenuItem 控制台操作ToolStripMenuItem;

		private ToolStripMenuItem 刷新所有数据ToolStripMenuItem;

		private ToolStripMenuItem 修改连接配置ToolStripMenuItem;

		private ToolStripMenuItem 删除当前链接ToolStripMenuItem;

		private ContextMenuStrip contextMenuStrip_class;

		private ToolStripMenuItem 展开所有ToolStripMenuItem;

		private Panel panel1;

		private Button button1;

		private ToolStripMenuItem 断开当前连接ToolStripMenuItem;

		private ToolStripMenuItem 修改密码ToolStripMenuItem;

		public FormRedisMain()
		{
			InitializeComponent();
			if (File.Exists(SettingsPath))
			{
				redisSettings = JArray.Parse(SoftSecurity.MD5Decrypt(File.ReadAllText(SettingsPath, Encoding.UTF8), "oa8H01kz")).ToObject<List<RedisSettings>>();
			}
			else
			{
				redisSettings = new List<RedisSettings>();
			}
		}

		private void FormMain_Load(object sender, EventArgs e)
		{
			ImageList imageList = new ImageList();
			imageList.Images.Add("loading", Resources.loading);
			imageList.Images.Add("VirtualMachine", Resources.VirtualMachine);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("redis", Resources.redis);
			imageList.Images.Add("redis_db", Resources.redis_db);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("Structure_507", Resources.Structure_507);
			imageList.Images.Add("Module_648", Resources.Module_648);
			imageList.Images.Add("docview_xaml_on_16x16", Resources.docview_xaml_on_16x16);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Table_748", Resources.Table_748);
			imageList.Images.Add("zset", Resources.zset);
			imageList.Images.Add("Tag_7213", Resources.Tag_7213);
			treeView1.ImageList = imageList;
			LoadRedisSettings();
			新增KeyToolStripMenuItem.Click += 新增KeyToolStripMenuItem_Click;
			刷新数据ToolStripMenuItem.Click += 刷新数据ToolStripMenuItem_Click;
			过滤KeyToolStripMenuItem.Click += 过滤KeyToolStripMenuItem_Click;
			清除所有KeyToolStripMenuItem.Click += 清除所有KeyToolStripMenuItem_Click;
			刷新所有数据ToolStripMenuItem.Click += 刷新所有数据ToolStripMenuItem_Click;
			服务器状态ToolStripMenuItem.Click += 服务器状态ToolStripMenuItem_Click;
			控制台操作ToolStripMenuItem.Click += 控制台操作ToolStripMenuItem_Click;
			修改连接配置ToolStripMenuItem.Click += 修改连接配置ToolStripMenuItem_Click;
			修改密码ToolStripMenuItem.Click += 修改密码ToolStripMenuItem_Click;
			断开当前连接ToolStripMenuItem.Click += 断开当前连接ToolStripMenuItem_Click;
			删除当前链接ToolStripMenuItem.Click += 删除当前链接ToolStripMenuItem_Click;
			展开所有ToolStripMenuItem.Click += 展开所有ToolStripMenuItem_Click;
			base.FormClosing += FormMain_FormClosing;
			CreateRedisShowTagControl<StartControl>();
		}

		private void FormMain_Shown(object sender, EventArgs e)
		{
			treeView1.SelectedNode = null;
			treeView1.AfterSelect += TreeView1_AfterSelect;
			treeView1.MouseDown += TreeView1_MouseDown;
		}

		private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			foreach (TreeNode node in treeView1.Nodes)
			{
				if (node != null && node.Tag is RedisSettings redisSettings)
				{
					redisSettings.Redis?.ConnectClose();
				}
			}
		}

		private void 刷新所有数据ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "VirtualMachine"))
			{
				RefreshRedisKey(selectedNode, true);
			}
		}

		private void 服务器状态ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DemoUtils.ShowMessage("暂时没有实现！");
		}

		private void 控制台操作ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "VirtualMachine") && selectedNode.Tag is RedisSettings redis)
			{
				CreateRedisShowTagControl<ConsoleControl>();
				if (userControl is ConsoleControl consoleControl)
				{
					consoleControl.SetRedis(redis);
				}
			}
		}

		private void 修改连接配置ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode == null || selectedNode.ImageKey != "VirtualMachine" || !(selectedNode.Tag is RedisSettings redisSettings))
			{
				return;
			}
			using (FormRedisAdd formRedisAdd = new FormRedisAdd(redisSettings))
			{
				if (formRedisAdd.ShowDialog() == DialogResult.OK)
				{
					if (redisSettings.Redis != null)
					{
						redisSettings.Redis.ConnectClose();
						redisSettings.Redis = null;
					}
					SaveRedisSettings();
					if (DemoUtils.ShowMessage("当前的连接配置已经更新，是否重新刷新数据？", "刷新确认", MessageBoxButtons.YesNo) == DialogResult.OK)
					{
						RefreshRedisKey(selectedNode, true);
					}
				}
			}
		}

		private void 修改密码ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode == null || selectedNode.ImageKey != "VirtualMachine" || !(selectedNode.Tag is RedisSettings redisSettings))
			{
				return;
			}
			if (redisSettings.Redis == null)
			{
				redisSettings.Redis = redisSettings.GetClient();
				if (!redisSettings.Redis.ConnectServer().IsSuccess)
				{
					DemoUtils.ShowMessage("连接Redis[" + redisSettings.Name + "] IpAddress:" + redisSettings.IpAddress + " 失败！");
					return;
				}
			}
			FormInputString formInputString = new FormInputString();
			formInputString.TextInfo = "请输入新的密码：";
			if (formInputString.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			OperateResult operateResult = ((!string.IsNullOrEmpty(formInputString.InputValue)) ? redisSettings.Redis.ReadCustomer("CONFIG SET requirepass " + formInputString.InputValue) : redisSettings.Redis.ReadCustomer("CONFIG SET requirepass "));
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("修改密码成功！");
				redisSettings.Password = formInputString.InputValue;
				if (redisSettings.Redis != null)
				{
					redisSettings.Redis.ConnectClose();
					redisSettings.Redis = null;
				}
				SaveRedisSettings();
				RefreshRedisKey(selectedNode, true);
			}
			else
			{
				DemoUtils.ShowMessage("修改密码失败！" + operateResult.Message);
			}
		}

		private void 断开当前连接ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "VirtualMachine") && selectedNode.Tag is RedisSettings redisSettings)
			{
				redisSettings.Redis?.ConnectClose();
				redisSettings.Redis = null;
				selectedNode.Nodes.Clear();
			}
		}

		private void 删除当前链接ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "VirtualMachine"))
			{
				if (selectedNode.Tag is RedisSettings redisSettings)
				{
					redisSettings?.Redis?.ConnectClose();
					this.redisSettings.Remove(redisSettings);
					SaveRedisSettings();
				}
				treeView1.Nodes.Remove(selectedNode);
			}
		}

		private void 新增KeyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode == null || selectedNode.ImageKey != "redis_db")
			{
				return;
			}
			if (!(selectedNode.Parent.Tag is RedisSettings redisSettings))
			{
				DemoUtils.ShowMessage("获取当前的Redis配置信息失败！");
				return;
			}
			if (redisSettings.Redis == null)
			{
				DemoUtils.ShowMessage("获取当前的Redis连接客户端失败！");
				return;
			}
			using (FormRedisInput formRedisInput = new FormRedisInput(redisSettings.Redis))
			{
				formRedisInput.ShowDialog();
			}
		}

		private void 刷新数据ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "redis_db"))
			{
				RefreshDbKeys(selectedNode, true);
			}
		}

		private void 过滤KeyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode == null || selectedNode.ImageKey != "redis_db" || !(selectedNode.Tag is DbSettings dbSettings))
			{
				return;
			}
			using (FormInputString formInputString = new FormInputString())
			{
				formInputString.TextInfo = "请输入新的过滤条件：";
				formInputString.InputValue = dbSettings.Filter;
				if (formInputString.ShowDialog() == DialogResult.OK)
				{
					dbSettings.Filter = formInputString.InputValue;
					RefreshDbKeys(selectedNode, true);
				}
			}
		}

		private void 清除所有KeyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode == null || selectedNode.ImageKey != "redis_db")
			{
				return;
			}
			if (!(selectedNode.Parent.Tag is RedisSettings redisSettings))
			{
				DemoUtils.ShowMessage("获取当前的Redis配置信息失败！");
			}
			else if (DemoUtils.ShowMessage($"请确认是否清除当前db块[{redisSettings.DBBlock}]的所有数据，当前的操作不可逆，请谨慎操作。", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				if (redisSettings.Redis == null)
				{
					DemoUtils.ShowMessage("获取当前的Redis连接客户端失败！");
				}
				else if (!redisSettings.Redis.FlushDB().IsSuccess)
				{
					DemoUtils.ShowMessage("获取当前的Redis清除数据失败！");
				}
				else
				{
					DemoUtils.ShowMessage("当前的DB块" + redisSettings.DBBlock + " 清除完成，请手动刷新。");
				}
			}
		}

		private void 展开所有ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && !(selectedNode.ImageKey != "Class_489"))
			{
				selectedNode.ExpandAll();
			}
		}

		private void TreeView1_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right)
			{
				return;
			}
			TreeNode nodeAt = treeView1.GetNodeAt(e.Location);
			if (treeView1.SelectedNode != nodeAt)
			{
				treeView1.SelectedNode = nodeAt;
			}
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null)
			{
				if (selectedNode.ImageKey == "redis_db")
				{
					contextMenuStrip_db.Show(treeView1, e.Location);
				}
				else if (selectedNode.ImageKey == "VirtualMachine")
				{
					contextMenuStrip_redis.Show(treeView1, e.Location);
				}
				else if (selectedNode.ImageKey == "Class_489")
				{
					contextMenuStrip_class.Show(treeView1, e.Location);
				}
			}
		}

		private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode.ImageKey == "VirtualMachine")
			{
				RefreshRedisKey(selectedNode);
			}
			else if (selectedNode.ImageKey == "redis_db")
			{
				RefreshDbKeys(selectedNode);
			}
			else
			{
				if (!(selectedNode.ImageKey == "Enum_582") && !(selectedNode.ImageKey == "brackets_Square_16xMD") && !(selectedNode.ImageKey == "Table_748") && !(selectedNode.ImageKey == "docview_xaml_on_16x16") && !(selectedNode.ImageKey == "zset"))
				{
					return;
				}
				TreeNode dbNode = GetDbNode(selectedNode);
				if (!(GetRedisNode(selectedNode).Tag is RedisSettings redisSettings) || !(dbNode.Tag is DbSettings dbSettings))
				{
					return;
				}
				if (redisSettings.DBBlock != dbSettings.DBNumber)
				{
					OperateResult operateResult = redisSettings.Redis.SelectDB(dbSettings.DBNumber);
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage($"当前{redisSettings.Redis} 切换db块{dbSettings.DBNumber} 失败！" + operateResult.Message);
						return;
					}
					redisSettings.DBBlock = dbSettings.DBNumber;
				}
				if (selectedNode.ImageKey == "Enum_582")
				{
					StringKeySelect(redisSettings, selectedNode.Text);
				}
				else if (selectedNode.ImageKey == "brackets_Square_16xMD")
				{
					ListKeySelect(redisSettings, selectedNode.Text);
				}
				else if (selectedNode.ImageKey == "Table_748")
				{
					HashKeySelect(redisSettings, selectedNode.Text);
				}
				else if (selectedNode.ImageKey == "docview_xaml_on_16x16")
				{
					SetKeySelect(redisSettings, selectedNode.Text);
				}
				else if (selectedNode.ImageKey == "zset")
				{
					ZSetKeySelect(redisSettings, selectedNode.Text);
				}
			}
		}

		private TreeNode GetRedisNode(TreeNode treeNode)
		{
			if (treeNode.ImageKey == "VirtualMachine")
			{
				return treeNode;
			}
			return GetRedisNode(treeNode.Parent);
		}

		private TreeNode GetDbNode(TreeNode treeNode)
		{
			if (treeNode.ImageKey == "redis_db")
			{
				return treeNode;
			}
			return GetDbNode(treeNode.Parent);
		}

		private void RefreshRedisKey(TreeNode select, bool reload = false)
		{
			if (!(select.Tag is RedisSettings redisSettings))
			{
				return;
			}
			if (reload)
			{
				select.Nodes.Clear();
			}
			if (redisSettings.Redis == null)
			{
				redisSettings.Redis = redisSettings.GetClient();
				OperateResult operateResult = redisSettings.Redis.ConnectServer();
				if (!operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("连接Redis[" + redisSettings.Name + "] IpAddress:" + redisSettings.IpAddress + " 失败\r\n" + operateResult.Message);
					return;
				}
			}
			if (!(select.Nodes.Count == 0 || reload))
			{
				return;
			}
			for (int i = 0; i < 16; i++)
			{
				if (i == 0)
				{
					OperateResult operateResult2 = redisSettings.Redis.SelectDB(i);
					if (!operateResult2.IsSuccess)
					{
						DemoUtils.ShowMessage("Redis读取失败！" + operateResult2.Message);
						return;
					}
				}
				else if (!redisSettings.Redis.SelectDB(i).IsSuccess)
				{
					break;
				}
				redisSettings.DBBlock = i;
				OperateResult<long> operateResult3 = redisSettings.Redis.DBSize();
				if (!operateResult3.IsSuccess)
				{
					DemoUtils.ShowMessage("当前加载数据块失败！原因：" + operateResult3.Message);
					return;
				}
				TreeNode treeNode = new TreeNode($"db{i} ({operateResult3.Content})");
				treeNode.ImageKey = "redis_db";
				treeNode.SelectedImageKey = "redis_db";
				treeNode.Tag = new DbSettings
				{
					DBNumber = i
				};
				select.Nodes.Add(treeNode);
			}
			if (redisSettings.Redis.SelectDB(0).IsSuccess)
			{
				redisSettings.DBBlock = 0;
			}
			select.Expand();
		}

		private void RefreshDbKeys(TreeNode select, bool reload = false)
		{
			if (!(select.Tag is DbSettings dbSettings))
			{
				return;
			}
			RedisSettings redisSettings = select.Parent.Tag as RedisSettings;
			int dBNumber = dbSettings.DBNumber;
			if (redisSettings == null)
			{
				DemoUtils.ShowMessage("获取当前的Redis配置信息失败！");
				return;
			}
			if (redisSettings.Redis == null)
			{
				DemoUtils.ShowMessage("获取当前的Redis连接客户端失败！");
				return;
			}
			if (reload)
			{
				select.Nodes.Clear();
			}
			if (select.Nodes.Count == 0)
			{
				int dBBlock = redisSettings.DBBlock;
				if (redisSettings.DBBlock != dBNumber)
				{
					if (!redisSettings.Redis.SelectDB(dBNumber).IsSuccess)
					{
						DemoUtils.ShowMessage($"当前客户端[{redisSettings.Name}] 切换DB{dBNumber} 失败！");
						return;
					}
					redisSettings.DBBlock = dBNumber;
				}
				OperateResult<string[]> operateResult = redisSettings.Redis.ReadAllKeys(dbSettings.Filter);
				if (!operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("遍历所有的节点信息失败！");
					return;
				}
				if (dbSettings.Filter == "*")
				{
					select.Text = $"db{dBNumber} ({operateResult.Content.Length})";
				}
				else
				{
					select.Text = $"db{dBNumber} ({operateResult.Content.Length}) [{dbSettings.Filter}]";
				}
				string[] content = operateResult.Content;
				foreach (string text in content)
				{
					AddTreeNode(select, redisSettings.Redis, text, text);
				}
				if (dBBlock != redisSettings.DBBlock)
				{
					if (!redisSettings.Redis.SelectDB(dBBlock).IsSuccess)
					{
						DemoUtils.ShowMessage($"当前客户端[{redisSettings.Name}] 切换回DB{dBBlock} 失败！");
						return;
					}
					redisSettings.DBBlock = dBBlock;
				}
			}
			select.Expand();
		}

		private void AddTreeNode(TreeNode parent, RedisClient redisClient, string key, string infactKey)
		{
			int num = key.IndexOf(':');
			if (num <= 0)
			{
				TreeNode treeNode = new TreeNode(infactKey);
				treeNode.Tag = infactKey;
				OperateResult<string> operateResult = redisClient.ReadKeyType(infactKey);
				if (operateResult.Content == "string")
				{
					treeNode.ImageKey = "Enum_582";
					treeNode.SelectedImageKey = "Enum_582";
				}
				else if (operateResult.Content == "list")
				{
					treeNode.ImageKey = "brackets_Square_16xMD";
					treeNode.SelectedImageKey = "brackets_Square_16xMD";
				}
				else if (operateResult.Content == "hash")
				{
					treeNode.ImageKey = "Table_748";
					treeNode.SelectedImageKey = "Table_748";
				}
				else if (operateResult.Content == "set")
				{
					treeNode.ImageKey = "docview_xaml_on_16x16";
					treeNode.SelectedImageKey = "docview_xaml_on_16x16";
				}
				else if (operateResult.Content == "zset")
				{
					treeNode.ImageKey = "zset";
					treeNode.SelectedImageKey = "zset";
				}
				parent.Nodes.Add(treeNode);
				return;
			}
			TreeNode treeNode2 = null;
			for (int i = 0; i < parent.Nodes.Count; i++)
			{
				if (parent.Nodes[i].Text == key.Substring(0, num))
				{
					treeNode2 = parent.Nodes[i];
					break;
				}
			}
			if (treeNode2 == null)
			{
				treeNode2 = new TreeNode(key.Substring(0, num));
				treeNode2.ImageKey = "Class_489";
				treeNode2.SelectedImageKey = "Class_489";
				AddTreeNode(treeNode2, redisClient, key.Substring(num + 1), infactKey);
				parent.Nodes.Add(treeNode2);
			}
			else
			{
				AddTreeNode(treeNode2, redisClient, key.Substring(num + 1), infactKey);
			}
		}

		private void CreateRedisShowTagControl<T>() where T : UserControl, new()
		{
			T val = new T();
			panel1.Controls.Add(val);
			val.Location = new Point(0, 0);
			val.Size = new Size(panel1.Width - 1, panel1.Height - 1);
			val.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			if (userControl != null)
			{
				panel1.Controls.Remove(userControl);
			}
			userControl?.Dispose();
			userControl = val;
		}

		private void StringKeySelect(RedisSettings redisSettings, string key)
		{
			if (userControl == null || !(userControl is StringValueControl))
			{
				CreateRedisShowTagControl<StringValueControl>();
			}
			if (userControl is StringValueControl stringValueControl)
			{
				stringValueControl.SetNewKey(redisSettings, key);
			}
		}

		private void ListKeySelect(RedisSettings redis, string key)
		{
			if (userControl == null || !(userControl is ListValueControl))
			{
				CreateRedisShowTagControl<ListValueControl>();
			}
			if (userControl is ListValueControl listValueControl)
			{
				listValueControl.SetNewKey(redis, key);
			}
		}

		private void HashKeySelect(RedisSettings redis, string key)
		{
			if (userControl == null || !(userControl is HashValueControl))
			{
				CreateRedisShowTagControl<HashValueControl>();
			}
			if (userControl is HashValueControl hashValueControl)
			{
				hashValueControl.SetNewKey(redis, key);
			}
		}

		private void SetKeySelect(RedisSettings redis, string key)
		{
			if (userControl == null || !(userControl is SetValueControl))
			{
				CreateRedisShowTagControl<SetValueControl>();
			}
			if (userControl is SetValueControl setValueControl)
			{
				setValueControl.SetNewKey(redis, key);
			}
		}

		private void ZSetKeySelect(RedisSettings redis, string key)
		{
			if (userControl == null || !(userControl is ZSetValueControl))
			{
				CreateRedisShowTagControl<ZSetValueControl>();
			}
			if (userControl is ZSetValueControl zSetValueControl)
			{
				zSetValueControl.SetNewKey(redis, key);
			}
		}

		private void LoadRedisSettings()
		{
			treeView1.Nodes.Clear();
			for (int i = 0; i < redisSettings.Count; i++)
			{
				TreeNode treeNode = new TreeNode(redisSettings[i].Name);
				treeNode.ImageKey = "VirtualMachine";
				treeNode.SelectedImageKey = "VirtualMachine";
				treeNode.Tag = redisSettings[i];
				treeView1.Nodes.Add(treeNode);
			}
		}

		private void SaveRedisSettings()
		{
			File.WriteAllText(SettingsPath, SoftSecurity.MD5Encrypt(JArray.FromObject(redisSettings).ToString(), "oa8H01kz"), Encoding.UTF8);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			FormRedisAdd form = new FormRedisAdd(null);
			try
			{
				while (true)
				{
					if (form.ShowDialog() == DialogResult.OK)
					{
						if (redisSettings.Find((RedisSettings m) => m.Name == form.Settings.Name) == null)
						{
							break;
						}
						DemoUtils.ShowMessage("当前输入的服务器别名已经存在，请重新输入！");
						continue;
					}
					return;
				}
				redisSettings.Add(form.Settings);
				SaveRedisSettings();
				TreeNode treeNode = new TreeNode(form.Settings.Name);
				treeNode.ImageKey = "VirtualMachine";
				treeNode.SelectedImageKey = "VirtualMachine";
				treeNode.Tag = form.Settings;
				treeView1.Nodes.Add(treeNode);
			}
			finally
			{
				if (form != null)
				{
					((IDisposable)form).Dispose();
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslRedisDesktop.FormRedisMain));
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.button1 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.label1 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.contextMenuStrip_db = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.新增KeyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.刷新数据ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.过滤KeyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.清除所有KeyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.contextMenuStrip_redis = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.服务器状态ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.控制台操作ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.刷新所有数据ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.修改连接配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.修改密码ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.断开当前连接ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.删除当前链接ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.contextMenuStrip_class = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.展开所有ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.statusStrip1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.contextMenuStrip_db.SuspendLayout();
			this.contextMenuStrip_redis.SuspendLayout();
			this.contextMenuStrip_class.SuspendLayout();
			base.SuspendLayout();
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.toolStripStatusLabel1 });
			this.statusStrip1.Location = new System.Drawing.Point(0, 651);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(1143, 22);
			this.statusStrip1.TabIndex = 2;
			this.statusStrip1.Text = "statusStrip1";
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(56, 17);
			this.toolStripStatusLabel1.Text = "准备就绪";
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.button1);
			this.splitContainer1.Panel1.Controls.Add(this.treeView1);
			this.splitContainer1.Panel1.Controls.Add(this.label1);
			this.splitContainer1.Panel2.Controls.Add(this.panel1);
			this.splitContainer1.Size = new System.Drawing.Size(1143, 651);
			this.splitContainer1.SplitterDistance = 330;
			this.splitContainer1.TabIndex = 3;
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Image = HslCommunicationDemo.Properties.Resources.action_add_16xLG;
			this.button1.Location = new System.Drawing.Point(296, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(32, 23);
			this.button1.TabIndex = 2;
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(3, 29);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(324, 619);
			this.treeView1.TabIndex = 1;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(80, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "服务器列表：";
			this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(809, 651);
			this.panel1.TabIndex = 0;
			this.contextMenuStrip_db.Items.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.新增KeyToolStripMenuItem, this.刷新数据ToolStripMenuItem, this.过滤KeyToolStripMenuItem, this.清除所有KeyToolStripMenuItem });
			this.contextMenuStrip_db.Name = "contextMenuStrip1";
			this.contextMenuStrip_db.Size = new System.Drawing.Size(146, 92);
			this.新增KeyToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_add_16xLG;
			this.新增KeyToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.新增KeyToolStripMenuItem.Name = "新增KeyToolStripMenuItem";
			this.新增KeyToolStripMenuItem.Size = new System.Drawing.Size(145, 22);
			this.新增KeyToolStripMenuItem.Text = "新增Key";
			this.刷新数据ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.Activity_16xLG;
			this.刷新数据ToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.刷新数据ToolStripMenuItem.Name = "刷新数据ToolStripMenuItem";
			this.刷新数据ToolStripMenuItem.Size = new System.Drawing.Size(145, 22);
			this.刷新数据ToolStripMenuItem.Text = "刷新数据";
			this.过滤KeyToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.filter;
			this.过滤KeyToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.过滤KeyToolStripMenuItem.Name = "过滤KeyToolStripMenuItem";
			this.过滤KeyToolStripMenuItem.Size = new System.Drawing.Size(145, 22);
			this.过滤KeyToolStripMenuItem.Text = "过滤Key";
			this.清除所有KeyToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.清除所有KeyToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.清除所有KeyToolStripMenuItem.Name = "清除所有KeyToolStripMenuItem";
			this.清除所有KeyToolStripMenuItem.Size = new System.Drawing.Size(145, 22);
			this.清除所有KeyToolStripMenuItem.Text = "清除所有Key";
			this.contextMenuStrip_redis.Items.AddRange(new System.Windows.Forms.ToolStripItem[7] { this.服务器状态ToolStripMenuItem, this.控制台操作ToolStripMenuItem, this.刷新所有数据ToolStripMenuItem, this.修改连接配置ToolStripMenuItem, this.修改密码ToolStripMenuItem, this.断开当前连接ToolStripMenuItem, this.删除当前链接ToolStripMenuItem });
			this.contextMenuStrip_redis.Name = "contextMenuStrip_redis";
			this.contextMenuStrip_redis.Size = new System.Drawing.Size(149, 158);
			this.服务器状态ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.server_info;
			this.服务器状态ToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.服务器状态ToolStripMenuItem.Name = "服务器状态ToolStripMenuItem";
			this.服务器状态ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.服务器状态ToolStripMenuItem.Text = "服务器状态";
			this.控制台操作ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.console;
			this.控制台操作ToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.控制台操作ToolStripMenuItem.Name = "控制台操作ToolStripMenuItem";
			this.控制台操作ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.控制台操作ToolStripMenuItem.Text = "控制台操作";
			this.刷新所有数据ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.Activity_16xLG;
			this.刷新所有数据ToolStripMenuItem.Name = "刷新所有数据ToolStripMenuItem";
			this.刷新所有数据ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.刷新所有数据ToolStripMenuItem.Text = "刷新所有数据";
			this.修改连接配置ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.settings;
			this.修改连接配置ToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.修改连接配置ToolStripMenuItem.Name = "修改连接配置ToolStripMenuItem";
			this.修改连接配置ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.修改连接配置ToolStripMenuItem.Text = "修改连接配置";
			this.修改密码ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.password;
			this.修改密码ToolStripMenuItem.Name = "修改密码ToolStripMenuItem";
			this.修改密码ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.修改密码ToolStripMenuItem.Text = "修改密码";
			this.断开当前连接ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.disconnect;
			this.断开当前连接ToolStripMenuItem.Name = "断开当前连接ToolStripMenuItem";
			this.断开当前连接ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.断开当前连接ToolStripMenuItem.Text = "断开当前连接";
			this.删除当前链接ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.删除当前链接ToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
			this.删除当前链接ToolStripMenuItem.Name = "删除当前链接ToolStripMenuItem";
			this.删除当前链接ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.删除当前链接ToolStripMenuItem.Text = "删除当前链接";
			this.contextMenuStrip_class.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.展开所有ToolStripMenuItem });
			this.contextMenuStrip_class.Name = "contextMenuStrip_class";
			this.contextMenuStrip_class.Size = new System.Drawing.Size(125, 26);
			this.展开所有ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.expand;
			this.展开所有ToolStripMenuItem.Name = "展开所有ToolStripMenuItem";
			this.展开所有ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.展开所有ToolStripMenuItem.Text = "展开所有";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(1143, 673);
			base.Controls.Add(this.splitContainer1);
			base.Controls.Add(this.statusStrip1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Name = "FormRedisMain";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "HslRedis Desktop";
			base.Load += new System.EventHandler(FormMain_Load);
			base.Shown += new System.EventHandler(FormMain_Shown);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel1.PerformLayout();
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.contextMenuStrip_db.ResumeLayout(false);
			this.contextMenuStrip_redis.ResumeLayout(false);
			this.contextMenuStrip_class.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
