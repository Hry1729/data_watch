using System.IO;
using System.Net.Sockets;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Siemens
{
	/// <summary>
	/// S7-Plus协议专用的数据流
	/// </summary>
	internal class S7PlusStream : NetworkStream
	{
		private SiemensS7Plus siemens;

		private MemoryStream ms = new MemoryStream();

		private Socket socket;

		/// <summary>
		/// 使用指定的<see cref="T:System.Net.Sockets.Socket" />对象，是否所属权来实例化一个对象
		/// </summary>
		/// <param name="socket">套接字对象</param>
		/// <param name="ownsSocket">是否所属权</param>
		/// <param name="siemens">当前的PLC对象</param>
		public S7PlusStream(Socket socket, bool ownsSocket, SiemensS7Plus siemens)
			: base(socket, ownsSocket)
		{
			this.socket = socket;
			this.siemens = siemens;
		}

		/// <inheritdoc />
		public override void Write(byte[] buffer, int offset, int size)
		{
			byte[] array = null;
			if (buffer[0] == 22 && buffer[5] == 1)
			{
				buffer[2] = 1;
			}
			array = ((offset != 0 || buffer.Length != size) ? SiemensS7Plus.BuildWithTPKTAndISO(buffer.SelectMiddle(offset, size)) : SiemensS7Plus.BuildWithTPKTAndISO(buffer));
			if (array != null)
			{
				base.Write(array, 0, array.Length);
			}
		}

		/// <inheritdoc />
		public override int Read(byte[] buffer, int offset, int size)
		{
			if (ms.Position >= ms.Length)
			{
				byte[] array = NetSupport.ReadBytesFromSocket(socket, 7);
				int num = array[2] * 256 + array[3];
				ms = new MemoryStream(num);
				if (num > 0)
				{
					ms.Write(NetSupport.ReadBytesFromSocket(socket, num), 0, num);
				}
			}
			return ms.Read(buffer, offset, size);
		}
	}
}
