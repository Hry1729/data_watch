using System;
using System.Threading.Tasks;
using HslCommunication.Core;
using HslCommunication.Instrument.DLT.Helper;

namespace HslCommunication.Instrument.DLT
{
	/// <summary>
	/// 698.45协议的TCP通信类(不是串口透传通信)，面向对象的用电信息数据交换协议，使用明文的通信方式。支持读取功率，总功，电压，电流，频率，功率因数等数据。<br />
	/// The TCP communication class of the 698.45 protocol (not the serial port transparent transmission communication), the object-oriented power consumption information data exchange protocol, 
	/// uses the clear text communication method. Support reading power, total power, voltage, current, frequency, power factor and other data.
	/// </summary>
	/// <remarks>
	/// <inheritdoc cref="T:HslCommunication.Instrument.DLT.DLT698" path="remarks" />
	/// </remarks>
	/// <example>
	/// <inheritdoc cref="T:HslCommunication.Instrument.DLT.DLT698" path="example" />
	/// </example>
	public class DLT698TcpNet : DLT698OverTcp, IDlt698, IReadWriteDevice, IReadWriteNet
	{
		/// <inheritdoc cref="M:HslCommunication.Core.Net.BinaryCommunication.#ctor" />
		public DLT698TcpNet()
		{
		}

		/// <summary>
		/// 指定地址域，密码，操作者代码来实例化一个对象，密码及操作者代码在写入操作的时候进行验证<br />
		/// Specify the address field, password, and operator code to instantiate an object, and the password and operator code are validated during write operations, 
		/// which address field is a 12-character BCD code, for example: 149100007290
		/// </summary>
		/// <param name="station">设备的地址信息，通常是一个12字符的BCD码</param>
		public DLT698TcpNet(string station = "AAAAAAAAAAAA")
			: base(station)
		{
		}

		/// <inheritdoc cref="M:HslCommunication.Instrument.DLT.DLT698OverTcp.#ctor(System.String,System.Int32,System.String)" />
		public DLT698TcpNet(string ipAddress, int port, string station = "AAAAAAAAAAAA")
			: base(ipAddress, port, station)
		{
		}

		/// <inheritdoc />
		protected override OperateResult InitializationOnConnect()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(CommunicationPipe, DLT698Helper.BuildEntireCommand(129, base.Station, base.CA, CreateLoginApdu(1, 0, 0)).Content, true, true);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(CommunicationPipe, DLT698Helper.BuildEntireCommand(129, base.Station, base.CA, CreateConnectApdu()).Content, true, true);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return base.InitializationOnConnect();
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> InitializationOnConnectAsync()
		{
			OperateResult<byte[]> read1 = await ReadFromCoreServerAsync(CommunicationPipe, DLT698Helper.BuildEntireCommand(129, base.Station, base.CA, CreateLoginApdu(1, 0, 0)).Content, true, true);
			if (!read1.IsSuccess)
			{
				return read1;
			}
			OperateResult<byte[]> read2 = await ReadFromCoreServerAsync(CommunicationPipe, DLT698Helper.BuildEntireCommand(129, base.Station, base.CA, CreateConnectApdu()).Content, true, true);
			if (!read2.IsSuccess)
			{
				return read2;
			}
			return await base.InitializationOnConnectAsync();
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"DLT698TcpNet[{IpAddress}:{Port}]";
		}

		internal static byte[] CreateLoginApdu(byte services = 1, byte piid = 0, byte type = 0)
		{
			byte[] array = new byte[15]
			{
				services, piid, type, 0, 132, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0
			};
			DLT698Helper.SetDltDataTime(array, 5, DateTime.Now);
			return array;
		}

		internal static byte[] CreateConnectApdu()
		{
			return "02 00 00 10 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 04 00 04 00 01 04 00 00 00 00 64 00 00".ToHexBytes();
		}
	}
}
