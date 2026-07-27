using System.Text;
using System.Threading;
using HslCommunication.BasicFramework;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Secs.Helper;
using HslCommunication.Secs.Message;
using HslCommunication.Secs.Types;

namespace HslCommunication.Secs
{
	/// <summary>
	/// Secs Hsms的虚拟服务器，可以用来模拟Secs设备，等待客户端的连接，可以自定义响应客户端的数据<br />
	/// The virtual server of Secs Hsms can be used to simulate Secs devices, wait for the connection of the client, and can customize the response to the client's data
	/// </summary>
	/// <remarks>
	/// 本服务器类将会自动返回 S0F0 的消息，收到 S0F0 消息后，无需手动处理回复
	/// </remarks>
	/// <example>
	/// 下面就看看基本的操作内容
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Secs\SecsGemSample.cs" region="Server Sample" title="基本的使用" />
	/// 关于<see cref="T:HslCommunication.Secs.Types.SecsValue" />类型，可以非常灵活的实例化，参考下面的示例代码
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Secs\SecsGemSample.cs" region="Sample2" title="SecsValue说明" />
	/// </example>
	public class SecsHsmsServer : CommunicationServer
	{
		/// <summary>
		/// 当接收到来自客户的Secs信息时触发的对象<br />
		/// Object fired when Secs information from client is received
		/// </summary>
		/// <param name="sender">触发的服务器对象</param>
		/// <param name="session">消息的会话对象信息</param>
		/// <param name="message">实际的数据信息</param>
		public delegate void SecsMessageReceivedDelegate(object sender, PipeSession session, SecsMessage message);

		private Encoding stringEncoding = Encoding.Default;

		private SoftIncrementCount incrementCount;

		private ushort deviceId = 1;

		private object lockObject = new object();

		private AutoResetEvent autoResetEvent = new AutoResetEvent(false);

		private uint sendMessageId = 0u;

		private bool waitMessageBack = true;

		private SecsMessage readSecsMessage = null;

		/// <summary>
		/// 获取或设置用于字符串解析的编码信息<br />
		/// Obtain or set encoding information for string parsing
		/// </summary>
		public Encoding StringEncoding
		{
			get
			{
				return stringEncoding;
			}
			set
			{
				stringEncoding = value;
			}
		}

		/// <summary>
		/// 获取或设置当前服务器的默认的ID信息，在发布消息时将使用当前的值<br />
		/// Gets or sets the default ID information for the current server, and the current value will be used when publishing messages
		/// </summary>
		public ushort DeviceId
		{
			get
			{
				return deviceId;
			}
			set
			{
				deviceId = value;
			}
		}

		/// <summary>
		/// 接收到数据的时候就触发的事件，示例详细参考API文档信息<br />
		/// An event that is triggered when data is received
		/// </summary>
		/// <remarks>
		/// 事件共有三个参数，sender指服务器本地的对象，为 <see cref="T:HslCommunication.Secs.SecsHsmsServer" /> 对象，session 指会话对象，网为 <see cref="T:HslCommunication.Core.Net.AppSession" />，message 为收到的原始数据 <see cref="T:HslCommunication.Secs.Types.SecsMessage" /> 对象
		/// </remarks>
		/// <example>
		/// </example>
		public event SecsMessageReceivedDelegate OnSecsMessageReceived;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public SecsHsmsServer()
		{
			incrementCount = new SoftIncrementCount(4294967295L, 0L);
			base.OnPipeMessageReceived += SecsHsmsServer_OnPipeMessageReceived;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new SecsHsmsMessage();
		}

		private void SecsHsmsServer_OnPipeMessageReceived(PipeSession session, byte[] buffer)
		{
			SecsMessage secsMessage = new SecsMessage(buffer, 4);
			secsMessage.StringEncoding = stringEncoding;
			base.LogNet?.WriteDebug(ToString(), $"[{session.Communication}] {StringResources.Language.Receive}：{buffer.ToHexString(' ')}");
			if (secsMessage.StreamNo == 0 && secsMessage.FunctionNo == 0 && secsMessage.BlockNo % 2 == 1)
			{
				session.Communication.Send(Secs1.BuildHSMSMessage(ushort.MaxValue, 0, 0, (ushort)(secsMessage.BlockNo + 1), secsMessage.MessageID, null, false));
			}
			if (waitMessageBack && secsMessage.MessageID == sendMessageId && secsMessage.FunctionNo != 0 && secsMessage.FunctionNo % 2 == 0)
			{
				readSecsMessage = secsMessage;
				autoResetEvent.Set();
			}
			RaiseDataReceived(this, session, secsMessage);
		}

