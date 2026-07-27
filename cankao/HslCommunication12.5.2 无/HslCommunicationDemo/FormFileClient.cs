using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Enthernet;
using HslCommunication.LogNet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormFileClient : HslFormContent
	{
		private IntegrationFileClient integrationFileClient;

		private int threadlength = 20;

		private Random random = new Random();

		private int upSuccess = 0;

		private int downSuccess = 0;

		private int deleteSuccess = 0;

		private IContainer components = null;

		private Panel panel1;

		private TextBox textBox15;

		private Label label21;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private GroupBox groupBox1;

		private ProgressBar progressBar1;

		private Label label7;

		private Button button3;

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

		private TextBox textBox_delete_id;

		private Label label_delete_id;

		private TextBox textBox_delete_group;

		private Label label_delete_group;

		private Button button5;

		private TextBox textBox_delete_factory;

		private Label label17;

		private GroupBox groupBox2;

		private TextBox textBox_download_fileName;

		private Label label16;

		private TextBox textBox_download_id;

		private Label label15;

		private TextBox textBox_download_group;

		private Label label14;

		private ProgressBar progressBar2;

		private Label label12;

		private Button button4;

		private TextBox textBox_download_factory;

		private Label label13;

		private TextBox textBox_upload_tag;

		private Label label31;

		private TextBox textBox_upload_name;

		private Label label30;

		private TextBox textBox_upload_id;

		private Label label27;

		private TextBox textBox_upload_group;

		private Label label28;

		private TextBox textBox_upload_factory;

		private Label label29;

		private Button button6;

		private Label label9;

		private Label label10;

		private Label label18;

		private UserControlHead userControlHead1;

		private Button button7;

		private Button button8;

		private Button button9;

		private Button button10;

		private TextBox textBox_show_id;

		private Label label2;

		private TextBox textBox_show_group;

		private Label label4;

		private TextBox textBox_show_factory;

		private Label label5;

		private Label label20;

		private TextBox textBox4;

		private Label label19;

		private Button button11;

		private Button button12;

		private Label label22;

		private Button button13;

		public FormFileClient()
		{
			InitializeComponent();
		}

		private void FormFileClient_Load(object sender, EventArgs e)
		{
			textBox15.Text = Guid.Empty.ToString();
		}

		private void IntegrationFileClientInitialization()
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage("Port输入异常，请重新输入！");
				return;
			}
			if (!int.TryParse(textBox4.Text, out var result2))
			{
				DemoUtils.ShowMessage("FileCache输入异常，请重新输入！");
				return;
			}
			integrationFileClient = new IntegrationFileClient(textBox1.Text, result)
			{
				ConnectTimeOut = 5000,
				Token = new Guid(textBox15.Text),
				LogNet = new LogNetSingle(Application.StartupPath + "\\Logs\\log.txt"),
				FileCacheSize = result2 * 1024
			};
			string path = Application.StartupPath + "\\Files";
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
			button1.Enabled = false;
			panel2.Enabled = true;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			IntegrationFileClientInitialization();
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

		private async void button3_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrEmpty(textBox3.Text))
			{
				if (!File.Exists(textBox3.Text))
				{
					DemoUtils.ShowMessage("选择的文件不存在，退出！");
					return;
				}
				button3.Enabled = false;
				string fileName = textBox3.Text;
				FileInfo fileInfo = new FileInfo(fileName);
				DateTime uploadStartTime = DateTime.Now;
				OperateResult result = await integrationFileClient.UploadFileAsync(fileName, fileInfo.Name, textBox_upload_factory.Text, textBox_upload_group.Text, textBox_upload_id.Text, textBox_upload_tag.Text, textBox_upload_name.Text, UpdateReportProgress);
				button3.Enabled = true;
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
		}

		private void ThreadUploadFile(object filename)
		{
			if (!(filename is string fileName))
			{
				return;
			}
			FileInfo fileInfo = new FileInfo(fileName);
			OperateResult result = integrationFileClient.UploadFile(fileName, fileInfo.Name, textBox_upload_factory.Text, textBox_upload_group.Text, textBox_upload_id.Text, textBox_upload_tag.Text, textBox_upload_name.Text, UpdateReportProgress);
			Invoke((Action<OperateResult>)delegate
			{
				button3.Enabled = true;
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("文件上传成功！");
				}
				else
				{
					DemoUtils.ShowMessage("文件上传失败：" + result.ToMessageShowString());
				}
			}, result);
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
				label10.Text = sended + "/" + totle;
				progressBar1.Value = value;
			}
		}

		private async void button4_Click(object sender, EventArgs e)
		{
			progressBar2.Value = 0;
			button4.Enabled = false;
			string fileName = textBox_download_fileName.Text;
			DateTime downloadStartTime = DateTime.Now;
			OperateResult result = await integrationFileClient.DownloadFileAsync(fileName, textBox_download_factory.Text, textBox_download_group.Text, textBox_download_id.Text, DownloadReportProgress, Application.StartupPath + "\\Files\\" + fileName);
			button4.Enabled = true;
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("文件下载成功！耗时：" + (DateTime.Now - downloadStartTime).TotalSeconds.ToString("F1") + " 秒");
			}
			else
			{
				DemoUtils.ShowMessage("文件下载失败：" + result.ToMessageShowString());
			}
		}

		private void button10_Click(object sender, EventArgs e)
		{
			string fileName = textBox_download_fileName.Text;
			OperateResult<bool> operateResult = integrationFileClient.IsFileExists(fileName, textBox_download_factory.Text, textBox_download_group.Text, textBox_download_id.Text);
			if (operateResult.IsSuccess)
			{
				if (operateResult.Content)
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
				DemoUtils.ShowMessage(operateResult.Message);
			}
		}

		private void ThreadDownloadFile(object filename)
		{
			if (!(filename is string fileName))
			{
				return;
			}
			OperateResult result = integrationFileClient.DownloadFile(fileName, textBox_download_factory.Text, textBox_download_group.Text, textBox_download_id.Text, DownloadReportProgress, Application.StartupPath + "\\Files\\" + filename);
			Invoke((Action<OperateResult>)delegate
			{
				button4.Enabled = true;
				if (result.IsSuccess)
				{
					DemoUtils.ShowMessage("文件下载成功！");
				}
				else
				{
					DemoUtils.ShowMessage("文件下载失败：" + result.ToMessageShowString());
				}
			}, result);
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
				label9.Text = receive + "/" + totle;
			}
		}

		private async void button5_Click(object sender, EventArgs e)
		{
			OperateResult result = await integrationFileClient.DeleteFileAsync(textBox_delete_fileName.Text, textBox_delete_factory.Text, textBox_delete_group.Text, textBox_delete_id.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("文件删除成功！");
			}
			else
			{
				DemoUtils.ShowMessage("文件删除失败，原因：" + result.ToMessageShowString());
			}
		}

		private async void button9_Click(object sender, EventArgs e)
		{
			OperateResult result = await integrationFileClient.DeleteFolderFilesAsync(textBox_delete_factory.Text, textBox_delete_group.Text, textBox_delete_id.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage("文件信息删除成功！");
			}
			else
			{
				DemoUtils.ShowMessage("文件删除失败，原因：" + result.ToMessageShowString());
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = integrationFileClient.DeleteEmptyFolders(textBox_delete_factory.Text, textBox_delete_group.Text, textBox_delete_id.Text);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("空目录信息删除成功！");
			}
			else
			{
				DemoUtils.ShowMessage("空目录信息删除失败，原因：" + operateResult.ToMessageShowString());
			}
		}

		private async void button12_Click(object sender, EventArgs e)
		{
			OperateResult<GroupFileInfo> result = await integrationFileClient.GetGroupFileInfoAsync(textBox_delete_factory.Text, textBox_delete_group.Text, textBox_delete_id.Text);
			if (result.IsSuccess)
			{
				label22.Text = result.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("获取文件大小失败，原因：" + result.ToMessageShowString());
			}
		}

		private async void button13_Click(object sender, EventArgs e)
		{
			OperateResult<GroupFileInfo[]> result = await integrationFileClient.GetSubGroupFileInfosAsync(textBox_delete_factory.Text, textBox_delete_group.Text, textBox_delete_id.Text);
			if (result.IsSuccess)
			{
				DemoUtils.ShowMessage(result.Content.ToJsonString());
			}
			else
			{
				DemoUtils.ShowMessage("获取文件大小失败，原因：" + result.ToMessageShowString());
			}
		}

		private void FillNodeByFactoryGroupId(TreeNode root, OperateResult<string[]> read, int grade)
		{
			root.Nodes.Clear();
			if (read.IsSuccess)
			{
				string[] content = read.Content;
				for (int i = 0; i < content.Length; i++)
				{
					TreeNode treeNode = new TreeNode(content[i]);
					treeNode.Tag = grade;
					treeNode.Nodes.Add(new TreeNode());
					root.Nodes.Add(treeNode);
				}
			}
			else
			{
				DemoUtils.ShowMessage(read.ToMessageShowString());
			}
		}

		private void FillNodeFilesByFactoryGroupId(TreeNode root, OperateResult<GroupFileItem[]> read)
		{
			root.Nodes.Clear();
			root.ExpandAll();
			if (read.IsSuccess)
			{
				GroupFileItem[] content = read.Content;
				foreach (GroupFileItem groupFileItem in content)
				{
					TreeNode treeNode = new TreeNode(groupFileItem.FileName);
					treeNode.Tag = groupFileItem;
					root.Nodes.Add(treeNode);
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
			FillNodeByFactoryGroupId(treeView1.Nodes[0], await integrationFileClient.DownloadPathFoldersAsync("", "", ""), 1);
			button6.Enabled = true;
			treeView1.Nodes[0].ExpandAll();
		}

		private async void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.Tag == null)
			{
				return;
			}
			object tag = node.Tag;
			int grade = default(int);
			int num;
			if (tag is int)
			{
				grade = (int)tag;
				num = 1;
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				switch (grade)
				{
				case 1:
					FillNodeByFactoryGroupId(node, await integrationFileClient.DownloadPathFoldersAsync(node.Text, "", ""), 2);
					break;
				case 2:
					FillNodeByFactoryGroupId(node, await integrationFileClient.DownloadPathFoldersAsync(node.Parent.Text, node.Text, ""), 3);
					break;
				case 3:
					FillNodeFilesByFactoryGroupId(node, await integrationFileClient.DownloadPathFileNamesAsync(node.Parent.Parent.Text, node.Parent.Text, node.Text));
					break;
				}
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.Tag == null || !(node.Tag is GroupFileItem groupFileItem))
			{
				return;
			}
			List<string> list = new List<string>(3)
			{
				string.Empty,
				string.Empty,
				string.Empty
			};
			list[0] = node.Parent?.Parent?.Parent?.Text;
			list[1] = node.Parent?.Parent?.Text;
			list[2] = node.Parent?.Text;
			for (int num = 2; num >= 0; num--)
			{
				if (string.IsNullOrEmpty(list[num]))
				{
					list.RemoveAt(num);
				}
			}
			if (list.Count > 0)
			{
				textBox_show_factory.Text = list[0];
			}
			if (list.Count > 1)
			{
				textBox_show_group.Text = list[1];
			}
			if (list.Count > 2)
			{
				textBox_show_id.Text = list[2];
			}
			textBox_file_fileName.Text = groupFileItem.FileName;
			textBox_file_fileSize.Text = groupFileItem.FileSize.ToString();
			textBox_file_date.Text = groupFileItem.UploadTime.ToString();
			textBox_file_dowloadTimes.Text = groupFileItem.DownloadTimes.ToString();
			textBox_file_upload.Text = groupFileItem.Owner;
			textBox_file_tag.Text = groupFileItem.Description;
		}

		private void TestMethod()
		{
			OperateResult<GroupFileItem[]> operateResult = integrationFileClient.DownloadPathFileNames("Files", "Group", "id");
			if (operateResult.IsSuccess)
			{
				GroupFileItem[] content = operateResult.Content;
				foreach (GroupFileItem groupFileItem in content)
				{
				}
			}
		}

		private void button7_Click(object sender, EventArgs e)
		{
			threadlength = 20;
			upSuccess = 0;
			downSuccess = 0;
			deleteSuccess = 0;
			for (int i = 0; i < threadlength; i++)
			{
				Thread thread = new Thread(ServerPressureTest);
				thread.IsBackground = true;
				thread.Start("B" + i);
			}
			button7.Enabled = false;
		}

		private void ServerPressureTest(object obj)
		{
			if (!(obj is string text))
			{
				return;
			}
			string text2 = Path.Combine(Application.StartupPath, text);
			Directory.CreateDirectory(text2);
			for (int i = 0; i < 100; i++)
			{
				string text3 = text + "-A" + random.Next(500) + ".txt";
				string text4 = Path.Combine(text2, text3);
				StringBuilder stringBuilder = new StringBuilder();
				for (int j = 0; j < 1024; j++)
				{
					stringBuilder.Append('1');
				}
				File.WriteAllText(text4, stringBuilder.ToString(), Encoding.UTF8);
				if (integrationFileClient.UploadFile(text4, "Files", "Group", "id", null).IsSuccess)
				{
					upSuccess++;
				}
				File.Delete(text4);
				if (!integrationFileClient.IsFileExists(text3, "Files", "Group", "id").Content)
				{
					upSuccess--;
					continue;
				}
				if (integrationFileClient.DownloadFile(text3, "Files", "Group", "id", null, text4).IsSuccess)
				{
					downSuccess++;
				}
				File.Delete(text4);
				if (integrationFileClient.DeleteFile(text3, "Files", "Group", "id").IsSuccess)
				{
					deleteSuccess++;
				}
				if (integrationFileClient.IsFileExists(text3, "Files", "Group", "id").Content)
				{
					deleteSuccess--;
				}
			}
			Directory.Delete(text2);
			if (Interlocked.Decrement(ref threadlength) == 0)
			{
				Invoke((Action)delegate
				{
					button7.Enabled = true;
					DemoUtils.ShowMessage("成功上传次数：" + upSuccess + Environment.NewLine + "成功下载次数：" + downSuccess + Environment.NewLine + "成功删除次数：" + deleteSuccess);
				});
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			threadlength = 20;
			upSuccess = 0;
			downSuccess = 0;
			deleteSuccess = 0;
			for (int i = 0; i < threadlength; i++)
			{
				ThreadPool.QueueUserWorkItem(ServerPressureTest2, "B" + i);
			}
			button8.Enabled = false;
		}

		private async void ServerPressureTest2(object obj)
		{
			if (!(obj is string path))
			{
				return;
			}
			string pathNew = Path.Combine(Application.StartupPath, path);
			Directory.CreateDirectory(pathNew);
			for (int j = 0; j < 100; j++)
			{
				string fileName = "A" + random.Next(500) + ".txt";
				string fullName = Path.Combine(pathNew, fileName);
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < 1024; i++)
				{
					sb.Append('1');
				}
				File.WriteAllText(fullName, sb.ToString(), Encoding.UTF8);
				if ((await integrationFileClient.UploadFileAsync(fullName, "Files", "Group", "id", null)).IsSuccess)
				{
					upSuccess++;
				}
				File.Delete(fullName);
				if ((await integrationFileClient.DownloadFileAsync(fileName, "Files", "Group", "id", null, fullName)).IsSuccess)
				{
					downSuccess++;
				}
				File.Delete(fullName);
			}
			Directory.Delete(pathNew);
			if (Interlocked.Decrement(ref threadlength) == 0)
			{
				Invoke((Action)delegate
				{
					button8.Enabled = true;
					DemoUtils.ShowMessage("成功上传次数：" + upSuccess + Environment.NewLine + "成功下载次数：" + downSuccess + Environment.NewLine + "成功删除次数：" + deleteSuccess);
				});
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlToken, textBox15.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlToken).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
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
			System.Windows.Forms.TreeNode treeNode = new System.Windows.Forms.TreeNode("文件列表");
			this.panel1 = new System.Windows.Forms.Panel();
			this.label20 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label19 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.textBox_show_id = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_show_group = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_show_factory = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.label18 = new System.Windows.Forms.Label();
			this.button6 = new System.Windows.Forms.Button();
			this.textBox_file_date = new System.Windows.Forms.TextBox();
			this.label32 = new System.Windows.Forms.Label();
			this.textBox_file_tag = new System.Windows.Forms.TextBox();
			this.label26 = new System.Windows.Forms.Label();
			this.textBox_file_upload = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.textBox_file_dowloadTimes = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.textBox_file_fileSize = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.textBox_file_fileName = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.button13 = new System.Windows.Forms.Button();
			this.label22 = new System.Windows.Forms.Label();
			this.button12 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.textBox_delete_fileName = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_delete_id = new System.Windows.Forms.TextBox();
			this.label_delete_id = new System.Windows.Forms.Label();
			this.textBox_delete_group = new System.Windows.Forms.TextBox();
			this.label_delete_group = new System.Windows.Forms.Label();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox_delete_factory = new System.Windows.Forms.TextBox();
			this.label17 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button10 = new System.Windows.Forms.Button();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_download_fileName = new System.Windows.Forms.TextBox();
			this.label16 = new System.Windows.Forms.Label();
			this.textBox_download_id = new System.Windows.Forms.TextBox();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_download_group = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.progressBar2 = new System.Windows.Forms.ProgressBar();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.textBox_download_factory = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_upload_tag = new System.Windows.Forms.TextBox();
			this.label31 = new System.Windows.Forms.Label();
			this.textBox_upload_name = new System.Windows.Forms.TextBox();
			this.label30 = new System.Windows.Forms.Label();
			this.textBox_upload_id = new System.Windows.Forms.TextBox();
			this.label27 = new System.Windows.Forms.Label();
			this.textBox_upload_group = new System.Windows.Forms.TextBox();
			this.label28 = new System.Windows.Forms.Label();
			this.textBox_upload_factory = new System.Windows.Forms.TextBox();
			this.label29 = new System.Windows.Forms.Label();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.label7 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label20);
			this.panel1.Controls.Add(this.textBox4);
			this.panel1.Controls.Add(this.label19);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 42);
			this.panel1.TabIndex = 7;
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(474, 12);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(24, 17);
			this.label20.TabIndex = 10;
			this.label20.Text = "KB";
			this.textBox4.Location = new System.Drawing.Point(395, 9);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(72, 23);
			this.textBox4.TabIndex = 9;
			this.textBox4.Text = "100";
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(325, 12);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(65, 17);
			this.label19.TabIndex = 8;
			this.label19.Text = "FileCache:";
			this.textBox15.Location = new System.Drawing.Point(552, 9);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(302, 23);
			this.textBox15.TabIndex = 7;
			this.textBox15.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(498, 12);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "令牌：";
			this.button1.Location = new System.Drawing.Point(875, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "初始化";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(244, 9);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(65, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "35002";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(190, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.textBox1.Location = new System.Drawing.Point(62, 9);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(113, 23);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip地址：";
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.groupBox4);
			this.panel2.Controls.Add(this.groupBox3);
			this.panel2.Controls.Add(this.groupBox2);
			this.panel2.Controls.Add(this.groupBox1);
			this.panel2.Enabled = false;
			this.panel2.Location = new System.Drawing.Point(3, 81);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 561);
			this.panel2.TabIndex = 13;
			this.groupBox4.Controls.Add(this.textBox_show_id);
			this.groupBox4.Controls.Add(this.label2);
			this.groupBox4.Controls.Add(this.textBox_show_group);
			this.groupBox4.Controls.Add(this.label4);
			this.groupBox4.Controls.Add(this.textBox_show_factory);
			this.groupBox4.Controls.Add(this.label5);
			this.groupBox4.Controls.Add(this.button8);
			this.groupBox4.Controls.Add(this.button7);
			this.groupBox4.Controls.Add(this.label18);
			this.groupBox4.Controls.Add(this.button6);
			this.groupBox4.Controls.Add(this.textBox_file_date);
			this.groupBox4.Controls.Add(this.label32);
			this.groupBox4.Controls.Add(this.textBox_file_tag);
			this.groupBox4.Controls.Add(this.label26);
			this.groupBox4.Controls.Add(this.textBox_file_upload);
			this.groupBox4.Controls.Add(this.label25);
			this.groupBox4.Controls.Add(this.textBox_file_dowloadTimes);
			this.groupBox4.Controls.Add(this.label24);
			this.groupBox4.Controls.Add(this.textBox_file_fileSize);
			this.groupBox4.Controls.Add(this.label23);
			this.groupBox4.Controls.Add(this.textBox_file_fileName);
			this.groupBox4.Controls.Add(this.label11);
			this.groupBox4.Controls.Add(this.treeView1);
			this.groupBox4.Location = new System.Drawing.Point(3, 289);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(985, 267);
			this.groupBox4.TabIndex = 3;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "浏览服务器文件";
			this.textBox_show_id.Location = new System.Drawing.Point(850, 58);
			this.textBox_show_id.Name = "textBox_show_id";
			this.textBox_show_id.Size = new System.Drawing.Size(84, 23);
			this.textBox_show_id.TabIndex = 41;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(817, 61);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(32, 17);
			this.label2.TabIndex = 40;
			this.label2.Text = "Id：";
			this.textBox_show_group.Location = new System.Drawing.Point(723, 58);
			this.textBox_show_group.Name = "textBox_show_group";
			this.textBox_show_group.Size = new System.Drawing.Size(84, 23);
			this.textBox_show_group.TabIndex = 39;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(667, 61);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(57, 17);
			this.label4.TabIndex = 38;
			this.label4.Text = "Group：";
			this.textBox_show_factory.Location = new System.Drawing.Point(577, 58);
			this.textBox_show_factory.Name = "textBox_show_factory";
			this.textBox_show_factory.Size = new System.Drawing.Size(84, 23);
			this.textBox_show_factory.TabIndex = 37;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(503, 61);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(62, 17);
			this.label5.TabIndex = 36;
			this.label5.Text = "Factory：";
			this.button8.Location = new System.Drawing.Point(424, 211);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(70, 32);
			this.button8.TabIndex = 35;
			this.button8.Text = "单上传";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Location = new System.Drawing.Point(424, 175);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(70, 32);
			this.button7.TabIndex = 34;
			this.button7.Text = "压力测试";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.label18.ForeColor = System.Drawing.Color.Red;
			this.label18.Location = new System.Drawing.Point(509, 14);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(425, 43);
			this.label18.TabIndex = 33;
			this.label18.Text = "注意：当服务器是AFS时，只有文件名和文件大小是有效的，而UFS时，下面数据都是有效的";
			this.button6.Location = new System.Drawing.Point(424, 20);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(70, 28);
			this.button6.TabIndex = 32;
			this.button6.Text = "刷新";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.textBox_file_date.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_file_date.Location = new System.Drawing.Point(576, 189);
			this.textBox_file_date.Name = "textBox_file_date";
			this.textBox_file_date.Size = new System.Drawing.Size(398, 23);
			this.textBox_file_date.TabIndex = 31;
			this.label32.AutoSize = true;
			this.label32.Location = new System.Drawing.Point(502, 192);
			this.label32.Name = "label32";
			this.label32.Size = new System.Drawing.Size(68, 17);
			this.label32.TabIndex = 30;
			this.label32.Text = "上传日期：";
			this.textBox_file_tag.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_file_tag.Location = new System.Drawing.Point(576, 216);
			this.textBox_file_tag.Name = "textBox_file_tag";
			this.textBox_file_tag.Size = new System.Drawing.Size(398, 23);
			this.textBox_file_tag.TabIndex = 29;
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(502, 219);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(44, 17);
			this.label26.TabIndex = 28;
			this.label26.Text = "备注：";
			this.textBox_file_upload.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_file_upload.Location = new System.Drawing.Point(802, 161);
			this.textBox_file_upload.Name = "textBox_file_upload";
			this.textBox_file_upload.Size = new System.Drawing.Size(172, 23);
			this.textBox_file_upload.TabIndex = 27;
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(728, 164);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(56, 17);
			this.label25.TabIndex = 26;
			this.label25.Text = "上传人：";
			this.textBox_file_dowloadTimes.Location = new System.Drawing.Point(576, 161);
			this.textBox_file_dowloadTimes.Name = "textBox_file_dowloadTimes";
			this.textBox_file_dowloadTimes.Size = new System.Drawing.Size(132, 23);
			this.textBox_file_dowloadTimes.TabIndex = 25;
			this.label24.AutoSize = true;
			this.label24.Location = new System.Drawing.Point(502, 164);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(68, 17);
			this.label24.TabIndex = 24;
			this.label24.Text = "下载次数：";
			this.textBox_file_fileSize.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_file_fileSize.Location = new System.Drawing.Point(576, 132);
			this.textBox_file_fileSize.Name = "textBox_file_fileSize";
			this.textBox_file_fileSize.Size = new System.Drawing.Size(398, 23);
			this.textBox_file_fileSize.TabIndex = 23;
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(502, 135);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(68, 17);
			this.label23.TabIndex = 22;
			this.label23.Text = "文件大小：";
			this.textBox_file_fileName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_file_fileName.Location = new System.Drawing.Point(576, 102);
			this.textBox_file_fileName.Name = "textBox_file_fileName";
			this.textBox_file_fileName.Size = new System.Drawing.Size(398, 23);
			this.textBox_file_fileName.TabIndex = 15;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(502, 105);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(68, 17);
			this.label11.TabIndex = 14;
			this.label11.Text = "文件名称：";
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(9, 22);
			this.treeView1.Name = "treeView1";
			treeNode.Name = "节点0";
			treeNode.Text = "文件列表";
			this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[1] { treeNode });
			this.treeView1.Size = new System.Drawing.Size(409, 239);
			this.treeView1.TabIndex = 0;
			this.treeView1.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(treeView1_BeforeExpand);
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Controls.Add(this.button13);
			this.groupBox3.Controls.Add(this.label22);
			this.groupBox3.Controls.Add(this.button12);
			this.groupBox3.Controls.Add(this.button11);
			this.groupBox3.Controls.Add(this.button9);
			this.groupBox3.Controls.Add(this.textBox_delete_fileName);
			this.groupBox3.Controls.Add(this.label8);
			this.groupBox3.Controls.Add(this.textBox_delete_id);
			this.groupBox3.Controls.Add(this.label_delete_id);
			this.groupBox3.Controls.Add(this.textBox_delete_group);
			this.groupBox3.Controls.Add(this.label_delete_group);
			this.groupBox3.Controls.Add(this.button5);
			this.groupBox3.Controls.Add(this.textBox_delete_factory);
			this.groupBox3.Controls.Add(this.label17);
			this.groupBox3.Location = new System.Drawing.Point(3, 213);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(985, 74);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "文件删除";
			this.button13.Location = new System.Drawing.Point(765, 42);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(95, 28);
			this.button13.TabIndex = 22;
			this.button13.Text = "子文件夹信息";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(118, 48);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(15, 17);
			this.label22.TabIndex = 21;
			this.label22.Text = "0";
			this.button12.Location = new System.Drawing.Point(9, 42);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(95, 28);
			this.button12.TabIndex = 20;
			this.button12.Text = "文件夹信息";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button11.Location = new System.Drawing.Point(866, 42);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(78, 28);
			this.button11.TabIndex = 19;
			this.button11.Text = "空目录删除";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button9.Location = new System.Drawing.Point(866, 13);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(78, 28);
			this.button9.TabIndex = 18;
			this.button9.Text = "全部删除";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.textBox_delete_fileName.Location = new System.Drawing.Point(517, 16);
			this.textBox_delete_fileName.Name = "textBox_delete_fileName";
			this.textBox_delete_fileName.Size = new System.Drawing.Size(267, 23);
			this.textBox_delete_fileName.TabIndex = 17;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(443, 19);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(56, 17);
			this.label8.TabIndex = 16;
			this.label8.Text = "文件名：";
			this.textBox_delete_id.Location = new System.Drawing.Point(353, 16);
			this.textBox_delete_id.Name = "textBox_delete_id";
			this.textBox_delete_id.Size = new System.Drawing.Size(84, 23);
			this.textBox_delete_id.TabIndex = 15;
			this.textBox_delete_id.Text = "Admin";
			this.label_delete_id.AutoSize = true;
			this.label_delete_id.Location = new System.Drawing.Point(320, 19);
			this.label_delete_id.Name = "label_delete_id";
			this.label_delete_id.Size = new System.Drawing.Size(32, 17);
			this.label_delete_id.TabIndex = 14;
			this.label_delete_id.Text = "Id：";
			this.textBox_delete_group.Location = new System.Drawing.Point(226, 16);
			this.textBox_delete_group.Name = "textBox_delete_group";
			this.textBox_delete_group.Size = new System.Drawing.Size(84, 23);
			this.textBox_delete_group.TabIndex = 13;
			this.textBox_delete_group.Text = "Personal";
			this.label_delete_group.AutoSize = true;
			this.label_delete_group.Location = new System.Drawing.Point(170, 19);
			this.label_delete_group.Name = "label_delete_group";
			this.label_delete_group.Size = new System.Drawing.Size(57, 17);
			this.label_delete_group.TabIndex = 12;
			this.label_delete_group.Text = "Group：";
			this.button5.Location = new System.Drawing.Point(790, 13);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(70, 28);
			this.button5.TabIndex = 7;
			this.button5.Text = "删除";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox_delete_factory.Location = new System.Drawing.Point(80, 16);
			this.textBox_delete_factory.Name = "textBox_delete_factory";
			this.textBox_delete_factory.Size = new System.Drawing.Size(84, 23);
			this.textBox_delete_factory.TabIndex = 5;
			this.textBox_delete_factory.Text = "Files";
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(6, 19);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(62, 17);
			this.label17.TabIndex = 4;
			this.label17.Text = "Factory：";
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.button10);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox_download_fileName);
			this.groupBox2.Controls.Add(this.label16);
			this.groupBox2.Controls.Add(this.textBox_download_id);
			this.groupBox2.Controls.Add(this.label15);
			this.groupBox2.Controls.Add(this.textBox_download_group);
			this.groupBox2.Controls.Add(this.label14);
			this.groupBox2.Controls.Add(this.progressBar2);
			this.groupBox2.Controls.Add(this.label12);
			this.groupBox2.Controls.Add(this.button4);
			this.groupBox2.Controls.Add(this.textBox_download_factory);
			this.groupBox2.Controls.Add(this.label13);
			this.groupBox2.Location = new System.Drawing.Point(3, 125);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(985, 86);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "文件下载";
			this.button10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button10.Location = new System.Drawing.Point(904, 52);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(70, 28);
			this.button10.TabIndex = 19;
			this.button10.Text = "是否存在";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.label9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(770, 56);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(27, 17);
			this.label9.TabIndex = 18;
			this.label9.Text = "0/0";
			this.textBox_download_fileName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_download_fileName.Location = new System.Drawing.Point(517, 25);
			this.textBox_download_fileName.Name = "textBox_download_fileName";
			this.textBox_download_fileName.Size = new System.Drawing.Size(381, 23);
			this.textBox_download_fileName.TabIndex = 17;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(443, 28);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(56, 17);
			this.label16.TabIndex = 16;
			this.label16.Text = "文件名：";
			this.textBox_download_id.Location = new System.Drawing.Point(353, 25);
			this.textBox_download_id.Name = "textBox_download_id";
			this.textBox_download_id.Size = new System.Drawing.Size(84, 23);
			this.textBox_download_id.TabIndex = 15;
			this.textBox_download_id.Text = "Admin";
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(320, 28);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(32, 17);
			this.label15.TabIndex = 14;
			this.label15.Text = "Id：";
			this.textBox_download_group.Location = new System.Drawing.Point(226, 25);
			this.textBox_download_group.Name = "textBox_download_group";
			this.textBox_download_group.Size = new System.Drawing.Size(84, 23);
			this.textBox_download_group.TabIndex = 13;
			this.textBox_download_group.Text = "Personal";
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(170, 28);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(57, 17);
			this.label14.TabIndex = 12;
			this.label14.Text = "Group：";
			this.progressBar2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.progressBar2.Location = new System.Drawing.Point(80, 56);
			this.progressBar2.Name = "progressBar2";
			this.progressBar2.Size = new System.Drawing.Size(677, 17);
			this.progressBar2.TabIndex = 9;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(6, 56);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 8;
			this.label12.Text = "进度：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(904, 22);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(70, 28);
			this.button4.TabIndex = 7;
			this.button4.Text = "下载";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.textBox_download_factory.Location = new System.Drawing.Point(80, 25);
			this.textBox_download_factory.Name = "textBox_download_factory";
			this.textBox_download_factory.Size = new System.Drawing.Size(84, 23);
			this.textBox_download_factory.TabIndex = 5;
			this.textBox_download_factory.Text = "Files";
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(6, 28);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(62, 17);
			this.label13.TabIndex = 4;
			this.label13.Text = "Factory：";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.label10);
			this.groupBox1.Controls.Add(this.textBox_upload_tag);
			this.groupBox1.Controls.Add(this.label31);
			this.groupBox1.Controls.Add(this.textBox_upload_name);
			this.groupBox1.Controls.Add(this.label30);
			this.groupBox1.Controls.Add(this.textBox_upload_id);
			this.groupBox1.Controls.Add(this.label27);
			this.groupBox1.Controls.Add(this.textBox_upload_group);
			this.groupBox1.Controls.Add(this.label28);
			this.groupBox1.Controls.Add(this.textBox_upload_factory);
			this.groupBox1.Controls.Add(this.label29);
			this.groupBox1.Controls.Add(this.progressBar1);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.button3);
			this.groupBox1.Controls.Add(this.button2);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(985, 117);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "文件上传";
			this.label10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(770, 87);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(27, 17);
			this.label10.TabIndex = 26;
			this.label10.Text = "0/0";
			this.textBox_upload_tag.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_upload_tag.Location = new System.Drawing.Point(709, 56);
			this.textBox_upload_tag.Name = "textBox_upload_tag";
			this.textBox_upload_tag.Size = new System.Drawing.Size(270, 23);
			this.textBox_upload_tag.TabIndex = 25;
			this.textBox_upload_tag.Text = "test test for example it is import";
			this.label31.AutoSize = true;
			this.label31.Location = new System.Drawing.Point(669, 59);
			this.label31.Name = "label31";
			this.label31.Size = new System.Drawing.Size(44, 17);
			this.label31.TabIndex = 24;
			this.label31.Text = "备注：";
			this.textBox_upload_name.Location = new System.Drawing.Point(578, 56);
			this.textBox_upload_name.Name = "textBox_upload_name";
			this.textBox_upload_name.Size = new System.Drawing.Size(84, 23);
			this.textBox_upload_name.TabIndex = 23;
			this.textBox_upload_name.Text = "you name";
			this.label30.AutoSize = true;
			this.label30.Location = new System.Drawing.Point(521, 59);
			this.label30.Name = "label30";
			this.label30.Size = new System.Drawing.Size(56, 17);
			this.label30.TabIndex = 22;
			this.label30.Text = "上传人：";
			this.textBox_upload_id.Location = new System.Drawing.Point(424, 56);
			this.textBox_upload_id.Name = "textBox_upload_id";
			this.textBox_upload_id.Size = new System.Drawing.Size(84, 23);
			this.textBox_upload_id.TabIndex = 21;
			this.textBox_upload_id.Text = "Admin";
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(391, 59);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(32, 17);
			this.label27.TabIndex = 20;
			this.label27.Text = "Id：";
			this.textBox_upload_group.Location = new System.Drawing.Point(297, 56);
			this.textBox_upload_group.Name = "textBox_upload_group";
			this.textBox_upload_group.Size = new System.Drawing.Size(84, 23);
			this.textBox_upload_group.TabIndex = 19;
			this.textBox_upload_group.Text = "Personal";
			this.label28.AutoSize = true;
			this.label28.Location = new System.Drawing.Point(241, 59);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(57, 17);
			this.label28.TabIndex = 18;
			this.label28.Text = "Group：";
			this.textBox_upload_factory.Location = new System.Drawing.Point(151, 56);
			this.textBox_upload_factory.Name = "textBox_upload_factory";
			this.textBox_upload_factory.Size = new System.Drawing.Size(84, 23);
			this.textBox_upload_factory.TabIndex = 17;
			this.textBox_upload_factory.Text = "Files";
			this.label29.AutoSize = true;
			this.label29.Location = new System.Drawing.Point(77, 59);
			this.label29.Name = "label29";
			this.label29.Size = new System.Drawing.Size(62, 17);
			this.label29.TabIndex = 16;
			this.label29.Text = "Factory：";
			this.progressBar1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.progressBar1.Location = new System.Drawing.Point(80, 87);
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(677, 17);
			this.progressBar1.TabIndex = 9;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 87);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 8;
			this.label7.Text = "进度：";
			this.button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button3.Location = new System.Drawing.Point(904, 22);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(70, 28);
			this.button3.TabIndex = 7;
			this.button3.Text = "上传";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Location = new System.Drawing.Point(828, 22);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(70, 28);
			this.button2.TabIndex = 6;
			this.button2.Text = "选择";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(80, 25);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(735, 23);
			this.textBox3.TabIndex = 5;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(6, 28);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 4;
			this.label6.Text = "文件路径：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/7746113.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Hsl - File";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormFileClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "文件客户端窗口";
			base.Load += new System.EventHandler(FormFileClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
