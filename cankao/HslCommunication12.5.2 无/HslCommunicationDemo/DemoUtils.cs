using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class DemoUtils
	{
		public static string DateTimeFormate = "yyyy-MM-dd HH:mm:ss.fff";

		public static string PlcDeviceName = "plc";

		public static string ModbusDeviceName = "modbus";

		public static readonly string IpAddressInputWrong = "IpAddress input wrong";

		public static readonly string PortInputWrong = "Port input wrong";

		public static readonly string SlotInputWrong = "Slot input wrong";

		public static readonly string BaudRateInputWrong = "Baud rate input wrong";

		public static readonly string DataBitsInputWrong = "Data bit input wrong";

		public static readonly string StopBitInputWrong = "Stop bit input wrong";

		public static bool PenelSizeFixed = false;

		public static void SetDeviveIp(TextBox textBox)
		{
		}

		public static void DataGridSpecifyRowCount(DataGridView dataGridView1, int row)
		{
			if (dataGridView1.AllowUserToAddRows)
			{
				row++;
			}
			if (dataGridView1.RowCount < row)
			{
				dataGridView1.Rows.Add(row - dataGridView1.RowCount);
			}
			else if (dataGridView1.RowCount > row)
			{
				int num = dataGridView1.RowCount - row;
				for (int i = 0; i < num; i++)
				{
					dataGridView1.Rows.RemoveAt(dataGridView1.Rows.Count - 1);
				}
			}
			if (row > 0)
			{
				dataGridView1.Rows[0].Cells[0].Selected = false;
			}
		}

		public static string GetRenderTimeText()
		{
			return DateTime.Now.ToString(FormMain.ShowMs ? "[HH:mm:ss.fff] " : "[HH:mm:ss] ");
		}

		public static void ReadResultRender<T>(OperateResult<T> result, string address, TextBox textBox)
		{
			if (result.IsSuccess)
			{
				if (result.Content is Array)
				{
					textBox.AppendText(GetRenderTimeText() + "[" + address + "] " + SoftBasic.ArrayFormat(result.Content) + Environment.NewLine);
				}
				else
				{
					textBox.AppendText(GetRenderTimeText() + $"[{address}] {result.Content}{Environment.NewLine}");
				}
			}
			else
			{
				ShowMessage(GetRenderTimeText() + "[" + address + "] Read Failed " + Environment.NewLine + "Reason：" + result.ToMessageShowString());
			}
		}

		public static void WriteResultRender(OperateResult result, string address)
		{
			if (result.IsSuccess)
			{
				ShowMessage(GetRenderTimeText() + "[" + address + "] Write Success");
				return;
			}
			ShowMessage(GetRenderTimeText() + "[" + address + "] Write Failed " + Environment.NewLine + " Reason：" + result.ToMessageShowString());
		}

		public static void WriteResultRender(OperateResult result)
		{
			if (result.IsSuccess)
			{
				ShowMessage(GetRenderTimeText() + "Success");
				return;
			}
			ShowMessage(GetRenderTimeText() + "Failed " + Environment.NewLine + " Reason：" + result.ToMessageShowString());
		}

		public static void WriteResultRender(Func<OperateResult> write, string address)
		{
			try
			{
				OperateResult operateResult = write();
				if (operateResult.IsSuccess)
				{
					ShowMessage(GetRenderTimeText() + "[" + address + "] Write Success");
					return;
				}
				ShowMessage(GetRenderTimeText() + "[" + address + "] Write Failed " + Environment.NewLine + " Reason：" + operateResult.ToMessageShowString());
			}
			catch (Exception ex)
			{
				ShowMessage("Data for writting is not corrent: " + ex.Message);
			}
		}

		public static void BulkReadRenderResult(IReadWriteNet readWrite, TextBox addTextBox, TextBox lengthTextBox, TextBox resultTextBox)
		{
			try
			{
				OperateResult<byte[]> operateResult = readWrite.Read(addTextBox.Text, ushort.Parse(lengthTextBox.Text));
				if (operateResult.IsSuccess)
				{
					resultTextBox.Text = "Result：" + SoftBasic.ByteToHexString(operateResult.Content);
				}
				else
				{
					ShowMessage("Read Failed：" + operateResult.ToMessageShowString());
				}
			}
			catch (Exception ex)
			{
				ShowMessage("Read Failed：" + ex.Message);
			}
		}

		public static string GetTimeCost(DateTime start)
		{
			TimeSpan timeSpan = DateTime.Now - start;
			if (timeSpan.TotalSeconds < 60.0)
			{
				return timeSpan.TotalMilliseconds.ToString("F0") + " ms";
			}
			if (timeSpan.TotalSeconds < 3600.0)
			{
				return timeSpan.TotalSeconds.ToString("F1") + " s";
			}
			if (timeSpan.TotalMinutes < 1440.0)
			{
				return timeSpan.TotalSeconds.ToString("F1") + " min";
			}
			return timeSpan.ToString();
		}

		public static void AppendTextBox(TextBox textBox, string key, string message, int maxKeyLength = -1)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] ");
			if (!string.IsNullOrEmpty(key))
			{
				stringBuilder.Append("[" + key + "] ");
			}
			stringBuilder.Append(message);
			stringBuilder.Append(Environment.NewLine);
			textBox.AppendText(stringBuilder.ToString());
		}

		public static OperateResult<int> ParseInt(string text)
		{
			try
			{
				if (text.StartsWith("0x") || text.StartsWith("0X"))
				{
					return OperateResult.CreateSuccessResult(Convert.ToInt32(text.Substring(2), 16));
				}
				return OperateResult.CreateSuccessResult(Convert.ToInt32(text));
			}
			catch (Exception ex)
			{
				return new OperateResult<int>("Convert Int Failed: " + ex.Message);
			}
		}

		public static void AddSpecialFunctionTab(TabControl tabControl, UserControl control, bool show = false, string title = null)
		{
			TabPage tabPage = new TabPage();
			tabPage.SuspendLayout();
			tabControl.Controls.Add(tabPage);
			tabPage.BackColor = SystemColors.Control;
			tabPage.Controls.Add(control);
			tabPage.Location = new Point(4, 26);
			tabPage.Name = "tabPage3";
			tabPage.Padding = new Padding(3);
			tabPage.Size = new Size(946, 252);
			tabPage.TabIndex = 0;
			if (string.IsNullOrEmpty(title))
			{
				tabPage.Text = ((Program.Language == 1) ? "特殊功能" : "Special Function");
			}
			else
			{
				tabPage.Text = title;
			}
			control.Dock = DockStyle.Fill;
			tabPage.ResumeLayout(false);
			if (show)
			{
				tabControl.SelectTab(tabPage);
			}
		}

		public static string[] GetEncodings()
		{
			return new string[7] { "ASCII", "Unicode", "Unicode-big", "UTF8", "UTF32", "ANSI", "GB2312" };
		}

		public static DialogResult ShowMessage(string message)
		{
			return FormMessageShow.ShowMessage(message);
		}

		public static DialogResult ShowMessage(string message, string title, MessageBoxButtons button)
		{
			return MessageBox.Show(message, title, button);
		}

		public static DialogResult ShowMessage(string message, string title, MessageBoxButtons button, MessageBoxIcon icon)
		{
			return MessageBox.Show(message, title, button, icon);
		}

		public static Encoding GetEncodingFromIndex(int index)
		{
			switch (index)
			{
			case 0:
				return Encoding.ASCII;
			case 1:
				return Encoding.Unicode;
			case 2:
				return Encoding.BigEndianUnicode;
			case 3:
				return Encoding.UTF8;
			case 4:
				return Encoding.UTF32;
			case 5:
				return Encoding.Default;
			case 6:
				return Encoding.GetEncoding("gb2312");
			default:
				return Encoding.ASCII;
			}
		}

		public static string GetEncodingTextFromIndex(int index)
		{
			switch (index)
			{
			case 0:
				return "Encoding.ASCII";
			case 1:
				return "Encoding.Unicode";
			case 2:
				return "Encoding.BigEndianUnicode";
			case 3:
				return "Encoding.UTF8";
			case 4:
				return "Encoding.UTF32";
			case 5:
				return "Encoding.Default";
			case 6:
				return "Encoding.GetEncoding( \"gb2312\" )";
			default:
				return "Encoding.ASCII";
			}
		}

		public static Icon ConvertToIcon(Image image)
		{
			if (image == null)
			{
				return null;
			}
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (MemoryStream memoryStream2 = new MemoryStream())
				{
					image.Save(memoryStream, ImageFormat.Png);
					using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream2))
					{
						binaryWriter.Write((short)0);
						binaryWriter.Write((short)1);
						binaryWriter.Write((short)1);
						binaryWriter.Write((byte)image.Width);
						binaryWriter.Write((byte)image.Height);
						binaryWriter.Write((byte)0);
						binaryWriter.Write((byte)0);
						binaryWriter.Write((short)0);
						binaryWriter.Write((short)32);
						binaryWriter.Write((int)memoryStream.Length);
						binaryWriter.Write(22);
						binaryWriter.Write(memoryStream.ToArray());
						binaryWriter.Flush();
						binaryWriter.Seek(0, SeekOrigin.Begin);
						return new Icon(memoryStream2);
					}
				}
			}
		}

		public static void SetPanelAnchor(Panel head, Panel content)
		{
			if (PenelSizeFixed)
			{
				head.Anchor = AnchorStyles.Top | AnchorStyles.Left;
				content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			}
		}

		public static void ClearDataGridView(DataGridView dataGridView)
		{
			if (MessageBox.Show((Program.Language == 1) ? "请确认是否删除全部行数据？" : "Are you sure delete all rows ?", "Delete Check", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				dataGridView.Rows.Clear();
			}
		}
	}
}
