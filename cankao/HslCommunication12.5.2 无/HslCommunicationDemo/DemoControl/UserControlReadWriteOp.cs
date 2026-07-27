using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunicationDemo.DemoControl
{
	public class UserControlReadWriteOp : UserControl
	{
		private string[] encodings = DemoUtils.GetEncodings();

		private Timer timer_read;

		private Button button_read_timer;

		private Timer timer_write;

		private Button button_write_timer;

		private long read_tick_count;

		private Image deviceImage = null;

		private string address = string.Empty;

		private IReadWriteNet readWriteNet;

		private bool isAsync = false;

		private MethodInfo readByteMethod = null;

		private MethodInfo writeByteMethod = null;

		private string readValueLast = string.Empty;

		private string readTimeLast = string.Empty;

		private long readValueRepeatTimes = 1L;

		private ValueLimit valueLimit = default(ValueLimit);

		private int timerValue = int.MinValue;

		private bool read_search_entered = false;

		private int find_index = -1;

		private string find_string = string.Empty;

		private IContainer components = null;

		private GroupBox groupBox2;

		private Label label19;

		private Button button_write_string;

		private Button button_write_double;

		private Button button_write_float;

		private Button button_write_ulong;

		private Button button_write_long;

		private Button button_write_uint;

		private Button button_write_int;

		private Button button_write_ushort;

		private Button button_write_short;

		private Button button_write_byte;

		private Button button_write_bool;

		private TextBox textBox_write_text;

		private Label label9;

		private TextBox textBox_write_address;

		private Label label10;

		private GroupBox groupBox1;

		private TextBox textBox5;

		private Button button_read_string;

		private Button button_read_double;

		private Button button_read_float;

		private Button button_read_ulong;

		private Button button_read_long;

		private Button button_read_uint;

		private Button button_read_int;

		private Button button_read_ushort;

		private Button button_read_short;

		private Button button_read_byte;

		private Button button_read_bool;

		private TextBox textBox4;

		private Label label7;

		private TextBox textBox3;

		private Label label6;

		private TextBox textBox1;

		private Label label8;

		private Button button1;

		private Button button_write_hex;

		private Label label5;

		private Label label11;

		private Label label3;

		private Label label4;

		private Label label2;

		private Label label1;

		private ComboBox comboBox_write_Encoding;

		private Label label13;

		private ComboBox comboBox_read_encoding;

		private Label label12;

		private GroupBox groupBox4;

		private Label label15;

		private TextBox textBox_timer_write_interval;

		private CheckBox checkBox_write_timer;

		private GroupBox groupBox3;

		private Label label14;

		private TextBox textBox_read_timer_interval;

		private CheckBox checkBox_read_timer;

		private Label label16;

		private Label label17;

		private Label label18;

		private Label label20;

		private Label label21;

		private Label label22;

		private CheckBox checkBox_mask_duplicates;

		private TextBox textBox_read_search;

		private Button button_find_string;

		private RadioButton radioButton_sync;

		private RadioButton radioButton_async;

		private Panel panel1;

		private Panel panel2;

		private RadioButton radioButton_read_int;

		private RadioButton radioButton_read_hex;

		private RadioButton radioButton_read_bit;

		public event EventHandler<string> MethodCodeClick;

		public UserControlReadWriteOp()
		{
			InitializeComponent();
			base.Disposed += UserControlReadWriteOp_Disposed;
			radioButton_async.CheckedChanged += RadioButton_async_CheckedChanged;
			radioButton_sync.CheckedChanged += RadioButton_async_CheckedChanged;
		}

		private void RadioButton_async_CheckedChanged(object sender, EventArgs e)
		{
			if (radioButton_sync.Checked)
			{
				isAsync = false;
			}
			else
			{
				isAsync = true;
			}
		}

		private void UserControlReadWriteOp_Disposed(object sender, EventArgs e)
		{
			if (checkBox_read_timer.Checked)
			{
				checkBox_read_timer.Checked = false;
			}
			if (checkBox_write_timer.Checked)
			{
				checkBox_write_timer.Checked = false;
			}
		}

		private void UserControlReadWriteOp_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			comboBox_read_encoding.DataSource = encodings;
			comboBox_write_Encoding.DataSource = encodings;
			checkBox_read_timer.CheckedChanged += CheckBox_read_timer_CheckedChanged;
			checkBox_write_timer.CheckedChanged += CheckBox_write_timer_CheckedChanged;
			if (base.ParentForm is HslFormContent hslFormContent)
			{
				deviceImage = hslFormContent.DeviceImage;
			}
		}

		private void CheckBox_write_timer_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox_write_timer.Checked)
			{
				if (!int.TryParse(textBox_timer_write_interval.Text, out var result))
				{
					DemoUtils.ShowMessage("Read time interval input wrong!");
					return;
				}
				if (result <= 0)
				{
					DemoUtils.ShowMessage("Read time interval can not below 0!");
					return;
				}
				timer_write?.Dispose();
				timer_write = new Timer();
				timer_write.Interval = result;
				timer_write.Tick += Timer_write_Tick;
				timer_write.Start();
				button_write_timer = null;
			}
			else
			{
				timer_write?.Dispose();
				timer_write = null;
				button_write_timer = null;
			}
		}

		public void Close()
		{
			if (checkBox_read_timer.Checked)
			{
				checkBox_read_timer.Checked = false;
			}
			if (checkBox_write_timer.Checked)
			{
				checkBox_write_timer.Checked = false;
			}
		}

		private void Timer_write_Tick(object sender, EventArgs e)
		{
			button_write_timer?.PerformClick();
		}

		private void CheckBox_read_timer_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox_read_timer.Checked)
			{
				if (!int.TryParse(textBox_read_timer_interval.Text, out var result))
				{
					DemoUtils.ShowMessage("Read time interval input wrong!");
					return;
				}
				if (result <= 0)
				{
					DemoUtils.ShowMessage("Read time interval can not below 0!");
					return;
				}
				timer_read?.Dispose();
				timer_read = new Timer();
				timer_read.Interval = result;
				timer_read.Tick += Timer_read_Tick;
				timer_read.Start();
				button_read_timer = null;
				read_tick_count = 0L;
			}
			else
			{
				timer_read?.Dispose();
				timer_read = null;
				button_read_timer = null;
				timerValue = int.MinValue;
			}
		}

		private void Timer_read_Tick(object sender, EventArgs e)
		{
			button_read_timer?.PerformClick();
			if (button_read_timer != null)
			{
				read_tick_count++;
				label17.Text = ((Program.Language == 1) ? "次数: " : "Freq: ") + read_tick_count;
			}
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "Curve Monitor";
				label6.Text = "address:";
				label7.Text = "result:";
				label8.Text = "length:";
				label17.Text = "Freq: 0";
				button_read_bool.Text = "Read Bit";
				button_read_byte.Text = "r-byte";
				button_read_short.Text = "r-short";
				button_read_ushort.Text = "r-ushort";
				button_read_int.Text = "r-int";
				button_read_uint.Text = "r-uint";
				button_read_long.Text = "r-long";
				button_read_ulong.Text = "r-ulong";
				button_read_float.Text = "r-float";
				button_read_double.Text = "r-double";
				button_read_string.Text = "r-string";
				label10.Text = "Address:";
				label9.Text = "Value:";
				label19.Text = "Note:The string can convert value\r\nif bool: true,false,0,1\r\nif array:[1,2,3], [1:100], [1*100]";
				button_write_bool.Text = "Write Bit";
				button_write_short.Text = "w-short";
				button_write_ushort.Text = "w-ushort";
				button_write_int.Text = "w-int";
				button_write_uint.Text = "w-uint";
				button_write_long.Text = "w-long";
				button_write_ulong.Text = "w-ulong";
				button_write_float.Text = "w-float";
				button_write_double.Text = "w-double";
				button_write_string.Text = "w-string";
				button_write_hex.Text = "w-hex";
				groupBox1.Text = "Single Data Read test";
				groupBox2.Text = "Single Data Write test";
				button_write_byte.Text = "w-byte";
				button1.Text = "Curve";
				label1.Text = "Time-Cost:";
				label12.Text = "Encoding:";
				label13.Text = "Encoding:";
				checkBox_read_timer.Text = "Timer Read";
				checkBox_write_timer.Text = "Timer Write";
				label16.Text = "if write 1-100, value input {1:100}";
				checkBox_mask_duplicates.Text = "Mask duplicates?";
				textBox_read_search.Text = "Search";
				button_find_string.Text = "find";
				radioButton_sync.Text = "Sync";
				radioButton_async.Text = "Async";
			}
		}

		public void EnableRKC()
		{
			button_read_bool.Enabled = false;
			button_read_byte.Enabled = false;
			button_read_short.Enabled = false;
			button_read_ushort.Enabled = false;
			button_read_int.Enabled = false;
			button_read_uint.Enabled = false;
			button_read_long.Enabled = false;
			button_read_ulong.Enabled = false;
			button_read_float.Enabled = false;
			button_read_string.Enabled = false;
			button_write_bool.Enabled = false;
			button_write_byte.Enabled = false;
			button_write_short.Enabled = false;
			button_write_ushort.Enabled = false;
			button_write_int.Enabled = false;
			button_write_uint.Enabled = false;
			button_write_long.Enabled = false;
			button_write_ulong.Enabled = false;
			button_write_float.Enabled = false;
			button_write_hex.Enabled = false;
			button_write_string.Enabled = false;
		}

		public void SetReadWriteNet(IReadWriteNet readWrite, string address, bool isAsync = false, int strLength = 10)
		{
			this.isAsync = isAsync;
			if (isAsync)
			{
				radioButton_async.Checked = true;
			}
			else
			{
				radioButton_sync.Checked = true;
			}
			this.address = address;
			if (string.IsNullOrEmpty(textBox3.Text))
			{
				textBox3.Text = address;
			}
			if (string.IsNullOrEmpty(textBox_write_address.Text))
			{
				textBox_write_address.Text = address;
			}
			textBox1.Text = strLength.ToString();
			readWriteNet = readWrite;
			Type type = readWrite.GetType();
			readByteMethod = type.GetMethod("ReadByte", new Type[1] { typeof(string) });
			if (readByteMethod == null)
			{
				button_read_byte.Enabled = false;
			}
			try
			{
				writeByteMethod = type.GetMethod("Write", new Type[2]
				{
					typeof(string),
					typeof(byte)
				});
				if (writeByteMethod == null)
				{
					button_write_byte.Enabled = false;
				}
			}
			catch
			{
				button_write_byte.Enabled = false;
			}
		}

		public string GetWriteAddress()
		{
			return textBox_write_address.Text;
		}

		private void SetTimeSpend(int millseconds)
		{
			valueLimit.SetNewValue(millseconds);
			label2.Text = $"{valueLimit.Current} ms";
			label3.Text = $"{valueLimit.MaxValue} ms";
			label5.Text = $"{valueLimit.MinValue} ms";
			label18.Text = $"{valueLimit.Average:F0} ms";
			label21.Text = $"{valueLimit.Count}";
		}

		private void RenderReadResult<T>(DateTime start, OperateResult<T> read, int renderResult = -1)
		{
			SetTimeSpend(Convert.ToInt32((DateTime.Now - start).TotalMilliseconds));
			if (!read.IsSuccess && checkBox_read_timer.Checked)
			{
				checkBox_read_timer.Checked = false;
			}
			ReadResultRender(read, textBox3.Text, textBox4, renderResult);
		}

		private void AppendReadResult(TextBox textBox, string text)
		{
			if (!checkBox_mask_duplicates.Checked)
			{
				textBox.AppendText(DemoUtils.GetRenderTimeText() + text + Environment.NewLine);
				return;
			}
			if (readValueLast == text)
			{
				readValueRepeatTimes++;
				if (textBox.Lines.Length != 0)
				{
					string[] lines = textBox.Lines;
					if (string.IsNullOrEmpty(lines[lines.Length - 1]))
					{
						lines[lines.Length - 2] = readTimeLast + $"[{readValueRepeatTimes}] {text}";
						textBox.Text = string.Join(Environment.NewLine, lines);
						textBox.SelectionStart = textBox.Text.Length;
						textBox.ScrollToCaret();
					}
					else
					{
						lines[lines.Length - 1] = readTimeLast + $"[{readValueRepeatTimes}] {text}";
						textBox.Text = string.Join(Environment.NewLine, lines);
						textBox.AppendText(Environment.NewLine);
					}
				}
			}
			else
			{
				readValueRepeatTimes = 1L;
				readTimeLast = DemoUtils.GetRenderTimeText();
				textBox.AppendText(readTimeLast + text + Environment.NewLine);
			}
			readValueLast = text;
		}

		public void ReadResultRender<T>(OperateResult<T> result, string address, TextBox textBox, int renderResult)
		{
			if (result.IsSuccess)
			{
				if (result.Content is Array)
				{
					AppendReadResult(textBox, "[" + address + "] " + SoftBasic.ArrayFormat(result.Content));
					return;
				}
				switch (renderResult)
				{
				case 1:
					AppendReadResult(textBox, $"[{address}] 0x{result.Content:X}");
					break;
				case 2:
				{
					Type type = result.Content.GetType();
					byte[] array = null;
					if (type == typeof(byte))
					{
						array = new byte[1] { (byte)(object)result.Content };
					}
					else if (type == typeof(short))
					{
						array = BitConverter.GetBytes((short)(object)result.Content);
					}
					else if (type == typeof(ushort))
					{
						array = BitConverter.GetBytes((ushort)(object)result.Content);
					}
					else if (type == typeof(int))
					{
						array = BitConverter.GetBytes((int)(object)result.Content);
					}
					else if (type == typeof(uint))
					{
						array = BitConverter.GetBytes((uint)(object)result.Content);
					}
					else if (type == typeof(long))
					{
						array = BitConverter.GetBytes((long)(object)result.Content);
					}
					else if (type == typeof(ulong))
					{
						array = BitConverter.GetBytes((ulong)(object)result.Content);
					}
					else if (type == typeof(float))
					{
						array = BitConverter.GetBytes((float)(object)result.Content);
					}
					else if (type == typeof(double))
					{
						array = BitConverter.GetBytes((double)(object)result.Content);
					}
					if (array == null)
					{
						AppendReadResult(textBox, $"[{address}] {result.Content}");
						break;
					}
					StringBuilder stringBuilder = new StringBuilder();
					bool[] array2 = SoftBasic.ByteToBoolArray(array);
					for (int i = 0; i < array2.Length; i++)
					{
						if (i != 0 && i % 8 == 0)
						{
							stringBuilder.Append(" ");
						}
						stringBuilder.Append(array2[i] ? '1' : '0');
					}
					AppendReadResult(textBox, $"[{address}] {stringBuilder}");
					break;
				}
				default:
					AppendReadResult(textBox, $"[{address}] {result.Content}");
					break;
				}
			}
			else
			{
				DemoUtils.ShowMessage(DemoUtils.GetRenderTimeText() + "[" + address + "] Read Failed " + Environment.NewLine + "Reason：" + result.ToMessageShowString());
			}
		}

		private int getRenderResult()
		{
			if (radioButton_read_hex.Checked)
			{
				return 1;
			}
			if (radioButton_read_bit.Checked)
			{
				return 2;
			}
			return -1;
		}

		private async void button_read_bool_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_bool.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadBoolAsync(textBox3.Text));
					GetReadCode(textBox3.Text, "OperateResult<bool> read = @deviceName.ReadBool( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadBoolAsync(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<bool[]> read = @deviceName.ReadBool( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_bool.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadBool(textBox3.Text));
				GetReadCode(textBox3.Text, "OperateResult<bool> read = @deviceName.ReadBool( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadBool(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<bool[]> read = @deviceName.ReadBool( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private void button_read_byte_Click(object sender, EventArgs e)
		{
			if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, (OperateResult<byte>)readByteMethod.Invoke(readWriteNet, new object[1] { textBox3.Text }), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<byte> read = @deviceName.ReadByte( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.Read(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<byte[]> read = @deviceName.Read( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_short_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_short.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt16Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<short> read = @deviceName.ReadInt16( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt16Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<short[]> read = @deviceName.ReadInt16( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_short.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt16(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<short> read = @deviceName.ReadInt16( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt16(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<short[]> read = @deviceName.ReadInt16( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_ushort_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_ushort.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt16Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<ushort> read = @deviceName.ReadUInt16( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt16Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<ushort[]> read = @deviceName.ReadUInt16( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_ushort.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt16(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<ushort> read = @deviceName.ReadUInt16( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt16(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<ushort[]> read = @deviceName.ReadUInt16( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_int_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_int.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt32Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<int> read = @deviceName.ReadInt32( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt32Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<int[]> read = @deviceName.ReadInt32( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_int.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt32(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<int> read = @deviceName.ReadInt32( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt32(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<int[]> read = @deviceName.ReadInt32( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_uint_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_uint.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt32Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<uint> read = @deviceName.ReadUInt32( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt32Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<uint[]> read = @deviceName.ReadUInt32( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_uint.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt32(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<uint> read = @deviceName.ReadUInt32( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt32(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<uint[]> read = @deviceName.ReadUInt32( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_long_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_long.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt64Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<long> read = @deviceName.ReadInt64( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadInt64Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<long[]> read = @deviceName.ReadInt64( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_long.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt64(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<long> read = @deviceName.ReadInt64( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadInt64(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<long[]> read = @deviceName.ReadInt64( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_ulong_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_ulong.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt64Async(textBox3.Text), getRenderResult());
					GetReadCode(textBox3.Text, "OperateResult<ulong> read = @deviceName.ReadUInt64( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadUInt64Async(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<ulong[]> read = @deviceName.ReadUInt64( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_ulong.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt64(textBox3.Text), getRenderResult());
				GetReadCode(textBox3.Text, "OperateResult<ulong> read = @deviceName.ReadUInt64( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadUInt64(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<ulong[]> read = @deviceName.ReadUInt64( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_float_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_float.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadFloatAsync(textBox3.Text));
					GetReadCode(textBox3.Text, "OperateResult<float> read = @deviceName.ReadFloat( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadFloatAsync(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<float[]> read = @deviceName.ReadFloat( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_float.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadFloat(textBox3.Text));
				GetReadCode(textBox3.Text, "OperateResult<float> read = @deviceName.ReadFloat( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadFloat(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<float[]> read = @deviceName.ReadFloat( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_double_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_double.Enabled = false;
				if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadDoubleAsync(textBox3.Text));
					GetReadCode(textBox3.Text, "OperateResult<double> read = @deviceName.ReadDouble( \"" + textBox3.Text + "\" );", false);
				}
				else
				{
					RenderReadResult(DateTime.Now, await readWriteNet.ReadDoubleAsync(textBox3.Text, ushort.Parse(textBox5.Text)));
					GetReadCode(textBox3.Text, "OperateResult<double[]> read = @deviceName.ReadDouble( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
				}
				button_read_double.Enabled = true;
			}
			else if (textBox5.Text == "1" || string.IsNullOrEmpty(textBox5.Text))
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadDouble(textBox3.Text));
				GetReadCode(textBox3.Text, "OperateResult<double> read = @deviceName.ReadDouble( \"" + textBox3.Text + "\" );", false);
			}
			else
			{
				RenderReadResult(DateTime.Now, readWriteNet.ReadDouble(textBox3.Text, ushort.Parse(textBox5.Text)));
				GetReadCode(textBox3.Text, "OperateResult<double[]> read = @deviceName.ReadDouble( \"" + textBox3.Text + "\", " + textBox5.Text + " );", true);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private async void button_read_string_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_read_string.Enabled = false;
				DateTime start = DateTime.Now;
				OperateResult<string> read = await readWriteNet.ReadStringAsync(textBox3.Text, ushort.Parse(textBox1.Text), DemoUtils.GetEncodingFromIndex(comboBox_read_encoding.SelectedIndex));
				if (read.IsSuccess && read.Content != null && read.Content.Contains("\0"))
				{
					read.Content = read.Content.Replace("\0", "\\0");
				}
				RenderReadResult(start, read);
				button_read_string.Enabled = true;
				GetReadCode(textBox3.Text, "OperateResult<string> read = @deviceName.ReadString( \"" + textBox3.Text + "\", " + textBox1.Text + ", " + DemoUtils.GetEncodingTextFromIndex(comboBox_read_encoding.SelectedIndex) + " );", false);
			}
			else
			{
				DateTime start2 = DateTime.Now;
				OperateResult<string> read2 = readWriteNet.ReadString(textBox3.Text, ushort.Parse(textBox1.Text), DemoUtils.GetEncodingFromIndex(comboBox_read_encoding.SelectedIndex));
				if (read2.IsSuccess && read2.Content != null && read2.Content.Contains("\0"))
				{
					read2.Content = read2.Content.Replace("\0", "\\0");
				}
				RenderReadResult(start2, read2);
				GetReadCode(textBox3.Text, "OperateResult<string> read = @deviceName.ReadString( \"" + textBox3.Text + "\", " + textBox1.Text + ", " + DemoUtils.GetEncodingTextFromIndex(comboBox_read_encoding.SelectedIndex) + " );", false);
			}
			if (checkBox_read_timer.Checked)
			{
				button_read_timer = sender as Button;
			}
		}

		private string GetWriteValueText()
		{
			if (checkBox_write_timer.Checked && Regex.IsMatch(textBox_write_text.Text, "\\{[\\d-]+:[\\d-]+\\}"))
			{
				MatchCollection matchCollection = Regex.Matches(textBox_write_text.Text, "[\\d-]+");
				if (matchCollection.Count == 2)
				{
					int num = Convert.ToInt32(matchCollection[0].Value);
					int num2 = Convert.ToInt32(matchCollection[1].Value);
					if (num < num2)
					{
						if (timerValue < num || timerValue >= num2)
						{
							timerValue = num;
						}
						else
						{
							timerValue++;
						}
					}
					else if (timerValue > num || timerValue <= num2)
					{
						timerValue = num;
					}
					else
					{
						timerValue--;
					}
					return timerValue.ToString();
				}
				return textBox_write_text.Text;
			}
			return textBox_write_text.Text;
		}

		private void RenderWriteResult(Func<OperateResult> writeFunc, Button button, string input)
		{
			if (isAsync)
			{
				button.Enabled = false;
			}
			DateTime now = DateTime.Now;
			OperateResult operateResult = writeFunc();
			SetTimeSpend(Convert.ToInt32((DateTime.Now - now).TotalMilliseconds));
			if (isAsync)
			{
				button.Enabled = true;
			}
			if (!operateResult.IsSuccess && checkBox_write_timer.Checked)
			{
				checkBox_write_timer.Checked = false;
			}
			WriteResultRender(operateResult, textBox_write_address.Text, input);
		}

		private async Task RenderWriteResult(Func<Task<OperateResult>> writeFunc, Button button, string input)
		{
			if (isAsync)
			{
				button.Enabled = false;
			}
			DateTime start = DateTime.Now;
			OperateResult write = await writeFunc();
			SetTimeSpend(Convert.ToInt32((DateTime.Now - start).TotalMilliseconds));
			if (isAsync)
			{
				button.Enabled = true;
			}
			if (!write.IsSuccess && checkBox_write_timer.Checked)
			{
				checkBox_write_timer.Checked = false;
			}
			WriteResultRender(write, textBox_write_address.Text, input);
		}

		private bool TransBoolValue(string value)
		{
			value = value.Trim();
			if (value == "1")
			{
				return true;
			}
			if (value == "0")
			{
				return false;
			}
			if (value.Equals("On", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (value.Equals("Off", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return bool.Parse(value);
		}

		private async void button_write_bool_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					bool[] value = input.ToStringArray(TransBoolValue);
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_bool, input);
						GetWriteCode(textBox_write_address.Text, "OperateResult write = @deviceName.Write( \"" + textBox_write_address.Text + "\", \"" + input + "\".ToStringArray<bool>( ) );");
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_bool, input);
						GetWriteCode(textBox_write_address.Text, "OperateResult write = @deviceName.Write( \"" + textBox_write_address.Text + "\", \"" + input + "\".ToStringArray<bool>( ) );");
					}
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("Bool Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else
			{
				if (input == "1")
				{
					input = "True";
				}
				if (input == "0")
				{
					input = "False";
				}
				if (bool.TryParse(input, out var value2))
				{
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_bool, input);
						GetWriteCode(textBox_write_address.Text, "OperateResult write = @deviceName.Write( \"" + textBox_write_address.Text + "\", " + input.ToLower() + " );");
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_bool, input);
						GetWriteCode(textBox_write_address.Text, "OperateResult write = @deviceName.Write( \"" + textBox_write_address.Text + "\", " + input.ToLower() + " );");
					}
				}
				else
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("Bool Data is not corrent: " + input);
				}
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool TryGetByteValue(string input, out byte value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (byte.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (input.StartsWith("-"))
			{
				if (sbyte.TryParse(input, out var result))
				{
					value = (byte)result;
					return true;
				}
			}
			else if (byte.TryParse(input, out value))
			{
				return true;
			}
			value = 0;
			return false;
		}

		private async void button_write_byte_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			byte value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					byte[] value = input.ToStringArray<byte>();
					if (isAsync)
					{
						button_write_short.Enabled = false;
						DateTime start = DateTime.Now;
						OperateResult write = await readWriteNet.WriteAsync(textBox_write_address.Text, value);
						SetTimeSpend(Convert.ToInt32((DateTime.Now - start).TotalMilliseconds));
						button_write_short.Enabled = true;
						if (!write.IsSuccess && checkBox_write_timer.Checked)
						{
							checkBox_write_timer.Checked = false;
						}
						WriteResultRender(write, textBox_write_address.Text, string.Empty);
					}
					else
					{
						DateTime start2 = DateTime.Now;
						OperateResult write2 = readWriteNet.Write(textBox_write_address.Text, value);
						SetTimeSpend(Convert.ToInt32((DateTime.Now - start2).TotalMilliseconds));
						if (!write2.IsSuccess && checkBox_write_timer.Checked)
						{
							checkBox_write_timer.Checked = false;
						}
						WriteResultRender(write2, textBox_write_address.Text, string.Empty);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "byte");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("byte Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (TryGetByteValue(input, out value2))
			{
				DateTime start3 = DateTime.Now;
				OperateResult write3 = (OperateResult)writeByteMethod.Invoke(readWriteNet, new object[2] { textBox_write_address.Text, value2 });
				SetTimeSpend(Convert.ToInt32((DateTime.Now - start3).TotalMilliseconds));
				if (!write3.IsSuccess && checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				WriteResultRender(write3, textBox_write_address.Text, input);
				GetWriteCode(textBox_write_address.Text, input, "byte", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("Byte Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private T[] GetArrayFromString<T>(string value, Func<long, int, T> funcAdd)
		{
			if (Regex.IsMatch(value, "\\[[-]?[0-9]+:[-]?[0-9]+\\]"))
			{
				MatchCollection matchCollection = Regex.Matches(value, "[-]?[0-9]+");
				long num = long.Parse(matchCollection[0].Value);
				long num2 = long.Parse(matchCollection[1].Value);
				T[] array = new T[Math.Abs(num2 - num) + 1];
				bool flag = num < num2;
				for (int i = 0; i < array.Length; i++)
				{
					if (flag)
					{
						array[i] = funcAdd(num, i);
					}
					else
					{
						array[i] = funcAdd(num, -i);
					}
				}
				return array;
			}
			if (Regex.IsMatch(value, "\\[[-]?[0-9]+\\*[0-9]+\\]"))
			{
				MatchCollection matchCollection2 = Regex.Matches(value, "[-]?[0-9]+");
				long arg = long.Parse(matchCollection2[0].Value);
				T[] array2 = new T[long.Parse(matchCollection2[1].Value)];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = funcAdd(arg, 0);
				}
				return array2;
			}
			return value.ToStringArray<T>();
		}

		private bool try_get_short_value(string input, out short value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (short.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (short.TryParse(input, out value))
			{
				return true;
			}
			value = 0;
			return false;
		}

		private async void button_write_short_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			short value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					short[] value = GetArrayFromString(input, (long m, int n) => (short)(m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_short, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_short, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "short");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("short Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_short_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_short, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_short, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "short", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("short Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool try_get_ushort_value(string input, out ushort value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (ushort.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (ushort.TryParse(input, out value))
			{
				return true;
			}
			value = 0;
			return false;
		}

		private async void button_write_ushort_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			ushort value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					ushort[] value = GetArrayFromString(input, (long m, int n) => (ushort)(m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_ushort, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_ushort, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "ushort");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("ushort Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_ushort_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_ushort, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_ushort, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "ushort", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("ushort Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool try_get_int_value(string input, out int value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (int.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (int.TryParse(input, out value))
			{
				return true;
			}
			value = 0;
			return false;
		}

		private async void button_write_int_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			int value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					int[] value = GetArrayFromString(input, (long m, int n) => (int)(m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_int, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_int, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "int");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("int Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_int_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_int, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_int, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "int", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("int Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool try_get_uint_value(string input, out uint value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (uint.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (uint.TryParse(input, out value))
			{
				return true;
			}
			value = 0u;
			return false;
		}

		private async void button_write_uint_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			uint value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					uint[] value = GetArrayFromString(input, (long m, int n) => (uint)(m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_uint, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_uint, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "uint");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("uint Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_uint_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_uint, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_uint, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "uint", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("uint Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool try_get_long_value(string input, out long value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (long.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (long.TryParse(input, out value))
			{
				return true;
			}
			value = 0L;
			return false;
		}

		private async void button_write_long_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			long value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					long[] value = GetArrayFromString(input, (long m, int n) => m + n);
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_long, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_long, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "long");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("long Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_long_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_long, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_long, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "long", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("long Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private bool try_get_ulong_value(string input, out ulong value)
		{
			if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				if (ulong.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out value))
				{
					return true;
				}
			}
			else if (ulong.TryParse(input, out value))
			{
				return true;
			}
			value = 0uL;
			return false;
		}

		private async void button_write_ulong_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			ulong value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					ulong[] value = GetArrayFromString(input, (long m, int n) => (ulong)(m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_ulong, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_ulong, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "ulong");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("ulong Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (try_get_ulong_value(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_ulong, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_ulong, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "ulong", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("ulong Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private async void button_write_float_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			float value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					float[] value = GetArrayFromString(input, (Func<long, int, float>)((long m, int n) => m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_float, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_float, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "float");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("float Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (float.TryParse(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_float, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_float, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "float", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("float Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private async void button_write_double_Click(object sender, EventArgs e)
		{
			string input = GetWriteValueText();
			double value2;
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					double[] value = GetArrayFromString(input, (Func<long, int, double>)((long m, int n) => m + n));
					if (isAsync)
					{
						await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value), button_write_double, input);
					}
					else
					{
						RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value), button_write_double, input);
					}
					GetWriteArrayCode(textBox_write_address.Text, value, "double");
				}
				catch (Exception ex)
				{
					if (checkBox_write_timer.Checked)
					{
						checkBox_write_timer.Checked = false;
					}
					DemoUtils.ShowMessage("float Data is not corrent: " + input + Environment.NewLine + ex.Message);
				}
			}
			else if (double.TryParse(input, out value2))
			{
				if (isAsync)
				{
					await RenderWriteResult(() => readWriteNet.WriteAsync(textBox_write_address.Text, value2), button_write_double, input);
				}
				else
				{
					RenderWriteResult(() => readWriteNet.Write(textBox_write_address.Text, value2), button_write_double, input);
				}
				GetWriteCode(textBox_write_address.Text, input, "double", false);
			}
			else
			{
				if (checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				DemoUtils.ShowMessage("double Data is not corrent: " + input);
			}
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private async void button_write_string_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_write_string.Enabled = false;
				DateTime start = DateTime.Now;
				OperateResult write = ((comboBox_write_Encoding.SelectedIndex != 0) ? (await readWriteNet.WriteAsync(textBox_write_address.Text, textBox_write_text.Text, DemoUtils.GetEncodingFromIndex(comboBox_write_Encoding.SelectedIndex))) : (await readWriteNet.WriteAsync(textBox_write_address.Text, textBox_write_text.Text)));
				SetTimeSpend(Convert.ToInt32((DateTime.Now - start).TotalMilliseconds));
				if (!write.IsSuccess && checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				WriteResultRender(write, textBox_write_address.Text, string.Empty);
				button_write_string.Enabled = true;
			}
			else
			{
				DateTime start2 = DateTime.Now;
				OperateResult write2 = ((comboBox_write_Encoding.SelectedIndex != 0) ? readWriteNet.Write(textBox_write_address.Text, textBox_write_text.Text, DemoUtils.GetEncodingFromIndex(comboBox_write_Encoding.SelectedIndex)) : readWriteNet.Write(textBox_write_address.Text, textBox_write_text.Text));
				SetTimeSpend(Convert.ToInt32((DateTime.Now - start2).TotalMilliseconds));
				if (!write2.IsSuccess && checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				WriteResultRender(write2, textBox_write_address.Text, string.Empty);
			}
			GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", \"" + textBox_write_text.Text + "\", " + DemoUtils.GetEncodingTextFromIndex(comboBox_write_Encoding.SelectedIndex) + " ) );");
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		private async void button_write_hex_Click(object sender, EventArgs e)
		{
			if (isAsync)
			{
				button_write_string.Enabled = false;
				DateTime start = DateTime.Now;
				OperateResult write = await readWriteNet.WriteAsync(textBox_write_address.Text, textBox_write_text.Text.ToHexBytes());
				SetTimeSpend(Convert.ToInt32((DateTime.Now - start).TotalMilliseconds));
				if (!write.IsSuccess && checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				WriteResultRender(write, textBox_write_address.Text, string.Empty);
				button_write_string.Enabled = true;
			}
			else
			{
				DateTime start2 = DateTime.Now;
				OperateResult write2 = readWriteNet.Write(textBox_write_address.Text, textBox_write_text.Text.ToHexBytes());
				SetTimeSpend(Convert.ToInt32((DateTime.Now - start2).TotalMilliseconds));
				if (!write2.IsSuccess && checkBox_write_timer.Checked)
				{
					checkBox_write_timer.Checked = false;
				}
				WriteResultRender(write2, textBox_write_address.Text, string.Empty);
			}
			GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", \"" + textBox_write_text.Text + "\".ToHexBytes( ) ) );");
			if (checkBox_write_timer.Checked)
			{
				button_write_timer = sender as Button;
			}
		}

		public void WriteResultRender(OperateResult result, string address, string input)
		{
			if (checkBox_write_timer.Checked && result.IsSuccess)
			{
				label16.Text = DateTime.Now.ToString("HH:mm:ss") + " Write Success: " + input;
			}
			else
			{
				DemoUtils.WriteResultRender(result, address);
			}
			if (!result.IsSuccess && checkBox_write_timer.Checked)
			{
				checkBox_write_timer.Checked = false;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			FormCurveMonitor formCurveMonitor = new FormCurveMonitor();
			if (deviceImage != null)
			{
				formCurveMonitor.SetProtocolImage(deviceImage);
			}
			formCurveMonitor.SetReadWrite(readWriteNet, address);
			FormMain.Form.ShowDockForm(formCurveMonitor);
		}

		private void GetReadCode(string address, string method, bool isArray)
		{
			if (!checkBox_read_timer.Checked)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("// 当前读取操作的代码 The code for the current read operation");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append(method);
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("if (read.IsSuccess)");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("{");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    Console.WriteLine( \"Read [" + address + "] Success, Value: \" + read.Content");
				if (isArray)
				{
					stringBuilder.Append(".ToArrayString( ) );");
				}
				else
				{
					stringBuilder.Append(" );");
				}
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("}");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("else");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("{");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    Console.WriteLine( \"Read [" + address + "] failed: \" + read.Message );");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("}");
				this.MethodCodeClick?.Invoke(readWriteNet, stringBuilder.ToString());
			}
		}

		private void GetWriteCode(string address, string input, string type, bool isArray)
		{
			if (isArray)
			{
				GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", \"" + input + "\".ToStringArray<" + type + ">( ) );");
			}
			else if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", " + type + ".Parse( \"" + input.Substring(2) + "\", NumberStyles.HexNumber ) );");
			}
			else
			{
				GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", " + type + ".Parse( \"" + input + "\" ) );");
			}
		}

		private void GetWriteArrayCode<T>(string address, T[] values, string type)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("new ");
			stringBuilder.Append(type);
			stringBuilder.Append("[] { ");
			if (values != null && values.Length != 0)
			{
				for (int i = 0; i < values.Length; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append(",");
					}
					stringBuilder.Append(values[i].ToString());
				}
			}
			stringBuilder.Append(" }");
			GetWriteCode(address, "OperateResult write = @deviceName.Write( \"" + address + "\", " + stringBuilder.ToString() + ");");
		}

		private void GetWriteCode(string address, string method)
		{
			if (!checkBox_write_timer.Checked)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("// 当前写入操作的代码 The code for the current write operation");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append(method);
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("if (write.IsSuccess)");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("{");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    Console.WriteLine( \"Write [" + address + "] success\" );");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("}");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("else");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("{");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    Console.WriteLine( \"Write [" + address + "] failed: \" + write.Message );");
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("}");
				this.MethodCodeClick?.Invoke(readWriteNet, stringBuilder.ToString());
			}
		}

		private void textBox_read_search_MouseClick(object sender, MouseEventArgs e)
		{
			if (!read_search_entered)
			{
				read_search_entered = true;
				textBox_read_search.Text = string.Empty;
				textBox_read_search.ForeColor = Color.Black;
			}
		}

		private void textBox_read_search_TextChanged(object sender, EventArgs e)
		{
		}

		private void textBox_read_search_Leave(object sender, EventArgs e)
		{
			if (read_search_entered && textBox_read_search.Text == string.Empty)
			{
				textBox_read_search.Text = ((Program.Language == 1) ? "搜索内容" : "Search");
				textBox_read_search.ForeColor = Color.Silver;
				read_search_entered = false;
			}
		}

		private void button_find_string_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(textBox_read_search.Text))
			{
				find_index = -1;
				find_string = string.Empty;
				return;
			}
			if (find_string != textBox_read_search.Text)
			{
				find_index = -1;
				find_string = textBox_read_search.Text;
			}
			int num = textBox4.Text.IndexOf(find_string, (find_index >= 0) ? find_index : 0);
			if (num < 0)
			{
				find_index = -1;
				DemoUtils.ShowMessage((Program.Language == 1) ? "全部查找完毕，再次Enter键重新查找" : "All search is complete, Enter again to find again");
				return;
			}
			int length = find_string.Length;
			textBox4.Select(num, length);
			textBox4.Focus();
			textBox4.ScrollToCaret();
			find_index = num + length;
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
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label21 = new System.Windows.Forms.Label();
			this.label22 = new System.Windows.Forms.Label();
			this.label18 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label20 = new System.Windows.Forms.Label();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.label15 = new System.Windows.Forms.Label();
			this.textBox_timer_write_interval = new System.Windows.Forms.TextBox();
			this.checkBox_write_timer = new System.Windows.Forms.CheckBox();
			this.comboBox_write_Encoding = new System.Windows.Forms.ComboBox();
			this.label13 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button_write_hex = new System.Windows.Forms.Button();
			this.button_write_string = new System.Windows.Forms.Button();
			this.button_write_double = new System.Windows.Forms.Button();
			this.button_write_float = new System.Windows.Forms.Button();
			this.button_write_ulong = new System.Windows.Forms.Button();
			this.button_write_long = new System.Windows.Forms.Button();
			this.button_write_uint = new System.Windows.Forms.Button();
			this.button_write_int = new System.Windows.Forms.Button();
			this.button_write_ushort = new System.Windows.Forms.Button();
			this.button_write_short = new System.Windows.Forms.Button();
			this.button_write_byte = new System.Windows.Forms.Button();
			this.button_write_bool = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_write_text = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_write_address = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.label19 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.radioButton_read_int = new System.Windows.Forms.RadioButton();
			this.radioButton_read_hex = new System.Windows.Forms.RadioButton();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton_sync = new System.Windows.Forms.RadioButton();
			this.radioButton_async = new System.Windows.Forms.RadioButton();
			this.button_find_string = new System.Windows.Forms.Button();
			this.textBox_read_search = new System.Windows.Forms.TextBox();
			this.checkBox_mask_duplicates = new System.Windows.Forms.CheckBox();
			this.label17 = new System.Windows.Forms.Label();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.label14 = new System.Windows.Forms.Label();
			this.textBox_read_timer_interval = new System.Windows.Forms.TextBox();
			this.checkBox_read_timer = new System.Windows.Forms.CheckBox();
			this.comboBox_read_encoding = new System.Windows.Forms.ComboBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.button_read_string = new System.Windows.Forms.Button();
			this.button_read_double = new System.Windows.Forms.Button();
			this.button_read_float = new System.Windows.Forms.Button();
			this.button_read_ulong = new System.Windows.Forms.Button();
			this.button_read_long = new System.Windows.Forms.Button();
			this.button_read_uint = new System.Windows.Forms.Button();
			this.button_read_int = new System.Windows.Forms.Button();
			this.button_read_ushort = new System.Windows.Forms.Button();
			this.button_read_short = new System.Windows.Forms.Button();
			this.button_read_byte = new System.Windows.Forms.Button();
			this.button_read_bool = new System.Windows.Forms.Button();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.radioButton_read_bit = new System.Windows.Forms.RadioButton();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox2.Controls.Add(this.label21);
			this.groupBox2.Controls.Add(this.label22);
			this.groupBox2.Controls.Add(this.label18);
			this.groupBox2.Controls.Add(this.label16);
			this.groupBox2.Controls.Add(this.label20);
			this.groupBox2.Controls.Add(this.groupBox4);
			this.groupBox2.Controls.Add(this.label15);
			this.groupBox2.Controls.Add(this.textBox_timer_write_interval);
			this.groupBox2.Controls.Add(this.checkBox_write_timer);
			this.groupBox2.Controls.Add(this.comboBox_write_Encoding);
			this.groupBox2.Controls.Add(this.label13);
			this.groupBox2.Controls.Add(this.label5);
			this.groupBox2.Controls.Add(this.label11);
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.label4);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.button_write_hex);
			this.groupBox2.Controls.Add(this.button_write_string);
			this.groupBox2.Controls.Add(this.button_write_double);
			this.groupBox2.Controls.Add(this.button_write_float);
			this.groupBox2.Controls.Add(this.button_write_ulong);
			this.groupBox2.Controls.Add(this.button_write_long);
			this.groupBox2.Controls.Add(this.button_write_uint);
			this.groupBox2.Controls.Add(this.button_write_int);
			this.groupBox2.Controls.Add(this.button_write_ushort);
			this.groupBox2.Controls.Add(this.button_write_short);
			this.groupBox2.Controls.Add(this.button_write_byte);
			this.groupBox2.Controls.Add(this.button_write_bool);
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this.textBox_write_text);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.textBox_write_address);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Controls.Add(this.label19);
			this.groupBox2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox2.Location = new System.Drawing.Point(516, 0);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(436, 270);
			this.groupBox2.TabIndex = 3;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "单数据写入测试";
			this.label21.AutoSize = true;
			this.label21.ForeColor = System.Drawing.Color.Gray;
			this.label21.Location = new System.Drawing.Point(168, 219);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(13, 17);
			this.label21.TabIndex = 36;
			this.label21.Text = "-";
			this.label22.AutoSize = true;
			this.label22.ForeColor = System.Drawing.Color.Gray;
			this.label22.Location = new System.Drawing.Point(122, 219);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(43, 17);
			this.label22.TabIndex = 35;
			this.label22.Text = "Tick：";
			this.label18.AutoSize = true;
			this.label18.ForeColor = System.Drawing.Color.Gray;
			this.label18.Location = new System.Drawing.Point(55, 219);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(34, 17);
			this.label18.TabIndex = 34;
			this.label18.Text = "- ms";
			this.label16.AutoSize = true;
			this.label16.ForeColor = System.Drawing.Color.Gray;
			this.label16.Location = new System.Drawing.Point(205, 247);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(180, 17);
			this.label16.TabIndex = 32;
			this.label16.Text = "动态写入1-100，值输入 {1:100}";
			this.label16.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.label20.AutoSize = true;
			this.label20.ForeColor = System.Drawing.Color.Gray;
			this.label20.Location = new System.Drawing.Point(9, 219);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(41, 17);
			this.label20.TabIndex = 33;
			this.label20.Text = "Ave：";
			this.groupBox4.Location = new System.Drawing.Point(6, 237);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(425, 2);
			this.groupBox4.TabIndex = 31;
			this.groupBox4.TabStop = false;
			this.label15.AutoSize = true;
			this.label15.ForeColor = System.Drawing.Color.Gray;
			this.label15.Location = new System.Drawing.Point(174, 247);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(25, 17);
			this.label15.TabIndex = 28;
			this.label15.Text = "ms";
			this.textBox_timer_write_interval.Location = new System.Drawing.Point(101, 243);
			this.textBox_timer_write_interval.Name = "textBox_timer_write_interval";
			this.textBox_timer_write_interval.Size = new System.Drawing.Size(72, 23);
			this.textBox_timer_write_interval.TabIndex = 30;
			this.textBox_timer_write_interval.Text = "1000";
			this.checkBox_write_timer.AutoSize = true;
			this.checkBox_write_timer.ForeColor = System.Drawing.Color.Gray;
			this.checkBox_write_timer.Location = new System.Drawing.Point(6, 245);
			this.checkBox_write_timer.Name = "checkBox_write_timer";
			this.checkBox_write_timer.Size = new System.Drawing.Size(75, 21);
			this.checkBox_write_timer.TabIndex = 29;
			this.checkBox_write_timer.Text = "定时写入";
			this.checkBox_write_timer.UseVisualStyleBackColor = true;
			this.comboBox_write_Encoding.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_write_Encoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_write_Encoding.FormattingEnabled = true;
			this.comboBox_write_Encoding.Location = new System.Drawing.Point(346, 205);
			this.comboBox_write_Encoding.Name = "comboBox_write_Encoding";
			this.comboBox_write_Encoding.Size = new System.Drawing.Size(84, 25);
			this.comboBox_write_Encoding.TabIndex = 27;
			this.label13.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(257, 209);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(44, 17);
			this.label13.TabIndex = 26;
			this.label13.Text = "编码：";
			this.label5.AutoSize = true;
			this.label5.ForeColor = System.Drawing.Color.Gray;
			this.label5.Location = new System.Drawing.Point(168, 201);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(34, 17);
			this.label5.TabIndex = 25;
			this.label5.Text = "- ms";
			this.label11.AutoSize = true;
			this.label11.ForeColor = System.Drawing.Color.Gray;
			this.label11.Location = new System.Drawing.Point(122, 201);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(42, 17);
			this.label11.TabIndex = 24;
			this.label11.Text = "Min：";
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.Color.Gray;
			this.label3.Location = new System.Drawing.Point(54, 202);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(34, 17);
			this.label3.TabIndex = 23;
			this.label3.Text = "- ms";
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.Color.Gray;
			this.label4.Location = new System.Drawing.Point(8, 202);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(45, 17);
			this.label4.TabIndex = 22;
			this.label4.Text = "Max：";
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.ForeColor = System.Drawing.Color.Teal;
			this.label2.Location = new System.Drawing.Point(90, 184);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(78, 18);
			this.label2.TabIndex = 21;
			this.label2.Text = "- ms";
			this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.button_write_hex.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_hex.Location = new System.Drawing.Point(257, 173);
			this.button_write_hex.Name = "button_write_hex";
			this.button_write_hex.Size = new System.Drawing.Size(84, 28);
			this.button_write_hex.TabIndex = 19;
			this.button_write_hex.Text = "Hex写入";
			this.button_write_hex.UseVisualStyleBackColor = true;
			this.button_write_hex.Click += new System.EventHandler(button_write_hex_Click);
			this.button_write_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_string.Location = new System.Drawing.Point(348, 173);
			this.button_write_string.Name = "button_write_string";
			this.button_write_string.Size = new System.Drawing.Size(84, 28);
			this.button_write_string.TabIndex = 16;
			this.button_write_string.Text = "字符串写入";
			this.button_write_string.UseVisualStyleBackColor = true;
			this.button_write_string.Click += new System.EventHandler(button_write_string_Click);
			this.button_write_double.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_double.Location = new System.Drawing.Point(348, 142);
			this.button_write_double.Name = "button_write_double";
			this.button_write_double.Size = new System.Drawing.Size(84, 28);
			this.button_write_double.TabIndex = 15;
			this.button_write_double.Text = "double写入";
			this.button_write_double.UseVisualStyleBackColor = true;
			this.button_write_double.Click += new System.EventHandler(button_write_double_Click);
			this.button_write_float.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_float.Location = new System.Drawing.Point(257, 142);
			this.button_write_float.Name = "button_write_float";
			this.button_write_float.Size = new System.Drawing.Size(84, 28);
			this.button_write_float.TabIndex = 14;
			this.button_write_float.Text = "float写入";
			this.button_write_float.UseVisualStyleBackColor = true;
			this.button_write_float.Click += new System.EventHandler(button_write_float_Click);
			this.button_write_ulong.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_ulong.Location = new System.Drawing.Point(348, 111);
			this.button_write_ulong.Name = "button_write_ulong";
			this.button_write_ulong.Size = new System.Drawing.Size(84, 28);
			this.button_write_ulong.TabIndex = 13;
			this.button_write_ulong.Text = "ulong写入";
			this.button_write_ulong.UseVisualStyleBackColor = true;
			this.button_write_ulong.Click += new System.EventHandler(button_write_ulong_Click);
			this.button_write_long.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_long.Location = new System.Drawing.Point(257, 111);
			this.button_write_long.Name = "button_write_long";
			this.button_write_long.Size = new System.Drawing.Size(84, 28);
			this.button_write_long.TabIndex = 12;
			this.button_write_long.Text = "long写入";
			this.button_write_long.UseVisualStyleBackColor = true;
			this.button_write_long.Click += new System.EventHandler(button_write_long_Click);
			this.button_write_uint.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_uint.Location = new System.Drawing.Point(348, 80);
			this.button_write_uint.Name = "button_write_uint";
			this.button_write_uint.Size = new System.Drawing.Size(84, 28);
			this.button_write_uint.TabIndex = 11;
			this.button_write_uint.Text = "uint写入";
			this.button_write_uint.UseVisualStyleBackColor = true;
			this.button_write_uint.Click += new System.EventHandler(button_write_uint_Click);
			this.button_write_int.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_int.Location = new System.Drawing.Point(257, 80);
			this.button_write_int.Name = "button_write_int";
			this.button_write_int.Size = new System.Drawing.Size(84, 28);
			this.button_write_int.TabIndex = 10;
			this.button_write_int.Text = "int写入";
			this.button_write_int.UseVisualStyleBackColor = true;
			this.button_write_int.Click += new System.EventHandler(button_write_int_Click);
			this.button_write_ushort.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_ushort.Location = new System.Drawing.Point(348, 49);
			this.button_write_ushort.Name = "button_write_ushort";
			this.button_write_ushort.Size = new System.Drawing.Size(84, 28);
			this.button_write_ushort.TabIndex = 9;
			this.button_write_ushort.Text = "ushort写入";
			this.button_write_ushort.UseVisualStyleBackColor = true;
			this.button_write_ushort.Click += new System.EventHandler(button_write_ushort_Click);
			this.button_write_short.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_short.Location = new System.Drawing.Point(257, 49);
			this.button_write_short.Name = "button_write_short";
			this.button_write_short.Size = new System.Drawing.Size(84, 28);
			this.button_write_short.TabIndex = 8;
			this.button_write_short.Text = "short写入";
			this.button_write_short.UseVisualStyleBackColor = true;
			this.button_write_short.Click += new System.EventHandler(button_write_short_Click);
			this.button_write_byte.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_byte.Location = new System.Drawing.Point(348, 18);
			this.button_write_byte.Name = "button_write_byte";
			this.button_write_byte.Size = new System.Drawing.Size(84, 28);
			this.button_write_byte.TabIndex = 7;
			this.button_write_byte.Text = "byte写入";
			this.button_write_byte.UseVisualStyleBackColor = true;
			this.button_write_byte.Click += new System.EventHandler(button_write_byte_Click);
			this.button_write_bool.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_write_bool.Location = new System.Drawing.Point(257, 18);
			this.button_write_bool.Name = "button_write_bool";
			this.button_write_bool.Size = new System.Drawing.Size(84, 28);
			this.button_write_bool.TabIndex = 6;
			this.button_write_bool.Text = "bool写入";
			this.button_write_bool.UseVisualStyleBackColor = true;
			this.button_write_bool.Click += new System.EventHandler(button_write_bool_Click);
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.ForeColor = System.Drawing.Color.Teal;
			this.label1.Location = new System.Drawing.Point(8, 184);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(83, 17);
			this.label1.TabIndex = 20;
			this.label1.Text = "当前通信耗时:";
			this.textBox_write_text.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_write_text.Location = new System.Drawing.Point(63, 50);
			this.textBox_write_text.Multiline = true;
			this.textBox_write_text.Name = "textBox_write_text";
			this.textBox_write_text.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_write_text.Size = new System.Drawing.Size(188, 79);
			this.textBox_write_text.TabIndex = 5;
			this.textBox_write_text.Text = "False";
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(5, 52);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(32, 17);
			this.label9.TabIndex = 4;
			this.label9.Text = "值：";
			this.textBox_write_address.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_write_address.Location = new System.Drawing.Point(63, 21);
			this.textBox_write_address.Name = "textBox_write_address";
			this.textBox_write_address.Size = new System.Drawing.Size(188, 23);
			this.textBox_write_address.TabIndex = 3;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(5, 24);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 2;
			this.label10.Text = "地址：";
			this.label19.ForeColor = System.Drawing.Color.IndianRed;
			this.label19.Location = new System.Drawing.Point(7, 132);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(244, 53);
			this.label19.TabIndex = 17;
			this.label19.Text = "注意：字符串需要能转化成对应的数据\r\nBool使用 True, False, 0, 1\r\n数组：[1,2,3], 连续: [1:100] 重复: [1*100]";
			this.button1.Location = new System.Drawing.Point(2, 206);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(54, 27);
			this.button1.TabIndex = 18;
			this.button1.Text = "曲线";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox1.Controls.Add(this.panel2);
			this.groupBox1.Controls.Add(this.panel1);
			this.groupBox1.Controls.Add(this.button_find_string);
			this.groupBox1.Controls.Add(this.textBox_read_search);
			this.groupBox1.Controls.Add(this.checkBox_mask_duplicates);
			this.groupBox1.Controls.Add(this.label17);
			this.groupBox1.Controls.Add(this.groupBox3);
			this.groupBox1.Controls.Add(this.label14);
			this.groupBox1.Controls.Add(this.textBox_read_timer_interval);
			this.groupBox1.Controls.Add(this.checkBox_read_timer);
			this.groupBox1.Controls.Add(this.comboBox_read_encoding);
			this.groupBox1.Controls.Add(this.label12);
			this.groupBox1.Controls.Add(this.textBox1);
			this.groupBox1.Controls.Add(this.button1);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textBox5);
			this.groupBox1.Controls.Add(this.button_read_string);
			this.groupBox1.Controls.Add(this.button_read_double);
			this.groupBox1.Controls.Add(this.button_read_float);
			this.groupBox1.Controls.Add(this.button_read_ulong);
			this.groupBox1.Controls.Add(this.button_read_long);
			this.groupBox1.Controls.Add(this.button_read_uint);
			this.groupBox1.Controls.Add(this.button_read_int);
			this.groupBox1.Controls.Add(this.button_read_ushort);
			this.groupBox1.Controls.Add(this.button_read_short);
			this.groupBox1.Controls.Add(this.button_read_byte);
			this.groupBox1.Controls.Add(this.button_read_bool);
			this.groupBox1.Controls.Add(this.textBox4);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox1.Location = new System.Drawing.Point(0, 0);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(512, 270);
			this.groupBox1.TabIndex = 2;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "单数据读取测试";
			this.panel2.Controls.Add(this.radioButton_read_bit);
			this.panel2.Controls.Add(this.radioButton_read_int);
			this.panel2.Controls.Add(this.radioButton_read_hex);
			this.panel2.Location = new System.Drawing.Point(2, 72);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(58, 67);
			this.panel2.TabIndex = 40;
			this.radioButton_read_int.AutoSize = true;
			this.radioButton_read_int.Checked = true;
			this.radioButton_read_int.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.radioButton_read_int.ForeColor = System.Drawing.Color.Gray;
			this.radioButton_read_int.Location = new System.Drawing.Point(4, 3);
			this.radioButton_read_int.Name = "radioButton_read_int";
			this.radioButton_read_int.Size = new System.Drawing.Size(47, 21);
			this.radioButton_read_int.TabIndex = 0;
			this.radioButton_read_int.TabStop = true;
			this.radioButton_read_int.Text = "Dec";
			this.radioButton_read_int.UseVisualStyleBackColor = true;
			this.radioButton_read_hex.AutoSize = true;
			this.radioButton_read_hex.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
			this.radioButton_read_hex.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Black;
			this.radioButton_read_hex.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.radioButton_read_hex.ForeColor = System.Drawing.Color.Gray;
			this.radioButton_read_hex.Location = new System.Drawing.Point(4, 24);
			this.radioButton_read_hex.Name = "radioButton_read_hex";
			this.radioButton_read_hex.Size = new System.Drawing.Size(47, 21);
			this.radioButton_read_hex.TabIndex = 1;
			this.radioButton_read_hex.Text = "Hex";
			this.radioButton_read_hex.UseVisualStyleBackColor = true;
			this.panel1.Controls.Add(this.radioButton_sync);
			this.panel1.Controls.Add(this.radioButton_async);
			this.panel1.Location = new System.Drawing.Point(2, 157);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(58, 48);
			this.panel1.TabIndex = 39;
			this.radioButton_sync.AutoSize = true;
			this.radioButton_sync.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.radioButton_sync.ForeColor = System.Drawing.Color.Gray;
			this.radioButton_sync.Location = new System.Drawing.Point(4, 3);
			this.radioButton_sync.Name = "radioButton_sync";
			this.radioButton_sync.Size = new System.Drawing.Size(49, 21);
			this.radioButton_sync.TabIndex = 0;
			this.radioButton_sync.Text = "同步";
			this.radioButton_sync.UseVisualStyleBackColor = true;
			this.radioButton_async.AutoSize = true;
			this.radioButton_async.Checked = true;
			this.radioButton_async.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
			this.radioButton_async.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Black;
			this.radioButton_async.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.radioButton_async.ForeColor = System.Drawing.Color.Gray;
			this.radioButton_async.Location = new System.Drawing.Point(4, 24);
			this.radioButton_async.Name = "radioButton_async";
			this.radioButton_async.Size = new System.Drawing.Size(49, 21);
			this.radioButton_async.TabIndex = 1;
			this.radioButton_async.TabStop = true;
			this.radioButton_async.Text = "异步";
			this.radioButton_async.UseVisualStyleBackColor = true;
			this.button_find_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_find_string.Location = new System.Drawing.Point(456, 241);
			this.button_find_string.Name = "button_find_string";
			this.button_find_string.Size = new System.Drawing.Size(50, 25);
			this.button_find_string.TabIndex = 29;
			this.button_find_string.Text = "搜索";
			this.button_find_string.UseVisualStyleBackColor = true;
			this.button_find_string.Click += new System.EventHandler(button_find_string_Click);
			this.textBox_read_search.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_read_search.ForeColor = System.Drawing.Color.Silver;
			this.textBox_read_search.Location = new System.Drawing.Point(369, 242);
			this.textBox_read_search.Name = "textBox_read_search";
			this.textBox_read_search.Size = new System.Drawing.Size(85, 23);
			this.textBox_read_search.TabIndex = 28;
			this.textBox_read_search.Text = "搜索内容";
			this.textBox_read_search.MouseClick += new System.Windows.Forms.MouseEventHandler(textBox_read_search_MouseClick);
			this.textBox_read_search.TextChanged += new System.EventHandler(textBox_read_search_TextChanged);
			this.textBox_read_search.Leave += new System.EventHandler(textBox_read_search_Leave);
			this.checkBox_mask_duplicates.AutoSize = true;
			this.checkBox_mask_duplicates.ForeColor = System.Drawing.Color.Gray;
			this.checkBox_mask_duplicates.Location = new System.Drawing.Point(268, 244);
			this.checkBox_mask_duplicates.Name = "checkBox_mask_duplicates";
			this.checkBox_mask_duplicates.Size = new System.Drawing.Size(99, 21);
			this.checkBox_mask_duplicates.TabIndex = 27;
			this.checkBox_mask_duplicates.Text = "屏蔽重复的值";
			this.checkBox_mask_duplicates.UseVisualStyleBackColor = true;
			this.label17.AutoSize = true;
			this.label17.ForeColor = System.Drawing.Color.DarkGreen;
			this.label17.Location = new System.Drawing.Point(168, 246);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(46, 17);
			this.label17.TabIndex = 26;
			this.label17.Text = "次数: 0";
			this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.groupBox3.Location = new System.Drawing.Point(4, 237);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(502, 2);
			this.groupBox3.TabIndex = 25;
			this.groupBox3.TabStop = false;
			this.label14.AutoSize = true;
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(143, 246);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(25, 17);
			this.label14.TabIndex = 5;
			this.label14.Text = "ms";
			this.textBox_read_timer_interval.Location = new System.Drawing.Point(91, 243);
			this.textBox_read_timer_interval.Name = "textBox_read_timer_interval";
			this.textBox_read_timer_interval.Size = new System.Drawing.Size(49, 23);
			this.textBox_read_timer_interval.TabIndex = 24;
			this.textBox_read_timer_interval.Text = "1000";
			this.checkBox_read_timer.AutoSize = true;
			this.checkBox_read_timer.ForeColor = System.Drawing.Color.Gray;
			this.checkBox_read_timer.Location = new System.Drawing.Point(4, 244);
			this.checkBox_read_timer.Name = "checkBox_read_timer";
			this.checkBox_read_timer.Size = new System.Drawing.Size(75, 21);
			this.checkBox_read_timer.TabIndex = 23;
			this.checkBox_read_timer.Text = "定时读取";
			this.checkBox_read_timer.UseVisualStyleBackColor = true;
			this.comboBox_read_encoding.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.comboBox_read_encoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_read_encoding.FormattingEnabled = true;
			this.comboBox_read_encoding.Location = new System.Drawing.Point(419, 205);
			this.comboBox_read_encoding.Name = "comboBox_read_encoding";
			this.comboBox_read_encoding.Size = new System.Drawing.Size(84, 25);
			this.comboBox_read_encoding.TabIndex = 22;
			this.label12.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(325, 209);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(44, 17);
			this.label12.TabIndex = 21;
			this.label12.Text = "编码：";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(369, 177);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(41, 23);
			this.textBox1.TabIndex = 19;
			this.textBox1.Text = "10";
			this.label8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(325, 180);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(44, 17);
			this.label8.TabIndex = 20;
			this.label8.Text = "长度：";
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(258, 23);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(64, 23);
			this.textBox5.TabIndex = 18;
			this.textBox5.Text = "1";
			this.button_read_string.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_string.Location = new System.Drawing.Point(419, 174);
			this.button_read_string.Name = "button_read_string";
			this.button_read_string.Size = new System.Drawing.Size(84, 28);
			this.button_read_string.TabIndex = 16;
			this.button_read_string.Text = "字符串读取";
			this.button_read_string.UseVisualStyleBackColor = true;
			this.button_read_string.Click += new System.EventHandler(button_read_string_Click);
			this.button_read_double.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_double.Location = new System.Drawing.Point(419, 144);
			this.button_read_double.Name = "button_read_double";
			this.button_read_double.Size = new System.Drawing.Size(84, 28);
			this.button_read_double.TabIndex = 15;
			this.button_read_double.Text = "double读取";
			this.button_read_double.UseVisualStyleBackColor = true;
			this.button_read_double.Click += new System.EventHandler(button_read_double_Click);
			this.button_read_float.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_float.Location = new System.Drawing.Point(328, 144);
			this.button_read_float.Name = "button_read_float";
			this.button_read_float.Size = new System.Drawing.Size(84, 28);
			this.button_read_float.TabIndex = 14;
			this.button_read_float.Text = "float读取";
			this.button_read_float.UseVisualStyleBackColor = true;
			this.button_read_float.Click += new System.EventHandler(button_read_float_Click);
			this.button_read_ulong.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_ulong.Location = new System.Drawing.Point(419, 113);
			this.button_read_ulong.Name = "button_read_ulong";
			this.button_read_ulong.Size = new System.Drawing.Size(84, 28);
			this.button_read_ulong.TabIndex = 13;
			this.button_read_ulong.Text = "ulong读取";
			this.button_read_ulong.UseVisualStyleBackColor = true;
			this.button_read_ulong.Click += new System.EventHandler(button_read_ulong_Click);
			this.button_read_long.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_long.Location = new System.Drawing.Point(328, 113);
			this.button_read_long.Name = "button_read_long";
			this.button_read_long.Size = new System.Drawing.Size(84, 28);
			this.button_read_long.TabIndex = 12;
			this.button_read_long.Text = "long读取";
			this.button_read_long.UseVisualStyleBackColor = true;
			this.button_read_long.Click += new System.EventHandler(button_read_long_Click);
			this.button_read_uint.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_uint.Location = new System.Drawing.Point(419, 82);
			this.button_read_uint.Name = "button_read_uint";
			this.button_read_uint.Size = new System.Drawing.Size(84, 28);
			this.button_read_uint.TabIndex = 11;
			this.button_read_uint.Text = "uint读取";
			this.button_read_uint.UseVisualStyleBackColor = true;
			this.button_read_uint.Click += new System.EventHandler(button_read_uint_Click);
			this.button_read_int.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_int.Location = new System.Drawing.Point(328, 82);
			this.button_read_int.Name = "button_read_int";
			this.button_read_int.Size = new System.Drawing.Size(84, 28);
			this.button_read_int.TabIndex = 10;
			this.button_read_int.Text = "int读取";
			this.button_read_int.UseVisualStyleBackColor = true;
			this.button_read_int.Click += new System.EventHandler(button_read_int_Click);
			this.button_read_ushort.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_ushort.Location = new System.Drawing.Point(419, 51);
			this.button_read_ushort.Name = "button_read_ushort";
			this.button_read_ushort.Size = new System.Drawing.Size(84, 28);
			this.button_read_ushort.TabIndex = 9;
			this.button_read_ushort.Text = "ushort读取";
			this.button_read_ushort.UseVisualStyleBackColor = true;
			this.button_read_ushort.Click += new System.EventHandler(button_read_ushort_Click);
			this.button_read_short.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_short.Location = new System.Drawing.Point(328, 51);
			this.button_read_short.Name = "button_read_short";
			this.button_read_short.Size = new System.Drawing.Size(84, 28);
			this.button_read_short.TabIndex = 8;
			this.button_read_short.Text = "short读取";
			this.button_read_short.UseVisualStyleBackColor = true;
			this.button_read_short.Click += new System.EventHandler(button_read_short_Click);
			this.button_read_byte.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_byte.Location = new System.Drawing.Point(419, 20);
			this.button_read_byte.Name = "button_read_byte";
			this.button_read_byte.Size = new System.Drawing.Size(84, 28);
			this.button_read_byte.TabIndex = 7;
			this.button_read_byte.Text = "byte读取";
			this.button_read_byte.UseVisualStyleBackColor = true;
			this.button_read_byte.Click += new System.EventHandler(button_read_byte_Click);
			this.button_read_bool.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_read_bool.Location = new System.Drawing.Point(328, 20);
			this.button_read_bool.Name = "button_read_bool";
			this.button_read_bool.Size = new System.Drawing.Size(84, 28);
			this.button_read_bool.TabIndex = 6;
			this.button_read_bool.Text = "bool读取";
			this.button_read_bool.UseVisualStyleBackColor = true;
			this.button_read_bool.Click += new System.EventHandler(button_read_bool_Click);
			this.textBox4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox4.Location = new System.Drawing.Point(63, 50);
			this.textBox4.Multiline = true;
			this.textBox4.Name = "textBox4";
			this.textBox4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox4.Size = new System.Drawing.Size(259, 181);
			this.textBox4.TabIndex = 5;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(5, 54);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(44, 17);
			this.label7.TabIndex = 4;
			this.label7.Text = "结果：";
			this.textBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox3.Location = new System.Drawing.Point(63, 23);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(189, 23);
			this.textBox3.TabIndex = 3;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(5, 26);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(44, 17);
			this.label6.TabIndex = 2;
			this.label6.Text = "地址：";
			this.radioButton_read_bit.AutoSize = true;
			this.radioButton_read_bit.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
			this.radioButton_read_bit.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Black;
			this.radioButton_read_bit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.radioButton_read_bit.ForeColor = System.Drawing.Color.Gray;
			this.radioButton_read_bit.Location = new System.Drawing.Point(4, 45);
			this.radioButton_read_bit.Name = "radioButton_read_bit";
			this.radioButton_read_bit.Size = new System.Drawing.Size(40, 21);
			this.radioButton_read_bit.TabIndex = 2;
			this.radioButton_read_bit.Text = "Bit";
			this.radioButton_read_bit.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Name = "UserControlReadWriteOp";
			base.Size = new System.Drawing.Size(954, 273);
			base.Load += new System.EventHandler(UserControlReadWriteOp_Load);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
		}
	}
}
