using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Profinet.Panasonic;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.LSIS
{
	/// <summary>
	/// Lsis的虚拟服务器，其中TCP的端口支持Fnet协议，串口支持Cnet协议<br />
	/// LSisServer
	/// </summary>
	public class LSisServer : DeviceServer
	{
		private SoftBuffer pBuffer;

		private SoftBuffer qBuffer;

		private SoftBuffer mBuffer;

		private SoftBuffer iBuffer;

		private SoftBuffer uBuffer;

		private SoftBuffer dBuffer;

		private SoftBuffer tBuffer;

		private const int DataPoolLength = 65536;

		private int station = 1;

		/// <summary>
		/// set plc
		/// </summary>
		public string SetCpuType { get; set; }

		/// <summary>
		/// LSisServer
		/// </summary>
		public LSisServer()
		{
			pBuffer = new SoftBuffer(65536);
			qBuffer = new SoftBuffer(65536);
			iBuffer = new SoftBuffer(65536);
			uBuffer = new SoftBuffer(65536);
			mBuffer = new SoftBuffer(65536);
			dBuffer = new SoftBuffer(131072);
			tBuffer = new SoftBuffer(131072);
			base.WordLength = 2;
			base.ByteTransform = new RegularByteTransform();
		}

		/// <summary>
		/// LSisServer
		/// </summary>
		public LSisServer(string CpuType)
			: this()
		{
			SetCpuType = CpuType;
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.LSIS.LSFastEnet.Read(System.String,System.UInt16)" />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<string> operateResult = AnalysisAddressToByteUnit(address, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			int index = int.Parse(operateResult.Content.Substring(1));
			switch (operateResult.Content[0])
			{
			case 'P':
				return OperateResult.CreateSuccessResult(pBuffer.GetBytes(index, length));
			case 'Q':
				return OperateResult.CreateSuccessResult(qBuffer.GetBytes(index, length));
			case 'M':
				return OperateResult.CreateSuccessResult(mBuffer.GetBytes(index, length));
			case 'I':
				return OperateResult.CreateSuccessResult(iBuffer.GetBytes(index, length));
			case 'U':
				return OperateResult.CreateSuccessResult(uBuffer.GetBytes(index, length));
			case 'D':
				return OperateResult.CreateSuccessResult(dBuffer.GetBytes(index, length));
			case 'T':
				return OperateResult.CreateSuccessResult(tBuffer.GetBytes(index, length));
			default:
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.LSIS.LSFastEnet.Write(System.String,System.Byte[])" />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			OperateResult<string> operateResult = AnalysisAddressToByteUnit(address, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			int destIndex = int.Parse(operateResult.Content.Substring(1));
			switch (operateResult.Content[0])
			{
			case 'P':
				pBuffer.SetBytes(value, destIndex);
				break;
			case 'Q':
				qBuffer.SetBytes(value, destIndex);
				break;
			case 'M':
				mBuffer.SetBytes(value, destIndex);
				break;
			case 'I':
				iBuffer.SetBytes(value, destIndex);
				break;
			case 'U':
				uBuffer.SetBytes(value, destIndex);
				break;
			case 'D':
				dBuffer.SetBytes(value, destIndex);
				break;
			case 'T':
				tBuffer.SetBytes(value, destIndex);
				break;
			default:
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.LSIS.LSFastEnet.ReadByte(System.String)" />
		[HslMqttApi("ReadByte", "")]
		public OperateResult<byte> ReadByte(string address)
		{
			return ByteTransformHelper.GetResultFromArray(Read(address, 1));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.LSIS.LSFastEnet.Write(System.String,System.Byte)" />
		[HslMqttApi("WriteByte", "")]
		public OperateResult Write(string address, byte value)
		{
			return Write(address, new byte[1] { value });
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			OperateResult<string> operateResult = AnalysisAddressToByteUnit(address, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			int destIndex = int.Parse(operateResult.Content.Substring(1));
			switch (operateResult.Content[0])
			{
			case 'P':
				return OperateResult.CreateSuccessResult(pBuffer.GetBool(destIndex, length));
			case 'Q':
				return OperateResult.CreateSuccessResult(qBuffer.GetBool(destIndex, length));
			case 'M':
				return OperateResult.CreateSuccessResult(mBuffer.GetBool(destIndex, length));
			case 'I':
				return OperateResult.CreateSuccessResult(iBuffer.GetBool(destIndex, length));
			case 'U':
				return OperateResult.CreateSuccessResult(uBuffer.GetBool(destIndex, length));
			default:
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		/// <inheritdoc />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			OperateResult<string> operateResult = AnalysisAddressToByteUnit(address, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			int destIndex = int.Parse(operateResult.Content.Substring(1));
			switch (operateResult.Content[0])
			{
			case 'P':
				pBuffer.SetBool(value, destIndex);
				return OperateResult.CreateSuccessResult();
			case 'Q':
				qBuffer.SetBool(value, destIndex);
				return OperateResult.CreateSuccessResult();
			case 'M':
				mBuffer.SetBool(value, destIndex);
				return OperateResult.CreateSuccessResult();
			case 'I':
				iBuffer.SetBool(value, destIndex);
				return OperateResult.CreateSuccessResult();
			case 'U':
				uBuffer.SetBool(value, destIndex);
				return OperateResult.CreateSuccessResult();
			default:
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new LsisFastEnetMessage();
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			if (session.Communication is PipeSerialPort)
			{
				try
				{
					byte[] value = null;
					if (receive[3] == 114 || receive[3] == 82)
					{
						value = ReadSerialByCommand(receive);
					}
					else if (receive[3] == 119 || receive[3] == 87)
					{
						value = WriteSerialByMessage(receive);
					}
					return OperateResult.CreateSuccessResult(value);
				}
				catch (Exception ex)
				{
					return new OperateResult<byte[]>(ex.Message + " Source: " + receive.ToHexString(' '));
				}
			}
			byte[] value2 = null;
			if (receive[20] == 84)
			{
				value2 = ReadByCommand(receive);
			}
			else if (receive[20] == 88)
			{
				value2 = WriteByMessage(receive);
			}
			return OperateResult.CreateSuccessResult(value2);
		}

		private byte[] ReadByCommand(byte[] command)
		{
			List<byte> list = new List<byte>();
			list.AddRange(command.SelectBegin(20));
			list[9] = 17;
			list[10] = 1;
			list[12] = 160;
			list[13] = 17;
			list[18] = 3;
			byte[] obj = new byte[10] { 85, 0, 0, 0, 8, 1, 0, 0, 1, 0 };
			obj[2] = command[22];
			obj[3] = command[23];
			list.AddRange(obj);
			int num = command[28];
			string @string = Encoding.ASCII.GetString(command, 31, num - 1);
			byte[] array;
			if (command[22] != 0)
			{
				array = ((command[22] == 1) ? Read(@string, 1).Content : ((command[22] == 2) ? Read(@string, 2).Content : ((command[22] == 3) ? Read(@string, 4).Content : ((command[22] == 4) ? Read(@string, 8).Content : ((command[22] != 20) ? Read(@string, 1).Content : Read(@string, BitConverter.ToUInt16(command, 30 + num)).Content)))));
			}
			else
			{
				int num2 = Convert.ToInt32(@string.Substring(2));
				array = ((!ReadBool(@string.Substring(0, 2) + num2 / 16 + (num2 % 16).ToString("X1")).Content) ? new byte[1] : new byte[1] { 1 });
			}
			list.AddRange(BitConverter.GetBytes((ushort)array.Length));
			list.AddRange(array);
			list[16] = (byte)(list.Count - 20);
			return list.ToArray();
		}

		private byte[] WriteByMessage(byte[] packCommand)
		{
			if (!base.EnableWrite)
			{
				return null;
			}
			List<byte> list = new List<byte>();
			list.AddRange(packCommand.SelectBegin(20));
			list[9] = 17;
			list[10] = 1;
			list[12] = 160;
			list[13] = 17;
			list[18] = 3;
			list.AddRange(new byte[10] { 89, 0, 20, 0, 8, 1, 0, 0, 1, 0 });
			int num = packCommand[28];
			string @string = Encoding.ASCII.GetString(packCommand, 31, num - 1);
			int length = BitConverter.ToUInt16(packCommand, 30 + num);
			int num2 = 0;
			byte[] array = base.ByteTransform.TransByte(packCommand, 32 + num, length);
			if (packCommand[22] == 0)
			{
				if (@string.IndexOf('.') < 0)
				{
					num2 = int.Parse(@string.Substring(2), NumberStyles.HexNumber);
				}
				else
				{
					string[] array2 = @string.Substring(2, @string.Length - 2).Split('.');
					num2 = ((array2.Length < 3) ? int.Parse(array2[1], NumberStyles.HexNumber) : int.Parse(array2[2], NumberStyles.HexNumber));
				}
				int num3 = Convert.ToInt32(num2);
				Write(@string.Substring(0, 2) + num3 / 16 + (num3 % 16).ToString("X1"), array[0] == 1);
			}
			else
			{
				Write(@string, array);
			}
			list[16] = (byte)(list.Count - 20);
			return list.ToArray();
		}

		/// <inheritdoc />
		protected override void LoadFromBytes(byte[] content)
		{
			if (content.Length < 262144)
			{
				throw new Exception("File is not correct");
			}
			pBuffer.SetBytes(content, 0, 0, 65536);
			qBuffer.SetBytes(content, 65536, 0, 65536);
			mBuffer.SetBytes(content, 131072, 0, 65536);
			dBuffer.SetBytes(content, 196608, 0, 65536);
		}

		/// <inheritdoc />
		protected override byte[] SaveToBytes()
		{
			byte[] array = new byte[262144];
			Array.Copy(pBuffer.GetBytes(), 0, array, 0, 65536);
			Array.Copy(qBuffer.GetBytes(), 0, array, 65536, 65536);
			Array.Copy(mBuffer.GetBytes(), 0, array, 131072, 65536);
			Array.Copy(dBuffer.GetBytes(), 0, array, 196608, 65536);
			return array;
		}

		/// <summary>
		/// NumberStyles HexNumber
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private static bool IsHex(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}
			bool result = false;
			for (int i = 0; i < value.Length; i++)
			{
				switch (value[i])
				{
				case 'A':
				case 'B':
				case 'C':
				case 'D':
				case 'E':
				case 'F':
				case 'a':
				case 'b':
				case 'c':
				case 'd':
				case 'e':
				case 'f':
					result = true;
					break;
				}
			}
			return result;
		}

		/// <summary>
		/// Check the intput string address
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public static int CheckAddress(string address)
		{
			int result = 0;
			if (IsHex(address))
			{
				if (int.TryParse(address, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var result2))
				{
					result = result2;
				}
			}
			else
			{
				result = int.Parse(address);
			}
			return result;
		}

		/// <inheritdoc />
		protected override bool CheckSerialReceiveDataComplete(byte[] buffer, int receivedLength)
		{
			if (receivedLength > 5)
			{
				return buffer[receivedLength - 3] == 4;
			}
			return base.CheckSerialReceiveDataComplete(buffer, receivedLength);
		}

		/// <inheritdoc />
		protected override string GetLogTextFromBinary(PipeSession session, byte[] content)
		{
			if (session != null && session.Communication is PipeSerialPort)
			{
				return SoftBasic.GetAsciiStringRender(content);
			}
			return base.GetLogTextFromBinary(session, content);
		}

		private byte[] PackReadSerialResponse(byte[] receive, short err, List<byte[]> data)
		{
			List<byte> list = new List<byte>(24);
			if (err == 0)
			{
				list.Add(6);
			}
			else
			{
				list.Add(21);
			}
			list.AddRange(SoftBasic.BuildAsciiBytesFrom((byte)station));
			list.Add(receive[3]);
			list.Add(receive[4]);
			list.Add(receive[5]);
			if (err == 0)
			{
				if (data != null)
				{
					if (Encoding.ASCII.GetString(receive, 4, 2) == "SS")
					{
						list.AddRange(SoftBasic.BuildAsciiBytesFrom((byte)data.Count));
					}
					else if (Encoding.ASCII.GetString(receive, 4, 2) == "SB")
					{
						list.AddRange(Encoding.ASCII.GetBytes("01"));
					}
					for (int i = 0; i < data.Count; i++)
					{
						list.AddRange(SoftBasic.BuildAsciiBytesFrom((byte)data[i].Length));
						list.AddRange(SoftBasic.BytesToAsciiBytes(data[i]));
					}
				}
			}
			else
			{
				list.AddRange(SoftBasic.BuildAsciiBytesFrom(err));
			}
			list.Add(3);
			int num = 0;
			for (int j = 0; j < list.Count; j++)
			{
				num += list[j];
			}
			list.AddRange(SoftBasic.BuildAsciiBytesFrom((byte)num));
			return list.ToArray();
		}

		private byte[] ReadSerialByCommand(byte[] command)
		{
			string asciiStringRender = SoftBasic.GetAsciiStringRender(command);
			if (Encoding.ASCII.GetString(command, 4, 2) == "SS")
			{
				int num = int.Parse(Encoding.ASCII.GetString(command, 6, 2));
				if (num > 16)
				{
					return PackReadSerialResponse(command, 4, null);
				}
				List<byte[]> list = new List<byte[]>();
				int num2 = 8;
				for (int i = 0; i < num; i++)
				{
					int num3 = Convert.ToInt32(Encoding.ASCII.GetString(command, num2, 2), 16);
					string @string = Encoding.ASCII.GetString(command, num2 + 2 + 1, num3 - 1);
					if (@string[1] != 'X')
					{
						OperateResult<byte[]> operateResult = Read(@string, AnalysisAddressLength(@string));
						if (!operateResult.IsSuccess)
						{
							return PackReadSerialResponse(command, 1, null);
						}
						list.Add(operateResult.Content);
					}
					else
					{
						OperateResult<bool> operateResult2 = ReadBool(@string);
						if (!operateResult2.IsSuccess)
						{
							return PackReadSerialResponse(command, 1, null);
						}
						list.Add((!operateResult2.Content) ? new byte[1] : new byte[1] { 1 });
					}
					num2 += 2 + num3;
				}
				return PackReadSerialResponse(command, 0, list);
			}
			if (Encoding.ASCII.GetString(command, 4, 2) == "SB")
			{
				int num4 = Convert.ToInt32(Encoding.ASCII.GetString(command, 6, 2), 16);
				string string2 = Encoding.ASCII.GetString(command, 9, num4 - 1);
				ushort num5 = (ushort)(Convert.ToUInt16(Encoding.ASCII.GetString(command, 8 + num4, 2), 16) * AnalysisAddressLength(string2));
				if (num5 > 120)
				{
					return PackReadSerialResponse(command, 4658, null);
				}
				OperateResult<byte[]> operateResult3 = Read(string2, num5);
				if (!operateResult3.IsSuccess)
				{
					return PackReadSerialResponse(command, 1, null);
				}
				return PackReadSerialResponse(command, 0, new List<byte[]> { operateResult3.Content });
			}
			return PackReadSerialResponse(command, 1, null);
		}

		private byte[] WriteSerialByMessage(byte[] command)
		{
			if (!base.EnableWrite)
			{
				return null;
			}
			if (Encoding.ASCII.GetString(command, 4, 2) == "SS")
			{
				int num = int.Parse(Encoding.ASCII.GetString(command, 6, 2));
				int num2 = 8;
				if (num > 16)
				{
					return PackReadSerialResponse(command, 4, null);
				}
				for (int i = 0; i < num; i++)
				{
					int num3 = Convert.ToInt32(Encoding.ASCII.GetString(command, num2, 2), 16);
					string @string = Encoding.ASCII.GetString(command, num2 + 2 + 1, num3 - 1);
					switch (@string[1])
					{
					case 'B':
					case 'D':
					case 'L':
					case 'W':
						if (!Write(@string, Encoding.ASCII.GetString(command, num2 + 2 + num3, AnalysisAddressLength(@string) * 2).ToHexBytes()).IsSuccess)
						{
							return PackReadSerialResponse(command, 1, null);
						}
						num2 += 2 + num3 + AnalysisAddressLength(@string) * 2;
						break;
					case 'X':
						if (!Write(@string, Convert.ToByte(Encoding.ASCII.GetString(command, num2 + 2 + num3, 2), 16) != 0).IsSuccess)
						{
							return PackReadSerialResponse(command, 1, null);
						}
						num2 += 2 + num3 + 2;
						break;
					}
				}
				return PackReadSerialResponse(command, 0, null);
			}
			if (Encoding.ASCII.GetString(command, 4, 2) == "SB")
			{
				int num4 = Convert.ToInt32(Encoding.ASCII.GetString(command, 6, 2), 16);
				string string2 = Encoding.ASCII.GetString(command, 9, num4 - 1);
				if (string2[1] == 'X')
				{
					return PackReadSerialResponse(command, 4402, null);
				}
				int num5 = Convert.ToUInt16(Encoding.ASCII.GetString(command, 8 + num4, 2), 16) * AnalysisAddressLength(string2);
				if (!Write(string2, Encoding.ASCII.GetString(command, 10 + num4, num5 * 2).ToHexBytes()).IsSuccess)
				{
					return PackReadSerialResponse(command, 1, null);
				}
				return PackReadSerialResponse(command, 0, null);
			}
			return PackReadSerialResponse(command, 4402, null);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"LSisServer[{base.Port}]";
		}

		private static ushort AnalysisAddressLength(string address)
		{
			switch (address[1])
			{
			case 'X':
				return 1;
			case 'B':
				return 1;
			case 'W':
				return 2;
			case 'D':
				return 4;
			case 'L':
				return 8;
			default:
				return 1;
			}
		}

		/// <summary>
		/// 将带有数据类型的地址，转换成实际的byte数组的地址信息，例如 MW100 转成 M200
		/// </summary>
		/// <param name="address">带有类型的地址</param>
		/// <param name="isBit">是否是位操作</param>
		/// <returns>最终的按照字节为单位的地址信息</returns>
		public OperateResult<string> AnalysisAddressToByteUnit(string address, bool isBit)
		{
			if (!"PMLKFTCDSQINUZR".Contains(address.Substring(0, 1)))
			{
				return new OperateResult<string>(StringResources.Language.NotSupportedDataType);
			}
			try
			{
				int num;
				if (address[0] == 'D' || address[0] == 'T')
				{
					switch (address[1])
					{
					case 'B':
						num = Convert.ToInt32(address.Substring(2));
						break;
					case 'W':
						num = Convert.ToInt32(address.Substring(2)) * 2;
						break;
					case 'D':
						num = Convert.ToInt32(address.Substring(2)) * 4;
						break;
					case 'L':
						num = Convert.ToInt32(address.Substring(2)) * 8;
						break;
					default:
						num = Convert.ToInt32(address.Substring(1)) * 2;
						break;
					}
				}
				else if (isBit)
				{
					num = ((address[1] != 'X') ? PanasonicHelper.CalculateComplexAddress(address.Substring(1)) : PanasonicHelper.CalculateComplexAddress(address.Substring(2)));
				}
				else
				{
					switch (address[1])
					{
					case 'X':
						num = Convert.ToInt32(address.Substring(2));
						break;
					case 'B':
						num = Convert.ToInt32(address.Substring(2));
						break;
					case 'W':
						num = Convert.ToInt32(address.Substring(2)) * 2;
						break;
					case 'D':
						num = Convert.ToInt32(address.Substring(2)) * 4;
						break;
					case 'L':
						num = Convert.ToInt32(address.Substring(2)) * 8;
						break;
					default:
						num = Convert.ToInt32(address.Substring(1)) * (isBit ? 1 : 2);
						break;
					}
				}
				return OperateResult.CreateSuccessResult(address.Substring(0, 1) + num);
			}
			catch (Exception ex)
			{
				return new OperateResult<string>("AnalysisAddress Failed: " + ex.Message + " Source: " + address);
			}
		}
	}
}
