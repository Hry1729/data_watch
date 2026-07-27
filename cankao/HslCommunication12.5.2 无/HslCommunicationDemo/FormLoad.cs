using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Enthernet;
using HslCommunication.Profinet.Siemens;
using HslCommunicationDemo.Algorithms;
using HslCommunicationDemo.BarCode;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Omron;
using HslCommunicationDemo.Robot;

namespace HslCommunicationDemo
{
	public class FormLoad : Form
	{
		public static Color ThemeColor = Color.AliceBlue;

		private IContainer components = null;

		private GroupBox groupBox1;

		private Button button3;

		private Button button2;

		private Button button1;

		private GroupBox groupBox2;

		private Button button6;

		private GroupBox groupBox3;

		private Button button4;

		private Button button5;

		private Button button7;

		private GroupBox groupBox4;

		private Button button8;

		private Button button9;

		private Button button10;

		private GroupBox groupBox5;

		private Button button11;

		private Button button12;

		private Button button13;

		private Button button14;

		private Button button15;

		private GroupBox groupBox6;

		private Button button16;

		private Button button18;

		private Button button17;

		private Button button19;

		private Button button20;

		private Button button21;

		private Button button22;

		private GroupBox groupBox7;

		private Button button23;

		private GroupBox groupBox8;

		private Button button24;

		private Button button25;

		private Button button27;

		private Button button26;

		private Button button28;

		private Button button29;

		private Button button30;

		private Button button31;

		private Button button32;

		private GroupBox groupBox9;

		private Button button33;

		private Button button34;

		private GroupBox groupBox10;

		private Button button35;

		private Button button36;

		private GroupBox groupBox11;

		private Button button37;

		private Button button38;

		private Button button39;

		private MenuStrip menuStrip1;

		private ToolStripMenuItem aboutToolStripMenuItem;

		private ToolStripMenuItem blogsToolStripMenuItem;

		private ToolStripMenuItem webSideToolStripMenuItem;

		private ToolStripMenuItem mesDemoToolStripMenuItem;

		private ToolStripMenuItem rToolStripMenuItem;

		private ToolStripMenuItem 简体中文ToolStripMenuItem;

		private ToolStripMenuItem englishToolStripMenuItem;

		private ToolStripMenuItem 日志ToolStripMenuItem;

		private ToolStripMenuItem verisonToolStripMenuItem;

		private GroupBox groupBox12;

		private Button button40;

		private Button button41;

		private Button button42;

		private Button button43;

		private ToolStripMenuItem 免责条款ToolStripMenuItem;

		private LinkLabel linkLabel1;

		private Label label1;

		private Button button44;

		private Button button45;

		private Button button46;

		private Button button47;

		private Button button48;

		private GroupBox groupBox13;

		private Button button50;

		private Button button49;

		private Button button51;

		private Button button52;

		private Button button53;

		private Button button54;

		private ToolStripMenuItem 论坛toolStripMenuItem;

		private Label label2;

		private GroupBox groupBox14;

		private Button button55;

		private Button button56;

		private Button button57;

		private Button button58;

		private Button button59;

		private Button button60;

		private ToolStripMenuItem 授权ToolStripMenuItem;

		private ToolStripMenuItem support赞助ToolStripMenuItem;

		private Button button61;

		private GroupBox groupBox15;

		private Button button63;

		private GroupBox groupBox16;

		private Button button62;

		private GroupBox groupBox17;

		private Button button64;

		private Button button65;

		private Button button66;

		private Button button67;

		private Button button68;

		private Button button69;

		private Button button70;

