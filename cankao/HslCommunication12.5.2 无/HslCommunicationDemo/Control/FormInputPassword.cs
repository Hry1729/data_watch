using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HslCommunicationDemo.Control
{
	public class FormInputPassword : Form
	{
		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private Button button1;

		public int StringMode { get; set; }

		public string Password { get; set; }

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

		public FormInputPassword()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (StringMode == 0)
			{
				if (string.IsNullOrEmpty(textBox1.Text))
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "当前输入的密码不能为空! " : "The currently entered password cannot be empty! ");
					return;
				}
				if (!Regex.IsMatch(textBox1.Text, "^[0-9A-Za-z]{4,32}$"))
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "密码只能是数字字母组成，且4~32位之间!" : "The password can only be composed of numeric letters and between 4~32 digits!");
					return;
				}
			}
			else if (StringMode == 1 && !string.IsNullOrEmpty(textBox1.Text))
			{
				try
				{
					int num = Convert.ToInt32(textBox1.Text);
				}
				catch
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "当前输入的不是数字，请重新输入数字! " : "The current input is not a number, please re-enter the number!");
					return;
				}
			}
			Password = textBox1.Text;
			base.DialogResult = DialogResult.OK;
		}

		private void FormInputPassword_Load(object sender, EventArgs e)
		{
			if (StringMode == 0)
			{
				if (Program.Language == 1)
				{
					label1.Text = "密码:";
					button1.Text = "确认";
					Text = "请输入密码";
				}
			}
			else if (StringMode == 1)
			{
				if (Program.Language == 1)
				{
					label1.Text = "数量:";
					button1.Text = "确认";
					Text = "请输入数量信息，可以为空";
				}
				else
				{
					label1.Text = "Length:";
					button1.Text = "OK";
					Text = "Please enter quantity information, can be empty";
				}
				textBox1.PasswordChar = '\0';
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(11, 30);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(43, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "PWD: ";
			this.textBox1.Location = new System.Drawing.Point(67, 27);
			this.textBox1.Name = "textBox1";
			this.textBox1.PasswordChar = '*';
			this.textBox1.Size = new System.Drawing.Size(312, 23);
			this.textBox1.TabIndex = 1;
			this.button1.Location = new System.Drawing.Point(127, 62);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(139, 37);
			this.button1.TabIndex = 2;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			base.AcceptButton = this.button1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(405, 111);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(421, 150);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(421, 150);
			base.Name = "FormInputPassword";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Enter Password for check";
			base.Load += new System.EventHandler(FormInputPassword_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
