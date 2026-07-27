using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.LogNet;
using HslCommunicationDemo.Algorithms;
using HslCommunicationDemo.BarCode;
using HslCommunicationDemo.HslDebug;
using HslCommunicationDemo.Instrument;
using HslCommunicationDemo.Light;
using HslCommunicationDemo.MQTT;
using HslCommunicationDemo.PLC;
using HslCommunicationDemo.PLC.Cimon;
using HslCommunicationDemo.PLC.Invt;
using HslCommunicationDemo.PLC.Omron;
using HslCommunicationDemo.PLC.Secs;
using HslCommunicationDemo.PLC.WeCon;
using HslCommunicationDemo.Properties;
using HslCommunicationDemo.Redis;
using HslCommunicationDemo.Robot;
using HslCommunicationDemo.Toledo;
using HslRedisDesktop;
using WeifenLuo.WinFormsUI.Docking;

namespace HslCommunicationDemo.DemoControl
{
	public class FormPanelLeft : DockContent
	{
		private ILogNet logNet = null;

		private ImageList imageList;

		private Dictionary<string, int> formIconImageIndex = new Dictionary<string, int>();

		private DockPanel dockPanel1;

		public static Type[] formTypes = Assembly.GetExecutingAssembly().GetTypes();

		private TreeNode treeNodeDebug;

		private IContainer components = null;

		private TreeView treeView1;

		public Dictionary<string, int> IconImageIndex => formIconImageIndex;

		public FormPanelLeft(DockPanel dockPanel, ImageList imageList, ILogNet logNet)
		{
			dockPanel1 = dockPanel;
			this.logNet = logNet;
			this.imageList = imageList;
			InitializeComponent();
		}

		private void FormPanelLeft_Load(object sender, EventArgs e)
		{
			base.CloseButtonVisible = false;
			treeView1.ImageList = imageList;
			TreeViewIni();
			treeView1.DoubleClick += TreeView1_DoubleClick;
			treeView1.MouseClick += TreeView1_MouseClick;
			SetLanguage();
		}

		public void SetLanguage()
		{
			if (Program.Language == 1)
			{
				Text = "设备列表";
			}
			else
			{
				Text = "Device List";
			}
		}

