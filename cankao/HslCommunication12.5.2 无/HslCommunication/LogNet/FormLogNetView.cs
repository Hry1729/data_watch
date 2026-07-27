using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HslCommunication.BasicFramework;

namespace HslCommunication.LogNet
{
	/// <summary>
	/// 日志查看器的窗口类，用于分析统计日志数据，实例化的时候可以直接日志文件，然后直接显示出文件内容出来，然后可以根据日志的等级，或是关键字进行搜索信息<br />
	/// The window class of the log viewer is used to analyze the statistical log data, and when instantiating, you can directly log the file, 
	/// and then directly display the file content, and then you can search for information according to the level of the log or keyword
	/// </summary>
	public class FormLogNetView : Form
	{
		private string logFilePath = string.Empty;

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private IContainer components = null;

		private LogNetAnalysisControl logNetAnalysisControl1;

		private Label label1;

		private TextBox textBox1;

		private Button userButton1;

		private StatusStrip statusStrip1;

		private ToolStripStatusLabel toolStripStatusLabel1;

		/// <summary>
		/// 获取或设置当前日志选择窗口的默认的路径信息<br />
		/// Get or set the default path information of the current log selection window
		/// </summary>
		public string OpenDialogDefaultPath { get; set; }

		/// <summary>
		/// 实例化一个默认的日志查看器的窗口<br />
		/// Instantiates a default log viewer window
		/// </summary>
		public FormLogNetView()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 指定一个日志路径实例化一个日志查看界面
		/// </summary>
		/// <param name="log">日志的路径</param>
		public FormLogNetView(string log)
		{
			InitializeComponent();
			logFilePath = log;
		}

		/// <inheritdoc />
		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			if (!string.IsNullOrEmpty(logFilePath))
			{
				textBox1.Text = logFilePath;
				DealWithFileName(logFilePath);
			}
			if (!string.IsNullOrEmpty(OpenDialogDefaultPath) && string.IsNullOrEmpty(logFilePath))
			{
				textBox1.Text = OpenDialogDefaultPath;
			}
		}

		private void FormLogNetView_Load(object sender, EventArgs e)
		{
			if(true)
			{
				toolStripStatusLabel1.Visible = false;
			}
			label1.Text = StringResources.Language.LogNetFilePath;
			userButton1.Text = StringResources.Language.LogNetFileSelect;
			Text = StringResources.Language.LogNetViewer;
		}

		private void userButton1_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (!string.IsNullOrEmpty(OpenDialogDefaultPath))
				{
					openFileDialog.InitialDirectory = OpenDialogDefaultPath;
				}
				openFileDialog.Filter = StringResources.Language.LogNetFilter;
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					textBox1.Text = openFileDialog.FileName;
					DealWithFileName(openFileDialog.FileName);
				}
			}
		}

		private void DealWithFileName(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
			{
				return;
			}
			if (!File.Exists(fileName))
			{
				MessageBox.Show(StringResources.Language.FileNotExist);
				return;
			}
			try
			{
				using (StreamReader streamReader = new StreamReader(fileName, Encoding.UTF8))
				{
					try
					{
						logNetAnalysisControl1.SetLogNetSource(streamReader.ReadToEnd());
					}
					catch (Exception ex)
					{
						SoftBasic.ShowExceptionMessage(ex);
					}
				}
			}
			catch (Exception ex2)
			{
				SoftBasic.ShowExceptionMessage(ex2);
			}
		}

		private void logNetAnalysisControl1_Load(object sender, EventArgs e)
		{
		}

		private void toolStripStatusLabel2_Click(object sender, EventArgs e)
		{
			try
			{
				Process.Start("explorer.exe", "https://github.com/dathlin/C-S-");
			}
			catch
			{
			}
		}

		private void textBox1_KeyDown(object sender, KeyEventArgs e)
		{
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunication.LogNet.FormLogNetView));
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.userButton1 = new System.Windows.Forms.Button();
			this.logNetAnalysisControl1 = new HslCommunication.LogNet.LogNetAnalysisControl();
			this.statusStrip1.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(9, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "文件路径：";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Font = new System.Drawing.Font("宋体", 10.5f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(83, 6);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(613, 23);
			this.textBox1.TabIndex = 2;
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.toolStripStatusLabel1 });
			this.statusStrip1.Location = new System.Drawing.Point(0, 555);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(824, 22);
			this.statusStrip1.TabIndex = 4;
			this.statusStrip1.Text = "statusStrip1";
			this.toolStripStatusLabel1.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(248, 17);
			this.toolStripStatusLabel1.Text = "本日志查看器由HslCommunication提供支持";
			this.userButton1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.userButton1.BackColor = System.Drawing.Color.Transparent;
			this.userButton1.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton1.Location = new System.Drawing.Point(717, 6);
			this.userButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton1.Name = "userButton1";
			this.userButton1.Size = new System.Drawing.Size(95, 25);
			this.userButton1.TabIndex = 3;
			this.userButton1.Text = "文件选择";
			this.userButton1.UseVisualStyleBackColor = false;
			this.userButton1.Click += new System.EventHandler(userButton1_Click);
			this.logNetAnalysisControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.logNetAnalysisControl1.Location = new System.Drawing.Point(6, 30);
			this.logNetAnalysisControl1.Name = "logNetAnalysisControl1";
			this.logNetAnalysisControl1.Size = new System.Drawing.Size(818, 522);
			this.logNetAnalysisControl1.TabIndex = 0;
			this.logNetAnalysisControl1.Load += new System.EventHandler(logNetAnalysisControl1_Load);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(824, 577);
			base.Controls.Add(this.statusStrip1);
			base.Controls.Add(this.userButton1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.logNetAnalysisControl1);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Name = "FormLogNetView";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "日志查看器";
			base.Load += new System.EventHandler(FormLogNetView_Load);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
