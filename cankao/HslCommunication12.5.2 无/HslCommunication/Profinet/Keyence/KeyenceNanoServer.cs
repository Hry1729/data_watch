using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Address;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Keyence
{
	/// <summary>
	/// 基恩士的上位链路协议的虚拟服务器
	/// </summary>
	public class KeyenceNanoServer : DeviceServer
	{
		private SoftBuffer rBuffer;

		private SoftBuffer bBuffer;

		private SoftBuffer mrBuffer;

		private SoftBuffer lrBuffer;

		private SoftBuffer crBuffer;

		private SoftBuffer vbBuffer;

		private SoftBuffer dmBuffer;

		private SoftBuffer emBuffer;

		private SoftBuffer wBuffer;

		private SoftBuffer atBuffer;

		private const int DataPoolLength = 65536;

		/// <summary>
		/// 实例化一个基于上位链路协议的虚拟的基恩士PLC对象，可以用来和<see cref="T:HslCommunication.Profinet.Keyence.KeyenceNanoSerialOverTcp" />进行通信测试。
		/// </summary>
		public KeyenceNanoServer()
		{
			rBuffer = new SoftBuffer(65536);
			bBuffer = new SoftBuffer(65536);
			mrBuffer = new SoftBuffer(65536);
			lrBuffer = new SoftBuffer(65536);
			crBuffer = new SoftBuffer(65536);
			vbBuffer = new SoftBuffer(65536);
			dmBuffer = new SoftBuffer(131072);
			emBuffer = new SoftBuffer(131072);
			wBuffer = new SoftBuffer(131072);
			atBuffer = new SoftBuffer(65536);
			base.ByteTransform = new RegularByteTransform();
			base.ByteTransform.IsStringReverseByteWord = true;
			base.WordLength = 1;
			LogMsgFormatBinary = false;
		}

		/// <inheritdoc />
		protected override byte[] SaveToBytes()
		{
			byte[] array = new byte[851968];
			rBuffer.GetBytes().CopyTo(array, 0);
			bBuffer.GetBytes().CopyTo(array, 65536);
			mrBuffer.GetBytes().CopyTo(array, 131072);
			lrBuffer.GetBytes().CopyTo(array, 196608);
			crBuffer.GetBytes().CopyTo(array, 262144);
			vbBuffer.GetBytes().CopyTo(array, 327680);
			dmBuffer.GetBytes().CopyTo(array, 393216);
			emBuffer.GetBytes().CopyTo(array, 524288);
			wBuffer.GetBytes().CopyTo(array, 655360);
			atBuffer.GetBytes().CopyTo(array, 786432);
			return array;
		}

		/// <inheritdoc />
		protected override void LoadFromBytes(byte[] content)
		{
			if (content.Length < 851968)
			{
				throw new Exception("File is not correct");
			}
			rBuffer.SetBytes(content, 0, 65536);
			bBuffer.SetBytes(content, 65536, 65536);
			mrBuffer.SetBytes(content, 131072, 65536);
			lrBuffer.SetBytes(content, 196608, 65536);
			crBuffer.SetBytes(content, 262144, 65536);
			vbBuffer.SetBytes(content, 327680, 65536);
			dmBuffer.SetBytes(content, 393216, 131072);
			emBuffer.SetBytes(content, 524288, 131072);
			wBuffer.SetBytes(content, 655360, 131072);
			atBuffer.SetBytes(content, 786432, 65536);
		}

		private byte[] TransBoolByteToBuffer(byte[] data)
		{
			bool[] array = new bool[data.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = data[i] != 0;
			}
			return array.ToByteArray();
		}

		private byte[] TransBufferToBoolByte(byte[] data)
		{
			bool[] array = data.ToBoolArray();
			byte[] array2 = new byte[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = (byte)(array[i] ? 1 : 0);
			}
			return array2;
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceNanoSerialOverTcp.Read(System.String,System.UInt16)" />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			try
			{
				if (address.StartsWith("DM"))
				{
					return OperateResult.CreateSuccessResult(dmBuffer.GetBytes(operateResult.Content.AddressStart * 2, length * 2));
				}
				if (address.StartsWith("EM"))
				{
					return OperateResult.CreateSuccessResult(emBuffer.GetBytes(operateResult.Content.AddressStart * 2, length * 2));
				}
				if (address.StartsWith("W"))
				{
					return OperateResult.CreateSuccessResult(wBuffer.GetBytes(operateResult.Content.AddressStart * 2, length * 2));
				}
				if (address.StartsWith("AT"))
				{
					return OperateResult.CreateSuccessResult(atBuffer.GetBytes(operateResult.Content.AddressStart * 4, length * 4));
				}
				if (address.StartsWith("MR"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(mrBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				if (address.StartsWith("LR"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(lrBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				if (address.StartsWith("CR"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(crBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				if (address.StartsWith("VB"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(vbBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				if (address.StartsWith("B"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(bBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				if (address.StartsWith("R"))
				{
					return OperateResult.CreateSuccessResult(TransBoolByteToBuffer(rBuffer.GetBytes(operateResult.Content.AddressStart, length * 16)));
				}
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType + " Reason:" + ex.Message);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceNanoSerialOverTcp.Write(System.String,System.Byte[])" />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(address, 0);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			try
			{
				if (address.StartsWith("DM"))
				{
					dmBuffer.SetBytes(value, operateResult.Content.AddressStart * 2);
				}
				else if (address.StartsWith("EM"))
				{
					emBuffer.SetBytes(value, operateResult.Content.AddressStart * 2);
				}
				else if (address.StartsWith("W"))
				{
					wBuffer.SetBytes(value, operateResult.Content.AddressStart * 2);
				}
				else if (address.StartsWith("AT"))
				{
					atBuffer.SetBytes(value, operateResult.Content.AddressStart * 4);
				}
				else if (address.StartsWith("MR"))
				{
					mrBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("LR"))
				{
					lrBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("CR"))
				{
					crBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("VB"))
				{
					vbBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("B"))
				{
					bBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				else
				{
					if (!address.StartsWith("R"))
					{
						return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
					}
					rBuffer.SetBytes(TransBufferToBoolByte(value), operateResult.Content.AddressStart);
				}
				return OperateResult.CreateSuccessResult();
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType + " Reason:" + ex.Message);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IReadWriteNet.ReadBool(System.String,System.UInt16)" />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(address, 0);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			try
			{
				if (address.StartsWith("R"))
				{
					return OperateResult.CreateSuccessResult((from m in rBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				if (address.StartsWith("B"))
				{
					return OperateResult.CreateSuccessResult((from m in bBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				if (address.StartsWith("MR"))
				{
					return OperateResult.CreateSuccessResult((from m in mrBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				if (address.StartsWith("LR"))
				{
					return OperateResult.CreateSuccessResult((from m in lrBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				if (address.StartsWith("CR"))
				{
					return OperateResult.CreateSuccessResult((from m in crBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				if (address.StartsWith("VB"))
				{
					return OperateResult.CreateSuccessResult((from m in vbBuffer.GetBytes(operateResult.Content.AddressStart, length)
						select m != 0).ToArray());
				}
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
			}
			catch (Exception ex)
			{
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType + " Reason:" + ex.Message);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IReadWriteNet.Write(System.String,System.Boolean[])" />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(address, 0);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			try
			{
				byte[] data = value.Select((bool m) => (byte)(m ? 1 : 0)).ToArray();
				if (address.StartsWith("R"))
				{
					rBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("B"))
				{
					bBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("MR"))
				{
					mrBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("LR"))
				{
					lrBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				else if (address.StartsWith("CR"))
				{
					crBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				else
				{
					if (!address.StartsWith("VB"))
					{
						return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
					}
					vbBuffer.SetBytes(data, operateResult.Content.AddressStart);
				}
				return OperateResult.CreateSuccessResult();
			}
			catch (Exception ex)
			{
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType + " Reason:" + ex.Message);
			}
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new SpecifiedCharacterMessage(13);
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			return OperateResult.CreateSuccessResult(ReadFromNanoCore(receive));
		}

		private byte[] GetBoolResponseData(byte[] data)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < data.Length; i++)
			{
				stringBuilder.Append(data[i]);
				if (i != data.Length - 1)
				{
					stringBuilder.Append(" ");
				}
			}
			stringBuilder.Append("\r\n");
			return Encoding.ASCII.GetBytes(stringBuilder.ToString());
		}

		private byte[] GetWordResponseData(byte[] data, string format = ".U")
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num = data.Length / 2;
			if (format == ".D" || format == ".L")
			{
				num = data.Length / 4;
			}
			for (int i = 0; i < num; i++)
			{
				switch (format)
				{
				case ".U":
					stringBuilder.Append(BitConverter.ToUInt16(data, i * 2).ToString("D5"));
					break;
				case ".S":
					stringBuilder.Append(BitConverter.ToInt16(data, i * 2));
					break;
				case ".D":
					stringBuilder.Append(BitConverter.ToUInt32(data, i * 4));
					break;
				case ".L":
					stringBuilder.Append(BitConverter.ToInt32(data, i * 4));
					break;
				default:
					stringBuilder.Append(BitConverter.ToUInt16(data, i * 2).ToString("X4"));
					break;
				}
				if (i != num - 1)
				{
					stringBuilder.Append(" ");
				}
			}
			stringBuilder.Append("\r\n");
			return Encoding.ASCII.GetBytes(stringBuilder.ToString());
		}

		private byte[] ReadFromNanoCore(byte[] receive)
		{
			string[] array = Encoding.ASCII.GetString(receive).Trim('\r', '\n').Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array[0] == "CR")
			{
				return Encoding.ASCII.GetBytes("CC\r\n");
			}
			if (array[0] == "CQ")
			{
				return Encoding.ASCII.GetBytes("CF\r\n");
			}
			if (array[0] == "ER")
			{
				return Encoding.ASCII.GetBytes("OK\r\n");
			}
			if (array[0] == "RD" || array[0] == "RDS")
			{
				return ReadByCommand(array);
			}
			if (array[0] == "WR" || array[0] == "WRS")
			{
				return WriteByCommand(array);
			}
			if (array[0] == "ST")
			{
				return WriteByCommand(new string[4]
				{
					"WRS",
					array[1],
					"1",
					"1"
				});
			}
			if (array[0] == "RS")
			{
				return WriteByCommand(new string[4]
				{
					"WRS",
					array[1],
					"1",
					"0"
				});
			}
			if (array[0] == "?K")
			{
				return Encoding.ASCII.GetBytes("53\r\n");
			}
			if (array[0] == "?M")
			{
				return Encoding.ASCII.GetBytes("1\r\n");
			}
			if (array[0] == "M0" || array[0] == "M1")
			{
				return Encoding.ASCII.GetBytes("OK\r\n");
			}
			if (array[0] == "WRT")
			{
				return Encoding.ASCII.GetBytes("OK\r\n");
			}
			return Encoding.ASCII.GetBytes("E0\r\n");
		}

		private byte[] ReadByCommand(string[] command)
		{
			try
			{
				string text = string.Empty;
				if (command[1].EndsWith(new string[5] { ".U", ".S", ".D", ".L", ".H" }))
				{
					text = command[1].Substring(command[1].Length - 2);
					command[1] = command[1].Remove(command[1].Length - 2);
				}
				int num = ((command.Length <= 2) ? 1 : int.Parse(command[2]));
				if (Regex.IsMatch(command[1], "^[0-9]+$"))
				{
					command[1] = "R" + command[1];
				}
				OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(command[1], (ushort)num);
				if (!operateResult.IsSuccess)
				{
					return Encoding.ASCII.GetBytes("E0\r\n");
				}
				KeyenceNanoAddress content = operateResult.Content;
				if (num > 1000)
				{
					return Encoding.ASCII.GetBytes("E0\r\n");
				}
				SoftBuffer softBuffer = null;
				switch (content.DataCode)
				{
				case "":
				case "R":
					softBuffer = rBuffer;
					break;
				case "B":
					softBuffer = bBuffer;
					break;
				case "MR":
					softBuffer = mrBuffer;
					break;
				case "LR":
					softBuffer = lrBuffer;
					break;
				case "CR":
					softBuffer = crBuffer;
					break;
				case "VB":
					softBuffer = vbBuffer;
					break;
				case "DM":
					return GetWordResponseData(dmBuffer.GetBytes(content.AddressStart * 2, num * 2), string.IsNullOrEmpty(text) ? ".U" : text);
				case "EM":
					return GetWordResponseData(emBuffer.GetBytes(content.AddressStart * 2, num * 2), string.IsNullOrEmpty(text) ? ".U" : text);
				case "W":
					return GetWordResponseData(wBuffer.GetBytes(content.AddressStart * 2, num * 2), string.IsNullOrEmpty(text) ? ".U" : text);
				case "AT":
					return GetWordResponseData(atBuffer.GetBytes(content.AddressStart * 4, num * 4), string.IsNullOrEmpty(text) ? ".D" : text);
				default:
					return Encoding.ASCII.GetBytes("E0\r\n");
				}
				if (string.IsNullOrEmpty(text))
				{
					return GetBoolResponseData(softBuffer.GetBytes(content.AddressStart, num));
				}
				return GetWordResponseData((from m in softBuffer.GetBytes(content.AddressStart, num * 16)
					select m != 0).ToArray().ToByteArray(), text);
			}
			catch
			{
				return Encoding.ASCII.GetBytes("E1\r\n");
			}
		}

		private byte[] WriteByCommand(string[] command)
		{
			if (!base.EnableWrite)
			{
				return Encoding.ASCII.GetBytes("E4\r\n");
			}
			try
			{
				string text = string.Empty;
				if (command[1].EndsWith(new string[5] { ".U", ".S", ".D", ".L", ".H" }))
				{
					text = command[1].Substring(command[1].Length - 2);
					command[1] = command[1].Remove(command[1].Length - 2);
				}
				int num = ((!(command[0] == "WRS")) ? 1 : int.Parse(command[2]));
				if (Regex.IsMatch(command[1], "^[0-9]+$"))
				{
					command[1] = "R" + command[1];
				}
				OperateResult<KeyenceNanoAddress> operateResult = KeyenceNanoAddress.ParseFrom(command[1], (ushort)num);
				if (!operateResult.IsSuccess)
				{
					return Encoding.ASCII.GetBytes("E0\r\n");
				}
				KeyenceNanoAddress content = operateResult.Content;
				if (num > 1000)
				{
					return Encoding.ASCII.GetBytes("E0\r\n");
				}
				if (command[1].StartsWith("R") || command[1].StartsWith("B") || command[1].StartsWith("MR") || command[1].StartsWith("LR") || command[1].StartsWith("CR") || command[1].StartsWith("VB"))
				{
					SoftBuffer softBuffer = null;
					if (command[1].StartsWith("R"))
					{
						softBuffer = rBuffer;
					}
					else if (command[1].StartsWith("B"))
					{
						softBuffer = bBuffer;
					}
					else if (command[1].StartsWith("MR"))
					{
						softBuffer = mrBuffer;
					}
					else if (command[1].StartsWith("LR"))
					{
						softBuffer = lrBuffer;
					}
					else if (command[1].StartsWith("CR"))
					{
						softBuffer = crBuffer;
					}
					else
					{
						if (!command[1].StartsWith("VB"))
						{
							return Encoding.ASCII.GetBytes("E0\r\n");
						}
						softBuffer = vbBuffer;
					}
					string[] source = command.RemoveBegin((command[0] == "WRS") ? 3 : 2);
					switch (text)
					{
					case ".U":
						softBuffer.SetBytes((from m in base.ByteTransform.TransByte(source.Select((string m) => ushort.Parse(m)).ToArray()).ToBoolArray()
							select (byte)(m ? 1 : 0)).ToArray(), content.AddressStart);
						break;
					case ".S":
						softBuffer.SetBytes((from m in base.ByteTransform.TransByte(source.Select((string m) => short.Parse(m)).ToArray()).ToBoolArray()
							select (byte)(m ? 1 : 0)).ToArray(), content.AddressStart);
						break;
					case ".D":
						softBuffer.SetBytes((from m in base.ByteTransform.TransByte(source.Select((string m) => uint.Parse(m)).ToArray()).ToBoolArray()
							select (byte)(m ? 1 : 0)).ToArray(), content.AddressStart);
						break;
					case ".L":
						softBuffer.SetBytes((from m in base.ByteTransform.TransByte(source.Select((string m) => int.Parse(m)).ToArray()).ToBoolArray()
							select (byte)(m ? 1 : 0)).ToArray(), content.AddressStart);
						break;
					case ".H":
						softBuffer.SetBytes((from m in base.ByteTransform.TransByte(source.Select((string m) => Convert.ToUInt16(m, 16)).ToArray()).ToBoolArray()
							select (byte)(m ? 1 : 0)).ToArray(), content.AddressStart);
						break;
					default:
						softBuffer.SetBytes(source.Select((string m) => byte.Parse(m)).ToArray(), content.AddressStart);
						break;
					}
				}
				else
				{
					byte[] data = base.ByteTransform.TransByte((from m in command.RemoveBegin((command[0] == "WRS") ? 3 : 2)
						select ushort.Parse(m)).ToArray());
					if (command[1].StartsWith("DM"))
					{
						dmBuffer.SetBytes(data, content.AddressStart * 2);
					}
					else if (command[1].StartsWith("EM"))
					{
						emBuffer.SetBytes(data, content.AddressStart * 2);
					}
					else if (command[1].StartsWith("W"))
					{
						wBuffer.SetBytes(data, content.AddressStart * 2);
					}
					else
					{
						if (!command[1].StartsWith("AT"))
						{
							return Encoding.ASCII.GetBytes("E0\r\n");
						}
						atBuffer.SetBytes(data, content.AddressStart * 4);
					}
				}
				return Encoding.ASCII.GetBytes("OK\r\n");
			}
			catch
			{
				return Encoding.ASCII.GetBytes("E1\r\n");
			}
		}

		/// <inheritdoc />
		protected override bool CheckSerialReceiveDataComplete(byte[] buffer, int receivedLength)
		{
			if (receivedLength < 1)
			{
				return false;
			}
			return buffer[receivedLength - 1] == 13;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"KeyenceNanoServer[{base.Port}]";
		}
	}
}
