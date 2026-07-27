using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;
using HslControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormMqttSyncClient : HslFormContent
	{
		private MqttSyncClient mqttSyncClient;

		private CodeExampleControl codeExampleControl;

		private MqttRpcApiInfo[] mqttRpcApiInfos;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private Button button3;

		private TextBox textBox4;

		private Label label9;

		private TextBox textBox5;

		private Label label7;

		private UserControlHead userControlHead1;

		private TextBox textBox10;

		private Label label4;

		private TextBox textBox9;

		private Label label2;

		private TextBox textBox3;

		private Label label6;

		private RadioButton radioButton_json;

		private RadioButton radioButton_xml;

		private RadioButton radioButton_text;

		private Panel panel3;

		private TextBox textBox7;

		private TextBox textBox6;

		private HslProgress hslProgress1;

		private Panel panel4;

		private TreeView treeView1;

		private Label label10;

		private Button button7;

		private TextBox textBox13;

		private Label label14;

		private TextBox textBox12;

		private Label label13;

		private Label label11;

		private Button button8;

		private Label label5;

		private Panel panel5;

		private TextBox textBox14;

		private Label label17;

		private HslProgress hslProgress3;

		private Panel panel6;

		private RadioButton radioButton1;

		private RadioButton radioButton2;

		private RadioButton radioButton6;

		private TextBox textBox17;

		private Label label19;

		private Button button12;

		private TextBox textBox19;

		private Label label21;

		private Label label22;

		private Label label8;

		private TextBox textBox16;

		private Label label16;

		private Label label18;

		private Panel panel7;

		private HslBarChart hslBarChart1;

		private CheckBox checkBox_rsa;

		private Label label23;

		private TextBox textBox_api_description;

		private Label label15;

		private Label label24;

		private TextBox textBox_api_sign;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private TabPage tabPage3;

		private TextBox textBox_session_willTopic;

		private Label label20;

		private TextBox textBox_session_userName;

		private Label label25;

		private TextBox textBox_session_topics;

		private Label label26;

		private TextBox textBox_session_rsa;

		private Label label27;

		private TextBox textBox_session_clientID;

		private Label label28;

		private TextBox textBox_session_ip;

		private Label label30;

		private TextBox textBox_session_onlineTime;

		private Label label31;

		private TextBox textBox_session_activeTime;

		private Label label29;

		private TextBox textBox_session_developer;

		private Label label32;

		private Label label33;

		private TextBox textBox_session_forbid;

		private Label label34;

		private Label label35;

		private Label label_info;

		private TextBox textBox_session_protocol;

		private Label label36;

		private PipeSelectControl pipeSelectControl1;

		public FormMqttSyncClient()
		{
			InitializeComponent();
			treeView1.AfterSelect += TreeView1_AfterSelect;
			treeView1.NodeMouseDoubleClick += TreeView1_NodeMouseDoubleClick;
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			panel4.Enabled = false;
			button2.Enabled = false;
			Language(Program.Language);
			ImageList imageList = new ImageList();
			imageList.Images.Add("VirtualMachine", Resources.VirtualMachine);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("Module_648", Resources.Module_648);
			imageList.Images.Add("Structure_507", Resources.Structure_507);
			imageList.Images.Add("server_info", Resources.server_info);
			treeView1.ImageList = imageList;
			treeView1.Nodes[0].ImageKey = "VirtualMachine";
			treeView1.Nodes[0].SelectedImageKey = "VirtualMachine";
			treeView1.Nodes[1].ImageKey = "VirtualMachine";
			treeView1.Nodes[1].SelectedImageKey = "VirtualMachine";
			treeView1.Nodes[2].ImageKey = "VirtualMachine";
			treeView1.Nodes[2].SelectedImageKey = "VirtualMachine";
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void Language(int language)
		{
			if (language != 1)
			{
				Text = "Mqtt Sync Client [RPC remote call client]";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label7.Text = "Topic:";
				label9.Text = "Payload:";
				button3.Text = "Read";
				button4.Text = "Clear";
				label12.Text = "R-Topic:";
				label5.Text = "R-Payload:";
				button3.Text = "Read";
				hslProgress1.TextRenderFormat = "Send {0}%";
				label6.Text = "Client ID:";
				label2.Text = "UserName:";
				label4.Text = "Password:";
				label11.Text = "Spend-Time:";
				button8.Text = "Refresh";
				label24.Text = "[Sign]";
				label15.Text = "[Note]";
			}
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			try
			{
				MqttConnectionOptions options = new MqttConnectionOptions
				{
					IpAddress = pipeSelectControl1.GetTcpIpAddress(),
					Port = int.Parse(pipeSelectControl1.GetTcpPort()),
					ClientId = textBox3.Text,
					UseRSAProvider = checkBox_rsa.Checked
				};
				if (!string.IsNullOrEmpty(textBox9.Text) || !string.IsNullOrEmpty(textBox10.Text))
				{
					options.Credentials = new MqttCredential(textBox9.Text, textBox10.Text);
				}
				button1.Enabled = false;
				mqttSyncClient = new MqttSyncClient(options);
				pipeSelectControl1.IniPipe(mqttSyncClient);
				OperateResult connect = await mqttSyncClient.ConnectServerAsync();
				if (connect.IsSuccess)
				{
					panel2.Enabled = true;
					button1.Enabled = false;
					button2.Enabled = true;
					panel4.Enabled = true;
					panel2.Enabled = true;
					MqttRpcApiRefresh(treeView1.Nodes[0]);
					TopicsRefresh(treeView1.Nodes[1]);
					SessionsRefresh(treeView1.Nodes[2]);
					DemoUtils.ShowMessage(StringResources.Language.ConnectServerSuccess);
					codeExampleControl.SetCodeText(mqttSyncClient);
				}
				else
				{
					button1.Enabled = true;
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + connect.Message);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			panel4.Enabled = false;
			mqttSyncClient?.ConnectClose();
		}

		private void ProgressReport(long already, long total)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<long, long>(ProgressReport), already, total);
				return;
			}
			hslProgress1.Value = (int)(already * 100 / total);
			if (already == total)
			{
				hslProgress1.TextRenderFormat = "Receive {0}%";
			}
		}

		private void ReceiveProgressReport(long already, long total)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<long, long>(ReceiveProgressReport), already, total);
			}
			else
			{
				hslProgress1.Value = (int)(already * 100 / total);
			}
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			hslProgress1.Value = 0;
			hslProgress1.TextRenderFormat = "Send {0}%";
			DateTime start = DateTime.Now;
			button3.Enabled = false;
			OperateResult<string, byte[]> read = await mqttSyncClient.ReadAsync(textBox5.Text, Encoding.UTF8.GetBytes(textBox4.Text), ProgressReport, null, ProgressReport);
			button3.Enabled = true;
			textBox7.Text = (int)(DateTime.Now - start).TotalMilliseconds + " ms";
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Failed:" + read.ToMessageShowString());
				return;
			}
			if (mqttRpcApiInfos != null)
			{
				MqttRpcApiInfo api = mqttRpcApiInfos.FirstOrDefault((MqttRpcApiInfo m) => m.ApiTopic == textBox5.Text);
				if (api != null)
				{
					api.ExamplePayload = textBox4.Text;
				}
			}
			textBox6.Text = read.Content1;
			string msg = Encoding.UTF8.GetString(read.Content2);
			if (radioButton_xml.Checked)
			{
				try
				{
					msg = XElement.Parse(msg).ToString();
				}
				catch
				{
				}
			}
			else if (radioButton_json.Checked)
			{
				try
				{
					msg = ((msg.StartsWith("\"{") && msg.EndsWith("}\"")) ? JObject.Parse(JsonConvert.DeserializeObject<string>(msg)).ToString() : ((!msg.StartsWith("\"[") || !msg.EndsWith("]\"")) ? JObject.Parse(msg).ToString() : JArray.Parse(JsonConvert.DeserializeObject<string>(msg)).ToString()));
				}
				catch
				{
				}
			}
			textBox8.Text = msg;
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult<MqttRpcApiInfo[]> operateResult = mqttSyncClient.ReadRpcApis();
			if (operateResult.IsSuccess)
			{
				textBox8.Text = operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed:" + operateResult.Message);
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			MqttRpcApiRefresh(treeView1.Nodes[0]);
			TopicsRefresh(treeView1.Nodes[1]);
			SessionsRefresh(treeView1.Nodes[2]);
		}

		public void UpdateMqttTopicMessage(MqttClientApplicationMessage message)
		{
			textBox19.Text = message.ClientId;
			textBox14.Text = message.UserName;
			label18.Text = message.CreateTime.ToString();
			textBox16.Text = message.Topic;
			label8.Text = message.QualityOfServiceLevel.ToString();
			string text = Encoding.UTF8.GetString(message.Payload);
			if (radioButton6.Checked)
			{
				try
				{
					text = XElement.Parse(text).ToString();
				}
				catch
				{
				}
			}
			else if (radioButton1.Checked)
			{
				try
				{
					text = ((!text.StartsWith("\"{") || !text.EndsWith("}\"")) ? JObject.Parse(text).ToString() : JObject.Parse(JsonConvert.DeserializeObject<string>(text)).ToString());
				}
				catch
				{
				}
			}
			textBox17.Text = ((text != null && text.Length > 10000) ? (text.Substring(0, 10000) + "...") : text);
		}

		private void MqttRpcApiRefresh(TreeNode rootNode)
		{
			rootNode.Nodes.Clear();
			OperateResult<MqttRpcApiInfo[]> operateResult = mqttSyncClient.ReadRpcApis();
			if (operateResult.IsSuccess)
			{
				mqttRpcApiInfos = operateResult.Content;
				MqttRpcApiInfo[] content = operateResult.Content;
				foreach (MqttRpcApiInfo mqttRpcApiInfo in content)
				{
					AddTreeNode(rootNode, mqttRpcApiInfo.ApiTopic, mqttRpcApiInfo.ApiTopic, mqttRpcApiInfo);
				}
				rootNode.Expand();
			}
		}

		private void SessionsRefresh(TreeNode rootNode)
		{
			rootNode.Nodes.Clear();
			OperateResult<MqttSessionInfo[]> operateResult = mqttSyncClient.ReadSessions();
			if (!operateResult.IsSuccess)
			{
				label_info.Text = "MqttServer not support Session API";
				return;
			}
			label_info.Text = "Session Count: " + operateResult.Content.Length;
			MqttSessionInfo[] content = operateResult.Content;
			foreach (MqttSessionInfo mqttSessionInfo in content)
			{
				TreeNode treeNode = new TreeNode(mqttSessionInfo.EndPoint.ToString() ?? "");
				treeNode.Tag = mqttSessionInfo;
				treeNode.ImageKey = "server_info";
				treeNode.SelectedImageKey = "server_info";
				rootNode.Nodes.Add(treeNode);
			}
			rootNode.Expand();
		}

		private void TopicsRefresh(TreeNode rootNode)
		{
			rootNode.Nodes.Clear();
			OperateResult<string[]> operateResult = mqttSyncClient.ReadRetainTopics();
			if (operateResult.IsSuccess)
			{
				string[] content = operateResult.Content;
				foreach (string text in content)
				{
					AddTopicTreeNode(rootNode, text, text);
				}
				rootNode.Expand();
			}
		}

		private void AddTopicTreeNode(TreeNode parent, string key, string infactKey)
		{
			int num = key.IndexOf('/');
			if (num <= 0)
			{
				TreeNode treeNode = new TreeNode(key ?? "");
				treeNode.Tag = infactKey;
				treeNode.ImageKey = "Enum_582";
				treeNode.SelectedImageKey = "Enum_582";
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
				AddTopicTreeNode(treeNode2, key.Substring(num + 1), infactKey);
				parent.Nodes.Add(treeNode2);
			}
			else
			{
				AddTopicTreeNode(treeNode2, key.Substring(num + 1), infactKey);
			}
		}

		private void AddTreeNode(TreeNode parent, string key, string infactKey, MqttRpcApiInfo mqttRpc)
		{
			int num = key.IndexOf('/');
			if (num <= 0)
			{
				TreeNode treeNode = new TreeNode(key ?? "");
				treeNode.Tag = mqttRpc;
				treeNode.ImageKey = (mqttRpc.IsMethodApi ? "Method_636" : "Enum_582");
				treeNode.SelectedImageKey = (mqttRpc.IsMethodApi ? "Method_636" : "Enum_582");
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
				AddTreeNode(treeNode2, key.Substring(num + 1), infactKey, mqttRpc);
				parent.Nodes.Add(treeNode2);
			}
			else
			{
				AddTreeNode(treeNode2, key.Substring(num + 1), infactKey, mqttRpc);
			}
		}

		private async void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node == null)
			{
				return;
			}
			if (node.SelectedImageKey == "VirtualMachine")
			{
				if (node.Text == "Rpc Apis")
				{
					tabControl1.SelectTab(tabPage1);
				}
				else if (node.Text == "Topics")
				{
					tabControl1.SelectTab(tabPage2);
				}
				else if (node.Text == "Sessions")
				{
					tabControl1.SelectTab(tabPage3);
				}
			}
			else if (node.Tag is MqttRpcApiInfo apiInfo)
			{
				tabControl1.SelectTab(tabPage1);
				textBox5.Text = apiInfo.ApiTopic;
				textBox4.Text = apiInfo.ExamplePayload;
				textBox12.Text = apiInfo.CalledCount.ToString();
				textBox13.Text = apiInfo.SpendTotalTime.ToString("F2");
				textBox_api_description.Text = apiInfo.Description;
				textBox_api_sign.Text = apiInfo.MethodSignature;
				OperateResult<long[]> read = mqttSyncClient.ReadRpcApiLog(apiInfo.ApiTopic);
				if (read.IsSuccess)
				{
					long[] data = read.Content.SelectLast(7);
					int[] ydata = new int[7];
					string[] xdata = new string[7];
					for (int i = 0; i < 7; i++)
					{
						ydata[i] = (int)data[i];
						xdata[i] = DateTime.Now.AddDays(i - 6).ToString("M-d");
						hslBarChart1.SetDataSource(ydata, xdata);
					}
				}
				else
				{
					hslBarChart1.SetDataSource(new int[7]);
				}
			}
			else if (node.Tag is string topic)
			{
				tabControl1.SelectTab(tabPage2);
				hslProgress3.Value = 0;
				OperateResult<MqttClientApplicationMessage> message = await mqttSyncClient.ReadTopicPayloadAsync(topic, ReceiveTopicProgressReport);
				if (!message.IsSuccess)
				{
					DemoUtils.ShowMessage("Failed: " + message.Message);
				}
				else
				{
					UpdateMqttTopicMessage(message.Content);
				}
			}
			else if (node.Tag is MqttSessionInfo session)
			{
				if (tabControl1.SelectedTab != tabPage3)
				{
					tabControl1.SelectedTab = tabPage3;
				}
				textBox_session_onlineTime.Text = session.OnlineTime.ToString(DemoUtils.DateTimeFormate);
				textBox_session_activeTime.Text = session.ActiveTime.ToString(DemoUtils.DateTimeFormate);
				textBox_session_ip.Text = session.EndPoint;
				textBox_session_protocol.Text = session.Protocol;
				textBox_session_developer.Text = session.DeveloperPermissions.ToString();
				textBox_session_forbid.Text = session.ForbidPublishTopic.ToString();
				textBox_session_clientID.Text = session.ClientId;
				textBox_session_rsa.Text = session.IsAesCryptography.ToString();
				textBox_session_userName.Text = session.UserName;
				textBox_session_willTopic.Text = session.WillTopic;
				textBox_session_topics.Lines = session.Topics;
			}
		}

		private void TreeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			TreeNode node = e.Node;
			if (node.SelectedImageKey == "VirtualMachine")
			{
				if (node.Text == "Sessions")
				{
					SessionsRefresh(treeView1.Nodes[2]);
					treeView1.Nodes[2].ExpandAll();
				}
				else if (node.Text == "Topics")
				{
					TopicsRefresh(treeView1.Nodes[1]);
					treeView1.Nodes[1].ExpandAll();
				}
			}
		}

		private void ReceiveTopicProgressReport(long already, long total)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<long, long>(ReceiveProgressReport), already, total);
			}
			else
			{
				hslProgress3.Value = (int)(already * 100 / total);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlCompanyID, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox9.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox10.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox3.Text = element.Attribute(DemoDeviceList.XmlCompanyID).Value;
			textBox9.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox10.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormMqttSyncClient_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
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
			System.Windows.Forms.TreeNode treeNode = new System.Windows.Forms.TreeNode("Rpc Apis");
			System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("Topics");
			System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("Sessions");
			this.panel1 = new System.Windows.Forms.Panel();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.checkBox_rsa = new System.Windows.Forms.CheckBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox_api_sign = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_api_description = new System.Windows.Forms.TextBox();
			this.hslBarChart1 = new HslControls.HslBarChart();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.hslProgress1 = new HslControls.HslProgress();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_json = new System.Windows.Forms.RadioButton();
			this.radioButton_text = new System.Windows.Forms.RadioButton();
			this.radioButton_xml = new System.Windows.Forms.RadioButton();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label23 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel4 = new System.Windows.Forms.Panel();
			this.button8 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.label10 = new System.Windows.Forms.Label();
			this.panel5 = new System.Windows.Forms.Panel();
			this.label18 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.label17 = new System.Windows.Forms.Label();
			this.hslProgress3 = new HslControls.HslProgress();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.panel6 = new System.Windows.Forms.Panel();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton6 = new System.Windows.Forms.RadioButton();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.label19 = new System.Windows.Forms.Label();
			this.button12 = new System.Windows.Forms.Button();
			this.textBox19 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.label22 = new System.Windows.Forms.Label();
			this.panel7 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.textBox_session_protocol = new System.Windows.Forms.TextBox();
			this.label36 = new System.Windows.Forms.Label();
			this.label_info = new System.Windows.Forms.Label();
			this.label35 = new System.Windows.Forms.Label();
			this.textBox_session_forbid = new System.Windows.Forms.TextBox();
			this.label34 = new System.Windows.Forms.Label();
			this.label33 = new System.Windows.Forms.Label();
			this.textBox_session_developer = new System.Windows.Forms.TextBox();
			this.label32 = new System.Windows.Forms.Label();
			this.textBox_session_activeTime = new System.Windows.Forms.TextBox();
			this.label29 = new System.Windows.Forms.Label();
			this.textBox_session_willTopic = new System.Windows.Forms.TextBox();
			this.label20 = new System.Windows.Forms.Label();
			this.textBox_session_userName = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.textBox_session_topics = new System.Windows.Forms.TextBox();
			this.label26 = new System.Windows.Forms.Label();
			this.textBox_session_rsa = new System.Windows.Forms.TextBox();
			this.label27 = new System.Windows.Forms.Label();
			this.textBox_session_clientID = new System.Windows.Forms.TextBox();
			this.label28 = new System.Windows.Forms.Label();
			this.textBox_session_ip = new System.Windows.Forms.TextBox();
			this.label30 = new System.Windows.Forms.Label();
			this.textBox_session_onlineTime = new System.Windows.Forms.TextBox();
			this.label31 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel4.SuspendLayout();
			this.panel5.SuspendLayout();
			this.panel6.SuspendLayout();
			this.panel7.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.checkBox_rsa);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.textBox10);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox9);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1101, 68);
			this.panel1.TabIndex = 7;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(3, 1);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 29);
			this.pipeSelectControl1.TabIndex = 19;
			this.pipeSelectControl1.TcpPortText = "1883";
			this.pipeSelectControl1.UdpPortText = "1883";
			this.checkBox_rsa.AutoSize = true;
			this.checkBox_rsa.Location = new System.Drawing.Point(864, 36);
			this.checkBox_rsa.Name = "checkBox_rsa";
			this.checkBox_rsa.Size = new System.Drawing.Size(135, 21);
			this.checkBox_rsa.TabIndex = 18;
			this.checkBox_rsa.Text = "RSA加密 (v10.2.0+)";
			this.checkBox_rsa.UseVisualStyleBackColor = true;
			this.textBox3.Location = new System.Drawing.Point(86, 35);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(382, 23);
			this.textBox3.TabIndex = 17;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(4, 38);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(80, 17);
			this.label6.TabIndex = 16;
			this.label6.Text = "客户端标识：";
			this.textBox10.Location = new System.Drawing.Point(738, 35);
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(113, 23);
			this.textBox10.TabIndex = 13;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(674, 39);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 12;
			this.label4.Text = "密码：";
			this.textBox9.Location = new System.Drawing.Point(557, 36);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(105, 23);
			this.textBox9.TabIndex = 11;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(481, 39);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(56, 17);
			this.label2.TabIndex = 10;
			this.label2.Text = "用户名：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(900, 1);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(99, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(799, 2);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_api_sign);
			this.panel2.Controls.Add(this.label24);
			this.panel2.Controls.Add(this.label15);
			this.panel2.Controls.Add(this.textBox_api_description);
			this.panel2.Controls.Add(this.hslBarChart1);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.textBox13);
			this.panel2.Controls.Add(this.label14);
			this.panel2.Controls.Add(this.textBox12);
			this.panel2.Controls.Add(this.label13);
			this.panel2.Controls.Add(this.hslProgress1);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.textBox6);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox4);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.label23);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Margin = new System.Windows.Forms.Padding(0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(822, 485);
			this.panel2.TabIndex = 13;
			this.textBox_api_sign.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_api_sign.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox_api_sign.ForeColor = System.Drawing.Color.Gray;
			this.textBox_api_sign.Location = new System.Drawing.Point(62, 39);
			this.textBox_api_sign.Name = "textBox_api_sign";
			this.textBox_api_sign.ReadOnly = true;
			this.textBox_api_sign.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_api_sign.Size = new System.Drawing.Size(755, 16);
			this.textBox_api_sign.TabIndex = 52;
			this.textBox_api_sign.Text = "[签名]";
			this.label24.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label24.AutoSize = true;
			this.label24.ForeColor = System.Drawing.Color.Gray;
			this.label24.Location = new System.Drawing.Point(0, 38);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(40, 17);
			this.label24.TabIndex = 51;
			this.label24.Text = "[签名]";
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label15.AutoSize = true;
			this.label15.ForeColor = System.Drawing.Color.Gray;
			this.label15.Location = new System.Drawing.Point(0, 60);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(40, 17);
			this.label15.TabIndex = 50;
			this.label15.Text = "[注释]";
			this.textBox_api_description.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_api_description.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox_api_description.ForeColor = System.Drawing.Color.Gray;
			this.textBox_api_description.Location = new System.Drawing.Point(62, 60);
			this.textBox_api_description.Multiline = true;
			this.textBox_api_description.Name = "textBox_api_description";
			this.textBox_api_description.ReadOnly = true;
			this.textBox_api_description.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_api_description.Size = new System.Drawing.Size(755, 56);
			this.textBox_api_description.TabIndex = 49;
			this.hslBarChart1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.hslBarChart1.BackColor = System.Drawing.Color.White;
			this.hslBarChart1.Location = new System.Drawing.Point(488, 122);
			this.hslBarChart1.Name = "hslBarChart1";
			this.hslBarChart1.ShowBarValueFormat = "{0}";
			this.hslBarChart1.Size = new System.Drawing.Size(329, 166);
			this.hslBarChart1.TabIndex = 46;
			this.hslBarChart1.Text = "hslBarChart1";
			this.hslBarChart1.Title = "Called Infomation";
			this.hslBarChart1.UseGradient = true;
			this.button7.Location = new System.Drawing.Point(138, 266);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(89, 28);
			this.button7.TabIndex = 40;
			this.button7.Text = "Apis Info";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.textBox13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox13.Location = new System.Drawing.Point(726, 8);
			this.textBox13.Name = "textBox13";
			this.textBox13.ReadOnly = true;
			this.textBox13.Size = new System.Drawing.Size(91, 23);
			this.textBox13.TabIndex = 39;
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(603, 11);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(113, 17);
			this.label14.TabIndex = 38;
			this.label14.Text = "Spend Total Time:";
			this.textBox12.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox12.Location = new System.Drawing.Point(500, 8);
			this.textBox12.Name = "textBox12";
			this.textBox12.ReadOnly = true;
			this.textBox12.Size = new System.Drawing.Size(91, 23);
			this.textBox12.TabIndex = 37;
			this.label13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(408, 11);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(85, 17);
			this.label13.TabIndex = 36;
			this.label13.Text = "Called Count:";
			this.hslProgress1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslProgress1.BackColor = System.Drawing.Color.Gainsboro;
			this.hslProgress1.Location = new System.Drawing.Point(62, 242);
			this.hslProgress1.Name = "hslProgress1";
			this.hslProgress1.ProgressColor = System.Drawing.Color.FromArgb(128, 255, 128);
			this.hslProgress1.ProgressStyle = HslControls.HslProgressStyle.Horizontal;
			this.hslProgress1.Size = new System.Drawing.Size(420, 18);
			this.hslProgress1.TabIndex = 32;
			this.hslProgress1.TextRenderFormat = "已发送 {0}%";
			this.hslProgress1.Value = 0;
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox7.Location = new System.Drawing.Point(296, 269);
			this.textBox7.Name = "textBox7";
			this.textBox7.ReadOnly = true;
			this.textBox7.Size = new System.Drawing.Size(186, 23);
			this.textBox7.TabIndex = 31;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(235, 272);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 30;
			this.label11.Text = "耗时：";
			this.textBox6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox6.Location = new System.Drawing.Point(62, 298);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(420, 23);
			this.textBox6.TabIndex = 29;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.radioButton_json);
			this.panel3.Controls.Add(this.radioButton_text);
			this.panel3.Controls.Add(this.radioButton_xml);
			this.panel3.Location = new System.Drawing.Point(486, 294);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(254, 28);
			this.panel3.TabIndex = 26;
			this.radioButton_json.AutoSize = true;
			this.radioButton_json.Location = new System.Drawing.Point(115, 3);
			this.radioButton_json.Name = "radioButton_json";
			this.radioButton_json.Size = new System.Drawing.Size(52, 21);
			this.radioButton_json.TabIndex = 28;
			this.radioButton_json.Text = "Json";
			this.radioButton_json.UseVisualStyleBackColor = true;
			this.radioButton_text.AutoSize = true;
			this.radioButton_text.Checked = true;
			this.radioButton_text.Location = new System.Drawing.Point(7, 3);
			this.radioButton_text.Name = "radioButton_text";
			this.radioButton_text.Size = new System.Drawing.Size(50, 21);
			this.radioButton_text.TabIndex = 26;
			this.radioButton_text.TabStop = true;
			this.radioButton_text.Text = "Text";
			this.radioButton_text.UseVisualStyleBackColor = true;
			this.radioButton_xml.AutoSize = true;
			this.radioButton_xml.Location = new System.Drawing.Point(61, 3);
			this.radioButton_xml.Name = "radioButton_xml";
			this.radioButton_xml.Size = new System.Drawing.Size(48, 21);
			this.radioButton_xml.TabIndex = 27;
			this.radioButton_xml.Text = "Xml";
			this.radioButton_xml.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 327);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(755, 153);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(0, 301);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(68, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "接收主题：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(748, 294);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(71, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(62, 266);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(70, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(62, 122);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(420, 115);
			this.textBox4.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(0, 125);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(68, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "上传数据：";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(62, 7);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(340, 23);
			this.textBox5.TabIndex = 9;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(0, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "主题：";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(0, 331);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 42;
			this.label5.Text = "接收数据：";
			this.label23.AutoSize = true;
			this.label23.ForeColor = System.Drawing.Color.Gray;
			this.label23.Location = new System.Drawing.Point(3, 243);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(44, 17);
			this.label23.TabIndex = 48;
			this.label23.Text = "进度：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/11631894.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MQTT RPC";
			this.userControlHead1.Size = new System.Drawing.Size(1108, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel4.Controls.Add(this.button8);
			this.panel4.Controls.Add(this.treeView1);
			this.panel4.Controls.Add(this.label10);
			this.panel4.Location = new System.Drawing.Point(3, 107);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(259, 521);
			this.panel4.TabIndex = 15;
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(180, 2);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(75, 23);
			this.button8.TabIndex = 2;
			this.button8.Text = "刷新";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(3, 27);
			this.treeView1.Name = "treeView1";
			treeNode.Name = "节点0";
			treeNode.Text = "Rpc Apis";
			treeNode2.Name = "节点0";
			treeNode2.Text = "Topics";
			treeNode3.Name = "节点1";
			treeNode3.Text = "Sessions";
			this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[3] { treeNode, treeNode2, treeNode3 });
			this.treeView1.Size = new System.Drawing.Size(251, 486);
			this.treeView1.TabIndex = 1;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(5, 6);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(140, 17);
			this.label10.TabIndex = 0;
			this.label10.Text = "Api List: (RPC 接口列表)";
			this.panel5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel5.Controls.Add(this.label18);
			this.panel5.Controls.Add(this.label16);
			this.panel5.Controls.Add(this.label8);
			this.panel5.Controls.Add(this.textBox14);
			this.panel5.Controls.Add(this.label17);
			this.panel5.Controls.Add(this.hslProgress3);
			this.panel5.Controls.Add(this.textBox16);
			this.panel5.Controls.Add(this.panel6);
			this.panel5.Controls.Add(this.textBox17);
			this.panel5.Controls.Add(this.label19);
			this.panel5.Controls.Add(this.button12);
			this.panel5.Controls.Add(this.textBox19);
			this.panel5.Controls.Add(this.label21);
			this.panel5.Controls.Add(this.label22);
			this.panel5.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel5.Location = new System.Drawing.Point(3, 3);
			this.panel5.Margin = new System.Windows.Forms.Padding(0);
			this.panel5.Name = "panel5";
			this.panel5.Size = new System.Drawing.Size(822, 485);
			this.panel5.TabIndex = 16;
			this.label18.AutoSize = true;
			this.label18.ForeColor = System.Drawing.Color.Gray;
			this.label18.Location = new System.Drawing.Point(62, 47);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(79, 17);
			this.label18.TabIndex = 45;
			this.label18.Text = "UpdateTime";
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(0, 47);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(48, 17);
			this.label16.TabIndex = 44;
			this.label16.Text = "Time：";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.Gray;
			this.label8.Location = new System.Drawing.Point(411, 83);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(37, 17);
			this.label8.TabIndex = 43;
			this.label8.Text = "Level";
			this.textBox14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox14.Location = new System.Drawing.Point(500, 8);
			this.textBox14.Name = "textBox14";
			this.textBox14.ReadOnly = true;
			this.textBox14.Size = new System.Drawing.Size(317, 23);
			this.textBox14.TabIndex = 37;
			this.label17.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(412, 11);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(73, 17);
			this.label17.TabIndex = 36;
			this.label17.Text = "UserName:";
			this.hslProgress3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslProgress3.BackColor = System.Drawing.Color.Gainsboro;
			this.hslProgress3.Location = new System.Drawing.Point(62, 109);
			this.hslProgress3.Name = "hslProgress3";
			this.hslProgress3.ProgressColor = System.Drawing.Color.FromArgb(128, 255, 128);
			this.hslProgress3.ProgressStyle = HslControls.HslProgressStyle.Horizontal;
			this.hslProgress3.Size = new System.Drawing.Size(755, 18);
			this.hslProgress3.TabIndex = 33;
			this.hslProgress3.TextRenderFormat = "已接收 {0}%";
			this.hslProgress3.Value = 0;
			this.textBox16.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox16.Location = new System.Drawing.Point(62, 80);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(343, 23);
			this.textBox16.TabIndex = 29;
			this.panel6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel6.Controls.Add(this.radioButton1);
			this.panel6.Controls.Add(this.radioButton2);
			this.panel6.Controls.Add(this.radioButton6);
			this.panel6.Location = new System.Drawing.Point(563, 78);
			this.panel6.Name = "panel6";
			this.panel6.Size = new System.Drawing.Size(184, 28);
			this.panel6.TabIndex = 26;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(123, 3);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(52, 21);
			this.radioButton1.TabIndex = 28;
			this.radioButton1.Text = "Json";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(13, 3);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(50, 21);
			this.radioButton2.TabIndex = 26;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "Text";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton6.AutoSize = true;
			this.radioButton6.Location = new System.Drawing.Point(69, 3);
			this.radioButton6.Name = "radioButton6";
			this.radioButton6.Size = new System.Drawing.Size(48, 21);
			this.radioButton6.TabIndex = 27;
			this.radioButton6.Text = "Xml";
			this.radioButton6.UseVisualStyleBackColor = true;
			this.textBox17.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox17.Location = new System.Drawing.Point(62, 133);
			this.textBox17.Multiline = true;
			this.textBox17.Name = "textBox17";
			this.textBox17.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox17.Size = new System.Drawing.Size(755, 344);
			this.textBox17.TabIndex = 18;
			this.textBox17.Text = "  ";
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(0, 83);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(68, 17);
			this.label19.TabIndex = 19;
			this.label19.Text = "接收主题：";
			this.button12.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button12.Location = new System.Drawing.Point(747, 78);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(71, 28);
			this.button12.TabIndex = 17;
			this.button12.Text = "清空";
			this.button12.UseVisualStyleBackColor = true;
			this.textBox19.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox19.Location = new System.Drawing.Point(62, 7);
			this.textBox19.Name = "textBox19";
			this.textBox19.ReadOnly = true;
			this.textBox19.Size = new System.Drawing.Size(343, 23);
			this.textBox19.TabIndex = 9;
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(0, 11);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(65, 17);
			this.label21.TabIndex = 7;
			this.label21.Text = "ClientID：";
			this.label22.AutoSize = true;
			this.label22.Location = new System.Drawing.Point(0, 136);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(68, 17);
			this.label22.TabIndex = 42;
			this.label22.Text = "接收数据：";
			this.panel7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel7.Controls.Add(this.tabControl1);
			this.panel7.Location = new System.Drawing.Point(266, 107);
			this.panel7.Name = "panel7";
			this.panel7.Size = new System.Drawing.Size(836, 521);
			this.panel7.TabIndex = 17;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(836, 521);
			this.tabControl1.TabIndex = 17;
			this.tabPage1.Controls.Add(this.panel2);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(828, 491);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Api Test";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.tabPage2.Controls.Add(this.panel5);
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(828, 491);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Topic";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.tabPage3.Controls.Add(this.textBox_session_protocol);
			this.tabPage3.Controls.Add(this.label36);
			this.tabPage3.Controls.Add(this.label_info);
			this.tabPage3.Controls.Add(this.label35);
			this.tabPage3.Controls.Add(this.textBox_session_forbid);
			this.tabPage3.Controls.Add(this.label34);
			this.tabPage3.Controls.Add(this.label33);
			this.tabPage3.Controls.Add(this.textBox_session_developer);
			this.tabPage3.Controls.Add(this.label32);
			this.tabPage3.Controls.Add(this.textBox_session_activeTime);
			this.tabPage3.Controls.Add(this.label29);
			this.tabPage3.Controls.Add(this.textBox_session_willTopic);
			this.tabPage3.Controls.Add(this.label20);
			this.tabPage3.Controls.Add(this.textBox_session_userName);
			this.tabPage3.Controls.Add(this.label25);
			this.tabPage3.Controls.Add(this.textBox_session_topics);
			this.tabPage3.Controls.Add(this.label26);
			this.tabPage3.Controls.Add(this.textBox_session_rsa);
			this.tabPage3.Controls.Add(this.label27);
			this.tabPage3.Controls.Add(this.textBox_session_clientID);
			this.tabPage3.Controls.Add(this.label28);
			this.tabPage3.Controls.Add(this.textBox_session_ip);
			this.tabPage3.Controls.Add(this.label30);
			this.tabPage3.Controls.Add(this.textBox_session_onlineTime);
			this.tabPage3.Controls.Add(this.label31);
			this.tabPage3.Location = new System.Drawing.Point(4, 26);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(828, 491);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Sessions";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.textBox_session_protocol.Location = new System.Drawing.Point(103, 99);
			this.textBox_session_protocol.Name = "textBox_session_protocol";
			this.textBox_session_protocol.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_protocol.TabIndex = 53;
			this.label36.AutoSize = true;
			this.label36.Location = new System.Drawing.Point(8, 102);
			this.label36.Name = "label36";
			this.label36.Size = new System.Drawing.Size(69, 17);
			this.label36.TabIndex = 52;
			this.label36.Text = "Protocol：";
			this.label_info.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_info.AutoSize = true;
			this.label_info.Location = new System.Drawing.Point(6, 465);
			this.label_info.Name = "label_info";
			this.label_info.Size = new System.Drawing.Size(0, 17);
			this.label_info.TabIndex = 51;
			this.label35.AutoSize = true;
			this.label35.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
			this.label35.Location = new System.Drawing.Point(388, 295);
			this.label35.Name = "label35";
			this.label35.Size = new System.Drawing.Size(64, 17);
			this.label35.TabIndex = 50;
			this.label35.Text = "(禁止发布)";
			this.textBox_session_forbid.Location = new System.Drawing.Point(103, 292);
			this.textBox_session_forbid.Name = "textBox_session_forbid";
			this.textBox_session_forbid.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_forbid.TabIndex = 49;
			this.label34.AutoSize = true;
			this.label34.Location = new System.Drawing.Point(8, 295);
			this.label34.Name = "label34";
			this.label34.Size = new System.Drawing.Size(86, 17);
			this.label34.TabIndex = 48;
			this.label34.Text = "Forbid-pub：";
			this.label33.AutoSize = true;
			this.label33.ForeColor = System.Drawing.Color.FromArgb(128, 64, 0);
			this.label33.Location = new System.Drawing.Point(388, 260);
			this.label33.Name = "label33";
			this.label33.Size = new System.Drawing.Size(76, 17);
			this.label33.TabIndex = 47;
			this.label33.Text = "(开发者权限)";
			this.textBox_session_developer.Location = new System.Drawing.Point(103, 257);
			this.textBox_session_developer.Name = "textBox_session_developer";
			this.textBox_session_developer.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_developer.TabIndex = 46;
			this.label32.AutoSize = true;
			this.label32.Location = new System.Drawing.Point(8, 260);
			this.label32.Name = "label32";
			this.label32.Size = new System.Drawing.Size(80, 17);
			this.label32.TabIndex = 45;
			this.label32.Text = "Developer：";
			this.textBox_session_activeTime.Location = new System.Drawing.Point(103, 68);
			this.textBox_session_activeTime.Name = "textBox_session_activeTime";
			this.textBox_session_activeTime.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_activeTime.TabIndex = 44;
			this.label29.AutoSize = true;
			this.label29.Location = new System.Drawing.Point(8, 71);
			this.label29.Name = "label29";
			this.label29.Size = new System.Drawing.Size(86, 17);
			this.label29.TabIndex = 43;
			this.label29.Text = "Active Time：";
			this.textBox_session_willTopic.Location = new System.Drawing.Point(103, 224);
			this.textBox_session_willTopic.Name = "textBox_session_willTopic";
			this.textBox_session_willTopic.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_willTopic.TabIndex = 42;
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(8, 227);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(73, 17);
			this.label20.TabIndex = 41;
			this.label20.Text = "WillTopic：";
			this.textBox_session_userName.Location = new System.Drawing.Point(103, 161);
			this.textBox_session_userName.Name = "textBox_session_userName";
			this.textBox_session_userName.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_userName.TabIndex = 40;
			this.label25.AutoSize = true;
			this.label25.Location = new System.Drawing.Point(8, 164);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(82, 17);
			this.label25.TabIndex = 39;
			this.label25.Text = "UserName：";
			this.textBox_session_topics.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_session_topics.Location = new System.Drawing.Point(484, 7);
			this.textBox_session_topics.Multiline = true;
			this.textBox_session_topics.Name = "textBox_session_topics";
			this.textBox_session_topics.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_session_topics.Size = new System.Drawing.Size(345, 483);
			this.textBox_session_topics.TabIndex = 38;
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(397, 13);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(76, 17);
			this.label26.TabIndex = 37;
			this.label26.Text = "Sub-Topics:";
			this.textBox_session_rsa.Location = new System.Drawing.Point(103, 191);
			this.textBox_session_rsa.Name = "textBox_session_rsa";
			this.textBox_session_rsa.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_rsa.TabIndex = 36;
			this.label27.AutoSize = true;
			this.label27.Location = new System.Drawing.Point(8, 194);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(70, 17);
			this.label27.TabIndex = 35;
			this.label27.Text = "RSA/AES：";
			this.textBox_session_clientID.Location = new System.Drawing.Point(103, 131);
			this.textBox_session_clientID.Name = "textBox_session_clientID";
			this.textBox_session_clientID.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_clientID.TabIndex = 34;
			this.label28.AutoSize = true;
			this.label28.Location = new System.Drawing.Point(8, 134);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(69, 17);
			this.label28.TabIndex = 33;
			this.label28.Text = "Client ID：";
			this.textBox_session_ip.Location = new System.Drawing.Point(102, 8);
			this.textBox_session_ip.Name = "textBox_session_ip";
			this.textBox_session_ip.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_ip.TabIndex = 30;
			this.label30.AutoSize = true;
			this.label30.Location = new System.Drawing.Point(7, 11);
			this.label30.Name = "label30";
			this.label30.Size = new System.Drawing.Size(84, 17);
			this.label30.TabIndex = 29;
			this.label30.Text = "Ip Address：";
			this.textBox_session_onlineTime.Location = new System.Drawing.Point(103, 37);
			this.textBox_session_onlineTime.Name = "textBox_session_onlineTime";
			this.textBox_session_onlineTime.Size = new System.Drawing.Size(280, 23);
			this.textBox_session_onlineTime.TabIndex = 28;
			this.label31.AutoSize = true;
			this.label31.Location = new System.Drawing.Point(8, 40);
			this.label31.Name = "label31";
			this.label31.Size = new System.Drawing.Size(89, 17);
			this.label31.TabIndex = 27;
			this.label31.Text = "Online Time：";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1108, 633);
			base.Controls.Add(this.panel7);
			base.Controls.Add(this.panel4);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMqttSyncClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "MQTT 同步客户端 (RPC远程调用客户端)";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttSyncClient_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			this.panel5.ResumeLayout(false);
			this.panel5.PerformLayout();
			this.panel6.ResumeLayout(false);
			this.panel6.PerformLayout();
			this.panel7.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
