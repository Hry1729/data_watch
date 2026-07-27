using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;
using HslCommunication.LogNet;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.DemoControl
{
	public class UserControlReadWriteServer : UserControl
	{
		private System.Windows.Forms.Timer timerSecond;

		private List<UserControl> allControls = new List<UserControl>();

		private string regexFilter = string.Empty;

		private string deviceName = "server";

		private DeviceServer deviceServer;

		private System.Windows.Forms.Timer timer_save_file;

		private string timer_save_file_path = string.Empty;

		private List<RemoteConnectInfo> remoteConnectInfos = new List<RemoteConnectInfo>();

		private FileStream fs = null;

		private int timeInterval = 1000;

		private long tickCount = 0L;

		private Thread threadWrite;

		private bool writeSuspended = false;

		private bool writeCycle = false;

		private bool abort = false;

		private bool showReadWrite = true;

		private IContainer components = null;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private Label label15;

		private Label label16;

		private CheckBox checkBox1;

		private TextBox textBox1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private TabPage tabPage2;

		private BatchReadControl batchReadControl1;

		private TabPage tabPage3;

		private DataTableControl dataTableControl1;

		private Button button_data_import;

		private CheckBox checkBox_cycle;

		private Panel panel1;

		private Button button_import_abort;

		private TabPage tabPage4;

		private DataSimulateControl dataSimulateControl1;

		private Button button_log_filter;

		private TextBox textBox_log_filter;

		private Label label1;

		private TabPage tabPage5;

		private Button button8;

		private Button button9;

		private Button button1;

		private Label label2;

		private DataGridView dataGridView_dtu_sessions;

		private TextBox textBox_others_code;

		private Label label3;

		private DataGridViewTextBoxColumn Column_number;

		private DataGridViewTextBoxColumn Column_ip;

		private DataGridViewTextBoxColumn Column_port;

		private DataGridViewTextBoxColumn Column_dtu;

		private DataGridViewTextBoxColumn Column_time;

		private DataGridViewButtonColumn Column_close;

		private Panel panel2;

		private CheckBox checkBox2;

		private TextBox textBox2;

		private Label label4;

		private PictureBox pictureBox1;

		public UserControlReadWriteOp ReadWriteOpControl => userControlReadWriteOp1;

		public BatchReadControl BatchRead => batchReadControl1;

		public UserControlReadWriteServer()
		{
			InitializeComponent();
			allControls.Add(batchReadControl1);
		}

		public void SetReadWriteServerLog(DeviceServer dataServerBase)
		{
			dataServerBase.LogNet = new LogNetSingle("");
			dataServerBase.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
		}

		public void SetReadWriteServer(DeviceServer dataServerBase, string address, int strLength = 10, string deviceName = "server")
		{
			if (dataServerBase.LogNet == null)
			{
				SetReadWriteServerLog(dataServerBase);
			}
			deviceServer = dataServerBase;
			this.deviceName = deviceName;
			userControlReadWriteOp1.SetReadWriteNet(deviceServer, address, false, strLength);
			batchReadControl1.SetReadWriteNet(deviceServer, address, "");
			batchReadControl1.SetVariableName(deviceName);
			dataTableControl1.SetReadWriteNet(deviceServer);
			dataSimulateControl1.SetReadWriteNet(deviceServer);
			timerSecond?.Dispose();
			timerSecond = new System.Windows.Forms.Timer();
			timerSecond.Interval = 1000;
			timerSecond.Tick += TimerSecond_Tick;
			timerSecond.Start();
		}

		private void TimerSecond_Tick(object sender, EventArgs e)
		{
			if (deviceServer != null)
			{
				label15.Text = deviceServer.OnlineCount.ToString();
			}
			List<RemoteConnectInfo> list = remoteConnectInfos;
			if (list != null && list.Count > 0)
			{
				RenderConnectInfos();
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			if (!checkBox1.Checked && e.HslMessage.Degree != HslMessageDegree.FATAL)
			{
				return;
			}
			try
			{
				if (base.InvokeRequired)
				{
					Invoke(new Action<object, HslEventArgs>(LogNet_BeforeSaveToFile), sender, e);
					return;
				}
				if (textBox1.TextLength > 2000000)
				{
					textBox1.Clear();
				}
				if (string.IsNullOrEmpty(regexFilter))
				{
					textBox1.AppendText(e.HslMessage.ToString() + Environment.NewLine);
				}
				else if (Regex.IsMatch(e.HslMessage.ToString() + Environment.NewLine, regexFilter))
				{
					textBox1.AppendText(e.HslMessage.ToString() + Environment.NewLine);
				}
			}
			catch
			{
			}
		}

		private void button_log_filter_Click(object sender, EventArgs e)
		{
			regexFilter = textBox_log_filter.Text;
		}

		public void SetEnable(bool enbale)
		{
			userControlReadWriteOp1.Enabled = enbale;
			for (int i = 0; i < allControls.Count; i++)
			{
				allControls[i].Enabled = enbale;
			}
			checkBox1.Enabled = enbale;
			checkBox_cycle.Enabled = enbale;
			button_data_import.Enabled = enbale;
			button_import_abort.Enabled = enbale;
			button1.Enabled = enbale;
			button8.Enabled = enbale;
			button9.Enabled = enbale;
			textBox1.Enabled = enbale;
			textBox_log_filter.Enabled = enbale;
			button_log_filter.Enabled = enbale;
			textBox_others_code.Enabled = enbale;
		}

		public void Close()
		{
			userControlReadWriteOp1.Close();
			dataTableControl1.Close();
			dataSimulateControl1.Close();
			if (timerSecond != null && timerSecond.Enabled)
			{
				timerSecond.Stop();
				timerSecond.Dispose();
				timerSecond = null;
			}
			if (timer_save_file != null && timer_save_file.Enabled)
			{
				timer_save_file.Stop();
				timer_save_file.Dispose();
				timer_save_file = null;
			}
		}

		private void UserControlReadWriteServer_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				button8.Text = "Load";
				button9.Text = "Save";
				checkBox1.Text = "Display log data?";
				label16.Text = "Client-Online:";
				button1.Text = "Connecting Alien client";
				label1.Text = "Regex:";
				button_log_filter.Text = "regex filter";
				checkBox_cycle.Text = "Cycle?";
				button_data_import.Text = "DataImport";
				button_import_abort.Text = "ImportAbort";
				tabPage1.Text = "LogInfo";
				tabPage2.Text = "Batch Read";
				tabPage3.Text = "Data Table";
				tabPage4.Text = "Simulate";
				tabPage5.Text = "Others";
				label2.Text = "Sessions:";
				button1.Text = "Connect Dtu Server";
				button9.Text = "Save Data to file";
				button8.Text = "Load Data from file";
				label4.Text = "Interval(sec):";
				checkBox2.Text = "Save Cycle?";
			}
			textBox1.Height = tabPage1.Height - 36;
		}

		private void button8_Click(object sender, EventArgs e)
		{
			if (deviceServer == null)
			{
				return;
			}
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				if (File.Exists(openFileDialog.FileName))
				{
					try
					{
						deviceServer.LoadDataPool(openFileDialog.FileName);
						DemoUtils.ShowMessage("Load data finish");
						textBox_others_code.Text = "server.LoadDataPool( @\"" + openFileDialog.FileName + "\" );";
						return;
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage("Load failed: " + ex.Message);
						return;
					}
				}
				DemoUtils.ShowMessage("File[" + openFileDialog.FileName + "] is not exist！");
			}
		}

		private void button9_Click(object sender, EventArgs e)
		{
			if (deviceServer == null)
			{
				return;
			}
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.FileName = "123.txt";
				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					try
					{
						deviceServer.SaveDataPool(saveFileDialog.FileName);
						DemoUtils.ShowMessage("Save file finish!");
						textBox_others_code.Text = "server.SaveDataPool( @\"" + saveFileDialog.FileName + "\" );";
						return;
					}
					catch (Exception ex)
					{
						DemoUtils.ShowMessage("Save failed: " + ex.Message);
						return;
					}
				}
			}
		}

		private void checkBox2_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox2.Checked)
			{
				int num = Convert.ToInt32(textBox2.Text);
				using (SaveFileDialog saveFileDialog = new SaveFileDialog())
				{
					saveFileDialog.FileName = "123.txt";
					if (saveFileDialog.ShowDialog() == DialogResult.OK)
					{
						timer_save_file_path = saveFileDialog.FileName;
						if (timer_save_file != null)
						{
							timer_save_file.Stop();
						}
						timer_save_file = new System.Windows.Forms.Timer();
						timer_save_file.Interval = num * 1000;
						timer_save_file.Tick += Timer_save_file_Tick;
						timer_save_file.Start();
						Timer_save_file_Tick(timer_save_file, e);
					}
					else
					{
						checkBox2.Checked = false;
					}
					return;
				}
			}
			if (timer_save_file != null)
			{
				timer_save_file.Stop();
				timer_save_file.Dispose();
				timer_save_file = null;
			}
			timer_save_file_path = string.Empty;
		}

		private void Timer_save_file_Tick(object sender, EventArgs e)
		{
			if (!string.IsNullOrEmpty(timer_save_file_path))
			{
				try
				{
					deviceServer.SaveDataPool(timer_save_file_path);
					textBox_others_code.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Save file: " + timer_save_file_path + "  Success!";
				}
				catch (Exception ex)
				{
					checkBox2.Checked = false;
					DemoUtils.ShowMessage("Save failed: " + ex.Message);
				}
			}
		}

		private void RenderConnectInfos()
		{
			DemoUtils.DataGridSpecifyRowCount(dataGridView_dtu_sessions, remoteConnectInfos.Count);
			for (int i = 0; i < remoteConnectInfos.Count; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView_dtu_sessions.Rows[i];
				RemoteConnectInfo remoteConnectInfo = remoteConnectInfos[i];
				dataGridViewRow.Cells[0].Value = i + 1;
				dataGridViewRow.Cells[1].Value = remoteConnectInfo.EndPoint.ToString();
				dataGridViewRow.Cells[2].Value = remoteConnectInfo.Status.ToString();
				dataGridViewRow.Cells[3].Value = remoteConnectInfo.DtuId;
				dataGridViewRow.Cells[4].Value = remoteConnectInfo.Session?.OnlineTime.ToString();
				dataGridViewRow.Cells[5].Value = "Close";
				dataGridViewRow.Tag = remoteConnectInfo;
			}
		}

		private void dataGridView_dtu_sessions_CellContentClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < dataGridView_dtu_sessions.RowCount && dataGridView_dtu_sessions.Rows[e.RowIndex].Tag is RemoteConnectInfo remoteConnectInfo)
			{
				remoteConnectInfo.Close();
				remoteConnectInfos.Remove(remoteConnectInfo);
				RenderConnectInfos();
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			using (FormInputAlien formInputAlien = new FormInputAlien())
			{
				if (formInputAlien.ShowDialog() == DialogResult.OK)
				{
					RemoteConnectInfo remoteConnectInfo = null;
					if (formInputAlien.UseHslDtuServer)
					{
						remoteConnectInfo = deviceServer.GetCommunicationServer().ConnectHslAlientClient(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.DTU, formInputAlien.Pwd, formInputAlien.NeedAckDtuResult);
						textBox_others_code.Text = $"RemoteConnectInfo remoteConnectInfo = {deviceName}.GetCommunicationServer( ).ConnectHslAlientClient( \"{formInputAlien.IpAddress}\", {formInputAlien.Port}, \"{formInputAlien.DTU}\", \"{formInputAlien.Pwd}\", {formInputAlien.NeedAckDtuResult} );" + "\r\n// 关闭连接的话 remoteConnectInfo.Close( );";
					}
					else
					{
						remoteConnectInfo = deviceServer.GetCommunicationServer().ConnectRemoteServer(formInputAlien.IpAddress, formInputAlien.Port, formInputAlien.CustomizeDTU);
						textBox_others_code.Text = $"RemoteConnectInfo remoteConnectInfo = {deviceName}.GetCommunicationServer( ).ConnectRemoteServer( \"{formInputAlien.IpAddress}\", {formInputAlien.Port}, \"{formInputAlien.CustomizeDTU.ToHexString()}\".ToHexBytes( ) );" + "\r\n// 关闭连接的话 remoteConnectInfo.Close( );";
					}
					if (remoteConnectInfo != null)
					{
						remoteConnectInfos.Add(remoteConnectInfo);
						RenderConnectInfos();
					}
					DemoUtils.ShowMessage((Program.Language == 1) ? "增加一个新的DTU连接成功" : "Add Connection success");
				}
			}
		}

		public void AddSpecialFunctionTab(UserControl control, bool show = false, string title = null)
		{
			DemoUtils.AddSpecialFunctionTab(tabControl1, control, show, title);
			if (control is AddressExampleControl)
			{
				return;
			}
			allControls.Add(control);
			CodeExampleControl codeExampleControl = control as CodeExampleControl;
			if (codeExampleControl != null)
			{
				userControlReadWriteOp1.MethodCodeClick += delegate(object sender, string e)
				{
					codeExampleControl.ReaderReadCode(e);
				};
			}
		}

		public void GetDataTable(XElement element)
		{
			element.RemoveNodes();
			dataTableControl1.GetDataTable(element);
			dataSimulateControl1.GetSimulateTable(element);
		}

		public int LoadDataTable(XElement element)
		{
			dataSimulateControl1.LoadSimulateTable(element);
			return dataTableControl1.LoadDataTable(element);
		}

		public void SelectTabDataTable()
		{
			tabControl1.SelectTab(tabPage3);
		}

		private void button_data_import_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Multiselect = false;
				openFileDialog.Filter = "数据文件(*.hsldata)|*.hsldata";
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				try
				{
					fs = new FileStream(openFileDialog.FileName, FileMode.Open);
					byte[] array = new byte[100];
					fs.Read(array, 0, 100);
					if (Encoding.ASCII.GetString(array, 0, 7) != "HSLDemo")
					{
						DemoUtils.ShowMessage("Not the hsl data file! ");
						fs.Dispose();
						return;
					}
					timeInterval = BitConverter.ToInt32(array, 26);
					threadWrite = new Thread(ThreadReadFile);
					tickCount = BitConverter.ToInt64(array, 30);
					button_data_import.Enabled = false;
					button_import_abort.Enabled = true;
					abort = false;
					threadWrite.Start();
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Data import failed: " + ex.Message);
				}
			}
		}

		private int GetUInt32(Stream stream, out int value)
		{
			byte[] array = new byte[4];
			int result = stream.Read(array, 0, 4);
			value = BitConverter.ToInt32(array, 0);
			return result;
		}

		private int GetString(Stream stream, int len, out string value)
		{
			byte[] array = new byte[len];
			int result = stream.Read(array, 0, len);
			value = Encoding.UTF8.GetString(array);
			return result;
		}

		private void ThreadReadFile()
		{
			DateTime dateTime = DateTime.Now.AddMilliseconds(-timeInterval);
			do
			{
				fs.Position = 100L;
				int num = 0;
				while (true)
				{
					if (timeInterval > 0)
					{
						Thread.Sleep(timeInterval);
					}
					if (abort)
					{
						break;
					}
					if (writeSuspended)
					{
						continue;
					}
					if (GetUInt32(fs, out var value) <= 0)
					{
						break;
					}
					num++;
					StringBuilder stringBuilder = new StringBuilder($"Write address[{num}/{tickCount}]:");
					int value2;
					string value3;
					int value4;
					for (int i = 0; i < value && GetUInt32(fs, out value2) > 0 && GetString(fs, value2, out value3) > 0 && GetUInt32(fs, out value4) > 0; i++)
					{
						switch (fs.ReadByte())
						{
						case 0:
						{
							if (GetUInt32(fs, out var value6) <= 0)
							{
								break;
							}
							byte[] array2 = new byte[value6];
							if (fs.Read(array2, 0, value6) != value6)
							{
								break;
							}
							OperateResult operateResult2 = deviceServer.Write(value3, array2);
							if (operateResult2.IsSuccess)
							{
								stringBuilder.Append($"{value3}[len:{array2.Length}]");
								if (i != value - 1)
								{
									stringBuilder.Append(";");
								}
							}
							else
							{
								deviceServer.LogNet?.WriteError(deviceServer.ToString(), "Write address: " + value3 + " failed: " + operateResult2.Message);
							}
							continue;
						}
						case 1:
						{
							if (GetUInt32(fs, out var value5) <= 0)
							{
								break;
							}
							int num2 = (value5 + 7) / 8;
							byte[] array = new byte[num2];
							if (fs.Read(array, 0, num2) != num2)
							{
								break;
							}
							OperateResult operateResult = deviceServer.Write(value3, array.ToBoolArray().SelectBegin(value5));
							if (operateResult.IsSuccess)
							{
								stringBuilder.Append($"{value3}[len-bool:{value5}]");
								if (i != value - 1)
								{
									stringBuilder.Append(";");
								}
							}
							else
							{
								deviceServer.LogNet?.WriteError(deviceServer.ToString(), "Write address[Bool]: " + value3 + " failed: " + operateResult.Message);
							}
							continue;
						}
						default:
							continue;
						}
						break;
					}
					deviceServer.LogNet?.WriteInfo(deviceServer.ToString(), stringBuilder.ToString());
				}
			}
			while (writeCycle && !abort);
			deviceServer.LogNet?.WriteInfo("Finish!");
			fs.Close();
			fs.Dispose();
			Invoke((Action)delegate
			{
				button_data_import.Enabled = true;
			});
		}

		private void checkBox_cycle_CheckedChanged(object sender, EventArgs e)
		{
			writeCycle = checkBox_cycle.Checked;
		}

		private void button_import_abort_Click(object sender, EventArgs e)
		{
			abort = true;
			button_data_import.Enabled = true;
			button_import_abort.Enabled = false;
		}

		private void label16_Click(object sender, EventArgs e)
		{
			using (FormPipeSessionList formPipeSessionList = new FormPipeSessionList())
			{
				formPipeSessionList.SetPipeSessions(deviceServer.GetCommunicationServer());
				formPipeSessionList.ShowDialog();
			}
		}

		private void pictureBox1_Click(object sender, EventArgs e)
		{
			if (showReadWrite)
			{
				showReadWrite = false;
				pictureBox1.Image = Resources.view_16xLG;
				userControlReadWriteOp1.Visible = false;
				tabControl1.Location = new Point(0, 0);
				tabControl1.Size = new Size(base.Width, base.Height);
			}
			else
			{
				showReadWrite = true;
				pictureBox1.Image = Resources.shortcut_16xLG;
				userControlReadWriteOp1.Visible = true;
				tabControl1.Location = new Point(0, 272);
				tabControl1.Size = new Size(base.Width, base.Height - 272);
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
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.button_log_filter = new System.Windows.Forms.Button();
			this.textBox_log_filter = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button_import_abort = new System.Windows.Forms.Button();
			this.checkBox_cycle = new System.Windows.Forms.CheckBox();
			this.button_data_import = new System.Windows.Forms.Button();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.batchReadControl1 = new HslCommunicationDemo.DemoControl.BatchReadControl();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.dataTableControl1 = new HslCommunicationDemo.DemoControl.DataTableControl();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.dataSimulateControl1 = new HslCommunicationDemo.DemoControl.DataSimulateControl();
			this.tabPage5 = new System.Windows.Forms.TabPage();
			this.panel2 = new System.Windows.Forms.Panel();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_others_code = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.button9 = new System.Windows.Forms.Button();
			this.dataGridView_dtu_sessions = new System.Windows.Forms.DataGridView();
			this.Column_number = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_ip = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_port = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_dtu = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_time = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column_close = new System.Windows.Forms.DataGridViewButtonColumn();
			this.button8 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.panel1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.tabPage5.SuspendLayout();
			this.panel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView_dtu_sessions).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.label15.AutoSize = true;
			this.label15.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label15.Font = new System.Drawing.Font("微软雅黑", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label15.ForeColor = System.Drawing.Color.Blue;
			this.label15.Location = new System.Drawing.Point(291, 5);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(19, 21);
			this.label15.TabIndex = 26;
			this.label15.Text = "0";
			this.label15.Click += new System.EventHandler(label16_Click);
			this.label16.AutoSize = true;
			this.label16.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label16.Font = new System.Drawing.Font("微软雅黑", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label16.ForeColor = System.Drawing.Color.Blue;
			this.label16.Location = new System.Drawing.Point(170, 5);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(106, 21);
			this.label16.TabIndex = 25;
			this.label16.Text = "在线客户端：";
			this.label16.Click += new System.EventHandler(label16_Click);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Checked = true;
			this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox1.Location = new System.Drawing.Point(6, 6);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(123, 21);
			this.checkBox1.TabIndex = 24;
			this.checkBox1.Text = "显示运行日志数据";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(6, 33);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(936, 160);
			this.textBox1.TabIndex = 23;
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Controls.Add(this.tabPage5);
			this.tabControl1.Location = new System.Drawing.Point(2, 272);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(956, 233);
			this.tabControl1.TabIndex = 31;
			this.tabPage1.Controls.Add(this.button_log_filter);
			this.tabPage1.Controls.Add(this.textBox_log_filter);
			this.tabPage1.Controls.Add(this.label1);
			this.tabPage1.Controls.Add(this.panel1);
			this.tabPage1.Controls.Add(this.checkBox1);
			this.tabPage1.Controls.Add(this.label16);
			this.tabPage1.Controls.Add(this.textBox1);
			this.tabPage1.Controls.Add(this.label15);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(948, 203);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "日志信息";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.button_log_filter.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_log_filter.Location = new System.Drawing.Point(850, 3);
			this.button_log_filter.Name = "button_log_filter";
			this.button_log_filter.Size = new System.Drawing.Size(95, 28);
			this.button_log_filter.TabIndex = 36;
			this.button_log_filter.Text = "设置过滤条件";
			this.button_log_filter.UseVisualStyleBackColor = true;
			this.button_log_filter.Click += new System.EventHandler(button_log_filter_Click);
			this.textBox_log_filter.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_log_filter.Location = new System.Drawing.Point(723, 5);
			this.textBox_log_filter.Name = "textBox_log_filter";
			this.textBox_log_filter.Size = new System.Drawing.Size(121, 23);
			this.textBox_log_filter.TabIndex = 35;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(673, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 17);
			this.label1.TabIndex = 34;
			this.label1.Text = "正则：";
			this.panel1.Controls.Add(this.button_import_abort);
			this.panel1.Controls.Add(this.checkBox_cycle);
			this.panel1.Controls.Add(this.button_data_import);
			this.panel1.Location = new System.Drawing.Point(361, 2);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(303, 30);
			this.panel1.TabIndex = 33;
			this.button_import_abort.Enabled = false;
			this.button_import_abort.Location = new System.Drawing.Point(186, 1);
			this.button_import_abort.Name = "button_import_abort";
			this.button_import_abort.Size = new System.Drawing.Size(108, 28);
			this.button_import_abort.TabIndex = 33;
			this.button_import_abort.Text = "终止导入";
			this.button_import_abort.UseVisualStyleBackColor = true;
			this.button_import_abort.Click += new System.EventHandler(button_import_abort_Click);
			this.checkBox_cycle.AutoSize = true;
			this.checkBox_cycle.Location = new System.Drawing.Point(10, 5);
			this.checkBox_cycle.Name = "checkBox_cycle";
			this.checkBox_cycle.Size = new System.Drawing.Size(51, 21);
			this.checkBox_cycle.TabIndex = 32;
			this.checkBox_cycle.Text = "循环";
			this.checkBox_cycle.UseVisualStyleBackColor = true;
			this.checkBox_cycle.CheckedChanged += new System.EventHandler(checkBox_cycle_CheckedChanged);
			this.button_data_import.Location = new System.Drawing.Point(72, 1);
			this.button_data_import.Name = "button_data_import";
			this.button_data_import.Size = new System.Drawing.Size(108, 28);
			this.button_data_import.TabIndex = 31;
			this.button_data_import.Text = "数据导入";
			this.button_data_import.UseVisualStyleBackColor = true;
			this.button_data_import.Click += new System.EventHandler(button_data_import_Click);
			this.tabPage2.Controls.Add(this.batchReadControl1);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(948, 207);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "批量读取";
			this.tabPage2.UseVisualStyleBackColor = true;
			this.batchReadControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.batchReadControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.batchReadControl1.IsSourceReadMode = false;
			this.batchReadControl1.Location = new System.Drawing.Point(3, 3);
			this.batchReadControl1.Name = "batchReadControl1";
			this.batchReadControl1.Size = new System.Drawing.Size(942, 201);
			this.batchReadControl1.TabIndex = 1;
			this.tabPage3.Controls.Add(this.dataTableControl1);
			this.tabPage3.Location = new System.Drawing.Point(4, 22);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(948, 207);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "点位变量";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.dataTableControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataTableControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.dataTableControl1.Location = new System.Drawing.Point(3, 3);
			this.dataTableControl1.Name = "dataTableControl1";
			this.dataTableControl1.Size = new System.Drawing.Size(942, 201);
			this.dataTableControl1.TabIndex = 1;
			this.tabPage4.Controls.Add(this.dataSimulateControl1);
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(948, 207);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "数据模拟";
			this.tabPage4.UseVisualStyleBackColor = true;
			this.dataSimulateControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataSimulateControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.dataSimulateControl1.Location = new System.Drawing.Point(3, 3);
			this.dataSimulateControl1.Name = "dataSimulateControl1";
			this.dataSimulateControl1.Size = new System.Drawing.Size(942, 201);
			this.dataSimulateControl1.TabIndex = 0;
			this.tabPage5.Controls.Add(this.panel2);
			this.tabPage5.Location = new System.Drawing.Point(4, 22);
			this.tabPage5.Name = "tabPage5";
			this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage5.Size = new System.Drawing.Size(948, 207);
			this.tabPage5.TabIndex = 5;
			this.tabPage5.Text = "其他功能";
			this.tabPage5.UseVisualStyleBackColor = true;
			this.panel2.Controls.Add(this.checkBox2);
			this.panel2.Controls.Add(this.textBox2);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.textBox_others_code);
			this.panel2.Controls.Add(this.button1);
			this.panel2.Controls.Add(this.label3);
			this.panel2.Controls.Add(this.button9);
			this.panel2.Controls.Add(this.dataGridView_dtu_sessions);
			this.panel2.Controls.Add(this.button8);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(3, 3);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(942, 201);
			this.panel2.TabIndex = 41;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Location = new System.Drawing.Point(532, 9);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(75, 21);
			this.checkBox2.TabIndex = 43;
			this.checkBox2.Text = "定时存储";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.checkBox2.CheckedChanged += new System.EventHandler(checkBox2_CheckedChanged);
			this.textBox2.Location = new System.Drawing.Point(421, 7);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(105, 23);
			this.textBox2.TabIndex = 42;
			this.textBox2.Text = "3600";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(3, 12);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 37;
			this.label2.Text = "连接列表：";
			this.textBox_others_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_others_code.Location = new System.Drawing.Point(60, 132);
			this.textBox_others_code.Multiline = true;
			this.textBox_others_code.Name = "textBox_others_code";
			this.textBox_others_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_others_code.Size = new System.Drawing.Size(879, 67);
			this.textBox_others_code.TabIndex = 40;
			this.button1.Location = new System.Drawing.Point(100, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(137, 28);
			this.button1.TabIndex = 34;
			this.button1.Text = "连接异形服务器";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3, 134);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(51, 17);
			this.label3.TabIndex = 39;
			this.label3.Text = "Code：";
			this.button9.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button9.Location = new System.Drawing.Point(648, 5);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(138, 28);
			this.button9.TabIndex = 36;
			this.button9.Text = "存储数据到文件";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.dataGridView_dtu_sessions.AllowUserToAddRows = false;
			this.dataGridView_dtu_sessions.AllowUserToDeleteRows = false;
			this.dataGridView_dtu_sessions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView_dtu_sessions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView_dtu_sessions.Columns.AddRange(this.Column_number, this.Column_ip, this.Column_port, this.Column_dtu, this.Column_time, this.Column_close);
			this.dataGridView_dtu_sessions.Location = new System.Drawing.Point(3, 38);
			this.dataGridView_dtu_sessions.Name = "dataGridView_dtu_sessions";
			this.dataGridView_dtu_sessions.ReadOnly = true;
			this.dataGridView_dtu_sessions.RowTemplate.Height = 23;
			this.dataGridView_dtu_sessions.Size = new System.Drawing.Size(936, 90);
			this.dataGridView_dtu_sessions.TabIndex = 38;
			this.dataGridView_dtu_sessions.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(dataGridView_dtu_sessions_CellContentClick);
			this.Column_number.HeaderText = "Number";
			this.Column_number.Name = "Column_number";
			this.Column_number.ReadOnly = true;
			this.Column_ip.HeaderText = "EndPoint";
			this.Column_ip.Name = "Column_ip";
			this.Column_ip.ReadOnly = true;
			this.Column_ip.Width = 220;
			this.Column_port.HeaderText = "Status";
			this.Column_port.Name = "Column_port";
			this.Column_port.ReadOnly = true;
			this.Column_dtu.HeaderText = "DTU";
			this.Column_dtu.Name = "Column_dtu";
			this.Column_dtu.ReadOnly = true;
			this.Column_dtu.Width = 200;
			this.Column_time.HeaderText = "ConnectTime";
			this.Column_time.Name = "Column_time";
			this.Column_time.ReadOnly = true;
			this.Column_time.Width = 180;
			this.Column_close.HeaderText = "Close";
			this.Column_close.Name = "Column_close";
			this.Column_close.ReadOnly = true;
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(792, 5);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(147, 28);
			this.button8.TabIndex = 35;
			this.button8.Text = "从文件加载数据";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(341, 11);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(55, 17);
			this.label4.TabIndex = 41;
			this.label4.Text = "间隔(秒):";
			this.userControlReadWriteOp1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(2, 1);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(956, 270);
			this.userControlReadWriteOp1.TabIndex = 0;
			this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.pictureBox1.Cursor = System.Windows.Forms.Cursors.Hand;
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.shortcut_16xLG;
			this.pictureBox1.Location = new System.Drawing.Point(945, -1);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(16, 16);
			this.pictureBox1.TabIndex = 32;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.Click += new System.EventHandler(pictureBox1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.pictureBox1);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.userControlReadWriteOp1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "UserControlReadWriteServer";
			base.Size = new System.Drawing.Size(960, 505);
			base.Load += new System.EventHandler(UserControlReadWriteServer_Load);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.tabPage2.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.tabPage5.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView_dtu_sessions).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
