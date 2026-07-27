using System;
using System.Text;

namespace HslCommunication.Core.IMessage
{
	/// <summary>
	/// 异形消息对象，用于异形客户端的注册包接收以及验证使用
	/// </summary>
	public class AlienMessage : NetMessageBase, INetMessage
	{
		/// <inheritdoc cref="P:HslCommunication.Core.IMessage.INetMessage.ProtocolHeadBytesLength" />
		public int ProtocolHeadBytesLength => 5;

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.CheckHeadBytesLegal(System.Byte[])" />
		public override bool CheckHeadBytesLegal(byte[] token)
		{
			if (base.HeadBytes == null)
			{
				return true;
			}
			if (base.HeadBytes[0] == 72)
			{
				return true;
			}
			return false;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetContentLengthByHeadBytes" />
		public int GetContentLengthByHeadBytes()
		{
			if (base.HeadBytes[3] >= 48 && base.HeadBytes[3] <= 70 && base.HeadBytes[4] >= 48 && base.HeadBytes[4] <= 70)
			{
				return Convert.ToInt32(Encoding.ASCII.GetString(base.HeadBytes, 3, 2), 16);
			}
			return base.HeadBytes[3] * 256 + base.HeadBytes[4];
		}
	}
}
