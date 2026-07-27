using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunication.Profinet.AllenBradley;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo
{
	public class FormAllenBrandlyBrowser : HslFormContent
	{
		private AllenBradleyNet allenBradleyNet = null;

		private TreeNode treeViewSelectedNode = null;

		private AbTagItem[] rootTags = null;

		private Dictionary<int, AbTagItem[]> dictStructDefine;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox_ip;

		private Label label1;

		private SplitContainer splitContainer1;

		private TreeView treeView1;

		private Panel panel2;

		private TextBox textBox4;

		private Label label2;

		private Button button3;

		private Panel panel3;

		private Panel panel4;

		private StatusStrip statusStrip1;

		private ToolStripStatusLabel toolStripStatusLabel1;

		private TextBox textBox16;

		private Label label8;

		private TextBox textBox15;

		private Label label23;

		private UserControlHead userControlHead1;

		private DataGridView dataGridView1;

		private Button button4;

		private DataGridViewTextBoxColumn Column_number;

		private DataGridViewImageColumn Column_image;

		private DataGridViewTextBoxColumn Column_name;

		private DataGridViewTextBoxColumn Column_type;

		private DataGridViewTextBoxColumn Column_TypeName;

		private DataGridViewTextBoxColumn Column_value;

		private TextBox textBox_regex;

		public FormAllenBrandlyBrowser()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void RedisBrowser_Load(object sender, EventArgs e)
		{
			DemoUtils.SetDeviveIp(textBox_ip);
			ImageList imageList = new ImageList();
			imageList.Images.Add("VirtualMachine", Resources.VirtualMachine);
			imageList.Images.Add("Class_489", Resources.Class_489);
			imageList.Images.Add("Enum_582", Resources.Enum_582);
			imageList.Images.Add("brackets_Square_16xMD", Resources.brackets_Square_16xMD);
			imageList.Images.Add("Method_636", Resources.Method_636);
			imageList.Images.Add("Module_648", Resources.Module_648);
			imageList.Images.Add("Structure_507", Resources.Structure_507);
			treeView1.ImageList = imageList;
			treeView1.Nodes.Add("Values");
			treeView1.Nodes[0].ImageKey = "VirtualMachine";
			treeView1.Nodes[0].SelectedImageKey = "VirtualMachine";
			treeViewSelectedNode = treeView1.Nodes[0];
			panel3.Enabled = false;
			panel4.Dock = DockStyle.Fill;
			dataGridView1.SizeChanged += DataGridView1_SizeChanged;
		}

		private void DataGridView1_SizeChanged(object sender, EventArgs e)
		{
			dataGridView1.Columns[5].Width = dataGridView1.Width - 21 - 435;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox2.Text, out var result))
			{
				DemoUtils.ShowMessage(DemoUtils.PortInputWrong);
				return;
			}
			if (!byte.TryParse(textBox15.Text, out var result2))
			{
				DemoUtils.ShowMessage(DemoUtils.SlotInputWrong);
				return;
			}
			dictStructDefine = new Dictionary<int, AbTagItem[]>();
			allenBradleyNet?.ConnectClose();
			allenBradleyNet = new AllenBradleyNet();
			allenBradleyNet.IpAddress = textBox_ip.Text;
			allenBradleyNet.Port = result;
			allenBradleyNet.Slot = result2;
			if (!string.IsNullOrEmpty(textBox16.Text))
			{
				allenBradleyNet.MessageRouter = new MessageRouter(textBox16.Text);
			}
			OperateResult operateResult = allenBradleyNet.ConnectServer();
			if (operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
				button2.Enabled = true;
				button1.Enabled = false;
				panel2.Enabled = true;
				panel3.Enabled = true;
				TagRefresh();
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
			});
		}

		private void button3_Click(object sender, EventArgs e)
		{
			TagRefresh();
		}

		private void TagRefresh()
		{
			treeView1.Nodes[0].Nodes.Clear();
			OperateResult<AbTagItem[]> operateResult = allenBradleyNet.TagEnumerator();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage(operateResult.Message);
				return;
			}
			rootTags = operateResult.Content;
			AddTreeNode(treeView1.Nodes[0], operateResult.Content, string.Empty, true, true);
			treeView1.Nodes[0].Expand();
		}

		private string GetIamgeKeyByTag(AbTagItem abTag)
		{
			if (abTag.ArrayDimension == 1)
			{
				return "brackets_Square_16xMD";
			}
			if (abTag.ArrayDimension > 1)
			{
				return "Module_648";
			}
			if (abTag.IsStruct)
			{
				return "Class_489";
			}
			return "Enum_582";
		}

		private void RenderDataGridView(AbTagItem[] items)
		{
			if (items == null)
			{
				items = new AbTagItem[0];
			}
			DemoUtils.DataGridSpecifyRowCount(dataGridView1, items.Length);
			for (int i = 0; i < items.Length; i++)
			{
				dataGridView1.Rows[i].Cells[0].Value = (i + 1).ToString();
				dataGridView1.Rows[i].Cells[1].Value = treeView1.ImageList.Images[GetIamgeKeyByTag(items[i])];
				dataGridView1.Rows[i].Cells[2].Value = items[i].Name;
				dataGridView1.Rows[i].Cells[3].Value = items[i].SymbolType.ToString("X");
				dataGridView1.Rows[i].Cells[4].Value = items[i].GetTypeText();
				if (items[i].Tag != null)
				{
					dataGridView1.Rows[i].Cells[5].Value = items[i].Tag.ToString();
				}
				else
				{
					dataGridView1.Rows[i].Cells[5].Value = "";
				}
			}
		}

		private void AddTreeNode(TreeNode parent, AbTagItem[] items, string parentName, bool showDataGrid, bool regexMatch)
		{
			foreach (AbTagItem abTagItem in items)
			{
				if (regexMatch && !string.IsNullOrEmpty(textBox_regex.Text) && !Regex.IsMatch(abTagItem.Name, textBox_regex.Text))
				{
					continue;
				}
				TreeNode treeNode = new TreeNode(abTagItem.Name);
				treeNode.Name = (string.IsNullOrEmpty(parentName) ? abTagItem.Name : (parentName + "." + abTagItem.Name));
				treeNode.ImageKey = GetIamgeKeyByTag(abTagItem);
				treeNode.SelectedImageKey = GetIamgeKeyByTag(abTagItem);
				treeNode.Tag = abTagItem;
				parent.Nodes.Add(treeNode);
				if (!abTagItem.IsStruct)
				{
					continue;
				}
				OperateResult<AbTagItem[]> @struct = GetStruct(abTagItem);
				if (!@struct.IsSuccess)
				{
					continue;
				}
				abTagItem.Members = @struct.Content;
				if (abTagItem.ArrayDimension == 0)
				{
					AddTreeNode(treeNode, @struct.Content, treeNode.Name, false, false);
				}
				else if (abTagItem.ArrayDimension == 1)
				{
					for (int j = 0; j < abTagItem.ArrayLength[0]; j++)
					{
						TreeNode treeNode2 = new TreeNode(abTagItem.Name + $"[{j}]");
						treeNode2.Name = (string.IsNullOrEmpty(parentName) ? (abTagItem.Name + $"[{j}]") : (parentName + "." + abTagItem.Name + $"[{j}]"));
						treeNode2.ImageKey = GetIamgeKeyByTag(abTagItem);
						treeNode2.SelectedImageKey = GetIamgeKeyByTag(abTagItem);
						AbTagItem abTagItem2 = new AbTagItem();
						abTagItem2.Name = abTagItem.Name + $"[{j}]";
						abTagItem2.InstanceID = abTagItem.InstanceID;
						abTagItem2.SymbolType = abTagItem.SymbolType;
						abTagItem2.IsStruct = abTagItem.IsStruct;
						abTagItem2.ArrayDimension = 0;
						abTagItem2.ArrayLength = abTagItem.ArrayLength;
						abTagItem2.Members = AbTagItem.CloneBy(abTagItem.Members);
						treeNode2.Tag = abTagItem2;
						AddTreeNode(treeNode2, @struct.Content, treeNode2.Name, false, false);
						treeNode.Nodes.Add(treeNode2);
					}
				}
				else
				{
					if (abTagItem.ArrayDimension != 2)
					{
						continue;
					}
					for (int k = 0; k < abTagItem.ArrayLength[0]; k++)
					{
						for (int l = 0; l < abTagItem.ArrayLength[1]; l++)
						{
							TreeNode treeNode3 = new TreeNode(abTagItem.Name + $"[{k},{l}]");
							treeNode3.Name = (string.IsNullOrEmpty(parentName) ? (abTagItem.Name + $"[{k},{l}]") : (parentName + "." + abTagItem.Name + $"[{k},{l}]"));
							treeNode3.ImageKey = GetIamgeKeyByTag(abTagItem);
							treeNode3.SelectedImageKey = GetIamgeKeyByTag(abTagItem);
							AbTagItem abTagItem3 = new AbTagItem();
							abTagItem3.Name = abTagItem.Name + $"[{k},{l}]";
							abTagItem3.InstanceID = abTagItem.InstanceID;
							abTagItem3.SymbolType = abTagItem.SymbolType;
							abTagItem3.IsStruct = abTagItem.IsStruct;
							abTagItem3.ArrayDimension = 0;
							abTagItem3.ArrayLength = abTagItem.ArrayLength;
							abTagItem3.Members = AbTagItem.CloneBy(abTagItem.Members);
							treeNode3.Tag = abTagItem3;
							AddTreeNode(treeNode3, @struct.Content, treeNode3.Name, false, false);
							treeNode.Nodes.Add(treeNode3);
						}
					}
				}
			}
			if (showDataGrid)
			{
				RenderDataGridView(items);
			}
		}

		private OperateResult<AbTagItem[]> GetStruct(AbTagItem tagItem)
		{
			int key = tagItem.SymbolType & 0xFFF;
			if (dictStructDefine.ContainsKey(key))
			{
				return OperateResult.CreateSuccessResult(AbTagItem.CloneBy(dictStructDefine[key]));
			}
			OperateResult<AbTagItem[]> operateResult = allenBradleyNet.StructTagEnumerator(tagItem);
			if (operateResult.IsSuccess)
			{
				dictStructDefine.Add(key, AbTagItem.CloneBy(operateResult.Content));
			}
			return operateResult;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			panel3.Enabled = false;
			button1.Enabled = true;
			button2.Enabled = false;
			allenBradleyNet?.ConnectClose();
		}

		private void ShowDataRoots()
		{
			if (rootTags == null)
			{
				return;
			}
			AbTagItem[] array = rootTags;
			foreach (AbTagItem abTagItem in array)
			{
				if (abTagItem.IsStruct)
				{
					continue;
				}
				if (abTagItem.ArrayDimension == 0)
				{
					if (abTagItem.SymbolType == 194)
					{
						OperateResult<byte> operateResult = allenBradleyNet.ReadByte(abTagItem.Name);
						if (operateResult.IsSuccess)
						{
							abTagItem.Tag = (sbyte)operateResult.Content;
						}
					}
					else if (abTagItem.SymbolType == 195)
					{
						OperateResult<short> operateResult2 = allenBradleyNet.ReadInt16(abTagItem.Name);
						if (operateResult2.IsSuccess)
						{
							abTagItem.Tag = operateResult2.Content;
						}
					}
					else if (abTagItem.SymbolType == 196)
					{
						OperateResult<int> operateResult3 = allenBradleyNet.ReadInt32(abTagItem.Name);
						if (operateResult3.IsSuccess)
						{
							abTagItem.Tag = operateResult3.Content;
						}
					}
					else if (abTagItem.SymbolType == 197)
					{
						OperateResult<long> operateResult4 = allenBradleyNet.ReadInt64(abTagItem.Name);
						if (operateResult4.IsSuccess)
						{
							abTagItem.Tag = operateResult4.Content;
						}
					}
					else if (abTagItem.SymbolType == 198)
					{
						OperateResult<byte> operateResult5 = allenBradleyNet.ReadByte(abTagItem.Name);
						if (operateResult5.IsSuccess)
						{
							abTagItem.Tag = operateResult5.Content;
						}
					}
					else if (abTagItem.SymbolType == 199)
					{
						OperateResult<ushort> operateResult6 = allenBradleyNet.ReadUInt16(abTagItem.Name);
						if (operateResult6.IsSuccess)
						{
							abTagItem.Tag = operateResult6.Content;
						}
					}
					else if (abTagItem.SymbolType == 200)
					{
						OperateResult<uint> operateResult7 = allenBradleyNet.ReadUInt32(abTagItem.Name);
						if (operateResult7.IsSuccess)
						{
							abTagItem.Tag = operateResult7.Content;
						}
					}
					else if (abTagItem.SymbolType == 201)
					{
						OperateResult<ulong> operateResult8 = allenBradleyNet.ReadUInt64(abTagItem.Name);
						if (operateResult8.IsSuccess)
						{
							abTagItem.Tag = operateResult8.Content;
						}
					}
					else if (abTagItem.SymbolType == 193)
					{
						OperateResult<bool> operateResult9 = allenBradleyNet.ReadBool(abTagItem.Name);
						if (operateResult9.IsSuccess)
						{
							abTagItem.Tag = operateResult9.Content;
						}
					}
					else if (abTagItem.SymbolType == 202)
					{
						OperateResult<float> operateResult10 = allenBradleyNet.ReadFloat(abTagItem.Name);
						if (operateResult10.IsSuccess)
						{
							abTagItem.Tag = operateResult10.Content;
						}
					}
					else if (abTagItem.SymbolType == 203)
					{
						OperateResult<double> operateResult11 = allenBradleyNet.ReadDouble(abTagItem.Name);
						if (operateResult11.IsSuccess)
						{
							abTagItem.Tag = operateResult11.Content;
						}
					}
					else if ((abTagItem.SymbolType | 0xF00) == 4033)
					{
						int boolIndex = abTagItem.SymbolType >> 8;
						OperateResult<byte[]> operateResult12 = allenBradleyNet.Read(abTagItem.Name, 1);
						if (operateResult12.IsSuccess)
						{
							abTagItem.Tag = operateResult12.Content[0].GetBoolByIndex(boolIndex);
						}
					}
				}
				else
				{
					if (abTagItem.ArrayDimension != 1)
					{
						continue;
					}
					if (abTagItem.SymbolType == 194)
					{
						OperateResult<byte[]> operateResult13 = allenBradleyNet.Read(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult13.IsSuccess)
						{
							sbyte[] array2 = new sbyte[operateResult13.Content.Length];
							for (int j = 0; j < array2.Length; j++)
							{
								array2[j] = (sbyte)operateResult13.Content[j];
							}
							abTagItem.Tag = array2.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 195)
					{
						OperateResult<short[]> operateResult14 = allenBradleyNet.ReadInt16(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult14.IsSuccess)
						{
							abTagItem.Tag = operateResult14.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 196)
					{
						OperateResult<int[]> operateResult15 = allenBradleyNet.ReadInt32(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult15.IsSuccess)
						{
							abTagItem.Tag = operateResult15.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 197)
					{
						OperateResult<long[]> operateResult16 = allenBradleyNet.ReadInt64(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult16.IsSuccess)
						{
							abTagItem.Tag = operateResult16.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 198)
					{
						OperateResult<byte[]> operateResult17 = allenBradleyNet.Read(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult17.IsSuccess)
						{
							abTagItem.Tag = operateResult17.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 199)
					{
						OperateResult<ushort[]> operateResult18 = allenBradleyNet.ReadUInt16(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult18.IsSuccess)
						{
							abTagItem.Tag = operateResult18.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 200)
					{
						OperateResult<uint[]> operateResult19 = allenBradleyNet.ReadUInt32(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult19.IsSuccess)
						{
							abTagItem.Tag = operateResult19.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 201)
					{
						OperateResult<ulong[]> operateResult20 = allenBradleyNet.ReadUInt64(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult20.IsSuccess)
						{
							abTagItem.Tag = operateResult20.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 202)
					{
						OperateResult<float[]> operateResult21 = allenBradleyNet.ReadFloat(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult21.IsSuccess)
						{
							abTagItem.Tag = operateResult21.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 203)
					{
						OperateResult<double[]> operateResult22 = allenBradleyNet.ReadDouble(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult22.IsSuccess)
						{
							abTagItem.Tag = operateResult22.Content.ToArrayString();
						}
					}
					else if (abTagItem.SymbolType == 211)
					{
						OperateResult<int[]> operateResult23 = allenBradleyNet.ReadInt32(abTagItem.Name, (ushort)abTagItem.ArrayLength[0]);
						if (operateResult23.IsSuccess)
						{
							abTagItem.Tag = SoftBasic.BoolArrayToString(allenBradleyNet.ByteTransform.TransByte(operateResult23.Content).ToBoolArray());
						}
					}
				}
			}
		}

		private void ReadStruct(AbTagItem tagItem, string tagName)
		{
			if (!tagItem.IsStruct)
			{
				return;
			}
			OperateResult<byte[]> operateResult = allenBradleyNet.Read(tagName, 1);
			if (!operateResult.IsSuccess || tagItem.Members == null)
			{
				return;
			}
			AbTagItem[] members = tagItem.Members;
			foreach (AbTagItem abTagItem in members)
			{
				if (abTagItem.IsStruct)
				{
					continue;
				}
				if (abTagItem.ArrayDimension == 0)
				{
					if (abTagItem.SymbolType == 194)
					{
						abTagItem.Tag = ((sbyte)operateResult.Content[abTagItem.ByteOffset]).ToString();
					}
					else if (abTagItem.SymbolType == 195)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt16(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 196)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt32(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 197)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt64(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 198)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransByte(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 199)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt16(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 200)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt32(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 201)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt64(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 193)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransBool(operateResult.Content, abTagItem.ByteOffset * 8 + abTagItem.ArrayLength[0]).ToString();
					}
					else if (abTagItem.SymbolType == 202)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransSingle(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 203)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransDouble(operateResult.Content, abTagItem.ByteOffset).ToString();
					}
					else if (abTagItem.SymbolType == 211)
					{
						abTagItem.Tag = SoftBasic.BoolArrayToString(allenBradleyNet.ByteTransform.TransByte(operateResult.Content, abTagItem.ByteOffset, 4).ToBoolArray());
					}
				}
				else if (abTagItem.ArrayDimension == 1)
				{
					if (abTagItem.SymbolType == 194)
					{
						abTagItem.Tag = Encoding.UTF8.GetString(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]);
					}
					else if (abTagItem.SymbolType == 195)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt16(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 196)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt32(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 197)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransInt64(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 198)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransByte(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 199)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt16(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 200)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt32(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 201)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransUInt64(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 202)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransSingle(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 203)
					{
						abTagItem.Tag = allenBradleyNet.ByteTransform.TransDouble(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0]).ToArrayString();
					}
					else if (abTagItem.SymbolType == 211)
					{
						abTagItem.Tag = SoftBasic.BoolArrayToString(allenBradleyNet.ByteTransform.TransByte(operateResult.Content, abTagItem.ByteOffset, abTagItem.ArrayLength[0] * 4).ToBoolArray());
					}
				}
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			if (treeViewSelectedNode == null)
			{
				return;
			}
			if (treeViewSelectedNode.Tag is AbTagItem abTagItem)
			{
				if (abTagItem.IsStruct && abTagItem.ArrayDimension == 0)
				{
					ReadStruct(abTagItem, treeViewSelectedNode.Name);
					RenderDataGridView(abTagItem.Members);
				}
				else if (treeViewSelectedNode.Parent.ImageKey == "VirtualMachine")
				{
					ShowDataRoots();
					RenderDataGridView(rootTags);
				}
			}
			else if (treeViewSelectedNode.ImageKey == "VirtualMachine")
			{
				ShowDataRoots();
				RenderDataGridView(rootTags);
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			treeViewSelectedNode = e.Node;
			RenderDataGridViewBySelect(e.Node);
		}

		private void RenderDataGridViewBySelect(TreeNode treeNode)
		{
			if (treeNode.Tag is AbTagItem abTagItem)
			{
				textBox4.Text = treeNode.Name;
				if (abTagItem.IsStruct)
				{
					RenderStructDataSelect(treeNode);
				}
				else if (treeNode.Parent.Tag is AbTagItem abTagItem2)
				{
					if (abTagItem2.IsStruct && abTagItem2.ArrayDimension == 1)
					{
						RenderDataGridView(abTagItem2.Members);
					}
				}
				else if (treeNode.Parent.Tag == null)
				{
					RenderDataGridView(rootTags);
				}
			}
			else if (treeNode.ImageKey == "VirtualMachine")
			{
				textBox4.Text = string.Empty;
				RenderDataGridView(rootTags);
			}
		}

		private void RenderStructDataSelect(TreeNode treeNode)
		{
			if (treeNode.Tag is AbTagItem abTagItem)
			{
				if (abTagItem.IsStruct)
				{
					if (abTagItem.ArrayDimension == 0)
					{
						RenderDataGridView(abTagItem.Members);
					}
					else
					{
						RenderStructDataSelect(treeNode.Parent);
					}
				}
			}
			else
			{
				RenderDataGridView(rootTags);
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlIpAddress, textBox_ip.Text);
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSlot, textBox15.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox_ip.Text = element.Attribute(DemoDeviceList.XmlIpAddress).Value;
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox15.Text = element.Attribute(DemoDeviceList.XmlSlot).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormAllenBrandlyBrowser_FormClosing(object sender, FormClosingEventArgs e)
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
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label23 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_ip = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.textBox_regex = new System.Windows.Forms.TextBox();
			this.button3 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.panel2 = new System.Windows.Forms.Panel();
			this.panel4 = new System.Windows.Forms.Panel();
			this.button4 = new System.Windows.Forms.Button();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column_number = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_image = new System.Windows.Forms.DataGridViewImageColumn();
			this.Column_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_type = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_TypeName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_value = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			this.panel3.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox16);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label23);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.textBox_ip);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(2, 34);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(976, 40);
			this.panel1.TabIndex = 8;
			this.textBox16.Location = new System.Drawing.Point(530, 7);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(159, 23);
			this.textBox16.TabIndex = 19;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(474, 10);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(50, 17);
			this.label8.TabIndex = 18;
			this.label8.Text = "Router:";
			this.textBox15.Location = new System.Drawing.Point(423, 7);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(32, 23);
			this.textBox15.TabIndex = 17;
			this.textBox15.Text = "0";
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(379, 10);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(41, 17);
			this.label23.TabIndex = 16;
			this.label23.Text = "slot：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(792, 4);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "disconnect";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(695, 4);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "connect";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(276, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(93, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "44818";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(222, 10);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(45, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "port：";
			this.textBox_ip.Location = new System.Drawing.Point(66, 7);
			this.textBox_ip.Name = "textBox_ip";
			this.textBox_ip.Size = new System.Drawing.Size(141, 23);
			this.textBox_ip.TabIndex = 1;
			this.textBox_ip.Text = "127.0.0.1";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 10);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(55, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Ip addr:";
			this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer1.Location = new System.Drawing.Point(3, 3);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.textBox_regex);
			this.splitContainer1.Panel1.Controls.Add(this.button3);
			this.splitContainer1.Panel1.Controls.Add(this.treeView1);
			this.splitContainer1.Panel2.Controls.Add(this.panel2);
			this.splitContainer1.Size = new System.Drawing.Size(970, 539);
			this.splitContainer1.SplitterDistance = 218;
			this.splitContainer1.TabIndex = 9;
			this.textBox_regex.Location = new System.Drawing.Point(3, 6);
			this.textBox_regex.Name = "textBox_regex";
			this.textBox_regex.Size = new System.Drawing.Size(129, 23);
			this.textBox_regex.TabIndex = 2;
			this.button3.Location = new System.Drawing.Point(142, 5);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(75, 26);
			this.button3.TabIndex = 1;
			this.button3.Text = "refresh";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.treeView1.Location = new System.Drawing.Point(3, 32);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(207, 504);
			this.treeView1.TabIndex = 0;
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(treeView1_AfterSelect);
			this.panel2.Controls.Add(this.panel4);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(748, 539);
			this.panel2.TabIndex = 0;
			this.panel4.Controls.Add(this.button4);
			this.panel4.Controls.Add(this.dataGridView1);
			this.panel4.Controls.Add(this.label2);
			this.panel4.Controls.Add(this.textBox4);
			this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel4.Location = new System.Drawing.Point(0, 0);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(748, 539);
			this.panel4.TabIndex = 5;
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(648, 3);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 27);
			this.button4.TabIndex = 6;
			this.button4.Text = "Show Value";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			this.dataGridView1.AllowUserToResizeRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(236, 236, 255);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column_number, this.Column_image, this.Column_name, this.Column_type, this.Column_TypeName, this.Column_value);
			this.dataGridView1.Location = new System.Drawing.Point(3, 35);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.Size = new System.Drawing.Size(736, 501);
			this.dataGridView1.TabIndex = 5;
			this.Column_number.HeaderText = "Number";
			this.Column_number.Name = "Column_number";
			this.Column_number.ReadOnly = true;
			this.Column_number.Width = 80;
			this.Column_image.HeaderText = "";
			this.Column_image.Name = "Column_image";
			this.Column_image.ReadOnly = true;
			this.Column_image.Width = 25;
			this.Column_name.HeaderText = "Name";
			this.Column_name.Name = "Column_name";
			this.Column_name.ReadOnly = true;
			this.Column_name.Width = 170;
			this.Column_type.HeaderText = "TypeCode";
			this.Column_type.Name = "Column_type";
			this.Column_type.ReadOnly = true;
			this.Column_type.Width = 80;
			this.Column_TypeName.HeaderText = "TypeName";
			this.Column_TypeName.Name = "Column_TypeName";
			this.Column_TypeName.ReadOnly = true;
			this.Column_TypeName.Width = 80;
			this.Column_value.HeaderText = "Value";
			this.Column_value.Name = "Column_value";
			this.Column_value.ReadOnly = true;
			this.Column_value.Width = 298;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(3, 9);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(67, 17);
			this.label2.TabIndex = 0;
			this.label2.Text = "string tag:";
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(73, 6);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(569, 23);
			this.textBox4.TabIndex = 1;
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.splitContainer1);
			this.panel3.Location = new System.Drawing.Point(2, 77);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(976, 545);
			this.panel3.TabIndex = 10;
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.toolStripStatusLabel1 });
			this.statusStrip1.Location = new System.Drawing.Point(0, 625);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(981, 22);
			this.statusStrip1.TabIndex = 11;
			this.statusStrip1.Text = "statusStrip1";
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(188, 17);
			this.toolStripStatusLabel1.Text = "就绪，支持节点的正则表达式查询";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/9607929.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "CIP";
			this.userControlHead1.Size = new System.Drawing.Size(981, 32);
			this.userControlHead1.TabIndex = 12;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(981, 647);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.statusStrip1);
			base.Controls.Add(this.panel3);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormAllenBrandlyBrowser";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Allen Brandly Browser";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormAllenBrandlyBrowser_FormClosing);
			base.Load += new System.EventHandler(RedisBrowser_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel1.PerformLayout();
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			this.panel4.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			this.panel3.ResumeLayout(false);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
