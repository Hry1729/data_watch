using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.MQTT
{
	public class FormInputNewPath : Form
	{
		private IContainer components = null;

		private Label label1;

		private TextBox textBox_path_old;

		private TextBox textBox_path_new;

		private Label label2;

		private Button button1;

		public string PathInput { get; set; }

		public FormInputNewPath(string oldPath, string newPath)
		{
			InitializeComponent();
			textBox_path_old.Text = oldPath;
			textBox_path_new.Text = newPath;
			textBox_path_old.ReadOnly = true;
		}

		private void FormInputNewPath_Load(object sender, EventArgs e)
		{
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_path_new.Text))
			{
				DemoUtils.ShowMessage("path can not be empty");
				return;
			}
			PathInput = textBox_path_new.Text;
			base.DialogResult = DialogResult.OK;
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
			this.textBox_path_old = new System.Windows.Forms.TextBox();
			this.textBox_path_new = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(35, 25);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "当前路径:";
			this.textBox_path_old.Location = new System.Drawing.Point(110, 22);
			this.textBox_path_old.Name = "textBox_path_old";
			this.textBox_path_old.Size = new System.Drawing.Size(322, 23);
			this.textBox_path_old.TabIndex = 1;
			this.textBox_path_new.Location = new System.Drawing.Point(110, 61);
			this.textBox_path_new.Name = "textBox_path_new";
			this.textBox_path_new.Size = new System.Drawing.Size(322, 23);
			this.textBox_path_new.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(35, 64);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(59, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "新的路径:";
			this.button1.Location = new System.Drawing.Point(153, 101);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(155, 32);
			this.button1.TabIndex = 4;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(491, 149);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox_path_new);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_path_old);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(507, 188);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(507, 188);
			base.Name = "FormInputNewPath";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormInputNewPath";
			base.Load += new System.EventHandler(FormInputNewPath_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
