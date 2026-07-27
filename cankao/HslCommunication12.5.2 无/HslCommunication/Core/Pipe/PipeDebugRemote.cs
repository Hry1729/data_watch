using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HslCommunication.Core.IMessage;

namespace HslCommunication.Core.Pipe
{
	/// <summary>
	/// 用于调试的远程管道信息<br />
	/// Remote pipeline information for debugging
	/// </summary>
	/// <remarks>
	/// 当前的管道主要用于调试，DEMO界面测试时可以直接连接远程的Debug服务器，方便调试，屏蔽了握手报文
	/// </remarks>
	public class PipeDebugRemote : PipeTcpNet
	{
		/// <summary>
		/// 获取或设置客户端的key信息，用于调试服务器区分不同的客户端，这样就支持多个客户端连接到同一个服务器，默认是0<br />
		/// Get or set the client's key information, used to distinguish different clients in the debug server, so that multiple clients can connect to the same server, the default is 0
		/// </summary>
		public ushort ClientKey { get; set; } = 0;

		/// <inheritdoc cref="M:HslCommunication.Core.Pipe.PipeSslNet.#ctor(System.Boolean)" />
		public PipeDebugRemote()
		{
		}

		/// <inheritdoc cref="M:HslCommunication.Core.Pipe.PipeSslNet.#ctor(System.String,System.Int32,System.Boolean)" />
		public PipeDebugRemote(string ipAddress, int port)
		{
			base.IpAddress = ipAddress;
			base.Port = port;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.Pipe.PipeSslNet.#ctor(System.Net.Sockets.Socket,System.Net.IPEndPoint,System.Boolean)" />
		public PipeDebugRemote(Socket socket, IPEndPoint iPEndPoint)
		{
			base.Socket = socket;
			base.IpAddress = iPEndPoint.Address.ToString();
			base.Port = iPEndPoint.Port;
		}

		private byte[] CreateHeaderBytes(byte[] sendValue)
		{
			byte[] array = new byte[6];
			BitConverter.GetBytes(ClientKey).CopyTo(array, 0);
			BitConverter.GetBytes(sendValue.Length).CopyTo(array, 2);
			return array;
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> ReadFromCoreServer(INetMessage netMessage, byte[] sendValue, bool hasResponseData, Action<byte[]> logMessage = null)
		{
			Send(CreateHeaderBytes(sendValue));
			return base.ReadFromCoreServer(netMessage, sendValue, hasResponseData, logMessage);
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReadFromCoreServerAsync(INetMessage netMessage, byte[] sendValue, bool hasResponseData, Action<byte[]> logMessage = null)
		{
			await SendAsync(CreateHeaderBytes(sendValue));
			return await base.ReadFromCoreServerAsync(netMessage, sendValue, hasResponseData, logMessage);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"PipeTcpNet[{base.IpAddress}:{base.Port}]";
		}
	}
}
