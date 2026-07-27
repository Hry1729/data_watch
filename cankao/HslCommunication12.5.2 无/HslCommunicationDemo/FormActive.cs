using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Security;
using HslControls;

namespace HslCommunicationDemo
{
	public class FormActive : Form
	{
		private string activePath = string.Empty;

		private IContainer components = null;

		private Button button1;

		private Label label1;

		private TextBox textBox1;

		private Label label2;

		private TextBox textBox2;

		private Label label3;

		private TextBox textBox3;

		public FormActive()
		{
			InitializeComponent();
			activePath = Path.Combine(Application.StartupPath, "active.txt");
			base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			bool flag = false;
			if ((textBox1.Text.Length >= 100) )
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "HslCommunication激活成功!" : "HslCommunication Activation successful!");
				File.WriteAllText(activePath, string.Empty, Encoding.UTF8);
				string text = new FileInfo(activePath).CreationTime.ToString("yyyy-MM-dd-mm-ss");
				File.WriteAllBytes(bytes: new AesCryptography(text + text).Encrypt(Encoding.UTF8.GetBytes(textBox1.Text)), path: activePath);
				base.DialogResult = DialogResult.OK;
			}
			else
			{
				DemoUtils.ShowMessage((Program.Language == 1) ? "HslCommunication激活失败！" : "HslCommunication Activation failed!");
			}
			if (!string.IsNullOrEmpty(textBox2.Text))
			{
				if (true)
				{
					string text2 = Path.Combine(Application.StartupPath, "controls_active.txt");
					DemoUtils.ShowMessage((Program.Language == 1) ? "HslControls激活成功!" : "HslControls Activation successful!");
					File.WriteAllText(text2, string.Empty, Encoding.UTF8);
					string text3 = new FileInfo(text2).CreationTime.ToString("yyyy-MM-dd-mm-ss");
					File.WriteAllBytes(text2, new AesCryptography(text3 + text3).Encrypt(Encoding.UTF8.GetBytes(textBox2.Text)));
					base.DialogResult = DialogResult.OK;
				}
				else
				{
					DemoUtils.ShowMessage((Program.Language == 1) ? "HslControls激活失败！" : "HslControls Activation failed!");
				}
			}
		}

		private void FormActive_Load(object sender, EventArgs e)
		{
		}

		private void FormActive_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				string code = string.Empty;
				try
				{
					code = SoftAuthorize.GetInfo(false);
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Get Code Failed: " + ex.Message);
					return;
				}
				try
				{
					Invoke((Action)delegate
					{
						textBox3.Text = code;
					});
				}
				catch
				{
				}
			}, null);
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.button1.Location = new System.Drawing.Point(230, 291);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(132, 37);
			this.button1.TabIndex = 0;
			this.button1.Text = "Active (激活)";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 35);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(154, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "HslCommunication Code:";
			this.textBox1.Location = new System.Drawing.Point(12, 55);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.PasswordChar = '*';
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(565, 133);
			this.textBox1.TabIndex = 2;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(12, 195);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(113, 17);
			this.label2.TabIndex = 3;
			this.label2.Text = "HslControls Code:";
			this.textBox2.Location = new System.Drawing.Point(12, 215);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(565, 64);
			this.textBox2.TabIndex = 4;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(12, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(95, 17);
			this.label3.TabIndex = 5;
			this.label3.Text = "Machine Code:";
			this.textBox3.Location = new System.Drawing.Point(113, 6);
			this.textBox3.Name = "textBox3";
			this.textBox3.ReadOnly = true;
			this.textBox3.Size = new System.Drawing.Size(464, 23);
			this.textBox3.TabIndex = 6;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.SystemColors.Control;
			base.ClientSize = new System.Drawing.Size(589, 337);
			base.Controls.Add(this.textBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.button1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "FormActive";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "FormActive";
			base.Load += new System.EventHandler(FormActive_Load);
			base.Shown += new System.EventHandler(FormActive_Shown);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