		public FormLoad()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S1200))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button6_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormModbus formModbus = new FormModbus())
			{
				formModbus.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button4_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsecBinary formMelsecBinary = new FormMelsecBinary())
			{
				formMelsecBinary.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S1500))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button3_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S300))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button39_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S400))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S200Smart))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void linkLabel8_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			try
			{
				Process.Start("http://118.24.36.220:8080/html/c136d3de-eab7-9b0f-4bdf-d891297c8018.htm");
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button7_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsecAscii formMelsecAscii = new FormMelsecAscii())
			{
				formMelsecAscii.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void linkLabel2_Click(object sender, EventArgs e)
		{
			new FormSupport().ShowDialog();
		}

		private void button8_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSimplifyNet formSimplifyNet = new FormSimplifyNet())
			{
				formSimplifyNet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button9_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormUdpNet formUdpNet = new FormUdpNet())
			{
				formUdpNet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button10_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemensFW formSiemensFW = new FormSiemensFW())
			{
				formSiemensFW.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button11_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormOmron formOmron = new FormOmron())
			{
				formOmron.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button12_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormFileClient formFileClient = new FormFileClient())
			{
				formFileClient.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button14_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormComplexNet formComplexNet = new FormComplexNet())
			{
				formComplexNet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button13_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsec1EBinary formMelsec1EBinary = new FormMelsec1EBinary())
			{
				formMelsec1EBinary.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button15_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormLogNet formLogNet = new FormLogNet())
			{
				formLogNet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button19_Click(object sender, EventArgs e)
		{
		}

		private void FormLoad_Load(object sender, EventArgs e)
		{
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
		}

		private void Support赞助ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (FormSupport formSupport = new FormSupport())
			{
				formSupport.ShowDialog();
			}
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				button19.Text = "异形 Modbus Tcp";
				button24.Text = "串口调试助手";
				button30.Text = "以太网调试助手";
				button25.Text = "傅立叶变换";
				button26.Text = "订单号测试";
				button27.Text = "注册码功能";
				button28.Text = "邮件发送";
				button31.Text = "字节变换";
				button29.Text = "异形 Simplify Net";
				button16.Text = "常用简单控件";
				Text = "HslCommunication 测试工具";
				免责条款ToolStripMenuItem.Text = "免责条款";
				论坛toolStripMenuItem.Text = "论坛";
				授权ToolStripMenuItem.Text = "授权";
			}
			else
			{
				button19.Text = "Alien Modbus Tcp";
				button24.Text = "Serial debug";
				button30.Text = "Tcp/Ip debug";
				button25.Text = "Fourier transformation";
				button26.Text = "OrderNumbe";
				button27.Text = "Registration";
				button28.Text = "Mail Send";
				button31.Text = "Bytes Transform";
				button29.Text = "Alien Simplify Net";
				button16.Text = "Simple Control";
				Text = "HslCommunication Test Tool";
				论坛toolStripMenuItem.Text = "BBS";
				免责条款ToolStripMenuItem.Text = "Disclaimer";
				授权ToolStripMenuItem.Text = "Authorize";
			}
		}

		private void LinkLabel6_Click(object sender, EventArgs e)
		{
			StringResources.SeteLanguageEnglish();
			Program.Language = 2;
			Language(Program.Language);
			DemoUtils.ShowMessage("Select English!");
		}

		private void LinkLabel5_Click(object sender, EventArgs e)
		{
			StringResources.SetLanguageChinese();
			Program.Language = 1;
			Language(Program.Language);
			DemoUtils.ShowMessage("已选择中文");
		}

		private void 论坛toolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				Process.Start("http://bbs.hslcommunication.cn/");
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button21_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormModbusRtu formModbusRtu = new FormModbusRtu())
			{
				formModbusRtu.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button22_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormPushNet formPushNet = new FormPushNet())
			{
				formPushNet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button23_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormEfort formEfort = new FormEfort())
			{
				formEfort.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button24_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSerialDebug formSerialDebug = new FormSerialDebug())
			{
				formSerialDebug.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button25_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FourierTransform fourierTransform = new FourierTransform())
			{
				fourierTransform.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button26_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSeqCreate formSeqCreate = new FormSeqCreate())
			{
				formSeqCreate.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button27_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormRegister formRegister = new FormRegister())
			{
				formRegister.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button28_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMail formMail = new FormMail())
			{
				formMail.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button29_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSimplifyNetAlien formSimplifyNetAlien = new FormSimplifyNetAlien())
			{
				formSimplifyNetAlien.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button30_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormTcpDebug formTcpDebug = new FormTcpDebug())
			{
				formTcpDebug.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button31_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormByteTransfer formByteTransfer = new FormByteTransfer())
			{
				formByteTransfer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button32_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsecSerial formMelsecSerial = new FormMelsecSerial())
			{
				formMelsecSerial.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button33_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormAllenBrandly formAllenBrandly = new FormAllenBrandly())
			{
				formAllenBrandly.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button34_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormModbusAscii formModbusAscii = new FormModbusAscii())
			{
				formModbusAscii.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button35_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormPanasonicMew formPanasonicMew = new FormPanasonicMew())
			{
				formPanasonicMew.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button36_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemensPPI formSiemensPPI = new FormSiemensPPI())
			{
				formSiemensPPI.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button37_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormDAM3601 formDAM = new FormDAM3601())
			{
				formDAM.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button38_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsecLinks formMelsecLinks = new FormMelsecLinks())
			{
				formMelsecLinks.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
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

		private void mesDemoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("http://118.24.36.220:8081/");
		}

		private void gitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("https://github.com/dathlin/HslCommunication");
		}

		private void patronageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			new FormSupport().ShowDialog();
		}

		private void 简体中文ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringResources.SetLanguageChinese();
			Program.Language = 1;
			Settings1.Default.language = Program.Language;
			Settings1.Default.Save();
			Language(Program.Language);
			DemoUtils.ShowMessage("已选择中文");
		}

		private void englishToolStripMenuItem_Click(object sender, EventArgs e)
		{
			StringResources.SeteLanguageEnglish();
			Program.Language = 2;
			Settings1.Default.language = Program.Language;
			Settings1.Default.Save();
			Language(Program.Language);
			DemoUtils.ShowMessage("Select English!");
		}

		private void 日志ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenWebside("http://118.24.36.220:8080/html/c136d3de-eab7-9b0f-4bdf-d891297c8018.htm");
		}

		private void button40_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormRedisClient formRedisClient = new FormRedisClient())
			{
				formRedisClient.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button41_Click(object sender, EventArgs e)
		{
			Hide();
			Show();
		}

		private void button42_Click(object sender, EventArgs e)
		{
		}

		private void button43_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormRedisSubscribe formRedisSubscribe = new FormRedisSubscribe())
			{
				formRedisSubscribe.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void FormLoad_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(ThreadPoolCheckVersion, null);
		}

		private void ThreadPoolCheckVersion(object obj)
		{
			Thread.Sleep(100);
			OperateResult<NetHandle, string> read = new NetSimplifyClient("118.24.36.220", 18467).ReadCustomerFromServer(1, SoftBasic.FrameworkVersion.ToString());
			if (!read.IsSuccess || !(new SystemVersion(read.Content2) > SoftBasic.FrameworkVersion))
			{
				return;
			}
			Invoke((Action)delegate
			{
				if (DemoUtils.ShowMessage("服务器有新版本：" + read.Content2 + Environment.NewLine + "是否启动更新？", "检测到更新", MessageBoxButtons.YesNo) == DialogResult.Yes)
				{
					try
					{
						Process.Start(Application.StartupPath + "\\软件自动更新.exe");
						Thread.Sleep(50);
						Close();
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage("更新软件丢失，无法启动更新： " + ex.Message);
					}
				}
			});
		}

		private void 免责条款ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormDisclaimer formDisclaimer = new FormDisclaimer())
			{
				formDisclaimer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			OpenWebside("https://github.com/dathlin/HslControlsDemo");
		}

		private void rToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (FormAuthor formAuthor = new FormAuthor())
			{
				formAuthor.ShowDialog();
			}
		}

		private void button44_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormEfortPrevious formEfortPrevious = new FormEfortPrevious())
			{
				formEfortPrevious.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button45_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormModbusServer formModbusServer = new FormModbusServer())
			{
				formModbusServer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button46_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormS7Server formS7Server = new FormS7Server())
			{
				formS7Server.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button47_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMelsec3C formMelsec3C = new FormMelsec3C())
			{
				formMelsec3C.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button48_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormOmronHostLink formOmronHostLink = new FormOmronHostLink())
			{
				formOmronHostLink.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button49_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormKeyenceBinary formKeyenceBinary = new FormKeyenceBinary())
			{
				formKeyenceBinary.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button50_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormKeyenceAscii formKeyenceAscii = new FormKeyenceAscii())
			{
				formKeyenceAscii.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button51_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormKuka formKuka = new FormKuka())
			{
				formKuka.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button52_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormOmronUdp formOmronUdp = new FormOmronUdp())
			{
				formOmronUdp.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button53_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSiemens formSiemens = new FormSiemens(SiemensPLCS.S200))
			{
				formSiemens.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void button54_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormKeyenceNanoSerial formKeyenceNanoSerial = new FormKeyenceNanoSerial())
			{
				formKeyenceNanoSerial.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button55_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormLsisFEnet formLsisFEnet = new FormLsisFEnet())
			{
				formLsisFEnet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button56_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormMcServer formMcServer = new FormMcServer())
			{
				formMcServer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button57_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormLsisCnet formLsisCnet = new FormLsisCnet())
			{
				formLsisCnet.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button58_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormPanasonicBinary formPanasonicBinary = new FormPanasonicBinary())
			{
				formPanasonicBinary.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button59_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormLSisServer formLSisServer = new FormLSisServer())
			{
				formLSisServer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button61_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormOmronCip formOmronCip = new FormOmronCip())
			{
				formOmronCip.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button63_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormFatekPrograme formFatekPrograme = new FormFatekPrograme())
			{
				formFatekPrograme.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button62_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormFujiSPB formFujiSPB = new FormFujiSPB())
			{
				formFujiSPB.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button64_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormOpenProtocol formOpenProtocol = new FormOpenProtocol())
			{
				formOpenProtocol.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button65_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormPid formPid = new FormPid())
			{
				formPid.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button66_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormSickBarCode formSickBarCode = new FormSickBarCode())
			{
				formSickBarCode.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button67_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormTcpServer formTcpServer = new FormTcpServer())
			{
				formTcpServer.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button68_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormModbusRtuOverTcp formModbusRtuOverTcp = new FormModbusRtuOverTcp())
			{
				formModbusRtuOverTcp.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button69_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FormABBWebApi formABBWebApi = new FormABBWebApi())
			{
				formABBWebApi.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
		}

		private void Button70_Click(object sender, EventArgs e)
		{
			Hide();
			Thread.Sleep(200);
			using (FourierFilter fourierFilter = new FourierFilter())
			{
				fourierFilter.ShowDialog();
			}
			Thread.Sleep(200);
			Show();
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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.button53 = new System.Windows.Forms.Button();
			this.button46 = new System.Windows.Forms.Button();
			this.button39 = new System.Windows.Forms.Button();
			this.button36 = new System.Windows.Forms.Button();
			this.button10 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.button68 = new System.Windows.Forms.Button();
			this.button45 = new System.Windows.Forms.Button();
			this.button34 = new System.Windows.Forms.Button();
			this.button21 = new System.Windows.Forms.Button();
			this.button19 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.button56 = new System.Windows.Forms.Button();
			this.button47 = new System.Windows.Forms.Button();
			this.button38 = new System.Windows.Forms.Button();
			this.button32 = new System.Windows.Forms.Button();
			this.button13 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.button29 = new System.Windows.Forms.Button();
			this.button22 = new System.Windows.Forms.Button();
			this.button15 = new System.Windows.Forms.Button();
			this.button14 = new System.Windows.Forms.Button();
			this.button12 = new System.Windows.Forms.Button();
			this.button9 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.button61 = new System.Windows.Forms.Button();
			this.button52 = new System.Windows.Forms.Button();
			this.button48 = new System.Windows.Forms.Button();
			this.button11 = new System.Windows.Forms.Button();
			this.groupBox6 = new System.Windows.Forms.GroupBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label1 = new System.Windows.Forms.Label();
			this.button41 = new System.Windows.Forms.Button();
			this.button20 = new System.Windows.Forms.Button();
			this.button18 = new System.Windows.Forms.Button();
			this.button17 = new System.Windows.Forms.Button();
			this.button16 = new System.Windows.Forms.Button();
			this.groupBox7 = new System.Windows.Forms.GroupBox();
			this.button69 = new System.Windows.Forms.Button();
			this.button60 = new System.Windows.Forms.Button();
			this.button51 = new System.Windows.Forms.Button();
			this.button44 = new System.Windows.Forms.Button();
			this.button23 = new System.Windows.Forms.Button();
			this.groupBox8 = new System.Windows.Forms.GroupBox();
			this.button31 = new System.Windows.Forms.Button();
			this.button30 = new System.Windows.Forms.Button();
			this.button28 = new System.Windows.Forms.Button();
			this.button27 = new System.Windows.Forms.Button();
			this.button26 = new System.Windows.Forms.Button();
			this.button25 = new System.Windows.Forms.Button();
			this.button24 = new System.Windows.Forms.Button();
			this.groupBox9 = new System.Windows.Forms.GroupBox();
			this.button33 = new System.Windows.Forms.Button();
			this.groupBox10 = new System.Windows.Forms.GroupBox();
			this.button58 = new System.Windows.Forms.Button();
			this.button35 = new System.Windows.Forms.Button();
			this.groupBox11 = new System.Windows.Forms.GroupBox();
			this.button37 = new System.Windows.Forms.Button();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.blogsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.webSideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.mesDemoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.rToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.support赞助ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.简体中文ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.englishToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.论坛toolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.日志ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.verisonToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.免责条款ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.授权ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.groupBox12 = new System.Windows.Forms.GroupBox();
			this.button43 = new System.Windows.Forms.Button();
			this.button42 = new System.Windows.Forms.Button();
			this.button40 = new System.Windows.Forms.Button();
			this.groupBox13 = new System.Windows.Forms.GroupBox();
			this.button54 = new System.Windows.Forms.Button();
			this.button50 = new System.Windows.Forms.Button();
			this.button49 = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox14 = new System.Windows.Forms.GroupBox();
			this.button59 = new System.Windows.Forms.Button();
			this.button57 = new System.Windows.Forms.Button();
			this.button55 = new System.Windows.Forms.Button();
			this.groupBox15 = new System.Windows.Forms.GroupBox();
			this.button63 = new System.Windows.Forms.Button();
			this.groupBox16 = new System.Windows.Forms.GroupBox();
			this.button62 = new System.Windows.Forms.Button();
			this.groupBox17 = new System.Windows.Forms.GroupBox();
			this.button70 = new System.Windows.Forms.Button();
			this.button67 = new System.Windows.Forms.Button();
			this.button66 = new System.Windows.Forms.Button();
			this.button65 = new System.Windows.Forms.Button();
			this.button64 = new System.Windows.Forms.Button();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox5.SuspendLayout();
			this.groupBox6.SuspendLayout();
			this.groupBox7.SuspendLayout();
			this.groupBox8.SuspendLayout();
			this.groupBox9.SuspendLayout();
			this.groupBox10.SuspendLayout();
			this.groupBox11.SuspendLayout();
			this.menuStrip1.SuspendLayout();
			this.groupBox12.SuspendLayout();
			this.groupBox13.SuspendLayout();
			this.groupBox14.SuspendLayout();
			this.groupBox15.SuspendLayout();
			this.groupBox16.SuspendLayout();
			this.groupBox17.SuspendLayout();
			base.SuspendLayout();
			this.groupBox1.Controls.Add(this.button53);
			this.groupBox1.Controls.Add(this.button46);
			this.groupBox1.Controls.Add(this.button39);
			this.groupBox1.Controls.Add(this.button36);
			this.groupBox1.Controls.Add(this.button10);
			this.groupBox1.Controls.Add(this.button5);
			this.groupBox1.Controls.Add(this.button3);
			this.groupBox1.Controls.Add(this.button2);
			this.groupBox1.Controls.Add(this.button1);
			this.groupBox1.Location = new System.Drawing.Point(203, 29);
			this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox1.Size = new System.Drawing.Size(186, 335);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Siemens PLC(西门子 PLC)";
			this.button53.Location = new System.Drawing.Point(18, 104);
			this.button53.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button53.Name = "button53";
			this.button53.Size = new System.Drawing.Size(150, 32);
			this.button53.TabIndex = 8;
			this.button53.Text = "s7-200";
			this.button53.UseVisualStyleBackColor = true;
			this.button53.Click += new System.EventHandler(Button53_Click);
			this.button46.Location = new System.Drawing.Point(18, 264);
			this.button46.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button46.Name = "button46";
			this.button46.Size = new System.Drawing.Size(150, 32);
			this.button46.TabIndex = 7;
			this.button46.Text = "S7 Virtual Server";
			this.button46.UseVisualStyleBackColor = true;
			this.button46.Click += new System.EventHandler(button46_Click);
			this.button39.Location = new System.Drawing.Point(98, 64);
			this.button39.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button39.Name = "button39";
			this.button39.Size = new System.Drawing.Size(70, 32);
			this.button39.TabIndex = 6;
			this.button39.Text = "s7-400";
			this.button39.UseVisualStyleBackColor = true;
			this.button39.Click += new System.EventHandler(button39_Click);
			this.button36.Location = new System.Drawing.Point(18, 224);
			this.button36.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button36.Name = "button36";
			this.button36.Size = new System.Drawing.Size(150, 32);
			this.button36.TabIndex = 5;
			this.button36.Text = "PPI";
			this.button36.UseVisualStyleBackColor = true;
			this.button36.Click += new System.EventHandler(button36_Click);
			this.button10.Location = new System.Drawing.Point(18, 184);
			this.button10.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button10.Name = "button10";
			this.button10.Size = new System.Drawing.Size(150, 32);
			this.button10.TabIndex = 4;
			this.button10.Text = "Fetch/Write";
			this.button10.UseVisualStyleBackColor = true;
			this.button10.Click += new System.EventHandler(button10_Click);
			this.button5.Location = new System.Drawing.Point(18, 144);
			this.button5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(150, 32);
			this.button5.TabIndex = 3;
			this.button5.Text = "s7-200Smart";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button3.Location = new System.Drawing.Point(18, 64);
			this.button3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(70, 32);
			this.button3.TabIndex = 2;
			this.button3.Text = "s7-300";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.button2.Location = new System.Drawing.Point(98, 24);
			this.button2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(70, 32);
			this.button2.TabIndex = 1;
			this.button2.Text = "s7-1500";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(18, 24);
			this.button1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(70, 32);
			this.button1.TabIndex = 0;
			this.button1.Text = "s7-1200";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.groupBox2.Controls.Add(this.button68);
			this.groupBox2.Controls.Add(this.button45);
			this.groupBox2.Controls.Add(this.button34);
			this.groupBox2.Controls.Add(this.button21);
			this.groupBox2.Controls.Add(this.button19);
			this.groupBox2.Controls.Add(this.button6);
			this.groupBox2.Location = new System.Drawing.Point(395, 29);
			this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox2.Size = new System.Drawing.Size(185, 335);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Modbus";
			this.button68.Location = new System.Drawing.Point(19, 224);
			this.button68.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button68.Name = "button68";
			this.button68.Size = new System.Drawing.Size(150, 32);
			this.button68.TabIndex = 5;
			this.button68.Text = "ModbusRtu Over Tcp";
			this.button68.UseVisualStyleBackColor = true;
			this.button68.Click += new System.EventHandler(Button68_Click);
			this.button45.Location = new System.Drawing.Point(19, 184);
			this.button45.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button45.Name = "button45";
			this.button45.Size = new System.Drawing.Size(150, 32);
			this.button45.TabIndex = 4;
			this.button45.Text = "Modbus Server";
			this.button45.UseVisualStyleBackColor = true;
			this.button45.Click += new System.EventHandler(button45_Click);
			this.button34.Location = new System.Drawing.Point(19, 144);
			this.button34.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button34.Name = "button34";
			this.button34.Size = new System.Drawing.Size(150, 32);
			this.button34.TabIndex = 3;
			this.button34.Text = "Modbus Ascii";
			this.button34.UseVisualStyleBackColor = true;
			this.button34.Click += new System.EventHandler(button34_Click);
			this.button21.Location = new System.Drawing.Point(19, 104);
			this.button21.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button21.Name = "button21";
			this.button21.Size = new System.Drawing.Size(150, 32);
			this.button21.TabIndex = 2;
			this.button21.Text = "Modbus Rtu";
			this.button21.UseVisualStyleBackColor = true;
			this.button21.Click += new System.EventHandler(button21_Click);
			this.button19.Location = new System.Drawing.Point(19, 64);
			this.button19.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button19.Name = "button19";
			this.button19.Size = new System.Drawing.Size(150, 32);
			this.button19.TabIndex = 1;
			this.button19.Text = "异形 Modbus Tcp";
			this.button19.UseVisualStyleBackColor = true;
			this.button19.Click += new System.EventHandler(button19_Click);
			this.button6.Location = new System.Drawing.Point(19, 24);
			this.button6.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(150, 32);
			this.button6.TabIndex = 0;
			this.button6.Text = "Modbus Tcp";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.groupBox3.Controls.Add(this.button56);
			this.groupBox3.Controls.Add(this.button47);
			this.groupBox3.Controls.Add(this.button38);
			this.groupBox3.Controls.Add(this.button32);
			this.groupBox3.Controls.Add(this.button13);
			this.groupBox3.Controls.Add(this.button7);
			this.groupBox3.Controls.Add(this.button4);
			this.groupBox3.Location = new System.Drawing.Point(9, 29);
			this.groupBox3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox3.Size = new System.Drawing.Size(183, 335);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Melsec PLC(三菱 PLC)";
			this.button56.Location = new System.Drawing.Point(15, 264);
			this.button56.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button56.Name = "button56";
			this.button56.Size = new System.Drawing.Size(150, 32);
			this.button56.TabIndex = 7;
			this.button56.Text = "Mc Virtual Server";
			this.button56.UseVisualStyleBackColor = true;
			this.button56.Click += new System.EventHandler(Button56_Click);
			this.button47.Location = new System.Drawing.Point(15, 224);
			this.button47.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button47.Name = "button47";
			this.button47.Size = new System.Drawing.Size(150, 32);
			this.button47.TabIndex = 6;
			this.button47.Text = "A-3C (format1)";
			this.button47.UseVisualStyleBackColor = true;
			this.button47.Click += new System.EventHandler(button47_Click);
			this.button38.Location = new System.Drawing.Point(15, 184);
			this.button38.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button38.Name = "button38";
			this.button38.Size = new System.Drawing.Size(150, 32);
			this.button38.TabIndex = 5;
			this.button38.Text = "Fx Links【485】";
			this.button38.UseVisualStyleBackColor = true;
			this.button38.Click += new System.EventHandler(button38_Click);
			this.button32.Location = new System.Drawing.Point(15, 144);
			this.button32.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button32.Name = "button32";
			this.button32.Size = new System.Drawing.Size(150, 32);
			this.button32.TabIndex = 4;
			this.button32.Text = "Fx Serial【编程口】";
			this.button32.UseVisualStyleBackColor = true;
			this.button32.Click += new System.EventHandler(button32_Click);
			this.button13.Location = new System.Drawing.Point(15, 104);
			this.button13.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button13.Name = "button13";
			this.button13.Size = new System.Drawing.Size(150, 32);
			this.button13.TabIndex = 3;
			this.button13.Text = "A-1E (Binary)";
			this.button13.UseVisualStyleBackColor = true;
			this.button13.Click += new System.EventHandler(button13_Click);
			this.button7.Location = new System.Drawing.Point(15, 64);
			this.button7.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(150, 32);
			this.button7.TabIndex = 2;
			this.button7.Text = "MC(ASCII)";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button4.Location = new System.Drawing.Point(15, 24);
			this.button4.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(150, 32);
			this.button4.TabIndex = 1;
			this.button4.Text = "MC (Binary)";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.groupBox4.Controls.Add(this.button29);
			this.groupBox4.Controls.Add(this.button22);
			this.groupBox4.Controls.Add(this.button15);
			this.groupBox4.Controls.Add(this.button14);
			this.groupBox4.Controls.Add(this.button12);
			this.groupBox4.Controls.Add(this.button9);
			this.groupBox4.Controls.Add(this.button8);
			this.groupBox4.Location = new System.Drawing.Point(9, 372);
			this.groupBox4.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox4.Size = new System.Drawing.Size(183, 315);
			this.groupBox4.TabIndex = 7;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "Hsl Support(HSL 协议)";
			this.button29.Location = new System.Drawing.Point(15, 64);
			this.button29.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button29.Name = "button29";
			this.button29.Size = new System.Drawing.Size(150, 32);
			this.button29.TabIndex = 6;
			this.button29.Text = "异形 Simplify Net";
			this.button29.UseVisualStyleBackColor = true;
			this.button29.Click += new System.EventHandler(button29_Click);
			this.button22.Location = new System.Drawing.Point(15, 264);
			this.button22.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button22.Name = "button22";
			this.button22.Size = new System.Drawing.Size(150, 32);
			this.button22.TabIndex = 5;
			this.button22.Text = "Push Net (消息推送)";
			this.button22.UseVisualStyleBackColor = true;
			this.button22.Click += new System.EventHandler(button22_Click);
			this.button15.Location = new System.Drawing.Point(15, 224);
			this.button15.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button15.Name = "button15";
			this.button15.Size = new System.Drawing.Size(150, 32);
			this.button15.TabIndex = 4;
			this.button15.Text = "Log (日志记录)";
			this.button15.UseVisualStyleBackColor = true;
			this.button15.Click += new System.EventHandler(button15_Click);
			this.button14.Location = new System.Drawing.Point(15, 104);
			this.button14.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button14.Name = "button14";
			this.button14.Size = new System.Drawing.Size(150, 32);
			this.button14.TabIndex = 3;
			this.button14.Text = "Complex (复杂交互)";
			this.button14.UseVisualStyleBackColor = true;
			this.button14.Click += new System.EventHandler(button14_Click);
			this.button12.Location = new System.Drawing.Point(15, 184);
			this.button12.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button12.Name = "button12";
			this.button12.Size = new System.Drawing.Size(150, 32);
			this.button12.TabIndex = 2;
			this.button12.Text = "File Net (文件收发)";
			this.button12.UseVisualStyleBackColor = true;
			this.button12.Click += new System.EventHandler(button12_Click);
			this.button9.Location = new System.Drawing.Point(15, 144);
			this.button9.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(150, 32);
			this.button9.TabIndex = 1;
			this.button9.Text = "Udp Net (Udp数据)";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.button8.Location = new System.Drawing.Point(15, 24);
			this.button8.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(150, 32);
			this.button8.TabIndex = 0;
			this.button8.Text = "Simplify Net (请求交互)";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.groupBox5.Controls.Add(this.button61);
			this.groupBox5.Controls.Add(this.button52);
			this.groupBox5.Controls.Add(this.button48);
			this.groupBox5.Controls.Add(this.button11);
			this.groupBox5.Location = new System.Drawing.Point(586, 29);
			this.groupBox5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox5.Size = new System.Drawing.Size(185, 187);
			this.groupBox5.TabIndex = 11;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "Omron PLC(欧姆龙 PLC)";
			this.button61.Location = new System.Drawing.Point(19, 144);
			this.button61.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button61.Name = "button61";
			this.button61.Size = new System.Drawing.Size(150, 32);
			this.button61.TabIndex = 3;
			this.button61.Text = "CIP net";
			this.button61.UseVisualStyleBackColor = true;
			this.button61.Click += new System.EventHandler(Button61_Click);
			this.button52.Location = new System.Drawing.Point(19, 64);
			this.button52.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button52.Name = "button52";
			this.button52.Size = new System.Drawing.Size(150, 32);
			this.button52.TabIndex = 2;
			this.button52.Text = "Fins Udp";
			this.button52.UseVisualStyleBackColor = true;
			this.button52.Click += new System.EventHandler(button52_Click);
			this.button48.Location = new System.Drawing.Point(19, 104);
			this.button48.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button48.Name = "button48";
			this.button48.Size = new System.Drawing.Size(150, 32);
			this.button48.TabIndex = 1;
			this.button48.Text = "Fins HostLink";
			this.button48.UseVisualStyleBackColor = true;
			this.button48.Click += new System.EventHandler(button48_Click);
			this.button11.Location = new System.Drawing.Point(19, 24);
			this.button11.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(150, 32);
			this.button11.TabIndex = 0;
			this.button11.Text = "Fins Tcp";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.groupBox6.Controls.Add(this.linkLabel1);
			this.groupBox6.Controls.Add(this.label1);
			this.groupBox6.Controls.Add(this.button41);
			this.groupBox6.Controls.Add(this.button20);
			this.groupBox6.Controls.Add(this.button18);
			this.groupBox6.Controls.Add(this.button17);
			this.groupBox6.Controls.Add(this.button16);
			this.groupBox6.Location = new System.Drawing.Point(203, 372);
			this.groupBox6.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox6.Name = "groupBox6";
			this.groupBox6.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox6.Size = new System.Drawing.Size(185, 315);
			this.groupBox6.TabIndex = 12;
			this.groupBox6.TabStop = false;
			this.groupBox6.Text = "Controls(控件)";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(16, 279);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(110, 17);
			this.linkLabel1.TabIndex = 6;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "HslControlsDemo";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			this.label1.ForeColor = System.Drawing.Color.Red;
			this.label1.Location = new System.Drawing.Point(16, 236);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(153, 38);
			this.label1.TabIndex = 5;
			this.label1.Text = "控件不再更新，所有控件转移到专门的控件库。";
			this.button41.Location = new System.Drawing.Point(19, 184);
			this.button41.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button41.Name = "button41";
			this.button41.Size = new System.Drawing.Size(150, 32);
			this.button41.TabIndex = 4;
			this.button41.Text = "Pipe (管道组态)";
			this.button41.UseVisualStyleBackColor = true;
			this.button41.Click += new System.EventHandler(button41_Click);
			this.button20.Location = new System.Drawing.Point(19, 143);
			this.button20.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button20.Name = "button20";
			this.button20.Size = new System.Drawing.Size(150, 32);
			this.button20.TabIndex = 3;
			this.button20.Text = "Pie Chart (饼图)";
			this.button20.UseVisualStyleBackColor = true;
			this.button18.Location = new System.Drawing.Point(19, 103);
			this.button18.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button18.Name = "button18";
			this.button18.Size = new System.Drawing.Size(150, 32);
			this.button18.TabIndex = 2;
			this.button18.Text = "Curve (曲线)";
			this.button18.UseVisualStyleBackColor = true;
			this.button17.Location = new System.Drawing.Point(19, 63);
			this.button17.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button17.Name = "button17";
			this.button17.Size = new System.Drawing.Size(150, 32);
			this.button17.TabIndex = 1;
			this.button17.Text = "Gauge (仪表盘)";
			this.button17.UseVisualStyleBackColor = true;
			this.button16.Location = new System.Drawing.Point(19, 24);
			this.button16.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button16.Name = "button16";
			this.button16.Size = new System.Drawing.Size(150, 32);
			this.button16.TabIndex = 0;
			this.button16.Text = "Simple Control";
			this.button16.UseVisualStyleBackColor = true;
			this.groupBox7.Controls.Add(this.button69);
			this.groupBox7.Controls.Add(this.button60);
			this.groupBox7.Controls.Add(this.button51);
			this.groupBox7.Controls.Add(this.button44);
			this.groupBox7.Controls.Add(this.button23);
			this.groupBox7.Location = new System.Drawing.Point(395, 372);
			this.groupBox7.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox7.Name = "groupBox7";
			this.groupBox7.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox7.Size = new System.Drawing.Size(185, 315);
			this.groupBox7.TabIndex = 14;
			this.groupBox7.TabStop = false;
			this.groupBox7.Text = "Robot(机器人)";
			this.button69.Location = new System.Drawing.Point(19, 184);
			this.button69.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button69.Name = "button69";
			this.button69.Size = new System.Drawing.Size(150, 32);
			this.button69.TabIndex = 4;
			this.button69.Text = "ABB";
			this.button69.UseVisualStyleBackColor = true;
			this.button69.Click += new System.EventHandler(Button69_Click);
			this.button60.Location = new System.Drawing.Point(19, 143);
			this.button60.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button60.Name = "button60";
			this.button60.Size = new System.Drawing.Size(150, 32);
			this.button60.TabIndex = 3;
			this.button60.Text = "YRC1000(YASKAWA)";
			this.button60.UseVisualStyleBackColor = true;
			this.button51.Location = new System.Drawing.Point(19, 103);
			this.button51.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button51.Name = "button51";
			this.button51.Size = new System.Drawing.Size(150, 32);
			this.button51.TabIndex = 2;
			this.button51.Text = "KUKA";
			this.button51.UseVisualStyleBackColor = true;
			this.button51.Click += new System.EventHandler(button51_Click);
			this.button44.Location = new System.Drawing.Point(19, 63);
			this.button44.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button44.Name = "button44";
			this.button44.Size = new System.Drawing.Size(150, 32);
			this.button44.TabIndex = 1;
			this.button44.Text = "EFORT-ER7BC10 (旧版)";
			this.button44.UseVisualStyleBackColor = true;
			this.button44.Click += new System.EventHandler(button44_Click);
			this.button23.Location = new System.Drawing.Point(19, 24);
			this.button23.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button23.Name = "button23";
			this.button23.Size = new System.Drawing.Size(150, 32);
			this.button23.TabIndex = 0;
			this.button23.Text = "EFORT-ER7BC10";
			this.button23.UseVisualStyleBackColor = true;
			this.button23.Click += new System.EventHandler(button23_Click);
			this.groupBox8.Controls.Add(this.button31);
			this.groupBox8.Controls.Add(this.button30);
			this.groupBox8.Controls.Add(this.button28);
			this.groupBox8.Controls.Add(this.button27);
			this.groupBox8.Controls.Add(this.button26);
			this.groupBox8.Controls.Add(this.button25);
			this.groupBox8.Controls.Add(this.button24);
			this.groupBox8.Location = new System.Drawing.Point(777, 372);
			this.groupBox8.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox8.Name = "groupBox8";
			this.groupBox8.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox8.Size = new System.Drawing.Size(185, 315);
			this.groupBox8.TabIndex = 15;
			this.groupBox8.TabStop = false;
			this.groupBox8.Text = "Others(其他)";
			this.button31.Location = new System.Drawing.Point(19, 262);
			this.button31.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button31.Name = "button31";
			this.button31.Size = new System.Drawing.Size(150, 32);
			this.button31.TabIndex = 6;
			this.button31.Text = "Bytes Transform";
			this.button31.UseVisualStyleBackColor = true;
			this.button31.Click += new System.EventHandler(button31_Click);
			this.button30.Location = new System.Drawing.Point(18, 63);
			this.button30.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button30.Name = "button30";
			this.button30.Size = new System.Drawing.Size(150, 32);
			this.button30.TabIndex = 5;
			this.button30.Text = "Tcp/Ip debug";
			this.button30.UseVisualStyleBackColor = true;
			this.button30.Click += new System.EventHandler(button30_Click);
			this.button28.Location = new System.Drawing.Point(19, 222);
			this.button28.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button28.Name = "button28";
			this.button28.Size = new System.Drawing.Size(150, 32);
			this.button28.TabIndex = 4;
			this.button28.Text = "Mail Send";
			this.button28.UseVisualStyleBackColor = true;
			this.button28.Click += new System.EventHandler(button28_Click);
			this.button27.Location = new System.Drawing.Point(19, 182);
			this.button27.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button27.Name = "button27";
			this.button27.Size = new System.Drawing.Size(150, 32);
			this.button27.TabIndex = 3;
			this.button27.Text = "Registration";
			this.button27.UseVisualStyleBackColor = true;
			this.button27.Click += new System.EventHandler(button27_Click);
			this.button26.Location = new System.Drawing.Point(19, 143);
			this.button26.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button26.Name = "button26";
			this.button26.Size = new System.Drawing.Size(150, 32);
			this.button26.TabIndex = 2;
			this.button26.Text = "OrderNumbe";
			this.button26.UseVisualStyleBackColor = true;
			this.button26.Click += new System.EventHandler(button26_Click);
			this.button25.Location = new System.Drawing.Point(19, 103);
			this.button25.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(150, 32);
			this.button25.TabIndex = 1;
			this.button25.Text = "Fourier transformation";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.button24.Location = new System.Drawing.Point(19, 24);
			this.button24.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button24.Name = "button24";
			this.button24.Size = new System.Drawing.Size(150, 32);
			this.button24.TabIndex = 0;
			this.button24.Text = "Serial debug";
			this.button24.UseVisualStyleBackColor = true;
			this.button24.Click += new System.EventHandler(button24_Click);
			this.groupBox9.Controls.Add(this.button33);
			this.groupBox9.Location = new System.Drawing.Point(968, 133);
			this.groupBox9.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox9.Name = "groupBox9";
			this.groupBox9.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox9.Size = new System.Drawing.Size(185, 121);
			this.groupBox9.TabIndex = 18;
			this.groupBox9.TabStop = false;
			this.groupBox9.Text = "AB PLC";
			this.button33.Location = new System.Drawing.Point(19, 24);
			this.button33.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button33.Name = "button33";
			this.button33.Size = new System.Drawing.Size(150, 32);
			this.button33.TabIndex = 0;
			this.button33.Text = "Logix Tcp";
			this.button33.UseVisualStyleBackColor = true;
			this.button33.Click += new System.EventHandler(button33_Click);
			this.groupBox10.Controls.Add(this.button58);
			this.groupBox10.Controls.Add(this.button35);
			this.groupBox10.Location = new System.Drawing.Point(777, 29);
			this.groupBox10.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox10.Name = "groupBox10";
			this.groupBox10.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox10.Size = new System.Drawing.Size(185, 187);
			this.groupBox10.TabIndex = 19;
			this.groupBox10.TabStop = false;
			this.groupBox10.Text = "Panasonic(松下 PLC)";
			this.button58.Location = new System.Drawing.Point(19, 64);
			this.button58.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button58.Name = "button58";
			this.button58.Size = new System.Drawing.Size(150, 32);
			this.button58.TabIndex = 3;
			this.button58.Text = "MC-3E (Binary)";
			this.button58.UseVisualStyleBackColor = true;
			this.button58.Click += new System.EventHandler(Button58_Click);
			this.button35.Location = new System.Drawing.Point(19, 24);
			this.button35.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button35.Name = "button35";
			this.button35.Size = new System.Drawing.Size(150, 32);
			this.button35.TabIndex = 0;
			this.button35.Text = "Mewtocol";
			this.button35.UseVisualStyleBackColor = true;
			this.button35.Click += new System.EventHandler(button35_Click);
			this.groupBox11.Controls.Add(this.button37);
			this.groupBox11.Location = new System.Drawing.Point(586, 372);
			this.groupBox11.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox11.Name = "groupBox11";
			this.groupBox11.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox11.Size = new System.Drawing.Size(185, 156);
			this.groupBox11.TabIndex = 22;
			this.groupBox11.TabStop = false;
			this.groupBox11.Text = "Instrument(仪器仪表)";
			this.button37.Location = new System.Drawing.Point(19, 24);
			this.button37.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button37.Name = "button37";
			this.button37.Size = new System.Drawing.Size(150, 32);
			this.button37.TabIndex = 0;
			this.button37.Text = "阿尔泰科技-DAM3601";
			this.button37.UseVisualStyleBackColor = true;
			this.button37.Click += new System.EventHandler(button37_Click);
			this.menuStrip1.BackColor = System.Drawing.Color.MediumPurple;
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[8] { this.aboutToolStripMenuItem, this.简体中文ToolStripMenuItem, this.englishToolStripMenuItem, this.论坛toolStripMenuItem, this.日志ToolStripMenuItem, this.verisonToolStripMenuItem, this.免责条款ToolStripMenuItem, this.授权ToolStripMenuItem });
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(1159, 25);
			this.menuStrip1.TabIndex = 26;
			this.menuStrip1.Text = "menuStrip1";
			this.aboutToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[5] { this.blogsToolStripMenuItem, this.webSideToolStripMenuItem, this.mesDemoToolStripMenuItem, this.rToolStripMenuItem, this.support赞助ToolStripMenuItem });
			this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
			this.aboutToolStripMenuItem.Size = new System.Drawing.Size(55, 21);
			this.aboutToolStripMenuItem.Text = "About";
			this.blogsToolStripMenuItem.Name = "blogsToolStripMenuItem";
			this.blogsToolStripMenuItem.Size = new System.Drawing.Size(250, 22);
			this.blogsToolStripMenuItem.Text = "Blogs [博客]";
			this.blogsToolStripMenuItem.Click += new System.EventHandler(blogsToolStripMenuItem_Click);
			this.webSideToolStripMenuItem.Name = "webSideToolStripMenuItem";
			this.webSideToolStripMenuItem.Size = new System.Drawing.Size(250, 22);
			this.webSideToolStripMenuItem.Text = "WebSide [官网]";
			this.webSideToolStripMenuItem.Click += new System.EventHandler(webSideToolStripMenuItem_Click);
			this.mesDemoToolStripMenuItem.Name = "mesDemoToolStripMenuItem";
			this.mesDemoToolStripMenuItem.Size = new System.Drawing.Size(250, 22);
			this.mesDemoToolStripMenuItem.Text = "Mes Demo [简易MES系统示例]";
			this.mesDemoToolStripMenuItem.Click += new System.EventHandler(mesDemoToolStripMenuItem_Click);
			this.rToolStripMenuItem.Name = "rToolStripMenuItem";
			this.rToolStripMenuItem.Size = new System.Drawing.Size(250, 22);
			this.rToolStripMenuItem.Text = "Richard Hu [作者]";
			this.rToolStripMenuItem.Click += new System.EventHandler(rToolStripMenuItem_Click);
			this.support赞助ToolStripMenuItem.Name = "support赞助ToolStripMenuItem";
			this.support赞助ToolStripMenuItem.Size = new System.Drawing.Size(250, 22);
			this.support赞助ToolStripMenuItem.Text = "Support [赞助]";
			this.简体中文ToolStripMenuItem.Name = "简体中文ToolStripMenuItem";
			this.简体中文ToolStripMenuItem.Size = new System.Drawing.Size(68, 21);
			this.简体中文ToolStripMenuItem.Text = "简体中文";
			this.简体中文ToolStripMenuItem.Click += new System.EventHandler(简体中文ToolStripMenuItem_Click);
			this.englishToolStripMenuItem.Name = "englishToolStripMenuItem";
			this.englishToolStripMenuItem.Size = new System.Drawing.Size(61, 21);
			this.englishToolStripMenuItem.Text = "English";
			this.englishToolStripMenuItem.Click += new System.EventHandler(englishToolStripMenuItem_Click);
			this.论坛toolStripMenuItem.Name = "论坛toolStripMenuItem";
			this.论坛toolStripMenuItem.Size = new System.Drawing.Size(43, 21);
			this.论坛toolStripMenuItem.Text = "BBS";
			this.论坛toolStripMenuItem.Click += new System.EventHandler(论坛toolStripMenuItem_Click);
			this.日志ToolStripMenuItem.Name = "日志ToolStripMenuItem";
			this.日志ToolStripMenuItem.Size = new System.Drawing.Size(83, 21);
			this.日志ToolStripMenuItem.Text = "Changelog";
			this.日志ToolStripMenuItem.Click += new System.EventHandler(日志ToolStripMenuItem_Click);
			this.verisonToolStripMenuItem.Name = "verisonToolStripMenuItem";
			this.verisonToolStripMenuItem.Size = new System.Drawing.Size(64, 21);
			this.verisonToolStripMenuItem.Text = "Verison";
			this.免责条款ToolStripMenuItem.Name = "免责条款ToolStripMenuItem";
			this.免责条款ToolStripMenuItem.Size = new System.Drawing.Size(68, 21);
			this.免责条款ToolStripMenuItem.Text = "免责条款";
			this.免责条款ToolStripMenuItem.Click += new System.EventHandler(免责条款ToolStripMenuItem_Click);
			this.授权ToolStripMenuItem.Name = "授权ToolStripMenuItem";
			this.授权ToolStripMenuItem.Size = new System.Drawing.Size(44, 21);
			this.授权ToolStripMenuItem.Text = "授权";
			this.groupBox12.Controls.Add(this.button43);
			this.groupBox12.Controls.Add(this.button42);
			this.groupBox12.Controls.Add(this.button40);
			this.groupBox12.Location = new System.Drawing.Point(586, 536);
			this.groupBox12.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox12.Name = "groupBox12";
			this.groupBox12.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox12.Size = new System.Drawing.Size(185, 151);
			this.groupBox12.TabIndex = 27;
			this.groupBox12.TabStop = false;
			this.groupBox12.Text = "Redis";
			this.button43.Location = new System.Drawing.Point(19, 102);
			this.button43.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button43.Name = "button43";
			this.button43.Size = new System.Drawing.Size(150, 32);
			this.button43.TabIndex = 2;
			this.button43.Text = "Redis Subscribe";
			this.button43.UseVisualStyleBackColor = true;
			this.button43.Click += new System.EventHandler(button43_Click);
			this.button42.Location = new System.Drawing.Point(19, 64);
			this.button42.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button42.Name = "button42";
			this.button42.Size = new System.Drawing.Size(150, 32);
			this.button42.TabIndex = 1;
			this.button42.Text = "Redis Browser";
			this.button42.UseVisualStyleBackColor = true;
			this.button42.Click += new System.EventHandler(button42_Click);
			this.button40.Location = new System.Drawing.Point(19, 24);
			this.button40.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button40.Name = "button40";
			this.button40.Size = new System.Drawing.Size(150, 32);
			this.button40.TabIndex = 0;
			this.button40.Text = "RedisClient";
			this.button40.UseVisualStyleBackColor = true;
			this.button40.Click += new System.EventHandler(button40_Click);
			this.groupBox13.Controls.Add(this.button54);
			this.groupBox13.Controls.Add(this.button50);
			this.groupBox13.Controls.Add(this.button49);
			this.groupBox13.Location = new System.Drawing.Point(777, 224);
			this.groupBox13.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox13.Name = "groupBox13";
			this.groupBox13.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox13.Size = new System.Drawing.Size(185, 140);
			this.groupBox13.TabIndex = 28;
			this.groupBox13.TabStop = false;
			this.groupBox13.Text = "Keyence(基恩士 PLC)";
			this.button54.Location = new System.Drawing.Point(18, 100);
			this.button54.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button54.Name = "button54";
			this.button54.Size = new System.Drawing.Size(150, 32);
			this.button54.TabIndex = 4;
			this.button54.Text = "Nano(ASCII)";
			this.button54.UseVisualStyleBackColor = true;
			this.button54.Click += new System.EventHandler(button54_Click);
			this.button50.Location = new System.Drawing.Point(18, 64);
			this.button50.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button50.Name = "button50";
			this.button50.Size = new System.Drawing.Size(150, 32);
			this.button50.TabIndex = 3;
			this.button50.Text = "MC-3E (ASCII)";
			this.button50.UseVisualStyleBackColor = true;
			this.button50.Click += new System.EventHandler(button50_Click);
			this.button49.Location = new System.Drawing.Point(18, 24);
			this.button49.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button49.Name = "button49";
			this.button49.Size = new System.Drawing.Size(150, 32);
			this.button49.TabIndex = 2;
			this.button49.Text = "MC-3E (Binary)";
			this.button49.UseVisualStyleBackColor = true;
			this.button49.Click += new System.EventHandler(button49_Click);
			this.label2.BackColor = System.Drawing.Color.MediumPurple;
			this.label2.ForeColor = System.Drawing.Color.White;
			this.label2.Location = new System.Drawing.Point(449, 0);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(704, 25);
			this.label2.TabIndex = 29;
			this.label2.Text = "如果你有什么问题，可以先去论坛看看";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.groupBox14.Controls.Add(this.button59);
			this.groupBox14.Controls.Add(this.button57);
			this.groupBox14.Controls.Add(this.button55);
			this.groupBox14.Location = new System.Drawing.Point(586, 224);
			this.groupBox14.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox14.Name = "groupBox14";
			this.groupBox14.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox14.Size = new System.Drawing.Size(185, 140);
			this.groupBox14.TabIndex = 30;
			this.groupBox14.TabStop = false;
			this.groupBox14.Text = "LSIS PLC";
			this.button59.Location = new System.Drawing.Point(19, 95);
			this.button59.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button59.Name = "button59";
			this.button59.Size = new System.Drawing.Size(150, 32);
			this.button59.TabIndex = 8;
			this.button59.Text = "LSis Virtual Server";
			this.button59.UseVisualStyleBackColor = true;
			this.button59.Click += new System.EventHandler(Button59_Click);
			this.button57.Location = new System.Drawing.Point(19, 55);
			this.button57.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button57.Name = "button57";
			this.button57.Size = new System.Drawing.Size(150, 32);
			this.button57.TabIndex = 1;
			this.button57.Text = "XGB Cnet";
			this.button57.UseVisualStyleBackColor = true;
			this.button57.Click += new System.EventHandler(Button57_Click);
			this.button55.Location = new System.Drawing.Point(19, 18);
			this.button55.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button55.Name = "button55";
			this.button55.Size = new System.Drawing.Size(150, 32);
			this.button55.TabIndex = 0;
			this.button55.Text = "XGB Fast Enet";
			this.button55.UseVisualStyleBackColor = true;
			this.button55.Click += new System.EventHandler(Button55_Click);
			this.groupBox15.Controls.Add(this.button63);
			this.groupBox15.Location = new System.Drawing.Point(968, 29);
			this.groupBox15.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox15.Name = "groupBox15";
			this.groupBox15.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox15.Size = new System.Drawing.Size(185, 96);
			this.groupBox15.TabIndex = 31;
			this.groupBox15.TabStop = false;
			this.groupBox15.Text = "FATEK(永宏 PLC)";
			this.button63.Location = new System.Drawing.Point(19, 24);
			this.button63.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button63.Name = "button63";
			this.button63.Size = new System.Drawing.Size(150, 32);
			this.button63.TabIndex = 0;
			this.button63.Text = "programe [编程口]";
			this.button63.UseVisualStyleBackColor = true;
			this.button63.Click += new System.EventHandler(Button63_Click);
			this.groupBox16.Controls.Add(this.button62);
			this.groupBox16.Location = new System.Drawing.Point(968, 262);
			this.groupBox16.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox16.Name = "groupBox16";
			this.groupBox16.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox16.Size = new System.Drawing.Size(185, 102);
			this.groupBox16.TabIndex = 32;
			this.groupBox16.TabStop = false;
			this.groupBox16.Text = "Fuji(富士 PLC)";
			this.button62.Location = new System.Drawing.Point(19, 24);
			this.button62.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button62.Name = "button62";
			this.button62.Size = new System.Drawing.Size(150, 32);
			this.button62.TabIndex = 0;
			this.button62.Text = "SPB [串口]";
			this.button62.UseVisualStyleBackColor = true;
			this.button62.Click += new System.EventHandler(Button62_Click);
			this.groupBox17.Controls.Add(this.button70);
			this.groupBox17.Controls.Add(this.button67);
			this.groupBox17.Controls.Add(this.button66);
			this.groupBox17.Controls.Add(this.button65);
			this.groupBox17.Controls.Add(this.button64);
			this.groupBox17.Location = new System.Drawing.Point(968, 372);
			this.groupBox17.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox17.Name = "groupBox17";
			this.groupBox17.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.groupBox17.Size = new System.Drawing.Size(185, 315);
			this.groupBox17.TabIndex = 33;
			this.groupBox17.TabStop = false;
			this.groupBox17.Text = "Special Protocols(特殊协议)";
			this.button70.Location = new System.Drawing.Point(19, 182);
			this.button70.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button70.Name = "button70";
			this.button70.Size = new System.Drawing.Size(150, 32);
			this.button70.TabIndex = 10;
			this.button70.Text = "Fourier Filter";
			this.button70.UseVisualStyleBackColor = true;
			this.button70.Click += new System.EventHandler(Button70_Click);
			this.button67.Location = new System.Drawing.Point(19, 64);
			this.button67.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button67.Name = "button67";
			this.button67.Size = new System.Drawing.Size(150, 32);
			this.button67.TabIndex = 9;
			this.button67.Text = "Tcp/Ip server";
			this.button67.UseVisualStyleBackColor = true;
			this.button67.Click += new System.EventHandler(Button67_Click);
			this.button66.Location = new System.Drawing.Point(19, 222);
			this.button66.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button66.Name = "button66";
			this.button66.Size = new System.Drawing.Size(150, 32);
			this.button66.TabIndex = 8;
			this.button66.Text = "Sick";
			this.button66.UseVisualStyleBackColor = true;
			this.button66.Click += new System.EventHandler(Button66_Click);
			this.button65.Location = new System.Drawing.Point(19, 262);
			this.button65.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button65.Name = "button65";
			this.button65.Size = new System.Drawing.Size(150, 32);
			this.button65.TabIndex = 7;
			this.button65.Text = "Pid";
			this.button65.UseVisualStyleBackColor = true;
			this.button65.Click += new System.EventHandler(Button65_Click);
			this.button64.Location = new System.Drawing.Point(19, 24);
			this.button64.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button64.Name = "button64";
			this.button64.Size = new System.Drawing.Size(150, 32);
			this.button64.TabIndex = 1;
			this.button64.Text = "Open Protocol";
			this.button64.UseVisualStyleBackColor = true;
			this.button64.Click += new System.EventHandler(Button64_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.AliceBlue;
			base.ClientSize = new System.Drawing.Size(1159, 700);
			base.Controls.Add(this.groupBox17);
			base.Controls.Add(this.groupBox16);
			base.Controls.Add(this.groupBox15);
			base.Controls.Add(this.groupBox14);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.groupBox13);
			base.Controls.Add(this.groupBox9);
			base.Controls.Add(this.groupBox12);
			base.Controls.Add(this.groupBox11);
			base.Controls.Add(this.groupBox10);
			base.Controls.Add(this.groupBox8);
			base.Controls.Add(this.groupBox7);
			base.Controls.Add(this.groupBox6);
			base.Controls.Add(this.groupBox5);
			base.Controls.Add(this.groupBox4);
			base.Controls.Add(this.groupBox3);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.menuStrip1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MainMenuStrip = this.menuStrip1;
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormLoad";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "HslCommunication Test Tools";
			base.Load += new System.EventHandler(FormLoad_Load);
			base.Shown += new System.EventHandler(FormLoad_Shown);
			this.groupBox1.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			this.groupBox4.ResumeLayout(false);
			this.groupBox5.ResumeLayout(false);
			this.groupBox6.ResumeLayout(false);
			this.groupBox6.PerformLayout();
			this.groupBox7.ResumeLayout(false);
			this.groupBox8.ResumeLayout(false);
			this.groupBox9.ResumeLayout(false);
			this.groupBox10.ResumeLayout(false);
			this.groupBox11.ResumeLayout(false);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.groupBox12.ResumeLayout(false);
			this.groupBox13.ResumeLayout(false);
			this.groupBox14.ResumeLayout(false);
			this.groupBox15.ResumeLayout(false);
			this.groupBox16.ResumeLayout(false);
			this.groupBox17.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
