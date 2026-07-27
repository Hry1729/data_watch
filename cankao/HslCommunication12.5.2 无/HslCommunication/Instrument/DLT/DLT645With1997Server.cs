using HslCommunication.Instrument.DLT.Helper;

namespace HslCommunication.Instrument.DLT
{
	/// <summary>
	/// DLT645-1997协议的服务端实现类，主要用于与DLT645-1997设备进行通信。
	/// </summary>
	public class DLT645With1997Server : DLT645Server
	{
		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public DLT645With1997Server()
		{
		}

		/// <inheritdoc />
		protected override void CreateAddressTags()
		{
			AddDltTag("9010", "12345678", 2);
			AddDltTag("9020", "12345679", 2);
			AddDltTag("9110", "12345680", 2);
			AddDltTag("9120", "12345681", 2);
			AddDltTag("9130", "12345682", 2);
			AddDltTag("9140", "12345683", 2);
			AddDltTag("9150", "12345684", 2);
			AddDltTag("9160", "12345685", 2);
			AddDltAsciiTag("B210", "07021756", -1);
			AddDltTag("B212", "1234", 0);
			AddDltTag("B214", "123456", 0);
			AddDltTag("B310", "4567", 0);
			AddDltTag("B311", "4568", 0);
			AddDltTag("B312", "4569", 0);
			AddDltTag("B313", "4570", 0);
			AddDltTag("B320", "123456", 0);
			AddDltTag("B321", "123457", 0);
			AddDltTag("B322", "123458", 0);
			AddDltTag("B323", "123459", 0);
			AddDltTag("B611", "0231", 1);
			AddDltTag("B612", "0232", 1);
			AddDltTag("B613", "0233", 1);
			AddDltTag("B621", "1234", 2);
			AddDltTag("B622", "1334", 2);
			AddDltTag("B623", "1434", 2);
			AddDltTag("B630", "234567", 4);
			AddDltTag("B631", "234568", 4);
			AddDltTag("B632", "234569", 4);
			AddDltTag("B633", "234570", 4);
			AddDltTag("B640", "1234", 2);
			AddDltTag("B641", "1235", 2);
			AddDltTag("B642", "1236", 2);
			AddDltTag("B643", "1237", 2);
			AddDltTag("B650", "2213", 3);
			AddDltTag("B651", "2214", 3);
			AddDltTag("B652", "2215", 3);
			AddDltTag("B653", "2216", 3);
			AddDltAsciiTag("C032", "123456654321", -1);
			AddDltAsciiTag("C033", "123457754321", -1);
			AddDltAsciiTag("C034", "112233445566", -1);
		}

		/// <inheritdoc />
		protected override DLT645Type GetLT645Type()
		{
			return DLT645Type.DLT1997;
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromDLTCore(byte[] receive)
		{
			return base.ReadFromDLTCore(receive);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"DLT645With1997Server[{base.Port}]";
		}
	}
}
