using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.LogNet;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormLogNet : HslFormContent
	{
		private ILogNet logNet;

		private int threadCount = 3;

		private IContainer components = null;

		private Panel panel1;

		private Button button2;

		private Button button1;

		private Label label4;

		private TextBox textBox2;

		private Label label3;

		private ComboBox comboBox1;

		private TextBox textBox1;

		private Label label1;

		private Panel panel2;

		private Button button4;

		private Button button3;

		private Label label5;

		private TextBox textBox3;

		private Button button7;

		private Button button6;

		private Button button5;

		private ComboBox comboBox2;

		private Label label6;

		private Label label8;

		private Label label7;

		private Button button8;

		private CheckBox checkBox1;

		private UserControlHead userControlHead1;

		private Button button9;

		private Button button_hour_offset;

		private Label label2;

		private TextBox textBox_hour_offset;

		private TextBox textBox_code;

		private Label label9;

		private CheckBox checkBox_encoding_bom;

		public FormLogNet()
		{
			InitializeComponent();
			checkBox_encoding_bom.CheckedChanged += CheckBox_encoding_bom_CheckedChanged;
		}

		private void CheckBox_encoding_bom_CheckedChanged(object sender, EventArgs e)
		{
			if (logNet != null)
			{
				logNet.EncoderShouldEmitUTF8Identifier = checkBox_encoding_bom.Checked;
			}
		}

		private void FormLogNet_Load(object sender, EventArgs e)
		{
			logNet = new LogNetSingle("log.txt");
			comboBox1.DataSource = SoftBasic.GetEnumValues<HslMessageDegree>();
			comboBox1.SelectedItem = HslMessageDegree.DEBUG;
			comboBox2.DataSource = SoftBasic.GetEnumValues<HslMessageDegree>();
			comboBox2.SelectedItem = HslMessageDegree.DEBUG;
			comboBox2.SelectedIndexChanged += ComboBox2_SelectedIndexChanged;
			logNet.FiltrateKeyword("123");
			logNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			if (Program.Language == 2)
			{
				label1.Text = "Log Degree:";
				label6.Text = "Save Degree:";
				label2.Text = "Time Offset:";
				button_hour_offset.Text = "Set";
				label3.Text = "KeyWord:";
				checkBox1.Text = "Cancel Save?";
				label4.Text = "Message:";
				button3.Text = "Load File";
				button4.Text = "Clear File";
				button8.Text = "LogViewer";
				label5.Text = "Content:";
				label9.Text = "Code:";
				button1.Text = "w-Message";
				button5.Text = "w-NewLine";
				button6.Text = "w-Description";
				button7.Text = "w-Exception";
				button9.Text = "Raise Exception";
			}
			textBox_code.Text = "ILogNet logNet = new LogNetSingle( \"log.txt\" );  // 如果传入 string.Empty 则不存文件";
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception ex)
			{
				logNet?.WriteException("UnhandledException", ex);
			}
		}

		private void LogNet_BeforeSaveToFile(object sender, HslEventArgs e)
		{
			e.HslMessage.Cancel = checkBox1.Checked;
		}

		private void ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			HslMessageDegree messageDegree = (HslMessageDegree)comboBox2.SelectedItem;
			logNet.SetMessageDegree(messageDegree);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			HslMessageDegree hslMessageDegree = (HslMessageDegree)comboBox1.SelectedItem;
			logNet.RecordMessage(hslMessageDegree, textBox1.Text, textBox2.Text);
			textBox_code.Text = $"logNet.RecordMessage( HslMessageDegree.{hslMessageDegree}, \"{textBox1.Text}\", \"{textBox2.Text}\" );";
		}

		private void button5_Click(object sender, EventArgs e)
		{
			logNet.WriteNewLine();
			textBox_code.Text = "logNet.WriteNewLine( );";
		}

		private void button6_Click(object sender, EventArgs e)
		{
			logNet.WriteDescrition(textBox2.Text);
			textBox_code.Text = "logNet.WriteDescrition( \"" + textBox2.Text + "\" );";
		}

		private void button7_Click(object sender, EventArgs e)
		{
			try
			{
                int val = 0;
				int num = 100 / val;
			}
			catch (Exception ex)
			{
				logNet.WriteException(textBox1.Text, ex);
				textBox_code.Text = "logNet.WriteException( \"" + textBox1.Text + "\", ex );";
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			threadCount = 3;
			for (int i = 0; i < threadCount; i++)
			{
				Thread thread = new Thread(ThreadLogTest);
				thread.IsBackground = true;
				thread.Start();
			}
			button2.Enabled = false;
		}

		private void ThreadLogTest()
		{
			DateTime now = DateTime.Now;
			for (int i = 0; i < 330000; i++)
			{
				logNet.WriteInfo("key", "这是一条测试日志");
			}
			TimeSpan ts = DateTime.Now - now;
			if (Interlocked.Decrement(ref threadCount) == 0)
			{
				Invoke((Action)delegate
				{
					DemoUtils.ShowMessage("完成！耗时：" + ts.TotalMilliseconds.ToString("F3") + " 毫秒");
					button2.Enabled = true;
				});
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (File.Exists("log.txt"))
			{
				using (StreamReader streamReader = new StreamReader("log.txt", Encoding.UTF8))
				{
					textBox3.Text = streamReader.ReadToEnd();
				}
			}
			else
			{
				DemoUtils.ShowMessage(StringResources.Language.FileNotExist);
			}
			textBox_code.Text = "string content = File.ReadAllText( \"log.txt\", Encoding.UTF8 );";
		}

		private void button4_Click(object sender, EventArgs e)
		{
			File.WriteAllBytes("log.txt", new byte[0]);
			textBox_code.Text = "System.IO.File.WriteAllBytes( \"log.txt\", new byte[0] );";
		}

		private void button8_Click(object sender, EventArgs e)
		{
			if (File.Exists("log.txt"))
			{
				using (FormLogNetView formLogNetView = new FormLogNetView("log.txt"))
				{
					formLogNetView.OpenDialogDefaultPath = Application.StartupPath;
					formLogNetView.ShowDialog();
				}
			}
			else
			{
				using (FormLogNetView formLogNetView2 = new FormLogNetView())
				{
					formLogNetView2.ShowDialog();
				}
			}
			textBox_code.Text = "using (FormLogNetView form = new FormLogNetView( \"log.txt\" ))\r\n{\r\n\tform.OpenDialogDefaultPath = Application.StartupPath;        // 如果需要指定默认的打开文件的路径\r\n\tform.ShowDialog( );\r\n}";
		}

		private void button9_Click(object sender, EventArgs e)
		{
			((Form)null).AcceptButton = button1;
		}

		private void button_hour_offset_Click(object sender, EventArgs e)
		{
			logNet.HourDeviation = int.Parse(textBox_hour_offset.Text);
			DemoUtils.ShowMessage("Finish");
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.checkBox_encoding_bom = new System.Windows.Forms.CheckBox();
			this.button_hour_offset = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_hour_offset = new System.Windows.Forms.TextBox();
			this.button9 = new System.Windows.Forms.Button();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.label8 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.comboBox2 = new System.Windows.Forms.ComboBox();
			this.label6 = new System.Windows.Forms.Label();
			this.button7 = new System.Windows.Forms.Button();
			this.button6 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.button8 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.checkBox_encoding_bom);
			this.panel1.Controls.Add(this.button_hour_offset);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox_hour_offset);
			this.panel1.Controls.Add(this.button9);
			this.panel1.Controls.Add(this.checkBox1);
			this.panel1.Controls.Add(this.label8);
			this.panel1.Controls.Add(this.label7);
			this.panel1.Controls.Add(this.comboBox2);
			this.panel1.Controls.Add(this.label6);
			this.panel1.Controls.Add(this.button7);
			this.panel1.Controls.Add(this.button6);
			this.panel1.Controls.Add(this.button5);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.comboBox1);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(997, 206);
			this.panel1.TabIndex = 25;
			this.checkBox_encoding_bom.AutoSize = true;
			this.checkBox_encoding_bom.Checked = true;
			this.checkBox_encoding_bom.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_encoding_bom.Location = new System.Drawing.Point(717, 8);
			this.checkBox_encoding_bom.Name = "checkBox_encoding_bom";
			this.checkBox_encoding_bom.Size = new System.Drawing.Size(221, 21);
			this.checkBox_encoding_bom.TabIndex = 22;
			this.checkBox_encoding_bom.Text = "EncoderShouldEmitUTF8Identifier";
			this.checkBox_encoding_bom.UseVisualStyleBackColor = true;
			this.button_hour_offset.Location = new System.Drawing.Point(608, 4);
			this.button_hour_offset.Name = "button_hour_offset";
			this.button_hour_offset.Size = new System.Drawing.Size(90, 28);
			this.button_hour_offset.TabIndex = 21;
			this.button_hour_offset.Text = "设置";
			this.button_hour_offset.UseVisualStyleBackColor = true;
			this.button_hour_offset.Click += new System.EventHandler(button_hour_offset_Click);
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(452, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 20;
			this.label2.Text = "小时偏移：";
			this.textBox_hour_offset.Location = new System.Drawing.Point(532, 6);
			this.textBox_hour_offset.Name = "textBox_hour_offset";
			this.textBox_hour_offset.Size = new System.Drawing.Size(68, 23);
			this.textBox_hour_offset.TabIndex = 19;
			this.textBox_hour_offset.Text = "0";
			this.button9.Location = new System.Drawing.Point(612, 164);
			this.button9.Name = "button9";
			this.button9.Size = new System.Drawing.Size(132, 28);
			this.button9.TabIndex = 18;
			this.button9.Text = "强制引发异常";
			this.button9.UseVisualStyleBackColor = true;
			this.button9.Click += new System.EventHandler(button9_Click);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(290, 39);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(171, 21);
			this.checkBox1.TabIndex = 17;
			this.checkBox1.Text = "是否取消存储，打勾则取消";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.label8.AutoSize = true;
			this.label8.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
			this.label8.Location = new System.Drawing.Point(482, 43);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(221, 17);
			this.label8.TabIndex = 16;
			this.label8.Text = "当前已经设置过滤关键字123的日志存储";
			this.label7.AutoSize = true;
			this.label7.ForeColor = System.Drawing.Color.Blue;
			this.label7.Location = new System.Drawing.Point(718, 43);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(164, 17);
			this.label7.TabIndex = 15;
			this.label7.Text = "文件路径：当前目录下log.txt";
			this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox2.FormattingEnabled = true;
			this.comboBox2.Location = new System.Drawing.Point(312, 5);
			this.comboBox2.Name = "comboBox2";
			this.comboBox2.Size = new System.Drawing.Size(121, 25);
			this.comboBox2.TabIndex = 14;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(231, 8);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 13;
			this.label6.Text = "存储等级：";
			this.button7.Location = new System.Drawing.Point(397, 164);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(107, 28);
			this.button7.TabIndex = 12;
			this.button7.Text = "写异常";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.button6.Location = new System.Drawing.Point(276, 164);
			this.button6.Name = "button6";
			this.button6.Size = new System.Drawing.Size(115, 28);
			this.button6.TabIndex = 11;
			this.button6.Text = "写注释";
			this.button6.UseVisualStyleBackColor = true;
			this.button6.Click += new System.EventHandler(button6_Click);
			this.button5.Location = new System.Drawing.Point(180, 164);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(90, 28);
			this.button5.TabIndex = 10;
			this.button5.Text = "写换行";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button2.Location = new System.Drawing.Point(818, 164);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(170, 28);
			this.button2.TabIndex = 9;
			this.button2.Text = "100万条日志测试";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(84, 164);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(90, 28);
			this.button1.TabIndex = 8;
			this.button1.Text = "写日志";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(3, 75);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 7;
			this.label4.Text = "日志信息：";
			this.textBox2.Location = new System.Drawing.Point(84, 71);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(908, 87);
			this.textBox2.TabIndex = 6;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3, 40);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(56, 17);
			this.label3.TabIndex = 5;
			this.label3.Text = "关键字：";
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(84, 5);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(121, 25);
			this.comboBox1.TabIndex = 4;
			this.textBox1.Location = new System.Drawing.Point(84, 37);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(186, 23);
			this.textBox1.TabIndex = 3;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 2;
			this.label1.Text = "日志等级：";
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.textBox_code);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.button8);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.textBox3);
			this.panel2.Location = new System.Drawing.Point(3, 244);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 398);
			this.panel2.TabIndex = 26;
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(84, 332);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(908, 61);
			this.textBox_code.TabIndex = 14;
			this.label9.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(3, 335);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(44, 17);
			this.label9.TabIndex = 13;
			this.label9.Text = "代码：";
			this.button8.Location = new System.Drawing.Point(276, 3);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(102, 28);
			this.button8.TabIndex = 12;
			this.button8.Text = "日志分析器";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button4.Location = new System.Drawing.Point(180, 3);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(90, 28);
			this.button4.TabIndex = 11;
			this.button4.Text = "清空文件";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button3.Location = new System.Drawing.Point(84, 3);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(90, 28);
			this.button3.TabIndex = 10;
			this.button3.Text = "加载文件";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(3, 42);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 9;
			this.label5.Text = "日志文件：";
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(84, 37);
			this.textBox3.Multiline = true;
			this.textBox3.Name = "textBox3";
			this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox3.Size = new System.Drawing.Size(908, 289);
			this.textBox3.TabIndex = 8;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "http://www.cnblogs.com/dathlin/p/7691693.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 27;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormLogNet";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormLogNet";
			base.Load += new System.EventHandler(FormLogNet_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
