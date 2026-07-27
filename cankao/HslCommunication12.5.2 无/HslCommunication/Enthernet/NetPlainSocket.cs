using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HslCommunication.Core;
using HslCommunication.Core.Net;

namespace HslCommunication.Enthernet
{
	/// <summary>
	/// 一个基于字节流的Socket通讯类，支持自动重连功能，接收数据以字符串或是字节数组的形式返回<br />
	/// A byte stream-based Socket communication class that supports automatic reconnection functionality, and the received data is returned in the form of strings or byte arrays
	/// </summary>
	public class NetPlainSocket : NetworkXBase
	{
		/// <inheritdoc cref="P:HslCommunication.Core.Net.TcpNetCommunication.ConnectTimeOut" />
		public int ConnectTimeOut = 5000;

		private Encoding encoding;

		private object connectLock = new object();

		private string ipAddress = "127.0.0.1";

		private int port = 10000;

		private int bufferLength = 2048;

		private byte[] buffer = null;

		private AsyncCallback socketAsyncCallBack = null;

		private WaitCallback reConnectCallBack = null;

		private int reconnectTick = 0;

		/// <summary>
		/// 获取或设置当前的字符串编码格式<br />
		/// Get or set the current string encoding format
		/// </summary>
		public Encoding Encoding
		{
			get
			{
				return encoding;
			}
			set
			{
				encoding = value;
			}
		}

		/// <summary>
		/// 获取或设置当前的缓存区的大小信息，默认2048字节<br />
		/// Get or set the size information of the current cache, with a default of 2048 bytes
		/// </summary>
		public int SocketBufferLength
		{
			get
			{
				return bufferLength;
			}
			set
			{
				if (value <= 0)
				{
					value = 2048;
				}
				bufferLength = value;
				buffer = new byte[bufferLength];
			}
		}

		/// <summary>
		/// 当接收到字符串时候的触发事件<br />
		/// The trigger event when a string is received
		/// </summary>
		public event Action<string> ReceivedString;

		/// <summary>
		/// 当接收到字节数据时候的触发事件<br />
		/// Triggered when byte data is received
		/// </summary>
		public event Action<byte[]> ReceivedData;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public NetPlainSocket()
		{
			buffer = new byte[bufferLength];
			encoding = Encoding.UTF8;
			socketAsyncCallBack = ReceiveCallBack;
			reConnectCallBack = ReConnectServer;
		}

		/// <summary>
		/// 使用指定的ip地址和端口号来实例化这个对象
		/// </summary>
		/// <param name="ipAddress">Ip地址</param>
		/// <param name="port">端口号</param>
		public NetPlainSocket(string ipAddress, int port)
			: this()
		{
			this.ipAddress = ipAddress;
			this.port = port;
		}

		/// <summary>
		/// 连接到远程服务器，返回是否连接成功<br />
		/// Connect to the remote server and return whether the connection was successful
		/// </summary>
		/// <returns>返回是否连接成功</returns>
		public OperateResult ConnectServer()
		{
			CoreSocket?.Close();
			OperateResult<Socket> operateResult = CreateSocketAndConnect(ipAddress, port, ConnectTimeOut);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			try
			{
				CoreSocket = operateResult.Content;
				CoreSocket.BeginReceive(buffer, 0, bufferLength, SocketFlags.None, socketAsyncCallBack, CoreSocket);
				return OperateResult.CreateSuccessResult();
			}
			catch (Exception ex)
			{
				return new OperateResult(ex.Message);
			}
		}

		/// <summary>
		/// 关闭当前的连接对象<br />
		/// Close the current connection object
		/// </summary>
		/// <returns>错误信息</returns>
		public OperateResult ConnectClose()
		{
			try
			{
				CoreSocket?.Close();
				return OperateResult.CreateSuccessResult();
			}
			catch (Exception ex)
			{
				return new OperateResult(ex.Message);
			}
		}

		/// <summary>
		/// 发送字符串到服务器上去，编码使用的是属性 <see cref="P:HslCommunication.Enthernet.NetPlainSocket.Encoding" /> 来指定<br />
		/// Send the string to the server, and the Encoding is specified using the attribute <see cref="P:HslCommunication.Enthernet.NetPlainSocket.Encoding" />
		/// </summary>
		/// <param name="text">文本信息</param>
		/// <returns>发送是否成功</returns>
		public OperateResult SendString(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return OperateResult.CreateSuccessResult();
			}
			return Send(CoreSocket, encoding.GetBytes(text));
		}

		/// <summary>
		/// 发送字节数据到服务器上去<br />
		/// Send byte data to the server
		/// </summary>
		/// <param name="data">二进制的字节数组</param>
		/// <returns>是否发送成功</returns>
		public OperateResult SendData(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				return OperateResult.CreateSuccessResult();
			}
			return Send(CoreSocket, data);
		}

		private void ReceiveCallBack(IAsyncResult ar)
		{
			if (!(ar.AsyncState is Socket socket))
			{
				return;
			}
			byte[] array = null;
			try
			{
				int num = socket.EndReceive(ar);
				if (num == 0)
				{
					CoreSocket?.Close();
					return;
				}
				array = new byte[num];
				Array.Copy(buffer, 0, array, 0, num);
				socket.BeginReceive(buffer, 0, bufferLength, SocketFlags.None, socketAsyncCallBack, socket);
			}
			catch (ObjectDisposedException)
			{
			}
			catch (Exception ex2)
			{
				base.LogNet?.WriteWarn(StringResources.Language.SocketContentReceiveException + ":" + ex2.Message);
				ThreadPool.QueueUserWorkItem(reConnectCallBack, null);
			}
			if (array != null)
			{
				this.ReceivedData?.Invoke(array);
				if (this.ReceivedString != null)
				{
					this.ReceivedString?.Invoke(encoding.GetString(array));
				}
			}
		}

		/// <summary>
		/// 重新连接服务器的委托方法<br />
		/// </summary>
		/// <param name="obj">无用的对象</param>
		private void ReConnectServer(object obj)
		{
			base.LogNet?.WriteWarn(StringResources.Language.ReConnectServerAfterTenSeconds);
			for (int i = 0; i < 10; i++)
			{
				HslHelper.ThreadSleep(1000);
				base.LogNet?.WriteWarn($"Wait for connecting server after {9 - i} seconds");
			}
			if (Interlocked.CompareExchange(ref reconnectTick, 1, 0) != 0)
			{
				return;
			}
			OperateResult<Socket> operateResult = CreateSocketAndConnect(ipAddress, port, ConnectTimeOut);
			if (!operateResult.IsSuccess)
			{
				ThreadPool.QueueUserWorkItem(reConnectCallBack, obj);
				Interlocked.Exchange(ref reconnectTick, 0);
				return;
			}
			lock (connectLock)
			{
				try
				{
					CoreSocket?.Close();
					CoreSocket = operateResult.Content;
					CoreSocket.BeginReceive(buffer, 0, bufferLength, SocketFlags.None, socketAsyncCallBack, CoreSocket);
					base.LogNet?.WriteWarn(StringResources.Language.ReConnectServerSuccess);
				}
				catch (Exception ex)
				{
					base.LogNet?.WriteWarn(StringResources.Language.RemoteClosedConnection + ":" + ex.Message);
					ThreadPool.QueueUserWorkItem(reConnectCallBack, obj);
				}
				finally
				{
					Interlocked.Exchange(ref reconnectTick, 0);
				}
			}
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"NetPlainSocket[{ipAddress}:{port}]";
		}
	}
}
