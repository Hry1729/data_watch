using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;

namespace HslCommunication.WebSocket
{
	/// <summary>
	/// WebSocket协议的实现，支持创建自定义的websocket服务器，直接给其他的网页端，客户端，手机端发送数据信息，详细看api文档说明<br />
	/// The implementation of the WebSocket protocol supports the creation of custom websocket servers and sends data information directly to other web pages, clients, and mobile phones. See the API documentation for details.
	/// </summary>
	/// <example>
	/// 使用本组件库可以非常简单方便的构造属于你自己的websocket服务器，从而实现和其他的客户端进行通信，尤其是和网页进行通讯，
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample1" title="简单的实例化" />
	/// 当客户端发送数据给服务器的时候，会发一个事件，并且把当前的会话暴露出来，下面举例打印消息，并且演示一个例子，发送数据给指定的会话。
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample2" title="接触数据" />
	/// 也可以在其他地方发送数据给所有的客户端，只要调用一个方法就可以了。
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample3" title="发送数据" />
	/// 当客户端上线之后也触发了当前的事件，我们可以手动捕获到
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample4" title="捕获上线事件" />
	/// 我们再来看看一个高级的操作，实现订阅，大多数的情况，websocket被设计成了订阅发布的操作。基本本服务器可以扩展出非常复杂功能的系统，我们来看一种最简单的操作。
	/// <br />
	/// 客户端给服务器发的数据都视为主题(topic)，这样服务器就可以辨认出主题信息，并追加主题。如下这么操作。
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample5" title="订阅实现" />
	/// 然后在发布的时候，调用下面的代码。
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\WebSocket\WebSocketServerSample.cs" region="Sample6" title="发布数据" />
	/// 可以看到，我们这里只有订阅操作，如果想要实现更为复杂的操作怎么办？丰富客户端发来的数据，携带命令，数据，就可以区分了。比如json数据。具体的实现需要看各位能力了。
	/// </example>
	public class WebSocketServer : CommunicationTcpServer, IDisposable
	{
		/// <summary>
		/// websocket的消息收到委托<br />
		/// websocket message received delegate
		/// </summary>
		/// <param name="session">当前的会话对象</param>
		/// <param name="message">websocket的消息</param>
		public delegate void OnClientApplicationMessageReceiveDelegate(WebSocketSession session, WebSocketMessage message);

		/// <summary>
		/// 当前websocket连接上服务器的事件委托<br />
		/// Event delegation of the server on the current websocket connection
		/// </summary>
		/// <param name="session">当前的会话对象</param>
		public delegate void OnClientConnectedDelegate(WebSocketSession session);

		private readonly Dictionary<string, string> retainKeys;

		private readonly object keysLock;

		private bool isRetain = true;

		private readonly List<WebSocketSession> wsSessions = new List<WebSocketSession>();

		private readonly object sessionsLock = new object();

		private Timer timerHeart;

		private bool disposedValue;

		private bool topicWildcard = false;

		private AsyncCallback beginReceiveCallback = null;

		/// <summary>
		/// 获取当前的在线的客户端数量<br />
		/// Get the current number of online clients
		/// </summary>
		public int OnlineCount => wsSessions.Count;

		/// <inheritdoc cref="P:HslCommunication.MQTT.MqttServer.TopicWildcard" />
		public bool TopicWildcard
		{
			get
			{
				return topicWildcard;
			}
			set
			{
				topicWildcard = value;
			}
		}

		/// <summary>
		/// 获取或设置当前的服务器是否对订阅主题信息缓存，方便订阅客户端立即收到结果，默认开启<br />
		/// Gets or sets whether the current server caches the topic information of the subscription, so that the subscription client can receive the results immediately. It is enabled by default.
		/// </summary>
		public bool IsTopicRetain
		{
			get
			{
				return isRetain;
			}
			set
			{
				isRetain = value;
			}
		}

