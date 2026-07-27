using System.IO;
using HslCommunication.Instrument.DLT.Helper;

namespace HslCommunication.Core.IMessage
{
	/// <summary>
	/// DLT 645协议的串口透传的消息类
	/// </summary>
	public class DLT645Message : NetMessageBase, INetMessage
	{
		private bool checkDataId = true;

		/// <inheritdoc cref="P:HslCommunication.Core.IMessage.INetMessage.ProtocolHeadBytesLength" />
		public int ProtocolHeadBytesLength => 10;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public DLT645Message()
		{
		}

		/// <summary>
		/// 根据是否需要检查数据标识，实例化一个对象<br />
		/// Instantiate an object based on whether it is necessary to check the data identifier
		/// </summary>
		/// <param name="checkDataId"></param>
		public DLT645Message(bool checkDataId)
		{
			this.checkDataId = checkDataId;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetContentLengthByHeadBytes" />
		public int GetContentLengthByHeadBytes()
		{
			return base.HeadBytes[9] + 2;
		}

		/// <inheritdoc />
		public override int PependedUselesByteLength(byte[] headByte)
		{
			int num = DLT645Helper.FindHeadCode68H(headByte);
			if (num < 0)
			{
				return 10;
			}
			return num;
		}

		/// <inheritdoc />s
		public override int CheckMessageMatch(byte[] send, byte[] receive)
		{
			if (!checkDataId)
			{
				return base.CheckMessageMatch(send, receive);
			}
			if (send == null || receive == null || send.Length < 16 || receive.Length < 16)
			{
				return base.CheckMessageMatch(send, receive);
			}
			int num = DLT645Helper.FindHeadCode68H(send);
			int num2 = DLT645Helper.FindHeadCode68H(receive);
			if (num < 0 || num2 < 0 || num > 4 || num2 > 4)
			{
				return base.CheckMessageMatch(send, receive);
			}
			if (send[num + 8] == 17 && receive[num2 + 8] == 145)
			{
				for (int i = 0; i < 4; i++)
				{
					if (num + 10 + i >= send.Length)
					{
						return 1;
					}
					if (num2 + 10 + i >= receive.Length)
					{
						return 1;
					}
					if (send[num + 10 + i] != receive[num2 + 10 + i])
					{
						return -1;
					}
				}
				return 1;
			}
			if (send[num + 8] == 1 && receive[num2 + 8] == 129)
			{
				for (int j = 0; j < 2; j++)
				{
					if (num + 10 + j >= send.Length)
					{
						return 1;
					}
					if (num2 + 10 + j >= receive.Length)
					{
						return 1;
					}
					if (send[num + 10 + j] != receive[num2 + 10 + j])
					{
						return -1;
					}
				}
				return 1;
			}
			return base.CheckMessageMatch(send, receive);
		}

		/// <inheritdoc />
		public override bool CheckReceiveDataComplete(byte[] send, MemoryStream ms)
		{
			return DLT645Helper.CheckReceiveDataComplete(ms);
		}
	}
}
