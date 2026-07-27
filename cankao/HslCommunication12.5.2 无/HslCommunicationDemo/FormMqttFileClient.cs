using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.MQTT;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo
{
	public class FormMqttFileClient : HslFormContent
	{
		private MqttSyncClient mqttSyncClient;

		private HslCancelToken uploadCacel = null;

		private HslCancelToken downloadCacel = null;

		private int threadlength = 20;

		private Random random = new Random();

		private int upSuccess = 0;

		private int downSuccess = 0;

		private int deleteSuccess = 0;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private GroupBox groupBox1;

		private ProgressBar progressBar1;

		private Label label7;

		private Button button_upload;

		private Button button2;

		private TextBox textBox3;

		private Label label6;

		private GroupBox groupBox4;

		private TextBox textBox_file_date;

		private Label label32;

		private TextBox textBox_file_tag;

		private Label label26;

		private TextBox textBox_file_upload;

		private Label label25;

		private TextBox textBox_file_dowloadTimes;

		private Label label24;

		private TextBox textBox_file_fileSize;

		private Label label23;

		private TextBox textBox_file_fileName;

		private Label label11;

		private TreeView treeView1;

		private GroupBox groupBox3;

		private TextBox textBox_delete_fileName;

		private Label label8;

		private Button button5;

		private GroupBox groupBox2;

		private TextBox textBox_download_fileName;

		private Label label16;

		private ProgressBar progressBar2;

		private Label label12;

		private Button button_download;

		private TextBox textBox_upload_tag;

		private Label label31;

		private TextBox textBox_upload_group;

		private Label label28;

		private Button button6;

		private Label label9;

		private Label label10;

		private UserControlHead userControlHead1;

		private Button button9;

		private Button button10;

		private TextBox textBox_show_group;

		private Label label4;

		private TextBox textBox1;

		private Label label1;

		private TextBox textBox10;

		private Label label3;

		private TextBox textBox9;

		private Label label19;

		private Button button1;

		private Button button7;

		private TextBox textBox2;

		private Label label20;

		private TextBox textBox_ip;

		private Label label21;

		private TextBox textBox5;

		private Label label2;

		private TextBox textBox6;

		private Label label5;

		private ContextMenuStrip contextMenuStrip1;

		private GroupBox groupBox5;

		private Label label13;

		private ToolStripMenuItem 下载文件ToolStripMenuItem;

		private ToolStripMenuItem 删除文件ToolStripMenuItem;

		private Button button8;

		private Label label15;

		private Label label14;

		private ContextMenuStrip contextMenuStrip2;

		private ToolStripMenuItem 删除目录ToolStripMenuItem;

		private ToolStripMenuItem 全部下载ToolStripMenuItem;

		private ToolStripMenuItem 批量上传ToolStripMenuItem;

		private Label label17;

		private Button button11;

		private Button button12;

		private CheckBox checkBox_rsa;

		private Button button_upload_cancel;

		private Button button_download_cancel;

		private ToolStripMenuItem 清除目录文件ToolStripMenuItem;

		private ToolStripMenuItem 刷新目录ToolStripMenuItem;

		private ToolStripMenuItem 重命名目录ToolStripMenuItem;

		public FormMqttFileClient()
		{
			InitializeComponent();
		}

		private void FormFileClient_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			ImageList imageList = new ImageList();
			imageList.Images.Add("VirtualMachine", Resources.VirtualMachine);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("Module_648", Resources.Module_648);
			imageList.Images.Add("Structure_507", Resources.Structure_507);
			imageList.Images.Add("loading", Resources.loading);
			imageList.Images.Add("file", Resources.file);
			imageList.Images.Add("dll", Resources.dll);
			imageList.Images.Add("exe", Resources.exe);
			imageList.Images.Add("xml", Resources.xml);
			imageList.Images.Add("png", Resources.png);
			imageList.Images.Add("doc", Resources.doc);
			imageList.Images.Add("chm", Resources.chm);
			imageList.Images.Add("ppt", Resources.ppt);
			imageList.Images.Add("xls", Resources.xls);
			imageList.Images.Add("vsd", Resources.vsd);
			imageList.Images.Add("pdf", Resources.pdf);
			imageList.Images.Add("ttf", Resources.ttf);
			imageList.Images.Add("txt", Resources.txt);
			imageList.Images.Add("js", Resources.js);
			imageList.Images.Add("jsp", Resources.jsp);
			imageList.Images.Add("msi", Resources.msi);
			imageList.Images.Add("rar", Resources.rar);
			imageList.Images.Add("sql", Resources.sql);
			imageList.Images.Add("cs", Resources.cs);
			imageList.Images.Add("java", Resources.java);
			imageList.Images.Add("py", Resources.py);
			imageList.Images.Add("css", Resources.css);
			imageList.Images.Add("vdo", Resources.vdo);
			imageList.Images.Add("wav", Resources.wav);
			treeView1.ImageList = imageList;
			treeView1.Nodes[0].ImageKey = "VirtualMachine";
			treeView1.Nodes[0].SelectedImageKey = "VirtualMachine";
		}

		private async void button7_Click_1(object sender, EventArgs e)
		{
			MqttConnectionOptions options = new MqttConnectionOptions
			{
				IpAddress = textBox_ip.Text,
				Port = int.Parse(textBox2.Text),
				ClientId = textBox1.Text,
				UseRSAProvider = checkBox_rsa.Checked
			};
			if (!string.IsNullOrEmpty(textBox9.Text) || !string.IsNullOrEmpty(textBox10.Text))
			{
				options.Credentials = new MqttCredential(textBox9.Text, textBox10.Text);
			}
			button7.Enabled = false;
			mqttSyncClient = new MqttSyncClient(options);
			OperateResult connect = await mqttSyncClient.ConnectServerAsync();
			if (connect.IsSuccess)
			{
				mqttSyncClient.ConnectClose();
				panel2.Enabled = true;
				button7.Enabled = false;
				button1.Enabled = true;
				DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
			}
			else
			{
				button7.Enabled = true;
				DemoUtils.ShowMessage(connect.Message);
			}
			string path = Application.StartupPath + "\\Files";
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
		}

		private async void 批量上传ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node.Tag is GroupFileItem)
			{
				return;
			}
			string path = GetGroupsFromNode(node);
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Multiselect = true;
				if (ofd.ShowDialog() == DialogResult.OK)
				{
					string[] fileNames = ofd.FileNames;
					foreach (string item in fileNames)
					{
						textBox3.Text = item;
						await UploadFlieExample(path);
					}
					DemoUtils.ShowMessage("上传目录[" + path + "]完成，共计文件：" + ofd.FileNames.Length);
				}
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox3.Text = openFileDialog.FileName;
				}
			}
		}

		private void 刷新目录ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			treeView1_BeforeExpand(sender, new TreeViewCancelEventArgs(treeView1.SelectedNode, false, TreeViewAction.Expand));
		}

		private async void button_upload_Click(object sender, EventArgs e)
		{
			uploadCacel = new HslCancelToken();
			if (!string.IsNullOrEmpty(textBox3.Text))
			{
				if (!File.Exists(textBox3.Text))
				{
					DemoUtils.ShowMessage("选择的文件不存在，退出！");
					return;
				}
				DateTime uploadStartTime = DateTime.Now;
				OperateResult result = await UploadFlieExample(string.Empty);
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("文件上传成功！耗时：" + (DateTime.Now - uploadStartTime).TotalSeconds.ToString("F1") + " 秒");
				}
				else
				{
					DemoUtils.ShowMessage("文件上传失败：" + result.ToMessageShowString());
				}
			}
			else
			{
				DemoUtils.ShowMessage("Please Select a File");
			}
			uploadCacel = null;
		}

		private async Task<OperateResult> UploadFlieExample(string group)
		{
			button_upload.Enabled = false;
			button_upload_cancel.Enabled = true;
			string fileName = textBox3.Text;
			FileInfo fileInfo = new FileInfo(fileName);
			group = (string.IsNullOrEmpty(group) ? textBox_upload_group.Text : group);
			OperateResult result = await mqttSyncClient.UploadFileAsync(fileName, group, fileInfo.Name, textBox_upload_tag.Text, UpdateReportProgress, uploadCacel);
			button_upload.Enabled = true;
			button_upload_cancel.Enabled = false;
			return result;
		}

		private void button_upload_cancel_Click(object sender, EventArgs e)
		{
			if (uploadCacel != null)
			{
				uploadCacel.IsCancelled = true;
			}
		}

		private void UpdateReportProgress(long sended, long totle)
		{
			if (progressBar1.InvokeRequired)
			{
				progressBar1.Invoke(new Action<long, long>(UpdateReportProgress), sended, totle);
			}
			else
			{
				int value = (int)(sended * 100 / totle);
				label10.Text = SoftBasic.GetSizeDescription(sended) + "/" + SoftBasic.GetSizeDescription(totle);
				progressBar1.Value = value;
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			Process.Start(Application.StartupPath + "\\Files");
		}

		private async void 下载文件ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node.Tag != null && node.Tag is GroupFileItem fileItem)
			{
				if (!button_download.Enabled)
				{
					DemoUtils.ShowMessage("请等待之前的下载完成再进行操作！");
					return;
				}
				textBox5.Text = GetGroupsFromNode(node.Parent);
				textBox_download_fileName.Text = fileItem.FileName;
				await DownloadFileExample();
			}
		}

		private async void 全部下载ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node.Tag is GroupFileItem)
			{
				return;
			}
			foreach (TreeNode item in node.Nodes)
			{
				if (item != null && item.Tag is GroupFileItem fileItem)
				{
					if (!button_download.Enabled)
					{
						DemoUtils.ShowMessage("请等待之前的下载完成再进行操作！");
						return;
					}
					textBox5.Text = GetGroupsFromNode(item.Parent);
					textBox_download_fileName.Text = fileItem.FileName;
					await DownloadFileExample();
				}
			}
		}

		private async void button_download_Click(object sender, EventArgs e)
		{
			downloadCacel = new HslCancelToken();
			await DownloadFileExample();
			downloadCacel = null;
		}

		private async Task DownloadFileExample()
		{
			progressBar2.Value = 0;
			button_download.Enabled = false;
			button_download_cancel.Enabled = true;
			label15.Text = "Start downloading...";
			string fileName = textBox_download_fileName.Text;
			DateTime downloadStartTime = DateTime.Now;
			OperateResult result = await mqttSyncClient.DownloadFileAsync(textBox5.Text, fileName, DownloadReportProgress, Application.StartupPath + "\\Files\\" + fileName, downloadCacel);
			button_download.Enabled = true;
			button_download_cancel.Enabled = false;
			if (result.IsSuccess)
			{
				label15.Text = "文件下载成功！耗时：" + (DateTime.Now - downloadStartTime).TotalSeconds.ToString("F1") + " 秒";
			}
			else
			{
				label15.Text = "文件下载失败：" + result.Message;
			}
		}

		private void button_download_cancel_Click(object sender, EventArgs e)
		{
			if (downloadCacel != null)
			{
				downloadCacel.IsCancelled = true;
			}
		}

		private void DownloadReportProgress(long receive, long totle)
		{
			if (progressBar2.InvokeRequired)
			{
				progressBar2.Invoke(new Action<long, long>(DownloadReportProgress), receive, totle);
			}
			else
			{
				int value = (int)(receive * 100 / totle);
				progressBar2.Value = value;
				label9.Text = SoftBasic.GetSizeDescription(receive) + "/" + SoftBasic.GetSizeDescription(totle);
			}
		}

		private async void button10_Click(object sender, EventArgs e)
		{
			string fileName = textBox_download_fileName.Text;
			OperateResult<bool> result = await mqttSyncClient.IsFileExistsAsync(textBox_upload_group.Text, fileName);
			if (result.IsSuccess)
			{
				if (result.Content)
				{
					DemoUtils.ShowMessage("文件存在！");
				}
				else
				{
					DemoUtils.ShowMessage("文件不存在！");
				}
			}
			else
			{
				DemoUtils.ShowMessage(result.Message);
			}
		}

		private async void 删除文件ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node.Tag != null && node.Tag is GroupFileItem fileItem)
			{
				OperateResult result = await mqttSyncClient.DeleteFileAsync(GetGroupsFromNode(node.Parent), fileItem.FileName);
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("文件删除成功！");
				}
				else
				{
					DemoUtils.ShowMessage("文件删除失败，原因：" + result.ToMessageShowString());
				}
			}
		}

		private async void button5_Click(object sender, EventArgs e)
		{
			OperateResult result = await mqttSyncClient.DeleteFileAsync(textBox6.Text, textBox_delete_fileName.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("文件删除成功！");
			}
			else
			{
				DemoUtils.ShowMessage("文件删除失败，原因：" + result.ToMessageShowString());
			}
		}

		private async void 删除目录ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node != null && DemoUtils.ShowMessage("是否确认删除目录: " + GetGroupsFromNode(node) + "?", "删除确认", MessageBoxButtons.YesNo) != DialogResult.No)
			{
				OperateResult result = await mqttSyncClient.DeleteFolderAsync(GetGroupsFromNode(node));
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("目录删除成功！");
				}
				else
				{
					DemoUtils.ShowMessage("目录删除失败，原因：" + result.ToMessageShowString());
				}
			}
		}

		private async void 清除目录文件ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node != null && DemoUtils.ShowMessage("是否确认清除目录[" + GetGroupsFromNode(node) + "]所有文件?", "删除确认", MessageBoxButtons.YesNo) != DialogResult.No)
			{
				OperateResult result = await mqttSyncClient.DeleteFolderFilesAsync(GetGroupsFromNode(node));
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("指定目录所有文件删除成功！");
				}
				else
				{
					DemoUtils.ShowMessage("指定目录所有文件删除失败，原因：" + result.ToMessageShowString());
				}
			}
		}

		private async void 重命名目录ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node == null)
			{
				return;
			}
			string path = GetGroupsFromNode(node);
			FormInputNewPath form = new FormInputNewPath(path, path);
			if (form.ShowDialog() == DialogResult.OK)
			{
				OperateResult result = await mqttSyncClient.RenameFolderAsync(path, form.PathInput);
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("修改目录名称成功！");
				}
				else
				{
					DemoUtils.ShowMessage("修改目录名称失败，原因：" + result.ToMessageShowString());
				}
			}
		}

		private async void button9_Click(object sender, EventArgs e)
		{
			OperateResult result = await mqttSyncClient.DeleteFolderFilesAsync(textBox6.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("文件目录删除成功！");
			}
			else
			{
				DemoUtils.ShowMessage("文件目录删除失败，原因：" + result.ToMessageShowString());
			}
		}

		private async void button11_Click(object sender, EventArgs e)
		{
			OperateResult<GroupFileInfo> result = await mqttSyncClient.GetGroupFileInfoAsync(textBox6.Text);
			if (result.IsSuccess)
			{
				label17.Text = result.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("获取目录大小失败，原因：" + result.ToMessageShowString());
			}
		}

		private async void button12_Click(object sender, EventArgs e)
		{
			OperateResult<GroupFileInfo[]> result = await mqttSyncClient.GetSubGroupFileInfosAsync(textBox6.Text, true);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage(result.Content.ToJsonString());
			}
			else
			{
				DemoUtils.ShowMessage("获取目录大小失败，原因：" + result.ToMessageShowString());
			}
		}

		private void FillNodeByFactoryGroupId(TreeNode root, OperateResult<string[]> read)
		{
			root.Nodes.Clear();
			if (read.IsSuccess)
			{
				string[] content = read.Content;
				foreach (string tag in content)
				{
					TreeNode treeNode = new TreeNode(tag);
					treeNode.Tag = tag;
					treeNode.Nodes.Add(new TreeNode("loading...")
					{
						ImageKey = "loading",
						SelectedImageKey = "loading"
					});
					root.Nodes.Add(treeNode);
					treeNode.ImageKey = "Class_489";
					treeNode.SelectedImageKey = "Class_489";
				}
			}
			else
			{
				DemoUtils.ShowMessage(read.ToMessageShowString());
			}
		}

		private string GetFileExtensionIconKey(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
			{
				return "file";
			}
			if (fileName.LastIndexOf('.') < 0 && fileName.LastIndexOf('.') == fileName.Length - 1)
			{
				return "file";
			}
			string text = fileName.Substring(fileName.LastIndexOf('.') + 1).ToLower();
			if (text == "doc" || text == "docx")
			{
				return "doc";
			}
			if (text == "xls" || text == "xlsx")
			{
				return "xls";
			}
			switch (text)
			{
			case "dll":
				return "dll";
			case "exe":
				return "exe";
			case "xml":
				return "xml";
			default:
				if (!(text == "gif"))
				{
					if (text == "chm")
					{
						return "chm";
					}
					if (text == "ppt" || text == "pptx")
					{
						return "ppt";
					}
					switch (text)
					{
					case "vsd":
						return "vsd";
					case "pdf":
						return "pdf";
					case "ttf":
						return "ttf";
					case "txt":
						return "txt";
					case "js":
						return "js";
					case "jsp":
						return "jsp";
					case "msi":
						return "msi";
					default:
						if (!(text == "7z"))
						{
							switch (text)
							{
							case "sql":
								return "sql";
							case "cs":
								return "cs";
							case "java":
								return "java";
							case "py":
								return "py";
							case "css":
								return "css";
							default:
								if (!(text == "3gp"))
								{
									switch (text)
									{
									default:
										if (!(text == "midi"))
										{
											return "file";
										}
										goto case "wav";
									case "wav":
									case "mp3":
									case "wma":
										return "wav";
									}
								}
								goto case "avi";
							case "avi":
							case "mov":
							case "rmvb":
							case "rm":
							case "flv":
							case "mp4":
								return "vdo";
							}
						}
						goto case "rar";
					case "rar":
					case "tar":
					case "zip":
					case "iso":
						return "rar";
					}
				}
				goto case "png";
			case "png":
			case "bmp":
			case "jpg":
			case "jpeg":
				return "png";
			}
		}

		private void FillNodeFilesByFactoryGroupId(TreeNode root, OperateResult<GroupFileItem[]> read)
		{
			if (read.IsSuccess)
			{
				GroupFileItem[] content = read.Content;
				foreach (GroupFileItem groupFileItem in content)
				{
					TreeNode treeNode = new TreeNode(groupFileItem.FileName);
					treeNode.Tag = groupFileItem;
					root.Nodes.Add(treeNode);
					treeNode.ImageKey = GetFileExtensionIconKey(groupFileItem.FileName);
					treeNode.SelectedImageKey = GetFileExtensionIconKey(groupFileItem.FileName);
				}
			}
			else
			{
				DemoUtils.ShowMessage(read.ToMessageShowString());
			}
		}

		private async void button6_Click(object sender, EventArgs e)
		{
			treeView1.Nodes[0].Nodes.Clear();
			button6.Enabled = false;
			FillNodeByFactoryGroupId(treeView1.Nodes[0], await mqttSyncClient.DownloadPathFoldersAsync(null));
			FillNodeFilesByFactoryGroupId(treeView1.Nodes[0], await mqttSyncClient.DownloadPathFileNamesAsync(null));
			button6.Enabled = true;
			treeView1.Nodes[0].Expand();
		}

		private async void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.Tag != null)
			{
				string groups = GetGroupsFromNode(node);
				FillNodeByFactoryGroupId(node, await mqttSyncClient.DownloadPathFoldersAsync(groups));
				FillNodeFilesByFactoryGroupId(node, await mqttSyncClient.DownloadPathFileNamesAsync(groups));
			}
		}

		private string GetGroupsFromNode(TreeNode node)
		{
			List<string> list = new List<string>();
			while (node != null)
			{
				list.Add(node.Text);
				node = node.Parent;
			}
			list.Reverse();
			list.RemoveAt(0);
			return GetGroupsString(list.ToArray());
		}

		private string GetGroupsString(string[] groups)
		{
			if (groups == null)
			{
				return string.Empty;
			}
			if (groups.Length == 0)
			{
				return string.Empty;
			}
			string text = groups[0];
			for (int i = 1; i < groups.Length; i++)
			{
				text = text + "/" + groups[i];
			}
			return text;
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.Tag != null && node.Tag is GroupFileItem groupFileItem)
			{
				textBox_show_group.Text = GetGroupsFromNode(node.Parent);
				textBox_file_fileName.Text = groupFileItem.FileName;
				textBox_file_fileSize.Text = groupFileItem.FileSize.ToString();
				textBox_file_date.Text = groupFileItem.UploadTime.ToString();
				textBox_file_dowloadTimes.Text = groupFileItem.DownloadTimes.ToString();
				textBox_file_upload.Text = groupFileItem.Owner;
				textBox_file_tag.Text = groupFileItem.Description;
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlCompanyID, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox1.Text = element.Attribute(DemoDeviceList.XmlCompanyID).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox10.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			mqttSyncClient?.ConnectClose();
			button7.Enabled = true;
			button1.Enabled = false;
		}

		private void treeView1_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right)
			{
				return;
			}
			treeView1.SelectedNode = treeView1.GetNodeAt(e.Location);
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode.Tag != null)
			{
				if (selectedNode.Tag is GroupFileItem)
				{
					contextMenuStrip1.Show(treeView1, e.Location);
				}
				else
				{
					contextMenuStrip2.Show(treeView1, e.Location);
				}
			}
			else
			{
				contextMenuStrip2.Show(treeView1, e.Location);
			}
		}

		private void FormMqttFileClient_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button7.Enabled)
			{
				button1_Click(null, EventArgs.Empty);
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
			System.Windows.Forms.TreeNode treeNode = new System.Windows.Forms.TreeNode("文件列表");
			this.panel1 = new System.Windows.Forms.Panel();
			this.checkBox_rsa = new System.Windows.Forms.CheckBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label19 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label20 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_show_group = new System.Windows.Forms.TextBox();
			this.textBox_file_fileName = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox_file_fileSize = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox_file_dowloadTimes = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.textBox_file_date = new System.Windows.Forms.TextBox();
			this.textBox_file_upload = new System.Windows.Forms.TextBox();
			this.label32 = new System.Windows.Forms.Label();
			this.label26 = new System.Windows.Forms.Label();
			this.textBox_file_tag = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.button12 = new System.Windows.Forms.Button();
			this.label17 = new System.Windows.Forms.Label();
			this.button11 = new System.Windows.Forms.Button();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button9 = new System.Windows.Forms.Button();
			this.textBox_delete_fileName = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button_download_cancel = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.label15 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button10 = new System.Windows.Forms.Button();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_download_fileName = new System.Windows.Forms.TextBox();
			this.label16 = new System.Windows.Forms.Label();
			this.progressBar2 = new System.Windows.Forms.ProgressBar();
			this.label12 = new System.Windows.Forms.Label();
			this.button_download = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button_upload_cancel = new System.Windows.Forms.Button();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_upload_tag = new System.Windows.Forms.TextBox();
			this.label31 = new System.Windows.Forms.Label();
			this.textBox_upload_group = new System.Windows.Forms.TextBox();
			this.label28 = new System.Windows.Forms.Label();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.label7 = new System.Windows.Forms.Label();
			this.button_upload = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.下载文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.删除文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.删除目录ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.全部下载ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.批量上传ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.清除目录文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.刷新目录ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.重命名目录ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox5.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			this.contextMenuStrip2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_rsa);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.textBox10);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label19);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.button7);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label20);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Location = new System.Drawing.Point(6, 37);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(993, 60);
			this.panel1.TabIndex = 7;
			this.checkBox_rsa.AutoSize = true;
			this.checkBox_rsa.Location = new System.Drawing.Point(712, 4);
			this.checkBox_rsa.Name = "checkBox_rsa";
			this.checkBox_rsa.Size = new System.Drawing.Size(212, 21);
			this.checkBox_rsa.TabIndex = 30;
			this.checkBox_rsa.Text = "RSA加密 (V10.2.0版本以上服务器)";
			this.checkBox_rsa.UseVisualStyleBackColor = true;
			this.textBox1.Location = new System.Drawing.Point(87, 32);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(611, 23);
			this.textBox1.TabIndex = 29;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(9, 35);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(80, 17);
			this.label1.TabIndex = 28;
			this.label1.Text = "客户端标识：";
			this.textBox10.Location = new System.Drawing.Point(585, 4);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(113, 23);
			this.textBox10.TabIndex = 27;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(521, 7);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 26;
			this.label3.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(418, 4);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(91, 23);
			this.textBox9.TabIndex = 25;
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(342, 7);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(56, 17);
			this.label19.TabIndex = 24;
			this.label19.Text = "用户名：";
			this.button1.Enabled = false;
			this.button1.Location = new System.Drawing.Point(808, 27);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 23;
			this.button1.Text = "断开连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.button7.Location = new System.Drawing.Point(708, 27);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(91, 28);
			this.button7.TabIndex = 22;
			this.button7.Text = "连接";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click_1);
			this.textBox2.Location = new System.Drawing.Point(244, 4);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(79, 23);
			this.textBox2.TabIndex = 21;
			this.textBox2.Text = "1883";
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(190, 7);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(56, 17);
			this.label20.TabIndex = 20;
			this.label20.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(63, 4);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(115, 23);
			this.textBox_ip.TabIndex = 19;
			this.textBox_ip.Text = "127.0.0.1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(9, 7);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(56, 17);
			this.label21.TabIndex = 18;
			this.label21.Text = "Ip地址：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.groupBox4);
			this.panel2.Enabled = false;
			this.panel2.Location = new System.Drawing.Point(6, 102);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(993, 539);
			this.panel2.TabIndex = 13;
			this.groupBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox4.Controls.Add(this.groupBox5);
			this.groupBox4.Controls.Add(this.label13);
			this.groupBox4.Controls.Add(this.groupBox3);
			this.groupBox4.Controls.Add(this.groupBox2);
			this.groupBox4.Controls.Add(this.groupBox1);
			this.groupBox4.Controls.Add(this.button6);
			this.groupBox4.Controls.Add(this.treeView1);
			this.groupBox4.Location = new System.Drawing.Point(3, 2);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(989, 532);
			this.groupBox4.TabIndex = 3;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "浏览服务器文件";
			this.groupBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox5.Controls.Add(this.label4);
			this.groupBox5.Controls.Add(this.label11);
			this.groupBox5.Controls.Add(this.textBox_show_group);
			this.groupBox5.Controls.Add(this.textBox_file_fileName);
			this.groupBox5.Controls.Add(this.label23);
			this.groupBox5.Controls.Add(this.textBox_file_fileSize);
			this.groupBox5.Controls.Add(this.label24);
			this.groupBox5.Controls.Add(this.textBox_file_dowloadTimes);
			this.groupBox5.Controls.Add(this.label25);
			this.groupBox5.Controls.Add(this.textBox_file_date);
			this.groupBox5.Controls.Add(this.textBox_file_upload);
			this.groupBox5.Controls.Add(this.label32);
			this.groupBox5.Controls.Add(this.label26);
			this.groupBox5.Controls.Add(this.textBox_file_tag);
			this.groupBox5.Location = new System.Drawing.Point(389, 11);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(594, 141);
			this.groupBox5.TabIndex = 41;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "选中文件详细信息";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(7, 23);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(57, 17);
			this.label4.TabIndex = 38;
			this.label4.Text = "Group：";
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(6, 53);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(56, 17);
			this.label11.TabIndex = 14;
			this.label11.Text = "文件名：";
			this.textBox_show_group.Location = new System.Drawing.Point(106, 20);
			this.textBox_show_group.Name = "textBox_show_group";
			this.textBox_show_group.Size = new System.Drawing.Size(481, 23);
			this.textBox_show_group.TabIndex = 39;
			this.textBox_file_fileName.Location = new System.Drawing.Point(106, 50);
			this.textBox_file_fileName.Name = "textBox_file_fileName";
			this.textBox_file_fileName.Size = new System.Drawing.Size(481, 23);
			this.textBox_file_fileName.TabIndex = 15;
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(6, 83);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(68, 17);
			this.label23.TabIndex = 22;
			this.label23.Text = "文件大小：";
			this.textBox_file_fileSize.Location = new System.Drawing.Point(106, 80);
			this.textBox_file_fileSize.Name = "textBox_file_fileSize";
			this.textBox_file_fileSize.Size = new System.Drawing.Size(135, 23);
			this.textBox_file_fileSize.TabIndex = 23;
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(259, 83);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(68, 17);
			this.label24.TabIndex = 24;
			this.label24.Text = "下载次数：";
			this.textBox_file_dowloadTimes.Location = new System.Drawing.Point(347, 80);
			this.textBox_file_dowloadTimes.Name = "textBox_file_dowloadTimes";
			this.textBox_file_dowloadTimes.Size = new System.Drawing.Size(80, 23);
			this.textBox_file_dowloadTimes.TabIndex = 25;
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(451, 83);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(56, 17);
			this.label25.TabIndex = 26;
			this.label25.Text = "上传人：";
			this.textBox_file_date.Location = new System.Drawing.Point(106, 110);
			this.textBox_file_date.Name = "textBox_file_date";
			this.textBox_file_date.Size = new System.Drawing.Size(187, 23);
			this.textBox_file_date.TabIndex = 31;
			this.textBox_file_upload.Location = new System.Drawing.Point(521, 80);
			this.textBox_file_upload.Name = "textBox_file_upload";
			this.textBox_file_upload.Size = new System.Drawing.Size(66, 23);
			this.textBox_file_upload.TabIndex = 27;
			this.label32.AutoSize = true;
			this.label32.Location = new System.Drawing.Point(6, 113);
			this.label32.Name = "label32";
			this.label32.Size = new System.Drawing.Size(68, 17);
			this.label32.TabIndex = 30;
			this.label32.Text = "上传日期：";
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(299, 113);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(44, 17);
			this.label26.TabIndex = 28;
			this.label26.Text = "备注：";
			this.textBox_file_tag.Location = new System.Drawing.Point(399, 110);
			this.textBox_file_tag.Name = "textBox_file_tag";
			this.textBox_file_tag.Size = new System.Drawing.Size(188, 23);
			this.textBox_file_tag.TabIndex = 29;
			this.label13.AutoSize = true;
			this.label13.ForeColor = System.Drawing.Color.Gray;
			this.label13.Location = new System.Drawing.Point(5, 21);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(116, 17);
			this.label13.TabIndex = 40;
			this.label13.Text = "右键弹出菜单可操作";
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Controls.Add(this.button12);
			this.groupBox3.Controls.Add(this.label17);
			this.groupBox3.Controls.Add(this.button11);
			this.groupBox3.Controls.Add(this.textBox6);
			this.groupBox3.Controls.Add(this.label5);
			this.groupBox3.Controls.Add(this.button9);
			this.groupBox3.Controls.Add(this.textBox_delete_fileName);
			this.groupBox3.Controls.Add(this.label8);
			this.groupBox3.Controls.Add(this.button5);
			this.groupBox3.Location = new System.Drawing.Point(389, 425);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(594, 101);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "文件删除";
			this.button12.Location = new System.Drawing.Point(156, 41);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(75, 28);
			this.button12.TabIndex = 26;
			this.button12.Text = "子目录";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.label17.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label17.Location = new System.Drawing.Point(344, 47);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(241, 51);
			this.label17.TabIndex = 25;
			this.label17.Text = "0";
			this.button11.Location = new System.Drawing.Point(237, 41);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(101, 28);
			this.button11.TabIndex = 24;
			this.button11.Text = "目录文件信息";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.textBox6.Location = new System.Drawing.Point(63, 17);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(178, 23);
			this.textBox6.TabIndex = 23;
			this.textBox6.Text = "Files/Personal/Admin";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(6, 20);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(57, 17);
			this.label5.TabIndex = 22;
			this.label5.Text = "Group：";
			this.button9.Location = new System.Drawing.Point(67, 41);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(83, 28);
			this.button9.TabIndex = 18;
			this.button9.Text = "删除目录";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.textBox_delete_fileName.Location = new System.Drawing.Point(321, 17);
			this.textBox_delete_fileName.Name = "textBox_delete_fileName";
			this.textBox_delete_fileName.Size = new System.Drawing.Size(266, 23);
			this.textBox_delete_fileName.TabIndex = 17;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(247, 20);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(56, 17);
			this.label8.TabIndex = 16;
			this.label8.Text = "文件名：";
			this.button5.Location = new System.Drawing.Point(7, 41);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(54, 28);
			this.button5.TabIndex = 7;
			this.button5.Text = "删除";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button_download_cancel);
			this.groupBox2.Controls.Add(this.button8);
			this.groupBox2.Controls.Add(this.label15);
			this.groupBox2.Controls.Add(this.label14);
			this.groupBox2.Controls.Add(this.textBox5);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.button10);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox_download_fileName);
			this.groupBox2.Controls.Add(this.label16);
			this.groupBox2.Controls.Add(this.progressBar2);
			this.groupBox2.Controls.Add(this.label12);
			this.groupBox2.Controls.Add(this.button_download);
			this.groupBox2.Location = new System.Drawing.Point(389, 296);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(594, 128);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "文件下载";
			this.button_download_cancel.Enabled = false;
			this.button_download_cancel.Location = new System.Drawing.Point(517, 74);
			this.button_download_cancel.Name = "button_download_cancel";
			this.button_download_cancel.Size = new System.Drawing.Size(70, 25);
			this.button_download_cancel.TabIndex = 25;
			this.button_download_cancel.Text = "取消";
			this.button_download_cancel.UseVisualStyleBackColor = true;
			this.button_download_cancel.Click += new System.EventHandler(button_download_cancel_Click);
			this.button8.Location = new System.Drawing.Point(517, 101);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(70, 25);
			this.button8.TabIndex = 24;
			this.button8.Text = "打开目录";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(78, 104);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(65, 17);
			this.label15.TabIndex = 23;
			this.label15.Text = "等待下载...";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(7, 104);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(44, 17);
			this.label14.TabIndex = 22;
			this.label14.Text = "状态：";
			this.textBox5.Location = new System.Drawing.Point(81, 21);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(430, 23);
			this.textBox5.TabIndex = 21;
			this.textBox5.Text = "Files/Personal/Admin";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 24);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(57, 17);
			this.label2.TabIndex = 20;
			this.label2.Text = "Group：";
			this.button10.Location = new System.Drawing.Point(517, 19);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(70, 25);
			this.button10.TabIndex = 19;
			this.button10.Text = "是否存在";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(417, 81);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(27, 17);
			this.label9.TabIndex = 18;
			this.label9.Text = "0/0";
			this.textBox_download_fileName.Location = new System.Drawing.Point(80, 52);
			this.textBox_download_fileName.Name = "textBox_download_fileName";
			this.textBox_download_fileName.Size = new System.Drawing.Size(431, 23);
			this.textBox_download_fileName.TabIndex = 17;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(6, 55);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(56, 17);
			this.label16.TabIndex = 16;
			this.label16.Text = "文件名：";
			this.progressBar2.Location = new System.Drawing.Point(80, 81);
			this.progressBar2.Name = "progressBar2";
			this.progressBar2.Size = new System.Drawing.Size(331, 17);
			this.progressBar2.TabIndex = 9;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(6, 81);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 8;
			this.label12.Text = "进度：";
			this.button_download.Location = new System.Drawing.Point(517, 47);
			this.button_download.Name = "button_download";
			this.button_download.Size = new System.Drawing.Size(70, 25);
			this.button_download.TabIndex = 7;
			this.button_download.Text = "下载";
			this.button_download.UseVisualStyleBackColor = true;
			this.button_download.Click += new System.EventHandler(button_download_Click);
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.button_upload_cancel);
			this.groupBox1.Controls.Add(this.label10);
			this.groupBox1.Controls.Add(this.textBox_upload_tag);
			this.groupBox1.Controls.Add(this.label31);
			this.groupBox1.Controls.Add(this.textBox_upload_group);
			this.groupBox1.Controls.Add(this.label28);
			this.groupBox1.Controls.Add(this.progressBar1);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.button_upload);
			this.groupBox1.Controls.Add(this.button2);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(389, 154);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(594, 138);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "文件上传";
			this.button_upload_cancel.Enabled = false;
			this.button_upload_cancel.Location = new System.Drawing.Point(518, 83);
			this.button_upload_cancel.Name = "button_upload_cancel";
			this.button_upload_cancel.Size = new System.Drawing.Size(70, 28);
			this.button_upload_cancel.TabIndex = 27;
			this.button_upload_cancel.Text = "取消";
			this.button_upload_cancel.UseVisualStyleBackColor = true;
			this.button_upload_cancel.Click += new System.EventHandler(button_upload_cancel_Click);
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(464, 113);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(27, 17);
			this.label10.TabIndex = 26;
			this.label10.Text = "0/0";
			this.textBox_upload_tag.Location = new System.Drawing.Point(80, 84);
			this.textBox_upload_tag.Name = "textBox_upload_tag";
			this.textBox_upload_tag.Size = new System.Drawing.Size(431, 23);
			this.textBox_upload_tag.TabIndex = 25;
			this.textBox_upload_tag.Text = "test test for example it is import";
			this.label31.AutoSize = true;
			this.label31.Location = new System.Drawing.Point(7, 87);
			this.label31.Name = "label31";
			this.label31.Size = new System.Drawing.Size(44, 17);
			this.label31.TabIndex = 24;
			this.label31.Text = "备注：";
			this.textBox_upload_group.Location = new System.Drawing.Point(80, 55);
			this.textBox_upload_group.Name = "textBox_upload_group";
			this.textBox_upload_group.Size = new System.Drawing.Size(431, 23);
			this.textBox_upload_group.TabIndex = 19;
			this.textBox_upload_group.Text = "Files/Personal/Admin";
			this.label28.AutoSize = true;
			this.label28.Location = new System.Drawing.Point(6, 58);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(57, 17);
			this.label28.TabIndex = 18;
			this.label28.Text = "Group：";
			this.progressBar1.Location = new System.Drawing.Point(80, 113);
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(378, 17);
			this.progressBar1.TabIndex = 9;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 113);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 8;
			this.label7.Text = "进度：";
			this.button_upload.Location = new System.Drawing.Point(517, 51);
			this.button_upload.Name = "button_upload";
			this.button_upload.Size = new System.Drawing.Size(70, 28);
			this.button_upload.TabIndex = 7;
			this.button_upload.Text = "上传";
			this.button_upload.UseVisualStyleBackColor = true;
			this.button_upload.Click += new System.EventHandler(button_upload_Click);
			this.button2.Location = new System.Drawing.Point(517, 20);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(70, 28);
			this.button2.TabIndex = 6;
			this.button2.Text = "选择";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.textBox3.Location = new System.Drawing.Point(80, 25);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(431, 23);
			this.textBox3.TabIndex = 5;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(6, 28);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 4;
			this.label6.Text = "文件路径：";
			this.button6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button6.Location = new System.Drawing.Point(310, 15);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(74, 23);
			this.button6.TabIndex = 32;
			this.button6.Text = "刷新";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(4, 42);
			this.treeView1.Name = "treeView1";
			treeNode.Name = "节点0";
			treeNode.Text = "文件列表";
			this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[1] { treeNode });
			this.treeView1.Size = new System.Drawing.Size(379, 484);
			this.treeView1.TabIndex = 0;
			this.treeView1.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(treeView1_BeforeExpand);
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.treeView1.MouseClick += new System.Windows.Forms.MouseEventHandler(treeView1_MouseClick);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/7746113.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MQTT - File";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.下载文件ToolStripMenuItem, this.删除文件ToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(125, 48);
			this.下载文件ToolStripMenuItem.Name = "下载文件ToolStripMenuItem";
			this.下载文件ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.下载文件ToolStripMenuItem.Text = "下载文件";
			this.下载文件ToolStripMenuItem.Click += new System.EventHandler(下载文件ToolStripMenuItem_Click);
			this.删除文件ToolStripMenuItem.Name = "删除文件ToolStripMenuItem";
			this.删除文件ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.删除文件ToolStripMenuItem.Text = "删除文件";
			this.删除文件ToolStripMenuItem.Click += new System.EventHandler(删除文件ToolStripMenuItem_Click);
			this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[6] { this.删除目录ToolStripMenuItem, this.全部下载ToolStripMenuItem, this.批量上传ToolStripMenuItem, this.清除目录文件ToolStripMenuItem, this.刷新目录ToolStripMenuItem, this.重命名目录ToolStripMenuItem });
			this.contextMenuStrip2.Name = "contextMenuStrip2";
			this.contextMenuStrip2.Size = new System.Drawing.Size(149, 136);
			this.删除目录ToolStripMenuItem.Name = "删除目录ToolStripMenuItem";
			this.删除目录ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.删除目录ToolStripMenuItem.Text = "删除目录";
			this.删除目录ToolStripMenuItem.Click += new System.EventHandler(删除目录ToolStripMenuItem_Click);
			this.全部下载ToolStripMenuItem.Name = "全部下载ToolStripMenuItem";
			this.全部下载ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.全部下载ToolStripMenuItem.Text = "全部下载";
			this.全部下载ToolStripMenuItem.Click += new System.EventHandler(全部下载ToolStripMenuItem_Click);
			this.批量上传ToolStripMenuItem.Name = "批量上传ToolStripMenuItem";
			this.批量上传ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.批量上传ToolStripMenuItem.Text = "批量上传";
			this.批量上传ToolStripMenuItem.Click += new System.EventHandler(批量上传ToolStripMenuItem_Click);
			this.清除目录文件ToolStripMenuItem.Name = "清除目录文件ToolStripMenuItem";
			this.清除目录文件ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.清除目录文件ToolStripMenuItem.Text = "清除目录文件";
			this.清除目录文件ToolStripMenuItem.Click += new System.EventHandler(清除目录文件ToolStripMenuItem_Click);
			this.刷新目录ToolStripMenuItem.Name = "刷新目录ToolStripMenuItem";
			this.刷新目录ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.刷新目录ToolStripMenuItem.Text = "刷新目录";
			this.刷新目录ToolStripMenuItem.Click += new System.EventHandler(刷新目录ToolStripMenuItem_Click);
			this.重命名目录ToolStripMenuItem.Name = "重命名目录ToolStripMenuItem";
			this.重命名目录ToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
			this.重命名目录ToolStripMenuItem.Text = "重命名目录";
			this.重命名目录ToolStripMenuItem.Click += new System.EventHandler(重命名目录ToolStripMenuItem_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMqttFileClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "文件客户端窗口";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttFileClient_FormClosing);
			base.Load += new System.EventHandler(FormFileClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.contextMenuStrip1.ResumeLayout(false);
			this.contextMenuStrip2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