		private void TreeView1_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				TreeNode nodeAt = treeView1.GetNodeAt(e.Location);
				if (nodeAt != null)
				{
					treeView1.SelectedNode = nodeAt;
				}
			}
		}

		private void RenderTreeNode(TreeNode treeNode)
		{
			if (treeNode == null || treeNode.Tag == null || !(treeNode.Tag is Type type))
			{
				return;
			}
			HslFormContent hslFormContent = (HslFormContent)type.GetConstructors()[0].Invoke(null);
			if (hslFormContent != null)
			{
				hslFormContent.LogNet = logNet;
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
				hslFormContent?.Show(dockPanel1);
			}
		}

		private void TreeView1_DoubleClick(object sender, EventArgs e)
		{
			RenderTreeNode(treeView1.SelectedNode);
		}

		private void TreeViewIni()
		{
			TreeNode treeNode = new TreeNode("Melsec Plc [三菱]", 8, 8);
			treeNode.Nodes.Add(GetTreeNodeByIndex("EtherNet/IP(CIP)", 8, typeof(FormMelsecCipNet)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-1E (Binary)", 8, typeof(FormMelsec1EBinary)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-1E (ASCII)", 8, typeof(FormMelsec1EAscii)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-1E Server", 8, typeof(FormMcA1EServer)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("MC (Binary)", 8, typeof(FormMelsecBinary)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("MC Udp(Binary)", 8, typeof(FormMelsecUdp)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("MC (ASCII)", 8, typeof(FormMelsecAscii)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("MC Udp(ASCII)", 8, typeof(FormMelsecAsciiUdp)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("MC-R (Binary)", 8, typeof(FormMelsecRBinary)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Serial【编程口】", 8, typeof(FormMelsecSerial)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Serial OverTcp", 8, typeof(FormMelsecSerialOverTcp)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Serial Server", 8, typeof(FormFxSerialServer)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Links【485】", 8, typeof(FormMelsecLinks)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Links OverTcp", 8, typeof(FormMelsecLinksOverTcp)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Fx Links Server", 8, typeof(FormFxLinksServer)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-3C (串口)", 8, typeof(FormMelsec3C)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-3C OverTcp", 8, typeof(FormMelsec3COverTcp)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("A-3C Server", 8, typeof(FormMcA3CServer)));
			treeNode.Nodes.Add(GetTreeNodeByIndex("Mc Virtual Server", 8, typeof(FormMcServer)));
			treeView1.Nodes.Add(treeNode);
			TreeNode treeNode2 = new TreeNode("Siemens Plc [西门子]", 14, 14);
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S1200", 14, typeof(FormSiemensS1200)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S1500", 14, typeof(FormSiemensS1500)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S300", 14, typeof(FormSiemensS300)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S400", 14, typeof(FormSiemensS400)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S200", 14, typeof(FormSiemensS200)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-S200 smart", 14, typeof(FormSiemensS200Smart)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("Fetch/Write", 14, typeof(FormSiemensFW)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("WebApi", 14, typeof(FormSiemensWebApi)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("PPI", 14, typeof(FormSiemensPPI)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("PPI OverTcp", 14, typeof(FormSiemensPPIOverTcp)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("MPI", 14, typeof(FormSiemensMPI)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7 Virtual Server", 14, typeof(FormS7Server)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7 PPI Server", 14, typeof(FormSiemensPPIServer)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("Fetch Write Server", 14, typeof(FormFetchWriteServer)));
			treeNode2.Nodes.Add(GetTreeNodeByIndex("S7-PLUS", 14, typeof(FormSiemensS7Plus)));
			treeView1.Nodes.Add(treeNode2);
			TreeNode treeNode3 = new TreeNode("Modbus", 9, 9);
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Tcp", 9, typeof(FormModbus)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Udp", 9, typeof(FormModbusUdp)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Rtu", 9, typeof(FormModbusRtu)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Rtu OverTcp", 9, typeof(FormModbusRtuOverTcp)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Ascii", 9, typeof(FormModbusAscii)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("ModbusAscii OverTcp", 9, typeof(FormModbusAsciiOverTcp)));
			treeNode3.Nodes.Add(GetTreeNodeByIndex("Modbus Server", 9, typeof(FormModbusServer)));
			treeView1.Nodes.Add(treeNode3);
			TreeNode treeNode4 = new TreeNode("Inovance Plc[汇川]", 5, 5);
			treeNode4.Nodes.Add(GetTreeNodeByIndex("InovanceSerial", 5, typeof(FormInovanceSerial)));
			treeNode4.Nodes.Add(GetTreeNodeByIndex("InovanceSerialOverTcp", 5, typeof(FormInovanceSerialOverTcp)));
			treeNode4.Nodes.Add(GetTreeNodeByIndex("InovanceTcpNet", 5, typeof(FormInovanceTcpNet)));
			treeView1.Nodes.Add(treeNode4);
			TreeNode treeNode5 = new TreeNode("Omron Plc[欧姆龙]", 10, 10);
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Fins Tcp", 10, typeof(FormOmron)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Fins Udp", 10, typeof(FormOmronUdp)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("EtherNet/IP(CIP)", 10, typeof(FormOmronCip)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Connected CIP", 10, typeof(FormOmronConnectedCip)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Omron CIP Server", 10, typeof(FormOmronCipServer)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("HostLink 【串口】", 10, typeof(FormOmronHostLink)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("HostLink OverTcp", 10, typeof(FormOmronHostLinkOverTcp)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("HostLink C-Mode", 10, typeof(FormOmronHostLinkCMode)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("C-Mode OverTcp", 10, typeof(FormOmronHostLinkCModeOverTcp)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Fins Virtual Server", 10, typeof(FormOmronServer)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Fins Udp Server", 10, typeof(FormOmronUdpServer)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("HostLink Server", 10, typeof(FormOmronHostLinkServer)));
			treeNode5.Nodes.Add(GetTreeNodeByIndex("Cmode Server", 10, typeof(FormOmronHostLinkCModeServer)));
			treeView1.Nodes.Add(treeNode5);
			TreeNode treeNode6 = new TreeNode("LSis Plc", 7, 7);
			treeNode6.Nodes.Add(GetTreeNodeByIndex("LSis Fast Enet", 7, typeof(FormLsisFEnet)));
			treeNode6.Nodes.Add(GetTreeNodeByIndex("LSis Cnet", 7, typeof(FormLsisCnet)));
			treeNode6.Nodes.Add(GetTreeNodeByIndex("LSis Cpu", 7, typeof(FormLsisCpu)));
			treeNode6.Nodes.Add(GetTreeNodeByIndex("LSis Cnet OverTcp", 7, typeof(FormLsisCnetOverTcp)));
			treeNode6.Nodes.Add(GetTreeNodeByIndex("LSis Virtual Server", 7, typeof(FormLSisServer)));
			treeView1.Nodes.Add(treeNode6);
			TreeNode treeNode7 = new TreeNode("Cimon[西蒙]", 47, 47);
			treeNode7.Nodes.Add(GetTreeNodeByIndex("HmiProtocol", 47, typeof(FormCimonHmiProtocol)));
			treeNode7.Nodes.Add(GetTreeNodeByIndex("CimonServer", 47, typeof(FormCimonServer)));
			treeView1.Nodes.Add(treeNode7);
			TreeNode treeNode8 = new TreeNode("Keyence Plc[基恩士]", 6, 6);
			treeNode8.Nodes.Add(GetTreeNodeByIndex("MC-3E (Binary)", 6, typeof(FormKeyenceBinary)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("MC-3E (ASCII)", 6, typeof(FormKeyenceAscii)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("Nano (ASCII)", 6, typeof(FormKeyenceNanoSerial)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("Nano OverTcp", 6, typeof(FormKeyenceNanoSerialOverTcp)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("Nano Server", 6, typeof(FormKeyenceNanoServer)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("SR2000 [读码]", 6, typeof(FormKeyenceSR2000)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("DL-EN1 [传感器]", 6, typeof(FormKeyenceDLEN1)));
			treeNode8.Nodes.Add(GetTreeNodeByIndex("Kv300/Older", 6, typeof(FormKeyenceKvOld)));
			treeView1.Nodes.Add(treeNode8);
			TreeNode treeNode9 = new TreeNode("Panasonic Plc[松下]", 11, 11);
			treeNode9.Nodes.Add(GetTreeNodeByIndex("MC-3E (Binary)", 11, typeof(FormPanasonicBinary)));
			treeNode9.Nodes.Add(GetTreeNodeByIndex("Mewtocol", 11, typeof(FormPanasonicMew)));
			treeNode9.Nodes.Add(GetTreeNodeByIndex("Mewtocol OverTcp", 11, typeof(FormPanasonicMewOverTcp)));
			treeNode9.Nodes.Add(GetTreeNodeByIndex("Mewtocol Server", 11, typeof(FormPanasonicMewtocolServer)));
			treeView1.Nodes.Add(treeNode9);
			TreeNode treeNode10 = new TreeNode("AllenBrandly[罗克韦尔]", 1, 1);
			treeNode10.Nodes.Add(GetTreeNodeByIndex("EtherNet/IP(CIP)", 1, typeof(FormAllenBrandly)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("Connected CIP", 1, typeof(FormAllenBrandlyConnectedCip)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("MicroCip(Micro800)", 1, typeof(FormAllenBrandlyMicroCip)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("CIP Browser", 1, typeof(FormAllenBrandlyBrowser)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("CIP Virtual Server", 1, typeof(FormCipServer)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("CIP PCCC", 1, typeof(FormAllenBrandlyPCCC)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("CIP PCCC Server", 1, typeof(FormPcccServer)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("SLC Net", 1, typeof(FormAllenBrandlySLC)));
			treeNode10.Nodes.Add(GetTreeNodeByIndex("DF1", 1, typeof(FormAllenBradleyDF1Serial)));
			treeView1.Nodes.Add(treeNode10);
			TreeNode treeNode11 = new TreeNode("Beckhoff Plc[倍福]", 20, 20);
			treeNode11.Nodes.Add(GetTreeNodeByIndex("Ads Net", 20, typeof(FormBeckhoffAdsNet)));
			treeNode11.Nodes.Add(GetTreeNodeByIndex("Ads Server", 20, typeof(FormBeckhoffAdsServer)));
			treeView1.Nodes.Add(treeNode11);
			TreeNode treeNode12 = new TreeNode("GE Plc[通用电气]", 33, 33);
			treeNode12.Nodes.Add(GetTreeNodeByIndex("SRTP", 33, typeof(FormGeSRTP)));
			treeNode12.Nodes.Add(GetTreeNodeByIndex("SRTP Server", 33, typeof(FormGeSRTPServer)));
			treeView1.Nodes.Add(treeNode12);
			TreeNode treeNode13 = new TreeNode("Yaskawa Plc[安川]", 29, 29);
			treeNode13.Nodes.Add(GetTreeNodeByIndex("MemobusTcp", 29, typeof(FormYASKAWAMemobusTcpNet)));
			treeNode13.Nodes.Add(GetTreeNodeByIndex("MemobusUdp", 29, typeof(FormYASKAWAMemobusUdpNet)));
			treeNode13.Nodes.Add(GetTreeNodeByIndex("MemobusTcpServer", 29, typeof(FormMemobusTcpServer)));
			treeView1.Nodes.Add(treeNode13);
			TreeNode treeNode14 = new TreeNode("yamatake[山武]", 34, 34);
			treeNode14.Nodes.Add(GetTreeNodeByIndex("DigitronCPL", 34, typeof(FormDigitronCPL)));
			treeNode14.Nodes.Add(GetTreeNodeByIndex("DigitronCPL OverTcp", 34, typeof(FormDigitronCPLOverTcp)));
			treeNode14.Nodes.Add(GetTreeNodeByIndex("DigitronCPL Server", 34, typeof(FormDigitronCPLServer)));
			treeView1.Nodes.Add(treeNode14);
			TreeNode treeNode15 = new TreeNode("RKC[理化]", 35, 35);
			treeNode15.Nodes.Add(GetTreeNodeByIndex("温度控制器", 35, typeof(FormRkcTemperatureController)));
			treeNode15.Nodes.Add(GetTreeNodeByIndex("温度控制器TCP", 35, typeof(FormRkcTemperatureControllerOverTcp)));
			treeNode15.Nodes.Add(GetTreeNodeByIndex("温度控制器Server", 35, typeof(FormRkcTemperatureControllerServer)));
			treeView1.Nodes.Add(treeNode15);
			TreeNode treeNode16 = new TreeNode("Fatek Plc[永宏]", 22, 22);
			treeNode16.Nodes.Add(GetTreeNodeByIndex("programe [编程口]", 22, typeof(FormFatekPrograme)));
			treeNode16.Nodes.Add(GetTreeNodeByIndex("programe OverTcp", 22, typeof(FormFatekProgrameOverTcp)));
			treeNode16.Nodes.Add(GetTreeNodeByIndex("programe Server", 22, typeof(FormFatekProgrameServer)));
			treeView1.Nodes.Add(treeNode16);
			TreeNode treeNode17 = new TreeNode("Vigor Plc[丰炜]", 36, 36);
			treeNode17.Nodes.Add(GetTreeNodeByIndex("Serial [编程口]", 36, typeof(FormVigorSerial)));
			treeNode17.Nodes.Add(GetTreeNodeByIndex("Serial OverTcp", 36, typeof(FormVigorSerialOverTcp)));
			treeNode17.Nodes.Add(GetTreeNodeByIndex("Virtual Server", 36, typeof(FormVigorServer)));
			treeView1.Nodes.Add(treeNode17);
			TreeNode treeNode18 = new TreeNode("Fuji Plc[富士]", 2, 2);
			treeNode18.Nodes.Add(GetTreeNodeByIndex("SPB [编程口]", 2, typeof(FormFujiSPB)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("SPB OverTcp", 2, typeof(FormFujiSPBOverTcp)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("SPB Server", 2, typeof(FormFujiSPBServer)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("SPH Net", 2, typeof(FormFujiSPHNet)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("SPH Server", 2, typeof(FormFujiSPHServer)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("CommandST", 2, typeof(FormFujiCSTNet)));
			treeNode18.Nodes.Add(GetTreeNodeByIndex("CommandST Server", 2, typeof(FormFujiCSTServer)));
			treeView1.Nodes.Add(treeNode18);
			TreeNode treeNode19 = new TreeNode("XinJE Plc[信捷]", 30, 30);
			treeNode19.Nodes.Add(GetTreeNodeByIndex("XinJE Serial", 30, typeof(FormXinJEXCSerial)));
			treeNode19.Nodes.Add(GetTreeNodeByIndex("XinJE Serial OverTcp", 30, typeof(FormXinJESerialOverTcp)));
			treeNode19.Nodes.Add(GetTreeNodeByIndex("XinJE TCP [Modbus]", 30, typeof(FormXinJETcpNet)));
			treeNode19.Nodes.Add(GetTreeNodeByIndex("XinJE Server", 30, typeof(FormXinJEInternalServer)));
			treeNode19.Nodes.Add(GetTreeNodeByIndex("XinJE TCP[专用]", 30, typeof(FormXinJEInternalTcp)));
			treeView1.Nodes.Add(treeNode19);
			TreeNode treeNode20 = new TreeNode("WeCon Plc[维控]", 43, 43);
			treeNode20.Nodes.Add(GetTreeNodeByIndex("WeCon ModbusTcp", 43, typeof(FormWeConModbus)));
			treeNode20.Nodes.Add(GetTreeNodeByIndex("WeCon ModbusRtu", 43, typeof(FormWeConModbusRtu)));
			treeView1.Nodes.Add(treeNode20);
			TreeNode treeNode21 = new TreeNode("MegMeet Plc[麦格米特]", 44, 44);
			treeNode21.Nodes.Add(GetTreeNodeByIndex("MegMeet Serial", 44, typeof(FormMegMeetSerial)));
			treeNode21.Nodes.Add(GetTreeNodeByIndex("MegMeet Serial OverTcp", 44, typeof(FormMegMeetSerialOverTcp)));
			treeNode21.Nodes.Add(GetTreeNodeByIndex("MegMeet TCP [Modbus]", 44, typeof(FormMegMeetTcpNet)));
			treeView1.Nodes.Add(treeNode21);
			TreeNode treeNode22 = new TreeNode("Invt Plc[英威腾]", 45, 45);
			treeNode22.Nodes.Add(GetTreeNodeByIndex("Invt ModbusTcp", 45, typeof(FormInvtModbus)));
			treeNode22.Nodes.Add(GetTreeNodeByIndex("Invt ModbusRtu", 45, typeof(FormInvtModbusRtu)));
			treeView1.Nodes.Add(treeNode22);
			TreeNode treeNode23 = new TreeNode("Yokogawa Plc[横河]", 31, 31);
			treeNode23.Nodes.Add(GetTreeNodeByIndex("Yokogawa Link Tcp", 31, typeof(FormYokogawaLinkTcp)));
			treeNode23.Nodes.Add(GetTreeNodeByIndex("Yokogawa Link Server", 31, typeof(FormYokogawaLinkServer)));
			treeView1.Nodes.Add(treeNode23);
			TreeNode treeNode24 = new TreeNode("Toyota Plc[丰田]", 39, 39);
			treeNode24.Nodes.Add(GetTreeNodeByIndex("ToyoPuc Tcp", 39, typeof(FormToyoPuc)));
			treeNode24.Nodes.Add(GetTreeNodeByIndex("ToyoPuc Server", 39, typeof(FormToyoPucServer)));
			treeView1.Nodes.Add(treeNode24);
			TreeNode treeNode25 = new TreeNode("Delta Plc[台达]", 32, 32);
			treeNode25.Nodes.Add(GetTreeNodeByIndex("Serial", 32, typeof(FormDeltaDvpSerial)));
			treeNode25.Nodes.Add(GetTreeNodeByIndex("Serial Over Tcp", 32, typeof(FormDeltaSerialOverTcp)));
			treeNode25.Nodes.Add(GetTreeNodeByIndex("Serial Ascii", 32, typeof(FormDeltaDvpSerialAscii)));
			treeNode25.Nodes.Add(GetTreeNodeByIndex("Ascii Over Tcp", 32, typeof(FormDeltaSerialAsciiOverTcp)));
			treeNode25.Nodes.Add(GetTreeNodeByIndex("Tcp Net", 32, typeof(FormDeltaDvpTcpNet)));
			treeView1.Nodes.Add(treeNode25);
			TreeNode treeNode26 = new TreeNode("ID Card[身份证]", 4, 4);
			treeNode26.Nodes.Add(GetTreeNodeByIndex("SAM Serial", 27, typeof(FormSAMSerial)));
			treeNode26.Nodes.Add(GetTreeNodeByIndex("SAM Tcp", 27, typeof(FormSAMTcpNet)));
			treeView1.Nodes.Add(treeNode26);
			TreeNode treeNode27 = new TreeNode("Redis", 12, 12);
			treeNode27.Nodes.Add(GetTreeNodeByIndex("Redis Client", 12, typeof(FormRedisClient)));
			treeNode27.Nodes.Add(GetTreeNodeByIndex("Redis Browser", 12, typeof(FormRedisMain)));
			treeNode27.Nodes.Add(GetTreeNodeByIndex("Redis Subscribe", 12, typeof(FormRedisSubscribe)));
			treeNode27.Nodes.Add(GetTreeNodeByIndex("Redis Copy", 12, typeof(FormRedisCopy)));
			treeView1.Nodes.Add(treeNode27);
			TreeNode treeNode28 = new TreeNode("MQTT", 17, 17);
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt Server", 17, typeof(FormMqttServer)));
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt Client", 17, typeof(FormMqttClient)));
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt RPC Client", 17, typeof(FormMqttSyncClient)));
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt File Server", 17, typeof(FormMqttFileServer)));
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt File Client", 17, typeof(FormMqttFileClient)));
			treeNode28.Nodes.Add(GetTreeNodeByIndex("Mqtt Rpc Device", 17, typeof(FormMqttRpcDevice)));
			treeView1.Nodes.Add(treeNode28);
			TreeNode treeNode29 = new TreeNode("WebSocket", 28, 28);
			treeNode29.Nodes.Add(GetTreeNodeByIndex("WebSocket Client", 28, typeof(FormWebsocketClient)));
			treeNode29.Nodes.Add(GetTreeNodeByIndex("WebSocket Server", 28, typeof(FormWebsocketServer)));
			treeNode29.Nodes.Add(GetTreeNodeByIndex("WebSocket QANet", 28, typeof(FormWebsocketQANet)));
			treeView1.Nodes.Add(treeNode29);
			TreeNode treeNode30 = new TreeNode("Http", 46, 46);
			treeNode30.Nodes.Add(GetTreeNodeByIndex("Http Web Server", 46, typeof(FormHttpServer)));
			treeNode30.Nodes.Add(GetTreeNodeByIndex("Http Web Client", 46, typeof(FormHttpClient)));
			treeView1.Nodes.Add(treeNode30);
			TreeNode treeNode31 = new TreeNode("Robot[机器人]", 19, 19);
			treeNode31.Nodes.Add(GetTreeNodeByIndex("EFORT New [新版]", 24, typeof(FormEfort)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("EFORT Pre [旧版]", 24, typeof(FormEfortPrevious)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("Kuka [库卡]", 23, typeof(FormKuka)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("Kuka Tcp [库卡]", 23, typeof(FormKukaTcp)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("YRC1000 [安川]", 29, typeof(FormYRC1000)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("HighEthernet [安川]", 29, typeof(FormYRCHighEthernet)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("ABB Web", 21, typeof(FormABBWebApi)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("ABB Web Server", 21, typeof(FormAbbServer)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("Fanuc [发那科]", 25, typeof(FormFanucRobot)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("Fanuc Server [发那科服务器]", 25, typeof(FormFanucRobotServer)));
			treeNode31.Nodes.Add(GetTreeNodeByIndex("Estun [埃斯顿]", 19, typeof(FormEstunTcp)));
			treeNode31.Nodes.Add(new TreeNode("Hyundai [现代]")
			{
				Tag = typeof(FormHyundaiUdp)
			});
			treeNode31.Nodes.Add(GetTreeNodeByIndex("YamahaRCX [雅马哈]", 48, typeof(FormYamahaRCX)));
			treeView1.Nodes.Add(treeNode31);
			TreeNode treeNode32 = new TreeNode("CNC[数控机床]");
			treeNode32.Nodes.Add(GetTreeNodeByIndex("Fanuc 0i [Test]", 25, typeof(FormCncFanuc)));
			treeView1.Nodes.Add(treeNode32);
			TreeNode treeNode33 = new TreeNode("Secs [半导体]");
			treeNode33.Nodes.Add(new TreeNode("Secs Gem")
			{
				Tag = typeof(FormSecsGem)
			});
			treeNode33.Nodes.Add(new TreeNode("Secs Server")
			{
				Tag = typeof(FormSecsHsmsServer)
			});
			treeView1.Nodes.Add(treeNode33);
			TreeNode treeNode34 = new TreeNode("Sensor[传感器]");
			treeNode34.Nodes.Add(new TreeNode("Vibration[捷杰振动]")
			{
				Tag = typeof(FormVibrationSensorClient)
			});
			treeView1.Nodes.Add(treeNode34);
			TreeNode treeNode35 = new TreeNode("Freedom[自由协议]");
			treeNode35.Nodes.Add(new TreeNode("TCP Net")
			{
				Tag = typeof(FormFreedomTcpNet)
			});
			treeNode35.Nodes.Add(new TreeNode("UDP Net")
			{
				Tag = typeof(FormFreedomUdpNet)
			});
			treeNode35.Nodes.Add(new TreeNode("Serial [串口]")
			{
				Tag = typeof(FormFreedomSerial)
			});
			treeView1.Nodes.Add(treeNode35);
			TreeNode treeNode36 = new TreeNode("Debug About[调试技术]", 15, 15);
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Regex [正则表达式]", 15, typeof(FormRegexTest)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Check [校验码调试]", 15, typeof(FormCheck)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Serial [串口调试]", 40, typeof(FormSerialDebug)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Tcp/Ip Client [网口调试]", 41, typeof(FormTcpDebug)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Tcp/Ip Server [网口调试]", 41, typeof(FormTcpServer)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Serial2Tcp [串口转网口]", 40, typeof(FormSerialToTcp)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Tcp2Tcp [网口转网口]", 41, typeof(FormTcpToTcp)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("PingTest [网络测试]", 41, typeof(FormPingIpAddress)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Bytes Data [数据调试]", 42, typeof(FormByteTransfer)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Mail [邮件调试]", 15, typeof(FormMail)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Order Number [订单号调试]", 15, typeof(FormSeqCreate)));
			treeNode36.Nodes.Add(GetTreeNodeByIndex("Regist [注册码调试]", 15, typeof(FormRegister)));
			treeView1.Nodes.Add(treeNode36);
			treeNodeDebug = treeNode36;
			TreeNode treeNode37 = new TreeNode("Hsl Protocal[HSL 协议]", 3, 3);
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Simplify Net [同步交互]", 3, typeof(FormSimplifyNet)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Simplify Net Alien", 3, typeof(FormSimplifyNetAlien)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Complex Net [异步交互]", 3, typeof(FormComplexNet)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Udp Net [同步交互]", 3, typeof(FormUdpNet)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("File Net [文件收发]", 3, typeof(FormFileClient)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Log Net [日志记录]", 3, typeof(FormLogNet)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Push Net [消息推送]", 3, typeof(FormPushNet)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("SoftUpdate [软件更新]", 3, typeof(FormUpdateServer)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Plain Net [明文交互]", 3, typeof(FormPlainSocket)));
			treeNode37.Nodes.Add(GetTreeNodeByIndex("Dtu Server[DTU服务器]", 3, typeof(FormDtuServer)));
			treeView1.Nodes.Add(treeNode37);
			TreeNode treeNode38 = new TreeNode("Bar/RFID[扫码及RFID]", 16, 16);
			treeNode38.Nodes.Add(GetTreeNodeByIndex("Sick", 16, typeof(FormSickBarCode)));
			treeNode38.Nodes.Add(GetTreeNodeByIndex("Turck Reader", 38, typeof(FormTurckReaderNet)));
			treeNode38.Nodes.Add(GetTreeNodeByIndex("Turck Reader Server", 38, typeof(FormTurckReaderServer)));
			treeView1.Nodes.Add(treeNode38);
			TreeNode treeNode39 = new TreeNode("Instrument [仪器仪表]");
			treeNode39.Nodes.Add(new TreeNode("DAM3601 [阿尔泰科技]")
			{
				Tag = typeof(FormDAM3601)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645 [电力规约]")
			{
				Tag = typeof(FormDLT645)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645 OverTcp")
			{
				Tag = typeof(FormDLT645OverTcp)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645 Server")
			{
				Tag = typeof(FormDLT645Server)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645-1997")
			{
				Tag = typeof(FormDLT645With1997)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645-1997 OverTcp")
			{
				Tag = typeof(FormDLT645With1997OverTcp)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT645-1997 Server")
			{
				Tag = typeof(FormDLT645With1997Server)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT698 [电力规约]")
			{
				Tag = typeof(FormDLT698)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT698 OverTcp")
			{
				Tag = typeof(FormDLT698OverTcp)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT698 TcpNet")
			{
				Tag = typeof(FormDLT698TcpNet)
			});
			treeNode39.Nodes.Add(new TreeNode("DLT698 Server")
			{
				Tag = typeof(FormDLT698Server)
			});
			treeNode39.Nodes.Add(new TreeNode("光源控制器")
			{
				Tag = typeof(FormShineInLight)
			});
			treeNode39.Nodes.Add(new TreeNode("DTSU6606 [德力西电表]")
			{
				Tag = typeof(FormDTSU6606)
			});
			treeNode39.Nodes.Add(new TreeNode("IEC104 [电力规约]", 37, 37)
			{
				Tag = typeof(FormIEC104)
			});
			treeNode39.Nodes.Add(new TreeNode("IEC104 Server", 37, 37)
			{
				Tag = typeof(FormIEC104Server)
			});
			treeNode39.Nodes.Add(new TreeNode("CJT188 [水表，燃气]")
			{
				Tag = typeof(FormCJT188)
			});
			treeNode39.Nodes.Add(new TreeNode("CJT188 OverTcp")
			{
				Tag = typeof(FormCJT188OverTcp)
			});
			treeView1.Nodes.Add(treeNode39);
			TreeNode treeNode40 = new TreeNode("Toledo [托利多]", 18, 18);
			treeNode40.Nodes.Add(GetTreeNodeByIndex("Toledo Server [网口串口]", 18, typeof(FormToledoTcpServer)));
			treeView1.Nodes.Add(treeNode40);
			TreeNode treeNode41 = new TreeNode("Algorithms [算法]");
			treeNode41.Nodes.Add(new TreeNode("Fourier [傅立叶算法]")
			{
				Tag = typeof(FourierTransform)
			});
			treeNode41.Nodes.Add(new TreeNode("Fourier [傅立叶滤波]")
			{
				Tag = typeof(FourierFilter)
			});
			treeNode41.Nodes.Add(new TreeNode("PID [Pid模拟]")
			{
				Tag = typeof(FormPid)
			});
			treeNode41.Nodes.Add(new TreeNode("RSA [加密解密]")
			{
				Tag = typeof(FormRSADebug)
			});
			treeNode41.Nodes.Add(new TreeNode("Cert [Hsl证书]")
			{
				Tag = typeof(FormHslCertificate)
			});
			treeView1.Nodes.Add(treeNode41);
			TreeNode treeNode42 = new TreeNode("Special [特殊协议]");
			treeNode42.Nodes.Add(new TreeNode("Open Protocol")
			{
				Tag = typeof(FormOpenProtocol)
			});
			treeNode42.Nodes.Add(new TreeNode("Open ProtocolServer")
			{
				Tag = typeof(FormOpenProtocolServer)
			});
			treeNode42.Nodes.Add(new TreeNode("南京自动化 DCS")
			{
				Tag = typeof(FormDcsNanJingAuto)
			});
			treeNode42.Nodes.Add(new TreeNode("Knx")
			{
				Tag = typeof(FormKnx)
			});
			treeNode42.Nodes.Add(new TreeNode("FFU用EC风机")
			{
				Tag = typeof(FormEcFanMachine)
			});
			treeView1.Nodes.Add(treeNode42);
		}

		private TreeNode GetTreeNodeByIndex(string name, int index, Type form)
		{
			formIconImageIndex.Add(form.Name, index);
			return new TreeNode(name, index, index)
			{
				Tag = form
			};
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

		public void RenderSerialDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[2]);
		}

		public void RenderTCPDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[3]);
		}

		public void RenderTCPServerDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[4]);
		}

		public void RenderSerial2TcpDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[5]);
		}

		public void RenderTCP2TcpDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[6]);
		}

		public void RenderPingTestDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[7]);
		}

		public void RenderByteTransformDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[8]);
		}

		public void RenderRegexDebug()
		{
			RenderTreeNode(treeNodeDebug.Nodes[0]);
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
			this.treeView1 = new System.Windows.Forms.TreeView();
			base.SuspendLayout();
			this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.treeView1.Indent = 19;
			this.treeView1.ItemHeight = 19;
			this.treeView1.Location = new System.Drawing.Point(0, 0);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(234, 640);
			this.treeView1.TabIndex = 1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(234, 640);
			base.Controls.Add(this.treeView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormPanelLeft";
			this.Text = "Device List";
			base.Load += new System.EventHandler(FormPanelLeft_Load);
			base.ResumeLayout(false);
		}
	}
}
