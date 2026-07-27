using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo
{
	public class FormByteTransfer : HslFormContent
	{
		private IByteTransform byteTransform = new RegularByteTransform();

		private IContainer components = null;

		private Panel panel2;

		private Button button1;

		private Label label8;

		private Label label6;

		private Label label3;

		private RadioButton radioButton_utf32;

		private RadioButton radioButton_utf8;

		private RadioButton radioButton_unicode;

		private RadioButton radioButton_ascii;

		private RadioButton radioButton9;

		private RadioButton radioButton8;

		private RadioButton radioButton7;

		private RadioButton radioButton6;

		private RadioButton radioButton5;

		private RadioButton radioButton4;

		private RadioButton radioButton3;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private Label label1;

		private TextBox textBox2;

		private TextBox textBox_input;

		private Label label7;

		private Button button2;

		private RadioButton radioButton_ansi;

		private UserControlHead userControlHead1;

		private RadioButton radioButton_dateTime_s;

		private Label label5;

		private Button button_open_file;

		private Button button4;

		private Button button5;

		private RadioButton radioButton_gb2312;

		private RadioButton radioButton_unicode_big;

		private RadioButton radioButton_base64;

		private Button button_url_decode;

		private Button button_url_encode;

		private Button button_save_file;

		private RadioButton radioButton_dateTime_ms;

		private TextBox textBox_datetime;

		private Label label2;

		private Label label4;

		private Label label_byte_count;

		private Label label9;

		private ComboBox comboBox1;

		private Label label10;

		private CheckBox checkBox_isStringReverseByWord;

		public FormByteTransfer()
		{
			InitializeComponent();
			comboBox1.SelectedIndex = 3;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (comboBox1.SelectedIndex)
			{
			case 0:
				byteTransform.DataFormat = DataFormat.ABCD;
				break;
			case 1:
				byteTransform.DataFormat = DataFormat.BADC;
				break;
			case 2:
				byteTransform.DataFormat = DataFormat.CDAB;
				break;
			case 3:
				byteTransform.DataFormat = DataFormat.DCBA;
				break;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			byte[] array = null;
			RadioButton radioButton = null;
			try
			{
				if (radioButton1.Checked)
				{
					array = textBox_input.Text.ToStringArray<byte>();
					radioButton = radioButton1;
				}
				else if (radioButton2.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<short>());
					radioButton = radioButton2;
				}
				else if (radioButton3.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<ushort>());
					radioButton = radioButton3;
				}
				else if (radioButton4.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<int>());
					radioButton = radioButton4;
				}
				else if (radioButton5.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<uint>());
					radioButton = radioButton5;
				}
				else if (radioButton6.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<long>());
					radioButton = radioButton6;
				}
				else if (radioButton7.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<ulong>());
					radioButton = radioButton7;
				}
				else if (radioButton8.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<float>());
					radioButton = radioButton8;
				}
				else if (radioButton9.Checked)
				{
					array = byteTransform.TransByte(textBox_input.Text.ToStringArray<double>());
					radioButton = radioButton9;
				}
				else if (radioButton_ascii.Checked)
				{
					array = Encoding.ASCII.GetBytes(textBox_input.Text);
					if (checkBox_isStringReverseByWord.Checked)
					{
						array = SoftBasic.BytesReverseByWord(array);
					}
					radioButton = radioButton_ascii;
				}
				else if (radioButton_unicode.Checked)
				{
					array = Encoding.Unicode.GetBytes(textBox_input.Text);
					if (checkBox_isStringReverseByWord.Checked)
					{
						array = SoftBasic.BytesReverseByWord(array);
					}
					radioButton = radioButton_unicode;
				}
				else if (radioButton_utf8.Checked)
				{
					array = Encoding.UTF8.GetBytes(textBox_input.Text);
					if (checkBox_isStringReverseByWord.Checked)
					{
						array = SoftBasic.BytesReverseByWord(array);
					}
					radioButton = radioButton_utf8;
				}
				else if (radioButton_utf32.Checked)
				{
					array = Encoding.UTF32.GetBytes(textBox_input.Text);
					if (checkBox_isStringReverseByWord.Checked)
					{
						array = SoftBasic.BytesReverseByWord(array);
					}
					radioButton = radioButton_utf32;
				}
				else if (radioButton_ansi.Checked)
				{
					array = Encoding.Default.GetBytes(textBox_input.Text);
					if (checkBox_isStringReverseByWord.Checked)
					{
						array = SoftBasic.BytesReverseByWord(array);
					}
					radioButton = radioButton_ansi;
				}
				else
				{
					if (radioButton_dateTime_s.Checked)
					{
						DateTime dateTime = DateTime.Parse(textBox_datetime.Text);
						double value = double.Parse(textBox_input.Text);
						radioButton = radioButton_dateTime_s;
						textBox2.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + textBox_input.Text + "] [" + radioButton.Text.PadRight(7, ' ') + "] Time " + dateTime.AddSeconds(value).ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
						return;
					}
					if (radioButton_gb2312.Checked)
					{
						array = Encoding.GetEncoding("gb2312").GetBytes(textBox_input.Text);
						radioButton = radioButton_gb2312;
					}
					else if (radioButton_unicode_big.Checked)
					{
						array = Encoding.BigEndianUnicode.GetBytes(textBox_input.Text);
						radioButton = radioButton_unicode_big;
					}
					else if (radioButton_base64.Checked)
					{
						array = Convert.FromBase64String(textBox_input.Text);
						radioButton = radioButton_base64;
					}
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
				return;
			}
			textBox2.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + textBox_input.Text + "] [" + radioButton.Text.PadRight(7, ' ') + "] Length[" + array.Length + "] " + SoftBasic.ByteToHexString(array, ' ') + Environment.NewLine);
			label_byte_count.Text = ((array == null) ? "0" : array.Length.ToString());
		}

		private void FormByteTransfer_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language != 1)
			{
				Text = "ByteTransform Tools";
				label7.Text = "Input value:";
				label3.Text = "Integer:";
				label6.Text = "Float:";
				label8.Text = "String:";
				label1.Text = "Output:\r\n(Hex)";
				button1.Text = "Conver-byte[]";
				button2.Text = "Re-conversion";
				label5.Text = "DateTime:";
				label4.Text = "Byte Count:";
				label2.Text = "Start";
				button_open_file.Text = "Open File";
				button_save_file.Text = "Save File";
				button4.Text = "Calcu MD5";
				button5.Text = "ImageBase64";
			}
		}

		private void FormByteTransfer_Shown(object sender, EventArgs e)
		{
			textBox_input.Focus();
		}

		private string GetRenderString<T>(Func<byte[], T[]> tarns)
		{
			T[] array = tarns(SoftBasic.HexStringToBytes(textBox_input.Text));
			return (array.Length == 1) ? array[0].ToString() : array.ToArrayString();
		}

		private string GetEncodingString(Func<byte[], string> encoding)
		{
			byte[] array = SoftBasic.HexStringToBytes(textBox_input.Text);
			if (checkBox_isStringReverseByWord.Checked)
			{
				array = SoftBasic.BytesReverseByWord(array);
			}
			return encoding(array);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			string text = string.Empty;
			RadioButton radioButton = null;
			try
			{
				if (radioButton1.Checked)
				{
					text = SoftBasic.HexStringToBytes(textBox_input.Text)[0].ToString();
					radioButton = radioButton1;
				}
				else if (radioButton2.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransInt16(m, 0, m.Length / 2));
					radioButton = radioButton2;
				}
				else if (radioButton3.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransUInt16(m, 0, m.Length / 2));
					radioButton = radioButton3;
				}
				else if (radioButton4.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransInt32(m, 0, m.Length / 4));
					radioButton = radioButton4;
				}
				else if (radioButton5.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransUInt32(m, 0, m.Length / 4));
					radioButton = radioButton5;
				}
				else if (radioButton6.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransInt64(m, 0, m.Length / 8));
					radioButton = radioButton6;
				}
				else if (radioButton7.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransUInt64(m, 0, m.Length / 8));
					radioButton = radioButton7;
				}
				else if (radioButton8.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransSingle(m, 0, m.Length / 4));
					radioButton = radioButton8;
				}
				else if (radioButton9.Checked)
				{
					text = GetRenderString((byte[] m) => byteTransform.TransDouble(m, 0, m.Length / 8));
					radioButton = radioButton9;
				}
				else if (radioButton_ascii.Checked)
				{
					text = GetEncodingString(Encoding.ASCII.GetString);
					radioButton = radioButton_ascii;
				}
				else if (radioButton_unicode.Checked)
				{
					text = GetEncodingString(Encoding.Unicode.GetString);
					radioButton = radioButton_unicode;
				}
				else if (radioButton_utf8.Checked)
				{
					text = GetEncodingString(Encoding.UTF8.GetString);
					radioButton = radioButton_utf8;
				}
				else if (radioButton_utf32.Checked)
				{
					text = GetEncodingString(Encoding.UTF32.GetString);
					radioButton = radioButton_utf32;
				}
				else if (radioButton_ansi.Checked)
				{
					text = GetEncodingString(Encoding.Default.GetString);
					radioButton = radioButton_utf32;
				}
				else if (radioButton_gb2312.Checked)
				{
					text = GetEncodingString(Encoding.GetEncoding("gb2312").GetString);
					radioButton = radioButton_gb2312;
				}
				else if (radioButton_unicode_big.Checked)
				{
					text = GetEncodingString(Encoding.BigEndianUnicode.GetString);
					radioButton = radioButton_unicode_big;
				}
				else if (radioButton_base64.Checked)
				{
					text = Convert.ToBase64String(SoftBasic.HexStringToBytes(textBox_input.Text));
					radioButton = radioButton_base64;
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
				return;
			}
			textBox2.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + textBox_input.Text + "] [" + radioButton.Text.PadRight(7, ' ') + "]  " + text + Environment.NewLine);
		}

		private void OpenFile(string filePath)
		{
			if (File.Exists(filePath))
			{
				byte[] array = File.ReadAllBytes(filePath);
				label_byte_count.Text = ((array == null) ? "0" : array.Length.ToString());
				if (radioButton_base64.Checked)
				{
					textBox2.Text = Convert.ToBase64String(array);
				}
				else
				{
					textBox2.Text = SoftBasic.ByteToHexString(array, ' ', 32);
				}
			}
			else
			{
				DemoUtils.ShowMessage("File[" + filePath + "] not exist");
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			try
			{
				if (string.IsNullOrEmpty(textBox_input.Text))
				{
					OpenFileDialog openFileDialog = new OpenFileDialog();
					if (openFileDialog.ShowDialog() == DialogResult.OK)
					{
						textBox_input.Text = openFileDialog.FileName;
						OpenFile(openFileDialog.FileName);
					}
				}
				else
				{
					OpenFile(textBox_input.Text);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Open File Failed:" + ex.Message);
			}
		}

		private void button_save_file_Click(object sender, EventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				File.WriteAllBytes(saveFileDialog.FileName, textBox2.Text.ToHexBytes());
			}
		}

		private void textBox2_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Link;
				textBox_input.Cursor = Cursors.Arrow;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void textBox2_DragDrop(object sender, DragEventArgs e)
		{
			string text = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
			textBox_input.Text = text;
			button3_Click(sender, e);
			textBox_input.Cursor = Cursors.IBeam;
		}

		private void button4_Click(object sender, EventArgs e)
		{
			try
			{
				if (string.IsNullOrEmpty(textBox_input.Text))
				{
					OpenFileDialog openFileDialog = new OpenFileDialog();
					if (openFileDialog.ShowDialog() == DialogResult.OK)
					{
						textBox_input.Text = openFileDialog.FileName;
						DateTime now = DateTime.Now;
						textBox2.Text = SoftBasic.CalculateFileMD5(openFileDialog.FileName);
						textBox2.AppendText(Environment.NewLine + "Total Coust:" + (DateTime.Now - now).TotalSeconds.ToString("F2") + " s");
					}
				}
				else
				{
					DateTime now2 = DateTime.Now;
					textBox2.Text = SoftBasic.CalculateFileMD5(textBox_input.Text);
					textBox2.AppendText(Environment.NewLine + "Total Coust:" + (DateTime.Now - now2).TotalSeconds.ToString("F2") + " s");
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("Failed:" + ex.Message);
			}
		}

		private string PrintBase64(byte[] content)
		{
			int num = 0;
			string text = Convert.ToBase64String(content);
			StringBuilder stringBuilder = new StringBuilder();
			while (num < text.Length)
			{
				int num2 = Math.Min(text.Length - num, 120);
				stringBuilder.Append(text.Substring(num, num2));
				num += num2;
				if (num < text.Length)
				{
					stringBuilder.Append(Environment.NewLine);
				}
			}
			return stringBuilder.ToString();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			try
			{
				if (!string.IsNullOrEmpty(textBox_input.Text))
				{
					if (File.Exists(textBox_input.Text))
					{
						byte[] content = File.ReadAllBytes(textBox_input.Text);
						textBox2.Text = PrintBase64(content);
					}
				}
				else if (Clipboard.ContainsImage())
				{
					using (MemoryStream memoryStream = new MemoryStream())
					{
						Clipboard.GetImage().Save(memoryStream, ImageFormat.Png);
						byte[] content2 = memoryStream.ToArray();
						textBox2.Text = PrintBase64(content2);
						return;
					}
				}
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		private void button_url_encode_Click(object sender, EventArgs e)
		{
			Encoding e2 = Encoding.UTF8;
			if (radioButton_ascii.Checked)
			{
				e2 = Encoding.ASCII;
			}
			else if (radioButton_unicode.Checked)
			{
				e2 = Encoding.Unicode;
			}
			else if (radioButton_utf8.Checked)
			{
				e2 = Encoding.UTF8;
			}
			else if (radioButton_utf32.Checked)
			{
				e2 = Encoding.UTF32;
			}
			else if (radioButton_ansi.Checked)
			{
				e2 = Encoding.Default;
			}
			else if (radioButton_gb2312.Checked)
			{
				e2 = Encoding.GetEncoding("gb2312");
			}
			else if (radioButton_unicode_big.Checked)
			{
				e2 = Encoding.BigEndianUnicode;
			}
			textBox2.Text = SoftBasic.UrlEncode(textBox_input.Text, e2);
		}

		private void button_url_decode_Click(object sender, EventArgs e)
		{
			Encoding encoding = Encoding.UTF8;
			if (radioButton_ascii.Checked)
			{
				encoding = Encoding.ASCII;
			}
			else if (radioButton_unicode.Checked)
			{
				encoding = Encoding.Unicode;
			}
			else if (radioButton_utf8.Checked)
			{
				encoding = Encoding.UTF8;
			}
			else if (radioButton_utf32.Checked)
			{
				encoding = Encoding.UTF32;
			}
			else if (radioButton_ansi.Checked)
			{
				encoding = Encoding.Default;
			}
			else if (radioButton_gb2312.Checked)
			{
				encoding = Encoding.GetEncoding("gb2312");
			}
			else if (radioButton_unicode_big.Checked)
			{
				encoding = Encoding.BigEndianUnicode;
			}
			textBox2.Text = SoftBasic.UrlDecode(textBox_input.Text, encoding);
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
			this.checkBox_isStringReverseByWord = new System.Windows.Forms.CheckBox();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label10 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.label_byte_count = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.radioButton_dateTime_ms = new System.Windows.Forms.RadioButton();
			this.textBox_datetime = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button_save_file = new System.Windows.Forms.Button();
			this.button_url_decode = new System.Windows.Forms.Button();
			this.button_url_encode = new System.Windows.Forms.Button();
			this.radioButton_base64 = new System.Windows.Forms.RadioButton();
			this.radioButton_unicode_big = new System.Windows.Forms.RadioButton();
			this.radioButton_gb2312 = new System.Windows.Forms.RadioButton();
			this.button5 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.button_open_file = new System.Windows.Forms.Button();
			this.radioButton_dateTime_s = new System.Windows.Forms.RadioButton();
			this.label5 = new System.Windows.Forms.Label();
			this.radioButton_ansi = new System.Windows.Forms.RadioButton();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.label8 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.radioButton_utf32 = new System.Windows.Forms.RadioButton();
			this.radioButton_utf8 = new System.Windows.Forms.RadioButton();
			this.radioButton_unicode = new System.Windows.Forms.RadioButton();
			this.radioButton_ascii = new System.Windows.Forms.RadioButton();
			this.radioButton9 = new System.Windows.Forms.RadioButton();
			this.radioButton8 = new System.Windows.Forms.RadioButton();
			this.radioButton7 = new System.Windows.Forms.RadioButton();
			this.radioButton6 = new System.Windows.Forms.RadioButton();
			this.radioButton5 = new System.Windows.Forms.RadioButton();
			this.radioButton4 = new System.Windows.Forms.RadioButton();
			this.radioButton3 = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox_input = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.checkBox_isStringReverseByWord);
			this.panel2.Controls.Add(this.comboBox1);
			this.panel2.Controls.Add(this.label10);
			this.panel2.Controls.Add(this.label9);
			this.panel2.Controls.Add(this.label_byte_count);
			this.panel2.Controls.Add(this.label4);
			this.panel2.Controls.Add(this.radioButton_dateTime_ms);
			this.panel2.Controls.Add(this.textBox_datetime);
			this.panel2.Controls.Add(this.label2);
			this.panel2.Controls.Add(this.button_save_file);
			this.panel2.Controls.Add(this.button_url_decode);
			this.panel2.Controls.Add(this.button_url_encode);
			this.panel2.Controls.Add(this.radioButton_base64);
			this.panel2.Controls.Add(this.radioButton_unicode_big);
			this.panel2.Controls.Add(this.radioButton_gb2312);
			this.panel2.Controls.Add(this.button5);
			this.panel2.Controls.Add(this.button4);
			this.panel2.Controls.Add(this.button_open_file);
			this.panel2.Controls.Add(this.radioButton_dateTime_s);
			this.panel2.Controls.Add(this.label5);
			this.panel2.Controls.Add(this.radioButton_ansi);
			this.panel2.Controls.Add(this.button2);
			this.panel2.Controls.Add(this.button1);
			this.panel2.Controls.Add(this.label8);
			this.panel2.Controls.Add(this.label6);
			this.panel2.Controls.Add(this.label3);
			this.panel2.Controls.Add(this.radioButton_utf32);
			this.panel2.Controls.Add(this.radioButton_utf8);
			this.panel2.Controls.Add(this.radioButton_unicode);
			this.panel2.Controls.Add(this.radioButton_ascii);
			this.panel2.Controls.Add(this.radioButton9);
			this.panel2.Controls.Add(this.radioButton8);
			this.panel2.Controls.Add(this.radioButton7);
			this.panel2.Controls.Add(this.radioButton6);
			this.panel2.Controls.Add(this.radioButton5);
			this.panel2.Controls.Add(this.radioButton4);
			this.panel2.Controls.Add(this.radioButton3);
			this.panel2.Controls.Add(this.radioButton2);
			this.panel2.Controls.Add(this.radioButton1);
			this.panel2.Controls.Add(this.label1);
			this.panel2.Controls.Add(this.textBox2);
			this.panel2.Controls.Add(this.textBox_input);
			this.panel2.Controls.Add(this.label7);
			this.panel2.Location = new System.Drawing.Point(3, 35);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(997, 606);
			this.panel2.TabIndex = 33;
			this.checkBox_isStringReverseByWord.AutoSize = true;
			this.checkBox_isStringReverseByWord.ForeColor = System.Drawing.Color.Green;
			this.checkBox_isStringReverseByWord.Location = new System.Drawing.Point(720, 162);
			this.checkBox_isStringReverseByWord.Name = "checkBox_isStringReverseByWord";
			this.checkBox_isStringReverseByWord.Size = new System.Drawing.Size(181, 21);
			this.checkBox_isStringReverseByWord.TabIndex = 54;
			this.checkBox_isStringReverseByWord.Text = "IsStringReverseByteWord?";
			this.checkBox_isStringReverseByWord.UseVisualStyleBackColor = true;
			this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[4] { "ABCD", "BADC", "CDAB", "DCBA" });
			this.comboBox1.Location = new System.Drawing.Point(907, 106);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(85, 25);
			this.comboBox1.TabIndex = 53;
			this.label10.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label10.AutoSize = true;
			this.label10.ForeColor = System.Drawing.Color.Green;
			this.label10.Location = new System.Drawing.Point(805, 109);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(96, 17);
			this.label10.TabIndex = 52;
			this.label10.Text = "ByteTransform:";
			this.label9.AutoSize = true;
			this.label9.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.label9.Location = new System.Drawing.Point(595, 109);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(145, 17);
			this.label9.TabIndex = 51;
			this.label9.Text = "if use array input [1,2,3]";
			this.label_byte_count.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_byte_count.AutoSize = true;
			this.label_byte_count.Location = new System.Drawing.Point(2, 583);
			this.label_byte_count.Name = "label_byte_count";
			this.label_byte_count.Size = new System.Drawing.Size(15, 17);
			this.label_byte_count.TabIndex = 50;
			this.label_byte_count.Text = "0";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(2, 564);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(51, 17);
			this.label4.TabIndex = 49;
			this.label4.Text = "字节数: ";
			this.radioButton_dateTime_ms.AutoSize = true;
			this.radioButton_dateTime_ms.Location = new System.Drawing.Point(218, 189);
			this.radioButton_dateTime_ms.Name = "radioButton_dateTime_ms";
			this.radioButton_dateTime_ms.Size = new System.Drawing.Size(99, 21);
			this.radioButton_dateTime_ms.TabIndex = 48;
			this.radioButton_dateTime_ms.Text = "datetime-ms";
			this.radioButton_dateTime_ms.UseVisualStyleBackColor = true;
			this.textBox_datetime.Location = new System.Drawing.Point(450, 188);
			this.textBox_datetime.Name = "textBox_datetime";
			this.textBox_datetime.Size = new System.Drawing.Size(144, 23);
			this.textBox_datetime.TabIndex = 47;
			this.textBox_datetime.Text = "1970-1-1 08:00:00";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(372, 191);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 46;
			this.label2.Text = "起始时间：";
			this.button_save_file.Location = new System.Drawing.Point(450, 216);
			this.button_save_file.Name = "button_save_file";
			this.button_save_file.Size = new System.Drawing.Size(103, 26);
			this.button_save_file.TabIndex = 45;
			this.button_save_file.Text = "保存文件";
			this.button_save_file.UseVisualStyleBackColor = true;
			this.button_save_file.Click += new System.EventHandler(button_save_file_Click);
			this.button_url_decode.Location = new System.Drawing.Point(893, 216);
			this.button_url_decode.Name = "button_url_decode";
			this.button_url_decode.Size = new System.Drawing.Size(96, 26);
			this.button_url_decode.TabIndex = 44;
			this.button_url_decode.Text = "Url Decode";
			this.button_url_decode.UseVisualStyleBackColor = true;
			this.button_url_decode.Click += new System.EventHandler(button_url_decode_Click);
			this.button_url_encode.Location = new System.Drawing.Point(791, 216);
			this.button_url_encode.Name = "button_url_encode";
			this.button_url_encode.Size = new System.Drawing.Size(96, 26);
			this.button_url_encode.TabIndex = 43;
			this.button_url_encode.Text = "Url Encode";
			this.button_url_encode.UseVisualStyleBackColor = true;
			this.button_url_encode.Click += new System.EventHandler(button_url_encode_Click);
			this.radioButton_base64.AutoSize = true;
			this.radioButton_base64.Location = new System.Drawing.Point(613, 161);
			this.radioButton_base64.Name = "radioButton_base64";
			this.radioButton_base64.Size = new System.Drawing.Size(68, 21);
			this.radioButton_base64.TabIndex = 42;
			this.radioButton_base64.Text = "Base64";
			this.radioButton_base64.UseVisualStyleBackColor = true;
			this.radioButton_unicode_big.AutoSize = true;
			this.radioButton_unicode_big.Location = new System.Drawing.Point(311, 161);
			this.radioButton_unicode_big.Name = "radioButton_unicode_big";
			this.radioButton_unicode_big.Size = new System.Drawing.Size(96, 21);
			this.radioButton_unicode_big.TabIndex = 41;
			this.radioButton_unicode_big.Text = "unicode-big";
			this.radioButton_unicode_big.UseVisualStyleBackColor = true;
			this.radioButton_gb2312.AutoSize = true;
			this.radioButton_gb2312.Location = new System.Drawing.Point(536, 161);
			this.radioButton_gb2312.Name = "radioButton_gb2312";
			this.radioButton_gb2312.Size = new System.Drawing.Size(71, 21);
			this.radioButton_gb2312.TabIndex = 40;
			this.radioButton_gb2312.Text = "GB2312";
			this.radioButton_gb2312.UseVisualStyleBackColor = true;
			this.button5.Location = new System.Drawing.Point(675, 216);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(110, 26);
			this.button5.TabIndex = 39;
			this.button5.Text = "图片base64";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.button4.Location = new System.Drawing.Point(559, 216);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(110, 26);
			this.button4.TabIndex = 38;
			this.button4.Text = "计算MD5";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button_open_file.Location = new System.Drawing.Point(334, 216);
			this.button_open_file.Name = "button_open_file";
			this.button_open_file.Size = new System.Drawing.Size(110, 26);
			this.button_open_file.TabIndex = 37;
			this.button_open_file.Text = "打开文件";
			this.button_open_file.UseVisualStyleBackColor = true;
			this.button_open_file.Click += new System.EventHandler(button3_Click);
			this.radioButton_dateTime_s.AutoSize = true;
			this.radioButton_dateTime_s.Location = new System.Drawing.Point(102, 189);
			this.radioButton_dateTime_s.Name = "radioButton_dateTime_s";
			this.radioButton_dateTime_s.Size = new System.Drawing.Size(88, 21);
			this.radioButton_dateTime_s.TabIndex = 36;
			this.radioButton_dateTime_s.Text = "datetime-s";
			this.radioButton_dateTime_s.UseVisualStyleBackColor = true;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(8, 191);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 17);
			this.label5.TabIndex = 35;
			this.label5.Text = "时间数据：";
			this.radioButton_ansi.AutoSize = true;
			this.radioButton_ansi.Location = new System.Drawing.Point(166, 161);
			this.radioButton_ansi.Name = "radioButton_ansi";
			this.radioButton_ansi.Size = new System.Drawing.Size(49, 21);
			this.radioButton_ansi.TabIndex = 33;
			this.radioButton_ansi.Text = "ansi";
			this.radioButton_ansi.UseVisualStyleBackColor = true;
			this.button2.Location = new System.Drawing.Point(218, 216);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(110, 26);
			this.button2.TabIndex = 32;
			this.button2.Text = "反向转换";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(102, 216);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(110, 26);
			this.button1.TabIndex = 31;
			this.button1.Text = "转换byte[]";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(8, 163);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(68, 17);
			this.label8.TabIndex = 30;
			this.label8.Text = "字符数据：";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 136);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 17);
			this.label6.TabIndex = 29;
			this.label6.Text = "浮点数据：";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 109);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(68, 17);
			this.label3.TabIndex = 28;
			this.label3.Text = "整型数据：";
			this.radioButton_utf32.AutoSize = true;
			this.radioButton_utf32.Location = new System.Drawing.Point(470, 161);
			this.radioButton_utf32.Name = "radioButton_utf32";
			this.radioButton_utf32.Size = new System.Drawing.Size(60, 21);
			this.radioButton_utf32.TabIndex = 27;
			this.radioButton_utf32.Text = "utf-32";
			this.radioButton_utf32.UseVisualStyleBackColor = true;
			this.radioButton_utf8.AutoSize = true;
			this.radioButton_utf8.Location = new System.Drawing.Point(411, 161);
			this.radioButton_utf8.Name = "radioButton_utf8";
			this.radioButton_utf8.Size = new System.Drawing.Size(53, 21);
			this.radioButton_utf8.TabIndex = 26;
			this.radioButton_utf8.Text = "utf-8";
			this.radioButton_utf8.UseVisualStyleBackColor = true;
			this.radioButton_unicode.AutoSize = true;
			this.radioButton_unicode.Location = new System.Drawing.Point(233, 161);
			this.radioButton_unicode.Name = "radioButton_unicode";
			this.radioButton_unicode.Size = new System.Drawing.Size(72, 21);
			this.radioButton_unicode.TabIndex = 25;
			this.radioButton_unicode.Text = "unicode";
			this.radioButton_unicode.UseVisualStyleBackColor = true;
			this.radioButton_ascii.AutoSize = true;
			this.radioButton_ascii.Location = new System.Drawing.Point(102, 161);
			this.radioButton_ascii.Name = "radioButton_ascii";
			this.radioButton_ascii.Size = new System.Drawing.Size(51, 21);
			this.radioButton_ascii.TabIndex = 24;
			this.radioButton_ascii.Text = "ascii";
			this.radioButton_ascii.UseVisualStyleBackColor = true;
			this.radioButton9.AutoSize = true;
			this.radioButton9.Location = new System.Drawing.Point(166, 134);
			this.radioButton9.Name = "radioButton9";
			this.radioButton9.Size = new System.Drawing.Size(67, 21);
			this.radioButton9.TabIndex = 23;
			this.radioButton9.Text = "double";
			this.radioButton9.UseVisualStyleBackColor = true;
			this.radioButton8.AutoSize = true;
			this.radioButton8.Location = new System.Drawing.Point(102, 134);
			this.radioButton8.Name = "radioButton8";
			this.radioButton8.Size = new System.Drawing.Size(52, 21);
			this.radioButton8.TabIndex = 22;
			this.radioButton8.Text = "float";
			this.radioButton8.UseVisualStyleBackColor = true;
			this.radioButton7.AutoSize = true;
			this.radioButton7.Location = new System.Drawing.Point(502, 107);
			this.radioButton7.Name = "radioButton7";
			this.radioButton7.Size = new System.Drawing.Size(59, 21);
			this.radioButton7.TabIndex = 21;
			this.radioButton7.Text = "ulong";
			this.radioButton7.UseVisualStyleBackColor = true;
			this.radioButton6.AutoSize = true;
			this.radioButton6.Location = new System.Drawing.Point(434, 107);
			this.radioButton6.Name = "radioButton6";
			this.radioButton6.Size = new System.Drawing.Size(52, 21);
			this.radioButton6.TabIndex = 20;
			this.radioButton6.Text = "long";
			this.radioButton6.UseVisualStyleBackColor = true;
			this.radioButton5.AutoSize = true;
			this.radioButton5.Location = new System.Drawing.Point(363, 107);
			this.radioButton5.Name = "radioButton5";
			this.radioButton5.Size = new System.Drawing.Size(61, 21);
			this.radioButton5.TabIndex = 19;
			this.radioButton5.Text = "uint32";
			this.radioButton5.UseVisualStyleBackColor = true;
			this.radioButton4.AutoSize = true;
			this.radioButton4.Checked = true;
			this.radioButton4.Location = new System.Drawing.Point(306, 107);
			this.radioButton4.Name = "radioButton4";
			this.radioButton4.Size = new System.Drawing.Size(54, 21);
			this.radioButton4.TabIndex = 18;
			this.radioButton4.TabStop = true;
			this.radioButton4.Text = "int32";
			this.radioButton4.UseVisualStyleBackColor = true;
			this.radioButton3.AutoSize = true;
			this.radioButton3.Location = new System.Drawing.Point(233, 107);
			this.radioButton3.Name = "radioButton3";
			this.radioButton3.Size = new System.Drawing.Size(63, 21);
			this.radioButton3.TabIndex = 17;
			this.radioButton3.Text = "ushort";
			this.radioButton3.UseVisualStyleBackColor = true;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Location = new System.Drawing.Point(166, 107);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(56, 21);
			this.radioButton2.TabIndex = 16;
			this.radioButton2.Text = "short";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(102, 107);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(51, 21);
			this.radioButton1.TabIndex = 15;
			this.radioButton1.Text = "byte";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(8, 250);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(44, 34);
			this.label1.TabIndex = 14;
			this.label1.Text = "输出：\r\n(Hex)";
			this.textBox2.AllowDrop = true;
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox2.Font = new System.Drawing.Font("宋体", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox2.Location = new System.Drawing.Point(102, 248);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox2.Size = new System.Drawing.Size(890, 353);
			this.textBox2.TabIndex = 8;
			this.textBox2.DragDrop += new System.Windows.Forms.DragEventHandler(textBox2_DragDrop);
			this.textBox2.DragEnter += new System.Windows.Forms.DragEventHandler(textBox2_DragEnter);
			this.textBox_input.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_input.Location = new System.Drawing.Point(102, 5);
			this.textBox_input.Multiline = true;
			this.textBox_input.Name = "textBox_input";
			this.textBox_input.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_input.Size = new System.Drawing.Size(890, 96);
			this.textBox_input.TabIndex = 9;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 7);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(92, 17);
			this.label7.TabIndex = 7;
			this.label7.Text = "等待转换的值：";
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.HelpLink = "https://www.cnblogs.com/dathlin/p/7703805.htmln";
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "Byte";
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
			base.Name = "FormByteTransfer";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "字节转换工具";
			base.Load += new System.EventHandler(FormByteTransfer_Load);
			base.Shown += new System.EventHandler(FormByteTransfer_Shown);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
