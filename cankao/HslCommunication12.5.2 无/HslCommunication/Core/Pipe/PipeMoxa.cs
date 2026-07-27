using System;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HslCommunication.Core.IMessage;
using HslCommunication.Reflection;

namespace HslCommunication.Core.Pipe
{
	/// <summary>
	/// 基于MOXA公司提供的串口驱动实现的管道类，目前仅支持windows平台，需要当前目录下存在 PCOMM.DLL 组件<br />
	/// The pipe class based on the serial port driver provided by MOXA currently only supports the windows platform and requires the existence of the PCOMM.DLL component in the current directory
	/// </summary>
	public class PipeMoxa : CommunicationPipe, IDisposable
	{
		private int portNumber = -1;

		private bool rts = false;

		private bool dtr = false;

		private bool isOpen = false;

		private int moxaBaudRate = 12;

		private int moxaDataBits = 3;

		private int moxaStopBits = 0;

		private int moxaParity = 0;

		private string moxaFormate = string.Empty;

		/// <summary>
		/// 获取或设置一个值，该值指示在串行通信中是否启用请求发送 (RTS) 信号。<br />
		/// Gets or sets a value indicating whether the request sending (RTS) signal is enabled in serial communication.
		/// </summary>
		public bool RtsEnable
		{
			get
			{
				return rts;
			}
			set
			{
				rts = value;
				if (isOpen)
				{
					PCommHelper.sio_RTS(portNumber, value ? 1 : 0);
				}
			}
		}

		/// <summary>
		/// 获取或设置一个值，该值指示在串行通信中是否启用数据终端就绪 (Drt) 信号。<br />
		/// Gets or sets a value that indicates whether the Data Terminal Ready (DRT) signal is enabled in serial communication.
		/// </summary>
		public bool DtrEnable
		{
			get
			{
				return dtr;
			}
			set
			{
				dtr = value;
				if (isOpen)
				{
					PCommHelper.sio_DTR(portNumber, value ? 1 : 0);
				}
			}
		}

		/// <summary>
		/// 从串口中至少接收的字节长度信息，默认为1个字节
		/// </summary>
		public int AtLeastReceiveLength { get; set; } = 1;

		/// <summary>
		/// 获取或设置连续接收空的数据次数，在数据接收完成时有效，每个单位消耗的时间为<see cref="P:HslCommunication.Core.Pipe.CommunicationPipe.SleepTime" />。<br />
		/// Obtain or set the number of consecutive times to receive empty data, which is valid when the data is received, and the time consumed by each unit is <see cref="P:HslCommunication.Core.Pipe.CommunicationPipe.SleepTime" />
		/// </summary>
		[HslMqttApi(Description = "Get or set the number of consecutive empty data receptions, which is valid when data reception is completed, default is 1")]
		public int ReceiveEmptyDataCount { get; set; } = 1;

		/// <summary>
		/// 是否在发送数据前清空缓冲数据，默认是false<br />
		/// Whether to empty the buffer before sending data, the default is false
		/// </summary>
		[HslMqttApi(Description = "Whether to empty the buffer before sending data, the default is false")]
		public bool IsClearCacheBeforeRead { get; set; }

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		public PipeMoxa()
		{
			base.SleepTime = 5;
			ReceiveEmptyDataCount = 4;
		}

		/// <summary>
		/// 根据串口的名称来实例化一个对象<br />
		/// Instantiate a default object
		/// </summary>
		/// <remarks>
		/// portName 支持格式化的方式，例如输入 COM3-9600-8-N-1，COM5-19200-7-E-2，其中奇偶校验的字母可选，N:无校验，O：奇校验，E:偶校验，停止位可选 0, 1, 2, 1.5 四种选项
		/// </remarks>
		public PipeMoxa(string portName)
		{
			base.SleepTime = 5;
			ReceiveEmptyDataCount = 4;
			SerialPortInni(portName);
		}