		/// <summary>
		/// 触发一个数据接收的事件信息<br />
		/// Event information that triggers a data reception
		/// </summary>
		/// <param name="source">数据的发送方</param>
		/// <param name="session">消息的会话对象信息</param>
		/// <param name="message">实际的数据信息</param>
		private void RaiseDataReceived(object source, PipeSession session, SecsMessage message)
		{
			this.OnSecsMessageReceived?.Invoke(source, session, message);
		}

		private OperateResult SendToCommunicationPipe(CommunicationPipe pipe, byte[] data)
		{
			base.LogNet?.WriteDebug(ToString(), $"[{pipe}] {StringResources.Language.Send}：{data.ToHexString(' ')}");
			return pipe.Send(data);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.SecsHsmsServer.SendByCommand(HslCommunication.Core.Net.PipeSession,HslCommunication.Secs.Types.SecsMessage,System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public OperateResult SendByCommand(PipeSession session, SecsMessage receiveMessage, byte stream, byte function, byte[] data)
		{
			return SendToCommunicationPipe(data: Secs1.BuildHSMSMessage(receiveMessage.DeviceID, stream, function, 0, receiveMessage.MessageID, data, false), pipe: session.Communication);
		}

		/// <summary>
		/// 向指定的会话信息发送SECS格式的原始字节数据信息，session 为当前的会话对象，<paramref name="receiveMessage" /> 为从客户端接收的数据，<paramref name="data" /> 是真实的返回数据<br />
		/// Send the original byte data information in SECS format to the specified session information. Session is the current session object, <paramref name="receiveMessage" /> is the data received from the client, and <paramref name="data" /> is the real returned data
		/// </summary>
		/// <param name="session">当前的会话对象</param>
		/// <param name="receiveMessage">接收到的Secs数据</param>
		/// <param name="stream">功能码1</param>
		/// <param name="function">功能码2</param>
		/// <param name="data">原始的字节数据，或是<see cref="T:HslCommunication.Secs.Types.SecsValue" />数据类</param>
		/// <param name="wBit">是否必须回复讯息</param>
		/// <returns>是否发送成功</returns>
		public OperateResult SendByCommand(PipeSession session, SecsMessage receiveMessage, byte stream, byte function, byte[] data, bool wBit)
		{
			return SendToCommunicationPipe(data: Secs1.BuildHSMSMessage(receiveMessage.DeviceID, stream, function, 0, receiveMessage.MessageID, data, wBit), pipe: session.Communication);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.SecsHsmsServer.SendByCommand(HslCommunication.Core.Net.PipeSession,HslCommunication.Secs.Types.SecsMessage,System.Byte,System.Byte,System.Byte[])" />
		public OperateResult SendByCommand(PipeSession session, SecsMessage receiveMessage, byte stream, byte function, SecsValue data)
		{
			return SendByCommand(session, receiveMessage, stream, function, (data == null) ? new byte[0] : data.ToSourceBytes(stringEncoding));
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.SecsHsmsServer.SendByCommand(HslCommunication.Core.Net.PipeSession,HslCommunication.Secs.Types.SecsMessage,System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public OperateResult SendByCommand(PipeSession session, SecsMessage receiveMessage, byte stream, byte function, SecsValue data, bool wBit)
		{
			return SendByCommand(session, receiveMessage, stream, function, (data == null) ? new byte[0] : data.ToSourceBytes(stringEncoding), wBit);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.SecsHsmsServer.PublishSecsMessage(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.Boolean)" />
		public OperateResult PublishSecsMessage(byte stream, byte function, SecsValue data)
		{
			return PublishSecsMessage(stream, function, data, false);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.SecsHsmsServer.PublishSecsMessage(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.UInt32,System.Boolean)" />
		public OperateResult PublishSecsMessage(byte stream, byte function, SecsValue data, bool wBit)
		{
			return PublishSecsMessage(stream, function, data, (uint)incrementCount.GetCurrentValue(), wBit);
		}

		/// <summary>
		/// 发布一个 <see cref="T:HslCommunication.Secs.Types.SecsValue" /> 数据到所有的在线客户端信息，返回是否发送成功<br />
		/// Publish a <see cref="T:HslCommunication.Secs.Types.SecsValue" /> data to all the online client information and return whether the sending was successful
		/// </summary>
		/// <param name="stream">功能码1</param>
		/// <param name="function">功能码2</param>
		/// <param name="data">数据对象</param>
		/// <param name="messageId">Secs协议的系统消息，也可以认为是消息ID</param>
		/// <param name="wBit">是否必须回复讯息</param>
		/// <returns>是否发送成功</returns>
		public OperateResult PublishSecsMessage(byte stream, byte function, SecsValue data, uint messageId, bool wBit)
		{
			if (data == null)
			{
				data = new SecsValue();
			}
			PipeSession[] pipeSessions = GetPipeSessions();
			for (int i = 0; i < pipeSessions.Length; i++)
			{
				OperateResult operateResult = SendToCommunicationPipe(data: Secs1.BuildHSMSMessage(deviceId, stream, function, 0, messageId, data.ToSourceBytes(stringEncoding), wBit), pipe: pipeSessions[i].Communication);
				if (!operateResult.IsSuccess)
				{
					return operateResult;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 发布一个 <see cref="T:HslCommunication.Secs.Types.SecsValue" /> 数据到指定的客户端信息，返回是否发送成功<br />
		/// Publish a <see cref="T:HslCommunication.Secs.Types.SecsValue" /> data to all the online client information and return whether the sending was successful
		/// </summary>
		/// <param name="session">会话信息</param>
		/// <param name="stream">功能码1</param>
		/// <param name="function">功能码2</param>
		/// <param name="data">数据对象</param>
		/// <param name="wBit">是否必须回复讯息</param>
		/// <returns>是否发送成功</returns>
		public OperateResult PublishSecsMessage(PipeSession session, byte stream, byte function, SecsValue data, bool wBit)
		{
			if (data == null)
			{
				data = new SecsValue();
			}
			return SendToCommunicationPipe(data: Secs1.BuildHSMSMessage(messageID: (uint)incrementCount.GetCurrentValue(), deviceID: deviceId, streamNo: stream, functionNo: function, blockNo: 0, data: data.ToSourceBytes(stringEncoding), wBit: wBit), pipe: session.Communication);
		}

		/// <summary>
		/// 以同步应答的方式从设备读取数据，设备收到本消息后，必须回复一个相同消息号的消息<br />
		/// Read data from the device in a synchronous response manner. After the device receives this message, it must reply with a message of the same message number
		/// </summary>
		/// <param name="pipe">会话信息</param>
		/// <param name="stream">功能码S</param>
		/// <param name="function">功能码F</param>
		/// <param name="data">数据内容</param>
		/// <param name="wBit">是否返回</param>
		/// <param name="timeout">超时时间，单位毫秒</param>
		/// <returns>读取结果信息</returns>
		public OperateResult<SecsMessage> ReadSecsMessage(PipeSession pipe, byte stream, byte function, SecsValue data, bool wBit, int timeout = 5000)
		{
			lock (lockObject)
			{
				uint messageID = (uint)incrementCount.GetCurrentValue();
				if (data == null)
				{
					data = new SecsValue();
				}
				byte[] data2 = Secs1.BuildHSMSMessage(deviceId, stream, function, 0, messageID, data.ToSourceBytes(stringEncoding), wBit);
				sendMessageId = messageID;
				waitMessageBack = true;
				OperateResult operateResult = SendToCommunicationPipe(pipe.Communication, data2);
				if (!operateResult.IsSuccess)
				{
					waitMessageBack = false;
					return OperateResult.CreateFailedResult<SecsMessage>(operateResult);
				}
				bool flag = autoResetEvent.WaitOne(timeout);
				waitMessageBack = false;
				if (flag)
				{
					return OperateResult.CreateSuccessResult(readSecsMessage);
				}
				return new OperateResult<SecsMessage>($"Read Timeout {timeout} ms");
			}
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"SecsHsmsServer[{base.Port}]";
		}
	}
}
