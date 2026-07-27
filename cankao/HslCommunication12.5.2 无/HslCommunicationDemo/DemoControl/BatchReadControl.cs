using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Device;
using HslCommunicationDemo.Control;

namespace HslCommunicationDemo.DemoControl
{
	public class BatchReadControl : UserControl
	{
		private bool isMouseMove = false;

		private int index = 0;

		private string lastRegexPatter = string.Empty;

		private int lastRegexIndex = 0;

		private bool isSourceReadMode = false;

		private Func<string[], ushort[], OperateResult<byte[]>> readRandom;

		private Func<string[], OperateResult<byte[]>> readWordRandom;

		private DeviceCommunication readWriteNet;

		private byte[] buffer;

		private int lineRenderCount = -1;

		private string buttonTips1 = "";

		private string buttonTips2 = "";

		private string buttonTips3 = "";

		private string variableName = "[变量名]";

		private IContainer components = null;

		private TextBox textBox_result;

		private Label label_result;

		private Button button_read;

		private TextBox textBox_length;

		private Label label_length;

		private TextBox textBox_address;

		private Label label_address;

		private Label label1;

		private Label label2;

		private Label label3;

		private TextBox textBox_search;

		private Button button_search;

		private Button button_read_random;

		private Button button_read_word;

		private Label label_index;

		private Label label_tips;

		private Label label4;

		private Button button_write;

		private ComboBox comboBox1;

		private TextBox textBox_code;

		private Label label_code;

		private CheckBox checkBox1;

		private CheckBox checkBox_word_reverse;

		private Label label5;

		[Browsable(true)]
		[Description("获取或设置当前是否报文读取的模式")]
		public bool IsSourceReadMode
		{
			get
			{
				return isSourceReadMode;
			}
			set
			{
				isSourceReadMode = value;
				if (value)
				{
					label_length.Visible = false;
					textBox_length.Visible = false;
					textBox_address.Text = "";
					textBox_address.Width = textBox_result.Width;
					label_address.Text = ((Program.Language == 1) ? "报文:" : "Message:");
					button_read.Text = ((Program.Language == 1) ? "完整报文" : "r-Message");
					button_write.Visible = false;
					textBox_address.Multiline = true;
					textBox_address.Height = 63;
					textBox_address.ScrollBars = ScrollBars.Vertical;
					textBox_result.Location = new Point(56, 94);
					label_result.Location = new Point(3, 94);
					label_tips.Location = new Point(53, 72);
					textBox_result.Size = new Size(base.Width - 162, base.Height - 127 - 40);
				}
				else
				{
					label_length.Visible = true;
					textBox_length.Visible = true;
					textBox_address.Width = textBox_result.Width - 271;
					label_address.Text = ((Program.Language == 1) ? "地址:" : "Address:");
					button_read.Text = ((Program.Language == 1) ? "批量读取" : "Batch Read");
					label_tips.Location = new Point(53, 32);
					textBox_address.Height = 23;
					textBox_address.Multiline = false;
					textBox_address.ScrollBars = ScrollBars.None;
					label_result.Location = new Point(3, 54);
					textBox_result.Location = new Point(56, 54);
					textBox_result.Size = new Size(base.Width - 162, base.Height - 127);
				}
			}
		}

