using System.ComponentModel;

namespace HslCommunicationDemo.DemoControl
{
	public class SettingTcpDebug : SettingTcpIP
	{
		[Description("获取或设置客户端的key信息，用于调试服务器区分不同的客户端，这样就支持多个客户端连接到同一个服务器，默认是0\r\nGet or set the client's key information, used to distinguish different clients in the debug server, so that multiple clients can connect to the same server, the default is 0")]
		[DefaultValue(0)]
		public ushort ClientKey { get; set; } = 0;

		public SettingTcpDebug()
		{
		}

		public SettingTcpDebug(SettingTcpDebug other)
			: base(other)
		{
			ClientKey = other.ClientKey;
		}
	}
}
