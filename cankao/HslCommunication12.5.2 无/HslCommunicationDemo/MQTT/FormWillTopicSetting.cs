using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.MQTT
{
	public class FormWillTopicSetting : Form
	{
		private string topic;

		private string message;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox_topic;

		private TextBox textBox_message;

		private Label label2;

		private Button button1;

		public string Topic => topic;

		public string Message => message;

		public FormWillTopicSetting(string topic, string message)
		{
			InitializeComponent();
			this.topic = topic;
			this.message = message;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			topic = textBox_topic.Text;
			message = textBox_message.Text;
			base.DialogResult = DialogResult.OK;
		}

		private void FormWillTopicSetting_Load(object sender, EventArgs e)
		{
			textBox_topic.Text = topic;
			textBox_message.Text = message;
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
			this.textBox_topic = new System.Windows.Forms.TextBox();
			this.textBox_message = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(19, 20);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(68, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Wiil Topic:";
			this.textBox_topic.Location = new System.Drawing.Point(93, 17);
			this.textBox_topic.Name = "textBox_topic";
			this.textBox_topic.Size = new System.Drawing.Size(240, 23);
			this.textBox_topic.TabIndex = 1;
			this.textBox_message.Location = new System.Drawing.Point(22, 79);
			this.textBox_message.Multiline = true;
			this.textBox_message.Name = "textBox_message";
			this.textBox_message.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_message.Size = new System.Drawing.Size(311, 154);
			this.textBox_message.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(19, 54);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(89, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "Wiil Message:";
			this.button1.Location = new System.Drawing.Point(104, 249);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(135, 37);
			this.button1.TabIndex = 4;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(348, 307);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox_message);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_topic);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormWillTopicSetting";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "WillTopicSetting";
			base.Load += new System.EventHandler(FormWillTopicSetting_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
