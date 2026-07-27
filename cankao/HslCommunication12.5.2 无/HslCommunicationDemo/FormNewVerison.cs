using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo
{
	public class FormNewVerison : Form
	{
		private IContainer components = null;

		private Button button1;

		private Button button2;

		private CheckBox checkBox1;

		private Label label1;

		public bool NewVersionIngored => checkBox1.Checked;

		public FormNewVerison()
		{
			InitializeComponent();
		}

		public DialogResult ShowDialog(string caption, string info)
		{
			Text = caption;
			label1.Text = info;
			return ShowDialog();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.Yes;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.No;
		}

		private void FormNewVerison_Load(object sender, EventArgs e)
		{
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
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.button1.Location = new System.Drawing.Point(163, 145);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(116, 34);
			this.button1.TabIndex = 0;
			this.button1.Text = "Yes (是)";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.button2.Location = new System.Drawing.Point(285, 145);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(116, 34);
			this.button2.TabIndex = 1;
			this.button2.Text = "No (否)";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(209, 182);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(148, 21);
			this.checkBox1.TabIndex = 2;
			this.checkBox1.Text = "Do not remind again";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(12, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(554, 124);
			this.label1.TabIndex = 3;
			this.label1.Text = "label1";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.ClientSize = new System.Drawing.Size(578, 203);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.checkBox1);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.button1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormNewVerison";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormNewVerison";
			base.Load += new System.EventHandler(FormNewVerison_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
