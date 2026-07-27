using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Address;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Melsec;
using HslCommunication.Profinet.Melsec.Helper;
using HslCommunication.Profinet.Omron;
using HslCommunication.Profinet.Omron.Helper;
using HslCommunication.Profinet.Siemens;
using HslCommunication.Robot.Hyundai;
using HslControls;

namespace HslCommunicationDemo.HslDebug
{
	public class FormMessageCreate : Form
	{
		private MelsecMcNet melsec1 = new MelsecMcNet();

		private MelsecFxLinks melsec2 = new MelsecFxLinks();

		private OmronFinsNet omron1 = new OmronFinsNet();

		private OmronFinsUdp omron2 = new OmronFinsUdp();

		private OmronHostLink omron3 = new OmronHostLink();

		private IContainer components = null;

		private TreeView treeView1;

		private HslPanelHead hslPanelHead1;

		private Button button_read_word;

		private Button button_read_bool;

		private TextBox textBox2;

		private Label label2;

		private TextBox textBox1;

		private Label label1;

		private Label label3;

		public MessgaeCreate MessageCreate { get; set; }

		public FormMessageCreate()
		{
			InitializeComponent();
		}

		public void AddTreeChild(TreeNode parent, string text, Func<string, ushort, OperateResult<byte[]>> buildReadBool, Func<string, ushort, OperateResult<byte[]>> buildReadWord, bool hex = true)
		{
			MessgaeCreate messgaeCreate = new MessgaeCreate();
			messgaeCreate.BuildReadBool = buildReadBool;
			messgaeCreate.BuildReadWord = buildReadWord;
			messgaeCreate.Text = text;
			messgaeCreate.HexBinary = hex;
			TreeNode treeNode = new TreeNode(text);
			treeNode.Tag = messgaeCreate;
			parent.Nodes.Add(treeNode);
		}

