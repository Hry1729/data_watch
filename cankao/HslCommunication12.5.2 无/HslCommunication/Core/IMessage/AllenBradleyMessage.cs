using System;

namespace HslCommunication.Core.IMessage
{
	/// <summary>
	/// 用于和 AllenBradley PLC 交互的消息协议类
	/// </summary>
	public class AllenBradleyMessage : NetMessageBase, INetMessage
	{
		private bool contextCheck = false;

		/// <inheritdoc cref="P:HslCommunication.Core.IMessage.INetMessage.ProtocolHeadBytesLength" />
		public int ProtocolHeadBytesLength => 24;

		/// <summary>
		/// 实例化一个默认的 <see cref="T:HslCommunication.Core.IMessage.AllenBradleyMessage" /> 对象
		/// </summary>
		public AllenBradleyMessage()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:HslCommunication.Core.IMessage.AllenBradleyMessage" /> class.
		/// </summary>
		/// <param name="contextCheck">A value indicating whether the message should perform a context check. If <see langword="true" />, the message will
		/// validate its context before processing.</param>
		public AllenBradleyMessage(bool contextCheck)
		{
			this.contextCheck = contextCheck;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetContentLengthByHeadBytes" />
		public int GetContentLengthByHeadBytes()
		{
			return BitConverter.ToUInt16(base.HeadBytes, 2);
		}

		/// <inheritdoc />
		public override int CheckMessageMatch(byte[] send, byte[] receive)
		{
			if (receive == null || receive.Length < 24 || send == null || send.Length < 24)
			{
				return 1;
			}
			if (contextCheck)
			{
				for (int i = 12; i < 20; i++)
				{
					if (receive[i] != send[i])
					{
						return -1;
					}
				}
			}
			return base.CheckMessageMatch(send, receive);
		}
	}
}
