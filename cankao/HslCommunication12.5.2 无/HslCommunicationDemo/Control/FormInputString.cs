using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HslCommunicationDemo.Control
{
	public class FormInputString : Form
	{
		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private Button button1;

		private Label label2;

		private TextBox textBox2;

		private TextBox textBox3;

		private Label label3;

		private Label label_tips;

		public string DeviceAlias { get; set; }

		public string Password { get; set; }

		public FormInputString()
		{
			InitializeComponent();
		}

		public FormInputString(string alias)
		{
			DeviceAlias = alias;
		}

		public FormInputString(string alias, string password)
		{
			InitializeComponent();
			DeviceAlias = alias;
			textBox1.Text = alias;
			textBox2.Text = password;
			textBox3.Text = password;
		}

		private void FormInputString_Load(object sender, EventArgs e)
		{
			if (Program.Language == 2)
			{
				Text = "Please enter a string";
				label1.Text = "Please enter the alias information. If \":\" is entered in the middle, it will be used as the category separator";
				button1.Text = "ok";
				label2.Text = "The password, if empty, means not encrypted and consists of fewer than 32 numbers or letters";
				label3.Text = "Please enter your password again:";
				label_tips.Text = "Tip: If the datasheet has configuration information, it will be stored together";
			}
			textBox1.Text = DeviceAlias;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox1.Text))
			{
				DemoUtils.ShowMessage("Value can not be null");
				return;
			}
			if (!string.IsNullOrEmpty(textBox2.Text))
			{
				if (!Regex.IsMatch(textBox2.Text, "^[0-9A-Za-z]{4,32}$"))
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "密码只能是数字字母组成，且4~32位之间!" : "The password can only be composed of numeric letters and between 4~32 digits!");
					return;
				}
				if (textBox2.Text != textBox3.Text)
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "两次密码输入不一致，请重新输入" : "The password is entered inconsistently twice, please re-enter it");
					return;
				}
				Password = textBox2.Text;
			}
			else if (!string.IsNullOrEmpty(textBox3.Text))
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "两次密码输入不一致，请重新输入" : "The password is entered inconsistently twice, please re-enter it");
				return;
			}
			DeviceAlias = textBox1.Text;
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label_tips = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.label1.Location = new System.Drawing.Point(12, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(449, 43);
			this.label1.TabIndex = 0;
			this.label1.Text = "请输入别名信息，如果中间输入了 \":\"（英文），将作为类别分隔符";
			this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			this.textBox1.Location = new System.Drawing.Point(12, 52);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(412, 23);
			this.textBox1.TabIndex = 1;
			this.button1.Location = new System.Drawing.Point(162, 238);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(93, 32);
			this.button1.TabIndex = 2;
			this.button1.Text = "确认";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label2.Location = new System.Drawing.Point(13, 77);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(448, 38);
			this.label2.TabIndex = 3;
			this.label2.Text = "密码，如果为空，则表示不加密，由小于32个的数字或字母组成";
			this.label2.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			this.textBox2.Location = new System.Drawing.Point(12, 118);
			this.textBox2.Name = "textBox2";
			this.textBox2.PasswordChar = '*';
			this.textBox2.Size = new System.Drawing.Size(412, 23);
			this.textBox2.TabIndex = 4;
			this.textBox3.Location = new System.Drawing.Point(12, 165);
			this.textBox3.Name = "textBox3";
			this.textBox3.PasswordChar = '*';
			this.textBox3.Size = new System.Drawing.Size(412, 23);
			this.textBox3.TabIndex = 5;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(13, 145);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(104, 17);
			this.label3.TabIndex = 6;
			this.label3.Text = "请再次输入密码：";
			this.label_tips.ForeColor = System.Drawing.Color.Green;
			this.label_tips.Location = new System.Drawing.Point(13, 191);
			this.label_tips.Name = "label_tips";
			this.label_tips.Size = new System.Drawing.Size(420, 40);
			this.label_tips.TabIndex = 7;
			this.label_tips.Text = "贴士：如果数据表有配置信息，将会一起存储";
			base.AcceptButton = this.button1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(434, 293);
			base.Controls.Add(this.label_tips);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "FormInputString";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "请输入字符串";
			base.Load += new System.EventHandler(FormInputString_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
