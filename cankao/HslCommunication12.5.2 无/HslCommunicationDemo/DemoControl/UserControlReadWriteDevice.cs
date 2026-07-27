using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunicationDemo.PLC.Common;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo.DemoControl
{
	public class UserControlReadWriteDevice : UserControl
	{
		private List<UserControl> allControls = new List<UserControl>();

		private bool showReadWrite = true;

		private IContainer components = null;

		private UserControlReadWriteOp userControlReadWriteOp1;

		private TabControl tabControl1;

		private TabPage tabPage1;

		private BatchReadControl batchReadControl1;

		private TabPage tabPage2;

		private BatchReadControl batchReadControl2;

		private TabPage tabPage3;

		private TabPage tabPage4;

		private DataTableControl dataTableControl1;

		private StressTesting stressTesting1;

		private TabPage tabPage5;

		private DataExportControl dataExportControl1;

		private TabPage tabPage6;

		private DataSimulateControl dataSimulateControl1;

		private TabPage tabPage7;

		private DebugRemoteControl debugRemoteControl1;

		private PictureBox pictureBox1;

		public UserControlReadWriteOp ReadWriteOpControl => userControlReadWriteOp1;

		public BatchReadControl BatchRead => batchReadControl1;

		public BatchReadControl MessageRead => batchReadControl2;

		public UserControlReadWriteDevice()
		{
			InitializeComponent();
			allControls.Add(batchReadControl1);
			allControls.Add(batchReadControl2);
			allControls.Add(stressTesting1);
			allControls.Add(dataExportControl1);
			allControls.Add(debugRemoteControl1);
		}

		private void UserControlReadWriteDevice_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				tabPage1.Text = "Batch Read";
				tabPage2.Text = "Frame Message Read";
				tabPage3.Text = "Thread Test";
				tabPage4.Text = "Data Table";
				tabPage5.Text = "Data Export";
				tabPage6.Text = "Simulate";
				tabPage7.Text = "Debug Remote";
			}
			batchReadControl2.IsSourceReadMode = true;
		}

		public void SetEnable(bool enbale)
		{
			userControlReadWriteOp1.Enabled = enbale;
			pictureBox1.Enabled = enbale;
			if (!enbale)
			{
				dataSimulateControl1.Close();
				debugRemoteControl1.Close();
				dataTableControl1.Close();
			}
			for (int i = 0; i < allControls.Count; i++)
			{
				allControls[i].Enabled = enbale;
			}
		}

		public void SetReadWriteNet(IReadWriteNet readWrite, string address, bool isAsync = false, int strLength = 10)
		{
			userControlReadWriteOp1.SetReadWriteNet(readWrite, address, isAsync, strLength);
			stressTesting1.SetReadWriteNet(readWrite, address);
			dataTableControl1.SetReadWriteNet(readWrite);
			dataExportControl1.SetReadWriteNet(readWrite);
			dataSimulateControl1.SetReadWriteNet(readWrite);
			debugRemoteControl1.SetCommunication(readWrite as BinaryCommunication);
		}

		public void SetDeviceVariableName(string name)
		{
			batchReadControl1.SetVariableName(name);
			batchReadControl2.SetVariableName(name);
		}

		public void AddSpecialFunctionTab(UserControl control, bool show = false, string title = null)
		{
			if (control == null)
			{
				return;
			}
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

		private void stressTesting1_Load(object sender, EventArgs e)
		{
		}

		public void RemoveReadBatch()
		{
			tabControl1.TabPages.Remove(tabPage1);
		}

		public void RemoveReadMessage()
		{
			tabControl1.TabPages.Remove(tabPage2);
		}

		public void GetDataTable(XElement element)
		{
			element.RemoveNodes();
			dataTableControl1.GetDataTable(element);
			dataSimulateControl1.GetSimulateTable(element);
		}

		public int LoadDataTable(XElement element)
		{
			int result = dataTableControl1.LoadDataTable(element);
			dataSimulateControl1.LoadSimulateTable(element);
			return result;
		}

		public void SelectTabDataTable()
		{
			tabControl1.SelectTab(tabPage4);
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
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.batchReadControl1 = new HslCommunicationDemo.DemoControl.BatchReadControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.batchReadControl2 = new HslCommunicationDemo.DemoControl.BatchReadControl();
			this.tabPage7 = new System.Windows.Forms.TabPage();
			this.debugRemoteControl1 = new HslCommunicationDemo.DemoControl.DebugRemoteControl();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.stressTesting1 = new HslCommunicationDemo.PLC.Common.StressTesting();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.dataTableControl1 = new HslCommunicationDemo.DemoControl.DataTableControl();
			this.tabPage6 = new System.Windows.Forms.TabPage();
			this.dataSimulateControl1 = new HslCommunicationDemo.DemoControl.DataSimulateControl();
			this.tabPage5 = new System.Windows.Forms.TabPage();
			this.dataExportControl1 = new HslCommunicationDemo.DemoControl.DataExportControl();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.userControlReadWriteOp1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteOp();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage7.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.tabPage6.SuspendLayout();
			this.tabPage5.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage7);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Controls.Add(this.tabPage6);
			this.tabControl1.Controls.Add(this.tabPage5);
			this.tabControl1.Location = new System.Drawing.Point(0, 272);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(954, 282);
			this.tabControl1.TabIndex = 1;
			this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
			this.tabPage1.Controls.Add(this.batchReadControl1);
			this.tabPage1.Location = new System.Drawing.Point(4, 26);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(946, 252);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "批量读取";
			this.batchReadControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.batchReadControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.batchReadControl1.IsSourceReadMode = false;
			this.batchReadControl1.Location = new System.Drawing.Point(3, 3);
			this.batchReadControl1.Name = "batchReadControl1";
			this.batchReadControl1.Size = new System.Drawing.Size(940, 246);
			this.batchReadControl1.TabIndex = 0;
			this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
			this.tabPage2.Controls.Add(this.batchReadControl2);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(192, 74);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "报文读取";
			this.batchReadControl2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.batchReadControl2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.batchReadControl2.IsSourceReadMode = false;
			this.batchReadControl2.Location = new System.Drawing.Point(3, 3);
			this.batchReadControl2.Name = "batchReadControl2";
			this.batchReadControl2.Size = new System.Drawing.Size(186, 68);
			this.batchReadControl2.TabIndex = 1;
			this.tabPage7.Controls.Add(this.debugRemoteControl1);
			this.tabPage7.Location = new System.Drawing.Point(4, 26);
			this.tabPage7.Name = "tabPage7";
			this.tabPage7.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage7.Size = new System.Drawing.Size(946, 252);
			this.tabPage7.TabIndex = 6;
			this.tabPage7.Text = "远程调试";
			this.tabPage7.UseVisualStyleBackColor = true;
			this.debugRemoteControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.debugRemoteControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.debugRemoteControl1.Location = new System.Drawing.Point(3, 3);
			this.debugRemoteControl1.Name = "debugRemoteControl1";
			this.debugRemoteControl1.Size = new System.Drawing.Size(940, 246);
			this.debugRemoteControl1.TabIndex = 0;
			this.tabPage3.Controls.Add(this.stressTesting1);
			this.tabPage3.Location = new System.Drawing.Point(4, 22);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(192, 74);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "线程测试";
			this.tabPage3.UseVisualStyleBackColor = true;
			this.stressTesting1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.stressTesting1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.stressTesting1.Location = new System.Drawing.Point(3, 3);
			this.stressTesting1.Name = "stressTesting1";
			this.stressTesting1.Size = new System.Drawing.Size(186, 68);
			this.stressTesting1.TabIndex = 0;
			this.tabPage4.Controls.Add(this.dataTableControl1);
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(192, 74);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "点位变量";
			this.tabPage4.UseVisualStyleBackColor = true;
			this.dataTableControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataTableControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.dataTableControl1.Location = new System.Drawing.Point(3, 3);
			this.dataTableControl1.Name = "dataTableControl1";
			this.dataTableControl1.Size = new System.Drawing.Size(186, 68);
			this.dataTableControl1.TabIndex = 0;
			this.tabPage6.Controls.Add(this.dataSimulateControl1);
			this.tabPage6.Location = new System.Drawing.Point(4, 22);
			this.tabPage6.Name = "tabPage6";
			this.tabPage6.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage6.Size = new System.Drawing.Size(192, 74);
			this.tabPage6.TabIndex = 4;
			this.tabPage6.Text = "数据模拟";
			this.tabPage6.UseVisualStyleBackColor = true;
			this.dataSimulateControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataSimulateControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.dataSimulateControl1.Location = new System.Drawing.Point(3, 3);
			this.dataSimulateControl1.Name = "dataSimulateControl1";
			this.dataSimulateControl1.Size = new System.Drawing.Size(186, 68);
			this.dataSimulateControl1.TabIndex = 0;
			this.tabPage5.Controls.Add(this.dataExportControl1);
			this.tabPage5.Location = new System.Drawing.Point(4, 22);
			this.tabPage5.Name = "tabPage5";
			this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage5.Size = new System.Drawing.Size(192, 74);
			this.tabPage5.TabIndex = 5;
			this.tabPage5.Text = "数据导出";
			this.tabPage5.UseVisualStyleBackColor = true;
			this.dataExportControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataExportControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.dataExportControl1.Location = new System.Drawing.Point(3, 3);
			this.dataExportControl1.Name = "dataExportControl1";
			this.dataExportControl1.Size = new System.Drawing.Size(186, 68);
			this.dataExportControl1.TabIndex = 0;
			this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.pictureBox1.Cursor = System.Windows.Forms.Cursors.Hand;
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.shortcut_16xLG;
			this.pictureBox1.Location = new System.Drawing.Point(938, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(16, 16);
			this.pictureBox1.TabIndex = 2;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.Click += new System.EventHandler(pictureBox1_Click);
			this.userControlReadWriteOp1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.userControlReadWriteOp1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteOp1.Name = "userControlReadWriteOp1";
			this.userControlReadWriteOp1.Size = new System.Drawing.Size(954, 273);
			this.userControlReadWriteOp1.TabIndex = 0;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.pictureBox1);
			base.Controls.Add(this.tabControl1);
			base.Controls.Add(this.userControlReadWriteOp1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "UserControlReadWriteDevice";
			base.Size = new System.Drawing.Size(954, 554);
			base.Load += new System.EventHandler(UserControlReadWriteDevice_Load);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage7.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.tabPage6.ResumeLayout(false);
			this.tabPage5.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
		}
	}
}