		/// <summary>
		/// 初始化串口信息，9600波特率，8位数据位，1位停止位，无奇偶校验<br />
		/// Initial serial port information, 9600 baud rate, 8 data bits, 1 stop bit, no parity
		/// </summary>
		/// <remarks>
		/// portName 支持格式化的方式，例如输入 COM3-9600-8-N-1，COM5-19200-7-E-2，其中奇偶校验的字母可选，N:无校验，O：奇校验，E:偶校验，停止位可选 0, 1, 2, 1.5 四种选项
		/// </remarks>
		/// <param name="portName">端口号信息，例如"COM3"</param>
		public void SerialPortInni(string portName)
		{
			if (portName.Contains("-"))
			{
				string portName2 = "COM1";
				int baudRate = 9600;
				int dataBits = 8;
				Parity parity = Parity.None;
				StopBits stopBits = StopBits.One;
				string[] array = portName.Split(new char[2] { '-', ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length == 0)
				{
					return;
				}
				int num = 0;
				if (!Regex.IsMatch(array[0], "^[0-9]+$"))
				{
					portName2 = array[0];
					num = 1;
				}
				if (num < array.Length)
				{
					baudRate = Convert.ToInt32(array[num++]);
				}
				if (num < array.Length)
				{
					dataBits = Convert.ToInt32(array[num++]);
				}
				if (num < array.Length)
				{
					int num2;
					switch (array[num++].ToUpper())
					{
					default:
						num2 = 4;
						break;
					case "E":
						num2 = 2;
						break;
					case "O":
						num2 = 1;
						break;
					case "N":
						num2 = 0;
						break;
					}
					parity = (Parity)num2;
				}
				if (num < array.Length)
				{
					int num3;
					switch (array[num++])
					{
					default:
						num3 = 3;
						break;
					case "0":
						num3 = 0;
						break;
					case "2":
						num3 = 2;
						break;
					case "1":
						num3 = 1;
						break;
					}
					stopBits = (StopBits)num3;
				}
				SerialPortInni(portName2, baudRate, dataBits, stopBits, parity);
			}
			else
			{
				SerialPortInni(portName, 9600, 8, StopBits.One, Parity.None);
			}
		}

		/// <summary>
		/// 初始化串口信息，波特率，数据位，停止位，奇偶校验需要全部自己来指定<br />
		/// Start serial port information, baud rate, data bit, stop bit, parity all need to be specified
		/// </summary>
		/// <param name="portName">端口号信息，例如"COM3"</param>
		/// <param name="baudRate">波特率</param>
		/// <param name="dataBits">数据位</param>
		/// <param name="stopBits">停止位</param>
		/// <param name="parity">奇偶校验</param>
		public void SerialPortInni(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity)
		{
			if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
			{
				portNumber = Convert.ToInt32(portName.Substring(3));
			}
			else
			{
				portNumber = Convert.ToInt32(portName);
			}
			switch (baudRate)
			{
			case 50:
				moxaBaudRate = 0;
				break;
			case 75:
				moxaBaudRate = 1;
				break;
			case 110:
				moxaBaudRate = 2;
				break;
			case 134:
				moxaBaudRate = 3;
				break;
			case 150:
				moxaBaudRate = 4;
				break;
			case 300:
				moxaBaudRate = 5;
				break;
			case 600:
				moxaBaudRate = 6;
				break;
			case 1200:
				moxaBaudRate = 7;
				break;
			case 1800:
				moxaBaudRate = 8;
				break;
			case 2400:
				moxaBaudRate = 9;
				break;
			case 4800:
				moxaBaudRate = 10;
				break;
			case 7200:
				moxaBaudRate = 11;
				break;
			case 9600:
				moxaBaudRate = 12;
				break;
			case 19200:
				moxaBaudRate = 13;
				break;
			case 38400:
				moxaBaudRate = 14;
				break;
			case 57600:
				moxaBaudRate = 15;
				break;
			case 115200:
				moxaBaudRate = 16;
				break;
			case 230400:
				moxaBaudRate = 17;
				break;
			case 460800:
				moxaBaudRate = 18;
				break;
			case 921600:
				moxaBaudRate = 19;
				break;
			}
			switch (dataBits)
			{
			case 5:
				moxaDataBits = 0;
				break;
			case 6:
				moxaDataBits = 1;
				break;
			case 7:
				moxaDataBits = 2;
				break;
			case 8:
				moxaDataBits = 3;
				break;
			}
			switch (stopBits)
			{
			case StopBits.One:
				moxaStopBits = 0;
				break;
			case StopBits.Two:
				moxaStopBits = 4;
				break;
			}
			switch (parity)
			{
			case Parity.None:
				moxaParity = 0;
				break;
			case Parity.Even:
				moxaParity = 24;
				break;
			case Parity.Odd:
				moxaParity = 8;
				break;
			case Parity.Mark:
				moxaParity = 40;
				break;
			case Parity.Space:
				moxaParity = 48;
				break;
			}
			moxaFormate = HslHelper.ToFormatString(portName, baudRate, dataBits, parity, stopBits);
		}

		/// <summary>
		/// 获取一个值，指示串口是否处于打开状态<br />
		/// Gets a value indicating whether the serial port is open
		/// </summary>
		/// <returns>是或否</returns>
		public bool IsOpen()
		{
			return isOpen;
		}

		/// <summary>
		/// 清除串口缓冲区的数据，并返回该数据，如果缓冲区没有数据，返回的字节数组长度为0<br />
		/// The number sent clears the data in the serial port buffer and returns that data, or if there is no data in the buffer, the length of the byte array returned is 0
		/// </summary>
		/// <returns>是否操作成功的方法</returns>
		public OperateResult<byte[]> ClearSerialCache()
		{
			return SPReceived(null, null, false);
		}

		private string GetErrorText(int ret)
		{
			switch (ret)
			{
			case 0:
				return "OK";
			case -1:
				return "Port number is invalid.";
			case -2:
				return "The board is not the MOXA compatible intelligent board";
			case -4:
				return "No data to read.";
			case -5:
				return "No such port or port is occupied by other program.";
			case -6:
				return "Can't control the port because it is set as auto H/W flow control by sio_flowctrl.";
			case -7:
				return "Bad parameter.";
			case -8:
				return "Calling Win32 function failed Call GetLastError to get the error code.";
			case -9:
				return "The com port does not support this function.";
			case -11:
				return "User abort blocked write.";
			case -12:
				return "Write timeout.";
			default:
				return "Unkown";
			}
		}

		/// <inheritdoc />
		public override OperateResult<bool> OpenCommunication()
		{
			try
			{
				if (!isOpen)
				{
					int num = PCommHelper.sio_open(portNumber);
					if (num != 0)
					{
						return new OperateResult<bool>(num, GetErrorText(num));
					}
					PCommHelper.sio_ioctl(mode: moxaDataBits | moxaParity | moxaStopBits, port: portNumber, baud: moxaBaudRate);
					if (RtsEnable)
					{
						PCommHelper.sio_RTS(portNumber, 1);
					}
					if (DtrEnable)
					{
						PCommHelper.sio_DTR(portNumber, 1);
					}
					ResetConnectErrorCount();
					isOpen = true;
					return OperateResult.CreateSuccessResult(true);
				}
				return OperateResult.CreateSuccessResult(false);
			}
			catch (Exception ex)
			{
				return new OperateResult<bool>("OpenCommunication failed: " + ex.Message);
			}
		}

		/// <inheritdoc />
		public override OperateResult CloseCommunication()
		{
			if (isOpen)
			{
				try
				{
					PCommHelper.sio_close(portNumber);
					isOpen = false;
				}
				catch (Exception ex)
				{
					return new OperateResult(ex.Message);
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		public override OperateResult Send(byte[] data, int offset, int size)
		{
			if (data != null && data.Length != 0)
			{
				if(false)
				{
					return new OperateResult<byte[]>(StringResources.Language.AuthorizationFailed);
				}
				try
				{
					int num = 0;
					if (offset == 0)
					{
						num = PCommHelper.sio_write(portNumber, data, data.Length);
					}
					else
					{
						byte[] array = data.SelectMiddle(offset, size);
						num = PCommHelper.sio_write(portNumber, array, array.Length);
					}
					if (num >= 0)
					{
						return OperateResult.CreateSuccessResult();
					}
					return new OperateResult(num, GetErrorText(num));
				}
				catch (Exception ex)
				{
					return new OperateResult(-IncrConnectErrorCount(), ex.Message);
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		public override OperateResult<int> Receive(byte[] buffer, int offset, int length, int timeOut = 60000, Action<long, long> reportProgress = null)
		{
			if(false)
			{
				return new OperateResult<int>(StringResources.Language.AuthorizationFailed);
			}
			try
			{
				if (length > 0)
				{
					int num = PCommHelper.sio_read(portNumber, ref buffer[offset], length);
					if (num >= 0)
					{
						return OperateResult.CreateSuccessResult(num);
					}
					return new OperateResult<int>(num, GetErrorText(num));
				}
				int num2 = PCommHelper.sio_read(portNumber, ref buffer[offset], buffer.Length - offset);
				if (num2 >= 0)
				{
					return OperateResult.CreateSuccessResult(num2);
				}
				return new OperateResult<int>(num2, GetErrorText(num2));
			}
			catch (Exception ex)
			{
				return new OperateResult<int>(-IncrConnectErrorCount(), ex.Message);
			}
		}

		/// <summary>
		/// 从串口接收一串字节数据信息，直到没有数据为止，如果参数awaitData为false, 第一轮接收没有数据则返回<br />
		/// Receives a string of bytes of data information from the serial port until there is no data, and returns if the parameter awaitData is false
		/// </summary>
		/// <param name="netMessage">定义的消息体对象</param>
		/// <param name="sendValue">等待发送的数据对象</param>
		/// <param name="awaitData">是否必须要等待数据返回</param>
		/// <param name="logMessage">用于消息记录的日志信息</param>
		/// <returns>结果数据对象</returns>
		private OperateResult<byte[]> SPReceived(INetMessage netMessage, byte[] sendValue, bool awaitData, Action<byte[]> logMessage = null)
		{
			if(false)
			{
				return new OperateResult<byte[]>(StringResources.Language.AuthorizationFailed);
			}
			byte[] array = null;
			MemoryStream memoryStream = null;
			try
			{
				array = new byte[1024];
				memoryStream = new MemoryStream();
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(ex.Message);
			}
			DateTime now = DateTime.Now;
			int num = 0;
			int num2 = 0;
			while (true)
			{
				num2++;
				if (num2 > 1 && base.SleepTime >= 0)
				{
					HslHelper.ThreadSleep(base.SleepTime);
				}
				try
				{
					if (PCommHelper.sio_iqueue(portNumber) < 1)
					{
						if (num2 == 1)
						{
							continue;
						}
						if ((DateTime.Now - now).TotalMilliseconds > (double)base.ReceiveTimeOut)
						{
							return new OperateResult<byte[]>(-IncrConnectErrorCount(), $"Time out: {base.ReceiveTimeOut}, received: {memoryStream.ToArray().ToHexString(' ')}");
						}
						if (memoryStream.Length >= AtLeastReceiveLength)
						{
							num++;
							if (netMessage != null || num < ReceiveEmptyDataCount)
							{
								continue;
							}
						}
						else if (awaitData)
						{
							continue;
						}
						break;
					}
					num = 0;
					int num3 = PCommHelper.sio_read(portNumber, ref array[0], array.Length);
					if (num3 < 0)
					{
						return new OperateResult<byte[]>(num3, GetErrorText(num3));
					}
					if (num3 > 0)
					{
						memoryStream.Write(array, 0, num3);
						logMessage?.Invoke(array.SelectBegin(num3));
					}
					if (netMessage != null && CheckMessageComplete(netMessage, sendValue, ref memoryStream))
					{
						break;
					}
					if (base.ReceiveTimeOut > 0 && (DateTime.Now - now).TotalMilliseconds > (double)base.ReceiveTimeOut)
					{
						return new OperateResult<byte[]>(-IncrConnectErrorCount(), $"Time out: {base.ReceiveTimeOut}, received: {memoryStream.ToArray().ToHexString(' ')}");
					}
					continue;
				}
				catch (Exception ex2)
				{
					return new OperateResult<byte[]>(-IncrConnectErrorCount(), ex2.Message);
				}
			}
			ResetConnectErrorCount();
			return OperateResult.CreateSuccessResult(memoryStream.ToArray());
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> ReceiveMessage(INetMessage netMessage, byte[] sendValue, bool useActivePush = true, Action<long, long> reportProgress = null, Action<byte[]> logMessage = null)
		{
			if (base.UseServerActivePush)
			{
				return base.ReceiveMessage(netMessage, sendValue, useActivePush, reportProgress);
			}
			return SPReceived(netMessage, sendValue, true, logMessage);
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> ReadFromCoreServer(INetMessage netMessage, byte[] sendValue, bool hasResponseData, Action<byte[]> logMessage = null)
		{
			if (IsClearCacheBeforeRead)
			{
				ClearSerialCache();
			}
			OperateResult<byte[]> operateResult = ReadFromCoreServerHelper(netMessage, sendValue, hasResponseData, 0, logMessage);
			if (operateResult.IsSuccess)
			{
				ResetConnectErrorCount();
			}
			return operateResult;
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReceiveMessageAsync(INetMessage netMessage, byte[] sendValue, bool useActivePush = true, Action<long, long> reportProgress = null, Action<byte[]> logMessage = null)
		{
			return await Task.Run(() => SPReceived(netMessage, sendValue, true, logMessage)).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReadFromCoreServerAsync(INetMessage netMessage, byte[] sendValue, bool hasResponseData, Action<byte[]> logMessage = null)
		{
			return await Task.Run(() => ReadFromCoreServer(netMessage, sendValue, hasResponseData, logMessage)).ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			CloseCommunication();
		}

		/// <summary>
		/// 获取当前串口参数的信息，以格式化的字符串返回<br />
		/// Obtain the information of the current serial port parameters and return them as formatted strings
		/// </summary>
		/// <returns>串口参数</returns>
		public string ToFormatString()
		{
			return moxaFormate;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"PipeMoxa[COM{portNumber}]";
		}
	}
}
