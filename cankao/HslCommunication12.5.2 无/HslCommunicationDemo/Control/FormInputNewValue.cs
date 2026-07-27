using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.Control
{
	public class FormInputNewValue : Form
	{
		private string oldString = "";

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private Button button1;

		public string InputValue { get; set; }

		public string Tip
		{
			get
			{
				return label1.Text;
			}
			set
			{
				label1.Text = value;
			}
		}

		public FormInputNewValue(string old)
		{
			InitializeComponent();
			oldString = old;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			InputValue = textBox1.Text;
			base.DialogResult = DialogResult.OK;
		}

		private void FormInputPassword_Load(object sender, EventArgs e)
		{
			if (Program.Language == 1)
			{
			}
			if (!string.IsNullOrEmpty(oldString))
			{
				textBox1.Text = oldString;
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslCommunicationDemo.Control.FormInputNewValue));
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(11, 25);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(47, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Value: ";
			this.textBox1.Location = new System.Drawing.Point(67, 22);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(405, 23);
			this.textBox1.TabIndex = 1;
			this.button1.Location = new System.Drawing.Point(165, 62);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(139, 37);
			this.button1.TabIndex = 2;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AcceptButton = this.button1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(484, 111);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(500, 150);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(500, 150);
			base.Name = "FormInputNewValue";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Enter New Value:";
			base.Load += new System.EventHandler(FormInputPassword_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
