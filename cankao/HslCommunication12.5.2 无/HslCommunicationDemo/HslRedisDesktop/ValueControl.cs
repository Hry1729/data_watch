using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication.BasicFramework;
using HslCommunicationDemo;
using Newtonsoft.Json;

namespace HslRedisDesktop
{
	public class ValueControl : UserControl
	{
		private string value = string.Empty;

		private int lastSeachIndex = -1;

		private string searchCondition = string.Empty;

		private IContainer components = null;

		private Label label1;

		private Label label2;

		private Label label3;

		private Panel panel1;

		private RadioButton radioButton3;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private TextBox textBox1;

		private TextBox textBox2;

		private LinkLabel linkLabel1;

		public ValueControl()
		{
			InitializeComponent();
		}

		public void SetValue(string value)
		{
			this.value = value;
			ShowValue();
		}

		private void ShowValue()
		{
			try
			{
				int num = Encoding.UTF8.GetBytes(value).Length;
				label2.Text = "大小: " + SoftBasic.GetSizeDescription(num);
				if (radioButton1.Checked)
				{
					textBox1.Text = value;
				}
				else if (radioButton2.Checked)
				{
					textBox1.Text = JsonConvert.DeserializeObject(value).ToString();
				}
				else if (radioButton3.Checked)
				{
					textBox1.Text = XElement.Parse(value).ToString();
				}
			}
			catch
			{
				textBox1.Text = value;
			}
		}

		private void ValueControl_Load(object sender, EventArgs e)
		{
			radioButton1.CheckedChanged += RadioButton_CheckedChanged;
			radioButton2.CheckedChanged += RadioButton_CheckedChanged;
			radioButton3.CheckedChanged += RadioButton_CheckedChanged;
		}

		private void RadioButton_CheckedChanged(object sender, EventArgs e)
		{
			ShowValue();
		}

		public string KeySourceValue()
		{
			return value;
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (string.IsNullOrEmpty(textBox1.Text) || string.IsNullOrEmpty(textBox2.Text))
			{
				DemoUtils.ShowMessage("输入的字符串为空，无法进行查找。");
				return;
			}
			if (string.IsNullOrEmpty(searchCondition))
			{
				searchCondition = textBox2.Text;
			}
			else if (searchCondition != textBox2.Text)
			{
				lastSeachIndex = -1;
				searchCondition = textBox2.Text;
			}
			int num = textBox1.Text.IndexOf(textBox2.Text, (lastSeachIndex != -1) ? lastSeachIndex : 0);
			if (num == -1)
			{
				DemoUtils.ShowMessage("没有找到相关的数据信息。");
				lastSeachIndex = -1;
				return;
			}
			lastSeachIndex = num + 1;
			textBox1.SelectionStart = num;
			textBox1.SelectionLength = textBox2.Text.Length;
			textBox1.ScrollToCaret();
			textBox1.Focus();
		}

		private void textBox2_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				linkLabel1_LinkClicked(null, null);
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
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 3);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "数据值：";
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.Silver;
			this.label2.Location = new System.Drawing.Point(59, 3);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(71, 17);
			this.label2.TabIndex = 1;
			this.label2.Text = "大小：0 KB";
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(363, 4);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(44, 17);
			this.label3.TabIndex = 2;
			this.label3.Text = "显示：";
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.Controls.Add(this.radioButton3);
			this.panel1.Controls.Add(this.radioButton2);
			this.panel1.Controls.Add(this.radioButton1);
			this.panel1.Location = new System.Drawing.Point(413, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(169, 19);
			this.panel1.TabIndex = 3;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(117, -2);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(48, 21);
			this.radioButton3.TabIndex = 2;
			this.radioButton3.TabStop = true;
			this.radioButton3.Text = "Xml";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(59, -2);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(52, 21);
			this.radioButton2.TabIndex = 1;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "Json";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(3, -2);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(50, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "Text";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(3, 25);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(583, 516);
			this.textBox1.TabIndex = 4;
			this.textBox2.Location = new System.Drawing.Point(158, 1);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(139, 23);
			this.textBox2.TabIndex = 5;
			this.textBox2.KeyDown += new System.Windows.Forms.KeyEventHandler(textBox2_KeyDown);
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(297, 4);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(32, 17);
			this.linkLabel1.TabIndex = 6;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "搜索";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ValueControl";
			base.Size = new System.Drawing.Size(589, 544);
			base.Load += new System.EventHandler(ValueControl_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
