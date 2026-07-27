using System.Threading;

namespace HslCommunication.Instrument.IEC.Helper
{
	/// <summary>
	/// IEC会话的额外信息，主要包含了这个会话的收发消息ID
	/// </summary>
	public class IECSessionInfo
	{
		private int sendMessageID = 0;

		private int recvMessageID = 0;

		/// <summary>
		/// 当前接收的消息ID
		/// </summary>
		public int RecvMessageID => recvMessageID;

		/// <summary>
		/// 获取发送的消息ID信息，并进行自增操作
		/// </summary>
		/// <returns></returns>
		public int GetSendMessageID()
		{
			int result = sendMessageID;
			Interlocked.Increment(ref sendMessageID);
			return result;
		}

		/// <summary>
		/// 自增一个接收到的消息ID
		/// </summary>
		/// <returns></returns>
		public int IncrRecvMessageID()
		{
			int result = Interlocked.Increment(ref recvMessageID);
			if (recvMessageID > 32767)
			{
				recvMessageID = 0;
			}
			return result;
		}
	}
}