		/// <summary>
		/// 获取当前的在线的客户端信息，可以用于额外的分析或是显示。
		/// </summary>
		public WebSocketSession[] OnlineSessions
		{
			get
			{
				WebSocketSession[] result = null;
				lock (sessionsLock)
				{
					result = wsSessions.ToArray();
				}
				return result;
			}
		}

		/// <summary>
		/// 设置的参数，最小单位为1s，当超过设置的时间间隔必须回复PONG报文，否则服务器认定为掉线。默认120秒<br />
		/// Set the minimum unit of the parameter is 1s. When the set time interval is exceeded, the PONG packet must be returned, otherwise the server considers it to be offline. 120 seconds by default
		/// </summary>
		/// <remarks>
		/// 保持连接（Keep Alive）是一个以秒为单位的时间间隔，它是指客户端返回一个PONG报文到下一次返回PONG报文的时候，
		/// 两者之间允许空闲的最大时间间隔。客户端负责保证控制报文发送的时间间隔不超过保持连接的值。
		/// </remarks>
		public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromSeconds(120.0);

		/// <summary>
		/// 获取或是设置用于保持连接的心跳时间的发送间隔。默认30秒钟，需要在服务启动之前设置<br />
		/// Gets or sets the sending interval of the heartbeat time used to keep the connection. 30 seconds by default, need to be set before the service starts
		/// </summary>
		public TimeSpan KeepAliveSendInterval { get; set; } = TimeSpan.FromSeconds(30.0);

		/// <summary>
		/// websocket的消息收到时触发<br />
		/// Triggered when a websocket message is received
		///             </summary>
		public event OnClientApplicationMessageReceiveDelegate OnClientApplicationMessageReceive;

		/// <summary>
		/// Websocket的客户端连接上来时触发<br />
		/// Triggered when a Websocket client connects
		/// </summary>
		public event OnClientConnectedDelegate OnClientConnected;

		/// <summary>
		/// Websocket的客户端下线时触发<br />
		/// Triggered when Websocket client connects
		/// </summary>
		public event OnClientConnectedDelegate OnClientDisConnected;

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		public WebSocketServer()
		{
			beginReceiveCallback = ReceiveCallback;
			retainKeys = new Dictionary<string, string>();
			keysLock = new object();
		}

		/// <inheritdoc />
		protected override void ExtraOnStart()
		{
			base.ExtraOnStart();
			if (KeepAliveSendInterval.TotalMilliseconds > 0.0 && timerHeart == null)
			{
				timerHeart = new Timer(ThreadTimerHeartCheck, null, 2000, (int)KeepAliveSendInterval.TotalMilliseconds);
			}
		}

		/// <inheritdoc />
		protected override void ExtraOnClose()
		{
			base.ExtraOnClose();
			CleanWsSession();
		}