		private void FormMessageCreate_Load(object sender, EventArgs e)
		{
			TreeNode node = new TreeNode("Melsec");
			AddTreeChild(node, "Qna3E-Binary", delegate(string address, ushort length)
			{
				OperateResult<McAddressData> operateResult = melsec1.McAnalysisAddress(address, length, true);
				return (!operateResult.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult) : OperateResult.CreateSuccessResult(McBinaryHelper.PackMcCommand(mcCore: McBinaryHelper.BuildReadMcCoreCommand(operateResult.Content, true), mc: new MelsecMcNet()));
			}, delegate(string address, ushort length)
			{
				OperateResult<McAddressData> operateResult2 = melsec1.McAnalysisAddress(address, length, true);
				return (!operateResult2.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult2) : OperateResult.CreateSuccessResult(McBinaryHelper.PackMcCommand(mcCore: McBinaryHelper.BuildReadMcCoreCommand(operateResult2.Content, false), mc: new MelsecMcNet()));
			});
			AddTreeChild(node, "Qna3E-Ascii", delegate(string address, ushort length)
			{
				OperateResult<McAddressData> operateResult3 = melsec1.McAnalysisAddress(address, length, true);
				return (!operateResult3.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult3) : OperateResult.CreateSuccessResult(McAsciiHelper.PackMcCommand(mcCore: McAsciiHelper.BuildAsciiReadMcCoreCommand(operateResult3.Content, true), mc: melsec1));
			}, delegate(string address, ushort length)
			{
				OperateResult<McAddressData> operateResult4 = melsec1.McAnalysisAddress(address, length, true);
				return (!operateResult4.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult4) : OperateResult.CreateSuccessResult(McAsciiHelper.PackMcCommand(mcCore: McAsciiHelper.BuildAsciiReadMcCoreCommand(operateResult4.Content, false), mc: melsec1));
			}, false);
			AddTreeChild(node, "A1E-Binary", (string address, ushort length) => MelsecA1ENet.BuildReadCommand(address, length, true, byte.MaxValue).Then((List<byte[]> m) => OperateResult.CreateSuccessResult(m[0])), (string address, ushort length) => MelsecA1ENet.BuildReadCommand(address, length, false, byte.MaxValue).Then((List<byte[]> m) => OperateResult.CreateSuccessResult(m[0])));
			AddTreeChild(node, "A1E-Ascii", (string address, ushort length) => MelsecA1EAsciiNet.BuildReadCommand(address, length, true, byte.MaxValue).Then((List<byte[]> m) => OperateResult.CreateSuccessResult(m[0])), (string address, ushort length) => MelsecA1EAsciiNet.BuildReadCommand(address, length, false, byte.MaxValue).Then((List<byte[]> m) => OperateResult.CreateSuccessResult(m[0])), false);
			AddTreeChild(node, "FxLinks", delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult5 = MelsecFxLinksHelper.BuildReadCommand(melsec2.Station, address, length, true, melsec2.WaittingTime);
				return (!operateResult5.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult5) : OperateResult.CreateSuccessResult(melsec2.PackCommandWithHeader(operateResult5.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult6 = MelsecFxLinksHelper.BuildReadCommand(melsec2.Station, address, length, false, melsec2.WaittingTime);
				return (!operateResult6.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult6) : OperateResult.CreateSuccessResult(melsec2.PackCommandWithHeader(operateResult6.Content[0]));
			}, false);
			treeView1.Nodes.Add(node);
			TreeNode node2 = new TreeNode("Siemens");
			AddTreeChild(node2, "S7", (string address, ushort length) => SiemensS7Net.BuildBitReadCommand(address, 1), delegate(string address, ushort length)
			{
				OperateResult<S7AddressData> operateResult7 = S7AddressData.ParseFrom(address, length);
				return (!operateResult7.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult7) : SiemensS7Net.BuildReadCommand(new S7AddressData[1] { operateResult7.Content }, 1);
			});
			treeView1.Nodes.Add(node2);
			TreeNode node3 = new TreeNode("Modbus");
			AddTreeChild(node3, "Modbus Tcp", delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult8 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 1);
				return (!operateResult8.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult8) : OperateResult.CreateSuccessResult(ModbusInfo.PackCommandToTcp(operateResult8.Content[0], 0));
			}, delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult9 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 3);
				return (!operateResult9.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult9) : OperateResult.CreateSuccessResult(ModbusInfo.PackCommandToTcp(operateResult9.Content[0], 0));
			});
			AddTreeChild(node3, "Modbus Rtu", delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult10 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 1);
				return (!operateResult10.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult10) : OperateResult.CreateSuccessResult(ModbusInfo.PackCommandToRtu(operateResult10.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult11 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 3);
				return (!operateResult11.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult11) : OperateResult.CreateSuccessResult(ModbusInfo.PackCommandToRtu(operateResult11.Content[0]));
			});
			AddTreeChild(node3, "Modbus Ascii", delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult12 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 1);
				return (!operateResult12.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult12) : OperateResult.CreateSuccessResult(ModbusInfo.TransModbusCoreToAsciiPackCommand(operateResult12.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<byte[][]> operateResult13 = ModbusInfo.BuildReadModbusCommand(address, length, 1, true, 3);
				return (!operateResult13.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult13) : OperateResult.CreateSuccessResult(ModbusInfo.TransModbusCoreToAsciiPackCommand(operateResult13.Content[0]));
			}, false);
			treeView1.Nodes.Add(node3);
			TreeNode node4 = new TreeNode("Omron");
			AddTreeChild(node4, "Fins Tcp", delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult14 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, true, int.MaxValue);
				return (!operateResult14.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult14) : OperateResult.CreateSuccessResult(omron1.PackCommandWithHeader(operateResult14.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult15 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, false, int.MaxValue);
				return (!operateResult15.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult15) : OperateResult.CreateSuccessResult(omron1.PackCommandWithHeader(operateResult15.Content[0]));
			});
			AddTreeChild(node4, "Fins Udp", delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult16 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, true, int.MaxValue);
				return (!operateResult16.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult16) : OperateResult.CreateSuccessResult(omron2.PackCommandWithHeader(operateResult16.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult17 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, false, int.MaxValue);
				return (!operateResult17.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult17) : OperateResult.CreateSuccessResult(omron2.PackCommandWithHeader(operateResult17.Content[0]));
			});
			AddTreeChild(node4, "Host Link", delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult18 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, true, int.MaxValue);
				return (!operateResult18.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult18) : OperateResult.CreateSuccessResult(OmronHostLinkHelper.PackCommand(omron3, 0, operateResult18.Content[0]));
			}, delegate(string address, ushort length)
			{
				OperateResult<List<byte[]>> operateResult19 = OmronFinsNetHelper.BuildReadCommand(OmronPlcType.CSCJ, address, length, false, int.MaxValue);
				return (!operateResult19.IsSuccess) ? OperateResult.CreateFailedResult<byte[]>(operateResult19) : OperateResult.CreateSuccessResult(OmronHostLinkHelper.PackCommand(omron3, 0, operateResult19.Content[0]));
			}, false);
			treeView1.Nodes.Add(node4);
			TreeNode node5 = new TreeNode("Omron");
			AddTreeChild(node5, "Hyundai UdpNet", (string address, ushort length) => OperateResult.CreateSuccessResult(new HyundaiData
			{
				Command = 'S',
				CharDummy = "ABC",
				State = 5,
				Count = 6,
				Data = new double[6] { 1.1, 2.2, 3.3, 4.4, 5.5, 6.6 }
			}.ToBytes()), (string address, ushort length) => OperateResult.CreateSuccessResult(new HyundaiData
			{
				Command = 'P',
				CharDummy = "CVD",
				State = 7,
				Count = 6,
				Data = new double[6] { 5.1, 5.2, 5.3, 5.4, 6.5, 100.6 }
			}.ToBytes()));
			treeView1.Nodes.Add(node5);
		}

		private void button_read_bool_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is MessgaeCreate messgaeCreate && messgaeCreate.BuildReadBool != null)
			{
				OperateResult<byte[]> operateResult = messgaeCreate.BuildReadBool(textBox1.Text, ushort.Parse(textBox2.Text));
				if (operateResult.IsSuccess)
				{
					messgaeCreate.Result = (messgaeCreate.HexBinary ? operateResult.Content.ToHexString(' ') : SoftBasic.GetAsciiStringRender(operateResult.Content));
					MessageCreate = messgaeCreate;
					base.DialogResult = DialogResult.OK;
				}
				else
				{
					DemoUtils.ShowMessage("Build failed: " + operateResult.Message);
				}
			}
		}

		private void button_read_word_Click(object sender, EventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is MessgaeCreate messgaeCreate && messgaeCreate.BuildReadWord != null)
			{
				OperateResult<byte[]> operateResult = messgaeCreate.BuildReadWord(textBox1.Text, ushort.Parse(textBox2.Text));
				if (operateResult.IsSuccess)
				{
					messgaeCreate.Result = (messgaeCreate.HexBinary ? operateResult.Content.ToHexString(' ') : SoftBasic.GetAsciiStringRender(operateResult.Content));
					MessageCreate = messgaeCreate;
					base.DialogResult = DialogResult.OK;
				}
				else
				{
					DemoUtils.ShowMessage("Build failed: " + operateResult.Message);
				}
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			TreeNode selectedNode = treeView1.SelectedNode;
			if (selectedNode != null && selectedNode.Tag is MessgaeCreate messgaeCreate)
			{
				label3.Text = "协议：" + messgaeCreate.Text;
			}
		}

		private void FormMessageCreate_Shown(object sender, EventArgs e)
		{
			treeView1.ExpandAll();
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
			this.hslPanelHead1 = new HslControls.HslPanelHead();
			this.button_read_word = new System.Windows.Forms.Button();
			this.button_read_bool = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.hslPanelHead1.SuspendLayout();
			base.SuspendLayout();
			this.treeView1.Dock = System.Windows.Forms.DockStyle.Left;
			this.treeView1.Location = new System.Drawing.Point(0, 0);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(227, 469);
			this.treeView1.TabIndex = 0;
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.hslPanelHead1.Controls.Add(this.button_read_word);
			this.hslPanelHead1.Controls.Add(this.button_read_bool);
			this.hslPanelHead1.Controls.Add(this.textBox2);
			this.hslPanelHead1.Controls.Add(this.label2);
			this.hslPanelHead1.Controls.Add(this.textBox1);
			this.hslPanelHead1.Controls.Add(this.label1);
			this.hslPanelHead1.Location = new System.Drawing.Point(233, 40);
			this.hslPanelHead1.Name = "hslPanelHead1";
			this.hslPanelHead1.Size = new System.Drawing.Size(562, 140);
			this.hslPanelHead1.TabIndex = 1;
			this.hslPanelHead1.Text = "读取";
			this.button_read_word.Location = new System.Drawing.Point(189, 93);
			this.button_read_word.Name = "button_read_word";
			this.button_read_word.Size = new System.Drawing.Size(162, 38);
			this.button_read_word.TabIndex = 5;
			this.button_read_word.Text = "Build Read Byte/Word";
			this.button_read_word.UseVisualStyleBackColor = true;
			this.button_read_word.Click += new System.EventHandler(button_read_word_Click);
			this.button_read_bool.Location = new System.Drawing.Point(14, 93);
			this.button_read_bool.Name = "button_read_bool";
			this.button_read_bool.Size = new System.Drawing.Size(162, 38);
			this.button_read_bool.TabIndex = 4;
			this.button_read_bool.Text = "Build Read Bool";
			this.button_read_bool.UseVisualStyleBackColor = true;
			this.button_read_bool.Click += new System.EventHandler(button_read_bool_Click);
			this.textBox2.Location = new System.Drawing.Point(426, 43);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(109, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(357, 46);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(50, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Length:";
			this.textBox1.Location = new System.Drawing.Point(94, 43);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(257, 23);
			this.textBox1.TabIndex = 1;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(11, 46);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Address:";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(233, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "协议：";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(800, 469);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.hslPanelHead1);
			base.Controls.Add(this.treeView1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormMessageCreate";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormMessageCreate";
			base.Load += new System.EventHandler(FormMessageCreate_Load);
			base.Shown += new System.EventHandler(FormMessageCreate_Shown);
			this.hslPanelHead1.ResumeLayout(false);
			this.hslPanelHead1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
