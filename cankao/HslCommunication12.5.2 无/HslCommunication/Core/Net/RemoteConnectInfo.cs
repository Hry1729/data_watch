using System.Net;
using System.Text;

namespace HslCommunication.Core.Net
{
	/// <summary>
	/// DTU远程连接的一些信息
	/// </summary>
	public class RemoteConnectInfo
	{
		private bool ack = false;

		private string dtuId = string.Empty;

		/// <summary>
		/// 远程端口号信息<br />
		/// Remote port number information
		/// </summary>
		public IPEndPoint EndPoint { get; set; }

		/// <summary>
		/// 会话信息<br />
		/// Session information
		/// </summary>
		public PipeSession Session { get; set; }

		/// <summary>
		/// DTU的注册包<br />
		/// Registration package for DTU
		/// </summary>
		public byte[] DtuBytes { get; set; }

		/// <summary>
		/// 是否需要返回注册结果的报文<br />
		/// Whether to return the registration result packet
		/// </summary>
		public bool NeedAckResult => ack;

		/// <summary>
		/// 如果使用的是 HSL 格式的DTU消息的话，就是传入的 dtuid 信息
		/// </summary>
		public string DtuId => dtuId;

		/// <summary>
		/// 当前DTU会话的状态信息，如果需要关闭当前的会话并退出重连，设置本属性为 <see cref="F:HslCommunication.Core.Net.DtuStatus.Closed" /> 即可
		/// </summary>
		public DtuStatus Status { get; set; } = DtuStatus.Create;

		/// <summary>
		/// 实例化一个远程连接信息对象，指定自定义的远程 DTU 注册包
		/// </summary>
		/// <param name="ipAddress">IP地址</param>
		/// <param name="port">端口号</param>
		/// <param name="dtu">dtu的注册包</param>
		public RemoteConnectInfo(string ipAddress, int port, byte[] dtu)
		{
			EndPoint = new IPEndPoint(IPAddress.Parse(HslHelper.GetIpAddressFromInput(ipAddress)), port);
			DtuBytes = dtu;
		}

		/// <summary>
		/// 实例化一个远程连接信息对象，传入 IP，Port 来创建符合 HSL 标准的 DTU 注册包
		/// </summary>
		/// <param name="ipAddress">IP地址</param>
		/// <param name="port">端口号</param>
		/// <param name="dtuId">DTU信息</param>
		/// <param name="password">密码</param>
		/// <param name="needAckResult">是否需要返回注册结果</param>
		public RemoteConnectInfo(string ipAddress, int port, string dtuId, string password = "", bool needAckResult = true)
		{
			this.dtuId = dtuId;
			EndPoint = new IPEndPoint(IPAddress.Parse(HslHelper.GetIpAddressFromInput(ipAddress)), port);
			DtuBytes = CreateHslAlienMessage(dtuId, password, needAckResult);
			ack = needAckResult;
		}

		private byte[] CreateHslAlienMessage(string dtuId, string password, bool needAckResult)
		{
			if (dtuId.Length > 11)
			{
				dtuId = dtuId.Substring(11);
			}
			byte[] array = new byte[30]
			{
				72, 83, 76, 0, 25, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			};
			if (dtuId.Length > 11)
			{
				dtuId = dtuId.Substring(0, 11);
			}
			Encoding.ASCII.GetBytes(dtuId).CopyTo(array, 5);
			if (!string.IsNullOrEmpty(password))
			{
				if (password.Length > 6)
				{
					password = password.Substring(6);
				}
				Encoding.ASCII.GetBytes(password).CopyTo(array, 16);
			}
			if (!needAckResult)
			{
				array[28] = 1;
			}
			return array;
		}

		/// <summary>
		/// 关闭当前的 DTU 连接<br />
		/// Close the current DTU connection
		/// </summary>
		public void Close()
		{
			Status = DtuStatus.Closed;
			Session?.Close();
		}
	}
}