		public BatchReadControl()
		{
			InitializeComponent();
			button_read_word.Visible = false;
			button_read_random.Visible = false;
			if (isSourceReadMode)
			{
				button_write.Visible = false;
			}
			if (Program.Language == 2)
			{
				label_address.Text = "Address:";
				label_length.Text = "Length:";
				label_result.Text = "Result:";
				if (isSourceReadMode)
				{
					button_read.Text = "r-Message";
				}
				else
				{
					button_read.Text = "r-Batch";
					button_write.Text = "w-Batch";
				}
				label3.Text = "Search:";
				button_search.Text = "Search";
				button_read_random.Text = "r-random";
				button_read_word.Text = "random-word";
				label1.Text = "Byte Len: ";
				label2.Text = "TimeCount: ";
				label4.Text = get_selected();
				label_code.Text = "Code:";
				checkBox1.Text = "RegularExp";
				checkBox_word_reverse.Text = "R-Word";
			}
			button_read.Click += Button_read_Click;
			button_write.Click += Button_write_Click;
			button_search.Click += Button_search_Click;
			textBox_search.TextChanged += TextBox_search_TextChanged;
			button_read_random.Click += Button_read_random_Click;
			button_read_word.Click += Button_read_word_Click;
			button_read.MouseEnter += Button_read_MouseEnter;
			button_read.MouseLeave += Button_read_MouseLeave;
			label_tips.Text = GetTips(string.Empty);
			button_read_random.MouseEnter += Button_read_random_MouseEnter;
			button_read_random.MouseLeave += Button_read_MouseLeave;
			button_read_word.MouseEnter += Button_read_word_MouseEnter;
			button_read_word.MouseLeave += Button_read_MouseLeave;
			textBox_result.MouseDown += TextBox_result_MouseDown;
			textBox_result.MouseMove += TextBox_result_MouseMove;
			textBox_result.MouseUp += TextBox_result_MouseUp;
			comboBox1.SelectedIndex = 0;
			comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
			checkBox_word_reverse.CheckedChanged += CheckBox_word_reverse_CheckedChanged;
		}

		private void CheckBox_word_reverse_CheckedChanged(object sender, EventArgs e)
		{
			RenderReadBytes(buffer);
		}

		private string get_selected()
		{
			if (Program.Language == 2)
			{
				return "Selected: ";
			}
			return "已选择：";
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			RenderReadBytes(buffer);
		}

