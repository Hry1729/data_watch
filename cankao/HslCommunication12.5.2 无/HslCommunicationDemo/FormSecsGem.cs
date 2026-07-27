using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Secs;
using HslCommunication.Secs.Types;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.Secs;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo
{
	public class FormSecsGem : HslFormContent
	{
		public class SecsTreeItem
		{
			public byte S { get; set; }

			public byte F { get; set; }

			public bool W { get; set; }

			public SecsValue Value { get; set; }

			public string Description { get; set; }

			public SecsValue ValueSingular { get; set; }

			public SecsTreeItem(byte s, byte f, bool w, SecsValue value, string decs, SecsValue singular = null)
			{
				S = s;
				F = f;
				W = w;
				Value = value;
				Description = decs;
				ValueSingular = singular;
			}

			public SecsTreeItem(XElement element)
			{
				LoadByXElement(element);
			}

			public string GetTreeNodeText()
			{
				return string.Format("S{0}F{1}{2} {3}", S, F, W ? "W" : "", Description);
			}

			public XElement ToXElement()
			{
				XElement xElement = new XElement("SecsTreeItem");
				xElement.SetAttributeValue("S", S);
				xElement.SetAttributeValue("F", F);
				xElement.SetAttributeValue("W", W);
				xElement.SetAttributeValue("Description", Description);
				if (Value != null)
				{
					XElement xElement2 = new XElement("Value");
					xElement2.Add(Value.ToXElement());
					xElement.Add(xElement2);
				}
				if (ValueSingular != null)
				{
					XElement xElement3 = new XElement("ValueSingular");
					xElement3.Add(ValueSingular.ToXElement());
					xElement.Add(xElement3);
				}
				return xElement;
			}

			public void LoadByXElement(XElement element)
			{
				S = HslFormContent.GetXmlValue(element, "S", S, byte.Parse);
				F = HslFormContent.GetXmlValue(element, "F", F, byte.Parse);
				W = HslFormContent.GetXmlValue(element, "W", W, bool.Parse);
				Description = HslFormContent.GetXmlValue(element, "Description", Description, (string m) => m);
				foreach (XElement item in element.Elements())
				{
					if (item.Name == "Value")
					{
						Value = new SecsValue(item.Elements().First());
					}
					else if (item.Name == "ValueSingular")
					{
						ValueSingular = new SecsValue(item.Elements().First());
					}
				}
			}
		}

		private SecsHsms secs = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private SecsValue renderSecsValueLeft = SecsValue.EmptySecsValue();

		private SecsValue renderSecsValueRight = SecsValue.EmptySecsValue();

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private Label label13;

		private TextBox textBox_function;

		private Label label12;

		private TextBox textBox_stream;

		private Label label11;

		private TextBox textBox_deviceID;

		private Label label21;

		private UserControlHead userControlHead1;

		private TextBox textBox_port;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private TextBox textBox_receive;

		private CheckBox checkBox1;

		private TextBox textBox_data;

		private CheckBox checkBox_back;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private Button button_S1F1;

		private TextBox textBox_s1;

		private Button button_s1f11;

		private TextBox textBox4;

		private Label label4;

		private Button button_s1f13;

		private Button button_s1f15;

		private Button button_s1f17;

		private TabPage tabPage_s2;

		private Panel panel3;

		private TextBox textBox_s2;

		private Button button_s2f13;

		private TreeView treeView1;

		private SplitContainer splitContainer1;

		private SplitContainer splitContainer2;

		private CheckBox checkBox2;

		private ComboBox comboBox1;

		private Label label5;

		private Button button4;

		private TabControl tabControl_buttom;

		private TabPage tabPage_log;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem addNewSecsItemToolStripMenuItem;

		private ToolStripMenuItem editSecsItemToolStripMenuItem;

		private ToolStripMenuItem deleteSecsItemToolStripMenuItem;

		private ToolStripMenuItem sendMessageToolStripMenuItem;

		private CheckBox checkBox_show_send_message;

		private RichTextBox richTextBox_main;

		private CheckBox checkBox_code_style;

		private ComboBox comboBox_log_style;

		private TabPage tabPage3;

		private TextBox textBox_code;

		private Button button_control_linktest;

		private Button button_control_select;

		private Button button_control_separate;

		private Button button_control_send;

		private TextBox textBox_stype;

		private Label label6;

		private TextBox textBox_ptype;

		private Label label2;

		private CheckBox checkBox_autoBackS1F1;

		public FormSecsGem()
		{
			InitializeComponent();
		}

		public static DeviceAddressExample[] GetSecsAddress()
		{
			return new DeviceAddressExample[14]
			{
				new DeviceAddressExample("sbyte", "", true, true, new SecsValue((sbyte)1).ToString()),
				new DeviceAddressExample("byte", "", true, true, new SecsValue((byte)2).ToString()),
				new DeviceAddressExample("short", "", true, true, new SecsValue((short)3).ToString()),
				new DeviceAddressExample("ushort", "", true, true, new SecsValue((ushort)4).ToString()),
				new DeviceAddressExample("int", "", true, true, new SecsValue(5).ToString()),
				new DeviceAddressExample("uint", "", true, true, new SecsValue(6u).ToString()),
				new DeviceAddressExample("long", "", true, true, new SecsValue(7L).ToString()),
				new DeviceAddressExample("ulong", "", true, true, new SecsValue(8uL).ToString()),
				new DeviceAddressExample("float", "", true, true, new SecsValue(9f).ToString()),
				new DeviceAddressExample("double", "", true, true, new SecsValue(10.0).ToString()),
				new DeviceAddressExample("string", "", true, true, new SecsValue("ABC").ToString()),
				new DeviceAddressExample("bool", "", true, true, new SecsValue(true).ToString()),
				new DeviceAddressExample("byte[]", "", true, true, new SecsValue(new byte[3] { 1, 2, 3 }).ToString()),
				new DeviceAddressExample("list", "", true, true, new SecsValue(new object[1] { (short)3 }).ToString())
			};
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			comboBox_log_style.SelectedIndex = 0;
			panel2.Enabled = false;
			comboBox1.SelectedIndex = 1;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(GetSecsAddress());
			DemoUtils.AddSpecialFunctionTab(tabControl_buttom, addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl_buttom, codeExampleControl, false, CodeExampleControl.GetTitle());
			TreeNode treeNode = new TreeNode("S1");
			AddTree(treeNode, new SecsTreeItem(1, 1, true, null, "Are You Online"));
			AddTree(treeNode, new SecsTreeItem(1, 3, true, new SecsValue(new object[2] { 1u, 2u }), "Selected Equipment Status"));
			AddTree(treeNode, new SecsTreeItem(1, 5, true, new SecsValue(new byte[1] { 1 }), "Formatted Status"));
			AddTree(treeNode, new SecsTreeItem(1, 7, false, new SecsValue(new byte[1] { 1 }), "Fixed Form Request"));
			AddTree(treeNode, new SecsTreeItem(1, 9, true, null, "Material Transfer Status"));
			AddTree(treeNode, new SecsTreeItem(1, 11, true, new SecsValue(new object[2] { 1u, 2u }), "Status Variable Namelist"));
			AddTree(treeNode, new SecsTreeItem(1, 13, true, new SecsValue(new object[2]
			{
				new byte[1] { 1 },
				new object[0]
			}), "Establish Communications"));
			AddTree(treeNode, new SecsTreeItem(1, 15, true, null, "Request OFF-LINE"));
			AddTree(treeNode, new SecsTreeItem(1, 17, true, null, "Request ON-LINE"));
			AddTree(treeNode, new SecsTreeItem(1, 19, true, new SecsValue(new object[3]
			{
				"object class name",
				new object[2] { "Job0001", "Job0002" },
				new object[2] { "attribute1", "attribute2" }
			}), "Get Attribute"));
			AddTree(treeNode, new SecsTreeItem(1, 21, true, new SecsValue(new object[2] { "Variable ID1", "Variable ID2" }), "Data Variable Namelist"));
			AddTree(treeNode, new SecsTreeItem(1, 23, true, new SecsValue(new object[2] { 1u, 2u }), "Collection Event Namelist"));
			treeView1.Nodes.Add(treeNode);
			TreeNode treeNode2 = new TreeNode("S2");
			AddTree(treeNode2, new SecsTreeItem(2, 1, false, new SecsValue(new byte[3] { 1, 2, 3 }), "Service Program Load Grant"));
			AddTree(treeNode2, new SecsTreeItem(2, 3, false, new SecsValue(new byte[3] { 1, 2, 3 }), "Service Program Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 5, false, new SecsValue("bin00"), "Service Program Load"));
			AddTree(treeNode2, new SecsTreeItem(2, 7, false, new SecsValue("bin00"), "Service Program Run Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 9, false, new SecsValue("bin00"), "Service Program Results"));
			AddTree(treeNode2, new SecsTreeItem(2, 11, false, null, "Service Program Directory"));
			AddTree(treeNode2, new SecsTreeItem(2, 13, true, new SecsValue(new object[2] { 1u, 2u }), "Equipment Constant"));
			AddTree(treeNode2, new SecsTreeItem(2, 15, true, new SecsValue(new object[2]
			{
				new object[2] { 1u, "1" },
				new object[2] { 2u, "2" }
			}), "New Equipment Constant Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 17, true, null, "Date and Time"));
			AddTree(treeNode2, new SecsTreeItem(2, 19, true, new SecsValue((byte)1), "Reset/Initialize Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 21, true, new SecsValue("pause"), "Remote Command Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 23, true, new SecsValue(new object[5]
			{
				"TRID",
				"000500",
				4u,
				5u,
				new object[2] { 1u, 2u }
			}), "Trace Initialize Send"));
			AddTree(treeNode2, new SecsTreeItem(2, 25, true, new SecsValue("00 01 03 03 0a 0d 1b 5d 18 18 1a 04 13 7f 80 fe ff".ToHexBytes()), "Loopback Diagnostic"));
			AddTree(treeNode2, new SecsTreeItem(2, 27, true, new SecsValue(new object[3]
			{
				new byte[1] { 1 },
				"banana",
				new object[1] { "ee052793.1" }
			}), "Initiate Processing Request"));
			AddTree(treeNode2, new SecsTreeItem(2, 29, true, new SecsValue(new object[1] { 220u }), "Equipment Constant Namelist"));
			AddTree(treeNode2, new SecsTreeItem(2, 31, true, new SecsValue(DateTime.Now.ToString("yyyyMMddHHmmss")), "Date and Time Set"));
			AddTree(treeNode2, new SecsTreeItem(2, 33, true, new SecsValue(new object[2]
			{
				1u,
				new object[1] { new object[2]
				{
					4u,
					new object[1] { "810" }
				} }
			}), "Define Report"));
			AddTree(treeNode2, new SecsTreeItem(2, 35, true, new SecsValue(new object[2]
			{
				1u,
				new object[1] { new object[2]
				{
					4050u,
					new object[1] { 1u }
				} }
			}), "Link Event Report"));
			AddTree(treeNode2, new SecsTreeItem(2, 37, true, new SecsValue(new object[2]
			{
				1u,
				new object[1] { new object[2]
				{
					4050u,
					new object[1] { 1u }
				} }
			}), "Enable/Disable Event Report"));
			AddTree(treeNode2, new SecsTreeItem(2, 39, true, new SecsValue(new object[2] { 1u, 649u }), "Multi-block Inquire"));
			AddTree(treeNode2, new SecsTreeItem(2, 41, true, new SecsValue(new object[2]
			{
				"pause",
				new object[1] { new object[2] { "ppexecname", "cmos168-zl0EC" } }
			}), "Host Command Send"));
			treeView1.Nodes.Add(treeNode2);
			TreeNode treeNode3 = new TreeNode("S3");
			AddTree(treeNode3, new SecsTreeItem(3, 1, true, null, "Material Status"));
			AddTree(treeNode3, new SecsTreeItem(3, 3, true, null, "Time to Completion Data"));
			AddTree(treeNode3, new SecsTreeItem(3, 5, true, new SecsValue(new object[2]
			{
				new byte[1] { 1 },
				new byte[1] { 24 }
			}), "Material Found Send"));
			AddTree(treeNode3, new SecsTreeItem(3, 7, true, new SecsValue(new object[3]
			{
				new byte[1] { 1 },
				new byte[1] { 24 },
				"ee052793.1"
			}), "Material Lost Send"));
			AddTree(treeNode3, new SecsTreeItem(3, 9, true, new SecsValue(new object[2] { "ee052793.1", "1" }), "Matl ID Equate Send"));
			AddTree(treeNode3, new SecsTreeItem(3, 11, true, new SecsValue((byte)1), "Matl ID Request"));
			AddTree(treeNode3, new SecsTreeItem(3, 13, true, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Matl ID Send"));
			AddTree(treeNode3, new SecsTreeItem(3, 17, true, new SecsValue(new object[5]
			{
				1u,
				"ProceedWithCarrier",
				"CSX 52078",
				(byte)1,
				new object[1] { new object[2] { "Usage", "product" } }
			}), "Carrier Action Request"));
			AddTree(treeNode3, new SecsTreeItem(3, 19, true, null, "Cancel All Carrier Out Req"));
			AddTree(treeNode3, new SecsTreeItem(3, 21, true, new SecsValue(new object[3]
			{
				"buffer1",
				(byte)1,
				new object[1] { (byte)1 }
			}), "Port Group Defn"));
			AddTree(treeNode3, new SecsTreeItem(3, 23, true, new SecsValue(new object[3]
			{
				"CancelReservationAtPort",
				"buffer1",
				new object[2]
				{
					"ServiceStatus",
					(byte)1
				}
			}), "Port Group Action Req"));
			AddTree(treeNode3, new SecsTreeItem(3, 25, true, new SecsValue(new object[3]
			{
				" ChangeServiceStatus",
				(byte)1,
				new object[1] { new object[2]
				{
					"ServiceStatus",
					(byte)1
				} }
			}), "Port Action Req"));
			AddTree(treeNode3, new SecsTreeItem(3, 27, true, new SecsValue(new object[2]
			{
				(byte)0,
				new object[1] { (byte)1 }
			}), "Change Access"));
			AddTree(treeNode3, new SecsTreeItem(3, 29, true, new SecsValue(new object[4] { "logical ID", "Carrier:CSX 52078", "S01", 649u }), "Carrier Tag Read Req"));
			AddTree(treeNode3, new SecsTreeItem(3, 31, true, new SecsValue(new object[5] { "logical ID", "Carrier:CSX 52078", "S01", 649u, "unformatted data" }), "Carrier Tag Write Data"));
			AddTree(treeNode3, new SecsTreeItem(3, 33, true, null, "Cancel All Pod Out Req"));
			treeView1.Nodes.Add(treeNode3);
			TreeNode treeNode4 = new TreeNode("S4");
			AddTree(treeNode4, new SecsTreeItem(4, 1, true, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Ready to Send Materials"));
			AddTree(treeNode4, new SecsTreeItem(4, 3, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Send Material"));
			AddTree(treeNode4, new SecsTreeItem(4, 5, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Handshake Complete"));
			AddTree(treeNode4, new SecsTreeItem(4, 9, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Stuck in Sender"));
			AddTree(treeNode4, new SecsTreeItem(4, 11, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Stuck in Receiver"));
			AddTree(treeNode4, new SecsTreeItem(4, 13, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Send Incomplete Timeout"));
			AddTree(treeNode4, new SecsTreeItem(4, 15, false, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Material Received"));
			AddTree(treeNode4, new SecsTreeItem(4, 17, true, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Request to Receive"));
			AddTree(treeNode4, new SecsTreeItem(4, 19, true, new SecsValue(new object[2]
			{
				1u,
				new object[2]
				{
					"TJH_U_M_E1086",
					new object[12]
					{
						3u,
						1u,
						"c000678",
						7u,
						(byte)1,
						"standard exchange",
						"AGV0001",
						1u,
						(byte)1,
						(byte)1,
						1u,
						"TF:1 0"
					}
				}
			}), "Transfer Job Create"));
			AddTree(treeNode4, new SecsTreeItem(4, 21, true, new SecsValue(new object[3]
			{
				new byte[1] { 96 },
				"PAUSE",
				new object[1] { new object[2] { "ppexecname", "cmos168-zl0EC3" } }
			}), "Transfer Job Command"));
			treeView1.Nodes.Add(treeNode4);
			TreeNode treeNode5 = new TreeNode("S5");
			AddTree(treeNode5, new SecsTreeItem(5, 3, true, new SecsValue(new object[2]
			{
				new byte[1],
				1000u
			}), "Enable/Disable Alarm Send"));
			AddTree(treeNode5, new SecsTreeItem(5, 5, true, new SecsValue(0u), "List Alarms Request"));
			AddTree(treeNode5, new SecsTreeItem(5, 7, true, null, "List Enabled Alarm Request"));
			AddTree(treeNode5, new SecsTreeItem(5, 9, true, new SecsValue(new object[5]
			{
				"YYMMDDHHMMSS",
				"out of ink",
				"ALARM",
				"ink not sensed at nozzle inlet",
				new object[1] { "manually insert new ink cartridge" }
			}), "Exception Post Notify"));
			AddTree(treeNode5, new SecsTreeItem(5, 11, true, new SecsValue(new object[4] { "YYMMDDHHMMSS", "out of ink", "ALARM", "ink not sensed at nozzle inlet" }), "Exception Clear Notify"));
			AddTree(treeNode5, new SecsTreeItem(5, 13, true, new SecsValue(new object[2] { "out of ink", "manually insert new ink cartridge" }), "Exception Recover Request"));
			AddTree(treeNode5, new SecsTreeItem(5, 17, true, new SecsValue("out of ink"), "Exception Recovery Abort Request"));
			treeView1.Nodes.Add(treeNode5);
			TreeNode treeNode6 = new TreeNode("S6");
			AddTree(treeNode6, new SecsTreeItem(6, 1, true, new SecsValue(new object[4]
			{
				"1",
				10u,
				"YYMMDDHHMMSS",
				new object[1] { (byte)65 }
			}), "Trace Data Send"));
			AddTree(treeNode6, new SecsTreeItem(6, 3, true, new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[1] { new object[2]
				{
					"12",
					new object[1] { new object[2] { 10u, "54" } }
				} }
			}), "Discrete Variable Data Send"));
			AddTree(treeNode6, new SecsTreeItem(6, 5, true, new SecsValue(new object[2] { 1u, 649u }), "Multi-block Data Send Inquire"));
			AddTree(treeNode6, new SecsTreeItem(6, 7, true, new SecsValue(1u), "Data Transfer Request"));
			AddTree(treeNode6, new SecsTreeItem(6, 9, true, new SecsValue(new object[4]
			{
				new byte[1] { 2 },
				1u,
				4050u,
				new object[1] { new object[2]
				{
					"12",
					new object[1] { "54" }
				} }
			}), "Formatted Variable Send"));
			treeView1.Nodes.Add(treeNode6);
			TreeNode treeNode7 = new TreeNode("S7");
			AddTree(treeNode7, new SecsTreeItem(7, 1, true, new SecsValue(new object[2] { "banana", 322u }), "Process Program Load Inquire"));
			AddTree(treeNode7, new SecsTreeItem(7, 3, true, new SecsValue(new object[2]
			{
				"banana",
				new byte[1]
			}), "Process Program Send\t"));
			treeView1.AfterSelect += TreeView1_AfterSelect;
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (secs != null)
			{
				switch (comboBox1.SelectedIndex)
				{
				case 0:
					secs.StringEncoding = Encoding.ASCII;
					break;
				case 1:
					secs.StringEncoding = Encoding.Default;
					break;
				case 2:
					secs.StringEncoding = Encoding.UTF8;
					break;
				case 3:
					secs.StringEncoding = Encoding.Unicode;
					break;
				case 4:
					secs.StringEncoding = Encoding.GetEncoding("gb2312");
					break;
				}
			}
		}

		private void RenderSelectedSecs(SecsTreeItem treeItem)
		{
			textBox_stream.Text = treeItem.S.ToString();
			textBox_function.Text = treeItem.F.ToString();
			checkBox_back.Checked = treeItem.W;
			if (treeItem.Value != null)
			{
				textBox_data.Text = (checkBox_code_style.Checked ? treeItem.Value.ToSourceCode(true) : treeItem.Value.ToXElement().ToString());
			}
			else
			{
				textBox_data.Text = string.Empty;
			}
			if (treeItem.ValueSingular != null)
			{
				textBox_receive.Text = (checkBox_code_style.Checked ? treeItem.ValueSingular.ToSourceCode(true) : treeItem.ValueSingular.ToString());
			}
			else
			{
				textBox_receive.Text = string.Empty;
			}
			renderSecsValueLeft = treeItem.Value;
			renderSecsValueRight = treeItem.ValueSingular;
		}

		private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is SecsTreeItem treeItem)
			{
				RenderSelectedSecs(treeItem);
			}
		}

		public void AddTree(TreeNode treeNode, SecsTreeItem treeItem)
		{
			TreeNode treeNode2 = new TreeNode(string.Format("S{0}F{1}{2} {3}", treeItem.S, treeItem.F, treeItem.W ? "W" : "", treeItem.Description));
			treeNode2.Tag = treeItem;
			treeNode.Nodes.Add(treeNode2);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "SECS Read Demo";
				label1.Text = "Ip:";
				label3.Text = "Port:";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				checkBox2.Text = "Initialization S0F0?";
				checkBox_autoBackS1F1.Text = "Auto Back S1F1?";
				checkBox_code_style.Text = "Show Code Info";
				tabPage_log.Text = "Value";
			}
			else
			{
				addNewSecsItemToolStripMenuItem.Text = "新增Secs功能码";
				editSecsItemToolStripMenuItem.Text = "编辑Secs功能码";
				deleteSecsItemToolStripMenuItem.Text = "删除Secs功能码";
				checkBox_show_send_message.Text = "显示发送消息";
				label13.Text = "SECS 日志区域";
				checkBox1.Text = "暂停输出";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_port.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			secs?.ConnectClose();
			secs = new SecsHsms(textBox_ip.Text, result);
			secs.DeviceID = ushort.Parse(textBox_deviceID.Text);
			secs.OnSecsMessageReceived += Secs_OnSecsMessageReceived;
			secs.InitializationS0F0 = checkBox2.Checked;
			secs.AutoBackS1F1 = checkBox_autoBackS1F1.Checked;
			secs.LogNet = base.LogNet;
			ComboBox1_SelectedIndexChanged(comboBox1, e);
			secs.LogNet = base.LogNet;
			try
			{
				OperateResult operateResult = secs.ConnectServer();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					List<string> list = new List<string>();
					list.Add("DeviceID");
					list.Add("InitializationS0F0");
					if (!secs.AutoBackS1F1)
					{
						list.Add("AutoBackS1F1");
					}
					list.Add("StringEncoding");
					codeExampleControl.SetCodeText("secs", secs, list.ToArray());
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void Secs_OnSecsMessageReceived(object sender, SecsMessage secsMessage)
		{
			if (base.InvokeRequired)
			{
				Invoke(new Action<object, SecsMessage>(Secs_OnSecsMessageReceived), sender, secsMessage);
				return;
			}
			if (!checkBox1.Checked)
			{
				AppendHeadText(-1, string.Format("RECV: S{0}F{1}{2} SystemBytes=", secsMessage.StreamNo, secsMessage.FunctionNo, secsMessage.W ? "W" : string.Empty) + secsMessage.MessageID);
				AppendHeadText(0, secsMessage.GetItemValues());
				richTextBox_main.ScrollToCaret();
			}
			if (secsMessage.FunctionNo == 0 || secsMessage.FunctionNo % 2 != 1)
			{
				return;
			}
			for (int i = 0; i < treeView1.Nodes.Count; i++)
			{
				if ("S" + secsMessage.StreamNo != treeView1.Nodes[i].Text)
				{
					continue;
				}
				TreeNode treeNode = treeView1.Nodes[i];
				for (int j = 0; j < treeNode.Nodes.Count; j++)
				{
					if (treeNode.Nodes[j].Tag is SecsTreeItem secsTreeItem && secsTreeItem.F == secsMessage.FunctionNo)
					{
						if (secsTreeItem.ValueSingular == null)
						{
							break;
						}
						SendSecsValue(secsMessage, secsTreeItem.S, (byte)(secsTreeItem.F + 1), secsTreeItem.W, secsTreeItem.ValueSingular);
					}
				}
				break;
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			secs.ConnectClose();
			button2.Enabled = false;
			button1.Enabled = true;
			panel2.Enabled = false;
		}

		private void button25_Click(object sender, EventArgs e)
		{
			SecsValue secsValue = (string.IsNullOrEmpty(textBox_data.Text) ? new SecsValue() : new SecsValue(XElement.Parse(textBox_data.Text)));
			OperateResult<SecsMessage> operateResult = secs.ReadSecsMessage(byte.Parse(textBox_stream.Text), byte.Parse(textBox_function.Text), secsValue, checkBox_back.Checked);
			if (operateResult.IsSuccess)
			{
				textBox_receive.Text = DateTime.Now.ToString("HH:mm:ss") + ": " + Environment.NewLine + operateResult.Content.GetItemValues();
			}
			else
			{
				DemoUtils.ShowMessage("读取失败！" + operateResult.ToMessageShowString());
			}
			string text = ((secsValue == null) ? "null" : secsValue.ToSourceCode());
			codeExampleControl.ReaderReadCode("OperateResult<SecsMessage> read = @deviceName.ReadSecsMessage( " + textBox_stream.Text + ", " + textBox_function.Text + ", " + text + ", " + checkBox_back.Checked.ToString().ToLower() + " );");
		}

		private void button3_Click(object sender, EventArgs e)
		{
			SecsValue secsValue = (string.IsNullOrEmpty(textBox_data.Text) ? SecsValue.EmptySecsValue() : new SecsValue(XElement.Parse(textBox_data.Text)));
			OperateResult operateResult = secs.SendByCommand(byte.Parse(textBox_stream.Text), byte.Parse(textBox_function.Text), secsValue, checkBox_back.Checked);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("发送成功！");
			}
			else
			{
				DemoUtils.ShowMessage("发送失败！" + operateResult.ToMessageShowString());
			}
			string text = ((secsValue == null) ? "null" : secsValue.ToSourceCode());
			codeExampleControl.ReaderReadCode("OperateResult send = @deviceName.SendByCommand( " + textBox_stream.Text + ", " + textBox_function.Text + ", " + text + ", " + checkBox_back.Checked.ToString().ToLower() + " );");
		}

		private void button4_Click(object sender, EventArgs e)
		{
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlBaudRate, textBox_port.Text);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox_deviceID.Text);
			element.SetAttributeValue("Encoding", comboBox1.SelectedIndex);
			element.SetAttributeValue("S0F0", checkBox2.Checked);
			element.SetAttributeValue("AutoBack", checkBox_autoBackS1F1.Checked);
			element.RemoveNodes();
			for (int i = 0; i < treeView1.Nodes.Count; i++)
			{
				TreeNode treeNode = treeView1.Nodes[i];
				XElement xElement = new XElement(treeNode.Text);
				for (int j = 0; j < treeNode.Nodes.Count; j++)
				{
					if (treeNode.Nodes[j].Tag is SecsTreeItem secsTreeItem)
					{
						xElement.Add(secsTreeItem.ToXElement());
					}
				}
				element.Add(xElement);
			}
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = SoftBasic.GetXmlValue(element, DemoDeviceList.XmlIpAddress, "127.0.0.1");
			textBox_port.Text = element.Attribute(DemoDeviceList.XmlBaudRate).Value;
			textBox_deviceID.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			comboBox1.SelectedIndex = SoftBasic.GetXmlValue(element, "Encoding", 1);
			checkBox2.Checked = SoftBasic.GetXmlValue(element, "S0F0", false);
			checkBox_autoBackS1F1.Checked = SoftBasic.GetXmlValue(element, "AutoBack", true);
			IEnumerable<XElement> enumerable = element.Elements();
			if (enumerable.Count() <= 0)
			{
				return;
			}
			treeView1.Nodes.Clear();
			foreach (XElement item in enumerable)
			{
				TreeNode treeNode = new TreeNode(item.Name.ToString());
				foreach (XElement item2 in item.Elements())
				{
					SecsTreeItem secsTreeItem = new SecsTreeItem(item2);
					TreeNode treeNode2 = new TreeNode(secsTreeItem.GetTreeNodeText());
					treeNode2.Tag = secsTreeItem;
					treeNode.Nodes.Add(treeNode2);
				}
				treeView1.Nodes.Add(treeNode);
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void button_S1F1_Click(object sender, EventArgs e)
		{
			OperateResult<OnlineData> operateResult = secs.Gem.S1F1_AreYouThere();
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button_s1f11_Click(object sender, EventArgs e)
		{
			OperateResult<VariableName[]> operateResult = null;
			operateResult = ((!string.IsNullOrEmpty(textBox4.Text)) ? secs.Gem.S1F11_StatusVariableNamelist(textBox4.Text.ToStringArray<int>()) : secs.Gem.S1F11_StatusVariableNamelist());
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button_s1f13_Click(object sender, EventArgs e)
		{
			OperateResult<OnlineData> operateResult = secs.Gem.S1F13_EstablishCommunications();
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content.ToJsonString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button_s1f15_Click(object sender, EventArgs e)
		{
			OperateResult<byte> operateResult = secs.Gem.S1F15_OfflineRequest();
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content + Environment.NewLine + "返回值说明，0: ok, 1: refused, 2: already online";
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button_s1f17_Click(object sender, EventArgs e)
		{
			OperateResult<byte> operateResult = secs.Gem.S1F17_OnlineRequest();
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content + Environment.NewLine + "返回值说明，0: ok, 1: refused, 2: already online";
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button_s2f13_Click(object sender, EventArgs e)
		{
			OperateResult<SecsValue> operateResult = secs.Gem.S2F13_EquipmentConstantRequest();
			if (operateResult.IsSuccess)
			{
				textBox_s1.Text = operateResult.Content.ToString();
			}
			else
			{
				DemoUtils.ShowMessage("Read failed: " + operateResult.Message);
			}
		}

		private void button4_Click_1(object sender, EventArgs e)
		{
			richTextBox_main.Clear();
		}

		private void RenderMessage(int code, string msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				return;
			}
			int length = richTextBox_main.Text.Length;
			richTextBox_main.AppendText(msg);
			richTextBox_main.Select(length, msg.Length);
			if (code < 0)
			{
				richTextBox_main.SelectionColor = Color.Gray;
			}
			else
			{
				switch (code)
				{
				case 0:
					richTextBox_main.SelectionColor = Color.Green;
					break;
				case 1:
					richTextBox_main.SelectionColor = Color.Blue;
					break;
				case 4:
					richTextBox_main.SelectionColor = Color.Purple;
					break;
				default:
					richTextBox_main.SelectionColor = Color.Red;
					break;
				}
			}
			richTextBox_main.AppendText(Environment.NewLine);
		}

		private void AppendHeadText(int code, string text)
		{
			RenderMessage(code, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + text);
		}

		private void AppendHeadText(int code, SecsValue secsValue)
		{
			RenderMessage(code, (comboBox_log_style.SelectedIndex == 0) ? secsValue.ToString() : ((comboBox_log_style.SelectedIndex == 1) ? secsValue.ToSourceCode(true) : ((comboBox_log_style.SelectedIndex == 2) ? secsValue.ToSourceCode() : secsValue.ToSMLString())));
		}

		private void SendSecsValue(SecsMessage secsMessage, byte s, byte f, bool w, SecsValue secsValue)
		{
			OperateResult operateResult = null;
			operateResult = ((secsMessage == null) ? secs.SendByCommand(byte.Parse(textBox_stream.Text), byte.Parse(textBox_function.Text), secsValue, w) : secs.SendByCommand(secsMessage, secsValue, w));
			if (!operateResult.IsSuccess)
			{
				AppendHeadText(5, string.Format("SEND:  S{0}F{1}{2}", s, f, w ? "W" : string.Empty) + " faild: " + operateResult.Message);
				richTextBox_main.ScrollToCaret();
			}
			else if (checkBox_show_send_message.Checked)
			{
				AppendHeadText(-1, string.Format("SEND:  S{0}F{1}{2}", s, f, w ? "W" : string.Empty));
				AppendHeadText(1, secsValue);
				richTextBox_main.ScrollToCaret();
			}
		}

		private void sendMessageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is SecsTreeItem secsTreeItem)
			{
				if (secsTreeItem.Value == null)
				{
					secsTreeItem.Value = SecsValue.EmptySecsValue();
				}
				SecsValue value = secsTreeItem.Value;
				SendSecsValue(null, secsTreeItem.S, secsTreeItem.F, secsTreeItem.W, value);
				string text = ((value == null) ? "null" : value.ToSourceCode());
				codeExampleControl.ReaderReadCode("OperateResult send = @deviceName.SendByCommand( " + textBox_stream.Text + ", " + textBox_function.Text + ", " + text + ", " + checkBox_back.Checked.ToString().ToLower() + " ); // 仅发送消息" + Environment.NewLine + Environment.NewLine + "OperateResult<SecsMessage> read = @deviceName.ReadSecsMessage( " + textBox_stream.Text + ", " + textBox_function.Text + ", " + text + ", " + checkBox_back.Checked.ToString().ToLower() + " );   // 应答方式");
			}
		}

		private void addNewSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.AddNewSecsItemToolStripMenuItem_Click(treeView1, RenderSelectedSecs, false);
		}

		private void editSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.EditSecsItemToolStripMenuItem_Click(treeView1, RenderSelectedSecs, false);
		}

		private void deleteSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.DeleteSecsItemToolStripMenuItem_Click(treeView1);
		}

		private void treeView1_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				TreeNode nodeAt = treeView1.GetNodeAt(e.Location);
				if (nodeAt != null)
				{
					treeView1.SelectedNode = nodeAt;
					contextMenuStrip1.Show(treeView1, e.Location);
				}
			}
		}

		private void checkBox_code_style_CheckedChanged(object sender, EventArgs e)
		{
			if (renderSecsValueLeft != null)
			{
				textBox_data.Text = (checkBox_code_style.Checked ? renderSecsValueLeft.ToSourceCode(true) : renderSecsValueLeft.ToXElement().ToString());
			}
			if (renderSecsValueRight != null)
			{
				textBox_receive.Text = (checkBox_code_style.Checked ? renderSecsValueRight.ToSourceCode(true) : renderSecsValueRight.ToString());
			}
		}

		private void button_control_select_Click(object sender, EventArgs e)
		{
			AppendHeadText(-1, "SEND:  S0F0 PType[0] SType[1]");
			richTextBox_main.ScrollToCaret();
			OperateResult operateResult = secs.SendControlMessage(0, 1);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Select.req send success!");
			}
			else
			{
				DemoUtils.ShowMessage("Select.req send faile: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = secs.SendControlMessage( 0x00, 0x01 );";
		}

		private void button_control_linktest_Click(object sender, EventArgs e)
		{
			AppendHeadText(-1, "SEND:  S0F0 PType[0] SType[5]");
			richTextBox_main.ScrollToCaret();
			OperateResult operateResult = secs.SendControlMessage(0, 5);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Linktest.req send success!");
			}
			else
			{
				DemoUtils.ShowMessage("Linktest.req send faile: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = secs.SendControlMessage( 0x00, 0x05 );";
		}

		private void button_control_separate_Click(object sender, EventArgs e)
		{
			AppendHeadText(-1, "SEND:  S0F0 PType[0] SType[9]");
			richTextBox_main.ScrollToCaret();
			OperateResult operateResult = secs.SendControlMessage(0, 9);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Separate.req send success!");
			}
			else
			{
				DemoUtils.ShowMessage("Separate.req send faile: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = secs.SendControlMessage( 0x00, 0x09 );";
		}

		private void button_control_send_Click(object sender, EventArgs e)
		{
			AppendHeadText(-1, "SEND:  S0F0 PType[" + textBox_ptype.Text + "] SType[" + textBox_stype.Text + "]");
			richTextBox_main.ScrollToCaret();
			OperateResult operateResult = secs.SendControlMessage(byte.Parse(textBox_ptype.Text), byte.Parse(textBox_stype.Text));
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("PType[" + textBox_ptype.Text + "] SType[" + textBox_stype.Text + "] Send success!");
			}
			else
			{
				DemoUtils.ShowMessage("PType[" + textBox_ptype.Text + "] SType[" + textBox_stype.Text + "] faile: " + operateResult.Message);
			}
			textBox_code.Text = "OperateResult send = secs.SendControlMessage( " + textBox_ptype.Text + ", " + textBox_stype.Text + " );";
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.checkBox_autoBackS1F1 = new System.Windows.Forms.CheckBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_deviceID = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.comboBox_log_style = new System.Windows.Forms.ComboBox();
			this.richTextBox_main = new System.Windows.Forms.RichTextBox();
			this.button4 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.label13 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.checkBox_show_send_message = new System.Windows.Forms.CheckBox();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.button_s1f17 = new System.Windows.Forms.Button();
			this.button_s1f15 = new System.Windows.Forms.Button();
			this.button_s1f13 = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.button_s1f11 = new System.Windows.Forms.Button();
			this.textBox_s1 = new System.Windows.Forms.TextBox();
			this.button_S1F1 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.tabPage_s2 = new System.Windows.Forms.TabPage();
			this.panel3 = new System.Windows.Forms.Panel();
			this.textBox_s2 = new System.Windows.Forms.TextBox();
			this.button_s2f13 = new System.Windows.Forms.Button();
			this.tabControl_buttom = new System.Windows.Forms.TabControl();
			this.tabPage_log = new System.Windows.Forms.TabPage();
			this.checkBox_code_style = new System.Windows.Forms.CheckBox();
			this.label11 = new System.Windows.Forms.Label();
			this.checkBox_back = new System.Windows.Forms.CheckBox();
			this.textBox_stream = new System.Windows.Forms.TextBox();
			this.textBox_function = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.textBox_data = new System.Windows.Forms.TextBox();
			this.textBox_receive = new System.Windows.Forms.TextBox();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.button_control_send = new System.Windows.Forms.Button();
			this.textBox_stype = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_ptype = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button_control_separate = new System.Windows.Forms.Button();
			this.button_control_linktest = new System.Windows.Forms.Button();
			this.button_control_select = new System.Windows.Forms.Button();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.sendMessageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.addNewSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.editSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.deleteSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage_s2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.tabControl_buttom.SuspendLayout();
			this.tabPage_log.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer2).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_autoBackS1F1);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.textBox_deviceID);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1004, 62);
			this.panel1.TabIndex = 0;
			this.checkBox_autoBackS1F1.AutoSize = true;
			this.checkBox_autoBackS1F1.Checked = true;
			this.checkBox_autoBackS1F1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_autoBackS1F1.Location = new System.Drawing.Point(170, 35);
			this.checkBox_autoBackS1F1.Name = "checkBox_autoBackS1F1";
			this.checkBox_autoBackS1F1.Size = new System.Drawing.Size(102, 21);
			this.checkBox_autoBackS1F1.TabIndex = 15;
			this.checkBox_autoBackS1F1.Text = "自动返回S1F1";
			this.checkBox_autoBackS1F1.UseVisualStyleBackColor = true;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[5] { "ASCII", "Default", "UTF8", "Unicode", "GB2312" });
			this.comboBox1.Location = new System.Drawing.Point(576, 4);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(104, 25);
			this.comboBox1.TabIndex = 14;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(508, 9);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(63, 17);
			this.label5.TabIndex = 13;
			this.label5.Text = "Encode：";
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(4, 35);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(136, 21);
			this.checkBox2.TabIndex = 12;
			this.checkBox2.Text = "初始化时发送 S0F0?";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.textBox_port.Location = new System.Drawing.Point(278, 6);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(76, 23);
			this.textBox_port.TabIndex = 11;
			this.textBox_port.Text = "5000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(224, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 10;
			this.label3.Text = "端口号：";
			this.textBox_ip.Location = new System.Drawing.Point(62, 6);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(156, 23);
			this.textBox_ip.TabIndex = 9;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 8;
			this.label1.Text = "Ip地址：";
			this.textBox_deviceID.Location = new System.Drawing.Point(436, 6);
			this.textBox_deviceID.Name = "textBox_deviceID";
			this.textBox_deviceID.Size = new System.Drawing.Size(44, 23);
			this.textBox_deviceID.TabIndex = 7;
			this.textBox_deviceID.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(360, 9);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(71, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "DeviceID：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(783, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "断开连接";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(686, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "连接";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.splitContainer1);
			this.panel2.Location = new System.Drawing.Point(3, 99);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1004, 579);
			this.panel2.TabIndex = 1;
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
			this.splitContainer1.Panel2.Controls.Add(this.tabControl_buttom);
			this.splitContainer1.Size = new System.Drawing.Size(1002, 577);
			this.splitContainer1.SplitterDistance = 332;
			this.splitContainer1.TabIndex = 7;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage_s2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1002, 332);
			this.tabControl1.TabIndex = 6;
			this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
			this.tabPage1.Controls.Add(this.comboBox_log_style);
			this.tabPage1.Controls.Add(this.richTextBox_main);
			this.tabPage1.Controls.Add(this.button4);
			this.tabPage1.Controls.Add(this.treeView1);
			this.tabPage1.Controls.Add(this.label13);
			this.tabPage1.Controls.Add(this.checkBox1);
			this.tabPage1.Controls.Add(this.checkBox_show_send_message);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(994, 302);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Base";
			this.comboBox_log_style.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_log_style.FormattingEnabled = true;
			this.comboBox_log_style.Items.AddRange(new object[4] { "XML", "Code", "Code-lite", "SML" });
			this.comboBox_log_style.Location = new System.Drawing.Point(454, 3);
			this.comboBox_log_style.Name = "comboBox_log_style";
			this.comboBox_log_style.Size = new System.Drawing.Size(101, 25);
			this.comboBox_log_style.TabIndex = 27;
			this.richTextBox_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.richTextBox_main.Location = new System.Drawing.Point(344, 31);
			this.richTextBox_main.Name = "richTextBox_main";
			this.richTextBox_main.Size = new System.Drawing.Size(644, 289);
			this.richTextBox_main.TabIndex = 26;
			this.richTextBox_main.Text = "";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(936, 1);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(55, 28);
			this.button4.TabIndex = 16;
			this.button4.Text = "clear";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click_1);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(5, 6);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(333, 313);
			this.treeView1.TabIndex = 24;
			this.treeView1.MouseDown += new System.Windows.Forms.MouseEventHandler(treeView1_MouseDown);
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(342, 7);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(106, 17);
			this.label13.TabIndex = 9;
			this.label13.Text = "SECS Log Area：";
			this.checkBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(850, 6);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(58, 21);
			this.checkBox1.TabIndex = 15;
			this.checkBox1.Text = "STOP";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox_show_send_message.AutoSize = true;
			this.checkBox_show_send_message.Checked = true;
			this.checkBox_show_send_message.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_show_send_message.Location = new System.Drawing.Point(619, 6);
			this.checkBox_show_send_message.Name = "checkBox_show_send_message";
			this.checkBox_show_send_message.Size = new System.Drawing.Size(121, 21);
			this.checkBox_show_send_message.TabIndex = 25;
			this.checkBox_show_send_message.Text = "Show Send Msg";
			this.checkBox_show_send_message.UseVisualStyleBackColor = true;
			this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
			this.tabPage2.Controls.Add(this.button_s1f17);
			this.tabPage2.Controls.Add(this.button_s1f15);
			this.tabPage2.Controls.Add(this.button_s1f13);
			this.tabPage2.Controls.Add(this.textBox4);
			this.tabPage2.Controls.Add(this.button_s1f11);
			this.tabPage2.Controls.Add(this.textBox_s1);
			this.tabPage2.Controls.Add(this.button_S1F1);
			this.tabPage2.Controls.Add(this.label4);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(994, 306);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "S1";
			this.button_s1f17.Location = new System.Drawing.Point(10, 152);
			this.button_s1f17.Name = "button_s1f17";
			this.button_s1f17.Size = new System.Drawing.Size(222, 30);
			this.button_s1f17.TabIndex = 29;
			this.button_s1f17.Text = "S1F17-OnlineRequest";
			this.button_s1f17.UseVisualStyleBackColor = true;
			this.button_s1f17.Click += new System.EventHandler(button_s1f17_Click);
			this.button_s1f15.Location = new System.Drawing.Point(10, 116);
			this.button_s1f15.Name = "button_s1f15";
			this.button_s1f15.Size = new System.Drawing.Size(222, 30);
			this.button_s1f15.TabIndex = 28;
			this.button_s1f15.Text = "S1F15-OfflineRequest";
			this.button_s1f15.UseVisualStyleBackColor = true;
			this.button_s1f15.Click += new System.EventHandler(button_s1f15_Click);
			this.button_s1f13.Location = new System.Drawing.Point(10, 80);
			this.button_s1f13.Name = "button_s1f13";
			this.button_s1f13.Size = new System.Drawing.Size(222, 30);
			this.button_s1f13.TabIndex = 27;
			this.button_s1f13.Text = "S1F13-EstablishCommunications";
			this.button_s1f13.UseVisualStyleBackColor = true;
			this.button_s1f13.Click += new System.EventHandler(button_s1f13_Click);
			this.textBox4.Location = new System.Drawing.Point(282, 48);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(194, 23);
			this.textBox4.TabIndex = 25;
			this.button_s1f11.Location = new System.Drawing.Point(10, 44);
			this.button_s1f11.Name = "button_s1f11";
			this.button_s1f11.Size = new System.Drawing.Size(222, 30);
			this.button_s1f11.TabIndex = 24;
			this.button_s1f11.Text = "S1F11-StatusVariableNamelist";
			this.button_s1f11.UseVisualStyleBackColor = true;
			this.button_s1f11.Click += new System.EventHandler(button_s1f11_Click);
			this.textBox_s1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_s1.Location = new System.Drawing.Point(729, 6);
			this.textBox_s1.Multiline = true;
			this.textBox_s1.Name = "textBox_s1";
			this.textBox_s1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_s1.Size = new System.Drawing.Size(259, 289);
			this.textBox_s1.TabIndex = 23;
			this.button_S1F1.Location = new System.Drawing.Point(10, 8);
			this.button_S1F1.Name = "button_S1F1";
			this.button_S1F1.Size = new System.Drawing.Size(222, 30);
			this.button_S1F1.TabIndex = 0;
			this.button_S1F1.Text = "S1F1-AreYouThere";
			this.button_S1F1.UseVisualStyleBackColor = true;
			this.button_S1F1.Click += new System.EventHandler(button_S1F1_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(240, 51);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(38, 17);
			this.label4.TabIndex = 26;
			this.label4.Text = "VIDs:";
			this.tabPage_s2.BackColor = System.Drawing.SystemColors.Control;
			this.tabPage_s2.Controls.Add(this.panel3);
			this.tabPage_s2.Location = new System.Drawing.Point(4, 22);
			this.tabPage_s2.Name = "tabPage_s2";
			this.tabPage_s2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage_s2.Size = new System.Drawing.Size(994, 306);
			this.tabPage_s2.TabIndex = 2;
			this.tabPage_s2.Text = "S2";
			this.panel3.AutoScroll = true;
			this.panel3.Controls.Add(this.textBox_s2);
			this.panel3.Controls.Add(this.button_s2f13);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel3.Location = new System.Drawing.Point(3, 3);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(988, 300);
			this.panel3.TabIndex = 0;
			this.textBox_s2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_s2.Location = new System.Drawing.Point(726, 3);
			this.textBox_s2.Multiline = true;
			this.textBox_s2.Name = "textBox_s2";
			this.textBox_s2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_s2.Size = new System.Drawing.Size(259, 298);
			this.textBox_s2.TabIndex = 24;
			this.button_s2f13.Location = new System.Drawing.Point(8, 7);
			this.button_s2f13.Name = "button_s2f13";
			this.button_s2f13.Size = new System.Drawing.Size(222, 30);
			this.button_s2f13.TabIndex = 1;
			this.button_s2f13.Text = "S2F13-EquipmentConstant";
			this.button_s2f13.UseVisualStyleBackColor = true;
			this.button_s2f13.Click += new System.EventHandler(button_s2f13_Click);
			this.tabControl_buttom.Controls.Add(this.tabPage_log);
			this.tabControl_buttom.Controls.Add(this.tabPage3);
			this.tabControl_buttom.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl_buttom.Location = new System.Drawing.Point(0, 0);
			this.tabControl_buttom.Name = "tabControl_buttom";
			this.tabControl_buttom.SelectedIndex = 0;
			this.tabControl_buttom.Size = new System.Drawing.Size(1002, 241);
			this.tabControl_buttom.TabIndex = 3;
			this.tabPage_log.Controls.Add(this.checkBox_code_style);
			this.tabPage_log.Controls.Add(this.label11);
			this.tabPage_log.Controls.Add(this.checkBox_back);
			this.tabPage_log.Controls.Add(this.textBox_stream);
			this.tabPage_log.Controls.Add(this.textBox_function);
			this.tabPage_log.Controls.Add(this.label12);
			this.tabPage_log.Controls.Add(this.splitContainer2);
			this.tabPage_log.Location = new System.Drawing.Point(4, 26);
			this.tabPage_log.Name = "tabPage_log";
			this.tabPage_log.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage_log.Size = new System.Drawing.Size(994, 211);
			this.tabPage_log.TabIndex = 0;
			this.tabPage_log.Text = "当前值信息";
			this.tabPage_log.UseVisualStyleBackColor = true;
			this.checkBox_code_style.AutoSize = true;
			this.checkBox_code_style.Location = new System.Drawing.Point(413, 6);
			this.checkBox_code_style.Name = "checkBox_code_style";
			this.checkBox_code_style.Size = new System.Drawing.Size(99, 21);
			this.checkBox_code_style.TabIndex = 27;
			this.checkBox_code_style.Text = "代码样式显示";
			this.checkBox_code_style.UseVisualStyleBackColor = true;
			this.checkBox_code_style.CheckedChanged += new System.EventHandler(checkBox_code_style_CheckedChanged);
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(6, 7);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(61, 17);
			this.label11.TabIndex = 4;
			this.label11.Text = "Stream：";
			this.checkBox_back.AutoSize = true;
			this.checkBox_back.Location = new System.Drawing.Point(255, 6);
			this.checkBox_back.Name = "checkBox_back";
			this.checkBox_back.Size = new System.Drawing.Size(105, 21);
			this.checkBox_back.TabIndex = 21;
			this.checkBox_back.Text = "W? [必须回复]";
			this.checkBox_back.UseVisualStyleBackColor = true;
			this.textBox_stream.Location = new System.Drawing.Point(68, 4);
			this.textBox_stream.Name = "textBox_stream";
			this.textBox_stream.ReadOnly = true;
			this.textBox_stream.Size = new System.Drawing.Size(49, 23);
			this.textBox_stream.TabIndex = 5;
			this.textBox_stream.Text = "1";
			this.textBox_function.Location = new System.Drawing.Point(190, 4);
			this.textBox_function.Name = "textBox_function";
			this.textBox_function.ReadOnly = true;
			this.textBox_function.Size = new System.Drawing.Size(51, 23);
			this.textBox_function.TabIndex = 7;
			this.textBox_function.Text = "1";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(123, 7);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(68, 17);
			this.label12.TabIndex = 6;
			this.label12.Text = "Function：";
			this.splitContainer2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer2.Location = new System.Drawing.Point(7, 33);
			this.splitContainer2.Name = "splitContainer2";
			this.splitContainer2.Panel1.Controls.Add(this.textBox_data);
			this.splitContainer2.Panel2.Controls.Add(this.textBox_receive);
			this.splitContainer2.Size = new System.Drawing.Size(981, 172);
			this.splitContainer2.SplitterDistance = 439;
			this.splitContainer2.TabIndex = 26;
			this.textBox_data.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBox_data.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_data.Location = new System.Drawing.Point(0, 0);
			this.textBox_data.Multiline = true;
			this.textBox_data.Name = "textBox_data";
			this.textBox_data.ReadOnly = true;
			this.textBox_data.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_data.Size = new System.Drawing.Size(439, 172);
			this.textBox_data.TabIndex = 20;
			this.textBox_receive.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBox_receive.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_receive.Location = new System.Drawing.Point(0, 0);
			this.textBox_receive.Multiline = true;
			this.textBox_receive.Name = "textBox_receive";
			this.textBox_receive.ReadOnly = true;
			this.textBox_receive.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_receive.Size = new System.Drawing.Size(538, 172);
			this.textBox_receive.TabIndex = 12;
			this.tabPage3.Controls.Add(this.button_control_send);
			this.tabPage3.Controls.Add(this.textBox_stype);
			this.tabPage3.Controls.Add(this.label6);
			this.tabPage3.Controls.Add(this.textBox_ptype);
			this.tabPage3.Controls.Add(this.label2);
			this.tabPage3.Controls.Add(this.button_control_separate);
			this.tabPage3.Controls.Add(this.button_control_linktest);
			this.tabPage3.Controls.Add(this.button_control_select);
			this.tabPage3.Controls.Add(this.textBox_code);
			this.tabPage3.Location = new System.Drawing.Point(4, 26);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(994, 211);
			this.tabPage3.TabIndex = 1;
			this.tabPage3.Text = "控制消息";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.button_control_send.Location = new System.Drawing.Point(744, 6);
			this.button_control_send.Name = "button_control_send";
			this.button_control_send.Size = new System.Drawing.Size(133, 30);
			this.button_control_send.TabIndex = 11;
			this.button_control_send.Text = "Send";
			this.button_control_send.UseVisualStyleBackColor = true;
			this.button_control_send.Click += new System.EventHandler(button_control_send_Click);
			this.textBox_stype.Location = new System.Drawing.Point(680, 10);
			this.textBox_stype.Name = "textBox_stype";
			this.textBox_stype.Size = new System.Drawing.Size(44, 23);
			this.textBox_stype.TabIndex = 10;
			this.textBox_stype.Text = "1";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(625, 13);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(46, 17);
			this.label6.TabIndex = 9;
			this.label6.Text = "SType:";
			this.textBox_ptype.Location = new System.Drawing.Point(564, 10);
			this.textBox_ptype.Name = "textBox_ptype";
			this.textBox_ptype.Size = new System.Drawing.Size(44, 23);
			this.textBox_ptype.TabIndex = 8;
			this.textBox_ptype.Text = "0";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(512, 13);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(46, 17);
			this.label2.TabIndex = 4;
			this.label2.Text = "PType:";
			this.button_control_separate.Location = new System.Drawing.Point(244, 6);
			this.button_control_separate.Name = "button_control_separate";
			this.button_control_separate.Size = new System.Drawing.Size(112, 30);
			this.button_control_separate.TabIndex = 3;
			this.button_control_separate.Text = "Separate.req";
			this.button_control_separate.UseVisualStyleBackColor = true;
			this.button_control_separate.Click += new System.EventHandler(button_control_separate_Click);
			this.button_control_linktest.Location = new System.Drawing.Point(126, 6);
			this.button_control_linktest.Name = "button_control_linktest";
			this.button_control_linktest.Size = new System.Drawing.Size(112, 30);
			this.button_control_linktest.TabIndex = 2;
			this.button_control_linktest.Text = "Linktest.req";
			this.button_control_linktest.UseVisualStyleBackColor = true;
			this.button_control_linktest.Click += new System.EventHandler(button_control_linktest_Click);
			this.button_control_select.Location = new System.Drawing.Point(8, 6);
			this.button_control_select.Name = "button_control_select";
			this.button_control_select.Size = new System.Drawing.Size(112, 30);
			this.button_control_select.TabIndex = 1;
			this.button_control_select.Text = "Select.req";
			this.button_control_select.UseVisualStyleBackColor = true;
			this.button_control_select.Click += new System.EventHandler(button_control_select_Click);
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(8, 79);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(980, 127);
			this.textBox_code.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Secs gem HSMS";
			this.userControlHead1.Size = new System.Drawing.Size(1011, 32);
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.sendMessageToolStripMenuItem, this.addNewSecsItemToolStripMenuItem, this.editSecsItemToolStripMenuItem, this.deleteSecsItemToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(179, 92);
			this.sendMessageToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.Cloud_16xLG;
			this.sendMessageToolStripMenuItem.Name = "sendMessageToolStripMenuItem";
			this.sendMessageToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
			this.sendMessageToolStripMenuItem.Text = "Send Message";
			this.sendMessageToolStripMenuItem.Click += new System.EventHandler(sendMessageToolStripMenuItem_Click);
			this.addNewSecsItemToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_add_16xLG;
			this.addNewSecsItemToolStripMenuItem.Name = "addNewSecsItemToolStripMenuItem";
			this.addNewSecsItemToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
			this.addNewSecsItemToolStripMenuItem.Text = "AddNewSecsItem";
			this.addNewSecsItemToolStripMenuItem.Click += new System.EventHandler(addNewSecsItemToolStripMenuItem_Click);
			this.editSecsItemToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.sig;
			this.editSecsItemToolStripMenuItem.Name = "editSecsItemToolStripMenuItem";
			this.editSecsItemToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
			this.editSecsItemToolStripMenuItem.Text = "EditSecsItem";
			this.editSecsItemToolStripMenuItem.Click += new System.EventHandler(editSecsItemToolStripMenuItem_Click);
			this.deleteSecsItemToolStripMenuItem.Image = HslCommunicationDemo.Properties.Resources.action_Cancel_16xLG;
			this.deleteSecsItemToolStripMenuItem.Name = "deleteSecsItemToolStripMenuItem";
			this.deleteSecsItemToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
			this.deleteSecsItemToolStripMenuItem.Text = "DeleteSecsItem";
			this.deleteSecsItemToolStripMenuItem.Click += new System.EventHandler(deleteSecsItemToolStripMenuItem_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1011, 681);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormSecsGem";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Secs/Gem访问Demo";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.tabPage_s2.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.tabControl_buttom.ResumeLayout(false);
			this.tabPage_log.ResumeLayout(false);
			this.tabPage_log.PerformLayout();
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel1.PerformLayout();
			this.splitContainer2.Panel2.ResumeLayout(false);
			this.splitContainer2.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer2).EndInit();
			this.splitContainer2.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
