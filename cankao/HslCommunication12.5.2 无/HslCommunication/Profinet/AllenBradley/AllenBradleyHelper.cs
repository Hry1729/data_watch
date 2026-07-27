using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Profinet.AllenBradley
{
	/// <summary>
	/// AB PLC的辅助类，用来辅助生成基本的指令信息
	/// </summary>
	public class AllenBradleyHelper
	{
		/// <summary>
		/// CIP命令中PCCC命令相关的信息
		/// </summary>
		public const byte CIP_Execute_PCCC = 75;

		/// <summary>
		/// CIP命令中的读取数据的服务
		/// </summary>
		public const byte CIP_READ_DATA = 76;

		/// <summary>
		/// CIP命令中的写数据的服务
		/// </summary>
		public const int CIP_WRITE_DATA = 77;

		/// <summary>
		/// CIP命令中的读并写的数据服务
		/// </summary>
		public const int CIP_READ_WRITE_DATA = 78;

		/// <summary>
		/// CIP命令中的读片段的数据服务
		/// </summary>
		public const int CIP_READ_FRAGMENT = 82;

		/// <summary>
		/// CIP命令中的写片段的数据服务
		/// </summary>
		public const int CIP_WRITE_FRAGMENT = 83;

		/// <summary>
		/// CIP指令中读取数据的列表
		/// </summary>
		public const byte CIP_READ_LIST = 85;

		/// <summary>
		/// CIP命令中的对数据读取服务
		/// </summary>
		public const int CIP_MULTIREAD_DATA = 4096;

		/// <summary>
		/// 日期的格式
		/// </summary>
		public const ushort CIP_Type_DATE = 8;

		/// <summary>
		/// 时间的格式
		/// </summary>
		public const ushort CIP_Type_TIME = 9;

		/// <summary>
		/// 日期时间格式，最完整的时间格式
		/// </summary>
		public const ushort CIP_Type_TimeAndDate = 10;

		/// <summary>
		/// 一天中的时间格式
		/// </summary>
		public const ushort CIP_Type_TimeOfDate = 11;

		/// <summary>
		/// bool型数据，一个字节长度
		/// </summary>
		public const ushort CIP_Type_Bool = 193;

		/// <summary>
		/// byte型数据，一个字节长度，SINT
		/// </summary>
		public const ushort CIP_Type_Byte = 194;

		/// <summary>
		/// 整型，两个字节长度，INT
		/// </summary>
		public const ushort CIP_Type_Word = 195;

		/// <summary>
		/// 长整型，四个字节长度，DINT
		/// </summary>
		public const ushort CIP_Type_DWord = 196;

		/// <summary>
		/// 特长整型，8个字节，LINT
		/// </summary>
		public const ushort CIP_Type_LInt = 197;

		/// <summary>
		/// Unsigned 8-bit integer, USINT
		/// </summary>
		public const ushort CIP_Type_USInt = 198;

		/// <summary>
		/// Unsigned 16-bit integer, UINT
		/// </summary>
		public const ushort CIP_Type_UInt = 199;

		/// <summary>
		///  Unsigned 32-bit integer, UDINT
		/// </summary>
		public const ushort CIP_Type_UDint = 200;

		/// <summary>
		///  Unsigned 64-bit integer, ULINT
		/// </summary>
		public const ushort CIP_Type_ULint = 201;

		/// <summary>
		/// 实数数据，四个字节长度
		/// </summary>
		public const ushort CIP_Type_Real = 202;

		/// <summary>
		/// 实数数据，八个字节的长度
		/// </summary>
		public const ushort CIP_Type_Double = 203;

		/// <summary>
		/// 结构体数据，不定长度
		/// </summary>
		public const ushort CIP_Type_Struct = 204;

		/// <summary>
		/// 字符串数据内容
		/// </summary>
		public const ushort CIP_Type_String = 208;

		/// <summary>
		///  Bit string, 8 bits, BYTE,
		/// </summary>
		public const ushort CIP_Type_D1 = 209;

		/// <summary>
		/// Bit string, 16-bits, WORD
		/// </summary>
		public const ushort CIP_Type_D2 = 210;

		/// <summary>
		/// Bit string, 32 bits, DWORD
		/// </summary>
		public const ushort CIP_Type_D3 = 211;

		/// <summary>
		/// Bit string, 64 bits LWORD
		/// </summary>
		public const ushort CIP_Type_D4 = 212;

		/// <summary>
		/// 二进制数据内容
		/// </summary>
		public const ushort CIP_Type_BitArray = 211;

		/// <summary>
		/// 连接方的厂商标识
		/// </summary>
		public const ushort OriginatorVendorID = 4105;

		/// <summary>
		/// 连接方的序列号
		/// </summary>
		public const uint OriginatorSerialNumber = 3248834059u;

		private static byte[] BuildRequestPathCommand(string address, bool isConnectedAddress = false)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				int num = HslHelper.ExtractParameter(ref address, "class", -1);
				if (num != -1)
				{
					int num2 = (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(address.Substring(2), 16) : Convert.ToInt32(address));
					if (num < 256)
					{
						memoryStream.WriteByte(32);
						memoryStream.WriteByte((byte)num);
					}
					else
					{
						memoryStream.WriteByte(33);
						memoryStream.WriteByte(0);
						memoryStream.WriteByte(BitConverter.GetBytes(num)[0]);
						memoryStream.WriteByte(BitConverter.GetBytes(num)[1]);
					}
					if (num2 < 256)
					{
						memoryStream.WriteByte(36);
						memoryStream.WriteByte((byte)num2);
					}
					else
					{
						memoryStream.WriteByte(37);
						memoryStream.WriteByte(0);
						memoryStream.WriteByte(BitConverter.GetBytes(num2)[0]);
						memoryStream.WriteByte(BitConverter.GetBytes(num2)[1]);
					}
				}
				else
				{
					string[] array = address.Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
					for (int i = 0; i < array.Length; i++)
					{
						string text = string.Empty;
						int num3 = array[i].IndexOf('[');
						int num4 = array[i].LastIndexOf(']');
						if (num3 > 0 && num4 > 0 && num4 > num3)
						{
							text = array[i].Substring(num3 + 1, num4 - num3 - 1);
							array[i] = array[i].Substring(0, num3);
						}
						memoryStream.WriteByte(145);
						byte[] bytes = Encoding.UTF8.GetBytes(array[i]);
						memoryStream.WriteByte((byte)bytes.Length);
						memoryStream.Write(bytes, 0, bytes.Length);
						if (bytes.Length % 2 == 1)
						{
							memoryStream.WriteByte(0);
						}
						if (string.IsNullOrEmpty(text))
						{
							continue;
						}
						MatchCollection matchCollection = Regex.Matches(text, "[0-9]+");
						for (int j = 0; j < matchCollection.Count; j++)
						{
							int num5 = Convert.ToInt32(matchCollection[j].Value);
							if (num5 < 256 && !isConnectedAddress)
							{
								memoryStream.WriteByte(40);
								memoryStream.WriteByte((byte)num5);
							}
							else if (num5 < 65536)
							{
								memoryStream.WriteByte(41);
								memoryStream.WriteByte(0);
								memoryStream.WriteByte(BitConverter.GetBytes(num5)[0]);
								memoryStream.WriteByte(BitConverter.GetBytes(num5)[1]);
							}
							else
							{
								memoryStream.WriteByte(42);
								memoryStream.WriteByte(0);
								memoryStream.Write(BitConverter.GetBytes(num5));
							}
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		/// <summary>
		/// 从生成的报文里面反解出实际的数据地址，不支持结构体嵌套，仅支持数据，一维数组，不支持多维数据
		/// </summary>
		/// <param name="pathCommand">地址路径报文</param>
		/// <returns>实际的地址</returns>
		public static string ParseRequestPathCommand(byte[] pathCommand)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < pathCommand.Length; i++)
			{
				if (pathCommand[i] != 145)
				{
					continue;
				}
				string text = Encoding.UTF8.GetString(pathCommand, i + 2, pathCommand[i + 1]).TrimEnd(default(char));
				stringBuilder.Append(text);
				int num = 2 + text.Length;
				if (text.Length % 2 == 1)
				{
					num++;
				}
				if (pathCommand.Length > num + i)
				{
					if (pathCommand[i + num] == 40)
					{
						stringBuilder.Append($"[{pathCommand[i + num + 1]}]");
					}
					else if (pathCommand[i + num] == 41)
					{
						stringBuilder.Append($"[{BitConverter.ToUInt16(pathCommand, i + num + 2)}]");
					}
				}
				stringBuilder.Append(".");
			}
			if (stringBuilder[stringBuilder.Length - 1] == '.')
			{
				stringBuilder.Remove(stringBuilder.Length - 1, 1);
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// 从生成的报文里面，反解出实际的 Symbol Class ID，以及  Instance ID 信息
		/// </summary>
		/// <param name="pathCommand"></param>
		/// <returns></returns>
		public static OperateResult<int, int> ParseRequestPathSymbolInstanceAddressing(byte[] pathCommand)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			if (pathCommand[num3] == 32)
			{
				num3++;
				num = pathCommand[num3++];
			}
			else
			{
				if (pathCommand[num3] != 33)
				{
					return new OperateResult<int, int>();
				}
				num3 += 2;
				num = BitConverter.ToUInt16(pathCommand, num3);
				num3 += 2;
			}
			if (pathCommand[num3] == 36)
			{
				num3++;
				num2 = pathCommand[num3++];
			}
			else
			{
				if (pathCommand[num3] != 37)
				{
					return new OperateResult<int, int>();
				}
				num3 += 2;
				num2 = BitConverter.ToUInt16(pathCommand, num3);
				num3 += 2;
			}
			return OperateResult.CreateSuccessResult(num, num2);
		}

		/// <summary>
		/// 获取枚举PLC数据信息的指令
		/// </summary>
		/// <param name="startInstance">实例的起始地址</param>
		/// <returns>结果数据</returns>
		public static byte[] BuildEnumeratorCommand(uint startInstance)
		{
			return new byte[16]
			{
				85,
				3,
				32,
				107,
				37,
				0,
				BitConverter.GetBytes(startInstance)[0],
				BitConverter.GetBytes(startInstance)[1],
				3,
				0,
				1,
				0,
				2,
				0,
				8,
				0
			};
		}

		/// <summary>
		/// 获取枚举PLC的局部变量的数据信息的指令
		/// </summary>
		/// <param name="startInstance">实例的起始地址</param>
		/// <returns>结果命令数据</returns>
		public static byte[] BuildEnumeratorProgrameMainCommand(uint startInstance)
		{
			byte[] array = new byte[38];
			array[0] = 85;
			array[1] = 14;
			array[2] = 145;
			array[3] = 19;
			Encoding.ASCII.GetBytes("Program:MainProgram").CopyTo(array, 4);
			array[23] = 0;
			array[24] = 32;
			array[25] = 107;
			array[26] = 37;
			array[27] = 0;
			array[28] = BitConverter.GetBytes(startInstance)[0];
			array[29] = BitConverter.GetBytes(startInstance)[1];
			array[30] = 3;
			array[31] = 0;
			array[32] = 1;
			array[33] = 0;
			array[34] = 2;
			array[35] = 0;
			array[36] = 8;
			array[37] = 0;
			return array;
		}

		/// <summary>
		/// 获取获得结构体句柄的命令
		/// </summary>
		/// <param name="symbolType">包含地址的信息</param>
		/// <returns>命令数据</returns>
		public static byte[] GetStructHandleCommand(ushort symbolType)
		{
			byte[] array = new byte[18];
			symbolType &= 0xFFF;
			array[0] = 3;
			array[1] = 3;
			array[2] = 32;
			array[3] = 108;
			array[4] = 37;
			array[5] = 0;
			array[6] = BitConverter.GetBytes(symbolType)[0];
			array[7] = BitConverter.GetBytes(symbolType)[1];
			array[8] = 4;
			array[9] = 0;
			array[10] = 4;
			array[11] = 0;
			array[12] = 5;
			array[13] = 0;
			array[14] = 2;
			array[15] = 0;
			array[16] = 1;
			array[17] = 0;
			return array;
		}

		/// <summary>
		/// 获取结构体内部数据结构的方法
		/// </summary>
		/// <param name="symbolType">地址</param>
		/// <param name="structHandle">句柄</param>
		/// <param name="offset">偏移量地址</param>
		/// <returns>指令</returns>
		public static byte[] GetStructItemNameType(ushort symbolType, AbStructHandle structHandle, int offset)
		{
			byte[] array = new byte[14];
			symbolType &= 0xFFF;
			byte[] bytes = BitConverter.GetBytes(structHandle.TemplateObjectDefinitionSize * 4 - 21);
			array[0] = 76;
			array[1] = 3;
			array[2] = 32;
			array[3] = 108;
			array[4] = 37;
			array[5] = 0;
			array[6] = BitConverter.GetBytes(symbolType)[0];
			array[7] = BitConverter.GetBytes(symbolType)[1];
			array[8] = BitConverter.GetBytes(offset)[0];
			array[9] = BitConverter.GetBytes(offset)[1];
			array[10] = BitConverter.GetBytes(offset)[2];
			array[11] = BitConverter.GetBytes(offset)[3];
			array[12] = bytes[0];
			array[13] = bytes[1];
			return array;
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.PackRequestHeader(System.UInt16,System.UInt32,System.UInt32,System.Byte[],System.Byte[])" />
		public static byte[] PackRequestHeader(ushort command, uint session, byte[] commandSpecificData, byte[] senderContext = null)
		{
			if (commandSpecificData == null)
			{
				commandSpecificData = new byte[0];
			}
			byte[] array = new byte[commandSpecificData.Length + 24];
			Array.Copy(commandSpecificData, 0, array, 24, commandSpecificData.Length);
			BitConverter.GetBytes(command).CopyTo(array, 0);
			BitConverter.GetBytes(session).CopyTo(array, 4);
			if (senderContext != null && senderContext.Length <= 8)
			{
				senderContext.CopyTo(array, 12);
			}
			BitConverter.GetBytes((ushort)commandSpecificData.Length).CopyTo(array, 2);
			return array;
		}

		/// <summary>
		/// 将CommandSpecificData的命令，打包成可发送的数据指令
		/// </summary>
		/// <param name="command">实际的命令暗号</param>
		/// <param name="error">错误号信息</param>
		/// <param name="session">当前会话的id</param>
		/// <param name="commandSpecificData">CommandSpecificData命令</param>
		/// <param name="senderContext">发送的上下文内容</param>
		/// <returns>最终可发送的数据命令</returns>
		public static byte[] PackRequestHeader(ushort command, uint error, uint session, byte[] commandSpecificData, byte[] senderContext = null)
		{
			byte[] array = PackRequestHeader(command, session, commandSpecificData, senderContext);
			BitConverter.GetBytes(error).CopyTo(array, 8);
			return array;
		}

		private static byte[] PackExecutePCCC(byte[] pccc)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(75);
			memoryStream.WriteByte(2);
			memoryStream.WriteByte(32);
			memoryStream.WriteByte(103);
			memoryStream.WriteByte(36);
			memoryStream.WriteByte(1);
			memoryStream.WriteByte(7);
			memoryStream.WriteByte(9);
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(11);
			memoryStream.WriteByte(70);
			memoryStream.WriteByte(165);
			memoryStream.WriteByte(193);
			memoryStream.Write(pccc);
			byte[] array = memoryStream.ToArray();
			BitConverter.GetBytes((ushort)4105).CopyTo(array, 7);
			BitConverter.GetBytes(3248834059u).CopyTo(array, 9);
			return array;
		}

		/// <summary>
		/// 打包一个PCCC的读取的命令报文
		/// </summary>
		/// <param name="tns">请求序号信息</param>
		/// <param name="address">请求的文件地址，地址示例：N7:1</param>
		/// <param name="length">请求的字节长度</param>
		/// <returns>PCCC的读取报文信息</returns>
		public static OperateResult<byte[]> PackExecutePCCCRead(int tns, string address, int length)
		{
			OperateResult<byte[]> operateResult = AllenBradleyDF1Serial.BuildProtectedTypedLogicalReadWithThreeAddressFields(tns, address, length);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return OperateResult.CreateSuccessResult(PackExecutePCCC(operateResult.Content));
		}

		/// <summary>
		/// 打包一个PCCC的写入的命令报文
		/// </summary>
		/// <param name="tns">请求序号信息</param>
		/// <param name="address">请求的文件地址，地址示例：N7:1</param>
		/// <param name="value">写入的原始数据信息</param>
		/// <returns>PCCC的写入报文信息</returns>
		public static OperateResult<byte[]> PackExecutePCCCWrite(int tns, string address, byte[] value)
		{
			OperateResult<byte[]> operateResult = AllenBradleyDF1Serial.BuildProtectedTypedLogicalWriteWithThreeAddressFields(tns, address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return OperateResult.CreateSuccessResult(PackExecutePCCC(operateResult.Content));
		}

		internal static OperateResult<byte[]> PackExecutePCCCWrite(int tns, string address, int bitIndex, bool value)
		{
			OperateResult<byte[]> operateResult = AllenBradleyDF1Serial.BuildProtectedTypedLogicalMaskWithThreeAddressFields(tns, address, bitIndex, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return OperateResult.CreateSuccessResult(PackExecutePCCC(operateResult.Content));
		}

		/// <summary>
		/// 打包生成一个请求读取数据的节点信息，CIP指令信息
		/// </summary>
		/// <param name="address">地址</param>
		/// <param name="length">指代数组的长度</param>
		/// <param name="isConnectedAddress">是否是连接模式下的地址，默认为false</param>
		/// <returns>CIP的指令信息</returns>
		public static byte[] PackRequsetRead(string address, int length, bool isConnectedAddress = false)
		{
			byte[] array = new byte[1024];
			int num = 0;
			array[num++] = 76;
			num++;
			byte[] array2 = BuildRequestPathCommand(address, isConnectedAddress);
			array2.CopyTo(array, num);
			num += array2.Length;
			array[1] = (byte)((num - 2) / 2);
			array[num++] = BitConverter.GetBytes(length)[0];
			array[num++] = BitConverter.GetBytes(length)[1];
			byte[] array3 = new byte[num];
			Array.Copy(array, 0, array3, 0, num);
			return array3;
		}

		/// <summary>
		/// 打包生成一个请求读取数据片段的节点信息，CIP指令信息
		/// </summary>
		/// <param name="address">节点的名称 -&gt; Tag Name</param>
		/// <param name="startIndex">起始的索引位置，以字节为单位 -&gt; The initial index position, in bytes</param>
		/// <param name="length">读取的数据长度，一次通讯总计490个字节 -&gt; Length of read data, a total of 490 bytes of communication</param>
		/// <returns>CIP的指令信息</returns>
		public static byte[] PackRequestReadSegment(string address, int startIndex, int length)
		{
			byte[] array = new byte[1024];
			int num = 0;
			array[num++] = 82;
			num++;
			byte[] array2 = BuildRequestPathCommand(address);
			array2.CopyTo(array, num);
			num += array2.Length;
			array[1] = (byte)((num - 2) / 2);
			array[num++] = BitConverter.GetBytes(length)[0];
			array[num++] = BitConverter.GetBytes(length)[1];
			array[num++] = BitConverter.GetBytes(startIndex)[0];
			array[num++] = BitConverter.GetBytes(startIndex)[1];
			array[num++] = BitConverter.GetBytes(startIndex)[2];
			array[num++] = BitConverter.GetBytes(startIndex)[3];
			byte[] array3 = new byte[num];
			Array.Copy(array, 0, array3, 0, num);
			return array3;
		}

		/// <summary>
		/// 根据指定的数据和类型，生成对应的数据
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="typeCode">数据类型</param>
		/// <param name="value">字节值</param>
		/// <param name="length">如果节点为数组，就是数组长度</param>
		/// <param name="isConnectedAddress">是否为连接模式的地址</param>
		/// <param name="paddingTail">是否需要在尾巴上补上一个尾巴字节，达到对齐的效果</param>
		/// <returns>CIP的指令信息</returns>
		public static byte[] PackRequestWrite(string address, ushort typeCode, byte[] value, int length = 1, bool isConnectedAddress = false, bool paddingTail = false)
		{
			byte[] array = new byte[1024];
			int num = 0;
			array[num++] = 77;
			num++;
			byte[] array2 = BuildRequestPathCommand(address, isConnectedAddress);
			array2.CopyTo(array, num);
			num += array2.Length;
			array[1] = (byte)((num - 2) / 2);
			array[num++] = BitConverter.GetBytes(typeCode)[0];
			array[num++] = BitConverter.GetBytes(typeCode)[1];
			array[num++] = BitConverter.GetBytes(length)[0];
			array[num++] = BitConverter.GetBytes(length)[1];
			if (value == null)
			{
				value = new byte[0];
			}
			int num2 = 0;
			if (paddingTail && typeCode == 193 && length == 1 && value.Length % 2 > 0)
			{
				num2 = 1;
			}
			byte[] array3 = new byte[value.Length + num + num2];
			Array.Copy(array, 0, array3, 0, num);
			value.CopyTo(array3, num);
			return array3;
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.PackRequestWrite(System.String,System.UInt16,System.Byte[],System.Int32,System.Boolean,System.Boolean)" />
		public static byte[] PackRequestWriteSegment(string address, ushort typeCode, byte[] value, int startIndex, int length = 1, bool isConnectedAddress = false)
		{
			byte[] array = new byte[1024];
			int num = 0;
			array[num++] = 83;
			num++;
			byte[] array2 = BuildRequestPathCommand(address, isConnectedAddress);
			array2.CopyTo(array, num);
			num += array2.Length;
			array[1] = (byte)((num - 2) / 2);
			array[num++] = BitConverter.GetBytes(typeCode)[0];
			array[num++] = BitConverter.GetBytes(typeCode)[1];
			array[num++] = BitConverter.GetBytes(length)[0];
			array[num++] = BitConverter.GetBytes(length)[1];
			array[num++] = BitConverter.GetBytes(startIndex)[0];
			array[num++] = BitConverter.GetBytes(startIndex)[1];
			array[num++] = BitConverter.GetBytes(startIndex)[2];
			array[num++] = BitConverter.GetBytes(startIndex)[3];
			if (value == null)
			{
				value = new byte[0];
			}
			byte[] array3 = new byte[value.Length + num];
			Array.Copy(array, 0, array3, 0, num);
			value.CopyTo(array3, num);
			return array3;
		}

		/// <summary>
		/// 根据传入的地址，或掩码，和掩码来创建一个读-修改-写的请求报文信息
		/// </summary>
		/// <param name="address">标签的地址信息</param>
		/// <param name="orMask">或的掩码</param>
		/// <param name="andMask">和的掩码</param>
		/// <param name="isConnectedAddress">是否为连接模式的地址</param>
		/// <returns>打包之后的CIP指令信息</returns>
		private static byte[] PackRequestReadModifyWrite(string address, byte[] orMask, byte[] andMask, bool isConnectedAddress = false)
		{
			byte[] array = new byte[1024];
			int num = 0;
			array[num++] = 78;
			num++;
			byte[] array2 = BuildRequestPathCommand(address, isConnectedAddress);
			array2.CopyTo(array, num);
			num += array2.Length;
			array[1] = (byte)((num - 2) / 2);
			array[num++] = (byte)orMask.Length;
			array[num++] = 0;
			orMask.CopyTo(array, num);
			num += orMask.Length;
			andMask.CopyTo(array, num);
			return array.SelectBegin(num + andMask.Length);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.PackRequestReadModifyWrite(System.String,System.Byte[],System.Byte[],System.Boolean)" />
		public static byte[] PackRequestReadModifyWrite(string address, uint orMask, uint andMask, bool isConnectedAddress = false)
		{
			return PackRequestReadModifyWrite(address, BitConverter.GetBytes(orMask), BitConverter.GetBytes(andMask), isConnectedAddress);
		}

		public static byte[] PackRequestReadModifyWrite(string address, int index, bool value, int dataBits, bool isConnectedAddress = false)
		{
			if (dataBits != 8 && dataBits != 16 && dataBits != 32 && dataBits != 64)
			{
				throw new ArgumentException("dataBits only support 8,16,32,64, but now: " + dataBits);
			}
			if (index / dataBits != 0)
			{
				address += $"[{index / dataBits}]";
			}
			index %= dataBits;
			if (value)
			{
				switch (dataBits)
				{
				case 64:
					return PackRequestReadModifyWrite(address, BitConverter.GetBytes((ulong)(1L << index)), new byte[8] { 255, 255, 255, 255, 255, 255, 255, 255 }, isConnectedAddress);
				case 32:
					return PackRequestReadModifyWrite(address, BitConverter.GetBytes((uint)(1 << index)), new byte[4] { 255, 255, 255, 255 }, isConnectedAddress);
				case 16:
					return PackRequestReadModifyWrite(address, BitConverter.GetBytes((ushort)(1 << index)), new byte[2] { 255, 255 }, isConnectedAddress);
				default:
				{
					byte b = (byte)(1 << index);
					return PackRequestReadModifyWrite(address, new byte[1] { b }, new byte[1] { 255 }, isConnectedAddress);
				}
				}
			}
			switch (dataBits)
			{
			case 64:
				return PackRequestReadModifyWrite(address, new byte[8], BitConverter.GetBytes((ulong)(~(1L << index))), isConnectedAddress);
			case 32:
				return PackRequestReadModifyWrite(address, new byte[4], BitConverter.GetBytes((uint)(~(1 << index))), isConnectedAddress);
			case 16:
				return PackRequestReadModifyWrite(address, new byte[2], BitConverter.GetBytes((ushort)(~(ushort)(1 << index))), isConnectedAddress);
			default:
			{
				byte b2 = (byte)(~(byte)(1 << index));
				return PackRequestReadModifyWrite(address, new byte[1], new byte[1] { b2 }, isConnectedAddress);
			}
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.PackRequestReadModifyWrite(System.String,System.UInt32,System.UInt32,System.Boolean)" />
		public static byte[] PackRequestReadModifyWrite(string address, int index, bool value, bool isConnectedAddress = false)
		{
			if (index / 32 != 0)
			{
				address += $"[{index / 32}]";
			}
			index %= 32;
			if (value)
			{
				return PackRequestReadModifyWrite(address, (uint)(1 << index), uint.MaxValue, isConnectedAddress);
			}
			return PackRequestReadModifyWrite(address, 0u, (uint)(~(1 << index)), isConnectedAddress);
		}

		/// <summary>
		/// 分析地址数据信息里的位索引的信息，例如a[10]  返回 a 和 10 索引，如果没有指定索引，就默认为0
		/// </summary>
		/// <param name="address">数据地址</param>
		/// <param name="arrayIndex">位索引</param>
		/// <returns>地址信息</returns>
		public static string AnalysisArrayIndex(string address, out int arrayIndex)
		{
			arrayIndex = 0;
			if (!address.EndsWith("]"))
			{
				Match match = Regex.Match(address, "\\.[0-9]+$");
				if (match.Success)
				{
					arrayIndex = int.Parse(match.Value.Substring(1));
					address = address.Substring(0, address.Length - match.Value.Length);
				}
				return address;
			}
			int num = address.LastIndexOf('[');
			if (num < 0)
			{
				return address;
			}
			address = address.Remove(address.Length - 1);
			try
			{
				arrayIndex = int.Parse(address.Substring(num + 1));
				address = address.Substring(0, num);
				return address;
			}
			catch
			{
				return address;
			}
		}

		/// <summary>
		/// 写入Bool数据的基本指令信息
		/// </summary>
		/// <param name="address">地址</param>
		/// <param name="value">值</param>
		/// <returns>报文信息</returns>
		public static byte[] PackRequestWrite(string address, bool value)
		{
			address = AnalysisArrayIndex(address, out var arrayIndex);
			return PackRequestReadModifyWrite(address, arrayIndex, value);
		}

		/// <summary>
		/// 将所有的cip指定进行打包操作。
		/// </summary>
		/// <param name="portSlot">PLC所在的面板槽号</param>
		/// <param name="cips">所有的cip打包指令信息</param>
		/// <returns>包含服务的信息</returns>
		public static byte[] PackCommandService(byte[] portSlot, params byte[][] cips)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(178);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(82);
			memoryStream.WriteByte(2);
			memoryStream.WriteByte(32);
			memoryStream.WriteByte(6);
			memoryStream.WriteByte(36);
			memoryStream.WriteByte(1);
			memoryStream.WriteByte(10);
			memoryStream.WriteByte(240);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			int num = 0;
			if (cips.Length == 1)
			{
				memoryStream.Write(cips[0], 0, cips[0].Length);
				num += cips[0].Length;
				if (cips[0].Length % 2 == 1)
				{
					memoryStream.WriteByte(0);
				}
			}
			else
			{
				memoryStream.WriteByte(10);
				memoryStream.WriteByte(2);
				memoryStream.WriteByte(32);
				memoryStream.WriteByte(2);
				memoryStream.WriteByte(36);
				memoryStream.WriteByte(1);
				num += 8;
				memoryStream.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2);
				ushort num2 = (ushort)(2 + 2 * cips.Length);
				num += 2 * cips.Length;
				for (int i = 0; i < cips.Length; i++)
				{
					memoryStream.Write(BitConverter.GetBytes(num2), 0, 2);
					num2 = (ushort)(num2 + cips[i].Length);
				}
				for (int j = 0; j < cips.Length; j++)
				{
					memoryStream.Write(cips[j], 0, cips[j].Length);
					num += cips[j].Length;
				}
			}
			if (portSlot != null)
			{
				memoryStream.WriteByte((byte)((portSlot.Length + 1) / 2));
				memoryStream.WriteByte(0);
				memoryStream.Write(portSlot, 0, portSlot.Length);
				if (portSlot.Length % 2 == 1)
				{
					memoryStream.WriteByte(0);
				}
			}
			byte[] array = memoryStream.ToArray();
			BitConverter.GetBytes((short)num).CopyTo(array, 12);
			BitConverter.GetBytes((short)(array.Length - 4)).CopyTo(array, 2);
			return array;
		}

		/// <summary>
		/// 将所有的cip指定进行打包操作。
		/// </summary>
		/// <param name="portSlot">PLC所在的面板槽号</param>
		/// <param name="cips">所有的cip打包指令信息</param>
		/// <returns>包含服务的信息</returns>
		public static byte[] PackCleanCommandService(byte[] portSlot, params byte[][] cips)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(178);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			if (cips.Length == 1)
			{
				memoryStream.Write(cips[0], 0, cips[0].Length);
			}
			else
			{
				memoryStream.WriteByte(10);
				memoryStream.WriteByte(2);
				memoryStream.WriteByte(32);
				memoryStream.WriteByte(2);
				memoryStream.WriteByte(36);
				memoryStream.WriteByte(1);
				memoryStream.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2);
				ushort num = (ushort)(2 + 2 * cips.Length);
				for (int i = 0; i < cips.Length; i++)
				{
					memoryStream.Write(BitConverter.GetBytes(num), 0, 2);
					num = (ushort)(num + cips[i].Length);
				}
				for (int j = 0; j < cips.Length; j++)
				{
					memoryStream.Write(cips[j], 0, cips[j].Length);
				}
			}
			memoryStream.WriteByte((byte)((portSlot.Length + 1) / 2));
			memoryStream.WriteByte(0);
			memoryStream.Write(portSlot, 0, portSlot.Length);
			if (portSlot.Length % 2 == 1)
			{
				memoryStream.WriteByte(0);
			}
			byte[] array = memoryStream.ToArray();
			BitConverter.GetBytes((short)(array.Length - 4)).CopyTo(array, 2);
			return array;
		}

		/// <summary>
		/// 对于CIP的服务器返回多个标签读取的报文进行打包
		/// </summary>
		/// <param name="cips">多个读取结果</param>
		/// <returns>打包后的结束</returns>
		public static byte[] PackServerReturnMultiTags(List<byte[]> cips)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(178);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(138);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.Write(BitConverter.GetBytes((ushort)cips.Count), 0, 2);
			ushort num = (ushort)(2 + 2 * cips.Count);
			for (int i = 0; i < cips.Count; i++)
			{
				memoryStream.Write(BitConverter.GetBytes(num), 0, 2);
				num = (ushort)(num + cips[i].Length);
			}
			for (int j = 0; j < cips.Count; j++)
			{
				memoryStream.Write(cips[j], 0, cips[j].Length);
			}
			byte[] array = memoryStream.ToArray();
			BitConverter.GetBytes((short)(array.Length - 4)).CopyTo(array, 2);
			return array;
		}

		/// <summary>
		/// 打包一个读取所有特性数据的报文信息，需要传入slot
		/// </summary>
		/// <param name="portSlot">站号信息</param>
		/// <param name="sessionHandle">会话的ID信息</param>
		/// <returns>最终发送的报文数据</returns>
		public static byte[] PackCommandGetAttributesAll(byte[] portSlot, uint sessionHandle)
		{
			return PackRequestHeader(111, sessionHandle, PackCommandSpecificData(new byte[4], PackCommandService(portSlot, new byte[6] { 1, 2, 32, 1, 36, 1 })));
		}

		/// <summary>
		/// 根据数据创建反馈的数据信息
		/// </summary>
		/// <param name="data">反馈的数据信息</param>
		/// <param name="isRead">是否是读取</param>
		/// <returns>数据</returns>
		public static byte[] PackCommandResponse(byte[] data, bool isRead)
		{
			if (data == null)
			{
				return new byte[6] { 0, 0, 4, 0, 0, 0 };
			}
			return SoftBasic.SpliceArray<byte>(new byte[6]
			{
				(byte)(isRead ? 204u : 205u),
				0,
				0,
				0,
				0,
				0
			}, data);
		}

		/// <summary>
		/// 生成读取直接节点数据信息的内容
		/// </summary>
		/// <param name="service">cip指令内容</param>
		/// <returns>最终的指令值</returns>
		public static byte[] PackCommandSpecificData(params byte[][] service)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(10);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(BitConverter.GetBytes(service.Length)[0]);
			memoryStream.WriteByte(BitConverter.GetBytes(service.Length)[1]);
			for (int i = 0; i < service.Length; i++)
			{
				memoryStream.Write(service[i], 0, service[i].Length);
			}
			return memoryStream.ToArray();
		}

		/// <summary>
		/// 将所有的cip指定进行打包操作。
		/// </summary>
		/// <param name="command">指令信息</param>
		/// <param name="code">服务器的代号信息</param>
		/// <param name="isConnected">是否基于连接的服务</param>
		/// <param name="sequence">当使用了基于连接的服务时，当前CIP消息号信息</param>
		/// <returns>包含服务的信息</returns>
		public static byte[] PackCommandSingleService(byte[] command, ushort code = 178, bool isConnected = false, ushort sequence = 0)
		{
			if (command == null)
			{
				command = new byte[0];
			}
			byte[] array = (isConnected ? new byte[6 + command.Length] : new byte[4 + command.Length]);
			array[0] = BitConverter.GetBytes(code)[0];
			array[1] = BitConverter.GetBytes(code)[1];
			array[2] = BitConverter.GetBytes(array.Length - 4)[0];
			array[3] = BitConverter.GetBytes(array.Length - 4)[1];
			command.CopyTo(array, isConnected ? 6 : 4);
			if (isConnected)
			{
				BitConverter.GetBytes(sequence).CopyTo(array, 4);
			}
			return array;
		}

		/// <summary>
		/// 向PLC注册会话ID的报文<br />
		/// Register a message with the PLC for the session ID
		/// </summary>
		/// <param name="senderContext">发送的上下文信息</param>
		/// <returns>报文信息 -&gt; Message information </returns>
		public static byte[] RegisterSessionHandle(byte[] senderContext = null)
		{
			return PackRequestHeader(101, 0u, new byte[4] { 1, 0, 0, 0 }, senderContext);
		}

		/// <summary>
		/// 获取卸载一个已注册的会话的报文<br />
		/// Get a message to uninstall a registered session
		/// </summary>
		/// <param name="sessionHandle">当前会话的ID信息</param>
		/// <returns>字节报文信息 -&gt; BYTE message information </returns>
		public static byte[] UnRegisterSessionHandle(uint sessionHandle)
		{
			return PackRequestHeader(102, sessionHandle, new byte[0]);
		}

		/// <summary>
		/// 初步检查返回的CIP协议的报文是否正确<br />
		/// Initially check whether the returned CIP protocol message is correct
		/// </summary>
		/// <param name="response">CIP的报文信息</param>
		/// <returns>是否正确的结果信息</returns>
		public static OperateResult CheckResponse(byte[] response)
		{
			try
			{
				int num = BitConverter.ToInt32(response, 8);
				if (num == 0)
				{
					return OperateResult.CreateSuccessResult();
				}
				string empty = string.Empty;
				switch (num)
				{
				case 1:
					empty = StringResources.Language.AllenBradleySessionStatus01;
					break;
				case 2:
					empty = StringResources.Language.AllenBradleySessionStatus02;
					break;
				case 3:
					empty = StringResources.Language.AllenBradleySessionStatus03;
					break;
				case 100:
					empty = StringResources.Language.AllenBradleySessionStatus64;
					break;
				case 101:
					empty = StringResources.Language.AllenBradleySessionStatus65;
					break;
				case 105:
					empty = StringResources.Language.AllenBradleySessionStatus69;
					break;
				default:
					empty = StringResources.Language.UnknownError;
					break;
				}
				return new OperateResult(num, empty);
			}
			catch (Exception ex)
			{
				return new OperateResult("CheckResponse failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
			}
		}

		/// <summary>
		/// 从PLC反馈的数据解析，返回解析后的数据内容，数据类型（在多项数据返回中无效），以及是否有更多的数据
		/// </summary>
		/// <param name="response">PLC的反馈数据</param>
		/// <param name="isRead">是否是返回的操作</param>
		/// <returns>带有结果标识的最终数据</returns>
		public static OperateResult<List<AbTagValue>, bool> ExtractActualDataList(byte[] response, bool isRead)
		{
			List<AbTagValue> list = new List<AbTagValue>();
			try
			{
				int num = 38;
				bool value = false;
				ushort num2 = BitConverter.ToUInt16(response, 38);
				if (BitConverter.ToInt32(response, 40) == 138)
				{
					num = 44;
					int num3 = BitConverter.ToUInt16(response, num);
					for (int i = 0; i < num3; i++)
					{
						int num4 = BitConverter.ToUInt16(response, num + 2 + i * 2) + num;
						int num5 = ((i == num3 - 1) ? response.Length : (BitConverter.ToUInt16(response, num + 4 + i * 2) + num));
						ushort num6 = BitConverter.ToUInt16(response, num4 + 2);
						switch (num6)
						{
						case 4:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley04
							};
						case 5:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley05
							};
						case 6:
							if (response[num + 2] == 210 || response[num + 2] == 204)
							{
								return new OperateResult<List<AbTagValue>, bool>
								{
									ErrorCode = num6,
									Message = StringResources.Language.AllenBradley06
								};
							}
							break;
						case 10:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley0A
							};
						case 12:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley0C
							};
						case 19:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley13
							};
						case 28:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley1C
							};
						case 30:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley1E
							};
						case 38:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.AllenBradley26
							};
						default:
							return new OperateResult<List<AbTagValue>, bool>
							{
								ErrorCode = num6,
								Message = StringResources.Language.UnknownError
							};
						case 0:
							break;
						}
						if (isRead)
						{
							AbTagValue abTagValue = new AbTagValue();
							abTagValue.Type = BitConverter.ToUInt16(response, num4 + 4);
							abTagValue.Value = response.SelectMiddle(num4 + 6, num5 - num4 - 6);
							list.Add(abTagValue);
						}
					}
				}
				else
				{
					byte b = response[num + 4];
					switch (b)
					{
					case 4:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley04
						};
					case 5:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley05
						};
					case 6:
						value = true;
						break;
					case 10:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley0A
						};
					case 12:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley0C
						};
					case 19:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley13
						};
					case 28:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley1C
						};
					case 30:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley1E
						};
					case 32:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley20
						};
					case 38:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.AllenBradley26
						};
					default:
						return new OperateResult<List<AbTagValue>, bool>
						{
							ErrorCode = b,
							Message = StringResources.Language.UnknownError
						};
					case 0:
						break;
					}
					if (response[num + 2] == 205 || response[num + 2] == 211)
					{
						return OperateResult.CreateSuccessResult(list, value);
					}
					if (response[num + 2] == 204 || response[num + 2] == 210)
					{
						AbTagValue abTagValue2 = new AbTagValue();
						abTagValue2.Type = BitConverter.ToUInt16(response, num + 6);
						abTagValue2.Value = response.SelectMiddle(num + 8, num2 - 6);
						list.Add(abTagValue2);
					}
					else if (response[num + 2] == 213)
					{
						AbTagValue abTagValue3 = new AbTagValue();
						abTagValue3.Value = response.SelectMiddle(num + 6, num2 - 4);
						list.Add(abTagValue3);
					}
				}
				return OperateResult.CreateSuccessResult(list, value);
			}
			catch (Exception ex)
			{
				return new OperateResult<List<AbTagValue>, bool>("ExtractActualData failed: " + ex.Message + Environment.NewLine + response.ToHexString(' '));
			}
		}

		/// <summary>
		/// 从PLC反馈的数据解析，返回解析后的数据内容，数据类型（在多项数据返回中无效），以及是否有更多的数据
		/// </summary>
		/// <param name="response">PLC的反馈数据</param>
		/// <param name="isRead">是否是返回的操作</param>
		/// <returns>带有结果标识的最终数据</returns>
		public static OperateResult<byte[], ushort, bool> ExtractActualData(byte[] response, bool isRead)
		{
			OperateResult<List<AbTagValue>, bool> operateResult = ExtractActualDataList(response, isRead);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[], ushort, bool>(operateResult);
			}
			if (operateResult.Content1.Count == 0)
			{
				return OperateResult.CreateSuccessResult(new byte[0], (ushort)0, operateResult.Content2);
			}
			List<byte> list = new List<byte>();
			foreach (AbTagValue item in operateResult.Content1)
			{
				list.AddRange(item.Value);
			}
			return OperateResult.CreateSuccessResult(list.ToArray(), operateResult.Content1[0].Type, operateResult.Content2);
		}

		internal static OperateResult<string> ExtractActualString(OperateResult<byte[], ushort, bool> read, IByteTransform byteTransform, Encoding encoding)
		{
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(read);
			}
			try
			{
				if (read.Content2 == 218)
				{
					if (read.Content1.Length >= 1)
					{
						if (read.Content1[0] == 0)
						{
							return OperateResult.CreateSuccessResult(string.Empty);
						}
						if (read.Content1[0] >= read.Content1.Length)
						{
							return OperateResult.CreateSuccessResult(encoding.GetString(read.Content1));
						}
						return OperateResult.CreateSuccessResult(encoding.GetString(read.Content1, 1, read.Content1[0]));
					}
					return OperateResult.CreateSuccessResult(encoding.GetString(read.Content1));
				}
				if (read.Content1.Length >= 6)
				{
					int num = byteTransform.TransInt32(read.Content1, 2);
					if (num == 0)
					{
						return OperateResult.CreateSuccessResult(string.Empty);
					}
					return OperateResult.CreateSuccessResult(encoding.GetString(read.Content1, 6, num));
				}
				return OperateResult.CreateSuccessResult(encoding.GetString(read.Content1));
			}
			catch (Exception ex)
			{
				return new OperateResult<string>(ex.Message + " Source: " + read.Content1.ToHexString(' '));
			}
		}

		/// <summary>
		/// 从PLC里读取当前PLC的型号信息<br />
		/// Read the current PLC model information from the PLC
		/// </summary>
		/// <param name="plc">PLC对象</param>
		/// <returns>型号数据信息</returns>
		public static OperateResult<string> ReadPlcType(IReadWriteDevice plc)
		{
			OperateResult<byte[]> operateResult = plc.ReadFromCoreServer("00 00 00 00 00 00 02 00 00 00 00 00 b2 00 06 00 01 02 20 01 24 01".ToHexBytes());
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			if (operateResult.Content.Length > 59 && operateResult.Content.Length >= 59 + operateResult.Content[58])
			{
				return OperateResult.CreateSuccessResult(Encoding.UTF8.GetString(operateResult.Content, 59, operateResult.Content[58]));
			}
			return new OperateResult<string>("Data is too short: " + operateResult.Content.ToHexString(' '));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.ReadPlcType(HslCommunication.Core.IReadWriteDevice)" />
		public static async Task<OperateResult<string>> ReadPlcTypeAsync(IReadWriteDevice plc)
		{
			OperateResult<byte[]> read = await plc.ReadFromCoreServerAsync("00 00 00 00 00 00 02 00 00 00 00 00 b2 00 06 00 01 02 20 01 24 01".ToHexBytes());
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(read);
			}
			if (read.Content.Length > 59 && read.Content.Length >= 59 + read.Content[58])
			{
				return OperateResult.CreateSuccessResult(Encoding.UTF8.GetString(read.Content, 59, read.Content[58]));
			}
			return new OperateResult<string>("Data is too short: " + read.Content.ToHexString(' '));
		}

		/// <summary>
		/// 读取指定地址的日期数据，最小日期为 1970年1月1日，当PLC的变量类型为 "Date" 和 "TimeAndDate" 时，都可以用本方法读取。<br />
		/// Read the date data of the specified address. The minimum date is January 1, 1970. When the PLC variable type is "Date" and "TimeAndDate", this method can be used to read.
		/// </summary>
		/// <param name="plc">当前的通信对象信息</param>
		/// <param name="address">PLC里变量的地址</param>
		/// <returns>日期结果对象</returns>
		public static OperateResult<DateTime> ReadDate(IReadWriteCip plc, string address)
		{
			OperateResult<long> operateResult = plc.ReadInt64(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<DateTime>(operateResult);
			}
			long value = operateResult.Content / 100;
			return OperateResult.CreateSuccessResult(new DateTime(1970, 1, 1).AddTicks(value));
		}

		/// <summary>
		/// 使用日期格式（Date）将指定的数据写入到指定的地址里，PLC的地址类型变量必须为 "Date"，否则写入失败。<br />
		/// Use the date format (Date) to write the specified data to the specified address. The PLC address type variable must be "Date", otherwise the writing will fail.
		/// </summary>
		/// <param name="plc">当前的通信对象信息</param>
		/// <param name="address">PLC里变量的地址</param>
		/// <param name="date">时间信息</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult WriteDate(IReadWriteCip plc, string address, DateTime date)
		{
			long value = (date.Date - new DateTime(1970, 1, 1)).Ticks * 100;
			return plc.WriteTag(address, 8, plc.ByteTransform.TransByte(value));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.WriteDate(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String,System.DateTime)" />
		public static OperateResult WriteTimeAndDate(IReadWriteCip plc, string address, DateTime date)
		{
			long value = (date - new DateTime(1970, 1, 1)).Ticks * 100;
			return plc.WriteTag(address, 10, plc.ByteTransform.TransByte(value));
		}

		/// <summary>
		/// 读取指定地址的时间数据，最小时间为 0，如果获取秒，可以访问 <see cref="P:System.TimeSpan.TotalSeconds" />，当PLC的变量类型为 "Time" 和 "TimeOfDate" 时，都可以用本方法读取。<br />
		/// Read the time data of the specified address. The minimum time is 0. If you get seconds, you can access <see cref="P:System.TimeSpan.TotalSeconds" />. 
		/// When the PLC variable type is "Time" and "TimeOfDate", you can use this Method to read.
		/// </summary>
		/// <param name="plc">当前的通信对象信息</param>
		/// <param name="address">PLC里变量的地址</param>
		/// <returns>时间的结果对象</returns>
		public static OperateResult<TimeSpan> ReadTime(IReadWriteCip plc, string address)
		{
			OperateResult<long> operateResult = plc.ReadInt64(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<TimeSpan>(operateResult);
			}
			return OperateResult.CreateSuccessResult(TimeSpan.FromTicks(operateResult.Content / 100));
		}

		/// <summary>
		/// 使用时间格式（TIME）将时间数据写入到PLC中指定的地址里去，PLC的地址类型变量必须为 "TIME"，否则写入失败。<br />
		/// Use the time format (TIME) to write the time data to the address specified in the PLC. The PLC address type variable must be "TIME", otherwise the writing will fail.
		/// </summary>
		/// <param name="plc">当前的通信对象信息</param>
		/// <param name="address">PLC里变量的地址</param>
		/// <param name="time">时间参数变量</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult WriteTime(IReadWriteCip plc, string address, TimeSpan time)
		{
			return plc.WriteTag(address, 9, plc.ByteTransform.TransByte(time.Ticks * 100));
		}

		/// <summary>
		/// 使用时间格式（TimeOfDate）将时间数据写入到PLC中指定的地址里去，PLC的地址类型变量必须为 "TimeOfDate"，否则写入失败。<br />
		/// Use the time format (TimeOfDate) to write the time data to the address specified in the PLC. The PLC address type variable must be "TimeOfDate", otherwise the writing will fail.
		/// </summary>
		/// <param name="plc">当前的通信对象信息</param>
		/// <param name="address">PLC里变量的地址</param>
		/// <param name="timeOfDate">时间参数变量</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult WriteTimeOfDate(IReadWriteCip plc, string address, TimeSpan timeOfDate)
		{
			return plc.WriteTag(address, 11, plc.ByteTransform.TransByte(timeOfDate.Ticks * 100));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.ReadDate(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String)" />
		public static async Task<OperateResult<DateTime>> ReadDateAsync(IReadWriteCip plc, string address)
		{
			OperateResult<long> read = await plc.ReadInt64Async(address);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<DateTime>(read);
			}
			long tick = read.Content / 100;
			return OperateResult.CreateSuccessResult(new DateTime(1970, 1, 1).AddTicks(tick));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.WriteDate(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String,System.DateTime)" />
		public static async Task<OperateResult> WriteDateAsync(IReadWriteCip plc, string address, DateTime date)
		{
			long tick = (date.Date - new DateTime(1970, 1, 1)).Ticks * 100;
			return await plc.WriteTagAsync(address, 8, plc.ByteTransform.TransByte(tick));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.WriteTimeAndDate(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String,System.DateTime)" />
		public static async Task<OperateResult> WriteTimeAndDateAsync(IReadWriteCip plc, string address, DateTime date)
		{
			long tick = (date - new DateTime(1970, 1, 1)).Ticks * 100;
			return await plc.WriteTagAsync(address, 10, plc.ByteTransform.TransByte(tick));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.ReadTime(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String)" />
		public static async Task<OperateResult<TimeSpan>> ReadTimeAsync(IReadWriteCip plc, string address)
		{
			OperateResult<long> read = await plc.ReadInt64Async(address);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<TimeSpan>(read);
			}
			return OperateResult.CreateSuccessResult(TimeSpan.FromTicks(read.Content / 100));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.WriteTime(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String,System.TimeSpan)" />
		public static async Task<OperateResult> WriteTimeAsync(IReadWriteCip plc, string address, TimeSpan time)
		{
			return await plc.WriteTagAsync(address, 9, plc.ByteTransform.TransByte(time.Ticks * 100));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.AllenBradley.AllenBradleyHelper.WriteTimeOfDate(HslCommunication.Profinet.AllenBradley.IReadWriteCip,System.String,System.TimeSpan)" />
		public static async Task<OperateResult> WriteTimeOfDateAsync(IReadWriteCip plc, string address, TimeSpan timeOfDate)
		{
			return await plc.WriteTagAsync(address, 11, plc.ByteTransform.TransByte(timeOfDate.Ticks * 100));
		}
	}
}
