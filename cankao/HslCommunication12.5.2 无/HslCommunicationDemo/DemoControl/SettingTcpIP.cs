using System.ComponentModel;
using HslCommunication.Core.Pipe;

namespace HslCommunicationDemo.DemoControl
{
	public class SettingTcpIP
	{
		[Description("在发送数据到PLC后，接收数据之前等待的时间。\r\nAfter sending data to PLC, wait time before receiving data.")]
		[DefaultValue(0)]
		public int SleepTime { get; set; } = 0;

		[Description("Socket的底层的心跳保活时间，毫秒单位，小于0则不设置。\r\nHeartbeat keepalive time of the underlying Socket, in milliseconds. If the value is smaller than 0, this parameter is not specified.")]
		[DefaultValue(-1)]
		public int SocketKeepAliveTime { get; set; } = -1;

		[Description("是否设置长连接操作，true表示长连接，反之则短连接。\r\nWhether to set the long connection operation: true indicates a long connection; otherwise, a short connection.")]
		[DefaultValue(true)]
		public bool IsPersistentConnection { get; set; } = true;

		[Description("绑定使用的本地IP地址，如果没有设置或是0.0.0.0，则表示任意可用的地址\r\nThe local IP address used by the binding, if not set or 0.0.0.0, represents any available address")]
		[DefaultValue("")]
		public string LocalIpAddress { get; set; } = "";

		[Description("本地使用的端口，-1表示自动\r\nPort used locally, -1 indicates automatic")]
		[DefaultValue(-1)]
		public int LocalPort { get; set; } = -1;

		[Description("当因为接收超时而关闭连接的时候，获取或设置超过指定次数的时候，是否需要重新连接，仅在同步访问下有效\r\nWhen the connection is closed due to a reception timeout, and when the number of retrieves or Settings exceeds the specified limit, is it necessary to reconnect")]
		[DefaultValue(1)]
		public int CloseOnRecvTimeOutTick { get; set; } = 1;

		[Description("在每条报文发送前，额外发送的数据内容，常用于lora，会追加四个字节的站号\r\nBefore each message is sent, the additional data content sent, often used in lora, is appended with a four-byte station")]
		[DefaultValue("")]
		public string SendBeforeHex { get; set; } = "";

		public SettingTcpIP()
		{
		}

		public SettingTcpIP(SettingTcpIP other)
		{
			SleepTime = other.SleepTime;
			SocketKeepAliveTime = other.SocketKeepAliveTime;
			IsPersistentConnection = other.IsPersistentConnection;
			LocalIpAddress = other.LocalIpAddress;
			LocalPort = other.LocalPort;
			SendBeforeHex = other.SendBeforeHex;
			CloseOnRecvTimeOutTick = other.CloseOnRecvTimeOutTick;
		}

		public SettingTcpIP(PipeTcpNet pipeTcpNet)
		{
			SleepTime = pipeTcpNet.SleepTime;
			SocketKeepAliveTime = pipeTcpNet.SocketKeepAliveTime;
			IsPersistentConnection = pipeTcpNet.IsPersistentConnection;
			CloseOnRecvTimeOutTick = pipeTcpNet.CloseOnRecvTimeOutTick;
			if (pipeTcpNet.LocalBinding != null)
			{
				LocalIpAddress = pipeTcpNet.LocalBinding.Address.ToString();
				LocalPort = pipeTcpNet.Port;
			}
		}

		public override string ToString()
		{
			return "Tcp/Udp Seting [网络管道额外参数配置]";
		}
	}
}
