using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.LogNet;
using HslCommunication.MQTT;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormMqttFileServer : HslFormContent
	{
		private Timer timer1s;

		private MqttServer mqttServer;

		private bool isStop = false;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private TextBox textBox2;

		private Label label3;

		private Panel panel2;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private UserControlHead userControlHead1;

		private CheckBox checkBox1;

		private Label label2;

		private Button button7;

		private Label label4;

		private Label label5;

		private TextBox textBox1;

		private Label label13;

		private DataGridView dataGridView1;

		private DataGridViewTextBoxColumn Column1;

		private DataGridViewTextBoxColumn Column2;

		private DataGridViewTextBoxColumn Column3;

		private DataGridViewTextBoxColumn Column4;

		private DataGridViewTextBoxColumn Column5;

		private DataGridViewTextBoxColumn Column6;

		private DataGridViewTextBoxColumn Column7;

		private DataGridViewTextBoxColumn Column8;

		private DataGridViewTextBoxColumn Column9;

		private DataGridViewTextBoxColumn Column10;

		private DataGridViewTextBoxColumn Column11;

		private Label label7;

		private Label label6;

		public FormMqttFileServer()
		{
			InitializeComponent();
		}

		private void FormClient_Load(object sender, EventArgs e)
		{
			panel2.Enabled = false;
			button2.Enabled = false;
			textBox1.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerFiles");
			Language(Program.Language);
			timer1s = new Timer();
			timer1s.Interval = 1000;
			timer1s.Tick += Timer1s_Tick;
			timer1s.Start();
		}

		private void Timer1s_Tick(object sender, EventArgs e)
		{
			if (mqttServer == null)
			{
				return;
			}
			label2.Text = "Online Count:" + mqttServer.OnlineCount;
			MqttFileMonitorItem[] monitorItemsSnapShoot = mqttServer.GetMonitorItemsSnapShoot();
			DataGridSpecifyRowCount(monitorItemsSnapShoot.Length);
			long num = 0L;
			long num2 = 0L;
			for (int i = 0; i < monitorItemsSnapShoot.Length; i++)
			{
				DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
				MqttFileMonitorItem mqttFileMonitorItem = monitorItemsSnapShoot[i];
				dataGridViewRow.Cells[0].Value = mqttFileMonitorItem.UniqueId.ToString();
				dataGridViewRow.Cells[1].Value = mqttFileMonitorItem.EndPoint.ToString();
				dataGridViewRow.Cells[2].Value = mqttFileMonitorItem.ClientId;
				dataGridViewRow.Cells[3].Value = mqttFileMonitorItem.UserName;
				dataGridViewRow.Cells[4].Value = mqttFileMonitorItem.Operate;
				dataGridViewRow.Cells[5].Value = mqttFileMonitorItem.Groups;
				dataGridViewRow.Cells[6].Value = mqttFileMonitorItem.FileName;
				dataGridViewRow.Cells[7].Value = mqttFileMonitorItem.StartTime.ToString();
				dataGridViewRow.Cells[8].Value = SoftBasic.GetSizeDescription(mqttFileMonitorItem.TotalSize);
				dataGridViewRow.Cells[9].Value = ((mqttFileMonitorItem.TotalSize == 0L) ? "-" : (SoftBasic.GetSizeDescription(mqttFileMonitorItem.SpeedSecond) + "/s"));
				dataGridViewRow.Cells[10].Value = ((mqttFileMonitorItem.TotalSize == 0L) ? "-" : (mqttFileMonitorItem.LastUpdateProgress * 100 / mqttFileMonitorItem.TotalSize + "%"));
				if (mqttFileMonitorItem.Operate == "Upload")
				{
					num += mqttFileMonitorItem.SpeedSecond;
				}
				if (mqttFileMonitorItem.Operate == "Download")
				{
					num2 += mqttFileMonitorItem.SpeedSecond;
				}
			}
			label6.Text = "Upload:" + SoftBasic.GetSizeDescription(num) + "/s";
			label7.Text = "Download:" + SoftBasic.GetSizeDescription(num2) + "/s";
		}

		private void DataGridSpecifyRowCount(int row)
		{
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

		private void Language(int language)
		{
			if (language != 1)
			{
				Text = "Mqtt File Server Test";
				label3.Text = "Port:";
				button1.Text = "Start";
				button2.Text = "Close";
				button4.Text = "Clear";
				label12.Text = "Receive:";
				label13.Text = "File Path:";
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				mqttServer = new MqttServer();
				mqttServer.ClientVerification += MqttServer_ClientVerification;
				mqttServer.FileOperateVerification += MqttServer_FileOperateVerification;
				mqttServer.ServerStart(int.Parse(textBox2.Text));
				mqttServer.LogNet = new LogNetSingle("");
				mqttServer.LogNet.SetMessageDegree(HslMessageDegree.INFO);
				mqttServer.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
				mqttServer.UseFileServer(textBox1.Text);
				mqttServer.OnFileChangedEvent += MqttServer_OnFileChangedEvent;
				button1.Enabled = false;
				button2.Enabled = true;
				panel2.Enabled = true;
				DemoUtils.ShowMessage("Start Success");
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Start Failed : " + ex.Message);
			}
		}

		private void MqttServer_OnFileChangedEvent(MqttSession session, MqttFileOperateInfo operateInfo)
		{
		}

		private OperateResult MqttServer_FileOperateVerification(MqttSession session, byte code, string[] groups, string[] fileNames)
		{
			if (string.IsNullOrEmpty(session.UserName) && code == 103)
			{
				return new OperateResult("Null Account Not Allowed operation");
			}
			if (session.UserName == "hsl" && (code == 102 || code == 103 || code == 101))
			{
				return new OperateResult("Account hsl not Allowed operation");
			}
			return OperateResult.CreateSuccessResult();
		}

		private int MqttServer_ClientVerification(MqttSession mqttSession, string clientId, string userName, string passwrod)
		{
			if (!checkBox1.Checked)
			{
				return 0;
			}
			if (userName == "admin" && passwrod == "123456")
			{
				return 0;
			}
			return 5;
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			Invoke((Action)delegate
			{
				textBox8.AppendText(e.HslMessage.ToString() + Environment.NewLine);
			});
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button1.Enabled = true;
			button2.Enabled = false;
			panel2.Enabled = false;
			mqttServer?.ServerClose();
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (!isStop)
			{
				button7.Text = "继续";
				isStop = true;
			}
			else
			{
				isStop = false;
				button7.Text = "暂停";
			}
		}

		public override void SaveXmlParameter(XElement element)
		{
			element.SetAttributeValue(DemoDeviceList.XmlPort, textBox2.Text);
			element.SetAttributeValue(DemoDeviceList.XmlFilePath, textBox1.Text);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			textBox2.Text = element.Attribute(DemoDeviceList.XmlPort).Value;
			textBox1.Text = element.Attribute(DemoDeviceList.XmlFilePath).Value;
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
		}

		private void FormMqttFileServer_FormClosing(object sender, FormClosingEventArgs e)
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label7 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column8 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column9 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column10 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Column11 = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.label5 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.button7 = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label13);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 72);
			this.panel1.TabIndex = 7;
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(95, 40);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(873, 23);
			this.textBox1.TabIndex = 8;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(7, 43);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(92, 17);
			this.label13.TabIndex = 7;
			this.label13.Text = "文件存储路径：";
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(163, 13);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(288, 21);
			this.checkBox1.TabIndex = 6;
			this.checkBox1.Text = "启用用户名和密码(用户名 admin  密码: 123456)";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(587, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭服务";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(478, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "启动服务";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox2.Location = new System.Drawing.Point(62, 11);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(75, 23);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "1883";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 14);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "端口号：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.label7);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.dataGridView1);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.button7);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.textBox8);
			this.panel2.Controls.Add(this.label12);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Location = new System.Drawing.Point(3, 111);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 532);
			this.panel2.TabIndex = 13;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(260, 182);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(70, 17);
			this.label7.TabIndex = 33;
			this.label7.Text = "Download:";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(105, 182);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(54, 17);
			this.label6.TabIndex = 32;
			this.label6.Text = "Upload:";
			this.dataGridView1.AllowUserToAddRows = false;
			this.dataGridView1.AllowUserToDeleteRows = false;
			dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
			this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Columns.AddRange(this.Column1, this.Column2, this.Column3, this.Column4, this.Column5, this.Column6, this.Column7, this.Column8, this.Column9, this.Column10, this.Column11);
			this.dataGridView1.Location = new System.Drawing.Point(3, 24);
			this.dataGridView1.MultiSelect = false;
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.ReadOnly = true;
			this.dataGridView1.RowHeadersVisible = false;
			this.dataGridView1.RowTemplate.Height = 23;
			this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dataGridView1.Size = new System.Drawing.Size(989, 150);
			this.dataGridView1.TabIndex = 31;
			this.Column1.HeaderText = "UniqueId";
			this.Column1.Name = "Column1";
			this.Column1.ReadOnly = true;
			this.Column1.Width = 86;
			this.Column2.HeaderText = "EndPoint";
			this.Column2.Name = "Column2";
			this.Column2.ReadOnly = true;
			this.Column2.Width = 84;
			this.Column3.HeaderText = "ClientId";
			this.Column3.Name = "Column3";
			this.Column3.ReadOnly = true;
			this.Column3.Width = 77;
			this.Column4.HeaderText = "UserName";
			this.Column4.Name = "Column4";
			this.Column4.ReadOnly = true;
			this.Column4.Width = 95;
			this.Column5.HeaderText = "Operate";
			this.Column5.Name = "Column5";
			this.Column5.ReadOnly = true;
			this.Column5.Width = 81;
			this.Column6.HeaderText = "Groups";
			this.Column6.Name = "Column6";
			this.Column6.ReadOnly = true;
			this.Column6.Width = 76;
			this.Column7.HeaderText = "FileName";
			this.Column7.Name = "Column7";
			this.Column7.ReadOnly = true;
			this.Column7.Width = 87;
			this.Column8.HeaderText = "StartTime";
			this.Column8.Name = "Column8";
			this.Column8.ReadOnly = true;
			this.Column8.Width = 88;
			this.Column9.HeaderText = "FileSize";
			this.Column9.Name = "Column9";
			this.Column9.ReadOnly = true;
			this.Column9.Width = 75;
			this.Column10.HeaderText = "Speed";
			this.Column10.Name = "Column10";
			this.Column10.ReadOnly = true;
			this.Column10.Width = 70;
			this.Column11.HeaderText = "Percent";
			this.Column11.Name = "Column11";
			this.Column11.ReadOnly = true;
			this.Column11.Width = 76;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(27, 4);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(93, 17);
			this.label5.TabIndex = 30;
			this.label5.Text = "Online Client：";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(8, 182);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(81, 17);
			this.label4.TabIndex = 29;
			this.label4.Text = "Total Speed:";
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(804, 180);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(91, 28);
			this.button7.TabIndex = 28;
			this.button7.Text = "暂停";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(874, 4);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(86, 17);
			this.label2.TabIndex = 27;
			this.label2.Text = "Online Count:";
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(62, 214);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(930, 313);
			this.textBox8.TabIndex = 18;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(8, 217);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(42, 17);
			this.label12.TabIndex = 19;
			this.label12.Text = "Log：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(901, 180);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(91, 28);
			this.button4.TabIndex = 17;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/12312952.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "MQTT-FILE";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 14;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormMqttFileServer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "MQTT 文件服务器";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormMqttFileServer_FormClosing);
			base.Load += new System.EventHandler(FormClient_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
