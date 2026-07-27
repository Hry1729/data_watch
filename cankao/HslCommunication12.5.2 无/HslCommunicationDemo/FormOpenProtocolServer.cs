using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.Profinet.OpenProtocol;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormOpenProtocolServer : HslFormContent
	{
		private OpenProtocolServer server = null;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private UserControlHead userControlHead1;

		private Panel panel2;

		private GroupBox groupBox1;

		private Button button_read_string;

		private TextBox textBox_result;

		private Label label7;

		private TextBox textBox_mid;

		private Label label6;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox_port;

		private Label label3;

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

		private Label label1;

		public FormOpenProtocolServer()
		{
			InitializeComponent();
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox_port.Text, out var result))
			{
				DemoUtils.ShowMessage("端口输入格式不正确！");
				return;
			}
			server = new OpenProtocolServer();
			server.LogNet = new LogNetSingle(string.Empty);
			server.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			try
			{
				server.ServerStart(result);
				DemoUtils.ShowMessage((Program.Language == 1) ? "启动成功！" : "Start success");
				button2.Enabled = true;
				button1.Enabled = false;
				panel2.Enabled = true;
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
				server?.ServerClose();
				button2.Enabled = false;
				button1.Enabled = true;
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox_result.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			});
		}

		private void OpenProtocol_ReceivedMessage(object sender, OpenEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox_result.AppendText(DateTime.Now.ToString() + " : " + e.Content + Environment.NewLine);
			});
		}

		private void FormOpenProtocol_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			TreeNode treeNode = new TreeNode("Parameter set messages");
			treeNode.Nodes.Add(new TreeNode("MID 0010 Parameter set ID upload")
			{
				Tag = new OpenMessage(10, 1, -1, -1, null)
			});
			treeNode.Nodes.Add(new TreeNode("MID 0012 Parameter set data upload")
			{
				Tag = new OpenMessage(12, 1, -1, -1, new List<string> { "000" })
			});
			treeNode.Nodes.Add(new TreeNode("MID 0014 Parameter set selected subscribe")
			{
				Tag = new OpenMessage(14, 1, -1, -1, null)
			});
			treeNode.Nodes.Add(new TreeNode("MID 0017 Parameter set selected unsubscribe")
			{
				Tag = new OpenMessage(17, 1, -1, -1, null)
			});
			treeNode.Nodes.Add(new TreeNode("MID 0018 Select Parameter set")
			{
				Tag = new OpenMessage(18, 1, -1, -1, new List<string> { "000", "00" })
			});
			treeNode.Nodes.Add(new TreeNode("MID 0019 Set Parameter set batch size")
			{
				Tag = new OpenMessage(19, 1, -1, -1, new List<string> { "000", "00" })
			});
			treeNode.Nodes.Add(new TreeNode("MID 0020 Reset Parameter set batch counter")
			{
				Tag = new OpenMessage(20, 1, -1, -1, new List<string> { "000" })
			});
			treeView1.Nodes.Add(treeNode);
			TreeNode treeNode2 = new TreeNode("Job message");
			treeNode2.Nodes.Add(new TreeNode("MID 0030 Job ID upload")
			{
				Tag = new OpenMessage(30, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0032 Job data upload")
			{
				Tag = new OpenMessage(32, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0034 Job info subscribe")
			{
				Tag = new OpenMessage(34, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0037 Job info unsubscribe")
			{
				Tag = new OpenMessage(37, 1, -1, -1, null)
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0038 Select Job")
			{
				Tag = new OpenMessage(38, 1, -1, -1, new List<string> { "00" })
			});
			treeNode2.Nodes.Add(new TreeNode("MID 0039 Job restart")
			{
				Tag = new OpenMessage(39, 1, -1, -1, new List<string> { "00" })
			});
			treeView1.Nodes.Add(treeNode2);
			TreeNode treeNode3 = new TreeNode("Tool messages");
			treeNode3.Nodes.Add(new TreeNode("MID 0040 Tool data upload")
			{
				Tag = new OpenMessage(40, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0042 Disable tool")
			{
				Tag = new OpenMessage(42, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0043 Enable tool")
			{
				Tag = new OpenMessage(43, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0044 Disconnect tool")
			{
				Tag = new OpenMessage(44, 1, -1, -1, null)
			});
			treeNode3.Nodes.Add(new TreeNode("MID 0045 Set calibration value")
			{
				Tag = new OpenMessage(44, 1, -1, -1, new List<string> { "011", "02003550" })
			});
			treeView1.Nodes.Add(treeNode3);
			TreeNode treeNode4 = new TreeNode("VIN Messages");
			treeNode4.Nodes.Add(new TreeNode("MID 0050 Vehicle ID Number download")
			{
				Tag = new OpenMessage(50, 1, -1, -1, new List<string> { "0000000000000000000000000" })
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0051 Vehicle ID Number subscribe")
			{
				Tag = new OpenMessage(51, 1, -1, -1, null)
			});
			treeNode4.Nodes.Add(new TreeNode("MID 0054 Vehicle ID Number unsubscrib")
			{
				Tag = new OpenMessage(54, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode4);
			TreeNode treeNode5 = new TreeNode("Tightening result messages");
			treeNode5.Nodes.Add(new TreeNode("MID 0060 Last tightening result data subscribe")
			{
				Tag = new OpenMessage(60, 1, -1, -1, null)
			});
			treeNode5.Nodes.Add(new TreeNode("MID 0063 Last tightening result data unsubscribe")
			{
				Tag = new OpenMessage(63, 1, -1, -1, null)
			});
			treeNode5.Nodes.Add(new TreeNode("MID 0064 Old tightening result upload")
			{
				Tag = new OpenMessage(64, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode5);
			TreeNode treeNode6 = new TreeNode("Alarm messages");
			treeNode6.Nodes.Add(new TreeNode("MID 0070 Alarm subscribe")
			{
				Tag = new OpenMessage(70, 1, -1, -1, null)
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0073 Alarm unsubscribe")
			{
				Tag = new OpenMessage(73, 1, -1, -1, null)
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0074 Alarm acknowledged on controller")
			{
				Tag = new OpenMessage(74, 1, -1, -1, new List<string> { "E406" })
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0076 Alarm status")
			{
				Tag = new OpenMessage(76, 1, -1, -1, new List<string> { "011", "02E404", "031", "041", "052024-07-03:17:08:12" })
			});
			treeNode6.Nodes.Add(new TreeNode("MID 0078 Acknowledge alarm remotely on controller")
			{
				Tag = new OpenMessage(78, 1, -1, -1, null)
			});
			treeView1.Nodes.Add(treeNode6);
			TreeNode treeNode7 = new TreeNode("Time messages");
			treeNode7.Nodes.Add(new TreeNode("MID 0080 Read time upload")
			{
				Tag = new OpenMessage(80, 1, -1, -1, null)
			});
			treeNode7.Nodes.Add(new TreeNode("MID 0082 Set Time")
			{
				Tag = new OpenMessage(82, 1, -1, -1, new List<string> { DateTime.Now.ToString("yyyy-MM-dd:HH:mm:ss") })
			});
			treeView1.Nodes.Add(treeNode7);
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

		private void Button_read_string_Click(object sender, EventArgs e)
		{
			try
			{
				server.Publish(int.Parse(textBox_mid.Text), int.Parse(textBox_revision.Text), int.Parse(textBox_stationID.Text), int.Parse(textBox_spindleID.Text), new List<string>(textBox_dataField.Lines));
				textBox_result.AppendText(DateTime.Now.ToString() + " : Publish success" + Environment.NewLine);
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue("Port", textBox_port.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_port.Text = HslFormContent.GetXmlValue(element, "Port", textBox_port.Text, (string m) => m);
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormOpenProtocolServer_FormClosing(object sender, FormClosingEventArgs e)
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
			this.textBox_revision = new System.Windows.Forms.TextBox();
			this.textBox_dataField = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_spindleID = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_stationID = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button_read_string = new System.Windows.Forms.Button();
			this.textBox_result = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_mid = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label1 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_port = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
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
			this.userControlHead1.ProtocolInfo = "Open Protocol Server";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 3;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.tabControl1);
			this.panel2.Location = new System.Drawing.Point(2, 81);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(1001, 560);
			this.panel2.TabIndex = 5;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(999, 558);
			this.tabControl1.TabIndex = 2;
			this.tabPage1.Controls.Add(this.treeView1);
			this.tabPage1.Controls.Add(this.groupBox1);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(991, 528);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Basic Test";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.treeView1.Location = new System.Drawing.Point(6, 6);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(309, 516);
			this.treeView1.TabIndex = 1;
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.textBox_revision);
			this.groupBox1.Controls.Add(this.textBox_dataField);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textBox_spindleID);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.textBox_stationID);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.textBox_result);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox_mid);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Location = new System.Drawing.Point(321, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(664, 522);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Single Read";
			this.textBox_revision.Location = new System.Drawing.Point(175, 21);
			this.textBox_revision.Name = "textBox_revision";
			this.textBox_revision.Size = new System.Drawing.Size(50, 23);
			this.textBox_revision.TabIndex = 18;
			this.textBox_revision.Text = "10";
			this.textBox_dataField.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_dataField.Location = new System.Drawing.Point(94, 59);
			this.textBox_dataField.Multiline = true;
			this.textBox_dataField.Name = "textBox_dataField";
			this.textBox_dataField.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_dataField.Size = new System.Drawing.Size(564, 86);
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
			this.button_read_string.Text = "Publish";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(Button_read_string_Click);
			this.textBox_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_result.Location = new System.Drawing.Point(94, 151);
			this.textBox_result.Multiline = true;
			this.textBox_result.Name = "textBox_result";
			this.textBox_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_result.Size = new System.Drawing.Size(564, 365);
			this.textBox_result.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(9, 156);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(42, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "Log：";
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
			this.tabPage2.Size = new System.Drawing.Size(991, 528);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Remain";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox_port);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1001, 42);
			this.panel1.TabIndex = 4;
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.Color.Gray;
			this.label1.Location = new System.Drawing.Point(382, 12);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(182, 17);
			this.label1.TabIndex = 6;
			this.label1.Text = "Support MID0010 ~ MID0082";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(238, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "Close";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(139, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "Start";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			this.textBox_port.Location = new System.Drawing.Point(60, 9);
			this.textBox_port.Name = "textBox_port";
			this.textBox_port.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_port.Size = new System.Drawing.Size(58, 23);
			this.textBox_port.TabIndex = 3;
			this.textBox_port.Text = "4545";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(9, 12);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(45, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "port：";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormOpenProtocolServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormOpenProtocol";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormOpenProtocolServer_FormClosing);
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
