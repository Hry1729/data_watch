using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core.Security;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormHslCertificate : HslFormContent
	{
		private IContainer components = null;

		private Panel panel1;

		private Button button_verify;

		private Button button_load;

		private Button button_create;

		private GroupBox groupBox2;

		private TextBox textBox_From;

		private Label label1;

		private GroupBox groupBox1;

		private Panel panel2;

		private Label label3;

		private RadioButton radioButton1;

		private RadioButton radioButton2;

		private TextBox textBox_pri_key;

		private Label label4;

		private TextBox textBox_pub_key;

		private Label label2;

		private TextBox textBox_NotBefore;

		private Label label6;

		private TextBox textBox_To;

		private Label label5;

		private TextBox textBox_Unique;

		private Label label9;

		private TextBox textBox_KeyWord;

		private Label label8;

		private TextBox textBox_NotAfter;

		private Label label7;

		private TextBox textBox_Descriptions;

		private Label label10;

		private UserControlHead userControlHead1;

		private Button button1;

		private TextBox textBox_hours;

		private Label label_hours;

		private TextBox textBox_createDate;

		private Label label11;

		public FormHslCertificate()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
			textBox_pri_key.Text = Convert.ToBase64String(rSACryptoServiceProvider.GetPEMPrivateKey());
			textBox_pub_key.Text = Convert.ToBase64String(rSACryptoServiceProvider.GetPEMPublicKey());
			textBox_NotBefore.Text = DateTime.Now.ToString("yyyy-MM-dd");
			textBox_NotAfter.Text = DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");
			textBox_createDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language != 1)
			{
				groupBox1.Text = "Public and private keys";
				label2.Text = "Public:";
				label3.Text = "Data formate:";
				label4.Text = "Private:";
				groupBox2.Text = "Certificate information";
				label1.Text = "Issuing:";
				label5.Text = "Holder:";
				label6.Text = "Not Before:";
				label7.Text = "Not After:";
				label11.Text = "Create Date:";
				label_hours.Text = "Hours: ";
				label8.Text = "Key Word:";
				label9.Text = "Unique:";
				label10.Text = "Extra info (key-value pairs + line breaks): For example: A: Hello";
				button_create.Text = "Create certificate";
				button_load.Text = "Load certificate";
				button_verify.Text = "Verify certificate";
			}
		}

		private void button_create_Click(object sender, EventArgs e)
		{
			HslCertificate hslCertificate = new HslCertificate(radioButton2.Checked ? Convert.FromBase64String(textBox_pub_key.Text) : textBox_pub_key.Text.ToHexBytes(), radioButton2.Checked ? Convert.FromBase64String(textBox_pri_key.Text) : textBox_pri_key.Text.ToHexBytes());
			hslCertificate.From = textBox_From.Text;
			hslCertificate.To = textBox_To.Text;
			hslCertificate.NotBefore = DateTime.Parse(textBox_NotBefore.Text);
			hslCertificate.NotAfter = DateTime.Parse(textBox_NotAfter.Text);
			hslCertificate.CreateTime = DateTime.Parse(textBox_createDate.Text);
			hslCertificate.EffectiveHours = Convert.ToInt32(textBox_hours.Text);
			hslCertificate.KeyWord = textBox_KeyWord.Text;
			hslCertificate.UniqueID = textBox_Unique.Text;
			string[] lines = textBox_Descriptions.Lines;
			if (lines != null && lines.Length != 0)
			{
				hslCertificate.Descriptions = new Dictionary<string, string>();
				string[] lines2 = textBox_Descriptions.Lines;
				for (int i = 0; i < lines2.Length; i++)
				{
					string[] array = lines2[i].Split(new char[3] { ',', '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
					if (array.Length == 2)
					{
						hslCertificate.Descriptions.Add(array[0], array[1]);
					}
				}
			}
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.FileName = "hsl.cert";
				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					File.WriteAllBytes(saveFileDialog.FileName, hslCertificate.GetSaveBytes());
				}
			}
			string message = "创建证书完成，是否复制证书到剪切板？";
			if (Program.Language == 2)
			{
				message = "After creating the certificate, do you want to copy the certificate to the clipboard?";
			}
			if (DemoUtils.ShowMessage(message, "Info", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				Clipboard.SetText(Convert.ToBase64String(hslCertificate.GetSaveBytes()));
			}
		}

		private void button_load_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				try
				{
					HslCertificate hslCertificate = HslCertificate.CreateFrom(File.ReadAllBytes(openFileDialog.FileName));
					textBox_pub_key.Text = (radioButton2.Checked ? Convert.ToBase64String(hslCertificate.PublicKey) : hslCertificate.PublicKey.ToHexString(' '));
					textBox_From.Text = hslCertificate.From;
					textBox_To.Text = hslCertificate.To;
					textBox_NotBefore.Text = hslCertificate.NotBefore.ToString("yyyy-MM-dd HH:mm:ss");
					textBox_NotAfter.Text = hslCertificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");
					textBox_KeyWord.Text = hslCertificate.KeyWord;
					textBox_Unique.Text = hslCertificate.UniqueID;
					textBox_createDate.Text = hslCertificate.CreateTime.ToString("yyyy-MM-dd");
					textBox_hours.Text = hslCertificate.EffectiveHours.ToString();
					Dictionary<string, string> descriptions = hslCertificate.Descriptions;
					if (descriptions == null || descriptions.Count <= 0)
					{
						return;
					}
					StringBuilder stringBuilder = new StringBuilder();
					foreach (KeyValuePair<string, string> description in hslCertificate.Descriptions)
					{
						stringBuilder.Append(description.Key);
						stringBuilder.Append(":");
						stringBuilder.Append(description.Value);
						stringBuilder.Append(Environment.NewLine);
					}
					textBox_Descriptions.Text = stringBuilder.ToString().TrimEnd('\r', '\n');
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Verify failed: " + ex.Message);
				}
			}
		}

		private void button_verify_Click(object sender, EventArgs e)
		{
			byte[] publicKey = (string.IsNullOrEmpty(textBox_pub_key.Text) ? null : (radioButton2.Checked ? Convert.FromBase64String(textBox_pub_key.Text) : textBox_pub_key.Text.ToHexBytes()));
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				try
				{
					if (HslCertificate.VerifyCer(publicKey, File.ReadAllBytes(openFileDialog.FileName)))
					{
						DemoUtils.ShowMessage("Verify success");
					}
					else
					{
						DemoUtils.ShowMessage("Verify failed");
					}
				}
				catch (Exception ex)
				{
					DemoUtils.ShowMessage("Verify failed: " + ex.Message);
				}
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			textBox_Unique.Text = Guid.NewGuid().ToString();
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
			this.button_verify = new System.Windows.Forms.Button();
			this.button_load = new System.Windows.Forms.Button();
			this.button_create = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.textBox_hours = new System.Windows.Forms.TextBox();
			this.label_hours = new System.Windows.Forms.Label();
			this.textBox_createDate = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.textBox_Descriptions = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_Unique = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_KeyWord = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_NotAfter = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_NotBefore = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_To = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_From = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label3 = new System.Windows.Forms.Label();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.textBox_pri_key = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_pub_key = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.button_verify);
			this.panel1.Controls.Add(this.button_load);
			this.panel1.Controls.Add(this.button_create);
			this.panel1.Controls.Add(this.groupBox2);
			this.panel1.Controls.Add(this.groupBox1);
			this.panel1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.panel1.Location = new System.Drawing.Point(4, 34);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(996, 608);
			this.panel1.TabIndex = 37;
			this.button_verify.Location = new System.Drawing.Point(616, 565);
			this.button_verify.Name = "button_verify";
			this.button_verify.Size = new System.Drawing.Size(180, 39);
			this.button_verify.TabIndex = 4;
			this.button_verify.Text = "验证证书";
			this.button_verify.UseVisualStyleBackColor = true;
			this.button_verify.Click += new System.EventHandler(button_verify_Click);
			this.button_load.Location = new System.Drawing.Point(418, 565);
			this.button_load.Name = "button_load";
			this.button_load.Size = new System.Drawing.Size(180, 39);
			this.button_load.TabIndex = 3;
			this.button_load.Text = "加载证书";
			this.button_load.UseVisualStyleBackColor = true;
			this.button_load.Click += new System.EventHandler(button_load_Click);
			this.button_create.Location = new System.Drawing.Point(218, 565);
			this.button_create.Name = "button_create";
			this.button_create.Size = new System.Drawing.Size(180, 39);
			this.button_create.TabIndex = 2;
			this.button_create.Text = "创建证书";
			this.button_create.UseVisualStyleBackColor = true;
			this.button_create.Click += new System.EventHandler(button_create_Click);
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.textBox_hours);
			this.groupBox2.Controls.Add(this.label_hours);
			this.groupBox2.Controls.Add(this.textBox_createDate);
			this.groupBox2.Controls.Add(this.label11);
			this.groupBox2.Controls.Add(this.button1);
			this.groupBox2.Controls.Add(this.textBox_Descriptions);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Controls.Add(this.textBox_Unique);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox_KeyWord);
			this.groupBox2.Controls.Add(this.label8);
			this.groupBox2.Controls.Add(this.textBox_NotAfter);
			this.groupBox2.Controls.Add(this.label7);
			this.groupBox2.Controls.Add(this.textBox_NotBefore);
			this.groupBox2.Controls.Add(this.label6);
			this.groupBox2.Controls.Add(this.textBox_To);
			this.groupBox2.Controls.Add(this.label5);
			this.groupBox2.Controls.Add(this.textBox_From);
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Location = new System.Drawing.Point(3, 304);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(988, 258);
			this.groupBox2.TabIndex = 1;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "证书信息";
			this.textBox_hours.Location = new System.Drawing.Point(316, 195);
			this.textBox_hours.Name = "textBox_hours";
			this.textBox_hours.Size = new System.Drawing.Size(115, 23);
			this.textBox_hours.TabIndex = 18;
			this.textBox_hours.Text = "0";
			this.label_hours.AutoSize = true;
			this.label_hours.Location = new System.Drawing.Point(223, 198);
			this.label_hours.Name = "label_hours";
			this.label_hours.Size = new System.Drawing.Size(59, 17);
			this.label_hours.TabIndex = 17;
			this.label_hours.Text = "有效小时:";
			this.textBox_createDate.Location = new System.Drawing.Point(93, 195);
			this.textBox_createDate.Name = "textBox_createDate";
			this.textBox_createDate.Size = new System.Drawing.Size(115, 23);
			this.textBox_createDate.TabIndex = 16;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(11, 198);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(59, 17);
			this.label11.TabIndex = 15;
			this.label11.Text = "发证日期:";
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(913, 21);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(69, 25);
			this.button1.TabIndex = 14;
			this.button1.Text = "Random";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.textBox_Descriptions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_Descriptions.Location = new System.Drawing.Point(453, 90);
			this.textBox_Descriptions.Multiline = true;
			this.textBox_Descriptions.Name = "textBox_Descriptions";
			this.textBox_Descriptions.Size = new System.Drawing.Size(529, 158);
			this.textBox_Descriptions.TabIndex = 13;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(450, 64);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(227, 17);
			this.label10.TabIndex = 12;
			this.label10.Text = "额外信息(键值对+换行符): 例如： A:你好";
			this.textBox_Unique.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_Unique.Location = new System.Drawing.Point(532, 23);
			this.textBox_Unique.Name = "textBox_Unique";
			this.textBox_Unique.Size = new System.Drawing.Size(374, 23);
			this.textBox_Unique.TabIndex = 11;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(450, 26);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(59, 17);
			this.label9.TabIndex = 10;
			this.label9.Text = "唯一编号:";
			this.textBox_KeyWord.Location = new System.Drawing.Point(93, 225);
			this.textBox_KeyWord.Name = "textBox_KeyWord";
			this.textBox_KeyWord.Size = new System.Drawing.Size(338, 23);
			this.textBox_KeyWord.TabIndex = 9;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(11, 228);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(47, 17);
			this.label8.TabIndex = 8;
			this.label8.Text = "关键字:";
			this.textBox_NotAfter.Location = new System.Drawing.Point(93, 163);
			this.textBox_NotAfter.Name = "textBox_NotAfter";
			this.textBox_NotAfter.Size = new System.Drawing.Size(338, 23);
			this.textBox_NotAfter.TabIndex = 7;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(11, 166);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(59, 17);
			this.label7.TabIndex = 6;
			this.label7.Text = "结束日期:";
			this.textBox_NotBefore.Location = new System.Drawing.Point(93, 132);
			this.textBox_NotBefore.Name = "textBox_NotBefore";
			this.textBox_NotBefore.Size = new System.Drawing.Size(338, 23);
			this.textBox_NotBefore.TabIndex = 5;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(11, 135);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(59, 17);
			this.label6.TabIndex = 4;
			this.label6.Text = "开始日期:";
			this.textBox_To.Location = new System.Drawing.Point(93, 76);
			this.textBox_To.Multiline = true;
			this.textBox_To.Name = "textBox_To";
			this.textBox_To.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_To.Size = new System.Drawing.Size(338, 50);
			this.textBox_To.TabIndex = 3;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(11, 79);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(47, 17);
			this.label5.TabIndex = 2;
			this.label5.Text = "持有者:";
			this.textBox_From.Location = new System.Drawing.Point(93, 20);
			this.textBox_From.Multiline = true;
			this.textBox_From.Name = "textBox_From";
			this.textBox_From.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_From.Size = new System.Drawing.Size(338, 50);
			this.textBox_From.TabIndex = 1;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(11, 23);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(47, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "发证人:";
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.panel2);
			this.groupBox1.Controls.Add(this.textBox_pri_key);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.textBox_pub_key);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(988, 303);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "公钥及私钥";
			this.panel2.Controls.Add(this.label3);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Location = new System.Drawing.Point(71, 21);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(232, 23);
			this.panel2.TabIndex = 53;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(4, 4);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(68, 17);
			this.label3.TabIndex = 28;
			this.label3.Text = "数据类型：";
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(98, 2);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(50, 21);
			this.radioButton1.TabIndex = 15;
			this.radioButton1.Text = "HEX";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(162, 2);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(60, 21);
			this.radioButton2.TabIndex = 16;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "String";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.textBox_pri_key.AllowDrop = true;
			this.textBox_pri_key.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_pri_key.Location = new System.Drawing.Point(339, 46);
			this.textBox_pri_key.Multiline = true;
			this.textBox_pri_key.Name = "textBox_pri_key";
			this.textBox_pri_key.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_pri_key.Size = new System.Drawing.Size(646, 249);
			this.textBox_pri_key.TabIndex = 52;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(339, 25);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 51;
			this.label4.Text = "私钥：";
			this.textBox_pub_key.AllowDrop = true;
			this.textBox_pub_key.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox_pub_key.Location = new System.Drawing.Point(6, 46);
			this.textBox_pub_key.Multiline = true;
			this.textBox_pub_key.Name = "textBox_pub_key";
			this.textBox_pub_key.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_pub_key.Size = new System.Drawing.Size(327, 249);
			this.textBox_pub_key.TabIndex = 50;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(4, 26);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 49;
			this.label2.Text = "公钥：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/15227913.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "RSA 证书";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 38;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel1);
			base.Name = "FormHslCertificate";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Certificate Test";
			base.Load += new System.EventHandler(Form1_Load);
			this.panel1.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