		private void ThreadTimerHeartCheck(object obj)
		{
			WebSocketSession[] array = null;
			lock (sessionsLock)
			{
				array = wsSessions.ToArray();
			}
			if (array == null || array.Length == 0)
			{
				return;
			}
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i].IsQASession)
				{
					if (DateTime.Now - array[i].HeartTime > KeepAlivePeriod)
					{
						RemoveAndCloseSession(array[i], "Heart check timeout[" + SoftBasic.GetTimeSpanDescription(DateTime.Now - array[i].HeartTime) + "]");
					}
					else
					{
						SendWebsocket(array[i], WebSocketHelper.WebScoketPackData(9, false, "Heart Check"));
					}
				}
			}
		}

		/// <inheritdoc />
		protected override void ThreadPoolLogin(PipeTcpNet pipe, IPEndPoint endPoint)
		{
			HandleWebsocketConnection(pipe, endPoint);
		}

		private async void ReceiveCallback(IAsyncResult ar)
		{
			if (ar.AsyncState is WebSocketSession session)
			{
				PipeTcpNet pipe = (PipeTcpNet)session.Communication;
				try
				{
					pipe.Socket.EndReceive(ar);
				}
				catch (Exception ex)
				{
					session.Close();
					base.LogNet?.WriteDebug(ToString(), "ReceiveCallback Failed:" + ex.Message);
					RemoveAndCloseSession(session);
					return;
				}
				HandleWebsocketMessage(session, await WebSocketHelper.ReceiveWebSocketPayloadAsync(pipe));
			}
		}

		private OperateResult SendWebsocket(WebSocketSession session, byte[] data)
		{
			return session.Communication.Send(data);
		}

		private async Task<OperateResult> SendWebsocketAsync(WebSocketSession session, byte[] data)
		{
			return await session.Communication.SendAsync(data).ConfigureAwait(false);
		}

		private void HandleWebsocketConnection(PipeTcpNet pipe, IPEndPoint endPoint)
		{
			WebSocketSession webSocketSession = new WebSocketSession
			{
				HeartTime = DateTime.Now,
				Remote = endPoint,
				Communication = pipe
			};
			OperateResult<byte[]> operateResult = webSocketSession.Communication.Receive(-1, 5000);
			if (!operateResult.IsSuccess)
			{
				return;
			}
			string @string = Encoding.UTF8.GetString(operateResult.Content);
			OperateResult operateResult2 = WebSocketHelper.CheckWebSocketLegality(@string);
			if (!operateResult2.IsSuccess)
			{
				pipe?.CloseCommunication();
				base.LogNet?.WriteDebug(ToString(), $"[{endPoint}] WebScoket Check Failed:" + operateResult2.Message + Environment.NewLine + @string);
				return;
			}
			OperateResult<byte[]> response = WebSocketHelper.GetResponse(@string);
			if (!response.IsSuccess)
			{
				pipe?.CloseCommunication();
				base.LogNet?.WriteDebug(ToString(), $"[{endPoint}] GetResponse Failed:" + response.Message);
			}
			else
			{
				if (!SendWebsocket(webSocketSession, response.Content).IsSuccess)
				{
					return;
				}
				webSocketSession.IsQASession = @string.Contains("HslRequestAndAnswer: true") || @string.Contains("HslRequestAndAnswer:true");
				Match match = Regex.Match(@string, "GET [\\S\\s]+ HTTP/1", RegexOptions.IgnoreCase);
				if (match.Success)
				{
					webSocketSession.Url = match.Value.Substring(4, match.Value.Length - 11);
				}
				try
				{
					string[] array = WebSocketHelper.GetWebSocketSubscribes(@string);
					if (array == null)
					{
						array = WebSocketHelper.GetWebSocketSubscribesFromUrl(webSocketSession.Url);
					}
					if (array != null)
					{
						webSocketSession.Topics = new List<string>(array);
						if (isRetain)
						{
							lock (keysLock)
							{
								if (TopicWildcard)
								{
									foreach (KeyValuePair<string, string> retainKey in retainKeys)
									{
										if (webSocketSession.IsClientSubscribe(retainKey.Key, TopicWildcard) && !SendWebsocket(webSocketSession, WebSocketHelper.WebScoketPackData(1, false, retainKey.Value)).IsSuccess)
										{
											return;
										}
									}
								}
								else
								{
									for (int i = 0; i < webSocketSession.Topics.Count; i++)
									{
										if (retainKeys.ContainsKey(webSocketSession.Topics[i]) && !SendWebsocket(webSocketSession, WebSocketHelper.WebScoketPackData(1, false, retainKeys[webSocketSession.Topics[i]])).IsSuccess)
										{
											return;
										}
									}
								}
							}
						}
					}
					pipe.Socket.BeginReceive(new byte[0], 0, 0, SocketFlags.None, beginReceiveCallback, webSocketSession);
					AddWsSession(webSocketSession);
				}
				catch (Exception ex)
				{
					pipe?.CloseCommunication();
					base.LogNet?.WriteDebug(ToString(), $"[{webSocketSession.Remote}] BeginReceive Failed: {ex.Message}");
					return;
				}
				this.OnClientConnected?.Invoke(webSocketSession);
			}
		}

		private void HandleWebsocketMessage(WebSocketSession session, OperateResult<WebSocketMessage> read)
		{
			if (!read.IsSuccess)
			{
				RemoveAndCloseSession(session);
				return;
			}
			session.HeartTime = DateTime.Now;
			if (read.Content.OpCode == 8)
			{
				session.Close();
				RemoveAndCloseSession(session, Encoding.UTF8.GetString(read.Content.Payload));
				return;
			}
			if (read.Content.OpCode == 9)
			{
				base.LogNet?.WriteDebug(ToString(), $"[{session.Remote}] PING: {read.Content}");
				OperateResult operateResult = SendWebsocket(session, WebSocketHelper.WebScoketPackData(10, false, read.Content.Payload));
				if (!operateResult.IsSuccess)
				{
					RemoveAndCloseSession(session, "HandleWebsocketMessage -> 09 opCode send back exception -> " + operateResult.Message);
					return;
				}
			}
			else if (read.Content.OpCode == 10)
			{
				base.LogNet?.WriteDebug(ToString(), $"[{session.Remote}] PONG: {read.Content}");
			}
			else
			{
				this.OnClientApplicationMessageReceive?.Invoke(session, read.Content);
			}
			try
			{
				(session.Communication as PipeTcpNet).Socket.BeginReceive(new byte[0], 0, 0, SocketFlags.None, beginReceiveCallback, session);
			}
			catch (Exception ex)
			{
				session.Close();
				RemoveAndCloseSession(session, "BeginReceive Exception -> " + ex.Message);
			}
		}

		private void PublishSessionList(IEnumerable<WebSocketSession> sessions, string payload)
		{
			foreach (WebSocketSession session in sessions)
			{
				OperateResult operateResult = SendWebsocket(session, WebSocketHelper.WebScoketPackData(1, false, payload));
				if (!operateResult.IsSuccess)
				{
					base.LogNet?.WriteError(ToString(), $"[{session.Remote}] Send Failed: {operateResult.Message}");
				}
			}
		}

		private void PublishSessionList(IEnumerable<WebSocketSession> sessions, byte[] payload)
		{
			foreach (WebSocketSession session in sessions)
			{
				OperateResult operateResult = SendWebsocket(session, WebSocketHelper.WebScoketPackData(2, false, payload));
				if (!operateResult.IsSuccess)
				{
					base.LogNet?.WriteError(ToString(), $"[{session.Remote}] Send Failed: {operateResult.Message}");
				}
			}
		}

		/// <summary>
		/// 向所有的客户端强制发送消息，消息类型为文本消息，也就是 opCode = 1<br />
		/// Force the sending of messages to all clients, with the message type being text messages, that is, opCode = 1
		/// </summary>
		/// <param name="payload">消息内容</param>
		public void PublishAllClientPayload(string payload)
		{
			List<WebSocketSession> list = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession)
					{
						list.Add(wsSessions[i]);
					}
				}
			}
			PublishSessionList(list, payload);
		}

		/// <inheritdoc cref="M:HslCommunication.WebSocket.WebSocketServer.PublishClientPayload(System.String,System.String,System.Boolean)" />
		public void PublishClientPayload(string topic, string payload)
		{
			PublishClientPayload(topic, payload, isRetain);
		}

		/// <summary>
		/// 向所有的客户端强制发送消息，消息类型为二进制消息，也就是 opCode = 2<br />
		/// Force the sending of messages to all clients, with the message type being binary messages, that is, opCode = 2
		/// </summary>
		/// <param name="payload">消息内容</param>
		public void PublishAllClientPayload(byte[] payload)
		{
			List<WebSocketSession> list = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession)
					{
						list.Add(wsSessions[i]);
					}
				}
			}
			PublishSessionList(list, payload);
		}

		/// <summary>
		/// 向订阅了topic主题的客户端发送二进制的消息，注意：二进制数据无法设置为消息驻留<br />
		/// Send binary messages to the clients that have subscribed to the topic. Note: Binary data cannot be set for message residence
		/// </summary>
		/// <param name="topic">主题</param>
		/// <param name="payload">二进制的数据消息</param>
		public void PublishClientPayload(string topic, byte[] payload)
		{
			List<WebSocketSession> list = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession && wsSessions[i].IsClientSubscribe(topic, topicWildcard))
					{
						list.Add(wsSessions[i]);
					}
				}
			}
			PublishSessionList(list, payload);
		}

		/// <summary>
		/// 向所有的客户端强制发送二进制数据，这个二进制数据是一个完整的websocket数据包，可以调用方法 <see cref="M:HslCommunication.WebSocket.WebSocketHelper.WebScoketPackData(System.Int32,System.Boolean,System.Byte[])" />来构建一个数据包<br />
		/// Force the sending of binary data to all clients. This binary data is a complete websocket data packet Can call methods <see cref="M:HslCommunication.WebSocket.WebSocketHelper.WebScoketPackData(System.Int32,System.Boolean,System.Byte[])" /> to build a data packet
		/// </summary>
		/// <param name="binary">原始的二进制数据</param>
		public void PublishClientBinary(byte[] binary)
		{
			for (int i = 0; i < wsSessions.Count; i++)
			{
				OperateResult operateResult = SendWebsocket(wsSessions[i], binary);
				if (!operateResult.IsSuccess)
				{
					base.LogNet?.WriteError(ToString(), $"[{wsSessions[i].Remote}] Send Failed: {operateResult.Message}");
				}
			}
		}

		/// <summary>
		/// 向订阅了topic主题的客户端发送消息<br />
		/// Send messages to clients subscribed to topic
		/// </summary>
		/// <param name="topic">主题</param>
		/// <param name="payload">消息内容</param>
		/// <param name="retain">指定当前的主题是否驻留</param>
		public void PublishClientPayload(string topic, string payload, bool retain)
		{
			List<WebSocketSession> list = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession && wsSessions[i].IsClientSubscribe(topic, topicWildcard))
					{
						list.Add(wsSessions[i]);
					}
				}
			}
			PublishSessionList(list, payload);
			if (retain)
			{
				AddTopicRetain(topic, payload);
			}
		}

		private async Task PublishSessionListAsync(List<WebSocketSession> sessions, string payload)
		{
			for (int i = 0; i < sessions.Count; i++)
			{
				OperateResult send = await SendWebsocketAsync(sessions[i], WebSocketHelper.WebScoketPackData(1, false, payload));
				if (!send.IsSuccess)
				{
					base.LogNet?.WriteError(ToString(), $"[{sessions[i].Remote}] Send Failed: {send.Message}");
				}
			}
		}

		/// <inheritdoc cref="M:HslCommunication.WebSocket.WebSocketServer.PublishAllClientPayload(System.String)" />
		public async Task PublishAllClientPayloadAsync(string payload)
		{
			List<WebSocketSession> sessions = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession)
					{
						sessions.Add(wsSessions[i]);
					}
				}
			}
			await PublishSessionListAsync(sessions, payload);
		}

		/// <inheritdoc cref="M:HslCommunication.WebSocket.WebSocketServer.PublishClientPayload(System.String,System.String,System.Boolean)" />
		public async Task PublishClientPayloadAsync(string topic, string payload)
		{
			await PublishClientPayloadAsync(topic, payload, isRetain);
		}

		/// <inheritdoc cref="M:HslCommunication.WebSocket.WebSocketServer.PublishClientPayload(System.String,System.String,System.Boolean)" />
		public async Task PublishClientPayloadAsync(string topic, string payload, bool retain)
		{
			List<WebSocketSession> sessions = new List<WebSocketSession>();
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					if (!wsSessions[i].IsQASession && wsSessions[i].IsClientSubscribe(topic, topicWildcard))
					{
						sessions.Add(wsSessions[i]);
					}
				}
			}
			await PublishSessionListAsync(sessions, payload);
			if (retain)
			{
				AddTopicRetain(topic, payload);
			}
		}

		/// <summary>
		/// 向指定的客户端发送数据<br />
		/// Send data to the specified client
		/// </summary>
		/// <param name="session">会话内容</param>
		/// <param name="payload">消息内容</param>
		public void SendClientPayload(WebSocketSession session, string payload)
		{
			SendWebsocket(session, WebSocketHelper.WebScoketPackData(1, false, payload));
		}

		/// <summary>
		/// 向指定的客户端发送数据<br />
		/// Send data to the specified client
		/// </summary>
		/// <param name="session">会话内容</param>
		/// <param name="payload">消息内容</param>
		public void SendClientPayload(WebSocketSession session, byte[] payload)
		{
			SendWebsocket(session, WebSocketHelper.WebScoketPackData(1, false, payload));
		}

		/// <summary>
		/// 给一个当前的会话信息动态添加订阅的主题<br />
		/// Dynamically add subscribed topics to a current session message
		/// </summary>
		/// <param name="session">会话内容</param>
		/// <param name="topic">主题信息</param>
		public void AddSessionTopic(WebSocketSession session, string topic)
		{
			session.AddTopic(topic);
			PublishSessionTopic(session, topic);
		}

		private void CleanWsSession()
		{
			lock (sessionsLock)
			{
				for (int i = 0; i < wsSessions.Count; i++)
				{
					wsSessions[i].Close();
				}
				wsSessions.Clear();
			}
		}

		private void AddWsSession(WebSocketSession session)
		{
			lock (sessionsLock)
			{
				wsSessions.Add(session);
			}
			base.LogNet?.WriteDebug(ToString(), $"Client[{session.Remote}] Online");
		}

		/// <summary>
		/// 让Websocket客户端正常下线，调用本方法即可自由控制会话客户端强制下线操作。<br />
		/// Let the Websocket client go offline normally. Call this method to freely control the session client to force offline operation.
		/// </summary>
		/// <param name="session">当前的会话信息</param>
		/// <param name="reason">下线的原因，默认为空</param>
		public void RemoveAndCloseSession(WebSocketSession session, string reason = null)
		{
			lock (sessionsLock)
			{
				wsSessions.Remove(session);
			}
			session.Close();
			base.LogNet?.WriteDebug(ToString(), $"Client[{session.Remote}]  Offline {reason}");
			this.OnClientDisConnected?.Invoke(session);
		}

		private void AddTopicRetain(string topic, string payload)
		{
			lock (keysLock)
			{
				if (retainKeys.ContainsKey(topic))
				{
					retainKeys[topic] = payload;
				}
				else
				{
					retainKeys.Add(topic, payload);
				}
			}
		}

		private void PublishSessionTopic(WebSocketSession session, string topic)
		{
			bool flag = false;
			string message = string.Empty;
			lock (keysLock)
			{
				if (retainKeys.ContainsKey(topic))
				{
					flag = true;
					message = retainKeys[topic];
				}
			}
			if (flag)
			{
				SendWebsocket(session, WebSocketHelper.WebScoketPackData(1, false, message));
			}
		}

		/// <summary>
		/// 释放当前的对象
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.OnClientApplicationMessageReceive = null;
					this.OnClientConnected = null;
					this.OnClientDisConnected = null;
				}
				disposedValue = true;
			}
		}

		/// <inheritdoc cref="M:System.IDisposable.Dispose" />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"WebSocketServer[{base.Port}]";
		}
	}
}
