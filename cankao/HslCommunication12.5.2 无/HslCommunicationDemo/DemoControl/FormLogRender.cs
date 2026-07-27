using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using WeifenLuo.WinFormsUI.Docking;

namespace HslCommunicationDemo.DemoControl
{
	public class FormLogRender : DockContent
	{
		private HslMessageDegree hslMessageDegree = HslMessageDegree.DEBUG;

		private string regexFilter = string.Empty;

		private string logOutputFileName = string.Empty;

		private StreamWriter logStreamWrite = null;

		private ILogNet logNet = null;

		private IContainer components = null;

		private TextBox textBox1;

		private Label label1;

		private ComboBox comboBox1;

		private CheckBox checkBox_stop_render;

		private Button button_regex_filter;

		private TextBox textBox2;

		private CheckBox checkBox_output_file;

		public FormLogRender(ILogNet logNet)
		{
			InitializeComponent();
			this.logNet = logNet;
			this.logNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			comboBox1.DataSource = SoftBasic.GetEnumValues<HslMessageDegree>();
			comboBox1.SelectedItem = HslMessageDegree.DEBUG;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			checkBox_output_file.CheckedChanged += CheckBox_output_file_CheckedChanged;
		}

		private void FormLogRender_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "Log";
				label1.Text = "Degree:";
				checkBox_stop_render.Text = "Stop render textbox";
				button_regex_filter.Text = "regex-filter";
				ShowCheckBoxText();
			}
		}

		private void ShowCheckBoxText()
		{
			checkBox_output_file.Text = ((Program.Language == 1) ? ("输出到文件(" + logOutputFileName + ")") : ("OutPut file(" + logOutputFileName + ")"));
		}

		private void CheckBox_output_file_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox_output_file.Checked)
			{
				using (SaveFileDialog saveFileDialog = new SaveFileDialog())
				{
					saveFileDialog.FileName = (string.IsNullOrEmpty(logOutputFileName) ? "log.txt" : logOutputFileName);
					if (saveFileDialog.ShowDialog() == DialogResult.OK)
					{
						try
						{
							logOutputFileName = saveFileDialog.FileName;
							logStreamWrite = new StreamWriter(logOutputFileName, true, Encoding.UTF8);
							ShowCheckBoxText();
							DemoUtils.ShowMessage("Set success");
							return;
						}
						catch (Exception ex)
						{
							logOutputFileName = null;
							SoftBasic.ShowExceptionMessage(ex);
							return;
						}
					}
					return;
				}
			}
			logOutputFileName = string.Empty;
			logStreamWrite?.Close();
			logStreamWrite?.Dispose();
			logStreamWrite = null;
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			hslMessageDegree = (HslMessageDegree)comboBox1.SelectedItem;
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			try
			{
				if (checkBox_stop_render.Checked || e.HslMessage.Degree > hslMessageDegree)
				{
					return;
				}
				bool flag = true;
				string log = e.HslMessage.ToString() + Environment.NewLine;
				if (!string.IsNullOrEmpty(regexFilter) && !Regex.IsMatch(log, regexFilter))
				{
					flag = false;
				}
				if (!string.IsNullOrEmpty(logOutputFileName) && logStreamWrite != null)
				{
					logStreamWrite.Write(log);
				}
				if (flag)
				{
					Invoke((Action)delegate
					{
						textBox1.AppendText(log);
					});
				}
			}
			catch
			{
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			logNet.BeforeSaveToFile -= LogNet_BeforeSaveToFile;
		}

		private void button_regex_filter_Click(object sender, EventArgs e)
		{
			regexFilter = textBox2.Text;
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.checkBox_stop_render = new System.Windows.Forms.CheckBox();
			this.button_regex_filter = new System.Windows.Forms.Button();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.checkBox_output_file = new System.Windows.Forms.CheckBox();
			base.SuspendLayout();
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(1, 31);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(906, 216);
			this.textBox1.TabIndex = 0;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(4, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "日志等级:";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(72, 3);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(102, 25);
			this.comboBox1.TabIndex = 2;
			this.checkBox_stop_render.AutoSize = true;
			this.checkBox_stop_render.Location = new System.Drawing.Point(187, 6);
			this.checkBox_stop_render.Name = "checkBox_stop_render";
			this.checkBox_stop_render.Size = new System.Drawing.Size(123, 21);
			this.checkBox_stop_render.TabIndex = 3;
			this.checkBox_stop_render.Text = "暂停显示日志输出";
			this.checkBox_stop_render.UseVisualStyleBackColor = true;
			this.button_regex_filter.Location = new System.Drawing.Point(502, 2);
			this.button_regex_filter.Name = "button_regex_filter";
			this.button_regex_filter.Size = new System.Drawing.Size(97, 27);
			this.button_regex_filter.TabIndex = 5;
			this.button_regex_filter.Text = "正则过滤";
			this.button_regex_filter.UseVisualStyleBackColor = true;
			this.button_regex_filter.Click += new System.EventHandler(button_regex_filter_Click);
			this.textBox2.Location = new System.Drawing.Point(342, 5);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(154, 23);
			this.textBox2.TabIndex = 6;
			this.checkBox_output_file.AutoSize = true;
			this.checkBox_output_file.Location = new System.Drawing.Point(609, 6);
			this.checkBox_output_file.Name = "checkBox_output_file";
			this.checkBox_output_file.Size = new System.Drawing.Size(131, 21);
			this.checkBox_output_file.TabIndex = 7;
			this.checkBox_output_file.Text = "输出到文件(log.txt)";
			this.checkBox_output_file.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(909, 247);
			base.Controls.Add(this.checkBox_output_file);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.button_regex_filter);
			base.Controls.Add(this.checkBox_stop_render);
			base.Controls.Add(this.comboBox1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormLogRender";
			this.Text = "日志显示";
			base.Load += new System.EventHandler(FormLogRender_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
