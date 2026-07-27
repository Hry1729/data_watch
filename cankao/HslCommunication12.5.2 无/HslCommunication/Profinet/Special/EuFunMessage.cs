using System.IO;
using HslCommunication.Core.IMessage;

namespace HslCommunication.Profinet.Special
{
	internal class EuFunMessage : NetMessageBase, INetMessage
	{
		/// <inheritdoc cref="P:HslCommunication.Core.IMessage.INetMessage.ProtocolHeadBytesLength" />
		public int ProtocolHeadBytesLength => -1;

		/// <summary>
		/// 指定是否检查站号来实例化一个对象
		/// </summary>
		public EuFunMessage()
		{
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetContentLengthByHeadBytes" />
		public int GetContentLengthByHeadBytes()
		{
			return 0;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.CheckReceiveDataComplete(System.Byte[],System.IO.MemoryStream)" />
		public override bool CheckReceiveDataComplete(byte[] send, MemoryStream ms)
		{
			return EcFanMachine.CheckReceiveDataComplete(send, ms.ToArray());
		}
	}
}
