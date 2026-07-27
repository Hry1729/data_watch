using System;
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
	public class FormRSADebug : HslFormContent
	{
		private IContainer components = null;

		private Panel panel2;

		private Button button_jiami;

		private Label label3;

		private RadioButton radioButton1;

		private Label label1;

		private TextBox textBox_jiami_result;

		private TextBox textBox_jiami_input;

		private Label label7;

		private Button button_jiemi;

		private UserControlHead userControlHead1;

		private Panel panel3;

		private Label label8;

		private RadioButton radioButton3;

		private RadioButton radioButton4;

		private Panel panel1;

		private RadioButton radioButton2;

		private Label label6;

		private TextBox textBox_jiemi_result;

		private TextBox textBox_jiemi_input;

		private Label label5;

		private TextBox textBox_pri_key;

		private Label label4;

		private TextBox textBox_pub_key;

		private Label label2;

		private Button button1;

		private Label label9;

		private Button button2;

		private Button button3;

		private ComboBox comboBox1;

		public FormRSADebug()
		{
			InitializeComponent();
		}

		private void FormByteTransfer_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
			textBox_pri_key.Text = Convert.ToBase64String(rSACryptoServiceProvider.GetPEMPrivateKey());
			textBox_pub_key.Text = Convert.ToBase64String(rSACryptoServiceProvider.GetPEMPublicKey());
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < 117; i++)
			{
				stringBuilder.Append("1");
			}
			textBox_jiami_input.Text = stringBuilder.ToString();
			comboBox1.DataSource = new object[5]
			{
				new SHA1CryptoServiceProvider(),
				new SHA256CryptoServiceProvider(),
				new SHA384CryptoServiceProvider(),
				new SHA512CryptoServiceProvider(),
				new MD5CryptoServiceProvider()
			};
		}

		private void Language(int language)
		{
			if (language != 1)
			{
				Text = "RSA encryption and decryption tool";
				label7.Text = "Original data: (Drag the file to the box below in non-admin mode)";
				label2.Text = "Public key";
				label4.Text = "Private Key";
				button_jiami.Text = "Encrypt";
				button_jiemi.Text = "Decrypt";
				label1.Text = "Output(HEX)";
				label6.Text = "Output(HEX)";
				label5.Text = "Encrypted data";
				label3.Text = "DataType:";
				label8.Text = "DataType:";
				button1.Text = "Result Save As";
			}
		}

		private void FormByteTransfer_Shown(object sender, EventArgs e)
		{
		}

		private void button3_Click(object sender, EventArgs e)
		{
		}

		private void textBox2_DragEnter(object sender, DragEventArgs e)
		{
		}

		private void textBox2_DragDrop(object sender, DragEventArgs e)
		{
		}

		private void textBox_jiemi_input_DragDrop(object sender, DragEventArgs e)
		{
			string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
			if (File.Exists(path))
			{
				textBox_jiemi_input.Text = File.ReadAllBytes(path).ToHexString(' ');
			}
			textBox_jiami_input.Cursor = Cursors.IBeam;
		}

		private void textBox_jiemi_input_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Link;
				textBox_jiami_input.Cursor = Cursors.Arrow;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void textBox_pri_key_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Link;
				textBox_pri_key.Cursor = Cursors.Arrow;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void textBox_pri_key_DragDrop(object sender, DragEventArgs e)
		{
			string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
			if (File.Exists(path))
			{
				textBox_pri_key.Text = File.ReadAllText(path, Encoding.UTF8);
			}
			textBox_pri_key.Cursor = Cursors.IBeam;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			byte[] array = RSAHelper.CreateRsaProviderFromPrivateKey(textBox_pri_key.Text).DecryptLargeData(textBox_jiemi_input.Text.ToHexBytes());
			if (radioButton3.Checked)
			{
				textBox_jiemi_result.Text = array.ToHexString(' ');
			}
			else
			{
				textBox_jiemi_result.Text = Encoding.UTF8.GetString(array);
			}
		}

		private void textBox_jiami_input_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Link;
				textBox_jiami_input.Cursor = Cursors.Arrow;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void textBox_jiami_input_DragDrop(object sender, DragEventArgs e)
		{
			string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
			if (File.Exists(path))
			{
				textBox_jiami_input.Text = File.ReadAllText(path, Encoding.UTF8);
			}
			textBox_jiami_input.Cursor = Cursors.IBeam;
		}

		private void textBox_pub_key_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Link;
				textBox_pub_key.Cursor = Cursors.Arrow;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void textBox_pub_key_DragDrop(object sender, DragEventArgs e)
		{
			string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
			if (File.Exists(path))
			{
				textBox_pub_key.Text = File.ReadAllText(path, Encoding.UTF8);
			}
			textBox_pub_key.Cursor = Cursors.IBeam;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = RSAHelper.CreateRsaProviderFromPublicKey(textBox_pub_key.Text);
			byte[] array = null;
			array = ((!radioButton1.Checked) ? rSACryptoServiceProvider.EncryptLargeData(Encoding.UTF8.GetBytes(textBox_jiami_input.Text)) : rSACryptoServiceProvider.EncryptLargeData(textBox_jiami_input.Text.ToHexBytes()));
			textBox_jiami_result.Text = array.ToHexString(' ');
			label9.Text = array.Length.ToString() ?? "";
		}

		private object GetHalg()
		{
			return comboBox1.SelectedItem;
		}

		private void button2_Click_1(object sender, EventArgs e)
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = RSAHelper.CreateRsaProviderFromPublicKey(textBox_pub_key.Text);
			byte[] array = null;
			if (rSACryptoServiceProvider.VerifyData((!radioButton1.Checked) ? Encoding.UTF8.GetBytes(textBox_jiami_input.Text) : textBox_jiami_input.Text.ToHexBytes(), GetHalg(), textBox_jiami_result.Text.ToHexBytes()))
			{
				DemoUtils.ShowMessage("Sign verify success!");
			}
			else
			{
				DemoUtils.ShowMessage("Sign verify failed");
			}
		}

		private void button3_Click_1(object sender, EventArgs e)
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = RSAHelper.CreateRsaProviderFromPrivateKey(textBox_pri_key.Text);
			byte[] array = null;
			array = ((!radioButton1.Checked) ? rSACryptoServiceProvider.SignData(Encoding.UTF8.GetBytes(textBox_jiami_input.Text), GetHalg()) : rSACryptoServiceProvider.SignData(textBox_jiami_input.Text.ToHexBytes(), GetHalg()));
			textBox_jiami_result.Text = array.ToHexString(' ');
			label9.Text = array.Length.ToString() ?? "";
		}

		private void button1_Click_1(object sender, EventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				File.WriteAllBytes(saveFileDialog.FileName, textBox_jiami_result.Text.ToHexBytes());
				DemoUtils.ShowMessage("Save Success");
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
			this.panel2 = new System.Windows.Forms.Panel();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.label9 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.panel3 = new System.Windows.Forms.Panel();
			this.label8 = new System.Windows.Forms.Label();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label3 = new System.Windows.Forms.Label();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_jiemi_result = new System.Windows.Forms.TextBox();
			this.textBox_jiemi_input = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_pri_key = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox_pub_key = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button_jiemi = new System.Windows.Forms.Button();
			this.button_jiami = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_jiami_result = new System.Windows.Forms.TextBox();
			this.textBox_jiami_input = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.button3);
			this.panel2.Controls.Add(this.button2);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.button1);
			this.panel2.Controls.Add(this.panel3);
			this.panel2.Controls.Add(this.panel1);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.textBox_jiemi_result);
			this.panel2.Controls.Add(this.textBox_jiemi_input);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.textBox_pri_key);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.textBox_pub_key);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.button_jiemi);
			this.panel2.Controls.Add(this.button_jiami);
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.textBox_jiami_result);
			this.panel2.Controls.Add(this.textBox_jiami_input);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(5, 38);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(994, 601);
			this.panel2.TabIndex = 33;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(326, 340);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(395, 25);
			this.comboBox1.TabIndex = 54;
			this.button3.Location = new System.Drawing.Point(193, 339);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(129, 26);
			this.button3.TabIndex = 53;
			this.button3.Text = "签名(原始数据)";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click_1);
			this.button2.Location = new System.Drawing.Point(100, 339);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(87, 26);
			this.button2.TabIndex = 52;
			this.button2.Text = "验签";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click_1);
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(107, 372);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(34, 17);
			this.label9.TabIndex = 51;
			this.label9.Text = "Size:";
			this.button1.Location = new System.Drawing.Point(328, 370);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(142, 26);
			this.button1.TabIndex = 50;
			this.button1.Text = "加密结果另存为";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click_1);
			this.panel3.Controls.Add(this.label8);
			this.panel3.Controls.Add(this.radioButton3);
			this.panel3.Controls.Add(this.radioButton4);
			this.panel3.Location = new System.Drawing.Point(754, 368);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(232, 28);
			this.panel3.TabIndex = 49;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(4, 4);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(68, 17);
			this.label8.TabIndex = 28;
			this.label8.Text = "数据类型：";
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(98, 2);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(50, 21);
			this.radioButton3.TabIndex = 15;
			this.radioButton3.Text = "HEX";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Checked = true;
			this.radioButton4.Location = new System.Drawing.Point(162, 2);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(60, 21);
			this.radioButton4.TabIndex = 16;
			this.radioButton4.TabStop = true;
			this.radioButton4.Text = "String";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.radioButton1);
			this.panel1.Controls.Add(this.radioButton2);
			this.panel1.Location = new System.Drawing.Point(75, 159);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(232, 23);
			this.panel1.TabIndex = 48;
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
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(626, 375);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(74, 17);
			this.label6.TabIndex = 47;
			this.label6.Text = "输出：(Hex)";
			this.textBox_jiemi_result.AllowDrop = true;
			this.textBox_jiemi_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_jiemi_result.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_jiemi_result.Location = new System.Drawing.Point(475, 399);
			this.textBox_jiemi_result.Multiline = true;
			this.textBox_jiemi_result.Name = "textBox_jiemi_result";
			this.textBox_jiemi_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_jiemi_result.Size = new System.Drawing.Size(514, 196);
			this.textBox_jiemi_result.TabIndex = 46;
			this.textBox_jiemi_input.AllowDrop = true;
			this.textBox_jiemi_input.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_jiemi_input.Location = new System.Drawing.Point(476, 38);
			this.textBox_jiemi_input.Multiline = true;
			this.textBox_jiemi_input.Name = "textBox_jiemi_input";
			this.textBox_jiemi_input.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_jiemi_input.Size = new System.Drawing.Size(513, 119);
			this.textBox_jiemi_input.TabIndex = 45;
			this.textBox_jiemi_input.DragDrop += new System.Windows.Forms.DragEventHandler(textBox_jiemi_input_DragDrop);
			this.textBox_jiemi_input.DragEnter += new System.Windows.Forms.DragEventHandler(textBox_jiemi_input_DragEnter);
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(473, 12);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(100, 17);
			this.label5.TabIndex = 44;
			this.label5.Text = "加密数据(HEX)：";
			this.textBox_pri_key.AllowDrop = true;
			this.textBox_pri_key.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_pri_key.Location = new System.Drawing.Point(476, 184);
			this.textBox_pri_key.Multiline = true;
			this.textBox_pri_key.Name = "textBox_pri_key";
			this.textBox_pri_key.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_pri_key.Size = new System.Drawing.Size(513, 153);
			this.textBox_pri_key.TabIndex = 43;
			this.textBox_pri_key.DragDrop += new System.Windows.Forms.DragEventHandler(textBox_pri_key_DragDrop);
			this.textBox_pri_key.DragEnter += new System.Windows.Forms.DragEventHandler(textBox_pri_key_DragEnter);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(473, 164);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(44, 17);
			this.label4.TabIndex = 42;
			this.label4.Text = "私钥：";
			this.textBox_pub_key.AllowDrop = true;
			this.textBox_pub_key.Location = new System.Drawing.Point(10, 184);
			this.textBox_pub_key.Multiline = true;
			this.textBox_pub_key.Name = "textBox_pub_key";
			this.textBox_pub_key.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_pub_key.Size = new System.Drawing.Size(460, 153);
			this.textBox_pub_key.TabIndex = 41;
			this.textBox_pub_key.DragDrop += new System.Windows.Forms.DragEventHandler(textBox_pub_key_DragDrop);
			this.textBox_pub_key.DragEnter += new System.Windows.Forms.DragEventHandler(textBox_pub_key_DragEnter);
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(8, 164);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 40;
			this.label2.Text = "公钥：";
			this.button_jiemi.Location = new System.Drawing.Point(475, 370);
			this.button_jiemi.Name = "button_jiemi";
			this.button_jiemi.Size = new System.Drawing.Size(110, 26);
			this.button_jiemi.TabIndex = 32;
			this.button_jiemi.Text = "解密";
			this.button_jiemi.UseVisualStyleBackColor = true;
			this.button_jiemi.Click += new System.EventHandler(button2_Click);
			this.button_jiami.Location = new System.Drawing.Point(11, 339);
			this.button_jiami.Name = "button_jiami";
			this.button_jiami.Size = new System.Drawing.Size(83, 26);
			this.button_jiami.TabIndex = 31;
			this.button_jiami.Text = "加密";
			this.button_jiami.UseVisualStyleBackColor = true;
			this.button_jiami.Click += new System.EventHandler(button1_Click);
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 372);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(74, 17);
			this.label1.TabIndex = 14;
			this.label1.Text = "输出：(Hex)";
			this.textBox_jiami_result.AllowDrop = true;
			this.textBox_jiami_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox_jiami_result.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox_jiami_result.Location = new System.Drawing.Point(10, 399);
			this.textBox_jiami_result.Multiline = true;
			this.textBox_jiami_result.Name = "textBox_jiami_result";
			this.textBox_jiami_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_jiami_result.Size = new System.Drawing.Size(460, 196);
			this.textBox_jiami_result.TabIndex = 8;
			this.textBox_jiami_result.DragDrop += new System.Windows.Forms.DragEventHandler(textBox2_DragDrop);
			this.textBox_jiami_result.DragEnter += new System.Windows.Forms.DragEventHandler(textBox2_DragEnter);
			this.textBox_jiami_input.AllowDrop = true;
			this.textBox_jiami_input.Location = new System.Drawing.Point(11, 38);
			this.textBox_jiami_input.Multiline = true;
			this.textBox_jiami_input.Name = "textBox_jiami_input";
			this.textBox_jiami_input.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_jiami_input.Size = new System.Drawing.Size(459, 119);
			this.textBox_jiami_input.TabIndex = 9;
			this.textBox_jiami_input.DragDrop += new System.Windows.Forms.DragEventHandler(textBox_jiami_input_DragDrop);
			this.textBox_jiami_input.DragEnter += new System.Windows.Forms.DragEventHandler(textBox_jiami_input_DragEnter);
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(3, 8);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(292, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "原始数据：(非管理员模式下文件拖到下面的框里即可)";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/15227913.html";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "RSA非对称加密解密";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 34;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormRSADebug";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "RSA加密解密工具";
			base.Load += new System.EventHandler(FormByteTransfer_Load);
			base.Shown += new System.EventHandler(FormByteTransfer_Shown);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