		private void TextBox_result_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isMouseMove)
			{
				return;
			}
			int selectionStart = textBox_result.SelectionStart;
			if (textBox_result.SelectionLength > 0)
			{
				if (comboBox1.SelectedIndex == 0)
				{
					label4.Text = get_selected() + selectionStart / 3 + " Len: " + textBox_result.SelectedText.ToHexBytes().Length;
				}
				else
				{
					label4.Text = get_selected() + selectionStart + " Len: " + textBox_result.SelectedText.Length;
				}
			}
		}

		private void TextBox_result_MouseDown(object sender, MouseEventArgs e)
		{
			isMouseMove = true;
		}

		private void TextBox_result_MouseUp(object sender, MouseEventArgs e)
		{
			isMouseMove = false;
		}

		private string GetTips(string tips)
		{
			string text = ((Program.Language == 1) ? "提示: " : "Tips: ");
			if (!string.IsNullOrEmpty(tips))
			{
				return text + tips;
			}
			return text;
		}

		private void Button_read_word_MouseEnter(object sender, EventArgs e)
		{
			label_tips.Text = GetTips(buttonTips3);
		}

		private void Button_read_random_MouseEnter(object sender, EventArgs e)
		{
			label_tips.Text = GetTips(buttonTips2);
		}

		private void Button_read_MouseEnter(object sender, EventArgs e)
		{
			label_tips.Text = GetTips(buttonTips1);
		}

		private void Button_read_MouseLeave(object sender, EventArgs e)
		{
			label_tips.Text = GetTips(string.Empty);
		}

		private void BatchReadControl_Load(object sender, EventArgs e)
		{
		}

		private void TextBox_search_TextChanged(object sender, EventArgs e)
		{
			index = 0;
		}

		private void Button_search_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_search.Text))
			{
				index = 0;
			}
			else if (checkBox1.Checked)
			{
				if (textBox_search.Text != lastRegexPatter)
				{
					lastRegexIndex = 0;
					lastRegexPatter = textBox_search.Text;
				}
				MatchCollection matchCollection = Regex.Matches(textBox_result.Text, textBox_search.Text);
				if (matchCollection.Count == 0)
				{
					index = 0;
					DemoUtils.ShowMessage((Program.Language == 2) ? "None find" : "没有查询到");
					return;
				}
				if (lastRegexIndex >= matchCollection.Count)
				{
					DemoUtils.ShowMessage((Program.Language == 2) ? "No more find, start search again" : "已经查询到末尾，没有发现更多的，再次点击重新查询");
					lastRegexIndex = 0;
					return;
				}
				Match match = matchCollection[lastRegexIndex];
				index = match.Index + 1;
				textBox_result.Select(match.Index, match.Length);
				label_index.Text = "Index:" + (match.Index - match.Index / 3);
				textBox_result.Focus();
				lastRegexIndex++;
			}
			else
			{
				int num = textBox_result.Text.IndexOf(textBox_search.Text, index);
				if (num < 0)
				{
					index = 0;
					DemoUtils.ShowMessage((Program.Language == 2) ? "None find" : "没有查询到");
					return;
				}
				index = num + 1;
				textBox_result.Select(num, textBox_search.Text.Length);
				label_index.Text = "Index:" + (num - num / 3);
				textBox_result.Focus();
			}
		}

		private void RenderList<T>(T[] data, string format = "{0}")
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (data != null)
			{
				for (int i = 0; i < data.Length; i++)
				{
					if (i != 0)
					{
						stringBuilder.Append(",");
					}
					if (lineRenderCount > 0 && i != 0 && i % lineRenderCount == 0)
					{
						stringBuilder.Append(Environment.NewLine);
					}
					stringBuilder.Append(string.Format(format, data[i]));
				}
			}
			textBox_result.Text = stringBuilder.ToString();
		}

		private void RenderReadBytes(byte[] buffer)
		{
			if (buffer == null)
			{
				return;
			}
			if (checkBox_word_reverse.Checked)
			{
				buffer = SoftBasic.BytesReverseByWord(buffer);
			}
			switch (comboBox1.SelectedIndex)
			{
			case 0:
				textBox_result.Text = buffer.ToHexString(' ', lineRenderCount);
				break;
			case 1:
				textBox_result.Text = SoftBasic.GetAsciiStringRender(buffer);
				break;
			case 2:
				RenderList(buffer, "{0,3}");
				break;
			case 3:
				RenderList(readWriteNet.ByteTransform.TransInt16(buffer, 0, buffer.Length / 2), "{0,8}");
				break;
			case 4:
				RenderList(readWriteNet.ByteTransform.TransUInt16(buffer, 0, buffer.Length / 2), "{0,8}");
				break;
			case 5:
				RenderList(readWriteNet.ByteTransform.TransInt32(buffer, 0, buffer.Length / 4), "{0,14}");
				break;
			case 6:
				RenderList(readWriteNet.ByteTransform.TransUInt32(buffer, 0, buffer.Length / 4), "{0,14}");
				break;
			case 7:
				RenderList(readWriteNet.ByteTransform.TransSingle(buffer, 0, buffer.Length / 4), "{0,14}");
				break;
			case 8:
				RenderList(readWriteNet.ByteTransform.TransDouble(buffer, 0, buffer.Length / 8), "{0,22}");
				break;
			case 9:
			{
				long[] array = readWriteNet.ByteTransform.TransInt64(buffer, 0, buffer.Length / 8);
				textBox_result.Text = array.ToArrayString();
				RenderList(array, "{0,22}");
				break;
			}
			case 10:
				RenderList(readWriteNet.ByteTransform.TransUInt64(buffer, 0, buffer.Length / 8), "{0,22}");
				break;
			case 11:
			{
				int num = ((lineRenderCount <= 0) ? 8 : lineRenderCount);
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < buffer.Length; i++)
				{
					if (i != 0 && i % num == 0)
					{
						stringBuilder.Append(Environment.NewLine);
					}
					else if (i != 0)
					{
						stringBuilder.Append("   ");
					}
					byte byt = buffer[i];
					for (int j = 0; j < 8; j++)
					{
						stringBuilder.Append(byt.GetBoolByIndex(j) ? "1" : "0");
					}
				}
				textBox_result.Text = stringBuilder.ToString();
				break;
			}
			}
		}

		private void RenderReadResult(OperateResult<byte[]> read, TimeSpan cost)
		{
			if (!read.IsSuccess)
			{
				DemoUtils.ShowMessage("read failed: " + read.ToMessageShowString());
			}
			string text = cost.TotalMilliseconds.ToString("F0");
			label2.Text = ((Program.Language == 1) ? "耗时：" : "TimeCount: ") + text + " ms";
			if (read.IsSuccess)
			{
				buffer = read.Content;
				label1.Text = ((Program.Language == 1) ? "字节数: " : "Byte Len: ") + read.Content?.Length.ToString();
				RenderReadBytes(read.Content);
			}
		}

		private void Button_read_Click(object sender, EventArgs e)
		{
			ushort result;
			if (button_read.Tag is Func<byte[], OperateResult<byte[]>> func)
			{
				DateTime now = DateTime.Now;
				RenderReadResult(func(textBox_address.Text.ToHexBytes()), DateTime.Now - now);
				textBox_code.Text = "OperateResult<byte[]> read = " + variableName + ".ReadFromCoreServer( \"" + textBox_address.Text + "\".ToHexBytes( ), true, false );  // 完整的报文";
			}
			else if (readWriteNet == null)
			{
				DemoUtils.ShowMessage("Current operate not success! readWriteNet is null");
			}
			else if (!ushort.TryParse(textBox_length.Text, out result))
			{
				DemoUtils.ShowMessage("Length input wrong! ");
			}
			else
			{
				DateTime now2 = DateTime.Now;
				RenderReadResult(readWriteNet.Read(textBox_address.Text, result), DateTime.Now - now2);
				textBox_code.Text = $"OperateResult<byte[]> read = {variableName}.Read( \"{textBox_address.Text}\", {result} );";
			}
		}

		private void Button_write_Click(object sender, EventArgs e)
		{
			if (readWriteNet == null)
			{
				DemoUtils.ShowMessage("Current operate not success! readWriteNet is null");
				return;
			}
			switch (comboBox1.SelectedIndex)
			{
			case 0:
				buffer = textBox_result.Text.ToHexBytes();
				break;
			case 1:
				buffer = SoftBasic.GetFromAsciiStringRender(textBox_result.Text);
				break;
			case 2:
				buffer = textBox_result.Text.ToStringArray<byte>();
				break;
			case 3:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<short>());
				break;
			case 4:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<ushort>());
				break;
			case 5:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<int>());
				break;
			case 6:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<uint>());
				break;
			case 7:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<float>());
				break;
			case 8:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<double>());
				break;
			case 9:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<long>());
				break;
			case 10:
				buffer = readWriteNet.ByteTransform.TransByte(textBox_result.Text.ToStringArray<ulong>());
				break;
			case 11:
				DemoUtils.ShowMessage((Program.Language == 1) ? "bool数组显示的时候，暂时不支持反写操作" : "bool array not supported write back!");
				return;
			}
			if (buffer == null)
			{
				DemoUtils.ShowMessage("data is empty!");
				return;
			}
			DateTime now = DateTime.Now;
			OperateResult operateResult = readWriteNet.Write(textBox_address.Text, buffer);
			string text = (DateTime.Now - now).TotalMilliseconds.ToString("F0");
			label2.Text = ((Program.Language == 1) ? "耗时：" : "TimeCount: ") + text + " ms";
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("write failed: " + operateResult.ToMessageShowString());
			}
			else
			{
				DemoUtils.ShowMessage("write success!");
			}
			textBox_code.Text = "OperateResult write = " + variableName + ".Write( \"" + textBox_address.Text + "\", \"" + buffer.ToHexString(' ') + "\".ToHexBytes( ) );";
		}

		private void Button_read_random_Click(object sender, EventArgs e)
		{
			if (button_read_random.Tag is Func<byte[], OperateResult<byte[]>> func)
			{
				DateTime now = DateTime.Now;
				RenderReadResult(func(textBox_address.Text.ToHexBytes()), DateTime.Now - now);
				textBox_code.Text = "OperateResult<byte[]> read = " + variableName + ".ReadFromCoreServer( \"" + textBox_address.Text + "\".ToHexBytes( ) );";
			}
			else if (readRandom != null)
			{
				string[] array = textBox_address.Text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				ushort[] array2 = (from m in textBox_length.Text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries)
					select ushort.Parse(m)).ToArray();
				DateTime now2 = DateTime.Now;
				RenderReadResult(readRandom(array, array2), DateTime.Now - now2);
				textBox_code.Text = "OperateResult<byte[]> read = " + variableName + "." + readRandom.Method.Name + "( new string[] {" + array.ToArrayString("\"{0}\"").Trim('[', ']') + "}, new ushort[] {" + array2.ToArrayString().Trim('[', ']') + "} );";
			}
		}

		private void Button_read_word_Click(object sender, EventArgs e)
		{
			if (button_read_word.Tag is Func<byte[], OperateResult<byte[]>> func)
			{
				DateTime now = DateTime.Now;
				RenderReadResult(func(textBox_address.Text.ToHexBytes()), DateTime.Now - now);
			}
			else if (readWordRandom != null)
			{
				string[] array = textBox_address.Text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				DateTime now2 = DateTime.Now;
				RenderReadResult(readWordRandom(array), DateTime.Now - now2);
				textBox_code.Text = "OperateResult<byte[]> read = " + variableName + "." + readWordRandom.Method.Name + "( new string[] {" + array.ToArrayString("\"{0}\"").Trim('[', ']') + "} );";
			}
		}

		public void SetReadWriteNet(DeviceCommunication device, string address, string tips)
		{
			readWriteNet = device;
			textBox_address.Text = address;
			buttonTips1 = tips;
		}

		public void SetReadRandom(Func<string[], ushort[], OperateResult<byte[]>> read, string tips)
		{
			button_read_random.Visible = true;
			readRandom = read;
			buttonTips2 = ((Program.Language == 1) ? "随机读取，输入多个地址及长度信息，';'间隔，例如：" : "Random reading, enter multiple address and length information, ';'Intervals, for example:") + tips;
		}

		public void SetReadWordRandom(Func<string[], OperateResult<byte[]>> read, string tips)
		{
			button_read_word.Visible = true;
			readWordRandom = read;
			buttonTips3 = tips;
			buttonTips2 = ((Program.Language == 1) ? "随机字读取，输入多个地址，';'间隔，例如：" : "Random word reading, enter multiple address, ';'Intervals, for example:") + tips;
		}

		public void SetReadSourceBytes(Func<byte[], OperateResult<byte[]>> read, string buttomName, string tips)
		{
			if (string.IsNullOrEmpty(buttomName))
			{
				button_read.Tag = read;
				buttonTips1 = ((Program.Language == 1) ? "请输入完整的二进制报文，全部的报文信息" : "Please enter the complete binary message, all the message information");
				if (!string.IsNullOrEmpty(tips))
				{
					textBox_address.Text = tips;
				}
			}
			else if (button_read_random.Tag == null)
			{
				button_read_random.Visible = true;
				button_read_random.Text = buttomName;
				button_read_random.Tag = read;
				buttonTips2 = tips;
			}
			else if (button_read_word.Tag == null)
			{
				button_read_word.Visible = true;
				button_read_word.Text = buttomName;
				button_read_word.Tag = read;
				buttonTips3 = tips;
			}
		}

		public void SetVariableName(string name)
		{
			variableName = name;
		}

		private void label5_Click(object sender, EventArgs e)
		{
			using (FormInputPassword formInputPassword = new FormInputPassword())
			{
				formInputPassword.StringMode = 1;
				if (formInputPassword.ShowDialog() == DialogResult.OK)
				{
					if (string.IsNullOrEmpty(formInputPassword.Password))
					{
						lineRenderCount = -1;
						label5.Text = ((Program.Language == 1) ? "每行默认" : "Line: Auto");
					}
					else
					{
						lineRenderCount = Convert.ToInt32(formInputPassword.Password);
						label5.Text = ((Program.Language == 1) ? "每行: " : "Line: ") + lineRenderCount;
					}
					RenderReadBytes(buffer);
				}
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
			this.textBox_result = new System.Windows.Forms.TextBox();
			this.label_result = new System.Windows.Forms.Label();
			this.button_read = new System.Windows.Forms.Button();
			this.textBox_length = new System.Windows.Forms.TextBox();
			this.label_length = new System.Windows.Forms.Label();
			this.textBox_address = new System.Windows.Forms.TextBox();
			this.label_address = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox_search = new System.Windows.Forms.TextBox();
			this.button_search = new System.Windows.Forms.Button();
			this.button_read_random = new System.Windows.Forms.Button();
			this.button_read_word = new System.Windows.Forms.Button();
			this.label_index = new System.Windows.Forms.Label();
			this.label_tips = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.button_write = new System.Windows.Forms.Button();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.textBox_code = new System.Windows.Forms.TextBox();
			this.label_code = new System.Windows.Forms.Label();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.checkBox_word_reverse = new System.Windows.Forms.CheckBox();
			this.label5 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.textBox_result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_result.Font = new System.Drawing.Font("Consolas", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
			this.textBox_result.Location = new System.Drawing.Point(56, 54);
			this.textBox_result.Multiline = true;
			this.textBox_result.Name = "textBox_result";
			this.textBox_result.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_result.Size = new System.Drawing.Size(683, 191);
			this.textBox_result.TabIndex = 33;
			this.label_result.AutoSize = true;
			this.label_result.Location = new System.Drawing.Point(3, 57);
			this.label_result.Name = "label_result";
			this.label_result.Size = new System.Drawing.Size(44, 17);
			this.label_result.TabIndex = 32;
			this.label_result.Text = "结果：";
			this.button_read.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read.Location = new System.Drawing.Point(745, 1);
			this.button_read.Name = "button_read";
			this.button_read.Size = new System.Drawing.Size(97, 28);
			this.button_read.TabIndex = 31;
			this.button_read.Text = "批量读取";
			this.button_read.UseVisualStyleBackColor = true;
			this.textBox_length.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_length.Location = new System.Drawing.Point(524, 4);
			this.textBox_length.Name = "textBox_length";
			this.textBox_length.Size = new System.Drawing.Size(215, 23);
			this.textBox_length.TabIndex = 30;
			this.textBox_length.Text = "10";
			this.label_length.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label_length.AutoSize = true;
			this.label_length.Location = new System.Drawing.Point(474, 7);
			this.label_length.Name = "label_length";
			this.label_length.Size = new System.Drawing.Size(44, 17);
			this.label_length.TabIndex = 29;
			this.label_length.Text = "长度：";
			this.textBox_address.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_address.Location = new System.Drawing.Point(56, 4);
			this.textBox_address.Name = "textBox_address";
			this.textBox_address.Size = new System.Drawing.Size(412, 23);
			this.textBox_address.TabIndex = 28;
			this.label_address.AutoSize = true;
			this.label_address.Location = new System.Drawing.Point(1, 7);
			this.label_address.Name = "label_address";
			this.label_address.Size = new System.Drawing.Size(44, 17);
			this.label_address.TabIndex = 27;
			this.label_address.Text = "地址：";
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(243, 295);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 34;
			this.label1.Text = "字节数：";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(339, 295);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 35;
			this.label2.Text = "耗时：";
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(576, 296);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(80, 17);
			this.label3.TabIndex = 36;
			this.label3.Text = "查找字符串：";
			this.textBox_search.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_search.Location = new System.Drawing.Point(660, 292);
			this.textBox_search.Name = "textBox_search";
			this.textBox_search.Size = new System.Drawing.Size(79, 23);
			this.textBox_search.TabIndex = 37;
			this.textBox_search.Text = "10";
			this.button_search.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.button_search.Location = new System.Drawing.Point(745, 289);
			this.button_search.Name = "button_search";
			this.button_search.Size = new System.Drawing.Size(97, 28);
			this.button_search.TabIndex = 38;
			this.button_search.Text = "查找";
			this.button_search.UseVisualStyleBackColor = true;
			this.button_read_random.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_random.Location = new System.Drawing.Point(745, 30);
			this.button_read_random.Name = "button_read_random";
			this.button_read_random.Size = new System.Drawing.Size(97, 28);
			this.button_read_random.TabIndex = 40;
			this.button_read_random.Text = "随机读取";
			this.button_read_random.UseVisualStyleBackColor = true;
			this.button_read_word.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_word.Location = new System.Drawing.Point(745, 59);
			this.button_read_word.Name = "button_read_word";
			this.button_read_word.Size = new System.Drawing.Size(97, 28);
			this.button_read_word.TabIndex = 41;
			this.button_read_word.Text = "随机字读取";
			this.button_read_word.UseVisualStyleBackColor = true;
			this.label_index.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label_index.AutoSize = true;
			this.label_index.ForeColor = System.Drawing.Color.Gray;
			this.label_index.Location = new System.Drawing.Point(745, 250);
			this.label_index.Name = "label_index";
			this.label_index.Size = new System.Drawing.Size(59, 17);
			this.label_index.TabIndex = 42;
			this.label_index.Text = "Index: -1";
			this.label_tips.AutoSize = true;
			this.label_tips.ForeColor = System.Drawing.Color.Gray;
			this.label_tips.Location = new System.Drawing.Point(53, 32);
			this.label_tips.Name = "label_tips";
			this.label_tips.Size = new System.Drawing.Size(35, 17);
			this.label_tips.TabIndex = 43;
			this.label_tips.Text = "Tips:";
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(440, 295);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(45, 17);
			this.label4.TabIndex = 44;
			this.label4.Text = "Select:";
			this.button_write.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write.Location = new System.Drawing.Point(745, 88);
			this.button_write.Name = "button_write";
			this.button_write.Size = new System.Drawing.Size(97, 28);
			this.button_write.TabIndex = 45;
			this.button_write.Text = "批量回写";
			this.button_write.UseVisualStyleBackColor = true;
			this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Items.AddRange(new object[12]
			{
				"HEX", "ASCII", "byte", "short", "ushort", "int", "uint", "float", "double", "long",
				"ulong", "boolArray"
			});
			this.comboBox1.Location = new System.Drawing.Point(56, 291);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(96, 25);
			this.comboBox1.TabIndex = 46;
			this.textBox_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_code.Location = new System.Drawing.Point(56, 248);
			this.textBox_code.Multiline = true;
			this.textBox_code.Name = "textBox_code";
			this.textBox_code.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_code.Size = new System.Drawing.Size(683, 39);
			this.textBox_code.TabIndex = 47;
			this.label_code.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label_code.AutoSize = true;
			this.label_code.Location = new System.Drawing.Point(1, 248);
			this.label_code.Name = "label_code";
			this.label_code.Size = new System.Drawing.Size(44, 17);
			this.label_code.TabIndex = 48;
			this.label_code.Text = "代码：";
			this.checkBox1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(747, 268);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(87, 21);
			this.checkBox1.TabIndex = 49;
			this.checkBox1.Text = "正则表达式";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox_word_reverse.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_word_reverse.AutoSize = true;
			this.checkBox_word_reverse.ForeColor = System.Drawing.Color.DarkRed;
			this.checkBox_word_reverse.Location = new System.Drawing.Point(156, 293);
			this.checkBox_word_reverse.Name = "checkBox_word_reverse";
			this.checkBox_word_reverse.Size = new System.Drawing.Size(63, 21);
			this.checkBox_word_reverse.TabIndex = 50;
			this.checkBox_word_reverse.Text = "字反转";
			this.checkBox_word_reverse.UseVisualStyleBackColor = true;
			this.label5.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.label5.Cursor = System.Windows.Forms.Cursors.Hand;
			this.label5.ForeColor = System.Drawing.Color.Gray;
			this.label5.Location = new System.Drawing.Point(745, 230);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(97, 17);
			this.label5.TabIndex = 51;
			this.label5.Text = "每行默认数量";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label5.Click += new System.EventHandler(label5_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label5);
			base.Controls.Add(this.checkBox1);
			base.Controls.Add(this.label_code);
			base.Controls.Add(this.textBox_code);
			base.Controls.Add(this.comboBox1);
			base.Controls.Add(this.button_write);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.label_index);
			base.Controls.Add(this.button_read_word);
			base.Controls.Add(this.button_read_random);
			base.Controls.Add(this.button_search);
			base.Controls.Add(this.textBox_search);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.textBox_result);
			base.Controls.Add(this.label_result);
			base.Controls.Add(this.button_read);
			base.Controls.Add(this.textBox_length);
			base.Controls.Add(this.label_length);
			base.Controls.Add(this.textBox_address);
			base.Controls.Add(this.label_address);
			base.Controls.Add(this.label_tips);
			base.Controls.Add(this.checkBox_word_reverse);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "BatchReadControl";
			base.Size = new System.Drawing.Size(845, 318);
			base.Load += new System.EventHandler(BatchReadControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
