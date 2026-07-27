using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core.Net;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;
using HslControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo
{
	public class FormHttpClient : HslFormContent
	{
		private NetworkWebApiBase webApiClient;

		private MqttRpcApiInfo[] mqttRpcApiInfos;

		private bool isTest = false;

		private int thread_status = 0;

		private int failed = 0;

		private DateTime thread_time_start = DateTime.Now;

		private long successCount = 0L;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button1;

		private TextBox textBox4;

		private Label label4;

		private TextBox textBox3;

		private Label label3;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private Panel panel4;

		private Button button8;

		private TreeView treeView1;

		private Label label10;

		private Panel panel2;

		private Button button7;

		private TextBox textBox13;

		private Label label14;

		private TextBox textBox12;

		private Label label13;

		private TextBox textBox7;

		private Label label11;

		private Panel panel3;

		private RadioButton radioButton5;

		private RadioButton radioButton3;

		private RadioButton radioButton4;

		private TextBox textBox8;

		private Button button4;

		private Button button3;

		private TextBox textBox5;

		private Label label9;

		private TextBox textBox9;

		private Label label7;

		private Label label5;

		private ComboBox comboBox1;

		private Button button2;

		private Label label6;

		private HslBarChart hslBarChart1;

		private CheckBox checkBox1;

		private ComboBox comboBox2;

		private Label label8;

		private CheckBox checkBox2;

		private TextBox textBox_api_sign;

		private Label label24;

		private Label label15;

		private TextBox textBox_api_description;

		private Label label12;

		private RadioButton radioButton_response_hex;

		private TextBox textBox_url_example;

		private Button button5;

		public FormHttpClient()
		{
			InitializeComponent();
		}

		private async void Button1_Click(object sender, EventArgs e)
		{
			try
			{
				webApiClient = new NetworkWebApiBase(textBox1.Text, int.Parse(textBox2.Text), textBox3.Text, textBox4.Text);
				panel2.Enabled = true;
				button1.Enabled = false;
				button2.Enabled = true;
				webApiClient.UseHttps = checkBox1.Checked;
				webApiClient.UseEncodingISO = checkBox2.Checked;
				await MqttRpcApiRefresh(treeView1.Nodes[0]);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Input Data is wrong! please int again!" + Environment.NewLine + ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
		}

		private void FormABBWebApi_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
				label6.Text = "Body传入参数，如果是GET模式的话，参数需要通过地址传送，例如 GetA?id=5&&name=job";
			}
			else
			{
				label6.Text = "Body input parameters, if it is in GET mode, the parameters need to be transmitted through the address, such as GetA?id=5&&name=job";
			}
			comboBox2.DataSource = new string[6] { "none", "text/plain", "application/javascript", "application/json", "text/html", "application/xml" };
			button2.Enabled = false;
			panel2.Enabled = false;
			comboBox1.DataSource = new HttpMethod[7]
			{
				HttpMethod.Get,
				HttpMethod.Post,
				HttpMethod.Put,
				HttpMethod.Delete,
				HttpMethod.Options,
				HttpMethod.Head,
				HttpMethod.Trace
			};
			comboBox1.SelectedIndex = 0;
			ImageList imageList = new ImageList();
			imageList.Images.Add("VirtualMachine", Resources.VirtualMachine);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("Module_648", Resources.Module_648);
			imageList.Images.Add("Structure_507", Resources.Structure_507);
			treeView1.AfterSelect += TreeView1_AfterSelect;
			treeView1.ImageList = imageList;
			treeView1.Nodes[0].ImageKey = "VirtualMachine";
			treeView1.Nodes[0].SelectedImageKey = "VirtualMachine";
		}

		private async void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode node = treeView1.SelectedNode;
			if (node == null || !(node.Tag is MqttRpcApiInfo apiInfo))
			{
				return;
			}
			textBox9.Text = apiInfo.ApiTopic;
			textBox5.Text = apiInfo.ExamplePayload;
			textBox12.Text = apiInfo.CalledCount.ToString();
			textBox13.Text = apiInfo.SpendTotalTime.ToString("F2");
			textBox_api_description.Text = apiInfo.Description;
			textBox_api_sign.Text = apiInfo.MethodSignature;
			if (apiInfo.HttpMethod.ToUpper() == "GET")
			{
				comboBox1.SelectedItem = HttpMethod.Get;
			}
			else if (apiInfo.HttpMethod.ToUpper() == "POST")
			{
				comboBox1.SelectedItem = HttpMethod.Post;
			}
			OperateResult<string> read = await ReadFromServer(webApiClient, new HttpMethod("HSL"), "Logs/" + apiInfo.ApiTopic, "");
			if (!read.IsSuccess)
			{
				return;
			}
			try
			{
				if (!string.IsNullOrEmpty(read.Content) && read.Content != "null")
				{
					long[] data = JArray.Parse(read.Content).ToObject<long[]>().SelectLast(7);
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
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message + Environment.NewLine + read.Content);
			}
		}

		private async Task<MqttRpcApiInfo[]> ReadMqttRpcApiInfo(NetworkWebApiBase webApiBase)
		{
			string url = string.Format("{0}://{1}:{2}/Apis", checkBox1.Checked ? "https" : "http", webApiBase.IpAddress, webApiBase.Port);
			try
			{
				using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("HSL"), url))
				{
					using (HttpResponseMessage response = await webApiBase.Client.SendAsync(request))
					{
						using (HttpContent content = response.Content)
						{
							response.EnsureSuccessStatusCode();
							return JArray.Parse(await content.ReadAsStringAsync()).ToObject<MqttRpcApiInfo[]>();
						}
					}
				}
			}
			catch
			{
				return null;
			}
		}

		private MqttRpcApiInfo[] ReadMqttRpcApiInfo2()
		{
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(string.Format("{0}://{1}:{2}/Apis", checkBox1.Checked ? "https" : "http", webApiClient.IpAddress, webApiClient.Port));
				httpWebRequest.ContentType = "application/json";
				httpWebRequest.KeepAlive = false;
				httpWebRequest.AllowAutoRedirect = true;
				httpWebRequest.CookieContainer = new CookieContainer();
				httpWebRequest.Accept = "application/json, text/javascript, */*; q=0.01";
				httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36";
				if (!string.IsNullOrEmpty(textBox3.Text))
				{
					httpWebRequest.Credentials = new NetworkCredential(textBox3.Text, textBox4.Text);
				}
				httpWebRequest.Method = "HSL";
				httpWebRequest.Timeout = 10000;
				string json = "";
				using (StreamReader streamReader = new StreamReader(((HttpWebResponse)httpWebRequest.GetResponse()).GetResponseStream()))
				{
					json = streamReader.ReadToEnd();
				}
				return JArray.Parse(json).ToObject<MqttRpcApiInfo[]>();
			}
			catch
			{
				return null;
			}
		}

		private async void button7_Click(object sender, EventArgs e)
		{
			if (isTest)
			{
				thread_status = 4;
				failed = 0;
				thread_time_start = DateTime.Now;
				Thread thread = new Thread(thread_test1);
				thread.IsBackground = true;
				thread.Start();
				Thread thread2 = new Thread(thread_test1);
				thread2.IsBackground = true;
				thread2.Start();
				Thread thread3 = new Thread(thread_test1);
				thread3.IsBackground = true;
				thread3.Start();
				Thread thread4 = new Thread(thread_test1);
				thread4.IsBackground = true;
				thread4.Start();
				button7.Enabled = false;
			}
			else
			{
				MqttRpcApiInfo[] apis = await ReadMqttRpcApiInfo(webApiClient);
				if (apis != null)
				{
					textBox8.Text = JArray.FromObject(apis).ToString();
				}
			}
		}

		private async void thread_test1()
		{
			string url = textBox9.Text;
			_ = textBox5.Text;
			int count = 10000;
			while (count > 0)
			{
				OperateResult<string> read = await webApiClient.PostAsync(url, new
				{
					id = count
				}.ToJsonString());
				if (!read.IsSuccess)
				{
					failed++;
				}
				else if (read.Content != (count + 1).ToString())
				{
					failed++;
				}
				count--;
				successCount++;
			}
			thread_end();
		}

		private void thread_end()
		{
			if (Interlocked.Decrement(ref thread_status) == 0)
			{
				Invoke((Action)delegate
				{
					label2.Text = successCount.ToString();
					button7.Enabled = true;
					DemoUtils.ShowMessage("Spend：" + (DateTime.Now - thread_time_start).TotalSeconds + Environment.NewLine + " Failed Count：" + failed);
				});
			}
		}

		private async Task<OperateResult<string>> ReadFromServer(NetworkWebApiBase webApiBase, HttpMethod httpMethod, string url, string body)
		{
			textBox_url_example.Text = webApiBase.GetEntireUrl(url);
			return await webApiBase.RequestAsync(httpMethod, url, body, (comboBox2.SelectedItem == null) ? comboBox2.Text : comboBox2.SelectedItem.ToString());
		}

		private async Task MqttRpcApiRefresh(TreeNode rootNode)
		{
			rootNode.Nodes.Clear();
			mqttRpcApiInfos = await Task.Run(() => ReadMqttRpcApiInfo2());
			if (mqttRpcApiInfos != null)
			{
				MqttRpcApiInfo[] array = mqttRpcApiInfos;
				foreach (MqttRpcApiInfo item in array)
				{
					AddTreeNode(rootNode, item.ApiTopic, item.ApiTopic, item);
				}
				rootNode.Expand();
			}
		}

		private async void button8_Click(object sender, EventArgs e)
		{
			await MqttRpcApiRefresh(treeView1.Nodes[0]);
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

		private string getJPropertValue(JProperty jProperty)
		{
			if (jProperty.Value.Type == JTokenType.Array)
			{
				JArray jArray = (JArray)jProperty.Value;
				StringBuilder stringBuilder = new StringBuilder(jProperty.Name + "=");
				for (int i = 0; i < jArray.Count; i++)
				{
					stringBuilder.Append(jArray[i]);
					if (i < jArray.Count - 1)
					{
						stringBuilder.Append(";");
					}
				}
				return stringBuilder.ToString();
			}
			return $"{jProperty.Name}={jProperty.Value}";
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			DateTime start = DateTime.Now;
			button3.Enabled = false;
			HttpMethod httpMethod = (HttpMethod)comboBox1.SelectedItem;
			string url = textBox9.Text;
			if (httpMethod == HttpMethod.Get && !url.Contains("?") && !string.IsNullOrEmpty(textBox5.Text))
			{
				try
				{
					url = url + "?" + string.Join("&", from m in JObject.Parse(textBox5.Text).Properties()
						select getJPropertValue(m));
				}
				catch
				{
				}
			}
			OperateResult<string> read = await ReadFromServer(webApiClient, httpMethod, url, textBox5.Text);
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
					api.ExamplePayload = textBox5.Text;
				}
			}
			string msg = read.Content;
			if (radioButton4.Checked)
			{
				try
				{
					msg = XElement.Parse(msg).ToString();
				}
				catch
				{
				}
			}
			else if (radioButton5.Checked)
			{
				try
				{
					msg = JObject.Parse(msg).ToString();
				}
				catch
				{
				}
			}
			else if (radioButton_response_hex.Checked)
			{
				msg = Encoding.UTF8.GetBytes(msg).ToHexString(' ');
			}
			textBox8.Text = msg;
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlUserName, textBox3.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPassword, textBox4.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox1.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlUserName).Value;
			textBox4.Text = element.Attribute(DemoDeviceList.XmlPassword).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button5_Click(object sender, EventArgs e)
		{
			try
			{
				Clipboard.SetText(JsonConvert.SerializeObject(JObject.Parse(textBox5.Text).ToString(Formatting.None)));
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("JObject.Parse failed: " + ex.Message);
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1 = new System.Windows.Forms.Panel();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.label8 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel4 = new System.Windows.Forms.Panel();
			this.button8 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.label10 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox_url_example = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox_api_sign = new System.Windows.Forms.TextBox();
			this.label24 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_api_description = new System.Windows.Forms.TextBox();
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.hslBarChart1 = new HslControls.HslBarChart();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button7 = new System.Windows.Forms.Button();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_response_hex = new System.Windows.Forms.RadioButton();
			this.radioButton5 = new System.Windows.Forms.RadioButton();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.panel1.SuspendLayout();
			this.panel4.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "web api";
			this.userControlHead1.Size = new System.Drawing.Size(1023, 32);
			this.userControlHead1.TabIndex = 30;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox4);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(4, 36);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1015, 56);
			this.panel1.TabIndex = 31;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(134, 31);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(124, 21);
			this.checkBox2.TabIndex = 21;
			this.checkBox2.Text = "UseEncodingISO";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.Fuchsia;
			this.label8.Location = new System.Drawing.Point(333, 36);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(208, 17);
			this.label8.TabIndex = 20;
			this.label8.Text = "if use https, the port is default 443";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(338, 14);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(80, 21);
			this.checkBox1.TabIndex = 19;
			this.checkBox1.Text = "UseHttps";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.button2.Location = new System.Drawing.Point(861, 11);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(93, 25);
			this.button2.TabIndex = 18;
			this.button2.Text = "close";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(753, 11);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(93, 25);
			this.button1.TabIndex = 17;
			this.button1.Text = "open";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			this.textBox4.Location = new System.Drawing.Point(635, 12);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(97, 23);
			this.textBox4.TabIndex = 16;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(568, 15);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(65, 17);
			this.label4.TabIndex = 15;
			this.label4.Text = "password";
			this.textBox3.Location = new System.Drawing.Point(479, 12);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(83, 23);
			this.textBox3.TabIndex = 14;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(433, 15);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(43, 17);
			this.label3.TabIndex = 13;
			this.label3.Text = "Name";
			this.textBox2.Location = new System.Drawing.Point(269, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(51, 23);
			this.textBox2.TabIndex = 12;
			this.textBox2.Text = "80";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(229, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(32, 17);
			this.label2.TabIndex = 11;
			this.label2.Text = "Port";
			this.textBox1.Location = new System.Drawing.Point(92, 5);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(125, 23);
			this.textBox1.TabIndex = 10;
			this.textBox1.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(17, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(72, 17);
			this.label1.TabIndex = 9;
			this.label1.Text = "Ip Address";
			this.panel4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel4.Controls.Add(this.button8);
			this.panel4.Controls.Add(this.treeView1);
			this.panel4.Controls.Add(this.label10);
			this.panel4.Location = new System.Drawing.Point(4, 97);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(259, 534);
			this.panel4.TabIndex = 33;
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(180, 2);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(75, 23);
			this.button8.TabIndex = 2;
			this.button8.Text = "刷新";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(3, 28);
			this.treeView1.Name = "treeView1";
			treeNode.Name = "节点0";
			treeNode.Text = "Rpc Apis";
			this.treeView1.Nodes.AddRange(new System.Windows.Forms.TreeNode[1] { treeNode });
			this.treeView1.Size = new System.Drawing.Size(251, 499);
			this.treeView1.TabIndex = 1;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(5, 6);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(140, 17);
			this.label10.TabIndex = 0;
			this.label10.Text = "Api List: (RPC 接口列表)";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.textBox_url_example);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.textBox_api_sign);
			this.panel2.Controls.Add(this.label24);
			this.panel2.Controls.Add(this.label15);
			this.panel2.Controls.Add(this.textBox_api_description);
			this.panel2.Controls.Add(this.comboBox2);
			this.panel2.Controls.Add(this.hslBarChart1);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.textBox13);
			this.panel2.Controls.Add(this.label14);
			this.panel2.Controls.Add(this.textBox12);
			this.panel2.Controls.Add(this.label13);
			this.panel2.Controls.Add(this.textBox7);
			this.panel2.Controls.Add(this.label11);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.textBox5);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.textBox9);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Location = new System.Drawing.Point(266, 97);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(753, 534);
			this.panel2.TabIndex = 34;
			this.button5.Location = new System.Drawing.Point(2, 220);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(54, 47);
			this.button5.TabIndex = 60;
			this.button5.Text = "Json\r\nString";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox_url_example.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_url_example.Location = new System.Drawing.Point(63, 507);
			this.textBox_url_example.Name = "textBox_url_example";
			this.textBox_url_example.ReadOnly = true;
			this.textBox_url_example.Size = new System.Drawing.Size(685, 23);
			this.textBox_url_example.TabIndex = 59;
			this.label12.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label12.AutoSize = true;
			this.label12.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label12.Location = new System.Drawing.Point(4, 511);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(37, 17);
			this.label12.TabIndex = 57;
			this.label12.Text = "Url：";
			this.textBox_api_sign.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_api_sign.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox_api_sign.ForeColor = System.Drawing.Color.Gray;
			this.textBox_api_sign.Location = new System.Drawing.Point(61, 38);
			this.textBox_api_sign.Name = "textBox_api_sign";
			this.textBox_api_sign.ReadOnly = true;
			this.textBox_api_sign.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_api_sign.Size = new System.Drawing.Size(683, 16);
			this.textBox_api_sign.TabIndex = 56;
			this.textBox_api_sign.Text = "[签名]";
			this.label24.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label24.AutoSize = true;
			this.label24.ForeColor = System.Drawing.Color.Gray;
			this.label24.Location = new System.Drawing.Point(-1, 37);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(40, 17);
			this.label24.TabIndex = 55;
			this.label24.Text = "[签名]";
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label15.AutoSize = true;
			this.label15.ForeColor = System.Drawing.Color.Gray;
			this.label15.Location = new System.Drawing.Point(-1, 59);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(40, 17);
			this.label15.TabIndex = 54;
			this.label15.Text = "[注释]";
			this.textBox_api_description.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_api_description.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.textBox_api_description.ForeColor = System.Drawing.Color.Gray;
			this.textBox_api_description.Location = new System.Drawing.Point(61, 59);
			this.textBox_api_description.Multiline = true;
			this.textBox_api_description.Name = "textBox_api_description";
			this.textBox_api_description.ReadOnly = true;
			this.textBox_api_description.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_api_description.Size = new System.Drawing.Size(683, 56);
			this.textBox_api_description.TabIndex = 53;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Location = new System.Drawing.Point(217, 271);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(179, 25);
			this.comboBox2.TabIndex = 46;
			this.hslBarChart1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.hslBarChart1.BackColor = System.Drawing.Color.White;
			this.hslBarChart1.Location = new System.Drawing.Point(445, 121);
			this.hslBarChart1.Name = "hslBarChart1";
			this.hslBarChart1.ShowBarValueFormat = "{0}";
			this.hslBarChart1.Size = new System.Drawing.Size(304, 143);
			this.hslBarChart1.TabIndex = 45;
			this.hslBarChart1.Text = "hslBarChart1";
			this.hslBarChart1.Title = "Called Infomation";
			this.hslBarChart1.UseGradient = true;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(62, 272);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(69, 25);
			this.comboBox1.TabIndex = 43;
			this.button7.Location = new System.Drawing.Point(402, 270);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(119, 28);
			this.button7.TabIndex = 40;
			this.button7.Text = "Apis Information";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.textBox13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox13.Location = new System.Drawing.Point(667, 7);
			this.textBox13.Name = "textBox13";
			this.textBox13.ReadOnly = true;
			this.textBox13.Size = new System.Drawing.Size(81, 23);
			this.textBox13.TabIndex = 39;
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(591, 11);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(72, 17);
			this.label14.TabIndex = 38;
			this.label14.Text = "Total Time:";
			this.textBox12.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox12.Location = new System.Drawing.Point(511, 7);
			this.textBox12.Name = "textBox12";
			this.textBox12.ReadOnly = true;
			this.textBox12.Size = new System.Drawing.Size(70, 23);
			this.textBox12.TabIndex = 37;
			this.label13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(418, 11);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(85, 17);
			this.label13.TabIndex = 36;
			this.label13.Text = "Called Count:";
			this.textBox7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox7.Location = new System.Drawing.Point(667, 270);
			this.textBox7.Name = "textBox7";
			this.textBox7.ReadOnly = true;
			this.textBox7.Size = new System.Drawing.Size(81, 23);
			this.textBox7.TabIndex = 31;
			this.label11.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(583, 272);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(44, 17);
			this.label11.TabIndex = 30;
			this.label11.Text = "耗时：";
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.radioButton_response_hex);
			this.panel3.Controls.Add(this.radioButton5);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Location = new System.Drawing.Point(431, 301);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(242, 28);
			this.panel3.TabIndex = 26;
			this.radioButton_response_hex.AutoSize = true;
			this.radioButton_response_hex.Location = new System.Drawing.Point(181, 3);
			this.radioButton_response_hex.Name = "radioButton_response_hex";
			this.radioButton_response_hex.Size = new System.Drawing.Size(48, 21);
			this.radioButton_response_hex.TabIndex = 29;
			this.radioButton_response_hex.Text = "Hex";
			this.radioButton_response_hex.UseVisualStyleBackColor = true;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(123, 3);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(52, 21);
			this.radioButton5.TabIndex = 28;
			this.radioButton5.Text = "Json";
			this.radioButton5.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Checked = true;
			this.radioButton3.Location = new System.Drawing.Point(13, 3);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(50, 21);
			this.radioButton3.TabIndex = 26;
			this.radioButton3.TabStop = true;
			this.radioButton3.Text = "Text";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Location = new System.Drawing.Point(69, 3);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(48, 21);
			this.radioButton4.TabIndex = 27;
			this.radioButton4.Text = "Xml";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 341);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(686, 163);
			this.textBox8.TabIndex = 18;
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(678, 301);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(71, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button3.Location = new System.Drawing.Point(141, 270);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(70, 28);
			this.button3.TabIndex = 12;
			this.button3.Text = "读取";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(62, 121);
			this.textBox5.Multiline = true;
			this.textBox5.Name = "textBox5";
			this.textBox5.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox5.Size = new System.Drawing.Size(377, 145);
			this.textBox5.TabIndex = 8;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(-1, 121);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(50, 17);
			this.label9.TabIndex = 11;
			this.label9.Text = "Body：";
			this.textBox9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox9.Location = new System.Drawing.Point(62, 7);
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(343, 23);
			this.textBox9.TabIndex = 9;
			this.textBox9.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(0, 11);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(39, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "Api：";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(-1, 341);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 42;
			this.label5.Text = "Response:";
			this.label6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.label6.Location = new System.Drawing.Point(60, 300);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(365, 36);
			this.label6.TabIndex = 44;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1023, 634);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel4);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormHttpClient";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormWebClient";
			base.Load += new System.EventHandler(FormABBWebApi_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
