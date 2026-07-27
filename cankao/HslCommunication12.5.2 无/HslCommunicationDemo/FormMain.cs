using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Security;
using HslCommunication.Enthernet;
using HslCommunication.LogNet;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;
using HslCommunicationDemo.Vip;
using HslControls;
using WeifenLuo.WinFormsUI.Docking;

namespace HslCommunicationDemo
{
	public class FormMain : Form
	{
		public static Color ThemeColor = Color.FromArgb(64, 64, 64);

		private bool topMost = false;

		private MqttClient mqttClient;

		private System.Windows.Forms.Timer timer;

		private Process cur = null;

		private PerformanceCounter curpcp = null;

		private const int MB_DIV = 1048576;

		private FormPanelLeft panelLeft;

		private FormSaveList panelSave;

		private ImageList imageList;

		public static Type[] formTypes = Assembly.GetExecutingAssembly().GetTypes();

		private ILogNet logNet;

		private FormLogRender logRender;

		private MqttSyncClient mqttSyncClient;

		private IContainer components = null;

		private MenuStrip menuStrip1;

		private ToolStripMenuItem verisonToolStripMenuItem;

		private ToolStripMenuItem 免责条款ToolStripMenuItem;

		private ToolStripMenuItem 授权ToolStripMenuItem;

		private VS2015BlueTheme vS2015BlueTheme1;

		private DockPanel dockPanel1;

		private Label label1;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem deleteDeviceToolStripMenuItem;

		private Label label2;

		private ToolStripMenuItem newVersionToolStripMenuItem;

		private ToolStripMenuItem activeToolStripMenuItem;

		private ToolStripMenuItem toolStripMenuItem_language;

		private ToolStripMenuItem 简体中文ToolStripMenuItem;

		private ToolStripMenuItem englishToolStripMenuItem;

		private ToolStripMenuItem toolStripMenuItem_Help;

		private ToolStripMenuItem toolStripMenuItem_HomePage;

		private ToolStripMenuItem toolStripMenuItem_ApiDoc;

		private ToolStripMenuItem toolStripMenuItem_Doc;

		private ToolStripMenuItem toolStripMenuItem_Debug;

		private ToolStripMenuItem toolStripMenuItem_CommunicationLog;

		private ToolStripMenuItem blogsToolStripMenuItem;

		private ToolStripMenuItem webSideToolStripMenuItem;

		private ToolStripMenuItem support赞助ToolStripMenuItem;

		private ToolStripMenuItem authorization授权ToolStripMenuItem;

		private ToolStripMenuItem toolStripMenuItem_SerialPort;

		private ToolStripMenuItem toolStripMenuItem_tcpUDP;

		private ToolStripMenuItem toolStripMenuItem_tcpUdpServer;

		private ToolStripMenuItem toolStripMenuItem_SourceCode;

		private ToolStripMenuItem toolStripMenuItem_serial2Tcp;

		private ToolStripMenuItem toolStripMenuItem_tcp2Tcp;

		private ToolStripMenuItem toolStripMenuItem_byteTransform;

		private ToolStripMenuItem regexRegularToolStripMenuItem;

		private ToolStripMenuItem ecologyToolStripMenuItem;

		private ToolStripMenuItem showMsToolStripMenuItem;

		private ToolStripMenuItem formTopMostToolStripMenuItem;

		private Label label_account;

		private ToolStripMenuItem testPanelSizeFixedToolStripMenuItem;

		private ToolStripMenuItem pingTestToolStripMenuItem;

		private ToolStripMenuItem lockToolStripMenuItem;

		public static bool ShowMs { get; set; }

		public static FormMain Form { get; set; }

		public FormMain()
		{
			InitializeComponent();
			Form = this;
			logNet = new LogNetSingle(string.Empty);
			showMsToolStripMenuItem.Click += ShowMsToolStripMenuItem_Click;
		}

		private void ShowMsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (ShowMs)
			{
				showMsToolStripMenuItem.Image = null;
			}
			else
			{
				showMsToolStripMenuItem.Image = Resources.StatusAnnotations_Complete_and_ok_16xLG_color;
			}
			ShowMs = !ShowMs;
		}

