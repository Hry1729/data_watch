using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core.Net;
using HslCommunication.Secs;
using HslCommunication.Secs.Types;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.PLC.Secs
{
	public class FormSecsHsmsServer : HslFormContent
	{
		private SecsHsmsServer server;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private Timer timer;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel1;

		private Button button11;

		private Button button1;

		private TextBox textBox_port;

		private Label label3;

		private GroupBox groupBox1;

		private TextBox textBox_data_back;

		private TreeView treeView1;

		private Button button_save_tree;

		private Label label1;

		private TextBox textBox_stream;

		private TextBox textBox_function;

		private Button button25;

		private Label label12;

		private Label D;

		private ComboBox comboBox1;

		private Label label5;

		private SplitContainer splitContainer1;

		private Panel panel3;

		private Panel panel2;

		private ListBox listBox1;

		private Label label2;

		private Label label4;

		private Label label7;

		private Label label8;

		private Button button_device_save;

		private Button button_device_send;

		private TextBox textBox_device_f;

		private TextBox textBox_device_s;

		private TextBox textBox_device_send;

		private Label label6;

		private CheckBox checkBox_device_w;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TextBox textBox_device_id;

		private Label label9;

		private TextBox textBox_online_max;

		private Label label10;

		private ContextMenuStrip contextMenuStrip1;

		private ToolStripMenuItem addNewSecsItemToolStripMenuItem;

		private ToolStripMenuItem editSecsItemToolStripMenuItem;

		private ToolStripMenuItem deleteSecsItemToolStripMenuItem;

		private Panel panel4;

		private ComboBox comboBox_log_style;

		private Button button2;

		private Label label11;

		private CheckBox checkBox_stop;

		private CheckBox checkBox_show_send_message;

		private RichTextBox richTextBox_main;

		private Button button_device_read;

		public FormSecsHsmsServer()
		{
			InitializeComponent();
			comboBox_log_style.SelectedIndex = 0;
		}

		private void FormSecsHsmsServer_Load(object sender, EventArgs e)
		{
			comboBox1.SelectedIndex = 1;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(FormSecsGem.GetSecsAddress());
			DemoUtils.AddSpecialFunctionTab(tabControl1, addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
			TreeNode treeNode = new TreeNode("S1");
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 1, true, new SecsValue(new object[2] { "MDLN", "SOFTRev" }), "Are You Online"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 3, true, new SecsValue(new object[2] { 1u, 2u }), "Selected Equipment Status"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 5, true, new SecsValue(new object[2] { 1u, 2u }), "Formatted Status"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 7, false, new SecsValue(new object[1] { new object[2] { "AlarmsEnabled", "0" } }), "Fixed Form Request"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 9, true, new SecsValue(new object[2]
			{
				new byte[1] { 1 },
				new byte[1] { 1 }
			}), "Material Transfer Status"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 11, true, new SecsValue(new object[1] { new object[3] { "SVID", "SVNAME", "UNITS" } }), "Status Variable Namelist"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 13, true, new SecsValue(new object[2]
			{
				new byte[1] { 1 },
				new object[2] { "gemsim", "1.0" }
			}), "Establish Communications", new SecsValue(new object[2] { "gemsim", "1.0" })));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 15, true, new SecsValue(new byte[1] { 1 }), "Request OFF-LINE"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 17, true, new SecsValue(new byte[1] { 1 }), "Request ON-LINE"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 19, true, new SecsValue(new object[2]
			{
				new object[1] { new object[2] { "ATTRDATA1", "ATTRDATA2" } },
				new object[1] { new object[2] { 4u, "ERRTEXT" } }
			}), "Get Attribute", new SecsValue(new object[3]
			{
				"A Carrier",
				new object[1] { "Job0001" },
				new object[1] { "SourceURL" }
			})));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 21, true, new SecsValue(new object[1] { new object[3] { "VID", "DVVALNAME", "Units" } }), "Data Variable Namelist"));
			AddTree(treeNode, new FormSecsGem.SecsTreeItem(1, 23, true, new SecsValue(new object[1] { new object[3]
			{
				123u,
				"ProcessStateUpdate",
				new object[1] { "810" }
			} }), "Collection Event Namelist Request"));
			treeView1.Nodes.Add(treeNode);
			TreeNode treeNode2 = new TreeNode("S2");
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 1, false, new SecsValue(new byte[2] { 1, 2 }), "Service Program Load Inquire", new SecsValue(new object[2] { "bin007", 322u })));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 3, false, new SecsValue(199u), "Service Program Send", new SecsValue(new byte[3] { 1, 2, 3 })));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 5, false, new SecsValue(new byte[2] { 1, 2 }), "Service Program Load Request", new SecsValue(new byte[3] { 1, 2, 3 })));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 7, false, new SecsValue(0u), "Service Program Run Send", new SecsValue("bin007")));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 9, false, new SecsValue("shutdown -i5 -g0 -y"), "Service Program Results Request", new SecsValue("bin007")));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 11, false, new SecsValue("bin007"), "Service Program Directory Request"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 13, true, new SecsValue(new object[2] { "1", "2" }), "Equipment Constant Request"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 15, true, new SecsValue(new byte[1]), "New Equipment Constant Send"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 17, true, new SecsValue("2022121708371902"), "Date and Time Request"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 19, true, new SecsValue((ushort)0), "Reset/Initialize Send"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 21, true, new SecsValue(new byte[1]), "Remote Command Send"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 23, true, new SecsValue(new byte[1]), "Trace Initialize Send"));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 25, true, new SecsValue(new byte[18]
			{
				0, 1, 3, 3, 10, 13, 27, 93, 24, 24,
				24, 26, 4, 19, 127, 128, 254, 255
			}), "Trace Initialize Send", new SecsValue(new byte[18]
			{
				0, 1, 3, 3, 10, 13, 27, 93, 24, 24,
				24, 26, 4, 19, 127, 128, 254, 255
			})));
			AddTree(treeNode2, new FormSecsGem.SecsTreeItem(2, 27, true, new SecsValue(new byte[1]), "Initiate Processing Request"));
			treeView1.Nodes.Add(treeNode2);
			TreeNode treeNode3 = new TreeNode("S3");
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 1, true, new SecsValue(new object[2]
			{
				new byte[1],
				new object[1] { new object[3]
				{
					new byte[1],
					new byte[1] { 24 },
					"ee052793.1"
				} }
			}), "Material Status Request"));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 3, true, new SecsValue(new object[2]
			{
				new byte[1],
				new object[1] { new object[3]
				{
					286u,
					new byte[1] { 24 },
					"ee052793.1"
				} }
			}), "Time to Completion Data"));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 5, true, SecsValue.EmptySecsValue(), "Time to Completion Data", new SecsValue(new object[2]
			{
				new byte[1] { 1 },
				new byte[1] { 24 }
			})));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 7, true, SecsValue.EmptySecsValue(), "Material Lost Send", new SecsValue(new object[3]
			{
				new byte[1] { 1 },
				new byte[1] { 24 },
				"ee052793.1"
			})));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 9, true, SecsValue.EmptySecsValue(), "Matl ID Equate Send", new SecsValue(new object[2] { "ee052793.1", "1" })));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 11, true, SecsValue.EmptySecsValue(), "Matl ID Request", new SecsValue((byte)1)));
			AddTree(treeNode3, new FormSecsGem.SecsTreeItem(3, 13, true, new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			}), "Matl ID Send"));
			treeView1.Nodes.Add(treeNode3);
			TreeNode treeNode4 = new TreeNode("S4");
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 1, true, new SecsValue(new byte[1]), "Ready to Send Materials", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 3, false, SecsValue.EmptySecsValue(), "Send Material", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 5, false, SecsValue.EmptySecsValue(), "Handshake Complete", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 7, false, SecsValue.EmptySecsValue(), "Not Ready to Send", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 9, false, SecsValue.EmptySecsValue(), "Stuck in Sender", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 11, false, SecsValue.EmptySecsValue(), "Stuck in Receiver", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 13, false, SecsValue.EmptySecsValue(), "Send Incomplete Timeout", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 15, false, SecsValue.EmptySecsValue(), "Material Received", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			AddTree(treeNode4, new FormSecsGem.SecsTreeItem(4, 17, true, new SecsValue(new byte[1]), "Ready to Send Materials", new SecsValue(new object[2]
			{
				(byte)1,
				"ee052793.1"
			})));
			treeView1.Nodes.Add(treeNode4);
			TreeNode treeNode5 = new TreeNode("S5");
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 1, true, SecsValue.EmptySecsValue(), "Alarm Report Send", new SecsValue(new object[3]
			{
				new byte[1],
				1000u,
				"sensor timeout at load elevator"
			})));
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 3, true, new SecsValue(new object[1] { (byte)0 }), "Enable/Disable Alarm Send"));
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 5, true, new SecsValue(new object[1] { new object[3]
			{
				(byte)0,
				1000u,
				"sensor timeout at load elevator"
			} }), "List Alarms Request"));
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 7, true, new SecsValue(new object[1] { new object[3]
			{
				(byte)0,
				1000u,
				"sensor timeout at load elevator"
			} }), "List Enabled Alarm Request"));
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 9, true, SecsValue.EmptySecsValue(), "Exception Post Notify", new SecsValue(new object[5]
			{
				DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss").Replace("-", ""),
				"out of ink",
				"ALARM",
				"ink not sensed at nozzle inlet",
				new object[1] { "manually insert new ink cartridge" }
			})));
			AddTree(treeNode5, new FormSecsGem.SecsTreeItem(5, 11, true, SecsValue.EmptySecsValue(), "Exception Clear Notify", new SecsValue(new object[4]
			{
				DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss").Replace("-", ""),
				"out of ink",
				"ALARM",
				"ink not sensed at nozzle inlet"
			})));
			treeView1.Nodes.Add(treeNode5);
			TreeNode treeNode6 = new TreeNode("S6");
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 1, true, SecsValue.EmptySecsValue(), "Trace Data Send", new SecsValue(new object[4]
			{
				"1",
				10u,
				DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss").Replace("-", ""),
				new object[1] { (byte)65 }
			})));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 3, true, SecsValue.EmptySecsValue(), "Discrete Variable Data Send", new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[1] { new object[2]
				{
					"12",
					new object[1] { new object[2] { 10u, "54" } }
				} }
			})));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 5, true, SecsValue.EmptySecsValue(), "Multi-block Data Send Inquire", new SecsValue(new object[2] { 1u, 649u })));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 7, true, new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[2]
				{
					"12",
					new object[1] { new object[2] { 10u, "54" } }
				}
			}), "Data Transfer Request"));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 9, true, SecsValue.EmptySecsValue(), "Formatted Variable Send", new SecsValue(new object[4]
			{
				new byte[1] { 2 },
				1u,
				4050u,
				new object[1] { new object[2]
				{
					"12",
					new object[1] { "54" }
				} }
			})));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 11, true, SecsValue.EmptySecsValue(), "Event Report Send", new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[1] { new object[2]
				{
					1u,
					new object[1] { "0" }
				} }
			})));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 13, true, SecsValue.EmptySecsValue(), "Annotated Event Report Send", new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[1] { new object[2]
				{
					1u,
					new object[1] { new object[2] { "810", "0" } }
				} }
			})));
			AddTree(treeNode6, new FormSecsGem.SecsTreeItem(6, 15, true, new SecsValue(new object[3]
			{
				1u,
				4050u,
				new object[1] { new object[2]
				{
					1u,
					new object[1] { "0" }
				} }
			}), "Event Report Request"));
			treeView1.Nodes.Add(treeNode6);
			treeView1.AfterSelect += TreeView1_AfterSelect;
			timer = new Timer();
			timer.Tick += Timer_Tick;
			timer.Start();
			if (Program.Language == 2)
			{
				label3.Text = "Port:";
				button1.Text = "Start";
				button11.Text = "Close";
				button_device_save.Text = "Save";
				groupBox1.Text = "Function Area";
				button_device_send.Text = "Broadcast";
				button25.Text = "Broadcast";
				button_save_tree.Text = "Save Ack";
				tabPage1.Text = "Log";
			}
			else
			{
				addNewSecsItemToolStripMenuItem.Text = "新增Secs功能码";
				editSecsItemToolStripMenuItem.Text = "编辑Secs功能码";
				deleteSecsItemToolStripMenuItem.Text = "删除Secs功能码";
			}
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (server != null)
			{
				listBox1.DataSource = server.GetPipeSessions();
			}
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (server != null)
			{
				switch (comboBox1.SelectedIndex)
				{
				case 0:
					server.StringEncoding = Encoding.ASCII;
					break;
				case 1:
					server.StringEncoding = Encoding.Default;
					break;
				case 2:
					server.StringEncoding = Encoding.UTF8;
					break;
				case 3:
					server.StringEncoding = Encoding.Unicode;
					break;
				case 4:
					server.StringEncoding = Encoding.GetEncoding("gb2312");
					break;
				}
			}
		}

		private void RenderSelectedSecs(FormSecsGem.SecsTreeItem treeItem)
		{
			textBox_stream.Text = treeItem.S.ToString();
			textBox_function.Text = (treeItem.F + 1).ToString();
			textBox_device_s.Text = treeItem.S.ToString();
			textBox_device_f.Text = treeItem.F.ToString();
			if (treeItem.Value != null)
			{
				textBox_data_back.Text = treeItem.Value.ToXElement().ToString();
			}
			else
			{
				textBox_data_back.Text = string.Empty;
			}
			if (treeItem.ValueSingular != null)
			{
				textBox_device_send.Text = treeItem.ValueSingular.ToXElement().ToString();
			}
			else
			{
				textBox_device_send.Text = string.Empty;
			}
		}

		private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is FormSecsGem.SecsTreeItem treeItem)
			{
				RenderSelectedSecs(treeItem);
			}
		}

		public void AddTree(TreeNode treeNode, FormSecsGem.SecsTreeItem treeItem)
		{
			TreeNode treeNode2 = new TreeNode(treeItem.GetTreeNodeText());
			treeNode2.Tag = treeItem;
			treeNode.Nodes.Add(treeNode2);
		}

		private void FormSecsHsmsServer_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button11_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				server = new SecsHsmsServer();
				server.DeviceId = ushort.Parse(textBox_device_id.Text);
				server.OnSecsMessageReceived += Server_OnSecsMessageReceived;
				server.LogNet = base.LogNet;
				if (!string.IsNullOrEmpty(textBox_online_max.Text))
				{
					server.SessionsMax = Convert.ToUInt32(textBox_online_max.Text);
				}
				server.ServerStart(int.Parse(textBox_port.Text));
				ComboBox1_SelectedIndexChanged(comboBox1, e);
				codeExampleControl.SetCodeText("server", "", server, "StringEncoding", "SessionsMax");
				button1.Enabled = false;
				button11.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Start failed: " + ex.Message);
			}
		}

		private string getBlockText(int block)
		{
			switch (block)
			{
			case 1:
				return "select.req";
			case 2:
				return "select.rsp";
			case 5:
				return "linktest.req";
			case 6:
				return "linktest.rsp";
			case 9:
				return "separate.req";
			default:
				return $"Block={block}";
			}
		}

		private void Server_OnSecsMessageReceived(object sender, PipeSession session, SecsMessage message)
		{
			if (!checkBox_stop.Checked && message != null)
			{
				Invoke((Action)delegate
				{
					if (message.StreamNo == 0 && message.FunctionNo == 0)
					{
						AppendHeadText(-1, string.Format("{0} RECV: S{1}F{2}{3} {4} SystemBytes=", session, message.StreamNo, message.FunctionNo, message.W ? "W" : string.Empty, getBlockText(message.BlockNo)) + message.MessageID);
						SecsValue itemValues = message.GetItemValues();
						if (itemValues != null && itemValues.ItemType != SecsItemType.None)
						{
							AppendHeadText(0, message.GetItemValues());
						}
					}
					else
					{
						AppendHeadText(-1, string.Format("{0} RECV: S{1}F{2}{3} SystemBytes=", session, message.StreamNo, message.FunctionNo, message.W ? "W" : string.Empty) + message.MessageID);
						AppendHeadText(0, message.GetItemValues());
					}
					richTextBox_main.ScrollToCaret();
				});
			}
			SecsMessage secsMessage = null;
			foreach (TreeNode node in treeView1.Nodes)
			{
				foreach (TreeNode node2 in node.Nodes)
				{
					if (node2.Tag is FormSecsGem.SecsTreeItem secsTreeItem && secsTreeItem.S == message.StreamNo && secsTreeItem.F == message.FunctionNo)
					{
						server.SendByCommand(session, message, message.StreamNo, (byte)(message.FunctionNo + 1), secsTreeItem.Value);
						secsMessage = new SecsMessage();
						secsMessage.StringEncoding = server.StringEncoding;
						secsMessage.StreamNo = message.StreamNo;
						secsMessage.FunctionNo = (byte)(message.FunctionNo + 1);
						secsMessage.Data = ((secsTreeItem.Value == null) ? new byte[0] : secsTreeItem.Value.ToSourceBytes(server.StringEncoding));
						break;
					}
				}
			}
			if (!checkBox_stop.Checked && secsMessage != null)
			{
				Invoke((Action)delegate
				{
					AppendHeadText(-1, string.Format("{0} SEND:  S{1}F{2}{3}", session, secsMessage.StreamNo, secsMessage.FunctionNo, secsMessage.W ? "W" : string.Empty));
					AppendHeadText(1, secsMessage.GetItemValues());
					richTextBox_main.ScrollToCaret();
				});
			}
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

		private string GetSecsValue(SecsValue secsValue)
		{
			if (secsValue == null)
			{
				secsValue = SecsValue.EmptySecsValue();
			}
			return (comboBox_log_style.SelectedIndex == 0) ? secsValue.ToString() : ((comboBox_log_style.SelectedIndex == 1) ? secsValue.ToSourceCode(true) : ((comboBox_log_style.SelectedIndex == 2) ? secsValue.ToSourceCode() : secsValue.ToSMLString()));
		}

		private void AppendHeadText(int code, SecsValue secsValue)
		{
			RenderMessage(code, GetSecsValue(secsValue));
		}

		private void button_save_tree_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is FormSecsGem.SecsTreeItem secsTreeItem)
			{
				secsTreeItem.Value = new SecsValue(XElement.Parse(textBox_data_back.Text));
				DemoUtils.ShowMessage("保存成功！");
			}
		}

		private void button25_Click(object sender, EventArgs e)
		{
			SecsValue secsValue = (string.IsNullOrEmpty(textBox_data_back.Text) ? null : new SecsValue(XElement.Parse(textBox_data_back.Text)));
			OperateResult operateResult = server.PublishSecsMessage(byte.Parse(textBox_stream.Text), byte.Parse(textBox_function.Text), secsValue);
			if (operateResult.IsSuccess)
			{
				AppendHeadText(-1, "SEND:  S" + textBox_stream.Text + "F" + textBox_function.Text + (checkBox_device_w.Checked ? "W" : string.Empty));
				AppendHeadText(1, secsValue);
				richTextBox_main.ScrollToCaret();
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.Message);
			}
			string text = ((secsValue == null) ? "null" : secsValue.ToSourceCode());
			codeExampleControl.ReaderReadCode("OperateResult publish = @deviceName.PublishSecsMessage( " + textBox_stream.Text + ", " + textBox_function.Text + ", " + text + " );");
		}

		private void button_device_send_Click(object sender, EventArgs e)
		{
			SecsValue secsValue = (string.IsNullOrEmpty(textBox_device_send.Text) ? null : new SecsValue(XElement.Parse(textBox_device_send.Text)));
			OperateResult operateResult = server.PublishSecsMessage(byte.Parse(textBox_device_s.Text), byte.Parse(textBox_device_f.Text), secsValue, checkBox_device_w.Checked);
			if (operateResult.IsSuccess)
			{
				AppendHeadText(-1, "SEND:  S" + textBox_device_s.Text + "F" + textBox_device_f.Text);
				AppendHeadText(1, secsValue);
				richTextBox_main.ScrollToCaret();
			}
			else
			{
				DemoUtils.ShowMessage("Failed: " + operateResult.Message);
			}
			string text = ((secsValue == null) ? "null" : secsValue.ToSourceCode());
			codeExampleControl.ReaderReadCode("OperateResult publish = @deviceName.PublishSecsMessage( " + textBox_device_s.Text + ", " + textBox_device_f.Text + ", " + text + ", " + checkBox_device_w.Checked.ToString().ToLower() + " );");
		}

		private void button_device_read_Click(object sender, EventArgs e)
		{
			SecsValue secsValue = (string.IsNullOrEmpty(textBox_device_send.Text) ? null : new SecsValue(XElement.Parse(textBox_device_send.Text)));
			PipeSession[] pipeSessions = server.GetPipeSessions();
			if (pipeSessions == null || pipeSessions.Length == 0)
			{
				DemoUtils.ShowMessage("没有连接的客户端！");
				return;
			}
			AppendHeadText(-1, "SEND:  S" + textBox_device_s.Text + "F" + textBox_device_f.Text);
			AppendHeadText(1, secsValue);
			OperateResult<SecsMessage> operateResult = server.ReadSecsMessage(pipeSessions[0], byte.Parse(textBox_device_s.Text), byte.Parse(textBox_device_f.Text), secsValue, checkBox_device_w.Checked);
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("Read Result: " + GetSecsValue(operateResult.Content.GetItemValues()));
			}
			else
			{
				DemoUtils.ShowMessage("Read Failed: " + operateResult.Message);
			}
		}

		private void button11_Click(object sender, EventArgs e)
		{
			server.ServerClose();
			button1.Enabled = true;
			button11.Enabled = false;
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

		private void editSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.EditSecsItemToolStripMenuItem_Click(treeView1, RenderSelectedSecs, true);
		}

		private void deleteSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.DeleteSecsItemToolStripMenuItem_Click(treeView1);
		}

		private void addNewSecsItemToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SecsHelper.AddNewSecsItemToolStripMenuItem_Click(treeView1, RenderSelectedSecs, true);
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox_port.Text);
			element.SetAttributeValue("SessionsMax", textBox_online_max.Text);
			element.SetAttributeValue("DeviceId", textBox_device_id.Text);
			element.SetAttributeValue("Encoding", comboBox1.SelectedIndex);
			element.RemoveNodes();
			for (int i = 0; i < treeView1.Nodes.Count; i++)
			{
				TreeNode treeNode = treeView1.Nodes[i];
				XElement xElement = new XElement(treeNode.Text);
				for (int j = 0; j < treeNode.Nodes.Count; j++)
				{
					if (treeNode.Nodes[j].Tag is FormSecsGem.SecsTreeItem secsTreeItem)
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
			textBox_port.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox_online_max.Text = HslFormContent.GetXmlValue(element, "SessionsMax", "", (string m) => m);
			textBox_device_id.Text = HslFormContent.GetXmlValue(element, "DeviceId", "1", (string m) => m);
			comboBox1.SelectedIndex = HslFormContent.GetXmlValue(element, "Encoding", 1, int.Parse);
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
					FormSecsGem.SecsTreeItem secsTreeItem = new FormSecsGem.SecsTreeItem(item2);
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox_online_max = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_device_id = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.button11 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.richTextBox_main = new System.Windows.Forms.RichTextBox();
			this.comboBox_log_style = new System.Windows.Forms.ComboBox();
			this.button2 = new System.Windows.Forms.Button();
			this.label11 = new System.Windows.Forms.Label();
			this.checkBox_stop = new System.Windows.Forms.CheckBox();
			this.checkBox_show_send_message = new System.Windows.Forms.CheckBox();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panel3 = new System.Windows.Forms.Panel();
			this.button_device_read = new System.Windows.Forms.Button();
			this.checkBox_device_w = new System.Windows.Forms.CheckBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.button_device_save = new System.Windows.Forms.Button();
			this.button_device_send = new System.Windows.Forms.Button();
			this.textBox_device_f = new System.Windows.Forms.TextBox();
			this.textBox_device_s = new System.Windows.Forms.TextBox();
			this.textBox_device_send = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox_data_back = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.D = new System.Windows.Forms.Label();
			this.button_save_tree = new System.Windows.Forms.Button();
			this.button25 = new System.Windows.Forms.Button();
			this.textBox_function = new System.Windows.Forms.TextBox();
			this.textBox_stream = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.panel4 = new System.Windows.Forms.Panel();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.addNewSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.editSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.deleteSecsItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.panel1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.panel4.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/8974215.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Secs gem HSMS Server";
			this.userControlHead1.Size = new System.Drawing.Size(1046, 32);
			this.userControlHead1.TabIndex = 3;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_online_max);
			this.panel1.Controls.Add(this.label10);
			this.panel1.Controls.Add(this.textBox_device_id);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.button11);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1040, 41);
			this.panel1.TabIndex = 4;
			this.textBox_online_max.Location = new System.Drawing.Point(423, 8);
			this.textBox_online_max.Name = "textBox_online_max";
			this.textBox_online_max.Size = new System.Drawing.Size(57, 23);
			this.textBox_online_max.TabIndex = 35;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(329, 11);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(95, 17);
			this.label10.TabIndex = 34;
			this.label10.Text = "SessionsMax：";
			this.textBox_device_id.Location = new System.Drawing.Point(576, 8);
			this.textBox_device_id.Name = "textBox_device_id";
			this.textBox_device_id.Size = new System.Drawing.Size(57, 23);
			this.textBox_device_id.TabIndex = 33;
			this.textBox_device_id.Text = "1";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(498, 11);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(75, 17);
			this.label9.TabIndex = 32;
			this.label9.Text = "Device ID：";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[5] { "ASCII", "Default", "UTF8", "Unicode", "GB2312" });
			this.comboBox1.Location = new System.Drawing.Point(722, 6);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(104, 25);
			this.comboBox1.TabIndex = 31;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(654, 11);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(63, 17);
			this.label5.TabIndex = 30;
			this.label5.Text = "Encode：";
			this.button11.Enabled = false;
			this.button11.Location = new System.Drawing.Point(235, 5);
			this.button11.Name = "button11";
			this.button11.Size = new System.Drawing.Size(83, 28);
			this.button11.TabIndex = 28;
			this.button11.Text = "关闭服务";
			this.button11.UseVisualStyleBackColor = true;
			this.button11.Click += new System.EventHandler(button11_Click);
			this.button1.Location = new System.Drawing.Point(145, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(83, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_port.Location = new System.Drawing.Point(74, 8);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.Size = new System.Drawing.Size(65, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "5000";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(9, 11);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.listBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.listBox1.BackColor = System.Drawing.Color.LightGray;
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 17;
			this.listBox1.Location = new System.Drawing.Point(754, 25);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(269, 208);
			this.listBox1.TabIndex = 34;
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(753, 5);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(93, 17);
			this.label2.TabIndex = 33;
			this.label2.Text = "Online Client：";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(937, 5);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(86, 17);
			this.label4.TabIndex = 32;
			this.label4.Text = "Online Count:";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.richTextBox_main);
			this.groupBox1.Controls.Add(this.comboBox_log_style);
			this.groupBox1.Controls.Add(this.button2);
			this.groupBox1.Controls.Add(this.label11);
			this.groupBox1.Controls.Add(this.checkBox_stop);
			this.groupBox1.Controls.Add(this.checkBox_show_send_message);
			this.groupBox1.Controls.Add(this.treeView1);
			this.groupBox1.Location = new System.Drawing.Point(3, 79);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(1040, 319);
			this.groupBox1.TabIndex = 6;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "功能定义区";
			this.richTextBox_main.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.richTextBox_main.Location = new System.Drawing.Point(324, 39);
			this.richTextBox_main.Name = "richTextBox_main";
			this.richTextBox_main.Size = new System.Drawing.Size(709, 274);
			this.richTextBox_main.TabIndex = 45;
			this.richTextBox_main.Text = "";
			this.comboBox_log_style.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_log_style.FormattingEnabled = true;
			this.comboBox_log_style.Items.AddRange(new object[4] { "XML", "Code", "Code-lite", "SML" });
			this.comboBox_log_style.Location = new System.Drawing.Point(442, 11);
			this.comboBox_log_style.Name = "comboBox_log_style";
			this.comboBox_log_style.Size = new System.Drawing.Size(101, 25);
			this.comboBox_log_style.TabIndex = 44;
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button2.Location = new System.Drawing.Point(924, 9);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(55, 28);
			this.button2.TabIndex = 42;
			this.button2.Text = "clear";
			this.button2.UseVisualStyleBackColor = true;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(330, 15);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(106, 17);
			this.label11.TabIndex = 40;
			this.label11.Text = "SECS Log Area：";
			this.checkBox_stop.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox_stop.AutoSize = true;
			this.checkBox_stop.Location = new System.Drawing.Point(838, 14);
			this.checkBox_stop.Name = "checkBox_stop";
			this.checkBox_stop.Size = new System.Drawing.Size(58, 21);
			this.checkBox_stop.TabIndex = 41;
			this.checkBox_stop.Text = "STOP";
			this.checkBox_stop.UseVisualStyleBackColor = true;
			this.checkBox_show_send_message.AutoSize = true;
			this.checkBox_show_send_message.Checked = true;
			this.checkBox_show_send_message.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_show_send_message.Location = new System.Drawing.Point(607, 14);
			this.checkBox_show_send_message.Name = "checkBox_show_send_message";
			this.checkBox_show_send_message.Size = new System.Drawing.Size(121, 21);
			this.checkBox_show_send_message.TabIndex = 43;
			this.checkBox_show_send_message.Text = "Show Send Msg";
			this.checkBox_show_send_message.UseVisualStyleBackColor = true;
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(7, 19);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(311, 295);
			this.treeView1.TabIndex = 36;
			this.treeView1.MouseDown += new System.Windows.Forms.MouseEventHandler(treeView1_MouseDown);
			this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer1.Location = new System.Drawing.Point(3, 5);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.panel3);
			this.splitContainer1.Panel2.Controls.Add(this.panel2);
			this.splitContainer1.Size = new System.Drawing.Size(745, 220);
			this.splitContainer1.SplitterDistance = 346;
			this.splitContainer1.TabIndex = 39;
			this.panel3.Controls.Add(this.button_device_read);
			this.panel3.Controls.Add(this.checkBox_device_w);
			this.panel3.Controls.Add(this.label7);
			this.panel3.Controls.Add(this.label8);
			this.panel3.Controls.Add(this.button_device_save);
			this.panel3.Controls.Add(this.button_device_send);
			this.panel3.Controls.Add(this.textBox_device_f);
			this.panel3.Controls.Add(this.textBox_device_s);
			this.panel3.Controls.Add(this.textBox_device_send);
			this.panel3.Controls.Add(this.label6);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel3.Location = new System.Drawing.Point(0, 0);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(346, 220);
			this.panel3.TabIndex = 1;
			this.button_device_read.Location = new System.Drawing.Point(271, 4);
			this.button_device_read.Name = "button_device_read";
			this.button_device_read.Size = new System.Drawing.Size(52, 28);
			this.button_device_read.TabIndex = 48;
			this.button_device_read.Text = "应答";
			this.button_device_read.UseVisualStyleBackColor = true;
			this.button_device_read.Click += new System.EventHandler(button_device_read_Click);
			this.checkBox_device_w.AutoSize = true;
			this.checkBox_device_w.Location = new System.Drawing.Point(119, 8);
			this.checkBox_device_w.Name = "checkBox_device_w";
			this.checkBox_device_w.Size = new System.Drawing.Size(45, 21);
			this.checkBox_device_w.TabIndex = 47;
			this.checkBox_device_w.Text = "W?";
			this.checkBox_device_w.UseVisualStyleBackColor = true;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(4, 9);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(27, 17);
			this.label7.TabIndex = 41;
			this.label7.Text = "S：";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(59, 9);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(26, 17);
			this.label8.TabIndex = 43;
			this.label8.Text = "F：";
			this.button_device_save.Location = new System.Drawing.Point(165, 4);
			this.button_device_save.Name = "button_device_save";
			this.button_device_save.Size = new System.Drawing.Size(49, 28);
			this.button_device_save.TabIndex = 46;
			this.button_device_save.Text = "保存";
			this.button_device_save.UseVisualStyleBackColor = true;
			this.button_device_send.Location = new System.Drawing.Point(217, 4);
			this.button_device_send.Name = "button_device_send";
			this.button_device_send.Size = new System.Drawing.Size(51, 28);
			this.button_device_send.TabIndex = 45;
			this.button_device_send.Text = "广播";
			this.button_device_send.UseVisualStyleBackColor = true;
			this.button_device_send.Click += new System.EventHandler(button_device_send_Click);
			this.textBox_device_f.Location = new System.Drawing.Point(85, 6);
			this.textBox_device_f.Name = "textBox_device_f";
			this.textBox_device_f.Size = new System.Drawing.Size(28, 23);
			this.textBox_device_f.TabIndex = 44;
			this.textBox_device_f.Text = "1";
			this.textBox_device_s.Location = new System.Drawing.Point(27, 6);
			this.textBox_device_s.Name = "textBox_device_s";
			this.textBox_device_s.Size = new System.Drawing.Size(29, 23);
			this.textBox_device_s.TabIndex = 42;
			this.textBox_device_s.Text = "1";
			this.textBox_device_send.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_device_send.Location = new System.Drawing.Point(4, 51);
			this.textBox_device_send.Multiline = true;
			this.textBox_device_send.Name = "textBox_device_send";
			this.textBox_device_send.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_device_send.Size = new System.Drawing.Size(340, 166);
			this.textBox_device_send.TabIndex = 40;
			this.label6.AutoSize = true;
			this.label6.ForeColor = System.Drawing.Color.Gray;
			this.label6.Location = new System.Drawing.Point(5, 31);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(127, 17);
			this.label6.TabIndex = 39;
			this.label6.Text = "Data(用于主动发送)：";
			this.panel2.Controls.Add(this.textBox_data_back);
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.D);
			this.panel2.Controls.Add(this.button_save_tree);
			this.panel2.Controls.Add(this.button25);
			this.panel2.Controls.Add(this.textBox_function);
			this.panel2.Controls.Add(this.textBox_stream);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(395, 220);
			this.panel2.TabIndex = 0;
			this.textBox_data_back.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_data_back.Location = new System.Drawing.Point(4, 51);
			this.textBox_data_back.Multiline = true;
			this.textBox_data_back.Name = "textBox_data_back";
			this.textBox_data_back.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_data_back.Size = new System.Drawing.Size(383, 166);
			this.textBox_data_back.TabIndex = 38;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(61, 17);
			this.label1.TabIndex = 27;
			this.label1.Text = "Stream：";
			this.D.AutoSize = true;
			this.D.ForeColor = System.Drawing.Color.Gray;
			this.D.Location = new System.Drawing.Point(3, 31);
			this.D.Name = "D";
			this.D.Size = new System.Drawing.Size(103, 17);
			this.D.TabIndex = 33;
			this.D.Text = "Data(用于应答)：";
			this.button_save_tree.Location = new System.Drawing.Point(202, 3);
			this.button_save_tree.Name = "button_save_tree";
			this.button_save_tree.Size = new System.Drawing.Size(73, 28);
			this.button_save_tree.TabIndex = 35;
			this.button_save_tree.Text = "保存应答";
			this.button_save_tree.UseVisualStyleBackColor = true;
			this.button_save_tree.Click += new System.EventHandler(button_save_tree_Click);
			this.button25.Location = new System.Drawing.Point(277, 3);
			this.button25.Name = "button25";
			this.button25.Size = new System.Drawing.Size(94, 28);
			this.button25.TabIndex = 31;
			this.button25.Text = "广播";
			this.button25.UseVisualStyleBackColor = true;
			this.button25.Click += new System.EventHandler(button25_Click);
			this.textBox_function.Location = new System.Drawing.Point(166, 5);
			this.textBox_function.Name = "textBox_function";
			this.textBox_function.Size = new System.Drawing.Size(32, 23);
			this.textBox_function.TabIndex = 30;
			this.textBox_function.Text = "1";
			this.textBox_stream.Location = new System.Drawing.Point(65, 5);
			this.textBox_stream.Name = "textBox_stream";
			this.textBox_stream.Size = new System.Drawing.Size(32, 23);
			this.textBox_stream.TabIndex = 28;
			this.textBox_stream.Text = "1";
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(98, 8);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(68, 17);
			this.label12.TabIndex = 29;
			this.label12.Text = "Function：";
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Location = new System.Drawing.Point(3, 404);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1040, 264);
			this.tabControl1.TabIndex = 7;
			this.tabPage1.Controls.Add(this.panel4);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(1032, 234);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "日志";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.panel4.Controls.Add(this.listBox1);
			this.panel4.Controls.Add(this.label2);
			this.panel4.Controls.Add(this.label4);
			this.panel4.Controls.Add(this.splitContainer1);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(3, 3);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(1026, 228);
			this.panel4.TabIndex = 35;
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.addNewSecsItemToolStripMenuItem, this.editSecsItemToolStripMenuItem, this.deleteSecsItemToolStripMenuItem });
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(179, 70);
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
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1046, 671);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormSecsHsmsServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormSecsHsmsServer";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSecsHsmsServer_FormClosing);
			base.Load += new System.EventHandler(FormSecsHsmsServer_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			this.contextMenuStrip1.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
