using System.ComponentModel;

namespace HslCommunicationDemo.DemoControl
{
	public class SettingSerial
	{
		[Description("获取或设置在串行通信中是否启用请求发送信号\r\nGets or sets whether to enable request signaling in serial communication")]
		[DefaultValue(false)]
		public bool RtsEnable { get; set; } = false;

		[Description("获取或设置在串行通信中是否启用数据终端就绪信号\r\nObtain or set whether to enable the data terminal ready signal in serial communication")]
		[DefaultValue(false)]
		public bool DtrEnable { get; set; } = false;

		[Description("获取或设置当前的发送数据后到接收数据前的等待时间，默认20毫秒\r\nThe default waiting time between the time the data is sent and the time between receiving the data is 20 milliseconds")]
		[DefaultValue(20)]
		public int SleepTime { get; set; } = 20;

		[Description("获取或设置当前的发送数据后接收数据的超时时间，默认5000毫秒\r\nGet or set the timeout for receiving data after the current sent data. The default is 5000 milliseconds")]
		[DefaultValue(5000)]
		public int ReceiveTimeOut { get; set; } = 5000;

		[Description("在每条报文发送前，额外发送的数据内容，常用于lora，会追加四个字节的站号\r\nBefore each message is sent, the additional data content sent, often used in lora, is appended with a four-byte station")]
		[DefaultValue("")]
		public string SendBeforeHex { get; set; } = "";

		public SettingSerial()
		{
		}

		public SettingSerial(SettingSerial other)
		{
			RtsEnable = other.RtsEnable;
			DtrEnable = other.DtrEnable;
			SleepTime = other.SleepTime;
			SendBeforeHex = other.SendBeforeHex;
			ReceiveTimeOut = other.ReceiveTimeOut;
		}

		public override string ToString()
		{
			return "Serial Setting [串口额外参数配置]";
		}
	}
}
