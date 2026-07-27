using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.Cimon.Helper;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Cimon
{
	/// <summary>
	/// Cimon的服务器端实现
	/// </summary>
	public class CimonServer : DeviceServer
	{
		private const int OffsetX = 0;

		private const int OffsetY = 1000;

		private const int OffsetM = 2000;

		private const int OffsetK = 3000;

		private const int OffsetL = 4000;

		private const int OffsetC = 5000;

		private const int OffsetT = 6000;

		private const int OffsetFlag = 7000;

		private const int OffsetD = 0;

		private const int OffsetF = 40000;

		private const int OffsetZ = 41000;

		private const int OffsetS = 42000;

		private SoftBuffer bufferBit;

		private SoftBuffer bufferWord;

		private const int DataPoolLength = 65536;

		private byte frameNo = 1;

		/// <inheritdoc cref="P:HslCommunication.Profinet.Cimon.CimonHmiProtocol.FrameNo" />
		public byte FrameNo
		{
			get
			{
				return frameNo;
			}
			set
			{
				frameNo = value;
			}
		}

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		public CimonServer()
		{
			bufferBit = new SoftBuffer(131072)
			{
				IsBoolReverseByWord = true
			};
			bufferWord = new SoftBuffer(65536);
			base.WordLength = 1;
			base.ByteTransform = new ReverseWordTransform();
		}

		private OperateResult<byte[]> CreateReadWordResult(SoftBuffer softBuffer, int offset, int address, int length)
		{
			return OperateResult.CreateSuccessResult(softBuffer.GetBytes(address * 2 + offset * 2, length * 2));
		}

		/// <inheritdoc />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			try
			{
				int address2 = int.Parse(address.Substring(1));
				if (address[0] == 'X')
				{
					return CreateReadWordResult(bufferBit, 0, address2, length);
				}
				if (address[0] == 'Y')
				{
					return CreateReadWordResult(bufferBit, 1000, address2, length);
				}
				if (address[0] == 'M')
				{
					return CreateReadWordResult(bufferBit, 2000, address2, length);
				}
				if (address[0] == 'K')
				{
					return CreateReadWordResult(bufferBit, 3000, address2, length);
				}
				if (address[0] == 'L')
				{
					return CreateReadWordResult(bufferBit, 4000, address2, length);
				}
				if (address[0] == 'C')
				{
					return CreateReadWordResult(bufferBit, 5000, address2, length);
				}
				if (address[0] == 'T')
				{
					return CreateReadWordResult(bufferBit, 6000, address2, length);
				}
				if (address[0] == 'D')
				{
					return CreateReadWordResult(bufferWord, 0, address2, length);
				}
				if (address[0] == 'F')
				{
					return CreateReadWordResult(bufferWord, 40000, address2, length);
				}
				if (address[0] == 'Z')
				{
					return CreateReadWordResult(bufferWord, 41000, address2, length);
				}
				if (address[0] == 'S')
				{
					return CreateReadWordResult(bufferWord, 42000, address2, length);
				}
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(ex.Message);
			}
		}

		private OperateResult CreateWriteWordResult(SoftBuffer softBuffer, int offset, int address, byte[] value)
		{
			softBuffer.SetBytes(value, address * 2 + offset * 2);
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			try
			{
				int address2 = int.Parse(address.Substring(1));
				if (address[0] == 'X')
				{
					return CreateWriteWordResult(bufferBit, 0, address2, value);
				}
				if (address[0] == 'Y')
				{
					return CreateWriteWordResult(bufferBit, 1000, address2, value);
				}
				if (address[0] == 'M')
				{
					return CreateWriteWordResult(bufferBit, 2000, address2, value);
				}
				if (address[0] == 'K')
				{
					return CreateWriteWordResult(bufferBit, 3000, address2, value);
				}
				if (address[0] == 'L')
				{
					return CreateWriteWordResult(bufferBit, 4000, address2, value);
				}
				if (address[0] == 'C')
				{
					return CreateWriteWordResult(bufferBit, 5000, address2, value);
				}
				if (address[0] == 'T')
				{
					return CreateWriteWordResult(bufferBit, 2000, address2, value);
				}
				if (address[0] == 'D')
				{
					return CreateWriteWordResult(bufferWord, 0, address2, value);
				}
				if (address[0] == 'F')
				{
					return CreateWriteWordResult(bufferWord, 40000, address2, value);
				}
				if (address[0] == 'Z')
				{
					return CreateWriteWordResult(bufferWord, 41000, address2, value);
				}
				if (address[0] == 'S')
				{
					return CreateWriteWordResult(bufferWord, 42000, address2, value);
				}
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(ex.Message);
			}
		}

		private int GetBitIndex(ref string address)
		{
			if (address.IndexOf('.') < 0)
			{
				int result = Convert.ToInt32(address.Substring(address.Length - 1), 16);
				address = address.Substring(0, address.Length - 1);
				return result;
			}
			int result2 = Convert.ToInt32(address.Substring(address.IndexOf('.') + 1), 16);
			address = address.Substring(0, address.IndexOf('.'));
			return result2;
		}

		private OperateResult<bool[]> CreateReadBitResult(SoftBuffer softBuffer, int offset, int bitIndex, int length)
		{
			return OperateResult.CreateSuccessResult(softBuffer.GetBool(bitIndex + offset * 16, length));
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			int bitIndex = GetBitIndex(ref address);
			try
			{
				int bitIndex2 = int.Parse(address.Substring(1)) * 16 + bitIndex;
				if (address[0] == 'X')
				{
					return CreateReadBitResult(bufferBit, 0, bitIndex2, length);
				}
				if (address[0] == 'Y')
				{
					return CreateReadBitResult(bufferBit, 1000, bitIndex2, length);
				}
				if (address[0] == 'M')
				{
					return CreateReadBitResult(bufferBit, 2000, bitIndex2, length);
				}
				if (address[0] == 'K')
				{
					return CreateReadBitResult(bufferBit, 3000, bitIndex2, length);
				}
				if (address[0] == 'L')
				{
					return CreateReadBitResult(bufferBit, 4000, bitIndex2, length);
				}
				if (address[0] == 'T')
				{
					return CreateReadBitResult(bufferBit, 6000, bitIndex2, length);
				}
				if (address[0] == 'C')
				{
					return CreateReadBitResult(bufferBit, 5000, bitIndex2, length);
				}
				if (address[0] == 'F')
				{
					return CreateReadBitResult(bufferBit, 7000, bitIndex2, length);
				}
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult<bool[]>(ex.Message);
			}
		}

		private OperateResult CreateWriteBitResult(SoftBuffer softBuffer, int offset, int bitIndex, bool[] value)
		{
			softBuffer.SetBool(value, bitIndex + offset * 16);
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			int bitIndex = GetBitIndex(ref address);
			try
			{
				int bitIndex2 = int.Parse(address.Substring(1)) * 16 + bitIndex;
				if (address[0] == 'X')
				{
					return CreateWriteBitResult(bufferBit, 0, bitIndex2, value);
				}
				if (address[0] == 'Y')
				{
					return CreateWriteBitResult(bufferBit, 1000, bitIndex2, value);
				}
				if (address[0] == 'M')
				{
					return CreateWriteBitResult(bufferBit, 2000, bitIndex2, value);
				}
				if (address[0] == 'K')
				{
					return CreateWriteBitResult(bufferBit, 3000, bitIndex2, value);
				}
				if (address[0] == 'L')
				{
					return CreateWriteBitResult(bufferBit, 4000, bitIndex2, value);
				}
				if (address[0] == 'T')
				{
					return CreateWriteBitResult(bufferBit, 6000, bitIndex2, value);
				}
				if (address[0] == 'C')
				{
					return CreateWriteBitResult(bufferBit, 5000, bitIndex2, value);
				}
				if (address[0] == 'F')
				{
					return CreateWriteBitResult(bufferBit, 7000, bitIndex2, value);
				}
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult(ex.Message);
			}
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			if (receive[10] == 82 || receive[10] == 114)
			{
				return OperateResult.CreateSuccessResult(ReadByCommand(receive));
			}
			if (receive[10] == 87 || receive[10] == 119)
			{
				return OperateResult.CreateSuccessResult(WriteByMessage(receive));
			}
			return OperateResult.CreateSuccessResult(CimonHelper.PackErrorResponse(frameNo, 14));
		}

		private byte[] ReadByCommand(byte[] command)
		{
			int num = command[21] * 256 + command[22];
			string @string = Encoding.ASCII.GetString(command, 13, 1);
			string string2 = Encoding.ASCII.GetString(command, 15, 6);
			if (command[10] == 82)
			{
				List<byte> list = new List<byte>();
				list.AddRange(new byte[9]);
				OperateResult<byte[]> operateResult = Read(@string + string2, (ushort)num);
				if (operateResult.IsSuccess)
				{
					list.AddRange(operateResult.Content);
					return CimonHelper.PackEntireCommand(true, frameNo, command[10], list.ToArray());
				}
				return CimonHelper.PackErrorResponse(frameNo, 3);
			}
			if (command[10] == 114)
			{
				List<byte> list2 = new List<byte>();
				list2.AddRange(new byte[9]);
				OperateResult<bool[]> operateResult2 = ReadBool(@string + string2, (ushort)num);
				if (operateResult2.IsSuccess)
				{
					list2.AddRange(operateResult2.Content.Select((bool m) => (byte)(m ? 1 : 0)).ToArray());
				}
				return CimonHelper.PackEntireCommand(true, frameNo, command[10], list2.ToArray());
			}
			return CimonHelper.PackErrorResponse(frameNo, 14);
		}

		private byte[] WriteByMessage(byte[] command)
		{
			if (!base.EnableWrite)
			{
				return null;
			}
			string @string = Encoding.ASCII.GetString(command, 14, 1);
			string string2 = Encoding.ASCII.GetString(command, 16, 6);
			int length = command[22] * 256 + command[23];
			byte[] array = base.ByteTransform.TransByte(command, 24, length);
			if (command[10] == 87)
			{
				if (Write(@string + string2, array).IsSuccess)
				{
					return CimonHelper.PackEntireCommand(true, frameNo, command[10], new byte[9]);
				}
				return CimonHelper.PackErrorResponse(frameNo, 3);
			}
			if (command[10] == 119)
			{
				bool[] array2 = new bool[array.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array2[i] = array[i] != 0;
				}
				if (Write(@string + string2, array2).IsSuccess)
				{
					return CimonHelper.PackEntireCommand(true, frameNo, command[10], new byte[9]);
				}
				return CimonHelper.PackErrorResponse(frameNo, 3);
			}
			return CimonHelper.PackErrorResponse(frameNo, 14);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"CimonServer[{base.Port}]";
		}
	}
}
