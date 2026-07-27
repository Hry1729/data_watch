using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Turck
{
	/// <summary>
	/// Reader协议的实现
	/// </summary>
	public class ReaderNet : DeviceTcpNet
	{
		private bool successfullyInitialized = false;

		/// <summary>
		/// 获取设备的唯一的UID信息，本值会在连接上PLC之后自动赋值
		/// </summary>
		public string UID { get; private set; }

		/// <summary>
		/// 获取当前设备的数据块总数量，本值会在连接上PLC之后自动赋值
		/// </summary>
		public byte NumberOfBlock { get; private set; }

		/// <summary>
		/// 获取当前设备的每个数据块拥有的字节数，本值会在连接上PLC之后自动赋值
		/// </summary>
		public byte BytesOfBlock { get; private set; }

		/// <summary>
		/// 实例化默认的构造方法<br />
		/// Instantiate the default constructor
		/// </summary>
		public ReaderNet()
		{
			base.WordLength = 2;
			base.ByteTransform = new RegularByteTransform();
		}

		/// <summary>
		/// 使用指定的ip地址和端口来实例化一个对象<br />
		/// Instantiate an object with the specified IP address and port
		/// </summary>
		/// <param name="ipAddress">设备的Ip地址</param>
		/// <param name="port">设备的端口号</param>
		public ReaderNet(string ipAddress, int port)
			: this()
		{
			IpAddress = ipAddress;
			Port = port;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new TurckReaderMessage();
		}

		/// <inheritdoc />
		protected override OperateResult InitializationOnConnect()
		{
			successfullyInitialized = false;
			return base.InitializationOnConnect();
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> InitializationOnConnectAsync()
		{
			successfullyInitialized = false;
			return await base.InitializationOnConnectAsync();
		}

		/// <summary>
		/// 读取指定地址的byte数据
		/// </summary>
		/// <param name="address">起始地址</param>
		/// <returns>是否读取成功的结果对象 -&gt; Whether to read the successful result object</returns>
		/// <example>参考<see cref="M:HslCommunication.Profinet.Turck.ReaderNet.Read(System.String,System.UInt16)" />的注释</example>
		[HslMqttApi("ReadByte", "")]
		public OperateResult<byte> ReadByte(string address)
		{
			return ByteTransformHelper.GetResultFromArray(Read(address, 1));
		}

		/// <summary>
		/// 向设备中写入byte数据，返回值说明<br />
		/// </summary>
		/// <param name="address">起始地址</param>
		/// <param name="value">byte数据 -&gt; Byte data</param>
		/// <returns>是否写入成功的结果对象 -&gt; Whether to write a successful result object</returns>
		[HslMqttApi("WriteByte", "")]
		public OperateResult Write(string address, byte value)
		{
			return Write(address, new byte[1] { value });
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Turck.ReaderNet.ReadByte(System.String)" />
		public async Task<OperateResult<byte>> ReadByteAsync(string address)
		{
			return ByteTransformHelper.GetResultFromArray(await ReadAsync(address, 1));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Turck.ReaderNet.Write(System.String,System.Byte)" />
		public async Task<OperateResult> WriteAsync(string address, byte value)
		{
			return await WriteAsync(address, new byte[1] { value });
		}

		private OperateResult<byte[]> CheckResponseContent(byte[] content)
		{
			if (content[1] == 10 && content[2] == 10)
			{
				if (content[5] == 0 && content[6] == 2 && content[7] == 0)
				{
					successfullyInitialized = false;
				}
				return new OperateResult<byte[]>(GetErrorText(content[5], content[6], content[7]) + " Source: " + content.ToHexString(' '));
			}
			if (content[1] == 7 && content[2] == 7)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (content.Length > 7)
			{
				return OperateResult.CreateSuccessResult(content.SelectMiddle(5, content.Length - 7));
			}
			return new OperateResult<byte[]>("Error message: " + content.ToHexString(' '));
		}

		private OperateResult<byte[]> ReadRaw(byte startBlock, byte lengthOfBlock)
		{
			List<byte[]> list = BuildReadCommand(startBlock, lengthOfBlock, BytesOfBlock);
			List<byte> list2 = new List<byte>();
			for (int i = 0; i < list.Count; i++)
			{
				OperateResult<byte[]> operateResult = ReadFromCoreServer(list[i]);
				if (!operateResult.IsSuccess)
				{
					return operateResult;
				}
				OperateResult<byte[]> operateResult2 = CheckResponseContent(operateResult.Content);
				if (!operateResult2.IsSuccess)
				{
					return operateResult2;
				}
				list2.AddRange(operateResult2.Content);
			}
			return OperateResult.CreateSuccessResult(list2.ToArray());
		}

		private OperateResult WriteRaw(byte startBlock, byte lengthOfBlock, byte[] value)
		{
			List<byte[]> list = BuildWriteCommand(startBlock, lengthOfBlock, BytesOfBlock, value);
			for (int i = 0; i < list.Count; i++)
			{
				OperateResult<byte[]> operateResult = ReadFromCoreServer(list[i]);
				if (!operateResult.IsSuccess)
				{
					return operateResult;
				}
				OperateResult<byte[]> operateResult2 = CheckResponseContent(operateResult.Content);
				if (!operateResult2.IsSuccess)
				{
					return operateResult2;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> operateResult = ReadRFIDInfo();
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult);
				}
			}
			OperateResult<ushort> operateResult2 = ParseAddress(address, false);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult2);
			}
			CalculateBlockAddress(operateResult2.Content, length, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> operateResult3 = ReadRaw(startBlock, lengthOfBlock);
			if (!operateResult3.IsSuccess)
			{
				return operateResult3;
			}
			return OperateResult.CreateSuccessResult(operateResult3.Content.SelectMiddle(operateResult2.Content % BytesOfBlock, length));
		}

		/// <inheritdoc />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> operateResult = ReadRFIDInfo();
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult);
				}
			}
			OperateResult<ushort> operateResult2 = ParseAddress(address, false);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult2);
			}
			CalculateBlockAddress(operateResult2.Content, (ushort)value.Length, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> operateResult3 = ReadRaw(startBlock, lengthOfBlock);
			if (!operateResult3.IsSuccess)
			{
				return operateResult3;
			}
			value.CopyTo(operateResult3.Content, operateResult2.Content % BytesOfBlock);
			return WriteRaw(startBlock, lengthOfBlock, operateResult3.Content);
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> operateResult = ReadRFIDInfo();
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<bool[]>(operateResult);
				}
			}
			OperateResult<ushort> operateResult2 = ParseAddress(address, true);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult2);
			}
			ushort num = (ushort)(operateResult2.Content / 8);
			ushort length2 = (ushort)((operateResult2.Content + length - 1) / 8 - num + 1);
			CalculateBlockAddress(num, length2, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> operateResult3 = ReadRaw(startBlock, lengthOfBlock);
			if (!operateResult3.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult3);
			}
			return OperateResult.CreateSuccessResult(operateResult3.Content.SelectMiddle(num % BytesOfBlock, length2).ToBoolArray().SelectMiddle(operateResult2.Content % 8, length));
		}

		/// <inheritdoc />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> operateResult = ReadRFIDInfo();
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult);
				}
			}
			OperateResult<ushort> operateResult2 = ParseAddress(address, true);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult2);
			}
			ushort num = (ushort)(operateResult2.Content / 8);
			CalculateBlockAddress(num, (ushort)((operateResult2.Content + value.Length - 1) / 8 - num + 1), BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> operateResult3 = ReadRaw(startBlock, lengthOfBlock);
			if (!operateResult3.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult3);
			}
			bool[] array = operateResult3.Content.ToBoolArray();
			value.CopyTo(array, num % BytesOfBlock * 8 + operateResult2.Content % 8);
			return WriteRaw(startBlock, lengthOfBlock, array.ToByteArray());
		}

		private async Task<OperateResult<byte[]>> ReadRawAsync(byte startBlock, byte lengthOfBlock)
		{
			List<byte[]> list = BuildReadCommand(startBlock, lengthOfBlock, BytesOfBlock);
			List<byte> result = new List<byte>();
			for (int i = 0; i < list.Count; i++)
			{
				OperateResult<byte[]> read = await ReadFromCoreServerAsync(list[i]);
				if (!read.IsSuccess)
				{
					return read;
				}
				OperateResult<byte[]> check = CheckResponseContent(read.Content);
				if (!check.IsSuccess)
				{
					return check;
				}
				result.AddRange(check.Content);
			}
			return OperateResult.CreateSuccessResult(result.ToArray());
		}

		private async Task<OperateResult> WriteRawAsync(byte startBlock, byte lengthOfBlock, byte[] value)
		{
			List<byte[]> list = BuildWriteCommand(startBlock, lengthOfBlock, BytesOfBlock, value);
			for (int i = 0; i < list.Count; i++)
			{
				OperateResult<byte[]> read = await ReadFromCoreServerAsync(list[i]);
				if (!read.IsSuccess)
				{
					return read;
				}
				OperateResult<byte[]> check = CheckResponseContent(read.Content);
				if (!check.IsSuccess)
				{
					return check;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> ini = await ReadRFIDInfoAsync();
				if (!ini.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(ini);
				}
			}
			OperateResult<ushort> addAnalysis = ParseAddress(address, false);
			if (!addAnalysis.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(addAnalysis);
			}
			CalculateBlockAddress(addAnalysis.Content, length, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> read = await ReadRawAsync(startBlock, lengthOfBlock);
			if (!read.IsSuccess)
			{
				return read;
			}
			return OperateResult.CreateSuccessResult(read.Content.SelectMiddle(addAnalysis.Content % BytesOfBlock, length));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, byte[] value)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> ini = await ReadRFIDInfoAsync();
				if (!ini.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(ini);
				}
			}
			OperateResult<ushort> addAnalysis = ParseAddress(address, false);
			if (!addAnalysis.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(addAnalysis);
			}
			CalculateBlockAddress(addAnalysis.Content, (ushort)value.Length, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> readRaw = await ReadRawAsync(startBlock, lengthOfBlock);
			if (!readRaw.IsSuccess)
			{
				return readRaw;
			}
			value.CopyTo(readRaw.Content, addAnalysis.Content % BytesOfBlock);
			return await WriteRawAsync(startBlock, lengthOfBlock, readRaw.Content);
		}

		/// <inheritdoc />
		public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> ini = await ReadRFIDInfoAsync();
				if (!ini.IsSuccess)
				{
					return OperateResult.CreateFailedResult<bool[]>(ini);
				}
			}
			OperateResult<ushort> addAnalysis = ParseAddress(address, true);
			if (!addAnalysis.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(addAnalysis);
			}
			ushort byteStart = (ushort)(addAnalysis.Content / 8);
			ushort byteLength = (ushort)((addAnalysis.Content + length - 1) / 8 - byteStart + 1);
			CalculateBlockAddress(byteStart, byteLength, BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> read = await ReadRawAsync(startBlock, lengthOfBlock);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(read);
			}
			return OperateResult.CreateSuccessResult(read.Content.SelectMiddle(byteStart % BytesOfBlock, byteLength).ToBoolArray().SelectMiddle(addAnalysis.Content % 8, length));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, bool[] value)
		{
			if (!successfullyInitialized)
			{
				OperateResult<string> ini = await ReadRFIDInfoAsync();
				if (!ini.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(ini);
				}
			}
			OperateResult<ushort> addAnalysis = ParseAddress(address, true);
			if (!addAnalysis.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(addAnalysis);
			}
			ushort byteStart = (ushort)(addAnalysis.Content / 8);
			CalculateBlockAddress(byteStart, (ushort)((addAnalysis.Content + value.Length - 1) / 8 - byteStart + 1), BytesOfBlock, out var startBlock, out var lengthOfBlock);
			OperateResult<byte[]> readRaw = await ReadRawAsync(startBlock, lengthOfBlock);
			if (!readRaw.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(readRaw);
			}
			bool[] boolArray = readRaw.Content.ToBoolArray();
			value.CopyTo(boolArray, byteStart % BytesOfBlock * 8 + addAnalysis.Content % 8);
			return await WriteRawAsync(startBlock, lengthOfBlock, boolArray.ToByteArray());
		}

		private OperateResult<string> ExtraUID(byte[] content)
		{
			OperateResult<byte[]> operateResult = CheckResponseContent(content);
			if (operateResult.IsSuccess)
			{
				UID = content.SelectMiddle(5, 8).ToHexString();
				NumberOfBlock = content[15];
				BytesOfBlock = (byte)(content[16] + 1);
				successfullyInitialized = true;
				return OperateResult.CreateSuccessResult(UID);
			}
			successfullyInitialized = false;
			return OperateResult.CreateFailedResult<string>(operateResult);
		}

		/// <summary>
		/// 读取载码体信息，并将读取的信息进行初始化
		/// </summary>
		/// <returns>返回UID信息</returns>
		public OperateResult<string> ReadRFIDInfo()
		{
			return ReadFromCoreServer(PackReaderCommand(new byte[2] { 112, 0 })).Then<string>(ExtraUID);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Turck.ReaderNet.ReadRFIDInfo" />
		public async Task<OperateResult<string>> ReadRFIDInfoAsync()
		{
			return (await ReadFromCoreServerAsync(PackReaderCommand(new byte[2] { 112, 0 }))).Then<string>(ExtraUID);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"ReaderNet[{IpAddress}:{Port}]";
		}

		private static string GetErrorText(byte err1, byte err2, byte err3)
		{
			switch (err1)
			{
			case 1:
				return "Command not supported";
			case 2:
				return "Command not correctly detected, e.g. wrong format";
			case 3:
				return "Command option not supportde";
			case 15:
				return "Undefined/General error";
			case 16:
				return "Requested memory block not available";
			case 17:
				return "Requested memory block is already locked";
			case 18:
				return "Requested memory block is locked and cannot be written";
			case 19:
				return "Writing of requested memory block not successful";
			case 20:
				return "Requested memory block could not be locked";
			default:
				return "Customer specific error codes";
			case 0:
				switch (err2)
				{
				case 1:
					return "CRC_ERR, telegram fault in the tag-response";
				case 2:
					return "TimeOut_ERR, no tag-response in the given time";
				case 4:
					return "Tag_ERR, tag defect, e.g. multiple crc-faults on the air interface";
				case 8:
					return "CHAIN_ERR, Tag has left the air interface before executing all commands";
				case 16:
					return "UID_ERR, other UID as expected was detected during addressed mode";
				default:
					return StringResources.Language.UnknownError;
				case 0:
					switch (err3)
					{
					case 1:
						return "TRANS_ERR, transceiver defect, e.g. Flash-checksum";
					case 2:
						return "CMD_ERR, fault during execution of a command";
					case 4:
						return "syntax_ERR, telegram content not valid, e.g. requested tag-memory address not available";
					case 8:
						return "PS_ERR, power supply too low";
					case 16:
						return "CMD_UNKNOWN, unknown command code";
					default:
						return StringResources.Language.UnknownError;
					}
				}
			}
		}

		/// <summary>
		/// 将字符串的地址解析出实际的整数地址，如果是位地址，支持使用小数点的形式 例如100.1
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="isBit">是否位地址</param>
		/// <returns>整数地址信息</returns>
		public static OperateResult<ushort> ParseAddress(string address, bool isBit)
		{
			try
			{
				if (!isBit)
				{
					return OperateResult.CreateSuccessResult(ushort.Parse(address));
				}
				if (address.IndexOf('.') < 0)
				{
					return OperateResult.CreateSuccessResult(ushort.Parse(address));
				}
				string[] array = address.Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
				return OperateResult.CreateSuccessResult((ushort)(int.Parse(array[0]) * 8 + int.Parse(array[1])));
			}
			catch (Exception ex)
			{
				return new OperateResult<ushort>("Address input wrong, reason: " + ex.Message);
			}
		}

		/// <summary>
		/// 计算缓存数据里的CRC校验信息，并返回CRC计算的结果
		/// </summary>
		/// <param name="data">数据信息</param>
		/// <param name="len">计算的长度信息</param>
		/// <returns>CRC计算结果</returns>
		public static byte[] CalculateCRC(byte[] data, int len)
		{
			int num = 65535;
			int num2 = 33800;
			byte[] array = new byte[2];
			for (int i = 0; i < len; i++)
			{
				num ^= data[i];
				for (int j = 0; j < 8; j++)
				{
					num = (((num & 1) != 1) ? (num >> 1) : ((num >> 1) ^ num2));
				}
			}
			num = ~num;
			array[0] = Convert.ToByte(num & 0xFF);
			array[1] = Convert.ToByte((num >> 8) & 0xFF);
			return array;
		}

		/// <summary>
		/// 计算并填充CRC校验到原始数据中去
		/// </summary>
		/// <param name="data">原始的数据信息</param>
		/// <param name="len">计算的长度信息</param>
		public static void CalculateAndFillCRC(byte[] data, int len)
		{
			byte[] array = CalculateCRC(data, len);
			data[len] = array[0];
			data[len + 1] = array[1];
		}

		/// <summary>
		/// 校验当前数据的CRC校验是否正确
		/// </summary>
		/// <param name="data">原始数据信息</param>
		/// <param name="len">长度数据信息</param>
		/// <returns>校验结果</returns>
		public static bool CheckCRC(byte[] data, int len)
		{
			byte[] array = CalculateCRC(data, len);
			return data[len] == array[0] && data[len + 1] == array[1];
		}

		/// <summary>
		/// 将普通的命令打造成图尔克的reader协议完整命令
		/// </summary>
		/// <param name="command">命令信息</param>
		/// <returns>完整的命令包</returns>
		public static byte[] PackReaderCommand(byte[] command)
		{
			byte[] array = new byte[5 + command.Length];
			array[0] = 170;
			array[1] = (byte)array.Length;
			array[2] = (byte)array.Length;
			command.CopyTo(array, 3);
			CalculateAndFillCRC(array, 3 + command.Length);
			return array;
		}

		/// <summary>
		/// 构建读取的数据块的命令信息，一次最多读取64个字节
		/// </summary>
		/// <param name="startBlock">需要读取的起始 Block。从 0 开始。</param>
		/// <param name="numberBlock">需要读取的 Block 数量。 从 0 开始。</param>
		/// <param name="bytesOfBlock">每个数据块占用的字节数</param>
		/// <returns>完整的命令报文信息</returns>
		private static List<byte[]> BuildReadCommand(byte startBlock, byte numberBlock, byte bytesOfBlock)
		{
			int[] array = SoftBasic.SplitIntegerToArray(numberBlock, 64 / bytesOfBlock);
			List<byte[]> list = new List<byte[]>();
			for (int i = 0; i < array.Length; i++)
			{
				list.Add(PackReaderCommand(new byte[4]
				{
					104,
					0,
					startBlock,
					(byte)(array[i] - 1)
				}));
				startBlock += (byte)array[i];
			}
			return list;
		}

		/// <summary>
		/// 构建写入数据块的命令信息，一次最多写入64个字节
		/// </summary>
		/// <param name="startBlock">需要读取的起始 Block。从 0 开始。</param>
		/// <param name="numberBlock">需要读取的 Block 数量。 从 0 开始。</param>
		/// <param name="bytesOfBlock">每个数据块占用的字节数</param>
		/// <param name="value">写入的数据</param>
		/// <returns>完整的写入的命令报文信息</returns>
		private static List<byte[]> BuildWriteCommand(byte startBlock, byte numberBlock, byte bytesOfBlock, byte[] value)
		{
			if (value == null)
			{
				value = new byte[0];
			}
			int[] array = SoftBasic.SplitIntegerToArray(numberBlock, 64 / bytesOfBlock);
			List<byte[]> list = new List<byte[]>();
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				byte[] array2 = new byte[4 + array[i] * bytesOfBlock];
				array2[0] = 105;
				array2[1] = 0;
				array2[2] = startBlock;
				array2[3] = (byte)(array[i] - 1);
				value.SelectMiddle(num, array[i] * bytesOfBlock).CopyTo(array2, 4);
				startBlock += (byte)array[i];
				num += array[i] * bytesOfBlock;
				list.Add(PackReaderCommand(array2));
			}
			return list;
		}

		private static void CalculateBlockAddress(ushort address, ushort length, byte bytesOfBlock, out byte startBlock, out byte lengthOfBlock)
		{
			startBlock = (byte)(address / bytesOfBlock);
			lengthOfBlock = (byte)((byte)((address + length - 1) / bytesOfBlock) - startBlock + 1);
		}
	}
}