		private void formTopMostToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (topMost)
			{
				formTopMostToolStripMenuItem.Image = null;
			}
			else
			{
				formTopMostToolStripMenuItem.Image = Resources.StatusAnnotations_Complete_and_ok_16xLG_color;
			}
			topMost = !topMost;
			base.TopMost = topMost;
		}

		private void testPanelSizeFixedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (DemoUtils.PenelSizeFixed)
			{
				testPanelSizeFixedToolStripMenuItem.Image = null;
			}
			else
			{
				testPanelSizeFixedToolStripMenuItem.Image = Resources.StatusAnnotations_Complete_and_ok_16xLG_color;
			}
			DemoUtils.PenelSizeFixed = !DemoUtils.PenelSizeFixed;
		}

		private void FormLoad_Load(object sender, EventArgs e)
		{
			LoadActive();
			LoadControlsActive();
			dockPanel1.Theme = vS2015BlueTheme1;
			logRender = new FormLogRender(logNet);
			ThemeColor = menuStrip1.BackColor;
			verisonToolStripMenuItem.Text = "Version: " + SoftBasic.FrameworkVersion.ToString();
			if (Settings1.Default.language == 1)
			{
				if (CultureInfo.CurrentCulture.ToString().StartsWith("zh"))
				{
					Program.Language = 1;
					Language(Program.Language);
				}
				else
				{
					StringResources.SeteLanguageEnglish();
					Program.Language = 2;
					Language(Program.Language);
				}
			}
			else
			{
				Program.Language = 2;
				StringResources.SeteLanguageEnglish();
				Language(Program.Language);
			}
			support赞助ToolStripMenuItem.Click += Support赞助ToolStripMenuItem_Click;
			imageList = new ImageList();
			imageList.ColorDepth = ColorDepth.Depth32Bit;
			imageList.ImageSize = new Size(16, 16);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("ab", Resources.ab);
			imageList.Images.Add("fujifilm", Resources.fujifilm);
			imageList.Images.Add("HslCommunication", Resources.HslCommunication);
			imageList.Images.Add("idcard", Resources.idcard);
			imageList.Images.Add("inovance", Resources.inovance);
			imageList.Images.Add("keyence", Resources.keyence);
			imageList.Images.Add("ls", Resources.ls);
			imageList.Images.Add("melsec", Resources.melsec);
			imageList.Images.Add("modbus", Resources.modbus);
			imageList.Images.Add("omron", Resources.omron);
			imageList.Images.Add("panasonic", Resources.panasonic);
			imageList.Images.Add("redis", Resources.redis);
			imageList.Images.Add("schneider", Resources.schneider);
			imageList.Images.Add("siemens", Resources.siemens);
			imageList.Images.Add("debug", Resources.debug);
			imageList.Images.Add("barcode", Resources.barcode);
			imageList.Images.Add("mqtt", Resources.mqtt);
			imageList.Images.Add("toledo", Resources.toledo);
			imageList.Images.Add("robot", Resources.robot);
			imageList.Images.Add("beckhoff", Resources.beckhoff);
			imageList.Images.Add("abb", Resources.abb);
			imageList.Images.Add("fatek", Resources.fatek);
			imageList.Images.Add("kuka", Resources.kuka);
			imageList.Images.Add("efort", Resources.efort);
			imageList.Images.Add("fanuc", Resources.fanuc);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("zkt", Resources.zkt);
			imageList.Images.Add("websocket", Resources.websocket);
			imageList.Images.Add("yaskawa", Resources.yaskawa);
			imageList.Images.Add("xinje", Resources.xinje);
			imageList.Images.Add("yokogawa", Resources.yokogawa);
			imageList.Images.Add("delta", Resources.delta);
			imageList.Images.Add("ge", Resources.ge);
			imageList.Images.Add("yamatake", Resources.Yamatake);
			imageList.Images.Add("rkc", Resources.rkc);
			imageList.Images.Add("vigor", Resources.vigor);
			imageList.Images.Add("iec", Resources.iec);
			imageList.Images.Add("turck", Resources.Turck);
			imageList.Images.Add("toyota", Resources.toyota);
			imageList.Images.Add("SerialPort", Resources.SerialPort);
			imageList.Images.Add("NetworkAdapter", Resources.NetworkAdapter);
			imageList.Images.Add("bin", Resources.bin);
			imageList.Images.Add("wecon", Resources.wecon);
			imageList.Images.Add("megmeet", Resources.megmeet);
			imageList.Images.Add("invt", Resources.invt);
			imageList.Images.Add("http", Resources.http);
			imageList.Images.Add("cimon", Resources.cimon);
			imageList.Images.Add("yamaha", Resources.yamaha);
			imageList.Images.Add("Certificate", Resources.Certificate);
			imageList.Images.Add("Lock_white", Resources.Lock_white);
			imageList.Images.Add("CSharpFile_SolutionExplorerNode", Resources.CSharpFile_SolutionExplorerNode);
			imageList.Images.Add("java", Resources.java1);
			panelLeft = new FormPanelLeft(dockPanel1, imageList, logNet);
			panelLeft.FormClosing += PanelLeft_FormClosing;
			panelLeft.Show(dockPanel1, DockState.DockLeft);
			panelSave = new FormSaveList(dockPanel1, imageList, logNet, panelLeft.IconImageIndex);
			panelSave.FormClosing += PanelLeft_FormClosing;
			panelSave.Show(dockPanel1, DockState.DockLeft);
			if (panelSave.LoadDeviceList())
			{
				panelSave.Activate();
			}
			else
			{
				panelLeft.Activate();
			}
			if (Program.ShowAuthorInfomation)
			{
				new FormIndex().Show(dockPanel1, DockState.Document);
			}
			else
			{
				Text = "调试工具";
			}
			timer = new System.Windows.Forms.Timer();
			timer.Interval = 1000;
			timer.Tick += Timer_Tick;
			timer.Start();
			newVersionToolStripMenuItem.Visible = false;
			activeToolStripMenuItem.Click += ActiveToolStripMenuItem_Click;
			toolStripMenuItem_HomePage.Visible = Program.ShowAuthorInfomation;
			toolStripMenuItem_ApiDoc.Visible = Program.ShowAuthorInfomation;
			免责条款ToolStripMenuItem.Visible = Program.ShowAuthorInfomation;
		}

		private void PanelLeft_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
		}

		private void FormSelect_FormClosing(object sender, FormClosingEventArgs e)
		{
			mqttClient?.ConnectClose();
		}

		private void FormLoad_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(ThreadPoolCheckVersion, null);
		}

		private void ThreadPoolCheckVersion(object obj)
		{
			Thread.Sleep(100);
			mqttClient = new MqttClient(new MqttConnectionOptions
			{
				IpAddress = "118.24.36.220",
				Port = 1883,
				ClientId = "HslDemo"
			});
			mqttClient.ConnectServer();
			OperateResult<NetHandle, string> read = new NetSimplifyClient("118.24.36.220", 18467).ReadCustomerFromServer(1, SoftBasic.FrameworkVersion.ToString());
			if (read.IsSuccess && new SystemVersion(read.Content2) > SoftBasic.FrameworkVersion)
			{
				Invoke((Action)delegate
				{
					if (!File.Exists(Path.Combine(Application.StartupPath, "newVersionIngored.txt")))
					{
						using (FormNewVerison formNewVerison = new FormNewVerison())
						{
							if (formNewVerison.ShowDialog("Version Check", "New version on server：" + read.Content2 + Environment.NewLine + " Start update?") == DialogResult.Yes)
							{
								NewVersionToolStripMenuItem_Click(null, new EventArgs());
							}
							if (formNewVerison.NewVersionIngored)
							{
								File.WriteAllText(Path.Combine(Application.StartupPath, "newVersionIngored.txt"), string.Empty, Encoding.UTF8);
							}
							else
							{
								File.Delete(Path.Combine(Application.StartupPath, "newVersionIngored.txt"));
							}
							return;
						}
					}
					newVersionToolStripMenuItem.Visible = true;
					newVersionToolStripMenuItem.Click += NewVersionToolStripMenuItem_Click;
				});
			}
			try
			{
				cur = Process.GetCurrentProcess();
				curpcp = new PerformanceCounter("Process", "Working Set - Private", cur.ProcessName);
			}
			catch
			{
			}
		}

		private void LoadActive()
		{
			try
			{
				string text = Path.Combine(Application.StartupPath, "active.txt");
				if (File.Exists(text))
				{
					string text2 = new FileInfo(text).CreationTime.ToString("yyyy-MM-dd-mm-ss");
					AesCryptography aesCryptography = new AesCryptography(text2 + text2);
					string @string = Encoding.UTF8.GetString(aesCryptography.Decrypt(File.ReadAllBytes(text)));
					bool flag = false;
					if ((@string.Length >= 100))
					{
						activeToolStripMenuItem.Text = "Actived!";
						activeToolStripMenuItem.ForeColor = Color.Lime;
					}
					else
					{
						File.Delete(text);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(SoftBasic.GetExceptionMessage(ex));
			}
		}

		private void LoadControlsActive()
		{
			try
			{
				string text = Path.Combine(Application.StartupPath, "controls_active.txt");
				if (File.Exists(text))
				{
					string text2 = new FileInfo(text).CreationTime.ToString("yyyy-MM-dd-mm-ss");
					AesCryptography aesCryptography = new AesCryptography(text2 + text2);
					byte[] array = File.ReadAllBytes(text);
					if (array != null && array.Length >= 1 )
					{
						MessageBox.Show("HslControls active failed");
						File.Delete(text);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(SoftBasic.GetExceptionMessage(ex));
			}
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "HslCommunication 测试工具";
				免责条款ToolStripMenuItem.Text = "全国使用分布";
				toolStripMenuItem_CommunicationLog.Text = "报文日志";
				toolStripMenuItem_ApiDoc.Text = "API 文档";
				deleteDeviceToolStripMenuItem.Text = "删除设备";
				toolStripMenuItem_language.Text = "语言(&L)";
				toolStripMenuItem_HomePage.Text = "官方网站";
				toolStripMenuItem_Debug.Text = "调试(&D)";
				toolStripMenuItem_Help.Text = "帮助(&H)";
				toolStripMenuItem_Doc.Text = "开发文档";
				toolStripMenuItem_SerialPort.Text = "串口调试";
				ecologyToolStripMenuItem.Text = "大生态(&E)";
				toolStripMenuItem_byteTransform.Text = "字节变换";
				regexRegularToolStripMenuItem.Text = "正则表达式";
				formTopMostToolStripMenuItem.Text = "窗体置顶";
				label_account.Text = "登录";
			}
			else
			{
				Text = "HslCommunication Test Tool";
				toolStripMenuItem_CommunicationLog.Text = "MsgLog";
				免责条款ToolStripMenuItem.Text = "China Map";
				toolStripMenuItem_ApiDoc.Text = "API Doc";
				toolStripMenuItem_language.Text = "Language(&L)";
				toolStripMenuItem_HomePage.Text = "HomePage";
				toolStripMenuItem_Debug.Text = "Debug(&D)";
				toolStripMenuItem_Help.Text = "Help(&H)";
				toolStripMenuItem_Doc.Text = "Document";
				toolStripMenuItem_SerialPort.Text = "SerialPort";
				ecologyToolStripMenuItem.Text = "Ecology(&E)";
				showMsToolStripMenuItem.Text = "Time Show Ms";
				toolStripMenuItem_byteTransform.Text = "ByteTransform";
				regexRegularToolStripMenuItem.Text = "RegexRegular";
				formTopMostToolStripMenuItem.Text = "TopMost";
				label_account.Text = "Login";
			}
		}

		private void ActiveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (FormActive formActive = new FormActive())
			{
				formActive.ShowDialog();
				LoadActive();
				LoadControlsActive();
			}
		}

		private void Support赞助ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (FormSupport formSupport = new FormSupport())
			{
				formSupport.ShowDialog();
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (curpcp != null)
			{
				string text = (curpcp.NextValue() / 1048576f).ToString("F1") + "MB";
				label2.Text = "Ram: " + text;
			}
			long num = Interlocked.Exchange(ref HslTimeOut.TimeoutDealCount, 0L);
			label1.Text = $"Timeout:{HslTimeOut.TimeOutCheckCount}/{num}  Lock:{SimpleHybirdLock.SimpleHybirdLockCount}  Wait:{SimpleHybirdLock.SimpleHybirdLockWaitCount}";
		}

		private void toolStripMenuItem_CommunicationLog_Click(object sender, EventArgs e)
		{
			if (logRender == null || logRender.IsDisposed)
			{
				logRender = new FormLogRender(logNet);
			}
			bool flag = false;
			foreach (DockPane pane in dockPanel1.Panes)
			{
				if (pane.DockState == DockState.Document)
				{
					logRender.Show(pane, DockAlignment.Bottom, 0.3);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				logRender.Show(dockPanel1, DockState.DockBottom);
			}
		}

		public static void OpenWebside(string url)
		{
			try
			{
				Process.Start(url);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void blogsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("http://www.cnblogs.com/dathlin/p/7703805.html");
		}

		private void webSideToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("http://www.hslcommunication.cn/");
		}

		private void toolStripMenuItem_Doc_Click_1(object sender, EventArgs e)
		{
			OpenWebside("http://www.hsltechnology.cn/Doc/HslCommunication");
		}

		private void 简体中文ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringResources.SetLanguageChinese();
			Program.Language = 1;
			Settings1.Default.language = Program.Language;
			Settings1.Default.Save();
			Language(Program.Language);
			panelLeft?.SetLanguage();
			panelSave?.SetLanguage();
			DemoUtils.ShowMessage("已选择中文");
		}

		private void englishToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringResources.SeteLanguageEnglish();
			Program.Language = 2;
			Settings1.Default.language = Program.Language;
			Settings1.Default.Save();
			Language(Program.Language);
			panelLeft?.SetLanguage();
			panelSave?.SetLanguage();
			DemoUtils.ShowMessage("Select English!");
		}

		private void toolStripMenuItem_HomePage_Click(object sender, EventArgs e)
		{
			OpenWebside("http://www.hsltechnology.cn");
		}

		private void toolStripMenuItem_SourceCode_Click(object sender, EventArgs e)
		{
			OpenWebside("https://github.com/dathlin/HslCommunication");
		}

		private void 免责条款ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			new FormHslMap().Show(dockPanel1);
		}

		private void ecologyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("http://www.hsltechnology.cn/Home/Ecology");
		}

		private void toolStripMenuItem_ApiDoc_Click(object sender, EventArgs e)
		{
			OpenWebside("http://api.hslcommunication.cn");
		}

		private void authorization授权ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			new FormCharge().Show(dockPanel1);
		}

		private void toolStripMenuItem_SerialPort_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderSerialDebug();
		}

		private void toolStripMenuItem_tcpUDP_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderTCPDebug();
		}

		private void pingTestToolStripMenuItem_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderPingTestDebug();
		}

		private void toolStripMenuItem_tcpUdpServer_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderTCPServerDebug();
		}

		private void toolStripMenuItem_serial2Tcp_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderSerial2TcpDebug();
		}

		private void toolStripMenuItem_tcp2Tcp_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderTCP2TcpDebug();
		}

		private void toolStripMenuItem_byteTransform_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderByteTransformDebug();
		}

		private void NewVersionToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				if (File.Exists(Path.Combine(Application.StartupPath, "Upgrade.exe")))
				{
					Process.Start(Path.Combine(Application.StartupPath, "Upgrade.exe"));
				}
				else if (File.Exists(Path.Combine(Application.StartupPath, "AutoUpdate.exe")))
				{
					Process.Start(Path.Combine(Application.StartupPath, "AutoUpdate.exe"));
				}
				else
				{
					Process.Start(Path.Combine(Application.StartupPath, "软件自动更新.exe"));
				}
				Thread.Sleep(50);
				Close();
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("更新软件丢失，无法启动更新： " + ex.Message);
			}
		}

		private void regexRegularToolStripMenuItem_Click(object sender, EventArgs e)
		{
			panelLeft?.RenderRegexDebug();
		}

		public FormSaveList GetPanelLeft()
		{
			return panelSave;
		}

		public void ShowDockForm(DockContent dockContent)
		{
			dockContent.Show(dockPanel1);
		}

		private void label_account_Click(object sender, EventArgs e)
		{
			if (mqttSyncClient != null)
			{
				if (DemoUtils.ShowMessage("是否注销当前用户？", "注销确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
				{
					mqttSyncClient.ConnectClose();
					mqttSyncClient = null;
					label_account.Text = "未登录";
				}
				return;
			}
			using (FormLogin formLogin = new FormLogin())
			{
				if (formLogin.ShowDialog() == DialogResult.OK)
				{
					mqttSyncClient = formLogin.GetMqttSyncClient();
					label_account.Text = "已登录";
					OperateResult<string> operateResult = mqttSyncClient.ReadRpc<string>("GetCompany", "");
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("获取公司信息失败: " + operateResult.Message);
					}
					else
					{
						new FormCreateCertificate(imageList, mqttSyncClient, operateResult.Content).Show(dockPanel1);
					}
				}
			}
		}

		private void lockToolStripMenuItem_Click(object sender, EventArgs e)
		{
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.FormMain));
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.toolStripMenuItem_language = new System.Windows.Forms.ToolStripMenuItem();
			this.简体中文ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.englishToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_Debug = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_CommunicationLog = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_SerialPort = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_tcpUDP = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_tcpUdpServer = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_serial2Tcp = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_tcp2Tcp = new System.Windows.Forms.ToolStripMenuItem();
			this.pingTestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_byteTransform = new System.Windows.Forms.ToolStripMenuItem();
			this.regexRegularToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.showMsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.formTopMostToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.testPanelSizeFixedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_Help = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_HomePage = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_ApiDoc = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_Doc = new System.Windows.Forms.ToolStripMenuItem();
			this.blogsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem_SourceCode = new System.Windows.Forms.ToolStripMenuItem();
			this.webSideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.support赞助ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.authorization授权ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.verisonToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.activeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ecologyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.免责条款ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.lockToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.授权ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.newVersionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.vS2015BlueTheme1 = new WeifenLuo.WinFormsUI.Docking.VS2015BlueTheme();
			this.dockPanel1 = new WeifenLuo.WinFormsUI.Docking.DockPanel();
			this.label1 = new System.Windows.Forms.Label();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.deleteDeviceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.label2 = new System.Windows.Forms.Label();
			this.label_account = new System.Windows.Forms.Label();
			this.menuStrip1.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.menuStrip1.BackColor = System.Drawing.Color.FromArgb(80, 100, 131);
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[10] { this.toolStripMenuItem_language, this.toolStripMenuItem_Debug, this.toolStripMenuItem_Help, this.verisonToolStripMenuItem, this.activeToolStripMenuItem, this.ecologyToolStripMenuItem, this.免责条款ToolStripMenuItem, this.lockToolStripMenuItem, this.授权ToolStripMenuItem, this.newVersionToolStripMenuItem });
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(1240, 25);
			this.menuStrip1.TabIndex = 26;
			this.menuStrip1.Text = "menuStrip1";
			this.toolStripMenuItem_language.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.简体中文ToolStripMenuItem, this.englishToolStripMenuItem });
			this.toolStripMenuItem_language.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.toolStripMenuItem_language.ImageTransparentColor = System.Drawing.Color.White;
			this.toolStripMenuItem_language.Name = "toolStripMenuItem_language";
			this.toolStripMenuItem_language.Size = new System.Drawing.Size(58, 21);
			this.toolStripMenuItem_language.Text = "语言(&L)";
			this.简体中文ToolStripMenuItem.ForeColor = System.Drawing.Color.Black;
			this.简体中文ToolStripMenuItem.Name = "简体中文ToolStripMenuItem";
			this.简体中文ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.简体中文ToolStripMenuItem.Text = "简体中文";
			this.简体中文ToolStripMenuItem.Click += new System.EventHandler(简体中文ToolStripMenuItem_Click);
			this.englishToolStripMenuItem.ForeColor = System.Drawing.Color.Black;
			this.englishToolStripMenuItem.Name = "englishToolStripMenuItem";
			this.englishToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
			this.englishToolStripMenuItem.Text = "English";
			this.englishToolStripMenuItem.Click += new System.EventHandler(englishToolStripMenuItem_Click);
			this.toolStripMenuItem_Debug.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[12]
			{
				this.toolStripMenuItem_CommunicationLog, this.toolStripMenuItem_SerialPort, this.toolStripMenuItem_tcpUDP, this.toolStripMenuItem_tcpUdpServer, this.toolStripMenuItem_serial2Tcp, this.toolStripMenuItem_tcp2Tcp, this.pingTestToolStripMenuItem, this.toolStripMenuItem_byteTransform, this.regexRegularToolStripMenuItem, this.showMsToolStripMenuItem,
				this.formTopMostToolStripMenuItem, this.testPanelSizeFixedToolStripMenuItem
			});
			this.toolStripMenuItem_Debug.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.toolStripMenuItem_Debug.Name = "toolStripMenuItem_Debug";
			this.toolStripMenuItem_Debug.Size = new System.Drawing.Size(76, 21);
			this.toolStripMenuItem_Debug.Text = "Debug(&D)";
			this.toolStripMenuItem_CommunicationLog.Image = HslCommunicationDemo.Properties.Resources.Log;
			this.toolStripMenuItem_CommunicationLog.Name = "toolStripMenuItem_CommunicationLog";
			this.toolStripMenuItem_CommunicationLog.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_CommunicationLog.Text = "CommunicationLog";
			this.toolStripMenuItem_CommunicationLog.Click += new System.EventHandler(toolStripMenuItem_CommunicationLog_Click);
			this.toolStripMenuItem_SerialPort.Image = HslCommunicationDemo.Properties.Resources.SerialPort;
			this.toolStripMenuItem_SerialPort.Name = "toolStripMenuItem_SerialPort";
			this.toolStripMenuItem_SerialPort.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_SerialPort.Text = "SerialPort";
			this.toolStripMenuItem_SerialPort.Click += new System.EventHandler(toolStripMenuItem_SerialPort_Click);
			this.toolStripMenuItem_tcpUDP.Image = HslCommunicationDemo.Properties.Resources.NetworkAdapter;
			this.toolStripMenuItem_tcpUDP.Name = "toolStripMenuItem_tcpUDP";
			this.toolStripMenuItem_tcpUDP.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_tcpUDP.Text = "Tcp/UDP";
			this.toolStripMenuItem_tcpUDP.Click += new System.EventHandler(toolStripMenuItem_tcpUDP_Click);
			this.toolStripMenuItem_tcpUdpServer.Image = HslCommunicationDemo.Properties.Resources.NetworkAdapter;
			this.toolStripMenuItem_tcpUdpServer.Name = "toolStripMenuItem_tcpUdpServer";
			this.toolStripMenuItem_tcpUdpServer.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_tcpUdpServer.Text = "Tcp/Udp Server";
			this.toolStripMenuItem_tcpUdpServer.Click += new System.EventHandler(toolStripMenuItem_tcpUdpServer_Click);
			this.toolStripMenuItem_serial2Tcp.Name = "toolStripMenuItem_serial2Tcp";
			this.toolStripMenuItem_serial2Tcp.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_serial2Tcp.Text = "Serial2Tcp";
			this.toolStripMenuItem_serial2Tcp.Click += new System.EventHandler(toolStripMenuItem_serial2Tcp_Click);
			this.toolStripMenuItem_tcp2Tcp.Name = "toolStripMenuItem_tcp2Tcp";
			this.toolStripMenuItem_tcp2Tcp.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_tcp2Tcp.Text = "Tcp2Tcp";
			this.toolStripMenuItem_tcp2Tcp.Click += new System.EventHandler(toolStripMenuItem_tcp2Tcp_Click);
			this.pingTestToolStripMenuItem.Name = "pingTestToolStripMenuItem";
			this.pingTestToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
			this.pingTestToolStripMenuItem.Text = "Ping Test";
			this.pingTestToolStripMenuItem.Click += new System.EventHandler(pingTestToolStripMenuItem_Click);
			this.toolStripMenuItem_byteTransform.Image = HslCommunicationDemo.Properties.Resources.bin;
			this.toolStripMenuItem_byteTransform.Name = "toolStripMenuItem_byteTransform";
			this.toolStripMenuItem_byteTransform.Size = new System.Drawing.Size(188, 22);
			this.toolStripMenuItem_byteTransform.Text = "ByteTransform";
			this.toolStripMenuItem_byteTransform.Click += new System.EventHandler(toolStripMenuItem_byteTransform_Click);
			this.regexRegularToolStripMenuItem.Name = "regexRegularToolStripMenuItem";
			this.regexRegularToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
			this.regexRegularToolStripMenuItem.Text = "RegexRegular";
			this.regexRegularToolStripMenuItem.Click += new System.EventHandler(regexRegularToolStripMenuItem_Click);
			this.showMsToolStripMenuItem.Name = "showMsToolStripMenuItem";
			this.showMsToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
			this.showMsToolStripMenuItem.Text = "读写时间显示毫秒";
			this.formTopMostToolStripMenuItem.Name = "formTopMostToolStripMenuItem";
			this.formTopMostToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
			this.formTopMostToolStripMenuItem.Text = "窗体置顶";
			this.formTopMostToolStripMenuItem.Click += new System.EventHandler(formTopMostToolStripMenuItem_Click);
			this.testPanelSizeFixedToolStripMenuItem.Name = "testPanelSizeFixedToolStripMenuItem";
			this.testPanelSizeFixedToolStripMenuItem.Size = new System.Drawing.Size(188, 22);
			this.testPanelSizeFixedToolStripMenuItem.Text = "测试界面大小固定";
			this.testPanelSizeFixedToolStripMenuItem.Click += new System.EventHandler(testPanelSizeFixedToolStripMenuItem_Click);
			this.toolStripMenuItem_Help.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[8] { this.toolStripMenuItem_HomePage, this.toolStripMenuItem_ApiDoc, this.toolStripMenuItem_Doc, this.blogsToolStripMenuItem, this.toolStripMenuItem_SourceCode, this.webSideToolStripMenuItem, this.support赞助ToolStripMenuItem, this.authorization授权ToolStripMenuItem });
			this.toolStripMenuItem_Help.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.toolStripMenuItem_Help.Name = "toolStripMenuItem_Help";
			this.toolStripMenuItem_Help.Size = new System.Drawing.Size(64, 21);
			this.toolStripMenuItem_Help.Text = "Help(&H)";
			this.toolStripMenuItem_HomePage.Image = HslCommunicationDemo.Properties.Resources.house_16xLG;
			this.toolStripMenuItem_HomePage.Name = "toolStripMenuItem_HomePage";
			this.toolStripMenuItem_HomePage.Size = new System.Drawing.Size(195, 22);
			this.toolStripMenuItem_HomePage.Text = "HomePage";
			this.toolStripMenuItem_HomePage.Click += new System.EventHandler(toolStripMenuItem_HomePage_Click);
			this.toolStripMenuItem_ApiDoc.Name = "toolStripMenuItem_ApiDoc";
			this.toolStripMenuItem_ApiDoc.Size = new System.Drawing.Size(195, 22);
			this.toolStripMenuItem_ApiDoc.Text = "ApiDoc";
			this.toolStripMenuItem_ApiDoc.Click += new System.EventHandler(toolStripMenuItem_ApiDoc_Click);
			this.toolStripMenuItem_Doc.Name = "toolStripMenuItem_Doc";
			this.toolStripMenuItem_Doc.Size = new System.Drawing.Size(195, 22);
			this.toolStripMenuItem_Doc.Text = "Document";
			this.toolStripMenuItem_Doc.Click += new System.EventHandler(toolStripMenuItem_Doc_Click_1);
			this.blogsToolStripMenuItem.Name = "blogsToolStripMenuItem";
			this.blogsToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
			this.blogsToolStripMenuItem.Text = "Blogs [博客]";
			this.blogsToolStripMenuItem.Click += new System.EventHandler(blogsToolStripMenuItem_Click);
			this.toolStripMenuItem_SourceCode.Image = HslCommunicationDemo.Properties.Resources.GitHub_Symbol;
			this.toolStripMenuItem_SourceCode.Name = "toolStripMenuItem_SourceCode";
			this.toolStripMenuItem_SourceCode.Size = new System.Drawing.Size(195, 22);
			this.toolStripMenuItem_SourceCode.Text = "SourceCode [源代码]";
			this.toolStripMenuItem_SourceCode.Click += new System.EventHandler(toolStripMenuItem_SourceCode_Click);
			this.webSideToolStripMenuItem.Name = "webSideToolStripMenuItem";
			this.webSideToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
			this.webSideToolStripMenuItem.Text = "WebSide [官网]";
			this.webSideToolStripMenuItem.Click += new System.EventHandler(webSideToolStripMenuItem_Click);
			this.support赞助ToolStripMenuItem.Name = "support赞助ToolStripMenuItem";
			this.support赞助ToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
			this.support赞助ToolStripMenuItem.Text = "Support [赞助]";
			this.authorization授权ToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.password;
			this.authorization授权ToolStripMenuItem.Name = "authorization授权ToolStripMenuItem";
			this.authorization授权ToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
			this.authorization授权ToolStripMenuItem.Text = "Authorization [授权]";
			this.authorization授权ToolStripMenuItem.Click += new System.EventHandler(authorization授权ToolStripMenuItem_Click);
			this.verisonToolStripMenuItem.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.verisonToolStripMenuItem.Name = "verisonToolStripMenuItem";
			this.verisonToolStripMenuItem.Size = new System.Drawing.Size(80, 21);
			this.verisonToolStripMenuItem.Text = "Verison(&V)";
			this.activeToolStripMenuItem.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.activeToolStripMenuItem.Name = "activeToolStripMenuItem";
			this.activeToolStripMenuItem.Size = new System.Drawing.Size(70, 21);
			this.activeToolStripMenuItem.Text = "Active(&A)";
			this.ecologyToolStripMenuItem.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.ecologyToolStripMenuItem.Name = "ecologyToolStripMenuItem";
			this.ecologyToolStripMenuItem.Size = new System.Drawing.Size(81, 21);
			this.ecologyToolStripMenuItem.Text = "Ecology(&E)";
			this.ecologyToolStripMenuItem.Click += new System.EventHandler(ecologyToolStripMenuItem_Click);
			this.免责条款ToolStripMenuItem.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.免责条款ToolStripMenuItem.Name = "免责条款ToolStripMenuItem";
			this.免责条款ToolStripMenuItem.Size = new System.Drawing.Size(92, 21);
			this.免责条款ToolStripMenuItem.Text = "全国使用情况";
			this.免责条款ToolStripMenuItem.Click += new System.EventHandler(免责条款ToolStripMenuItem_Click);
			this.lockToolStripMenuItem.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.lockToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.Lock_white;
			this.lockToolStripMenuItem.Name = "lockToolStripMenuItem";
			this.lockToolStripMenuItem.Size = new System.Drawing.Size(28, 21);
			this.lockToolStripMenuItem.ToolTipText = "点击锁定本DEMO程序，直到输入正确的密码为止";
			this.lockToolStripMenuItem.Click += new System.EventHandler(lockToolStripMenuItem_Click);
			this.授权ToolStripMenuItem.Name = "授权ToolStripMenuItem";
			this.授权ToolStripMenuItem.Size = new System.Drawing.Size(12, 21);
			this.newVersionToolStripMenuItem.ForeColor = System.Drawing.Color.Yellow;
			this.newVersionToolStripMenuItem.Name = "newVersionToolStripMenuItem";
			this.newVersionToolStripMenuItem.Size = new System.Drawing.Size(98, 21);
			this.newVersionToolStripMenuItem.Text = "New Version!";
			this.dockPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dockPanel1.DockLeftPortion = 225.0;
			this.dockPanel1.Location = new System.Drawing.Point(0, 25);
			this.dockPanel1.Name = "dockPanel1";
			this.dockPanel1.Size = new System.Drawing.Size(1240, 691);
			this.dockPanel1.TabIndex = 36;
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label1.BackColor = System.Drawing.Color.FromArgb(80, 100, 131);
			this.label1.ForeColor = System.Drawing.Color.FromArgb(255, 255, 128);
			this.label1.Location = new System.Drawing.Point(894, 3);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(230, 21);
			this.label1.TabIndex = 39;
			this.label1.Text = "Thread:0  Lock:0";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.deleteDeviceToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(152, 26);
			this.deleteDeviceToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.deleteDeviceToolStripMenuItem.Name = "deleteDeviceToolStripMenuItem";
			this.deleteDeviceToolStripMenuItem.Size = new System.Drawing.Size(151, 22);
			this.deleteDeviceToolStripMenuItem.Text = "DeleteDevice";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.BackColor = System.Drawing.Color.FromArgb(80, 100, 131);
			this.label2.ForeColor = System.Drawing.Color.FromArgb(255, 255, 128);
			this.label2.Location = new System.Drawing.Point(720, 3);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(148, 21);
			this.label2.TabIndex = 42;
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.label_account.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_account.BackColor = System.Drawing.Color.FromArgb(80, 100, 131);
			this.label_account.ForeColor = System.Drawing.Color.Cyan;
			this.label_account.Location = new System.Drawing.Point(1146, 5);
			this.label_account.Name = "label_account";
			this.label_account.Size = new System.Drawing.Size(94, 17);
			this.label_account.TabIndex = 45;
			this.label_account.Text = "登录";
			this.label_account.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.label_account.Click += new System.EventHandler(label_account_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.AliceBlue;
			base.ClientSize = new System.Drawing.Size(1240, 716);
			base.Controls.Add(this.label_account);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.dockPanel1);
			base.Controls.Add(this.menuStrip1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.IsMdiContainer = true;
			base.MainMenuStrip = this.menuStrip1;
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMain";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "HslCommunication Test Tools";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSelect_FormClosing);
			base.Load += new System.EventHandler(FormLoad_Load);
			base.Shown += new System.EventHandler(FormLoad_Shown);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
