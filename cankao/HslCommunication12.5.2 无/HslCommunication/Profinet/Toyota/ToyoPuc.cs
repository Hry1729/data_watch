using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Address;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Toyota
{
	/// <summary>
	/// 丰田工机的计算机链接协议实现，通过2PORT-EFR模块实现对PLC的读写数据操作，需要在PLC配置相关的以太网端口信息，才能进行通信。具体的地址参考DEMO程序信息。<br />
	/// Toyota Gongki's computer link protocol realizes the reading and writing data operation of the PLC through the 2PORT-EFR module, 
	/// and the relevant Ethernet port information needs to be configured in the PLC in order to communicate. For specific addresses, refer to Demo UI.
	/// </summary>
	/// <remarks>
	/// 感谢QQ：435416143 对本类的测试，时间：2023年5月2日<br />
	/// 位地址支持  K,V,T,C,L,X,Y,M,EK,EV,EC,ET,EL,EX,EY,EM,GX,GY,GM 地址，也可以指定prg参数，例如 prg=1;K000<br />
	/// 当然也可以使用字地址访问上述的位地址的，例如 M100~M10F 的位地址，就等于 M10 读字，也就是short。字地址额外支持：S,N,R,D,B,ES,EN,H,U,EB
	/// </remarks>
	public class ToyoPuc : DeviceTcpNet
	{
		private class WordAddress
		{
			public string Address { get; set; }

			public int BitIndex { get; set; }

			public ushort WordLength { get; set; }
		}

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public ToyoPuc()
		{
			base.ByteTransform = new RegularByteTransform();
			base.WordLength = 1;
		}

		/// <summary>
		/// 指定IP地址，端口号信息来实例化一个对象
		/// </summary>
		/// <param name="ipAddress">IP地址</param>
		/// <param name="port">端口号</param>
		public ToyoPuc(string ipAddress, int port)
			: this()
		{
			IpAddress = ipAddress;
			Port = port;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new ToyoPucMessage();
		}

		/// <inheritdoc />
		public override byte[] PackCommandWithHeader(byte[] command)
		{
			byte[] array = new byte[command.Length + 4];
			array[2] = BitConverter.GetBytes(command.Length)[0];
			array[3] = BitConverter.GetBytes(command.Length)[1];
			command.CopyTo(array, 4);
			return array;
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> UnpackResponseContent(byte[] send, byte[] response)
		{
			if (response == null || response.Length < 4)
			{
				return new OperateResult<byte[]>("Receive data too short: " + response.ToHexString(' '));
			}
			if (response[0] != 128)
			{
				return new OperateResult<byte[]>("FT check failed: " + response.ToHexString(' '));
			}
			if (response[1] != 0)
			{
				return new OperateResult<byte[]>((response.Length == 4) ? GetErrorText(response[1]) : GetErrorText(response[4]));
			}
			if (response.Length > 5)
			{
				return OperateResult.CreateSuccessResult(response.RemoveBegin(5));
			}
			return OperateResult.CreateSuccessResult(new byte[0]);
		}

		private static string GetErrorText(byte code)
		{
			switch (code)
			{
			case 17:
				return StringResources.Language.ToyoPuc11;
			case 32:
				return StringResources.Language.ToyoPuc20;
			case 33:
				return StringResources.Language.ToyoPuc21;
			case 35:
				return StringResources.Language.ToyoPuc23;
			case 36:
				return StringResources.Language.ToyoPuc24;
			case 37:
				return StringResources.Language.ToyoPuc25;
			case 52:
				return StringResources.Language.ToyoPuc34;
			case 62:
				return StringResources.Language.ToyoPuc3E;
			case 63:
				return StringResources.Language.ToyoPuc3F;
			case 64:
				return StringResources.Language.ToyoPuc40;
			case 65:
				return StringResources.Language.ToyoPuc41;
			default:
				return StringResources.Language.UnknownError;
			}
		}

		private OperateResult<WordAddress> ExtraWordAddress(string address, ushort length)
		{
			try
			{
				int num = 0;
				int num2 = address.IndexOf('.');
				if (num2 > 0)
				{
					num = Convert.ToInt32(address.Substring(num2 + 1), 16);
					address = address.Substring(0, num2);
				}
				else
				{
					num = Convert.ToInt32(address.Substring(address.Length - 1), 16);
					address = address.Substring(0, address.Length - 1);
					if (address.StartsWith("EK", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EV", StringComparison.OrdinalIgnoreCase) || address.StartsWith("ET", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EC", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EL", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EX", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EY", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EM", StringComparison.OrdinalIgnoreCase))
					{
						if (address.Length == 2)
						{
							address += "0";
						}
					}
					else if (address.Length == 1)
					{
						address += "0";
					}
				}
				int num3 = (num + length + 15) / 16;
				return OperateResult.CreateSuccessResult(new WordAddress
				{
					Address = address,
					BitIndex = num,
					WordLength = (ushort)num3
				});
			}
			catch (Exception ex)
			{
				return new OperateResult<WordAddress>("ExtraWordAddress failed: " + ex.Message);
			}
		}

		/// <inheritdoc />
		/// <remarks>
		/// 地址类型地址 K,V,T,C,L,X,Y,M,EK,EV,EC,ET,EL,EX,EY,EM 地址，也可以指定程序号(prg)参数，例如 prg=1;K000
		/// </remarks>
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			if (address.IndexOf(".") > 0)
			{
				return HslHelper.ReadBool(this, address, length);
			}
			OperateResult<WordAddress> operateResult = ExtraWordAddress(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = Read(operateResult.Content.Address, operateResult.Content.WordLength);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult2);
			}
			return OperateResult.CreateSuccessResult(operateResult2.Content.ToBoolArray().SelectMiddle(operateResult.Content.BitIndex, length));
		}

		/// <inheritdoc />
		/// <remarks>
		/// 地址类型地址 K,V,T,C,L,X,Y,M,EK,EV,EC,ET,EL,EX,EY,EM 地址，也可以指定程序号(prg)参数，例如 prg=1;K000
		/// </remarks>
		[HslMqttApi("WriteBool", "")]
		public override OperateResult Write(string address, bool value)
		{
			if (Regex.IsMatch(address, "prg=[0-9+];", RegexOptions.IgnoreCase))
			{
				return HslHelper.WriteBool(this, address, new bool[1] { value }, 16, false, true);
			}
			if (address.IndexOf('.') > 0)
			{
				return HslHelper.WriteBool(this, address, new bool[1] { value });
			}
			OperateResult<byte[]> operateResult = BuildWriteBoolCommand(address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return ReadFromCoreServer(operateResult.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IReadWriteNet.Write(System.String,System.Boolean[])" />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			return HslHelper.WriteBool(this, address, value, 16, false, true);
		}

		/// <inheritdoc />
		/// <remarks>
		/// 地址类型地址 K,V,T,C,L,X,Y,M,EK,EV,EC,ET,EL,EX,EY,EM,S,N,R,D,B,ES,EN,H,U,EB地址，也可以指定程序号(prg)参数，例如 prg=1;K000
		/// </remarks>
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<byte[]> operateResult = BuildReadWordCommand(address, length);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return ReadFromCoreServer(operateResult.Content);
		}

		/// <inheritdoc />
		/// <remarks>
		/// 地址类型地址 K,V,T,C,L,X,Y,M,EK,EV,EC,ET,EL,EX,EY,EM,S,N,R,D,B,ES,EN,H,U,EB地址，也可以指定程序号(prg)参数，例如 prg=1;K000
		/// </remarks>
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			OperateResult<byte[]> operateResult = BuildWriteWordCommand(address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return ReadFromCoreServer(operateResult.Content);
		}

		/// <summary>
		/// 随机读取字地址的数据，可以一次性读取多个地址的数据，返回原始字节类型，需要自己做一些二次分析操作<br />
		/// Random read word address data, you can read multiple address data at one time, return the original byte type, you need to do some secondary analysis operations
		/// </summary>
		/// <param name="address">地址数组信息</param>
		/// <returns>读取成功的结果对象，原始字节数据信息</returns>
		[HslMqttApi("ReadRandom", "")]
		public OperateResult<byte[]> ReadRandom(string[] address)
		{
			OperateResult<List<byte[]>> operateResult = BuildReadRandomCommand(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			return ReadFromCoreServer(operateResult.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Toyota.ToyoPuc.ReadBool(System.String,System.UInt16)" />
		public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
		{
			OperateResult<WordAddress> analysis = ExtraWordAddress(address, length);
			if (!analysis.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(analysis);
			}
			OperateResult<byte[]> read = await ReadAsync(analysis.Content.Address, analysis.Content.WordLength);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(read);
			}
			return OperateResult.CreateSuccessResult(read.Content.ToBoolArray().SelectMiddle(analysis.Content.BitIndex, length));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Toyota.ToyoPuc.Write(System.String,System.Boolean)" />
		public override async Task<OperateResult> WriteAsync(string address, bool value)
		{
			if (Regex.IsMatch(address, "prg=[0-9+];", RegexOptions.IgnoreCase))
			{
				return await HslHelper.WriteBoolAsync(this, address, new bool[1] { value }, 16, false, true);
			}
			if (address.IndexOf('.') > 0)
			{
				return await HslHelper.WriteBoolAsync(this, address, new bool[1] { value });
			}
			OperateResult<byte[]> command = BuildWriteBoolCommand(address, value);
			if (!command.IsSuccess)
			{
				return command;
			}
			return await ReadFromCoreServerAsync(command.Content);
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, bool[] value)
		{
			return await HslHelper.WriteBoolAsync(this, address, value, 16, false, true);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Toyota.ToyoPuc.Read(System.String,System.UInt16)" />
		public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
		{
			OperateResult<byte[]> command = BuildReadWordCommand(address, length);
			if (!command.IsSuccess)
			{
				return command;
			}
			return await ReadFromCoreServerAsync(command.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Toyota.ToyoPuc.Write(System.String,System.Byte[])" />
		public override async Task<OperateResult> WriteAsync(string address, byte[] value)
		{
			OperateResult<byte[]> command = BuildWriteWordCommand(address, value);
			if (!command.IsSuccess)
			{
				return command;
			}
			return await ReadFromCoreServerAsync(command.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Toyota.ToyoPuc.ReadRandom(System.String[])" />
		public async Task<OperateResult<byte[]>> ReadRandomAsync(string[] address)
		{
			OperateResult<List<byte[]>> command = BuildReadRandomCommand(address);
			if (!command.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(command);
			}
			return await ReadFromCoreServerAsync(command.Content);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"ToyoPuc[{IpAddress}:{Port}]";
		}

		private static OperateResult<byte[]> BuildReadBoolCommand(string address)
		{
			OperateResult<ToyoPucAddress> operateResult = ToyoPucAddress.ParseFrom(address, 1, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			ToyoPucAddress content = operateResult.Content;
			if (content.PRG >= 0)
			{
				return new OperateResult<byte[]>();
			}
			return OperateResult.CreateSuccessResult(new byte[3]
			{
				32,
				BitConverter.GetBytes(content.AddressStart)[0],
				BitConverter.GetBytes(content.AddressStart)[1]
			});
		}

		private static OperateResult<byte[]> BuildReadWordCommand(string address, ushort length)
		{
			OperateResult<ToyoPucAddress> operateResult = ToyoPucAddress.ParseFrom(address, length, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			ToyoPucAddress content = operateResult.Content;
			if (content.PRG >= 0)
			{
				return OperateResult.CreateSuccessResult(new byte[6]
				{
					148,
					(byte)content.PRG,
					BitConverter.GetBytes(content.AddressStart)[0],
					BitConverter.GetBytes(content.AddressStart)[1],
					BitConverter.GetBytes(length)[0],
					BitConverter.GetBytes(length)[1]
				});
			}
			return OperateResult.CreateSuccessResult(new byte[5]
			{
				28,
				BitConverter.GetBytes(content.AddressStart)[0],
				BitConverter.GetBytes(content.AddressStart)[1],
				BitConverter.GetBytes(length)[0],
				BitConverter.GetBytes(length)[1]
			});
		}

		private static OperateResult<byte[]> BuildWriteWordCommand(string address, byte[] value)
		{
			OperateResult<ToyoPucAddress> operateResult = ToyoPucAddress.ParseFrom(address, 1, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			ToyoPucAddress content = operateResult.Content;
			if (content.PRG >= 0)
			{
				byte[] array = new byte[4 + value.Length];
				array[0] = 149;
				array[1] = (byte)content.PRG;
				array[2] = BitConverter.GetBytes(content.AddressStart)[0];
				array[3] = BitConverter.GetBytes(content.AddressStart)[1];
				value.CopyTo(array, 4);
				return OperateResult.CreateSuccessResult(array);
			}
			byte[] array2 = new byte[3 + value.Length];
			array2[0] = 29;
			array2[1] = BitConverter.GetBytes(content.AddressStart)[0];
			array2[2] = BitConverter.GetBytes(content.AddressStart)[1];
			value.CopyTo(array2, 3);
			return OperateResult.CreateSuccessResult(array2);
		}

		private static OperateResult<byte[]> BuildWriteBoolCommand(string address, bool value)
		{
			OperateResult<ToyoPucAddress> operateResult = ToyoPucAddress.ParseFrom(address, 1, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			if (!operateResult.Content.WriteBitEnabled)
			{
				return new OperateResult<byte[]>("Address: " + address + "  is not support write bit, because it is word address");
			}
			ToyoPucAddress content = operateResult.Content;
			if (content.PRG >= 0)
			{
				return new OperateResult<byte[]>("Not supported prg write bool");
			}
			byte[] array = new byte[4]
			{
				33,
				BitConverter.GetBytes(content.AddressStart)[0],
				BitConverter.GetBytes(content.AddressStart)[1],
				0
			};
			if (value)
			{
				array[3] = 1;
			}
			return OperateResult.CreateSuccessResult(array);
		}

		private static OperateResult<List<byte[]>> BuildReadRandomCommand(string[] address)
		{
			List<string[]> list = SoftBasic.ArraySplitByLength(address, 80);
			List<byte[]> list2 = new List<byte[]>();
			for (int i = 0; i < list.Count; i++)
			{
				string[] array = list[i];
				byte[] array2 = new byte[1 + array.Length * 2];
				array2[0] = 34;
				for (int j = 0; j < array.Length; j++)
				{
					OperateResult<ToyoPucAddress> operateResult = ToyoPucAddress.ParseFrom(array[j], 1, false);
					if (!operateResult.IsSuccess)
					{
						return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
					}
					ToyoPucAddress content = operateResult.Content;
					array2[j * 2 + 1] = BitConverter.GetBytes(content.AddressStart)[0];
					array2[j * 2 + 2] = BitConverter.GetBytes(content.AddressStart)[1];
				}
				list2.Add(array2);
			}
			return OperateResult.CreateSuccessResult(list2);
		}
	}
}
