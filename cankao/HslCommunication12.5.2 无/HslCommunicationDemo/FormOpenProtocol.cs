using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Profinet.OpenProtocol;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormOpenProtocol : HslFormContent
	{
		private long sub_tick = 0L;

		private long read_tick = 0L;

		private OpenProtocolNet openProtocol = null;

		private CodeExampleControl codeExampleControl;

		private ToolTip toolTip;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel2;

		private GroupBox groupBox1;

		private Button button_read_string;

		private TextBox textBox_mid;

		private Label label6;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox_revision;

		private Label label2;

		private TextBox textBox_stationID;

		private Label label4;

		private TextBox textBox_spindleID;

		private Label label5;

		private TextBox textBox_dataField;

		private Label label8;

		private TreeView treeView1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private TextBox textBox_revison_connect;

		private Label label9;

		private TextBox textBox_read_content;

		private Label label10;

		private Label label7;

		private Label label_read_tick;

		private Label label16;

		private Label label_read_byteLength;

		private Label label14;

		private Label label_read_cost;

		private Label label12;

		private Label label_read_time;

		private CheckBox checkBox_sub_stop;

		private TextBox textBox_log;

		private Label label11;

		private CheckBox checkBox_sub_format;

		private Label label_subscribe_tick;

		private Label label15;

		private CheckBox checkBox1;

		private PipeSelectControl pipeSelectControl1;

		private CheckBox checkBox2;

		private TextBox textBox_sub_mid;

		private Label label_sub_mid;

		private TextBox textBox_code;

		private Label label1;

		public FormOpenProtocol()
		{
			InitializeComponent();
			toolTip = new ToolTip();
			string caption = "额外的mid数组，如果你得用于订阅返回的mid大于421，则需要在数组里额外指定，例如 1000,1010";
			if (Program.Language == 2)
			{
				caption = "Additional mid array, if you need to subscribe to the returned mid greater than 421, you need to specify it additionally in the array, such as 1000,1010";
			}
			toolTip.SetToolTip(label_sub_mid, caption);
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_revison_connect.Text, out var result))
			{
				DemoUtils.ShowMessage("revison input format wrong！");
				return;
			}
			openProtocol?.ConnectClose();
			openProtocol = new OpenProtocolNet();
			openProtocol.LogNet = base.LogNet;
			openProtocol.AutoAckControllerMessage = checkBox1.Checked;
			openProtocol.KeepAliveMessageEnable = checkBox2.Checked;
			openProtocol.RevisonOnConnected = result;
			openProtocol.OnReceivedOpenMessage += OpenProtocol_ReceivedMessage;
			try
			{
				if (!string.IsNullOrEmpty(textBox_sub_mid.Text))
				{
					openProtocol.ExtraSubscribeMID = textBox_sub_mid.Text.ToStringArray<int>();
				}
				pipeSelectControl1.IniPipe(openProtocol);
				OperateResult operateResult = DeviceConnectPLC(openProtocol);
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button2.Enabled = true;
					button1.Enabled = false;
					panel2.Enabled = true;
					List<string> list = new List<string>();
					list.Add("RevisonOnConnected");
					list.Add("AutoAckControllerMessage");
					list.Add("KeepAliveMessageEnable");
					if (!string.IsNullOrEmpty(textBox_sub_mid.Text))
					{
						list.Add("ExtraSubscribeMID");
					}
					codeExampleControl.SetCodeText("openProtocol", openProtocol, list.ToArray());
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				openProtocol?.ConnectClose();
				button2.Enabled = false;
				button1.Enabled = true;
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void OpenProtocol_ReceivedMessage(object sender, OpenEventArgs e)
		{
			Invoke((Action)delegate
			{
				Interlocked.Increment(ref sub_tick);
				label_subscribe_tick.Text = sub_tick.ToString();
				if (!checkBox_sub_stop.Checked)
				{
					if (checkBox_sub_format.Checked)
					{
						textBox_log.Text = DateTime.Now.ToString() + " :               Length: " + e.Content.Length + Environment.NewLine + Environment.NewLine + GetRenderOpenMessage(e.Content);
					}
					else
					{
						textBox_log.AppendText(DateTime.Now.ToString() + " : " + e.Content + Environment.NewLine);
					}
				}
			});
		}

		private void FormOpenProtocol_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			TreeNode treeNode = new TreeNode(" Communication messages");
			treeNode.Nodes.Add(new TreeNode("MID 0008 Data message subscription")
			{
				Tag = new OpenMessage(8, 1, -1, -1, new List<string> { "0900" })
			});
			treeNode.Nodes.Add(new TreeNode("MID 0009 Data message unsubscribe")
			{
				Tag = new OpenMessage(9, 1, -1, -1, new List<string> { "0900" })
			});
			treeView1.Nodes.Add(treeNode);
			TreeNode treeNode2 = new TreeNode("Parameter set messages");
			treeNode2.Nodes.Add(new TreeNode("MID 0010 Parameter set ID upload")
			{
				Tag = new OpenMessage(10, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0012 Parameter set data upload")
			{
				Tag = new OpenMessage(12, 1, -1, -1, new List<string> { "000" })
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0014 Parameter set selected subscribe")
			{
				Tag = new OpenMessage(14, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0017 Parameter set selected unsubscribe")
			{
				Tag = new OpenMessage(17, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0018 Select Parameter set")
			{
				Tag = new OpenMessage(18, 1, -1, -1, new List<string> { "000" })
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0019 Set Parameter set batch size")
			{
				Tag = new OpenMessage(19, 1, -1, -1, new List<string> { "000", "00" })
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0020 Reset Parameter set batch counter")
			{
				Tag = new OpenMessage(20, 1, -1, -1, new List<string> { "000" })
			});
			treeView1.Nodes.Add(treeNode2);
			TreeNode treeNode3 = new TreeNode("Job message");
			treeNode3.Nodes.Add(new TreeNode("MID 0030 Job ID upload")
			{
				Tag = new OpenMessage(30, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0032 Job data upload")
			{
				Tag = new OpenMessage(32, 1, -1, -1, new List<string> { "01" })
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0034 Job info subscribe")
			{
				Tag = new OpenMessage(34, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0037 Job info unsubscribe")
			{
				Tag = new OpenMessage(37, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0038 Select Job")
			{
				Tag = new OpenMessage(38, 1, -1, -1, new List<string> { "01" })
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0039 Job restart")
			{
				Tag = new OpenMessage(39, 1, -1, -1, new List<string> { "00" })
			});
			treeView1.Nodes.Add(treeNode3);
			TreeNode treeNode4 = new TreeNode("Tool messages");
			treeNode4.Nodes.Add(new TreeNode("MID 0040 Tool data upload")
			{
				Tag = new OpenMessage(40, 1, -1, -1, null)
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0042 Disable tool")
			{
				Tag = new OpenMessage(42, 1, -1, -1, null)
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0043 Enable tool")
			{
				Tag = new OpenMessage(43, 1, -1, -1, null)
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0044 Disconnect tool")
			{
				Tag = new OpenMessage(44, 1, -1, -1, null)
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0045 Set calibration value")
			{
				Tag = new OpenMessage(44, 1, -1, -1, new List<string> { "011", "02003550" })
			});
			treeView1.Nodes.Add(treeNode4);
			TreeNode treeNode5 = new TreeNode("VIN Messages");
			treeNode5.Nodes.Add(new TreeNode("MID 0050 Vehicle ID Number download")
			{
				Tag = new OpenMessage(50, 1, -1, -1, new List<string> { "0000000000000000000000000" })
			});
			treeNode5.Nodes.Add(new TreeNode("MID 0051 Vehicle ID Number subscribe")
			{
				Tag = new OpenMessage(51, 1, -1, -1, null)
			});
			treeNode5.Nodes.Add(new TreeNode("MID 0054 Vehicle ID Number unsubscrib")
			{
				Tag = new OpenMessage(54, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode5);
			TreeNode treeNode6 = new TreeNode("Tightening result messages");
			treeNode6.Nodes.Add(new TreeNode("MID 0060 Last tightening result data subscribe")
			{
				Tag = new OpenMessage(60, 1, -1, -1, null)
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0063 Last tightening result data unsubscribe")
			{
				Tag = new OpenMessage(63, 1, -1, -1, null)
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0064 Old tightening result upload")
			{
				Tag = new OpenMessage(64, 1, -1, -1, new List<string> { "0000000000" })
			});
			treeView1.Nodes.Add(treeNode6);
			TreeNode treeNode7 = new TreeNode("Alarm messages");
			treeNode7.Nodes.Add(new TreeNode("MID 0070 Alarm subscribe")
			{
				Tag = new OpenMessage(70, 1, -1, -1, null)
			});
			treeNode7.Nodes.Add(new TreeNode("MID 0073 Alarm unsubscribe")
			{
				Tag = new OpenMessage(73, 1, -1, -1, null)
			});
			treeNode7.Nodes.Add(new TreeNode("MID 0078 Acknowledge alarm remotely on controller")
			{
				Tag = new OpenMessage(78, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode7);
			TreeNode treeNode8 = new TreeNode("Time messages");
			treeNode8.Nodes.Add(new TreeNode("MID 0080 Read time upload")
			{
				Tag = new OpenMessage(80, 1, -1, -1, null)
			});
			treeNode8.Nodes.Add(new TreeNode("MID 0082 Set Time")
			{
				Tag = new OpenMessage(82, 1, -1, -1, new List<string> { DateTime.Now.ToString("yyyy-MM-dd:HH:mm:ss") })
			});
			treeView1.Nodes.Add(treeNode8);
			TreeNode treeNode9 = new TreeNode("Multi-spindle status messages");
			treeNode9.Nodes.Add(new TreeNode("MID 0090 Multi-spindle status subscribe")
			{
				Tag = new OpenMessage(90, 1, -1, -1, null)
			});
			treeNode9.Nodes.Add(new TreeNode("MID 0093 Multi-spindle status unsubscribe")
			{
				Tag = new OpenMessage(93, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode9);
			TreeNode treeNode10 = new TreeNode("Multi-spindle result messages");
			treeNode10.Nodes.Add(new TreeNode("MID 0100 Multi-spindle result subscribe")
			{
				Tag = new OpenMessage(100, 1, -1, -1, null)
			});
			treeNode10.Nodes.Add(new TreeNode("MID 0103 Multi spindle result unsubscribe")
			{
				Tag = new OpenMessage(103, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode10);
			TreeNode treeNode11 = new TreeNode("PowerMACS result data");
			treeNode11.Nodes.Add(new TreeNode("MID 0105 Last PowerMACS tightening result data subscribe")
			{
				Tag = new OpenMessage(105, 1, -1, -1, null)
			});
			treeNode11.Nodes.Add(new TreeNode("MID 0109 Last PowerMACS tightening result data unsubscribe")
			{
				Tag = new OpenMessage(109, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode11);
			treeView1.ExpandAll();
			treeView1.AfterSelect += TreeView1_AfterSelect;
			codeExampleControl = new CodeExampleControl();
			DemoUtils.AddSpecialFunctionTab(tabControl1, codeExampleControl, false, CodeExampleControl.GetTitle());
		}

		private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is OpenMessage openMessage)
			{
				textBox_mid.Text = openMessage.MID.ToString();
				textBox_revision.Text = openMessage.Revision.ToString();
				textBox_stationID.Text = openMessage.StationID.ToString();
				textBox_spindleID.Text = openMessage.SpindleID.ToString();
				if (openMessage.DataField != null)
				{
					textBox_dataField.Lines = openMessage.DataField.ToArray();
				}
				else
				{
					textBox_dataField.Text = string.Empty;
				}
			}
		}

		private string GetMIDText(int mid)
		{
			switch (mid)
			{
			case 1:
				return "Communication start";
			case 2:
				return "Communication start acknowledge";
			case 3:
				return "Communication stop";
			case 4:
				return "Command error";
			case 5:
				return "Command accepted";
			case 10:
				return "Parameter set ID upload request";
			case 11:
				return "Parameter set ID upload reply";
			case 12:
				return "Parameter set data upload request";
			case 13:
				return "Parameter set data upload reply";
			case 14:
				return "Parameter set selected subscribe";
			case 15:
				return "Parameter set selected";
			case 16:
				return "Parameter set selected acknowledge";
			case 17:
				return "Parameter set selected unsubscribe";
			case 18:
				return "Select Parameter set";
			case 19:
				return "Set Parameter set batch size";
			case 20:
				return "Reset Parameter set batch counter";
			case 30:
				return "Job ID upload request";
			case 31:
				return "Job ID upload reply";
			case 32:
				return "Job data upload request";
			case 33:
				return "Job data upload reply";
			case 34:
				return "Job info subscribe";
			case 35:
				return "Job info";
			case 36:
				return "Job info acknowledge";
			case 37:
				return "Job info unsubscribe";
			case 38:
				return "Select Job";
			case 39:
				return "Job restart";
			case 40:
				return "Tool data upload request";
			case 41:
				return "Tool data upload reply";
			case 42:
				return "Disable tool";
			case 43:
				return "Enable tool";
			case 44:
				return "Disconnect tool request";
			case 45:
				return "Set calibration value request";
			case 50:
				return "Vehicle ID Number download request";
			case 51:
				return "Vehicle ID Number subscribe";
			case 52:
				return "Vehicle ID Number";
			case 53:
				return "Vehicle ID Number acknowledge";
			case 54:
				return "Vehicle ID Number unsubscribe";
			case 60:
				return "Last tightening result data subscribe";
			case 61:
				return "Last tightening result data";
			case 62:
				return "Last tightening result data acknowledge";
			case 63:
				return "Last tightening result data unsubscribe";
			case 64:
				return "Old tightening result upload request";
			case 65:
				return "Old tightening result upload reply";
			case 70:
				return "Alarm subscribe";
			case 71:
				return "Alarm";
			case 72:
				return "Alarm acknowledge";
			case 73:
				return "Alarm unsubscribe";
			case 74:
				return "Alarm acknowledged on controller";
			case 75:
				return "Alarm acknowledged on controller acknowledge";
			case 76:
				return "Alarm status";
			case 77:
				return "Alarm status acknowledge";
			case 78:
				return "Acknowledge alarm remotely on controller";
			case 80:
				return "Read time upload request";
			case 81:
				return "Read time upload reply";
			case 82:
				return "Set Time";
			case 90:
				return "Multi-spindle status subscribe";
			case 91:
				return "Multi-spindle status";
			case 92:
				return "Multi-spindle status acknowledge";
			case 93:
				return "Multi-spindle status unsubscribe";
			case 100:
				return "Multi-spindle result subscribe";
			case 101:
				return "Multi-spindle result";
			case 102:
				return "Multi-spindle result acknowledge";
			case 103:
				return "Multi spindle result unsubscribe";
			default:
				return "Unknown";
			}
		}

		private void AddStringBuilder(StringBuilder sb, string content, bool withIndex = true, params string[] paras)
		{
			sb.Append("Name".PadRight(35, ' '));
			sb.Append("Index:Len".PadRight(15, ' '));
			sb.Append("Num".PadRight(10, ' '));
			sb.Append("Value");
			sb.AppendLine();
			for (int i = 0; i < paras.Length; i++)
			{
				int num = paras[i].IndexOf('[');
				if (num < 0)
				{
					break;
				}
				string text = string.Empty;
				if (!paras[i].EndsWith("]"))
				{
					int num2 = paras[i].LastIndexOf("]");
					if (num2 > 0)
					{
						text = paras[i].Substring(num2 + 1);
						paras[i] = paras[i].Substring(0, num2 + 1);
					}
				}
				string text2 = paras[i].Substring(0, num);
				MatchCollection matchCollection = Regex.Matches(paras[i].Substring(num), "[0-9]+");
				if (matchCollection.Count != 2)
				{
					int num3 = Convert.ToInt32(matchCollection[0].Value) - 1;
					if (num3 >= content.Length - 1)
					{
						break;
					}
					sb.Append(text2.PadRight(35, ' '));
					sb.Append($"[{num3}:N]".PadRight(15, ' '));
					if (matchCollection.Count == 1 && paras[i].Substring(num).EndsWith("N]"))
					{
						if (withIndex)
						{
							sb.Append(content.Substring(num3, 2).PadRight(10, ' '));
							sb.Append(content.Substring(num3 + 2));
						}
						else
						{
							sb.Append(" ".PadRight(10, ' '));
							sb.Append(content.Substring(num3));
						}
						if (sb[sb.Length - 1] == '\0')
						{
							sb.Remove(sb.Length - 1, 1);
						}
					}
					if (!string.IsNullOrEmpty(text))
					{
						sb.Append("  // " + text);
					}
				}
				else
				{
					int num4 = Convert.ToInt32(matchCollection[0].Value) - 1;
					int num5 = Convert.ToInt32(matchCollection[1].Value) - 1 - num4 + 1;
					if (num4 >= content.Length - 1)
					{
						break;
					}
					sb.Append(text2.PadRight(35, ' '));
					sb.Append($"[{num4}:{num5}]".PadRight(15, ' '));
					if (withIndex)
					{
						sb.Append(content.Substring(num4, 2).PadRight(10, ' '));
						sb.Append(content.Substring(num4 + 2, num5 - 2));
					}
					else
					{
						sb.Append(" ".PadRight(10, ' '));
						sb.Append(content.Substring(num4, num5));
					}
					if (!string.IsNullOrEmpty(text))
					{
						sb.Append("  // " + text);
					}
				}
				sb.Append(Environment.NewLine);
			}
		}

		private string GetRenderOpenMessage(string content)
		{
			int num = Convert.ToInt32(content.Substring(4, 4));
			int num2 = Convert.ToInt32(content.Substring(8, 3));
			StringBuilder stringBuilder = new StringBuilder("MID " + content.Substring(4, 4) + " : " + GetMIDText(num));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Revision " + content.Substring(8, 3) + "                       Ack flag: " + content.Substring(11, 1) + " " + ((content[11] == '0') ? "Ack needed" : ((content[11] == '1') ? "No ack needed" : string.Empty)));
			stringBuilder.AppendLine();
			if (num == 4)
			{
				stringBuilder.AppendLine("Command MID: " + content.Substring(20, 4) + "   Error: " + content.Substring(24, 2));
				stringBuilder.AppendLine("Message: " + OpenProtocolNet.GetErrorText(Convert.ToInt32(content.Substring(24, 2))));
			}
			else if (num == 5)
			{
				AddStringBuilder(stringBuilder, content, false, "MID number accepted[21-24]");
			}
			else if (num == 11)
			{
				AddStringBuilder(stringBuilder, content, false, "The number of parameter sets[21-23]", "The ID of each parameter set[24-N]");
			}
			else if (num == 13)
			{
				AddStringBuilder(stringBuilder, content, true, "Parameter set ID[21-25]", "Parameter set name[26-52]", "Rotation direction[53-55]1=CW;2=CCW", "Batch size[56-59]", "Torque min[60-67]", "Torque max[68-75]", "Torque final target[76-83]", "Angle min[84-90]", "Angle max[91-97]", "Final Angle Target[98-104]");
			}
			else if (num == 15)
			{
				AddStringBuilder(stringBuilder, content, false, "Parameter set ID[21-23]", "Date of last change[21-42]");
			}
			else if (num == 31 && num2 == 1)
			{
				AddStringBuilder(stringBuilder, content, false, "Number of Jobs[21-22]", "Job ID of each Job[23-N]");
			}
			else if (num == 31 && num2 == 2)
			{
				AddStringBuilder(stringBuilder, content, false, "Number of Jobs[21-24]", "Job ID of each Job[25-N]");
			}
			else if (num == 33)
			{
				AddStringBuilder(stringBuilder, content, true, "Job ID[21-24]", "Job name[25-51]", "Forced order[52-54]", "Max time for first tightening[55-60]", "Max time to complete Job[61-67]", "Job batch mode[68-70]", "Lock at Job done[71-73]", "Use line control[74-76]", "Repeat Job[77-79]", "Tool loosening[80-82]", "Reserved[83-85]", "Number of parameter sets[86-89]", "Job list[90-N]");
			}
			else if (num == 35 && num2 == 1)
			{
				AddStringBuilder(stringBuilder, content, true, "Job ID[21-24]", "Job status[25-27]", "Job batch mode[28-30]", "Job batch size[31-36]", "Job batch counter[37-42]", "Time stamp[43-63]");
			}
			else if (num == 41)
			{
				AddStringBuilder(stringBuilder, content, true, "Tool serial number[21-36]", "Tool number of tightening[37-48]", "Last calibration date[49-69]", "Controller serial number[70-81]", "Calibration value[82-89]", "Last service date[90-110]", "Tightenings since service[111-122]", "Tool type[123-126]", "Motor size[127-130]", "Open end data[131-135]", "Controller software version[136-156]");
			}
			else if (num == 52)
			{
				AddStringBuilder(stringBuilder, content, true, "VIN number[21-47]", "Identifier result part 2[48-74]", "Identifier result part 3[75-91]", "Identifier result part 4[92-128]");
			}
			else if (num == 61 && num2 == 1)
			{
				AddStringBuilder(stringBuilder, content, true, "Cell ID[21-26]", "Channel ID[27-30]", "Torque controller Name[31-57]", "VIN Number[58-84]", "Job ID[85-88]", "Parameter set ID[89-93]", "Batch size[94-99]", "Batch counter[100-105]", "Tightening Status[106-108]", "Torque status[109-111]", "Angle status[112-114]", "Torque Min limit[115-122]", "Torque Max limit[123-130]", "Torque final target[131-138]", "Torque[139-146]", "Angle Min[147-153]", "Angle Max[154-160]", "Final Angle Target[161-167]", "Angle[168-174]", "Time stamp[175-195]", "Date/time of last change[196-216]", "Batch status[217-219]", "Tightening ID[220-231]");
			}
			else if (num == 61 && num2 == 999)
			{
				AddStringBuilder(stringBuilder, content, false, "VIN Number[21-45]", "Job ID[46-47]", "Parameter set ID[48-50]", "Batch size[51-54]", "Batch counter[55-58]", "Batch status[59-59]", "Tightening status[60-60]", "Torque status[61-61]", "Angle status[62-62]", "Torque[63-68]", "Angle[69-73]", "Time stamp[74-92]", "Date/time of last change[93-111]", "Tightening ID[112-121]");
			}
			else if (num == 65 && num2 == 1)
			{
				AddStringBuilder(stringBuilder, content, true, "Tightening ID[21-32]", "VIN Number[33-59]", "Parameter set ID[60-64]", "Batch counter[65-70]", "Tightening Status[71-73]", "Torque status[74-76]", "Angle status[77-79]", "Torque[80-87]", "Angle[88-94]", "Time stamp[95-115]", "Batch status[116-118]");
			}
			else
			{
				switch (num)
				{
				case 71:
					AddStringBuilder(stringBuilder, content, true, "Error code[21-26]", "Controller ready status[27-29]", "Tool ready status[30-32]", "Time[33-53]");
					break;
				case 74:
					AddStringBuilder(stringBuilder, content, false, "Error code[21-24]");
					break;
				case 76:
					AddStringBuilder(stringBuilder, content, true, "Alarm status[21-23]", "Error code[24-29]", "Controller ready status[30-32]", "Tool ready status[33-35]", "Time[36-56]");
					break;
				case 81:
					AddStringBuilder(stringBuilder, content, false, "Time[21-39]");
					break;
				case 106:
					AddStringBuilder(stringBuilder, content, true, "Total no of messages[21-24]", "Message number[25-28]", "Data No System[29-40]", "Station No[41-44]", "Station Name[45-66]", "Time[67-87]", "Mode No[88-91]", "Mode Name[92-113]", "Simple status[114-116]", "PM Status[117-119]", "Wp. Id[120-161]", "Number of Bolts[162-165]");
					break;
				case 107:
					AddStringBuilder(stringBuilder, content, true, "Total no of messages[21-24]", "Message number[25-28]", "Data No System[29-40]", "Station No[41-44]", "Time[45-55]", "Bolt number[56-61]", "Bolt name[62-83]", "Program name[84-105]", "PM status[106-108]", "Errors[109-160]", "Customer error code[161-166]", "Number of Bolt results[167-170]");
					break;
				}
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Source Content: ======================================================");
			stringBuilder.Append(content);
			return stringBuilder.ToString();
		}

		private string getArrayString(string[] values)
		{
			if (values == null || values.Length == 0)
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < values.Length; i++)
			{
				stringBuilder.Append("\"");
				stringBuilder.Append(values[i]);
				stringBuilder.Append("\"");
				if (i < values.Length - 1)
				{
					stringBuilder.Append(",");
				}
			}
			return stringBuilder.ToString();
		}

		private void Button_read_string_Click(object sender, EventArgs e)
		{
			try
			{
				textBox_read_content.Text = string.Empty;
				textBox_read_content.Refresh();
				DateTime now = DateTime.Now;
				OperateResult<string> operateResult = openProtocol.ReadCustomer(int.Parse(textBox_mid.Text), int.Parse(textBox_revision.Text), int.Parse(textBox_stationID.Text), int.Parse(textBox_spindleID.Text), new List<string>(textBox_dataField.Lines));
				TimeSpan timeSpan = DateTime.Now - now;
				if (operateResult.IsSuccess)
				{
					label_read_time.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
					label_read_cost.Text = timeSpan.TotalMilliseconds.ToString("F0") + " ms";
					read_tick++;
					label_read_tick.Text = read_tick.ToString();
					label_read_byteLength.Text = operateResult.Content.Length.ToString();
					textBox_read_content.Text = GetRenderOpenMessage(operateResult.Content);
				}
				else
				{
					DemoUtils.ShowMessage("Read Failed :" + operateResult.Message);
				}
				textBox_code.Text = $"OperateResult<string> read = openProtocol.ReadCustomer( {int.Parse(textBox_mid.Text)}," + $"{int.Parse(textBox_revision.Text)}, {int.Parse(textBox_stationID.Text)}, {int.Parse(textBox_spindleID.Text)}, " + "new List<string>() {" + getArrayString(textBox_dataField.Lines) + "} );";
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue("RevisonConnect", textBox_revison_connect.Text);
			element.SetAttributeValue("AutoAckControllerMessage", checkBox1.Checked);
			element.SetAttributeValue("MID9999", checkBox2.Checked);
			element.SetAttributeValue("SubMID", textBox_sub_mid.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.TcpPipe);
			textBox_revison_connect.Text = HslFormContent.GetXmlValue(element, "RevisonConnect", "1", (string m) => m);
			checkBox1.Checked = HslFormContent.GetXmlValue(element, "AutoAckControllerMessage", checkBox1.Checked, bool.Parse);
			checkBox2.Checked = HslFormContent.GetXmlValue(element, "MID9999", true, bool.Parse);
			textBox_sub_mid.Text = HslFormContent.GetXmlValue(element, "SubMID", string.Empty, (string m) => m);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormOpenProtocol_FormClosing(object sender, FormClosingEventArgs e)
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2 = new System.Windows.Forms.Panel();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label_subscribe_tick = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.checkBox_sub_format = new System.Windows.Forms.CheckBox();
			this.checkBox_sub_stop = new System.Windows.Forms.CheckBox();
			this.textBox_log = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.label_read_tick = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label_read_byteLength = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label_read_cost = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label_read_time = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_read_content = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_revision = new System.Windows.Forms.TextBox();
			this.textBox_dataField = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_spindleID = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_stationID = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button_read_string = new System.Windows.Forms.Button();
			this.textBox_mid = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox_sub_mid = new System.Windows.Forms.TextBox();
			this.label_sub_mid = new System.Windows.Forms.Label();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox_revison_connect = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Open Protocol";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 3;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Location = new System.Drawing.Point(2, 98);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1001, 545);
			this.panel2.TabIndex = 5;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(999, 543);
			this.tabControl1.TabIndex = 2;
			this.tabPage1.Controls.Add(this.treeView1);
			this.tabPage1.Controls.Add(this.groupBox1);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(991, 513);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Basic Test";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(6, 6);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(309, 501);
			this.treeView1.TabIndex = 1;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.textBox_code);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.label_subscribe_tick);
			this.groupBox1.Controls.Add(this.label15);
			this.groupBox1.Controls.Add(this.checkBox_sub_format);
			this.groupBox1.Controls.Add(this.checkBox_sub_stop);
			this.groupBox1.Controls.Add(this.textBox_log);
			this.groupBox1.Controls.Add(this.label11);
			this.groupBox1.Controls.Add(this.label_read_tick);
			this.groupBox1.Controls.Add(this.label16);
			this.groupBox1.Controls.Add(this.label_read_byteLength);
			this.groupBox1.Controls.Add(this.label14);
			this.groupBox1.Controls.Add(this.label_read_cost);
			this.groupBox1.Controls.Add(this.label12);
			this.groupBox1.Controls.Add(this.label_read_time);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox_read_content);
			this.groupBox1.Controls.Add(this.label10);
			this.groupBox1.Controls.Add(this.textBox_revision);
			this.groupBox1.Controls.Add(this.textBox_dataField);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textBox_spindleID);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.textBox_stationID);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox_mid);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(321, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(664, 507);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Single Read";
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(94, 479);
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.Size = new System.Drawing.Size(564, 23);
			this.textBox_code.TabIndex = 42;
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 482);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(51, 17);
			this.label1.TabIndex = 41;
			this.label1.Text = "Code：";
			this.label_subscribe_tick.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_subscribe_tick.AutoSize = true;
			this.label_subscribe_tick.ForeColor = System.Drawing.Color.Gray;
			this.label_subscribe_tick.Location = new System.Drawing.Point(42, 458);
			this.label_subscribe_tick.Name = "label_subscribe_tick";
			this.label_subscribe_tick.Size = new System.Drawing.Size(13, 17);
			this.label_subscribe_tick.TabIndex = 40;
			this.label_subscribe_tick.Text = "-";
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label15.AutoSize = true;
			this.label15.ForeColor = System.Drawing.Color.Gray;
			this.label15.Location = new System.Drawing.Point(6, 458);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(43, 17);
			this.label15.TabIndex = 39;
			this.label15.Text = "Tick：";
			this.checkBox_sub_format.AutoSize = true;
			this.checkBox_sub_format.Location = new System.Drawing.Point(6, 435);
			this.checkBox_sub_format.Name = "checkBox_sub_format";
			this.checkBox_sub_format.Size = new System.Drawing.Size(68, 21);
			this.checkBox_sub_format.TabIndex = 38;
			this.checkBox_sub_format.Text = "Format";
			this.checkBox_sub_format.UseVisualStyleBackColor = true;
			this.checkBox_sub_stop.AutoSize = true;
			this.checkBox_sub_stop.Location = new System.Drawing.Point(6, 408);
			this.checkBox_sub_stop.Name = "checkBox_sub_stop";
			this.checkBox_sub_stop.Size = new System.Drawing.Size(54, 21);
			this.checkBox_sub_stop.TabIndex = 37;
			this.checkBox_sub_stop.Text = "Stop";
			this.checkBox_sub_stop.UseVisualStyleBackColor = true;
			this.textBox_log.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_log.Font = new System.Drawing.Font("Consolas", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_log.Location = new System.Drawing.Point(94, 375);
			this.textBox_log.Multiline = true;
			this.textBox_log.Name = "textBox_log";
			this.textBox_log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_log.Size = new System.Drawing.Size(564, 98);
			this.textBox_log.TabIndex = 36;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(3, 381);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(77, 17);
			this.label11.TabIndex = 35;
			this.label11.Text = "Subscribe：";
			this.label_read_tick.AutoSize = true;
			this.label_read_tick.ForeColor = System.Drawing.Color.Gray;
			this.label_read_tick.Location = new System.Drawing.Point(622, 167);
			this.label_read_tick.Name = "label_read_tick";
			this.label_read_tick.Size = new System.Drawing.Size(13, 17);
			this.label_read_tick.TabIndex = 34;
			this.label_read_tick.Text = "-";
			this.label16.AutoSize = true;
			this.label16.ForeColor = System.Drawing.Color.Gray;
			this.label16.Location = new System.Drawing.Point(556, 167);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(43, 17);
			this.label16.TabIndex = 33;
			this.label16.Text = "Tick：";
			this.label_read_byteLength.AutoSize = true;
			this.label_read_byteLength.ForeColor = System.Drawing.Color.Gray;
			this.label_read_byteLength.Location = new System.Drawing.Point(495, 167);
			this.label_read_byteLength.Name = "label_read_byteLength";
			this.label_read_byteLength.Size = new System.Drawing.Size(13, 17);
			this.label_read_byteLength.TabIndex = 32;
			this.label_read_byteLength.Text = "-";
			this.label14.AutoSize = true;
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(436, 167);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(59, 17);
			this.label14.TabIndex = 31;
			this.label14.Text = "Length：";
			this.label_read_cost.AutoSize = true;
			this.label_read_cost.ForeColor = System.Drawing.Color.Gray;
			this.label_read_cost.Location = new System.Drawing.Point(354, 167);
			this.label_read_cost.Name = "label_read_cost";
			this.label_read_cost.Size = new System.Drawing.Size(13, 17);
			this.label_read_cost.TabIndex = 30;
			this.label_read_cost.Text = "-";
			this.label12.AutoSize = true;
			this.label12.ForeColor = System.Drawing.Color.Gray;
			this.label12.Location = new System.Drawing.Point(305, 167);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(46, 17);
			this.label12.TabIndex = 29;
			this.label12.Text = "Cost：";
			this.label_read_time.AutoSize = true;
			this.label_read_time.ForeColor = System.Drawing.Color.Gray;
			this.label_read_time.Location = new System.Drawing.Point(140, 167);
			this.label_read_time.Name = "label_read_time";
			this.label_read_time.Size = new System.Drawing.Size(13, 17);
			this.label_read_time.TabIndex = 28;
			this.label_read_time.Text = "-";
			this.label7.AutoSize = true;
			this.label7.ForeColor = System.Drawing.Color.Gray;
			this.label7.Location = new System.Drawing.Point(91, 167);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(48, 17);
			this.label7.TabIndex = 27;
			this.label7.Text = "Time：";
			this.textBox_read_content.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_read_content.Font = new System.Drawing.Font("Consolas", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_read_content.Location = new System.Drawing.Point(94, 187);
			this.textBox_read_content.Multiline = true;
			this.textBox_read_content.Name = "textBox_read_content";
			this.textBox_read_content.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_read_content.Size = new System.Drawing.Size(564, 184);
			this.textBox_read_content.TabIndex = 26;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(3, 191);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(89, 17);
			this.label10.TabIndex = 25;
			this.label10.Text = "Read Result：";
			this.textBox_revision.Location = new System.Drawing.Point(175, 21);
			this.textBox_revision.Name = "textBox_revision";
			this.textBox_revision.Size = new System.Drawing.Size(50, 23);
			this.textBox_revision.TabIndex = 18;
			this.textBox_revision.Text = "1";
			this.textBox_dataField.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_dataField.Location = new System.Drawing.Point(94, 59);
			this.textBox_dataField.Multiline = true;
			this.textBox_dataField.Name = "textBox_dataField";
			this.textBox_dataField.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_dataField.Size = new System.Drawing.Size(564, 103);
			this.textBox_dataField.TabIndex = 24;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(9, 59);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(87, 34);
			this.label8.TabIndex = 23;
			this.label8.Text = "parameters：\r\n多个参数换行";
			this.textBox_spindleID.Location = new System.Drawing.Point(444, 21);
			this.textBox_spindleID.Name = "textBox_spindleID";
			this.textBox_spindleID.Size = new System.Drawing.Size(48, 23);
			this.textBox_spindleID.TabIndex = 22;
			this.textBox_spindleID.Text = "0";
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(367, 24);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(74, 17);
			this.label5.TabIndex = 21;
			this.label5.Text = "spindleId：";
			this.textBox_stationID.Location = new System.Drawing.Point(313, 21);
			this.textBox_stationID.Name = "textBox_stationID";
			this.textBox_stationID.Size = new System.Drawing.Size(48, 23);
			this.textBox_stationID.TabIndex = 20;
			this.textBox_stationID.Text = "0";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(233, 24);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(71, 17);
			this.label4.TabIndex = 19;
			this.label4.Text = "stationId：";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(114, 24);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(62, 17);
			this.label2.TabIndex = 17;
			this.label2.Text = "revison：";
			this.button_read_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_string.Location = new System.Drawing.Point(524, 19);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(134, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "read";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(Button_read_string_Click);
			this.textBox_mid.Location = new System.Drawing.Point(60, 21);
			this.textBox_mid.Name = "textBox_mid";
			this.textBox_mid.Size = new System.Drawing.Size(51, 23);
			this.textBox_mid.TabIndex = 3;
			this.textBox_mid.Text = "10";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(9, 24);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(45, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "MID：";
			this.tabPage2.Location = new System.Drawing.Point(4, 26);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(991, 513);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "API Test";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox_sub_mid);
			this.panel1.Controls.Add(this.label_sub_mid);
			this.panel1.Controls.Add(this.checkBox2);
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.textBox_revison_connect);
			this.panel1.Controls.Add(this.label9);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1001, 61);
			this.panel1.TabIndex = 4;
			this.textBox_sub_mid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_sub_mid.Location = new System.Drawing.Point(670, 33);
			this.textBox_sub_mid.MinimumSize = new System.Drawing.Size(100, 23);
			this.textBox_sub_mid.Name = "textBox_sub_mid";
			this.textBox_sub_mid.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_sub_mid.Size = new System.Drawing.Size(319, 23);
			this.textBox_sub_mid.TabIndex = 37;
			this.label_sub_mid.AutoSize = true;
			this.label_sub_mid.Location = new System.Drawing.Point(601, 36);
			this.label_sub_mid.Name = "label_sub_mid";
			this.label_sub_mid.Size = new System.Drawing.Size(63, 17);
			this.label_sub_mid.TabIndex = 36;
			this.label_sub_mid.Text = "Sub-MID:";
			this.checkBox2.AutoSize = true;
			this.checkBox2.Checked = true;
			this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox2.Location = new System.Drawing.Point(401, 35);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(184, 21);
			this.checkBox2.TabIndex = 35;
			this.checkBox2.Text = "MID9999 KeepAlive Enable";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(3, 3);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.TcpPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 34;
			this.pipeSelectControl1.TcpPortText = "4545";
			this.pipeSelectControl1.UdpPortText = "4545";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(199, 35);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(192, 21);
			this.checkBox1.TabIndex = 8;
			this.checkBox1.Text = "AutoAckControllerMessage?";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox_revison_connect.Location = new System.Drawing.Point(150, 33);
			this.textBox_revison_connect.Name = "textBox_revison_connect";
			this.textBox_revison_connect.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_revison_connect.Size = new System.Drawing.Size(42, 23);
			this.textBox_revison_connect.TabIndex = 7;
			this.textBox_revison_connect.Text = "1";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(3, 36);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(144, 17);
			this.label9.TabIndex = 6;
			this.label9.Text = "RevisonOnConnected：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(898, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "Disconnect";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(799, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "Connect";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormOpenProtocol";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormOpenProtocol";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormOpenProtocol_FormClosing);
			base.Load += new System.EventHandler(FormOpenProtocol_Load);
			this.panel2.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
